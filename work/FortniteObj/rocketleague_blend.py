"""Build an editable packed Blender scene from extracted UE3 glTF meshes and material references."""
import bpy,json,re,sys,math
from pathlib import Path
from mathutils import Matrix,Vector
q=json.loads(Path(sys.argv[sys.argv.index('--')+1]).read_text(encoding='utf-8-sig'));folder=Path(q['Output']);asset=q['Asset'];warnings=[]
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
resources=folder/'resources';images={p.stem.casefold():p for p in resources.rglob('*.png')};images.update({p.stem.casefold():p for p in (folder/'decal').rglob('*.png')})
matfiles={p.stem.casefold():p for p in resources.rglob('*.mat')}
images.update({p.stem.casefold():p for p in (folder/'wheels').rglob('*.png')});matfiles.update({p.stem.casefold():p for p in (folder/'wheels').rglob('*.mat')})
appearance=q.get('Appearance') or {}
paints={'Crimson':'#B7162D','Lime':'#A2D928','Black':'#15171B','Sky Blue':'#63CBF2','Cobalt':'#264DD9','Burnt Sienna':'#7B3D27','Forest Green':'#208448','Purple':'#7C3DB8','Pink':'#E881B5','Orange':'#F17E24','Grey':'#8A8B90','Titanium White':'#ECEEF0','Saffron':'#E1CB29','Gold':'#D6AC49'}
def colour(text):
 text=text.lstrip('#');srgb=[int(text[i:i+2],16)/255 for i in (0,2,4)];return tuple((v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4) for v in srgb)+(1,)
primary=colour(appearance.get('Primary','#195CB8'));secondary=colour(appearance.get('Secondary','#EF8124'))
properties=(folder/'decal-properties.txt').read_text(encoding='utf-8',errors='replace') if (folder/'decal-properties.txt').exists() else ''
# Read actual authored parameter names instead of treating a decal mask as a diffuse image.
skin=re.search(r"ParameterValue = Texture2D'[^']*\.([^'.]+)'\s+ParameterName = Skin\b",properties)
skinfile=images.get(skin[1].casefold()) if skin else None
models=list(resources.rglob('*.gltf'))
if asset['Id']=='RL23':models=[p for p in models if p.stem=='Body_Octane_SK']
elif '|' in asset['ObjectPath']:models=[p for p in models if p.stem==asset['ObjectPath'].split('|')[1]]
if asset['Category']=='Cars':
 skeletal=[p for p in models if 'SkeletalMesh' in p.parts[-2]]
 if skeletal:models=skeletal
models.sort()
for p in models:
 try:bpy.ops.import_scene.gltf(filepath=str(p))
 except Exception as ex:warnings.append(p.name+': '+str(ex))
