import bpy
import json
import sys
from pathlib import Path
from mathutils import Matrix, Quaternion, Vector

request = json.loads(Path(sys.argv[sys.argv.index('--') + 1]).read_text(encoding='utf-8-sig'))
folder = Path(request['Output'])
name = request['Name']
source = json.loads((folder / 'part-scene.json').read_text(encoding='utf-8'))
bpy.ops.wm.read_factory_settings(use_empty=True)
# Swap Y/Z to convert Unity's left-handed, Y-up coordinates to Blender Z-up.
basis = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
materials = {}
for key, definition in source['Materials'].items():
    material = bpy.data.materials.new(definition['Name'])
    material.use_nodes = True
    nodes, links = material.node_tree.nodes, material.node_tree.links
    shader = nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = definition['Color']
    shader.inputs['Metallic'].default_value = min(1, max(0, definition['Metallic']))
    shader.inputs['Roughness'].default_value = min(1, max(0, definition['Roughness']))
    floats, colors = definition.get('Floats', {}), definition.get('Colors', {})
    textures = definition.get('Textures', {})
    loaded = {}
    def math_node(operation, a, b=0):
        n = nodes.new('ShaderNodeMath'); n.operation = operation
        for slot, value in zip(n.inputs, (a, b)):
            if isinstance(value, (int, float)): slot.default_value = value
            else: links.new(value, slot)
        return n.outputs[0]
    def multiply_color(a, b):
        n = nodes.new('ShaderNodeMixRGB'); n.blend_type = 'MULTIPLY'; n.inputs[0].default_value = 1
        for slot, value in zip((n.inputs[1], n.inputs[2]), (a, b)):
            if isinstance(value, (list, tuple)): slot.default_value = value
            else: links.new(value, slot)
        return n.outputs[0]
    def tex(prop, color=False, base_uv=False):
        if prop in loaded: return loaded[prop]
        d = textures[prop]
        # Separate datablocks prevent a shared colour/data map from changing colour space.
        image = bpy.data.images.load(str(folder / d['Image']), check_existing=False)
        image.colorspace_settings.name = 'sRGB' if color else 'Non-Color'
        image.pack()
        n = nodes.new('ShaderNodeTexImage'); n.image = image; n.label = prop
        uv = nodes.new('ShaderNodeUVMap')
        channel = int(floats.get('_UVEmissive' if 'Emiss' in prop else '_UVBase', 0))
        if prop == '_DetailMap' and floats.get('_UVDetail', 4) < 4:
            channel = int(floats['_UVDetail'])
        if channel > 3: channel = 0
        uv.uv_map = 'UVMap' if channel == 0 else 'UVMap' + str(channel)
        mapping = nodes.new('ShaderNodeMapping')
        coords = textures.get('_BaseColorMap', d) if base_uv else d
        mapping.inputs['Scale'].default_value = (*coords.get('Scale', [1, 1]), 1)
        mapping.inputs['Location'].default_value = (*coords.get('Offset', [0, 0]), 0)
        links.new(uv.outputs['UV'], mapping.inputs['Vector']); links.new(mapping.outputs['Vector'], n.inputs['Vector'])
        loaded[prop] = n
        return n
    def split(n):
        out = nodes.new('ShaderNodeSeparateColor'); links.new(n.outputs['Color'], out.inputs[0])
        return out.outputs
    def remap(value, low, high):
        return math_node('ADD', math_node('MULTIPLY', value, high-low), low)
    base = next((p for p in ('_BaseColorMap','_MainTex','_BaseMap') if p in textures), None)
    color_output = multiply_color(tex(base, True).outputs['Color'], definition['Color']) if base else None
    alpha = tex(base, True).outputs['Alpha'] if base else None
    mask_channels = None
    roughness = definition['Roughness']
    if '_MaskMap' in textures:
        mask = tex('_MaskMap', base_uv=True); mask_channels = split(mask)
        links.new(remap(mask_channels[0], floats.get('_MetallicRemapMin',0), floats.get('_MetallicRemapMax',1)), shader.inputs['Metallic'])
        smooth = remap(mask.outputs['Alpha'], floats.get('_SmoothnessRemapMin',0), floats.get('_SmoothnessRemapMax',1))
        roughness = math_node('SUBTRACT',1,smooth)
        ao = remap(mask_channels[1],floats.get('_AORemapMin',0),floats.get('_AORemapMax',1))
        color_output = multiply_color(color_output or definition['Color'],ao)
    elif '_MetallicGlossMap' in textures:
        metal=tex('_MetallicGlossMap',base_uv=True)
        links.new(split(metal)[0],shader.inputs['Metallic'])
        roughness = math_node('SUBTRACT',1,math_node('MULTIPLY',metal.outputs['Alpha'],floats.get('_GlossMapScale',1)))
    if '_OcclusionMap' in textures and mask_channels is None:
        ao=remap(split(tex('_OcclusionMap',base_uv=True))[1],1-floats.get('_OcclusionStrength',1),1)
        color_output=multiply_color(color_output or definition['Color'],ao)
    def normal(n, detail=False, strength=1):
        channels=split(n)
        x=n.outputs['Alpha'] if detail else math_node('MULTIPLY',channels[0],n.outputs['Alpha'])
        x=math_node('SUBTRACT',math_node('MULTIPLY',x,2),1)
        y=math_node('SUBTRACT',math_node('MULTIPLY',channels[1],2),1)
        z=math_node('SQRT',math_node('MAXIMUM',math_node('SUBTRACT',1,math_node('ADD',math_node('MULTIPLY',x,x),math_node('MULTIPLY',y,y))),0))
        rgb=nodes.new('ShaderNodeCombineColor')
        for output,slot in zip((x,y,z),rgb.inputs): links.new(remap(output,0.5,1),slot)
        nm=nodes.new('ShaderNodeNormalMap')
        if isinstance(strength,(int,float)): nm.inputs['Strength'].default_value=strength
        else: links.new(strength,nm.inputs['Strength'])
        links.new(rgb.outputs[0],nm.inputs['Color']); return nm.outputs['Normal']
    normal_prop=next((p for p in ('_NormalMap','_BumpMap') if p in textures),None)
    normal_output=normal(tex(normal_prop,base_uv=True),strength=floats.get('_NormalScale',floats.get('_BumpScale',1))) if normal_prop else None
    if '_DetailMap' in textures and floats.get('_EnableDetail',1):
        detail=tex('_DetailMap'); channels=split(detail)
        weight=mask_channels[2] if mask_channels else 1
        detail_factor=math_node('ADD',1,math_node('MULTIPLY',math_node('SUBTRACT',math_node('MULTIPLY',channels[0],2),1),math_node('MULTIPLY',weight,floats.get('_DetailAlbedoScale',1))))
        color_output=multiply_color(color_output or definition['Color'],detail_factor)
        delta=math_node('MULTIPLY',math_node('SUBTRACT',math_node('MULTIPLY',channels[2],2),1),math_node('MULTIPLY',weight,floats.get('_DetailSmoothnessScale',1)))
        roughness=math_node('MINIMUM',math_node('MAXIMUM',math_node('SUBTRACT',roughness,delta),0),1)
        dn=normal(detail,True,math_node('MULTIPLY',weight,floats.get('_DetailNormalScale',1)))
        if normal_output:
            geometry=nodes.new('ShaderNodeNewGeometry')
            perturbation=nodes.new('ShaderNodeVectorMath');perturbation.operation='SUBTRACT';links.new(dn,perturbation.inputs[0]);links.new(geometry.outputs['Normal'],perturbation.inputs[1])
            mix=nodes.new('ShaderNodeVectorMath');mix.operation='ADD';links.new(normal_output,mix.inputs[0]);links.new(perturbation.outputs[0],mix.inputs[1])
            norm=nodes.new('ShaderNodeVectorMath');norm.operation='NORMALIZE';links.new(mix.outputs[0],norm.inputs[0]);normal_output=norm.outputs[0]
        else: normal_output=dn
    if normal_output: links.new(normal_output,shader.inputs['Normal'])
    if not isinstance(roughness,(int,float)): links.new(roughness,shader.inputs['Roughness'])
    if color_output: links.new(color_output,shader.inputs['Base Color'])
    if floats.get('_SurfaceType',0) or floats.get('_AlphaCutoffEnable',0) or floats.get('_Mode',0):
        opacity=math_node('MULTIPLY',alpha,definition['Color'][3]) if alpha else definition['Color'][3]
        if floats.get('_AlphaCutoffEnable',0): opacity=math_node('GREATER_THAN',opacity,floats.get('_AlphaCutoff',0.5))
        if isinstance(opacity,(int,float)): shader.inputs['Alpha'].default_value=opacity
        else: links.new(opacity,shader.inputs['Alpha'])
    emission=colors.get('_EmissiveColor',colors.get('_EmissionColor',{}))
    emission_color=[emission.get(c,0) for c in 'rgb']+[1]
    ep=next((p for p in ('_EmissiveColorMap','_EmissionMap') if p in textures),None)
    if max(emission_color[:3])>0:
        shader.inputs['Emission Strength'].default_value=1
        if ep: links.new(multiply_color(tex(ep,True).outputs['Color'],emission_color),shader.inputs['Emission Color'])
        else: shader.inputs['Emission Color'].default_value=emission_color
    # Preserve every referenced image, including unsupported custom shader inputs.
    for prop in textures:
        if prop not in loaded: tex(prop)
    materials[key] = material
