(() => {
  'use strict';
  const byId=id=>document.getElementById(id);
  const grid=byId('catalog-grid'),search=byId('catalog-search'),game=byId('catalog-game'),category=byId('catalog-category');
  const count=byId('catalog-count'),previous=byId('catalog-previous'),next=byId('catalog-next');
  const allowed={Fortnite:['Skins','Back blings','Pickaxes','Gliders'],'PC Building Simulator 2':['Motherboards','GPUs','CPUs','RAM','Cases','Coolers','Fans']};
  const marks={Skins:'SK', 'Back blings':'BB', Pickaxes:'PX',Gliders:'GL',Motherboards:'MB',GPUs:'GPU',CPUs:'CPU',RAM:'RAM',Cases:'CASE',Coolers:'COOL',Fans:'FAN'};
  let assets=[],filtered=[],page=0,loaded=false,loading=false,searchTimer,feedbackTimer;
  const pageSize=48;
  const styleRadix=1024n;
  function channelsFor(asset){
    const channels=asset.styleChannels;
    return Array.isArray(channels)&&channels.length&&channels.every(channel=>Array.isArray(channel.options)&&channel.options.length>0&&channel.options.length<=Number(styleRadix))?channels:[];
  }
  function defaultIndex(channel){
    const index=channel.options.findIndex(option=>option.key===channel.defaultOption);
    return index>=0?index:0;
  }
  function matchesQuery(asset,query){
    if(!query)return true;
    if(/^[a-z][1-9]\d*(?:s\d+)?$/i.test(query)){
      return asset.id.toLocaleLowerCase()===query||decodeCombination(asset,query)!==null||
        (Array.isArray(asset.styles)&&asset.styles.some(style=>typeof style.id==='string'&&style.id.toLocaleLowerCase()===query));
    }
    return asset.search.includes(query);
  }
  function decodeCombination(asset,query){
    const channels=channelsFor(asset),prefix=asset.id.toLocaleLowerCase()+'s';
    if(!channels.length||!query.startsWith(prefix))return null;
    const suffix=query.slice(prefix.length);
    if(!/^[1-9]\d{0,100}$/.test(suffix))return null;
    let ordinal=BigInt(suffix)-1n;
    const indices=channels.map(()=>{const index=Number(ordinal%styleRadix);ordinal/=styleRadix;return index;});
    return ordinal===0n&&indices.every((index,i)=>index<channels[i].options.length)?indices:null;
  }
  function categoryOptions(){
    const selected=category.value;
    category.replaceChildren(new Option('all categories',''));
    const values=game.value?allowed[game.value]:Object.values(allowed).flat();
    values.forEach(value=>category.append(new Option(value,value)));
    if(values.includes(selected))category.value=selected;
  }
  categoryOptions();
  function feedback(message){
    const status=byId('copy-status');status.textContent=message;
    clearTimeout(feedbackTimer);feedbackTimer=setTimeout(()=>status.textContent='',5000);
  }
  async function copy(asset,input,button){
    let copied=false;
    try{await navigator.clipboard.writeText(asset.id);copied=true;}catch{
      input.focus();input.select();
      try{copied=document.execCommand('copy');}catch{/* Text remains selected for manual copy. */}
    }
    if(copied){button.textContent='copied ✓';feedback(`Copied ${asset.name}'s id. Paste it into the porter.`);setTimeout(()=>button.textContent='copy id',1800);}
    else feedback('The asset id is selected. Press Ctrl+C, or use your device’s Copy action.');
  }
  function card(asset){
    let selected=asset;
    const item=document.createElement('li');item.className='asset-card';
    const preview=document.createElement('div');preview.className='asset-preview';
    const mark=document.createElement('span');mark.className='asset-mark';mark.textContent=marks[asset.category]||'3D';mark.setAttribute('aria-hidden','true');preview.append(mark);
    const unavailable=document.createElement('span');unavailable.className='asset-unavailable';unavailable.textContent='preview unavailable';unavailable.hidden=true;preview.append(unavailable);
    function showImage(value){
      preview.querySelector('img')?.remove();mark.hidden=false;unavailable.hidden=true;
      // Catalogue-owned relative images and public HTTPS thumbnails are supported.
      const source=value.image;
      if(value.imageUnavailable||typeof source!=='string'||!(/^(?:https:\/\/|\.\/assets\/|assets\/)/i.test(source))){unavailable.hidden=false;return;}
      const image=document.createElement('img');image.src=source;image.alt='';image.loading='lazy';image.decoding='async';image.referrerPolicy='no-referrer';image.addEventListener('load',()=>{if(image.isConnected){mark.hidden=true;unavailable.hidden=true;}},{once:true});image.addEventListener('error',()=>{if(image.isConnected){image.remove();mark.hidden=false;unavailable.hidden=false;}},{once:true});preview.append(image);
    }
    const meta=document.createElement('p');meta.className='asset-category';meta.textContent=`${asset.game==='Fortnite'?'fortnite':'pc building simulator 2'} / ${asset.category.toLowerCase()}`;
    const title=document.createElement('h2');title.textContent=asset.name;
    const actions=document.createElement('div');actions.className='asset-actions';
    const id=document.createElement('input');id.value=asset.id;id.readOnly=true;id.setAttribute('aria-label',`${asset.name} asset id`);id.title=asset.id;
    const button=document.createElement('button');button.type='button';button.textContent='copy id';button.setAttribute('aria-label',`Copy id for ${asset.name}`);button.addEventListener('click',()=>copy(selected,id,button));
    actions.append(id,button);item.append(preview,meta,title);
    const channels=channelsFor(asset);
    const styles=Array.isArray(asset.styles)?asset.styles.filter(style=>typeof style.id==='string'&&typeof style.name==='string'&&style.id!==asset.id):[];
    if(channels.length){
      const details=document.createElement('details');details.className='asset-customize';
      const summary=document.createElement('summary');summary.textContent='customize styles';details.append(summary);
      const panel=document.createElement('div');panel.className='asset-style-panel';
      const selects=[];
      const previewNote=document.createElement('p');previewNote.className='asset-preview-note';previewNote.textContent='asset preview';
      preview.after(previewNote);
      function choose(previewChannel=-1){
        let ordinal=0n,multiplier=1n;
        channels.forEach((channel,index)=>{ordinal+=BigInt(selects[index].selectedIndex)*multiplier;multiplier*=styleRadix;});
        const option=previewChannel>=0?channels[previewChannel].options[selects[previewChannel].selectedIndex]:null;
        selected={...asset,id:`${asset.id}S${ordinal+1n}`,name:`${asset.name} — custom styles`,image:option?.image||asset.image,imageUnavailable:option?.image?false:asset.imageUnavailable};
        id.value=selected.id;id.title=selected.id;button.textContent='copy id';
        id.setAttribute('aria-label',`${asset.name} complete style combination id`);
        button.setAttribute('aria-label',`Copy complete style combination id for ${asset.name}`);
        previewNote.textContent=option?.image?'option preview · not a combined render':'asset preview';
        showImage(selected);
      }
      channels.forEach((channel,index)=>{
        const label=document.createElement('label');label.className='asset-style';
        const caption=document.createElement('span');caption.textContent=channel.name||channel.key;
        const select=document.createElement('select');select.setAttribute('aria-label',`${channel.name||channel.key} for ${asset.name}`);
        channel.options.forEach((option,optionIndex)=>select.append(new Option(option.name||option.key,String(optionIndex))));
        select.selectedIndex=defaultIndex(channel);
        selects.push(select);select.addEventListener('change',()=>choose(index));label.append(caption,select);panel.append(label);
      });
      const apply=document.createElement('button');apply.type='button';apply.className='style-apply';apply.textContent='use these styles';apply.addEventListener('click',()=>choose());
      const reset=document.createElement('button');reset.type='button';reset.className='style-reset';reset.textContent='use default';reset.addEventListener('click',()=>{
        selects.forEach((select,index)=>select.selectedIndex=defaultIndex(channels[index]));selected=asset;id.value=asset.id;id.title=asset.id;button.textContent='copy id';
        id.setAttribute('aria-label',`${asset.name} asset id`);button.setAttribute('aria-label',`Copy id for ${asset.name}`);previewNote.textContent='asset preview';showImage(asset);
      });
      panel.append(apply,reset);details.append(panel);item.append(details);
      const exact=decodeCombination(asset,search.value.trim().toLocaleLowerCase());
      if(exact){exact.forEach((value,index)=>selects[index].selectedIndex=value);details.open=true;choose();}
    }else if(styles.length){
      const label=document.createElement('label');label.className='asset-style';
      const caption=document.createElement('span');caption.textContent='style';
      const select=document.createElement('select');select.setAttribute('aria-label',`Style for ${asset.name}`);select.append(new Option('default',asset.id));
      styles.forEach(style=>select.append(new Option(style.name,style.id)));
      function choose(){
        const style=styles.find(value=>value.id===select.value);
        selected=style?{...style,name:`${asset.name} — ${style.name}`}:asset;
        id.value=selected.id;id.title=selected.id;id.setAttribute('aria-label',`${selected.name} asset id`);
        button.textContent='copy id';button.setAttribute('aria-label',`Copy id for ${selected.name}`);showImage(selected);
      }
      select.addEventListener('change',choose);
      const query=search.value.trim().toLocaleLowerCase();
      const exact=styles.find(style=>style.id.toLocaleLowerCase()===query);
      if(exact){select.value=exact.id;choose();}
      label.append(caption,select);item.append(label);
    }
    showImage(selected);item.append(actions);return item;
  }
  function render(){
    const pages=Math.max(1,Math.ceil(filtered.length/pageSize));page=Math.min(page,pages-1);
    const fragment=document.createDocumentFragment();filtered.slice(page*pageSize,(page+1)*pageSize).forEach(asset=>fragment.append(card(asset)));grid.replaceChildren(fragment);
    count.textContent=`${filtered.length.toLocaleString()} ${filtered.length===1?'model':'models'}`;
    byId('catalog-empty').hidden=filtered.length>0;
    byId('catalog-page').textContent=`page ${page+1} of ${pages}`;
    previous.disabled=page===0;next.disabled=page>=pages-1;
  }
  function filter(){
    if(!loaded)return;
    const query=search.value.trim().toLocaleLowerCase();
    filtered=assets.filter(asset=>(!game.value||asset.game===game.value)&&(!category.value||asset.category===category.value)&&matchesQuery(asset,query));
    page=0;render();
  }
  async function load(){
    if(loading||loaded)return;loading=true;byId('catalog-error').hidden=true;count.textContent='loading the collection';grid.setAttribute('aria-busy','true');
    const controller=new AbortController(),timeout=setTimeout(()=>controller.abort(),20000);
    try{
      const response=await fetch('./assets/catalog.json',{signal:controller.signal});if(!response.ok)throw new Error('Catalogue unavailable');
      const payload=await response.json();if(!Array.isArray(payload.assets)||(payload.styleRadix!==undefined&&payload.styleRadix!==Number(styleRadix)))throw new Error('Invalid catalogue');
      assets=payload.assets.filter(asset=>typeof asset.id==='string'&&typeof asset.name==='string'&&allowed[asset.game]?.includes(asset.category)).map(asset=>({...asset,search:`${asset.name} ${asset.id} ${(Array.isArray(asset.styles)?asset.styles:[]).map(style=>`${style.name} ${style.id}`).join(' ')} ${channelsFor(asset).map(channel=>`${channel.name} ${channel.options.map(option=>option.name).join(' ')}`).join(' ')}`.toLocaleLowerCase()}));
      assets.sort((a,b)=>a.name.localeCompare(b.name,undefined,{numeric:true,sensitivity:'base'}));loaded=true;filter();
    }catch{byId('catalog-error').hidden=false;count.textContent='collection unavailable';}
    finally{clearTimeout(timeout);loading=false;grid.setAttribute('aria-busy','false');}
  }
  game.addEventListener('change',()=>{categoryOptions();filter();});category.addEventListener('change',filter);
  search.addEventListener('input',()=>{clearTimeout(searchTimer);searchTimer=setTimeout(filter,120);});
  function turn(delta){page+=delta;render();byId('catalog-count').scrollIntoView({block:'start',behavior:'instant'});}
  previous.addEventListener('click',()=>turn(-1));next.addEventListener('click',()=>turn(1));
  byId('catalog-retry').addEventListener('click',load);
  document.addEventListener('porter:page',event=>{if(event.detail==='home')load();});
})();
