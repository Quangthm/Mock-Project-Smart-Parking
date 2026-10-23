import { request } from "./authApi"
import {
  operatorsApi,
  parkingApi,
  type Structure,
} from "./parkingApi"
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
  operationalStatus: string
  isPhysicallyOccupied: boolean
  reservationState: string | null
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
    address: structure.site.address,
    latitude: structure.site.latitude,
    longitude: structure.site.longitude,
    isActive: structure.site.isActive,
    totalSlots: structure.slots.length,
    floors: floorUnits.length,
    configuredBackupCount: structure.capacityViews.find((view) => view.unitId === null)?.configuredBackup ?? 0,
    units: structure.units,
    slots: structure.slots.map((slot) => {
      const operational = slot.operationalStatus.toUpperCase()
      const reservation = slot.reservationState?.toUpperCase() ?? ""
      const status: SlotStatus = slot.isPhysicallyOccupied
        ? "occupied"
        : operational === "MAINTENANCE"
          ? "maintenance"
          : operational !== "OPERATIONAL"
            ? "disabled"
            : reservation !== "" && reservation !== "NONE"
              ? "reserved"
              : "available"

      return {
        id: slot.id,
        unitId: slot.unitId,
        number: slot.code,
        floor: floorFor(slot.unitId),
        status,
        vehicleType: slot.vehicleType,
        operationalStatus: slot.operationalStatus,
        isPhysicallyOccupied: slot.isPhysicallyOccupied,
        reservationState: slot.reservationState,
      }
    }),
  }
}

export async function loadOperatorSites(user: User | null): Promise<OperatorSite[]> {
  if (!user) return []

  if (user.role === "owner") {
    const sites = await parkingApi.list()
    const structures = await Promise.all(sites.map((site) => parkingApi.structure(site.id)))
    return structures.map(siteFromStructure)
  }

  const assignments = await operatorsApi.assignments()
  const structures = await Promise.all(
    assignments.map((assignment) => parkingApi.operatorLayout(assignment.siteId)),
  )
  return structures.map(siteFromStructure)
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

function mapPaymentMethod(method: string | null): PaymentMethod | undefined {
  switch (method?.toUpperCase()) {
    case "VNPAY":
      return "vnpay"
    case "MOMO":
      return "momo"
    case "ZALOPAY":
      return "zalopay"
    case "VISA":
    case "CREDIT_CARD":
      return "visa"
    case "APPLEPAY":
    case "APPLE_PAY":
      return "applepay"
    case "QR":
      return "qr"
    default:
      return undefined
  }
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
