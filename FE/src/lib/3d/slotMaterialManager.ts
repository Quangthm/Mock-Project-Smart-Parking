import * as THREE from "three"

import type { ParkingSlot } from "../types"

import { SLOT_STATUS_COLORS } from "./parking3DConfig"

export class SlotMaterialManager {
  private originalMaterials =
    new Map<number, THREE.Material | THREE.Material[]>()

  private activeMaterials = new Map<number, THREE.MeshStandardMaterial>()

  /**
   * Applies the correct material color and state to a slot mesh.
   */

  public applySlotState(
    mesh: THREE.Mesh,

    slot: ParkingSlot | null | undefined,

    isSelected: boolean,

    isFocused: boolean,
  ): void {
    if (!(mesh instanceof THREE.Mesh)) return

    // Save original material if not saved yet

    if (!this.originalMaterials.has(mesh.id)) {
      this.originalMaterials.set(mesh.id, mesh.material)
    }

    const status = slot?.status || "available"

    let targetColor = SLOT_STATUS_COLORS[status]

    let emissiveIntensity = 0.2

    let opacity = 0.85

    if (isSelected || isFocused) {
      targetColor = SLOT_STATUS_COLORS.selected

      emissiveIntensity = 0.6

      opacity = 0.95
    } else if (status === "occupied") {
      opacity = 0.75

      emissiveIntensity = 0.25
    } else if (status === "reserved") {
      opacity = 0.8

      emissiveIntensity = 0.3
    }

    // Get or create dedicated slot material

    let mat = this.activeMaterials.get(mesh.id)

    if (!mat) {
      mat = new THREE.MeshStandardMaterial({
        color: targetColor,

        roughness: 0.35,

        metalness: 0.1,

        transparent: true,

        opacity,

        emissive: targetColor,

        emissiveIntensity,

        side: THREE.DoubleSide,
      })

      this.activeMaterials.set(mesh.id, mat)

      mesh.material = mat
    } else {
      mat.color.setHex(targetColor)

      mat.emissive.setHex(targetColor)

      mat.emissiveIntensity = emissiveIntensity

      mat.opacity = opacity

      mat.needsUpdate = true
    }
  }

  /**
   * Pulse animation for the currently selected or focused slot.
   */

  public tick(time: number, selectedMeshId?: number): void {
    if (!selectedMeshId) return

    const mat = this.activeMaterials.get(selectedMeshId)

    if (mat) {
      const pulse = 0.45 + 0.35 * Math.sin(time * 5)

      mat.emissiveIntensity = pulse
    }
  }

  /**
   * Restores original material to a mesh.
   */

  public restoreOriginal(mesh: THREE.Mesh): void {
    const orig = this.originalMaterials.get(mesh.id)

    if (orig) {
      mesh.material = orig
    }
  }

  /**
   * Clean up all allocated materials.
   */

  public dispose(): void {
    this.activeMaterials.forEach((mat) => mat.dispose())

    this.activeMaterials.clear()

    this.originalMaterials.clear()
  }
}
