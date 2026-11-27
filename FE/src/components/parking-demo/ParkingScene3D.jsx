// src/components/ParkingScene3D.jsx
// Interactive DEMO-HQ outdoor parking scene plus alternate model previews.

import { forwardRef, useEffect, useImperativeHandle, useRef, useState } from "react";
import * as THREE from "three";
import { GLTFLoader } from "three/examples/jsm/loaders/GLTFLoader.js";
import { OrbitControls } from "three/examples/jsm/controls/OrbitControls.js";
import { DEMO_SLOT_CODES } from "./demoLayout.js";

/* ============================ CẤU HÌNH ============================ */
const PARKING_URL = "/models/outdoor_parking.glb";
const CAR_URL = "/models/car_model.glb";

const CAR_LENGTH = 1.9; // m — xe trong bãi dài ~1.89m
const CAR_FRONT_IS_NEG_Z = true; // car_model.glb: đèn pha nằm phía -Z
const SPEED = 5; // m/s (tốc độ trung bình khi chạy tới/ra)
const SPEED_REVERSE = 2.2; // m/s khi lùi ra khỏi slot
const TURN_RADIUS = 1.6; // m — bán kính bo góc khi chạy tiến
const REV_RADIUS = 1.8; // m — bán kính bo góc khi lùi
const REV_OUT = 3.2; // m — lùi lệch ra lối đi bao xa trước khi chạy tiến
const HIDE_DEMO_CARS = true; // Occupancy comes from the live demo site.

// DEMO-HQ's 36 standard and 12 EV mesh codes are arranged into four display zones.
const ZONES = { A: [1, 12], B: [13, 24], C: [25, 36], D: [37, 48] };

// Toạ độ đường đi (mét, hệ toạ độ của model) — lấy từ vạch kẻ/mũi tên trong model
const GATE_START = new THREE.Vector3(1.9, 0, 18); // làn vào, mép ngoài đường dẫn
const ENTRY_LANE_X = 1.9;
const AISLE3_Z = 9.8; // lối đi phía nam (sát hàng D)
const EAST_ROAD_X = 11.9; // đường vòng phía đông
const AISLE_Z = { 1: -6.1, 2: -6.1, 3: 3.7, 4: AISLE3_Z }; // hàng -> lối đi dùng để rẽ vào
// Đường ra (check-out): đi về phía tây rồi xuống làn ra ở x = -1.9
const WEST_ROAD_X = -10.0; // giữa hàng slot (x=-8.35) và dải phân cách khu xe máy (x=-11.3)
const EXIT_LANE_X = -1.9;
const GATE_END_Z = 19.5; // xe biến mất ở đây sau khi ra khỏi cổng

// DEMO-HQ has twelve EV bays after its thirty-six standard bays.
const EV_SLOTS = Array.from({ length: 12 }, (_, index) => index + 37);

const STATUS_STYLE = {
  AVAILABLE: { color: 0x4b5563, emissive: 0x000000, intensity: 0 },
  RESERVED: { color: 0xc2410c, emissive: 0xff7a1a, intensity: 1.1 },
  OCCUPIED: { color: 0x7f1d1d, emissive: 0xcc0000, intensity: 0.35 }, // đỏ đậm, đứng yên
};
const EV_COLOR = 0x2b5d8a;
const CAR_COLORS = [0x2563eb, 0xf59e0b, 0x10b981, 0x8b5cf6, 0xef4444, 0xe5e7eb];

/* ============================ TIỆN ÍCH ============================ */
const pad = (n, l = 3) => String(n).padStart(l, "0");
const slotMeshName = (n) => DEMO_SLOT_CODES[n - 1];
const easeInOut = (t) => 0.5 - 0.5 * Math.cos(Math.PI * t);

function zoneOfSlot(n) {
  return Object.keys(ZONES).find((z) => n >= ZONES[z][0] && n <= ZONES[z][1]);
}
function slotLabel(n) {
  const z = zoneOfSlot(n);
  return `${z}${pad(n - ZONES[z][0] + 1, 2)}`; // A01..A12, B01..B12
}

