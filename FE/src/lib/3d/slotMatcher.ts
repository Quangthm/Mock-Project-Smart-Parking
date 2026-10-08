import * as THREE from 'three';
import type { ParkingSlot } from '../types';

/**
 * Normalizes a slot identifier for robust matching.
 * Examples:
 *   "Slot_A-001" -> "A-001"
 *   "A_001"      -> "A-001"
 *   "a-001"      -> "A-001"
 *   "A-001.001"  -> "A-001" (Blender copy duplicate suffix)
 *   "Slot-101"   -> "101"
 */
export function normalizeSlotIdentifier(raw: string): string {
  if (!raw) return '';
  let str = raw.trim();

  // Strip Blender duplicate suffixes like ".001" or ".002"
  str = str.replace(/\.\d{3}$/, '');

  // Strip common prefixes: "slot_", "slot-", "carparkingspace_", "car_parking_space_", "carparking_", "parking_", "bay_"
  str = str.replace(/^(?:carparkingspace|car_parking_space|carparking|car|parking|slot|bay|mesh|object)[_\s-]?/i, '');

  // Replace underscores between letter and numbers with hyphen (e.g., A_001 -> A-001)
  str = str.replace(/^([a-zA-Z]+)_+(\d+)/, '$1-$2');

  return str.toUpperCase();
}

/**
 * Checks whether an object name resembles a parking slot.
 * Matches patterns such as:
 * - A-001, B-023, M-005
 * - 001, 101, 202
 * - Slot_A-001, slot-B-012, CarParkingSpace_001
 */
export function isParkingSlotObjectName(name: string): boolean {
  if (!name) return false;
  const normalized = normalizeSlotIdentifier(name);

  // Pattern: Letter(s) followed by optional hyphen and numbers (e.g., A-001, A001, M-12)
  // or pure digits (e.g., 001, 101, 102)
  const slotPattern = /^([A-Z]{1,3}-\d{1,4}|\d{1,4}|[A-Z]{1,2}\d{1,4})$/;
  return slotPattern.test(normalized);
}

export interface SlotMatchResult {
  /** Map from normalized slot number/key to Three.js Object3D */
  meshBySlotKey: Map<string, THREE.Object3D>;
  /** Map from Object3D id to ParkingSlot */
  slotByObjectId: Map<number, ParkingSlot>;
  /** All unique candidate slot meshes in the 3D model */
  allSlotMeshes: THREE.Mesh[];
  /** All slot keys found in the 3D model */
  detectedSlotKeys: string[];
}

/**
 * Checks whether an object is an actual parking bay / slot mesh candidate.
 */
function isSlotCandidateMesh(child: THREE.Object3D): boolean {
  if (child.userData.isSlot === true) return true;
  if (!(child instanceof THREE.Mesh)) return false;

  const name = child.name || '';
  // Exclude signs, posts, text, arrows, poles, barriers, chargers, etc.
  if (/sign|post|text|arrow|pole|camera|sensor|barrier|line|screen|logo|body|bay_\d|plinth|led|light/i.test(name)) {
    return false;
  }

  if (/carparkingspace/i.test(name)) return true;
  if (/parking_ev_car/i.test(name)) return true;
  if (/parking_ev/i.test(name)) return true;
  if (/parkingspace/i.test(name)) return true;
  if (/\b(?:slot|bay)\b/i.test(name)) return true;
  if (/car_[g|l\d]_\d+/i.test(name)) return true;
  if (/b\d+_car_\d+/i.test(name)) return true;
  if (/b\d+_ev_car_\d+/i.test(name)) return true;

  return isParkingSlotObjectName(name);
}

/**
 * Traverses a 3D model scene graph, detects all slot meshes,
 * and maps them to the provided ParkingSlot array.
 */
