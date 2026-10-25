"""Recover effect attachments and authored animation timing from cooked FXActor data."""
import json
from pathlib import Path
from rocketleague_scene import extract_properties
from rocketleague_animation import attach_animation

def export_effect(package,root,out,tools,run,warnings):
 p=extract_properties(package,root,out/'effect-data',tools,['FXActor*','Particle*','Distribution*','SkeletalMeshComponent*','AnimNode*','AnimSet','Material*'])
 actors=[o for o in p.objects.values() if o['Class'].startswith('FXActor') and o['Properties'].get('Attachments')]
 # Base archetypes may be forced into the same cooked package. Prefer the derived product actor.
 parents={o['Properties'].get('Parent',{}).get('Index') for o in actors if isinstance(o['Properties'].get('Parent'),dict)}
 actors=[o for o in actors if o['Index'] not in parents]
 attachments=[];particles=[];unsupported=set()
 for actor in actors:
  for a in p.inherited(actor['Index']).get('Attachments',[]):
   ref=a.get('Component') or {};component=p.objects.get(ref.get('Index'))
   if not component:unsupported.add(ref.get('Class','Unknown component'));continue
   props=p.inherited(component['Index']);delay=max(0,a.get('AttachDelay',0));mesh=props.get('SkeletalMesh')
   if mesh:
    models=[f for f in (out/'resources').rglob('*.gltf') if f.stem==mesh['Name']]
    if not models:warnings.append('Effect skeletal mesh missing: '+mesh['Path']);continue
    animnode=props.get('Animations') or {};node=p.inherited(animnode.get('Index',0));sequence=node.get('AnimSeqName')
    sets=props.get('AnimSets',[])
    for animset in sets:
     run(['-export','-md5','-nomesh','-nostat','-notex',package,animset['Name'],'AnimSet'],root,out/'animations',out/('animation-'+animset['Name']+'.log'))
    animations=[f for f in (out/'animations').rglob('*.md5anim') if f.stem==sequence]
    destination=models[0].with_name(models[0].stem+'__'+str(component['Index'])+'.gltf')
    animation=None
    if animations:animation=attach_animation(models[0],animations[0],destination,node.get('Rate',1),delay)
    else:destination=models[0];warnings.append('Effect animation missing: '+str(sequence))
    attachments.append({'Name':a['Name'],'Model':destination.relative_to(out).as_posix(),'Properties':props,'Materials':[m['Name'] if m else None for m in props.get('Materials',[])],'Delay':delay,'Animation':animation})
   elif component['Class']=='ParticleSystemComponent':
    template=props.get('Template') or {}
    if 'painted' not in template.get('Name','').casefold():particles.append({'Name':a['Name'],'Delay':delay,'Properties':props,'Template':template})
   else:unsupported.add(component['Class'])
 document={'Package':package,'Attachments':attachments,'ParticleComponents':particles,'Objects':p.document()['Objects'],'UnsupportedComponents':sorted(unsupported)}
 (out/'effect-scene.json').write_text(json.dumps(document,indent=2),encoding='utf-8')
 if unsupported:warnings.append('Effect runtime components retained as source data: '+', '.join(sorted(unsupported)))
 if particles:warnings.append('Particle emitter definitions and timing are included; unsupported particle modules are retained as source data.')
 return document
