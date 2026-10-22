import bpy,json,math
from pathlib import Path
root=Path(__file__).resolve().parent/'game-final'
results=json.loads((root/'unity-results.json').read_text())
checks=[]
for result in results:
 folder=Path(result['Folder'])
 if not result['Meshes']:
  path=next(folder.glob('*.png'));image=bpy.data.images.load(str(path));assert image.size[0]>0 and image.size[1]>0
  checks.append({'PNG':path.name,'Size':list(image.size)});continue
 bpy.ops.wm.open_mainfile(filepath=str(next(folder.glob('*.blend'))),use_scripts=False)
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert meshes and all(len(o.data.polygons)>0 for o in meshes)
 assert all(math.isfinite(v) for o in meshes for pt in o.bound_box for v in pt)
 count=sum(len(o.data.polygons) for o in meshes)
 checks.append({'Blend':folder.name,'Faces':count})
 for fmt,ext in [('GLB','glb'),('GLTF','gltf'),('FBX','fbx'),('OBJ','obj'),('STL','stl')]:
  if not (folder/fmt).exists():continue
  bpy.ops.wm.read_factory_settings(use_empty=True)
  path=str(next((folder/fmt).glob('*.'+ext)))
  assert len(path)<250,(len(path),path)
  if fmt in ['GLB','GLTF']:bpy.ops.import_scene.gltf(filepath=path)
  elif fmt=='FBX':bpy.ops.import_scene.fbx(filepath=path)
  elif fmt=='OBJ':bpy.ops.wm.obj_import(filepath=path)
  else:bpy.ops.wm.stl_import(filepath=path)
  meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert meshes
  triangles=sum(len(o.data.polygons) for o in meshes);assert triangles==count,(fmt,triangles,count)
  assert all(math.isfinite(v) for o in meshes for pt in o.bound_box for v in pt)
  checks.append({'Format':fmt,'Faces':triangles,'PathLength':len(path)})
(root/'verified.json').write_text(json.dumps(checks,indent=2));print('PASS',len(checks),'saved file checks')
