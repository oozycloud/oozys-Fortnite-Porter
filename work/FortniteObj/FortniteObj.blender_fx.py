"""Build editable Blender previews for authored Fortnite pickaxe FX references."""
import bpy, json, math, os, sys
from mathutils import Vector


def _move(obj, collection):
    for old in list(obj.users_collection):
        old.objects.unlink(obj)
    collection.objects.link(obj)


def _mat(name, color, emission=4.0):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    if bpy.app.version < (5, 0, 0): mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()
    bsdf = nodes.new('ShaderNodeBsdfPrincipled')
    out = nodes.new('ShaderNodeOutputMaterial')
    bsdf.inputs['Base Color'].default_value = (*color, 1.0)
    bsdf.inputs['Metallic'].default_value = 0.0
    bsdf.inputs['Roughness'].default_value = 0.28
    bsdf.inputs['Emission Color'].default_value = (*color, 1.0)
    bsdf.inputs['Emission Strength'].default_value = emission
    links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    return mat


def _sphere(name, location, scale, material, collection):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=1.0, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(material)
    _move(obj, collection)
    return obj


def _flame(name, location, scale, material, collection, tilt=0.0):
    # A bent, tapered tongue reads as fire much better than a sphere or a
    # straight cone. The alternating ring centers add a small natural curl.
    vertices = []
    rings = 5
    sides = 10
    for ring in range(rings):
        t = ring / (rings - 1)
        radius = 0.92 * (1.0 - t) ** 0.58
        cx = math.sin(tilt * 1.7 + t * 3.2) * 0.22 * t
        cy = math.cos(tilt * 1.3 + t * 2.4) * 0.16 * t
        z = -1.0 + t * 1.75
        for side in range(sides):
            angle = side * math.tau / sides
            vertices.append((cx + math.cos(angle) * radius, cy + math.sin(angle) * radius, z))
    tip = len(vertices)
    vertices.append((math.sin(tilt * 1.7 + 3.2) * 0.24, math.cos(tilt * 1.3 + 2.4) * 0.18, 1.15))
    faces = []
    for ring in range(rings - 1):
        for side in range(sides):
            a = ring * sides + side
            b = ring * sides + (side + 1) % sides
            c = (ring + 1) * sides + (side + 1) % sides
            d = (ring + 1) * sides + side
            faces.append((a, b, c, d))
    for side in range(sides):
        faces.append(((rings - 1) * sides + side, (rings - 1) * sides + (side + 1) % sides, tip))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.location = location
    obj.scale = scale
    obj.data.materials.append(material)
    return obj


def _trail(name, points, material, collection):
    curve = bpy.data.curves.new(name, 'CURVE')
    curve.dimensions = '3D'
    curve.bevel_depth = 0.035
    curve.bevel_resolution = 3
    spline = curve.splines.new('BEZIER')
    spline.bezier_points.add(len(points) - 1)
    for bp, point in zip(spline.bezier_points, points):
        bp.co = point
        bp.handle_left_type = 'AUTO'
        bp.handle_right_type = 'AUTO'
    obj = bpy.data.objects.new(name, curve)
    collection.objects.link(obj)
    obj.data.materials.append(material)
    return obj


def _parent(obj, target):
    if target is None:
        return
    matrix = obj.matrix_world.copy()
    obj.parent = target
    obj.matrix_world = matrix


