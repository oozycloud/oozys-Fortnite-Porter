import bpy,json
from pathlib import Path
anchors=[o for o in bpy.data.objects if o.name.startswith('Wheel ') and o.type=='EMPTY']
assert len(anchors)==4
assert not any(o.name.startswith('Icosphere') for o in bpy.data.objects)
assert sum(o.type=='MESH' and not o.get('AnimatedWheelFace') for o in bpy.context.scene.objects)==5
assert len({tuple(round(x,5) for x in o.matrix_world.translation) for o in anchors})==4
assert all(o.hide_get() for o in bpy.context.scene.objects if o.type in {'ARMATURE','EMPTY'})
for o in bpy.data.objects:
 if o.type=='MESH' and o.name.startswith('Wheel_'):
  assert all(m.object==o.parent for m in o.modifiers if m.type=='ARMATURE')
mat=bpy.data.materials['Body_Oramac_MIC'];assert mat['RocketLeagueLivery']=='ncrcy_stripes_D.png'
shader=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');assert shader.inputs['Base Color'].links[0].from_node.image.name=='ncrcy_stripes_D.png'
assert bpy.data.images['ncrcy_stripes_D.png'].packed_file
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':assert area.spaces.active.shading.type=='MATERIAL';assert not area.spaces.active.overlay.show_extras;assert not area.spaces.active.overlay.show_floor
Path('recovery/rocketleague/camaro-fix-validation.json').write_text(json.dumps({'DistinctWheelMounts':4,'ClonedArmaturesBoundCorrectly':True,'PackedNASCARLivery':True,'HiddenRigWidgets':True,'MaterialPreview':True},indent=2))
print('PASS four corners, per-wheel armatures, packed livery and clean material preview')
