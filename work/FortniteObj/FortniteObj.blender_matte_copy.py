"""Save a matte copy of an existing export; never overwrite its source file."""
import bpy, json, os, sys

source, destination = map(os.path.abspath, sys.argv[sys.argv.index('--') + 1:][:2])
if os.path.normcase(source) == os.path.normcase(destination) or os.path.exists(destination):
    raise ValueError('Choose a new destination; the original file is preserved.')
bpy.ops.wm.open_mainfile(filepath=source, load_ui=False, use_scripts=False)
report = []
materials = {slot.material for obj in bpy.data.objects if obj.type == 'MESH' for slot in obj.material_slots if slot.material}
for mat in materials:
    if bpy.app.version < (5, 0, 0): mat.use_nodes = True
    tree = mat.node_tree
    count = 0
    for node in list(tree.nodes):
        if node.type != 'BSDF_PRINCIPLED':
            continue
        for socket, value in (('Metallic', 0.0), ('Specular IOR Level', 0.15), ('Coat Weight', 0.0), ('Sheen Weight', 0.0)):
            if socket not in node.inputs:
                continue
            for link in list(node.inputs[socket].links):
                tree.links.remove(link)
            node.inputs[socket].default_value = value
        rough = node.inputs['Roughness']
        if rough.is_linked:
            original = rough.links[0].from_socket
            for link in list(rough.links):
                tree.links.remove(link)
            clamp = tree.nodes.new('ShaderNodeMath')
            clamp.operation = 'MAXIMUM'
            clamp.label = 'Matte roughness floor'
            clamp.inputs[1].default_value = 0.65
            clamp.location = (node.location.x - 190, node.location.y - 250)
            tree.links.new(original, clamp.inputs[0])
            tree.links.new(clamp.outputs[0], rough)
        else:
            rough.default_value = max(0.72, rough.default_value)
        count += 1
    report.append({'Material': mat.name, 'PrincipledShadersAdjusted': count})
if not any(m['PrincipledShadersAdjusted'] for m in report):
    raise ValueError('No supported surface shaders were found in this Blender file.')
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=destination)
with open(os.path.join(os.path.dirname(destination), 'material-repair-report.json'), 'w', encoding='utf-8') as output:
    json.dump({'Source': source, 'Copy': destination, 'Materials': report}, output, indent=2)
print('Saved matte copy: ' + destination)