wheelobjects=[]
if q.get('Wheel'):
 before=set(bpy.data.objects)
 for path in sorted((folder/'wheels').rglob('*.gltf')):
  bpy.ops.import_scene.gltf(filepath=str(path))
 wheelobjects=list(set(bpy.data.objects)-before)
 rigs=[o for o in bpy.data.objects if o.type=='ARMATURE' and o not in wheelobjects]
 rig=rigs[0] if rigs else None
 mounts=[b for b in rig.data.bones if re.search(r'(FL|FR|BL|BR|RL|RR).*Disc',b.name,re.I)] if rig else []
 if len(mounts)>=4:
  roots=[o for o in wheelobjects if o.parent not in wheelobjects]
  for i,bone in enumerate(mounts[:4]):
   anchor=bpy.data.objects.new('Wheel '+bone.name,None);bpy.context.scene.collection.objects.link(anchor)
   anchor.matrix_world=rig.matrix_world@Matrix.Translation(bone.head_local)@Matrix.Rotation(math.pi if bone.head_local.y>0 else 0,4,'Z')
   if i==0: copies={o:o for o in wheelobjects}
   else:
    copies={o:o.copy() for o in wheelobjects}
    for original,clone in copies.items():bpy.context.scene.collection.objects.link(clone)
   for original,clone in copies.items():
    local=original.matrix_local.copy() if original.parent in wheelobjects else original.matrix_world.copy()
    # Original objects have already moved on later iterations; retain their authored local transform.
    if i==0:original['RocketWheelSourceMatrix']=[v for row in local for v in row]
    elif original.parent not in wheelobjects:
     flat=original['RocketWheelSourceMatrix'];local=Matrix([flat[k:k+4] for k in range(0,16,4)])
    clone.parent=copies.get(original.parent,anchor);clone.matrix_parent_inverse=Matrix.Identity(4);clone.matrix_basis=local
 else:warnings.append('Wheel mounting bones were not found. The selected wheel remains a separate mesh at its source origin.')
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
 normalname=values.get('Normal','')
 normal=tex(normalname,True) if not re.search('carbonfiber|detail|fur_|brushed|sparkle',normalname,re.I) else None
 if normal:
  nm=nodes.new('ShaderNodeNormalMap');links.new(normal.outputs['Color'],nm.inputs['Color']);links.new(nm.outputs[0],shader.inputs['Normal'])
 shader.inputs['Roughness'].default_value=.42
 isbody=asset['Category']=='Cars' and re.search('body|paint',mat.name,re.I) and not re.search('chassis',mat.name,re.I)
 if isbody:
  mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MIX';mix.inputs[0].default_value=0;mix.inputs[1].default_value=primary;mix.inputs[2].default_value=secondary
  mask=None
  if skinfile:
   skintex=tex(skinfile.stem,True);mask=skintex.outputs['Alpha'];links.new(mask,mix.inputs[0]);mat['RocketLeagueDecal']=q['Decal']['Name'];mat['DecalMask']=skinfile.name
  if diffuse:
   shaded=nodes.new('ShaderNodeMixRGB');shaded.blend_type='MULTIPLY';shaded.inputs[0].default_value=1;links.new(diffuse.outputs['Color'],shaded.inputs[1]);links.new(mix.outputs[0],shaded.inputs[2]);links.new(shaded.outputs[0],shader.inputs['Base Color'])
  else:links.new(mix.outputs[0],shader.inputs['Base Color'])
  finishes={'Glossy':(.23,.05),'Matte':(.83,0),'Metallic':(.27,.7),'Pearlescent':(.22,.22),'Anodized':(.2,.85),'Anodized Pearl':(.2,.85),'Brushed Metal':(.38,.85),'Carbon Fiber':(.35,.5)}
  first=appearance.get('PrimaryFinish','Glossy');second=appearance.get('SecondaryFinish','Glossy');r1,m1=finishes.get(first,finishes['Glossy']);r2,m2=finishes.get(second,finishes['Glossy'])
  for slot,a,b in [('Roughness',r1,r2),('Metallic',m1,m2)]:
   if mask:
    value=nodes.new('ShaderNodeMapRange');value.inputs['From Min'].default_value=0;value.inputs['From Max'].default_value=1;value.inputs['To Min'].default_value=a;value.inputs['To Max'].default_value=b;links.new(mask,value.inputs['Value']);links.new(value.outputs[0],shader.inputs[slot])
   else:shader.inputs[slot].default_value=a
  if 'Pearl' in first or first=='Pearlescent':shader.inputs['Iridescence Weight'].default_value=.6;shader.inputs['Iridescence Thickness'].default_value=400
  if first in ['Carbon Fiber','Brushed Metal']:
   coords=nodes.new('ShaderNodeTexCoord');noise=nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=150 if first=='Carbon Fiber' else 350;links.new(coords.outputs['Generated'],noise.inputs['Vector']);bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.08;bump.inputs['Distance'].default_value=.001;links.new(noise.outputs['Fac'],bump.inputs['Height']);links.new(bump.outputs[0],shader.inputs['Normal'])
  mat['PrimaryColour']=appearance.get('Primary','#195CB8');mat['DecalColour']=appearance.get('Secondary','#EF8124');mat['PrimaryFinish']=first;mat['DecalFinish']=second
 # Standard wheel shaders use separate UV tiles for tyre and rim.
 wheelvalues=list(values.values());rimname=next((v for v in wheelvalues if re.search(r'Rim_D$',v,re.I)),None);tirename=next((v for v in wheelvalues if re.search(r'Tire_D$',v,re.I)),None)
 if rimname and tirename:
  rim=tex(rimname);tire=tex(tirename);coords=nodes.new('ShaderNodeTexCoord');splituv=nodes.new('ShaderNodeSeparateXYZ');links.new(coords.outputs['UV'],splituv.inputs[0]);is_tire=nodes.new('ShaderNodeMath');is_tire.operation='GREATER_THAN';is_tire.inputs[1].default_value=1;links.new(splituv.outputs['Y'],is_tire.inputs[0]);surface=nodes.new('ShaderNodeMixRGB');links.new(is_tire.outputs[0],surface.inputs[0]);links.new(rim.outputs['Color'],surface.inputs[1]);links.new(tire.outputs['Color'],surface.inputs[2]);links.new(surface.outputs[0],shader.inputs['Base Color'])
  paint=appearance.get('WheelPaint','Default') if asset['Category']=='Cars' else appearance.get('BodyPaint','Default')
  if paint in paints:
   tinted=nodes.new('ShaderNodeMixRGB');tinted.blend_type='MIX';tinted.inputs[0].default_value=.8;tinted.inputs[2].default_value=colour(paints[paint]);links.new(rim.outputs['Color'],tinted.inputs[1]);links.new(tinted.outputs[0],surface.inputs[1]);mat['PaintVariant']=paint
 if re.search('chassis',mat.name,re.I) and appearance.get('BodyPaint') in paints:
  maskname=next((v for v in wheelvalues if re.search('parts_rgb',v,re.I)),None)
  if maskname and diffuse:
   masktex=tex(maskname,True);channels=nodes.new('ShaderNodeSeparateColor');links.new(masktex.outputs['Color'],channels.inputs[0]);tint=nodes.new('ShaderNodeMixRGB');tint.blend_type='MULTIPLY';links.new(channels.outputs['Blue'],tint.inputs[0]);links.new(diffuse.outputs['Color'],tint.inputs[1]);tint.inputs[2].default_value=colour(paints[appearance['BodyPaint']]);links.new(tint.outputs[0],shader.inputs['Base Color']);mat['PaintVariant']=appearance['BodyPaint']
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
report=json.loads(Path(q['Result']).read_text());report['Meshes']=len(meshobjects);report['Warnings']+=warnings;report['DecalMaskApplied']=bool(skinfile);report['Appearance']=appearance;report['MountedWheels']=4 if q.get('Wheel') and len(mounts)>=4 else 0;Path(q['Result']).write_text(json.dumps(report,indent=2))

if q.get('Preview'):
 scene=bpy.context.scene;visible=[o for o in scene.objects if o.type=='MESH' and not o.hide_render and o.name!='Icosphere'];corners=[o.matrix_world@Vector(c) for o in visible for c in o.bound_box]
 lo=Vector([min(v[i] for v in corners) for i in range(3)]);hi=Vector([max(v[i] for v in corners) for i in range(3)]);center=(lo+hi)/2;size=max(max(hi-lo),.1)
 bpy.ops.object.camera_add(location=center+Vector((1,-1,.65)).normalized()*size*1.8);camera=bpy.context.object;camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.lens=45;scene.camera=camera
 bpy.ops.object.light_add(type='SUN');bpy.context.object.data.energy=3;bpy.context.object.rotation_euler=(.45,-.6,-.3)
 scene.world.color=(.18,.18,.18);scene.render.engine='CYCLES';scene.cycles.samples=16;scene.render.resolution_x=640;scene.render.resolution_y=480;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.render.filepath=str(folder/'preview.png');bpy.ops.render.render(write_still=True)
