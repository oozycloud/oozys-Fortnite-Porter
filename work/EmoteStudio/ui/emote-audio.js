// Audio follows the model clock; preview FPS never controls song speed.
export class EmoteAudio {
 constructor(){this.context=null;this.tracks=[];this.buffers=[];this.sources=[];this.key='';this.revision=0;this.enabled=true;this.volume=.7;this.anchor=null;this.lastTime=0;this.loaded=false;this.error='';}
 ensureContext(){if(!this.context){this.context=new AudioContext();this.gain=this.context.createGain();this.gain.gain.value=this.volume;this.gain.connect(this.context.destination);}}
 async activate(){this.ensureContext();await this.context.resume();if(this.tracks.length&&!this.loaded)this.load();}
 async select(key,tracks){if(key===this.key)return;this.stop();this.key=key;this.tracks=tracks;this.loaded=false;this.buffers=[];this.error='';++this.revision;if(tracks.length){this.ensureContext();await this.load();}}
 async load(){const revision=this.revision;try{const buffers=await Promise.all(this.tracks.map(async track=>{const response=await fetch(track.url);if(!response.ok)throw Error('Emote audio could not be loaded.');return this.context.decodeAudioData(await response.arrayBuffer());}));if(revision!==this.revision)return;this.buffers=buffers;this.loaded=true;}catch(error){if(revision===this.revision)this.error=error.message;}}
 stop(){for(const source of this.sources){try{source.stop();}catch{}source.disconnect();}this.sources=[];this.anchor=null;}
 setEnabled(value){this.enabled=Boolean(value);if(!this.enabled)this.stop();}
 setVolume(value){this.volume=Math.max(0,Math.min(1,Number(value)||0));if(this.gain)this.gain.gain.value=this.volume;}
 sync(info,offset=0){
  if(!info.playing||!this.enabled||!this.loaded||this.context?.state!=='running'||document.hidden){this.stop();this.lastTime=info.time;return;}
  const rate=info.speed||1,clock=this.context.currentTime;
  if(this.anchor&&Math.abs(this.anchor.rate-rate)<1e-6&&info.time>=this.lastTime-.05&&Math.abs(info.time-(this.anchor.model+(clock-this.anchor.clock)*rate))<.10){this.lastTime=info.time;return;}
  this.stop();this.anchor={model:info.time,clock,rate};this.lastTime=info.time;
  this.tracks.forEach((track,index)=>{const elapsed=info.time+offset-(Number(track.start)||0),buffer=this.buffers[index];if(!track.loop&&elapsed>=buffer.duration)return;const source=this.context.createBufferSource();source.buffer=buffer;source.loop=Boolean(track.loop);source.playbackRate.value=rate;const gain=this.context.createGain();gain.gain.value=Math.max(0,Math.min(4,(Number.isFinite(Number(track.gain))?Number(track.gain):1)));source.connect(gain);gain.connect(this.gain);source.onended=()=>gain.disconnect();source.start(clock+Math.max(0,-elapsed)/rate,track.loop&&elapsed>=0?elapsed%buffer.duration:Math.max(0,elapsed));this.sources.push(source);});
 }
 get info(){return{tracks:this.tracks.length,loaded:this.loaded,playing:Boolean(this.anchor),state:this.context?.state||'inactive',error:this.error};}
 dispose(){++this.revision;this.stop();this.context?.close();}
}
