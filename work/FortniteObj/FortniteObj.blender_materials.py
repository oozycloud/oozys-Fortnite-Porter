"""Run using blender --background --python blender_materials.py -- materials.json."""
import bpy, json, os, sys
from mathutils import Vector

def configure_sky(node, directional=False):
    # Blender 5.1 renamed Nishita; choose from the installed version's enum.
    available = {item.identifier for item in node.bl_rna.properties['sky_type'].enum_items}
    node.sky_type = 'HOSEK_WILKIE' if directional else next(kind for kind in ('MULTIPLE_SCATTERING', 'NISHITA', 'HOSEK_WILKIE') if kind in available)
    node.sun_elevation = .65
    node.sun_rotation = .3
    node.sun_direction = Vector((.76, .24, .61)).normalized()
    return node

def sky_colour(tree, direction, preset):
    """Shared world and sphere shader, with no external image dependencies."""
    if preset == 'Night':
        separate = tree.nodes.new('ShaderNodeSeparateXYZ'); tree.links.new(direction, separate.inputs[0])
        ramp = tree.nodes.new('ShaderNodeValToRGB'); ramp.label = 'Night horizon'
        ramp.color_ramp.elements[0].color = (.015, .025, .065, 1)
        ramp.color_ramp.elements[1].color = (.001, .003, .012, 1)
        tree.links.new(separate.outputs['Z'], ramp.inputs[0])
        stars = tree.nodes.new('ShaderNodeTexVoronoi'); stars.inputs['Scale'].default_value = 180
        tree.links.new(direction, stars.inputs['Vector'])
        threshold = tree.nodes.new('ShaderNodeMath'); threshold.operation = 'LESS_THAN'; threshold.inputs[1].default_value = .045
        tree.links.new(stars.outputs['Distance'], threshold.inputs[0])
        above = tree.nodes.new('ShaderNodeMath'); above.operation = 'GREATER_THAN'; above.inputs[1].default_value = .03
        tree.links.new(separate.outputs['Z'], above.inputs[0])
        visible = tree.nodes.new('ShaderNodeMath'); visible.operation = 'MULTIPLY'
        tree.links.new(threshold.outputs[0], visible.inputs[0]); tree.links.new(above.outputs[0], visible.inputs[1])
        mix = tree.nodes.new('ShaderNodeMixRGB'); mix.inputs[2].default_value = (.7, .8, 1, 1)
        tree.links.new(visible.outputs[0], mix.inputs[0]); tree.links.new(ramp.outputs[0], mix.inputs[1])
        return mix.outputs[0]
    sky = configure_sky(tree.nodes.new('ShaderNodeTexSky'), directional=True)
    if preset in ('Sunrise', 'Sunset'):
        sky.sun_direction = Vector((1 if preset == 'Sunset' else -1, .2, .045)).normalized()
        sky.turbidity = 5 if preset == 'Sunset' else 3
    tree.links.new(direction, sky.inputs['Vector'])
    if preset == 'Overcast':
        noise = tree.nodes.new('ShaderNodeTexNoise'); noise.inputs['Scale'].default_value = 3
        noise.inputs['Detail'].default_value = 4; tree.links.new(direction, noise.inputs['Vector'])
        ramp = tree.nodes.new('ShaderNodeValToRGB'); ramp.label = 'Cloud cover'
        ramp.color_ramp.elements[0].color = (.14, .18, .24, 1)
        ramp.color_ramp.elements[1].color = (.6, .65, .7, 1)
        tree.links.new(noise.outputs['Fac'], ramp.inputs[0]); return ramp.outputs[0]
    return sky.outputs['Color']

manifest = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else os.path.join(os.path.dirname(__file__), 'materials.json')
folder = os.path.dirname(os.path.abspath(manifest))
sys.path.insert(0, folder)
with open(manifest, encoding='utf-8') as f:
    data = json.load(f)
