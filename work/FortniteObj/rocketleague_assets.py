"""Rocket League disk catalogue and UEViewer extraction bridge. No game files are modified."""
import csv,json,re,subprocess,sys,urllib.request,base64,time,shutil
from pathlib import Path
BASE=Path(__file__).resolve().parent
TOOLS=BASE/'rocketleague'
CATEGORIES={'Body':'Cars','Decal':'Decals','Wheels':'Wheels','Goal Explosion':'Goal explosions','Rocket Boost':'Boosts','Trail':'Trails','Topper':'Toppers','Antenna':'Antennas','Paint Finish':'Paint finishes','Engine Audio':'Engine audio','Player Banner':'Banners','Avatar Border':'Avatar borders','Player Anthem':'Anthems'}
def save(path,data): Path(path).write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
def cooked(root):
 p=Path(root)
 for x in [p/'TAGame/CookedPCConsole',p/'CookedPCConsole',p]:
  if x.is_dir() and any(x.glob('*.upk')):return x.resolve()
 raise ValueError('Choose the Rocket League installation folder or TAGame/CookedPCConsole.')
def run(args,root,out,log):
 p=subprocess.run([str(TOOLS/'umodel.exe'),*args,'-game=rocketleague','-path='+str(root),'-out='+str(out)],cwd=log.parent,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=0x08000000)
 text=p.stdout.decode('utf-8',errors='replace');log.write_text(text,encoding='utf-8')
 if p.returncode or '*** ERROR:' in text:raise RuntimeError('Rocket League reader failed. '+text[-1800:])
 return text
def refresh_keys(cache,warnings,force=False):
 cache.mkdir(parents=True,exist_ok=True);target=cache/'aes.txt'
 if force or not target.exists() or time.time()-target.stat().st_mtime>3600:
  try:
   req=urllib.request.Request('https://raw.githubusercontent.com/ShinyEmii/Toga-Files/master/aes.txt',headers={'User-Agent':'oozys-Fortnite-Porter'})
   raw=urllib.request.urlopen(req,timeout=15).read(1000000).decode('ascii')
   keys=[s.strip() for s in raw.splitlines() if s.strip()]
   if not keys or any(len(base64.b64decode(s,validate=True))!=32 for s in keys):raise ValueError('Invalid key list')
   temp=cache/'aes.tmp';temp.write_text('\n'.join(keys),encoding='ascii');temp.replace(target)
  except Exception: warnings.append('Key refresh unavailable; using the last saved Rocket League keys.')
 if not target.exists():target.write_bytes((TOOLS/'aes.txt').read_bytes())
 return target
