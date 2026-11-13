import { HubConnectionBuilder, HttpTransportType, LogLevel } from "@microsoft/signalr"
import { authApi, authExpiredEvent, authRefreshedEvent, ensureAccessToken } from "./authApi"
import { operatorRealtimeGuard, type RealtimeEventEnvelope } from "./realtimeIdempotency"

const serverUrl = (import.meta.env.VITE_RESERVATION_API_BASE_URL ?? "http://localhost:5055").replace(/\/$/, "")

export const LAYOUT_EVENT_TYPES = ["SlotStateChanged", "OccupancyChanged", "OccupancyReleased"] as const
export type LayoutConnectionState = "connecting" | "connected" | "unavailable"
let shutdown: Promise<void> | undefined

function isPermanentConnectionFailure(error: unknown): boolean {
  if (!error || typeof error !== "object") return false
  const failure = error as { statusCode?: number; message?: string; innerErrors?: unknown[] }
  return [400, 401, 403, 404].includes(failure.statusCode ?? 0) ||
    /INVALID_SITE_ID|OPERATOR_LOT_UNASSIGNED|UNAUTHENTICATED|Forbidden|Status code ['"]?(400|401|403|404)\b/i.test(failure.message ?? "") ||
    (Array.isArray(failure.innerErrors) && failure.innerErrors.some(isPermanentConnectionFailure))
}

function slotIdOf(envelope: RealtimeEventEnvelope): string | undefined {
  const payload = envelope.payload
  if (!payload || typeof payload !== "object") return undefined
  const slotId = (payload as { slotId?: unknown }).slotId
  return typeof slotId === "string" ? slotId : undefined
}

export function watchOperatorLayout(
  siteId: string,
  onLayoutChanged: (refreshBookings?: boolean) => void | Promise<void>,
  onConnectionStateChange?: (state: LayoutConnectionState) => void,
): () => void {
  if (!/^[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}$/i.test(siteId)) return () => {}

  let stopped = false
  let retry: ReturnType<typeof setTimeout> | undefined
  let starting: Promise<void> | undefined
  let invalidation: ReturnType<typeof setTimeout> | undefined
  let refreshing = false
  let dirty = false
  let refreshBookings = false
  let retryAttempts = 0
  let connectedAt = 0
  let nextAttemptAt = 0
  let blocked = false
  const connection = new HubConnectionBuilder()
    .withUrl(serverUrl + "/ws/parking-lots/" + encodeURIComponent(siteId) + "/layout", {
      accessTokenFactory: ensureAccessToken, withCredentials: false,
      // Do not silently introduce HTTP polling when the realtime endpoint rejects WebSockets.
      transport: HttpTransportType.WebSockets,
    })
    .configureLogging(LogLevel.None)
    .build()

  // Coalesce bursts and retain one trailing snapshot if changes arrive during a read.
  function invalidate() {
    dirty = true
    if (stopped || refreshing || invalidation !== undefined || document.visibilityState === "hidden") return
    invalidation = setTimeout(() => {
      invalidation = undefined
      if (stopped || document.visibilityState === "hidden") return
      dirty = false
      refreshing = true
      const bookingsChanged = refreshBookings
      refreshBookings = false
      void Promise.resolve().then(() => { if (!stopped) return onLayoutChanged(bookingsChanged) })
        .catch(() => { /* The resource loader owns its error state. */ })
        .finally(() => { refreshing = false; if (!stopped && dirty) invalidate() })
    }, 300)
  }

  // Events invalidate an authoritative snapshot; never patch business state from a delta.
  function receive(envelope: RealtimeEventEnvelope, eventType: string) {
    if (stopped || envelope.siteId && envelope.siteId !== siteId) return
    const slotId = slotIdOf(envelope)
    if (!operatorRealtimeGuard.decide(envelope, slotId).apply) return
    operatorRealtimeGuard.commit(envelope, slotId)
    if (eventType === "OccupancyChanged" || eventType === "OccupancyReleased") refreshBookings = true
    invalidate()
  }

  const handlers = LAYOUT_EVENT_TYPES.map(eventType => {
    const handler = (envelope: RealtimeEventEnvelope) => receive(envelope, eventType)
    connection.on(eventType, handler)
    return { eventType, handler }
  })

  // A fresh connect/reconnect starts a new authoritative snapshot; drop stale watermarks first.
  connection.on("SnapshotRequired", () => {
    if (stopped) return
    operatorRealtimeGuard.reset()
    refreshBookings = true
    invalidate()
  })

  function clearRetry() {
    clearTimeout(retry)
    retry = undefined
  }
  function scheduleRetry(delay: number) {
    if (stopped || blocked || retry !== undefined) return
    if (retryAttempts >= 3 || authApi.expiresAt() <= Date.now() + 30_000) {
      onConnectionStateChange?.("unavailable")
      return
    }
    const wait = Math.min(60_000, delay * 2 ** Math.max(0, retryAttempts - 1)) + Math.floor(Math.random() * 1000)
    nextAttemptAt = Date.now() + wait
    retry = setTimeout(() => {
      retry = undefined
      if (authApi.expiresAt() > Date.now() + 30_000) void connect()
    }, wait)
  }
  async function connect() {
    if (stopped || blocked || retryAttempts >= 3 || Date.now() < nextAttemptAt || starting || connection.state !== "Disconnected" || document.visibilityState === "hidden" ||
      authApi.expiresAt() <= Date.now() + 30_000) return
    if (shutdown) {
      await shutdown
      if (stopped || starting || connection.state !== "Disconnected") return
    }
    clearRetry()
    retryAttempts++
    onConnectionStateChange?.("connecting")
    starting = connection.start().then(() => {
      connectedAt = Date.now()
      if (!stopped) onConnectionStateChange?.("connected")
    }).catch((error: unknown) => {
      if (stopped) return
      onConnectionStateChange?.("unavailable")
      if (isPermanentConnectionFailure(error)) { blocked = true; clearRetry(); return }
      scheduleRetry(10_000)
    }).finally(() => { starting = undefined })
    await starting
  }
  // Reconnect after the next authenticated activity/refresh; never refresh solely
  // to keep an idle subscription alive. Server closes connections at token expiry.
  connection.onclose((error?: Error) => {
    if (stopped) return
    onConnectionStateChange?.("unavailable")
    if (isPermanentConnectionFailure(error)) { blocked = true; clearRetry(); return }
    if (connectedAt && Date.now() - connectedAt > 60_000) retryAttempts = 0
    scheduleRetry(2_000)
  })
  const resume = () => {
    if (connection.state === "Disconnected") void connect()
    else if (dirty) invalidate()
  }
  const expire = () => { blocked = true; clearRetry(); void connection.stop().catch(() => {}) }
  window.addEventListener(authRefreshedEvent, resume)
  window.addEventListener(authExpiredEvent, expire)
  window.addEventListener("focus", resume)
  document.addEventListener("visibilitychange", resume)
  operatorRealtimeGuard.reset()
  void connect()
  return () => {
    stopped = true
    clearRetry()
    clearTimeout(invalidation)
    for (const { eventType, handler } of handlers) connection.off(eventType, handler)
    window.removeEventListener(authRefreshedEvent, resume)
    window.removeEventListener(authExpiredEvent, expire)
    window.removeEventListener("focus", resume)
    document.removeEventListener("visibilitychange", resume)
    const stopping = connection.stop().catch(() => {})
    shutdown = stopping
    void stopping.then(() => { if (shutdown === stopping) shutdown = undefined })
  }
}
