import { request } from "./authApi"
import { store } from "./store"
import { loadOwnerModelUrl } from "./3d/ownerModelStorage"
import {
  operatorsApi,
  parkingApi,
  type Structure,
  type Unit,
} from "./parkingApi"
import { isSlotBookable } from "./operationalLayout"
import type {
  OperationalLayoutProjection,
  PhysicalState,
  ReservationState,
} from "./operationalLayout"
import type {
  AppNotification,
  Booking,
  BookingStatus,
  PaymentMethod,
  PaymentStatus,
  ParkingSlot,
  SlotStatus,
  SupportTicket,
  User,
} from "./types"

type Response<T> = { success: boolean; data: T }

const reservationServer = (
  import.meta.env.VITE_RESERVATION_API_BASE_URL ?? "http://localhost:5055"
).replace(/\/$/, "")

const parkingServer = (
  import.meta.env.VITE_PARKING_API_BASE_URL ?? "http://localhost:5045"
).replace(/\/$/, "")

export interface OperatorSlot extends ParkingSlot {
  unitId: string
  vehicleType: string
  /** Legacy presentation string kept for existing consumers; `physicalState` is authoritative. */
  operationalStatus: string
  isPhysicallyOccupied: boolean
  /** Normalised reservation axis: legacy `null`/`NONE` becomes `UNRESERVED`. */
  reservationState: ReservationState
  physicalState: PhysicalState
  isBookable: boolean
}

/**
 * Physical axis -> derived `ParkingSlot.status` presentation value. UNKNOWN and
 * UNAVAILABLE fail closed to `disabled`, never `available`.
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

const LEGACY_OPERATIONAL_STATUS: Record<PhysicalState, string> = {
  AVAILABLE: "OPERATIONAL",
  OCCUPIED: "OPERATIONAL",
  UNKNOWN: "UNKNOWN",
  MAINTENANCE: "MAINTENANCE",
  UNAVAILABLE: "UNAVAILABLE",
}

/** Derived presentation status; unknown axis values fail closed to `disabled`. */
function derivedSlotStatus(
  physicalState: PhysicalState,
  reservationState: ReservationState,
): SlotStatus {
  const physical = PHYSICAL_SLOT_STATUS[physicalState]
  if (physical !== "available") return physical || "disabled"
  return RESERVATION_SLOT_STATUS[reservationState] || "disabled"
}

/** Explicit normalisation: legacy `null`/`NONE`/empty means UNRESERVED. */
function normalizeReservationState(raw: string | null | undefined): ReservationState {
  const value = (raw ?? "").trim().toUpperCase()
  if (value === "" || value === "NONE" || value === "UNRESERVED") return "UNRESERVED"
  if (value === "RESERVED") return "RESERVED"
  if (value === "PROTECTED") return "PROTECTED"
  if (value === "BACKUP") return "BACKUP"
  // Unknown legacy values stay non-bookable instead of looking unreserved.
  return "RESERVED"
}

function physicalStateFromLegacy(slot: Structure["slots"][number]): PhysicalState {
  if (slot.isPhysicallyOccupied) return "OCCUPIED"
  const operational = (slot.operationalStatus ?? "").toUpperCase()
  if (operational === "OPERATIONAL") return "AVAILABLE"
  if (operational === "MAINTENANCE") return "MAINTENANCE"
  if (operational === "UNAVAILABLE" || operational === "DISABLED" || operational === "BLOCKED")
    return "UNAVAILABLE"
  return "UNKNOWN"
}

export interface OperatorSite {
  id: string
  tenantId: string
  code: string
  name: string
  address: string
  latitude: number | null
  longitude: number | null
  isActive: boolean
  totalSlots: number
  floors: number
  configuredBackupCount: number
  /**
   * Server freshness triple from the operational-layout projection. `null` on
   * the legacy owner aggregate, which carries no freshness metadata, so callers
   * must treat it as unknown rather than fresh.
   */
  freshness?: {
    generatedAt: string
    isFresh: boolean
    stalenessThresholdSeconds: number
  } | null
  modelUrl?: string
  modelFileName?: string
  slots: OperatorSlot[]
  units: Structure["units"]
}

