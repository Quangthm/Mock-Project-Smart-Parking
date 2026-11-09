import * as THREE from "three"

import type { ParkingSlot } from "../types"
import {
  isSlotCandidateName,
  mapMeshCandidates,
  normalizeSlotIdentifier,
} from "./meshSlotMapping"

/**
 * SPARK-238-owned tolerant mesh binding.
 *
 * This is the registration/interactivity side of the viewer: a template mesh is
 * interactive only when it maps to a *registered* slot. Unregistered template
 * meshes keep no slot identity at all: they cannot be hovered, claimed, counted
 * or selected for booking (A7/A12).
 *
 * Operational-overlay colouring is deliberately NOT this module's job: the
 * raw-exact SPARK-228 contract lives in `./slotMatcher` and is composed alongside
 * this mapping in the viewer so both decisions stay single-sourced.
 */
export interface RegisteredMeshMapping {
  /** Map from normalized slot number/key to Three.js Object3D. Registered slots only. */
  meshBySlotKey: Map<string, THREE.Object3D>

  /** Map from Object3D id to ParkingSlot. Registered slots only. */
  slotByObjectId: Map<number, ParkingSlot>

  /** All slot meshes that map to a registered slot: the only interactive meshes. */
  allSlotMeshes: THREE.Mesh[]

  /** Template meshes with no registered slot. Presentation only, never interactive. */
  unregisteredSlotMeshes: THREE.Mesh[]

  /** Stable mesh key (uuid) to raw template name, for the registration multi-select. */
  unregisteredMeshNames: Map<string, string>

  /** All slot keys matched to a registered slot. */
  detectedSlotKeys: string[]
}

/**
 * Checks whether an object is an actual parking bay / slot mesh candidate.
 */
function isSlotCandidateMesh(child: THREE.Object3D): boolean {
  if (child.userData.isSlot === true) return true
  if (!(child instanceof THREE.Mesh)) return false
  return isSlotCandidateName(child.name || "")
}

/**
 * Traverses a 3D model scene graph, detects the template's parking bay meshes and maps
 * them to the provided registered slots.
 *
 * Only a mesh that maps to a registered slot receives slot data and becomes interactive.
 * Unregistered template meshes keep no slot identity at all: they cannot be hovered,
 * claimed, counted or selected for booking (A7/A12).
 */
export function mapRegisteredMeshes(
  scene: THREE.Object3D,
  slots: ParkingSlot[] = [],
): RegisteredMeshMapping {
  const meshBySlotKey = new Map<string, THREE.Object3D>()
  const slotByObjectId = new Map<number, ParkingSlot>()
  const detectedKeysSet = new Set<string>()
  const allSlotMeshes: THREE.Mesh[] = []
  const unregisteredSlotMeshes: THREE.Mesh[] = []
  const unregisteredMeshNames = new Map<string, string>()
  const seenMeshes = new Set<THREE.Mesh>()

  // 1. Discover all candidate slot meshes with their world positions
  const candidateList: Array<{ mesh: THREE.Mesh; worldPos: THREE.Vector3 }> = []
  scene.traverse((child) => {
    if (
      child instanceof THREE.Mesh &&
      !seenMeshes.has(child) &&
      isSlotCandidateMesh(child)
    ) {
      seenMeshes.add(child)
      const worldPos = new THREE.Vector3()
      child.getWorldPosition(worldPos)
      candidateList.push({ mesh: child, worldPos })
    }
  })

  // 2. Sort candidate meshes spatially: floor elevation (Y), row (Z), column (X)
  candidateList.sort((a, b) => {
    if (Math.abs(a.worldPos.y - b.worldPos.y) > 1.5) {
      return a.worldPos.y - b.worldPos.y
    }
    if (Math.abs(a.worldPos.z - b.worldPos.z) > 40.0) {
      return a.worldPos.z - b.worldPos.z
    }
    return a.worldPos.x - b.worldPos.x
  })

  // 3. A previous mapping may have decorated these meshes; reset before re-mapping so a
  //    slot that was released in the meantime cannot leave an interactive ghost behind.
  for (const { mesh } of candidateList) {
    delete mesh.userData.isSlot
    delete mesh.userData.slotData
    delete mesh.userData.slotNumber
    delete mesh.userData.slotId
    delete mesh.userData.slotKey
  }

  const slotById = new Map(slots.map((slot) => [slot.id, slot]))
  const mapping = mapMeshCandidates(
    candidateList.map(({ mesh }) => ({ key: mesh.uuid, name: mesh.name || "" })),
    slots.map((slot) => ({ id: slot.id, code: slot.number })),
  )

  const meshByKey = new Map(candidateList.map(({ mesh }) => [mesh.uuid, mesh]))
  for (const match of mapping.matches) {
    const mesh = meshByKey.get(match.key)
    const targetSlot = slotById.get(match.slotId)
    if (!mesh || !targetSlot) continue

    mesh.userData.isSlot = true
    mesh.userData.slotData = targetSlot
    mesh.userData.slotNumber = targetSlot.number
    mesh.userData.slotId = targetSlot.id
    mesh.userData.slotKey = targetSlot.number
    detectedKeysSet.add(targetSlot.number)
    slotByObjectId.set(mesh.id, targetSlot)
    allSlotMeshes.push(mesh)

    // Register all access keys
    if (targetSlot.number) {
      meshBySlotKey.set(targetSlot.number, mesh)
      meshBySlotKey.set(targetSlot.number.replace(/-/g, ""), mesh)
      const digitsOnly = targetSlot.number.replace(/\D/g, "")
      if (digitsOnly) {
        meshBySlotKey.set(digitsOnly, mesh)
        meshBySlotKey.set(String(parseInt(digitsOnly, 10)), mesh)
      }
    }
    if (targetSlot.id) meshBySlotKey.set(targetSlot.id, mesh)
    if (mesh.name) meshBySlotKey.set(mesh.name, mesh)
    meshBySlotKey.set(String(mesh.id), mesh)
  }

  for (const candidate of mapping.unregistered) {
    const mesh = meshByKey.get(candidate.key)
    if (!mesh) continue
    unregisteredSlotMeshes.push(mesh)
    unregisteredMeshNames.set(candidate.key, mesh.name || candidate.name)
  }

  return {
    meshBySlotKey,
    slotByObjectId,
    allSlotMeshes,
    unregisteredSlotMeshes,
    unregisteredMeshNames,
    detectedSlotKeys: Array.from(detectedKeysSet).sort(),
  }
}

/**
 * Given a target slot ID or number, finds the corresponding Three.js Object3D.
 */
export function findRegisteredSlotMesh(
  meshBySlotKey: Map<string, THREE.Object3D>,
  slot: ParkingSlot | string | null | undefined,
): THREE.Object3D | null {
  if (!slot) return null
  const target = typeof slot === "string" ? slot : slot.number || slot.id
  const normalized = normalizeSlotIdentifier(target)
  const noHyphen = normalized.replace(/-/g, "")
  const digitsOnly = normalized.replace(/\D/g, "")
  const trimmedDigits = digitsOnly ? String(parseInt(digitsOnly, 10)) : ""
  return (
    meshBySlotKey.get(target) ||
    meshBySlotKey.get(normalized) ||
    meshBySlotKey.get(noHyphen) ||
    (digitsOnly ? meshBySlotKey.get(digitsOnly) : null) ||
    (trimmedDigits ? meshBySlotKey.get(trimmedDigits) : null) ||
    null
  )
}
