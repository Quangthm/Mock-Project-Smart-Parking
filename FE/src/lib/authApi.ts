import type { Role, User } from "./types"

import { store } from "./store"

const baseUrl = (
  import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5035"
).replace(/\/$/, "")

const tokenKey = "sp_access_token"

const refreshKey = "sp_refresh_token"

const expiryKey = "sp_access_expires_at"

export const authExpiredEvent = "sp-auth-expired"

interface ApiUser {
  userId: string
  fullName: string
  email: string
  role: Role
}

interface LoginSession {
  accessToken: string
  refreshToken: string
  expiresIn: number
  user: ApiUser
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

    password: "",
    accountStatus: "active",
    lockedUntil: undefined,

    createdAt: cached?.createdAt ?? new Date().toISOString(),
  }
}

function clearSession() {
  sessionStorage.removeItem(tokenKey)

  sessionStorage.removeItem(refreshKey)

  sessionStorage.removeItem(expiryKey)
}

export async function request<T>(
  path: string,
  method = "GET",
  body?: unknown,
  apiPrefix = "/api/auth",
  serverUrl = baseUrl,
): Promise<T> {
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

  if (!response.ok) {
    if (
      response.status === 401 &&
      !["/login", "/logout", "/otp/login", "/otp/request"].includes(path) &&
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

    throw new AuthApiError(message, response.status)
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
  ): Promise<User> {
    const response = await request<ApiResponse<LoginSession>>(
      "/otp/login",
      "POST",
      { challengeId, code, ...(totp ? { totp } : {}) },
    )

    if (
      !response.success ||
      !response.data?.accessToken ||
      !response.data.refreshToken
    )
      throw new Error("Invalid sign-in response.")

    const user = toUser(response.data.user)

    sessionStorage.setItem(tokenKey, response.data.accessToken)
    sessionStorage.setItem(refreshKey, response.data.refreshToken)

    sessionStorage.setItem(
      expiryKey,
      String(Date.now() + response.data.expiresIn * 1000),
    )
    return user
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

  async login(email: string, password: string): Promise<User> {
    const response = await request<ApiResponse<LoginSession>>(
      "/login",
      "POST",
      { email: email.trim(), password },
    )

    if (
      !response.success ||
      !response.data?.accessToken ||
      !response.data.refreshToken
    )
      throw new Error("Invalid login response.")

    const user = toUser(response.data.user)

    sessionStorage.setItem(tokenKey, response.data.accessToken)

    sessionStorage.setItem(refreshKey, response.data.refreshToken)

    sessionStorage.setItem(
      expiryKey,
      String(Date.now() + response.data.expiresIn * 1000),
    )

    return user
  },

  async currentUser(): Promise<User | null> {
    if (!sessionStorage.getItem(tokenKey)) return null

    const token = sessionStorage.getItem(tokenKey)

    const response = await request<ApiResponse<ApiUser>>("/me")

    // A response from a previous session must not restore it after logout or a new login.

    if (token !== sessionStorage.getItem(tokenKey)) return null

    return toUser(response.data)
  },

  async logout(): Promise<void> {
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

          await request<ApiResponse<ApiUser>>("/me")
        } catch {
          // Preserve the original logout error. A /me 401 already dispatches authExpiredEvent.
        }
      }

      throw error
    }

    if (token === sessionStorage.getItem(tokenKey)) clearSession()
  },
}
