import type { Role, User } from "./types"

import { store } from "./store"

const baseUrl = (
  import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5035"
).replace(/\/$/, "")

const tokenKey = "sp_access_token"

const refreshKey = "sp_refresh_token"

const expiryKey = "sp_access_expires_at"

export const authExpiredEvent = "sp-auth-expired"
export const authRefreshedEvent = "sp-auth-refreshed"
let refreshInFlight: Promise<void> | null = null
let logoutInProgress = false
// A refresh rotates credentials within a login; login/logout changes its identity.
let sessionGeneration = 0

function requireSessionGeneration(expected: number) {
  if (sessionGeneration !== expected) throw new Error("Your sign-in changed. Please try again.")
}

interface ApiUser {
  userId: string
  fullName: string
  email: string
  role: Role
  onboardingComplete?: boolean
}

interface LoginSession {
  accessToken: string
  refreshToken: string
  expiresIn: number
  user: ApiUser
  recoveryCodesRemaining?: number | null
}

export interface MfaLoginChallenge {
  challengeId: string
  challengeToken: string
  expiresAt: string
}

export type PasswordLoginResult = User | { mfaRequired: true; challenge: MfaLoginChallenge }

export interface MfaVerification {
  password: string
  totp?: string
  recoveryCode?: string
}

export interface MfaStatus {
  enabled: boolean
  recoveryCodesRemaining: number
}

export interface MfaSetup {
  secret: string
  uri: string
  expiresAt: string
}

export interface MfaRecoveryCodes {
  recoveryCodes: string[]
  signInRequired: boolean
}

interface ApiResponse<T> {
  success: boolean
  message?: string
  data: T
  errors?: Array<{ message: string }>
}

export interface DriverRegistration {
  registrationId: string
  channel: "email" | "sms"
  expiresAt: string
  resendAvailableAt: string
}

export class AuthApiError extends Error {
  constructor(
    message: string,
    public status: number,
    public code?: string,
  ) {
    super(message)
  }
}

function toUser(account: ApiUser): User {
  if (!["driver", "owner", "operator", "admin"].includes(account.role)) {
    throw new Error("The server returned an unsupported account role.")
  }

  const cached = store.findUserById(account.userId)

  // Local data is for UI fixtures only. Identity, role and status come from the backend.

  return {
    ...cached,

    id: account.userId,
    email: account.email,
    name: account.fullName,
    role: account.role,
    onboardingComplete: account.onboardingComplete ?? cached?.onboardingComplete,

    password: "",
    accountStatus: "active",
    lockedUntil: undefined,

    createdAt: cached?.createdAt ?? new Date().toISOString(),
  }
}

function clearSession() {
  sessionGeneration++
  sessionStorage.removeItem(tokenKey)

  sessionStorage.removeItem(refreshKey)

  sessionStorage.removeItem(expiryKey)
  sessionStorage.removeItem("sp_mfa_recovery_remaining")
}

function saveLoginSession(response: ApiResponse<LoginSession>, isRefresh = false): User {
  if (!response.success || !response.data?.accessToken || !response.data.refreshToken)
    throw new Error("Invalid sign-in response.")

  const user = toUser(response.data.user)
  if (!isRefresh) sessionGeneration++
  sessionStorage.setItem(tokenKey, response.data.accessToken)
  sessionStorage.setItem(refreshKey, response.data.refreshToken)
  sessionStorage.setItem(expiryKey, String(Date.now() + response.data.expiresIn * 1000))
  if (response.data.recoveryCodesRemaining != null)
    sessionStorage.setItem("sp_mfa_recovery_remaining", String(response.data.recoveryCodesRemaining))
  else
    sessionStorage.removeItem("sp_mfa_recovery_remaining")
  return user
}

function anonymousPath(path: string, prefix: string) {
  return prefix === "/api/auth" && (["/login", "/refresh", "/otp/login", "/otp/request", "/mfa/login", "/bootstrap-password"].includes(path) || path.startsWith("/register/"))
}

