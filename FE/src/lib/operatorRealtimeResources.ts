import { authExpiredEvent, getSessionGeneration } from "./authApi"
import { loadOwnerModelUrl } from "./3d/ownerModelStorage"
import {
  loadOperatorSites,
  readAssignedOperatorSite,
  reservationApi,
  type OperatorSite,
  type ParkingSessionHistoryEntry,
} from "./operatorApi"
import { operatorsApi } from "./parkingApi"
import {
  FULL_LAYOUT_MIN_INTERVAL_MS,
  sharedRealtimeCoordinator,
  type CoordinatorMetrics,
  type RealtimeAudienceRole,
  type RealtimeReadReason,
  type RealtimeResourceClass,
  type ResourceScopeKey,
  type ResourceSyncSnapshot,
} from "./realtimeResourceCoordinator"
import type { Booking, User } from "./types"

export interface OperatorSiteMeta {
  tenantId: string
  permissions: string[]
  modelUrl?: string
  modelFileName?: string
}

let activeRole: RealtimeAudienceRole = "operator"
let activeUserId = "anonymous"
let cacheEpoch = 0
const siteAssignmentMeta = new Map<string, OperatorSiteMeta>()
const INITIAL_CONNECT_REUSE_WINDOW_MS = FULL_LAYOUT_MIN_INTERVAL_MS

function currentGeneration(): number {
  return typeof getSessionGeneration === "function" ? getSessionGeneration() : 0
}

function normalizeSiteId(siteId: string): string {
  return siteId.trim().toLowerCase()
}

function metaKey(generation: number, siteId: string): string {
  return `${generation}:${normalizeSiteId(siteId)}`
}

function pruneMetaForGeneration(generation: number): void {
  const prefix = `${generation}:`
  for (const key of siteAssignmentMeta.keys()) {
    if (!key.startsWith(prefix)) siteAssignmentMeta.delete(key)
  }
}

function syncCoordinatorGeneration(): number {
  const generation = currentGeneration()
  sharedRealtimeCoordinator.setSessionGeneration(generation)
  pruneMetaForGeneration(generation)
  return generation
}

function buildScope(
  siteId: string,
  resourceClass: RealtimeResourceClass,
  filterKey?: string,
): ResourceScopeKey {
  const generation = syncCoordinatorGeneration()
  return {
    sessionGeneration: generation,
    role: activeRole,
    userId: activeUserId || "anonymous",
    siteId: normalizeSiteId(siteId),
    resourceClass,
    filterKey,
  }
}

function readMeta(siteId: string): OperatorSiteMeta {
  const generation = currentGeneration()
  return siteAssignmentMeta.get(metaKey(generation, siteId)) ?? {
    tenantId: "",
    permissions: [],
  }
}

async function fetchOperatorLayoutResource(siteId: string): Promise<OperatorSite> {
  const epochAtStart = cacheEpoch
  const meta = readMeta(siteId)
  const loaded = await readAssignedOperatorSite({
    siteId,
    tenantId: meta.tenantId,
    permissions: meta.permissions,
  })
  const latestMeta = epochAtStart === cacheEpoch ? readMeta(siteId) : { tenantId: "", permissions: [] }
  const preservedModelUrl = latestMeta.modelUrl ?? loaded.modelUrl
  const preservedModelFileName = latestMeta.modelFileName ?? loaded.modelFileName
  if (epochAtStart === cacheEpoch && preservedModelUrl && !latestMeta.modelUrl) {
    registerOperatorSiteMeta(siteId, {
      modelUrl: preservedModelUrl,
      modelFileName: preservedModelFileName,
    })
  }
  return {
    ...loaded,
    modelUrl: preservedModelUrl,
    modelFileName: preservedModelFileName,
  }
}

function ensureLayoutObserver(
  siteId: string,
  onState?: (snapshot: ResourceSyncSnapshot<OperatorSite>) => void,
): () => void {
  const scope = buildScope(siteId, "full-layout", "operator-site")
  return sharedRealtimeCoordinator.registerResource<OperatorSite>(
    scope,
    () => fetchOperatorLayoutResource(siteId),
    onState,
  )
}

export function registerOperatorRealtimeIdentity(
  role: RealtimeAudienceRole,
  userId: string,
): void {
  const nextRole = role || "operator"
  const nextUser = userId.trim() || "anonymous"
  const changed = activeRole !== nextRole || activeUserId !== nextUser
  activeRole = nextRole
  activeUserId = nextUser
  if (changed) {
    clearOperatorRealtimeResources()
  }
}

