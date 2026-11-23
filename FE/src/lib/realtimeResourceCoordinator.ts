export type RealtimeAudienceRole = "operator" | "owner" | "driver" | "guest"
export type RealtimeResourceClass = "full-layout" | "capacity-summary" | "sanitized-detail" | "structure" | "bookings"
export type RealtimeReadPriority = "high" | "normal"
export type RealtimeReadReason = "initial" | "reconnect" | "invalidation" | "mutation"
export type RealtimeReadOutcome = "synced" | "stale" | "error"

export const OWNER_MAX_SUBSCRIBED_SITES = 20
export const DRIVER_MAX_VISIBLE_SITES = 10
export const DRIVER_VIEWPORT_DEBOUNCE_MS = 400
export const FULL_LAYOUT_MIN_INTERVAL_MS = 2_000
export const MAX_READ_STARTS_PER_SECOND = 4
export const READ_START_WINDOW_MS = 1_000
export const MAX_CONCURRENT_READS = 4
export const MAX_INACTIVE_RESOURCES = 64
export const MAX_FAIRNESS_HIGH_STREAK = 3

export interface ResourceScopeKey {
  sessionGeneration: number
  role: RealtimeAudienceRole
  userId: string
  siteId: string
  resourceClass: RealtimeResourceClass
  filterKey?: string
}

export interface ResourceReadContext {
  reason: RealtimeReadReason
  signalKey: string
  sessionGeneration: number
}

export interface ExplicitReadOutcome<TData = unknown> {
  outcome: RealtimeReadOutcome
  data?: TData
  error?: unknown
}

export type ResourceFetcherResult<TData = unknown> = ExplicitReadOutcome<TData> | TData
export type ResourceFetcher<TData = unknown> = (
  context: ResourceReadContext,
) => ResourceFetcherResult<TData> | Promise<ResourceFetcherResult<TData>>

export interface ResourceSyncSnapshot<TData = unknown> {
  key: string
  lastOutcome: RealtimeReadOutcome | "pending"
  lastSyncedAt: number
  lastStartedAt: number
  inFlight: boolean
  dirty: boolean
  data?: TData
  lastError?: unknown
}

export interface CoordinatorMetrics {
  activeConcurrentReads: number
  totalReadsStarted: number
  totalCoalescedInvalidations: number
  lastQueueDelayMs: number
  maxQueueDelayMs: number
  trackedResources: number
}

export interface CoordinatorOptions {
  now?: () => number
  setTimer?: (fn: () => void, delayMs: number) => ReturnType<typeof setTimeout>
  clearTimer?: (handle: ReturnType<typeof setTimeout>) => void
  maxStartsPerSecond?: number
  maxConcurrentReads?: number
  fullLayoutMinIntervalMs?: number
  maxInactiveResources?: number
}

interface InternalResourceEntry<TData = unknown> {
  key: string
  scope: ResourceScopeKey
  normalizedSiteId: string
  fetcher: ResourceFetcher<TData>
  observers: Map<symbol, ((snapshot: ResourceSyncSnapshot<TData>) => void) | undefined>
  activeWaiters: Array<(snapshot: ResourceSyncSnapshot<TData>) => void>
  queuedWaiters: Array<(snapshot: ResourceSyncSnapshot<TData>) => void>
  inFlight: boolean
  dirty: boolean
  queuedPriority?: RealtimeReadPriority
  queuedReason?: RealtimeReadReason
  enqueuedAt: number
  lastStartedAt: number
  lastSyncedAt: number
  lastOutcome: RealtimeReadOutcome | "pending"
  data?: TData
  lastError?: unknown
  lastTouchedAt: number
}

export function buildResourceKey(scope: ResourceScopeKey): string {
  const userId = (scope.userId || "anonymous").trim().toLowerCase()
  const siteId = scope.siteId.trim().toLowerCase()
  const filterKey = (scope.filterKey ?? "default").trim() || "default"
  return `${scope.sessionGeneration}:${scope.role}:${userId}:${siteId}:${scope.resourceClass}:${filterKey}`
}