export async function ensureAccessToken(): Promise<string> {
  const token = sessionStorage.getItem(tokenKey)
  if (!token) return ""
  if (logoutInProgress) throw new Error("Sign out is in progress.")
  if (Number(sessionStorage.getItem(expiryKey) ?? 0) > Date.now() + 30_000 && !refreshInFlight) return token
  if (!refreshInFlight) {
    const originalRefresh = sessionStorage.getItem(refreshKey)
    refreshInFlight = (async () => {
      if (!originalRefresh) {
        clearSession()
        window.dispatchEvent(new Event(authExpiredEvent))
        throw new AuthApiError("Please sign in again.", 401, "INVALID_TOKEN")
      }
      let response: Response
      try {
        response = await fetch(baseUrl + "/api/auth/refresh", {
          method: "POST", cache: "no-store", signal: AbortSignal.timeout(10_000),
          headers: { "Content-Type": "application/json" }, body: JSON.stringify({ refreshToken: originalRefresh }),
        })
      } catch { throw new Error("Cannot refresh your session. Please try again.") }
      const payload = await response.json().catch(() => null)
      // An old in-flight response must never restore or erase a different login/logout.
      if (sessionStorage.getItem(refreshKey) !== originalRefresh || sessionStorage.getItem(tokenKey) !== token) return
      if (!response.ok) {
        if (response.status === 401) {
          clearSession()
          window.dispatchEvent(new Event(authExpiredEvent))
        }
        throw new AuthApiError(payload?.message ?? "Cannot refresh your session.", response.status, payload?.code)
      }
      saveLoginSession(payload, true)
      window.dispatchEvent(new Event(authRefreshedEvent))
    })().finally(() => { refreshInFlight = null })
  }
  await refreshInFlight
  return sessionStorage.getItem(tokenKey) ?? ""
}

export async function request<T>(
  path: string,
  method = "GET",
  body?: unknown,
  apiPrefix = "/api/auth",
  serverUrl = baseUrl,
): Promise<T> {
  const generation = sessionGeneration
  if (!anonymousPath(path, apiPrefix) && path !== "/logout") {
    await ensureAccessToken()
    requireSessionGeneration(generation)
  }
  const token = sessionStorage.getItem(tokenKey)

  let response: Response

  try {
    response = await fetch(serverUrl + apiPrefix + path, {
      method,
      cache: "no-store",
      signal: AbortSignal.timeout(10_000),

      headers: {
        ...(body !== undefined ? { "Content-Type": "application/json" } : {}),

        ...(token ? { Authorization: "Bearer " + token } : {}),
      },

      ...(body !== undefined ? { body: JSON.stringify(body) } : {}),
    })
  } catch {
    throw new Error("Cannot reach the authentication server. Please try again.")
  }

  const payload =
    response.status === 204 ? null : await response.json().catch(() => null)

  // Do not let an old response update the UI or clear a replacement login.
  const issuesSession = apiPrefix === "/api/auth" && ["/login", "/otp/login", "/mfa/login"].includes(path)
  if (!anonymousPath(path, apiPrefix) || issuesSession) requireSessionGeneration(generation)

  if (!response.ok) {
    const isMfaManagement = ["/mfa/setup", "/mfa/enable", "/mfa/disable", "/mfa/recovery-codes"].includes(path)
    const verificationFailed = isMfaManagement && ["AUTH_FAILED", "MFA_INVALID", "MFA_REQUIRED"].includes(payload?.code)
    if (
      response.status === 401 &&
      token &&
      !anonymousPath(path, apiPrefix) && path !== "/logout" &&
      !verificationFailed &&
      token === sessionStorage.getItem(tokenKey)
    ) {
      clearSession()

      window.dispatchEvent(new Event(authExpiredEvent))
    }

    const errors = payload?.errors

    const validationMessage = Array.isArray(errors)
      ? errors.map((error: { message: string }) => error.message).join(" ")
      : errors && typeof errors === "object"
        ? Object.values(errors).flat().join(" ")
        : ""

    const message =
      validationMessage ||
      payload?.message ||
      (response.status === 401
        ? "Your session has expired. Please sign in again."
        : "Authentication request failed.")

    throw new AuthApiError(message, response.status, payload?.code)
  }

  return payload as T
}

