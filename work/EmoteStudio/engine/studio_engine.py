"""Emote Studio's isolated Blender worker. Source .blend scripts are never enabled."""
import bpy,json,sys,os,math,traceback,re,copy,faulthandler,gzip,base64,textwrap
faulthandler.enable()
from pathlib import Path
from mathutils import Matrix,Quaternion,Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
from porter_emote_tools import legacy_payload,unreal_transform
from porter_emote_tools.blender_animation import apply_animation,local_matrix

def progress(message,value):print('STUDIO_PROGRESS '+json.dumps({'message':message,'progress':value}),flush=True)
def load(path):bpy.ops.wm.open_mainfile(filepath=str(Path(path).resolve()),use_scripts=False)
def object_mode():
 if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
def pack():
 warnings=[]
 for image in bpy.data.images:
  if image.packed_file or image.packed_files:continue
  if image.source=='FILE' and not image.has_data:
   try:image.reload()
   except Exception:pass
  if image.source=='FILE' and image.has_data:
   try:image.pack()
   except Exception as e:warnings.append('Could not pack '+image.name+': '+str(e))
  elif image.source=='FILE' and not image.has_data:warnings.append('Missing texture: '+image.name)
 return warnings

def rigs():
 return [{'name':r.name,'bones':len(r.data.bones),'meshes':sum(o.type=='MESH' and o.find_armature()==r for o in bpy.context.scene.objects)} for r in bpy.context.scene.objects if r.type=='ARMATURE']
def target_rig(name=None):
 candidates=rigs()
 if not candidates:raise ValueError('This model has no armature. Import a rigged .blend model to add emotes.')
 choice=next((x for x in candidates if x['name']==name),None) if name else max(candidates,key=lambda r:(r['meshes'],r['bones']))
 if not choice:raise ValueError('The selected armature is no longer in this model.')
 return bpy.data.objects[choice['name']]
def model_info(path,rig=None):
 allrigs=rigs();visible=[o for o in bpy.context.scene.objects if o.type=='MESH' and not o.hide_render]
 return {'name':Path(path).stem,'path':str(path),'rig':rig.name if rig else '', 'rigs':allrigs,'meshCount':len(visible),'textureCount':sum(bool(i.has_data or i.packed_file or i.packed_files) for i in bpy.data.images if i.type=='IMAGE'),'bones':[b.name for b in rig.data.bones] if rig else []}

def read_emote(path):
 path=Path(path)
 if path.suffix.lower()=='.json':
  data=json.loads(path.read_text(encoding='utf-8-sig'))
 else:
  with bpy.data.libraries.load(str(path),link=False) as (available,loaded):
   modern='Porter Emote.json' in available.texts
   if modern:loaded.texts=['Porter Emote.json']
   else:loaded.objects=list(available.objects);loaded.actions=list(available.actions);loaded.scenes=list(available.scenes)
  if modern:data=json.loads(loaded.texts[0].as_string())
  else:
   candidates=[o for o in loaded.objects if o and o.type=='ARMATURE']
   if len(candidates)!=1:raise ValueError(path.name+': choose an emote file with one armature.')
   scene=next((s for s in loaded.scenes if s),None);fps=scene.render.fps/scene.render.fps_base if scene else 30
   data=legacy_payload(candidates[0],[a for a in loaded.actions if a],fps)
 if not isinstance(data,dict) or not data.get('Clips') or not data.get('Bones'):raise ValueError(path.name+': no skeletal emote data found.')
 return data

def canonical(name):
 n=re.sub(r'\.\d{3}$','',name).casefold()
 for prefix in ['mixamorig:','mixamorig','armature|','bip001_','bip01_','def-']:
  if n.startswith(prefix):n=n[len(prefix):]
 n=re.sub('[^a-z0-9]','',n)
 aliases={'hips':'pelvis','hip':'pelvis','spine':'spine01','spine1':'spine02','spine2':'spine03','neck':'neck01','leftshoulder':'claviclel','rightshoulder':'clavicler','leftarm':'upperarml','rightarm':'upperarmr','leftforearm':'lowerarml','rightforearm':'lowerarmr','lefthand':'handl','righthand':'handr','leftupleg':'thighl','rightupleg':'thighr','leftleg':'calfl','rightleg':'calfr','leftfoot':'footl','rightfoot':'footr','lefttoebase':'balll','righttoebase':'ballr'}
 if n in aliases:return aliases[n]
 for side,letter in [('left','l'),('right','r')]:
  for source,dest in [('thumb','thumb'),('index','index'),('middle','middle'),('ring','ring'),('pinky','pinky')]:
   match=re.fullmatch(side+'hand'+source+'([123])',n)
   if match:return dest+'0'+match[1]+letter
 return n

