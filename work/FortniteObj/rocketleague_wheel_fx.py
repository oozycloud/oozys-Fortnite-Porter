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
 material=build_zomba_material(bpy,pattern,ramp,paint)

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


def build_zomba_material(bpy,pattern,ramp,paint):
 """Port of Proto_09_Mat's SM5 pixel shader and uniform expressions.

 UV rotations: +time and -2*time radians. LUT pans: -.2 and -.5 units/s.
 Twirl channels R/B and G/A are sampled separately; alpha is not max(RGB).
 Source disassembly/provenance is documented in docs/zomba-shader.md.
 """
 scene=bpy.context.scene
 if 'ZombaTimeScale' not in scene:
  scene['ZombaTimeScale']=1.0
  scene.id_properties_ui('ZombaTimeScale').update(description='1 = recovered game shader timing; 0 = pause.',min=0,max=20)
 name='Zomba source shader'+(' painted' if paint else '')
 material=bpy.data.materials.get(name)
 if material:return material
 material=bpy.data.materials.new(name);material.use_nodes=True
 nodes=material.node_tree.nodes;nodes.clear();links=material.node_tree.links
 def mathnode(op,*args):
  n=nodes.new('ShaderNodeMath');n.operation=op
  for i,a in enumerate(args):
   if isinstance(a,(int,float)):n.inputs[i].default_value=a
   else:links.new(a,n.inputs[i])
  return n.outputs[0]
 def combine(x,y,z=0):
  n=nodes.new('ShaderNodeCombineXYZ')
  for i,a in enumerate((x,y,z)):
   if isinstance(a,(int,float)):n.inputs[i].default_value=a
   else:links.new(a,n.inputs[i])
  return n.outputs[0]
 def split(value):
  n=nodes.new('ShaderNodeSeparateXYZ');links.new(value,n.inputs[0]);return list(n.outputs)
 def clamp(a):return mathnode('MINIMUM',mathnode('MAXIMUM',a,0),1)
 def sample(path,uv,label,noncolor=False):
  n=nodes.new('ShaderNodeTexImage');n.image=bpy.data.images.load(str(path),check_existing=True);n.label=label;n.extension='REPEAT';n.image.alpha_mode='CHANNEL_PACKED'
  n.image.colorspace_settings.name='Non-Color' if noncolor else 'sRGB'
  links.new(uv,n.inputs['Vector']);channels=nodes.new('ShaderNodeSeparateColor');links.new(n.outputs['Color'],channels.inputs[0]);return list(channels.outputs)[:3]+[n.outputs['Alpha']]
 clock=nodes.new('ShaderNodeValue');clock.label='Game shader seconds';driver=clock.outputs[0].driver_add('default_value').driver
 for name,path in [('fps','render.fps'),('base','render.fps_base'),('speed','["ZombaTimeScale"]')]:
  var=driver.variables.new();var.name=name;var.targets[0].id_type='SCENE';var.targets[0].id=scene;var.targets[0].data_path=path
 driver.expression='(frame-1)*base/fps*speed';time=clock.outputs[0]
 uv=nodes.new('ShaderNodeTexCoord');xy=split(uv.outputs['UV']);x=mathnode('SUBTRACT',xy[0],.5);y=mathnode('SUBTRACT',xy[1],.5)
 # glTF/Blender V is flipped relative to the original Unreal particle UV.
 y=mathnode('MULTIPLY',y,-1)
 radius=mathnode('ADD',mathnode('MULTIPLY',x,x),mathnode('MULTIPLY',y,y))
 def rotated(rate):
  angle=mathnode('MULTIPLY',time,rate);c=mathnode('COSINE',angle);sn=mathnode('SINE',angle)
  u=mathnode('ADD',mathnode('SUBTRACT',mathnode('MULTIPLY',c,x),mathnode('MULTIPLY',sn,y)),.5)
  v=mathnode('ADD',mathnode('ADD',mathnode('MULTIPLY',sn,x),mathnode('MULTIPLY',c,y)),.5)
  return combine(u,v)
 a=sample(pattern,rotated(1),'Twirl R/B: +1 radian per second')
 b=sample(pattern,rotated(-2),'Twirl G/A: -2 radians per second')
 r2=mathnode('MULTIPLY',mathnode('MINIMUM',radius,1),2)
 def lut(rate):return sample(ramp,combine(mathnode('ADD',r2,mathnode('FRACT',mathnode('MULTIPLY',time,rate))),r2),'Source colour/pulse LUT')
 pulse=lut(-.2);hue=lut(-.5)
 edge=mathnode('MAXIMUM',mathnode('MULTIPLY',mathnode('SUBTRACT',.22,radius),3),0)
 summed=mathnode('ADD',a[0],b[1])
 amp=clamp(mathnode('MULTIPLY',mathnode('SUBTRACT',mathnode('MULTIPLY',summed,mathnode('MULTIPLY',a[2],b[3])),.1),10))
 patternlight=clamp(mathnode('MULTIPLY',mathnode('MULTIPLY',summed,mathnode('ADD',mathnode('ADD',pulse[0],pulse[1]),pulse[2])),.8))
 flash=mathnode('MULTIPLY',amp,pulse[0]);boost=mathnode('MULTIPLY',flash,6)
 strength=mathnode('ADD',mathnode('MULTIPLY',mathnode('MULTIPLY',mathnode('ADD',mathnode('ADD',patternlight,boost),.3),mathnode('POWER',edge,.2)),2),boost)
 alpha=clamp(mathnode('MULTIPLY',mathnode('MULTIPLY',mathnode('ADD',flash,edge),edge),15))
 # The game's Hue_Shift function rotates RGB around the neutral-grey axis.
 # Painted particles set HueShift_Sharpness=0, retaining the chosen colour.
 colour=paint or (1,.1,.1,1)
 if paint:rgb=combine(*colour[:3])
 else:
  angle=mathnode('MULTIPLY',mathnode('MULTIPLY',hue[2],r2),math.tau);c=mathnode('COSINE',angle);sn=mathnode('SINE',angle);mean=sum(colour[:3])/3
  channels=[]
  for i in range(3):
   cross=(colour[(i+2)%3]-colour[(i+1)%3])/math.sqrt(3)
   channels.append(mathnode('ADD',mathnode('ADD',mathnode('MULTIPLY',c,colour[i]),mathnode('MULTIPLY',mathnode('SUBTRACT',1,c),mean)),mathnode('MULTIPLY',sn,cross)))
  rgb=combine(*channels)
 emission=nodes.new('ShaderNodeEmission');links.new(rgb,emission.inputs['Color']);links.new(strength,emission.inputs['Strength'])
 transparent=nodes.new('ShaderNodeBsdfTransparent');mix=nodes.new('ShaderNodeMixShader');links.new(alpha,mix.inputs[0]);links.new(transparent.outputs[0],mix.inputs[1]);links.new(emission.outputs[0],mix.inputs[2]);output=nodes.new('ShaderNodeOutputMaterial');links.new(mix.outputs[0],output.inputs['Surface'])
 material['SourceTexture']='Twirl_RGB_T';material['SourceColourRamp']='Curve_Rainbow';material['SourceShaderGuid']='f989e447-37b1-4565-88bd-03224d48e241';material['SourceShaderPort']=True;material['AnimationRatesRadians']=[1.0,-2.0];material['LutPanRates']=[-.2,-.5]
 return material