export function registerOperatorSiteMeta(
  siteId: string,
  meta: Partial<OperatorSiteMeta>,
): void {
  const generation = syncCoordinatorGeneration()
  const key = metaKey(generation, siteId)
  const existing = siteAssignmentMeta.get(key)
  siteAssignmentMeta.set(key, {
    tenantId: meta.tenantId ?? existing?.tenantId ?? "",
    permissions: meta.permissions ?? existing?.permissions ?? [],
    modelUrl: meta.modelUrl ?? existing?.modelUrl,
    modelFileName: meta.modelFileName ?? existing?.modelFileName,
  })
}

export function subscribeOperatorLayout(
  siteId: string,
  onState: (snapshot: ResourceSyncSnapshot<OperatorSite>) => void,
): () => void {
  if (!siteId) return () => {}
  return ensureLayoutObserver(siteId, onState)
}

export async function reconcileOperatorLayout(
  siteId: string,
  reason: RealtimeReadReason = "invalidation",
  options: { force?: boolean } = {},
): Promise<ResourceSyncSnapshot<OperatorSite>> {
  const scope = buildScope(siteId, "full-layout", "operator-site")
  sharedRealtimeCoordinator.setSelectedSiteId(siteId)
  if (!options.force && reason === "initial") {
    const existing = sharedRealtimeCoordinator.getResourceState<OperatorSite>(scope)
    if (
      existing &&
      existing.lastOutcome === "synced" &&
      existing.data &&
      !existing.dirty &&
      Date.now() - existing.lastSyncedAt < INITIAL_CONNECT_REUSE_WINDOW_MS
    ) {
      return existing
    }
  }
  const unsub = ensureLayoutObserver(siteId)
  try {
    return await sharedRealtimeCoordinator.reconcileResource<OperatorSite>(scope, {
      reason,
      priority: "high",
    })
  } finally {
    unsub()
  }
}

export function invalidateOperatorLayout(
  siteId: string,
  reason: RealtimeReadReason = "mutation",
): void {
  if (!siteId) return
  const scope = buildScope(siteId, "full-layout", "operator-site")
  sharedRealtimeCoordinator.invalidateResource(scope, {
    reason,
    priority: "high",
  })
}

export function subscribeOperatorBookings(
  siteId: string,
  onState: (snapshot: ResourceSyncSnapshot<Booking[]>) => void,
): () => void {
  if (!siteId) return () => {}
  const scope = buildScope(siteId, "bookings", "reservations")
  return sharedRealtimeCoordinator.registerResource<Booking[]>(
    scope,
    () => reservationApi.list(siteId),
    onState,
  )
}

export function invalidateOperatorBookings(
  siteId: string,
  reason: RealtimeReadReason = "invalidation",
): void {
  if (!siteId) return
  const scope = buildScope(siteId, "bookings", "reservations")
  if (reason === "initial") {
    const existing = sharedRealtimeCoordinator.getResourceState<Booking[]>(scope)
    if (
      existing &&
      existing.lastOutcome === "synced" &&
      existing.data &&
      !existing.dirty &&
      Date.now() - existing.lastSyncedAt < INITIAL_CONNECT_REUSE_WINDOW_MS
    ) {
      return
    }
  }
  sharedRealtimeCoordinator.invalidateResource(scope, {
    reason,
    priority: reason === "mutation" ? "high" : "normal",
  })
}

export function subscribeOperatorParkingSessions(
  siteId: string,
  onState: (snapshot: ResourceSyncSnapshot<ParkingSessionHistoryEntry[]>) => void,
): () => void {
  if (!siteId) return () => {}
  const scope = buildScope(siteId, "bookings", "parking-sessions")
  return sharedRealtimeCoordinator.registerResource<ParkingSessionHistoryEntry[]>(
    scope,
    () => reservationApi.listParkingSessions(siteId),
    onState,
  )
}