def mapping_for(rig,data,custom):
 source={b['Name'] for b in data['Bones']};candidates={}
 for b in rig.data.bones:
  for name in [b.get('SourceName',b.name),b.name]:candidates.setdefault(canonical(name),[]).append(b.name)
 result={}
 for name in source:
  if name in custom:
   if custom[name] in rig.data.bones:result[name]=custom[name]
  elif canonical(name) in candidates:result[name]=candidates[canonical(name)][0]
 return result

def adapt_foreign(data,rig,mapping):
 """Transfer humanoid rest-relative rotations; retain the custom rig's proportions."""
 out=copy.deepcopy(data);targetbones=[];indices={b.name:i for i,b in enumerate(rig.data.bones)}
 target_rest={}
 for bone in rig.data.bones:
  local=bone.parent.matrix_local.inverted()@bone.matrix_local if bone.parent else bone.matrix_local.copy();target_rest[bone.name]=local
  v=unreal_transform(local,bone.parent is None)
  targetbones.append({'Name':bone.name,'Parent':indices[bone.parent.name] if bone.parent else -1,'Translation':v[:3],'Rotation':v[3:7],'Scale':v[7:10],'TranslationMode':'Animation'})
 for clip in out['Clips']:
  bones=clip.get('Bones',data['Bones']);source={b['Name']:b for b in bones};global_rest={};local_rest={}
  def world(index):
   b=bones[index];name=b['Name']
   if name not in global_rest:
    local=local_matrix(b['Translation'],b['Rotation'],b['Scale'],b['Parent']<0);local_rest[name]=local
    global_rest[name]=world(b['Parent'])@local if b['Parent']>=0 else local
   return global_rest[name]
  for i in range(len(bones)):world(i)
  # Scale locomotion/bounce to the custom model's height; never scale its mesh.
  src_height=max((m.translation.z for m in global_rest.values()),default=1)-min((m.translation.z for m in global_rest.values()),default=0)
  dst_height=max((b.head_local.z for b in rig.data.bones),default=1)-min((b.head_local.z for b in rig.data.bones),default=0)
  ratio=dst_height/src_height if src_height>1e-5 else 1
  tracks=[]
  for track in clip['Tracks']:
   name=track['Bone'];target=mapping.get(name)
   if not target or name not in source:continue
   sb=source[name];tb=rig.data.bones[target];srest=local_rest[name];sworld=global_rest[name].to_quaternion();tworld=(rig.matrix_world@tb.matrix_local).to_quaternion();tlocal=target_rest[target];tp,tq,ts=tlocal.decompose();sp,sq,ss=srest.decompose()
   parent_source=global_rest[bones[sb['Parent']]['Name']].to_quaternion() if sb['Parent']>=0 else Quaternion()
   parent_target=(rig.matrix_world@(tb.parent.matrix_local if tb.parent else Matrix.Identity(4))).to_quaternion()
   values=[]
   for v in track['Frames']:
    anim=local_matrix(v[:3],v[3:7],v[7:10],sb['Parent']<0);ap,aq,asc=anim.decompose()
    delta=sworld@(sq.conjugated()@aq)@sworld.conjugated();desired=tq@(tworld.conjugated()@delta@tworld)
    offset=parent_target.conjugated()@(parent_source@(ap-sp))*ratio
    # Most limb translation is skeleton-authored; only root/hips need bounce/traversal.
    position=tp+offset if canonical(name) in {'root','pelvis'} else tp
    scale=Vector(tuple(ts[i]*(asc[i]/ss[i] if abs(ss[i])>1e-6 else 1) for i in range(3)))
    values.append(unreal_transform(Matrix.LocRotScale(position,desired,scale),tb.parent is None))
   tracks.append({'Bone':target,'Frames':values})
  clip['Bones']=targetbones;clip['Tracks']=tracks
 out['Bones']=targetbones
 for b in rig.data.bones:b['SourceName']=b.name
 return out

def selected_action(cfg,manifest):
 entries=manifest.get('actions',[]);id=cfg.get('selectedEmote');index=int(cfg.get('clipIndex',0))
 item=next((a for a in entries if a['emoteId']==id and a['clipIndex']==index),None)
 rig=target_rig(cfg.get('rig') or manifest.get('rig'))
 if item:
  action=bpy.data.actions[item['action']];rig.animation_data_create();rig.animation_data.action=action
  if action.slots:rig.animation_data.action_slot=action.slots[0]
  scene=bpy.context.scene;fps=float(item['fps']);scene.render.fps=max(1,round(fps));scene.render.fps_base=scene.render.fps/fps
  scene.frame_start=1;scene.frame_end=max(1,int(item['frames']));scene.frame_set(1)
 if item and bpy.data.texts.get('Emote Studio sources.json.gz.b64'):
  sources=json.loads(gzip.decompress(base64.b64decode(bpy.data.texts['Emote Studio sources.json.gz.b64'].as_string())))
  if item['emoteId'] in sources:
   payload=copy.deepcopy(sources[item['emoteId']]);payload['Clips']=[payload['Clips'][item['clipIndex']]]
   text=bpy.data.texts.get('Porter Emote.json') or bpy.data.texts.new('Porter Emote.json');text.from_string(json.dumps(payload,indent=1))
 return rig,item

