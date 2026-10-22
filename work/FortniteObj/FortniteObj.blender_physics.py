"""Editable garment simulation and body collision surfaces."""
import bpy, json, re

CLOTH_BONE = re.compile(r'cape|cloak|cloth|skirt|dress|coat|scarf|ponytail', re.I)
CLOTH_MESH = re.compile(r'cape|cloak|skirt|dress|scarf', re.I)
DYNAMIC_BONE = re.compile(r'^dyn_', re.I)


def switch_driver(modifier, property_name, obj):
    curve = modifier.driver_add(property_name)
    curve.driver.type = 'SCRIPTED'
    variable = curve.driver.variables.new()
    variable.name = 'enabled'
    variable.type = 'SINGLE_PROP'
    variable.targets[0].id = obj
    variable.targets[0].data_path = '["Enable Cloth"]'
    curve.driver.expression = 'enabled'


def prepare_physics(rig, objects):
    physics_collection = bpy.data.collections.new('Physics attachments')
    bpy.context.scene.collection.children.link(physics_collection)
    bone_collection = rig.data.collections.new('Cloth and physics bones')
    attachment_bones = set()
    cloth_objects = []
    for obj in objects:
        # Tiny residual garment weights on a separated body are not permission
        # to simulate its skin. It must remain a collider for the actual coat.
        if obj.get('Garment split') and not obj.get('Garment'):
            continue
        source_physics = set(json.loads(obj.get('SourcePhysicsBones', '[]')))
        flexible = set()
        for bone in rig.data.bones:
            name = bone.get('SourceName', bone.name)
            if name in source_physics or CLOTH_BONE.search(name) or DYNAMIC_BONE.search(name):
                attachment_bones.add(bone.name)
                bone_collection.assign(bone)
            if CLOTH_BONE.search(name):
                flexible.add(bone.name)
        if not obj.data.vertices:
            continue
        whole_cloth = bool(CLOTH_MESH.search(obj.name))
        # Upper chain bones anchor cloth to the animated rig.
        free_bones = {name for name in flexible if rig.data.bones[name].parent and rig.data.bones[name].parent.name in flexible}
        group_indices = {g.index for g in obj.vertex_groups if g.name in free_bones}
        if not whole_cloth and not group_indices:
            continue
        weights = [sum(g.weight for g in v.groups if g.group in group_indices) for v in obj.data.vertices]
        if whole_cloth and max(weights, default=0) < 0.1:
            heights = [v.co.z for v in obj.data.vertices]
            low, high = min(heights), max(heights)
            span = max(high-low, 1e-5)
            weights = [min(1, max(0, ((high-z)/span-0.08)/0.12)) for z in heights]
            pin_source = 'Suggested top-edge pins; edit Physics Pins weights before simulation.'
        else:
            pin_source = 'Pins inferred from cloth-chain skin weights; review before simulation.'
        if max(weights, default=0) <= 0:
            continue
        pins = obj.vertex_groups.new(name='Physics Pins')
        for v, weight in zip(obj.data.vertices, weights):
            # Inferred weights need a weak rest-shape tether: source simulation
            # constraints are not available, especially across layered coats.
            pins.add([v.index], max(0.5, 1-min(1,weight)), 'REPLACE')
        # Exported character garments should be simulation-ready immediately.
        # Users can still switch this off from the custom property if they want
        # a static render or need to edit the suggested pin weights first.
        obj['Enable Cloth'] = True
        obj.id_properties_ui('Enable Cloth').update(description='Disable for a static render. Physics Pins: 1 = attached, 0 = free. Bake in Physics Properties after posing.')
        obj['Physics pins'] = pin_source
        weld = obj.modifiers.new('Cloth seams', 'WELD')
        weld.merge_threshold = 0.00001
        cloth = obj.modifiers.new('Cloth simulation', 'CLOTH')
        cloth.settings.vertex_group_mass = pins.name
        cloth.settings.quality = 12
        cloth.settings.pin_stiffness = 20
        cloth.settings.use_dynamic_mesh = True
        cloth.settings.mass = 0.3
        cloth.settings.tension_stiffness = 25
        cloth.settings.compression_stiffness = 25
        cloth.settings.shear_stiffness = 15
        cloth.settings.bending_stiffness = 2
        # Cooked garments contain overlapping lining and UV-seam vertices.
        # Self collision treats those initial overlaps as penetration and can
        # launch the entire garment away from the character.
        cloth.collision_settings.use_self_collision = False
        cloth.collision_settings.distance_min = 0.001
        cloth.collision_settings.collision_quality = 8
        cloth.point_cache.frame_start = 1
        cloth.point_cache.frame_end = bpy.context.scene.frame_end
        for modifier in (weld, cloth):
            modifier.show_viewport = True
            modifier.show_render = True
            for prop in ('show_viewport', 'show_render'):
                switch_driver(modifier, prop, obj)
        cloth_objects.append(obj.name)
    # Every non-garment mesh participates as a collision surface so dresses,
    # capes and hair do not pass through the body when the scene is played.
    cloth_set = set(cloth_objects)
    for obj in objects:
        if obj.name in cloth_set or not obj.data.vertices:
            continue
        if not any(mod.type == 'COLLISION' for mod in obj.modifiers):
            collision = obj.modifiers.new('Character collision', 'COLLISION')
            collision.show_viewport = True
            collision.show_render = True
            # Blender's default two-centimetre shell intersects fitted clothes
            # before the animation starts. Source meshes are exported in metres.
            obj.collision.thickness_outer = 0.001
            obj.collision.thickness_inner = 0.001
    for name in sorted(attachment_bones):
        point = bpy.data.objects.new('Attach_' + name, None)
        point.empty_display_type = 'SPHERE'
        point.empty_display_size = 0.025
        point.hide_render = True
        physics_collection.objects.link(point)
        constraint = point.constraints.new('COPY_TRANSFORMS')
        constraint.target = rig
        constraint.subtarget = name
        point['Source bone'] = rig.data.bones[name].get('SourceName', name)
        point['Purpose'] = 'Animated attachment point. Parent accessories or collision objects here.'
        point.hide_set(True)
    text = bpy.data.texts.get('Physics setup.txt') or bpy.data.texts.new('Physics setup.txt')
    text.clear()
    text.write('CLOTH AND PHYSICS\n\nGarments and flexible accessories are exported simulation-ready. Physics Pins controls attachment: weight 1 holds a vertex to the animated mesh; 0 lets it move. Review the suggested weights in Weight Paint. Disable Enable Cloth under Object Properties > Custom Properties for a static render, then bake using Physics Properties after posing.\n\nCharacter collision is added to non-garment meshes so dresses, capes and hair collide with the body. Physics attachments contains hidden, bone-following points for source physics bones and cloth chains. Unhide the needed point and parent an accessory or collision object to it.\n\nThis is an editable Blender setup. Unreal simulation constraints and source cloth masks are approximated from the exported weights. Clear the simulation cache after changing a pose, pin weights, or physics settings.\n')
    return {'ClothObjects': cloth_objects, 'CollisionObjects': [o.name for o in objects if any(m.type == 'COLLISION' for m in o.modifiers)], 'AttachmentPoints': len(attachment_bones), 'SimulationEnabled': bool(cloth_objects)}

