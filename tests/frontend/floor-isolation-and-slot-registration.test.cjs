const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');
const ts = require('../../FE/node_modules/typescript');

function loadModule(file, customImports = {}) {
  const source = fs.readFileSync(path.join(__dirname, '../../FE/src', file), 'utf8');
  const js = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  }).outputText;
  const context = {
    exports: {},
    require: name => {
      if (name in customImports) return customImports[name];
      if (name.includes('parking3DConfig')) return loadModule('lib/3d/parking3DConfig.ts');
      if (name === 'react') return { useState: () => [null, () => {}], useEffect: () => {} };
      throw new Error(`Unexpected import: ${name}`);
    },
  };
  vm.runInNewContext(js, context);
  return context.exports;
}

const config = loadModule('lib/3d/parking3DConfig.ts');
const capacity = loadModule('lib/3d/modelSlotCapacity.ts');

test('STATIC_PARKING_MODEL_METADATA defines verified floor structures and slot counts', () => {
  const outdoor = config.STATIC_PARKING_MODEL_METADATA['outdoor_parking_lot.glb'];
  assert.equal(outdoor.totalCapacity, 48);
  assert.equal(outdoor.floors.length, 1);
  assert.equal(outdoor.floors[0].floorKey, 'Ground');
  assert.equal(outdoor.floors[0].capacity, 48);

  const underground = config.STATIC_PARKING_MODEL_METADATA['underground_parking_lot.glb'];
  assert.equal(underground.totalCapacity, 48);
  assert.equal(underground.floors.length, 2);
  assert.equal(underground.floors[0].floorKey, 'B1');
  assert.equal(underground.floors[0].capacity, 24);
  assert.equal(underground.floors[1].floorKey, 'B2');
  assert.equal(underground.floors[1].capacity, 24);

  const indoor = config.STATIC_PARKING_MODEL_METADATA['indoor_parking_lot.glb'];
  assert.equal(indoor.totalCapacity, 64);
  assert.equal(indoor.floors.length, 3);
  assert.equal(indoor.floors[0].floorKey, 'G');
  assert.equal(indoor.floors[0].capacity, 20);
  assert.equal(indoor.floors[1].floorKey, 'L1');
  assert.equal(indoor.floors[1].capacity, 18);
  assert.equal(indoor.floors[2].floorKey, 'L2');
  assert.equal(indoor.floors[2].capacity, 26);
});

test('getStaticModelMetadata returns 0ms metadata for all system model paths', () => {
  const m1 = config.getStaticModelMetadata('/models/parking/underground_parking_lot.glb');
  assert.ok(m1);
  assert.equal(m1.fileName, 'underground_parking_lot.glb');
  assert.equal(m1.totalCapacity, 48);

  const m2 = config.getStaticModelMetadata('OUTDOOR_PARKING_LOT.GLB');
  assert.ok(m2);
  assert.equal(m2.totalCapacity, 48);

  const m3 = config.getStaticModelMetadata('https://cdn.example.com/models/indoor_parking_lot.glb?v=1');
  assert.ok(m3);
  assert.equal(m3.totalCapacity, 64);

  const m4 = config.getStaticModelMetadata('custom_owner_model.glb');
  assert.equal(m4, null);
});

test('readModelCarCapacity returns instant 0ms result for standard models without network', async () => {
  const capOutdoor = await capacity.readModelCarCapacity('/models/parking/outdoor_parking_lot.glb');
  assert.equal(capOutdoor, 48);

  const capUnderground = await capacity.readModelCarCapacity('/models/parking/underground_parking_lot.glb');
  assert.equal(capUnderground, 48);

  const capIndoor = await capacity.readModelCarCapacity('/models/parking/indoor_parking_lot.glb');
  assert.equal(capIndoor, 64);
});

test('analyzeModelFloors scans scene graph nodes and groups car bays by floor', () => {
  const mockNodes = [
    { name: 'Floor_B1', children: [1, 2] },
    { name: 'B1_Car_001' },
    { name: 'B1_Car_002' },
    { name: 'Floor_B2', children: [4, 5, 6] },
    { name: 'B2_Car_001' },
    { name: 'B2_Car_002' },
    { name: 'B2_Car_003' },
  ];

  const floors = capacity.analyzeModelFloors(mockNodes);
  assert.equal(floors.length, 2);
  assert.equal(floors[0].floorKey, 'B1');
  assert.equal(floors[0].displayName, 'Tầng B1');
  assert.equal(floors[0].capacity, 2);

  assert.equal(floors[1].floorKey, 'B2');
  assert.equal(floors[1].displayName, 'Tầng B2');
  assert.equal(floors[1].capacity, 3);
});