export function selectOwnerSubscriptionSites(
  visibleSiteIds: readonly string[],
  selectedSiteId?: string | null,
  limit = OWNER_MAX_SUBSCRIBED_SITES,
): string[] {
  const cap = Math.max(0, Math.floor(limit))
  const seen = new Set<string>()
  const result: string[] = []
  const addSite = (raw?: string | null) => {
    if (!raw || result.length >= cap) return
    const trimmed = raw.trim()
    if (!trimmed) return
    const normalized = trimmed.toLowerCase()
    if (seen.has(normalized)) return
    seen.add(normalized)
    result.push(trimmed)
  }

  addSite(selectedSiteId)
  for (const id of visibleSiteIds) addSite(id)
  return result
}

export function selectDriverSubscriptionSites(
  visibleSiteIds: readonly string[],
  selectedSiteId?: string | null,
  maxVisible = DRIVER_MAX_VISIBLE_SITES,
): { visibleSiteIds: string[]; selectedSiteId: string | null } {
  const cap = Math.max(0, Math.floor(maxVisible))
  const seen = new Set<string>()
  const visible: string[] = []
  for (const raw of visibleSiteIds) {
    if (visible.length >= cap) break
    const trimmed = raw?.trim()
    if (!trimmed) continue
    const normalized = trimmed.toLowerCase()
    if (seen.has(normalized)) continue
    seen.add(normalized)
    visible.push(trimmed)
  }
  const normalizedSelected = selectedSiteId?.trim() || null
  return { visibleSiteIds: visible, selectedSiteId: normalizedSelected }
}

export function createDriverViewportDebouncer(
  onCommit: (visibleSiteIds: string[], selectedSiteId: string | null) => void,
  delayMs = DRIVER_VIEWPORT_DEBOUNCE_MS,
  timerApi?: {
    setTimer?: (fn: () => void, delayMs: number) => ReturnType<typeof setTimeout>
    clearTimer?: (handle: ReturnType<typeof setTimeout>) => void
  },
) {
  const scheduleTimer = timerApi?.setTimer ?? ((fn: () => void, ms: number) => setTimeout(fn, ms))
  const cancelTimer = timerApi?.clearTimer ?? ((handle: ReturnType<typeof setTimeout>) => clearTimeout(handle))
  let timer: ReturnType<typeof setTimeout> | undefined
  let latestVisible: string[] = []
  let latestSelected: string | null = null

  const flush = () => {
    if (timer !== undefined) {
      cancelTimer(timer)
      timer = undefined
    }
    const selection = selectDriverSubscriptionSites(latestVisible, latestSelected)
    onCommit(selection.visibleSiteIds, selection.selectedSiteId)
  }

  return {
    schedule(visibleSiteIds: readonly string[], selectedSiteId?: string | null) {
      latestVisible = [...visibleSiteIds]
      latestSelected = selectedSiteId ?? null
      if (timer !== undefined) cancelTimer(timer)
      timer = scheduleTimer(() => {
        timer = undefined
        flush()
      }, delayMs)
    },
    flush,
    cancel() {
      if (timer !== undefined) {
        cancelTimer(timer)
        timer = undefined
      }
    },
  }
}

/**
 * Merges incoming live slot projection updates by slot ID without replacing
 * unsaved editable Owner structure forms or reloading cached 3D GLTF model assets.
 */
export function mergeLiveStructureProjection<
  TForm extends Record<string, unknown>,
  TSlot extends { id: string },
>(
  current: {
    formDraft: TForm
    modelUrl?: string | null
    slots: readonly TSlot[]
    lastSyncedAt?: number
  },
  incoming: {
    slots: readonly TSlot[]
    syncedAt?: number
  },
): {
  formDraft: TForm
  modelUrl?: string | null
  slots: TSlot[]
  lastSyncedAt: number
} {
  const existingById = new Map(current.slots.map(slot => [slot.id, slot]))
  const mergedSlots = incoming.slots.map(nextSlot => {
    const previous = existingById.get(nextSlot.id)
    return previous ? { ...previous, ...nextSlot } : { ...nextSlot }
  })
  return {
    formDraft: current.formDraft,
    modelUrl: current.modelUrl,
    slots: mergedSlots,
    lastSyncedAt: incoming.syncedAt ?? current.lastSyncedAt ?? 0,
  }
}

