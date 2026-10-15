import { useState, useMemo, useEffect } from "react"
import { structureStore } from "../data/structureStore"
import type {
  ParkingFloorItem,
  ParkingZoneItem,
  ParkingSpaceItem,
  ParkingStructureData,
} from "../../../lib/structureTypes"
import type { Site } from "../../../lib/parkingApi"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { FloorModal } from "./FloorModal"
import { ZoneModal } from "./ZoneModal"
import { SpaceDetailModal } from "./SpaceDetailModal"

interface ParkingStructureProps {
  sites?: Site[]
  selectedSiteId?: string
  onChanged?: () => void
}

// 6 Canonical Status Configuration for UI
export const STATUS_DEFINITIONS = {
  available: {
    label: "Available",
    color: "#16a34a",
    bg: "rgba(34, 197, 94, 0.12)",
    border: "rgba(34, 197, 94, 0.35)",
    icon: "check-circle",
    desc: "Available for parking or reservations",
  },
  reserved: {
    label: "Reserved",
    color: "#2563eb",
    bg: "rgba(37, 99, 235, 0.12)",
    border: "rgba(37, 99, 235, 0.35)",
    icon: "clock",
    desc: "Reserved through the app",
  },
  pending_payment: {
    label: "Pending Payment",
    color: "#d97706",
    bg: "rgba(245, 158, 11, 0.12)",
    border: "rgba(245, 158, 11, 0.35)",
    icon: "banknote",
    desc: "Temporarily held while payment is in progress",
  },
  backup: {
    label: "Slot Backup",
    color: "#9333ea",
    bg: "rgba(147, 51, 234, 0.12)",
    border: "rgba(147, 51, 234, 0.35)",
    icon: "shield",
    desc: "Backup space for emergencies and reallocation",
  },
  occupied: {
    label: "Occupied",
    color: "#dc2626",
    bg: "rgba(239, 68, 68, 0.12)",
    border: "rgba(239, 68, 68, 0.35)",
    icon: "car",
    desc: "A vehicle is currently parked in this space",
  },
  protected: {
    label: "Protected",
    color: "#6366f1",
    bg: "rgba(99, 102, 241, 0.15)",
    border: "rgba(99, 102, 241, 0.45)",
    icon: "shield",
    desc: "Protected for an upcoming reservation; walk-ins are blocked",
  },
}

