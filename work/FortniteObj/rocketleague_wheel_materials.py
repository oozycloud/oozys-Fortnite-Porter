"""Separate tyre and rim surfaces so standard interchange materials stay correct."""
import math
import numpy as np

def separate(bpy):
 for original in list(bpy.data.materials):
  tyre_name=original.get('WheelTyreTexture');rim_name=original.get('WheelRimTexture')
  if not tyre_name or not rim_name or original.get('WheelSurface'):continue
  if not any(o.type=='MESH' and any(f.material_index<len(o.data.materials) and o.data.materials[f.material_index]==original for f in o.data.polygons) for o in bpy.data.objects):continue
  variants=[]
  for rubber,name in [(False,rim_name),(True,tyre_name)]:
   mat=original.copy();mat.name=original.name+(' tyre' if rubber else ' rim');nodes=mat.node_tree.nodes;links=mat.node_tree.links
   shader=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
   image=next((n for n in nodes if n.type=='TEX_IMAGE' and n.label==name),None)
   if image is None:raise ValueError('Missing wheel surface image '+name)
   # Preserve painted rims in a standard image, not an unsupported mix node.
   tint=next((n for n in nodes if n.type=='MIX_RGB' and n.inputs[1].is_linked and n.inputs[1].links[0].from_node==image and not n.inputs[2].is_linked and abs(n.inputs[0].default_value-.8)<.001),None) if not rubber and original.get('PaintVariant') else None
   if tint:
    source=image.image;pixels=np.empty(len(source.pixels),dtype=np.float32);source.pixels.foreach_get(pixels);pixels=pixels.reshape(-1,4);raw=pixels[:,:3];linear=np.where(raw<=.04045,raw/12.92,((raw+.055)/1.055)**2.4);linear=linear*.2+np.array(tint.inputs[2].default_value[:3])*.8;pixels[:,:3]=np.where(linear<=.0031308,linear*12.92,1.055*np.maximum(linear,0)**(1/2.4)-.055)
    baked=bpy.data.images.new(original.name+' painted rim',width=source.size[0],height=source.size[1],alpha=True);baked.colorspace_settings.name='sRGB';baked.pixels.foreach_set(pixels.ravel());baked.pack();image.image=baked
   links.new(image.outputs['Color'],shader.inputs['Base Color'])
   for socket,value in [('Roughness',.82 if rubber else .42),('Metallic',0 if rubber else .35)]:
    for link in list(shader.inputs[socket].links):links.remove(link)
    shader.inputs[socket].default_value=value
   normal=next((n for n in nodes if n.type=='TEX_IMAGE' and n.label==name[:-2]+'_N'),None)
   for link in list(shader.inputs['Normal'].links):links.remove(link)
   if normal:
    nm=nodes.new('ShaderNodeNormalMap');links.new(normal.outputs['Color'],nm.inputs['Color']);links.new(nm.outputs[0],shader.inputs['Normal'])
   mat['WheelSurface']='Tyre' if rubber else 'Rim';variants.append(mat)
  for obj in list(bpy.data.objects):
   if obj.type!='MESH' or original not in obj.data.materials[:]:continue
   slots=[i for i,m in enumerate(obj.data.materials) if m==original]
   indices=[]
   for mat in variants:obj.data.materials.append(mat);indices.append(len(obj.data.materials)-1)
   attribute=obj.data.color_attributes.active_color
   uv=obj.data.uv_layers.active
   for polygon in obj.data.polygons:
    if polygon.material_index not in slots:continue
    if original.get('WheelTyreSelector')=='VertexColor.R' and attribute:
     values=[attribute.data[v if attribute.domain=='POINT' else loop].color[0] for v,loop in zip(polygon.vertices,polygon.loop_indices)]
    elif uv:values=[float(uv.data[loop].uv.y>1) for loop in polygon.loop_indices]
    else:raise ValueError('Wheel has no authored tyre/rim selector: '+obj.name)
    polygon.material_index=indices[int(sum(values)/len(values)>.5)]
