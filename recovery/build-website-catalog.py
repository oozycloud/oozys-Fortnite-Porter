"""Build public metadata only; never include local archive paths or game payloads."""
import json
import re
from datetime import datetime, timezone
from pathlib import Path
root=Path(__file__).resolve().parent.parent
fortnite=json.loads((root/'recovery/site-ghoul/catalog.json').read_text(encoding='utf-8-sig'))
parts=json.loads((root/'recovery/pc-parts-check/catalog.json').read_text(encoding='utf-8-sig'))['Assets']
part_filters=json.loads((root/'work/FortniteObj/pc_part_filters.json').read_text())
excluded_parts={n.casefold() for n in part_filters['exclude']+part_filters['fittings']}
assets=[]
registry_path=root/'website/assets/id-registry.json'
registry=json.loads(registry_path.read_text(encoding='utf-8')) if registry_path.exists() else {'assets':[]}
previous={x['source']:x for x in registry['assets']}
channels=json.loads((root/'recovery/website-catalog/style-channels.json').read_text(encoding='utf-8'))
overrides=json.loads((root/'recovery/catalog-image-overrides.json').read_text()) if (root/'recovery/catalog-image-overrides.json').exists() else {}
http=json.loads((root/'recovery/catalog-image-http.json').read_text()) if (root/'recovery/catalog-image-http.json').exists() else {}
api={x['id'].lower():x for x in json.loads((root/'recovery/fortnite-api-cosmetics.json').read_text())['data']}
for game,prefix,source,categories in [
 ('Fortnite','fn',fortnite,{'Skins','Back blings','Pickaxes','Gliders'}),
 ('PC Building Simulator 2','pcbs2',parts,{'Motherboards','GPUs','CPUs','RAM','Cases','Coolers','Fans'})]:
 for item in source:
  if item['Category'] not in categories or (prefix=='pcbs2' and item['Name'].casefold() in excluded_parts):continue
  row=dict(source=prefix+':'+item['Id'],assetId=item['Id'],name=item['Name'],game=game,category=item['Category'])
  if prefix=='pcbs2':
   row['name']=re.sub(r'^(?:MB|GPU|CPU|RAM|AC|AIO|FAN|CASE|LC)_','',row['name'],flags=re.I).replace('_',' ').strip()
  if game=='Fortnite' and item.get('ImageUrl','').startswith('https://fortnite-api.com/'):row['image']=item['ImageUrl']
  assets.append(row)
assert len({a['source'].lower() for a in assets})==len(assets),'Duplicate catalogue IDs'
assets.sort(key=lambda a:(a['name'].casefold(),a['source']))
next_number=max([int(re.search(r'\d+',x['id'])[0]) for x in previous.values()]+[0])+1
for row in assets:
 old=previous.get(row['source'])
 if old:row['id']=old['id']
 else:
  row['id']=next(iter(re.findall('[A-Za-z]',row['name'])),'X').upper()+str(next_number);next_number+=1
 source_channels=channels.get(row['assetId'],[]) if row['game']=='Fortnite' else []
 new_channels=[]
 for channel in source_channels:
  variant=next((v for v in api.get(row['assetId'].lower(),{}).get('variants') or [] if v.get('type','').casefold()==channel['Name'].casefold()),{})
  options=[]
  for opt in channel['Options']:
   option={'key':opt['Id'],'name':opt['Name']}
   if opt.get('Color'):option['color']=opt['Color']
   match=next((v for v in variant.get('options',[]) if v.get('name','').casefold()==opt['Name'].casefold()),{})
   if match.get('image'):option['image']=match['image']
   options.append(option)
  new_channels.append({'key':channel['Id'],'name':channel['Name'],'defaultOption':channel['DefaultOption'],'options':options})
 # Existing slots retain their meaning; new channels/options append to the end.
 if old:
  old_channels=old.get('styleChannels',[])
  for old_channel in old_channels:
   new_channel=next((c for c in new_channels if c['key']==old_channel['key']),None)
   if new_channel:
    old_keys=[o['key'] for o in old_channel['options']]
    new_channel['options'].sort(key=lambda o:old_keys.index(o['key']) if o['key'] in old_keys else len(old_keys)+int(o['key']))
  keys=[c['key'] for c in old_channels]
  new_channels.sort(key=lambda c:keys.index(c['key']) if c['key'] in keys else len(keys))
 if new_channels:row['styleChannels']=new_channels
for row in assets:
 if row['source'] in overrides:row['image']=overrides[row['source']]
 elif row['game']=='PC Building Simulator 2' and (root/f"website/assets/parts/{row['id']}.webp").exists():row['image']=f"assets/parts/{row['id']}.webp"
 elif row.get('image') and http.get(row['image'])==404:row.pop('image')
 if not row.get('image'):row['imageUnavailable']=True
assert all(len(c['options'])<=1024 for a in assets for c in a.get('styleChannels',[]))
registry_path.write_text(json.dumps({'styleRadix':1024,'assets':assets},ensure_ascii=False,separators=(',',':')),encoding='utf-8')
for row in assets:row.pop('source',None);row.pop('assetId',None)
out=root/'website/assets/catalog.json'
out.write_text(json.dumps(dict(generatedAt=datetime.now(timezone.utc).isoformat(),styleRadix=1024,assets=assets),ensure_ascii=False,separators=(',',':')),encoding='utf-8')
print(json.dumps({'total':len(assets),'categories':{c:sum(a['category']==c for a in assets) for c in sorted({a['category'] for a in assets})},'bytes':out.stat().st_size}))
