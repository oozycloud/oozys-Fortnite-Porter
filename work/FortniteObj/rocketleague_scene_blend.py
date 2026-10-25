"""Blender-side UE3 placement conversion, matching UEViewer glTF's coordinate basis."""
import math
from mathutils import Matrix

def unreal_matrix(props,actor=False):
 location=props.get('Location' if actor else 'Translation',{})
 rotation=props.get('Rotation',{});unit=math.tau/65536
 p,y,r=[rotation.get(k,0)*unit for k in ['Pitch','Yaw','Roll']]
 cp,sp,cy,sy,cr,sr=math.cos(p),math.sin(p),math.cos(y),math.sin(y),math.cos(r),math.sin(r)
 m=Matrix(((cp*cy,sr*sp*cy-cr*sy,-cr*sp*cy-sr*sy,0),(cp*sy,sr*sp*sy+cr*cy,cy*sr-cr*sp*sy,0),(sp,-sr*cp,cr*cp,0),(0,0,0,1)))
 scale=props.get('DrawScale' if actor else 'Scale',1)
 axis=props.get('DrawScale3D' if actor else 'Scale3D',{})
 m=m@Matrix.Diagonal([scale*axis.get(k,1) for k in ['X','Y','Z']]+[1])
 for i,k in enumerate(['X','Y','Z']):m[i][3]=location.get(k,0)*.01
 # UEViewer swaps Y/Z for glTF; Blender's glTF importer then rotates Y-up to Z-up.
 basis=Matrix.Diagonal((1,-1,1,1))
 return basis@m@basis

def assemble(document,folder,bpy,warnings):
 templates={};placed=[]
 for entry in document['Instances']:
  model=entry['Model']
  if model not in templates:
   before=set(bpy.data.objects)
   bpy.ops.import_scene.gltf(filepath=str(folder/model))
   objects=list(set(bpy.data.objects)-before)
   templates[model]=[(o,o.matrix_world.copy()) for o in objects if o.type=='MESH']
   for o in objects:
    if o.type!='MESH':bpy.data.objects.remove(o,do_unlink=True)
  transform=unreal_matrix(entry.get('Actor',{}),True)@unreal_matrix(entry['Properties'])
  for template,local in templates[model]:
   obj=template.copy();obj.data=template.data;obj.parent=None;bpy.context.scene.collection.objects.link(obj)
   obj.matrix_world=transform@local;obj.name=entry['Name'];obj['RocketLeaguePlacement']=entry['Name'];obj['SourceMesh']=entry['Mesh']
   obj['RocketMaterialOverrides']=__import__('json').dumps(entry['Materials'])
   if entry['Properties'].get('HiddenGame') or entry['Properties'].get('bHiddenInGame'):obj.hide_render=True
   placed.append(obj)
 for objects in templates.values():
  for obj,_ in objects:bpy.data.objects.remove(obj,do_unlink=True)
 # Material overrides are assigned after all mesh imports have registered their materials.
 for obj in placed:
  overrides=__import__('json').loads(obj['RocketMaterialOverrides'])
  if overrides:
   for i,name in enumerate(overrides):
    if not name:continue
    material=bpy.data.materials.get(name)
    if material is None:
     material=bpy.data.materials.new(name);material.use_nodes=True
    while len(obj.material_slots)<=i:obj.data.materials.append(None)
    obj.material_slots[i].link='OBJECT';obj.material_slots[i].material=material
 return placed
