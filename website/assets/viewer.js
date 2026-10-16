import * as THREE from 'three';
import { GLTFLoader } from './vendor/GLTFLoader.js';
import { RoomEnvironment } from './vendor/RoomEnvironment.js';

const canvas = document.querySelector('#model-canvas');
const stage = document.querySelector('#studio');
const status = document.querySelector('#model-status');
const reduced = matchMedia('(prefers-reduced-motion: reduce)');
const buttons = [...document.querySelectorAll('[data-model]')];
const credit = document.querySelector('#model-credit');
const models = {
  gamerock: {file:'gamerock-4090.glb', label:'palit gamerock · rtx 4090', tilt:[-.12,-.22,-.10]},
  ghoul: {file:'pink-ghoul-trooper.glb', label:'fortnite · pink ghoul trooper', tilt:[0,-.12,0]},
  axe: {file:'leviathan-axe.glb', label:'fortnite · leviathan axe · no effects', tilt:[0,-.12,-.08]},
  taichi: {file:'asrock-taichi.glb', label:'asrock x870e taichi · motherboard', tilt:[-.04,-.14,-.06]},
};
let renderer;
try {
  renderer = new THREE.WebGLRenderer({canvas, antialias: true, alpha: true, powerPreference: 'low-power'});
} catch {
  status.textContent = 'the 3d preview is unavailable on this device. you can still begin below.';
  buttons.forEach(button => {button.disabled=true;});
}
if (renderer) {
  renderer.setPixelRatio(Math.min(devicePixelRatio, 1.75));
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.05;
  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(32, 1, .01, 100);
  camera.position.set(0, 0, 5.3);
  const pmrem = new THREE.PMREMGenerator(renderer);
  const room = new RoomEnvironment();
  const environment = pmrem.fromScene(room, .055);
  scene.environment = environment.texture;
  scene.environmentIntensity = .8;
  room.dispose(); pmrem.dispose();
  const key = new THREE.DirectionalLight(0xfffaf3, 2.8); key.position.set(-3, 5, 4); scene.add(key);
  const fill = new THREE.DirectionalLight(0xffffff, 1.4); fill.position.set(4, 0, 2); scene.add(fill);
  const pivot = new THREE.Group(); scene.add(pivot);
  let loaded = false, active = true, frame = 0, last = 0;
  let selected = 'gamerock', revision = 0;
  const cache = new Map(), pending = new Map();
  const loader = new GLTFLoader();
  const fittedSize = new THREE.Vector3(3.5,1.8,.5);
  const target = new THREE.Vector2(), position = new THREE.Vector2(), velocity = new THREE.Vector2();
  function resize() {
    const width = stage.clientWidth, height = stage.clientHeight;
    if (!width || !height) return;
    renderer.setSize(width, height, false);
    camera.aspect = width / height;
    const tangent = Math.tan(THREE.MathUtils.degToRad(camera.fov/2));
    camera.position.z = Math.max(fittedSize.y/2/tangent, fittedSize.x/2/tangent/camera.aspect)*1.32 + fittedSize.z/2;
    camera.updateProjectionMatrix();
    if (loaded) renderer.render(scene, camera);
  }
  new ResizeObserver(resize).observe(stage);
  stage.addEventListener('pointermove', event => {
    if (event.pointerType !== 'mouse' || reduced.matches) return;
    const bounds = stage.getBoundingClientRect();
    target.set((event.clientX-bounds.left)/bounds.width-.5, (event.clientY-bounds.top)/bounds.height-.5);
  });
  stage.addEventListener('pointerleave', () => target.set(0,0));
  function animate(time) {
    frame = 0;
    if (!active || document.hidden || !loaded) return;
    const dt = Math.min((time-last)/1000 || .016, .04); last = time;
    if (reduced.matches) { position.set(0,0); velocity.set(0,0); }
    else {
      velocity.x += ((target.x-position.x)*28-velocity.x*8)*dt;
      velocity.y += ((target.y-position.y)*28-velocity.y*8)*dt;
      position.addScaledVector(velocity,dt);
    }
    const tilt = models[selected].tilt;
    pivot.rotation.x = tilt[0] + position.y*.18;
    pivot.rotation.y = tilt[1] + position.x*.22;
    pivot.rotation.z = tilt[2] + position.x*.025;
    renderer.render(scene,camera);
    if (!reduced.matches) frame = requestAnimationFrame(animate);
  }
  function resume() { if (!frame && loaded && active && !document.hidden) {last=0;frame=requestAnimationFrame(animate);} }
  document.addEventListener('visibilitychange', () => {if(document.hidden){cancelAnimationFrame(frame);frame=0;}else resume();});
  document.addEventListener('porter:page', event => {active=event.detail==='entrance';if(active){resize();resume();}else{cancelAnimationFrame(frame);frame=0;}});
  reduced.addEventListener('change', resume);
  canvas.addEventListener('webglcontextlost', event => {event.preventDefault();cancelAnimationFrame(frame);frame=0;status.hidden=false;status.textContent='the 3d preview was paused by your browser. you can still begin below.';});
  function dispose(model) {
    const textures=new Set(), materials=new Set(), geometries=new Set();
    model.traverse(object => {
      if(object.geometry) geometries.add(object.geometry);
      for(const material of object.material ? (Array.isArray(object.material)?object.material:[object.material]) : []) {
        materials.add(material);
        Object.values(material).forEach(value => {if(value?.isTexture)textures.add(value);});
      }
    });
    textures.forEach(texture => {texture.dispose();texture.source?.data?.close?.();});
    materials.forEach(material=>material.dispose());geometries.forEach(geometry=>geometry.dispose());
  }
  function trimCache() {
    for(const [key,model] of cache) {
      if(cache.size<=2) break;
      if(key!==selected){cache.delete(key);dispose(model);}
    }
  }
  async function loadModel(key) {
    if(cache.has(key)) {const model=cache.get(key);cache.delete(key);cache.set(key,model);return model;}
    if(!pending.has(key)) pending.set(key,loader.loadAsync(`./assets/${models[key].file}`).then(gltf=>{
      const model=gltf.scene;
      const bounds=new THREE.Box3().setFromObject(model),size=bounds.getSize(new THREE.Vector3());
      model.position.sub(bounds.getCenter(new THREE.Vector3()));
      const holder=new THREE.Group();holder.add(model);holder.scale.setScalar(3.5/Math.max(size.x,size.y,size.z));
      cache.set(key,holder);return holder;
    }).finally(()=>pending.delete(key)));
    return pending.get(key);
  }
  async function select(key) {
    const request=++revision;selected=key;loaded=false;
    cancelAnimationFrame(frame);frame=0;
    pivot.clear();target.set(0,0);position.set(0,0);velocity.set(0,0);
    pivot.rotation.set(...models[key].tilt);
    canvas.hidden=true;canvas.classList.remove('ready');
    stage.setAttribute('aria-busy','true');stage.setAttribute('aria-label',`Loading ${models[key].label}`);
    credit.textContent=models[key].label;
    buttons.forEach(button=>button.setAttribute('aria-pressed',String(button.dataset.model===key)));
    status.hidden=false;status.textContent=`preparing ${models[key].label}`;
    try {
      const model=await loadModel(key);
      if(request!==revision){trimCache();return;}
      pivot.add(model);pivot.updateMatrixWorld(true);
      new THREE.Box3().setFromObject(pivot).getSize(fittedSize);
      loaded=true;status.hidden=true;canvas.hidden=false;canvas.classList.add('ready');
      stage.setAttribute('aria-label',`Interactive textured ${models[key].label}`);
      resize();resume();trimCache();
    } catch {
      if(request!==revision)return;
      status.textContent=`${models[key].label} could not load. select another model or begin below.`;
      stage.setAttribute('aria-label',`${models[key].label} preview unavailable`);
    } finally {if(request===revision)stage.setAttribute('aria-busy','false');}
  }
  buttons.forEach((button,index)=>{
    button.addEventListener('click',()=>select(button.dataset.model));
    button.addEventListener('keydown',event=>{
      const next=event.key==='ArrowRight'?(index+1)%buttons.length:event.key==='ArrowLeft'?(index+buttons.length-1)%buttons.length:event.key==='Home'?0:event.key==='End'?buttons.length-1:null;
      if(next!==null){event.preventDefault();buttons[next].focus();buttons[next].click();}
    });
  });
  select('gamerock');
}
