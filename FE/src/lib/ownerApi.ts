import { request } from "./authApi"

import type { Device, LotType } from "./types"

export interface OwnerLotSetup {
  name: string
  address: string
  lotType: LotType
  totalSlots: number
  floors: number
  devices: Device[]
  modelFileName: string
  hourlyRate: number
  dailyRate: number
  nightRate: number
  gracePeriodMinutes: number
  subscriptionMonthly: number
  subscriptionYearly: number
  policyAccepted: boolean
}

export interface OwnerApplicationRecord {
  id: string
  ownerId: string
  ownerName: string
  businessName: string

  email: string
  phone: string
  lotType: LotType

  status: "pending" | "approved" | "rejected"
  submittedAt: string

  reviewedAt?: string
  reviewedBy?: string
  reviewNote?: string

  contactVerified: boolean

  lotSetupComplete?: boolean
  lotSetup?: OwnerLotSetup | null
  setupToken?: string

  verification?: { challengeId: string; expiresAt: string; resendAt: string }

  deliveryId?: string
  deliveryStatus?: string
}

interface Response<T> {
  success: boolean
  data: T
}

export const ownerApi = {
  async register(body: {
    fullName: string
    businessName: string
    email: string
    phone: string

    password?: string
    lotType: LotType
    agreedToPolicy: boolean
  }): Promise<OwnerApplicationRecord> {
    const r = await request<Response<OwnerApplicationRecord>>(
      "/register/owner",
      "POST",
      body,
    )

    if (!r.success || !r.data?.id)
      throw new Error("Invalid registration response.")

    return r.data
  },

  async submitLotSetup(
    registrationId: string,
    setupToken: string,
    lot: OwnerLotSetup,
  ): Promise<OwnerApplicationRecord> {
    if (!registrationId || !setupToken) {
      throw new Error("Parking lot setup is unavailable. Please contact support.")
    }

    const r = await request<Response<OwnerApplicationRecord>>(
      "/register/owner/lot-setup",
      "POST",
      { registrationId, setupToken, lot },
    )

    if (!r.success || r.data?.id !== registrationId ||
        !r.data.lotSetupComplete || !r.data.verification?.challengeId) {
      throw new Error("The server did not confirm the parking lot setup.")
    }

    return r.data
  },

  async list(): Promise<OwnerApplicationRecord[]> {
    const r = await request<Response<OwnerApplicationRecord[]>>(
      "",
      "GET",
      undefined,
      "/api/owner-applications",
    )

    if (!r.success || !Array.isArray(r.data))
      throw new Error("Invalid application response.")

    return r.data
  },

  async review(
    id: string,
    status: "approved" | "rejected",
    reviewNote: string,
  ): Promise<OwnerApplicationRecord> {
    const r = await request<Response<OwnerApplicationRecord>>(
      `/${encodeURIComponent(id)}/review`,
      "PATCH",

      { status, reviewNote },
      "/api/owner-applications",
    )

    if (!r.success || !r.data?.id) throw new Error("Invalid review response.")

    return r.data
  },
}
