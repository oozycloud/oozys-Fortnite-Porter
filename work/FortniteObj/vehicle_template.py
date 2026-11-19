"""Read authored vehicle UVs; never re-unwrap or alter the source model."""
import bpy,json,sys,math
from pathlib import Path
cfg=json.loads(Path(sys.argv[sys.argv.index('--')+1]).read_text(encoding='utf-8-sig'))
bpy.ops.wm.open_mainfile(filepath=cfg['Blend'],use_scripts=False)
groups={};warnings=[]
for obj in bpy.context.scene.objects:
 if obj.type!='MESH' or obj.hide_render or not len(obj.data.polygons):continue
 uv=next((u for u in obj.data.uv_layers if u.active_render),obj.data.uv_layers.active)
 if uv is None:warnings.append(obj.name+': no UV map; no usable paint template can be generated.');continue
 for face in obj.data.polygons:
  mat=obj.data.materials[face.material_index] if face.material_index<len(obj.data.materials) else None
  name=mat.name if mat else 'Unassigned material'
  points=[[float(uv.data[i].uv.x),float(uv.data[i].uv.y)] for i in face.loop_indices]
  if not all(math.isfinite(v) and abs(v)<10000 for p in points for v in p):continue
  key=(name,uv.name);group=groups.setdefault(key,{'Material':name,'UV':uv.name,'Faces':[]})
  group['Faces'].append({'Mesh':obj.name,'Points':points})
if not groups:warnings.append('No vehicle surfaces with usable UV coordinates were found; the model is retained without paint templates.')
Path(cfg['Result']).write_text(json.dumps({'Groups':list(groups.values()),'Warnings':warnings}),encoding='utf-8')
