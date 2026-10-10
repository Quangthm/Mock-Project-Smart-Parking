import { useState, useMemo, useEffect } from "react"
import { structureStore } from "../data/structureStore"
import type {
  ParkingFloorItem,
  ParkingZoneItem,
  ParkingSpaceItem,
  ParkingStructureData,
  SpaceStatus,
  VehicleCategory,
  PhysicalState,
  ReservationProtectionState,
} from "../../../lib/structureTypes"
import type { ParkingLot } from "../../../lib/types"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { FloorModal } from "./FloorModal"
import { ZoneModal } from "./ZoneModal"
import { SpaceDetailModal } from "./SpaceDetailModal"

interface ParkingStructureProps {
  sites?: ParkingLot[]
  selectedSiteId?: string
}

export function ParkingStructure({
  sites = [],
  selectedSiteId = "all",
}: ParkingStructureProps) {
  // Available lots
  const availableLots = useMemo(() => {
    if (sites && sites.length > 0) return sites
    return [
      {
        id: "lot-002",
        name: "Bitexco Financial Tower Outdoor Lot",
        address: "2 Hải Triều, Bến Nghé, Quận 1, TP. Hồ Chí Minh",
      } as ParkingLot,
      {
        id: "lot-001",
        name: "Vinhomes Grand Park Parking Center",
        address:
          "Khu đô thị Vinhomes Grand Park, Phường Long Thạnh Mỹ, TP. Thủ Đức",
      } as ParkingLot,
      {
        id: "lot-003",
        name: "Nguyen Hue Boulevard Outdoor Lot",
        address: "68 Nguyễn Huệ, Bến Nghé, Quận 1, TP. Hồ Chí Minh",
      } as ParkingLot,
    ]
  }, [sites])

  const [activeLotId, setActiveLotId] = useState<string>(() => {
    if (selectedSiteId && selectedSiteId !== "all") return selectedSiteId
    return availableLots[0]?.id || "lot-002"
  })

  // Synchronize with external selectedSiteId
  useEffect(() => {
    if (selectedSiteId && selectedSiteId !== "all") {
      setActiveLotId(selectedSiteId)
    }
  }, [selectedSiteId])

  // Synchronize when availableLots change
  useEffect(() => {
    if (
      availableLots.length > 0 &&
      !availableLots.some((l) => l.id === activeLotId)
    ) {
      setActiveLotId(availableLots[0].id)
    }
  }, [availableLots, activeLotId])

  // Current Structure Data
  const [structure, setStructure] = useState<ParkingStructureData>(() =>
    structureStore.getStructure(activeLotId),
  )

  const reloadStructure = () => {
    setStructure(structureStore.getStructure(activeLotId))
  }

  // Reload when lot changes
  useEffect(() => {
    const s = structureStore.getStructure(activeLotId)
    setStructure(s)
    setSelectedFloorId(s.floors?.[0]?.id || "")
    setSelectedZoneFilter("all")
  }, [activeLotId])

  // Listen to store change events across tabs or components
  useEffect(() => {
    const handler = () => {
      setStructure(structureStore.getStructure(activeLotId))
    }
    window.addEventListener("sp-structure-change", handler)
    return () => window.removeEventListener("sp-structure-change", handler)
  }, [activeLotId])

  // Active Floor Selection
  const [selectedFloorId, setSelectedFloorId] = useState<string>("")

  useEffect(() => {
    if (structure?.floors && structure.floors.length > 0) {
      if (!structure.floors.some((f) => f.id === selectedFloorId)) {
        setSelectedFloorId(structure.floors[0].id)
      }
    } else {
      setSelectedFloorId("")
    }
  }, [structure, selectedFloorId])

  // Active Zone Selection ('all' or specific zoneId)
  const [selectedZoneFilter, setSelectedZoneFilter] = useState<string>("all")

  // Search & Filters
  const [searchQuery, setSearchQuery] = useState("")
  const [statusFilter, setStatusFilter] =
    useState<"all" | SpaceStatus | "protected" | "backup" | "pending_payment">(
      "all",
    )
  const [vehicleFilter, setVehicleFilter] = useState<"all" | VehicleCategory>(
    "all",
  )

  // Capacity Accounting Category Switcher
  const [capacityCategory, setCapacityCategory] =
    useState<"all" | "car" | "motorcycle">("all")

  // Modal States
  const [isFloorModalOpen, setIsFloorModalOpen] = useState(false)
  const [editingFloor, setEditingFloor] = useState<ParkingFloorItem | null>(
    null,
  )

  const [isZoneModalOpen, setIsZoneModalOpen] = useState(false)
  const [editingZone, setEditingZone] = useState<ParkingZoneItem | null>(null)

  const [deletingTarget, setDeletingTarget] = useState<{
    type: "floor" | "zone"
    id: string
    name: string
  } | null>(null)

  const [inspectingSpace, setInspectingSpace] =
    useState<ParkingSpaceItem | null>(null)

  // Computed Current Floor & Zones
  const currentFloor = useMemo(() => {
    if (!structure?.floors || structure.floors.length === 0) return null
    return (
      structure.floors.find((f) => f.id === selectedFloorId) ||
      structure.floors[0] ||
      null
    )
  }, [structure, selectedFloorId])

  // Authoritative SRS Structure Stats
  const stats = useMemo(
    () => structureStore.calculateStats(structure),
    [structure],
  )

  // Active stats mapped according to category switcher
  const activeStats = useMemo(() => {
    if (capacityCategory === "car") {
      return {
        total: stats.byCategory.car.total,
        occupied: stats.byCategory.car.occupied,
        reserved: stats.byCategory.car.reserved,
        protected: stats.byCategory.car.protected,
        pendingPayment: stats.byCategory.car.pendingPayment,
        backup: stats.byCategory.car.backup,
        maintenance: stats.byCategory.car.maintenance,
        available: stats.byCategory.car.available,
      }
    }
    if (capacityCategory === "motorcycle") {
      return {
        total: stats.byCategory.motorcycle.total,
        occupied: stats.byCategory.motorcycle.occupied,
        reserved: stats.byCategory.motorcycle.reserved,
        protected: stats.byCategory.motorcycle.protected,
        pendingPayment: stats.byCategory.motorcycle.pendingPayment,
        backup: stats.byCategory.motorcycle.backup,
        maintenance: stats.byCategory.motorcycle.maintenance,
        available: stats.byCategory.motorcycle.available,
      }
    }
    return {
      total: stats.totalSpaces,
      occupied: stats.occupiedSpaces,
      reserved: stats.reservedSpaces,
      protected: stats.protectedSpaces,
      pendingPayment: stats.pendingPaymentHolds,
      backup: stats.backupCapacity,
      maintenance: stats.maintenanceSpaces + stats.disabledSpaces,
      available: stats.availableCapacity,
    }
  }, [stats, capacityCategory])

  // Detect Operational Conflicts (SRS 8.2 & 9)
  const operationalConflicts = useMemo(() => {
    const list: Array<{
      space: ParkingSpaceItem
      floorName: string
      zoneName: string
      reason: string
    }> = []
    if (!structure?.floors) return list
    structure.floors.forEach((floor) => {
      if (!floor.zones) return
      floor.zones.forEach((zone) => {
        if (!zone.spaces) return
        zone.spaces.forEach((space) => {
          const physical =
            space.physicalState ||
            (space.status === "occupied" ? "OCCUPIED" : "AVAILABLE")
          const res =
            space.reservationState || (space.isProtected ? "PROTECTED" : "NONE")
          if (
            physical === "OCCUPIED" &&
            (res === "PROTECTED" || space.isProtected)
          ) {
            list.push({
              space,
              floorName: floor.name,
              zoneName: zone.name,
              reason:
                space.conflictReason ||
                `Simultaneous Occupied + Protected hold conflict (Incoming reservation: ${space.currentBooking?.licensePlate || "Driver"})`,
            })
          }
        })
      })
    })
    return list
  }, [structure])

  // Spaces Filtering
  const displayedSpaces = useMemo(() => {
    if (!currentFloor || !currentFloor.zones) return []

    let zones = currentFloor.zones
    if (selectedZoneFilter !== "all") {
      zones = zones.filter((z) => z.id === selectedZoneFilter)
    }

    const allSpaces: Array<ParkingSpaceItem & {
      floorCode: string
      floorName: string
      zoneName: string
    }> = []

    zones.forEach((zone) => {
      if (!zone.spaces) return
      zone.spaces.forEach((space) => {
        allSpaces.push({
          ...space,
          floorCode: currentFloor.code,
          floorName: currentFloor.name,
          zoneName: zone.name,
        })
      })
    })

    return allSpaces.filter((space) => {
      if (searchQuery.trim()) {
        const query = searchQuery.trim().toLowerCase()
        const matchesCode = space.code.toLowerCase().includes(query)
        const matchesZone = space.zoneName.toLowerCase().includes(query)
        const matchesPlate = space.currentBooking?.licensePlate
          ?.toLowerCase()
          .includes(query)
        const matchesReq = space.requestedSlotCode
          ?.toLowerCase()
          .includes(query)
        if (!matchesCode && !matchesZone && !matchesPlate && !matchesReq)
          return false
      }

      if (statusFilter !== "all") {
        const physical =
          space.physicalState ||
          (space.status === "occupied"
            ? "OCCUPIED"
            : space.status === "maintenance"
              ? "MAINTENANCE"
              : space.status === "disabled"
                ? "UNAVAILABLE"
                : "AVAILABLE")
        const res =
          space.reservationState ||
          (space.isProtected
            ? "PROTECTED"
            : space.isPendingPayment
              ? "PENDING_PAYMENT"
              : space.isBackup
                ? "BACKUP"
                : space.status === "reserved"
                  ? "RESERVED"
                  : "NONE")

        if (statusFilter === "available") {
          if (physical !== "AVAILABLE" || res !== "NONE") return false
        } else if (statusFilter === "occupied") {
          if (physical !== "OCCUPIED") return false
        } else if (statusFilter === "reserved") {
          if (res !== "RESERVED") return false
        } else if (statusFilter === "protected") {
          if (res !== "PROTECTED" && !space.isProtected) return false
        } else if (statusFilter === "backup") {
          if (res !== "BACKUP" && !space.isBackup) return false
        } else if (statusFilter === "pending_payment") {
          if (res !== "PENDING_PAYMENT" && !space.isPendingPayment) return false
        } else if (statusFilter === "maintenance") {
          if (physical !== "MAINTENANCE") return false
        } else if (statusFilter === "disabled") {
          if (physical !== "UNAVAILABLE") return false
        }
      }

      if (vehicleFilter !== "all" && space.vehicleType !== vehicleFilter) {
        return false
      }
      return true
    })
  }, [
    currentFloor,
    selectedZoneFilter,
    searchQuery,
    statusFilter,
    vehicleFilter,
  ])

  // Floor Handlers
  const handleSaveFloor = (
    name: string,
    code: string,
    status?: "active" | "inactive",
  ) => {
    if (editingFloor) {
      structureStore.updateFloor(activeLotId, editingFloor.id, {
        name,
        code,
        status,
      })
    } else {
      const created = structureStore.addFloor(activeLotId, name, code)
      if (created) setSelectedFloorId(created.id)
    }
    reloadStructure()
    setEditingFloor(null)
  }

  const handleConfirmDelete = () => {
    if (!deletingTarget) return
    if (deletingTarget.type === "floor") {
      structureStore.deleteFloor(activeLotId, deletingTarget.id)
      const updated = structureStore.getStructure(activeLotId)
      setStructure(updated)
      if (selectedFloorId === deletingTarget.id) {
        setSelectedFloorId(updated.floors?.[0]?.id || "")
      }
    } else if (deletingTarget.type === "zone" && currentFloor) {
      structureStore.deleteZone(activeLotId, currentFloor.id, deletingTarget.id)
      reloadStructure()
    }
    setDeletingTarget(null)
  }

  // Zone Handlers
  const handleSaveZone = (
    name: string,
    vehicleType: VehicleCategory,
    initialSpaces?: number,
    status?: "active" | "inactive",
  ) => {
    if (!currentFloor) return
    if (editingZone) {
      structureStore.updateZone(activeLotId, currentFloor.id, editingZone.id, {
        name,
        vehicleType,
        status,
      })
    } else {
      structureStore.addZone(
        activeLotId,
        currentFloor.id,
        name,
        vehicleType,
        initialSpaces || 12,
      )
    }
    reloadStructure()
    setEditingZone(null)
  }

  // Space Status Update
  const handleUpdateSpaceStatus = (
    spaceId: string,
    newStatus: SpaceStatus,
    note?: string,
    options?: {
      physicalState?: PhysicalState
      reservationState?: ReservationProtectionState
      isProtected?: boolean
      isBackup?: boolean
    },
  ) => {
    structureStore.updateSpaceStatus(
      activeLotId,
      spaceId,
      newStatus,
      note,
      options,
    )
    reloadStructure()
    if (inspectingSpace && inspectingSpace.id === spaceId) {
      setInspectingSpace((prev) =>
        prev
          ? {
              ...prev,
              status: newStatus,
              maintenanceNote: note,
              ...(options?.physicalState
                ? { physicalState: options.physicalState }
                : {}),
              ...(options?.reservationState
                ? { reservationState: options.reservationState }
                : {}),
              ...(options?.isProtected !== undefined
                ? { isProtected: options.isProtected }
                : {}),
              ...(options?.isBackup !== undefined
                ? { isBackup: options.isBackup }
                : {}),
            }
          : null,
      )
    }
  }

  // Active Policy Values safely accessed with defaults
  const protectionWindow =
    structure?.policy?.reservationProtectionWindowMinutes ??
    (structure?.policy?.protectionWindowHours
      ? structure.policy.protectionWindowHours * 60
      : 60)
  const allocationLead = structure?.policy?.allocationLeadTimeMinutes ?? 30
  const occupancyValidity =
    structure?.policy?.occupancyValidityTimespanHours ??
    structure?.policy?.occupancyValidityHours ??
    4
  const backupBuffer = structure?.policy?.backupCapacityPercentage ?? 8

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* SECTION A: LOT HEADER & AUTHORITATIVE SRS CAPACITY ACCOUNTING */}
      <div
        className="card"
        style={{
          display: "flex",
          flexDirection: "column",
          gap: "1rem",
          padding: "1.25rem",
        }}
      >
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "flex-start",
            flexWrap: "wrap",
            gap: "1rem",
          }}
        >
          <div style={{ flex: "1 1 320px" }}>
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: "0.5rem",
                marginBottom: "0.25rem",
              }}
            >
              <span
                style={{
                  fontSize: "0.72rem",
                  fontWeight: 700,
                  color: "var(--primary)",
                  letterSpacing: "0.08em",
                  textTransform: "uppercase",
                }}
              >
                Owner Operations · Parking Structure & Capacity
              </span>
              <span
                style={{
                  fontSize: "0.7rem",
                  padding: "0.1rem 0.45rem",
                  borderRadius: "999px",
                  background: "#22c55e20",
                  color: "#16a34a",
                  fontWeight: 700,
                }}
              >
                SRS v0.9 COMPLIANT
              </span>
            </div>
            <h2
              style={{
                fontFamily: "Outfit",
                fontWeight: 700,
                fontSize: "1.35rem",
                margin: "0 0 0.25rem",
                color: "var(--fg)",
              }}
            >
              {structure?.lotName || "Parking Lot"}
            </h2>
            <p
              style={{
                margin: 0,
                fontSize: "0.82rem",
                color: "var(--muted)",
                display: "flex",
                alignItems: "center",
                gap: "0.35rem",
              }}
            >
              <UntitledIcon name="map-pin" size={14} />
              {structure?.address || "Facility Address"}
            </p>
          </div>

          {/* Lot Selector */}
          <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
            <label
              style={{
                fontSize: "0.82rem",
                fontWeight: 600,
                color: "var(--muted)",
              }}
            >
              Facility:
            </label>
            <select
              className="input"
              style={{
                minWidth: 230,
                fontSize: "0.85rem",
                padding: "0.45rem 0.75rem",
              }}
              value={activeLotId}
              onChange={(e) => setActiveLotId(e.target.value)}
            >
              {availableLots.map((lot) => (
                <option key={lot.id} value={lot.id}>
                  {lot.name}
                </option>
              ))}
            </select>
          </div>
        </div>

        {/* Capacity Category Switcher Bar & Invariant Formula */}
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "wrap",
            gap: "0.75rem",
            paddingTop: "0.25rem",
          }}
        >
          <div style={{ display: "flex", alignItems: "center", gap: "0.4rem" }}>
            <span
              style={{
                fontSize: "0.78rem",
                fontWeight: 600,
                color: "var(--muted)",
              }}
            >
              View Accounting:
            </span>
            <div
              style={{
                display: "inline-flex",
                background: "var(--bg)",
                padding: "2px",
                borderRadius: "6px",
                border: "1px solid var(--border)",
              }}
            >
              <button
                type="button"
                onClick={() => setCapacityCategory("all")}
                style={{
                  padding: "0.25rem 0.65rem",
                  fontSize: "0.75rem",
                  fontWeight: 600,
                  borderRadius: "4px",
                  border: "none",
                  background:
                    capacityCategory === "all" ? "var(--card)" : "transparent",
                  color:
                    capacityCategory === "all" ? "var(--fg)" : "var(--muted)",
                  cursor: "pointer",
                  boxShadow:
                    capacityCategory === "all"
                      ? "0 1px 3px rgba(0,0,0,0.1)"
                      : "none",
                }}
              >
                All Vehicle Types
              </button>
              <button
                type="button"
                onClick={() => setCapacityCategory("car")}
                style={{
                  padding: "0.25rem 0.65rem",
                  fontSize: "0.75rem",
                  fontWeight: 600,
                  borderRadius: "4px",
                  border: "none",
                  background:
                    capacityCategory === "car" ? "var(--card)" : "transparent",
                  color:
                    capacityCategory === "car" ? "var(--fg)" : "var(--muted)",
                  cursor: "pointer",
                  boxShadow:
                    capacityCategory === "car"
                      ? "0 1px 3px rgba(0,0,0,0.1)"
                      : "none",
                }}
              >
                Cars (Ô tô)
              </button>
              <button
                type="button"
                onClick={() => setCapacityCategory("motorcycle")}
                style={{
                  padding: "0.25rem 0.65rem",
                  fontSize: "0.75rem",
                  fontWeight: 600,
                  borderRadius: "4px",
                  border: "none",
                  background:
                    capacityCategory === "motorcycle"
                      ? "var(--card)"
                      : "transparent",
                  color:
                    capacityCategory === "motorcycle"
                      ? "var(--fg)"
                      : "var(--muted)",
                  cursor: "pointer",
                  boxShadow:
                    capacityCategory === "motorcycle"
                      ? "0 1px 3px rgba(0,0,0,0.1)"
                      : "none",
                }}
              >
                Motorcycles (Xe máy)
              </button>
            </div>
          </div>

          <div
            style={{
              fontSize: "0.72rem",
              color: "var(--muted)",
              fontStyle: "italic",
            }}
          >
            Invariant: Available = Total − Occupied − Protected − Pending
            Payment − Backup
          </div>
        </div>

        {/* Authoritative Capacity Accounting Cards (SRS 8.3 & 3.4.5) */}
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(130px, 1fr))",
            gap: "0.625rem",
          }}
        >
          {/* Total Capacity */}
          <div
            style={{
              background: "var(--bg)",
              border: "1px solid var(--border)",
              borderRadius: "0.5rem",
              padding: "0.75rem 0.875rem",
            }}
          >
            <div
              style={{
                fontSize: "0.72rem",
                color: "var(--muted)",
                fontWeight: 600,
                display: "flex",
                alignItems: "center",
                gap: "0.3rem",
              }}
            >
              <UntitledIcon name="grid" size={13} /> Total Capacity
            </div>
            <div
              style={{
                fontSize: "1.25rem",
                fontWeight: 800,
                fontFamily: "Outfit",
                marginTop: "0.2rem",
                color: "var(--fg)",
              }}
            >
              {activeStats.total}
            </div>
            <span style={{ fontSize: "0.68rem", color: "var(--muted)" }}>
              {stats.totalFloors} floors defined
            </span>
          </div>

          {/* Available Capacity (Authoritative Invariant) */}
          <div
            style={{
              background: "var(--bg)",
              border: "1px solid #22c55e50",
              borderRadius: "0.5rem",
              padding: "0.75rem 0.875rem",
            }}
          >
            <div
              style={{
                fontSize: "0.72rem",
                color: "#16a34a",
                fontWeight: 700,
                display: "flex",
                alignItems: "center",
                gap: "0.3rem",
              }}
            >
              <UntitledIcon name="check-circle" size={13} /> Available Capacity
            </div>
            <div
              style={{
                fontSize: "1.25rem",
                fontWeight: 800,
                fontFamily: "Outfit",
                marginTop: "0.2rem",
                color: "#16a34a",
              }}
            >
              {activeStats.available}
            </div>
            <span style={{ fontSize: "0.68rem", color: "#16a34a" }}>
              Authoritative walk-in
            </span>
          </div>

          {/* Occupied Capacity */}
          <div
            style={{
              background: "var(--bg)",
              border: "1px solid #ef444440",
              borderRadius: "0.5rem",
              padding: "0.75rem 0.875rem",
            }}
          >
            <div
              style={{
                fontSize: "0.72rem",
                color: "#dc2626",
                fontWeight: 600,
                display: "flex",
                alignItems: "center",
                gap: "0.3rem",
              }}
            >
              <UntitledIcon name="car" size={13} /> Occupied
            </div>
            <div
              style={{
                fontSize: "1.25rem",
                fontWeight: 800,
                fontFamily: "Outfit",
                marginTop: "0.2rem",
                color: "#dc2626",
              }}
            >
              {activeStats.occupied}
            </div>
            <span style={{ fontSize: "0.68rem", color: "var(--muted)" }}>
              Sensor / Physical present
            </span>
          </div>

          {/* Protected Capacity */}
          <div
            style={{
              background: "var(--bg)",
              border: "1px solid #6366f140",
              borderRadius: "0.5rem",
              padding: "0.75rem 0.875rem",
            }}
          >
            <div
              style={{
                fontSize: "0.72rem",
                color: "#6366f1",
                fontWeight: 600,
                display: "flex",
                alignItems: "center",
                gap: "0.3rem",
              }}
            >
              <UntitledIcon name="shield" size={13} /> Protected Holds
            </div>
            <div
              style={{
                fontSize: "1.25rem",
                fontWeight: 800,
                fontFamily: "Outfit",
                marginTop: "0.2rem",
                color: "#6366f1",
              }}
            >
              {activeStats.protected}
            </div>
            <span style={{ fontSize: "0.68rem", color: "var(--muted)" }}>
              In {protectionWindow}m protection window
            </span>
          </div>

          {/* Reserved Capacity */}
          <div
            style={{
              background: "var(--bg)",
              border: "1px solid #f59e0b40",
              borderRadius: "0.5rem",
              padding: "0.75rem 0.875rem",
            }}
          >
            <div
              style={{
                fontSize: "0.72rem",
                color: "#d97706",
                fontWeight: 600,
                display: "flex",
                alignItems: "center",
                gap: "0.3rem",
              }}
            >
              <UntitledIcon name="clock" size={13} /> Reserved
            </div>
            <div
              style={{
                fontSize: "1.25rem",
                fontWeight: 800,
                fontFamily: "Outfit",
                marginTop: "0.2rem",
                color: "#d97706",
              }}
            >
              {activeStats.reserved}
            </div>
            <span style={{ fontSize: "0.68rem", color: "var(--muted)" }}>
              Outside protection lead
            </span>
          </div>

          {/* Pending-Payment Holds */}
          <div
            style={{
              background: "var(--bg)",
              border: "1px solid #eab30840",
              borderRadius: "0.5rem",
              padding: "0.75rem 0.875rem",
            }}
          >
            <div
              style={{
                fontSize: "0.72rem",
                color: "#ca8a04",
                fontWeight: 600,
                display: "flex",
                alignItems: "center",
                gap: "0.3rem",
              }}
            >
              <UntitledIcon name="banknote" size={13} /> Pending Holds
            </div>
            <div
              style={{
                fontSize: "1.25rem",
                fontWeight: 800,
                fontFamily: "Outfit",
                marginTop: "0.2rem",
                color: "#ca8a04",
              }}
            >
              {activeStats.pendingPayment}
            </div>
            <span style={{ fontSize: "0.68rem", color: "var(--muted)" }}>
              15m payment checkout
            </span>
          </div>

          {/* Backup Inventory */}
          <div
            style={{
              background: "var(--bg)",
              border: "1px solid #8b5cf640",
              borderRadius: "0.5rem",
              padding: "0.75rem 0.875rem",
            }}
          >
            <div
              style={{
                fontSize: "0.72rem",
                color: "#7c3aed",
                fontWeight: 600,
                display: "flex",
                alignItems: "center",
                gap: "0.3rem",
              }}
            >
              <UntitledIcon name="shield" size={13} /> Backup Inventory
            </div>
            <div
              style={{
                fontSize: "1.25rem",
                fontWeight: 800,
                fontFamily: "Outfit",
                marginTop: "0.2rem",
                color: "#7c3aed",
              }}
            >
              {activeStats.backup}
            </div>
            <span style={{ fontSize: "0.68rem", color: "var(--muted)" }}>
              Designated contingency
            </span>
          </div>

          {/* Maintenance */}
          <div
            style={{
              background: "var(--bg)",
              border: "1px solid #f9731640",
              borderRadius: "0.5rem",
              padding: "0.75rem 0.875rem",
            }}
          >
            <div
              style={{
                fontSize: "0.72rem",
                color: "#ea580c",
                fontWeight: 600,
                display: "flex",
                alignItems: "center",
                gap: "0.3rem",
              }}
            >
              <UntitledIcon name="wrench" size={13} /> Maintenance
            </div>
            <div
              style={{
                fontSize: "1.25rem",
                fontWeight: 800,
                fontFamily: "Outfit",
                marginTop: "0.2rem",
                color: "#ea580c",
              }}
            >
              {activeStats.maintenance}
            </div>
            <span style={{ fontSize: "0.68rem", color: "var(--muted)" }}>
              Sensor / Slot repair
            </span>
          </div>
        </div>

        {/* SRS Policy Parameters Summary Strip (SRS Section 9) */}
        <div
          style={{
            background: "var(--bg)",
            border: "1px solid var(--border)",
            borderRadius: "0.5rem",
            padding: "0.65rem 0.875rem",
            display: "flex",
            flexWrap: "wrap",
            alignItems: "center",
            justifyContent: "space-between",
            gap: "0.75rem",
            fontSize: "0.76rem",
          }}
        >
          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: "0.4rem",
              fontWeight: 700,
              color: "var(--primary)",
            }}
          >
            <UntitledIcon name="settings" size={13} />
            <span>SRS Policy Boundaries:</span>
          </div>
          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: "1rem",
              flexWrap: "wrap",
              color: "var(--muted)",
            }}
          >
            <span>
              Protection Window:{" "}
              <strong style={{ color: "var(--fg)" }}>
                {protectionWindow} mins
              </strong>
            </span>
            <span>
              Allocation Lead:{" "}
              <strong style={{ color: "var(--fg)" }}>
                {allocationLead} mins
              </strong>
            </span>
            <span>
              Occupancy Validity:{" "}
              <strong style={{ color: "var(--fg)" }}>
                {occupancyValidity} hrs
              </strong>
            </span>
            <span>
              Backup Target:{" "}
              <strong style={{ color: "var(--fg)" }}>
                {backupBuffer}% buffer
              </strong>
            </span>
            <span>
              Hierarchy:{" "}
              <strong style={{ color: "var(--fg)" }}>
                Earliest Start Time → Earliest Confirmation
              </strong>
            </span>
          </div>
        </div>

        {/* OPERATIONAL CONFLICT ALERT BANNER (SRS 8.2 & 9) */}
        {operationalConflicts.length > 0 && (
          <div
            style={{
              background: "color-mix(in srgb, #ef4444 10%, var(--card))",
              border: "1.5px solid #ef4444",
              borderRadius: "0.65rem",
              padding: "0.85rem 1rem",
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
              flexWrap: "wrap",
              gap: "0.75rem",
            }}
          >
            <div
              style={{
                display: "flex",
                alignItems: "flex-start",
                gap: "0.65rem",
              }}
            >
              <span style={{ color: "#ef4444", marginTop: "0.1rem" }}>
                <UntitledIcon name="alert" size={20} />
              </span>
              <div>
                <div
                  style={{
                    fontWeight: 700,
                    fontSize: "0.85rem",
                    color: "#ef4444",
                  }}
                >
                  SRS Policy Notice: {operationalConflicts.length} Operational
                  Conflict Active
                </div>
                <p
                  style={{
                    margin: "0.15rem 0 0",
                    fontSize: "0.78rem",
                    color: "var(--fg)",
                    lineHeight: 1.45,
                  }}
                >
                  Slot <strong>{operationalConflicts[0].space.code}</strong> (
                  {operationalConflicts[0].floorName}) is physically OCCUPIED
                  while active PROTECTED reservation is scheduled. Per SRS v0.9
                  baseline, occupying vehicle is not automatically displaced.
                  Reallocation to designated backup slot or operator
                  intervention recommended.
                </p>
              </div>
            </div>
            <button
              type="button"
              className="btn-outline"
              style={{
                fontSize: "0.76rem",
                borderColor: "#ef4444",
                color: "#ef4444",
                padding: "0.3rem 0.75rem",
              }}
              onClick={() => setInspectingSpace(operationalConflicts[0].space)}
            >
              Inspect Slot {operationalConflicts[0].space.code}
            </button>
          </div>
        )}
      </div>

      {/* SECTION B: FLOOR MANAGEMENT */}
      <div
        className="card"
        style={{
          padding: "1.25rem",
          display: "flex",
          flexDirection: "column",
          gap: "1rem",
        }}
      >
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "wrap",
            gap: "0.75rem",
          }}
        >
          <div>
            <h3
              style={{
                fontFamily: "Outfit",
                fontWeight: 700,
                fontSize: "1.1rem",
                margin: 0,
                color: "var(--fg)",
              }}
            >
              Floors & Levels ({structure?.floors?.length || 0})
            </h3>
            <p
              style={{
                margin: "0.15rem 0 0",
                fontSize: "0.78rem",
                color: "var(--muted)",
              }}
            >
              Select a floor level to view zones and manage parking grid slots.
            </p>
          </div>
          <button
            type="button"
            className="btn-primary"
            style={{ fontSize: "0.82rem", padding: "0.45rem 0.85rem" }}
            onClick={() => {
              setEditingFloor(null)
              setIsFloorModalOpen(true)
            }}
          >
            <UntitledIcon name="plus" size={15} /> Add Floor
          </button>
        </div>

        {/* Floor Cards / Tabs Strip */}
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fill, minmax(260px, 1fr))",
            gap: "0.75rem",
          }}
        >
          {(structure?.floors || []).map((floor) => {
            const isSelected = currentFloor?.id === floor.id
            const floorSpaces = (floor.zones || []).flatMap(
              (z) => z.spaces || [],
            )
            const avail = floorSpaces.filter((s) => {
              const p =
                s.physicalState ||
                (s.status === "available" ? "AVAILABLE" : "OCCUPIED")
              const r = s.reservationState || "NONE"
              return p === "AVAILABLE" && r === "NONE"
            }).length
            const occ = floorSpaces.filter(
              (s) => s.physicalState === "OCCUPIED" || s.status === "occupied",
            ).length

            return (
              <div
                key={floor.id}
                onClick={() => setSelectedFloorId(floor.id)}
                style={{
                  border: `2px solid ${
                    isSelected ? "var(--primary)" : "var(--border)"
                  }`,
                  background: isSelected
                    ? "color-mix(in srgb, var(--primary) 7%, var(--card))"
                    : "var(--bg)",
                  borderRadius: "0.75rem",
                  padding: "0.875rem 1rem",
                  cursor: "pointer",
                  transition: "all 0.15s ease",
                  position: "relative",
                }}
              >
                <div
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "flex-start",
                    marginBottom: "0.35rem",
                  }}
                >
                  <div
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: "0.4rem",
                    }}
                  >
                    <span
                      style={{
                        fontFamily: "Outfit",
                        fontWeight: 800,
                        fontSize: "1rem",
                        color: isSelected ? "var(--primary)" : "var(--fg)",
                      }}
                    >
                      [{floor.code}] {floor.name}
                    </span>
                  </div>
                  <span
                    style={{
                      fontSize: "0.68rem",
                      fontWeight: 700,
                      padding: "0.1rem 0.4rem",
                      borderRadius: "999px",
                      background:
                        floor.status === "active" ? "#22c55e18" : "#ef444418",
                      color: floor.status === "active" ? "#16a34a" : "#dc2626",
                    }}
                  >
                    {floor.status.toUpperCase()}
                  </span>
                </div>

                <div
                  style={{
                    display: "flex",
                    gap: "0.85rem",
                    fontSize: "0.78rem",
                    color: "var(--muted)",
                    marginTop: "0.4rem",
                  }}
                >
                  <span>{(floor.zones || []).length} Zones</span>
                  <span>{floorSpaces.length} Spaces</span>
                  <span style={{ color: "#16a34a" }}>● {avail} free</span>
                  <span style={{ color: "#dc2626" }}>● {occ} occ</span>
                </div>

                {/* Floor Controls */}
                <div
                  style={{
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "flex-end",
                    gap: "0.35rem",
                    marginTop: "0.65rem",
                    paddingTop: "0.5rem",
                    borderTop: "1px solid var(--border)",
                  }}
                  onClick={(e) => e.stopPropagation()}
                >
                  <button
                    type="button"
                    title="Edit Floor"
                    className="btn-outline"
                    style={{
                      fontSize: "0.72rem",
                      padding: "0.2rem 0.5rem",
                      border: "1px solid var(--border)",
                    }}
                    onClick={() => {
                      setEditingFloor(floor)
                      setIsFloorModalOpen(true)
                    }}
                  >
                    Edit
                  </button>
                  <button
                    type="button"
                    title={
                      floor.status === "active" ? "Deactivate" : "Activate"
                    }
                    style={{
                      fontSize: "0.72rem",
                      padding: "0.2rem 0.5rem",
                      borderRadius: "var(--radius)",
                      border: "1px solid var(--border)",
                      background: "transparent",
                      color: floor.status === "active" ? "#ea580c" : "#16a34a",
                      cursor: "pointer",
                    }}
                    onClick={() => {
                      structureStore.updateFloor(activeLotId, floor.id, {
                        status:
                          floor.status === "active" ? "inactive" : "active",
                      })
                      reloadStructure()
                    }}
                  >
                    {floor.status === "active" ? "Deactivate" : "Activate"}
                  </button>
                  <button
                    type="button"
                    title="Delete Floor"
                    style={{
                      fontSize: "0.72rem",
                      padding: "0.2rem 0.45rem",
                      borderRadius: "var(--radius)",
                      border: "1px solid #ef444440",
                      background: "transparent",
                      color: "#dc2626",
                      cursor: "pointer",
                    }}
                    onClick={() =>
                      setDeletingTarget({
                        type: "floor",
                        id: floor.id,
                        name: `Floor ${floor.code} (${floor.name})`,
                      })
                    }
                  >
                    Delete
                  </button>
                </div>
              </div>
            )
          })}

          {(!structure?.floors || structure.floors.length === 0) && (
            <div
              style={{
                gridColumn: "1 / -1",
                textAlign: "center",
                padding: "2rem",
                color: "var(--muted)",
              }}
            >
              No floors defined yet. Click "+ Add Floor" to begin configuring
              this parking structure.
            </div>
          )}
        </div>
      </div>

      {/* SECTION C & D: ZONE MANAGEMENT & PARKING SPACE VISUAL GRID */}
      {currentFloor && (
        <div
          className="card"
          style={{
            padding: "1.25rem",
            display: "flex",
            flexDirection: "column",
            gap: "1.25rem",
          }}
        >
          {/* Floor Header Bar */}
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              flexWrap: "wrap",
              gap: "0.75rem",
            }}
          >
            <div>
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: "0.5rem",
                }}
              >
                <h3
                  style={{
                    fontFamily: "Outfit",
                    fontWeight: 700,
                    fontSize: "1.2rem",
                    margin: 0,
                    color: "var(--fg)",
                  }}
                >
                  Level {currentFloor.code} Zones & Space Allocation Grid
                </h3>
                <span
                  style={{
                    fontSize: "0.72rem",
                    color: "var(--muted)",
                    background: "var(--bg)",
                    padding: "0.15rem 0.5rem",
                    borderRadius: "4px",
                    border: "1px solid var(--border)",
                  }}
                >
                  {currentFloor.name}
                </span>
              </div>
              <p
                style={{
                  margin: "0.2rem 0 0",
                  fontSize: "0.78rem",
                  color: "var(--muted)",
                }}
              >
                Multi-dimensional grid displaying physical sensor observations
                and reservation protection holds.
              </p>
            </div>

            <button
              type="button"
              className="btn-primary"
              style={{ fontSize: "0.82rem", padding: "0.45rem 0.85rem" }}
              onClick={() => {
                setEditingZone(null)
                setIsZoneModalOpen(true)
              }}
            >
              <UntitledIcon name="plus" size={15} /> Add Zone to{" "}
              {currentFloor.code}
            </button>
          </div>

          {/* Zones Summary Strip */}
          <div
            style={{
              display: "flex",
              gap: "0.75rem",
              overflowX: "auto",
              paddingBottom: "0.25rem",
            }}
          >
            <button
              type="button"
              onClick={() => setSelectedZoneFilter("all")}
              style={{
                padding: "0.5rem 0.85rem",
                borderRadius: "var(--radius)",
                border: `1.5px solid ${
                  selectedZoneFilter === "all"
                    ? "var(--primary)"
                    : "var(--border)"
                }`,
                background:
                  selectedZoneFilter === "all" ? "var(--primary)" : "var(--bg)",
                color:
                  selectedZoneFilter === "all"
                    ? "var(--primary-fg)"
                    : "var(--fg)",
                fontWeight: 600,
                fontSize: "0.8rem",
                cursor: "pointer",
                whiteSpace: "nowrap",
                display: "flex",
                alignItems: "center",
                gap: "0.4rem",
              }}
            >
              <UntitledIcon name="grid" size={14} /> All Zones (
              {(currentFloor.zones || []).length})
            </button>

            {(currentFloor.zones || []).map((zone) => {
              const isSelected = selectedZoneFilter === zone.id
              const spaces = zone.spaces || []
              const avail = spaces.filter((s) => {
                const p =
                  s.physicalState ||
                  (s.status === "available" ? "AVAILABLE" : "OCCUPIED")
                const r = s.reservationState || "NONE"
                return p === "AVAILABLE" && r === "NONE"
              }).length

              return (
                <div
                  key={zone.id}
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: "0.5rem",
                    padding: "0.4rem 0.75rem",
                    borderRadius: "var(--radius)",
                    border: `1.5px solid ${
                      isSelected ? "var(--primary)" : "var(--border)"
                    }`,
                    background: isSelected
                      ? "color-mix(in srgb, var(--primary) 12%, var(--bg))"
                      : "var(--bg)",
                    whiteSpace: "nowrap",
                    cursor: "pointer",
                  }}
                  onClick={() => setSelectedZoneFilter(zone.id)}
                >
                  <UntitledIcon
                    name={zone.vehicleType === "car" ? "car" : "motorcycle"}
                    size={15}
                  />
                  <span
                    style={{
                      fontSize: "0.82rem",
                      fontWeight: 600,
                      color: "var(--fg)",
                    }}
                  >
                    {zone.name}
                  </span>
                  <span style={{ fontSize: "0.72rem", color: "var(--muted)" }}>
                    ({avail} free / {spaces.length})
                  </span>

                  {/* Actions */}
                  <div
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: "0.2rem",
                      marginLeft: "0.35rem",
                    }}
                    onClick={(e) => e.stopPropagation()}
                  >
                    <button
                      type="button"
                      title="Edit Zone"
                      style={{
                        border: "none",
                        background: "transparent",
                        color: "var(--muted)",
                        cursor: "pointer",
                        padding: "2px",
                      }}
                      onClick={() => {
                        setEditingZone(zone)
                        setIsZoneModalOpen(true)
                      }}
                    >
                      <UntitledIcon name="settings" size={13} />
                    </button>
                    <button
                      type="button"
                      title="Delete Zone"
                      style={{
                        border: "none",
                        background: "transparent",
                        color: "#dc2626",
                        cursor: "pointer",
                        padding: "2px",
                      }}
                      onClick={() =>
                        setDeletingTarget({
                          type: "zone",
                          id: zone.id,
                          name: `${zone.name} (${spaces.length} spaces)`,
                        })
                      }
                    >
                      <UntitledIcon name="x" size={13} />
                    </button>
                  </div>
                </div>
              )
            })}
          </div>

          {/* SECTION E: SEARCH & FILTER CONTROLS */}
          <div
            style={{
              display: "flex",
              flexWrap: "wrap",
              alignItems: "center",
              justifyContent: "space-between",
              gap: "0.75rem",
              padding: "0.75rem 1rem",
              background: "var(--bg)",
              borderRadius: "0.625rem",
              border: "1px solid var(--border)",
            }}
          >
            {/* Search Box */}
            <div
              style={{
                position: "relative",
                flex: "1 1 200px",
                maxWidth: 300,
              }}
            >
              <span
                style={{
                  position: "absolute",
                  left: "0.7rem",
                  top: "50%",
                  transform: "translateY(-50%)",
                  color: "var(--muted)",
                }}
              >
                <UntitledIcon name="search" size={14} />
              </span>
              <input
                className="input"
                style={{
                  paddingLeft: "2.1rem",
                  fontSize: "0.82rem",
                  paddingBlock: "0.4rem",
                }}
                placeholder="Search code, plate, or requested slot..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
              />
            </div>

            {/* Filter Pills */}
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: "0.5rem",
                flexWrap: "wrap",
              }}
            >
              {/* Vehicle Type Filter */}
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: "0.25rem",
                  fontSize: "0.78rem",
                }}
              >
                <span style={{ color: "var(--muted)", fontWeight: 600 }}>
                  Vehicle:
                </span>
                <select
                  className="input"
                  style={{
                    width: "auto",
                    fontSize: "0.78rem",
                    padding: "0.35rem 0.65rem",
                  }}
                  value={vehicleFilter}
                  onChange={(e) => setVehicleFilter(e.target.value as any)}
                >
                  <option value="all">All Vehicles</option>
                  <option value="car">Car (Ô tô)</option>
                  <option value="motorcycle">Motorcycle (Xe máy)</option>
                </select>
              </div>

              {/* Multi-dimensional Status Filter */}
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: "0.25rem",
                  fontSize: "0.78rem",
                }}
              >
                <span style={{ color: "var(--muted)", fontWeight: 600 }}>
                  State Dimension:
                </span>
                <select
                  className="input"
                  style={{
                    width: "auto",
                    fontSize: "0.78rem",
                    padding: "0.35rem 0.65rem",
                  }}
                  value={statusFilter}
                  onChange={(e) => setStatusFilter(e.target.value as any)}
                >
                  <option value="all">All States</option>
                  <option value="available">🟢 Vacant & Available</option>
                  <option value="occupied">🔴 Physically Occupied</option>
                  <option value="protected">🛡️ Protected Hold (SRS)</option>
                  <option value="reserved">🟡 Confirmed Booking</option>
                  <option value="pending_payment">
                    💳 Pending Payment Hold
                  </option>
                  <option value="backup">🟣 Designated Backup</option>
                  <option value="maintenance">🔧 Maintenance</option>
                  <option value="disabled">🚫 Out of Service</option>
                </select>
              </div>

              {(searchQuery ||
                statusFilter !== "all" ||
                vehicleFilter !== "all" ||
                selectedZoneFilter !== "all") && (
                <button
                  type="button"
                  className="btn-outline"
                  style={{ fontSize: "0.75rem", padding: "0.3rem 0.65rem" }}
                  onClick={() => {
                    setSearchQuery("")
                    setStatusFilter("all")
                    setVehicleFilter("all")
                    setSelectedZoneFilter("all")
                  }}
                >
                  Reset
                </button>
              )}
            </div>
          </div>

          {/* PARKING SPACE VISUAL GRID WITH ORTHOGONAL STATE DIMENSIONS */}
          <div
            style={{
              display: "flex",
              flexDirection: "column",
              gap: "0.75rem",
            }}
          >
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                fontSize: "0.78rem",
                color: "var(--muted)",
                flexWrap: "wrap",
                gap: "0.5rem",
              }}
            >
              <span>
                Showing <strong>{displayedSpaces.length}</strong> spaces
                matching filters
              </span>
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: "0.65rem",
                  flexWrap: "wrap",
                  fontSize: "0.72rem",
                }}
              >
                <span
                  style={{
                    display: "inline-flex",
                    alignItems: "center",
                    gap: "0.25rem",
                  }}
                >
                  <span
                    style={{
                      width: 8,
                      height: 8,
                      borderRadius: "50%",
                      background: "#22c55e",
                    }}
                  />
                  Vacant
                </span>
                <span
                  style={{
                    display: "inline-flex",
                    alignItems: "center",
                    gap: "0.25rem",
                  }}
                >
                  <span
                    style={{
                      width: 8,
                      height: 8,
                      borderRadius: "50%",
                      background: "#ef4444",
                    }}
                  />
                  Occupied
                </span>
                <span
                  style={{
                    display: "inline-flex",
                    alignItems: "center",
                    gap: "0.25rem",
                  }}
                >
                  <span
                    style={{
                      width: 8,
                      height: 8,
                      borderRadius: "50%",
                      background: "#6366f1",
                    }}
                  />
                  Protected
                </span>
                <span
                  style={{
                    display: "inline-flex",
                    alignItems: "center",
                    gap: "0.25rem",
                  }}
                >
                  <span
                    style={{
                      width: 8,
                      height: 8,
                      borderRadius: "50%",
                      background: "#8b5cf6",
                    }}
                  />
                  Backup
                </span>
                <span
                  style={{
                    display: "inline-flex",
                    alignItems: "center",
                    gap: "0.25rem",
                  }}
                >
                  <span
                    style={{
                      width: 8,
                      height: 8,
                      borderRadius: "50%",
                      background: "#f59e0b",
                    }}
                  />
                  Reserved
                </span>
                <span
                  style={{
                    display: "inline-flex",
                    alignItems: "center",
                    gap: "0.25rem",
                  }}
                >
                  <span
                    style={{
                      width: 8,
                      height: 8,
                      borderRadius: "50%",
                      background: "#ca8a04",
                    }}
                  />
                  Pending
                </span>
              </div>
            </div>

            {displayedSpaces.length > 0 ? (
              <div
                style={{
                  display: "grid",
                  gridTemplateColumns: "repeat(auto-fill, minmax(115px, 1fr))",
                  gap: "0.625rem",
                }}
              >
                {displayedSpaces.map((space) => {
                  const physical: PhysicalState =
                    space.physicalState ||
                    (space.status === "occupied"
                      ? "OCCUPIED"
                      : space.status === "maintenance"
                        ? "MAINTENANCE"
                        : space.status === "disabled"
                          ? "UNAVAILABLE"
                          : "AVAILABLE")
                  const reservation: ReservationProtectionState =
                    space.reservationState ||
                    (space.isProtected
                      ? "PROTECTED"
                      : space.isPendingPayment
                        ? "PENDING_PAYMENT"
                        : space.isBackup
                          ? "BACKUP"
                          : space.status === "reserved"
                            ? "RESERVED"
                            : "NONE")

                  const isSimultaneous =
                    physical === "OCCUPIED" &&
                    (reservation === "PROTECTED" || space.isProtected)
                  const isReallocated = Boolean(
                    space.requestedSlotCode &&
                      space.requestedSlotCode !== space.code,
                  )

                  let borderColor = "#22c55e"
                  let bgColor = "#22c55e10"
                  let primaryBadgeText = "TRỐNG"
                  let primaryBadgeColor = "#16a34a"

                  if (isSimultaneous) {
                    borderColor = "#ef4444"
                    bgColor =
                      "linear-gradient(135deg, rgba(239, 68, 68, 0.12) 0%, rgba(99, 102, 241, 0.14) 100%)"
                    primaryBadgeText = "OCCUPIED"
                    primaryBadgeColor = "#dc2626"
                  } else if (physical === "OCCUPIED") {
                    borderColor = "#ef4444"
                    bgColor = "#ef444412"
                    primaryBadgeText = "OCCUPIED"
                    primaryBadgeColor = "#dc2626"
                  } else if (physical === "MAINTENANCE") {
                    borderColor = "#f97316"
                    bgColor = "#f9731612"
                    primaryBadgeText = "BẢO TRÌ"
                    primaryBadgeColor = "#ea580c"
                  } else if (physical === "UNAVAILABLE") {
                    borderColor = "#64748b"
                    bgColor = "#64748b10"
                    primaryBadgeText = "KHÓA"
                    primaryBadgeColor = "#475569"
                  } else if (reservation === "PROTECTED") {
                    borderColor = "#6366f1"
                    bgColor = "#6366f112"
                    primaryBadgeText = "TRỐNG"
                    primaryBadgeColor = "#16a34a"
                  } else if (reservation === "BACKUP") {
                    borderColor = "#8b5cf6"
                    bgColor = "#8b5cf612"
                    primaryBadgeText = "TRỐNG"
                    primaryBadgeColor = "#16a34a"
                  } else if (reservation === "RESERVED") {
                    borderColor = "#f59e0b"
                    bgColor = "#f59e0b12"
                    primaryBadgeText = "TRỐNG"
                    primaryBadgeColor = "#16a34a"
                  } else if (reservation === "PENDING_PAYMENT") {
                    borderColor = "#eab308"
                    bgColor = "#eab30812"
                    primaryBadgeText = "TRỐNG"
                    primaryBadgeColor = "#16a34a"
                  }

                  return (
                    <div
                      key={space.id}
                      onClick={() => setInspectingSpace(space)}
                      style={{
                        padding: "0.65rem 0.5rem",
                        borderRadius: "0.5rem",
                        border: `1.5px ${
                          physical === "UNAVAILABLE" ? "dashed" : "solid"
                        } ${borderColor}`,
                        background: bgColor,
                        cursor: "pointer",
                        display: "flex",
                        flexDirection: "column",
                        alignItems: "center",
                        justifyContent: "center",
                        gap: "0.25rem",
                        textAlign: "center",
                        transition: "transform 0.12s, box-shadow 0.12s",
                        userSelect: "none",
                        position: "relative",
                      }}
                      onMouseEnter={(e) => {
                        e.currentTarget.style.transform = "translateY(-2px)"
                        e.currentTarget.style.boxShadow =
                          "0 4px 12px rgba(0, 0, 0, 0.1)"
                      }}
                      onMouseLeave={(e) => {
                        e.currentTarget.style.transform = "translateY(0)"
                        e.currentTarget.style.boxShadow = "none"
                      }}
                    >
                      {/* Simultaneous Conflict Icon Indicator */}
                      {isSimultaneous && (
                        <span
                          title="Simultaneous OCCUPIED + PROTECTED conflict!"
                          style={{
                            position: "absolute",
                            top: 4,
                            right: 4,
                            color: "#ef4444",
                            lineHeight: 1,
                          }}
                        >
                          <UntitledIcon name="alert" size={12} />
                        </span>
                      )}

                      {/* Space Code */}
                      <span
                        style={{
                          fontFamily: "Outfit",
                          fontWeight: 800,
                          fontSize: "1.05rem",
                          color: "var(--fg)",
                          lineHeight: 1,
                        }}
                      >
                        {space.code}
                      </span>

                      {/* Vehicle Category Icon */}
                      <div
                        style={{
                          display: "flex",
                          alignItems: "center",
                          gap: "0.2rem",
                          color: primaryBadgeColor,
                        }}
                      >
                        <UntitledIcon
                          name={
                            space.vehicleType === "car" ? "car" : "motorcycle"
                          }
                          size={13}
                        />
                      </div>

                      {/* Physical Observation Badge */}
                      <span
                        style={{
                          fontSize: "0.62rem",
                          fontWeight: 700,
                          padding: "0.1rem 0.35rem",
                          borderRadius: "3px",
                          background: "var(--bg)",
                          color: primaryBadgeColor,
                          border: `1px solid ${borderColor}50`,
                          textTransform: "uppercase",
                        }}
                      >
                        {primaryBadgeText}
                      </span>

                      {/* Reservation Dimension Pill */}
                      {reservation === "PROTECTED" && (
                        <span
                          style={{
                            fontSize: "0.6rem",
                            fontWeight: 700,
                            padding: "0.08rem 0.3rem",
                            borderRadius: "3px",
                            background: "#6366f120",
                            color: "#6366f1",
                            border: "1px solid #6366f140",
                          }}
                        >
                          PROTECTED
                        </span>
                      )}
                      {reservation === "BACKUP" && (
                        <span
                          style={{
                            fontSize: "0.6rem",
                            fontWeight: 700,
                            padding: "0.08rem 0.3rem",
                            borderRadius: "3px",
                            background: "#8b5cf620",
                            color: "#7c3aed",
                            border: "1px solid #8b5cf640",
                          }}
                        >
                          BACKUP
                        </span>
                      )}
                      {reservation === "RESERVED" && !isSimultaneous && (
                        <span
                          style={{
                            fontSize: "0.6rem",
                            fontWeight: 700,
                            padding: "0.08rem 0.3rem",
                            borderRadius: "3px",
                            background: "#f59e0b20",
                            color: "#d97706",
                            border: "1px solid #f59e0b40",
                          }}
                        >
                          RESERVED
                        </span>
                      )}
                      {reservation === "PENDING_PAYMENT" && (
                        <span
                          style={{
                            fontSize: "0.6rem",
                            fontWeight: 700,
                            padding: "0.08rem 0.3rem",
                            borderRadius: "3px",
                            background: "#eab30820",
                            color: "#ca8a04",
                            border: "1px solid #eab30840",
                          }}
                        >
                          PENDING
                        </span>
                      )}

                      {/* Reallocated Tag (SRS 3.4.6) */}
                      {isReallocated && (
                        <span
                          title={`Originally requested: ${space.requestedSlotCode}`}
                          style={{
                            fontSize: "0.58rem",
                            fontWeight: 700,
                            color: "var(--muted)",
                            background: "var(--bg)",
                            padding: "0.05rem 0.25rem",
                            borderRadius: "2px",
                            border: "1px dashed var(--border)",
                          }}
                        >
                          Req: {space.requestedSlotCode}
                        </span>
                      )}
                    </div>
                  )
                })}
              </div>
            ) : (
              <div
                style={{
                  textAlign: "center",
                  padding: "2.5rem",
                  background: "var(--bg)",
                  borderRadius: "0.75rem",
                  border: "1px dashed var(--border)",
                  color: "var(--muted)",
                }}
              >
                <div
                  style={{ color: "var(--primary)", marginBottom: "0.5rem" }}
                >
                  <UntitledIcon name="search" size={28} />
                </div>
                <p style={{ margin: 0, fontWeight: 600 }}>
                  No parking spaces match the current filter criteria.
                </p>
                <button
                  type="button"
                  className="btn-outline"
                  style={{
                    marginTop: "0.75rem",
                    fontSize: "0.8rem",
                    padding: "0.35rem 0.85rem",
                  }}
                  onClick={() => {
                    setSearchQuery("")
                    setStatusFilter("all")
                    setVehicleFilter("all")
                    setSelectedZoneFilter("all")
                  }}
                >
                  Clear All Filters
                </button>
              </div>
            )}
          </div>
        </div>
      )}

      {/* MODAL: FLOOR ADD / EDIT */}
      <FloorModal
        isOpen={isFloorModalOpen}
        initialData={editingFloor}
        onClose={() => {
          setIsFloorModalOpen(false)
          setEditingFloor(null)
        }}
        onSave={handleSaveFloor}
      />

      {/* MODAL: ZONE ADD / EDIT */}
      <ZoneModal
        isOpen={isZoneModalOpen}
        floorCode={currentFloor?.code || ""}
        initialData={editingZone}
        onClose={() => {
          setIsZoneModalOpen(false)
          setEditingZone(null)
        }}
        onSave={handleSaveZone}
      />

      {/* MODAL: SPACE DETAILS */}
      <SpaceDetailModal
        space={inspectingSpace}
        floorName={currentFloor?.name || ""}
        floorCode={currentFloor?.code || ""}
        zoneName={
          inspectingSpace
            ? currentFloor?.zones?.find((z) => z.id === inspectingSpace.zoneId)
                ?.name || "Zone"
            : ""
        }
        onClose={() => setInspectingSpace(null)}
        onUpdateStatus={handleUpdateSpaceStatus}
      />

      {/* CONFIRMATION MODAL: DELETE */}
      {deletingTarget && (
        <div
          role="presentation"
          onMouseDown={(e) => {
            if (e.target === e.currentTarget) setDeletingTarget(null)
          }}
          style={{
            position: "fixed",
            inset: 0,
            zIndex: 1000,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            padding: "1rem",
            background: "rgba(2, 6, 23, 0.65)",
            backdropFilter: "blur(4px)",
          }}
        >
          <div
            role="dialog"
            aria-modal="true"
            className="card"
            style={{
              width: "100%",
              maxWidth: 420,
              background: "var(--bg)",
              color: "var(--fg)",
              border: "1px solid var(--border)",
              borderRadius: "1rem",
              padding: "1.5rem",
            }}
          >
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: "0.5rem",
                color: "#dc2626",
                marginBottom: "0.75rem",
              }}
            >
              <UntitledIcon name="alert" size={22} />
              <h3
                style={{
                  fontFamily: "Outfit",
                  fontWeight: 700,
                  fontSize: "1.2rem",
                  margin: 0,
                }}
              >
                Confirm Deletion
              </h3>
            </div>
            <p
              style={{
                margin: "0 0 1.25rem",
                fontSize: "0.875rem",
                color: "var(--fg)",
                lineHeight: 1.5,
              }}
            >
              Are you sure you want to delete{" "}
              <strong>{deletingTarget.name}</strong>?
              {deletingTarget.type === "floor" &&
                " All zones and parking spaces under this floor will also be permanently removed."}
            </p>
            <div
              style={{
                display: "flex",
                justifyContent: "flex-end",
                gap: "0.625rem",
              }}
            >
              <button
                type="button"
                className="btn-outline"
                onClick={() => setDeletingTarget(null)}
              >
                Cancel
              </button>
              <button
                type="button"
                className="btn-primary"
                style={{ background: "#dc2626" }}
                onClick={handleConfirmDelete}
              >
                Delete Permanently
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
