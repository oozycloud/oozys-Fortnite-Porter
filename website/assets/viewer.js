import * as THREE from 'three';
import { GLTFLoader } from './vendor/GLTFLoader.js';
import { RoomEnvironment } from './vendor/RoomEnvironment.js';

const canvas = document.querySelector('#model-canvas');
const stage = document.querySelector('#studio');
const status = document.querySelector('#model-status');
const reduced = matchMedia('(prefers-reduced-motion: reduce)');
const assetUrl = './assets/renegade-raider-scenario.glb?v=scenario4';
const description = 'Renegade Raider · Black & Gold · Scenario';
let renderer;
try {
  renderer = new THREE.WebGLRenderer({canvas, antialias: true, alpha: true, powerPreference: 'low-power'});
} catch {
  status.textContent = 'the 3d preview is unavailable on this device. you can still begin below.';
  stage.setAttribute('aria-busy', 'false');
}

if (renderer) {
  renderer.setPixelRatio(Math.min(devicePixelRatio, 1.75));
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.05;
  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(32, 1, .01, 100);
  const pmrem = new THREE.PMREMGenerator(renderer);
  const room = new RoomEnvironment();
  const environment = pmrem.fromScene(room, .055);
  scene.environment = environment.texture;
  scene.environmentIntensity = .8;
  room.dispose(); pmrem.dispose();
  const key = new THREE.DirectionalLight(0xfffaf3, 2.8); key.position.set(-3, 5, 4); scene.add(key);
  const fill = new THREE.DirectionalLight(0xffffff, 1.4); fill.position.set(4, 0, 2); scene.add(fill);
  const pivot = new THREE.Group(); scene.add(pivot);
  const baseTilt = new THREE.Euler(0, -.05, 0);
  pivot.rotation.copy(baseTilt);
  const fittedSize = new THREE.Vector3(2.4, 3.5, 1.5);
  const orbit = new THREE.Vector2(), angularVelocity = new THREE.Vector2();
  let drag = null, pinch = null;
  const pointers = new Map();
  let zoom = 1;
  const zoomLimits = [.6, 2.2];
  const zoomOut = document.querySelector('[data-zoom="out"]');
  const zoomIn = document.querySelector('[data-zoom="in"]');
  const resetViewButton = document.querySelector('[data-view-reset]');
  const pitchLimit = .45;
  let loaded = false, active = true, frame = 0, last = null, mixer, action, loopDuration = 0, savedTime = 0;
  let disposed = false, contextLost = false;

  function render() { if (loaded && !disposed && !contextLost) renderer.render(scene, camera); }
  function resize() {
    const width = stage.clientWidth, height = stage.clientHeight;
    if (!width || !height || disposed) return;
    renderer.setSize(width, height, false);
    camera.aspect = width / height;
    const tangent = Math.tan(THREE.MathUtils.degToRad(camera.fov / 2));
    camera.position.set(0, 0, Math.max(fittedSize.y / 2 / tangent, fittedSize.x / 2 / tangent / camera.aspect) * 1.24 / zoom + fittedSize.z / 2);
    camera.far = Math.max(100, camera.position.z + fittedSize.z * 4);
    camera.updateProjectionMatrix();
    render();
  }
  const observer = new ResizeObserver(resize); observer.observe(stage);
  function applyOrbit() {
    pivot.rotation.set(baseTilt.x + orbit.y, baseTilt.y + orbit.x, baseTilt.z);
    stage.dataset.orbitYaw = String(orbit.x); stage.dataset.orbitPitch = String(orbit.y);
  }
  function syncZoomControls() {
    stage.dataset.zoom = String(zoom);
    zoomOut.disabled = !loaded || zoom <= zoomLimits[0] + .0001;
    zoomIn.disabled = !loaded || zoom >= zoomLimits[1] - .0001;
    resetViewButton.disabled = !loaded;
  }
  function setZoom(value) {
    zoom = THREE.MathUtils.clamp(value, ...zoomLimits); syncZoomControls(); resize(); resume();
  }
  function resetView() {
    orbit.set(0, 0); angularVelocity.set(0, 0); zoom = 1;
    syncZoomControls(); applyOrbit(); resize(); resume();
  }
  zoomOut.addEventListener('click', () => setZoom(zoom / 1.12));
  zoomIn.addEventListener('click', () => setZoom(zoom * 1.12));
  resetViewButton.addEventListener('click', resetView);
  stage.addEventListener('wheel', event => {
    // Keep browser Ctrl/+ zoom intact. Wheel scrolling elsewhere stays native.
    if (!loaded || !active || event.ctrlKey || !event.deltaY) return;
    event.preventDefault();
    const unit = event.deltaMode === 1 ? 16 : event.deltaMode === 2 ? stage.clientHeight : 1;
    setZoom(zoom * Math.exp(-THREE.MathUtils.clamp(event.deltaY * unit, -500, 500) * .0012));
  }, {passive: false});
  function pinchDistance() {
    const pair = [...pointers.values()].slice(0, 2);
    return Math.hypot(pair[1].x - pair[0].x, pair[1].y - pair[0].y);
  }
  stage.addEventListener('pointerdown', event => {
    if (!loaded || event.target.closest('.preview-controls') || (event.pointerType !== 'touch' && !event.isPrimary) || (event.pointerType === 'mouse' && event.button !== 0)) return;
    event.preventDefault(); angularVelocity.set(0, 0);
    pointers.set(event.pointerId, {x: event.clientX, y: event.clientY, time: event.timeStamp});
    stage.setPointerCapture(event.pointerId); stage.classList.add('dragging');
    if (pointers.size === 1) drag = {id: event.pointerId, ...pointers.get(event.pointerId)};
    else {drag = null; pinch = {distance: Math.max(pinchDistance(), 1), zoom};}
    stage.focus({preventScroll: true});
  });
  stage.addEventListener('pointermove', event => {
    if (!pointers.has(event.pointerId)) return;
    event.preventDefault();
    pointers.set(event.pointerId, {x: event.clientX, y: event.clientY, time: event.timeStamp});
    if (pinch && pointers.size >= 2) {
      setZoom(pinch.zoom * pinchDistance() / pinch.distance); return;
    }
    if (!drag || event.pointerId !== drag.id) return;
    const bounds = stage.getBoundingClientRect();
    const dx = (event.clientX - drag.x) / Math.max(bounds.width, 1) * Math.PI * 2;
    const dy = (event.clientY - drag.y) / Math.max(bounds.height, 1) * Math.PI * .65;
    const elapsed = THREE.MathUtils.clamp((event.timeStamp - drag.time) / 1000, .008, .06);
    orbit.x += dx; orbit.y = THREE.MathUtils.clamp(orbit.y + dy, -pitchLimit, pitchLimit);
    if (!reduced.matches) angularVelocity.set(THREE.MathUtils.clamp(dx / elapsed, -4, 4), THREE.MathUtils.clamp(dy / elapsed, -2, 2));
    drag.x = event.clientX; drag.y = event.clientY; drag.time = event.timeStamp;
    applyOrbit(); render(); resume();
  });
  function endDrag(event) {
    if (!pointers.has(event.pointerId)) return;
    if (pinch || event.type !== 'pointerup' || !drag || event.timeStamp - drag.time > 100 || reduced.matches) angularVelocity.set(0, 0);
    pointers.delete(event.pointerId); drag = null; pinch = null;
    if (stage.hasPointerCapture(event.pointerId)) stage.releasePointerCapture(event.pointerId);
    if (pointers.size === 1) {
      const [id, point] = pointers.entries().next().value; drag = {id, ...point};
    } else if (pointers.size >= 2) pinch = {distance: Math.max(pinchDistance(), 1), zoom};
    else stage.classList.remove('dragging');
    resume();
  }
  for (const name of ['pointerup', 'pointercancel', 'lostpointercapture']) stage.addEventListener(name, endDrag);
  stage.addEventListener('keydown', event => {
    if (!loaded) return;
    const step = event.shiftKey ? Math.PI / 6 : Math.PI / 12;
    if (event.key === 'ArrowLeft') orbit.x -= step;
    else if (event.key === 'ArrowRight') orbit.x += step;
    else if (event.key === 'ArrowUp') orbit.y = Math.max(-pitchLimit, orbit.y - .08);
    else if (event.key === 'ArrowDown') orbit.y = Math.min(pitchLimit, orbit.y + .08);
    else if (event.key === '+' || event.key === '=') {event.preventDefault(); setZoom(zoom * 1.12); return;}
    else if (event.key === '-' || event.key === '_') {event.preventDefault(); setZoom(zoom / 1.12); return;}
    else if (event.key === 'Home' || event.key === '0') {event.preventDefault(); resetView(); return;}
    else return;
    event.preventDefault(); angularVelocity.set(0, 0); applyOrbit(); render(); resume();
  });
  function stop() {
    cancelAnimationFrame(frame); frame = 0; last = null; angularVelocity.set(0, 0);
    const ids = [...pointers.keys()]; pointers.clear(); drag = null; pinch = null;
    stage.classList.remove('dragging');
    for (const id of ids) if (stage.hasPointerCapture(id)) stage.releasePointerCapture(id);
  }
  function animate(time) {
    frame = 0;
    if (!active || document.hidden || !loaded || disposed || contextLost) {last = null; return;}
    // Resetting last on every pause avoids advancing through the hidden interval.
    const dt = last === null ? 0 : Math.max(0, (time - last) / 1000); last = time;
    if (reduced.matches) angularVelocity.set(0, 0);
    else {
      // Scenario keeps original speed while drag/orbit is purely a view transform.
      mixer.update(dt);
      if (!drag && !pinch) {
        const step = Math.min(dt, .04);
        orbit.addScaledVector(angularVelocity, step);
        orbit.y = THREE.MathUtils.clamp(orbit.y, -pitchLimit, pitchLimit);
        if (Math.abs(orbit.y) >= pitchLimit) angularVelocity.y = 0;
        angularVelocity.multiplyScalar(Math.exp(-7 * step));
        if (angularVelocity.lengthSq() < .00001) angularVelocity.set(0, 0);
      }
    }
    applyOrbit();
    render();
    if (!reduced.matches) frame = requestAnimationFrame(animate);
  }
  function resume() {
    if (!frame && loaded && active && !document.hidden && !disposed && !contextLost) {
      last = null; frame = requestAnimationFrame(animate);
    }
  }
  document.addEventListener('visibilitychange', () => {if (document.hidden) stop(); else resume();});
  document.addEventListener('porter:page', event => {
    active = event.detail === 'entrance';
    if (active) {resize(); resume();} else stop();
  });
  function motionPreferenceChanged() {
    stop(); applyOrbit();
    if (action) {
      if (reduced.matches) {savedTime = action.time; mixer.setTime(loopDuration * .35);}
      else {mixer.setTime(savedTime);}
    }
    if (loaded) stage.setAttribute('aria-label', `${description}${reduced.matches ? ' · still preview' : ' · looping 3d preview'}`);
    render(); resume();
  }
  reduced.addEventListener('change', motionPreferenceChanged);
  canvas.addEventListener('webglcontextlost', event => {
    event.preventDefault(); contextLost = true; stop(); status.hidden = false;
    status.textContent = 'the 3d preview was paused by your browser. you can still begin below.';
  });
  canvas.addEventListener('webglcontextrestored', () => {contextLost = false; if (loaded) {status.hidden = true; resize(); resume();}});

  function metadataBounds(gltf) {
    const values = [gltf.scene.userData?.porterAnimatedBounds, gltf.userData?.porterAnimatedBounds];
    for (const value of values) {
      if (Array.isArray(value?.min) && Array.isArray(value?.max) && value.min.length === 3 && value.max.length === 3 && [...value.min, ...value.max].every(Number.isFinite)) {
        const box = new THREE.Box3(new THREE.Vector3(...value.min), new THREE.Vector3(...value.max));
        if (!box.isEmpty()) return box;
      }
    }
    return null;
  }
  function clipBounds(model, animationMixer, duration) {
    const box = new THREE.Box3();
    // Sample the whole loop, rather than fitting its first pose and clipping
    // Scenario's raised arms. Padding also covers motion between samples.
    const samples = Math.min(240, Math.max(60, Math.ceil(duration * 30)));
    for (let i = 0; i <= samples; i++) {
      animationMixer.setTime(duration * i / samples); model.updateMatrixWorld(true);
      model.traverse(object => {if (object.isSkinnedMesh) object.computeBoundingBox();});
      box.union(new THREE.Box3().setFromObject(model, true));
    }
    animationMixer.setTime(0); model.updateMatrixWorld(true);
    return box;
  }

  async function loadHero() {
    stage.setAttribute('aria-busy', 'true');
    status.hidden = false; status.textContent = 'preparing renegade raider';
    try {
      const gltf = await new GLTFLoader().loadAsync(assetUrl);
      const model = gltf.scene;
      const clips = gltf.animations || [];
      const clip = clips.find(item => /scenario/i.test(item.name) && !/intro|outro|breakout/i.test(item.name)) || clips.find(item => !/_(intro|outro|breakout|start|end)(?:_|$)/i.test(item.name));
      if (!clip || !Number.isFinite(clip.duration) || clip.duration <= 0) throw new Error('Scenario animation is missing');
      const durationValues = [clip.userData?.porterLoopDuration, clip.userData?.loopDuration, gltf.scene.userData?.porterLoopDuration, gltf.scene.userData?.loopDuration, gltf.userData?.porterLoopDuration, gltf.userData?.loopDuration];
      loopDuration = Number(durationValues.find(value => Number.isFinite(Number(value)) && Number(value) > 0)) || clip.duration;
      if (loopDuration > clip.duration + .001) throw new Error('Loop duration exceeds supplied animation');
      const loopClip = clip.clone(); loopClip.duration = loopDuration;
      mixer = new THREE.AnimationMixer(model);
      action = mixer.clipAction(loopClip); action.setLoop(THREE.LoopRepeat, Infinity); action.timeScale = 1; action.play();
      const bounds = metadataBounds(gltf) || clipBounds(model, mixer, loopDuration);
      const extent = bounds.getSize(new THREE.Vector3());
      if (bounds.isEmpty() || !Number.isFinite(extent.length()) || extent.length() <= 0) throw new Error('Model bounds are invalid');
      // Centre a parent, leaving animated scene/root transforms untouched.
      const centered = new THREE.Group(); centered.position.copy(bounds.getCenter(new THREE.Vector3())).negate(); centered.add(model);
      const holder = new THREE.Group(); holder.add(centered); holder.scale.setScalar(3.5 / Math.max(extent.x, extent.y, extent.z));
      pivot.add(holder); pivot.updateMatrixWorld(true);
      // The offset group centres the original bounds before scaling/tilting.
      fittedSize.copy(extent).multiplyScalar(holder.scale.x);
      // A full yaw turn must fit the depth as well as the front silhouette.
      const horizontalSpan = Math.hypot(fittedSize.x, fittedSize.z);
      fittedSize.y += horizontalSpan * Math.sin(pitchLimit);
      fittedSize.x = horizontalSpan; fittedSize.z = horizontalSpan;
      mixer.setTime(reduced.matches ? loopDuration * .35 : 0);
      loaded = true; syncZoomControls(); status.hidden = true; canvas.hidden = false; canvas.classList.add('ready');
      stage.setAttribute('aria-label', `${description}${reduced.matches ? ' · still preview' : ' · looping 3d preview'}`);
      stage.dataset.animationDuration = String(loopDuration); stage.dataset.animation = clip.name;
      resize(); resume();
    } catch (error) {
      status.hidden = false; status.textContent = 'the character preview could not load. you can still begin below.';
      stage.setAttribute('aria-label', 'Renegade Raider preview unavailable');
      console.warn('Renegade Raider preview unavailable:', error);
    } finally {stage.setAttribute('aria-busy', 'false');}
  }
  loadHero();
}