// Model KHÔNG có material -> three.js dùng mặc định (metalness=1 => đen). Tự gán màu theo tên.
const COLOR_RULES = [
  [/^(CarParkingSpace|PARKING_EV_CAR|MotorcycleParkingSpace)/, 0x4b5563],
  [/Parking_Asphalt|Approach_Road|Entrance_Approach/, 0x2b2f36],
  [/Grass/, 0x3f7d3a],
  [/Trunk/, 0x6b4f33],
  [/Canopy/, 0x2f7d32],
  [/Glass/, 0x9cc9e8],
  [/Sidewalk|Island|Median|Foundation|Pedestrian/, 0xb8bcc2],
  [/Crosswalk|Zebra|RoadArrow|LaneDivider|Separator/, 0xf3f4f6],
  [/LED/, 0xfff2b0],
  [/Taillights/, 0xdc2626],
  [/Headlights/, 0xfff7cc],
  [/Wheel/, 0x111827],
  [/Cabin/, 0x9ca3af],
  [/Chassis/, 0x2563eb],
  [/Sign.*(Board|Face)|Monument/, 0x1e40af],
  [/Text|Screen/, 0xffffff],
  [/Wall|Accent|Roof/, 0xe7e1d3],
];
function colorForName(name) {
  for (const [re, c] of COLOR_RULES) if (re.test(name)) return c;
  return 0xc8ccd2;
}
function styleMeshes(root) {
  const lights = [];
  root.traverse((o) => {
    if (o.isLight) lights.push(o);
    if (!o.isMesh) return;
    const name = o.name || o.parent?.name || "";
    const mat = new THREE.MeshStandardMaterial({
      color: colorForName(name),
      roughness: 0.85,
      metalness: 0.05,
      side: THREE.DoubleSide,
    });
    if (/Glass/.test(name)) { mat.transparent = true; mat.opacity = 0.55; }
    if (/LED/.test(name)) { mat.emissive = new THREE.Color(0xfff2b0); mat.emissiveIntensity = 0.6; }
    o.material = mat;
    o.castShadow = !/Asphalt|Grass|Road|Sidewalk|Space|Divider|Arrow|Crosswalk|Zebra/.test(name);
    o.receiveShadow = true;
  });
  lights.forEach((l) => l.parent.remove(l)); // dùng đèn của mình cho dễ kiểm soát
}

// Đường đi gồm đoạn thẳng + góc bo tròn (Bezier bậc 2)
function buildRoundedPath(points, radius = TURN_RADIUS) {
  const path = new THREE.CurvePath();
  let cursor = points[0].clone();
  const addLine = (a, b) => { if (a.distanceTo(b) > 1e-4) path.add(new THREE.LineCurve3(a.clone(), b.clone())); };
  for (let i = 1; i < points.length - 1; i++) {
    const prev = points[i - 1], p = points[i], next = points[i + 1];
    const r = Math.min(radius, prev.distanceTo(p) / 2, p.distanceTo(next) / 2);
    const a = p.clone().add(prev.clone().sub(p).normalize().multiplyScalar(r));
    const b = p.clone().add(next.clone().sub(p).normalize().multiplyScalar(r));
    addLine(cursor, a);
    path.add(new THREE.QuadraticBezierCurve3(a, p.clone(), b));
    cursor = b;
  }
  addLine(cursor, points[points.length - 1]);
  return path;
}
// Bỏ các điểm quá sát nhau (tránh khúc lượn gắt khi slot gần làn vào)
function dropShortSegments(pts, minLen = 0.8) {
  const out = [pts[0]];
  for (let i = 1; i < pts.length - 1; i++) if (pts[i].distanceTo(out[out.length - 1]) >= minLen) out.push(pts[i]);
  out.push(pts[pts.length - 1]);
  return out;
}

const V = (x, z) => new THREE.Vector3(x, 0, z);
const easeStart = (t, a = 0.2) => // tăng tốc trong a đầu, sau đó đều (xe rời cổng không phanh)
  (t < a ? (t * t) / (2 * a) : a / 2 + (t - a)) / (1 - a / 2);

// Đường vào: cổng -> làn vào -> (đường vòng phía đông) -> lối đi -> slot, mũi xe quay vào slot
function entryPoints(slot) {
  const az = AISLE_Z[slot.row];
  const pts = [GATE_START.clone(), V(ENTRY_LANE_X, AISLE3_Z)];
  if (slot.row !== 4) pts.push(V(EAST_ROAD_X, AISLE3_Z), V(EAST_ROAD_X, az));
  pts.push(V(slot.x, az), V(slot.x, slot.z));
  return dropShortSegments(pts);
}
// Đường ra: LÙI ra lối đi (đuôi xe lệch về phía ngược hướng chạy để mũi xe tự xoay đúng hướng),
// rồi chạy tiến về phía tây -> làn ra -> cổng ra
function exitPlan(slot) {
  const az = AISLE_Z[slot.row];
  const d = slot.row === 4 ? (slot.x > EXIT_LANE_X ? -1 : 1) : -1; // hướng chạy dọc lối đi (x)
  const p2 = V(slot.x - d * REV_OUT, az);
  const back = buildRoundedPath([V(slot.x, slot.z), V(slot.x, az), p2], REV_RADIUS);
  const fwd =
    slot.row === 4
      ? [p2, V(EXIT_LANE_X, az), V(EXIT_LANE_X, GATE_END_Z)]
      : [p2, V(WEST_ROAD_X, az), V(WEST_ROAD_X, AISLE3_Z), V(EXIT_LANE_X, AISLE3_Z), V(EXIT_LANE_X, GATE_END_Z)];
  return { back, fwd: buildRoundedPath(dropShortSegments(fwd)) };
}

