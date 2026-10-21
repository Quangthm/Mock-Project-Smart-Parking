import { useMemo, useState } from "react"
import type { OperatorSite, OperatorSlot } from "../../../lib/operatorApi"

export type SlotStatusFilter = "all" | "available" | "occupied" | "reserved" | "backup" | "maintenance"
type ViewMode = "3d" | "grid" | "list"

export function useLotStatus(lot: OperatorSite | null, initialSlotCode = "", initialStatusFilter: SlotStatusFilter = "all") {
  const [viewMode, setViewMode] = useState<ViewMode>("3d")
  const [selectedFloor, setSelectedFloor] = useState<number>(1)
  const [selectedZone, setSelectedZone] = useState<string>("all")
  const [vehicleTypeFilter, setVehicleTypeFilter] = useState<"all" | "car" | "motorcycle">("all")
  const [statusFilter, setStatusFilter] = useState(initialStatusFilter)
  const [searchQuery, setSearchQuery] = useState(initialSlotCode)
  const [inspectedSlot, setInspectedSlot] = useState<OperatorSlot | null>(null)
  const [threeError, setThreeError] = useState(false)

  const isOutdoor = (lot?.floors ?? 1) <= 1
  const availableZones = useMemo(() => {
    if (!lot) return []
    return lot.units.filter((unit) => unit.type === "ZONE")
      .filter((zone) => {
        const parent = lot.units.find((unit) => unit.id === zone.parentId)
        const floor = parent?.type === "FLOOR"
          ? lot.units.filter((unit) => unit.type === "FLOOR").findIndex((unit) => unit.id === parent.id) + 1
          : 1
        return isOutdoor || floor === selectedFloor
      })
  }, [lot, isOutdoor, selectedFloor])

  const allSlots: OperatorSlot[] = useMemo(() => lot?.slots ?? [], [lot])
  const filteredSlots = useMemo(() => allSlots.filter((slot) => {
    if (!isOutdoor && slot.floor !== selectedFloor) return false
    if (selectedZone !== "all") {
      let unit = lot?.units.find((item) => item.id === slot.unitId)
      while (unit && unit.id !== selectedZone) {
        unit = unit.parentId ? lot?.units.find((item) => item.id === unit?.parentId) : undefined
      }
      if (!unit) return false
    }
    const isMoto = slot.vehicleType.toLowerCase().includes("motorcycle")
    if (vehicleTypeFilter === "car" && isMoto) return false
    if (vehicleTypeFilter === "motorcycle" && !isMoto) return false
    if (statusFilter === "available" && slot.status !== "available") return false
    if (statusFilter === "occupied" && slot.status !== "occupied") return false
    if (statusFilter === "reserved" && slot.status !== "reserved") return false
    if (statusFilter === "maintenance" && slot.status !== "maintenance" && slot.status !== "disabled") return false
    if (statusFilter === "backup" && slot.reservationState?.toUpperCase() !== "BACKUP") return false
    if (searchQuery.trim() && !slot.number.toLowerCase().includes(searchQuery.trim().toLowerCase())) return false
    return true
  }), [allSlots, isOutdoor, selectedFloor, selectedZone, vehicleTypeFilter, statusFilter, searchQuery, lot])

  const availableCount = lot?.slots.filter((s) => s.status === "available").length ?? 0
  const occupiedCount = lot?.slots.filter((s) => s.status === "occupied").length ?? 0
  const reservedCount = lot?.slots.filter((s) => s.status === "reserved").length ?? 0
  const maintenanceCount = lot?.slots.filter((s) => s.status === "maintenance" || s.status === "disabled").length ?? 0

  return {
    viewMode, setViewMode, selectedFloor, setSelectedFloor, selectedZone, setSelectedZone,
    vehicleTypeFilter, setVehicleTypeFilter, statusFilter, setStatusFilter, searchQuery, setSearchQuery,
    inspectedSlot, setInspectedSlot, threeError, setThreeError, isOutdoor, availableZones,
    allSlots, filteredSlots, availableCount, occupiedCount, reservedCount, maintenanceCount,
  }
}