export interface GateReservation {
  id: string
  accountId: string
  siteId: string
  siteName: string
  slotId: string | null
  slotNumber: string
  licensePlate: string
  status: BookingStatus
  paymentStatus: PaymentStatus | null
  paymentMethod: string | null
  amount: number | null
  startTime: string
  endTime: string
  entryTime: string | null
  exitTime: string | null
  entryImageUrl: string | null
  exitImageUrl: string | null
  createdAt: string
}

export interface WalkInParkingSession {
  id: string
  siteId: string
  slotId: string | null
  slotNumber: string | null
  licensePlate: string
  vehicleType: string
  entryTime: string
  exitTime: string | null
  entryImageUrl: string | null
  exitImageUrl: string | null
}

export interface ParkingSessionHistoryEntry {
  id: string
  reservationId: string | null
  tenantId: string
  siteId: string
  accessItemId: string | null
  accountId: string | null
  currentSlotId: string | null
  slotNumber: string | null
  originalPlate: string | null
  normalizedPlate: string | null
  vehicleType: string
  ticketReference: string | null
  entryTime: string
  entryImageUrl: string | null
  exitTime: string | null
  exitImageUrl: string | null
  status: string
  version: number
  createdAt: string
}

export interface OperatorIncident {
  id: string
  siteId: string
  type: string
  details: string
  status: "reported" | "resolved"
  actorId: string
  createdAt: string
}

export interface OperatorAuditEvent {
  id: string
  actorId: string | null
  actorRole: string
  action: string
  details: string
  timestamp: string
}

function siteFromStructure(structure: Structure): OperatorSite {
  const ownerModel = store.getLots().find(lot => lot.id === structure.site.id)
  const floorUnits = structure.units.filter((unit) => unit.type === "FLOOR")
  const unitsById = new Map(structure.units.map((unit) => [unit.id, unit]))
  const floorNumbers = new Map(floorUnits.map((unit, index) => [unit.id, index + 1]))

  function floorFor(unitId: string) {
    let unit = unitsById.get(unitId)
    while (unit) {
      const floor = floorNumbers.get(unit.id)
      if (floor) return floor
      unit = unit.parentId ? unitsById.get(unit.parentId) : undefined
    }
    return 1
  }

  return {
    id: structure.site.id,
    tenantId: structure.site.tenantId,
    code: structure.site.code,
    name: structure.site.name,
    modelUrl: ownerModel?.modelUrl?.startsWith("blob:") ? undefined : ownerModel?.modelUrl,
    modelFileName: ownerModel?.modelFileName,
    address: structure.site.address,
    latitude: structure.site.latitude,
    longitude: structure.site.longitude,
    isActive: structure.site.isActive,
    totalSlots: structure.slots.length,
    floors: floorUnits.length,
    configuredBackupCount: structure.capacityViews.find((view) => view.unitId === null)?.configuredBackup ?? 0,
    // The legacy aggregate has no freshness triple; never fabricate one.
    freshness: null,
    units: structure.units,
    slots: structure.slots.map((slot) => {
      const physicalState = physicalStateFromLegacy(slot)
      const reservationState = normalizeReservationState(slot.reservationState)

      return {
        id: slot.id,
        unitId: slot.unitId,
        number: slot.code,
        floor: floorFor(slot.unitId),
        status: derivedSlotStatus(physicalState, reservationState),
        vehicleType: slot.vehicleType,
        operationalStatus: slot.operationalStatus,
        isPhysicallyOccupied: physicalState === "OCCUPIED",
        reservationState,
        physicalState,
        isBookable: isSlotBookable({ physicalState, reservationState }),
      }
    }),
  }
}

