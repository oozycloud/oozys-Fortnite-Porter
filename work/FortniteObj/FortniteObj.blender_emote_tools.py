bl_info = {'name':"oozy's Emote Tools",'author':'oozycloud','version':(0,0,1),'blender':(4,4,0),'location':'3D View > Sidebar > Porter','description':'Apply a separately exported emote to the selected skin','category':'Animation'}

import bpy, json, math
from bpy.props import StringProperty
from bpy_extras.io_utils import ImportHelper
from mathutils import Matrix, Quaternion, Vector, Euler
from .blender_animation import apply_animation, BASIS, DISPLAY


def curves(action):
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                yield from bag.fcurves


def unreal_transform(matrix, root):
    m = BASIS @ matrix @ DISPLAY.inverted() @ BASIS if root else BASIS @ DISPLAY @ matrix @ DISPLAY.inverted() @ BASIS
    p,q,s=m.decompose()
    return [p.x*100,p.y*100,p.z*100,q.x,q.y,q.z,q.w,s.x,s.y,s.z]


def legacy_payload(source, actions, fps):
    """Recover local poses from older emote files that lack packed source data."""
    bones=[]
    names={b.name:i for i,b in enumerate(source.data.bones)}
    rests={}
    for b in source.data.bones:
        rest=b.parent.matrix_local.inverted() @ b.matrix_local if b.parent else b.matrix_local.copy()
        rests[b.name]=rest
        t=unreal_transform(rest,b.parent is None)
        name=b.get('SourceName',b.name)
        bones.append({'Name':name,'Parent':names[b.parent.name] if b.parent else -1,'Translation':t[:3],'Rotation':t[3:7],'Scale':t[7:10],'TranslationMode':'Animation' if b.parent is None else 'Animation' if name.casefold().startswith(('ik_', 'attach')) else 'OrientAndScale'})
    clips=[]; source.animation_data_create()
    for action in actions:
        if not any(c.data_path.startswith('pose.bones[') for c in curves(action)):continue
        channels={(c.data_path,c.array_index):c for c in curves(action)}
        start,end=action.frame_range
        count=max(2,math.ceil(end-start)+1)
        frames=[min(end,start+i) for i in range(count)]
        tracks=[{'Bone':b['Name'],'Frames':[]} for b in bones]
        def sample(bone,path,default,frame):
            prefix=bone.path_from_id(path)
            return [channels[(prefix,i)].evaluate(frame) if (prefix,i) in channels else value for i,value in enumerate(default)]
        for frame in frames:
            for index,pb in enumerate(source.pose.bones):
                position=Vector(sample(pb,'location',(0,0,0),frame))
                scale=Vector(sample(pb,'scale',(1,1,1),frame))
                if pb.rotation_mode=='QUATERNION':rotation=Quaternion(sample(pb,'rotation_quaternion',(1,0,0,0),frame))
                elif pb.rotation_mode=='AXIS_ANGLE':
                    angle,*axis=sample(pb,'rotation_axis_angle',(0,0,1,0),frame)
                    rotation=Quaternion(Vector(axis),angle)
                else:rotation=Euler(sample(pb,'rotation_euler',(0,0,0),frame),pb.rotation_mode).to_quaternion()
                values=unreal_transform(rests[pb.name] @ Matrix.LocRotScale(position,rotation,scale),pb.parent is None)
                tracks[index]['Frames'].append(values)
        clips.append({'Name':action.name,'Bones':bones,'Tracks':tracks,'Frames':count,'Fps':fps,'FrameTimes':[(f-start)/fps for f in frames],'Duration':(end-start)/fps})
    if not clips:raise ValueError('This file has no skeletal emote actions.')
    return {'Bones':bones,'Clips':clips}


