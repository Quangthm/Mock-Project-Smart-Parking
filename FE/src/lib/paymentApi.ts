import { ensureAccessToken } from "./authApi"

const paymentBase = (import.meta.env.VITE_PAYMENT_API_BASE_URL ?? "http://localhost:5065").replace(/\/$/, "")
const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i
const checkoutKey = (orderId: string) => `sp_payos_checkout_${orderId}`
const idempotencyKey = (orderId: string) => `sp_payos_idempotency_${orderId}`
const checkoutStorage = typeof window === "undefined" ? null : window.localStorage

export interface CheckoutResponse {
  paymentTransactionId: string
  orderCode: number
  amount: number
  checkoutUrl: string
  qrCode: string
  accountNumber: string
  accountName: string
  bin: string
  status: string
  expiresAt: string
}

export interface PaymentStatusResponse {
  transactionId: string
  orderId: string
  orderCode: number
  amount: number
  paymentMethod: string
  status: string
  paidAt: string | null
  isAuthoritative: boolean
}

type Envelope<T> = { success: boolean; data?: T; errorCode?: string; message?: string }

export interface DriverInvoice {
  invoiceIdentity: { invoiceId: string; invoiceNumber: string; issuedAt: string }
  parkingSessionReference: { siteName: string; plateNumber: string }
  financialSummary: { finalTotalAmount: number; currency: string; surplusRefund: number }
  financialStatus: { status: string; isSettled: boolean; paymentMethod: string | null }
}

export interface DriverInvoicePage {
  items: DriverInvoice[]
  totalCount: number
  pageNumber: number
  totalPages: number
}

export function isPaymentOrderId(value: string): boolean {
  return uuidPattern.test(value.trim())
}

export function isTrustedPayOsUrl(value: string): boolean {
  try {
    const url = new URL(value)
    return (url.protocol === "https:" && url.hostname === "pay.payos.vn") ||
      (url.protocol === "http:" && url.hostname === "localhost" && url.port === "5173")
  } catch { return false }
}

export function classifyPaymentStatus(status: PaymentStatusResponse): "success" | "failed" | "pending" {
  if (status.status === "SUCCESS" && status.isAuthoritative) return "success"
  if (status.status === "FAILED" || status.status === "CANCELLED") return "failed"
  return "pending"
}

async function paymentRequest<T>(path: string, init: RequestInit): Promise<T> {
  const token = await ensureAccessToken()
  if (!token) throw new Error("Please sign in before paying.")
  let response: Response
  try {
    response = await fetch(`${paymentBase}/api/v1/payments${path}`, {
      ...init,
      cache: "no-store",
      headers: { Authorization: `Bearer ${token}`, ...init.headers },
    })
  } catch {
    throw new Error("Payment Service could not be reached. The checkout result is unknown; do not start another payment until it is checked.")
  }
  const payload = await response.json().catch(() => null) as Envelope<T> | null
  if (!response.ok || !payload?.success || !payload.data) {
    const errors: Record<string, string> = {
      ORDER_NOT_FOUND: "This payment order was not found. Check the order ID.",
      ORDER_ALREADY_PAID: "This order has already been paid.",
      ORDER_EXPIRED: "This payment order has expired.",
      PAYMENT_RETRY_LIMIT_EXCEEDED: "This order has reached its payment attempt limit.",
      GATEWAY_TIMEOUT: "The gateway timed out. The result is unknown; do not start another payment until it is checked.",
    }
    throw new Error(errors[payload?.errorCode ?? ""] ?? "Payment Service could not complete this request. Please try again later.")
  }
  return payload.data
}

async function invoiceRequest(path: string, accept: string, signal?: AbortSignal): Promise<Response> {
  const token = await ensureAccessToken()
  if (!token) throw new Error("Sign in to view your invoices.")
  const response = await fetch(`${paymentBase}/api/v1/payments/invoices${path}`, {
    headers: { Authorization: `Bearer ${token}`, Accept: accept },
    cache: "no-store",
    signal,
  })
  if (response.ok) return response
  const errors: Record<number, string> = {
    401: "Your session has expired. Sign in to view your invoices.",
    403: "You do not have access to this invoice information.",
    404: "Invoice information could not be found.",
    422: "This invoice is not available for download until payment is settled.",
  }
  throw new Error(errors[response.status] ?? "Payment Service could not load invoice information. Please try again.")
}