def export_preview(output,rig,item):
 progress('Preparing the 3D preview…',.8);object_mode();scene=bpy.context.scene
 bpy.ops.object.select_all(action='DESELECT')
 objects=[o for o in scene.objects if o.type=='MESH' and not o.hide_render and (o.find_armature()==rig or o.parent==rig)]
 if not objects:objects=[o for o in scene.objects if o.type=='MESH' and not o.hide_render]
 for o in objects+[rig]:o.hide_set(False);o.select_set(True)
 bpy.context.view_layer.objects.active=rig
 for o in objects:
  for mod in list(o.modifiers):
   if mod.type in {'CLOTH','COLLISION'}:o.modifiers.remove(mod)
 for i in bpy.data.images:
  if (i.packed_file or i.packed_files) and not i.has_data:
   try:_=i.pixels[0]
   except Exception:pass
  if i.has_data and max(i.size)>1024:
   factor=1024/max(i.size);i.scale(max(1,round(i.size[0]*factor)),max(1,round(i.size[1]*factor)))
 bpy.ops.export_scene.gltf(filepath=output,export_format='GLB',use_selection=True,export_animations=bool(item),export_animation_mode='ACTIVE_ACTIONS',export_frame_range=False,export_anim_slide_to_zero=True,export_extras=True,export_cameras=False,export_lights=False,export_apply=False)

