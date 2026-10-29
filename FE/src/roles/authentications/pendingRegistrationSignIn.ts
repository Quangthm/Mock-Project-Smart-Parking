import { authApi, AuthApiError } from "../../lib/authApi"
import { ownerApi } from "../../lib/ownerApi"
import { ensureOwnerRegistrationStorage, ownerRegistrationStage, pendingOwnerKey, restoreOwnerRegistration, validOwnerChallenge } from "../../lib/ownerRegistration"
import { pendingDriverKey } from "./Sign Up/DriverOtpForm"

// Both password login and the existing email/SMS login action resume pending registrations.
export async function resumePendingSignIn(error: unknown, contact: string): Promise<"sign-up" | "parking-lot-setup" | "pending-approval" | null> {
  if (!(error instanceof AuthApiError) ||
      !["ACCOUNT_PENDING_VERIFICATION", "ACCOUNT_PENDING_APPROVAL"].includes(error.code ?? "")) return null

  ensureOwnerRegistrationStorage()
  contact = contact.trim()
  if (error.code === "ACCOUNT_PENDING_VERIFICATION") {
    try {
      const registration = await authApi.recoverDriver(contact)
      sessionStorage.setItem(pendingDriverKey, JSON.stringify({ ...registration, contact }))
      sessionStorage.removeItem(pendingOwnerKey)
      localStorage.removeItem(pendingOwnerKey)
      return "sign-up"
    } catch (driverError) {
      if (!(driverError instanceof AuthApiError) || driverError.code !== "REGISTRATION_NOT_FOUND") throw driverError
    }
  }

  const stored = restoreOwnerRegistration()
  if (error.code === "ACCOUNT_PENDING_VERIFICATION" && stored?.email.toLowerCase() === contact.toLowerCase()) {
    if (ownerRegistrationStage(stored) === "setup") {
      sessionStorage.setItem(pendingOwnerKey, JSON.stringify(stored))
      sessionStorage.removeItem(pendingDriverKey)
      return "parking-lot-setup"
    }
    if (validOwnerChallenge(stored.setupRecovery) && Date.parse(stored.setupRecovery!.expiresAt) > Date.now()) {
      sessionStorage.setItem(pendingOwnerKey, JSON.stringify(stored))
      sessionStorage.removeItem(pendingDriverKey)
      return "sign-up"
    }
  }

  let recovered
  try { recovered = await ownerApi.recover(contact) }
  catch (recoveryError) {
    // Other roles can also be pending; leave their existing sign-in status message intact.
    if (recoveryError instanceof AuthApiError && recoveryError.code === "REGISTRATION_NOT_FOUND") return null
    throw recoveryError
  }
  sessionStorage.removeItem(pendingDriverKey)
  if (recovered.lotSetupComplete === true && recovered.emailVerified === true) {
    sessionStorage.removeItem(pendingOwnerKey)
    localStorage.removeItem(pendingOwnerKey)
    return "pending-approval"
  }
  sessionStorage.setItem(pendingOwnerKey, JSON.stringify(recovered))
  return "sign-up"
}
