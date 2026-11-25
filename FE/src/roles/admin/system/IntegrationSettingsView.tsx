import { useCallback, useEffect, useState } from "react"

import { Badge, type BadgeTone } from "../../../components/ui/Badge"
import { Button } from "../../../components/ui/Button"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import {
  getNotificationIntegrations,
  type NotificationChannelItem,
} from "../../../lib/adminIntegrationApi"
import { AuthApiError } from "../../../lib/authApi"

const CHANNEL_ORDER = ["EMAIL", "SMS", "PUSH", "IN_APP"]

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

export function IntegrationSettingsView() {
  const [channels, setChannels] = useState<NotificationChannelItem[]>([])
  const [channelsLoading, setChannelsLoading] = useState(true)
  const [channelsError, setChannelsError] = useState<string | null>(null)

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
    void loadNotifications()
  }, [loadNotifications])

  const sortedChannels = [...channels].sort(
    (a, b) =>
      (CHANNEL_ORDER.indexOf(a.channel) + 1 || 99) - (CHANNEL_ORDER.indexOf(b.channel) + 1 || 99),
  )

  return (
    <div className="grid items-start gap-5 xl:grid-cols-2">
      <section className="card" style={{ borderRadius: 20, padding: "1.25rem" }}>
        <div className="flex flex-wrap items-center gap-2">
          <span style={{ color: "var(--primary)" }}>
            <UntitledIcon name="wallet" size={18} />
          </span>
          <h2 className="text-base font-semibold" style={{ color: "var(--fg)" }}>
            Payment configuration deferred
          </h2>
        </div>
        <p className="mt-1 text-xs" style={{ color: "var(--muted)" }}>
          PayOS configuration is unavailable in this MVP.
        </p>
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