def scan(q):
 root=cooked(q['Root']);meta=json.loads((TOOLS/'catalog-metadata.json').read_text(encoding='utf-8'));files={p.stem.lower():p for p in root.glob('*.upk')};assets=[];styles={};used=set();products={}
 for p in meta['Products']:
  slot=CATEGORIES.get(p['Slot']);f=files.get((p['Package']+'_SF').lower()) or files.get(p['Package'].lower())
  if not slot or not f:continue
  label=p['Label'] if p['Label'] and not p['Label'].startswith('?INT?') else p['Name'].replace('_',' ')
  a={'Id':'RL'+p['ID'],'Name':label,'Category':slot,'ObjectPath':str(f),'PackagePath':str(root),'Availability':'Source resources' if slot in ['Goal explosions','Boosts','Trails','Engine audio','Anthems','Banners','Avatar borders','Paint finishes'] else 'Mesh and textures'}
  if slot=='Goal explosions':a['Availability']='Effect resources; Unreal simulation unavailable'
  assets.append(a);products[a['Id']]=p;used.add(f.stem.lower())
 for a in [x for x in assets if x['Category']=='Cars']:
  name=a['Name'].casefold();token=products[a['Id']]['Name'].removeprefix('Body_').casefold()
  compatible=[]
  for d in [x for x in assets if x['Category']=='Decals']:
   p=products[d['Id']];label=d['Name'];prefix=label.split(':',1)[0].strip().casefold() if ':' in label else None
   # Car-specific labels are authoritative. Generic legacy black-market decals are universal.
   if prefix==name or (prefix is None and p['Name'].casefold().startswith('skin_'+token+'_')) or (prefix is None and p['Quality']=='Black Market' and name in {'octane','breakout','backfire','paladin','road hog','gizmo','x-devil','merc','venom','hotshot','fennec','dominus','takumi'}):
    compatible.append({'Id':d['Id'],'Name':label,'Package':d['ObjectPath']})
  styles[a['Id']]=sorted(compatible,key=lambda x:(x['Name'].casefold(),x['Id']))
 for m in meta['Maps']:
  f=files.get(m['Name'].lower())
  if f:
   assets.append({'Id':'RLM_'+f.stem,'Name':m['Label'] or f.stem,'Category':'Maps','ObjectPath':str(f),'PackagePath':str(root),'Availability':'Mesh library; level placement unavailable'});used.add(f.stem.lower())
 # New packages remain discoverable even when public product metadata trails a game update.
 prefixes=[('body_','Cars'),('goalexplosion_','Goal explosions'),('explosion_','Goal explosions'),('wheel_','Wheels'),('boost_','Boosts'),('trail_','Trails'),('topper_','Toppers'),('antenna_','Antennas'),('skin_','Decals')]
 for key,f in files.items():
  if key in used or not key.endswith('_sf') or key.endswith('_t_sf'):continue
  category=next((v for k,v in prefixes if key.startswith(k)),None)
  if category:
   assets.append({'Id':'RLP_'+f.stem,'Name':f.stem[:-3].replace('_',' '),'Category':category,'ObjectPath':str(f),'PackagePath':str(root),'Availability':'Unindexed package; compatibility not verified'})
 # Balls live in game-mode packages rather than the product database.
 ball_packages=sorted([p for k,p in files.items() if k.startswith('gameinfo_') and not k.endswith('_t_sf')],key=lambda p:(p.stem.casefold()!='gameinfo_soccar_sf',p.stem.casefold()))
 seenballs=set()
 cache=Path(q['Cache']);warnings=[];refresh_keys(cache,warnings)
 for i,f in enumerate(ball_packages):
  print(f'Reading Rocket League balls {i+1}/{len(ball_packages)}',flush=True)
  try:
   text=run(['-list',f.stem],root,cache,cache/'ball-list.log')
   for match in re.finditer(r'^\s*\d+\s+[0-9A-F]+\s+[0-9A-F]+\s+(StaticMesh|SkeletalMesh)\s+(\S+)',text,re.M|re.I):
    cls,obj=match.groups()
    if re.search('ball|puck|football|cube|egg',obj,re.I) and not re.search('indicator|highlight|sprite|sphere|timer|lightrays|visualizer|pieces|lines',obj,re.I) and obj.casefold() not in seenballs:
     seenballs.add(obj.casefold())
     assets.append({'Id':'RLB_'+f.stem+'_'+obj,'Name':re.sub(r'[_]+',' ',obj),'Category':'Balls','ObjectPath':str(f)+'|'+obj+'|'+cls,'PackagePath':str(root),'Availability':'Mesh and textures'})
  except Exception as ex:warnings.append(f.name+': '+str(ex))
 # Stable unique IDs and known package paths; no missing products are fabricated.
 unique={a['Id']:a for a in assets};save(q['Result'],{'Assets':sorted(unique.values(),key=lambda a:(a['Category'],a['Name'].casefold(),a['Id'])),'Styles':styles,'Warnings':warnings})
