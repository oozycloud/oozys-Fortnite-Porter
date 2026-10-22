"""Rebuild meshes in source vertex order and attach the game's weighted skeleton."""
import bpy, math, json, re
from mathutils import Matrix, Quaternion, Vector


def split_accessories(scene, specs):
    materials = {s['Name']: s for s in specs}
    parts = []
    for part in scene['Parts']:
        if not part['Faces']:
            parts.append(part)
            continue
        colors = part.get('VertexColors') or []
        split_materials = any(word in part['Name'].lower() for word in ('head', 'faceacc', 'accessor', 'hair'))
        groups = {}
        for face, slot in zip(part['Faces'], part['FaceMaterials']):
            spec = materials.get(part['Materials'][slot], {})
            if spec.get('HiddenSurface'):
                continue
            element = None
            # Fortnite's Hide Element IDs are encoded as red = ID / 10, G=B=0.
            # Do not interpret cloth weights or baked cartoon normals as IDs.
            if colors and any(v >= 0.5 for v in spec.get('HiddenElements', {}).values()):
                values = [colors[v] for v in face]
                r = sum(c[0] for c in values) / len(values)
                candidate = round(r * 20) / 2
                if all(abs(c[0]-r)<0.008 and c[1]<0.004 and c[2]<0.004 for c in values) and abs(r-candidate/10)<0.004:
                    element = candidate
            key = (slot if split_materials or element is not None else -1, element)
            groups.setdefault(key, []).append((face, slot))
        if not groups:
            continue
        if len(groups) == 1 and next(iter(groups))[1] is None and sum(len(g) for g in groups.values()) == len(part['Faces']):
            parts.append(part)
            continue
        for (slot, element), faces in groups.items():
            used = sorted({v for face, _ in faces for v in face})
            remap = {v:i for i,v in enumerate(used)}
            spec = materials.get(part['Materials'][slot], {}) if slot >= 0 else {}
            label = spec.get('Source', '').rsplit('.', 1)[-1] or part['Name']
            hidden = False
            if element is not None:
                label += ' - Element ' + ('%02d' % element if element.is_integer() else str(element))
                for name, value in spec.get('HiddenElements', {}).items():
                    try:
                        number = float(name.split()[-1].replace('_','.'))
                    except ValueError:
                        continue
                    if number == element:
                        hidden = value >= 0.5
            if hidden:
                # This export represents the chosen style. Unselected geometry
                # should not survive as detached, hidden accessory objects.
                continue
            child = dict(part, Name=label, Hidden=hidden, SourcePart=part['Name'])
            for attr in ('Positions','Normals','UVs','Weights'):
                child[attr] = [part[attr][v] for v in used]
            child['VertexColors'] = [colors[v] for v in used] if colors else None
            child['ExtraUVs'] = [[uv[v] for v in used] for uv in part.get('ExtraUVs') or []]
            child['Faces'] = [[remap[v] for v in face] for face, _ in faces]
            child['FaceMaterials'] = [s for _,s in faces]
            parts.append(child)
    return dict(scene, Parts=parts)


def split_garments(scene):
    parts = []
    for part in scene['Parts']:
        flexible = {i for i, bone in enumerate(part['Bones']) if re.search(r'dress|skirt|cape|cloak|scarf|coat|cloth|ponytail', bone['Name'], re.I)}
        if not flexible or not part['Faces']:
            parts.append(part); continue
        parent = list(range(len(part['Positions'])))
        def root(i):
            while parent[i] != i:
                parent[i] = parent[parent[i]]; i = parent[i]
            return i
        for face in part['Faces']:
            for vertex in face[1:]: parent[root(vertex)] = root(face[0])
        garment_islands = {root(i) for i, weights in enumerate(part['Weights']) if sum(w['Weight'] for w in weights if w['Bone'] in flexible) > .1}
        groups = {}
        for face, material in zip(part['Faces'], part['FaceMaterials']):
            groups.setdefault(root(face[0]) in garment_islands, []).append((face, material))
        if len(groups) < 2:
            parts.append(part); continue
        for garment, faces in groups.items():
            used = sorted({v for face, _ in faces for v in face}); remap = {v:i for i,v in enumerate(used)}
            child = dict(part, Name=part['Name'] + (' - Garment' if garment else ' - Body'), SourcePart=part.get('SourcePart',part['Name']), Garment=garment)
            for attr in ('Positions','Normals','UVs','Weights'): child[attr] = [part[attr][v] for v in used]
            child['VertexColors'] = [part['VertexColors'][v] for v in used] if part.get('VertexColors') else None
            child['ExtraUVs'] = [[uv[v] for v in used] for uv in part.get('ExtraUVs') or []]
            child['Faces'] = [[remap[v] for v in face] for face,_ in faces]
            child['FaceMaterials'] = [slot for _,slot in faces]
            parts.append(child)
    return dict(scene, Parts=parts)


