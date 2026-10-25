"""Typed UE3 property reader for payloads decoded by the native package extractor."""
import re,struct,json,math
from pathlib import Path
TYPES={'IntProperty','FloatProperty','BoolProperty','ObjectProperty','ClassProperty','NameProperty','StrProperty','StructProperty','ArrayProperty','ByteProperty','DelegateProperty','InterfaceProperty','MapProperty','QWordProperty'}
class PropertyError(ValueError):pass
class Package:
 def __init__(self,root):
  self.root=Path(root);self.name=self.root.name
  self.names={int(i):name for i,name in re.findall(r'^(\d+) = "(.*)"$',(self.root/'NameTable.txt').read_text(encoding='utf-8',errors='replace'),re.M)}
  def table(path):
   return {int(i):{'Class':cls,'Path':name,'Name':name.rsplit('.',1)[-1]} for i,cls,name in re.findall(r"^(\d+) = ([^']+)'(.*)'$",path.read_text(encoding='utf-8',errors='replace'),re.M)}
  self.exports=table(self.root/('Objects.txt' if (self.root/'Objects.txt').exists() else 'ExportTable.txt'));self.imports=table(self.root/'ImportTable.txt');self.objects={}
  links=self.root/'ObjectLinks.txt'
  if links.exists():
   for line in links.read_text().splitlines():
    index,outer,archetype,flags=map(int,line.split())
    entry=(self.exports.get(index-1) if index>0 else self.imports.get(-index-1))
    if entry is not None:entry.update(Outer=outer,Archetype=archetype,Flags=flags)
   # ImportTable's legacy display omits intermediate groups. Rebuild the full path.
   def import_path(index,seen):
    if index in seen:raise PropertyError('Cyclic import outer')
    entry=self.imports[-index-1];outer=entry.get('Outer',0)
    prefix=import_path(outer,seen|{index}) if outer<0 else self.exports.get(outer-1,{}).get('Path','')
    return (prefix+'.' if prefix else '')+entry['Name']
   for index,entry in self.imports.items():entry['Path']=import_path(-index-1,set())
  for p in self.root.rglob('*'):
   if p.is_file() and p.suffix not in ['.txt','.json']:
    path=p.relative_to(self.root).with_suffix('').as_posix().replace('/','.')
    matches=([int(p.stem)-1] if p.stem.isdigit() and int(p.stem)-1 in self.exports else [i for i,o in self.exports.items() if o['Path']==path and o['Class']==p.suffix[1:]])
    if len(matches)==1:
     raw=p.read_bytes();props,offset,end=self.object_properties(raw);self.objects[matches[0]+1]=dict(self.exports[matches[0]],Index=matches[0]+1,Properties=props,PropertyOffset=offset,TailSize=len(raw)-end,File=str(p))
 def inherited(self,index,seen=None):
  seen=set() if seen is None else seen
  if index in seen:raise PropertyError('Cyclic archetype')
  obj=self.objects.get(index)
  if not obj:return {}
  parent=obj.get('Archetype',0)
  values=self.inherited(parent,seen|{index}) if parent>0 else {}
  return {**values,**obj['Properties']}
 def fname(self,data,offset):
  if offset+8>len(data):raise PropertyError('Truncated name')
  index,number=struct.unpack_from('<ii',data,offset)
  if index not in self.names or number<0 or number>1000000:raise PropertyError('Invalid name')
  return self.names[index]+('_'+str(number-1) if number else '')
 def reference(self,index):
  if index==0:return None
  source=self.exports.get(index-1) if index>0 else self.imports.get(-index-1)
  return {'Index':index,**source} if source else {'Index':index,'Unresolved':True}
 def properties(self,data,start=0,depth=0):
  if depth>20:raise PropertyError('Property nesting limit')
  offset=start;result={}
  while offset+8<=len(data):
   name=self.fname(data,offset);offset+=8
   if name=='None':return result,offset
   typ=self.fname(data,offset);offset+=8
   if typ not in TYPES:raise PropertyError('Unknown property type '+typ)
   if offset+8>len(data):raise PropertyError('Truncated tag')
   size,array=struct.unpack_from('<ii',data,offset);offset+=8
   if size<0 or size>len(data) or array<0 or array>100000:raise PropertyError('Invalid tag size')
   subtype=None
   if typ in ['StructProperty','ByteProperty']:subtype=self.fname(data,offset);offset+=8
   if typ=='BoolProperty':value=bool(data[offset]);offset+=1
   else:
    if offset+size>len(data):raise PropertyError('Truncated value')
    value=self.value(typ,subtype,data[offset:offset+size],name,depth+1);offset+=size
   key=name if array==0 else name+'['+str(array)+']'
   result[key]=value
  raise PropertyError('Missing property terminator')
 def value(self,typ,subtype,raw,name,depth):
  if typ=='IntProperty' and len(raw)==4:return struct.unpack('<i',raw)[0]
  if typ=='FloatProperty' and len(raw)==4:
   value=struct.unpack('<f',raw)[0]
   if not math.isfinite(value):raise PropertyError('Nonfinite scalar')
   return value
  if typ in ['ObjectProperty','ClassProperty'] and len(raw)==4:return self.reference(struct.unpack('<i',raw)[0])
  if typ=='NameProperty' and len(raw)==8:return self.fname(raw,0)
  if typ=='ByteProperty':return self.fname(raw,0) if len(raw)==8 else raw[0] if raw else None
  if typ=='QWordProperty' and len(raw)==8:return struct.unpack('<Q',raw)[0]
  if typ=='StrProperty' and len(raw)>=4:
   length=struct.unpack_from('<i',raw)[0]
   return raw[4:4+abs(length)*(2 if length<0 else 1)].decode('utf-16-le' if length<0 else 'utf-8',errors='replace').rstrip('\0')
  if typ=='StructProperty':
   formats={'Vector':('fff',['X','Y','Z']),'Vector2D':('ff',['X','Y']),'Rotator':('iii',['Pitch','Yaw','Roll']),'Quat':('ffff',['X','Y','Z','W']),'LinearColor':('ffff',['R','G','B','A']),'Color':('BBBB',['B','G','R','A'])}
   if subtype in formats:
    fmt,keys=formats[subtype]
    if len(raw)==struct.calcsize('<'+fmt):return dict(zip(keys,struct.unpack('<'+fmt,raw)))
   try:
    p,end=self.properties(raw,0,depth)
    if end==len(raw):return p
   except (PropertyError,struct.error,IndexError):pass
   # UE3 distribution structs begin with an object reference followed by cooked lookup data.
   if subtype and subtype.startswith('RawDistribution') and len(raw)>=4:return {'Distribution':self.reference(struct.unpack_from('<i',raw)[0]),'Type':subtype}
   return {'Type':subtype,'Raw':raw.hex()}
  if typ=='ArrayProperty' and len(raw)>=4:
   count=struct.unpack_from('<i',raw)[0]
   if count<0 or count>1000000:raise PropertyError('Invalid array count')
   body=raw[4:]
   if count==0:return []
   if name in {'LookupTable','LODDistances'} and len(body)==4*count:
    return list(struct.unpack('<'+'f'*count,body))
   try:
    offset=0;items=[]
    for i in range(count):item,offset=self.properties(body,offset,depth);items.append(item)
    if offset==len(body):return items
   except (PropertyError,struct.error,IndexError):pass
   if len(body)==4*count:
    vals=struct.unpack('<'+'i'*count,body)
    if name in {'Materials','Emitters','LODLevels','Modules','SpawnModules','UpdateModules','OrbitModules','Components','Actors','StaticMeshComponents','InstanceComponents','StreamingLevels','MeshMaterials'} or all(v==0 or (v-1 in self.exports if v>0 else -v-1 in self.imports) for v in vals):return [self.reference(v) for v in vals]
    return list(vals)
   return {'Count':count,'Raw':body.hex()}
  return {'Type':typ,'Raw':raw.hex()}
 def object_properties(self,data):
  candidates=[]
  for offset in range(min(128,len(data))):
   try:
    first=self.fname(data,offset);typ=self.fname(data,offset+8)
    if first=='None' or typ not in TYPES:continue
    p,end=self.properties(data,offset)
    if p:candidates.append((len(p),p,offset,end))
   except (PropertyError,struct.error,IndexError):pass
  if candidates:
   # A nested tagged struct may have more fields than its owning UObject.
   # The earliest valid stream is the object's stream, not the largest nested one.
   _,p,offset,end=min(candidates,key=lambda x:x[2]);return p,offset,end
  return {},0,0
 def document(self):return {'Package':self.name,'Objects':list(self.objects.values())}
if __name__=='__main__':
 import sys
 p=Package(sys.argv[1]);Path(sys.argv[2]).write_text(json.dumps(p.document(),indent=2),encoding='utf-8');print(len(p.objects),'objects;',sum(bool(o['Properties']) for o in p.objects.values()),'parsed property lists')
