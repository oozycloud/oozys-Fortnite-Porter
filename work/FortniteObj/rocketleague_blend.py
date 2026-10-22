"""Build an editable packed Blender scene from extracted UE3 glTF meshes and material references."""
import bpy,json,re,sys,math
from pathlib import Path
q=json.loads(Path(sys.argv[sys.argv.index('--')+1]).read_text(encoding='utf-8-sig'));folder=Path(q['Output']);asset=q['Asset'];warnings=[]
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
resources=folder/'resources';images={p.stem.casefold():p for p in resources.rglob('*.png')};images.update({p.stem.casefold():p for p in (folder/'decal').rglob('*.png')})
matfiles={p.stem.casefold():p for p in resources.rglob('*.mat')}
properties=(folder/'decal-properties.txt').read_text(encoding='utf-8',errors='replace') if (folder/'decal-properties.txt').exists() else ''
# Read actual authored parameter names instead of treating a decal mask as a diffuse image.
skin=re.search(r"ParameterValue = Texture2D'[^']*\.([^'.]+)'\s+ParameterName = Skin\b",properties)
skinfile=images.get(skin[1].casefold()) if skin else None
models=list(resources.rglob('*.gltf'))
if '|' in asset['ObjectPath']:models=[p for p in models if p.stem==asset['ObjectPath'].split('|')[1]]
if asset['Category']=='Cars':
 skeletal=[p for p in models if 'SkeletalMesh' in p.parts[-2]]
 if skeletal:models=skeletal
models.sort()
for p in models:
 try:bpy.ops.import_scene.gltf(filepath=str(p))
 except Exception as ex:warnings.append(p.name+': '+str(ex))
meshobjects=[o for o in bpy.data.objects if o.type=='MESH' and len(o.data.polygons)]
if not meshobjects:raise RuntimeError('No mesh could be imported. The extracted source resources remain available.')
for mat in list(bpy.data.materials):
 # glTF importer may suffix material names when a package contains multiple meshes.
 key=re.sub(r'\.\d{3}$','',mat.name).casefold();definition=matfiles.get(key)
 if not definition:continue
 values=dict(line.split('=',1) for line in definition.read_text().splitlines() if '=' in line)
 mat.use_nodes=True;nodes=mat.node_tree.nodes;links=mat.node_tree.links;shader=next((n for n in nodes if n.type=='BSDF_PRINCIPLED'),None)
 if not shader:continue
 def tex(name,noncolor=False):
  p=images.get(name.casefold())
  if not p:return None
  n=nodes.new('ShaderNodeTexImage');n.image=bpy.data.images.load(str(p),check_existing=True);n.label=name
  if noncolor:n.image.colorspace_settings.name='Non-Color'
  return n
 diffuse=tex(values.get('Diffuse',''))
 if diffuse:links.new(diffuse.outputs['Color'],shader.inputs['Base Color'])
 normal=tex(values.get('Normal',''),True)
 if normal:
  nm=nodes.new('ShaderNodeNormalMap');links.new(normal.outputs['Color'],nm.inputs['Color']);links.new(nm.outputs[0],shader.inputs['Normal'])
 shader.inputs['Roughness'].default_value=.42
 if skinfile and re.search('body|paint',mat.name,re.I) and not re.search('chassis',mat.name,re.I):
  skintex=tex(skinfile.stem,True);split=nodes.new('ShaderNodeSeparateColor');links.new(skintex.outputs['Color'],split.inputs[0])
  mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MIX';mix.inputs[1].default_value=(.025,.12,.42,1);mix.inputs[2].default_value=(.95,.3,.035,1);links.new(split.outputs['Red'],mix.inputs[0])
  if diffuse:
   shaded=nodes.new('ShaderNodeMixRGB');shaded.blend_type='MULTIPLY';shaded.inputs[0].default_value=1;links.new(diffuse.outputs['Color'],shaded.inputs[1]);links.new(mix.outputs[0],shaded.inputs[2]);links.new(shaded.outputs[0],shader.inputs['Base Color'])
  else:links.new(mix.outputs[0],shader.inputs['Base Color'])
  mat['RocketLeagueDecal']=q['Decal']['Name'];mat['DecalMask']=skinfile.name
 # Preserve unmapped images in an editable node group on the material.
 for value in values.values():
  if value.casefold() in images and value not in [values.get('Diffuse'),values.get('Normal')]:tex(value)
if q.get('Decal'):
 warnings.append('Decal paint/shader reconstruction is simplified; source textures and parameter properties are retained.')
 if not skinfile:warnings.append('This decal uses a custom shader without a supported Skin mask. Its source resources are included; appearance is not reproduced.')
for img in bpy.data.images:
 if img.source=='FILE':
  try:img.pack()
  except Exception as ex:warnings.append('Could not pack '+img.name+': '+str(ex))
for o in bpy.data.objects:o['SourceGame']='Rocket League'
bpy.context.scene.unit_settings.system='METRIC'
bpy.context.scene['AssetId']=asset['Id'];bpy.context.scene['AssetName']=asset['Name'];bpy.context.scene['Category']=asset['Category'];bpy.context.scene['ExportLimits']='Unreal gameplay, level placement and particle simulation are not reproduced.'
bpy.ops.wm.save_as_mainfile(filepath=str(folder/(q['Name']+'.blend')))
report=json.loads(Path(q['Result']).read_text());report['Meshes']=len(meshobjects);report['Warnings']+=warnings;report['DecalMaskApplied']=bool(skinfile);Path(q['Result']).write_text(json.dumps(report,indent=2))