/**
 * Maps the authoritative operational-layout projection into the existing
 * `OperatorSite`/`OperatorSlot` shape used by the operator screens. The two
 * contract axes are preserved; `status` is only a derived presentation value.
 *
 * The projection does not carry site address/latitude/longitude/isActive, so
 * those legacy display-only fields are left neutral here. Operators cannot read
 * `GET /api/parking-lots`, so we intentionally do not make an extra call for them.
 * The projection's freshness triple is carried through so the screen never has to
 * fetch a second response just to decide staleness.
 */
function siteFromOperationalLayout(
  projection: OperationalLayoutProjection,
  tenantId: string,
): OperatorSite {
  const floorById = new Map(projection.floors.map((floor) => [floor.floorId, floor]))
  const floorIndex = new Map(projection.floors.map((floor, index) => [floor.floorId, index + 1]))
  const units: Unit[] = projection.floors.map((floor) => ({
    id: floor.floorId,
    parentId: null,
    name: floor.name,
    type: "FLOOR",
    maxCapacity: floor.maxCapacity,
  }))
  const zoneUnitIds = new Map<string, string>()

  for (const slot of projection.slots) {
    if (!slot.zoneCode) continue
    const key = `${slot.floorId}:${slot.zoneCode}`
    if (zoneUnitIds.has(key)) continue
    const id = `zone:${key}`
    zoneUnitIds.set(key, id)
    units.push({
      id,
      parentId: slot.floorId,
      name: slot.zoneCode,
      type: "ZONE",
      maxCapacity: 0,
    })
  }

  return {
    id: projection.siteId,
    tenantId,
    code: projection.siteCode,
    name: projection.siteName,
    address: "",
    latitude: null,
    longitude: null,
    isActive: true,
    totalSlots: projection.slots.length,
    floors: projection.floors.length,
    configuredBackupCount: projection.slots.filter(
      (slot) => slot.reservationState === "BACKUP",
    ).length,
    freshness: {
      generatedAt: projection.generatedAt,
      isFresh: projection.isFresh,
      stalenessThresholdSeconds: projection.stalenessThresholdSeconds,
    },
    units,
    slots: projection.slots.map((slot) => {
      const floor = floorById.get(slot.floorId)
      const zoneId = slot.zoneCode
        ? zoneUnitIds.get(`${slot.floorId}:${slot.zoneCode}`)
        : undefined

      return {
        id: slot.slotId,
        unitId: zoneId ?? slot.floorId,
        number: slot.slotCode,
        floor: floor?.level ?? floorIndex.get(slot.floorId) ?? 1,
        status: derivedSlotStatus(slot.physicalState, slot.reservationState),
        vehicleType: slot.supportedVehicleType,
        operationalStatus: LEGACY_OPERATIONAL_STATUS[slot.physicalState] || "UNKNOWN",
        isPhysicallyOccupied: slot.physicalState === "OCCUPIED",
        reservationState: slot.reservationState,
        physicalState: slot.physicalState,
        isBookable: isSlotBookable(slot),
      }
    }),
  }
}

export async function loadOperatorSites(user: User | null): Promise<OperatorSite[]> {
  if (!user) return []

  if (user.role === "owner") {
    const sites = await parkingApi.list()
    const siteById = new Map(sites.map((site) => [site.id, site]))
    const projections = await Promise.all(
      sites.map((site) => parkingApi.operationalLayout(site.id)),
    )
    return Promise.all(projections.map(async (projection, index) => {
      const siteMeta = siteById.get(sites[index].id)
      const site = siteFromOperationalLayout(projection, siteMeta?.tenantId ?? "")
      const merged: OperatorSite = {
        ...site,
        address: siteMeta?.address ?? site.address,
        latitude: siteMeta?.latitude ?? site.latitude,
        longitude: siteMeta?.longitude ?? site.longitude,
        isActive: siteMeta?.isActive ?? site.isActive,
      }
      const savedModelUrl = await loadOwnerModelUrl(site.id).catch(() => null)
      return savedModelUrl ? { ...merged, modelUrl: savedModelUrl } : merged
    }))
  }

  const assignments = await operatorsApi.assignments()
  const projections = await Promise.all(
    assignments.map((assignment) => parkingApi.operationalLayout(assignment.siteId)),
  )
  return Promise.all(projections.map(async (projection, index) => {
    const site = siteFromOperationalLayout(
      projection,
      assignments[index]?.tenantId ?? "",
    )
    const savedModelUrl = await loadOwnerModelUrl(site.id).catch(() => null)
    return savedModelUrl ? { ...site, modelUrl: savedModelUrl } : site
  }))
}