def build_scene(scene, rig_name, specs=()):
    scene = split_garments(split_accessories(scene, specs))
    # Unreal left-handed centimetres -> Blender right-handed metres, Z up.
    basis = Matrix.Diagonal((1.0, -1.0, 1.0, 1.0))
    display_axes = Matrix.Rotation(-math.pi / 2, 4, 'Z')
    definitions = {}
    source_names = {}
    warnings = []
    objects = []
    for part_number, part in enumerate(sorted(scene['Parts'], key=lambda p: len(p['Positions']), reverse=True)):
        local_world = {}
        bone_names = {}
        visiting = set()
        def world(index):
            if index in local_world:
                return local_world[index]
            if index in visiting:
                raise ValueError('Cycle in source bone hierarchy')
            visiting.add(index)
            bone = part['Bones'][index]
            q = bone['Rotation']
            transform = Matrix.LocRotScale(Vector(bone['Translation']) / 100, Quaternion((q[3], q[0], q[1], q[2])), Vector(bone['Scale']))
            parent = bone['Parent']
            if parent >= 0:
                transform = world(parent) @ transform
            visiting.remove(index)
            local_world[index] = transform
            return transform
        for i, bone in enumerate(part['Bones']):
            transform = basis @ world(i) @ basis @ display_axes
            parent = bone_names[bone['Parent']] if bone['Parent'] >= 0 else None
            name = bone['Name'][:63]
            existing = definitions.get(name)
            if existing and (existing[0] != parent or (existing[1].translation - transform.translation).length > 0.005 or existing[1].to_quaternion().rotation_difference(transform.to_quaternion()).angle > 0.01):
                # Hair/accessory helpers may share names but have different rest poses.
                # Keep their own transforms and weights while sharing compatible body bones.
                name = f"{name[:45]}__part{part_number}"
            bone_names[i] = name
            if name not in definitions:
                definitions[name] = (parent, transform)
                source_names[name] = bone['Name']
        vertices = [(p[0] / 100, -p[1] / 100, p[2] / 100) for p in part['Positions']]
        mesh = bpy.data.meshes.new(part['Name'])
        mesh.from_pydata(vertices, [], part['Faces'])
        mesh.update()
        obj = bpy.data.objects.new(part['Name'], mesh)
        obj['SourcePart'] = part.get('SourcePart', part['Name'])
        obj['Style visible'] = not part.get('Hidden', False)
        obj.hide_render = part.get('Hidden', False)
        obj.hide_viewport = part.get('Hidden', False)
        obj['SourcePhysicsBones'] = json.dumps(part.get('PhysicsBones') or [])
        obj['Garment'] = part.get('Garment', False)
        obj['Garment split'] = 'Garment' in part
        bpy.context.collection.objects.link(obj)
        transform = part.get('Transform')
        if transform:
            # Map instances are authored in Unreal centimetres and use a
            # left-handed Y axis; convert the actor transform to Blender metres.
            location = transform.get('Location', [0, 0, 0])
            rotation = transform.get('Rotation', [0, 0, 0])
            scale = transform.get('Scale', [1, 1, 1])
            obj.location = (location[0] / 100.0, -location[1] / 100.0, location[2] / 100.0)
            q = transform['Quaternion']
            obj.rotation_mode = 'QUATERNION'
            obj.rotation_quaternion = Quaternion((q[3], -q[0], q[1], -q[2]))
            obj.scale = scale
        for name in part['Materials']:
            mesh.materials.append(bpy.data.materials.get(name) or bpy.data.materials.new(name))
        for polygon, material_index in zip(mesh.polygons, part['FaceMaterials']):
            polygon.material_index = material_index
            polygon.use_smooth = True
        uv = mesh.uv_layers.new(name='UVMap')
        uv.data.foreach_set('uv', [c for loop in mesh.loops for c in part['UVs'][loop.vertex_index]])
        for index, values in enumerate(part.get('ExtraUVs') or []):
            extra = mesh.uv_layers.new(name='UVMap_' + str(index+1))
            extra.data.foreach_set('uv', [c for loop in mesh.loops for c in values[loop.vertex_index]])
        mesh.uv_layers.active_index = 0
        if part.get('VertexColors'):
            color = mesh.color_attributes.new(name='SourceColor', type='FLOAT_COLOR', domain='POINT')
            color.data.foreach_set('color', [v for rgba in part['VertexColors'] for v in rgba])
        mesh.normals_split_custom_set_from_vertices([(n[0], -n[1], n[2]) for n in part['Normals']])
        groups = [obj.vertex_groups.new(name=bone_names[i]) for i in range(len(part['Bones']))]
        for vertex, weights in enumerate(part['Weights']):
            for influence in weights:
                groups[influence['Bone']].add([vertex], influence['Weight'], 'REPLACE')
        if part['Bones']:
            objects.append(obj)
        if part.get('OverlayMaterial'):
            obj['Outline material'] = part['OverlayMaterial']
    if not definitions:
        return None, warnings
    armature = bpy.data.armatures.new(rig_name)
    rig = bpy.data.objects.new(rig_name, armature)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    rig.show_in_front = True
    armature.display_type = 'OCTAHEDRAL'
    bpy.ops.object.mode_set(mode='EDIT')
    for name, (parent, transform) in definitions.items():
        bone = armature.edit_bones.new(name)
        distances = [(candidate.translation - transform.translation).length for parent_name, candidate in definitions.values() if parent_name == name]
        distances = [d for d in distances if d > 0.005]
        # New edit bones have zero length. Assigning a matrix at zero length
        # loses its direction; extending it later creates a different rest basis.
        bone.length = (min(distances) if distances else 0.025) if parent else 0.1
        bone.matrix = transform
        bone.use_deform = True
    for name, (parent, _) in definitions.items():
        if parent is not None:
            armature.edit_bones[name].parent = armature.edit_bones[parent]
        armature.edit_bones[name].use_connect = False
    bpy.ops.object.mode_set(mode='OBJECT')
    for bone in armature.bones:
        bone['SourceName'] = source_names[bone.name]
    for obj in objects:
        modifier = obj.modifiers.new('Fortnite skin weights', 'ARMATURE')
        modifier.object = rig
        modifier.use_deform_preserve_volume = False
        obj.parent = rig
        obj.select_set(False)
    # Keep the posing bones easy to find without hundreds of face/helper bones in front.
    main = armature.collections.new('Body and posing')
    helpers = armature.collections.new('Face and helper bones')
    for bone in armature.bones:
        name = bone.name.lower()
        primary = name in {'root', 'pelvis', 'neck_01', 'neck_02', 'head'} or name.startswith(('spine_', 'clavicle_', 'upperarm_', 'lowerarm_', 'hand_', 'thigh_', 'calf_', 'foot_', 'ball_'))
        (main if primary or len(armature.bones) < 30 else helpers).assign(bone)
    # Keep facial controls, fingers, cloth and accessory helpers visible and selectable.
    # They carry the source weights, so hiding the collection makes an otherwise
    # correctly rigged export look like it has no face or finger rig.
    helpers.is_visible = True
    rig['How to pose'] = 'Select this rig, switch to Pose Mode, select a bone and press R. Alt-R clears rotation.'
    rig['Rig type'] = 'Game deformation skeleton and weights; no IK control rig or animation clips'
    from blender_physics import prepare_physics
    report = prepare_physics(rig, objects)
    rig['Physics setup'] = json.dumps(report)
    # Read the evaluated skin, including animation and cloth, instead of running
    # a second cloth simulation on an opaque duplicate of the character.
    for obj in objects:
        material = bpy.data.materials.get(obj.get('Outline material', ''))
        if material is None:
            continue
        shell = bpy.data.objects.new(obj.name + ' - Toon Outer Layer', bpy.data.meshes.new(obj.name + ' outline'))
        bpy.context.collection.objects.link(shell)
        shell.parent = rig
        shell.matrix_world = obj.matrix_world.copy()
        shell.hide_render = obj.hide_render
        shell.hide_viewport = obj.hide_viewport
        shell['Outer Layer'] = True
        shell['SourcePart'] = obj.name
        shell.visible_shadow = False
        nodes = bpy.data.node_groups.new(shell.name, 'GeometryNodeTree')
        nodes.interface.new_socket(name='Geometry', in_out='OUTPUT', socket_type='NodeSocketGeometry')
        source = nodes.nodes.new('GeometryNodeObjectInfo')
        source.transform_space = 'RELATIVE'
        source.inputs['Object'].default_value = obj
        normal = nodes.nodes.new('GeometryNodeInputNormal')
        offset = nodes.nodes.new('ShaderNodeVectorMath'); offset.operation = 'SCALE'
        offset.inputs['Scale'].default_value = 0.0015
        expand = nodes.nodes.new('GeometryNodeSetPosition')
        flip = nodes.nodes.new('GeometryNodeFlipFaces')
        assign = nodes.nodes.new('GeometryNodeSetMaterial'); assign.inputs['Material'].default_value = material
        output = nodes.nodes.new('NodeGroupOutput')
        nodes.links.new(source.outputs['Geometry'], expand.inputs['Geometry'])
        nodes.links.new(normal.outputs['Normal'], offset.inputs[0])
        nodes.links.new(offset.outputs['Vector'], expand.inputs['Offset'])
        nodes.links.new(expand.outputs['Geometry'], flip.inputs['Mesh'])
        nodes.links.new(flip.outputs['Mesh'], assign.inputs['Geometry'])
        nodes.links.new(assign.outputs['Geometry'], output.inputs['Geometry'])
        shell.modifiers.new('Outline follows evaluated skin', 'NODES').node_group = nodes
    return rig, list(dict.fromkeys(warnings))