function parkingAnchors(root, parkingUrl) {
  if (parkingUrl === PARKING_URL) {
    return DEMO_SLOT_CODES.map((name) => ({ name, floor: "outdoor", ev: name.startsWith("PARKING_EV") }))
      .map((item) => ({ ...item, mesh: root.getObjectByName(item.name) }))
      .filter((item) => item.mesh?.isMesh);
  }

  const candidates = [];
  root.traverse((object) => {
    if (!object.isMesh) return;
    if (parkingUrl.includes("indoor_parking_lot")) {
      const match = /^PARKING_(G|L1|L2)_(CAR|EV_CAR)_/i.exec(object.name);
      if (match) candidates.push({ mesh: object, floor: match[1].toUpperCase(), ev: match[2].toUpperCase() === "EV_CAR" });
    } else if (parkingUrl.includes("underground_parking_lot")) {
      const match = /^(B1|B2)_(Car|EV_Car)_/i.exec(object.name);
      if (match) candidates.push({ mesh: object, floor: match[1].toUpperCase(), ev: match[2].toUpperCase() === "EV_CAR" });
    }
  });

  const floorOrder = parkingUrl.includes("indoor_parking_lot") ? ["G", "L1", "L2"] : ["B1", "B2"];
  const bounds = (mesh) => new THREE.Box3().setFromObject(mesh);
  const sortedByFloor = floorOrder.flatMap((floor) => candidates
    .filter((item) => item.floor === floor)
    .sort((left, right) => {
      const a = bounds(left.mesh).getCenter(new THREE.Vector3());
      const b = bounds(right.mesh).getCenter(new THREE.Vector3());
      return a.z - b.z || a.x - b.x || left.mesh.name.localeCompare(right.mesh.name);
    }));

  // Keep the demo's 48 DEMO-HQ records mapped one-to-one; spread indoor bays across its three levels.
  if (parkingUrl.includes("indoor_parking_lot")) {
    return floorOrder.flatMap((floor) => sortedByFloor.filter((item) => item.floor === floor).slice(0, 16));
  }
  return sortedByFloor.slice(0, DEMO_SLOT_CODES.length);
}

function modelEntryPoint(parkingUrl, floor, y) {
  if (parkingUrl.includes("underground_parking_lot")) {
    return floor === "B2" ? new THREE.Vector3(17.35, y, -0.04) : new THREE.Vector3(0, y, -4.6);
  }
  if (parkingUrl.includes("indoor_parking_lot")) {
    const z = floor === "L2" ? 2.6 : floor === "L1" ? -0.6 : -2.4;
    return new THREE.Vector3(-8.13, y, z);
  }
  return GATE_START.clone();
}

function modelEntryPoints(slot) {
  if (slot.modelKind === "outdoor") return entryPoints(slot);
  if (slot.parkingAxis === "x") {
    return dropShortSegments([
      slot.routeStart.clone(),
      new THREE.Vector3(slot.aisleX, slot.y, slot.routeStart.z),
      new THREE.Vector3(slot.aisleX, slot.y, slot.z),
      new THREE.Vector3(slot.x, slot.y, slot.z),
    ]);
  }
  const aisle = slot.aisleZ;
  return dropShortSegments([
    slot.routeStart.clone(),
    new THREE.Vector3(slot.routeStart.x, slot.y, aisle),
    new THREE.Vector3(slot.x, slot.y, aisle),
    new THREE.Vector3(slot.x, slot.y, slot.z),
  ]);
}

function modelExitPlan(slot) {
  if (slot.modelKind === "outdoor") return exitPlan(slot);
  const start = slot.routeStart;
  if (slot.parkingAxis === "x") {
    const aislePoint = new THREE.Vector3(slot.aisleX, slot.y, slot.z);
    const back = buildRoundedPath([new THREE.Vector3(slot.x, slot.y, slot.z), aislePoint], REV_RADIUS);
    const fwdPoints = [aislePoint, new THREE.Vector3(slot.aisleX, slot.y, start.z), start.clone()];
    return { back, fwd: buildRoundedPath(dropShortSegments(fwdPoints)) };
  }
  const aislePoint = V(slot.x, slot.aisleZ);
  aislePoint.y = slot.y;
  const back = buildRoundedPath([V(slot.x, slot.z).setY(slot.y), aislePoint], REV_RADIUS);
  const fwdPoints = [aislePoint, V(start.x, slot.aisleZ).setY(slot.y), start.clone()];
  return { back, fwd: buildRoundedPath(dropShortSegments(fwdPoints)) };
}

