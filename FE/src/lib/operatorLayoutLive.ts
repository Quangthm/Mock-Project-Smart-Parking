import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr"
import { authApi, authExpiredEvent, authRefreshedEvent, ensureAccessToken } from "./authApi"
import { operatorRealtimeGuard, type RealtimeEventEnvelope } from "./realtimeIdempotency"

const serverUrl = (import.meta.env.VITE_RESERVATION_API_BASE_URL ?? "http://localhost:5055").replace(/\/$/, "")

export const LAYOUT_EVENT_TYPES = ["SlotStateChanged", "OccupancyChanged", "OccupancyReleased"] as const

function slotIdOf(envelope: RealtimeEventEnvelope): string | undefined {
  const payload = envelope.payload
  if (!payload || typeof payload !== "object") return undefined
  const slotId = (payload as { slotId?: unknown }).slotId
  return typeof slotId === "string" ? slotId : undefined
}

export function watchOperatorLayout(siteId: string, onLayoutChanged: () => void): () => void {
  if (!siteId || !siteId.trim()) return () => {}

  let stopped = false
  let retry: ReturnType<typeof setTimeout> | undefined
  let starting: Promise<void> | undefined
  const connection = new HubConnectionBuilder()
    .withUrl(serverUrl + "/ws/parking-lots/" + encodeURIComponent(siteId) + "/layout", { accessTokenFactory: ensureAccessToken, withCredentials: false })
    .configureLogging(LogLevel.None)
    .build()

  // One accepted event = one authoritative snapshot re-fetch. Never patch local state from a delta.
  function receive(envelope: RealtimeEventEnvelope) {
    const slotId = slotIdOf(envelope)
    if (!operatorRealtimeGuard.decide(envelope, slotId).apply) return
    operatorRealtimeGuard.commit(envelope, slotId)
    onLayoutChanged()
  }

  for (const eventType of LAYOUT_EVENT_TYPES) connection.on(eventType, receive)

  // A fresh connect/reconnect starts a new authoritative snapshot; drop stale watermarks first.
  connection.on("SnapshotRequired", () => {
    operatorRealtimeGuard.reset()
    onLayoutChanged()
  })

  function clearRetry() {
    clearTimeout(retry)
    retry = undefined
  }
  function scheduleRetry(delay: number) {
    clearRetry()
    if (stopped || authApi.expiresAt() <= Date.now() + 30_000) return
    retry = setTimeout(() => {
      retry = undefined
      if (authApi.expiresAt() > Date.now() + 30_000) void connect()
    }, delay)
  }
  async function connect() {
    if (stopped || starting || connection.state !== "Disconnected" || document.visibilityState === "hidden") return
    clearRetry()
    starting = connection.start().catch(() => {
      scheduleRetry(10_000)
    }).finally(() => { starting = undefined })
    await starting
  }
  // Reconnect after the next authenticated activity/refresh; never refresh solely
  // to keep an idle subscription alive. Server closes connections at token expiry.
  connection.onclose(() => {
    scheduleRetry(2_000)
  })
  const resume = () => {
    if (connection.state === "Disconnected") void connect()
  }
  const expire = () => { clearRetry(); void connection.stop() }
  window.addEventListener(authRefreshedEvent, resume)
  window.addEventListener(authExpiredEvent, expire)
  window.addEventListener("focus", resume)
  document.addEventListener("visibilitychange", resume)
  operatorRealtimeGuard.reset()
  void connect()
  return () => {
    stopped = true
    clearRetry()
    window.removeEventListener(authRefreshedEvent, resume)
    window.removeEventListener(authExpiredEvent, expire)
    window.removeEventListener("focus", resume)
    document.removeEventListener("visibilitychange", resume)
    void connection.stop()
  }
}