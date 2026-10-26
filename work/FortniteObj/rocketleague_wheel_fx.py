"""Source wheel effect attachments and a Blender reconstruction of the Zomba rim shader."""
import json,math
from pathlib import Path

def extract_wheel_effects(package,root,out,tools):
 from rocketleague_scene import extract_properties
 p=extract_properties(package,root,out/'wheel-data',tools,['Wheel*','Product*','Rim*','Particle*','Distribution*','Material*'])
 products=[o for o in p.objects.values() if o['Class']=='ProductAsset_Wheel_TA']
 def values(ref):return p.inherited((ref or {}).get('Index',0))
 corners=[]
 if products:
  generator=values(p.inherited(products[0]['Index']).get('Generator'))
  for ref in generator.get('Wheels',[]):
   settings=values(ref);attachments=[]
   for a in settings.get('Attachments',[]):
    component=values(a.get('Component'));system=values(component.get('Template'));emitters=[]
    for emitterref in system.get('Emitters',[]):
     emitter=values(emitterref)
     if not emitter.get('LODLevels') or emitter.get('bCookedOut'):continue
     lod=values(emitter['LODLevels'][0]);required=values(lod.get('RequiredModule'));material=required.get('Material') or {}
     if material.get('Name') not in ['Proto_09_Mat','Proto_09_MIC_Metallic']:continue
     modules=[p.objects[m['Index']] for m in lod.get('Modules',[]) if m and m['Index'] in p.objects and values(m).get('bEnabled') is not False]
     emitters.append({'Name':emitterref['Path'],'Material':material,'Modules':modules})
    if emitters:attachments.append({'Socket':a.get('Socket'),'Component':component,'Emitters':emitters})
   corners.append({'Mesh':(settings.get('SkeletalMesh') or {}).get('Name'),'Attachments':attachments})
 document={'Package':package,'Corners':corners,'Objects':p.document()['Objects']}
 (out/'wheel-effects.json').write_text(json.dumps(document,indent=2),encoding='utf-8')
 return document

def native_socket_matrix(path,name):
 from mathutils import Matrix,Quaternion,Vector
 doc=json.loads(Path(path).read_text());nodes=doc['nodes'];parents={child:i for i,n in enumerate(nodes) for child in n.get('children',[])}
 index=next((i for i,n in enumerate(nodes) if n.get('name')==name),None)
 if index is None:return None
 def matrix(i):
  n=nodes[i]
  if 'matrix' in n:
   flat=n['matrix'];m=Matrix([flat[k:k+4] for k in range(0,16,4)]).transposed()
  else:
   q=n.get('rotation',[0,0,0,1]);m=Matrix.LocRotScale(Vector(n.get('translation',[0,0,0])),Quaternion((q[3],q[0],q[1],q[2])),Vector(n.get('scale',[1,1,1])))
  return matrix(parents[i])@m if i in parents else m
 basis=Matrix.Rotation(math.pi/2,4,'X')
 return basis@matrix(index)@basis.inverted()

