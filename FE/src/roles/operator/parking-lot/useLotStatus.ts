import { useCallback, useEffect, useMemo, useState } from "react"
import type { DemoSlotSession, OperatorSite, OperatorSlot } from "../../../lib/operatorApi"

export type SlotStatusFilter = "all" | "available" | "occupied" | "reserved" | "backup" | "maintenance"
type ViewMode = "grid" | "list"
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
  return `data:image/svg+xml,${encodeURIComponent(`<svg xmlns="http://www.w3.org/2000/svg" width="640" height="360" viewBox="0 0 640 360"><rect width="640" height="360" fill="#111827"/><rect x="34" y="34" width="572" height="292" rx="18" fill="${color}" opacity=".22"/><path d="M155 239h330l-35-70c-6-12-18-20-32-20H250c-14 0-27 8-34 21l-35 69h-26v31h34c8 17 26 29 46 29s37-12 45-29h118c8 17 25 29 45 29s38-12 46-29h35v-31h-28l-23-70z" fill="${color}"/><circle cx="238" cy="266" r="20" fill="#0f172a"/><circle cx="433" cy="266" r="20" fill="#0f172a"/><text x="320" y="92" fill="white" text-anchor="middle" font-family="Arial" font-size="18">DEMO CAMERA CAPTURE</text></svg>`)}`
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