function isExplicitOutcome<TData>(value: unknown): value is ExplicitReadOutcome<TData> {
  if (!value || typeof value !== "object") return false
  const record = value as Record<string, unknown>
  if (record.outcome !== "synced" && record.outcome !== "stale" && record.outcome !== "error") return false
  return Object.keys(record).every(k => k === "outcome" || k === "data" || k === "error")
}

export class RealtimeResourceCoordinator {
  private readonly now: () => number
  private readonly scheduleTimer: (fn: () => void, delayMs: number) => ReturnType<typeof setTimeout>
  private readonly cancelTimer: (handle: ReturnType<typeof setTimeout>) => void
  private readonly maxStartsPerSecond: number
  private readonly maxConcurrentReads: number
  private readonly fullLayoutMinIntervalMs: number
  private readonly maxInactiveResources: number

  private sessionGeneration = 0
  private selectedSiteId: string | null = null
  private entries = new Map<string, InternalResourceEntry<unknown>>()
  private highQueue: string[] = []
  private normalQueue: string[] = []
  private startTimestamps: number[] = []
  private readonly activePermits = new Set<symbol>()
  private highStreak = 0
  private drainTimer: ReturnType<typeof setTimeout> | undefined
  private drainTimerTargetAt = 0
  private totalReadsStarted = 0
  private totalCoalescedInvalidations = 0
  private lastQueueDelayMs = 0
  private maxQueueDelayMs = 0

  constructor(options: CoordinatorOptions = {}) {
    this.now = options.now ?? (() => Date.now())
    this.scheduleTimer = options.setTimer ?? ((fn, delayMs) => setTimeout(fn, delayMs))
    this.cancelTimer = options.clearTimer ?? (handle => clearTimeout(handle))
    this.maxStartsPerSecond = options.maxStartsPerSecond ?? MAX_READ_STARTS_PER_SECOND
    this.maxConcurrentReads = options.maxConcurrentReads ?? MAX_CONCURRENT_READS
    this.fullLayoutMinIntervalMs = options.fullLayoutMinIntervalMs ?? FULL_LAYOUT_MIN_INTERVAL_MS
    this.maxInactiveResources = options.maxInactiveResources ?? MAX_INACTIVE_RESOURCES
  }

  setSessionGeneration(generation: number): void {
    if (generation === this.sessionGeneration) return
    this.sessionGeneration = generation
    const obsoleteKeys: string[] = []
    for (const [key, entry] of this.entries.entries()) {
      if (entry.scope.sessionGeneration !== generation) obsoleteKeys.push(key)
    }
    for (const key of obsoleteKeys) {
      const entry = this.entries.get(key)
      if (entry) this.resolveWaitersAsStale(entry)
      this.entries.delete(key)
    }
    this.highQueue = this.highQueue.filter(key => this.entries.has(key))
    this.normalQueue = this.normalQueue.filter(key => this.entries.has(key))
    if (this.drainTimer !== undefined) {
      this.cancelTimer(this.drainTimer)
      this.drainTimer = undefined
      this.drainTimerTargetAt = 0
    }
    if (this.highQueue.length > 0 || this.normalQueue.length > 0) {
      this.pump()
    }
  }

  setSelectedSiteId(siteId: string | null): void {
    this.selectedSiteId = siteId ? siteId.trim().toLowerCase() : null
  }

