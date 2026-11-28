import { HubConnectionBuilder, HttpTransportType, LogLevel } from "@microsoft/signalr"
import { authApi, authExpiredEvent, authRefreshedEvent, ensureAccessToken, getSessionGeneration } from "./authApi"
import { RealtimeIdempotencyGuard, operatorRealtimeGuard, type RealtimeEventEnvelope } from "./realtimeIdempotency"

const serverUrl = (import.meta.env.VITE_RESERVATION_API_BASE_URL ?? "http://localhost:5055").replace(/\/$/, "")

export const LAYOUT_EVENT_TYPES = ["SlotStateChanged", "OccupancyChanged", "OccupancyReleased"] as const
export type LayoutConnectionState = "connecting" | "connected" | "unavailable"
export type LayoutSyncOutcome = "synced" | "stale" | "error"
export type LayoutSubscriberResult = LayoutSyncOutcome | boolean | void

export interface LayoutInvalidationDetails {
  refreshLayout: boolean
  affectedResources: string[]
  refreshReason?: "initial" | "reconnect" | "invalidation"
}

export interface LayoutSyncDiagnostics {
  transportState: LayoutConnectionState
  transportConnectedAt: number
  acceptedAt: number
  lastSyncedAt: number
  lastSyncOutcome: LayoutSyncOutcome | "pending"
  retryAttempts: number
}

const MAX_INITIAL_ATTEMPTS = 3
const FOREGROUND_COOLDOWN_MS = 60_000
const UUID_PATTERN = /^[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}$/i

interface RetryBudget {
  retryAttempts: number
  nextAttemptAt: number
  blocked: boolean
  blockedReason?: "auth" | "permanent"
  connectedAt: number
  acceptedAt: number
  lastSyncedAt: number
  lastSyncOutcome: LayoutSyncOutcome | "pending"
  windowStartedAt: number
  guard: typeof operatorRealtimeGuard
}

interface LayoutSubscriber {
  onLayoutChanged: (refreshBookings?: boolean, details?: LayoutInvalidationDetails) => LayoutSubscriberResult | Promise<LayoutSubscriberResult>
  onConnectionStateChange?: (state: LayoutConnectionState) => void
  onSyncStateChange?: (diagnostics: LayoutSyncDiagnostics) => void
}

interface SharedLayoutChannel {
  key: string
  subscribers: Set<LayoutSubscriber>
  state: LayoutConnectionState
  start: () => void
  stop: () => void
}

let shutdown: Promise<void> | undefined
const activeChannels = new Map<string, SharedLayoutChannel>()
const retryBudgets = new Map<string, RetryBudget>()

function currentGeneration(): number {
  return typeof getSessionGeneration === "function" ? getSessionGeneration() : 0
}

function createGuard() {
  return typeof RealtimeIdempotencyGuard === "function"
    ? new RealtimeIdempotencyGuard()
    : operatorRealtimeGuard
}

function budgetFor(generation: number, normalizedSiteId: string): RetryBudget {
  const prefix = `${generation}:`
  for (const existingKey of retryBudgets.keys()) {
    if (!existingKey.startsWith(prefix)) retryBudgets.delete(existingKey)
  }
  const key = `${prefix}${normalizedSiteId}`
  let budget = retryBudgets.get(key)
  if (!budget) {
    budget = {
      retryAttempts: 0,
      nextAttemptAt: 0,
      blocked: false,
      connectedAt: 0,
      acceptedAt: 0,
      lastSyncedAt: 0,
      lastSyncOutcome: "pending",
      windowStartedAt: 0,
      guard: createGuard(),
    }
    retryBudgets.set(key, budget)
  }
  return budget
}

