import { useCallback, useEffect, useMemo, useRef, useState } from "react"
import {
  reservationApi,
  type DemoSlotSession,
  type OperatorSite,
  type OperatorSlot,
  type ParkingSessionHistoryEntry,
} from "../../../lib/operatorApi"
import { AuthApiError } from "../../../lib/authApi"
import type { LayoutConnectionState, LayoutSyncOutcome } from "../../../lib/operatorLayoutLive"
import {
  invalidateOperatorGateResources,
  invalidateOperatorParkingSessions,
  reconcileOperatorLayout,
  subscribeOperatorLayout,
  subscribeOperatorParkingSessions,
} from "../../../lib/operatorRealtimeResources"
import type { LotType, SlotStatus } from "../../../lib/types"
import {
  formatSlotLabel,
  isLayoutStale,
  layoutAgeSeconds,
  mapSlotVisualState,
  type OperationalLayoutProjection,
  type PhysicalState,
  type ReservationState,
  type SlotVisualState,
  type SlotVisualStateDto,
} from "../../../lib/operationalLayout"

/**
 * Maps a numeric floor selection to the GLB node key used by the 3D model's
 * floor-isolation pass. `"all"` and lot types without a modelled floor layout
 * resolve to `"all"` so nothing is hidden.
 */
export function floorKeyFor(lotType: LotType | undefined | null, floor: number | "all"): string {
  if (floor === "all") return "all"
  if (lotType === "basement") return floor === 1 ? "B1" : floor === 2 ? "B2" : `B${floor}`
  if (lotType === "multi-storey") return floor === 1 ? "G" : floor === 2 ? "L1" : floor === 3 ? "L2" : `L${floor - 1}`
  return "all"
}

/**
 * Kept as the legacy six-value vocabulary so existing callers keep compiling.
 * `LotVisualFilter` adds the fail-closed states used by this screen's legend and
 * filter.
 */
export type SlotStatusFilter =
  | "all"
  | "available"
  | "occupied"
  | "reserved"
  | "backup"
  | "maintenance"

export type LotVisualFilter =
  | SlotStatusFilter
  | "unknown"
  | "unavailable"

type ViewMode = "3d" | "grid" | "list"
export type DemoVehicle = {
  plate: string
  imageUrl: string
  session: DemoSlotSession
}

export type PendingCheckIn = {
  plate: string
  imageUrl?: string
  vehicleType?: string
  session?: DemoSlotSession
  slotId?: string
}

function toDemoSession(session: ParkingSessionHistoryEntry): DemoSlotSession {
  const plate = session.originalPlate ?? session.normalizedPlate ?? ""
  return {
    id: session.id,
    reservationId: session.reservationId,
    originalPlate: plate,
    normalizedPlate: session.normalizedPlate ?? plate.replace(/[^a-zA-Z0-9]/g, "").toUpperCase(),
    vehicleType: session.vehicleType,
    ticketReference: session.ticketReference ?? `SESSION-${session.id.slice(0, 8).toUpperCase()}`,
    entryTime: session.entryTime,
    exitTime: session.exitTime,
    status: session.status,
    entryImageUrl: session.entryImageUrl,
    exitImageUrl: session.exitImageUrl,
  }
}

function pendingCheckInStorageKey(siteId: string) {
  return `smartpark:demo-pending-checkins:${siteId}`
}

function acceptsDemoVehicle(slot: OperatorSlot, vehicleType?: string) {
  return vehicleType !== "CAR" || slot.vehicleType === "CAR" || slot.vehicleType === "ANY"
}

function demoVehicleImage(index: number) {
  const colors = ["#38bdf8", "#a78bfa", "#34d399", "#fbbf24", "#fb7185"]
  const color = colors[index % colors.length]
  return `data:image/svg+xml,${encodeURIComponent(`<svg xmlns="http://www.w3.org/2000/svg" width="640" height="360"><rect width="640" height="360" fill="#111827"/><rect x="34" y="34" width="572" height="292" rx="18" fill="${color}" opacity=".22"/><text x="320" y="180" fill="white" text-anchor="middle" font-family="Arial" font-size="24">DEMO CAMERA CAPTURE</text></svg>`)}`
}

function createDemoSession(plate: string, vehicleType: string, imageUrl: string): DemoSlotSession {
  const now = new Date().toISOString()
  return {
    id: `demo-session-${crypto.randomUUID()}`,
    originalPlate: plate,
    normalizedPlate: plate.replace(/[^a-zA-Z0-9]/g, "").toUpperCase(),
    vehicleType,
    ticketReference: `WALKIN-${plate.replace(/[^a-zA-Z0-9]/g, "").toUpperCase()}`,
    entryTime: now,
    exitTime: null,
    status: "ACTIVE",
    entryImageUrl: imageUrl,
    exitImageUrl: null,
  }
}

