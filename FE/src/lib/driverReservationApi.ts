import { ApiConnectionError, AuthApiError, request } from "./authApi"

const server = (import.meta.env.VITE_RESERVATION_API_BASE_URL ?? "http://localhost:5055").replace(/\/$/, "")

export interface ReservationSelection {
  vehicleId: string
  siteId: string
  requestedSlotId: string
  start: string
  end: string
  paymentMethod: "PAYOS" | "CASH"
}

export interface ReservationTerms {
  termsId: string
  termsVersion: string
  validUntil: string
  commercial: {
    bookedAmount: number
    depositAmount: number
    currency: string
    disclosure: string
  }
}

export interface ReservationIntent extends ReservationSelection {
  mode: "SPECIFIC_SLOT"
  termsId: string
  termsVersion: string
}

export interface ReservationReceipt {
  requestId: string
  state: string
  reservationId: string | null
  code: string | null
}

export interface DriverReservation {
  reservationId: string
  siteId: string
  requestedSlotId: string | null
  state: string
  start: string
  end: string
  holdExpiresAt: string | null
  orderId: string | null
}

async function call<T>(path: string, body?: unknown, key?: string): Promise<T> {
  try {
    const response = await request<{ success: boolean; data: T }>(
      path, body === undefined ? "GET" : "POST", body, "/api", server,
      key ? { "Idempotency-Key": key } : undefined,
    )
    if (!response.success || response.data == null) throw new Error("Reservation Service returned an incomplete response.")
    return response.data
  } catch (cause) {
    if (cause instanceof ApiConnectionError) {
      throw new Error("Could not connect to the Reservation Service. Please try again later.")
    }
    if (cause instanceof AuthApiError && cause.code === "RESERVATION_ADMISSION_DISABLED") {
      throw new Error("Reservations are currently disabled. Pricing and checkout are unavailable.")
    }
    if (cause instanceof AuthApiError && cause.code === "OWNER_UNAVAILABLE") {
      throw new Error("Reservation pricing or payment is not available right now. Please try again later.")
    }
    throw cause
  }
}

export const driverReservationApi = {
  preview: (selection: Omit<ReservationSelection, "paymentMethod">) =>
    call<{ bookedAmount: number; currency: string }>("/reservation-price-previews", selection),
  review: (selection: ReservationSelection) => call<ReservationTerms>("/reservation-terms", selection),
  create: (intent: ReservationIntent, key: string) => call<ReservationReceipt>("/reservation-requests", intent, key),
  receipt: (id: string) => call<ReservationReceipt>(`/reservation-requests/${encodeURIComponent(id)}`),
  read: (id: string) => call<DriverReservation>(`/me/reservations/${encodeURIComponent(id)}`),
  list: () => call<DriverReservation[]>("/me/reservations"),
}
