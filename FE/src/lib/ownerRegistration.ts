import type { OwnerApplicationRecord } from "./ownerApi"

export const pendingOwnerKey = "sp_pending_owner_registration"

export type PendingOwnerRegistration = Pick<OwnerApplicationRecord, "email"> &
  Partial<Pick<OwnerApplicationRecord, "id" | "setupToken" | "lotType" | "lotSetupComplete" | "verification">> & {
    setupRecovery?: OwnerApplicationRecord["verification"]
    emailVerified?: boolean
  }

export function validOwnerChallenge(value: OwnerApplicationRecord["verification"]): boolean {
  return !!value && typeof value.challengeId === "string" && value.challengeId.trim().length > 0 &&
    Number.isFinite(Date.parse(value.expiresAt)) && Number.isFinite(Date.parse(value.resendAt))
}

const nonempty = (value: unknown): value is string =>
  typeof value === "string" && value.trim().length > 0

export function ensureOwnerRegistrationStorage() {
  try {
    const probeKey = `${pendingOwnerKey}_probe`
    sessionStorage.setItem(probeKey, "1")
    sessionStorage.removeItem(probeKey)
  } catch {
    throw new Error("Browser storage is unavailable. Enable session storage before continuing registration.")
  }
}

export function ownerRegistrationStage(value: PendingOwnerRegistration): "setup" | "verification" | "recovery" {
  const challenge = value.verification
  if (value.lotSetupComplete === true && nonempty(challenge?.challengeId) &&
      Number.isFinite(Date.parse(challenge!.expiresAt)) &&
      Number.isFinite(Date.parse(challenge!.resendAt))) return "verification"
  if (value.lotSetupComplete !== true && nonempty(value.id) && nonempty(value.setupToken) &&
      ["outdoor", "basement", "multi-storey"].includes(value.lotType ?? "")) return "setup"
  return "recovery"
}

export function restoreOwnerRegistration(): PendingOwnerRegistration | null {
  // Read each store independently so corrupt/unavailable session storage cannot hide a legacy record.
  for (const storage of [() => sessionStorage, () => localStorage]) {
    try {
      const value = JSON.parse(storage().getItem(pendingOwnerKey) ?? "null")
      if (value && nonempty(value.email)) return value
    } catch { /* Try the next storage. */ }
  }
  return null
}
