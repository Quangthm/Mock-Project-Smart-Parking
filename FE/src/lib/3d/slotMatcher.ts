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

  // Strip common prefixes: "slot_", "slot-", "slot ", "parking_", "bay_"
  str = str.replace(/^(?:slot|parking|bay|mesh|object)[_\s-]?/i, '');

  // Replace underscores between letter and numbers with hyphen (e.g., A_001 -> A-001)
  str = str.replace(/^([a-zA-Z]+)_+(\d+)/, '$1-$2');

  return str.toUpperCase();
}

/**
 * Checks whether an object name resembles a parking slot.
 * Matches patterns such as:
 * - A-001, B-023, M-005
 * - 101, 202
 * - Slot_A-001, slot-B-012
 */
export function isParkingSlotObjectName(name: string): boolean {
  if (!name) return false;
  const normalized = normalizeSlotIdentifier(name);

  // Pattern: Letter(s) followed by optional hyphen and numbers (e.g., A-001, A001, M-12)
  // or pure digits (e.g., 101, 102)
  const slotPattern = /^([A-Z]{1,3}-\d{1,4}|\d{2,4}|[A-Z]{1,2}\d{2,4})$/;
  return slotPattern.test(normalized);
}

export interface SlotMatchResult {
  /** Map from normalized slot number/key to Three.js Object3D */
  meshBySlotKey: Map<string, THREE.Object3D>;
  /** Map from Object3D id to ParkingSlot */
  slotByObjectId: Map<number, ParkingSlot>;
  /** All slot keys found in the 3D model */
  detectedSlotKeys: string[];
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

  // Build lookup index for provided ParkingSlot data
  const slotIndex = new Map<string, ParkingSlot>();
  for (const s of slots) {
    const keyNum = normalizeSlotIdentifier(s.number);
    const keyId = normalizeSlotIdentifier(s.id);
    if (keyNum) slotIndex.set(keyNum, s);
    if (keyId) slotIndex.set(keyId, s);
    // Also index standard variation without hyphen (e.g., A001)
    const noHyphen = keyNum.replace(/-/g, '');
    if (noHyphen) slotIndex.set(noHyphen, s);
  }

  scene.traverse((child) => {
    // We check either Meshes or Group objects specifically designated as slots
    const isSlotCandidate =
      (child instanceof THREE.Mesh || child.userData.isSlot === true) &&
      (child.userData.isSlot === true || isParkingSlotObjectName(child.name));

    if (isSlotCandidate) {
      const explicitKey = child.userData.slotNumber || child.userData.slotId;
      const slotKey = normalizeSlotIdentifier(explicitKey || child.name);

      if (slotKey) {
        detectedKeysSet.add(slotKey);
        meshBySlotKey.set(slotKey, child);
        // Also map without hyphen
        const cleanKey = slotKey.replace(/-/g, '');
        meshBySlotKey.set(cleanKey, child);

        // Find matching ParkingSlot
        const matchedSlot =
          slotIndex.get(slotKey) ||
          slotIndex.get(cleanKey) ||
          slots.find((s) => normalizeSlotIdentifier(s.number) === slotKey || normalizeSlotIdentifier(s.id) === slotKey);

        if (matchedSlot) {
          slotByObjectId.set(child.id, matchedSlot);
          child.userData.slotData = matchedSlot;
        }

        child.userData.isSlot = true;
        child.userData.slotKey = slotKey;
      }
    }
  });

  return {
    meshBySlotKey,
    slotByObjectId,
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

  return meshBySlotKey.get(normalized) || meshBySlotKey.get(noHyphen) || null;
}
