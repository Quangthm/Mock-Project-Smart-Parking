import { request, AuthApiError } from "./authApi"

import type { Device, LotType } from "./types"
import { ownerRegistrationStage, validOwnerChallenge, type PendingOwnerRegistration } from "./ownerRegistration"

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

    if (!r.success || !r.data?.id || typeof r.data.email !== "string" ||
        r.data.email.trim().toLowerCase() !== body.email.trim().toLowerCase())
      throw new Error("Invalid registration response.")

    if (ownerRegistrationStage(r.data) !== "setup")
      throw new Error("The registration server did not return a valid setup credential. Please contact support.")

    return r.data
  },

  async recover(email: string): Promise<PendingOwnerRegistration> {
    try {
      const r = await request<Response<PendingOwnerRegistration>>(
        "/register/owner/recover", "POST", { contact: email },
      )
      if (!r.success || !r.data || ownerRegistrationStage(r.data) !== "verification")
        throw new Error("The server did not return a saved parking lot and valid verification challenge.")
      return { ...r.data, email }
    } catch (error) {
      if (error instanceof AuthApiError && error.code === "SETUP_RECOVERY_REQUIRED")
        return ownerApi.requestSetupRecovery(email)
      throw error
    }
  },

  async requestSetupRecovery(email: string): Promise<PendingOwnerRegistration> {
    const r = await request<Response<NonNullable<OwnerApplicationRecord["verification"]>>>(
      "/register/owner/setup-recovery/request", "POST", { contact: email },
    )
    if (!r.success || !validOwnerChallenge(r.data)) throw new Error("Invalid setup recovery challenge.")
    return { email, lotSetupComplete: false, setupRecovery: r.data }
  },

  async completeSetupRecovery(email: string, challengeId: string, code: string): Promise<PendingOwnerRegistration> {
    const r = await request<Response<OwnerApplicationRecord>>(
      "/register/owner/setup-recovery/complete", "POST", { challengeId, code },
    )
    if (!r.success || !r.data || ownerRegistrationStage(r.data) !== "setup" ||
        r.data.email?.toLowerCase() !== email.trim().toLowerCase())
      throw new Error("The server did not return a valid setup credential.")
    return r.data
  },

  async submitLotSetup(
    registrationId: string,
    setupToken: string,
    lot: OwnerLotSetup,
  ): Promise<OwnerApplicationRecord> {
    if (!registrationId || !setupToken) {
      throw new Error("Your setup credential is missing. Resume registration in the original browser tab or contact support.")
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
