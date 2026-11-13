import { request } from "./authApi"

const server = (import.meta.env.VITE_RESERVATION_API_BASE_URL ?? "http://localhost:5055").replace(/\/$/, "")
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
export interface ReservationTerms {
  termsId: string
  termsVersion: string
  validUntil: string
  commercial: ReservationPreview & { disclosure: string; prepaymentMode: string; coverageVersion: string }
  operationalPolicy: { values: Record<string, number>; arrivalLateToleranceEnabled: boolean; cancellationTimezone: string }
}
export interface ReservationReceipt {
  requestId: string
  state: string
  reservationId: string | null
  code: string | null
}
export interface OwnReservation {
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
}
async function call<T>(path: string, method = "GET", body?: unknown, headers?: Record<string, string>): Promise<T> {
  const response = await request<{ success: boolean; data: T }>(path, method, body, "/api", server, headers)
  if (!response.success || response.data == null) throw new Error("Reservation Service returned no result.")
  return response.data
}
export const reservationsApi = {
  preview: (selection: Omit<ReservationSelection, "vehicleId" | "paymentMethod"> & { vehicleId?: string; paymentMethod?: string }) =>
    call<ReservationPreview>("/reservation-price-previews", "POST", selection),
  review: (selection: ReservationSelection) => call<ReservationTerms>("/reservation-terms", "POST", selection),
  submit: (selection: ReservationSelection, terms: ReservationTerms, key: string) =>
    call<ReservationReceipt>("/reservation-requests", "POST", {
      ...selection, mode: "SPECIFIC_SLOT", termsId: terms.termsId, termsVersion: terms.termsVersion,
    }, { "Idempotency-Key": key }),
  receipt: (id: string) => call<ReservationReceipt>(`/reservation-requests/${encodeURIComponent(id)}`),
  booking: (id: string) => call<OwnReservation>(`/me/reservations/${encodeURIComponent(id)}`),
  list: () => call<OwnReservation[]>("/me/reservations?pageSize=100"),
}