function makeLabelSprite(text) {
  const c = document.createElement("canvas");
  c.width = 256; c.height = 96;
  const g = c.getContext("2d");
  g.fillStyle = "rgba(194,65,12,0.96)";
  g.beginPath(); g.roundRect(4, 4, 248, 88, 18); g.fill();
  g.fillStyle = "#fff"; g.font = "bold 44px sans-serif"; g.textAlign = "center"; g.textBaseline = "middle";
  g.fillText(text, 128, 50);
  const tex = new THREE.CanvasTexture(c);
  tex.colorSpace = THREE.SRGBColorSpace;
  const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: tex, depthTest: false, transparent: true }));
  s.scale.set(1.5, 0.56, 1);
  s.renderOrder = 10;
  return s;
}
function makeGlow(w, d) {
  const g = new THREE.Group();
  const box = new THREE.Mesh(
    new THREE.BoxGeometry(w, 1.1, d),
    new THREE.MeshBasicMaterial({ color: 0xff7a1a, transparent: true, opacity: 0.25, depthWrite: false, blending: THREE.AdditiveBlending })
  );
  box.position.y = 0.55;
  const edges = new THREE.LineSegments(
    new THREE.EdgesGeometry(box.geometry),
    new THREE.LineBasicMaterial({ color: 0xffa33c })
  );
  edges.position.y = 0.55;
  g.add(box, edges);
  g.userData = { box, edges };
  return g;
}
function disposeObject(o) {
  o.traverse((n) => {
    n.geometry?.dispose?.();
    const m = n.material;
    if (m) { m.map?.dispose?.(); m.dispose?.(); }
  });
}