/**
 * The data lane attaches the operational snapshot to the operator site. Both
 * `operationalLayout` and `layout` are accepted so this screen keeps working
 * while the data layer and the visual layer land in parallel.
 */
type LayoutAwareSite = OperatorSite & {
  operationalLayout?: OperationalLayoutProjection | null
  layout?: OperationalLayoutProjection | null
}

type AxesAwareSlot = OperatorSlot & {
  physicalState?: PhysicalState | null
  slotCode?: string | null
}

export interface SlotAxes {
  physicalState: PhysicalState
  reservationState: ReservationState | null
}

export interface SlotView extends SlotAxes {
  visualState: SlotVisualState
}

export interface LotCounts {
  available: number
  occupied: number
  reserved: number
  backup: number
  maintenance: number
  unavailable: number
  unknown: number
}

export interface UseLotStatusOptions {
  layout?: OperationalLayoutProjection | null
  /**
   * Snapshot refetch hook. When omitted, reconnect falls back to the app's
   * existing `sp-operator-refresh` data-loading event.
   */
  refreshLayout?: () => Promise<OperationalLayoutProjection | null | void>
  refreshOnResume?: boolean
  connectionState?: LayoutConnectionState
  syncState?: LayoutSyncOutcome | "pending"
}

export type SlotProjectionLookup = (
  slot: OperatorSlot,
) => SlotVisualStateDto | undefined

const RESERVATION_STATES: readonly ReservationState[] = [
  "UNRESERVED",
  "RESERVED",
  "PROTECTED",
  "BACKUP",
]

const LEGACY_STATUS_PHYSICAL: Record<SlotStatus, PhysicalState> = {
  available: "AVAILABLE",
  occupied: "OCCUPIED",
  reserved: "AVAILABLE",
  maintenance: "MAINTENANCE",
  disabled: "UNAVAILABLE",
}

export function resolveLotLayout(
  lot: OperatorSite | null | undefined,
): OperationalLayoutProjection | null {
  if (!lot) return null
  const carrier = lot as LayoutAwareSite
  return carrier.operationalLayout ?? carrier.layout ?? null
}

function legacyPhysicalState(slot: OperatorSlot): PhysicalState {
  if (slot.isPhysicallyOccupied) return "OCCUPIED"

  const operational = (slot.operationalStatus ?? "").toUpperCase()
  if (operational === "MAINTENANCE") return "MAINTENANCE"
  if (operational === "UNKNOWN") return "UNKNOWN"
  if (
    operational === "UNAVAILABLE" ||
    operational === "BLOCKED" ||
    operational === "DISABLED"
  ) {
    return "UNAVAILABLE"
  }

  return LEGACY_STATUS_PHYSICAL[slot.status] ?? "UNKNOWN"
}

function normalizeReservation(
  raw: string | null | undefined,
): ReservationState | null {
  const value = (raw ?? "").trim().toUpperCase()
  if (value === "" || value === "NONE" || value === "UNRESERVED") {
    return "UNRESERVED"
  }
  return RESERVATION_STATES.includes(value as ReservationState)
    ? (value as ReservationState)
    : null
}

/**
 * Keeps the physical and reservation axes separate. The projection wins over
 * legacy fields; a drifted axis value fails closed downstream in
 * {@link slotVisualState}.
 */
export function resolveSlotAxes(
  slot: OperatorSlot,
  projected?: SlotVisualStateDto | null,
): SlotAxes {
  const direct = slot as AxesAwareSlot
  return {
    physicalState:
      projected?.physicalState ??
      direct.physicalState ??
      legacyPhysicalState(slot),
    reservationState: normalizeReservation(
      projected?.reservationState ?? slot.reservationState,
    ),
  }
}

export function slotVisualState(axes: SlotAxes): SlotVisualState {
  return mapSlotVisualState({
    physicalState: axes.physicalState,
    reservationState: axes.reservationState as ReservationState,
  })
}

/**
 * The existing `Record<..., predicate>` filter pattern, extended with the two
 * new axes. `backup` stays on the reservation axis; `unknown` finds fail-closed
 * bays so operators can inspect them.
 */
export function createStatusFilterPredicates(
  projectedFor: SlotProjectionLookup,
): Record<Exclude<LotVisualFilter, "all">, (slot: OperatorSlot) => boolean> {
  const axesOf = (slot: OperatorSlot) => resolveSlotAxes(slot, projectedFor(slot))

  return {
    available: (slot) => slotVisualState(axesOf(slot)) === "available",
    occupied: (slot) => axesOf(slot).physicalState === "OCCUPIED",
    reserved: (slot) => {
      const reservation = axesOf(slot).reservationState
      return reservation === "RESERVED" || reservation === "PROTECTED"
    },
    backup: (slot) => axesOf(slot).reservationState === "BACKUP",
    maintenance: (slot) => axesOf(slot).physicalState === "MAINTENANCE",
    unknown: (slot) => slotVisualState(axesOf(slot)) === "unknown",
    unavailable: (slot) => axesOf(slot).physicalState === "UNAVAILABLE",
  }
}