  registerResource<TData = unknown>(
    scope: ResourceScopeKey,
    fetcher: ResourceFetcher<TData>,
    onState?: (snapshot: ResourceSyncSnapshot<TData>) => void,
  ): () => void {
    if (scope.sessionGeneration !== this.sessionGeneration && scope.sessionGeneration > this.sessionGeneration) {
      this.setSessionGeneration(scope.sessionGeneration)
    }
    const key = buildResourceKey(scope)
    let entry = this.entries.get(key) as InternalResourceEntry<TData> | undefined
    if (!entry) {
      entry = {
        key,
        scope: { ...scope },
        normalizedSiteId: scope.siteId.trim().toLowerCase(),
        fetcher,
        observers: new Map(),
        activeWaiters: [],
        queuedWaiters: [],
        inFlight: false,
        dirty: false,
        enqueuedAt: 0,
        lastStartedAt: 0,
        lastSyncedAt: 0,
        lastOutcome: "pending",
        lastTouchedAt: this.now(),
      }
      this.entries.set(key, entry as InternalResourceEntry<unknown>)
    } else {
      entry.lastTouchedAt = this.now()
    }

    const token = Symbol("observer")
    entry.observers.set(token, onState)
    this.evictInactiveIfNeeded()

    if (onState && (entry.lastOutcome !== "pending" || entry.inFlight)) {
      onState(this.toSnapshot(entry))
    }

    return () => {
      const existing = this.entries.get(key)
      if (!existing) return
      existing.observers.delete(token)
      existing.lastTouchedAt = this.now()
      if (existing.observers.size === 0 && !existing.inFlight) {
        existing.dirty = false
        existing.queuedPriority = undefined
        existing.queuedReason = undefined
        this.resolveQueuedWaitersAsStale(existing)
      }
      this.evictInactiveIfNeeded()
    }
  }

  reconcileResource<TData = unknown>(
    scope: ResourceScopeKey,
    options: { priority?: RealtimeReadPriority; reason?: RealtimeReadReason } = {},
  ): Promise<ResourceSyncSnapshot<TData>> {
    const key = buildResourceKey(scope)
    const entry = this.entries.get(key) as InternalResourceEntry<TData> | undefined
    if (!entry || entry.observers.size === 0 || scope.sessionGeneration !== this.sessionGeneration) {
      return Promise.resolve({
        key,
        lastOutcome: "stale",
        lastSyncedAt: entry?.lastSyncedAt ?? 0,
        lastStartedAt: entry?.lastStartedAt ?? 0,
        inFlight: false,
        dirty: false,
      })
    }

    const reason = options.reason ?? "invalidation"
    if (reason === "initial" && entry.inFlight && !entry.dirty) {
      return new Promise<ResourceSyncSnapshot<TData>>(resolve => {
        entry.activeWaiters.push(resolve)
      })
    }

    return new Promise<ResourceSyncSnapshot<TData>>(resolve => {
      entry.queuedWaiters.push(resolve)
      this.invalidateResource(scope, options)
      if (
        this.entries.get(key) === entry &&
        !entry.inFlight &&
        !entry.dirty &&
        entry.queuedWaiters.includes(resolve)
      ) {
        entry.queuedWaiters = entry.queuedWaiters.filter(w => w !== resolve)
        resolve({ ...this.toSnapshot(entry), lastOutcome: "stale" })
      }
    })
  }

  invalidateResource(
    scope: ResourceScopeKey,
    options: { priority?: RealtimeReadPriority; reason?: RealtimeReadReason } = {},
  ): void {
    const key = buildResourceKey(scope)
    const entry = this.entries.get(key)
    const reason = options.reason ?? "invalidation"
    const priority = options.priority ?? this.defaultPriorityFor(scope.resourceClass, reason)
    if (entry) {
      this.enqueueEntry(entry, priority, reason)
    }

    // Selected-detail amendment: summary-only invalidation for the selected site also dirties
    // its active sanitized-detail (Driver) or full-layout (Owner detail) projection.
    if (scope.resourceClass === "capacity-summary") {
      const normalizedSiteId = scope.siteId.trim().toLowerCase()
      if (this.selectedSiteId && normalizedSiteId === this.selectedSiteId) {
        const prefix = `${scope.sessionGeneration}:${scope.role}:${(scope.userId || "anonymous").trim().toLowerCase()}:${normalizedSiteId}:`
        for (const candidate of this.entries.values()) {
          if (
            candidate.key.startsWith(prefix) &&
            (candidate.scope.resourceClass === "sanitized-detail" || candidate.scope.resourceClass === "full-layout") &&
            candidate.observers.size > 0
          ) {
            this.enqueueEntry(candidate, "high", reason)
          }
        }
      }
    }

    this.pump()
  }

