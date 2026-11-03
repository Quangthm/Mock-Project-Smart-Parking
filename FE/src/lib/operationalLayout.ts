// Two-axis operational layout contract — 3D Digital Twin API v1.2.0 (SPARK-228).
//
// The physical and reservation axes are deliberately kept separate. Never collapse
// them into a single `status` field: the contract makes that a HIGH risk (R2), because
// a bay can be OCCUPIED + PROTECTED and only AVAILABLE + UNRESERVED is bookable.

export type PhysicalState = 'AVAILABLE' | 'OCCUPIED' | 'UNKNOWN' | 'MAINTENANCE' | 'UNAVAILABLE'
export type ReservationState = 'UNRESERVED' | 'RESERVED' | 'PROTECTED' | 'BACKUP'
export type SessionType = 'STANDARD' | 'EV_CHARGING' | 'RESERVED' | 'VIP'
export type SupportedVehicleType = 'CAR' | 'MOTORCYCLE' | 'OVERSIZED'
export type SlotType = 'STANDARD' | 'EV' | 'DISABLED' | 'VIP'

/** Raw mesh name used for FR-MAP-03 binding, e.g. `CarParkingSpace_008 2`. */
export const SLOT_CODE_PATTERN = /^[A-Za-z0-9_-]+( [A-Za-z0-9_-]+)?$/

export interface SlotCoordinates3D {
  x: number
  y: number
  z: number
}

export interface SlotFeatures {
  covered?: boolean
  hasCharger?: boolean
  chargerPowerKw?: number | null
  [key: string]: unknown
}

export interface ApproachingReservation {
  reservationId?: string | null
  requestedSlotCode?: string | null
  allocatedSlotCode?: string | null
  isReallocated?: boolean | null
  reallocationReason?: string | null
  expectedArrivalTime?: string | null
  gracePeriodExpiresAt?: string | null
  [key: string]: unknown
}

export interface SlotOperatorContext {
  activeSessionId?: string | null
  maskedPlate?: string | null
  approachingReservation?: ApproachingReservation | null
  [key: string]: unknown
}

export interface SlotVisualStateDto {
  slotId: string
  slotCode: string
  floorId: string
  floorCode: string
  zoneCode?: string | null
  rowCode?: string | null
  supportedVehicleType: SupportedVehicleType
  slotType: SlotType
  physicalState: PhysicalState
  reservationState: ReservationState
  sessionType?: SessionType | null
  isEvCharging: boolean
  isBookable: boolean
  coordinates3d?: SlotCoordinates3D | null
  features?: SlotFeatures | null
  operatorContext?: SlotOperatorContext | null
  updatedAt: string
}

export interface MotorcycleCapacity {
  maxCapacity: number
  occupiedCount: number
  availableCount: number
}

export interface ZoneCapacity {
  zoneId: string
  zoneCode: string
  floorCode: string
  name?: string | null
  maxCapacity: number
  occupiedCount: number
  allocatedSlotCount?: number
  zoneOnlyReservedCount: number
}

export interface LayoutMetrics {
  totalPhysicalCapacity: number
  occupiedCount: number
  reservedCount: number
  availableCarSlots: number
  motorcycleCapacity: MotorcycleCapacity
  zoneCapacities?: ZoneCapacity[]
}

export interface FloorProjection {
  floorId: string
  floorCode: string
  name: string
  level: number
  maxCapacity: number
}

export interface OperationalLayoutProjection {
  siteId: string
  siteCode: string
  siteName: string
  contractVersion: string
  generatedAt: string
  isFresh: boolean
  stalenessThresholdSeconds: number
  metrics: LayoutMetrics
  floors: FloorProjection[]
  slots: SlotVisualStateDto[]
}

export interface OperationalLayoutEnvelope {
  success: boolean
  data: OperationalLayoutProjection
}

/**
 * Presentation state derived from the two contract axes.
 *
 * `unknown` is the fail-closed value: it must never be rendered as an empty or
 * available bay (acceptance A4).
 */
export type SlotVisualState =
  | 'available'
  | 'occupied'
  | 'reserved'
  | 'backup'
  | 'maintenance'
  | 'unavailable'
  | 'unknown'

/** Base colour class, driven by the physical axis only. */
export const PHYSICAL_STATE_VISUAL: Record<PhysicalState, SlotVisualState> = {
  AVAILABLE: 'available',
  OCCUPIED: 'occupied',
  UNKNOWN: 'unknown',
  MAINTENANCE: 'maintenance',
  UNAVAILABLE: 'unavailable',
}

/**
 * Reservation overlay. Only applied when the physical axis is AVAILABLE: the
 * contract keeps an occupied bay OCCUPIED even when it is PROTECTED or BACKUP.
 *
 * BACKUP is a distinct visual state, not a synonym for RESERVED: an operator
 * who designates a bay as an operational buffer must be able to tell it apart
 * from a normal reservation (SPARK-228 follow-up).
 */
export const RESERVATION_STATE_VISUAL: Record<ReservationState, SlotVisualState> = {
  UNRESERVED: 'available',
  RESERVED: 'reserved',
  PROTECTED: 'reserved',
  BACKUP: 'backup',
}

/** Records are read with an explicit miss branch so a drifted value fails closed. */
export function mapSlotVisualState(
  slot: Pick<SlotVisualStateDto, 'physicalState' | 'reservationState'> | null | undefined,
): SlotVisualState {
  if (!slot) return 'unknown'

  const physical = PHYSICAL_STATE_VISUAL[slot.physicalState]
  if (!physical) return 'unknown'
  if (physical !== 'available') return physical

  return RESERVATION_STATE_VISUAL[slot.reservationState] ?? 'unknown'
}