export function resolveOperatorSlotView(
  slot: OperatorSlot,
  projected?: SlotVisualStateDto,
): SlotView {
  const axes = resolveSlotAxes(slot, projected)
  if (axes.physicalState === "OCCUPIED" && slot.demoSession) {
    return {
      physicalState: "OCCUPIED",
      reservationState: axes.reservationState,
      visualState: "occupied",
    }
  }
  return { ...axes, visualState: slotVisualState(axes) }
}

export function reconcileSlotAssignment(
  slot: OperatorSlot,
  assignment: DemoVehicle | undefined,
  pending: DemoVehicle | undefined,
  projected: SlotVisualStateDto | undefined,
): OperatorSlot {
  const direct = slot as AxesAwareSlot
  const authoritativePhysical =
    projected?.physicalState ??
    direct.physicalState ??
    legacyPhysicalState(slot)

  const isPhysicalReleased = authoritativePhysical === "AVAILABLE" || authoritativePhysical === "MAINTENANCE"
  if (assignment && !isPhysicalReleased && assignment.session.status !== "COMPLETED") {
    return {
      ...slot,
      status: "occupied",
      physicalState: "OCCUPIED",
      isPhysicallyOccupied: true,
      isBookable: false,
      demoPlate: assignment.plate,
      demoImageUrl: assignment.imageUrl,
      demoCheckedInAt: assignment.session.entryTime,
      demoSession: assignment.session,
    }
  }
  return pending
    ? { ...slot, demoImageUrl: pending.imageUrl, demoCheckedInAt: pending.session.entryTime }
    : slot
}

export interface CheckoutMatchDetail {
  siteId?: string
  plate?: string
  sessionId?: string
  reservationId?: string | null
  slotId?: string | null
}

function normalizeCheckoutPlate(value?: string | null): string {
  return (value ?? "").replace(/[^a-zA-Z0-9]/g, "").toUpperCase()
}

function matchesCheckoutTarget(
  candidate: {
    slotId?: string | null
    plate?: string
    session?: {
      id?: string
      reservationId?: string | null
      originalPlate?: string
      normalizedPlate?: string
    }
  },
  detail: CheckoutMatchDetail,
): boolean {
  const detailSessionId = (detail.sessionId ?? "").trim()
  const detailReservationId = (detail.reservationId ?? "").trim()
  const itemSessionId = (candidate.session?.id ?? "").trim()
  const itemReservationId = (candidate.session?.reservationId ?? "").trim()

  const detailHasIdentity = Boolean(detailSessionId || detailReservationId)
  const itemHasIdentity = Boolean(itemSessionId || itemReservationId)

  // Authoritative session identity check: when both sides identify a session
  // or reservation, a mismatch must preserve the distinct occupant even if the
  // slot or license plate was reused.
  if (detailHasIdentity && itemHasIdentity) {
    if (detailSessionId && itemSessionId && itemSessionId === detailSessionId) return true
    if (detailSessionId && itemReservationId && itemReservationId === detailSessionId) return true
    if (detailReservationId && itemReservationId && itemReservationId === detailReservationId) return true
    if (detailReservationId && itemSessionId && itemSessionId === detailReservationId) return true
    return false
  }

  // Fallback compatibility for legacy slot-only or plate-only events/entries
  // where at least one side lacks session identity.
  if (detail.slotId && candidate.slotId && candidate.slotId === detail.slotId) {
    return true
  }

  const targetPlate = normalizeCheckoutPlate(detail.plate)
  if (!targetPlate) return false

  const plateA = normalizeCheckoutPlate(candidate.plate)
  const plateB = normalizeCheckoutPlate(candidate.session?.originalPlate)
  const plateC = normalizeCheckoutPlate(candidate.session?.normalizedPlate)
  return plateA === targetPlate || plateB === targetPlate || plateC === targetPlate
}

export function filterAssignmentsOnCheckout(
  assignments: Record<string, DemoVehicle>,
  detail: CheckoutMatchDetail,
): Record<string, DemoVehicle> {
  return Object.fromEntries(
    Object.entries(assignments).filter(
      ([slotId, vehicle]) =>
        !matchesCheckoutTarget(
          {
            slotId,
            plate: vehicle.plate,
            session: vehicle.session,
          },
          detail,
        ),
    ),
  )
}

