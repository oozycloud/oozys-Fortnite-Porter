"""Actual Games Manager material + emote regression, run inside Blender."""
import bpy,json,sys
from pathlib import Path
scene=bpy.context.scene
for fragment,texture in [('Body.MI_','T_TimberStakePurse_Body_D'),('FaceAcc.MI_','T_TimberStakePurse_FaceAcc_D')]:
 material=next(m for m in bpy.data.materials if fragment in m.get('SourceMaterial',''))
 shader=next(n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
 image=shader.inputs['Base Color'].links[0].from_node.image
 assert texture in image.name and image.packed_file
 assert 'Weave' not in image.name
 metal=shader.inputs['Metallic'].links[0].from_node.image
 assert sum(list(metal.pixels)[0::4])/len(list(metal.pixels)[0::4])<.2,metal.name
 assert all(n.image.packed_file for n in material.node_tree.nodes if n.type=='TEX_IMAGE')
rig=next(o for o in scene.objects if o.type=='ARMATURE' and o.animation_data and o.animation_data.action)
action=rig.animation_data.action
assert 'Camouflage' in action.name,action.name
scene.frame_set(1);a=[tuple(v for row in b.matrix for v in row) for b in rig.pose.bones]
scene.frame_set(60);b=[tuple(v for row in b.matrix for v in row) for b in rig.pose.bones]
assert a!=b,'Emote does not move the skin rig'
print('PASS separate packed body/mask maps, nonmetal cloth and active moving Dancin Domino action')