export const authApi = {
  async requestOtp(contact: string) {
    return (
      await request<ApiResponse<{
        challengeId: string
        expiresAt: string
        resendAt: string
      }>>("/otp/request", "POST", { contact })
    ).data
  },

  async loginOtp(
    challengeId: string,
    code: string,
    totp?: string,
    recoveryCode?: string,
  ): Promise<User> {
    const response = await request<ApiResponse<LoginSession>>(
      "/otp/login",
      "POST",
      { challengeId, code, ...(totp ? { totp } : {}), ...(recoveryCode ? { recoveryCode } : {}) },
    )

    return saveLoginSession(response)
  },

  async registerDriver(body: {
    fullName: string
    email?: string
    phone?: string
    password?: string
  }): Promise<DriverRegistration> {
    const response = await request<ApiResponse<DriverRegistration>>(
      "/register/driver",
      "POST",
      body,
    )

    if (!response.success || !response.data?.registrationId)
      throw new Error("Invalid registration response.")

    return response.data
  },

  async recoverDriver(contact: string): Promise<DriverRegistration> {
    const response = await request<ApiResponse<DriverRegistration>>(
      "/register/driver/recover",
      "POST",
      { contact },
    )

    if (!response.success || !response.data?.registrationId)
      throw new Error("Pending registration was not found.")

    return response.data
  },

  async verifyDriver(registrationId: string, code: string): Promise<void> {
    const response = await request<ApiResponse<never>>(
      "/register/driver/verify",
      "POST",
      { registrationId, code },
    )

    if (!response.success) throw new Error("Verification failed.")
  },

  async resendDriver(registrationId: string): Promise<DriverRegistration> {
    const response = await request<ApiResponse<DriverRegistration>>(
      "/register/driver/resend",
      "POST",
      { registrationId },
    )

    if (!response.success || !response.data?.registrationId)
      throw new Error("Invalid registration response.")

    return response.data
  },

  clearSession,

  expiresAt: () => Number(sessionStorage.getItem(expiryKey) ?? 0),

  async login(email: string, password: string): Promise<PasswordLoginResult> {
    const response = await request<ApiResponse<LoginSession | MfaLoginChallenge> & { mfaRequired?: boolean }>(
      "/login",
      "POST",
      { email: email.trim(), password },
    )

    if (response.mfaRequired && response.success) {
      const challenge = response.data as MfaLoginChallenge
      if (!challenge?.challengeId || !challenge.challengeToken || !challenge.expiresAt)
        throw new Error("Invalid verification challenge.")
      return { mfaRequired: true, challenge }
    }
    return saveLoginSession(response as ApiResponse<LoginSession>)
  },

  async completeMfaLogin(challenge: MfaLoginChallenge, totp?: string, recoveryCode?: string): Promise<User> {
    const response = await request<ApiResponse<LoginSession>>("/mfa/login", "POST", {
      challengeId: challenge.challengeId,
      challengeToken: challenge.challengeToken,
      ...(totp ? { totp } : {}),
      ...(recoveryCode ? { recoveryCode } : {}),
    })
    return saveLoginSession(response)
  },

  async mfaStatus(): Promise<MfaStatus> {
    return (await request<ApiResponse<MfaStatus>>("/mfa/status")).data
  },

  async setupMfa(verification: MfaVerification): Promise<MfaSetup> {
    return (await request<ApiResponse<MfaSetup>>("/mfa/setup", "POST", verification)).data
  },

  async enableMfa(code: string): Promise<MfaRecoveryCodes> {
    return (await request<ApiResponse<MfaRecoveryCodes>>("/mfa/enable", "POST", { code })).data
  },

  async disableMfa(verification: MfaVerification): Promise<void> {
    await request("/mfa/disable", "POST", verification)
  },

  async regenerateRecoveryCodes(verification: MfaVerification): Promise<MfaRecoveryCodes> {
    return (await request<ApiResponse<MfaRecoveryCodes>>("/mfa/recovery-codes", "POST", verification)).data
  },

  async currentUser(): Promise<User | null> {
    if (!sessionStorage.getItem(tokenKey)) return null
    const generation = sessionGeneration
    await ensureAccessToken()
    requireSessionGeneration(generation)
    const token = sessionStorage.getItem(tokenKey)

    const response = await request<ApiResponse<ApiUser>>("/me")

    // A response from a previous session must not restore it after logout or a new login.

    if (token !== sessionStorage.getItem(tokenKey)) return null

    return toUser(response.data)
  },

  async logout(): Promise<void> {
    const generation = sessionGeneration
    if (refreshInFlight) await refreshInFlight
    requireSessionGeneration(generation)
    // Refresh an expired access token before the authenticated logout request.
    await ensureAccessToken()
    requireSessionGeneration(generation)
    logoutInProgress = true
    const token = sessionStorage.getItem(tokenKey)

    try {
      const response = await request<ApiResponse<never>>("/logout", "POST", {
        refreshToken: sessionStorage.getItem(refreshKey),
      })

      if (!response?.success)
        throw new Error("Sign out failed. Please try again.")
    } catch (error) {
      // A logout 401 may mean a mismatched refresh token while the bearer is still valid.

      if (
        error instanceof AuthApiError &&
        error.status === 401 &&
        token &&
        token === sessionStorage.getItem(tokenKey)
      ) {
        try {
          // Only a failed bearer check may expire the local session; a valid one stays available for retry.

          logoutInProgress = false
          await request<ApiResponse<ApiUser>>("/me")
        } catch {
          // Preserve the original logout error. A /me 401 already dispatches authExpiredEvent.
        }
      }

      logoutInProgress = false
      throw error
    }

    if (token === sessionStorage.getItem(tokenKey)) clearSession()
    logoutInProgress = false
  },
}
