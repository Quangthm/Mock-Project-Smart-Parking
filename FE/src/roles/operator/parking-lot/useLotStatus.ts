import { useCallback, useEffect, useMemo, useRef, useState } from "react"
import type { DemoSlotSession, OperatorSite, OperatorSlot } from "../../../lib/operatorApi"
import { parkingApi } from "../../../lib/parkingApi"
import type { LotType, SlotStatus } from "../../../lib/types"
import {
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
type DemoVehicle = {
  plate: string
  imageUrl: string
  session: DemoSlotSession
}

function pendingCheckInStorageKey(siteId: string) {
  return `smartpark:demo-pending-checkins:${siteId}`
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
    () => options.layout ?? fetchedLayout ?? siteLayout,
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
  }, [site?.id])

  // A new site object from the parent load already carries its slots and
  // freshness triple captured together, so drop any older hook-level snapshot.
  const previousLotRef = useRef(lot)
  useEffect(() => {
    if (previousLotRef.current === lot) return
    previousLotRef.current = lot
    lastLayoutRef.current = null
    setFetchedLayout(null)
  }, [lot])

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
        const projection = await parkingApi.operationalLayout(site.id)
        setFetchedLayout(projection)
        lastLayoutRef.current = projection
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
  }, [refreshLayout])

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

  const scanSlots = useCallback(() => {
    if (!site || isScanning) return
    setIsScanning(true)
    window.setTimeout(() => {
      setDemoAssignments((current) => ({ ...current, ...pendingDemoAssignments }))
      setPendingDemoAssignments({})
      window.localStorage.removeItem(pendingCheckInStorageKey(site.id))
      setLastScanAt(Date.now())
      setIsScanning(false)
    }, 850)
  }, [isScanning, pendingDemoAssignments, site])

  useEffect(() => {
    if (!site) return
    setDemoAssignments({})
    setPendingDemoAssignments({})
    setLastScanAt(null)
    const stored = JSON.parse(
      window.localStorage.getItem(pendingCheckInStorageKey(site.id)) ?? "[]",
    ) as Array<{ plate: string; imageUrl?: string; vehicleType?: string; session?: DemoSlotSession }>
    if (stored.length === 0) return
    const assignments: Record<string, DemoVehicle> = {}
    const availableSlots = site.slots.filter((slot) => slot.status === "available")
    stored.forEach((checkIn, index) => {
      const slot = availableSlots.splice(Math.floor(Math.random() * availableSlots.length), 1)[0]
      if (!slot) return
      const imageUrl = checkIn.imageUrl || demoVehicleImage(Date.now() + index)
      assignments[slot.id] = {
        plate: checkIn.plate,
        imageUrl,
        session: checkIn.session ?? createDemoSession(checkIn.plate, checkIn.vehicleType ?? "CAR", imageUrl),
      }
    })
    setPendingDemoAssignments(assignments)
  }, [site?.id])

  useEffect(() => {
    const handleCheckIn = (event: Event) => {
      const detail = (event as CustomEvent<{
        siteId: string; plate: string; imageUrl?: string; session?: DemoSlotSession; vehicleType?: string
      }>).detail
      if (!site || detail.siteId !== site.id) return
      const imageUrl = detail.imageUrl || demoVehicleImage(Date.now())
      const vehicle = {
        plate: detail.plate,
        imageUrl,
        session: detail.session ?? createDemoSession(detail.plate, detail.vehicleType ?? "CAR", imageUrl),
      }
      setPendingDemoAssignments((current) => {
        const occupiedIds = new Set(Object.keys(demoAssignments))
        const pendingIds = new Set(Object.keys(current))
        const availableSlots = site.slots.filter((slot) =>
          slot.status === "available" && !occupiedIds.has(slot.id) && !pendingIds.has(slot.id),
        )
        const slot = availableSlots[Math.floor(Math.random() * availableSlots.length)]
        return slot ? { ...current, [slot.id]: vehicle } : current
      })
      const stored = JSON.parse(window.localStorage.getItem(pendingCheckInStorageKey(site.id)) ?? "[]") as Array<{ plate: string }>
      if (!stored.some((checkIn) => checkIn.plate === detail.plate)) {
        stored.push(detail)
        window.localStorage.setItem(pendingCheckInStorageKey(site.id), JSON.stringify(stored))
      }
    }
    const handleCheckOut = (event: Event) => {
      const detail = (event as CustomEvent<{ siteId: string; plate: string }>).detail
      if (!site || detail.siteId !== site.id) return
      const removePlate = (assignments: Record<string, DemoVehicle>) =>
        Object.fromEntries(Object.entries(assignments).filter(([, vehicle]) => vehicle.plate !== detail.plate))
      setDemoAssignments(removePlate)
      setPendingDemoAssignments(removePlate)
      const stored = JSON.parse(window.localStorage.getItem(pendingCheckInStorageKey(site.id)) ?? "[]") as Array<{ plate: string }>
      window.localStorage.setItem(pendingCheckInStorageKey(site.id), JSON.stringify(stored.filter((checkIn) => checkIn.plate !== detail.plate)))
    }
    window.addEventListener("smartpark:demo-check-in", handleCheckIn)
    window.addEventListener("smartpark:demo-check-out", handleCheckOut)
    return () => {
      window.removeEventListener("smartpark:demo-check-in", handleCheckIn)
      window.removeEventListener("smartpark:demo-check-out", handleCheckOut)
    }
  }, [demoAssignments, site])

  useEffect(() => {
    const timer = window.setInterval(scanSlots, 300000)
    return () => window.clearInterval(timer)
  }, [scanSlots])

  const allSlots: OperatorSlot[] = useMemo(() => (site?.slots ?? []).map((slot) => {
    const assignment = demoAssignments[slot.id]
    const pending = pendingDemoAssignments[slot.id]
    if (assignment) {
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
  }), [site, demoAssignments, pendingDemoAssignments])

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

  const slotView = useCallback((slot: OperatorSlot): SlotView => {
    if (slot.demoSession) {
      return {
        physicalState: "OCCUPIED",
        reservationState: resolveSlotAxes(slot, projectedFor(slot)).reservationState,
        visualState: "occupied",
      }
    }
    const axes = resolveSlotAxes(slot, projectedFor(slot))
    return { ...axes, visualState: slotVisualState(axes) }
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
    if (searchQuery.trim() && !slot.number.toLowerCase().includes(searchQuery.trim().toLowerCase())) return false
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
  const isStale = refreshFailed || (freshness !== null && isLayoutStale(freshness, now))
  const staleAgeSeconds = freshness !== null ? layoutAgeSeconds(freshness, now) : null

  return {
    viewMode, setViewMode, selectedFloor, setSelectedFloor, selectedZone, setSelectedZone,
    vehicleTypeFilter, setVehicleTypeFilter, statusFilter, setStatusFilter, searchQuery, setSearchQuery,
    inspectedSlot, setInspectedSlot, threeError, setThreeError, isOutdoor, availableZones,
    site, allSlots, filteredSlots, counts, slotView, layout: activeLayout,
    isStale, staleAgeSeconds, isRefreshing: refreshing, refreshFailed, refreshLayout,
    scanSlots, isScanning, lastScanAt,
  }
}
