import {StudioPreview} from './preview.js';
const $=id=>document.getElementById(id);
const native=Boolean(window.chrome?.webview);
const initial={version:'beta 0.12.1',busy:false,progress:0,message:'Import a model to begin.',error:null,blender:null,projectName:'untitled project',model:null,emotes:[],selectedEmote:null,selectedClip:0,previewUrl:null,previewRevision:0,warnings:[],mappingNeeded:false,boneMap:{},sourceBones:[]};
let state={...initial},loadedPreviewKey='',loadingPreviewKey='',filter='',dismissedError=null;
let wheelControlsKey='',wheelPoseContext='',wheelPoseAppliedKey='',wheelPoseObservedKey='',wheelPoseLocalKey=null,wheelPoseTimer=0,wheelPosePending=null,applyingWheelPose=false;
let wheelCorner='FL',wheelMode='steer';
const preview=new StudioPreview($('preview-canvas'),$('viewport'));
const pendingButtons=['import-model','viewport-import','replace-model','add-emotes','rig-select','open-project','save-project','export','choose-blender','apply-mapping','clip-select'];
function command(type,data={}) {if(['export','saveProject','chooseModel','openProject','selectRig','selectEmote'].includes(type))flushWheelPose();if(native)window.chrome.webview.postMessage({type,...data});else status('File import and saving are available in the desktop app.');}
function status(message,progress) {$('status-message').textContent=message||'Ready.';if(Number.isFinite(progress))$('progress').value=Math.max(0,Math.min(100,progress>=0&&progress<=1?progress*100:progress));}
function selectedEmote(){return(state.emotes||[]).find(e=>e.id===state.selectedEmote);}
function selectedClip(){const e=selectedEmote();return e?(e.clips?.[Number(state.selectedClip)||0]||null):(state.model?.nativeClip||null);}
function name(value,fallback=''){return typeof value==='string'&&value?value:fallback;}
function clock(seconds){const n=Math.max(0,Number(seconds)||0);return `${String(Math.floor(n/60)).padStart(2,'0')}:${(n%60).toFixed(2).padStart(5,'0')}`;}
function option(value,text){const el=document.createElement('option');el.value=value;el.textContent=text;return el;}
function setBusy(){
 const busy=Boolean(state.busy);preview.setPoseEnabled(!busy);for(const id of pendingButtons)$(id).disabled=busy||!native;
 const rigged=Boolean(state.model&&(state.model.rig||state.model.rigs?.length));
 $('add-emotes').disabled=busy||!native||!rigged;
 $('add-emotes').title=state.model&&!rigged?'This model needs an armature before emotes can be added. Choose a rigged .blend file.':'Import one or several emote files';
 $('save-project').disabled=busy||!native||!state.model;
 $('export').disabled=busy||!native||(!rigged&&!state.model?.nativeAnimation)||(!selectedEmote()&&!state.model?.nativeAnimation)||Boolean(state.mappingNeeded);
 $('rig-select').disabled=busy||!native||!(state.model?.rigs?.length>1);
 $('clip-select').disabled=busy||!native||!(selectedEmote()?.clips?.length>1);
 $('job-progress').hidden=!busy;$('cancel').disabled=!busy;
 $('status-dot').classList.toggle('bad',Boolean(state.error));
 $('status-dot').classList.toggle('good',Boolean(state.model)&&!busy&&!state.error);
 for(const button of document.querySelectorAll('.emote-pick,.remove-emote'))button.disabled=busy||!native;
}
function cleanWheelPose(pose){const result={};for(const key of Object.keys(pose||{}).sort()){const angle=Number(pose[key]);if(/^wheel_(FL|FR|RL|RR)_(steer|roll)$/.test(key)&&Number.isFinite(angle)&&Math.abs(angle)>1e-8)result[key]=angle;}return result;}
function wheelPoseKey(pose){return JSON.stringify(cleanWheelPose(pose));}
function resetWheelContext(){const key=state.previewUrl?state.previewUrl.split('/').slice(0,4).join('/'):state.model?.path||'';if(key===wheelPoseContext)return;wheelPoseContext=key;clearTimeout(wheelPoseTimer);wheelPoseTimer=0;wheelPosePending=null;wheelPoseLocalKey=null;wheelPoseAppliedKey='';wheelPoseObservedKey='';wheelControlsKey='';wheelCorner='FL';wheelMode='steer';}
function flushWheelPose(){clearTimeout(wheelPoseTimer);wheelPoseTimer=0;if(!wheelPosePending)return;const pose=wheelPosePending;wheelPosePending=null;if(native)command('setWheelPose',{pose});}
function applyHostWheelPose(force=false){
 if(!preview.info.loaded||loadingPreviewKey||preview.info.gizmoDragging)return;const pose=cleanWheelPose(state.wheelPose||{}),key=wheelPoseKey(pose);
 if(wheelPoseLocalKey!==null&&key!==wheelPoseLocalKey)return;
 if(key===wheelPoseLocalKey)wheelPoseLocalKey=null;
 if(force||key!==wheelPoseAppliedKey){applyingWheelPose=true;try{preview.applyWheelPose(pose);}finally{applyingWheelPose=false;}}
 wheelPoseAppliedKey=key;wheelPoseObservedKey=key;updateWheelAngle((pose[`wheel_${wheelCorner}_${wheelMode}`]||0)*180/Math.PI);
}
function updateWheelAngle(angle){const limit=wheelMode==='roll'?360:90;const degrees=Number.isFinite(Number(angle))?Number(angle):0;$('wheel-angle').min=String(-limit);$('wheel-angle').max=String(limit);$('wheel-angle').value=String(Math.max(-limit,Math.min(limit,degrees)));$('wheel-angle-value').textContent=`${Math.round(degrees*10)/10}°`;$('wheel-angle').setAttribute('aria-valuetext',`${Math.round(degrees*10)/10} degrees`);}
function syncWheelControls(force=false){
 const controls=Array.isArray(preview.info.wheelControls)?preview.info.wheelControls:[];const labels={FL:'Front left',FR:'Front right',RL:'Rear left',RR:'Rear right'};
 const wheelOnly=Boolean(state.model)&&controls.length>0&&!selectedEmote();
 $('inspector').classList.toggle('wheel-focused',wheelOnly);$('move-section').hidden=wheelOnly;
 $('wheel-posing').hidden=!state.model||Boolean(loadingPreviewKey)||!controls.length;
 const key=controls.join('|');if(key!==wheelControlsKey){wheelControlsKey=key;$('wheel-corner').replaceChildren();for(const corner of controls)$('wheel-corner').append(option(corner,labels[corner]||corner));if(!controls.includes(wheelCorner))wheelCorner=controls[0]||'FL';$('wheel-corner').value=wheelCorner;$('wheel-mode').value=wheelMode;if(controls.length)preview.selectWheel(wheelCorner,wheelMode);}else if(force&&controls.length)preview.selectWheel(wheelCorner,wheelMode);
 const disabled=Boolean(state.busy)||!controls.length;for(const id of ['wheel-corner','wheel-mode','wheel-angle','reset-wheel','reset-wheels'])$(id).disabled=disabled;
}
function renderLibrary(){
 const list=$('emote-list');list.replaceChildren();const emotes=state.emotes||[];
 $('emote-count').textContent=String(emotes.length);
 $('emote-empty').hidden=emotes.length>0;$('emote-search').disabled=!emotes.length;
 for(const emote of emotes.filter(e=>name(e.name).toLowerCase().includes(filter.toLowerCase()))){
  const item=document.createElement('li');item.className='emote-item';item.classList.toggle('selected',emote.id===state.selectedEmote);
  const pick=document.createElement('button');pick.className='emote-pick';pick.type='button';pick.setAttribute('aria-pressed',String(emote.id===state.selectedEmote));pick.title=emote.path||emote.name;
  const title=document.createElement('strong');title.textContent=emote.name||'Untitled emote';const detail=document.createElement('span');const clip=emote.clips?.[0];detail.textContent=emote.clips?.length>1?`${emote.clips.length} animation clips`:clip?`${Number(clip.duration||0).toFixed(2)} s · ${clip.loop?'loop':'one shot'}`:'Animation file';pick.append(title,detail);
  pick.addEventListener('click',()=>command('selectEmote',{id:emote.id,clipIndex:emote.id===state.selectedEmote?(Number(state.selectedClip)||0):Math.max(0,(emote.clips||[]).findIndex(c=>!/(?:^|_)(?:intro|outro|start|end|breakout)(?:_|$)/i.test(c.name||'')))}));
  const remove=document.createElement('button');remove.className='remove-emote';remove.textContent='×';remove.title='Remove from this project';remove.setAttribute('aria-label',`Remove ${emote.name||'emote'}`);remove.addEventListener('click',()=>command('removeEmote',{id:emote.id}));item.append(pick,remove);list.append(item);
 }
 if(emotes.length&&!list.children.length){const item=document.createElement('li');item.className='quiet-note';item.textContent='No imported emotes match this search.';list.append(item);}
}
function renderMapping(){
 $('mapping-section').hidden=!state.mappingNeeded;const area=$('bone-mapping');area.replaceChildren();if(!state.mappingNeeded)return;
 for(const source of state.sourceBones||[]){const bone=typeof source==='string'?source:source.name;if(!bone)continue;const row=document.createElement('div');row.className='bone-row';const label=document.createElement('label');label.textContent=bone;const select=document.createElement('select');select.dataset.source=bone;select.setAttribute('aria-label',`Model bone for ${bone}`);select.append(option('','Not mapped'));
  for(const target of state.model?.bones||[])select.append(option(typeof target==='string'?target:target.name,typeof target==='string'?target:target.name));select.value=state.boneMap?.[bone]||'';row.append(label,select);area.append(row);}
}
function renderWarnings(){
 const warnings=[...(state.warnings||[]),...(selectedEmote()?.warnings||[])].filter(Boolean);const unique=[...new Set(warnings.map(String))];$('warnings-section').hidden=!unique.length;$('warning-count').textContent=String(unique.length);$('warnings').replaceChildren();for(const message of unique){const item=document.createElement('li');item.textContent=message;$('warnings').append(item);}
}
function render(){
 $('version').textContent=name(state.version,'beta 0.12.1');$('project-name').textContent=name(state.projectName,'untitled project');
 const model=state.model;$('model-empty').hidden=Boolean(model);$('model-details').hidden=!model;$('replace-model').hidden=!model;$('import-model').hidden=Boolean(model);
 if(model){$('model-name').textContent=name(model.name,'Untitled model');$('model-path').textContent=name(model.path);$('model-path').title=name(model.path);$('mesh-stat').textContent=`${model.meshCount??0} meshes`;$('texture-stat').textContent=`${model.textureCount??0} textures`;$('rig-select').replaceChildren();
  const rigs=model.rigs||[];if(!rigs.length)$('rig-select').append(option('','No armature found'));for(const rig of rigs)$('rig-select').append(option(rig.name,`${rig.name} · ${rig.bones} bones`));$('rig-select').value=model.rig||rigs[0]?.name||'';$('rig-warning').hidden=rigs.length>0;$('rig-warning').textContent=model.nativeAnimation?'This wheel uses material animation; an armature is only needed for emotes.':'This model has no armature. Choose a rigged .blend to add emotes.';}
 const emote=selectedEmote(),clip=selectedClip(),nativeClip=!emote&&model?.nativeAnimation?clip:null;$('selected-name').textContent=emote?name(emote.name,'Untitled emote'):nativeClip?name(nativeClip.name,nativeClip.procedural?'Wheel animation':'Model animation'):'No emote selected';$('selected-note').textContent=emote?'This move is applied to the selected model rig. You can keep several emotes in the same project.':nativeClip?.procedural?'Real-time material playback. Press Play to preview the wheel animation. Frame rate and animation speed are separate.':nativeClip?'This animation is already part of your imported model. Use the timeline to preview it.':'Choose an emote from your library. Its animation will be applied to your model.';
 $('preview-title').textContent=emote?name(emote.name,'the selected move'):model?name(model.name,'your model'):'make it move.';
 $('clip-field').hidden=!(emote?.clips?.length);$('clip-select').replaceChildren();for(const [index,c]of(emote?.clips||[]).entries())$('clip-select').append(option(index,c.name||`Clip ${index+1}`));$('clip-select').value=String(Number(state.selectedClip)||0);
 $('clip-facts').hidden=!clip;if(clip){$('clip-duration').textContent=clip.procedural?'Continuous':`${Number(clip.duration||0).toFixed(2)} seconds`;$('clip-loop').textContent=clip.procedural?'Real-time':clip.loop?'Repeating loop':'Play once';}
 $('engine-status').textContent=state.blender?'Blender ready':'Blender not found';$('engine-dot').classList.toggle('good',Boolean(state.blender));$('engine-dot').classList.toggle('bad',!state.blender);$('choose-blender').title=typeof state.blender==='string'?state.blender:'Locate your Blender installation';
 const error=state.error?String(state.error):'';$('error-banner').hidden=!error||dismissedError===error;$('error-text').textContent=error;
 resetWheelContext();renderLibrary();renderMapping();renderWarnings();setBusy();updateExportNote();status(state.message||'Ready.',state.progress);
 $('viewport-empty').hidden=Boolean(state.previewUrl)||Boolean(preview.info.loaded);$('viewport-label').hidden=!preview.info.loaded;
 if(state.busy){$('viewport-loading').hidden=false;$('preview-loading-text').textContent=state.message||'Preparing your model';}else if(!loadingPreviewKey)$('viewport-loading').hidden=true;
 const key=state.previewUrl?`${state.previewUrl}|${state.previewRevision}`:'';
 if(key&&key!==loadedPreviewKey&&key!==loadingPreviewKey){loadingPreviewKey=key;$('viewport-loading').hidden=false;$('preview-loading-text').textContent='Loading model preview';$('viewport-failure').hidden=true;preview.load(state.previewUrl+(state.previewUrl.includes('?')?'&':'?')+'studioRevision='+encodeURIComponent(state.previewRevision||0),clip||{}).then(applied=>{if(!applied)return;loadedPreviewKey=key;if(loadingPreviewKey===key)loadingPreviewKey='';$('viewport-loading').hidden=Boolean(state.busy)===false;$('viewport-empty').hidden=true;$('viewport-label').hidden=false;applyHostWheelPose(true);syncWheelControls(true);}).catch(error=>{if(loadingPreviewKey!==key)return;loadingPreviewKey='';$('viewport-loading').hidden=true;$('viewport-failure').textContent=error.message||'The 3D preview could not load.';$('viewport-failure').hidden=false;});}
 else if(!key&&!state.model&&preview.info.loaded){preview.clear();loadedPreviewKey='';loadingPreviewKey='';$('viewport-empty').hidden=false;}
 applyHostWheelPose();syncWheelControls();
}
function receive(message){if(!message||typeof message!=='object')return;if(message.type==='state'){state={...initial,...message.state};render();}else if(message.type==='progress'){state.message=message.message;state.progress=message.progress;status(message.message,message.progress);if(state.busy){$('preview-loading-text').textContent=message.message||'Preparing your model';}}}
if(native)window.chrome.webview.addEventListener('message',event=>receive(event.data));
window.EmoteStudio=Object.freeze({receive});Object.defineProperty(window,'EmoteStudioPreview',{get:()=>Object.freeze({...preview.info})});
for(const[id,type]of Object.entries({'import-model':'chooseModel','viewport-import':'chooseModel','replace-model':'chooseModel','add-emotes':'addEmotes','open-project':'openProject','save-project':'saveProject','choose-blender':'chooseBlender','cancel':'cancel'}))$(id).addEventListener('click',()=>command(type));
for(const button of document.querySelectorAll('[data-command]'))button.addEventListener('click',()=>command(button.dataset.command));
$('titlebar').addEventListener('pointerdown',event=>{if(event.button===0&&!event.target.closest('button,input,select'))command('dragWindow');});$('titlebar').addEventListener('dblclick',event=>{if(!event.target.closest('button'))command('maximize');});
$('rig-select').addEventListener('change',()=>command('selectRig',{name:$('rig-select').value}));$('clip-select').addEventListener('change',()=>command('selectEmote',{id:state.selectedEmote,clipIndex:Number($('clip-select').value)}));
$('emote-search').addEventListener('input',()=>{filter=$('emote-search').value;renderLibrary();setBusy();});
function updateExportNote(){const material=Boolean(!selectedEmote()&&state.model?.nativeClip?.procedural);$('export-note').textContent={BLEND:material?'Model, packed textures and original wheel animation, together in one .blend.':'Model, textures and selected animation, together in one .blend.',GLB:material?'Model and standard textures. Wheel material animation stays in .blend; GLB does not retain Blender material drivers.':'Packed model, standard textures and selected animation. Custom Blender shaders remain in .blend.',FBX:material?'Model and rig for other 3D tools. Wheel material animation stays in .blend; FBX does not retain Blender material drivers.':'Model, rig and selected animation for other 3D tools. Texture support depends on the receiving app.'}[$('export-format').value];}
$('export').addEventListener('click',()=>command('export',{format:$('export-format').value}));$('export-format').addEventListener('change',updateExportNote);
$('apply-mapping').addEventListener('click',()=>{const mapping={};for(const select of document.querySelectorAll('#bone-mapping select'))if(select.value)mapping[select.dataset.source]=select.value;command('mapBones',{mapping});});
$('dismiss-error').addEventListener('click',()=>{dismissedError=String(state.error);$('error-banner').hidden=true;});
$('play').addEventListener('click',()=>preview.toggle());$('stop').addEventListener('click',()=>preview.stop());$('step-back').addEventListener('click',()=>preview.step(-1));$('step-forward').addEventListener('click',()=>preview.step(1));$('seek').addEventListener('input',()=>preview.seekFrame(Number($('seek').value)));$('speed').addEventListener('change',()=>preview.setSpeed($('speed').value));
try{const saved=localStorage.getItem('oozy-studio-preview-fps');if(['0','60','120','144','240'].includes(saved))$('preview-fps').value=saved;}catch{}
preview.setFrameLimit(Number($('preview-fps').value));$('preview-fps').addEventListener('change',()=>{const limit=Number($('preview-fps').value);preview.setFrameLimit(limit);try{localStorage.setItem('oozy-studio-preview-fps',String(limit));}catch{}});
$('wheel-corner').addEventListener('change',()=>{wheelCorner=$('wheel-corner').value;preview.selectWheel(wheelCorner,wheelMode);});
$('wheel-mode').addEventListener('change',()=>{wheelMode=$('wheel-mode').value;preview.selectWheel(wheelCorner,wheelMode);});
$('wheel-angle').addEventListener('input',()=>preview.setWheelAngle(Number($('wheel-angle').value)));
$('wheel-angle').addEventListener('change',flushWheelPose);
$('reset-wheel').addEventListener('click',()=>{preview.resetWheel(false);flushWheelPose();});$('reset-wheels').addEventListener('click',()=>{preview.resetWheel(true);flushWheelPose();});
preview.addEventListener('wheelpose',event=>{const detail=event.detail||{};if(detail.corner)wheelCorner=detail.corner;if(detail.mode)wheelMode=detail.mode;$('wheel-corner').value=wheelCorner;$('wheel-mode').value=wheelMode;updateWheelAngle(detail.angle);if(applyingWheelPose||loadingPreviewKey)return;const pose=cleanWheelPose(detail.pose||{}),key=wheelPoseKey(pose);if(key===wheelPoseObservedKey)return;wheelPoseObservedKey=key;wheelPoseAppliedKey=key;wheelPoseLocalKey=key;wheelPosePending=pose;clearTimeout(wheelPoseTimer);wheelPoseTimer=setTimeout(flushWheelPose,150);});
$('fit-view').addEventListener('click',()=>preview.fit());$('reset-view').addEventListener('click',()=>preview.fit());$('zoom-in').addEventListener('click',()=>preview.zoomBy(1.15));$('zoom-out').addEventListener('click',()=>preview.zoomBy(1/1.15));$('toggle-grid').addEventListener('click',()=>{const on=$('toggle-grid').getAttribute('aria-pressed')!=='true';$('toggle-grid').setAttribute('aria-pressed',String(on));preview.setGrid(on);});
preview.addEventListener('time',event=>{const info=event.detail;$('frame-now').textContent=String(info.frame);$('frame-total').textContent=info.hasAnimation?String(info.totalFrames):'—';$('seek').max=String(info.totalFrames);$('seek').value=String(info.frame);$('seek').disabled=!info.hasAnimation;$('seek').style.setProperty('--played',`${(info.frame-1)/Math.max(info.totalFrames-1,1)*100}%`);$('time-readout').textContent=`${clock(info.time)} / ${clock(info.duration)}`;$('fps-readout').textContent=info.hasAnimation?(info.procedural?`wheel animation${Number(info.measuredFps)>0?' · '+Math.round(info.measuredFps)+' fps':''}`:`${Number(info.fps.toFixed(2))} fps · ${selectedClip()?.loop?'loop':'one shot'}`):'Select an animation to begin';$('fps-readout').title=info.procedural?'Measured preview frame rate. Animation timing is independent of this rate.':'';$('play').textContent=info.playing?'Ⅱ':'▶';$('play').setAttribute('aria-label',info.playing?'Pause':'Play');for(const id of ['play','stop','step-back','step-forward','speed'])$(id).disabled=!info.hasAnimation;});
preview.addEventListener('error',event=>{$('viewport-failure').textContent=event.detail;$('viewport-failure').hidden=false;$('viewport-loading').hidden=true;});
for(const id of ['play','stop','step-back','step-forward','speed'])$(id).disabled=true;
if(preview.failure){$('viewport-failure').textContent=preview.failure;$('viewport-failure').hidden=false;}
document.addEventListener('keydown',event=>{if(event.target.closest('input,select,textarea,button')||event.target===$('viewport'))return;if(event.code==='Space'&&preview.info.hasAnimation){event.preventDefault();preview.toggle();}if((event.ctrlKey||event.metaKey)&&event.key.toLowerCase()==='s'){event.preventDefault();if(!state.busy&&state.model)command('saveProject');}});
window.addEventListener('pagehide',()=>{flushWheelPose();clearTimeout(wheelPoseTimer);preview.dispose();},{once:true});$('browser-note').hidden=native;render();command('ready');
