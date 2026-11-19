import * as THREE from 'three';
import {GLTFLoader} from './assets/vendor/GLTFLoader.js';
import {TransformControls} from './assets/vendor/TransformControls.js';
import {createWheelEffects} from './wheel-effects.js';
import {RoomEnvironment} from './assets/vendor/RoomEnvironment.js';

export class StudioPreview extends EventTarget {
  constructor(canvas, stage) {
    super(); this.canvas=canvas;this.stage=stage;this.revision=0;this.model=null;this.mixer=null;this.action=null;this.meta={};this.duration=0;this.fps=30;this.totalFrames=1;this.playing=false;this.speed=1;this.frameLimit=0;this.measuredFps=0;this.playbackTime=0;this.wheelEffects=null;this.lastRender=null;this.fpsStart=null;this.renderedFrames=0;this.lastNotify=0;this.ready=false;this.frameRequest=0;this.last=null;this.orbit=new THREE.Vector2();this.pointers=new Map();this.zoom=1;this.gridVisible=false;
    this.reduced=matchMedia('(prefers-reduced-motion: reduce)');
    try {this.renderer=new THREE.WebGLRenderer({canvas,antialias:true,alpha:true,powerPreference:'high-performance'});} catch(error) {this.failure='This device could not start the 3D preview. Import and save controls are still available.';return;}
    this.renderer.setPixelRatio(Math.min(devicePixelRatio,1.75));this.renderer.outputColorSpace=THREE.SRGBColorSpace;this.renderer.toneMapping=THREE.ACESFilmicToneMapping;this.renderer.toneMappingExposure=1.05;
    this.scene=new THREE.Scene();this.camera=new THREE.PerspectiveCamera(35,1,.01,500);this.camera.position.set(0,0,5);
    const pmrem=new THREE.PMREMGenerator(this.renderer);const room=new RoomEnvironment();this.environment=pmrem.fromScene(room,.06);this.scene.environment=this.environment.texture;room.dispose();pmrem.dispose();
    this.scene.environmentIntensity=.65;const key=new THREE.DirectionalLight(0xfff8ec,2.7);key.position.set(-3,5,4);this.scene.add(key);const fill=new THREE.DirectionalLight(0xffffff,1.2);fill.position.set(4,1,2);this.scene.add(fill);
    this.pivot=new THREE.Group();this.scene.add(this.pivot);this.grid=new THREE.GridHelper(10,20,0x6d5d43,0x393630);this.grid.visible=false;this.grid.material.transparent=true;this.grid.material.opacity=.4;this.scene.add(this.grid);
    this.wheelPose={};this.wheelBones=new Map();this.wheelAnimated=new Set();this.wheelCorner='FL';this.wheelMode='steer';
    this.transform=new TransformControls(this.camera,canvas);this.transform.setMode('rotate');this.transform.setSpace('local');this.transform.setSize(.8);this.transform.showX=false;this.transform.showY=true;this.transform.showZ=false;this.scene.add(this.transform.getHelper());
    this.transform.addEventListener('change',()=>this.render());
    this.transform.addEventListener('mouseDown',()=>{if(this.action)this.pause();});
    this.transform.addEventListener('objectChange',()=>{const name=`wheel_${this.wheelCorner}_${this.wheelMode}`,entry=this.wheelBones.get(name);if(!entry)return;const delta=entry.source.clone().invert().multiply(entry.bone.quaternion);let angle=2*Math.atan2(delta.y,delta.w);angle=Math.atan2(Math.sin(angle),Math.cos(angle));if(this.wheelMode==='steer')angle=THREE.MathUtils.clamp(angle,-Math.PI/2,Math.PI/2);this.wheelPose[name]=angle;this.applyWheelTransforms();this.emitWheelPose();});
    this.extent=new THREE.Vector3(2,3,1);this.observer=new ResizeObserver(()=>this.resize());this.observer.observe(stage);
    this.bindInput();document.addEventListener('visibilitychange',()=>{this.last=null;if(document.hidden)this.stopRenderLoop();else this.resume();});
    this.reduced.addEventListener('change',()=>{if(this.reduced.matches)this.pause();});
    canvas.addEventListener('webglcontextlost',event=>{event.preventDefault();this.contextLost=true;this.stopRenderLoop();this.dispatchEvent(new CustomEvent('error',{detail:'The 3D preview was paused by your graphics driver.'}));});
    canvas.addEventListener('webglcontextrestored',()=>{this.contextLost=false;this.resize();this.resume();});this.resize();
  }
  bindInput(){
    this.stage.addEventListener('pointerdown',event=>{
      if(this.transform?.enabled&&(this.transform.dragging||this.transform.axis))return;
      if(!this.ready||event.target.closest('button')||event.button>0)return;
      event.preventDefault();this.stage.focus({preventScroll:true});this.stage.setPointerCapture(event.pointerId);this.pointers.set(event.pointerId,{x:event.clientX,y:event.clientY});this.stage.classList.add('dragging');this.pinch=this.pointers.size===2?this.pointerDistance():null;
    });
    this.stage.addEventListener('pointermove',event=>{
      if(!this.pointers.has(event.pointerId))return;
      const before=this.pointers.get(event.pointerId);this.pointers.set(event.pointerId,{x:event.clientX,y:event.clientY});
      if(this.pointers.size>1){const distance=this.pointerDistance();if(this.pinch>0)this.zoomBy(distance/this.pinch);this.pinch=distance;}
      else {this.orbit.x+=(event.clientX-before.x)/Math.max(this.stage.clientWidth,1)*Math.PI*2;this.orbit.y=THREE.MathUtils.clamp(this.orbit.y+(event.clientY-before.y)/Math.max(this.stage.clientHeight,1)*Math.PI,-1.25,1.25);this.applyOrbit();}
      event.preventDefault();
    });
    const end=event=>{this.pointers.delete(event.pointerId);this.pinch=null;if(!this.pointers.size)this.stage.classList.remove('dragging');};for(const type of ['pointerup','pointercancel','lostpointercapture'])this.stage.addEventListener(type,end);
    this.stage.addEventListener('wheel',event=>{if(!this.ready||event.ctrlKey)return;event.preventDefault();const unit=event.deltaMode===1?16:event.deltaMode===2?this.stage.clientHeight:1;this.zoomBy(Math.exp(-THREE.MathUtils.clamp(event.deltaY*unit,-500,500)*.0015));},{passive:false});
    this.stage.addEventListener('keydown',event=>{
      if(event.target!==this.stage)return;let handled=true;
      if(event.key==='ArrowLeft')this.orbit.x-=.14;else if(event.key==='ArrowRight')this.orbit.x+=.14;else if(event.key==='ArrowUp')this.orbit.y=THREE.MathUtils.clamp(this.orbit.y-.1,-1.25,1.25);else if(event.key==='ArrowDown')this.orbit.y=THREE.MathUtils.clamp(this.orbit.y+.1,-1.25,1.25);else if(event.key==='+'||event.key==='=')this.zoomBy(1.12);else if(event.key==='-')this.zoomBy(1/1.12);else if(event.key==='Home'||event.key==='0')this.fit();else if(event.code==='Space')this.toggle();else handled=false;
      if(handled){event.preventDefault();this.applyOrbit();}
    });
  }
  pointerDistance(){const points=[...this.pointers.values()];return points.length>1?Math.hypot(points[0].x-points[1].x,points[0].y-points[1].y):0;}
  applyOrbit(){if(!this.pivot)return;this.pivot.rotation.set(this.orbit.y,this.orbit.x,0);this.render();}
  zoomBy(factor){this.zoom=THREE.MathUtils.clamp(this.zoom*factor,.45,3.5);this.resize();}
  fit(){this.zoom=1;this.orbit.set(0,0);this.applyOrbit();this.resize();}
  setGrid(visible){this.gridVisible=Boolean(visible);if(this.grid)this.grid.visible=this.gridVisible;this.render();}
  resize(){
    if(!this.renderer||this.contextLost)return;const width=this.stage.clientWidth,height=this.stage.clientHeight;if(!width||!height)return;
    this.renderer.setSize(width,height,false);this.camera.aspect=width/height;const tangent=Math.tan(THREE.MathUtils.degToRad(this.camera.fov/2));
    this.camera.position.set(0,0,Math.max(this.extent.y/2/tangent,this.extent.x/2/tangent/this.camera.aspect)*1.2/this.zoom+this.extent.z/2);
    this.camera.near=Math.max(.001,this.camera.position.z/1000);this.camera.far=Math.max(100,this.camera.position.z+this.extent.length()*10);this.camera.updateProjectionMatrix();this.render();
  }
  render(){if(this.ready&&this.renderer&&!this.contextLost)this.renderer.render(this.scene,this.camera);}
  stopRenderLoop(){if(this.frameRequest)cancelAnimationFrame(this.frameRequest);this.frameRequest=0;}
  resume(){if(this.frameRequest||!this.ready||document.hidden||this.contextLost)return;this.last=null;this.frameRequest=requestAnimationFrame(t=>this.tick(t));}
  tick(time){
    this.frameRequest=0;if(!this.ready||document.hidden||this.contextLost)return;
    const elapsed=this.last===null?0:Math.max(0,(time-this.last)/1000);this.last=time;
    if(this.playing){this.playbackTime+=elapsed*this.speed;if(this.mixer&&this.action)this.mixer.update(elapsed*this.speed);this.applyWheelTransforms();if(this.wheelEffects?.count)this.wheelEffects.update(this.playbackTime);
      if(!this.action&&this.playbackTime>=this.duration){this.duration=Math.ceil((this.playbackTime+1)/60)*60;this.totalFrames=Math.round(this.duration*this.fps);}
      if(this.action&&!this.meta.loop&&!this.wheelEffects?.count&&this.action.time>=this.duration-1e-6)this.playing=false;
    }
    if(this.lastRender===null||!this.frameLimit||time-this.lastRender>=1000/this.frameLimit-.3){this.render();this.lastRender=time;this.renderedFrames++;if(this.fpsStart===null)this.fpsStart=time;if(time-this.fpsStart>=750){this.measuredFps=this.renderedFrames*1000/(time-this.fpsStart);this.renderedFrames=0;this.fpsStart=time;}}
    if(time-this.lastNotify>=33||!this.playing){this.notify();this.lastNotify=time;}
    if(this.playing)this.frameRequest=requestAnimationFrame(t=>this.tick(t));else this.last=null;
  }
  notify(){this.dispatchEvent(new CustomEvent('time',{detail:this.info}));}
  get info(){const t=this.action?Math.min(this.duration,Math.max(0,this.action.time)):this.playbackTime;return{loaded:this.ready,playing:this.playing,frame:Math.min(this.totalFrames,Math.floor(t*this.fps+1e-5)+1),totalFrames:this.totalFrames,time:t,duration:this.duration,fps:this.fps,hasAnimation:Boolean(this.action||this.wheelEffects?.count),procedural:Boolean(this.wheelEffects?.count),proceduralMeshes:this.wheelEffects?.count||0,measuredFps:this.measuredFps,frameLimit:this.frameLimit,gizmoDragging:Boolean(this.transform?.dragging),wheelControls:[...new Set([...this.wheelBones.keys()].map(n=>n.split('_')[1]))],speed:this.speed};}
  toggle(){if(this.playing)this.pause();else this.play();}
  play(){if(!this.action&&!this.wheelEffects?.count)return;if(this.action&&!this.meta.loop&&this.action.time>=this.duration-1e-6){this.action.reset();this.action.play();this.playbackTime=0;}this.playing=true;this.lastRender=null;this.fpsStart=null;this.renderedFrames=0;this.resume();this.notify();}
  pause(){this.playing=false;this.stopRenderLoop();this.last=null;this.render();this.notify();}
  stop(){this.pause();this.seekFrame(1);}
  seekFrame(frame){if(!this.action&&!this.wheelEffects?.count)return;this.pause();const value=THREE.MathUtils.clamp(Number(frame)||1,1,this.totalFrames);this.playbackTime=THREE.MathUtils.clamp((value-1)/this.fps,0,this.duration);if(this.action){this.action.paused=false;this.action.enabled=true;this.action.time=this.playbackTime;this.mixer.update(0);}this.applyWheelTransforms();this.wheelEffects?.update(this.playbackTime);this.render();this.notify();}
  step(amount){this.seekFrame(this.info.frame+amount);}
  setFrameLimit(fps){const value=Number(fps)||0;this.frameLimit=[0,60,120,144,240].includes(value)?value:0;if(this.wheelEffects?.count&&!this.action){this.fps=this.frameLimit||60;this.totalFrames=Math.round(this.duration*this.fps);}this.lastRender=null;this.measuredFps=0;this.fpsStart=null;this.renderedFrames=0;this.notify();}
  setSpeed(speed){this.speed=THREE.MathUtils.clamp(Number(speed)||1,.1,4);this.notify();}
  applyWheelTransforms(){
    if(this.action)this.mixer.update(0);
    for(const [name,entry]of this.wheelBones){if(!this.action||!this.wheelAnimated.has(name))entry.bone.quaternion.copy(entry.rest);entry.source.copy(entry.bone.quaternion);const angle=Number(this.wheelPose[name])||0;if(angle)entry.bone.quaternion.multiply(new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0,1,0),angle));}this.model?.updateMatrixWorld(true);
  }
  applyWheelPose(pose={}){this.wheelPose={};for(const [name,value]of Object.entries(pose))if(/^wheel_(FL|FR|RL|RR)_(steer|roll)$/.test(name)&&Number.isFinite(Number(value)))this.wheelPose[name]=Number(value);if(this.action)this.mixer.update(0);this.applyWheelTransforms();this.render();}
  setPoseEnabled(enabled){if(this.transform)this.transform.enabled=Boolean(enabled);}
  selectWheel(corner,mode='steer'){this.wheelCorner=corner;this.wheelMode=mode;const entry=this.wheelBones.get(`wheel_${corner}_${mode}`);if(entry)this.transform.attach(entry.bone);else this.transform.detach();this.emitWheelPose();this.render();}
  setWheelAngle(degrees){const name=`wheel_${this.wheelCorner}_${this.wheelMode}`;if(!this.wheelBones.has(name))return;if(this.action)this.pause();let value=THREE.MathUtils.degToRad(Number(degrees)||0);if(this.wheelMode==='steer')value=THREE.MathUtils.clamp(value,-Math.PI/2,Math.PI/2);this.wheelPose[name]=value;if(this.action)this.mixer.update(0);this.applyWheelTransforms();this.emitWheelPose();this.render();}
  resetWheel(all=false){for(const name of Object.keys(this.wheelPose))if(all||name.startsWith(`wheel_${this.wheelCorner}_`))delete this.wheelPose[name];if(this.action)this.mixer.update(0);this.applyWheelTransforms();this.emitWheelPose();this.render();}
  emitWheelPose(){this.dispatchEvent(new CustomEvent('wheelpose',{detail:{pose:{...this.wheelPose},corner:this.wheelCorner,mode:this.wheelMode,angle:THREE.MathUtils.radToDeg(this.wheelPose[`wheel_${this.wheelCorner}_${this.wheelMode}`]||0)}}));}
  disposeModel(root){if(!root)return;const geometry=new Set(),materials=new Set(),textures=new Set();root.traverse(o=>{if(o.geometry)geometry.add(o.geometry);for(const material of o.material?(Array.isArray(o.material)?o.material:[o.material]):[]){materials.add(material);Object.values(material).forEach(v=>{if(v?.isTexture)textures.add(v);});}});textures.forEach(t=>{t.dispose();t.source?.data?.close?.();});materials.forEach(m=>m.dispose());geometry.forEach(g=>g.dispose());}
  async load(url,meta={}){
    const request=++this.revision;if(!this.renderer)throw new Error(this.failure);
    this.dispatchEvent(new Event('loading'));
    let gltf;try {gltf=await new GLTFLoader().loadAsync(url);}catch(error){if(request===this.revision)this.dispatchEvent(new CustomEvent('error',{detail:'The model preview could not load. The original model is unchanged. '+(error.message||'')}));throw error;}
    if(request!==this.revision){this.disposeModel(gltf.scene);return false;}
    const root=gltf.scene;const effects=await createWheelEffects(root);if(request!==this.revision){effects.dispose();this.disposeModel(root);return false;}const mixer=new THREE.AnimationMixer(root);const clip=gltf.animations?.[0];let action=null;
    if(clip&&clip.duration>0){action=mixer.clipAction(clip);action.setLoop(meta.loop?THREE.LoopRepeat:THREE.LoopOnce,meta.loop?Infinity:1);action.clampWhenFinished=!meta.loop;action.play();mixer.setTime(0);}
    root.updateMatrixWorld(true);const bounds=new THREE.Box3();
    if(action){const samples=Math.min(90,Math.max(24,Math.ceil(clip.duration*8)));for(let i=0;i<=samples;i++){mixer.setTime(clip.duration*i/samples);root.updateMatrixWorld(true);root.traverse(o=>{if(o.isSkinnedMesh)o.computeBoundingBox();});bounds.union(new THREE.Box3().setFromObject(root,true));}mixer.setTime(0);action.reset();action.play();root.updateMatrixWorld(true);}
    else bounds.setFromObject(root,true);
    if(bounds.isEmpty()) {this.disposeModel(root);throw new Error('The imported file has no visible model geometry.');}
    const size=bounds.getSize(new THREE.Vector3());const scale=3.3/Math.max(size.x,size.y,size.z,.001);const centered=new THREE.Group();centered.position.copy(bounds.getCenter(new THREE.Vector3())).negate();centered.add(root);const holder=new THREE.Group();holder.add(centered);holder.scale.setScalar(scale);
    this.pause();this.wheelEffects?.dispose();this.wheelEffects=effects;this.playbackTime=0;if(this.mixer)this.mixer.stopAllAction();if(this.model){this.pivot.remove(this.model);this.disposeModel(this.model);}this.model=holder;this.pivot.add(holder);this.mixer=mixer;this.action=action;this.wheelBones.clear();this.wheelAnimated.clear();this.transform.detach();root.traverse(bone=>{if(bone.isBone&&/^wheel_(FL|FR|RL|RR)_(steer|roll)$/.test(bone.name)){this.wheelBones.set(bone.name,{bone,rest:bone.quaternion.clone(),source:bone.quaternion.clone()});if(clip?.tracks.some(t=>t.name.includes(bone.name+'.quaternion')||t.name.includes('['+bone.name+']')))this.wheelAnimated.add(bone.name);}});this.applyWheelTransforms();this.meta=meta;this.duration=clip?.duration||(effects.count?60:0);this.fps=Number(meta.fps)>0?Number(meta.fps):30;
    this.totalFrames=action?Math.max(1,Math.round(Number(meta.playbackFrames)||(this.meta.loop?this.duration*this.fps:(Number(meta.frames)||this.duration*this.fps+1)))):1;
    if(effects.count&&!action){this.fps=this.frameLimit||60;this.totalFrames=Math.round(this.duration*this.fps);}
    this.extent.copy(size).multiplyScalar(scale);this.extent.x=Math.max(this.extent.x,this.extent.z);this.grid.position.y=-size.y*scale/2;this.grid.visible=this.gridVisible;this.ready=true;this.fit();this.playing=Boolean(action||effects.count)&&!this.reduced.matches;
    this.render();this.notify();this.resume();this.dispatchEvent(new Event('loaded'));return true;
  }
  clear(){++this.revision;this.pause();this.wheelEffects?.dispose();this.wheelEffects=null;this.playbackTime=0;this.transform?.detach();this.wheelBones.clear();if(this.mixer)this.mixer.stopAllAction();if(this.model){this.pivot.remove(this.model);this.disposeModel(this.model);}this.model=null;this.mixer=null;this.action=null;this.ready=false;this.duration=0;this.totalFrames=1;if(this.renderer)this.renderer.clear();this.notify();}
  dispose(){this.clear();this.observer?.disconnect();this.transform?.dispose();this.environment?.dispose();this.renderer?.dispose();}
}
