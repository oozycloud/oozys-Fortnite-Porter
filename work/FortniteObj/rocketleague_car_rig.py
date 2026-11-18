"""Editable wheel controls on the car's existing deformation armature."""
import math,re
from mathutils import Vector,Matrix

CORNERS={'FL':r'^FL_(?:Disc|WheelTranslation)(?:_jnt)?$','FR':r'^FR_(?:Disc|WheelTranslation)(?:_jnt)?$','RL':r'^(?:BL|RL)_(?:Disc|WheelTranslation)(?:_jnt)?$','RR':r'^(?:BR|RR)_(?:Disc|WheelTranslation)(?:_jnt)?$'}
def mounts_for(rig):
 result={}
 for corner,pattern in CORNERS.items():
  candidates=[b for b in rig.data.bones if re.match(pattern,b.name,re.I)];candidates.sort(key=lambda b:('disc' not in b.name.lower(),b.name))
  if candidates:result[corner]=candidates[0].name
 return result

def build(bpy,warnings):
 candidates=[(o,mounts_for(o)) for o in bpy.context.scene.objects if o.type=='ARMATURE']
 candidate=next(((o,m) for o,m in candidates if len(m)==4),None)
 if candidate is None:warnings.append('Editable wheel rig: this car has no four named wheel mounts. Its original armature was retained.');return None
 rig,mounts=candidate;rig.hide_set(False);bpy.context.view_layer.objects.active=rig;rig.select_set(True)
 originals=[b.name for b in rig.data.bones];rest={c:rig.data.bones[name].matrix_local.copy() for c,name in mounts.items()}
 inverse=rig.matrix_world.to_3x3().inverted();up=(inverse@Vector((0,0,1))).normalized()
 side=(rest['FR'].translation-rest['FL'].translation).normalized()
 spacing=max((rest['FL'].translation-rest['FR'].translation).length,.1);size=spacing*.08
 ancestor=rig.data.bones[mounts['FL']]
 while ancestor.parent:ancestor=ancestor.parent
 parent_name=ancestor.name
 if bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
 bpy.ops.object.mode_set(mode='EDIT')
 names={}
 for corner,matrix in rest.items():
  point=matrix.translation;parent=rig.data.edit_bones[parent_name];names[corner]={}
  for suffix,axis in [('move',up),('steer',up),('roll',side),('follow',None)]:
   name='wheel_'+corner+'_'+suffix
   bone=rig.data.edit_bones.get(name) or rig.data.edit_bones.new(name);bone.head=point;bone.tail=point+(axis if axis is not None else matrix.col[1].xyz.normalized())*size;bone.parent=parent;bone.use_connect=False;bone.use_deform=False
   if suffix=='follow':bone.matrix=matrix;bone.length=max(rig.data.edit_bones[mounts[corner]].length,.001)
   parent=bone;names[corner][suffix]=name
  driven=rig.data.edit_bones[mounts[corner]];length=driven.length;matrix=driven.matrix.copy();driven.parent=parent;driven.matrix=matrix;driven.length=length
 bpy.ops.object.mode_set(mode='OBJECT')
 source=rig.data.collections.get('Source skeleton') or rig.data.collections.new('Source skeleton');controls=rig.data.collections.get('Wheel controls') or rig.data.collections.new('Wheel controls')
 for name in originals:
  bone=rig.data.bones[name]
  for collection in list(bone.collections):collection.unassign(bone)
  source.assign(bone)
 source.is_visible=False;controls.is_visible=True
 def widget(name,vertices,edges):
  mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,edges,[]);mesh.update();obj=bpy.data.objects.new(name,mesh);bpy.context.scene.collection.objects.link(obj);obj.hide_render=True;obj.hide_set(True);obj['PorterRigWidget']=True;return obj
 circle=widget('wheel_control_ring',[(math.cos(i*2*math.pi/32),0,math.sin(i*2*math.pi/32)) for i in range(32)],[(i,(i+1)%32) for i in range(32)])
 cube=widget('wheel_control_move',[(x,y,z) for x in [-.5,.5] for y in [-.5,.5] for z in [-.5,.5]],[(i,j) for i in range(8) for j in range(i+1,8) if bin(i^j).count('1')==1])
 for corner,parts in names.items():
  for suffix,name in parts.items():
   bone=rig.data.bones[name]
   for collection in list(bone.collections):collection.unassign(bone)
   (source if suffix=='follow' else controls).assign(bone)
   pose=rig.pose.bones[name];pose.rotation_mode='XYZ'
   if suffix!='follow':
    pose.custom_shape=cube if suffix=='move' else circle;pose.use_custom_shape_bone_size=False;pose.custom_shape_scale_xyz=(size,size,size);pose.color.palette='THEME04' if suffix=='move' else 'THEME03';pose.lock_scale=(True,True,True)
    pose.lock_location=(False,False,False) if suffix=='move' else (True,True,True);pose.lock_rotation=(True,True,True) if suffix=='move' else (True,False,True)
 # Parent every mounted wheel assembly to its corner's driven control, preserving its current rest transform.
 bpy.context.view_layer.update()
 for obj in list(bpy.context.scene.objects):
  mount=obj.get('RocketWheelMount')
  if not mount:continue
  corner=next((c for c,name in mounts.items() if name==mount),None)
  if not corner:continue
  world=obj.matrix_world.copy();obj.parent=rig;obj.parent_type='BONE';obj.parent_bone=names[corner]['follow'];bpy.context.view_layer.update();obj.matrix_world=world
 rig.name=str(bpy.context.scene.get('AssetName',rig.name))+' wheel rig' if bpy.context.scene.get('AssetName') else 'Car wheel rig';rig['PorterWheelRig']=True;rig['WheelControlNames']=__import__('json').dumps(names);rig.show_in_front=True
 text=bpy.data.texts.get('Wheel controls — read me') or bpy.data.texts.new('Wheel controls — read me');text.from_string('Select the car armature in the Outliner and enter Pose Mode. Open the Wheel controls bone collection.\nEach corner has: wheel_FL_move (G to position), wheel_FL_steer (R Y Y to steer on its local Y axis), and wheel_FL_roll (R Y Y to spin on its local Y axis).\nFR = front right, RL = rear left, RR = rear right. Source bones stay in the hidden Source skeleton collection.\nThe move controls carry the tyre, rim, animated wheel face and original wheel mount together. Clear transforms with Alt+G / Alt+R to restore the exported pose.\n')
 bpy.context.view_layer.update();return rig