def import_emote(filepath, target):
    if not target or target.type!='ARMATURE':raise ValueError('Select the exported skin or its armature first.')
    scene=bpy.context.scene
    original_animation=target.animation_data
    previous_action=original_animation.action if original_animation else None
    previous_slot=original_animation.action_slot if original_animation else None
    previous_tracks=[(t,t.mute) for t in original_animation.nla_tracks] if original_animation else []
    previous_time=(scene.frame_start,scene.frame_end,scene.render.fps,scene.render.fps_base,scene.frame_current,scene.frame_subframe)
    previous_pose={b.name:(b.matrix_basis.copy(),b.rotation_mode) for b in target.pose.bones}
    previous_rig_type=target.get('Rig type')
    original_actions=set(bpy.data.actions)
    orphan_types=('collections','meshes','armatures','materials','images','sounds')
    original_data={kind:set(getattr(bpy.data,kind)) for kind in orphan_types}
    # Load text as data only. Never execute scripts supplied by an emote file.
    with bpy.data.libraries.load(filepath,link=False) as (available,loaded):
        modern='Porter Emote.json' in available.texts
        if modern:loaded.texts=['Porter Emote.json']
        else:
            loaded.objects=list(available.objects)
            loaded.actions=list(available.actions)
            loaded.scenes=list(available.scenes)
    try:
        if modern:
            data=json.loads(loaded.texts[0].as_string())
        else:
            armatures=[o for o in loaded.objects if o and o.type=='ARMATURE']
            if len(armatures)!=1:raise ValueError('The older emote file must contain one animation armature.')
            source=armatures[0]
            temporary=bpy.data.collections.new('Porter import temporary');bpy.context.scene.collection.children.link(temporary)
            temporary.objects.link(source)
            source_scene=next((s for s in loaded.scenes if s),None)
            fps=source_scene.render.fps/source_scene.render.fps_base if source_scene else 30
            data=legacy_payload(source,[a for a in loaded.actions if a],fps)
        if not isinstance(data,dict) or not data.get('Clips') or not data.get('Bones'):
            raise ValueError('No usable emote data was found.')
        target_names={b.get('SourceName',b.name).casefold() for b in target.data.bones}
        shared=target_names & {b['Name'].casefold() for b in data['Bones']}
        if len(shared)<min(10,len(data['Bones'])):raise ValueError('This emote does not match the selected skin skeleton.')
        # Source NLA can override the new action; keep tracks for undo but mute.
        if target.animation_data:
            for track in target.animation_data.nla_tracks:track.mute=True
        result=apply_animation(target,data)
        for obj in bpy.data.objects:
            if obj.find_armature()!=target:continue
            for modifier in obj.modifiers:
                if modifier.type=='CLOTH':
                    modifier.point_cache.frame_start=bpy.context.scene.frame_start
                    modifier.point_cache.frame_end=bpy.context.scene.frame_end
        bpy.context.scene.frame_set(1)
        return result
    except Exception:
        if original_animation:
            target.animation_data.action=previous_action
            if previous_slot:target.animation_data.action_slot=previous_slot
        elif target.animation_data:target.animation_data_clear()
        for track,muted in previous_tracks:track.mute=muted
        for bone in target.pose.bones:
            bone.matrix_basis,bone.rotation_mode=previous_pose[bone.name]
        scene.frame_start,scene.frame_end,scene.render.fps,scene.render.fps_base=previous_time[:4]
        scene.frame_set(previous_time[4],subframe=previous_time[5])
        if previous_rig_type is None:
            if 'Rig type' in target:del target['Rig type']
        else:target['Rig type']=previous_rig_type
        imported_actions=set(loaded.actions) if not modern else set()
        for action in set(bpy.data.actions)-original_actions-imported_actions:bpy.data.actions.remove(action)
        raise
    finally:
        if modern:
            for text in loaded.texts:
                if text:bpy.data.texts.remove(text)
        else:
            for obj in loaded.objects:
                if obj:bpy.data.objects.remove(obj,do_unlink=True)
            for scene in loaded.scenes:
                if scene:bpy.data.scenes.remove(scene)
            for action in loaded.actions:
                if action:bpy.data.actions.remove(action)
            if 'temporary' in locals():bpy.data.collections.remove(temporary)
            # Clean only datablocks brought in by this import, never user data.
            for _ in range(3):
                for kind in orphan_types:
                    blocks=getattr(bpy.data,kind)
                    for block in set(blocks)-original_data[kind]:
                        if block.users==0:blocks.remove(block)


class PORTER_OT_apply_emote(bpy.types.Operator,ImportHelper):
    bl_idname='porter.apply_emote'
    bl_label='Apply exported emote'
    bl_options={'REGISTER','UNDO'}
    filename_ext='.blend'
    filter_glob:StringProperty(default='*.blend',options={'HIDDEN'})
    @classmethod
    def poll(cls,context):
        obj=context.object
        return obj is not None and (obj.type=='ARMATURE' or obj.find_armature() is not None)
    def execute(self,context):
        obj=context.object;target=obj if obj.type=='ARMATURE' else obj.find_armature()
        try:
            report=import_emote(self.filepath,target)
            self.report({'INFO'},f"Applied {len(report)} emote action(s) to {target.name}. Press Space to play.")
            return {'FINISHED'}
        except Exception as ex:
            self.report({'ERROR'},str(ex));return {'CANCELLED'}


class PORTER_PT_emotes(bpy.types.Panel):
    bl_label='Emotes';bl_idname='PORTER_PT_emotes';bl_space_type='VIEW_3D';bl_region_type='UI';bl_category='Porter'
    def draw(self,context):
        layout=self.layout
        layout.label(text='1. Select your skin or its rig.')
        layout.label(text='2. Choose the exported emote file.')
        layout.operator('porter.apply_emote',icon='ACTION')
        obj=context.object
        rig=obj if obj and obj.type=='ARMATURE' else obj.find_armature() if obj else None
        if rig and rig.animation_data:
            layout.template_ID(rig.animation_data,'action')
        layout.label(text='Press Space to play. No parenting needed.')


classes=(PORTER_OT_apply_emote,PORTER_PT_emotes)
def register():
    for cls in classes:bpy.utils.register_class(cls)
def unregister():
    for cls in reversed(classes):bpy.utils.unregister_class(cls)