objects = []
for node in source['Nodes']:
    mesh = None
    if 'Mesh' in node:
        data = source['Meshes'][node['Mesh']]
        mesh = bpy.data.meshes.new(data['Name'])
        mesh.from_pydata([(v[0], v[2], v[1]) for v in data['Vertices']], [], data['Faces'])
        mesh.update()
        for key in node.get('Materials', []):
            mesh.materials.append(materials.get(key))
        for index, coords in enumerate([data['UV']] + data.get('ExtraUV', [])):
            if len(coords) != len(mesh.vertices): continue
            layer = mesh.uv_layers.new(name='UVMap' if index == 0 else 'UVMap' + str(index))
            for loop in mesh.loops:
                layer.data[loop.index].uv = coords[loop.vertex_index][:2]
        for polygon, slot in zip(mesh.polygons, data['Slots']):
            polygon.material_index = min(slot, max(0, len(mesh.materials)-1))
            polygon.use_smooth = True
        if len(data.get('Normals', [])) == len(mesh.vertices):
            mesh.normals_split_custom_set_from_vertices([(n[0], n[2], n[1]) for n in data['Normals']])
    obj = bpy.data.objects.new(node['Name'], mesh)
    bpy.context.scene.collection.objects.link(obj)
    if node['Parent'] is not None:
        obj.parent = objects[node['Parent']]
    p, q, s = node['Position'], node['Rotation'], node['Scale']
    unity_matrix = Matrix.LocRotScale(Vector(tuple(p[k] for k in 'xyz')), Quaternion(tuple(q[k] for k in 'wxyz')), Vector(tuple(s[k] for k in 'xyz')))
    obj.matrix_basis = basis @ unity_matrix @ basis
    objects.append(obj)
if not any(o.type == 'MESH' and len(o.data.polygons) for o in objects):
    raise RuntimeError('The part contains no exportable mesh geometry.')
bpy.context.scene.unit_settings.system = 'METRIC'
bpy.ops.wm.save_as_mainfile(filepath=str(folder / (name + '.blend')))
if request.get('Format') == 'SOURCE':
    bpy.ops.wm.obj_export(filepath=str(folder / (name + '.obj')), path_mode='RELATIVE', export_materials=True)
