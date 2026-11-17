import { useCallback, useEffect, useState } from "react"

import { Badge, type BadgeTone } from "../../../components/ui/Badge"
import { Button } from "../../../components/ui/Button"
import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import {
  PAYMENT_BASE_URL_ALLOWLIST,
  getNotificationIntegrations,
  getPaymentIntegration,
  updatePaymentIntegration,
  type NotificationChannelItem,
  type PaymentIntegrationDto,
  type PaymentMode,
  type PaymentStatus,
  type UpdatePaymentIntegrationRequest,
} from "../../../lib/adminIntegrationApi"
import { AuthApiError } from "../../../lib/authApi"

const CHANNEL_ORDER = ["EMAIL", "SMS", "PUSH", "IN_APP"]

const MODE_TONE: Record<PaymentMode, BadgeTone> = {
  LIVE: "primary",
  SANDBOX: "purple",
  FIXTURE: "muted",
}

const STATUS_TONE: Record<PaymentStatus, BadgeTone> = {
  HEALTHY: "success",
  UNTESTED: "warning",
  DEGRADED: "warning",
  MISCONFIGURED: "danger",
}

const CHANNEL_STATUS_TONE: Record<string, BadgeTone> = {
  ACTIVE: "success",
  DEGRADED: "warning",
  UNAVAILABLE: "danger",
  DISABLED: "muted",
}

function formatDateTime(value: string | null | undefined): string {
  if (!value) return "Never"
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return "Never"
  return date.toLocaleString("en-GB", { dateStyle: "medium", timeStyle: "short" })
}

function describeIntegrationError(error: unknown): string {
  if (error instanceof AuthApiError) {
    if (error.code === "INVALID_PAYMENT_BASE_URL") {
      return "The base URL is not permitted for the selected mode."
    }
    if (error.code === "VALIDATION_FAILED" || error.code === "INVALID_REQUEST") {
      return "Some values are invalid. Review the form and try again."
    }
    return error.message
  }
  return error instanceof Error ? error.message : "Something went wrong. Please try again."
}

function Skeleton({ className = "" }: { className?: string }) {
  return (
    <div className={`animate-pulse rounded-lg ${className}`.trim()} style={{ background: "var(--border)" }} />
  )
}

function CardError({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <div
      role="alert"
      className="flex flex-wrap items-center gap-3 rounded-xl border p-3 text-sm"
      style={{
        borderColor: "var(--occupied)",
        background: "color-mix(in srgb, var(--occupied) 10%, var(--card))",
        color: "var(--badge-danger-strong)",
      }}
    >
      <UntitledIcon name="alert" size={18} />
      <span className="min-w-0 flex-1">{message}</span>
      <Button variant="outline" className="text-xs" onClick={onRetry}>
        Retry
      </Button>
    </div>
  )
}

function InfoRow({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-2 py-1.5" style={{ borderBottom: "1px solid var(--border)" }}>
      <span className="text-xs" style={{ color: "var(--muted)" }}>
        {label}
      </span>
      <span className="min-w-0 text-sm font-medium" style={{ color: "var(--fg)", textAlign: "right" }}>
        {children}
      </span>
    </div>
  )
}

interface PaymentForm {
  mode: PaymentMode
  baseUrl: string
  clientId: string
  apiKey: string
  checksumKey: string
}

