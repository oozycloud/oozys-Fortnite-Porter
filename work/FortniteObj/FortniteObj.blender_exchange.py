import bpy, json, os, sys

with open(sys.argv[sys.argv.index('--') + 1], encoding='utf-8-sig') as stream:
    request = json.load(stream)
bpy.ops.wm.open_mainfile(filepath=request['Blend'], use_scripts=False)
if bpy.context.object and bpy.context.object.mode != 'OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')
scene = bpy.context.scene
scene.frame_set(scene.frame_start)
bpy.ops.object.select_all(action='DESELECT')
objects = [o for o in scene.objects if o.type in {'MESH', 'ARMATURE'} and not o.hide_render and not o.hide_get()]
meshes = [o for o in objects if o.type == 'MESH' and len(o.data.polygons)]
if not meshes:
    raise ValueError('This scene has no visible mesh geometry to export.')
for obj in objects:
    obj.select_set(True)
    # Simulation and procedural displacement are retained in the original blend.
    # Exchange formats receive the stable reference geometry and skinning.
    for modifier in list(obj.modifiers):
        if modifier.type in {'CLOTH', 'COLLISION'}:
            obj.modifiers.remove(modifier)
    if obj.type == 'MESH' and request['Triangulate']:
        obj.modifiers.new('Export triangulation', 'TRIANGULATE')
bpy.context.view_layer.objects.active = meshes[0]

limit = int(request['TextureLimit'])
if limit not in (0, 512, 1024, 2048, 4096):
    raise ValueError('Unsupported texture size limit')
for index, image in enumerate(bpy.data.images):
    image.name = "img_%04d" % index
    width, height = image.size
    if limit and max(width, height) > limit:
        ratio = limit / max(width, height)
        image.scale(max(1, round(width * ratio)), max(1, round(height * ratio)))

kind = request['Format']
extension = {'GLB': '.glb', 'GLTF': '.gltf', 'FBX': '.fbx', 'OBJ': '.obj', 'STL': '.stl'}[kind]
path = os.path.join(request['Output'], request['Name'] + extension)
animate = bool(request['Animation'])
if kind in {'FBX', 'OBJ'}:
    texture_dir = os.path.join(request['Output'], 'textures')
    os.makedirs(texture_dir, exist_ok=True)
    for index, image in enumerate(bpy.data.images):
        if not image.has_data or image.type not in {'IMAGE', 'UV_TEST'}:
            continue
        image.filepath_raw = os.path.join(texture_dir, 'texture_%04d.png' % index)
        image.file_format = 'PNG'
        image.save()

if kind in {'GLB', 'GLTF'}:
    bpy.ops.export_scene.gltf(filepath=path, export_format='GLB' if kind == 'GLB' else 'GLTF_SEPARATE',
        use_selection=True, export_animations=animate, export_animation_mode='ACTIVE_ACTIONS',
        export_cameras=False, export_lights=False, export_apply=True, export_image_format='AUTO')
elif kind == 'FBX':
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH', 'ARMATURE'},
        add_leaf_bones=False, path_mode='COPY', embed_textures=True, bake_anim=animate,
        bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0.0,
        axis_forward='-Z', axis_up='Y')
elif kind == 'OBJ':
    bpy.ops.wm.obj_export(filepath=path, export_selected_objects=True, export_materials=True,
        path_mode='RELATIVE', forward_axis='NEGATIVE_Z', up_axis='Y', apply_modifiers=True)
    # Blender's OBJ writer can use a packed image's old path even after saving
    # a copy. Bind the MTL to the files explicitly written into this package.
    mtl_path = os.path.splitext(path)[0] + '.mtl'
    with open(mtl_path, encoding='utf-8') as stream:
        lines = stream.readlines()
    for index, line in enumerate(lines):
        if line.startswith(('map_', 'bump ')):
            prefix, reference = line.rstrip().rsplit(' ', 1)
            filename = os.path.basename(reference.replace('\\', '/'))
            if not os.path.isfile(os.path.join(texture_dir, filename)):
                raise ValueError('Missing OBJ texture: ' + filename)
            lines[index] = prefix + ' textures/' + filename + '\n'
    with open(mtl_path, 'w', encoding='utf-8') as stream:
        stream.writelines(lines)
elif kind == 'STL':
    bpy.ops.wm.stl_export(filepath=path, export_selected_objects=True, global_scale=1000.0,
        apply_modifiers=True, use_scene_unit=False)

if not os.path.isfile(path) or os.path.getsize(path) == 0:
    raise RuntimeError('Exporter did not produce a file')
with open(os.path.join(request['Output'], 'complete.json'), 'w', encoding='utf-8') as stream:
    json.dump({'Format': kind, 'File': os.path.basename(path), 'MeshCount': len(meshes),
        'Animation': animate and kind in {'GLB', 'GLTF', 'FBX'}, 'TextureLimit': limit,
        'Triangulated': request['Triangulate'], 'Units': 'millimetres' if kind == 'STL' else 'metres',
        'Limitations': 'Custom shaders, procedural effects and cloth are preserved in the original Blender file.'}, stream, indent=2)
print('Exchange export complete:', path)