function toBooking(reservation: GateReservation): Booking {
  const paymentMethod = mapPaymentMethod(reservation.paymentMethod)
  return {
    id: reservation.id,
    driverId: reservation.accountId,
    lotId: reservation.siteId,
    lotName: reservation.siteName,
    slotId: reservation.slotId ?? "",
    slotNumber: reservation.slotNumber,
    licensePlate: reservation.licensePlate,
    startTime: reservation.startTime,
    endTime: reservation.endTime,
    status: reservation.status,
    paymentStatus: reservation.paymentStatus ?? undefined,
    completedAt: reservation.exitTime ?? undefined,
    entryImageUrl: reservation.entryImageUrl,
    exitImageUrl: reservation.exitImageUrl,
    paymentMethod,
    amount: reservation.amount ?? 0,
    createdAt: reservation.createdAt,
  }
}

const PAYMENT_METHOD_MAP: Record<string, PaymentMethod> = {
  VNPAY: "vnpay",
  MOMO: "momo",
  ZALOPAY: "zalopay",
  VISA: "visa",
  CREDIT_CARD: "visa",
  APPLEPAY: "applepay",
  APPLE_PAY: "applepay",
  QR: "qr",
}

function mapPaymentMethod(method: string | null): PaymentMethod | undefined {
  if (!method) return undefined
  return PAYMENT_METHOD_MAP[method.toUpperCase()]
}

async function reservationRequest<T>(
  path: string,
  method = "GET",
  body?: unknown,
): Promise<T> {
  const result = await request<Response<T>>(
    path,
    method,
    body,
    "/api/reservations",
    reservationServer,
  )
  if (!result.success) throw new Error("Reservation request failed.")
  return result.data
}

async function parkingSessionRequest<T>(
  path: string,
  method = "GET",
  body?: unknown,
): Promise<T> {
  const result = await request<Response<T>>(
    path,
    method,
    body,
    "/api/parking-sessions",
    reservationServer,
  )
  if (!result.success) throw new Error("Parking session request failed.")
  return result.data
}

async function operationsRequest<T>(
  siteId: string,
  path: string,
  method = "GET",
  body?: unknown,
): Promise<T> {
  const result = await request<Response<T>>(
    path,
    method,
    body,
    `/api/operator/sites/${siteId}`,
    parkingServer,
  )
  if (!result.success) throw new Error("Operator request failed.")
  return result.data
}

