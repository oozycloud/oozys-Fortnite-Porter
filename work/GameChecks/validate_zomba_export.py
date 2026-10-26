"""Run in Blender with an exported Zomba wheel or car + Zomba scene already open."""
import bpy,json,math
from pathlib import Path
scene=bpy.context.scene
faces=[o for o in scene.objects if o.get('AnimatedWheelFace')]
expected=2 if scene.get('Category')=='Wheels' else 8
assert len(faces)==expected,len(faces)
assert len({o.parent for o in faces})==expected//2
assert all(o.get('SourceSocket')=='spinner_jnt' and o.get('SourceEmitter') for o in faces)
assert all(math.isfinite(v) for o in faces for row in o.matrix_world for v in row)
assert not any(o.name.startswith('Icosphere') for o in scene.objects)
material=faces[0].data.materials[0]
assert material['SourceTexture']=='Twirl_RGB_T'
textures=[n.image for n in material.node_tree.nodes if n.type=='TEX_IMAGE']
assert {i.name for i in textures}=={'Twirl_RGB_T.png','Curve_Rainbow.png'}
assert all(i.packed_file for i in textures)
clock=next(n for n in material.node_tree.nodes if n.type=='VALUE')
values=[]
for fps,frame in [(24,1),(24,8),(24,25),(30,31)]:
 scene.render.fps=fps;scene.frame_set(frame);bpy.context.view_layer.update();values.append(clock.outputs[0].default_value)
assert values[0]!=values[1]
assert abs(values[2]-values[3])<1e-5,values
assert all(d.is_valid for d in material.node_tree.animation_data.drivers)
report={'AnimatedFaces':expected,'WheelCorners':expected//2,'PackedSourceTextures':True,'ClockChangesAcrossFrames':True,'SameSpeedAt24And30FPS':True}
Path('recovery/rocketleague/zomba-validation.json').write_text(json.dumps(report,indent=2));print(report)
