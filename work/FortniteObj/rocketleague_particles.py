"""Bake supported Cascade sprite emitters from their cooked distribution tables."""
import math,random,json
from mathutils import Vector
from rocketleague_scene_blend import unreal_matrix

def distribution(value,time=0,width=1,default=0,randoms=None):
 table=value.get('LookupTable',[]) if isinstance(value,dict) else []
 if not table:return [default]*width
 chunk=value.get('LookupTableChunkSize',width);rows=[table[i:i+chunk] for i in range(2,len(table),chunk)]
 rows=[r for r in rows if len(r)==chunk]
 if not rows:return [default]*width
 sample=max(0,min(len(rows)-1,(time-value.get('LookupTableStartTime',0))*value.get('LookupTableTimeScale',0)))
 a=int(sample);b=min(a+1,len(rows)-1);fraction=sample-a
 result=[rows[a][i]*(1-fraction)+rows[b][i]*fraction for i in range(chunk)]
 if chunk>=width*2:
  randoms=randoms or [.5]*width
  return [result[i]+(result[i+width]-result[i])*randoms[i] for i in range(width)]
 return result[:width]

def bake(document,folder,bpy,images,matfiles,warnings):
 objects={o['Index']:o for o in document['Objects']};rng=random.Random(1729);count=0;unsupported=set();fps=bpy.context.scene.render.fps
 def props(ref):return objects.get((ref or {}).get('Index'),{}).get('Properties',{})
 supported={'ParticleModuleLifetime','ParticleModuleSize','ParticleModuleColor','ParticleModuleColorScaleOverLife','ParticleModuleSizeMultiplyLife','ParticleModuleVelocity','ParticleModuleAcceleration','ParticleModuleLocationDirect','ParticleModuleLocation','ParticleModuleCameraOffset'}
 for component in document['ParticleComponents']:
  system=props(component['Template']);transform=unreal_matrix(component['Properties'])
  for emitterref in system.get('Emitters',[]):
   emitter=props(emitterref)
   if emitter.get('bCookedOut') or not emitter.get('LODLevels'):continue
   lod=props(emitter['LODLevels'][0]);required=props(lod.get('RequiredModule'));spawn=props(lod.get('SpawnModule'))
   if lod.get('bEnabled') is False:continue
   if lod.get('TypeDataModule'):
    unsupported.add(objects.get(lod['TypeDataModule']['Index'],{}).get('Class','Particle type data'));continue
   modules={}
   for ref in lod.get('Modules',[]):
    obj=objects.get(ref['Index'],{});values=obj.get('Properties',{})
    if values.get('bEnabled') is False:continue
    cls=obj.get('Class','Unknown')
    if cls not in supported:unsupported.add(cls)
    else:modules[cls]=values
   materialref=required.get('Material') or {};definition=matfiles.get(materialref.get('Name','').casefold())
   if not definition:unsupported.add('Sprite material '+materialref.get('Path','missing'));continue
   values=dict(line.split('=',1) for line in definition.read_text().splitlines() if '=' in line)
   image=images.get(values.get('Diffuse','').casefold())
   if not image:unsupported.add('Sprite texture '+materialref.get('Name','missing'));continue
   material=bpy.data.materials.new('Particle '+materialref['Name']);material.use_nodes=True;nodes=material.node_tree.nodes;nodes.clear();links=material.node_tree.links
   output=nodes.new('ShaderNodeOutputMaterial');transparent=nodes.new('ShaderNodeBsdfTransparent');emission=nodes.new('ShaderNodeEmission');mix=nodes.new('ShaderNodeMixShader');texture=nodes.new('ShaderNodeTexImage');texture.image=bpy.data.images.load(str(image),check_existing=True);info=nodes.new('ShaderNodeObjectInfo');colour=nodes.new('ShaderNodeMixRGB');colour.blend_type='MULTIPLY';colour.inputs[0].default_value=1;links.new(texture.outputs['Color'],colour.inputs[1]);links.new(info.outputs['Color'],colour.inputs[2]);links.new(colour.outputs[0],emission.inputs['Color']);alpha=nodes.new('ShaderNodeMath');alpha.operation='MULTIPLY';links.new(texture.outputs['Alpha'],alpha.inputs[0]);links.new(info.outputs['Alpha'],alpha.inputs[1]);links.new(alpha.outputs[0],mix.inputs[0]);links.new(transparent.outputs[0],mix.inputs[1]);links.new(emission.outputs[0],mix.inputs[2]);links.new(mix.outputs[0],output.inputs['Surface'])
   duration=required.get('EmitterDuration',1);loops=required.get('EmitterLoops',1)
   if loops==0:loops=1;unsupported.add('Infinite emitter loop (one cycle baked)')
   births=[]
   for loop in range(min(loops,8)):
    base=component['Delay']+required.get('EmitterDelay',0)+loop*duration
    for burst in spawn.get('BurstList',[]):
     births.extend([base+burst.get('Time',0)]*max(0,burst.get('Count',0)))
    rate=distribution(spawn.get('Rate',{}),default=0)[0]
    if rate>0:births.extend(base+i/rate for i in range(min(1000,int(rate*duration))))
   for birth in births:
    if count>=1000:unsupported.add('Particle budget exceeded (1,000 baked sprites)');break
    seed=[rng.random() for _ in range(3)]
    life=distribution(modules.get('ParticleModuleLifetime',{}).get('LifeTime',{}),birth,default=1,randoms=seed)[0]
    if life<=0:continue
    size=distribution(modules.get('ParticleModuleSize',{}).get('StartSize',{}),birth,3,1,seed)
    velocity=distribution(modules.get('ParticleModuleVelocity',{}).get('StartVelocity',{}),birth,3,0,seed)
    acceleration=distribution(modules.get('ParticleModuleAcceleration',{}).get('Acceleration',{}),birth,3,0,seed)
    start=distribution(modules.get('ParticleModuleLocation',{}).get('StartLocation',{}),birth,3,0,seed)
    startcolor=distribution(modules.get('ParticleModuleColor',{}).get('StartColor',{}),birth,3,1,seed)
    startalpha=distribution(modules.get('ParticleModuleColor',{}).get('StartAlpha',{}),birth,1,1,seed)[0]
    mesh=bpy.data.meshes.new('Cascade sprite');mesh.from_pydata([(-.5,-.5,0),(.5,-.5,0),(.5,.5,0),(-.5,.5,0)],[],[(0,1,2,3)]);mesh.materials.append(material);uv=mesh.uv_layers.new()
    for loop,coord in zip(uv.data,[(0,0),(1,0),(1,1),(0,1)]):loop.uv=coord
    obj=bpy.data.objects.new(component['Name']+' / '+emitter.get('EmitterName','Sprite'),mesh);bpy.context.scene.collection.objects.link(obj);obj['RocketParticle']=True;obj['SourceEmitter']=emitterref['Path'];obj['BornSeconds']=birth;obj['LifetimeSeconds']=life
    for hidden,frame in [(True,0),(False,1+birth*fps),(True,1+(birth+life)*fps)]:obj.hide_render=hidden;obj.keyframe_insert('hide_render',frame=frame)
    for step in range(17):
     age=step/16;seconds=age*life
     direct=modules.get('ParticleModuleLocationDirect',{}).get('Location')
     position=distribution(direct,seconds,3,0,seed) if direct else [start[i]+velocity[i]*seconds+.5*acceleration[i]*seconds*seconds for i in range(3)]
     obj.location=transform@Vector((position[0]*.01,-position[1]*.01,position[2]*.01))
     multiplier=distribution(modules.get('ParticleModuleSizeMultiplyLife',{}).get('LifeMultiplier',{}),age,3,1,seed);obj.scale=(size[0]*multiplier[0]*.01,size[1]*multiplier[1]*.01,1)
     factor=distribution(modules.get('ParticleModuleColorScaleOverLife',{}).get('ColorScaleOverLife',{}),age,3,1,seed);alpha=distribution(modules.get('ParticleModuleColorScaleOverLife',{}).get('AlphaScaleOverLife',{}),age,1,1,seed)[0];obj.color=tuple(startcolor[i]*factor[i] for i in range(3))+(max(0,min(1,startalpha*alpha)),)
     frame=1+(birth+seconds)*fps
     for prop in ['location','scale','color']:obj.keyframe_insert(prop,frame=frame)
    count+=1;bpy.context.scene.frame_end=max(bpy.context.scene.frame_end,math.ceil((birth+life)*fps)+1)
 if unsupported:warnings.append('Particle features not reconstructed: '+', '.join(sorted(unsupported)))
 return count
