import { request } from "./authApi"

export const reservationServer = (import.meta.env.VITE_RESERVATION_API_BASE_URL ?? "http://localhost:5055").replace(/\/$/, "")
export interface ReservationSelection {
  vehicleId: string
  siteId: string
  requestedSlotId: string
  start: string
  end: string
  paymentMethod: string
}
export interface ReservationPreview {
  bookedAmount: number
  depositAmount: number | null
  currency: string
  pricingVersion: string
}
export interface ReviewedOperationalPolicy {
  values: Record<string, number>
  arrivalLateToleranceEnabled: boolean
  cancellationTimezone: string
}
export interface ReviewedReservationSelection {
  licensePlate: string
  requestedSlotId: string
  start: string
  end: string
  paymentMethod: "PAYOS"
}
export interface ReviewedReservationIntent extends ReviewedReservationSelection {
  quoteId: string
  quoteVersion: string
}
export interface ReviewedReservationQuote extends Omit<ReviewedReservationIntent, "licensePlate"> {
  canonicalPlate: string
  siteId: string
  validUntil: string
  bookedAmount: number
  coveredAmount: number
  depositAmount: number
  currency: string
  disclosure: string
  operationalPolicy: ReviewedOperationalPolicy
}
export interface ReservationReceipt {
  requestId: string
  state: string
  reservationId: string | null
  code: string | null
}
export interface OwnReservation {
  requestId: string | null
  reservationId: string
  state: string
  siteId: string
  requestedSlotId: string | null
  start: string
  end: string
  holdExpiresAt: string | null
  orderId: string | null
  orderProvisioningState: string
  orderProvisioningCode: string | null
  bookedAmount: number | null
  depositAmount: number | null
  currency: string | null
  confirmedAt: string | null
  createdAt: string
  // Nullable P0 history fields; older deployments may omit them.
  siteName?: string | null
  siteAddress?: string | null
  licensePlate?: string | null
  allocatedSlotId?: string | null
  allocationStatus?: string | null
  parkingSessionId?: string | null
  paymentStatus?: string | null
  // TODO: Driver history BE must expose authorized session evidence; P0 omits these fields.
  entryImageUrl?: string | null
  exitImageUrl?: string | null
}
export interface ReservationProgress {
  eventId?: string | null
  requestId: string
  version: number
  admissionState: string
  checks: { account: string; vehicle: string; facility: string; booking: string }
  reservationId: string | null
  reservationState: string | null
  paymentReadiness: string | null
  commercialReadiness: string | null
  outcomeCode: string | null
  orderId: string | null
  holdExpiresAt: string | null
  bookedAmount: number | null
  depositAmount: number | null
  currency: string | null
}
export interface ReservationHistoryCursor { before: string; beforeId: string }
async function call<T>(path: string, method = "GET", body?: unknown, headers?: Record<string, string>): Promise<T> {
  const response = await request<{ success: boolean; data: T }>(path, method, body, "/api", reservationServer, headers)
  if (!response.success || response.data == null) throw new Error("Reservation Service returned no result.")
  return response.data
}
export const reservationsApi = {
  reviewQuote: (selection: ReviewedReservationSelection) =>
    call<ReviewedReservationQuote>("/reservation-quotes", "POST", selection),
  submitQuote: (intent: ReviewedReservationIntent, key: string) =>
    call<ReservationReceipt>("/reservation-requests", "POST", intent, { "Idempotency-Key": key }),
  preview: (selection: Omit<ReservationSelection, "vehicleId" | "paymentMethod"> & { vehicleId?: string; paymentMethod?: string }) =>
    call<ReservationPreview>("/reservation-price-previews", "POST", selection),
  receipt: (id: string) => call<ReservationReceipt>(`/reservation-requests/${encodeURIComponent(id)}`),
  // A new connection must not reuse a GET started before its group subscription.
  progress: (id: string) => call<ReservationProgress>(`/reservation-requests/${encodeURIComponent(id)}/progress`,
    "GET", undefined, { "X-Request-ID": crypto.randomUUID() }),
  booking: (id: string) => call<OwnReservation>(`/me/reservations/${encodeURIComponent(id)}`),
  requestForOrder: (orderId: string) =>
    call<{ requestId: string | null }>(`/me/reservations/by-order/${encodeURIComponent(orderId)}/request`),
  list: () => call<OwnReservation[]>("/me/reservations?pageSize=100"),
  async historyPage(pageSize = 9, cursor?: ReservationHistoryCursor): Promise<OwnReservation[]> {
    if (!Number.isInteger(pageSize) || pageSize < 1 || pageSize > 100) throw new Error("Invalid reservation page size.")
    const query = new URLSearchParams({ pageSize: String(pageSize) })
    if (cursor) {
      if (!cursor.before || !Number.isFinite(Date.parse(cursor.before)) ||
        !/^(?!00000000-0000-0000-0000-000000000000$)[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}$/i.test(cursor.beforeId)) {
        throw new Error("Invalid reservation history cursor.")
      }
      // Preserve the server's timestamp precision; do not round-trip through Date.toISOString().
      query.set("before", cursor.before)
      query.set("beforeId", cursor.beforeId)
    }
    const items = await call<OwnReservation[]>(`/me/reservations?${query}`)
    if (!Array.isArray(items) || items.some(item => !item || typeof item.reservationId !== "string" ||
      typeof item.siteId !== "string" || typeof item.state !== "string" || !Number.isFinite(Date.parse(item.createdAt)))) {
      throw new Error("Reservation Service returned an invalid history page.")
    }
    return items
  },
}