export function ParkingStructure({
  sites = [],
  selectedSiteId = "all",
  onChanged,
}: ParkingStructureProps) {
  // Available lots list
  const availableLots = useMemo(() => {
    if (sites && sites.length > 0) return sites
    return [
      {
        id: "lot-002",
        name: "Bitexco Financial Tower Outdoor Lot",
        address: "2 Hải Triều, Bến Nghé, Quận 1, TP. Hồ Chí Minh",
      } as Site,
      {
        id: "lot-001",
        name: "Vinhomes Grand Park Parking Center",
        address: "Khu đô thị Vinhomes Grand Park, Phường Long Thạnh Mỹ, TP. Thủ Đức",
      } as Site,
      {
        id: "lot-003",
        name: "Nguyen Hue Boulevard Outdoor Lot",
        address: "68 Nguyễn Huệ, Bến Nghé, Quận 1, TP. Hồ Chí Minh",
      } as Site,
    ]
  }, [sites])

  const [activeLotId, setActiveLotId] = useState<string>(() => {
    if (selectedSiteId && selectedSiteId !== "all") return selectedSiteId
    return availableLots[0]?.id || "lot-002"
  })

  useEffect(() => {
    if (selectedSiteId && selectedSiteId !== "all") {
      setActiveLotId(selectedSiteId)
    }
  }, [selectedSiteId])

  useEffect(() => {
    if (availableLots.length > 0 && !availableLots.some((l) => l.id === activeLotId)) {
      setActiveLotId(availableLots[0].id)
    }
  }, [availableLots, activeLotId])

  // Current Structure Data
  const [structure, setStructure] = useState<ParkingStructureData>(() =>
    structureStore.getStructure(activeLotId),
  )

  // Floor Selection
  const [selectedFloorId, setSelectedFloorId] = useState<string>("")
  const [selectedZoneFilter, setSelectedZoneFilter] = useState<string>("all")

  // Filter & Search
  const [statusFilter, setStatusFilter] = useState<
    "all" | "available" | "reserved" | "pending_payment" | "backup" | "occupied" | "protected" | "dual"
  >("all")
  const [searchQuery, setSearchQuery] = useState("")

  // Owner Protected Buffer Setting (x hours)
  const [protectionWindowHours, setProtectionWindowHours] = useState<number>(
    () => (structure.policy?.reservationProtectionWindowMinutes ?? 60) / 60,
  )

  const protectionStartMinutes = 10 * 60 - Math.round(protectionWindowHours * 60)
  const protectionStartTime = `${String(Math.floor(protectionStartMinutes / 60)).padStart(2, "0")}:${String(protectionStartMinutes % 60).padStart(2, "0")}`
  // Modal States
  const [isFloorModalOpen, setIsFloorModalOpen] = useState(false)
  const [editingFloor, setEditingFloor] = useState<ParkingFloorItem | null>(null)
  const [isZoneModalOpen, setIsZoneModalOpen] = useState(false)
  const [editingZone, setEditingZone] = useState<ParkingZoneItem | null>(null)
  const [inspectingSpace, setInspectingSpace] = useState<ParkingSpaceItem | null>(null)

  useEffect(() => {
    const s = structureStore.getStructure(activeLotId)
    setStructure(s)
    setProtectionWindowHours((s.policy?.reservationProtectionWindowMinutes ?? 60) / 60)
    setSelectedFloorId(s.floors?.[0]?.id || "")
    setSelectedZoneFilter("all")
  }, [activeLotId])

  const reloadStructure = () => {
    setStructure(structureStore.getStructure(activeLotId))
    onChanged?.()
  }

  function updateProtectionWindow(hours: number) {
    const updated = {
      ...structure,
      policy: {
        ...structure.policy,
        protectionWindowHours: hours,
        reservationProtectionWindowMinutes: Math.round(hours * 60),
      },
    }
    structureStore.saveStructure(updated)
    setStructure(updated)
    setProtectionWindowHours(hours)
    onChanged?.()
  }

  const currentFloor = useMemo(() => {
    if (!structure?.floors || structure.floors.length === 0) return null
    return structure.floors.find((f) => f.id === selectedFloorId) || structure.floors[0] || null
  }, [structure, selectedFloorId])

  // Get all spaces flattened for stats
  const allSpaces = useMemo(() => {
    const spaces: ParkingSpaceItem[] = []
    structure?.floors?.forEach((fl) => {
      fl.zones?.forEach((zn) => {
        zn.spaces?.forEach((sp) => spaces.push(sp))
      })
    })
    return spaces
  }, [structure])

  // Count by the 6 canonical states
  const counts = useMemo(() => {
    let available = 0
    let reserved = 0
    let pendingPayment = 0
    let backup = 0
    let occupied = 0
    let protectedCount = 0
    let dualOccupiedProtected = 0

    allSpaces.forEach((s) => {
      const physical = s.physicalState || (s.status === "occupied" ? "OCCUPIED" : "AVAILABLE")
      const reservation =
        s.reservationState ||
        (s.isProtected ? "PROTECTED" : s.isBackup ? "BACKUP" : s.isPendingPayment ? "PENDING_PAYMENT" : s.status === "reserved" ? "RESERVED" : "NONE")

      const isOcc = physical === "OCCUPIED"
      const isProt = reservation === "PROTECTED" || s.isProtected

      if (isOcc && isProt) dualOccupiedProtected++
      if (isOcc) occupied++
      if (isProt) protectedCount++
      if (s.isBackup || reservation === "BACKUP") backup++
      if (s.isPendingPayment || reservation === "PENDING_PAYMENT") pendingPayment++
      if (s.status === "reserved" && !isProt && !s.isBackup && !s.isPendingPayment) reserved++
      if (!isOcc && !isProt && !s.isBackup && !s.isPendingPayment && s.status === "available") available++
    })

    return {
      total: allSpaces.length,
      available,
      reserved,
      pendingPayment,
      backup,
      occupied,
      protected: protectedCount,
      dual: dualOccupiedProtected,
    }
  }, [allSpaces])

  // Filtered spaces for current floor
  const displayedSpaces = useMemo(() => {
    if (!currentFloor || !currentFloor.zones) return []

    let zones = currentFloor.zones
    if (selectedZoneFilter !== "all") {
      zones = zones.filter((z) => z.id === selectedZoneFilter)
    }

    const result: Array<ParkingSpaceItem & { zoneName: string }> = []
    zones.forEach((z) => {
      z.spaces.forEach((s) => {
        const physical = s.physicalState || (s.status === "occupied" ? "OCCUPIED" : "AVAILABLE")
        const reservation =
          s.reservationState ||
          (s.isProtected ? "PROTECTED" : s.isBackup ? "BACKUP" : s.isPendingPayment ? "PENDING_PAYMENT" : s.status === "reserved" ? "RESERVED" : "NONE")

        const isOcc = physical === "OCCUPIED"
        const isProt = reservation === "PROTECTED" || s.isProtected
        const isDual = isOcc && isProt

        // Status matching
        let matchesStatus = true
        if (statusFilter === "dual") matchesStatus = isDual
        else if (statusFilter === "occupied") matchesStatus = isOcc
        else if (statusFilter === "protected") matchesStatus = isProt
        else if (statusFilter === "backup") matchesStatus = s.isBackup || reservation === "BACKUP"
        else if (statusFilter === "pending_payment") matchesStatus = s.isPendingPayment || reservation === "PENDING_PAYMENT"
        else if (statusFilter === "reserved") matchesStatus = s.status === "reserved"
        else if (statusFilter === "available") matchesStatus = !isOcc && !isProt && !s.isBackup && s.status === "available"

        // Search matching
        const matchesSearch =
          !searchQuery.trim() ||
          s.code.toLowerCase().includes(searchQuery.toLowerCase()) ||
          s.currentBooking?.licensePlate?.toLowerCase().includes(searchQuery.toLowerCase()) ||
          s.currentBooking?.driverName?.toLowerCase().includes(searchQuery.toLowerCase())

        if (matchesStatus && matchesSearch) {
          result.push({ ...s, zoneName: z.name })
        }
      })
    })

    return result
  }, [currentFloor, selectedZoneFilter, statusFilter, searchQuery])

  return (
    <section className="space-y-6 text-[var(--fg)]">
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 border-b border-[var(--border)] pb-4">
        <div>
          <h1 className="text-xl font-bold tracking-tight text-[var(--fg)] flex items-center gap-2">
            <span>Parking Structure & Protection System</span>
            <span className="text-xs px-2.5 py-0.5 rounded-full bg-[var(--primary)]/10 text-[var(--primary)] font-semibold">
              SRS 3.2.2 / 8.2
            </span>
          </h1>
          <p className="mt-1 text-xs text-[var(--muted)]">
            Manage the parking layout, space statuses, reservation protection, and conflict reallocation.
          </p>
        </div>

        {/* Site Selector */}
        <div className="flex items-center gap-2.5">
          <label className="text-xs font-semibold text-[var(--muted)]">Site:</label>
          <select
            className="input min-w-[200px] text-xs font-medium"
            value={activeLotId}
            onChange={(e) => setActiveLotId(e.target.value)}
          >
            {availableLots.map((l) => (
              <option key={l.id} value={l.id}>
                {l.name}
              </option>
            ))}
          </select>
        </div>
      </div>

      {notice && <p role="status" className="rounded-lg border border-emerald-500/30 bg-emerald-500/10 px-4 py-2 text-sm text-emerald-700 dark:text-emerald-300">{notice}</p>}
      {error && <div role="alert" className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-2 text-sm text-red-700 dark:text-red-300"><span>{error}</span>{siteId && <button type="button" className="underline" onClick={() => setReload((value) => value + 1)}>Retry</button>}</div>}
      {!siteId && <p className="rounded-xl border border-[var(--border)] p-6 text-sm text-[var(--muted)]">No parking sites are available for this account.</p>}
      {loading && <p role="status" className="rounded-xl border border-[var(--border)] p-6 text-sm text-[var(--muted)]">Loading parking structure…</p>}

      {structure && !loading && (
        <>
          {!slots.length && <p className="rounded-xl border border-amber-500/30 bg-amber-500/10 px-4 py-3 text-xs text-amber-700 dark:text-amber-300">No backend parking slots are configured for this site yet. Add a unit, then add slots here for operators to use.</p>}
          <div className="grid grid-cols-2 gap-2.5 sm:grid-cols-3 lg:grid-cols-6">
            <Metric label="Physical slots" value={slots.length} tone="available" />
            <Metric label="Available" value={siteCapacity.length > 0 && siteCapacity.every((view) => view.available !== null) ? total("available") : "—"} tone="available" />
            <Metric label="Occupied" value={slots.filter((slot) => slot.isPhysicallyOccupied).length} tone="occupied" />
            <Metric label="Protected" value={siteCapacity.length ? total("protected") : "—"} tone="protected" />
            <Metric label="Pending payment" value={siteCapacity.length ? total("pendingPayment") : "—"} tone="payment" />
            <Metric label="Backup" value={siteCapacity.length ? total("effectiveBackup") : "—"} tone="backup" />
          </div>
          <div className="text-xl font-bold text-emerald-600 dark:text-emerald-400 mt-1">
            {counts.available}
          </div>
        </div>

        {/* 2. Reserved */}
        <div
          onClick={() => setStatusFilter(statusFilter === "reserved" ? "all" : "reserved")}
          className={`p-3 rounded-xl border cursor-pointer transition-all ${
            statusFilter === "reserved" ? "ring-2 ring-blue-500 shadow-xs" : ""
          }`}
          style={{ background: STATUS_DEFINITIONS.reserved.bg, borderColor: STATUS_DEFINITIONS.reserved.border }}
        >
          <div className="text-[11px] font-semibold text-blue-700 dark:text-blue-300 flex items-center justify-between">
            <span>Reserved</span>
            <UntitledIcon name="clock" size={13} />
          </div>
          <div className="text-xl font-bold text-blue-600 dark:text-blue-400 mt-1">
            {counts.reserved}
          </div>
        </div>

        {/* 3. Pending Payment */}
        <div
          onClick={() => setStatusFilter(statusFilter === "pending_payment" ? "all" : "pending_payment")}
          className={`p-3 rounded-xl border cursor-pointer transition-all ${
            statusFilter === "pending_payment" ? "ring-2 ring-amber-500 shadow-xs" : ""
          }`}
          style={{ background: STATUS_DEFINITIONS.pending_payment.bg, borderColor: STATUS_DEFINITIONS.pending_payment.border }}
        >
          <div className="text-[11px] font-semibold text-amber-700 dark:text-amber-300 flex items-center justify-between">
            <span>Pending Payment</span>
            <UntitledIcon name="banknote" size={13} />
          </div>
          <div className="text-xl font-bold text-amber-600 dark:text-amber-400 mt-1">
            {counts.pendingPayment}
          </div>
        </div>

        {/* 4. Slot Backup */}
        <div
          onClick={() => setStatusFilter(statusFilter === "backup" ? "all" : "backup")}
          className={`p-3 rounded-xl border cursor-pointer transition-all ${
            statusFilter === "backup" ? "ring-2 ring-purple-500 shadow-xs" : ""
          }`}
          style={{ background: STATUS_DEFINITIONS.backup.bg, borderColor: STATUS_DEFINITIONS.backup.border }}
        >
          <div className="text-[11px] font-semibold text-purple-700 dark:text-purple-300 flex items-center justify-between">
            <span>Slot Backup</span>
            <UntitledIcon name="shield" size={13} />
          </div>
          <div className="text-xl font-bold text-purple-600 dark:text-purple-400 mt-1">
            {counts.backup}
          </div>
        </div>

        {/* 5. Occupied */}
        <div
          onClick={() => setStatusFilter(statusFilter === "occupied" ? "all" : "occupied")}
          className={`p-3 rounded-xl border cursor-pointer transition-all ${
            statusFilter === "occupied" ? "ring-2 ring-red-500 shadow-xs" : ""
          }`}
          style={{ background: STATUS_DEFINITIONS.occupied.bg, borderColor: STATUS_DEFINITIONS.occupied.border }}
        >
          <div className="text-[11px] font-semibold text-red-700 dark:text-red-300 flex items-center justify-between">
            <span>Occupied</span>
            <UntitledIcon name="car" size={13} />
          </div>
          <div className="text-xl font-bold text-red-600 dark:text-red-400 mt-1">
            {counts.occupied}
          </div>
        </div>

        {/* 6. Protected */}
        <div
          onClick={() => setStatusFilter(statusFilter === "protected" ? "all" : "protected")}
          className={`p-3 rounded-xl border cursor-pointer transition-all ${
            statusFilter === "protected" ? "ring-2 ring-indigo-500 shadow-xs" : ""
          }`}
          style={{ background: STATUS_DEFINITIONS.protected.bg, borderColor: STATUS_DEFINITIONS.protected.border }}
        >
          <div className="text-[11px] font-semibold text-indigo-700 dark:text-indigo-300 flex items-center justify-between">
            <span>Protected</span>
            <UntitledIcon name="shield" size={13} />
          </div>
          <div className="text-xl font-bold text-indigo-600 dark:text-indigo-400 mt-1">
            {counts.protected}
          </div>
        </div>

        {/* Dual State: Vừa Occupied vừa Protected */}
        <div
          onClick={() => setStatusFilter(statusFilter === "dual" ? "all" : "dual")}
          className={`p-3 rounded-xl border cursor-pointer transition-all ${
            statusFilter === "dual" ? "ring-2 ring-rose-500 shadow-xs" : ""
          } ${counts.dual > 0 ? "animate-pulse" : ""}`}
          style={{
            background: "rgba(225, 29, 72, 0.12)",
            borderColor: "rgba(225, 29, 72, 0.4)",
          }}
        >
          <div className="text-[11px] font-semibold text-rose-700 dark:text-rose-300 flex items-center justify-between">
            <span>Occupied + Protected</span>
            <UntitledIcon name="alert" size={13} />
          </div>
          <div className="text-xl font-bold text-rose-600 dark:text-rose-400 mt-1">
            {counts.dual}
          </div>
        </div>
      </div>

      {/* Owner Protected Buffer Time (x) Configuration Card */}
      <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-4 space-y-3">
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
          <div className="flex items-center gap-2.5">
            <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-[var(--primary)]/10 text-[var(--primary)]">
              <UntitledIcon name="shield" size={16} />
            </div>
            <div>
              <h3 className="font-semibold text-xs text-[var(--fg)]">
                Reservation Protection Window
              </h3>
              <p className="text-[11px] text-[var(--muted)]">
                Choose how long before a reservation the space is protected from walk-ins.
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <span className="text-xs text-[var(--muted)] font-medium">Protection window:</span>
            <div className="flex rounded-lg border border-[var(--border)] bg-[var(--bg)] p-0.5">
              {[0.5, 1.0, 1.5, 2.0].map((val) => (
                <button
                  key={val}
                  type="button"
                  onClick={() => updateProtectionWindow(val)}
                  className={`px-3 py-1 rounded-md text-xs font-semibold transition-colors ${
                    protectionWindowHours === val
                      ? "bg-[var(--primary)] text-white shadow-xs"
                      : "text-[var(--muted)] hover:text-[var(--fg)]"
                  }`}
                >
                  {val === 0.5 ? "30 minutes" : `${val} hours`}
                </button>
              ))}
            </div>
          </div>
        </div>

        {/* Formula Explainer */}
        <div className="p-3 rounded-lg border border-[var(--border)] bg-[var(--bg)]/60 text-xs text-[var(--muted)] flex items-start gap-2">
          <UntitledIcon name="help" size={14} className="text-[var(--primary)] mt-0.5 shrink-0" />
          <div className="leading-relaxed">
            <strong className="text-[var(--fg)]">Protection rule: </strong>
            For a reservation from <strong>10:00 - 12:00</strong> with a protection window of <strong>{protectionWindowHours} hours</strong>, protection starts at{" "}
            <strong className="text-indigo-600 dark:text-indigo-400">
              {protectionStartTime}
            </strong>
            . From {protectionStartTime} to 10:00, the space is <strong>Protected</strong> → Walk-ins cannot use this space.
          </div>
        </div>
      </div>

      {/* Floors, Zones & Visual Slots Grid */}
      <div className="space-y-4">
        {/* Floor and Zone Selector Tabs */}
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-[var(--border)] pb-2">
          {/* Floor tabs */}
          <div className="flex items-center gap-1.5 overflow-x-auto">
            {structure?.floors?.map((fl) => (
              <button
                key={fl.id}
                type="button"
                className={`px-3 py-1.5 rounded-lg text-xs font-semibold transition-colors flex items-center gap-1.5 ${
                  currentFloor?.id === fl.id
                    ? "bg-[var(--primary)] text-white shadow-xs"
                    : "border border-[var(--border)] bg-[var(--card)] text-[var(--muted)] hover:text-[var(--fg)]"
                }`}
                onClick={() => setSelectedFloorId(fl.id)}
              >
                <span>{fl.name}</span>
                <span className="text-[10px] opacity-75 font-mono">({fl.code})</span>
              </button>
            ))}

            <button
              type="button"
              className="px-2.5 py-1.5 rounded-lg border border-dashed border-[var(--border)] text-xs text-[var(--muted)] hover:text-[var(--fg)] flex items-center gap-1"
              onClick={() => {
                setEditingFloor(null)
                setIsFloorModalOpen(true)
              }}
            >
              <UntitledIcon name="plus" size={12} />
              <span>Add Floor</span>
            </button>
          </div>

          {/* Zone filter */}
          <div className="flex items-center gap-2 text-xs">
            <span className="text-[var(--muted)] font-medium">Zone:</span>
            <select
              className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-2.5 py-1 text-xs text-[var(--fg)] outline-none"
              value={selectedZoneFilter}
              onChange={(e) => setSelectedZoneFilter(e.target.value)}
            >
              <option value="all">All Zones ({currentFloor?.zones?.length || 0})</option>
              {currentFloor?.zones?.map((z) => (
                <option key={z.id} value={z.id}>
                  {z.name} ({z.spaces?.length || 0} slots)
                </option>
              ))}
            </select>

            <button
              type="button"
              className="btn-outline text-xs px-2.5 py-1"
              onClick={() => {
                setEditingZone(null)
                setIsZoneModalOpen(true)
              }}
            >
              + Add Zone
            </button>
          </div>
        </div>

        {/* Search bar inside floor layout */}
        <div className="flex flex-wrap items-center justify-between gap-3 p-3 rounded-xl border border-[var(--border)] bg-[var(--card)] text-xs">
          <div className="flex items-center gap-2 flex-1 min-w-[200px] max-w-sm">
            <UntitledIcon name="search" size={14} className="text-[var(--muted)] shrink-0" />
            <input
              className="w-full bg-transparent text-xs text-[var(--fg)] placeholder:text-[var(--muted)] outline-none"
              placeholder="Search by space code (e.g. A01), license plate, or driver name…"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
            />
            {searchQuery && (
              <button type="button" onClick={() => setSearchQuery("")} className="text-[var(--muted)]">
                <UntitledIcon name="x" size={12} />
              </button>
            )}
          </div>

          <div className="text-[11px] text-[var(--muted)]">
            Showing: <strong>{displayedSpaces.length}</strong> / {allSpaces.length} slots
          </div>
        </div>

        {/* Visual Slot Grid Map */}
        {displayedSpaces.length > 0 ? (
          <div className="grid grid-cols-2 sm:grid-cols-4 md:grid-cols-6 lg:grid-cols-8 gap-3">
            {displayedSpaces.map((space) => {
              const physical = space.physicalState || (space.status === "occupied" ? "OCCUPIED" : "AVAILABLE")
              const reservation =
                space.reservationState ||
                (space.isProtected
                  ? "PROTECTED"
                  : space.isBackup
                    ? "BACKUP"
                    : space.isPendingPayment
                      ? "PENDING_PAYMENT"
                      : space.status === "reserved"
                        ? "RESERVED"
                        : "NONE")

              const isOcc = physical === "OCCUPIED"
              const isProt = reservation === "PROTECTED" || space.isProtected
              const isDual = isOcc && isProt
              const isReallocated = Boolean(space.requestedSlotCode && space.requestedSlotCode !== space.code)

              // Determine primary style definition
              const def = isDual
                ? {
                    label: "Occupied + Protected",
                    bg: "rgba(225, 29, 72, 0.15)",
                    border: "rgba(225, 29, 72, 0.7)",
                    color: "#e11d48",
                  }
                : isProt
                  ? STATUS_DEFINITIONS.protected
                  : isOcc
                    ? STATUS_DEFINITIONS.occupied
                    : space.isBackup
                      ? STATUS_DEFINITIONS.backup
                      : space.isPendingPayment
                        ? STATUS_DEFINITIONS.pending_payment
                        : space.status === "reserved"
                          ? STATUS_DEFINITIONS.reserved
                          : STATUS_DEFINITIONS.available

              return (
                <div
                  key={space.id}
                  onClick={() => setInspectingSpace(space)}
                  className={`p-3 rounded-xl border-2 cursor-pointer transition-all hover:scale-[1.02] flex flex-col justify-between min-h-[110px] relative overflow-hidden ${
                    isDual ? "animate-pulse ring-2 ring-rose-500 shadow-md" : ""
                  }`}
                  style={{
                    background: def.bg,
                    borderColor: def.border,
                  }}
                >
                  {/* Top row: Code & Vehicle Icon */}
                  <div className="flex items-center justify-between">
                    <span className="font-mono font-bold text-sm text-[var(--fg)]">{space.code}</span>
                    <span className="text-xs" title={space.vehicleType}>
                      {space.vehicleType === "motorcycle" ? "🏍️" : "🚗"}
                    </span>
                  </div>

                  {/* Middle row: Badges */}
                  <div className="my-1.5 space-y-1">
                    <div
                      className="inline-block px-1.5 py-0.5 rounded text-[10px] font-bold uppercase truncate max-w-full"
                      style={{ color: def.color }}
                    >
                      {def.label}
                    </div>

                    {isReallocated && (
                      <div className="text-[9px] font-mono px-1 py-0.5 rounded bg-emerald-500/20 text-emerald-700 dark:text-emerald-300">
                        ⚡ Reallocated from {space.requestedSlotCode}
                      </div>
                    )}

                    {space.currentBooking?.licensePlate && (
                      <div className="text-[10px] font-mono text-[var(--fg)] truncate">
                        {space.currentBooking.licensePlate}
                      </div>
                    )}
                  </div>

                  {/* Bottom row: Zone name */}
                  <div className="text-[9px] text-[var(--muted)] truncate">{space.zoneName}</div>
                </div>
              )
            })}
          </div>
        ) : (
          <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-12 text-center text-xs text-[var(--muted)]">
            No parking spaces match the current filters.
          </div>
        )}
      </div>

      {/* Modal 1: Space Detail Modal */}
      {inspectingSpace && (
        <SpaceDetailModal
          space={inspectingSpace}
          floorName={currentFloor?.name || "Ground Floor"}
          floorCode={currentFloor?.code || "G"}
          zoneName={currentFloor?.zones?.find((z) => z.id === inspectingSpace.zoneId)?.name || "Zone"}
          onClose={() => setInspectingSpace(null)}
          onUpdateStatus={(spaceId, newStatus, note, options) => {
            structureStore.updateSpaceStatus(activeLotId, spaceId, newStatus, note, options)
            reloadStructure()
            setInspectingSpace(null)
          }}
        />
      )}

      {/* Modal 2: Floor Create/Edit Modal */}
      <FloorModal
        isOpen={isFloorModalOpen}
        initialData={editingFloor}
        onClose={() => {
          setIsFloorModalOpen(false)
          setEditingFloor(null)
        }}
        onSave={(name, code) => {
          structureStore.createFloor(activeLotId, name, code)
          reloadStructure()
          setIsFloorModalOpen(false)
        }}
      />

      {/* Modal 3: Zone Create/Edit Modal */}
      <ZoneModal
        isOpen={isZoneModalOpen}
        floorCode={currentFloor?.code || "G"}
        initialData={editingZone}
        onClose={() => {
          setIsZoneModalOpen(false)
          setEditingZone(null)
        }}
        onSave={(name, vehicleType, initialSpaces) => {
          if (!currentFloor) return
          structureStore.createZone(activeLotId, currentFloor.id, name, vehicleType, initialSpaces || 12)
          reloadStructure()
          setIsZoneModalOpen(false)
        }}
      />
    </section>
  )
}