export function invalidateOperatorParkingSessions(
  siteId: string,
  reason: RealtimeReadReason = "invalidation",
): void {
  if (!siteId) return
  const scope = buildScope(siteId, "bookings", "parking-sessions")
  if (reason === "initial") {
    const existing = sharedRealtimeCoordinator.getResourceState<ParkingSessionHistoryEntry[]>(scope)
    // Another initial observer can reuse the pending canonical read. Leave any
    // mutation/event-driven trailing read intact rather than adding initial work.
    if (existing?.inFlight) return
    if (
      existing &&
      existing.lastOutcome === "synced" &&
      existing.data &&
      !existing.dirty &&
      Date.now() - existing.lastSyncedAt < INITIAL_CONNECT_REUSE_WINDOW_MS
    ) {
      return
    }
  }
  sharedRealtimeCoordinator.invalidateResource(scope, {
    reason,
    priority: reason === "mutation" ? "high" : "normal",
  })
}

export function invalidateOperatorGateResources(
  siteId: string,
  reason: RealtimeReadReason = "mutation",
  options: { includeLayout?: boolean } = {},
): void {
  if (!siteId) return
  if (options.includeLayout !== false) {
    invalidateOperatorLayout(siteId, reason)
  }
  invalidateOperatorBookings(siteId, reason)
  invalidateOperatorParkingSessions(siteId, reason)
}

export async function reconcileOperatorBookingLookup(
  siteId: string,
  referenceId: string,
  reason: RealtimeReadReason = "invalidation",
): Promise<ResourceSyncSnapshot<Booking>> {
  const scope = buildScope(siteId, "bookings", `lookup:${referenceId}`)
  const unsub = sharedRealtimeCoordinator.registerResource<Booking>(
    scope,
    () => reservationApi.lookup(siteId, referenceId),
  )
  try {
    return await sharedRealtimeCoordinator.reconcileResource<Booking>(scope, {
      reason,
      priority: "high",
    })
  } finally {
    unsub()
  }
}

export async function loadAssignedOperatorSitesCoordinated(
  user: User | null,
  options: { force?: boolean } = {},
): Promise<OperatorSite[]> {
  if (!user) return []
  registerOperatorRealtimeIdentity(
    user.role === "owner" || user.role === "driver" || user.role === "operator" ? user.role : "operator",
    user.id,
  )
  if (user.role !== "operator") {
    const sites = await loadOperatorSites(user)
    for (const site of sites) {
      registerOperatorSiteMeta(site.id, {
        tenantId: site.tenantId,
        permissions: site.assignmentPermissions ?? [],
        modelUrl: site.modelUrl,
        modelFileName: site.modelFileName,
      })
    }
    return sites
  }

  const assignments = await operatorsApi.assignments()
  if (!Array.isArray(assignments)) {
    throw new Error("The assignment service returned an invalid site list. Please try again.")
  }
  if (assignments.length === 0) return []

  const results = await Promise.allSettled(
    assignments.map(async (assignment) => {
      const savedModelUrl = await loadOwnerModelUrl(assignment.siteId).catch(() => null)
      registerOperatorSiteMeta(assignment.siteId, {
        tenantId: assignment.tenantId,
        permissions: assignment.permissions,
        modelUrl: savedModelUrl ?? undefined,
      })
      const snapshot = await reconcileOperatorLayout(assignment.siteId, "initial", { force: options.force === true })
      if (snapshot.lastOutcome === "error") {
        throw snapshot.lastError instanceof Error
          ? snapshot.lastError
          : new Error("Assigned parking site layout could not be loaded.")
      }
      if (!snapshot.data) {
        throw new Error("Assigned parking site layout was unavailable.")
      }
      return snapshot.data
    }),
  )

  const available = results.flatMap((result) =>
    result.status === "fulfilled" ? [result.value] : [],
  )
  if (results.length > 0 && available.length === 0) {
    const firstFailure = results.find((result) => result.status === "rejected")
    throw firstFailure?.status === "rejected"
      ? firstFailure.reason
      : new Error("Assigned parking sites are unavailable.")
  }
  return available
}

export function clearOperatorRealtimeResources(): void {
  cacheEpoch++
  siteAssignmentMeta.clear()
  sharedRealtimeCoordinator.clearAll()
}

export function getOperatorRealtimeMetrics(): CoordinatorMetrics {
  return sharedRealtimeCoordinator.getMetrics()
}

if (typeof window !== "undefined") {
  window.addEventListener(authExpiredEvent, clearOperatorRealtimeResources)
  ;(window as Window & { __smartparkRealtimeMetrics?: () => CoordinatorMetrics }).__smartparkRealtimeMetrics =
    getOperatorRealtimeMetrics
}