def build_fx(folder, target=None):
    with open(os.path.join(folder, 'fx.json'), encoding='utf-8') as handle:
        data = json.load(handle)
    collection = bpy.data.collections.new('Fortnite FX · ' + data.get('Asset', 'Pickaxe'))
    bpy.context.scene.collection.children.link(collection)

    asset_text = (data.get('Asset', '') + ' ' + ' '.join(data.get('IdleEffects', []))).lower()
    merry = any(word in asset_text for word in ('candy', 'mint', 'merry'))
    palette = [(0.05, 1.0, 0.22), (1.0, 0.05, 0.08), (1.0, 0.65, 0.06)] if merry else [(0.08, 0.72, 1.0), (0.65, 0.12, 1.0), (1.0, 0.25, 0.06)]
    idle_mat = _mat('FX Idle Glow', (0.015, 0.32, 0.045) if merry else palette[0], 1.4)
    inner_mat = _mat('FX Idle Inner Flame', (0.18, 0.95, 0.08) if merry else (0.36, 0.9, 1.0), 2.4)
    swing_mat = _mat('FX Swing Glow', palette[1], 3.0)
    impact_mat = _mat('FX Impact Glow', palette[2], 2.5)

    if target is not None:
        bounds = [target.matrix_world @ Vector(corner) for corner in target.bound_box]
        low = Vector((min(p.x for p in bounds), min(p.y for p in bounds), min(p.z for p in bounds)))
        high = Vector((max(p.x for p in bounds), max(p.y for p in bounds), max(p.z for p in bounds)))
        center = (low + high) * 0.5
        extent = max((high - low).length * 0.045, 0.06)
    else:
        center, extent = Vector((0, 0, 0)), 0.15

    objects = []
    for index in range(14):
        angle = index * math.tau / 14.0
        radius = extent * (3.0 + (index % 3) * 0.8)
        point = center + Vector((math.cos(angle) * radius, math.sin(angle) * radius, (index % 4 - 1.5) * extent * 1.4))
        tilt = math.sin(index * 2.17) * 0.7
        obj = _flame('FX Idle Flame %02d' % index, point, (extent * 0.5, extent * 0.5, extent * 1.5), idle_mat, collection, tilt)
        inner = _flame('FX Idle Flame Core %02d' % index, point + Vector((0, 0, extent * 0.18)), (extent * 0.24, extent * 0.24, extent * 0.82), inner_mat, collection, tilt * 0.7)
        for flame in (obj, inner):
            flame.scale = (0.05, 0.05, 0.05)
            flame.keyframe_insert('scale', frame=1)
            flame.scale = (1.0, 1.0, 1.0)
            flame.keyframe_insert('scale', frame=6 + (index % 4) * 2)
            flame.scale = (0.1, 0.1, 0.1)
            flame.keyframe_insert('scale', frame=18 + (index % 3) * 2)
            objects.append(flame)

    trail_points = [center + Vector((-extent * 7, -extent * 2, -extent * 3)), center + Vector((0, extent * 2, extent * 2)), center + Vector((extent * 7, extent * 4, extent * 5))]
    for index, offset in enumerate((Vector((0, 0, 0)), Vector((0, extent * 0.8, extent * 0.5)))):
        obj = _trail('FX Swing Trail %02d' % index, [point + offset for point in trail_points], swing_mat, collection)
        obj.data.bevel_factor_end = 0.0
        obj.data.keyframe_insert('bevel_factor_end', frame=1)
        obj.data.bevel_factor_end = 1.0
        obj.data.keyframe_insert('bevel_factor_end', frame=10 + index * 3)
        obj.data.bevel_factor_end = 0.0
        obj.data.keyframe_insert('bevel_factor_end', frame=22 + index * 2)
        objects.append(obj)

    bpy.ops.mesh.primitive_torus_add(major_radius=extent * 2.5, minor_radius=extent * 0.12, major_segments=32, minor_segments=8, location=center)
    ring = bpy.context.object
    ring.name = 'FX Impact Ring'
    ring.data.materials.append(impact_mat)
    _move(ring, collection)
    ring.scale = (0.05, 0.05, 0.05)
    ring.keyframe_insert('scale', frame=1)
    ring.scale = (1.0, 1.0, 1.0)
    ring.keyframe_insert('scale', frame=8)
    ring.scale = (1.8, 1.8, 1.8)
    ring.keyframe_insert('scale', frame=16)
    ring.scale = (0.05, 0.05, 0.05)
    ring.keyframe_insert('scale', frame=24)
    objects.append(ring)

    collection['Source FX'] = json.dumps(data.get('IdleEffects', []) + data.get('SwingEffects', []) + data.get('Trails', []) + data.get('ImpactEffects', []))
    collection['Idle Socket'] = data.get('IdleSocket', '')
    collection['Swing Socket'] = data.get('SwingSocket', '')
    collection['Trail Sockets'] = '%s → %s' % (data.get('TrailFirstSocket', ''), data.get('TrailSecondSocket', ''))
    collection['Preview Frames'] = '1-24'
    # Keep procedural effects attached to the exported pickaxe/mesh so they
    # follow the asset when it is moved or parented into another scene.
    if target is not None:
        for effect in objects:
            _parent(effect, target)
    collection['Placement'] = 'Centered on target bounds and parented to the exported mesh'
    bpy.context.scene.frame_start = 1
    bpy.context.scene.frame_end = 24
    return {'Collection': collection.name, 'Objects': len(objects), 'Sources': data, 'Sockets': {'Idle': data.get('IdleSocket', ''), 'Swing': data.get('SwingSocket', ''), 'TrailFirst': data.get('TrailFirstSocket', ''), 'TrailSecond': data.get('TrailSecondSocket', '')}}

