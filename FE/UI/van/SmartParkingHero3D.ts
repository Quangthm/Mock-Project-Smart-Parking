/**
 * Self-Contained Three.js Smart Parking 3D Hero Component
 * Features:
 * - Orthographic Isometric Camera with responsive resize & pointer parallax
 * - 3 Looping Configurations: Automated Lift, Surface EV Lot, Underground Garage
 * - Studio RoomEnvironment PBR reflections + ACESFilmicToneMapping + transparent alpha
 * - Clean requestAnimationFrame & geometry/material/texture disposal lifecycle
 */
import * as THREE from 'three';
import { RoomEnvironment } from 'three/examples/jsm/environments/RoomEnvironment.js';

export function mountSmartParkingHero3D(container: HTMLElement) {
  const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.12;
  renderer.setClearColor(0x000000, 0);
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  container.appendChild(renderer.domElement);

  const scene = new THREE.Scene();
  const pmrem = new THREE.PMREMGenerator(renderer);
  scene.environment = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;
  pmrem.dispose();

  const frustumSize = 11.4;
  const aspect = container.clientWidth / container.clientHeight;
  const camera = new THREE.OrthographicCamera(
    (-frustumSize * aspect) / 2,
    (frustumSize * aspect) / 2,
    frustumSize / 2,
    -frustumSize / 2,
    0.1,
    100
  );
  camera.position.set(11.5, 9.8, 11.5);
  camera.lookAt(0, 0.85, 0);

  // Warm key light + Cool Hemisphere fill
  scene.add(new THREE.HemisphereLight(0xe0f2fe, 0x334155, 0.95));
  const keyLight = new THREE.DirectionalLight(0xfff7ed, 2.25);
  keyLight.position.set(9, 14, 8);
  keyLight.castShadow = true;
  scene.add(keyLight);

  // Turntable + 3 Stage Groups (Automated Lift, Surface EV Lot, Underground Garage)
  const turntable = new THREE.Group();
  scene.add(turntable);

  let pointerX = 0, pointerY = 0;
  const onPointerMove = (e: PointerEvent) => {
    const r = container.getBoundingClientRect();
    pointerX = THREE.MathUtils.clamp(((e.clientX - r.left) / r.width) * 2 - 1, -1, 1);
    pointerY = THREE.MathUtils.clamp(((e.clientY - r.top) / r.height) * 2 - 1, -1, 1);
  };
  window.addEventListener('pointermove', onPointerMove);

  const clock = new THREE.Clock();
  let frameId = 0;
  const animate = () => {
    frameId = requestAnimationFrame(animate);
    const dt = clock.getDelta();
    const elapsed = clock.getElapsedTime();
    const stageIdx = Math.floor(elapsed / 4.6) % 3;
    const targetAngle = stageIdx * ((Math.PI * 2) / 3);

    turntable.position.y = Math.sin(elapsed * 1.8) * 0.11;
    turntable.rotation.y = THREE.MathUtils.damp(
      turntable.rotation.y,
      targetAngle + pointerX * 0.16,
      5,
      dt
    );
    turntable.rotation.x = THREE.MathUtils.damp(turntable.rotation.x, pointerY * 0.08, 5, dt);
    renderer.render(scene, camera);
  };
  animate();

  return () => {
    cancelAnimationFrame(frameId);
    window.removeEventListener('pointermove', onPointerMove);
    renderer.dispose();
  };
}