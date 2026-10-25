"""Preserve named cooked material parameters, including animated UberSkin inputs."""
from rocketleague_scene import extract_properties
import json

def export_shader(package,root,out,tools):
 p=extract_properties(package,root,out/'shader-data',tools,['Material*'])
 defaults={}
 for o in p.objects.values():
  props=o['Properties']
  if 'ParameterName' in props and 'DefaultValue' in props:defaults[props['ParameterName']]=props['DefaultValue']
 candidates=[o for o in p.objects.values() if o['Class']=='MaterialInstanceConstant']
 candidates.sort(key=lambda o:sum(len(o['Properties'].get(k,[])) for k in ['ScalarParameterValues','VectorParameterValues','TextureParameterValues']),reverse=True)
 parameters=defaults.copy();material=None
 if candidates:
  material=candidates[0]['Name']
  for key in ['ScalarParameterValues','VectorParameterValues','TextureParameterValues']:
   for param in candidates[0]['Properties'].get(key,[]):
    if isinstance(param,dict) and 'ParameterName' in param:parameters[param['ParameterName']]=param.get('ParameterValue')
 result={'Material':material,'Parameters':parameters,'Objects':p.document()['Objects']}
 (out/'decal-shader.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
 return result

def animated_mask(mat,images,folder,bpy):
 path=folder/'decal-shader.json'
 if not path.exists():return None
 params=json.loads(path.read_text(encoding='utf-8'))['Parameters'];nodes=mat.node_tree.nodes;links=mat.node_tree.links
 outputs=[]
 for name in ['FX*Noise1','FX*Noise2']:
  source=params.get(name);transform=params.get(name+'Transform')
  if not isinstance(source,dict) or not isinstance(transform,dict):continue
  image=images.get(source.get('Name','').casefold())
  if not image:continue
  uv=nodes.new('ShaderNodeTexCoord');mapping=nodes.new('ShaderNodeMapping');mapping.label=name+' authored scale and pan';links.new(uv.outputs['UV'],mapping.inputs['Vector'])
  mapping.inputs['Scale'].default_value=(transform.get('R',1),transform.get('G',1),1)
  for axis,key in enumerate(['B','A']):
   speed=transform.get(key,0)
   if speed:
    driver=mapping.inputs['Location'].driver_add('default_value',axis).driver
    for varname,prop in [('fps','render.fps'),('base','render.fps_base')]:
     var=driver.variables.new();var.name=varname;var.targets[0].id_type='SCENE';var.targets[0].id=bpy.context.scene;var.targets[0].data_path=prop
    # Blender flips texture V on glTF import, so V panning changes sign too.
    driver.expression=f'(frame-1)*base/fps*{speed if axis==0 else -speed!r}'
  texture=nodes.new('ShaderNodeTexImage');texture.image=bpy.data.images.load(str(image),check_existing=True);texture.image.colorspace_settings.name='Non-Color';texture.label=name;links.new(mapping.outputs['Vector'],texture.inputs['Vector'])
  # Packed noise channels are data, not sRGB diffuse colour. The green channel
  # contains the fire pattern; a luminance conversion suppresses it almost entirely.
  channel=nodes.new('ShaderNodeSeparateColor');links.new(texture.outputs['Color'],channel.inputs[0]);outputs.append(channel.outputs['Green'])
 if not outputs:return None
 signal=outputs[0]
 if len(outputs)>1:
  mix=nodes.new('ShaderNodeMath');mix.operation='ADD';mix.use_clamp=True;links.new(signal,mix.inputs[0]);links.new(outputs[1],mix.inputs[1]);signal=mix.outputs[0]
 ramp=nodes.new('ShaderNodeMapRange');ramp.clamp=True
 position=params.get('DecalBlendRampPosition',.5);width=max(.001,params.get('DecalBlendRampWidth',.5))
 ramp.inputs['From Min'].default_value=position-width/2;ramp.inputs['From Max'].default_value=position+width/2;links.new(signal,ramp.inputs['Value'])
 mat['RocketAnimatedShader']='UberSkin noise pan';mat['RocketShaderParameters']=json.dumps(params)
 return ramp.outputs[0]