export function IntegrationSettingsView() {
  const [payment, setPayment] = useState<PaymentIntegrationDto | null>(null)
  const [paymentLoading, setPaymentLoading] = useState(true)
  const [paymentError, setPaymentError] = useState<string | null>(null)

  const [channels, setChannels] = useState<NotificationChannelItem[]>([])
  const [channelsLoading, setChannelsLoading] = useState(true)
  const [channelsError, setChannelsError] = useState<string | null>(null)

  const [form, setForm] = useState<PaymentForm | null>(null)
  const [saving, setSaving] = useState(false)
  const [baseUrlError, setBaseUrlError] = useState<string | null>(null)
  const [saveError, setSaveError] = useState<string | null>(null)
  const [saveNotice, setSaveNotice] = useState<string | null>(null)

  const loadPayment = useCallback(async () => {
    setPaymentLoading(true)
    setPaymentError(null)
    try {
      const result = await getPaymentIntegration()
      setPayment(result)
      setForm({
        mode: result.mode,
        baseUrl: result.baseUrl,
        clientId: "",
        apiKey: "",
        checksumKey: "",
      })
    } catch (error) {
      setPaymentError(describeIntegrationError(error))
    } finally {
      setPaymentLoading(false)
    }
  }, [])

  const loadNotifications = useCallback(async () => {
    setChannelsLoading(true)
    setChannelsError(null)
    try {
      const result = await getNotificationIntegrations()
      setChannels(result.channels)
    } catch (error) {
      setChannelsError(describeIntegrationError(error))
    } finally {
      setChannelsLoading(false)
    }
  }, [])

  useEffect(() => {
    void loadPayment()
    void loadNotifications()
  }, [loadPayment, loadNotifications])

  function changeMode(nextMode: PaymentMode) {
    setBaseUrlError(null)
    setSaveNotice(null)
    setSaveError(null)
    setForm((current) => {
      if (!current) return current
      const allowlist = PAYMENT_BASE_URL_ALLOWLIST[nextMode]
      const baseUrl = allowlist.includes(current.baseUrl) ? current.baseUrl : allowlist[0]
      return {
        ...current,
        mode: nextMode,
        baseUrl,
        clientId: "",
        apiKey: "",
        checksumKey: "",
      }
    })
  }

  async function savePayment(event: React.FormEvent) {
    event.preventDefault()
    if (!form || !payment) return

    const allowlist = PAYMENT_BASE_URL_ALLOWLIST[form.mode]
    if (!allowlist.includes(form.baseUrl)) {
      setBaseUrlError(`This URL is not permitted for ${form.mode} mode.`)
      return
    }

    setBaseUrlError(null)
    setSaveError(null)
    setSaveNotice(null)
    setSaving(true)
    try {
      const body: UpdatePaymentIntegrationRequest = { mode: form.mode, baseUrl: form.baseUrl }
      const clientId = form.clientId.trim()
      const apiKey = form.apiKey.trim()
      const checksumKey = form.checksumKey.trim()
      if (clientId) body.clientId = clientId
      if (apiKey) body.apiKey = apiKey
      if (checksumKey) body.checksumKey = checksumKey

      const result = await updatePaymentIntegration(body)
      setPayment((current) =>
        current ? { ...current, ...result.data, capabilities: current.capabilities } : current,
      )
      setForm((current) => (current ? { ...current, clientId: "", apiKey: "", checksumKey: "" } : current))
      setSaveNotice(result.message || "Payment integration updated.")
    } catch (error) {
      if (error instanceof AuthApiError && error.code === "INVALID_PAYMENT_BASE_URL") {
        setBaseUrlError(`This URL is not permitted for ${form.mode} mode.`)
      } else {
        setSaveError(describeIntegrationError(error))
      }
    } finally {
      setSaving(false)
    }
  }

  const sortedChannels = [...channels].sort(
    (a, b) =>
      (CHANNEL_ORDER.indexOf(a.channel) + 1 || 99) - (CHANNEL_ORDER.indexOf(b.channel) + 1 || 99),
  )

  return (
    <div className="grid items-start gap-5 xl:grid-cols-2">
      {/* ── PayOS Gateway ─────────────────────────────────────────────── */}
      <section className="card" style={{ borderRadius: 20, padding: "1.25rem" }}>
        <div className="flex flex-wrap items-center gap-2">
          <span style={{ color: "var(--primary)" }}>
            <UntitledIcon name="wallet" size={18} />
          </span>
          <h2 className="text-base font-semibold" style={{ color: "var(--fg)" }}>
            PayOS Gateway
          </h2>
          {payment && <Badge tone={MODE_TONE[payment.mode]}>{payment.mode}</Badge>}
          {payment && <Badge tone={STATUS_TONE[payment.status]}>{payment.status}</Badge>}
        </div>

        {paymentLoading ? (
          <div className="mt-4 grid gap-2">
            {[0, 1, 2, 3].map((n) => (
              <Skeleton key={n} className="h-8 w-full" />
            ))}
          </div>
        ) : paymentError ? (
          <div className="mt-4">
            <CardError message={paymentError} onRetry={() => void loadPayment()} />
          </div>
        ) : !payment ? (
          <p className="mt-4 text-sm" style={{ color: "var(--muted)" }}>
            No payment integration is configured.
          </p>
        ) : (
          <>
            <div className="mt-4">
              <InfoRow label="Provider">{payment.provider}</InfoRow>
              <InfoRow label="Base URL">
                <code className="text-xs" style={{ fontFamily: "monospace" }}>
                  {payment.baseUrl}
                </code>
              </InfoRow>
              <InfoRow label="Client ID">
                {payment.clientIdMasked ? (
                  <code className="text-xs" style={{ fontFamily: "monospace" }}>
                    {payment.clientIdMasked}
                  </code>
                ) : (
                  <span style={{ color: "var(--muted)" }}>Not set</span>
                )}
              </InfoRow>
              <InfoRow label="API key">
                {payment.apiKeyConfigured ? <Badge tone="success">Configured</Badge> : <Badge tone="muted">Not set</Badge>}
              </InfoRow>
              <InfoRow label="Checksum key">
                {payment.checksumKeyConfigured ? <Badge tone="success">Configured</Badge> : <Badge tone="muted">Not set</Badge>}
              </InfoRow>
              <InfoRow label="Last tested">{formatDateTime(payment.lastTestedAt)}</InfoRow>
              <InfoRow label="Capabilities">
                <span className="flex flex-wrap justify-end gap-1">
                  {payment.capabilities.supportsVietQr && <Badge tone="primary">VietQR</Badge>}
                  {payment.capabilities.supportsWebhook && <Badge tone="primary">Webhook</Badge>}
                  {payment.capabilities.supportsDirectRefund ? (
                    <Badge tone="primary">Direct refund</Badge>
                  ) : (
                    <Badge tone="muted">No direct refund</Badge>
                  )}
                </span>
              </InfoRow>
            </div>

            {form && (
              <form onSubmit={savePayment} className="mt-4 grid gap-3 border-t pt-4" style={{ borderColor: "var(--border)" }}>
                <div className="flex items-center gap-2">
                  <UntitledIcon name="sliders" size={16} />
                  <h3 className="text-sm font-semibold" style={{ color: "var(--fg)" }}>
                    Update configuration
                  </h3>
                </div>
                <p className="text-xs" style={{ color: "var(--muted)" }}>
                  Secret fields are never shown again. Leave a secret field blank to keep the existing value.
                </p>

                {saveError && (
                  <div
                    role="alert"
                    className="flex items-center gap-2 rounded-xl border p-2.5 text-xs"
                    style={{
                      borderColor: "var(--occupied)",
                      background: "color-mix(in srgb, var(--occupied) 10%, var(--card))",
                      color: "var(--badge-danger-strong)",
                    }}
                  >
                    <UntitledIcon name="alert" size={16} />
                    <span>{saveError}</span>
                  </div>
                )}
                {saveNotice && (
                  <div
                    role="status"
                    className="flex items-center gap-2 rounded-xl border p-2.5 text-xs"
                    style={{
                      borderColor: "var(--available)",
                      background: "color-mix(in srgb, var(--available) 10%, var(--card))",
                      color: "var(--badge-success-fg)",
                    }}
                  >
                    <UntitledIcon name="check-circle" size={16} />
                    <span>{saveNotice}</span>
                  </div>
                )}

                <div className="grid gap-3 sm:grid-cols-2">
                  <div>
                    <label className="label" htmlFor="payment-mode">
                      Mode
                    </label>
                    <RoundedSelect
                      id="payment-mode"
                      className="input"
                      value={form.mode}
                      onChange={(event) => changeMode(event.target.value as PaymentMode)}
                    >
                      {(["LIVE", "SANDBOX", "FIXTURE"] as PaymentMode[]).map((mode) => (
                        <option key={mode} value={mode}>
                          {mode}
                        </option>
                      ))}
                    </RoundedSelect>
                  </div>
                  <div>
                    <label className="label" htmlFor="payment-base-url">
                      Base URL
                    </label>
                    <RoundedSelect
                      id="payment-base-url"
                      className="input"
                      value={form.baseUrl}
                      onChange={(event) => {
                        setBaseUrlError(null)
                        setForm((current) => (current ? { ...current, baseUrl: event.target.value } : current))
                      }}
                    >
                      {Array.from(new Set([...PAYMENT_BASE_URL_ALLOWLIST[form.mode], form.baseUrl])).map(
                        (url) => (
                          <option key={url} value={url}>
                            {url}
                          </option>
                        ),
                      )}
                    </RoundedSelect>
                    {baseUrlError && (
                      <p className="mt-1 text-xs" style={{ color: "var(--badge-danger-strong)" }}>
                        {baseUrlError}
                      </p>
                    )}
                  </div>
                </div>

                {form.mode === "FIXTURE" && (
                  <div
                    className="flex items-center gap-2 rounded-xl border p-2.5 text-xs"
                    style={{
                      borderColor: "var(--reserved)",
                      background: "color-mix(in srgb, var(--reserved) 12%, var(--card))",
                      color: "var(--badge-warning-fg)",
                    }}
                  >
                    <UntitledIcon name="alert" size={16} />
                    <span>FIXTURE mode performs zero outbound HTTP calls; all payment calls are simulated locally.</span>
                  </div>
                )}

                <div className="grid gap-3">
                  <div>
                    <label className="label" htmlFor="payment-client-id">
                      Client ID{" "}
                      <span style={{ color: "var(--muted)", fontWeight: 400 }}>
                        (leave blank to keep the current ID)
                      </span>
                    </label>
                    <input
                      id="payment-client-id"
                      className="input"
                      autoComplete="off"
                      placeholder="New client ID"
                      value={form.clientId}
                      onChange={(event) =>
                        setForm((current) => (current ? { ...current, clientId: event.target.value } : current))
                      }
                    />
                  </div>
                  <div>
                    <label className="label" htmlFor="payment-api-key">
                      API key{" "}
                      <span style={{ color: "var(--muted)", fontWeight: 400 }}>
                        (leave blank to keep the current key)
                      </span>
                    </label>
                    <input
                      id="payment-api-key"
                      className="input"
                      type="password"
                      autoComplete="new-password"
                      placeholder="New API key"
                      value={form.apiKey}
                      onChange={(event) =>
                        setForm((current) => (current ? { ...current, apiKey: event.target.value } : current))
                      }
                    />
                  </div>
                  <div>
                    <label className="label" htmlFor="payment-checksum-key">
                      Checksum key{" "}
                      <span style={{ color: "var(--muted)", fontWeight: 400 }}>
                        (leave blank to keep the current key)
                      </span>
                    </label>
                    <input
                      id="payment-checksum-key"
                      className="input"
                      type="password"
                      autoComplete="new-password"
                      placeholder="New checksum key"
                      value={form.checksumKey}
                      onChange={(event) =>
                        setForm((current) => (current ? { ...current, checksumKey: event.target.value } : current))
                      }
                    />
                  </div>
                </div>

                <div className="flex justify-end">
                  <Button type="submit" variant="primary" loading={saving} leadingIcon={<UntitledIcon name="check" size={16} />}>
                    Save integration
                  </Button>
                </div>
              </form>
            )}
          </>
        )}
      </section>

      {/* ── Notification Channels ────────────────────────────────────── */}
      <section className="card" style={{ borderRadius: 20, padding: "1.25rem" }}>
        <div className="flex items-center gap-2">
          <span style={{ color: "var(--primary)" }}>
            <UntitledIcon name="mail" size={18} />
          </span>
          <h2 className="text-base font-semibold" style={{ color: "var(--fg)" }}>
            Notification Channels
          </h2>
        </div>
        <p className="mt-1 text-xs" style={{ color: "var(--muted)" }}>
          Delivery adapters for driver and operator messages.
        </p>

        {channelsLoading ? (
          <div className="mt-4 grid gap-3">
            {[0, 1, 2, 3].map((n) => (
              <Skeleton key={n} className="h-16 w-full" />
            ))}
          </div>
        ) : channelsError ? (
          <div className="mt-4">
            <CardError message={channelsError} onRetry={() => void loadNotifications()} />
          </div>
        ) : sortedChannels.length === 0 ? (
          <p className="mt-4 text-sm" style={{ color: "var(--muted)" }}>
            No notification channels reported.
          </p>
        ) : (
          <div className="mt-4 grid gap-2">
            {sortedChannels.map((channel) => (
              <div
                key={channel.channel}
                className="rounded-xl border p-3"
                style={{ borderColor: "var(--border)", background: "var(--bg)" }}
              >
                <div className="flex flex-wrap items-center gap-2">
                  <strong className="text-sm" style={{ color: "var(--fg)" }}>
                    {channel.channel.replace("_", "-")}
                  </strong>
                  <Badge tone={CHANNEL_STATUS_TONE[channel.status] ?? "muted"}>{channel.status}</Badge>
                  {channel.isReady ? (
                    <Badge tone="success">Ready</Badge>
                  ) : (
                    <Badge tone="muted">Not ready</Badge>
                  )}
                </div>
                <p className="mt-1 text-xs" style={{ color: "var(--muted)" }}>
                  Provider: {channel.provider}
                </p>
                <p className="mt-1 text-xs" style={{ color: "var(--muted)", lineHeight: 1.5 }}>
                  {channel.description}
                </p>
                <p className="mt-1 text-xs" style={{ color: "var(--muted)" }}>
                  Last tested: {formatDateTime(channel.lastTestedAt)}
                </p>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  )
}
