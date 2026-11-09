import { request } from "./authApi"

export interface DriverParkingSession {
  id: string
  reservationId?: string | null
  tenantId: string
  siteId: string
  accessItemId?: string | null
  accountId?: string | null
  currentSlotId?: string | null
  slotNumber?: string | null
  originalPlate?: string | null
  normalizedPlate?: string | null
  vehicleType: string
  ticketReference?: string | null
  entryTime: string
  entryImageUrl?: string | null
  exitTime?: string | null
  exitImageUrl?: string | null
  status: string
  version: number
  createdAt: string
}

type ResponseEnvelope<T> = {
  success: boolean
  data: T
  message?: string
}

const reservationServer = (
  import.meta.env.VITE_RESERVATION_API_BASE_URL ?? "http://localhost:5055"
).replace(/\/$/, "")

export const driverApi = {
  async getParkingHistory(options?: {
    status?: "ALL" | "ACTIVE" | "COMPLETED"
    limit?: number
  }): Promise<DriverParkingSession[]> {
    const params = new URLSearchParams()
    if (options?.status && options.status !== "ALL") {
      params.append("status", options.status)
    }
    if (options?.limit) {
      params.append("limit", options.limit.toString())
    }

    const query = params.toString() ? `?${params.toString()}` : ""
    const response = await request<ResponseEnvelope<DriverParkingSession[]>>(
      query,
      "GET",
      undefined,
      "/api/parking-sessions/my-history",
      reservationServer,
    )

    if (!response.success || !Array.isArray(response.data)) {
      throw new Error(response.message || "Failed to load driver parking history.")
    }

    return response.data
  },
}