def export(q):
 a=q['Asset'];root=cooked(a['PackagePath']);out=Path(q['Output']);cache=Path(q['Cache']);warnings=[];key=refresh_keys(cache,warnings)
 (out/'aes.txt').write_bytes(key.read_bytes())
 parts=a['ObjectPath'].split('|');f=Path(parts[0]).resolve()
 if f.parent!=root or f.suffix.lower()!='.upk' or not f.is_file():raise ValueError('Asset package is not in this Rocket League installation.')
 # Octane's standard body is cooked into Startup; its product package contains the premium-UV alternate.
 if a['Id']=='RL23':parts=[str(root/'Startup.upk'),'Body_Octane_SK','SkeletalMesh'];f=root/'Startup.upk'
 args=['-export','-gltf','-png','-noanim',f.stem,*parts[1:]]
 text=run(args,root,out/'resources',out/'reader.log')
 if len(parts)>1:
  dependencies={}
  for cls,obj,pkg in re.findall(r'Loading (Texture2D|Material3|MaterialInstanceConstant) (\S+) from package (\S+)\.upk',text):
   dependencies.setdefault(pkg,set()).add(obj)
  for i,(pkg,objects) in enumerate(dependencies.items()):
   if (root/(pkg+'.upk')).exists():
    text+=run(['-export','-png','-noanim',*['-obj='+obj for obj in sorted(objects)],pkg],root,out/'resources',out/('dependency-'+str(i)+'.log'))
 if q.get('Wheel'):
  wheel=Path(q['Wheel']['ObjectPath']).resolve()
  if wheel.parent!=root or not wheel.is_file():raise ValueError('Wheel package is missing.')
  text+=run(['-export','-gltf','-png','-noanim',wheel.stem],root,out/'wheels',out/'wheel-reader.log')
  run(['-dump','-all',wheel.stem],root,out/'wheels',out/'wheel-properties.txt')
 if q.get('Decal'):
  decal=Path(q['Decal']['Package']).resolve()
  if decal.parent!=root or not decal.is_file():raise ValueError('Decal package is missing.')
  text+=run(['-export','-gltf','-png','-noanim',decal.stem],root,out/'decal',out/'decal-reader.log')
  run(['-dump','-all',decal.stem],root,out/'decal',out/'decal-properties.txt')
 if a['Category'] in ['Goal explosions','Boosts','Trails','Maps']:
  source=out/'source';source.mkdir(exist_ok=True);shutil.copy2(f,source/f.name)
  run(['-list',f.stem],root,out/'resources',out/'package-objects.txt')
  warnings.append('Unreal level placement and particle/gameplay simulation are not reconstructed. This export contains available meshes, textures and material resources.')
 if 'WARNING:' in text:warnings.append('The package reader reported unresolved dependencies. See reader.log for details.')
 meshes=list((out/'resources').rglob('*.gltf'));textures=list(out.rglob('*.png'))
 if q.get('Preview') and not meshes:
  thumbnail=root/(re.sub('_SF$','',f.stem,flags=re.I)+'_T_SF.upk')
  if thumbnail.exists():
   try:
    run(['-export','-png','-nomesh','-nostat',thumbnail.stem],root,out/'thumbnail',out/'thumbnail-reader.log')
    candidates=list((out/'thumbnail').rglob('*.png'))
    if candidates:shutil.copy2(max(candidates,key=lambda p:p.stat().st_size),out/'preview.png')
   except Exception as ex:warnings.append('Thumbnail unavailable: '+str(ex))
 if not meshes:warnings.append('This package has no directly exportable mesh. Texture/material resources are retained; no Blender scene is fabricated.')
 (out/'aes.txt').unlink(missing_ok=True)
 save(q['Result'],{'Meshes':len(meshes),'Textures':len(textures),'Warnings':warnings,'ResourceOnly':not meshes})
if __name__=='__main__':
 try:
  q=json.loads(Path(sys.argv[1]).read_text(encoding='utf-8-sig'))
  if q['Action']=='capabilities':
   asset=q['Asset'];root=cooked(asset['PackagePath']);stage=Path(q['Cache']);warnings=[];refresh_keys(stage,warnings)
   text=run(['-dump','-all',Path(asset['ObjectPath'].split('|')[0]).stem],root,stage,stage/('details-'+asset['Id']+'.log'))
   save(q['Result'],{'Paint':bool(re.search(r'ParameterName = PaintColor\b',text)),'PaintFinish':asset['Category']=='Cars'})
  elif q['Action']=='keys':
   warnings=[];refresh_keys(Path(q['Cache']),warnings,True);print('Rocket League compatibility refreshed.' if not warnings else warnings[0],flush=True)
  else:(scan if q['Action']=='scan' else export)(q)
 except Exception as ex:print(str(ex),file=sys.stderr);sys.exit(1)
