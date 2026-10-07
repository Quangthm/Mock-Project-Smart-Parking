import * as THREE from 'three';
import type { LotType, ParkingSlot } from '../types';

/**
 * Creates a clean 3D parking lot procedural scene.
 * Generates slots matching the requested slots array or generates standardized slots:
 * A-001, A-002, B-001, etc.
 */
export function generateProceduralParkingLot(
  lotType: LotType = 'outdoor',
  slots: ParkingSlot[] = [],
  floors = 1
): THREE.Group {
  const root = new THREE.Group();
  root.name = `Procedural_${lotType}_ParkingLot`;

  // Determine slot count and names
  const effectiveSlots = slots.length > 0 ? slots : generateFallbackSlotList(lotType, floors);
  const slotsPerRow = 6;
  const slotWidth = 2.8;
  const slotLength = 5.2;
  const aisleWidth = 7.0;

  // Environment base
  const totalRows = Math.ceil(effectiveSlots.length / slotsPerRow);
  const lotWidthTotal = slotsPerRow * (slotWidth + 0.4) + 6;
  const lotLengthTotal = totalRows * (slotLength + 1.2) + aisleWidth + 8;

  // Floor / Ground mesh
  const groundGeo = new THREE.PlaneGeometry(lotWidthTotal, lotLengthTotal);
  groundGeo.rotateX(-Math.PI / 2);

  const groundColor =
    lotType === 'basement' ? 0x1e293b : lotType === 'multi-storey' ? 0x334155 : 0x0f172a;
  const groundMat = new THREE.MeshStandardMaterial({
    color: groundColor,
    roughness: 0.8,
    metalness: 0.2,
  });
  const groundMesh = new THREE.Mesh(groundGeo, groundMat);
  groundMesh.receiveShadow = true;
  root.add(groundMesh);

  // Line marking material
  const lineMat = new THREE.MeshBasicMaterial({ color: 0xffffff });

  // Generate slots
  effectiveSlots.forEach((slot, index) => {
    const row = Math.floor(index / slotsPerRow);
    const col = index % slotsPerRow;

    const isTopSide = row % 2 === 0;
    const x = (col - (slotsPerRow - 1) / 2) * (slotWidth + 0.4);
    const zOffset = Math.floor(row / 2) * (slotLength * 2 + aisleWidth);
    const z = (isTopSide ? -1 : 1) * (aisleWidth / 2 + slotLength / 2) + zOffset - lotLengthTotal / 4;

    // Slot mesh representing the parking bay
    const slotGeo = new THREE.BoxGeometry(slotWidth, 0.08, slotLength);
    const slotMat = new THREE.MeshStandardMaterial({
      color: 0x334155,
      roughness: 0.6,
      metalness: 0.1,
    });

    const slotMesh = new THREE.Mesh(slotGeo, slotMat);
    slotMesh.position.set(x, 0.04, z);

    // Set standardized naming convention: e.g. "A-001" or slot.number
    const slotName = slot.number || `A-${String(index + 1).padStart(3, '0')}`;
    slotMesh.name = slotName;
    slotMesh.userData = {
      isSlot: true,
      slotId: slot.id,
      slotNumber: slotName,
      slotData: slot,
    };
    slotMesh.castShadow = true;
    slotMesh.receiveShadow = true;
    root.add(slotMesh);

    // Boundary lines for parking slot
    const lineGeo = new THREE.BoxGeometry(0.12, 0.09, slotLength);
    const leftLine = new THREE.Mesh(lineGeo, lineMat);
    leftLine.position.set(x - slotWidth / 2, 0.05, z);
    root.add(leftLine);

    const rightLine = new THREE.Mesh(lineGeo, lineMat);
    rightLine.position.set(x + slotWidth / 2, 0.05, z);
    root.add(rightLine);

    // Wheel stop bumper
    const bumperGeo = new THREE.BoxGeometry(slotWidth * 0.7, 0.2, 0.25);
    const bumperMat = new THREE.MeshStandardMaterial({ color: 0xf59e0b, roughness: 0.5 });
    const bumper = new THREE.Mesh(bumperGeo, bumperMat);
    bumper.position.set(x, 0.1, z + (isTopSide ? -1 : 1) * (slotLength / 2 - 0.4));
    root.add(bumper);
  });

  // Lighting
  const ambientLight = new THREE.AmbientLight(0xffffff, 0.7);
  root.add(ambientLight);

  const dirLight = new THREE.DirectionalLight(0xffffff, 1.2);
  dirLight.position.set(15, 25, 20);
  dirLight.castShadow = true;
  root.add(dirLight);

  // Additional soft fill light
  const fillLight = new THREE.DirectionalLight(0x93c5fd, 0.5);
  fillLight.position.set(-15, 15, -20);
  root.add(fillLight);

  // Pillars if basement or multi-storey
  if (lotType === 'basement' || lotType === 'multi-storey') {
    const pillarGeo = new THREE.BoxGeometry(0.8, 4.5, 0.8);
    const pillarMat = new THREE.MeshStandardMaterial({ color: 0x475569 });

    for (let col = 0; col <= slotsPerRow; col += 2) {
      const px = (col - slotsPerRow / 2) * (slotWidth + 0.4);
      const pillar = new THREE.Mesh(pillarGeo, pillarMat);
      pillar.position.set(px, 2.25, -lotLengthTotal / 4);
      root.add(pillar);
    }
  }

  return root;
}

function generateFallbackSlotList(lotType: LotType, floors: number): ParkingSlot[] {
  const prefix = lotType === 'outdoor' ? 'A' : lotType === 'basement' ? 'B' : 'M';
  const list: ParkingSlot[] = [];
  const count = 18;
  const statuses: ParkingSlot['status'][] = ['available', 'available', 'available', 'occupied', 'reserved'];

  for (let i = 1; i <= count; i++) {
    list.push({
      id: `slot-fallback-${i}`,
      number: `${prefix}-${String(i).padStart(3, '0')}`,
      floor: 1,
      status: statuses[(i - 1) % statuses.length],
    });
  }
  return list;
}