/* ============================ COMPONENT ============================ */
// API (qua ref):
//   bookZone(zoneHoặcSlot, bookingId) -> nhãn slot | null     slot sáng đỏ nhấp nháy
//   checkIn(bookingId)  -> Promise<bool>                       xe chạy vào đúng slot đã book
//   checkOut(bookingId) -> Promise<bool>                       xe lùi ra, chạy về cổng ra
//   cancelBooking(id) / release(id) / reset()
//   syncBookings(items, { animate })                           đồng bộ từ server (xem ParkingDemo3D)
//        items = [{ id, target: "A" | "A05", state: "RESERVED" | "PARKED" }]
const ParkingScene3D = forwardRef(function ParkingScene3D(
  { className = "", followCar = true, onChange, parkingUrl = PARKING_URL },
  ref
) {
  const mountRef = useRef(null);
  const engine = useRef(null);
  const onChangeRef = useRef(onChange);
  onChangeRef.current = onChange;
  const followRef = useRef(followCar);
  followRef.current = followCar;
  const [progress, setProgress] = useState(0);
  const [error, setError] = useState("");

  useImperativeHandle(
    ref,
    () => ({
      bookZone: (target, id) => engine.current?.bookZone(target, id) ?? null,
      bookSlot: (target, id, override) => engine.current?.bookSlot(target, id, override) ?? null,
      checkIn: (id) => engine.current?.checkIn(id) ?? Promise.resolve(false),
      checkOut: (id) => engine.current?.checkOut(id) ?? Promise.resolve(false),
      cancelBooking: (id) => engine.current?.cancelBooking(id),
      release: (id) => engine.current?.release(id),
      syncBookings: (items, opts) => engine.current?.syncBookings(items, opts) ?? Promise.resolve(),
      reset: () => engine.current?.reset(),
      getSlots: () => engine.current?.getSlots() ?? [],
    }),
    []
  );

  useEffect(() => {
    const mount = mountRef.current;
    let disposed = false;
    let raf = 0;

    /* ---- renderer / scene / camera ---- */
    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    mount.appendChild(renderer.domElement);

    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0xcfe3f2);
    scene.add(new THREE.HemisphereLight(0xffffff, 0x8a9a7b, 1.1));
    const sun = new THREE.DirectionalLight(0xffffff, 2.2);
    sun.position.set(-14, 26, 18);
    sun.castShadow = true;
    sun.shadow.mapSize.set(2048, 2048);
    Object.assign(sun.shadow.camera, { left: -30, right: 30, top: 30, bottom: -30, near: 1, far: 80 });
    scene.add(sun);

    const camera = new THREE.PerspectiveCamera(45, 1, 0.1, 300);
    camera.position.set(17, 20, 30);
    const controls = new OrbitControls(camera, renderer.domElement);
    controls.target.set(0, 0, 2);
    controls.enableDamping = true;
    controls.maxPolarAngle = Math.PI / 2 - 0.05;
    controls.minDistance = 4;
    controls.maxDistance = 70;

    const resize = () => {
      const w = mount.clientWidth || 1, h = mount.clientHeight || 1;
      renderer.setSize(w, h);
      camera.aspect = w / h;
      camera.updateProjectionMatrix();
    };
    const ro = new ResizeObserver(resize);
    ro.observe(mount);
    resize();

    /* ---- trạng thái ---- */
    const slots = new Map(); // label -> slot
    const byBooking = new Map(); // bookingId -> slot
    const cars = new Set();
    const tasks = new Set();
    const zoneAlias = new Map(); // tên zone trong DB -> key trong ZONES (khi không trùng A..D)
    let carTemplate = null;
    let wheelRadius = 0.12; // m, đo lại khi dựng template
    const followed = new Set();
    let carCounter = 0;

    const emit = () => onChangeRef.current?.(getSlots());
    const getSlots = () =>
      [...slots.values()].map((s) => ({
        label: s.label, zone: s.zone, status: s.status, bookingId: s.bookingId, ev: s.ev,
      }));

    function applyStyle(slot) {
      const st = STATUS_STYLE[slot.status];
      const base = slot.ev && slot.status === "AVAILABLE" ? EV_COLOR : st.color;
      slot.mesh.material.color.setHex(base);
      slot.mesh.material.emissive.setHex(st.emissive);
      slot.mesh.material.emissiveIntensity = st.intensity;
      const showGlow = slot.status === "RESERVED";
      if (showGlow && !slot.glow) {
        slot.glow = makeGlow(slot.width, slot.depth);
        slot.glow.position.set(slot.x, slot.y + 0.02, slot.z);
        slot.tag = makeLabelSprite(slot.label);
        slot.tag.position.set(slot.x, slot.y + 2.0, slot.z);
        scene.add(slot.glow, slot.tag);
      }
      if (!showGlow && slot.glow) {
        scene.remove(slot.glow, slot.tag);
        disposeObject(slot.glow); disposeObject(slot.tag);
        slot.glow = slot.tag = null;
      }
    }
    function setStatus(slot, status, bookingId = null) {
      slot.status = status;
      slot.bookingId = bookingId;
      applyStyle(slot);
      emit();
    }

    /* ---- load model ---- */
    const loader = new GLTFLoader();
    const loadGLB = (url, onProg) =>
      new Promise((res, rej) => loader.load(url, res, (e) => e.total && onProg?.(e.loaded / e.total), rej));

    function buildCarTemplate(gltf) {
      const model = gltf.scene;
      styleMeshes(model);
      const box = new THREE.Box3().setFromObject(model);
      const size = box.getSize(new THREE.Vector3());
      const center = box.getCenter(new THREE.Vector3());
      const inner = new THREE.Group();
      model.position.set(-center.x, -box.min.y, -center.z); // gốc = giữa-đáy xe
      inner.add(model);
      inner.scale.setScalar(CAR_LENGTH / Math.max(size.x, size.z));
      inner.rotation.y = CAR_FRONT_IS_NEG_Z ? Math.PI : 0; // sau đó "mũi xe" = +Z
      const holder = new THREE.Group();
      holder.add(inner);
      holder.updateMatrixWorld(true);
      // bán kính bánh xe (thế giới) để tính góc quay = quãng đường / bán kính
      const w = holder.getObjectByName("Vehicle_Sedan_04_Wheel_45_64");
      if (w) wheelRadius = new THREE.Box3().setFromObject(w).getSize(new THREE.Vector3()).y / 2;
      return holder;
    }

    function setupParking(gltf) {
      const root = gltf.scene;
      styleMeshes(root);
      scene.add(root);
      root.updateMatrixWorld(true);

      if (parkingUrl !== PARKING_URL) {
        const bounds = new THREE.Box3().setFromObject(root);
        const center = bounds.getCenter(new THREE.Vector3());
        const size = bounds.getSize(new THREE.Vector3());
        const span = Math.max(size.x, size.y, size.z, 1);
        controls.target.copy(center);
        camera.position.set(center.x + span * 0.72, center.y + span * 0.68, center.z + span * 0.88);
        camera.near = Math.max(span / 1000, 0.1);
        camera.far = span * 12;
        camera.updateProjectionMatrix();
        controls.update();
      }

      const anchors = parkingAnchors(root, parkingUrl);
      const floorRows = new Map();
      for (const item of anchors) {
        if (item.floor === "outdoor") continue;
        const box = new THREE.Box3().setFromObject(item.mesh);
        const center = box.getCenter(new THREE.Vector3());
        const size = box.getSize(new THREE.Vector3());
        item.parkingAxis = size.x > size.z ? "x" : "z";
        const key = `${item.floor}:${item.parkingAxis}`;
        const rows = floorRows.get(key) ?? [];
        const position = item.parkingAxis === "x" ? center.x : center.z;
        if (!rows.some((coordinate) => Math.abs(coordinate - position) < 0.2)) rows.push(position);
        floorRows.set(key, rows);
      }
      floorRows.forEach((rows) => rows.sort((a, b) => a - b));

      anchors.forEach(({ mesh, floor, ev }, index) => {
        const n = index + 1;
        mesh.material = mesh.material.clone(); // mỗi slot 1 material riêng
        const box = new THREE.Box3().setFromObject(mesh);
        const c = box.getCenter(new THREE.Vector3());
        const size = box.getSize(new THREE.Vector3());
        const label = slotLabel(n);
        const parkingAxis = floor === "outdoor" ? "z" : anchors[index].parkingAxis;
        const rows = floorRows.get(`${floor}:${parkingAxis}`) ?? [];
        const aisles = rows.slice(0, -1).map((coordinate, rowIndex) => (coordinate + rows[rowIndex + 1]) / 2);
        const coordinate = parkingAxis === "x" ? c.x : c.z;
        const aisle = floor === "outdoor" ? null : aisles.reduce((best, value) =>
          Math.abs(value - coordinate) < Math.abs(best - coordinate) ? value : best, aisles[0] ?? coordinate);
        const aisleX = parkingAxis === "x" ? aisle : null;
        const aisleZ = parkingAxis === "z" ? aisle : null;
        const y = floor === "outdoor" ? c.y : box.min.y + 0.02;
        const slot = {
          n, label, zone: zoneOfSlot(n), mesh, x: c.x, z: c.z,
          y,
          width: Math.max(size.x + 0.18, parkingAxis === "x" ? 2.2 : 1.25),
          depth: Math.max(size.z + 0.18, parkingAxis === "z" ? 2.2 : 1.25),
          modelKind: floor === "outdoor" ? "outdoor" : "alternate", parkingAxis,
          floor, aisleX, aisleZ, routeStart: floor === "outdoor" ? GATE_START.clone() : modelEntryPoint(parkingUrl, floor, y),
          row: Object.entries(AISLE_Z).reduce((closest, [row, aisleZ]) =>
            Math.abs(aisleZ - c.z) < Math.abs(AISLE_Z[closest] - c.z) ? Number(row) : closest, 1),
          ev: floor === "outdoor" ? EV_SLOTS.includes(n) : ev,
          status: "AVAILABLE", bookingId: null, glow: null, tag: null, car: null, busy: false,
        };
        slots.set(label, slot);
        applyStyle(slot);
      });
      // Built-in cars are hidden; occupancy comes from the live demo site.
      ["Car_001", "Car_002", "Car_003", "Car_004"].forEach((nm) => {
        const car = root.getObjectByName(nm);
        if (!car) return;
        if (HIDE_DEMO_CARS) { car.visible = false; return; }
        const p = new THREE.Box3().setFromObject(car).getCenter(new THREE.Vector3());
        const near = [...slots.values()].find((s) => Math.hypot(s.x - p.x, s.z - p.z) < 0.8);
        if (near) setStatus(near, "OCCUPIED", `static:${nm}`);
      });
    }

    /* ---- xe & animation ---- */
    function spawnCar() {
      const car = carTemplate.clone(true);
      const color = CAR_COLORS[carCounter++ % CAR_COLORS.length];
      car.userData.wheels = [];
      car.traverse((o) => {
        if (o.isMesh && /Chassis/.test(o.name)) { o.material = o.material.clone(); o.material.color.setHex(color); }
        if (o.isMesh && /Wheel/.test(o.name)) car.userData.wheels.push(o);
      });
      cars.add(car);
      scene.add(car);
      return car;
    }
    function removeCar(car) {
      if (!car) return;
      followed.delete(car);
      scene.remove(car); cars.delete(car);
    }
    // quay bánh quanh trục ngang (trục X của model). dist > 0 = đi tiến, < 0 = lùi
    const spinQ = new THREE.Quaternion(), AXIS_X = new THREE.Vector3(1, 0, 0);
    function spinWheels(car, dist) {
      const ang = ((CAR_FRONT_IS_NEG_Z ? -1 : 1) * dist) / wheelRadius;
      spinQ.setFromAxisAngle(AXIS_X, ang);
      car.userData.wheels.forEach((w) => w.quaternion.premultiply(spinQ));
    }
    // chạy xe dọc 1 đường cong. reverse: lùi (mũi xe ngược hướng chuyển động)
    function runPath(car, curve, { speed = SPEED, reverse = false, ease = easeInOut } = {}) {
      return new Promise((resolve) => {
        const duration = Math.max(curve.getLength() / speed, 0.5);
        const p = new THREE.Vector3(), t = new THREE.Vector3(), prev = car.position.clone();
        let time = 0;
        const task = (dt) => {
          time += dt;
          const u = ease(Math.min(time / duration, 1));
          curve.getPoint(u, p);
          curve.getTangent(Math.min(Math.max(u, 0.001), 0.999), t);
          car.position.copy(p);
          car.rotation.y = Math.atan2(t.x, t.z) + (reverse ? Math.PI : 0); // mũi xe = +Z
          // quãng đường có dấu theo hướng mũi xe -> bánh quay đúng chiều khi lùi
          const fx = Math.sin(car.rotation.y), fz = Math.cos(car.rotation.y);
          spinWheels(car, (p.x - prev.x) * fx + (p.z - prev.z) * fz);
          prev.copy(p);
          if (time >= duration) { tasks.delete(task); resolve(); }
        };
        tasks.add(task);
      });
    }
    function placeCarInstantly(slot) {
      const car = spawnCar();
      const az = AISLE_Z[slot.row];
      car.position.set(slot.x, 0, slot.z);
      car.position.y = slot.y;
      const aisleZ = slot.aisleZ ?? az;
      if (slot.parkingAxis === "x") car.rotation.y = slot.x < slot.aisleX ? Math.PI / 2 : -Math.PI / 2;
      else car.rotation.y = slot.z < aisleZ ? 0 : Math.PI; // face into the bay from its aisle
      slot.car = car;
    }

    /* ---- API ---- */
    function zoneKey(target) {
      if (ZONES[target]) return target;
      if (!zoneAlias.has(target)) {
        const free = Object.keys(ZONES).find((z) => ![...zoneAlias.values()].includes(z));
        if (free) zoneAlias.set(target, free);
      }
      return zoneAlias.get(target);
    }
    const assignable = (s) => s.status === "AVAILABLE" && !s.busy;
    function resolveSlots(target) {
      const exact = slots.get(target) || [...slots.values()].find((x) => x.mesh.name === target);
      if (exact) return [exact];
      const z = zoneKey(target);
      if (z) {
        return [...slots.values()]
          .filter((s) => s.zone === z)
          .sort((a, b) => Math.abs(a.x - ENTRY_LANE_X) - Math.abs(b.x - ENTRY_LANE_X)); // gần làn vào trước
      }
      return [];
    }

    const api = {
      bookSlot(target, bookingId, { override = false } = {}) {
        const slot = resolveSlots(target)[0];
        if (!slot) return null;
        if (slot.status !== "AVAILABLE" || slot.busy) {
          if (!override) return null;
          if (slot.bookingId) byBooking.delete(String(slot.bookingId));
          removeCar(slot.car);
          slot.car = null;
          slot.busy = false;
          setStatus(slot, "AVAILABLE");
        }
        byBooking.set(bookingId, slot);
        setStatus(slot, "RESERVED", bookingId);
        return slot.label;
      },
      bookZone(target, bookingId, { overflow = false } = {}) {
        if (byBooking.has(bookingId)) return byBooking.get(bookingId).label;
        let slot = resolveSlots(target).find(assignable);
        if (!slot && overflow) slot = [...slots.values()].filter(assignable).sort((a, b) => a.n - b.n)[0];
        if (!slot) return null;
        byBooking.set(bookingId, slot);
        setStatus(slot, "RESERVED", bookingId);
        return slot.label;
      },
      async checkIn(bookingId) {
        const slot = byBooking.get(bookingId);
        if (!slot || slot.status !== "RESERVED" || slot.busy || !carTemplate) return false;
        slot.busy = true;
        const car = spawnCar();
        car.position.copy(slot.routeStart);
        followed.add(car);
        await runPath(car, buildRoundedPath(modelEntryPoints(slot)));
        followed.delete(car);
        slot.busy = false;
        if (disposed) return false;
        slot.car = car;
        setStatus(slot, "OCCUPIED", bookingId);
        return true;
      },
      async checkOut(bookingId) {
        const slot = byBooking.get(bookingId);
        if (!slot || slot.status !== "OCCUPIED" || slot.busy || !slot.car) return false;
        slot.busy = true;
        const car = slot.car;
        const { back, fwd } = modelExitPlan(slot);
        followed.add(car);
        await runPath(car, back, { speed: SPEED_REVERSE, reverse: true });
        if (disposed) return false;
        slot.car = null; slot.busy = false; // xe đã rời slot -> slot trống lại
        byBooking.delete(bookingId);
        setStatus(slot, "AVAILABLE");
        await runPath(car, fwd, { ease: easeStart });
        removeCar(car);
        return true;
      },
      cancelBooking(bookingId) {
        const slot = byBooking.get(bookingId);
        if (!slot || slot.status !== "RESERVED") return;
        byBooking.delete(bookingId);
        setStatus(slot, "AVAILABLE");
      },
      release(bookingId) { // xoá ngay, không animation
        const slot = byBooking.get(bookingId);
        if (!slot) return;
        removeCar(slot.car); slot.car = null; slot.busy = false;
        byBooking.delete(bookingId);
        setStatus(slot, "AVAILABLE");
      },
      reset() {
        tasks.clear(); followed.clear();
        [...cars].forEach(removeCar);
        byBooking.clear();
        slots.forEach((s) => {
          s.car = null; s.busy = false;
          if (!String(s.bookingId).startsWith("static:")) setStatus(s, "AVAILABLE");
        });
      },
      // Đồng bộ với server. Lần đầu (animate:false): đặt ngay trạng thái. Các lần sau (animate:true):
      // RESERVED->PARKED = chạy animation check-in, biến mất khỏi danh sách = chạy animation check-out.
      async syncBookings(items, { animate = true } = {}) {
        const wanted = new Map(items.map((i) => [String(i.id), i]));
        const jobs = [];
        for (const [id, slot] of [...byBooking]) {
          if (String(slot.bookingId).startsWith("static:") || wanted.has(id) || slot.busy) continue;
          if (slot.status === "OCCUPIED" && animate) jobs.push(api.checkOut(id));
          else api.release(id);
        }
        for (const it of items) {
          const id = String(it.id);
          const slot = byBooking.get(id);
          if (!slot) {
            const label = api.bookZone(it.target, id, { overflow: true });
            if (!label) continue;
            if (it.state === "PARKED") {
              const s = byBooking.get(id);
              placeCarInstantly(s);
              setStatus(s, "OCCUPIED", id);
            }
          } else if (it.state === "PARKED" && slot.status === "RESERVED" && !slot.busy) {
            if (animate) jobs.push(api.checkIn(id));
            else { placeCarInstantly(slot); setStatus(slot, "OCCUPIED", id); }
          } else if (it.state === "RESERVED" && slot.status === "OCCUPIED" && !slot.busy) {
            api.release(id); api.bookZone(it.target, id, { overflow: true });
          }
        }
        await Promise.all(jobs);
      },
      getSlots,
    };
    engine.current = api;

    /* ---- vòng lặp render ---- */
    const clock = new THREE.Clock();
    const centroid = new THREE.Vector3(), delta = new THREE.Vector3(), last = new THREE.Vector3();
    const tick = () => {
      raf = requestAnimationFrame(tick);
      const dt = Math.min(clock.getDelta(), 0.05);
      const now = clock.elapsedTime;

      [...tasks].forEach((fn) => fn(dt));

      // nhấp nháy slot đang RESERVED
      const pulse = 0.5 + 0.5 * Math.sin(now * 5);
      slots.forEach((s) => {
        if (s.status !== "RESERVED" || !s.glow) return;
        s.mesh.material.emissiveIntensity = 0.4 + 1.2 * pulse;
        s.glow.userData.box.material.opacity = 0.12 + 0.25 * pulse;
      });

      // camera bám theo xe (hoặc trung điểm các xe) khi đang chạy
      if (followed.size && followRef.current) {
        centroid.set(0, 0, 0);
        followed.forEach((c) => centroid.add(c.position));
        centroid.divideScalar(followed.size);
        last.copy(controls.target);
        controls.target.lerp(centroid, 1 - Math.exp(-3 * dt));
        delta.copy(controls.target).sub(last);
        camera.position.add(delta);
      }
      controls.update();
      renderer.render(scene, camera);
    };

    /* ---- chạy ---- */
    (async () => {
      try {
        const [parking, car] = await Promise.all([
          loadGLB(parkingUrl, (p) => !disposed && setProgress(Math.round(p * 100))),
          loadGLB(CAR_URL),
        ]);
        if (disposed) return;
        carTemplate = buildCarTemplate(car);
        setupParking(parking);
        if (slots.size !== DEMO_SLOT_CODES.length) throw new Error(`Expected ${DEMO_SLOT_CODES.length} mapped parking slots, found ${slots.size}.`);
        setProgress(100);
        emit();
      } catch (e) {
        console.error(e);
        if (!disposed) setError("Could not load this 3D model. Check that its GLB file is available.");
      }
    })();
    tick();

    return () => {
      disposed = true;
      cancelAnimationFrame(raf);
      ro.disconnect();
      controls.dispose();
      scene.traverse((o) => { o.geometry?.dispose?.(); });
      renderer.dispose();
      renderer.domElement.remove();
      engine.current = null;
    };
  }, [parkingUrl]);

  return (
    <div className={`relative w-full h-full min-h-[420px] ${className}`}>
      <div ref={mountRef} className="absolute inset-0" />
      {progress < 100 && !error && (
        <div className="absolute inset-0 flex items-center justify-center bg-slate-900/60 text-white text-sm">
          Loading 3D model… {progress}%
        </div>
      )}
      {error && (
        <div className="absolute inset-0 flex items-center justify-center bg-slate-900/70 text-red-300 text-sm px-6 text-center">
          {error}
        </div>
      )}
    </div>
  );
});

export default ParkingScene3D;
