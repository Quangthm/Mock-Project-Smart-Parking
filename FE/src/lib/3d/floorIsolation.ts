import * as THREE from "three"
import type { SlotMatchResult } from "./slotMatcher"

export interface BindingDiagnostics {
  matched: number
  unmatchedSlotCodes: string[]
  unmatchedMeshNames: string[]
  totalUnmatchedSlotCodes: number
  totalUnmatchedMeshNames: number
  signature: string
}

const BINDING_EXAMPLE_LIMIT = 10

/**
 * Builds the one-line binding report for a mapping pass. Returns `null` when
 * every contract slot code found a mesh and every candidate mesh found a slot,
 * so callers never log a healthy mapping.
 */
export function describeBindingDiagnostics(
  match: Pick<SlotMatchResult, "unmatchedSlotCodes" | "unmatchedMeshNames">,
  slotCount: number,
): BindingDiagnostics | null {
  const unmatchedSlotCodes = Array.isArray(match.unmatchedSlotCodes)
    ? match.unmatchedSlotCodes
    : []
  const unmatchedMeshNames = Array.isArray(match.unmatchedMeshNames)
    ? match.unmatchedMeshNames
    : []

  if (unmatchedSlotCodes.length === 0 && unmatchedMeshNames.length === 0) {
    return null
  }

  return {
    matched: Math.max(0, slotCount - unmatchedSlotCodes.length),
    unmatchedSlotCodes: unmatchedSlotCodes.slice(0, BINDING_EXAMPLE_LIMIT),
    unmatchedMeshNames: unmatchedMeshNames.slice(0, BINDING_EXAMPLE_LIMIT),
    totalUnmatchedSlotCodes: unmatchedSlotCodes.length,
    totalUnmatchedMeshNames: unmatchedMeshNames.length,
    signature: `${unmatchedSlotCodes.sort().join(",")}|${unmatchedMeshNames.sort().join(",")}`,
  }
}

export function isObjectVisible(obj: THREE.Object3D): boolean {
  let cur: THREE.Object3D | null = obj
  while (cur) {
    if (!cur.visible) return false
    cur = cur.parent
  }
  return true
}

export function applyFloorIsolation(
  scene: THREE.Object3D,
  activeFloor?: string | null,
): THREE.Object3D | null {
  if (!activeFloor || activeFloor.toLowerCase() === "all") {
    scene.traverse((node) => {
      node.visible = true
    })
    return scene
  }

  const cleanFloor = activeFloor.trim().toUpperCase()

  const floorGroups: Array<{ key: string; node: THREE.Object3D }> = []
  scene.traverse((node) => {
    if (node.children && node.children.length > 0) {
      const match = (node.name || "").match(
        /^(?:Floor[_-]?([A-Z0-9]+)|Level[_-]?([A-Z0-9]+)|(B\d+)|(OutdoorParking))$/i,
      )
      if (match) {
        const key = (match[1] || match[2] || match[3] || match[4] || "").toUpperCase()
        floorGroups.push({ key, node })
      }
    }
  })

  // For models with 0 or 1 floor (like outdoor lots), keep all nodes visible
  if (floorGroups.length <= 1) {
    scene.traverse((node) => {
      node.visible = true
    })
    return scene
  }

  // Find target floor group
  let activeGroup: THREE.Object3D | null = null
  for (const item of floorGroups) {
    const isTarget =
      item.key === cleanFloor ||
      item.node.name.toUpperCase() === cleanFloor ||
      item.node.name.toUpperCase() === `FLOOR_${cleanFloor}` ||
      (cleanFloor === "GROUND" && (item.key === "OUTDOORPARKING" || item.key === "G")) ||
      (cleanFloor === "OUTDOOR" && item.key === "OUTDOORPARKING")

    if (isTarget) {
      activeGroup = item.node
      break
    }
  }

  // If no floor group matched the active floor (e.g. stale floor ID from another site),
  // fallback to showing all nodes - NEVER hide all nodes!
  if (!activeGroup) {
    scene.traverse((node) => {
      node.visible = true
    })
    return scene
  }

  // Hide other floor groups, show the active floor group
  for (const item of floorGroups) {
    item.node.visible = item.node === activeGroup
  }

  // Ensure active floor group and all ancestors (e.g. Scene 1, Node_0) remain visible
  let ancestor: THREE.Object3D | null = activeGroup
  while (ancestor) {
    ancestor.visible = true
    ancestor = ancestor.parent
  }

  return activeGroup
}

export interface UnregisteredMeshRef {
  key: string
  name: string
}
