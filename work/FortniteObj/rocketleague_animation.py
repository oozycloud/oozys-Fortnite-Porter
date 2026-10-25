"""Attach decoded UEViewer MD5 animation tracks to the matching glTF skeleton."""
import json,re,math,struct
from pathlib import Path

def attach_animation(model,animation,destination,rate=1,delay=0):
 text=Path(animation).read_text();fps=float(re.search(r'frameRate\s+([\d.]+)',text)[1]);frames=int(re.search(r'numFrames\s+(\d+)',text)[1])
 hierarchy=re.search(r'hierarchy\s*\{(.*?)\}',text,re.S)[1]
 bones=re.findall(r'"([^"]+)"\s+(-?\d+)\s+(\d+)\s+(\d+)',hierarchy)
 samples=[list(map(float,body.split())) for _,body in re.findall(r'frame\s+(\d+)\s*\{(.*?)\}',text,re.S)]
 if len(samples)!=frames or any(int(flags)!=63 for _,_,flags,_ in bones):raise ValueError('Unsupported or truncated native animation tracks.')
 if not math.isfinite(rate) or rate<=0:raise ValueError('Animation rate must be positive.')
 doc=json.loads(Path(model).read_text());doc.pop('animations',None);nodes={n.get('name'):i for i,n in enumerate(doc['nodes'])};data=bytearray();channels=[];samplers=[]
 def accessor(values,kind,width,minimum=None,maximum=None):
  offset=len(data);data.extend(struct.pack('<'+'f'*len(values),*values))
  view=len(doc.setdefault('bufferViews',[]));doc['bufferViews'].append({'buffer':len(doc.get('buffers',[])),'byteOffset':offset,'byteLength':len(data)-offset})
  a={'bufferView':view,'componentType':5126,'count':len(values)//width,'type':kind}
  if minimum is not None:a.update(min=minimum,max=maximum)
  index=len(doc.setdefault('accessors',[]));doc['accessors'].append(a);return index
 times=[delay+i/fps/rate for i in range(frames)];timeindex=accessor(times,'SCALAR',1,[times[0]],[times[-1]])
 for name,_,_,start in bones:
  if name not in nodes:continue
  start=int(start);positions=[];rotations=[]
  for frame in samples:
   if start+6>len(frame):raise ValueError('Truncated animation frame.')
   x,y,z,qx,qy,qz=frame[start:start+6];qw=math.sqrt(max(0,1-qx*qx-qy*qy-qz*qz))
   positions.extend((x*.01,z*.01,-y*.01));rotations.extend((qx,qz,-qy,qw))
  for path,values,kind,width in [('translation',positions,'VEC3',3),('rotation',rotations,'VEC4',4)]:
   output=accessor(values,kind,width);sampler=len(samplers);samplers.append({'input':timeindex,'output':output,'interpolation':'LINEAR'});channels.append({'sampler':sampler,'target':{'node':nodes[name],'path':path}})
 if not channels:raise ValueError('Animation has no bones matching this mesh.')
 destination=Path(destination);binary=destination.with_suffix('.anim.bin');binary.write_bytes(data)
 doc.setdefault('buffers',[]).append({'uri':binary.name,'byteLength':len(data)});doc['animations']=[{'name':Path(animation).stem,'channels':channels,'samplers':samplers}]
 destination.write_text(json.dumps(doc),encoding='utf-8')
 return {'Frames':frames,'Duration':times[-1],'Tracks':len(channels)//2}