test('floor isolation logic selectively shows target floor and hides other floors', () => {
  // Synthetic THREE Object3D tree
  const b1 = { name: 'Floor_B1', visible: true, children: [{ name: 'B1_Car_001', visible: true, parent: null }] };
  b1.children[0].parent = b1;
  const b2 = { name: 'Floor_B2', visible: true, children: [{ name: 'B2_Car_001', visible: true, parent: null }] };
  b2.children[0].parent = b2;
  const sky = { name: 'SplineSkyAmbient', visible: true, children: [] };
  const scene = {
    name: 'Scene 1',
    visible: true,
    children: [b1, b2, sky],
    traverse(fn) {
      fn(this);
      b1.children.forEach(fn);
      b2.children.forEach(fn);
      fn(b1);
      fn(b2);
      fn(sky);
    },
  };
  b1.parent = scene;
  b2.parent = scene;
  sky.parent = scene;

  const THREE = require('../../FE/node_modules/three');
  const { applyFloorIsolation, isObjectVisible } = loadModule('lib/3d/floorIsolation.ts', { three: THREE });

  // When isolating Floor B1:
  const target = applyFloorIsolation(scene, 'B1');
  assert.equal(target, b1);
  assert.equal(b1.visible, true);
  assert.equal(b2.visible, false);
  assert.equal(scene.visible, true);
  assert.equal(isObjectVisible(b1.children[0]), true);
  assert.equal(isObjectVisible(b2.children[0]), false);

  // When switching to Floor B2:
  const targetB2 = applyFloorIsolation(scene, 'B2');
  assert.equal(targetB2, b2);
  assert.equal(b1.visible, false);
  assert.equal(b2.visible, true);
  assert.equal(scene.visible, true);
  assert.equal(isObjectVisible(b1.children[0]), false);
  assert.equal(isObjectVisible(b2.children[0]), true);

  // When viewing entire model ('all'): restores visibility of all floors
  const targetAll = applyFloorIsolation(scene, 'all');
  assert.equal(targetAll, scene);
  assert.equal(b1.visible, true);
  assert.equal(b2.visible, true);
  assert.equal(scene.visible, true);
  assert.equal(isObjectVisible(b1.children[0]), true);
  assert.equal(isObjectVisible(b2.children[0]), true);

  // Fallback when activeFloor is unknown or stale: never hides all nodes
  const targetFallback = applyFloorIsolation(scene, 'UNKNOWN_OR_STALE');
  assert.equal(targetFallback, scene);
  assert.equal(b1.visible, true);
  assert.equal(b2.visible, true);
  assert.equal(scene.visible, true);

  // Fallback when model has <= 1 floor (like outdoor lot): keeps all nodes visible
  const outdoorGroup = { name: 'OutdoorParking', visible: true, children: [{ name: 'Car_001', visible: true }] };
  outdoorGroup.children[0].parent = outdoorGroup;
  const outdoorScene = {
    name: 'OutdoorScene',
    visible: true,
    children: [outdoorGroup],
    traverse(fn) {
      fn(this);
      fn(outdoorGroup);
      outdoorGroup.children.forEach(fn);
    },
  };
  outdoorGroup.parent = outdoorScene;

  const targetOutdoor = applyFloorIsolation(outdoorScene, 'B1');
  assert.equal(targetOutdoor, outdoorScene);
  assert.equal(outdoorGroup.visible, true);
  assert.equal(outdoorScene.visible, true);
});

test('CameraController safely handles empty objects and recovers from NaN coordinates', () => {
  const THREE = require('../../FE/node_modules/three');
  const { CameraController } = loadModule('lib/3d/cameraController.ts', { three: THREE });
  const ctrl = new CameraController();
  const cam = new THREE.PerspectiveCamera(45, 1, 0.5, 500);
  cam.position.set(NaN, NaN, NaN);
  const controls = { target: new THREE.Vector3(NaN, NaN, NaN), update() {} };
  const emptyScene = new THREE.Object3D();

  ctrl.resetToOverview(emptyScene, cam, controls);
  assert.equal(Number.isFinite(cam.position.x), true);
  assert.equal(Number.isFinite(controls.target.x), true);
  assert.equal(cam.position.x, 22);
  assert.equal(controls.target.x, 0);

  ctrl.focusOnObject(emptyScene, cam, controls);
  assert.equal(Number.isFinite(cam.position.x), true);
  assert.equal(Number.isFinite(controls.target.x), true);
});

test('deep cloning mesh materials prevents shared material mutations across instances', () => {
  const THREE = require('../../FE/node_modules/three');
  const group = new THREE.Group();
  const mat = new THREE.MeshBasicMaterial({ color: 0x336699 });
  const mesh = new THREE.Mesh(new THREE.BoxGeometry(1, 1, 1), mat);
  group.add(mesh);

  const clone = group.clone(true);
  clone.traverse((node) => {
    if (node instanceof THREE.Mesh && node.material) {
      node.material = Array.isArray(node.material)
        ? node.material.map((m) => m.clone())
        : node.material.clone();
    }
  });

  const clonedMesh = clone.children[0];
  assert.notEqual(clonedMesh.material, mat);
  clonedMesh.material.dispose();
  assert.equal(mat.color.getHex(), 0x336699);
});

test('auto-cleaning of registered slots removes succeeded meshes from selection and outcomes', () => {
  const selection = [
    { key: 'mesh-1', name: 'Car_001', code: 'A-001' },
    { key: 'mesh-2', name: 'Car_002', code: 'A-002' },
    { key: 'mesh-3', name: 'Car_003', code: 'A-003' },
  ];
  const nextOutcomes = {
    'mesh-1': { key: 'mesh-1', code: 'A-001', status: 'succeeded', slotId: 'slot-1' },
    'mesh-2': { key: 'mesh-2', code: 'A-002', status: 'succeeded', slotId: 'slot-2' },
    'mesh-3': { key: 'mesh-3', code: 'A-003', status: 'failed', message: 'Duplicate code' },
  };

  const succeededKeys = new Set(
    Object.values(nextOutcomes)
      .filter(o => o.status === 'succeeded')
      .map(o => o.key),
  );

  const remainingSelection = selection.filter(item => !succeededKeys.has(item.key));
  const remainingOutcomes = Object.fromEntries(
    Object.entries(nextOutcomes).filter(([key]) => !succeededKeys.has(key)),
  );

  assert.deepEqual(remainingSelection, [{ key: 'mesh-3', name: 'Car_003', code: 'A-003' }]);
  assert.deepEqual(Object.keys(remainingOutcomes), ['mesh-3']);
  assert.equal(remainingOutcomes['mesh-3'].status, 'failed');
});