  getResourceState<TData = unknown>(scope: ResourceScopeKey): ResourceSyncSnapshot<TData> | null {
    const entry = this.entries.get(buildResourceKey(scope)) as InternalResourceEntry<TData> | undefined
    return entry ? this.toSnapshot(entry) : null
  }

  getMetrics(): CoordinatorMetrics {
    return {
      activeConcurrentReads: this.activePermits.size,
      totalReadsStarted: this.totalReadsStarted,
      totalCoalescedInvalidations: this.totalCoalescedInvalidations,
      lastQueueDelayMs: this.lastQueueDelayMs,
      maxQueueDelayMs: this.maxQueueDelayMs,
      trackedResources: this.entries.size,
    }
  }

  clearAll(): void {
    if (this.drainTimer !== undefined) {
      this.cancelTimer(this.drainTimer)
      this.drainTimer = undefined
      this.drainTimerTargetAt = 0
    }
    for (const entry of this.entries.values()) {
      this.resolveWaitersAsStale(entry)
    }
    this.highQueue = []
    this.normalQueue = []
    this.entries.clear()
    this.pruneWindow(this.now())
  }

  dispose(): void {
    this.clearAll()
  }

  private defaultPriorityFor(resourceClass: RealtimeResourceClass, reason: RealtimeReadReason): RealtimeReadPriority {
    if (reason === "initial" || reason === "reconnect" || reason === "mutation") return "high"
    if (resourceClass === "sanitized-detail" || resourceClass === "full-layout") return "high"
    return "normal"
  }

  private enqueueEntry(entry: InternalResourceEntry<unknown>, priority: RealtimeReadPriority, reason: RealtimeReadReason): void {
    entry.lastTouchedAt = this.now()
    if (entry.observers.size === 0) return

    if (entry.inFlight || entry.queuedPriority !== undefined) {
      this.totalCoalescedInvalidations++
    }
    entry.dirty = true
    if (entry.queuedPriority === undefined) {
      entry.enqueuedAt = this.now()
    }
    if (priority === "high" || entry.queuedPriority !== "high") {
      entry.queuedPriority = priority
    }
    entry.queuedReason = reason
    if (entry.inFlight) return

    if (entry.queuedPriority === "high") {
      this.normalQueue = this.normalQueue.filter(k => k !== entry.key)
      if (!this.highQueue.includes(entry.key)) this.highQueue.push(entry.key)
    } else if (!this.highQueue.includes(entry.key) && !this.normalQueue.includes(entry.key)) {
      this.normalQueue.push(entry.key)
    }
  }

  private pruneWindow(now: number): void {
    const cutoff = now - READ_START_WINDOW_MS
    while (this.startTimestamps.length > 0 && this.startTimestamps[0] <= cutoff) {
      this.startTimestamps.shift()
    }
  }

  private earliestAllowedTime(entry: InternalResourceEntry<unknown>, now: number): number {
    this.pruneWindow(now)
    let allowedAt = now
    if (this.startTimestamps.length >= this.maxStartsPerSecond) {
      allowedAt = Math.max(allowedAt, this.startTimestamps[0] + READ_START_WINDOW_MS)
    }
    if (
      (entry.scope.resourceClass === "full-layout" || entry.scope.resourceClass === "sanitized-detail") &&
      entry.queuedReason === "invalidation" &&
      entry.lastStartedAt > 0
    ) {
      allowedAt = Math.max(allowedAt, entry.lastStartedAt + this.fullLayoutMinIntervalMs)
    }
    return allowedAt
  }

  private schedulePumpAt(targetTime: number): void {
    if (this.drainTimer !== undefined && this.drainTimerTargetAt > 0 && this.drainTimerTargetAt <= targetTime) {
      return
    }
    if (this.drainTimer !== undefined) {
      this.cancelTimer(this.drainTimer)
      this.drainTimer = undefined
      this.drainTimerTargetAt = 0
    }
    const delay = Math.max(1, targetTime - this.now())
    this.drainTimerTargetAt = this.now() + delay
    this.drainTimer = this.scheduleTimer(() => {
      this.drainTimer = undefined
      this.drainTimerTargetAt = 0
      this.pump()
    }, delay)
  }

