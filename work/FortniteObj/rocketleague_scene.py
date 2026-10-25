"""Read authored UE3 level placement; never substitute a grid of unplaced meshes."""
import json, subprocess, re
from pathlib import Path
from rocketleague_properties import Package

LEVEL_CLASSES=['StaticMeshComponent','InstancedStaticMeshComponent','StaticMeshActor','InterpActor','StaticMeshCollectionActor','LevelStreaming*','World','Level']

def extract_properties(package,root,out,tools,classes=LEVEL_CLASSES):
 out=Path(out);out.mkdir(parents=True,exist_ok=True)
 args=[str(tools/'porter-rl-dump.exe'),'-game=rocketleague','-path='+str(root),'-out='+str(out),*['-filter='+c for c in classes],package]
 result=subprocess.run(args,cwd=out.parent,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=0x08000000)
 text=result.stdout.decode('utf-8',errors='replace');(out/(package+'.log')).write_text(text,encoding='utf-8')
 if result.returncode or '*** ERROR:' in text:raise RuntimeError('Level metadata reader failed: '+text[-1200:])
 directory=next((p for p in out.iterdir() if p.is_dir() and p.name.casefold()==package.casefold()),None)
 if directory is None:raise RuntimeError('Level metadata reader returned no package: '+package)
 return Package(directory)

def export_map(start,root,out,tools,run,warnings):
 files={p.stem.casefold():p for p in root.glob('*.upk')};queue=[start];seen=set();placements=[];packages=[];needs={}
 while queue:
  name=queue.pop(0)
  if name.casefold() in seen:continue
  seen.add(name.casefold())
  if len(seen)>128:raise RuntimeError('Map has more than 128 streamed levels; refusing an unbounded traversal.')
  source=files.get(name.casefold())
  if not source:warnings.append('Streamed level is missing: '+name);continue
  print('Reading Rocket League level '+source.stem,flush=True)
  p=extract_properties(source.stem,root,out/'level-data',tools);packages.append(source.stem)
  for obj in p.objects.values():
   props=p.inherited(obj['Index'])
   if obj['Class'].startswith('LevelStreaming'):
    child=props.get('PackageName')
    if isinstance(child,str) and child:queue.append(child)
   if obj['Class'] not in ['StaticMeshComponent','InstancedStaticMeshComponent'] or '.PersistentLevel.' not in obj['Path']:continue
   mesh=props.get('StaticMesh')
   if not mesh:continue # Reflection-capture preview components have no runtime mesh.
   meshpackage=source.stem if mesh['Index']>0 else mesh['Path'].split('.')[0]
   actual=files.get(meshpackage.casefold()) or files.get((meshpackage+'_SF').casefold())
   if actual:meshpackage=actual.stem
   else:
    warnings.append('Placed mesh package is unavailable: '+mesh['Path']);continue
   needs.setdefault(meshpackage,set()).add(mesh['Name'])
   materials=[]
   for mat in props.get('Materials',[]):
    materials.append(mat['Name'] if mat else None)
    if mat:
     pkg=source.stem if mat['Index']>0 else mat['Path'].split('.')[0]
     if pkg.casefold() in files:needs.setdefault(files[pkg.casefold()].stem,set()).add(mat['Name'])
   owner=p.objects.get(obj.get('Outer',0));actor=p.inherited(owner['Index']) if owner and owner['Class']!='StaticMeshCollectionActor' else {}
   placements.append({'Name':source.stem+':'+obj['Path'],'Package':meshpackage,'Mesh':mesh['Name'],'Properties':props,'Actor':actor,'Materials':materials})
 # Export each distinct mesh once. Linked instances retain every authored placement.
 for i,(pkg,names) in enumerate(needs.items()):
  print(f'Reading Rocket League map resources {i+1}/{len(needs)}: {pkg}',flush=True)
  target=out/'resources'/pkg
  text=run(['-export','-gltf','-png','-noanim',*['-obj='+n for n in sorted(names)],pkg],root,target,out/('map-resources-'+str(i)+'.log'))
  dependencies={}
  for cls,obj,dep in re.findall(r'Loading (Texture2D|Material3|MaterialInstanceConstant) (\S+) from package (\S+)\.upk',text):dependencies.setdefault(dep,set()).add(obj)
  for j,(dep,objects) in enumerate(dependencies.items()):
   if dep.casefold() in files:run(['-export','-png','-noanim',*['-obj='+n for n in sorted(objects)],dep],root,target,out/(f'map-dependency-{i}-{j}.log'))
 models=list((out/'resources').rglob('*.gltf'));valid=[]
 for item in placements:
  choices=[p for p in models if p.stem==item['Mesh'] and p.relative_to(out/'resources').parts[0]==item['Package']]
  if len(choices)!=1:warnings.append('Placed mesh could not be extracted: '+item['Name']);continue
  item['Model']=choices[0].relative_to(out).as_posix();valid.append(item)
 if not valid:raise RuntimeError('No placed meshes could be extracted from this map.')
 document={'Packages':packages,'Instances':valid,'UnrealUnitsPerMetre':100,'Warnings':warnings.copy()}
 (out/'level-scene.json').write_text(json.dumps(document,indent=2),encoding='utf-8')
 warnings.append('Map geometry is assembled from authored streamed levels. Unreal lighting, gameplay actors and runtime effects remain source data.')
 return document