/**
 * Booking eligibility. The server value wins when present; otherwise it is
 * derived from the two axes so a missing field can never read as bookable.
 */
export function isSlotBookable(
  slot:
    | (Pick<SlotVisualStateDto, 'physicalState' | 'reservationState'> & {
        isBookable?: boolean | null
      })
    | null
    | undefined,
): boolean {
  if (!slot) return false
  if (typeof slot.isBookable === 'boolean') return slot.isBookable
  return slot.physicalState === 'AVAILABLE' && slot.reservationState === 'UNRESERVED'
}

/**
 * Staleness is decided by the server: the threshold always comes from the
 * response, never from a hard-coded value. A missing or nonsensical threshold,
 * an explicit `isFresh: false`, or an unparsable timestamp all fail closed.
 */
export function isLayoutStale(
  layout: Pick<OperationalLayoutProjection, 'generatedAt' | 'isFresh' | 'stalenessThresholdSeconds'> | null | undefined,
  now: number = Date.now(),
): boolean {
  if (!layout) return true
  if (layout.isFresh === false) return true

  const threshold = layout.stalenessThresholdSeconds
  if (typeof threshold !== 'number' || !Number.isFinite(threshold) || threshold <= 0) return true

  const generatedAt = Date.parse(layout.generatedAt)
  if (Number.isNaN(generatedAt)) return true

  return now - generatedAt > threshold * 1000
}

/** Age of the snapshot in seconds, or `null` when the timestamp is unusable. */
export function layoutAgeSeconds(
  layout: Pick<OperationalLayoutProjection, 'generatedAt'> | null | undefined,
  now: number = Date.now(),
): number | null {
  if (!layout) return null
  const generatedAt = Date.parse(layout.generatedAt)
  if (Number.isNaN(generatedAt)) return null
  return Math.max(0, Math.round((now - generatedAt) / 1000))
}

export type SlotCodeMatch = 'exact' | 'none'

/**
 * FR-MAP-03 binding: the contract `slotCode` is the raw GLB mesh name, so the
 * comparison is exact and case-sensitive. No prefix stripping, no index fallback.
 */
export function matchSlotCode(meshNames: readonly string[], slotCode: string): SlotCodeMatch {
  if (!slotCode) return 'none'
  return meshNames.includes(slotCode) ? 'exact' : 'none'
}

export interface SlotCodeMatchReport {
  matched: Map<string, string>
  unmatchedSlotCodes: string[]
  unmatchedMeshNames: string[]
}

/**
 * Dev-only fixture accessor for `parkingApi.operationalLayout`.
 *
 * Returns `null` unless `VITE_OPERATIONAL_LAYOUT_FIXTURE === 'true'` and Vite's
 * DEV flag are both set, so production always hits the parking API. The accessor
 * lives here rather than in `parkingApi.ts` only because the existing CommonJS
 * test harnesses transpile `parkingApi.ts` and cannot parse a second
 * `import.meta.env` reference there.
 */
export async function readOperationalLayoutFixture(): Promise<OperationalLayoutProjection | null> {
  if (import.meta.env.VITE_OPERATIONAL_LAYOUT_FIXTURE !== 'true' || !import.meta.env.DEV) {
    return null
  }

  const module = await import('../fixtures/slots-operational-fixture.json')
  const envelope = (module.default ?? module) as OperationalLayoutEnvelope
  if (envelope.success === false) throw new Error('Parking request failed.')

  return envelope.data
}

/**
 * Display-only label for a bay.
 *
 * The raw `slotCode` is the FR-MAP-03 binding key (it must equal the GLB mesh
 * name exactly), so it is never rewritten for the data layer. This function
 * exists purely for what a human reads on screen: the asset names
 * `CarParkingSpace_008` and `CarParkingSpace_008 2` (Blender duplicate suffix)
 * become `CAR-008` and `CAR-008-2`, and `PARKING_EV_CAR_001` becomes `EV-001`.
 * Codes that already look human (`CAR-01`, `MOTO-02`) pass through untouched.
 */
export function formatSlotLabel(slotCode: string | null | undefined): string {
  const raw = (slotCode ?? '').trim()
  if (!raw) return ''

  const car = /^CarParkingSpace_(\d+)(?:\s+(\d+))?$/i.exec(raw)
  if (car) return car[2] ? `CAR-${car[1]}-${car[2]}` : `CAR-${car[1]}`

  const ev = /^PARKING_EV_CAR_(\d+)$/i.exec(raw)
  if (ev) return `EV-${ev[1]}`

  return raw
}

/** Same rule as {@link matchSlotCode}, for a whole site; used for diagnostics. */
export function matchSlotCodes(
  meshNames: readonly string[],
  slotCodes: readonly string[],
): SlotCodeMatchReport {
  const meshSet = new Set(meshNames)
  const matched = new Map<string, string>()
  const unmatchedSlotCodes: string[] = []

  for (const slotCode of slotCodes) {
    if (slotCode && meshSet.has(slotCode)) matched.set(slotCode, slotCode)
    else unmatchedSlotCodes.push(slotCode)
  }

  const matchedMeshes = new Set(matched.values())
  const unmatchedMeshNames = meshNames.filter((name) => !matchedMeshes.has(name))

  return { matched, unmatchedSlotCodes, unmatchedMeshNames }
}
