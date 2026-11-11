// Maps the authoritative operational-layout projection (SPARK-228 contract v1.2.0)
// into the existing driver-facing `ParkingSlot` shape.
//
// Only the presentation `status` is derived; the raw contract `slotCode` is
// preserved as `number` because the 3D viewer binds meshes by that exact value
// (FR-MAP-03). Type-only imports keep this module dependency-free so the
// CommonJS test harness can load it without stubbing a module graph.
import type { ParkingSlot, SlotStatus } from "./types"
import type {
  PhysicalState,
  ReservationState,
  SlotVisualStateDto,
} from "./operationalLayout"

/**
 * Physical axis -> derived `ParkingSlot.status` presentation value. UNKNOWN and
 * UNAVAILABLE fail closed to `disabled`, never `available`.
 *
 * Mirrors `operatorApi.PHYSICAL_SLOT_STATUS` so the driver and operator screens
 * agree on the derived status convention.
 */
const PHYSICAL_SLOT_STATUS: Record<PhysicalState, SlotStatus> = {
  AVAILABLE: "available",
  OCCUPIED: "occupied",
  UNKNOWN: "disabled",
  MAINTENANCE: "maintenance",
  UNAVAILABLE: "disabled",
}

/** Reservation overlay, applied only when the physical axis is AVAILABLE. */
const RESERVATION_SLOT_STATUS: Record<ReservationState, SlotStatus> = {
  UNRESERVED: "available",
  RESERVED: "reserved",
  PROTECTED: "reserved",
  BACKUP: "reserved",
}

/**
 * Derived presentation status, mirroring `operatorApi.derivedSlotStatus`.
 * Unknown axis values fail closed to `disabled`, never `available`.
 */
export function driverSlotStatus(
  physicalState: PhysicalState,
  reservationState: ReservationState,
): SlotStatus {
  const physical = PHYSICAL_SLOT_STATUS[physicalState]
  if (physical !== "available") return physical || "disabled"
  return RESERVATION_SLOT_STATUS[reservationState] || "disabled"
}

/** Floor number parsed from the contract `floorCode` digits, defaulting to 1. */
export function driverSlotFloor(floorCode: string | null | undefined): number {
  const match = /(\d+)/.exec(floorCode ?? "")
  if (!match) return 1
  const floor = Number.parseInt(match[1], 10)
  return Number.isFinite(floor) && floor > 0 ? floor : 1
}

/** Maps one contract slot into the driver `ParkingSlot` shape. */
export function mapOperationalLayoutSlot(slot: SlotVisualStateDto): ParkingSlot {
  return {
    id: slot.slotId,
    // Raw contract code: the 3D mesh binding key, never `formatSlotLabel(...)`.
    number: slot.slotCode,
    floor: driverSlotFloor(slot.floorCode),
    status: driverSlotStatus(slot.physicalState, slot.reservationState),
  }
}

/** Maps a whole projection's `slots` array into driver `ParkingSlot` records. */
export function mapOperationalLayoutSlots(
  slots: readonly SlotVisualStateDto[] | null | undefined,
): ParkingSlot[] {
  if (!Array.isArray(slots)) return []
  return slots.map(mapOperationalLayoutSlot)
}