  private dequeueReadyEntry(now: number): { entry: InternalResourceEntry<unknown> | null; nextWaitAt: number | null } {
    const pickFrom = (queue: string[]): { entry: InternalResourceEntry<unknown> | null; waitAt: number | null } => {
      let earliestWait: number | null = null
      for (let i = 0; i < queue.length; i++) {
        const key = queue[i]
        const entry = this.entries.get(key)
        if (!entry || entry.observers.size === 0 || entry.inFlight || !entry.dirty) {
          queue.splice(i, 1)
          i--
          continue
        }
        const allowedAt = this.earliestAllowedTime(entry, now)
        if (allowedAt <= now) {
          queue.splice(i, 1)
          return { entry, waitAt: null }
        }
        earliestWait = earliestWait === null ? allowedAt : Math.min(earliestWait, allowedAt)
      }
      return { entry: null, waitAt: earliestWait }
    }

    if (this.highStreak >= MAX_FAIRNESS_HIGH_STREAK && this.normalQueue.length > 0) {
      const normalCandidate = pickFrom(this.normalQueue)
      if (normalCandidate.entry) {
        this.highStreak = 0
        return { entry: normalCandidate.entry, nextWaitAt: null }
      }
    }

    const highCandidate = pickFrom(this.highQueue)
    if (highCandidate.entry) {
      this.highStreak++
      return { entry: highCandidate.entry, nextWaitAt: null }
    }

    const normalCandidate = pickFrom(this.normalQueue)
    if (normalCandidate.entry) {
      this.highStreak = 0
      return { entry: normalCandidate.entry, nextWaitAt: null }
    }

    const waits = [highCandidate.waitAt, normalCandidate.waitAt].filter((v): v is number => v !== null)
    return { entry: null, nextWaitAt: waits.length > 0 ? Math.min(...waits) : null }
  }

  private pump(): void {
    while (this.activePermits.size < this.maxConcurrentReads) {
      const now = this.now()
      const { entry, nextWaitAt } = this.dequeueReadyEntry(now)
      if (!entry) {
        if (nextWaitAt !== null) this.schedulePumpAt(nextWaitAt)
        break
      }
      this.startEntryRead(entry, now)
    }
  }

  private startEntryRead(entry: InternalResourceEntry<unknown>, now: number): void {
    const queueDelay = entry.enqueuedAt > 0 ? Math.max(0, now - entry.enqueuedAt) : 0
    this.lastQueueDelayMs = queueDelay
    if (queueDelay > this.maxQueueDelayMs) this.maxQueueDelayMs = queueDelay

    entry.dirty = false
    entry.inFlight = true
    entry.queuedPriority = undefined
    const reason = entry.queuedReason ?? "invalidation"
    entry.queuedReason = undefined
    entry.enqueuedAt = 0
    entry.lastStartedAt = now
    if (entry.queuedWaiters.length > 0) {
      entry.activeWaiters.push(...entry.queuedWaiters.splice(0, entry.queuedWaiters.length))
    }
    const permit = Symbol("read-permit")
    this.startTimestamps.push(now)
    this.totalReadsStarted++
    this.activePermits.add(permit)
    this.emitState(entry)

    const releasePermit = () => {
      this.activePermits.delete(permit)
    }

    const generationAtStart = entry.scope.sessionGeneration
    const readContext: ResourceReadContext = {
      reason,
      signalKey: entry.key,
      sessionGeneration: generationAtStart,
    }

    Promise.resolve()
      .then(() => entry.fetcher(readContext))
      .then(
        rawResult => {
          releasePermit()
          entry.inFlight = false
          if (generationAtStart !== this.sessionGeneration || this.entries.get(entry.key) !== entry || entry.observers.size === 0) {
            if (entry.observers.size === 0) entry.dirty = false
            this.resolveActiveWaitersAsStale(entry)
            if (entry.observers.size === 0) this.resolveQueuedWaitersAsStale(entry)
            this.evictInactiveIfNeeded()
            this.pump()
            return
          }

          if (isExplicitOutcome(rawResult)) {
            if (rawResult.outcome === "error") {
              entry.lastOutcome = "error"
              entry.lastError = rawResult.error
            } else if (rawResult.outcome === "stale") {
              entry.lastOutcome = "stale"
            } else {
              entry.lastOutcome = "synced"
              entry.lastError = undefined
              entry.lastSyncedAt = this.now()
              if ("data" in rawResult) entry.data = rawResult.data
            }
          } else {
            entry.lastOutcome = "synced"
            entry.lastError = undefined
            entry.lastSyncedAt = this.now()
            entry.data = rawResult
          }

          this.emitState(entry)
          this.resolveActiveWaiters(entry, this.toSnapshot(entry))
          if (entry.dirty && entry.observers.size > 0) {
            this.enqueueEntry(entry, entry.queuedPriority ?? "normal", entry.queuedReason ?? "invalidation")
          }
          this.pump()
        },
        error => {
          releasePermit()
          entry.inFlight = false
          if (generationAtStart !== this.sessionGeneration || this.entries.get(entry.key) !== entry || entry.observers.size === 0) {
            if (entry.observers.size === 0) entry.dirty = false
            this.resolveActiveWaitersAsStale(entry)
            if (entry.observers.size === 0) this.resolveQueuedWaitersAsStale(entry)
            this.evictInactiveIfNeeded()
            this.pump()
            return
          }

          entry.lastOutcome = "error"
          entry.lastError = error
          this.emitState(entry)
          this.resolveActiveWaiters(entry, this.toSnapshot(entry))
          if (entry.dirty && entry.observers.size > 0) {
            this.enqueueEntry(entry, entry.queuedPriority ?? "normal", entry.queuedReason ?? "invalidation")
          }
          this.pump()
        },
      )
  }