export function useLotStatus(lot: OperatorSite | null, initialSlotCode = "", initialStatusFilter: SlotStatusFilter = "all") {
  const [viewMode, setViewMode] = useState<ViewMode>("grid")
  const [selectedFloor, setSelectedFloor] = useState<number>(1)
  const [selectedZone, setSelectedZone] = useState<string>("all")
  const [vehicleTypeFilter, setVehicleTypeFilter] = useState<"all" | "car" | "motorcycle">("all")
  const [statusFilter, setStatusFilter] = useState(initialStatusFilter)
  const [searchQuery, setSearchQuery] = useState(initialSlotCode)
  const [inspectedSlot, setInspectedSlot] = useState<OperatorSlot | null>(null)
  const [threeError, setThreeError] = useState(false)
  const [demoAssignments, setDemoAssignments] = useState<Record<string, DemoVehicle>>({})
  const [pendingDemoAssignments, setPendingDemoAssignments] = useState<Record<string, DemoVehicle>>({})
  const [isScanning, setIsScanning] = useState(false)
  const [lastScanAt, setLastScanAt] = useState<number | null>(null)

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

  const scanSlots = useCallback(() => {
    if (!lot || isScanning) return

    setIsScanning(true)
    window.setTimeout(() => {
      setDemoAssignments((current) => ({ ...current, ...pendingDemoAssignments }))
      setPendingDemoAssignments({})
      window.localStorage.removeItem(pendingCheckInStorageKey(lot.id))
      setLastScanAt(Date.now())
      setIsScanning(false)
    }, 850)
  }, [isScanning, pendingDemoAssignments, lot])

  useEffect(() => {
    if (!lot) return
    setDemoAssignments({})
    setPendingDemoAssignments({})
    setLastScanAt(null)

    const storedCheckIns = JSON.parse(
      window.localStorage.getItem(pendingCheckInStorageKey(lot.id)) ?? "[]",
    ) as Array<{
      plate: string
      imageUrl?: string
      vehicleType?: string
      session?: DemoSlotSession
    }>
    if (storedCheckIns.length > 0) {
      setPendingDemoAssignments(() => {
        const assignments: Record<string, DemoVehicle> = {}
        const availableSlots = (lot.slots ?? []).filter((slot) => slot.status === "available")
        storedCheckIns.forEach((checkIn, index) => {
          if (availableSlots.length === 0) return
          const slotIndex = Math.floor(Math.random() * availableSlots.length)
          const slot = availableSlots.splice(slotIndex, 1)[0]
          if (!slot) return
          const imageUrl = checkIn.imageUrl || demoVehicleImage(Date.now() + index)
          assignments[slot.id] = {
            plate: checkIn.plate,
            imageUrl,
            session: checkIn.session ?? createDemoSession(checkIn.plate, checkIn.vehicleType ?? "CAR", imageUrl),
          }
        })
        return assignments
      })
    }
  }, [lot?.id])

  useEffect(() => {
    const handleCheckIn = (event: Event) => {
      const detail = (event as CustomEvent<{
        siteId: string
        plate: string
        imageUrl?: string
        session?: DemoSlotSession
        vehicleType?: string
      }>).detail
      if (!lot || detail.siteId !== lot.id) return
      const imageUrl = detail.imageUrl || demoVehicleImage(Date.now())
      const vehicle = {
        plate: detail.plate,
        imageUrl,
        session: detail.session ?? createDemoSession(detail.plate, detail.vehicleType ?? "CAR", imageUrl),
      }
      setPendingDemoAssignments((current) => {
        const occupiedIds = new Set(Object.keys(demoAssignments))
        const pendingIds = new Set(Object.keys(current))
        const availableSlots = (lot.slots ?? []).filter((slot) =>
          slot.status === "available" && !occupiedIds.has(slot.id) && !pendingIds.has(slot.id),
        )
        if (availableSlots.length === 0) return current
        const slot = availableSlots[Math.floor(Math.random() * availableSlots.length)]
        return { ...current, [slot.id]: vehicle }
      })
      const storedCheckIns = JSON.parse(
        window.localStorage.getItem(pendingCheckInStorageKey(lot.id)) ?? "[]",
      ) as Array<{ plate: string }>
      if (!storedCheckIns.some((checkIn) => checkIn.plate === detail.plate)) {
        storedCheckIns.push(detail)
        window.localStorage.setItem(pendingCheckInStorageKey(lot.id), JSON.stringify(storedCheckIns))
      }
    }
    const handleCheckOut = (event: Event) => {
      const detail = (event as CustomEvent<{ siteId: string; plate: string }>).detail
      if (!lot || detail.siteId !== lot.id) return
      setDemoAssignments((assignments) => Object.fromEntries(
        Object.entries(assignments).filter(([, vehicle]) => vehicle.plate !== detail.plate),
      ))
      setPendingDemoAssignments((assignments) => Object.fromEntries(
        Object.entries(assignments).filter(([, vehicle]) => vehicle.plate !== detail.plate),
      ))
      const storedCheckIns = JSON.parse(
        window.localStorage.getItem(pendingCheckInStorageKey(lot.id)) ?? "[]",
      ) as Array<{ plate: string }>
      window.localStorage.setItem(
        pendingCheckInStorageKey(lot.id),
        JSON.stringify(storedCheckIns.filter((checkIn) => checkIn.plate !== detail.plate)),
      )
    }
    window.addEventListener("smartpark:demo-check-in", handleCheckIn)
    window.addEventListener("smartpark:demo-check-out", handleCheckOut)
    return () => {
      window.removeEventListener("smartpark:demo-check-in", handleCheckIn)
      window.removeEventListener("smartpark:demo-check-out", handleCheckOut)
    }
  }, [demoAssignments, lot])

  useEffect(() => {
    const timer = window.setInterval(scanSlots, 300000)
    return () => window.clearInterval(timer)
  }, [scanSlots])

  const allSlots: OperatorSlot[] = useMemo(() => (lot?.slots ?? []).map((slot) => {
    const assignment = demoAssignments[slot.id]
    const pendingAssignment = pendingDemoAssignments[slot.id]
    return assignment
      ? {
          ...slot,
          status: "occupied",
          isPhysicallyOccupied: true,
          demoPlate: assignment.plate,
          demoImageUrl: assignment.imageUrl,
          demoCheckedInAt: new Date().toISOString(),
          demoSession: assignment.session,
        }
      : pendingAssignment
        ? {
            ...slot,
            demoImageUrl: pendingAssignment.imageUrl,
            demoCheckedInAt: pendingAssignment.session.entryTime,
          }
      : slot
  }), [lot, demoAssignments, pendingDemoAssignments])
  const filteredSlots = useMemo(() => allSlots.filter((slot) => {
    if (!isOutdoor && slot.floor !== selectedFloor) return false
    if (selectedZone !== "all") {
      let unit = lot?.units.find((item) => item.id === slot.unitId)
      while (unit && unit.id !== selectedZone) {
        unit = unit.parentId ? lot?.units.find((item) => item.id === unit?.parentId) : undefined
      }
      if (!unit) return false
    }
    const STATUS_FILTER_PREDICATES: Record<Exclude<SlotStatusFilter, "all">, (s: OperatorSlot) => boolean> = {
      available: (s) => s.status === "available",
      occupied: (s) => s.status === "occupied",
      reserved: (s) => s.status === "reserved",
      maintenance: (s) => s.status === "maintenance" || s.status === "disabled",
      backup: (s) => s.reservationState?.toUpperCase() === "BACKUP",
    }

    const isMoto = slot.vehicleType.toLowerCase().includes("motorcycle")
    if (vehicleTypeFilter === "car" && isMoto) return false
    if (vehicleTypeFilter === "motorcycle" && !isMoto) return false
    if (statusFilter !== "all" && !STATUS_FILTER_PREDICATES[statusFilter]?.(slot)) return false
    if (searchQuery.trim() && !slot.number.toLowerCase().includes(searchQuery.trim().toLowerCase())) return false
    return true
  }), [allSlots, isOutdoor, selectedFloor, selectedZone, vehicleTypeFilter, statusFilter, searchQuery, lot])

  const availableCount = allSlots.filter((s) => s.status === "available").length
  const occupiedCount = allSlots.filter((s) => s.status === "occupied").length
  const reservedCount = allSlots.filter((s) => s.status === "reserved").length
  const maintenanceCount = allSlots.filter((s) => s.status === "maintenance" || s.status === "disabled").length

  return {
    viewMode, setViewMode, selectedFloor, setSelectedFloor, selectedZone, setSelectedZone,
    vehicleTypeFilter, setVehicleTypeFilter, statusFilter, setStatusFilter, searchQuery, setSearchQuery,
    inspectedSlot, setInspectedSlot, threeError, setThreeError, isOutdoor, availableZones,
    allSlots, filteredSlots, availableCount, occupiedCount, reservedCount, maintenanceCount,
    scanSlots, isScanning, lastScanAt,
  }
}
