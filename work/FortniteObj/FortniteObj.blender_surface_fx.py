"""Masked surface animation; no scene-wide glow or additive emission."""
import bpy


def apply_surface_fx(mat, spec, textures, bsdf, out):
    tree = mat.node_tree
    scalars, colors = spec.get('Scalars', {}), spec.get('Colors', {})
    def node(kind, label):
        value = tree.nodes.new(kind); value.label = label
        return value
    def channel(texture, index):
        split = node('ShaderNodeSeparateColor', 'Effect mask channels')
        tree.links.new(texture.outputs['Color'], split.inputs['Color'])
        return split.outputs[index]
    def mix(a, b, factor, label):
        n = node('ShaderNodeMixRGB', label)
        tree.links.new(factor, n.inputs[0])
        for value, socket in ((a,n.inputs[1]), (b,n.inputs[2])):
            if isinstance(value, (tuple,list)): socket.default_value = value
            else: tree.links.new(value, socket)
        return n.outputs[0]
    def pan(texture, speed, scale=1):
        uv = node('ShaderNodeTexCoord', 'Surface coordinates')
        mapping = node('ShaderNodeMapping', 'Animated surface UVs'); mapping.vector_type='POINT'
        mapping.inputs['Scale'].default_value = (scale,scale,1)
        tree.links.new(uv.outputs['UV'], mapping.inputs['Vector'])
        tree.links.new(mapping.outputs[0], texture.inputs['Vector'])
        for axis, velocity in enumerate(speed[:2]):
            curve = mapping.inputs['Location'].driver_add('default_value', axis)
            driver = curve.driver
            for name, path in [('fps','render.fps'),('fps_base','render.fps_base')]:
                var=driver.variables.new(); var.name=name; var.type='SINGLE_PROP'
                var.targets[0].id_type='SCENE'; var.targets[0].id=bpy.context.scene; var.targets[0].data_path=path
            driver.expression=f'(frame-1)*fps_base/fps*{velocity!r}'
        return mapping
    effects=[]
    base = textures.get('BaseColor')
    colour = base.outputs['Color'] if base else spec['Color']
    if 'CustomizationPattern' in textures and all(k in colors for k in ('Primary Color', 'Secondary Color', 'Accent Color')):
        # Packed SkinFX channels: green selects cloth, red places the pattern,
        # blue selects gloves/shoes/trim. Black retains the original material.
        pattern = textures['CustomizationPattern']
        tint = (1, 1, 1, 1)
        for index, parameter in ((1, 'Primary Color'), (0, 'Secondary Color'), (2, 'Accent Color')):
            tint = mix(tint, colors[parameter], channel(pattern, index), parameter)
        detail = node('ShaderNodeMixRGB', 'Cloth colour and texture detail'); detail.blend_type = 'MULTIPLY'
        detail.inputs[0].default_value = 1
        if base: tree.links.new(base.outputs['Color'], detail.inputs[1])
        else: detail.inputs[1].default_value = spec['Color']
        tree.links.new(tint, detail.inputs[2]); colour = detail.outputs[0]
        tree.links.new(colour, bsdf.inputs['Base Color'])
        mat['Customization'] = 'Independent base, pattern and accent colours'
    if 'StyleLightMask' in textures and 'SelectedLightColor' in colors:
        split = node('ShaderNodeSeparateColor', 'Authored light mask')
        tree.links.new(textures['StyleLightMask'].outputs['Color'], split.inputs['Color'])
        mask = split.outputs[0]
        for component in (split.outputs[1], split.outputs[2]):
            maximum = node('ShaderNodeMath', 'Light coverage'); maximum.operation = 'MAXIMUM'
            tree.links.new(mask, maximum.inputs[0]); tree.links.new(component, maximum.inputs[1])
            mask = maximum.outputs[0]
        colour = mix(colour, colors['SelectedLightColor'], mask, 'Selected light colour')
        effects.append(mask)
    if spec.get('Switches', {}).get('Use negative') and 'JacketMask' in textures:
        # The universe master supplies body colour procedurally. Its diffuse
        # map is deliberately black; the red FX channel excludes the coat.
        mask=channel(textures['JacketMask'],0)
        tint=colors.get('Universe_NegativeColor',[0,1,2,1])
        maximum=max(1,max(tint[:3]))
        tint=tuple(c/maximum for c in tint[:3])+(1,)
        edge=node('ShaderNodeFresnel','Universe body contours')
        edge.inputs['IOR'].default_value=1.45
        if bsdf.inputs['Normal'].is_linked:
            tree.links.new(bsdf.inputs['Normal'].links[0].from_socket,edge.inputs['Normal'])
        ramp=node('ShaderNodeValToRGB','Universe cyan body')
        ramp.color_ramp.elements[0].color=tuple(c*.22 for c in tint[:3])+(1,)
        ramp.color_ramp.elements[1].color=tint
        tree.links.new(edge.outputs[0],ramp.inputs[0])
        detail=node('ShaderNodeMixRGB','Preserve body markings');detail.blend_type='SCREEN'
        detail.inputs[0].default_value=1
        tree.links.new(ramp.outputs[0],detail.inputs[1])
        if base:tree.links.new(base.outputs['Color'],detail.inputs[2])
        else:detail.inputs[2].default_value=(0,0,0,1)
        colour=mix(colour,detail.outputs[0],mask,'Universe body only')
        fill=node('ShaderNodeMath','Body surface fill');fill.operation='MULTIPLY';fill.inputs[1].default_value=.3
        tree.links.new(mask,fill.inputs[0]);effects.append(fill.outputs[0])
        mat['Universe body']='Masked cyan contour approximation of the Unreal negative material'
    if 'JacketPattern' in textures and 'JacketMask' in textures:
        pattern=textures['JacketPattern']
        pan(pattern, (scalars.get('Jacket Pan Speed X',0),-scalars.get('Jacket Pan Speed Y',0)))
        picker=colors.get('Jacket channel',[0,1,0,0])
        mask=channel(textures['JacketMask'], max(range(3),key=lambda i:picker[i]))
        pattern_value=channel(pattern,0)
        if 'JacketGradient' in textures:
            atlas=textures['JacketGradient'];atlas.extension='EXTEND'
            uv=node('ShaderNodeCombineXYZ','Authored colour curve row')
            uv.inputs['Y'].default_value=1-(scalars.get('Gradient',0)+.5)/atlas.image.size[1]
            tree.links.new(pattern_value,uv.inputs['X']);tree.links.new(uv.outputs[0],atlas.inputs['Vector'])
            pattern_colour=atlas.outputs['Color']
        else:
            pattern_colour=pattern.outputs['Color']
        colour=mix(colour,pattern_colour,mask,'Animated jacket only')
        effects.append(mask)
    if 'Planet' in textures and 'PlanetColor' in textures:
        planet=textures['PlanetColor']
        pan(planet,(scalars.get('PanX',0),-scalars.get('PanY',0)))
        mask=channel(textures['Planet'],1)
        colour=mix(colour,planet.outputs['Color'],mask,'Rotating planet')
        effects.append(mask)
        # The inner sphere is selected separately from the planet map.
        inner=channel(textures['Planet'],2)
        tint=colors.get('Gem Slight Glow Color',[.1,.6,1,1])
        colour=mix(colour,tint,inner,'Inner sphere colour')
        effects.append(inner)
    if 'Galaxy' in textures and 'GalaxyMask' in textures:
        galaxy=textures['Galaxy']; pan(galaxy,colors.get('Galaxy_PanSpeed',[0,0,0,0]),scalars.get('GalaxyTiling',1))
        # The character master stores its galaxy surface mask in SkinFX green.
        mask=channel(textures['GalaxyMask'],1)
        galaxy_colour=galaxy.outputs['Color']
        if 'Stars' in textures:
            stars=textures['Stars'];pan(stars,colors.get('StarsSmall_PanSpeed',[0,0,0,0]),scalars.get('Small Star Tiling',1))
            galaxy_colour=mix(galaxy_colour,(0.8,0.9,1,1),channel(stars,2),'Stars over nebula')
        colour=mix(colour,galaxy_colour,mask,'Galaxy inside visor')
        effects.append(mask)
    if 'FaceAtlas' in textures:
        atlas=textures['FaceAtlas']; atlas.extension='CLIP'
        count=max(1,round(scalars.get('SubImages',1)))
        frame=max(0,min(count*count-1,round(scalars.get('Default Face',0))))
        scale=max(.001,scalars.get('Flipbook scale',1))
        scale_v=scale*max(.001,scalars.get('Flipbook scale V relative',1))
        uv=node('ShaderNodeTexCoord','Face coordinates')
        local=node('ShaderNodeMapping','Face placement');local.vector_type='POINT'
        local.inputs['Scale'].default_value=(1/scale,1/scale_v,1)
        local.inputs['Location'].default_value=((-.5+scalars.get('Flipbook X',0))/scale+.5,(-.5-scalars.get('Flipbook Y',0))/scale_v+.5,0)
        tree.links.new(uv.outputs['UV'],local.inputs['Vector'])
        # Clip to the chosen cell so neighbouring expressions cannot bleed in.
        sep=node('ShaderNodeSeparateXYZ','Face cell bounds');tree.links.new(local.outputs[0],sep.inputs[0])
        bounds=[]
        for axis in ('X','Y'):
            for operation,limit in [('GREATER_THAN',0),('LESS_THAN',1)]:
                n=node('ShaderNodeMath','Face cell clip');n.operation=operation;n.inputs[1].default_value=limit
                tree.links.new(sep.outputs[axis],n.inputs[0]);bounds.append(n.outputs[0])
        tile=node('ShaderNodeMapping','Authored default expression');tile.vector_type='POINT'
        tile.inputs['Scale'].default_value=(1/count,1/count,1)
        tile.inputs['Location'].default_value=(frame%count/count,1-(frame//count+1)/count,0)
        tree.links.new(local.outputs[0],tile.inputs['Vector']);tree.links.new(tile.outputs[0],atlas.inputs['Vector'])
        mask=channel(atlas,0)
        if 'GalaxyMask' in textures: bounds.append(channel(textures['GalaxyMask'],1))
        for bound in bounds:
            n=node('ShaderNodeMath','Clipped face');n.operation='MULTIPLY'
            tree.links.new(mask,n.inputs[0]);tree.links.new(bound,n.inputs[1]);mask=n.outputs[0]
        tint=colors.get('FlipbookTint',[1,1,1,1]);maximum=max(1,max(tint[:3]))
        colour=mix(colour,tuple(c/maximum for c in tint[:3])+(1,),mask,'Face expression')
        effects.append(mask)
    if not effects: return
    tree.links.new(colour,bsdf.inputs['Base Color'])
    mask=effects[0]
    for extra in effects[1:]:
        n=node('ShaderNodeMath','Combined effect area');n.operation='MAXIMUM'
        tree.links.new(mask,n.inputs[0]);tree.links.new(extra,n.inputs[1]);mask=n.outputs[0]
    ray=node('ShaderNodeLightPath','Camera only')
    camera_mask=node('ShaderNodeMath','Visible surface effect');camera_mask.operation='MULTIPLY'
    tree.links.new(mask,camera_mask.inputs[0]);tree.links.new(ray.outputs['Is Camera Ray'],camera_mask.inputs[1])
    flat=node('ShaderNodeEmission','Surface colour');flat.inputs['Strength'].default_value=1
    tree.links.new(colour,flat.inputs['Color'])
    surface=node('ShaderNodeMixShader','Masked surface colour')
    tree.links.new(camera_mask.outputs[0],surface.inputs[0]);tree.links.new(bsdf.outputs[0],surface.inputs[1]);tree.links.new(flat.outputs[0],surface.inputs[2])
    tree.links.new(surface.outputs[0],out.inputs['Surface'])
    mat['Surface animation']='Authored texture masks and time-based UV movement'