export function filterPendingCheckInsOnCheckout<
  T extends {
    plate?: string
    slotId?: string | null
    session?: {
      id?: string
      reservationId?: string | null
      originalPlate?: string
      normalizedPlate?: string
    } | null
  },
>(stored: T[], detail: CheckoutMatchDetail): T[] {
  return stored.filter(
    (checkIn) =>
      !matchesCheckoutTarget(
        {
          slotId: checkIn.slotId,
          plate: checkIn.plate,
          session: checkIn.session ?? undefined,
        },
        detail,
      ),
  )
}

export interface OperatorFreshnessEvaluation {
  isStale: boolean
  isSourceStale: boolean
  isSyncFailed: boolean
  isMalformedMetadata: boolean
  isAgeStale: boolean
  isHealthyIdleLive: boolean
  staleAgeSeconds: number | null
}

export interface EvaluateOperatorFreshnessOptions {
  connectionState?: LayoutConnectionState
  syncState?: LayoutSyncOutcome | "pending"
  refreshFailed?: boolean
  now?: number
}

export function evaluateOperatorFreshness(
  freshness: Pick<OperationalLayoutProjection, "generatedAt" | "isFresh" | "stalenessThresholdSeconds"> | null | undefined,
  options: EvaluateOperatorFreshnessOptions = {},
): OperatorFreshnessEvaluation {
  const now = options.now ?? Date.now()
  const isSyncFailed = Boolean(options.refreshFailed || options.syncState === "error" || options.syncState === "stale")

  if (!freshness) {
    return {
      isStale: isSyncFailed,
      isSourceStale: false,
      isSyncFailed,
      isMalformedMetadata: false,
      isAgeStale: false,
      isHealthyIdleLive: false,
      staleAgeSeconds: null,
    }
  }

  const threshold = freshness.stalenessThresholdSeconds
  const isInvalidThreshold = typeof threshold !== "number" || !Number.isFinite(threshold) || threshold <= 0
  const parsedGeneratedAt = Date.parse(freshness.generatedAt)
  const isInvalidTimestamp = Number.isNaN(parsedGeneratedAt)
  const isMalformedMetadata = isInvalidThreshold || isInvalidTimestamp

  const isSourceStale = freshness.isFresh !== true
  const staleAgeSeconds = isInvalidTimestamp ? null : Math.max(0, Math.round((now - parsedGeneratedAt) / 1000))
  const isElapsedAge = !isMalformedMetadata && (now - parsedGeneratedAt > (threshold as number) * 1000)

  const isHealthyIdleLive =
    options.connectionState === "connected" &&
    options.syncState === "synced" &&
    freshness.isFresh === true &&
    !isMalformedMetadata &&
    !isSyncFailed

  const isAgeStale = isElapsedAge || isMalformedMetadata
  const isStale = isSyncFailed || isSourceStale || isMalformedMetadata || (isElapsedAge && !isHealthyIdleLive)

  return {
    isStale,
    isSourceStale,
    isSyncFailed,
    isMalformedMetadata,
    isAgeStale,
    isHealthyIdleLive,
    staleAgeSeconds,
  }
}