bpy.ops.wm.read_factory_settings(use_empty=True)
rig = None
if data.get('Scene'):
    from blender_rig import build_scene
    with open(os.path.join(folder, data['Scene']), encoding='utf-8') as f:
        rig, rig_warnings = build_scene(json.load(f), data.get('RigName', 'Fortnite_Rig'), data.get('Materials', []))
    with open(os.path.join(folder, 'rig-report.json'), 'w', encoding='utf-8') as f:
        json.dump({'Armature': rig.name if rig else None, 'Bones': len(rig.data.bones) if rig else 0, 'Warnings': rig_warnings}, f, indent=2)
elif data.get('WrapPreview'):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=64, ring_count=32, radius=1)
    obj=bpy.context.object;obj.name='Wrap material preview'
    obj.data.materials.append(bpy.data.materials.new('Wrap'))
    for polygon in obj.data.polygons: polygon.use_smooth=True
    obj['Purpose']='Generated preview mesh. Append the Wrap material to use it on your own model.'
elif data.get('Emoticon'):
    image = bpy.data.images.load(os.path.join(folder, data['Emoticon']), check_existing=True)
    columns, rows = max(1, data.get('Columns', 1)), max(1, data.get('Rows', 1))
    count = max(1, data.get('Frames', 1))
    bpy.ops.mesh.primitive_plane_add(size=2)
    obj = bpy.context.object; obj.name = data.get('ImageAssetName', 'Emoticon')
    obj.scale.x = image.size[0] * rows / max(1, image.size[1] * columns)
    mat = bpy.data.materials.new('Emoticon image')
    if bpy.app.version < (5, 0, 0): mat.use_nodes = True
    tree = mat.node_tree; tree.nodes.clear()
    uv = tree.nodes.new('ShaderNodeTexCoord')
    mapping = tree.nodes.new('ShaderNodeMapping'); mapping.vector_type = 'POINT'
    mapping.inputs['Scale'].default_value = (1/columns, 1/rows, 1)
    tree.links.new(uv.outputs['UV'], mapping.inputs['Vector'])
    tex = tree.nodes.new('ShaderNodeTexImage'); tex.image = image; tex.extension = 'CLIP'
    tree.links.new(mapping.outputs['Vector'], tex.inputs['Vector'])
    colour = tree.nodes.new('ShaderNodeEmission'); colour.inputs['Strength'].default_value = 1
    transparent = tree.nodes.new('ShaderNodeBsdfTransparent')
    mix = tree.nodes.new('ShaderNodeMixShader'); out = tree.nodes.new('ShaderNodeOutputMaterial')
    tree.links.new(tex.outputs['Color'], colour.inputs['Color'])
    tree.links.new(tex.outputs['Alpha'], mix.inputs[0])
    tree.links.new(transparent.outputs[0], mix.inputs[1]); tree.links.new(colour.outputs[0], mix.inputs[2])
    tree.links.new(mix.outputs[0], out.inputs['Surface'])
    fps = max(1, min(240, float(data.get('Fps', 12))))
    scene = bpy.context.scene
    scene.render.fps = round(fps); scene.render.fps_base = round(fps)/fps
    scene.frame_start = 1; scene.frame_end = count
    for index in range(count):
        mapping.inputs['Location'].default_value = (index % columns / columns, 1-(index//columns+1)/rows, 0)
        mapping.inputs['Location'].keyframe_insert('default_value', frame=index+1)
    if tree.animation_data and tree.animation_data.action:
        for layer in tree.animation_data.action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        for key in curve.keyframe_points: key.interpolation = 'CONSTANT'
    obj['Animated frames'] = count; obj['Frames per second'] = fps
    obj.data.materials.append(mat)
elif data.get('Skybox'):
    # Standalone procedural skybox asset. The world is the actual backdrop;
    # the large inward-facing sphere makes it visible in viewport renders too.
    bpy.ops.mesh.primitive_uv_sphere_add(segments=96, ring_count=48, radius=1000)
    obj = bpy.context.object; obj.name = 'Skybox'
    sky_mat = bpy.data.materials.new('Skybox World Material')
    if bpy.app.version < (5, 0, 0): sky_mat.use_nodes = True
    stree = sky_mat.node_tree; stree.nodes.clear()
    sb = stree.nodes.new('ShaderNodeEmission'); so = stree.nodes.new('ShaderNodeOutputMaterial')
    preset = data.get('SkyPreset', 'Daylight')
    coordinates=stree.nodes.new('ShaderNodeTexCoord');direction=stree.nodes.new('ShaderNodeVectorMath');direction.operation='NORMALIZE'
    stree.links.new(coordinates.outputs['Object'],direction.inputs[0])
    stree.links.new(sky_colour(stree, direction.outputs[0], preset),sb.inputs['Color']);sb.inputs['Strength'].default_value=1
    transparent=stree.nodes.new('ShaderNodeBsdfTransparent');camera=stree.nodes.new('ShaderNodeLightPath');mix=stree.nodes.new('ShaderNodeMixShader')
    stree.links.new(camera.outputs['Is Camera Ray'],mix.inputs[0]);stree.links.new(transparent.outputs[0],mix.inputs[1]);stree.links.new(sb.outputs[0],mix.inputs[2]);stree.links.new(mix.outputs[0],so.inputs['Surface']);obj.data.materials.append(sky_mat)
    obj['Purpose'] = 'Procedural skybox backdrop'
    world = bpy.context.scene.world or bpy.data.worlds.new('Skybox World'); bpy.context.scene.world = world
    if bpy.app.version < (5, 0, 0): world.use_nodes = True
    bg = world.node_tree.nodes.get('Background'); coords = world.node_tree.nodes.new('ShaderNodeTexCoord')
    world.node_tree.links.new(sky_colour(world.node_tree, coords.outputs['Normal'], preset), bg.inputs['Color']); bg.inputs['Strength'].default_value = 1
    world['Sky preset'] = preset
    # Open inside the backdrop and use the exported world in material preview.
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                area.spaces.active.clip_end = 5000
                area.spaces.active.shading.type = 'MATERIAL'
                area.spaces.active.region_3d.view_location = (0, 0, 0)
                area.spaces.active.region_3d.view_distance = 2
                area.spaces.active.shading.use_scene_world = True
                area.spaces.active.shading.use_scene_world_render = True
else:
    bpy.ops.wm.obj_import(filepath=os.path.join(folder, data['Obj']))
for spec in data['Materials']:
    mat = bpy.data.materials.get(spec['Name'])
    if mat is None:
        continue
    if bpy.app.version < (5, 0, 0): mat.use_nodes = True
    tree = mat.node_tree
    tree.nodes.clear()
    bsdf = tree.nodes.new('ShaderNodeBsdfPrincipled'); bsdf.location = (200, 50)
    out = tree.nodes.new('ShaderNodeOutputMaterial'); out.location = (550, 50)
    tree.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    bsdf.inputs['Base Color'].default_value = spec['Color']
    bsdf.inputs['Emission Strength'].default_value = 0
    bsdf.inputs['Emission Color'].default_value = (0, 0, 0, 1)
    mat['SourceMaterial'] = spec['Source']
    textures = {}
    matte = spec.get('Matte', data.get('MatteMaterials', False))
    bsdf.inputs['Roughness'].default_value = max(0.65, spec.get('Roughness', 0.72)) if matte else spec.get('Roughness', 0.5)
    bsdf.inputs['Metallic'].default_value = 0 if matte else spec.get('Metallic',0)
    bsdf.inputs['Specular IOR Level'].default_value = 0.15 if matte else spec.get('Specular', 0.5)
    # OverlayMaterialData is Fortnite's inverted-hull toon outline. The source
    # material inherits a debug/default grid texture when decoded outside Unreal;
    # use a clean unlit black surface for the generated outer shell instead.
    is_outer = 'InvHullOutline' in spec.get('Source', '') or spec['Name'].endswith('_outer')
    if is_outer:
        bsdf.inputs['Base Color'].default_value = (0.012, 0.012, 0.012, 1)
        bsdf.inputs['Roughness'].default_value = 1.0
        bsdf.inputs['Metallic'].default_value = 0.0
        bsdf.inputs['Specular IOR Level'].default_value = 0.0
        geo = tree.nodes.new('ShaderNodeNewGeometry')
        ray = tree.nodes.new('ShaderNodeLightPath')
        invisible = tree.nodes.new('ShaderNodeMath'); invisible.operation = 'MAXIMUM'
        tree.links.new(geo.outputs['Backfacing'], invisible.inputs[0])
        tree.links.new(ray.outputs['Is Shadow Ray'], invisible.inputs[1])
        transparent = tree.nodes.new('ShaderNodeBsdfTransparent')
        mix = tree.nodes.new('ShaderNodeMixShader')
        tree.links.new(invisible.outputs[0], mix.inputs[0])
        tree.links.new(bsdf.outputs[0], mix.inputs[1])
        tree.links.new(transparent.outputs[0], mix.inputs[2])
        tree.links.new(mix.outputs[0], out.inputs['Surface'])
        mat['Shading'] = 'Toon outer layer outline (inverted hull)'
        continue
    sockets = {'BaseColor': 'Base Color', 'Specular': 'Specular IOR Level', 'Metallic': 'Metallic', 'Roughness': 'Roughness', 'Alpha': 'Alpha'}
    for index, (role, relative) in enumerate(spec['Maps'].items()):
        # Scalar values alone have no effect when a texture is connected. Matte
        # exports must also prevent masks from driving these reflection inputs.
        if role == 'Emission' or matte and role in ('Metallic', 'Specular'):
            continue
        tex = tree.nodes.new('ShaderNodeTexImage'); tex.label = role; tex.location = (-700, 400 - index * 290)
        tex.image = bpy.data.images.load(os.path.join(folder, relative), check_existing=True)
        textures[role] = tex
        tex.image.colorspace_settings.name = 'sRGB' if role.startswith('BaseColor') or role in ('ShadedColor', 'LineColor', 'Galaxy', 'PlanetColor') else 'Non-Color'
        if role == 'Normal':
            normal = tree.nodes.new('ShaderNodeNormalMap'); normal.location = (-200, 100)
            tree.links.new(tex.outputs['Color'], normal.inputs['Color'])
            tree.links.new(normal.outputs['Normal'], bsdf.inputs['Normal'])
        elif matte and role == 'Roughness':
            floor = tree.nodes.new('ShaderNodeMath'); floor.operation = 'MAXIMUM'
            floor.label = 'Matte roughness floor'; floor.inputs[1].default_value = 0.65
            floor.location = (-150, -300)
            tree.links.new(tex.outputs['Color'], floor.inputs[0])
            tree.links.new(floor.outputs[0], bsdf.inputs['Roughness'])
        elif role in sockets:
            tree.links.new(tex.outputs['Color'], bsdf.inputs[sockets[role]])
        if role == 'BaseColor' and spec.get('UseBaseAlpha') and 'Alpha' not in spec['Maps']:
            tree.links.new(tex.outputs['Alpha'], bsdf.inputs['Alpha'])
    from blender_surface_fx import apply_surface_fx
    apply_surface_fx(mat, spec, textures, bsdf, out)
    if spec.get('LayerCount', 1) > 1:
        mat['Shading'] = 'Fortnite layers selected by UV channel 1'
        selector = tree.nodes.new('ShaderNodeUVMap'); selector.uv_map = 'UVMap_1'
        separate = tree.nodes.new('ShaderNodeSeparateXYZ'); tree.links.new(selector.outputs['UV'], separate.inputs[0])
        layer_id = tree.nodes.new('ShaderNodeMath'); layer_id.operation = 'FLOOR'
        tree.links.new(separate.outputs['X'], layer_id.inputs[0])
        factors = {}
        for i in range(2, spec['LayerCount']+1):
            compare = tree.nodes.new('ShaderNodeMath'); compare.operation = 'COMPARE'
            compare.inputs[1].default_value = i-1; compare.inputs[2].default_value = 0.1
            tree.links.new(layer_id.outputs[0], compare.inputs[0]); factors[i] = compare.outputs[0]
        for role, socket in (('BaseColor','Base Color'),('Normal','Normal'),('Roughness','Roughness')):
            if role not in textures: continue
            value = textures[role].outputs['Color']
            for i in range(2, spec['LayerCount']+1):
                if role+str(i) not in textures: continue
                mix = tree.nodes.new('ShaderNodeMixRGB'); mix.label = role + ' layer ' + str(i)
                tree.links.new(factors[i],mix.inputs[0]); tree.links.new(value,mix.inputs[1])
                tree.links.new(textures[role+str(i)].outputs['Color'],mix.inputs[2]); value=mix.outputs[0]
            if role == 'Normal':
                normal=tree.nodes.new('ShaderNodeNormalMap'); tree.links.new(value,normal.inputs['Color']); value=normal.outputs[0]
            if role == 'Roughness' and matte:
                floor=tree.nodes.new('ShaderNodeMath'); floor.operation='MAXIMUM'; floor.inputs[1].default_value=0.65
                tree.links.new(value,floor.inputs[0]); value=floor.outputs[0]
            tree.links.new(value,bsdf.inputs[socket])
    if spec.get('CartoonColor') and 'BaseColor' in textures:
        # Authored colour textures plus a three-step light ramp give character
        # materials the graphic animated-film treatment instead of a flat matte.
        # Works in Eevee and Cycles without an additive emission layer.
        mat['Shading'] = 'Cartoon: authored lit/shaded colours and ink lines'
        bsdf.inputs['Specular IOR Level'].default_value = 0
        bsdf.inputs['Roughness'].default_value = 1
        for key in ('Roughness','Metallic','Specular IOR Level'):
            for link in list(bsdf.inputs[key].links): tree.links.remove(link)
        geo = tree.nodes.new('ShaderNodeNewGeometry')
        dot = tree.nodes.new('ShaderNodeVectorMath'); dot.operation = 'DOT_PRODUCT'
        dot.inputs[1].default_value = Vector((0.4,-0.65,0.65)).normalized()
        tree.links.new(geo.outputs['Normal'], dot.inputs[0])
        threshold = tree.nodes.new('ShaderNodeMath'); threshold.operation = 'GREATER_THAN'
        threshold.label = 'Cel shadow boundary'; threshold.inputs[1].default_value = 0.05
        tree.links.new(dot.outputs['Value'], threshold.inputs[0])
        shade = tree.nodes.new('ShaderNodeMixRGB'); shade.label = 'Authored shadow / lit colour'
        tree.links.new(threshold.outputs[0], shade.inputs[0])
        tree.links.new(textures['BaseColor'].outputs['Color'], shade.inputs[2])
        if 'ShadedColor' in textures:
            tree.links.new(textures['ShadedColor'].outputs['Color'], shade.inputs[1])
        else:
            shadow = tree.nodes.new('ShaderNodeMixRGB'); shadow.blend_type = 'MULTIPLY'
            shadow.inputs[0].default_value = 1; shadow.inputs[2].default_value = (0.65,0.65,0.65,1)
            tree.links.new(textures['BaseColor'].outputs['Color'], shadow.inputs[1])
            tree.links.new(shadow.outputs[0], shade.inputs[1])
        tree.links.new(shade.outputs[0], bsdf.inputs['Base Color'])
        # Camera-only flat colour preserves the graphic palette without adding
        # an emissive layer, lighting other objects, or washing out the texture.
        flat = tree.nodes.new('ShaderNodeEmission'); flat.label = 'Cel colour (camera only)'
        flat.inputs['Strength'].default_value = 1
        tree.links.new(shade.outputs[0], flat.inputs['Color'])
        ray = tree.nodes.new('ShaderNodeLightPath')
        surface = tree.nodes.new('ShaderNodeMixShader')
        tree.links.new(ray.outputs['Is Camera Ray'], surface.inputs[0])
        tree.links.new(bsdf.outputs[0], surface.inputs[1]); tree.links.new(flat.outputs[0], surface.inputs[2])
        tree.links.new(surface.outputs[0], out.inputs['Surface'])
# Keep fallback slots matte too when a source material could not be decoded.
if data.get('MatteMaterials', False):
    known = {spec['Name'] for spec in data['Materials']}
    for mat in bpy.data.materials:
        if mat.name in known:
            continue
        if bpy.app.version < (5, 0, 0): mat.use_nodes = True
        for node in mat.node_tree.nodes:
            if node.type == 'BSDF_PRINCIPLED':
                for socket, value in (('Metallic', 0), ('Specular IOR Level', 0.15), ('Roughness', 0.72)):
                    for link in list(node.inputs[socket].links):
                        mat.node_tree.links.remove(link)
                    node.inputs[socket].default_value = value
# Remove unused images created by OBJ's automatic material conversion before packing.
for image in list(bpy.data.images):
    if image.users == 0:
        bpy.data.images.remove(image)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
if os.path.exists(os.path.join(folder, 'fx.json')) and meshes:
    sys.path.insert(0, folder)
    from blender_fx import build_fx
    target = next((o for o in meshes if 'pickaxe' in o.name.lower() or 'weapon' in o.name.lower()), meshes[0])
    fx_report = build_fx(folder, target)
    with open(os.path.join(folder, 'fx-report.json'), 'w', encoding='utf-8') as f:
        json.dump(fx_report, f, indent=2)
bpy.context.view_layer.update()
points = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
if points and not data.get('Skybox'):
    low = Vector(tuple(min(p[i] for p in points) for i in range(3)))
    high = Vector(tuple(max(p[i] for p in points) for i in range(3)))
    center = (low + high) / 2
    extent = max(high - low)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                area.spaces.active.shading.type = 'MATERIAL'
                area.spaces.active.clip_end = max(1000, extent * 20)
                area.spaces.active.region_3d.view_location = center
                area.spaces.active.region_3d.view_distance = extent * 1.8
                area.spaces.active.region_3d.view_rotation = Vector((-3, 1, -0.6)).to_track_quat('-Z', 'Y')
    # Map exports are also profile-picture scenes.  Give the .blend a square
    # render camera and a three-point setup so pressing Numpad 0 then F12
    # produces a useful composition without manual camera work.  The setup is
    # deliberately created only when map.json is present; character exports
    # keep their existing animation/posing workflow.
    if os.path.exists(os.path.join(folder, 'map.json')):
        scene = bpy.context.scene
        try:
            scene.render.engine = 'BLENDER_EEVEE_NEXT'
        except (TypeError, ValueError):
            # Blender 4.x and some vendor builds expose the same renderer as
            # BLENDER_EEVEE instead of the 4.3+ enum name.
            scene.render.engine = 'BLENDER_EEVEE'
        scene.render.resolution_x = 1024
        scene.render.resolution_y = 1024
        scene.render.resolution_percentage = 100
        scene.render.image_settings.file_format = 'PNG'
        scene.render.filepath = os.path.join(folder, 'profile.png')
        scene.render.film_transparent = False
        world = scene.world or bpy.data.worlds.new('Profile World')
        scene.world = world
        if bpy.app.version < (5, 0, 0): world.use_nodes = True
        bg = world.node_tree.nodes.get('Background')
        if bg:
            bg.inputs['Color'].default_value = (0.12, 0.16, 0.22, 1)
            bg.inputs['Strength'].default_value = 0.5

        def look_at(obj, target):
            obj.rotation_euler = (target - obj.location).to_track_quat('-Z', 'Y').to_euler()

        cam_data = bpy.data.cameras.new('Profile Camera')
        cam = bpy.data.objects.new('Profile Camera', cam_data)
        scene.collection.objects.link(cam)
        cam.location = center + Vector((extent * 1.25, -extent * 1.55, extent * 0.9))
        cam_data.lens = 58
        cam_data.clip_end = max(1000, extent * 20)
        cam_data.sensor_width = 36
        look_at(cam, center)
        scene.camera = cam

        def area_light(name, location, energy, color, size):
            data = bpy.data.lights.new(name, 'AREA')
            data.energy = energy
            data.color = color
            data.shape = 'DISK'
            data.size = size
            obj = bpy.data.objects.new(name, data)
            scene.collection.objects.link(obj)
            obj.location = location
            look_at(obj, center)
            return obj

        power = max(1000.0, extent * extent * 35.0)
        area_light('Profile Key', center + Vector((extent * 0.7, -extent * 0.8, extent * 1.4)), power, (1.0, 0.82, 0.66), extent * 0.7)
        area_light('Profile Fill', center + Vector((-extent * 1.1, -extent * 0.5, extent * 0.5)), power * 0.55, (0.42, 0.65, 1.0), extent * 0.9)
        area_light('Profile Rim', center + Vector((extent * 0.2, extent * 1.0, extent * 1.2)), power * 0.8, (0.35, 0.85, 1.0), extent * 0.6)
        sky = world.node_tree.nodes.new('ShaderNodeTexSky')
        configure_sky(sky)
        world.node_tree.links.new(sky.outputs['Color'], bg.inputs['Color'])
        world['Skybox'] = 'Procedural atmosphere'
if data.get('RequireAnimation') and (not rig or not os.path.exists(os.path.join(folder,'animation.json'))):
    raise ValueError('The requested skin + emote scene has no rig or animation data')
if rig and os.path.exists(os.path.join(folder, 'animation.json')):
    from blender_animation import apply_animation
    with open(os.path.join(folder,'animation.json'),encoding='utf-8') as f:
        animation_data=json.load(f)
    payload=bpy.data.texts.get('Porter Emote.json') or bpy.data.texts.new('Porter Emote.json')
    payload.from_string(json.dumps(animation_data,indent=1))
    animation_report=apply_animation(rig,animation_data)
    if not rig.animation_data.action or not rig.animation_data.action_slot:
        raise ValueError('The emote action was not attached to the skin rig')
    with open(os.path.join(folder,'animation-report.json'),'w',encoding='utf-8') as f:
        json.dump(animation_report,f,indent=2)
for obj in meshes:
    for modifier in obj.modifiers:
        if modifier.type == 'CLOTH':
            modifier.point_cache.frame_start = bpy.context.scene.frame_start
            modifier.point_cache.frame_end = bpy.context.scene.frame_end
if rig and rig.get('Physics setup'):
    with open(os.path.join(folder, 'physics-report.json'), 'w', encoding='utf-8') as f:
        f.write(rig['Physics setup'])
if data.get('Emoticon') or any(s.get('CartoonColor') for s in data.get('Materials', [])):
    bpy.context.scene.view_settings.view_transform = 'Standard'
    bpy.context.scene.view_settings.look = 'None'
bpy.ops.file.pack_all()
if rig:
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode='POSE')
    bpy.ops.pose.select_all(action='DESELECT')
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(folder, data['Blend']))
print('Packed Blender export complete')