  private resolveActiveWaiters(entry: InternalResourceEntry<unknown>, snapshot: ResourceSyncSnapshot<unknown>): void {
    if (entry.activeWaiters.length === 0) return
    const waiters = entry.activeWaiters.splice(0, entry.activeWaiters.length)
    for (const resolve of waiters) resolve(snapshot)
  }

  private resolveActiveWaitersAsStale(entry: InternalResourceEntry<unknown>): void {
    if (entry.activeWaiters.length === 0) return
    this.resolveActiveWaiters(entry, { ...this.toSnapshot(entry), lastOutcome: "stale" })
  }

  private resolveQueuedWaitersAsStale(entry: InternalResourceEntry<unknown>): void {
    if (entry.queuedWaiters.length === 0) return
    const snapshot = { ...this.toSnapshot(entry), lastOutcome: "stale" as const }
    const waiters = entry.queuedWaiters.splice(0, entry.queuedWaiters.length)
    for (const resolve of waiters) resolve(snapshot)
  }

  private resolveWaitersAsStale(entry: InternalResourceEntry<unknown>): void {
    this.resolveActiveWaitersAsStale(entry)
    this.resolveQueuedWaitersAsStale(entry)
  }

  private emitState(entry: InternalResourceEntry<unknown>): void {
    const snapshot = this.toSnapshot(entry)
    for (const observer of entry.observers.values()) observer?.(snapshot)
  }

  private toSnapshot<TData>(entry: InternalResourceEntry<TData>): ResourceSyncSnapshot<TData> {
    return {
      key: entry.key,
      lastOutcome: entry.lastOutcome,
      lastSyncedAt: entry.lastSyncedAt,
      lastStartedAt: entry.lastStartedAt,
      inFlight: entry.inFlight,
      dirty: entry.dirty,
      data: entry.data,
      lastError: entry.lastError,
    }
  }

  private evictInactiveIfNeeded(): void {
    const inactive: InternalResourceEntry<unknown>[] = []
    for (const entry of this.entries.values()) {
      if (entry.observers.size === 0 && !entry.inFlight && !entry.dirty) {
        inactive.push(entry)
      }
    }
    if (inactive.length <= this.maxInactiveResources) return
    inactive.sort((a, b) => a.lastTouchedAt - b.lastTouchedAt)
    const toRemove = inactive.length - this.maxInactiveResources
    for (let i = 0; i < toRemove; i++) {
      this.entries.delete(inactive[i].key)
    }
  }
}

export const sharedRealtimeCoordinator = new RealtimeResourceCoordinator()