def build_wheel_faces(folder,anchor,corner,bpy,images,paint=None):
 from mathutils import Matrix,Vector
 from rocketleague_scene_blend import unreal_matrix
 path=folder/'wheel-effects.json'
 if not path.exists():return 0
 document=json.loads(path.read_text(encoding='utf-8'));corners=document['Corners']
 if corner>=len(corners):return 0
 settings=corners[corner];modelroot=folder/'wheels' if (folder/'wheels').exists() else folder/'resources'
 models=[p for p in modelroot.rglob('*.gltf') if p.stem==settings['Mesh']]
 pattern=images.get('twirl_rgb_t');ramp=images.get('curve_rainbow')
 if not models or not pattern or not ramp:return 0
 scene=bpy.context.scene
 if 'ZombaPulseCyclesPerSecond' not in scene:
  scene['ZombaPulseCyclesPerSecond']=1.0
  scene.id_properties_ui('ZombaPulseCyclesPerSecond').update(description='Editable reconstructed pulse speed; the compiled Unreal shader clock is not preserved.',min=0,max=20)
 materialname='Zomba animated rim'+(' painted' if paint else '')
 material=bpy.data.materials.get(materialname)
 if material is None:
  material=bpy.data.materials.new(materialname);material.use_nodes=True;nodes=material.node_tree.nodes;nodes.clear();links=material.node_tree.links
  output=nodes.new('ShaderNodeOutputMaterial');transparent=nodes.new('ShaderNodeBsdfTransparent');emission=nodes.new('ShaderNodeEmission');mix=nodes.new('ShaderNodeMixShader')
  texture=nodes.new('ShaderNodeTexImage');texture.image=bpy.data.images.load(str(pattern),check_existing=True);texture.image.colorspace_settings.name='Non-Color';texture.label='Actual Zomba packed twirl texture'
  separate=nodes.new('ShaderNodeSeparateColor');links.new(texture.outputs['Color'],separate.inputs[0])
  clock=nodes.new('ShaderNodeValue');clock.label='Reconstructed editable pulse clock';driver=clock.outputs[0].driver_add('default_value').driver
  for name,data_path in [('fps','render.fps'),('base','render.fps_base'),('speed','["ZombaPulseCyclesPerSecond"]')]:
   var=driver.variables.new();var.name=name;var.targets[0].id_type='SCENE';var.targets[0].id=scene;var.targets[0].data_path=data_path
  driver.expression='(frame-1)*base/fps*speed'
  def mathnode(operation,a,b=None):
   node=nodes.new('ShaderNodeMath');node.operation=operation
   for index,value in enumerate([a,b]):
    if value is None:continue
    if isinstance(value,(int,float)):node.inputs[index].default_value=value
    else:links.new(value,node.inputs[index])
   return node.outputs[0]
  total=None;mask=None
  for index,channel in enumerate(['Red','Green','Blue']):
   wave=mathnode('MULTIPLY',clock.outputs[0],math.tau);wave=mathnode('ADD',wave,index*math.tau/3);wave=mathnode('SINE',wave);wave=mathnode('MULTIPLY_ADD',wave,.5)
   # MULTIPLY_ADD's third socket is the +0.5 offset.
   wave.node.inputs[2].default_value=.5
   weighted=mathnode('MULTIPLY',separate.outputs[channel],wave);total=weighted if total is None else mathnode('ADD',total,weighted)
   mask=separate.outputs[channel] if mask is None else mathnode('MAXIMUM',mask,separate.outputs[channel])
  if paint:emission.inputs['Color'].default_value=paint
  else:
   coord=nodes.new('ShaderNodeCombineXYZ');links.new(mathnode('FRACT',total),coord.inputs['X']);coord.inputs['Y'].default_value=.5
   rainbow=nodes.new('ShaderNodeTexImage');rainbow.image=bpy.data.images.load(str(ramp),check_existing=True);rainbow.label='Actual Zomba colour ramp';links.new(coord.outputs[0],rainbow.inputs['Vector']);links.new(rainbow.outputs['Color'],emission.inputs['Color'])
  links.new(mathnode('ADD',total,.15),emission.inputs['Strength'])
  opacity=mathnode('MULTIPLY',mask,15);opacity.node.use_clamp=True;links.new(opacity,mix.inputs[0]);links.new(transparent.outputs[0],mix.inputs[1]);links.new(emission.outputs[0],mix.inputs[2]);links.new(mix.outputs[0],output.inputs['Surface'])
  material['SourceTexture']='Twirl_RGB_T';material['SourceColourRamp']='Curve_Rainbow';material['AnimatedWheelReconstruction']=True
 count=0
 for attachment in settings['Attachments']:
  socket=native_socket_matrix(models[0],attachment.get('Socket'))
  if socket is None:continue
  for emitter in attachment['Emitters']:
   modules={m['Class']:m['Properties'] for m in emitter['Modules']}
   size=modules.get('ParticleModuleSize',{}).get('StartSize',{}).get('LookupTable',[])
   location=modules.get('ParticleModuleLocation',{}).get('StartLocation',{}).get('LookupTable',[])
   if len(size)<3 or len(location)<5:continue
   diameter=size[2]*.01;position=Vector((location[2]*.01,-location[3]*.01,location[4]*.01))
   rotation=modules.get('ParticleModuleRotation',{}).get('StartRotation',{}).get('LookupTable',[])
   angle=(rotation[2] if len(rotation)>2 else 0)*math.tau
   vertices=[(0,0,0)]+[(math.cos(i*math.tau/64)*diameter/2,0,math.sin(i*math.tau/64)*diameter/2) for i in range(64)]
   mesh=bpy.data.meshes.new('Zomba rim face');mesh.from_pydata(vertices,[],[(0,i+1,(i+1)%64+1) for i in range(64)]);mesh.materials.append(material);uv=mesh.uv_layers.new()
   for loop in mesh.loops:
    v=vertices[loop.vertex_index];uv.data[loop.index].uv=(.5+(math.cos(angle)*v[0]-math.sin(angle)*v[2])/diameter,.5+(math.sin(angle)*v[0]+math.cos(angle)*v[2])/diameter)
   obj=bpy.data.objects.new('Zomba animated face '+str(corner+1)+'-'+str(count+1),mesh);scene.collection.objects.link(obj);obj.parent=anchor
   obj.matrix_local=socket@unreal_matrix(attachment['Component'])@Matrix.Translation(position)
   obj['SourceEmitter']=emitter['Name'];obj['SourceSocket']=attachment['Socket'];obj['AnimatedWheelFace']=True;count+=1
 return count