export const paymentApi = {
  async invoiceHistory(filters: { pageNumber: number; status: string; fromDate: string; toDate: string }, signal?: AbortSignal): Promise<DriverInvoicePage> {
    const query = new URLSearchParams({ pageNumber: String(filters.pageNumber), pageSize: "10" })
    if (filters.status) query.set("status", filters.status)
    if (filters.fromDate) query.set("fromDate", `${filters.fromDate}T00:00:00+07:00`)
    if (filters.toDate) query.set("toDate", `${filters.toDate}T23:59:59.9999999+07:00`)
    const response = await invoiceRequest(`/my-history?${query}`, "application/json", signal)
    const page = await response.json() as DriverInvoicePage
    if (!Array.isArray(page?.items) || !Number.isInteger(page.totalPages) || !Number.isInteger(page.pageNumber)) {
      throw new Error("Payment Service returned an invalid invoice list.")
    }
    return page
  },
  async downloadInvoice(invoiceId: string, signal?: AbortSignal): Promise<Blob> {
    if (!isPaymentOrderId(invoiceId)) throw new Error("Invalid invoice ID.")
    const response = await invoiceRequest(`/${invoiceId}/download`, "application/pdf", signal)
    if (!response.headers.get("content-type")?.toLowerCase().startsWith("application/pdf")) {
      throw new Error("Payment Service did not return a PDF.")
    }
    return response.blob()
  },
  async checkout(orderId: string): Promise<CheckoutResponse> {
    const normalized = orderId.trim().toLowerCase()
    if (!isPaymentOrderId(normalized)) throw new Error("Enter a valid payment order ID.")
    const key = checkoutStorage?.getItem(idempotencyKey(normalized)) ?? crypto.randomUUID()
    checkoutStorage?.setItem(idempotencyKey(normalized), key)
    const origin = window.location.origin
    const result = await paymentRequest<CheckoutResponse>(`/orders/${normalized}/checkout`, {
      method: "POST",
      headers: { "Content-Type": "application/json", "Idempotency-Key": key },
      body: JSON.stringify({
        paymentMethod: "PAYOS",
        returnUrl: `${origin}/payment/success?orderId=${normalized}`,
        cancelUrl: `${origin}/payment/fail?orderId=${normalized}`,
      }),
    })
    if (!isPaymentOrderId(result.paymentTransactionId)) throw new Error("Payment Service returned an invalid transaction ID.")
    if (!isTrustedPayOsUrl(result.checkoutUrl)) {
      throw new Error("Payment Service returned an untrusted checkout link.")
    }
    if (result.accountNumber === "0123456789" && result.accountName === "SMARTPARKING CORP") {
      throw new Error("Payment Service is using its demo gateway. Configure real payOS credentials before sending a payment.")
    }
    checkoutStorage?.setItem(checkoutKey(normalized), JSON.stringify(result))
    checkoutStorage?.setItem("sp_payos_latest_order", normalized)
    return result
  },
  status(transactionId: string): Promise<PaymentStatusResponse> {
    if (!isPaymentOrderId(transactionId)) throw new Error("Invalid payment transaction ID.")
    return paymentRequest<PaymentStatusResponse>(`/transactions/${transactionId}/status`, { method: "GET" })
  },
  rememberedCheckout(orderId: string): CheckoutResponse | null {
    if (!isPaymentOrderId(orderId)) return null
    try {
      const value = JSON.parse(checkoutStorage?.getItem(checkoutKey(orderId.toLowerCase())) ?? "null")
      return value && isPaymentOrderId(value.paymentTransactionId) && isTrustedPayOsUrl(value.checkoutUrl) ? value as CheckoutResponse : null
    } catch { return null }
  },
  resetAttempt(orderId: string): void {
    if (!isPaymentOrderId(orderId)) return
    checkoutStorage?.removeItem(idempotencyKey(orderId.toLowerCase()))
    checkoutStorage?.removeItem(checkoutKey(orderId.toLowerCase()))
  },
}
