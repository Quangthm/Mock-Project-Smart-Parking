import { ensureAccessToken, request } from "./authApi"

const PAYMENT_PREFIX = "/api/v1/admin/integrations/payment"

const NOTIFICATION_PREFIX = "/api/v1/admin/integrations/notifications"

export type PaymentMode = "LIVE" | "SANDBOX" | "FIXTURE"

export type PaymentStatus = "HEALTHY" | "MISCONFIGURED" | "DEGRADED" | "UNTESTED"

export interface PaymentCapabilities {
  supportsVietQr: boolean
  supportsWebhook: boolean
  supportsDirectRefund: boolean
}

export interface PaymentIntegrationDto {
  provider: string
  mode: PaymentMode
  baseUrl: string
  isConfigured: boolean
  clientIdMasked: string | null
  apiKeyConfigured: boolean
  checksumKeyConfigured: boolean
  status: PaymentStatus
  lastTestedAt: string | null
  capabilities: PaymentCapabilities
}

export interface UpdatePaymentIntegrationRequest {
  mode: PaymentMode
  baseUrl: string
  clientId?: string | null
  apiKey?: string | null
  checksumKey?: string | null
}

export type UpdatePaymentIntegrationResult = Omit<PaymentIntegrationDto, "capabilities">

export interface NotificationChannelItem {
  channel: string
  provider: string
  isReady: boolean
  status: string
  description: string
  lastTestedAt: string | null
}

export interface NotificationIntegrationsDto {
  channels: NotificationChannelItem[]
}

export const PAYMENT_BASE_URL_ALLOWLIST: Record<PaymentMode, string[]> = {
  LIVE: ["https://api-merchant.payos.vn"],
  SANDBOX: ["https://api-merchant.payos.vn"],
  FIXTURE: ["http://localhost:5173", "https://fixture.internal"],
}

interface Response<T> {
  success: boolean
  data: T
  message?: string
}

const paymentServer = () =>
  (import.meta.env.VITE_PAYMENT_API_BASE_URL ?? "http://localhost:5065").replace(/\/$/, "")

const notificationServer = () =>
  (import.meta.env.VITE_NOTIFICATION_API_BASE_URL ?? "http://localhost:5068").replace(/\/$/, "")

export async function getPaymentIntegration(): Promise<PaymentIntegrationDto> {
  await ensureAccessToken()
  const response = await request<Response<PaymentIntegrationDto>>(
    "",
    "GET",
    undefined,
    PAYMENT_PREFIX,
    paymentServer(),
  )
  if (!response.success || !response.data?.provider)
    throw new Error("Invalid payment integration response.")
  return response.data
}

export async function updatePaymentIntegration(
  body: UpdatePaymentIntegrationRequest,
): Promise<{ message: string; data: UpdatePaymentIntegrationResult }> {
  await ensureAccessToken()
  const response = await request<Response<UpdatePaymentIntegrationResult>>(
    "",
    "PUT",
    body,
    PAYMENT_PREFIX,
    paymentServer(),
  )
  if (!response.success || !response.data?.provider)
    throw new Error("Invalid payment integration update response.")
  return { message: response.message ?? "", data: response.data }
}

export async function getNotificationIntegrations(): Promise<NotificationIntegrationsDto> {
  await ensureAccessToken()
  const response = await request<Response<NotificationIntegrationsDto>>(
    "",
    "GET",
    undefined,
    NOTIFICATION_PREFIX,
    notificationServer(),
  )
  if (!response.success || !response.data || !Array.isArray(response.data.channels))
    throw new Error("Invalid notification integrations response.")
  return response.data
}