def run(cfg):
 action=cfg['action'];warnings=[]
 if action=='inspectModel':
  progress('Reading model and rig…',.1);load(cfg['source']);object_mode();rig=target_rig(cfg.get('rig'));info=model_info(cfg['source'],rig)
  for r in [o for o in bpy.context.scene.objects if o.type=='ARMATURE']:
   if r.animation_data:
    r.animation_data.action=None
    for track in r.animation_data.nla_tracks:track.mute=True
   for pb in r.pose.bones:pb.matrix_basis=Matrix.Identity(4)
  warnings+=pack();bpy.context.scene.frame_set(1);bpy.ops.wm.save_as_mainfile(filepath=cfg['baseOutput']);export_preview(cfg['previewOutput'],rig,None)
  return {'model':info,'warnings':warnings}
 if action=='inspectEmote':
  progress('Reading emote clips…',.2);data=read_emote(cfg['source'])
  clips=[{'name':c['Name'],'frames':c.get('LoopFrames') or (round(c.get('Duration',(c['Frames']-1)/c['Fps'])*c['Fps']) if c.get('Loop') else c['Frames']),'fps':c['Fps'],'duration':c.get('Duration',(c['Frames']-1)/c['Fps']),'loop':bool(c.get('Loop'))} for c in data['Clips']]
  Path(cfg['payloadOutput']).write_text(json.dumps(data),encoding='utf-8')
  return {'name':data.get('Name',Path(cfg['source']).stem),'clips':clips,'sourceBones':[b['Name'] for b in data['Bones']]}
 if action=='compose':
  load(cfg['base']);object_mode();rig=target_rig(cfg.get('rig'));base_actions=set(bpy.data.actions);actions=[];mapping_needed=False;source_bones=[];map_result={};source_payloads={}
  original_names={b.name:b.get('SourceName') for b in rig.data.bones}
  for i,emote in enumerate(cfg.get('emotes',[])):
   progress('Applying '+emote['name']+'…',.1+.55*i/max(1,len(cfg['emotes'])));data=json.loads(Path(emote['payload']).read_text(encoding='utf-8'));source_bones=list(dict.fromkeys(source_bones+[b['Name'] for b in data['Bones']]))
   source_payloads[emote['id']]=copy.deepcopy(data)
   mapping=mapping_for(rig,data,cfg.get('boneMap') or {});map_result.update(mapping)
   required={b['Name'] for b in data['Bones'] if canonical(b['Name']) in {'pelvis','head','spine01','upperarml','upperarmr','thighl','thighr'}}
   if len(mapping)<min(10,len(data['Bones'])) or not required.issubset(mapping):
    mapping_needed=True;warnings.append(emote['name']+': the rig needs a bone map before this emote can be applied.');continue
   for bone in rig.data.bones:
    if original_names[bone.name] is None:
     if 'SourceName' in bone:del bone['SourceName']
    else:bone['SourceName']=original_names[bone.name]
   porter=all(original_names.get(t) is not None for t in mapping.values())
   if porter:
    for source,target in mapping.items():rig.data.bones[target]['SourceName']=source
   else:
    data=adapt_foreign(data,rig,mapping);warnings.append(emote['name']+': humanoid rest-pose retargeting was used. Check the preview for this custom rig.')
   if rig.animation_data:
    for t in rig.animation_data.nla_tracks:t.mute=True
   report=apply_animation(rig,data)
   for index,r in enumerate(report):
    a=bpy.data.actions[r['Name']];a.use_fake_user=True
    actions.append({'emoteId':emote['id'],'clipIndex':index,'action':a.name,'name':emote['clips'][index]['name'],'frames':r.get('PlaybackFrames',r['Frames']),'fps':r['Fps'],'duration':r['Duration'],'loop':r.get('Loop',False)})
  progress('Keeping imported animation actions…',.68)
  manifest={'rig':rig.name,'actions':actions,'mappingNeeded':mapping_needed,'sourceBones':source_bones,'boneMap':map_result,'warnings':warnings,'targetBones':[b.name for b in rig.data.bones]}
  text=bpy.data.texts.get('Emote Studio.json') or bpy.data.texts.new('Emote Studio.json');text.clear();text.write(json.dumps(manifest))
  progress('Storing emote source data…',.7)
  source_text=bpy.data.texts.get('Emote Studio sources.json.gz.b64') or bpy.data.texts.new('Emote Studio sources.json.gz.b64');source_text.from_string('\n'.join(textwrap.wrap(base64.b64encode(gzip.compress(json.dumps(source_payloads).encode(),compresslevel=1)).decode(),120)))
  progress('Selecting the active emote…',.72)
  selected_action(cfg,manifest);progress('Packing project textures…',.74);warnings+=pack();progress('Saving the working model…',.76);bpy.ops.wm.save_as_mainfile(filepath=cfg['blendOutput'])
  return manifest
 if action in {'preview','export'}:
  load(cfg['blend']);manifest=json.loads(bpy.data.texts['Emote Studio.json'].as_string()) if bpy.data.texts.get('Emote Studio.json') else {'rig':cfg.get('rig'),'actions':[]};rig,item=selected_action(cfg,manifest)
  if action=='preview':export_preview(cfg['output'],rig,item);return {'action':item,'warnings':manifest.get('warnings',[]),'targetBones':[b.name for b in rig.data.bones]}
  progress('Saving '+cfg['format']+'…',.6);object_mode();kind=cfg['format'];output=cfg['output'];warnings+=pack()
  if kind=='BLEND':bpy.ops.wm.save_as_mainfile(filepath=output)
  else:
   bpy.ops.object.select_all(action='DESELECT')
   for o in bpy.context.scene.objects:
    if o.type in {'MESH','ARMATURE'} and not o.hide_render:o.hide_set(False);o.select_set(True)
   bpy.context.view_layer.objects.active=rig
   if kind=='GLB':bpy.ops.export_scene.gltf(filepath=output,export_format='GLB',use_selection=True,export_animations=bool(item),export_animation_mode='ACTIVE_ACTIONS',export_frame_range=False,export_anim_slide_to_zero=True,export_extras=True,export_cameras=False,export_lights=False)
   elif kind=='FBX':
    if item:bpy.context.scene.frame_end=math.ceil(rig.animation_data.action.frame_range[1])
    for image in bpy.data.images:
     if image.packed_file and image.has_data:
      image.filepath_raw=str(Path(output).parent/('studio-texture-'+str(list(bpy.data.images).index(image))+'.png'));image.file_format='PNG';image.save()
    bpy.ops.export_scene.fbx(filepath=output,use_selection=True,object_types={'MESH','ARMATURE'},add_leaf_bones=False,bake_anim=bool(item),bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,path_mode='COPY',embed_textures=True)
   else:raise ValueError('Unsupported export format.')
  return {'output':output,'warnings':warnings,'action':item}
 raise ValueError('Unknown worker action.')

cfg=json.loads(Path(sys.argv[sys.argv.index('--')+1]).read_text(encoding='utf-8-sig'))
try:
 result=run(cfg);Path(cfg['result']).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');progress('Ready',1)
except Exception as error:
 Path(cfg['result']).write_text(json.dumps({'error':str(error),'trace':traceback.format_exc()}),encoding='utf-8');traceback.print_exc();sys.exit(1)