export const reservationApi = {
  async list(siteId: string) {
    const items = await reservationRequest<GateReservation[]>(`?siteId=${encodeURIComponent(siteId)}`)
    return items.map(toBooking)
  },

  listParkingSessions(siteId: string) {
    return parkingSessionRequest<ParkingSessionHistoryEntry[]>(
      `?siteId=${encodeURIComponent(siteId)}`,
    )
  },

  async lookup(siteId: string, query: string) {
    const item = await reservationRequest<GateReservation>(
      `/lookup?siteId=${encodeURIComponent(siteId)}&query=${encodeURIComponent(query)}`,
    )
    return toBooking(item)
  },

  async checkIn(siteId: string, reservationId: string, plateNumber?: string, imageData?: string) {
    const item = await reservationRequest<GateReservation>(
      `/${encodeURIComponent(reservationId)}/check-in?siteId=${encodeURIComponent(siteId)}`,
      "POST",
      { plateNumber, imageData },
    )
    return toBooking(item)
  },

  async checkOut(siteId: string, reservationId: string, plateNumber?: string, imageData?: string) {
    const item = await reservationRequest<GateReservation>(
      `/${encodeURIComponent(reservationId)}/check-out?siteId=${encodeURIComponent(siteId)}`,
      "POST",
      { plateNumber, imageData },
    )
    return toBooking(item)
  },

  walkInLookup(siteId: string, plate: string) {
    return parkingSessionRequest<WalkInParkingSession>(
      `/walk-in/lookup?siteId=${encodeURIComponent(siteId)}&plate=${encodeURIComponent(plate)}`,
    )
  },

  walkInCheckIn(siteId: string, plateNumber: string, imageData?: string, vehicleType?: string) {
    return parkingSessionRequest<WalkInParkingSession>(
      `/walk-in/check-in?siteId=${encodeURIComponent(siteId)}`,
      "POST",
      { plateNumber, imageData, vehicleType },
    )
  },

  walkInCheckOut(siteId: string, sessionId: string, plateNumber: string, imageData?: string) {
    return parkingSessionRequest<WalkInParkingSession>(
      `/${encodeURIComponent(sessionId)}/walk-in/check-out?siteId=${encodeURIComponent(siteId)}`,
      "POST",
      { plateNumber, imageData },
    )
  },
}

export const operatorOperationsApi = {
  listTickets: (siteId: string) =>
    operationsRequest<SupportTicket[]>(siteId, "/tickets"),

  createTicket: (siteId: string, body: { subject: string; message: string; bookingId?: string }) =>
    operationsRequest<SupportTicket>(siteId, "/tickets", "POST", body),

  updateTicket: (
    siteId: string,
    ticketId: string,
    status: SupportTicket["status"],
    resolutionNote?: string,
  ) =>
    operationsRequest<SupportTicket>(
      siteId,
      `/tickets/${encodeURIComponent(ticketId)}`,
      "PATCH",
      { status, resolutionNote },
    ),

  listIncidents: (siteId: string) =>
    operationsRequest<OperatorIncident[]>(siteId, "/incidents"),

  createIncident: (siteId: string, body: { type: string; details: string }) =>
    operationsRequest<OperatorIncident>(siteId, "/incidents", "POST", body),

  resolveIncident: (siteId: string, incidentId: string) =>
    operationsRequest<OperatorIncident>(
      siteId,
      `/incidents/${encodeURIComponent(incidentId)}`,
      "PATCH",
    ),

  listAuditEvents: (siteId: string) =>
    operationsRequest<OperatorAuditEvent[]>(siteId, "/audit"),

  recordAuditEvent: (siteId: string, body: { action: string; details: string }) =>
    operationsRequest<void>(siteId, "/audit", "POST", body),

  async listNotifications(siteId: string) {
    const items = await operationsRequest<Array<{
      id: string
      recipientId: string
      type: AppNotification["type"]
      title: string
      message: string
      timestamp: string
      isRead: boolean
      relatedEntityId?: string | null
    }>>(siteId, "/notifications")
    return items.map((item): AppNotification => ({
      id: item.id,
      recipientId: item.recipientId,
      recipientRole: "operator",
      type: item.type,
      title: item.title,
      message: item.message,
      timestamp: item.timestamp,
      isRead: item.isRead,
      relatedEntityId: item.relatedEntityId ?? undefined,
    }))
  },

  markNotificationRead: (siteId: string, notificationId: string) =>
    operationsRequest<void>(
      siteId,
      `/notifications/${encodeURIComponent(notificationId)}/read`,
      "PATCH",
    ),
}
