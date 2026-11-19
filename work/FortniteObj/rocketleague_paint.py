"""Bake static Rocket League paint masks to portable, correctly coloured textures."""
import re
from pathlib import Path
import numpy as np

FINISHES={'Glossy':(.23,.05),'Matte':(.83,0),'Metallic':(.27,.7),'Pearlescent':(.22,.22),'Anodized':(.2,.85),'Anodized Pearl':(.2,.85),'Brushed Metal':(.38,.85),'Carbon Fiber':(.35,.5)}

def apply(mat,shader,images,values,skinfile,shaderdata,q,primary,secondary,paints,colour,bpy,warnings):
 nodes=mat.node_tree.nodes;links=mat.node_tree.links;params=shaderdata.get('Parameters',{});appearance=q.get('Appearance') or {};decal=q.get('Decal');diffuse=images.get(values.get('Diffuse','').casefold());decaldiff=params.get('Diffuse');decalpath=images.get(decaldiff.get('Name','').casefold()) if isinstance(decaldiff,dict) else None
 skinref=params.get('Skin')
 if isinstance(skinref,dict):skinfile=images.get(skinref.get('Name','').casefold()) or skinfile
 # A material's inherited Diffuse is normally the unpainted car, NOT a finished decal.
 authored=params.get('1_Diffuse_Skin') or params.get('Diffuse_Skin') or params.get('DecalDiffuse')
 full_livery=images.get(authored.get('Name','').casefold()) if isinstance(authored,dict) else None
 full_livery=full_livery or (decalpath if decal and not skinfile and isinstance(decaldiff,dict) and re.search(r'(?:^|\.)(?:skin|decal|livery)',decaldiff.get('Path',''),re.I) else None)
 if decal and not skinfile and not full_livery:
  from rocketleague_shader import animated_mask
  animated=animated_mask(mat,images,Path(q['Output']),bpy)
  if animated:
   mix=nodes.new('ShaderNodeMixRGB');mix.inputs[1].default_value=primary;mix.inputs[2].default_value=secondary;links.new(animated,mix.inputs[0]);links.new(mix.outputs[0],shader.inputs['Base Color']);shader.inputs['Roughness'].default_value=FINISHES.get(appearance.get('PrimaryFinish','Glossy'),FINISHES['Glossy'])[0];mat['RocketLeagueDecal']=decal['Name'];return animated
 blank=next((images[v.casefold()] for v in values.values() if 'blankskin' in v.casefold() and v.casefold() in images),None)
 maskpath=skinfile or blank
 locked=bool(decal and (decal.get('FixedColours') or 'esport' in decal.get('Name','').lower() or 'esport' in decal.get('Package','').lower()) and isinstance(params.get('TeamColor'),dict) and isinstance(params.get('CustomColor'),dict))
 def vector(name,default):
  value=params.get(name);return np.array([value.get(c,default[i]) for i,c in enumerate('RGB')],dtype=np.float32) if isinstance(value,dict) else np.array(default[:3],dtype=np.float32)
 first=vector('TeamColor',primary) if locked else np.array(primary[:3],dtype=np.float32);second=vector('CustomColor',secondary) if locked else np.array(secondary[:3],dtype=np.float32);trim=vector('PaintColor',(.03,.03,.03,1))
 chosen=full_livery or diffuse or maskpath
 if chosen is None:
  warnings.append(mat.name+': no body paint texture or source mask was found.');return None
 reference=bpy.data.images.load(str(chosen),check_existing=True);w,h=reference.size
 if not w or not h:_=reference.pixels[0];w,h=reference.size
 # Respect an explicit texture limit; keep native resolution otherwise.
 limit=int(q.get('TextureLimit') or 0)
 if limit and max(w,h)>limit:factor=limit/max(w,h);w=max(1,round(w*factor));h=max(1,round(h*factor))
 def read(path,data=False):
  if path is None:return None
  image=bpy.data.images.load(str(path),check_existing=True)
  if data:image.colorspace_settings.name='Non-Color'
  temporary=None
  if tuple(image.size)!=(w,h):temporary=image.copy();temporary.scale(w,h);image=temporary
  pixels=np.empty(w*h*4,dtype=np.float32);image.pixels.foreach_get(pixels)
  if temporary:bpy.data.images.remove(temporary)
  return pixels.reshape(h,w,4)
 base=read(full_livery or diffuse);mask=read(maskpath,True)
 trim_data=read(blank,True) if blank and appearance.get('BodyPaint') in paints else None
 if base is None:base=np.ones((h,w,4),dtype=np.float32)
 rough1,metal1=FINISHES.get(appearance.get('PrimaryFinish','Glossy'),FINISHES['Glossy']);rough2,metal2=FINISHES.get(appearance.get('SecondaryFinish','Glossy'),FINISHES['Glossy'])
 # Car paint has a clear coat; matte finishes deliberately omit it.
 shader.inputs['Coat Weight'].default_value=0 if appearance.get('PrimaryFinish')=='Matte' else .35
 shader.inputs['Coat Roughness'].default_value=.14
 output=np.ones((h,w,4),dtype=np.float32);surface=np.ones((h,w,4),dtype=np.float32)
 for start in range(0,h,128):
  stop=min(h,start+128);raw=base[start:stop,:,:3];linear=np.where(raw<=.04045,raw/12.92,((raw+.055)/1.055)**2.4);paint=linear.copy();detail=np.zeros(linear.shape[:2],dtype=np.float32)
  if not full_livery:
   packed=mask[start:stop] if mask is not None else None
   if packed is None:paint=linear*first
   else:
    red,green,blue,alpha=[packed[:,:,i:i+1] for i in range(4)]
    paint=linear*(1-red)+linear*first*red
    if decal:
     paint=paint*(1-green)+linear*second*green;paint=paint*(1-blue)+linear*trim*blue;detail=green[:,:,0]
     if locked:paint=paint*(1-alpha)+linear*second*alpha;detail=np.maximum(detail,alpha[:,:,0])
  if trim_data is not None:
   trim_mask=trim_data[start:stop];region=np.clip(1-trim_mask[:,:,:1]-trim_mask[:,:,2:3],0,1)
   paint=paint*(1-region)+linear*np.array(colour(paints[appearance['BodyPaint']])[:3])*region
  output[start:stop,:,:3]=np.where(paint<=.0031308,paint*12.92,1.055*np.maximum(paint,0)**(1/2.4)-.055)
  surface[start:stop,:,1]=rough1*(1-detail)+rough2*detail;surface[start:stop,:,2]=metal1*(1-detail)+metal2*detail
 def texture(label,pixels,noncolor):
  image=bpy.data.images.new(mat.name+' '+label,width=w,height=h,alpha=True);image.colorspace_settings.name='Non-Color' if noncolor else 'sRGB';image.pixels.foreach_set(np.clip(pixels,0,1).ravel());image.filepath_raw=str(Path(q['Output'])/(re.sub(r'[^a-zA-Z0-9_-]','_',mat.name)+'_'+label+'.png'));image.file_format='PNG';image.save();image.pack();node=nodes.new('ShaderNodeTexImage');node.image=image;node.label=label;return node
 baked=texture('paint_colour',output,False);links.new(baked.outputs['Color'],shader.inputs['Base Color']);packed=texture('paint_surface',surface,True);channels=nodes.new('ShaderNodeSeparateColor');links.new(packed.outputs['Color'],channels.inputs[0]);links.new(channels.outputs['Green'],shader.inputs['Roughness']);links.new(channels.outputs['Blue'],shader.inputs['Metallic'])
 mat['RocketPaintBaked']=True;mat['RocketPaintMask']=str(maskpath.name) if maskpath else '';mat['RocketTeamColoursLocked']=locked
 if decal and (skinfile or full_livery):mat['RocketLeagueDecal']=decal['Name']
 if full_livery:mat['RocketLeagueLivery']=full_livery.name
 if trim_data is not None:mat['PaintVariant']=appearance['BodyPaint']
 return None
