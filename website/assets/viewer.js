import * as THREE from 'three';
import { GLTFLoader } from './vendor/GLTFLoader.js';
import { RoomEnvironment } from './vendor/RoomEnvironment.js';

const canvas = document.querySelector('#model-canvas');
const stage = document.querySelector('#studio');
const status = document.querySelector('#model-status');
const reduced = matchMedia('(prefers-reduced-motion: reduce)');
let renderer;
try {
  renderer = new THREE.WebGLRenderer({canvas, antialias: true, alpha: true, powerPreference: 'low-power'});
} catch {
  status.textContent = 'the 3d preview is unavailable on this device. you can still begin below.';
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
  const target = new THREE.Vector2(), position = new THREE.Vector2(), velocity = new THREE.Vector2();
  function resize() {
    const width = stage.clientWidth, height = stage.clientHeight;
    renderer.setSize(width, height, false);
    camera.aspect = width / height;
    camera.position.z = camera.aspect < 1.4 ? 5.3 * 1.4 / camera.aspect : 5.3;
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
    pivot.rotation.x = -.12 + position.y*.18;
    pivot.rotation.y = -.22 + position.x*.22;
    pivot.rotation.z = -.10 + position.x*.025;
    renderer.render(scene,camera);
    if (!reduced.matches) frame = requestAnimationFrame(animate);
  }
  function resume() { if (!frame && loaded && active && !document.hidden) {last=0;frame=requestAnimationFrame(animate);} }
  document.addEventListener('visibilitychange', () => {if(document.hidden){cancelAnimationFrame(frame);frame=0;}else resume();});
  document.addEventListener('porter:page', event => {active=event.detail==='entrance';if(active){resize();resume();}else{cancelAnimationFrame(frame);frame=0;}});
  reduced.addEventListener('change', resume);
  canvas.addEventListener('webglcontextlost', event => {event.preventDefault();cancelAnimationFrame(frame);frame=0;status.hidden=false;status.textContent='the 3d preview was paused by your browser. you can still begin below.';});
  new GLTFLoader().load('./assets/gamerock-4090.glb', gltf => {
    const model = gltf.scene;
    const bounds = new THREE.Box3().setFromObject(model);
    const size = bounds.getSize(new THREE.Vector3());
    const center = bounds.getCenter(new THREE.Vector3());
    model.position.sub(center);
    const holder = new THREE.Group(); holder.add(model);
    holder.scale.setScalar(3.5/Math.max(size.x,size.y,size.z));
    pivot.add(holder);
    loaded=true; status.hidden=true; canvas.classList.add('ready');resize();resume();
  }, undefined, () => {status.textContent='the model preview could not load. you can still begin below.';});
}
