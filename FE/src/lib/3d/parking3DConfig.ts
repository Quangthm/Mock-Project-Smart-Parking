import type { LotType, SlotStatus } from "../types"
import type { SlotVisualState } from "../operationalLayout"

export interface Parking3DModelDef {
  id: string

  fileName: "outdoor_parking_lot.glb" | "indoor_parking_lot.glb" | "underground_parking_lot.glb"

  path: string

  label: string

  lotType: LotType

  description: string
}

export const ALLOWED_PARKING_MODELS: Record<LotType, Parking3DModelDef> = {
  outdoor: {
    id: "outdoor_parking_lot",

    fileName: "outdoor_parking_lot.glb",

    path: "/models/parking/outdoor_parking_lot.glb",

    label: "Outdoor Parking Lot",

    lotType: "outdoor",

    description: "Outdoor parking 3D model (outdoor_parking_lot.glb)",
  },

  "multi-storey": {
    id: "indoor_parking_lot",

    fileName: "indoor_parking_lot.glb",

    path: "/models/parking/indoor_parking_lot.glb",

    label: "Indoor / Multi-Storey Parking Lot",

    lotType: "multi-storey",

    description:
      "Indoor / multi-storey parking 3D model (indoor_parking_lot.glb)",
  },

  basement: {
    id: "underground_parking_lot",

    fileName: "underground_parking_lot.glb",

    path: "/models/parking/underground_parking_lot.glb",

    label: "Underground / Basement Parking Lot",

    lotType: "basement",

    description:
      "Underground parking 3D model (underground_parking_lot.glb)",
  },
}

export const ALLOWED_MODEL_FILE_NAMES = [
  "outdoor_parking_lot.glb",

  "outdoor_parking_lot_.glb",

  "indoor_parking_lot.glb",

  "underground_parking_lot.glb",
] as const

export type AllowedModelFileName = typeof ALLOWED_MODEL_FILE_NAMES[number]

export function isAllowedModelFileName(
  fileName: string,
): fileName is AllowedModelFileName {
  return ALLOWED_MODEL_FILE_NAMES.includes(
    fileName.trim().toLowerCase() as AllowedModelFileName,
  )
}

export function getModelDefByFileName(
  fileName: string,
): Parking3DModelDef | undefined {
  const normalized = fileName.trim().toLowerCase()

  if (normalized === "outdoor_parking_lot_.glb") {
    return ALLOWED_PARKING_MODELS.outdoor
  }

  return Object.values(ALLOWED_PARKING_MODELS).find(
    (m) => m.fileName.toLowerCase() === normalized,
  )
}

export function getModelDefByLotType(lotType: LotType): Parking3DModelDef {
  return ALLOWED_PARKING_MODELS[lotType] || ALLOWED_PARKING_MODELS.outdoor
}

export function validateModelUpload(
  fileName: string,
  expectedLotType?: LotType,
): {
  isValid: boolean

  message?: string

  modelDef?: Parking3DModelDef
} {
  const normalized = fileName.trim().toLowerCase()

  if (!isAllowedModelFileName(normalized)) {
    return {
      isValid: false,

      message: `File name "${fileName}" is invalid. Only outdoor_parking_lot.glb, indoor_parking_lot.glb, or underground_parking_lot.glb are supported.`,
    }
  }

  const modelDef = getModelDefByFileName(normalized)

  if (!modelDef) {
    return {
      isValid: false,

      message: "No matching 3D model definition was found.",
    }
  }

  if (expectedLotType && modelDef.lotType !== expectedLotType) {
    return {
      isValid: false,

      message: `File "${fileName}" is for "${modelDef.label}", but the selected site type is "${ALLOWED_PARKING_MODELS[expectedLotType].label}". Choose a matching model file or change the site type.`,

      modelDef,
    }
  }

  return {
    isValid: true,

    modelDef,
  }
}

export const SLOT_STATUS_COLORS: Record<SlotStatus | "selected" | "default", number> =
  {
    available: 0x22c55e, // Emerald Green

    occupied: 0xef4444, // Red

    reserved: 0xf59e0b, // Amber

    selected: 0x3b82f6, // Blue

    maintenance: 0x64748b, // Slate Gray

    disabled: 0x475569, // Dark Slate

    default: 0x64748b, // Slate Gray
  }

export const SLOT_STATUS_HEX: Record<SlotStatus | "selected" | "default", string> = {
  available: "#22c55e",

  occupied: "#ef4444",

  reserved: "#f59e0b",

  selected: "#3b82f6",

  maintenance: "#64748b",

  disabled: "#475569",

  default: "#64748b",
}

/**
 * Two-axis visual vocabulary (SPARK-228). Keyed by the presentation state
 * derived from `physicalState` + `reservationState`, never by a single
 * collapsed `status` field.
 *
 * `unknown` is deliberately a desaturated violet-grey. It must never read as an
 * empty/free bay (A4), so it is far from the emerald `available` and also
 * distinct from the blue-grey `maintenance` and the near-black `unavailable`.
 *
 * `backup` is a saturated cyan (hue ~191°). A designated backup bay is an
 * intentional hold, so it sits opposite the warm amber `reserved` in hue while
 * keeping a comparable weight: an operator scanning the scene reads "cool hold"
 * vs "warm hold" without comparing labels. It is also ~50° off `available`
 * green, ~70° off `unknown` violet-grey and ~76° off `occupied` red.
 */
export const SLOT_VISUAL_STATE_COLORS: Record<
  SlotVisualState | "selected" | "default",
  number
> = {
  available: 0x22c55e, // Emerald Green
  occupied: 0xef4444, // Red
  reserved: 0xf59e0b, // Amber
  backup: 0x0891b2, // Cyan — designated backup buffer, never amber
  maintenance: 0x64748b, // Slate Gray
  unavailable: 0x334155, // Deep Slate — physically blocked / out of service
  unknown: 0x7c6f8f, // Desaturated violet-grey — no reliable reading
  selected: 0x3b82f6, // Blue
  default: 0x64748b, // Slate Gray
}

export const SLOT_VISUAL_STATE_HEX: Record<
  SlotVisualState | "selected" | "default",
  string
> = {
  available: "#22c55e",
  occupied: "#ef4444",
  reserved: "#f59e0b",
  backup: "#0891b2",
  maintenance: "#64748b",
  unavailable: "#334155",
  unknown: "#7c6f8f",
  selected: "#3b82f6",
  default: "#64748b",
}

export const SLOT_VISUAL_STATE_LABELS: Record<SlotVisualState, string> = {
  available: "Available",
  occupied: "Occupied",
  reserved: "Reserved",
  backup: "Backup",
  maintenance: "Maintenance",
  unavailable: "Unavailable",
  unknown: "Unknown",
}
