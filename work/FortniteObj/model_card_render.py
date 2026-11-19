"""Render a real exported model for the card cache; imported scripts stay disabled."""
import bpy,sys,math
from mathutils import Vector
from pathlib import Path
args=sys.argv[sys.argv.index('--')+1:];bpy.ops.wm.open_mainfile(filepath=args[0],use_scripts=False)
if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
for o in list(bpy.context.scene.objects):
 if o.type in {'LIGHT','CAMERA'}:bpy.data.objects.remove(o,do_unlink=True)
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH' and not o.hide_render and len(o.data.polygons)]
if not meshes:raise ValueError('No model geometry for preview.')
points=[o.matrix_world@Vector(c) for o in meshes for c in o.bound_box];lo=Vector([min(p[i] for p in points) for i in range(3)]);hi=Vector([max(p[i] for p in points) for i in range(3)]);center=(hi+lo)/2;size=max(max(hi-lo),.001)
bpy.ops.object.camera_add(location=center+Vector((1,-1,.8)).normalized()*size*2.4);camera=bpy.context.object;camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=size*1.55;bpy.context.scene.camera=camera
for position,energy,scale in [((1,-1,2),100,2),((-1,-.3,1),50,2),((0,1,1.5),70,1.5)]:
 bpy.ops.object.light_add(type='AREA',location=center+Vector(position)*size);light=bpy.context.object;light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler();light.data.energy=energy*size*size;light.data.shape='DISK';light.data.size=scale*size
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=12;scene.render.resolution_x=384;scene.render.resolution_y=384;scene.render.resolution_percentage=100;scene.render.film_transparent=True;scene.render.image_settings.file_format='PNG';scene.render.filepath=args[1];bpy.ops.render.render(write_still=True)