export function useLotStatus(
  lot: OperatorSite | null,
  initialSlotCode = "",
  initialStatusFilter: LotVisualFilter = "all",
  options: UseLotStatusOptions = {},
) {
  const [viewMode, setViewMode] = useState<ViewMode>("3d")
  const [selectedFloor, setSelectedFloor] = useState<number | "all">(1)
  const [selectedZone, setSelectedZone] = useState<string>("all")
  const [vehicleTypeFilter, setVehicleTypeFilter] = useState<"all" | "car" | "motorcycle">("all")
  const [statusFilter, setStatusFilter] = useState(initialStatusFilter)
  const [searchQuery, setSearchQuery] = useState(initialSlotCode)
  const [inspectedSlot, setInspectedSlot] = useState<OperatorSlot | null>(null)
  const [threeError, setThreeError] = useState(false)
  const [now, setNow] = useState(() => Date.now())
  const [refreshing, setRefreshing] = useState(false)
  const [refreshFailed, setRefreshFailed] = useState(false)
  const [demoAssignments, setDemoAssignments] = useState<Record<string, DemoVehicle>>({})
  const [pendingDemoAssignments, setPendingDemoAssignments] = useState<Record<string, DemoVehicle>>({})
  const [isScanning, setIsScanning] = useState(false)
  const [scanError, setScanError] = useState("")
  const [lastScanAt, setLastScanAt] = useState<number | null>(null)

  // Keep the last known site and snapshot so a failed reconnect refetch never
  // blanks the screen or falls back to fixture data.
  const lastLotRef = useRef<OperatorSite | null>(lot)
  if (lot) lastLotRef.current = lot
  const site = lot ?? lastLotRef.current

  const lastLayoutRef = useRef<OperationalLayoutProjection | null>(null)
  const siteLayout = resolveLotLayout(site)
  const [fetchedLayout, setFetchedLayout] = useState<OperationalLayoutProjection | null>(null)
  const propLayout = useMemo(
    () => {
      if (options.layout) return options.layout
      if (fetchedLayout && siteLayout) {
        const fetchedTime = Date.parse(fetchedLayout.generatedAt) || 0
        const siteTime = Date.parse(siteLayout.generatedAt) || 0
        return fetchedTime >= siteTime ? fetchedLayout : siteLayout
      }
      return fetchedLayout ?? siteLayout ?? null
    },
    [options.layout, siteLayout, fetchedLayout],
  )
  useEffect(() => {
    if (propLayout) lastLayoutRef.current = propLayout
  }, [propLayout])

  const layout = propLayout ?? lastLayoutRef.current
  // A projection only describes the site it was generated for. Switching sites
  // must never borrow the previous site's freshness or slot states.
  const activeLayout =
    layout && site && layout.siteId === site.id ? layout : null

  useEffect(() => {
    const id = window.setInterval(() => setNow(Date.now()), 5000)
    return () => window.clearInterval(id)
  }, [])

  const previousSiteIdRef = useRef(site?.id)
  useEffect(() => {
    if (previousSiteIdRef.current === site?.id) return
    previousSiteIdRef.current = site?.id
    lastLayoutRef.current = null
    setFetchedLayout(null)
    setRefreshFailed(false)
    // A zone selection is site-scoped: never carry it across a site switch.
    setSelectedZone("all")
  }, [site?.id])

  useEffect(() => {
    if (options.layout || !site?.id) return
    let active = true
    const unsubscribe = subscribeOperatorLayout(site.id, (snapshot) => {
      if (!active) return
      if (snapshot.lastOutcome === "error") {
        setRefreshFailed(true)
        setNow(Date.now())
        return
      }
      const nextLayout = resolveLotLayout(snapshot.data)
      if (nextLayout) {
        setFetchedLayout(nextLayout)
        lastLayoutRef.current = nextLayout
        if (snapshot.lastOutcome === "synced") {
          setRefreshFailed(false)
        }
        setNow(Date.now())
      }
    })
    return () => {
      active = false
      unsubscribe()
    }
  }, [options.layout, site?.id])

  const refreshLayoutRef = useRef(options.refreshLayout)
  refreshLayoutRef.current = options.refreshLayout
  const refreshInFlightRef = useRef(false)

  const refreshLayout = useCallback(async () => {
    if (refreshInFlightRef.current) return
    refreshInFlightRef.current = true
    setRefreshing(true)
    try {
      const run = refreshLayoutRef.current
      if (run) {
        const next = await run()
        if (next) {
          setFetchedLayout(next)
          lastLayoutRef.current = next
        }
      } else if (site) {
        const snapshot = await reconcileOperatorLayout(site.id, "mutation", { force: true })
        if (snapshot.lastOutcome === "error") {
          throw snapshot.lastError instanceof Error
            ? snapshot.lastError
            : new Error("Failed to refresh parking layout.")
        }
        const projection = resolveLotLayout(snapshot.data)
        if (projection) {
          setFetchedLayout(projection)
          lastLayoutRef.current = projection
        }
      }
      setRefreshFailed(false)
    } catch {
      setRefreshFailed(true)
    } finally {
      refreshInFlightRef.current = false
      setRefreshing(false)
      setNow(Date.now())
    }
  }, [site])

  useEffect(() => {
    if (options.refreshOnResume === false) return
    const onOnline = () => {
      void refreshLayout()
    }
    const onVisibilityChange = () => {
      if (document.visibilityState === "visible") void refreshLayout()
    }

    window.addEventListener("online", onOnline)
    document.addEventListener("visibilitychange", onVisibilityChange)

    return () => {
      window.removeEventListener("online", onOnline)
      document.removeEventListener("visibilitychange", onVisibilityChange)
    }
  }, [refreshLayout, options.refreshOnResume])

  const isOutdoor = (site?.floors ?? 1) <= 1
  const availableZones = useMemo(() => {
    if (!site) return []
    return site.units.filter((unit) => unit.type === "ZONE")
      .filter((zone) => {
        const parent = site.units.find((unit) => unit.id === zone.parentId)
        const floor = parent?.type === "FLOOR"
          ? site.units.filter((unit) => unit.type === "FLOOR").findIndex((unit) => unit.id === parent.id) + 1
          : 1
        return isOutdoor || selectedFloor === "all" || floor === selectedFloor
      })
  }, [site, isOutdoor, selectedFloor])

  // Keep the zone filter valid for the current site/floor: if the selected zone
  // is no longer offered, fall back to "all" instead of showing zero slots.
  useEffect(() => {
    if (selectedZone === "all") return
    if (availableZones.some((zone) => zone.id === selectedZone)) return
    setSelectedZone("all")
  }, [availableZones, selectedZone])

  const scanSlots = useCallback(async () => {
    if (!site || isScanning || Object.keys(pendingDemoAssignments).length === 0) return
    setIsScanning(true)
    setScanError("")
    await new Promise<void>((resolve) => window.setTimeout(resolve, 850))

    const failed: Record<string, DemoVehicle> = {}
    const completedIds: string[] = []
    const staleIds: string[] = []
    const errors: string[] = []
    for (const [slotId, vehicle] of Object.entries(pendingDemoAssignments)) {
      const referenceId = vehicle.session.reservationId ?? vehicle.session.id
      try {
        const session = await reservationApi.scanParkingSession(site.id, referenceId, slotId)
        const assignedSlotId = session.currentSlotId ?? slotId
        setDemoAssignments((current) => ({
          ...current,
          [assignedSlotId]: { ...vehicle, session: toDemoSession(session) },
        }))
        completedIds.push(vehicle.session.id)
      } catch (error) {
        if (error instanceof AuthApiError && error.status === 404 &&
            error.code === "PARKING_SESSION_NOT_FOUND") {
          // This local pending item has no active session in OperationService. Drop the
          // stale browser-only assignment so every later scan does not retry the same 404.
          staleIds.push(vehicle.session.id)
          errors.push(`${vehicle.plate}: active parking session was not found; check in the vehicle again.`)
          continue
        }
        failed[slotId] = vehicle
        errors.push(`${vehicle.plate}: ${error instanceof Error ? error.message : "Could not save the scanned parking slot."}`)
      }
    }

    setPendingDemoAssignments(failed)
    if (errors.length > 0) setScanError(errors.join(" "))
    const storageKey = pendingCheckInStorageKey(site.id)
    const pending = JSON.parse(window.localStorage.getItem(storageKey) ?? "[]") as PendingCheckIn[]
    window.localStorage.setItem(storageKey, JSON.stringify(
      pending.filter((item) => !completedIds.includes(item.session?.id ?? "") &&
        !staleIds.includes(item.session?.id ?? "")),
    ))
    setLastScanAt(Date.now())
    if (completedIds.length > 0) {
      invalidateOperatorGateResources(site.id, "mutation", { includeLayout: false })
      await refreshLayout()
    }
    setIsScanning(false)
  }, [isScanning, pendingDemoAssignments, refreshLayout, site])

  useEffect(() => {
    if (!site) return
    setDemoAssignments({})
    setPendingDemoAssignments({})
    setLastScanAt(null)

    let active = true
    const unsubscribe = subscribeOperatorParkingSessions(site.id, (snapshot) => {
      if (!active || !snapshot.data) return
      const assignments: Record<string, DemoVehicle> = {}
      const locatedIds = new Set<string>()
      for (const session of snapshot.data) {
        if (!session.currentSlotId || !session.locationScannedAt ||
            !(session.status === "ACTIVE" || session.status === "PENDING_PAYMENT")) continue
        const vehicle = toDemoSession(session)
        locatedIds.add(session.id)
        if (session.reservationId) locatedIds.add(session.reservationId)
        assignments[session.currentSlotId] = {
          plate: vehicle.originalPlate,
          imageUrl: vehicle.entryImageUrl ?? "",
          session: vehicle,
        }
      }
      setDemoAssignments(assignments)
      if (locatedIds.size > 0) {
        setPendingDemoAssignments((current) => Object.fromEntries(
          Object.entries(current).filter(([, vehicle]) => !locatedIds.has(vehicle.session.id)),
        ))
        const key = pendingCheckInStorageKey(site.id)
        const pending = JSON.parse(window.localStorage.getItem(key) ?? "[]") as PendingCheckIn[]
        window.localStorage.setItem(key, JSON.stringify(
          pending.filter((item) => !item.session || !locatedIds.has(item.session.id)),
        ))
      }
    })
    invalidateOperatorParkingSessions(site.id, "initial")

    const stored = JSON.parse(
      window.localStorage.getItem(pendingCheckInStorageKey(site.id)) ?? "[]",
    ) as PendingCheckIn[]
    if (stored.length === 0) return () => { active = false; unsubscribe() }
    const assignments: Record<string, DemoVehicle> = {}
    const availableSlots = site.slots.filter((slot) => slot.status === "available")
    stored.forEach((checkIn, index) => {
      const candidates = availableSlots.filter((slot) =>
        acceptsDemoVehicle(slot, checkIn.vehicleType ?? checkIn.session?.vehicleType))
      const slot = candidates.find((candidate) => candidate.id === checkIn.slotId)
        ?? candidates[Math.floor(Math.random() * candidates.length)]
      if (!slot) return
      availableSlots.splice(availableSlots.indexOf(slot), 1)
      const imageUrl = checkIn.imageUrl || demoVehicleImage(Date.now() + index)
      assignments[slot.id] = {
        plate: checkIn.plate,
        imageUrl,
        session: checkIn.session ?? createDemoSession(checkIn.plate, checkIn.vehicleType ?? "CAR", imageUrl),
      }
      checkIn.slotId = slot.id
    })
    setPendingDemoAssignments(assignments)
    window.localStorage.setItem(pendingCheckInStorageKey(site.id), JSON.stringify(stored))
    return () => {
      active = false
      unsubscribe()
    }
  }, [site?.id])

  useEffect(() => {
    const handleCheckIn = (event: Event) => {
      const detail = (event as CustomEvent<{
        siteId: string; plate: string; imageUrl?: string; session?: DemoSlotSession; vehicleType?: string
        slotId?: string; recordedSlotId?: string
      }>).detail
      if (!site || detail.siteId !== site.id) return
      const imageUrl = detail.imageUrl || demoVehicleImage(Date.now())
      const vehicle = {
        plate: detail.plate,
        imageUrl,
        session: detail.session ?? createDemoSession(detail.plate, detail.vehicleType ?? "CAR", imageUrl),
      }
      if (detail.recordedSlotId) {
        const slotId = detail.recordedSlotId
        setDemoAssignments((current) => ({ ...current, [slotId]: vehicle }))
        void refreshLayout()
        return
      }
      const occupiedIds = new Set(Object.keys(demoAssignments))
      const pendingIds = new Set(Object.keys(pendingDemoAssignments))
      const availableSlots = site.slots.filter((slot) =>
        slot.status === "available" && !occupiedIds.has(slot.id) && !pendingIds.has(slot.id) &&
        acceptsDemoVehicle(slot, vehicle.session.vehicleType),
      )
      const slot = availableSlots.find((candidate) => candidate.id === detail.slotId)
        ?? availableSlots[Math.floor(Math.random() * availableSlots.length)]
      if (!slot) return
      setPendingDemoAssignments((current) => ({ ...current, [slot.id]: vehicle }))
      const storageKey = pendingCheckInStorageKey(site.id)
      const stored = JSON.parse(window.localStorage.getItem(storageKey) ?? "[]") as PendingCheckIn[]
      const existingIndex = stored.findIndex((checkIn) => checkIn.session?.id === vehicle.session.id)
      if (existingIndex >= 0) {
        stored[existingIndex] = { ...stored[existingIndex], ...vehicle, slotId: slot.id }
        window.localStorage.setItem(storageKey, JSON.stringify(stored))
      } else {
        stored.push({ ...vehicle, slotId: slot.id })
        window.localStorage.setItem(storageKey, JSON.stringify(stored))
      }
    }
    const handleCheckOut = (event: Event) => {
      const detail = (event as CustomEvent<{
        siteId: string
        plate: string
        sessionId?: string
        reservationId?: string | null
        slotId?: string | null
      }>).detail
      if (!site || detail.siteId !== site.id) return
      const removeMatch = (assignments: Record<string, DemoVehicle>) =>
        filterAssignmentsOnCheckout(assignments, detail)
      setDemoAssignments(removeMatch)
      setPendingDemoAssignments(removeMatch)
      const storageKey = pendingCheckInStorageKey(site.id)
      const stored = JSON.parse(window.localStorage.getItem(storageKey) ?? "[]") as PendingCheckIn[]
      window.localStorage.setItem(
        storageKey,
        JSON.stringify(filterPendingCheckInsOnCheckout(stored, detail)),
      )
      invalidateOperatorGateResources(site.id, "mutation", { includeLayout: false })
      void refreshLayout()
    }
    window.addEventListener("smartpark:demo-check-in", handleCheckIn)
    window.addEventListener("smartpark:demo-check-out", handleCheckOut)
    return () => {
      window.removeEventListener("smartpark:demo-check-in", handleCheckIn)
      window.removeEventListener("smartpark:demo-check-out", handleCheckOut)
    }
  }, [demoAssignments, pendingDemoAssignments, refreshLayout, site])

  useEffect(() => {
    const timer = window.setInterval(scanSlots, 300000)
    return () => window.clearInterval(timer)
  }, [scanSlots])

  const projectionSlots = useMemo(() => {
    const map = new Map<string, SlotVisualStateDto>()
    for (const slot of activeLayout?.slots ?? []) {
      map.set(slot.slotCode, slot)
      map.set(slot.slotId, slot)
    }
    return map
  }, [activeLayout])

  const projectedFor = useCallback<SlotProjectionLookup>((slot) => {
    const direct = slot as AxesAwareSlot
    return (
      projectionSlots.get(slot.id) ??
      projectionSlots.get(slot.number) ??
      (direct.slotCode ? projectionSlots.get(direct.slotCode) : undefined)
    )
  }, [projectionSlots])

  const allSlots: OperatorSlot[] = useMemo(() => (site?.slots ?? []).map((slot) => {
    const assignment = demoAssignments[slot.id]
    const pending = pendingDemoAssignments[slot.id]
    const projected = projectedFor(slot)
    return reconcileSlotAssignment(slot, assignment, pending, projected)
  }), [site, demoAssignments, pendingDemoAssignments, projectedFor])

  const slotView = useCallback((slot: OperatorSlot): SlotView => {
    return resolveOperatorSlotView(slot, projectedFor(slot))
  }, [projectedFor])

  const STATUS_FILTER_PREDICATES = useMemo(
    () => createStatusFilterPredicates(projectedFor),
    [projectedFor],
  )

  const filteredSlots = useMemo(() => allSlots.filter((slot) => {
    if (!isOutdoor && selectedFloor !== "all" && slot.floor !== selectedFloor) return false
    if (selectedZone !== "all") {
      let unit = site?.units.find((item) => item.id === slot.unitId)
      while (unit && unit.id !== selectedZone) {
        unit = unit.parentId ? site?.units.find((item) => item.id === unit?.parentId) : undefined
      }
      if (!unit) return false
    }

    const isMoto = slot.vehicleType.toLowerCase().includes("motorcycle")
    if (vehicleTypeFilter === "car" && isMoto) return false
    if (vehicleTypeFilter === "motorcycle" && !isMoto) return false
    if (statusFilter !== "all" && !STATUS_FILTER_PREDICATES[statusFilter]?.(slot)) return false
    if (searchQuery.trim()) {
      const query = searchQuery.trim().toLowerCase()
      const rawMatch = slot.number.toLowerCase().includes(query)
      const labelMatch = formatSlotLabel(slot.number).toLowerCase().includes(query)
      if (!rawMatch && !labelMatch) return false
    }
    return true
  }), [allSlots, isOutdoor, selectedFloor, selectedZone, vehicleTypeFilter, statusFilter, searchQuery, site, STATUS_FILTER_PREDICATES])

  const counts = useMemo<LotCounts>(() => {
    const next: LotCounts = {
      available: 0,
      occupied: 0,
      reserved: 0,
      backup: 0,
      maintenance: 0,
      unavailable: 0,
      unknown: 0,
    }

    for (const slot of allSlots) {
      const view = slotView(slot)
      if (view.visualState === "available") next.available += 1
      if (view.physicalState === "OCCUPIED") next.occupied += 1
      if (view.reservationState === "RESERVED" || view.reservationState === "PROTECTED") next.reserved += 1
      // BACKUP is counted on the reservation axis, so an occupied bay that is
      // also held as backup is both `occupied` and `backup`.
      if (view.reservationState === "BACKUP") next.backup += 1
      if (view.physicalState === "MAINTENANCE") next.maintenance += 1
      if (view.physicalState === "UNAVAILABLE") next.unavailable += 1
      if (view.physicalState === "UNKNOWN" || view.visualState === "unknown") next.unknown += 1
    }

    return next
  }, [allSlots, slotView])

  // Freshness must come from the same response that produced the slot axes. The
  // operator path attaches the projection's triple to `site.freshness`; the owner
  // path uses the legacy aggregate, which carries no freshness triple, so treat
  // that as unknown: never dim, never show the stale banner, never claim fresh.
  const freshness = activeLayout ?? site?.freshness ?? null
  const freshnessEval = evaluateOperatorFreshness(freshness, {
    connectionState: options.connectionState,
    syncState: options.syncState,
    refreshFailed,
    now,
  })
  const { isStale, staleAgeSeconds } = freshnessEval

  return {
    viewMode, setViewMode, selectedFloor, setSelectedFloor, selectedZone, setSelectedZone,
    vehicleTypeFilter, setVehicleTypeFilter, statusFilter, setStatusFilter, searchQuery, setSearchQuery,
    inspectedSlot, setInspectedSlot, threeError, setThreeError, isOutdoor, availableZones,
    site, allSlots, filteredSlots, counts, slotView, layout: activeLayout,
    serviceAvailability: fetchedLayout?.serviceAvailability ?? site?.serviceAvailability ?? null,
    isStale, staleAgeSeconds, isRefreshing: refreshing, refreshFailed, refreshLayout,
    scanSlots, isScanning, lastScanAt, scanError,
  }
}
