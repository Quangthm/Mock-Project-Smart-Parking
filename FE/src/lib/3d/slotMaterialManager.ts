import * as THREE from "three"

import {
  mapSlotVisualState,
  type PhysicalState,
  type ReservationState,
  type SlotVisualState,
} from "../operationalLayout"

import { SLOT_VISUAL_STATE_COLORS } from "./parking3DConfig"

/**
 * Minimal shape accepted by the material manager. A slot may carry the full
 * two-axis projection, or only the legacy single `status` while older screens
 * are still being migrated.
 */
export type SlotMaterialInput = {
  status?: string | null
  physicalState?: PhysicalState | null
  reservationState?: ReservationState | null
}

const LEGACY_STATUS_VISUAL: Partial<Record<string, SlotVisualState>> = {
  available: "available",
  occupied: "occupied",
  reserved: "reserved",
  maintenance: "maintenance",
  disabled: "unavailable",
}

/**
 * Fail-closed visual state resolver.
 *
 * The two-axis projection always wins and is mapped through
 * {@link mapSlotVisualState}, which returns `unknown` for a missing or drifted
 * value. Legacy single-axis payloads are mapped through a closed record; an
 * unrecognised or missing legacy status is also `unknown`, never `available`.
 */
export function resolveSlotVisualState(
  slot: SlotMaterialInput | null | undefined,
): SlotVisualState {
  if (!slot) return "unknown"

  if (slot.physicalState != null) {
    return mapSlotVisualState({
      physicalState: slot.physicalState,
      reservationState: slot.reservationState as ReservationState,
    })
  }

  const legacy = slot.status
  if (!legacy) return "unknown"
  return LEGACY_STATUS_VISUAL[legacy] ?? "unknown"
}

/**
 * Closed record: every {@link SlotVisualState} must be listed, so a new state
 * cannot silently fall back to `unknown`. `backup` keeps the `reserved` weight
 * (same opacity/emissive strength) because both are intentional holds; only the
 * hue differs, which is what tells an operator them apart.
 */
const STATUS_MATERIAL_CONFIG: Record<
  SlotVisualState,
  { opacity: number; emissiveIntensity: number }
> = {
  available: { opacity: 0.85, emissiveIntensity: 0.2 },
  occupied: { opacity: 0.75, emissiveIntensity: 0.25 },
  reserved: { opacity: 0.8, emissiveIntensity: 0.3 },
  backup: { opacity: 0.8, emissiveIntensity: 0.3 },
  maintenance: { opacity: 0.85, emissiveIntensity: 0.2 },
  unavailable: { opacity: 0.65, emissiveIntensity: 0.08 },
  unknown: { opacity: 0.72, emissiveIntensity: 0.1 },
}

const HIGHLIGHT_MATERIAL_CONFIG = { opacity: 0.95, emissiveIntensity: 0.6 }

export class SlotMaterialManager {
  private originalMaterials =
    new Map<number, THREE.Material | THREE.Material[]>()

  private activeMaterials = new Map<number, THREE.MeshStandardMaterial>()

  // Dirty check: cache applied visual state signature per mesh to avoid
  // redundant WebGL material recompilation / updates when state has not changed.
  private appliedSlotState = new Map<number, string>()

  /**
   * Applies the correct material color and state to a slot mesh.
   */

  public applySlotState(
    mesh: THREE.Mesh,

    slot: SlotMaterialInput | null | undefined,

    isSelected: boolean,

    isFocused: boolean,
  ): void {
    if (!(mesh instanceof THREE.Mesh)) return

    // Save original material if not saved yet

    if (!this.originalMaterials.has(mesh.id)) {
      this.originalMaterials.set(mesh.id, mesh.material)
    }

    const visualState = resolveSlotVisualState(slot)

    const isHighlighted = isSelected || isFocused
    const targetColor = isHighlighted
      ? SLOT_VISUAL_STATE_COLORS.selected
      : (SLOT_VISUAL_STATE_COLORS[visualState] ??
        SLOT_VISUAL_STATE_COLORS.unknown)

    const { opacity, emissiveIntensity } = isHighlighted
      ? HIGHLIGHT_MATERIAL_CONFIG
      : (STATUS_MATERIAL_CONFIG[visualState] ?? STATUS_MATERIAL_CONFIG.unknown)

    const stateSignature = `state:${targetColor}:${opacity}:${emissiveIntensity}:${isHighlighted ? 1 : 0}`
    if (
      this.appliedSlotState.get(mesh.id) === stateSignature &&
      this.activeMaterials.has(mesh.id) &&
      mesh.material === this.activeMaterials.get(mesh.id)
    ) {
      return
    }
    this.appliedSlotState.set(mesh.id, stateSignature)

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

      mesh.material = mat

      mat.needsUpdate = true
    }
  }

  /**
   * Selection tint for a template mesh the Owner picked for registration. An unselected
   * mesh is restored to the model's own material so it stays visually inert.
   */

  public applyRegistrationState(mesh: THREE.Mesh, selected: boolean): void {
    if (!(mesh instanceof THREE.Mesh)) return

    if (!this.originalMaterials.has(mesh.id)) {
      this.originalMaterials.set(mesh.id, mesh.material)
    }

    if (!selected) {
      this.restoreOriginal(mesh)
      return
    }

    const regSignature = "reg:selected"
    if (
      this.appliedSlotState.get(mesh.id) === regSignature &&
      this.activeMaterials.has(mesh.id) &&
      mesh.material === this.activeMaterials.get(mesh.id)
    ) {
      return
    }
    this.appliedSlotState.set(mesh.id, regSignature)

    const targetColor = 0xf59e0b
    let mat = this.activeMaterials.get(mesh.id)
    if (!mat) {
      mat = new THREE.MeshStandardMaterial({
        roughness: 0.35,
        metalness: 0.1,
        transparent: true,
        side: THREE.DoubleSide,
      })
      this.activeMaterials.set(mesh.id, mat)
      mesh.material = mat
    }
    mat.color.setHex(targetColor)
    mat.emissive.setHex(targetColor)
    mat.emissiveIntensity = 0.5
    mat.opacity = 0.9
    mesh.material = mat
    mat.needsUpdate = true
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
    this.appliedSlotState.delete(mesh.id)

    const orig = this.originalMaterials.get(mesh.id)

    if (orig) {
      mesh.material = orig
    }
  }

  /**
   * Clean up all allocated materials.
   */

  public dispose(): void {
    this.appliedSlotState.clear()

    this.activeMaterials.forEach((mat) => mat.dispose())

    this.activeMaterials.clear()

    this.originalMaterials.clear()
  }
}