function failureBlockReason(error: unknown): "auth" | "permanent" | undefined {
  if (!error || typeof error !== "object") return undefined
  const failure = error as { statusCode?: number; message?: string; innerErrors?: unknown[] }
  const message = failure.message ?? ""
  if ([400, 403, 404].includes(failure.statusCode ?? 0) ||
    /INVALID_SITE_ID|OPERATOR_LOT_UNASSIGNED|Forbidden|Status code ['"]?(400|403|404)\b/i.test(message)) {
    return "permanent"
  }
  if (failure.statusCode === 401 || /UNAUTHENTICATED|Status code ['"]?401\b/i.test(message)) {
    return "auth"
  }
  if (Array.isArray(failure.innerErrors)) {
    for (const inner of failure.innerErrors) {
      const reason = failureBlockReason(inner)
      if (reason === "permanent") return "permanent"
      if (reason === "auth") return "auth"
    }
  }
  return undefined
}

function slotIdOf(envelope: RealtimeEventEnvelope): string | undefined {
  const payload = envelope.payload
  if (!payload || typeof payload !== "object") return undefined
  const slotId = (payload as { slotId?: unknown }).slotId
  return typeof slotId === "string" ? slotId : undefined
}

function createSharedChannel(
  key: string,
  siteId: string,
  generation: number,
  budget: RetryBudget,
): SharedLayoutChannel {
  const normalizedSiteId = siteId.toLowerCase()
  const subscribers = new Set<LayoutSubscriber>()
  let stopped = false
  let intentionalStop = false
  let attemptFence = 0
  let activeAttempt = 0
  let accepted = false
  let acceptedAttempt = 0
  let syncedAttempt = 0
  let connectedAttempt = 0
  let state: LayoutConnectionState = budget.blocked ? "unavailable" : "connecting"
  let retry: ReturnType<typeof setTimeout> | undefined
  let starting: Promise<void> | undefined
  let invalidation: ReturnType<typeof setTimeout> | undefined
  let refreshing = false
  let dirty = false
  let refreshBookings = false
  let pendingLayoutRefresh = false
  let pendingRefreshReason: "initial" | "reconnect" | "invalidation" = "initial"
  let hasDeliveredInitialConnectSnapshot = false
  const pendingResources = new Set<string>()

  function clearInvalidation() {
    clearTimeout(invalidation)
    invalidation = undefined
  }

  function replenishIfHealthy(now: number) {
    if (budget.acceptedAt > 0 && now - budget.acceptedAt >= FOREGROUND_COOLDOWN_MS) {
      budget.retryAttempts = 0
      budget.nextAttemptAt = 0
      budget.windowStartedAt = 0
    }
  }

  function completeReconciliationIfReady(attempt: number) {
    if (
      stopped ||
      attempt <= 0 ||
      attempt !== activeAttempt ||
      !accepted ||
      acceptedAttempt !== attempt ||
      syncedAttempt !== attempt ||
      (connection.state !== "Connected" && connectedAttempt !== attempt)
    ) {
      return
    }
    const now = Date.now()
    if (budget.acceptedAt === 0) budget.acceptedAt = now
    replenishIfHealthy(now)
    if (state !== "connected") notifyState("connected")
  }

  function classifyResult(result: PromiseSettledResult<LayoutSubscriberResult>): LayoutSyncOutcome {
    if (result.status === "rejected") return "error"
    if (result.value === "error" || result.value === false) return "error"
    if (result.value === "stale") return "stale"
    return "synced"
  }

  const channel: SharedLayoutChannel = {
    key,
    subscribers,
    state,
    start: () => {
      void connect()
    },
    stop: () => {
      if (accepted && acceptedAttempt === activeAttempt) replenishIfHealthy(Date.now())
      activeAttempt = ++attemptFence
      accepted = false
      acceptedAttempt = 0
      syncedAttempt = 0
      connectedAttempt = 0
      budget.acceptedAt = 0
      stopped = true
      intentionalStop = true
      clearRetry()
      clearInvalidation()
      for (const { eventType, handler } of handlers) connection.off?.(eventType, handler)
      connection.off?.("SnapshotRequired", onSnapshotRequired)
      window.removeEventListener(authRefreshedEvent, onAuthRefreshed)
      window.removeEventListener(authExpiredEvent, expire)
      window.removeEventListener("focus", resume)
      window.removeEventListener("online", resume)
      window.removeEventListener("offline", resume)
      document.removeEventListener("visibilitychange", resume)
      const stopping = connection.stop().catch(() => {})
      shutdown = stopping
      void stopping.then(() => { if (shutdown === stopping) shutdown = undefined })
    },
  }

  const connection = new HubConnectionBuilder()
    .withUrl(serverUrl + "/ws/parking-lots/" + encodeURIComponent(siteId) + "/layout", {
      accessTokenFactory: ensureAccessToken, withCredentials: false,
      // Do not silently introduce HTTP polling when the realtime endpoint rejects WebSockets.
      transport: HttpTransportType.WebSockets,
    })
    .configureLogging(LogLevel.None)
    .build()

  function emitSyncState() {
    const diagnostics: LayoutSyncDiagnostics = {
      transportState: state,
      transportConnectedAt: budget.connectedAt,
      acceptedAt: budget.acceptedAt,
      lastSyncedAt: budget.lastSyncedAt,
      lastSyncOutcome: budget.lastSyncOutcome,
      retryAttempts: budget.retryAttempts,
    }
    for (const subscriber of subscribers) subscriber.onSyncStateChange?.(diagnostics)
  }

  function notifyState(nextState: LayoutConnectionState) {
    state = nextState
    channel.state = nextState
    for (const subscriber of subscribers) subscriber.onConnectionStateChange?.(nextState)
    emitSyncState()
  }

  // Coalesce bursts and retain one trailing snapshot if changes arrive during a read.
  function invalidate() {
    dirty = true
    if (stopped || refreshing || invalidation !== undefined || document.visibilityState === "hidden" ||
      (typeof navigator !== "undefined" && navigator.onLine === false)) return
    invalidation = setTimeout(() => {
      invalidation = undefined
      if (stopped || document.visibilityState === "hidden" ||
        (typeof navigator !== "undefined" && navigator.onLine === false)) return
      dirty = false
      refreshing = true
      const readAttempt = activeAttempt
      const bookingsChanged = refreshBookings
      refreshBookings = false
      const shouldRefreshLayout = pendingLayoutRefresh || syncedAttempt !== readAttempt
      pendingLayoutRefresh = false
      const refreshReason = pendingRefreshReason
      pendingRefreshReason = "invalidation"
      const resources = pendingResources.size > 0
        ? Array.from(pendingResources)
        : (shouldRefreshLayout ? ["full-layout"] : ["bookings"])
      pendingResources.clear()
      const details: LayoutInvalidationDetails = {
        refreshLayout: shouldRefreshLayout,
        affectedResources: resources,
        refreshReason,
      }
      const tasks = Array.from(subscribers, subscriber =>
        Promise.resolve().then(() => {
          if (stopped || !subscribers.has(subscriber)) return "stale" as const
          return subscriber.onLayoutChanged(bookingsChanged, details)
        }),
      )
      void Promise.allSettled(tasks)
        .then(results => {
          if (stopped || readAttempt === 0 || readAttempt !== activeAttempt) return
          if (results.length === 0) return
          const outcomes = results.map(classifyResult)
          if (outcomes.includes("error")) {
            budget.lastSyncOutcome = "error"
            emitSyncState()
            return
          }
          if (outcomes.includes("stale")) {
            if (shouldRefreshLayout) {
              budget.lastSyncOutcome = "stale"
              emitSyncState()
            }
            return
          }
          if (!shouldRefreshLayout) return
          budget.lastSyncedAt = Date.now()
          budget.lastSyncOutcome = "synced"
          emitSyncState()
          if (accepted && acceptedAttempt === readAttempt) {
            syncedAttempt = readAttempt
            completeReconciliationIfReady(readAttempt)
          }
        })
        .finally(() => { refreshing = false; if (!stopped && dirty) invalidate() })
    }, 300)
  }

  function markAcceptedCurrentAttempt() {
    if (activeAttempt === 0) activeAttempt = ++attemptFence
    accepted = true
    acceptedAttempt = activeAttempt
  }

  // Events invalidate an authoritative snapshot; never patch business state from a delta.
  function receive(envelope: RealtimeEventEnvelope, eventType: string) {
    if (stopped || (envelope.siteId && envelope.siteId.toLowerCase() !== normalizedSiteId)) return
    const slotId = slotIdOf(envelope)
    if (!budget.guard.decide(envelope, slotId).apply) return
    budget.guard.commit(envelope, slotId)
    markAcceptedCurrentAttempt()
    pendingRefreshReason = "invalidation"
    pendingLayoutRefresh = true
    pendingResources.add("full-layout")
    if (eventType === "OccupancyChanged" || eventType === "OccupancyReleased") refreshBookings = true
    invalidate()
  }

  const handlers = LAYOUT_EVENT_TYPES.map(eventType => {
    const handler = (envelope: RealtimeEventEnvelope) => receive(envelope, eventType)
    connection.on(eventType, handler)
    return { eventType, handler }
  })

  // Connect reconciliation drops stale watermarks; business outbox envelopes deduplicate by eventId.
  const onSnapshotRequired = (arg?: unknown) => {
    if (stopped) return
    if (typeof arg === "string") {
      if (arg && arg.toLowerCase() !== normalizedSiteId) return
      markAcceptedCurrentAttempt()
      budget.guard.reset()
      pendingRefreshReason = !hasDeliveredInitialConnectSnapshot && budget.lastSyncedAt === 0
        ? "initial"
        : "reconnect"
      hasDeliveredInitialConnectSnapshot = true
      pendingLayoutRefresh = true
      pendingResources.add("full-layout")
      pendingResources.add("capacity-summary")
      refreshBookings = true
      invalidate()
      return
    }
    if (arg && typeof arg === "object") {
      const envelope = arg as Partial<RealtimeEventEnvelope>
      if (typeof envelope.siteId === "string" && envelope.siteId.toLowerCase() !== normalizedSiteId) return
      if (typeof envelope.eventId === "string" && envelope.eventId.length > 0) {
        const normalizedEnvelope: RealtimeEventEnvelope = {
          ...envelope,
          eventId: envelope.eventId,
        }
        const slotId = slotIdOf(normalizedEnvelope)
        if (!budget.guard.decide(normalizedEnvelope, slotId).apply) return
        budget.guard.commit(normalizedEnvelope, slotId)
        markAcceptedCurrentAttempt()
        pendingRefreshReason = "invalidation"
        const rawResources = envelope.payload && typeof envelope.payload === "object"
          ? (envelope.payload as { affectedResources?: unknown }).affectedResources
          : undefined
        if (Array.isArray(rawResources) && rawResources.length > 0) {
          let hasLayout = false
          for (const item of rawResources) {
            if (typeof item !== "string") continue
            const trimmed = item.trim()
            if (!trimmed) continue
            pendingResources.add(trimmed)
            if (trimmed === "bookings") {
              refreshBookings = true
            } else if (trimmed === "full-layout" || trimmed === "capacity-summary" || trimmed === "structure") {
              hasLayout = true
            }
          }
          if (hasLayout) pendingLayoutRefresh = true
        } else {
          pendingLayoutRefresh = true
          refreshBookings = true
        }
        invalidate()
        return
      }
    }
    markAcceptedCurrentAttempt()
    budget.guard.reset()
    pendingLayoutRefresh = true
    refreshBookings = true
    invalidate()
  }
  connection.on("SnapshotRequired", onSnapshotRequired)

  function clearRetry() {
    clearTimeout(retry)
    retry = undefined
  }

  function canScheduleProbe(): boolean {
    return !stopped &&
      !budget.blocked &&
      subscribers.size > 0 &&
      currentGeneration() === generation &&
      document.visibilityState !== "hidden" &&
      (typeof navigator === "undefined" || navigator.onLine !== false) &&
      authApi.expiresAt() > Date.now() + 30_000
  }

  function scheduleRetry(delay: number) {
    if (stopped || budget.blocked || retry !== undefined) return
    const now = Date.now()
    if (authApi.expiresAt() <= now + 30_000) {
      notifyState("unavailable")
      return
    }
    let wait: number
    if (budget.retryAttempts >= MAX_INITIAL_ATTEMPTS) {
      if (budget.nextAttemptAt <= now) {
        budget.nextAttemptAt = now + FOREGROUND_COOLDOWN_MS
      }
      notifyState("unavailable")
      wait = Math.max(0, budget.nextAttemptAt - now)
    } else {
      wait = budget.nextAttemptAt > now
        ? budget.nextAttemptAt - now
        : Math.min(60_000, delay * 2 ** Math.max(0, budget.retryAttempts - 1)) + Math.floor(Math.random() * 1000)
      budget.nextAttemptAt = now + wait
    }
    if (!canScheduleProbe()) return
    retry = setTimeout(() => {
      retry = undefined
      if (!canScheduleProbe()) return
      void connect()
    }, wait)
  }

  function canConnectNow(): boolean {
    return !stopped &&
      !budget.blocked &&
      subscribers.size > 0 &&
      currentGeneration() === generation &&
      connection.state === "Disconnected" &&
      !starting &&
      document.visibilityState !== "hidden" &&
      (typeof navigator === "undefined" || navigator.onLine !== false)
  }

  async function connect() {
    if (!canConnectNow()) {
      return
    }
    const initialNow = Date.now()
    if (initialNow < budget.nextAttemptAt) {
      notifyState("unavailable")
      scheduleRetry(FOREGROUND_COOLDOWN_MS)
      return
    }
    if (authApi.expiresAt() <= initialNow + 30_000) {
      try {
        const token = await ensureAccessToken()
        if (!canConnectNow()) {
          return
        }
        if (Date.now() < budget.nextAttemptAt) {
          notifyState("unavailable")
          scheduleRetry(FOREGROUND_COOLDOWN_MS)
          return
        }
        if (!token || authApi.expiresAt() <= Date.now() + 30_000) {
          notifyState("unavailable")
          return
        }
      } catch {
        if (!stopped && currentGeneration() === generation) {
          notifyState("unavailable")
        }
        return
      }
    }
    if (budget.blocked) {
      notifyState("unavailable")
      return
    }
    const now = Date.now()
    if (now < budget.nextAttemptAt) {
      notifyState("unavailable")
      scheduleRetry(FOREGROUND_COOLDOWN_MS)
      return
    }
    if (shutdown) {
      await shutdown
      if (!canConnectNow()) return
      if (Date.now() < budget.nextAttemptAt) {
        notifyState("unavailable")
        scheduleRetry(FOREGROUND_COOLDOWN_MS)
        return
      }
    }
    clearRetry()
    const attemptNow = Date.now()
    if (budget.windowStartedAt === 0 || attemptNow - budget.windowStartedAt >= FOREGROUND_COOLDOWN_MS) {
      budget.windowStartedAt = attemptNow
    }
    budget.retryAttempts++
    if (budget.retryAttempts >= MAX_INITIAL_ATTEMPTS) {
      budget.nextAttemptAt = attemptNow + FOREGROUND_COOLDOWN_MS
    }
    const attempt = ++attemptFence
    activeAttempt = attempt
    accepted = false
    acceptedAttempt = 0
    syncedAttempt = 0
    connectedAttempt = 0
    budget.acceptedAt = 0
    budget.lastSyncOutcome = "pending"
    notifyState("connecting")
    starting = connection.start().then(() => {
      if (stopped || attempt !== activeAttempt || currentGeneration() !== generation) return
      connectedAttempt = attempt
      budget.connectedAt = Date.now()
      budget.blocked = false
      budget.blockedReason = undefined
      completeReconciliationIfReady(attempt)
    }).catch((error: unknown) => {
      if (stopped || attempt !== activeAttempt || currentGeneration() !== generation) return
      activeAttempt = ++attemptFence
      accepted = false
      acceptedAttempt = 0
      syncedAttempt = 0
      connectedAttempt = 0
      notifyState("unavailable")
      const blockReason = failureBlockReason(error)
      if (blockReason) {
        budget.blocked = true
        budget.blockedReason = blockReason
        clearRetry()
        return
      }
      scheduleRetry(10_000)
    }).finally(() => { starting = undefined })
    await starting
  }

  // Reconnect after the next authenticated activity/refresh; never refresh solely
  // to keep an idle subscription alive. Server closes connections at token expiry.
  connection.onclose((error?: Error) => {
    const previousAttempt = activeAttempt
    activeAttempt = ++attemptFence
    const wasAccepted = accepted && acceptedAttempt === previousAttempt
    accepted = false
    acceptedAttempt = 0
    syncedAttempt = 0
    connectedAttempt = 0
    if (stopped || intentionalStop) {
      budget.acceptedAt = 0
      return
    }
    notifyState("unavailable")
    const blockReason = failureBlockReason(error)
    if (blockReason) {
      budget.acceptedAt = 0
      budget.blocked = true
      budget.blockedReason = blockReason
      clearRetry()
      return
    }
    if (wasAccepted) replenishIfHealthy(Date.now())
    budget.acceptedAt = 0
    scheduleRetry(2_000)
  })

  const resume = () => {
    if (stopped) return
    if (document.visibilityState === "hidden" ||
      (typeof navigator !== "undefined" && navigator.onLine === false)) {
      clearRetry()
      clearInvalidation()
      return
    }
    if (connection.state === "Disconnected") {
      if (!budget.blocked && Date.now() < budget.nextAttemptAt) {
        notifyState("unavailable")
        scheduleRetry(FOREGROUND_COOLDOWN_MS)
        return
      }
      void connect()
    } else if (dirty || (accepted && syncedAttempt !== activeAttempt)) {
      invalidate()
    }
  }
  const onAuthRefreshed = () => {
    if (stopped) return
    intentionalStop = false
    if (budget.blocked && budget.blockedReason === "auth") {
      budget.blocked = false
      budget.blockedReason = undefined
      budget.nextAttemptAt = 0
      budget.retryAttempts = 0
      budget.windowStartedAt = 0
    }
    resume()
  }
  const expire = () => {
    activeAttempt = ++attemptFence
    accepted = false
    acceptedAttempt = 0
    syncedAttempt = 0
    connectedAttempt = 0
    budget.acceptedAt = 0
    budget.blocked = true
    budget.blockedReason = "auth"
    clearRetry()
    notifyState("unavailable")
    intentionalStop = true
    void connection.stop().catch(() => {})
  }
  window.addEventListener(authRefreshedEvent, onAuthRefreshed)
  window.addEventListener(authExpiredEvent, expire)
  window.addEventListener("focus", resume)
  window.addEventListener("online", resume)
  window.addEventListener("offline", resume)
  document.addEventListener("visibilitychange", resume)

  return channel
}

export interface ReconcileOperatorSiteOptions<TSite extends { id: string }> {
  expectedSiteId: string
  isActive: () => boolean
  getCurrentSite: () => TSite | null | undefined
  refreshSite: (site: TSite) => Promise<TSite | null>
  applySite: (updated: TSite) => void
  onError: (error: unknown) => void
  onClearError?: () => void
  onBookingsInvalidated?: () => void
  onSyncSuccess?: (syncedAt: number) => void
  refreshBookings?: boolean
  details?: LayoutInvalidationDetails
}

export async function reconcileOperatorSiteSnapshot<TSite extends { id: string }>(
  options: ReconcileOperatorSiteOptions<TSite>,
): Promise<LayoutSyncOutcome> {
  const site = options.getCurrentSite()
  if (!options.isActive() || !site || site.id !== options.expectedSiteId) {
    return "stale"
  }
  if (options.refreshBookings) {
    options.onBookingsInvalidated?.()
  }
  if (options.details?.refreshLayout === false) {
    return "synced"
  }
  try {
    const updated = await options.refreshSite(site)
    const latestSite = options.getCurrentSite()
    if (!options.isActive() || !latestSite || latestSite.id !== options.expectedSiteId || updated === null) {
      return "stale"
    }
    options.applySite(updated)
    options.onClearError?.()
    options.onSyncSuccess?.(Date.now())
    return "synced"
  } catch (error) {
    if (!options.isActive() || options.getCurrentSite()?.id !== options.expectedSiteId) {
      return "stale"
    }
    options.onError(error)
    return "error"
  }
}

export function getOperatorLayoutSyncState(siteId: string): LayoutSyncDiagnostics | null {
  if (!UUID_PATTERN.test(siteId)) return null
  const generation = currentGeneration()
  const key = `${generation}:${siteId.toLowerCase()}`
  const channel = activeChannels.get(key)
  const budget = retryBudgets.get(key)
  if (!channel && !budget) return null
  return {
    transportState: channel?.state ?? (budget?.blocked ? "unavailable" : "connecting"),
    transportConnectedAt: budget?.connectedAt ?? 0,
    acceptedAt: budget?.acceptedAt ?? 0,
    lastSyncedAt: budget?.lastSyncedAt ?? 0,
    lastSyncOutcome: budget?.lastSyncOutcome ?? "pending",
    retryAttempts: budget?.retryAttempts ?? 0,
  }
}

export function watchOperatorLayout(
  siteId: string,
  onLayoutChanged: (refreshBookings?: boolean, details?: LayoutInvalidationDetails) => LayoutSubscriberResult | Promise<LayoutSubscriberResult>,
  onConnectionStateChange?: (state: LayoutConnectionState) => void,
  onSyncStateChange?: (diagnostics: LayoutSyncDiagnostics) => void,
): () => void {
  if (!UUID_PATTERN.test(siteId)) return () => {}

  const generation = currentGeneration()
  const normalizedSiteId = siteId.toLowerCase()
  const channelKey = `${generation}:${normalizedSiteId}`
  const subscriber: LayoutSubscriber = { onLayoutChanged, onConnectionStateChange, onSyncStateChange }

  let channel = activeChannels.get(channelKey)
  if (!channel) {
    const budget = budgetFor(generation, normalizedSiteId)
    channel = createSharedChannel(channelKey, siteId, generation, budget)
    channel.subscribers.add(subscriber)
    activeChannels.set(channelKey, channel)
    channel.start()
  } else {
    channel.subscribers.add(subscriber)
    onConnectionStateChange?.(channel.state)
    const diag = getOperatorLayoutSyncState(siteId)
    if (diag) onSyncStateChange?.(diag)
  }

  let unsubscribed = false
  return () => {
    if (unsubscribed) return
    unsubscribed = true
    channel.subscribers.delete(subscriber)
    if (channel.subscribers.size > 0) return
    if (activeChannels.get(channelKey) === channel) activeChannels.delete(channelKey)
    channel.stop()
  }
}