export function mapModelSlots(
  scene: THREE.Object3D,
  slots: ParkingSlot[] = []
): SlotMatchResult {
  const meshBySlotKey = new Map<string, THREE.Object3D>();
  const slotByObjectId = new Map<number, ParkingSlot>();
  const detectedKeysSet = new Set<string>();
  const allSlotMeshes: THREE.Mesh[] = [];
  const seenMeshes = new Set<THREE.Mesh>();

  // 1. Discover all candidate slot meshes with their world positions
  const candidateList: Array<{ mesh: THREE.Mesh; worldPos: THREE.Vector3 }> = [];

  scene.traverse((child) => {
    if (child instanceof THREE.Mesh && !seenMeshes.has(child) && isSlotCandidateMesh(child)) {
      seenMeshes.add(child);
      const worldPos = new THREE.Vector3();
      child.getWorldPosition(worldPos);
      candidateList.push({ mesh: child, worldPos });
    }
  });

  // 2. Sort candidate meshes spatially:
  // - Floor elevation first (Y)
  // - Row second (Z)
  // - Column third (X: left to right)
  candidateList.sort((a, b) => {
    if (Math.abs(a.worldPos.y - b.worldPos.y) > 1.5) {
      return a.worldPos.y - b.worldPos.y;
    }
    if (Math.abs(a.worldPos.z - b.worldPos.z) > 40.0) {
      return a.worldPos.z - b.worldPos.z;
    }
    return a.worldPos.x - b.worldPos.x;
  });

  // 3. Build lookup index for provided ParkingSlot data
  const slotByIndex = new Map<string, ParkingSlot>();
  for (const s of slots) {
    const keyNum = normalizeSlotIdentifier(s.number);
    const keyId = normalizeSlotIdentifier(s.id);
    if (keyNum) slotByIndex.set(keyNum, s);
    if (keyId) slotByIndex.set(keyId, s);
    const noHyphen = keyNum.replace(/-/g, '');
    if (noHyphen) slotByIndex.set(noHyphen, s);
  }

  // 4. Map each candidate mesh to its slot:
  // Spatial ordering guarantees every physical spot gets a distinct slot!
  candidateList.forEach((item, index) => {
    const mesh = item.mesh;
    allSlotMeshes.push(mesh);

    // Prefer spatial index match with slots array, fallback to auto-generated slot
    let targetSlot = slots[index];
    if (!targetSlot) {
      targetSlot = {
        id: `slot-auto-${index + 1}`,
        number: `A-${String(index + 1).padStart(3, '0')}`,
        floor: 1,
        status: 'available',
      };
    }

    mesh.userData.isSlot = true;
    mesh.userData.slotData = targetSlot;
    mesh.userData.slotNumber = targetSlot.number;
    mesh.userData.slotId = targetSlot.id;
    mesh.userData.slotKey = targetSlot.number;

    detectedKeysSet.add(targetSlot.number);
    slotByObjectId.set(mesh.id, targetSlot);

    // Register all access keys
    meshBySlotKey.set(targetSlot.number, mesh);
    meshBySlotKey.set(targetSlot.id, mesh);
    meshBySlotKey.set(targetSlot.number.replace(/-/g, ''), mesh);
    const digitsOnly = targetSlot.number.replace(/\D/g, '');
    if (digitsOnly) {
      meshBySlotKey.set(digitsOnly, mesh);
      meshBySlotKey.set(String(parseInt(digitsOnly, 10)), mesh);
    }
    meshBySlotKey.set(mesh.name, mesh);
    meshBySlotKey.set(String(mesh.id), mesh);
  });

  return {
    meshBySlotKey,
    slotByObjectId,
    allSlotMeshes,
    detectedSlotKeys: Array.from(detectedKeysSet).sort(),
  };
}

/**
 * Given a target slot ID or number, finds the corresponding Three.js Object3D.
 */
export function findSlotMesh(
  meshBySlotKey: Map<string, THREE.Object3D>,
  slot: ParkingSlot | string | null | undefined
): THREE.Object3D | null {
  if (!slot) return null;

  const target = typeof slot === 'string' ? slot : (slot.number || slot.id);
  const normalized = normalizeSlotIdentifier(target);
  const noHyphen = normalized.replace(/-/g, '');
  const digitsOnly = normalized.replace(/\D/g, '');
  const trimmedDigits = digitsOnly ? String(parseInt(digitsOnly, 10)) : '';

  return (
    meshBySlotKey.get(target) ||
    meshBySlotKey.get(normalized) ||
    meshBySlotKey.get(noHyphen) ||
    (digitsOnly ? meshBySlotKey.get(digitsOnly) : null) ||
    (trimmedDigits ? meshBySlotKey.get(trimmedDigits) : null) ||
    null
  );
}
