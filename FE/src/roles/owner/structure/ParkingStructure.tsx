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
    label: "Trống",
    color: "#16a34a",
    bg: "rgba(34, 197, 94, 0.12)",
    border: "rgba(34, 197, 94, 0.35)",
    icon: "check-circle",
    desc: "Slot trống, sẵn sàng nhận xe hoặc cho khách đặt",
  },
  reserved: {
    label: "Đã đặt",
    color: "#2563eb",
    bg: "rgba(37, 99, 235, 0.12)",
    border: "rgba(37, 99, 235, 0.35)",
    icon: "clock",
    desc: "Khách đã đặt trước thành công qua ứng dụng",
  },
  pending_payment: {
    label: "Đang chờ thanh toán",
    color: "#d97706",
    bg: "rgba(245, 158, 11, 0.12)",
    border: "rgba(245, 158, 11, 0.35)",
    icon: "banknote",
    desc: "Khách đang giữ chỗ và tiến hành thanh toán (hold)",
  },
  backup: {
    label: "Slot Backup",
    color: "#9333ea",
    bg: "rgba(147, 51, 234, 0.12)",
    border: "rgba(147, 51, 234, 0.35)",
    icon: "shield",
    desc: "Chỗ dự phòng khẩn cấp/điều phối sự cố bãi đỗ",
  },
  occupied: {
    label: "Đã có xe",
    color: "#dc2626",
    bg: "rgba(239, 68, 68, 0.12)",
    border: "rgba(239, 68, 68, 0.35)",
    icon: "car",
    desc: "Hiện đang có xe thực tế đỗ tại vị trí",
  },
  protected: {
    label: "Protected",
    color: "#6366f1",
    bg: "rgba(99, 102, 241, 0.15)",
    border: "rgba(99, 102, 241, 0.45)",
    icon: "shield",
    desc: "Khóa slot bảo vệ cho khách đặt trước, cấm xe vãng lai",
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
  const [protectionWindowHours, setProtectionWindowHours] = useState<number>(1.0)

  // Simulation State
  const [simStep, setSimStep] = useState<number>(0)
  const [simLogs, setSimLogs] = useState<
    Array<{ time: string; msg: string; type: "info" | "warning" | "success" }>
  >([])

  // Modal States
  const [isFloorModalOpen, setIsFloorModalOpen] = useState(false)
  const [editingFloor, setEditingFloor] = useState<ParkingFloorItem | null>(null)
  const [isZoneModalOpen, setIsZoneModalOpen] = useState(false)
  const [editingZone, setEditingZone] = useState<ParkingZoneItem | null>(null)
  const [inspectingSpace, setInspectingSpace] = useState<ParkingSpaceItem | null>(null)

  useEffect(() => {
    const s = structureStore.getStructure(activeLotId)
    setStructure(s)
    setSelectedFloorId(s.floors?.[0]?.id || "")
    setSelectedZoneFilter("all")
    setSimStep(0)
    setSimLogs([])
  }, [activeLotId])

  const reloadStructure = () => {
    setStructure(structureStore.getStructure(activeLotId))
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

  // Simulation Controller: Step-by-step conflict detection & Auto-reallocation
  function handleNextSimStep() {
    const nextStep = simStep + 1
    setSimStep(nextStep)

    const updated = { ...structure }
    const firstZone = updated.floors?.[0]?.zones?.[0]
    if (!firstZone || !firstZone.spaces.length) return

    const slot1 = firstZone.spaces[0] // A-01
    const slot5 = firstZone.spaces[4] || firstZone.spaces[1] // A-05 fallback

    if (nextStep === 1) {
      // Step 1: 06:00 - 08:30: Xe 1 đang đậu (Occupied). Khách B đặt 10h-12h
      slot1.status = "occupied"
      slot1.physicalState = "OCCUPIED"
      slot1.reservationState = "NONE"
      slot1.isProtected = false
      slot1.conflictReason = undefined
      slot1.currentBooking = {
        bookingId: "bk-sim-01",
        driverName: "Nguyễn Văn An (Xe vãng lai)",
        licensePlate: "51K-889.23",
        startTime: "06:00",
        endTime: "09:00",
      }

      setSimLogs((cur) => [
        {
          time: "06:00 - 08:30",
          msg: `Xe vãng lai (51K-889.23) đậu vào slot ${slot1.code} từ 06:00 đến 09:00. Khách B (Lê Minh Đạt) đã đặt trước slot ${slot1.code} khung giờ 10:00 - 12:00.`,
          type: "info",
        },
        ...cur,
      ])
    } else if (nextStep === 2) {
      // Step 2: 09:00 (10h - x): Kích hoạt Protected!
      // Slot trở thành OCCUPIED + PROTECTED (Dual State)
      slot1.isProtected = true
      slot1.reservationState = "PROTECTED"
      slot1.protectionStartTime = "09:00"
      slot1.conflictReason = `Khung bảo vệ kích hoạt (${protectionWindowHours}h trước). Slot ${slot1.code} bị KHÓA chống xe vãng lai.`

      setSimLogs((cur) => [
        {
          time: "09:00 (10h - x)",
          msg: `[KÍCH HOẠT PROTECTED] Bắt đầu quyền bảo vệ cho khách B (10h-12h). Slot ${slot1.code} chuyển sang trạng thái DUAL: Vừa OCCUPIED (xe 51K-889.23 chưa rời) vừa PROTECTED! Hệ thống khóa slot cấm xe vãng lai khác.`,
          type: "warning",
        },
        ...cur,
      ])
    } else if (nextStep === 3) {
      // Step 3: 09:15 (Trong khoảng 9h -> 9h45): Phát hiện xe chưa rời!
      slot1.conflictReason = `CẢNH BÁO XUNG ĐỘT: Xe cũ (51K-889.23) chưa rời khỏi vị trí đặt. Hệ thống bắt đầu quét tìm slot trống để tự động điều phối!`

      setSimLogs((cur) => [
        {
          time: "09:15 (9h -> 9h45)",
          msg: `[PHÁT HIỆN XUNG ĐỘT] Quá 9h15 mà xe cũ chưa rời đi. Hệ thống gửi thông báo cho khách B: "Vị trí đặt ${slot1.code} đang có xe khác đậu, hệ thống đang tự động tìm vị trí mới..." và quét tìm slot trống phù hợp.`,
          type: "warning",
        },
        ...cur,
      ])
    } else if (nextStep === 4) {
      // Step 4: TH1: Tìm thấy slot mới -> Auto Reallocate sang slot5!
      slot5.status = "reserved"
      slot5.physicalState = "AVAILABLE"
      slot5.reservationState = "PROTECTED"
      slot5.isProtected = true
      slot5.requestedSlotCode = slot1.code
      slot5.allocationTime = "09:16"
      slot5.currentBooking = {
        bookingId: "bk-sim-02",
        driverName: "Lê Minh Đạt (Khách đặt 10h-12h)",
        licensePlate: "30A-999.88",
        startTime: "10:00",
        endTime: "12:00",
        isAllocated: true,
        isReallocated: true,
      }

      // Slot 1 remains with original parking car until exit
      slot1.conflictReason = `Đã tự động điều phối khách 10h-12h sang vị trí mới: ${slot5.code}.`

      setSimLogs((cur) => [
        {
          time: "09:16 (TH1 Thành công)",
          msg: `[TỰ ĐỘNG ALLOCATE SLOT MỚI] Tìm thấy slot trống ${slot5.code}! Tự động gán vị trí mới cho khách B (30A-999.88). Gửi thông báo Push tới Driver: "Vị trí đặt đỗ xe của bạn đã được chuyển từ ${slot1.code} sang ${slot5.code}. Vui lòng theo chỉ dẫn mới."`,
          type: "success",
        },
        ...cur,
      ])
    }

    structureStore.saveStructure(updated)
    setStructure(updated)
  }

  function handleResetSim() {
    setSimStep(0)
    setSimLogs([])
    reloadStructure()
  }

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
            Cấu hình bố cục không gian, quản lý 6 trạng thái vận hành, cơ chế bảo vệ chỗ đỗ và tự động tái cấp phát khi có xung đột.
          </p>
        </div>

        {/* Site Selector */}
        <div className="flex items-center gap-2.5">
          <label className="text-xs font-semibold text-[var(--muted)]">Cơ sở:</label>
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

      {/* 6 Canonical Status Metrics Bar with DUAL STATE Highlight */}
      <div className="grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-7 gap-2.5">
        {/* 1. Trống */}
        <div
          onClick={() => setStatusFilter(statusFilter === "available" ? "all" : "available")}
          className={`p-3 rounded-xl border cursor-pointer transition-all ${
            statusFilter === "available" ? "ring-2 ring-emerald-500 shadow-xs" : ""
          }`}
          style={{ background: STATUS_DEFINITIONS.available.bg, borderColor: STATUS_DEFINITIONS.available.border }}
        >
          <div className="text-[11px] font-semibold text-emerald-700 dark:text-emerald-300 flex items-center justify-between">
            <span>Trống</span>
            <UntitledIcon name="check-circle" size={13} />
          </div>
          <div className="text-xl font-bold text-emerald-600 dark:text-emerald-400 mt-1">
            {counts.available}
          </div>
        </div>

        {/* 2. Đã đặt */}
        <div
          onClick={() => setStatusFilter(statusFilter === "reserved" ? "all" : "reserved")}
          className={`p-3 rounded-xl border cursor-pointer transition-all ${
            statusFilter === "reserved" ? "ring-2 ring-blue-500 shadow-xs" : ""
          }`}
          style={{ background: STATUS_DEFINITIONS.reserved.bg, borderColor: STATUS_DEFINITIONS.reserved.border }}
        >
          <div className="text-[11px] font-semibold text-blue-700 dark:text-blue-300 flex items-center justify-between">
            <span>Đã đặt</span>
            <UntitledIcon name="clock" size={13} />
          </div>
          <div className="text-xl font-bold text-blue-600 dark:text-blue-400 mt-1">
            {counts.reserved}
          </div>
        </div>

        {/* 3. Chờ thanh toán */}
        <div
          onClick={() => setStatusFilter(statusFilter === "pending_payment" ? "all" : "pending_payment")}
          className={`p-3 rounded-xl border cursor-pointer transition-all ${
            statusFilter === "pending_payment" ? "ring-2 ring-amber-500 shadow-xs" : ""
          }`}
          style={{ background: STATUS_DEFINITIONS.pending_payment.bg, borderColor: STATUS_DEFINITIONS.pending_payment.border }}
        >
          <div className="text-[11px] font-semibold text-amber-700 dark:text-amber-300 flex items-center justify-between">
            <span>Chờ thanh toán</span>
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

        {/* 5. Đã có xe */}
        <div
          onClick={() => setStatusFilter(statusFilter === "occupied" ? "all" : "occupied")}
          className={`p-3 rounded-xl border cursor-pointer transition-all ${
            statusFilter === "occupied" ? "ring-2 ring-red-500 shadow-xs" : ""
          }`}
          style={{ background: STATUS_DEFINITIONS.occupied.bg, borderColor: STATUS_DEFINITIONS.occupied.border }}
        >
          <div className="text-[11px] font-semibold text-red-700 dark:text-red-300 flex items-center justify-between">
            <span>Đã có xe</span>
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
                Cấu hình thời gian bảo vệ chỗ đặt (Protected Window x tiếng)
              </h3>
              <p className="text-[11px] text-[var(--muted)]">
                Owner có toàn quyền thiết lập thời gian bảo vệ trước giờ khách đến. Trong khoảng (Giờ đặt - x), slot tự động khóa bảo vệ.
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <span className="text-xs text-[var(--muted)] font-medium">Thời gian bảo vệ x:</span>
            <div className="flex rounded-lg border border-[var(--border)] bg-[var(--bg)] p-0.5">
              {[0.5, 1.0, 1.5, 2.0].map((val) => (
                <button
                  key={val}
                  type="button"
                  onClick={() => setProtectionWindowHours(val)}
                  className={`px-3 py-1 rounded-md text-xs font-semibold transition-colors ${
                    protectionWindowHours === val
                      ? "bg-[var(--primary)] text-white shadow-xs"
                      : "text-[var(--muted)] hover:text-[var(--fg)]"
                  }`}
                >
                  {val === 0.5 ? "30 phút" : `${val} tiếng`}
                </button>
              ))}
            </div>
          </div>
        </div>

        {/* Formula Explainer */}
        <div className="p-3 rounded-lg border border-[var(--border)] bg-[var(--bg)]/60 text-xs text-[var(--muted)] flex items-start gap-2">
          <UntitledIcon name="info" size={14} className="text-[var(--primary)] mt-0.5 shrink-0" />
          <div className="leading-relaxed">
            <strong className="text-[var(--fg)]">Quy tắc bảo vệ: </strong>
            Nếu khách đặt chỗ từ <strong>10:00 - 12:00</strong> với x = <strong>{protectionWindowHours} tiếng</strong>, slot bắt đầu được bảo vệ từ{" "}
            <strong className="text-indigo-600 dark:text-indigo-400">
              {10 - protectionWindowHours}:00
            </strong>
            . Trong khoảng từ {10 - protectionWindowHours}:00 đến 10:00, slot tính là <strong>Protected</strong> $\rightarrow$ Khóa slot, cấm xe vãng lai vào.
          </div>
        </div>
      </div>

      {/* Interactive Simulation Widget: Conflict & Auto-Reallocation Demo */}
      <div className="rounded-xl border border-indigo-500/30 bg-gradient-to-r from-indigo-500/5 via-[var(--card)] to-purple-500/5 p-4.5 space-y-4">
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 border-b border-[var(--border)] pb-3">
          <div>
            <div className="flex items-center gap-2">
              <span className="flex h-6 w-6 items-center justify-center rounded-full bg-indigo-500 text-white text-xs font-bold">
                🎮
              </span>
              <h3 className="font-bold text-sm text-[var(--fg)]">
                Trình mô phỏng Kịch bản Đặt chỗ & Xử lý Xung đột tự động
              </h3>
            </div>
            <p className="text-xs text-[var(--muted)] mt-0.5">
              Mô phỏng xe vãng lai đỗ 6h-9h, khách đặt 10h-12h, kích hoạt Protected, phát hiện xe chưa rời và tự động Reallocate slot mới.
            </p>
          </div>

          <div className="flex items-center gap-2">
            <button
              type="button"
              className="btn-outline text-xs px-3 py-1.5"
              onClick={handleResetSim}
              disabled={simStep === 0}
            >
              Đặt lại kịch bản
            </button>
            <button
              type="button"
              className="btn-primary text-xs px-4 py-1.5 flex items-center gap-1.5"
              onClick={handleNextSimStep}
              disabled={simStep >= 4}
            >
              <span>{simStep === 0 ? "Bắt đầu mô phỏng" : `Tiến trình: Bước ${simStep + 1} / 4`}</span>
              <UntitledIcon name="arrow-right" size={13} />
            </button>
          </div>
        </div>

        {/* Step Indicator */}
        <div className="grid grid-cols-1 sm:grid-cols-4 gap-2.5">
          <div
            className={`p-2.5 rounded-lg border text-xs transition-all ${
              simStep >= 1 ? "border-blue-500/40 bg-blue-500/10" : "border-[var(--border)] bg-[var(--bg)]/50 opacity-60"
            }`}
          >
            <div className="font-bold text-blue-700 dark:text-blue-300">Bước 1: 06h - 08h30</div>
            <div className="text-[11px] text-[var(--muted)] mt-1">Xe 1 đỗ (Occupied). Khách B đặt trước 10h-12h.</div>
          </div>

          <div
            className={`p-2.5 rounded-lg border text-xs transition-all ${
              simStep >= 2 ? "border-indigo-500/40 bg-indigo-500/10" : "border-[var(--border)] bg-[var(--bg)]/50 opacity-60"
            }`}
          >
            <div className="font-bold text-indigo-700 dark:text-indigo-300">Bước 2: 09h00 (10h - x)</div>
            <div className="text-[11px] text-[var(--muted)] mt-1">Kích hoạt Protected. Trạng thái Occupied + Protected!</div>
          </div>

          <div
            className={`p-2.5 rounded-lg border text-xs transition-all ${
              simStep >= 3 ? "border-amber-500/40 bg-amber-500/10" : "border-[var(--border)] bg-[var(--bg)]/50 opacity-60"
            }`}
          >
            <div className="font-bold text-amber-700 dark:text-amber-300">Bước 3: 09h15 (9h - 9h45)</div>
            <div className="text-[11px] text-[var(--muted)] mt-1">Phát hiện xe chưa rời! Báo khách B & tìm slot mới.</div>
          </div>

          <div
            className={`p-2.5 rounded-lg border text-xs transition-all ${
              simStep >= 4 ? "border-emerald-500/40 bg-emerald-500/10" : "border-[var(--border)] bg-[var(--bg)]/50 opacity-60"
            }`}
          >
            <div className="font-bold text-emerald-700 dark:text-emerald-300">Bước 4: 09h16 (TH1 Thành công)</div>
            <div className="text-[11px] text-[var(--muted)] mt-1">Auto Reallocate slot mới & gửi thông báo Driver.</div>
          </div>
        </div>

        {/* Real-time simulation event stream */}
        {simLogs.length > 0 && (
          <div className="rounded-lg border border-[var(--border)] bg-[var(--bg)] p-3 space-y-2 max-h-40 overflow-y-auto font-mono text-[11px]">
            <div className="font-sans font-bold text-xs text-[var(--fg)] mb-1 flex items-center justify-between">
              <span>Nhật ký điều phối thời gian thực:</span>
              <span className="text-[10px] text-emerald-600 dark:text-emerald-400">● Live Stream</span>
            </div>
            {simLogs.map((log, idx) => (
              <div
                key={idx}
                className={`p-2 rounded border flex items-start gap-2 ${
                  log.type === "success"
                    ? "border-emerald-500/30 bg-emerald-500/10 text-emerald-800 dark:text-emerald-300"
                    : log.type === "warning"
                      ? "border-amber-500/30 bg-amber-500/10 text-amber-800 dark:text-amber-300"
                      : "border-blue-500/30 bg-blue-500/10 text-blue-800 dark:text-blue-300"
                }`}
              >
                <span className="font-bold shrink-0">[{log.time}]</span>
                <span className="font-sans text-xs leading-relaxed">{log.msg}</span>
              </div>
            ))}
          </div>
        )}
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
              <span>Thêm tầng</span>
            </button>
          </div>

          {/* Zone filter */}
          <div className="flex items-center gap-2 text-xs">
            <span className="text-[var(--muted)] font-medium">Khu vực:</span>
            <select
              className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-2.5 py-1 text-xs text-[var(--fg)] outline-none"
              value={selectedZoneFilter}
              onChange={(e) => setSelectedZoneFilter(e.target.value)}
            >
              <option value="all">Tất cả khu vực ({currentFloor?.zones?.length || 0})</option>
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
              + Thêm Zone
            </button>
          </div>
        </div>

        {/* Search bar inside floor layout */}
        <div className="flex flex-wrap items-center justify-between gap-3 p-3 rounded-xl border border-[var(--border)] bg-[var(--card)] text-xs">
          <div className="flex items-center gap-2 flex-1 min-w-[200px] max-w-sm">
            <UntitledIcon name="search" size={14} className="text-[var(--muted)] shrink-0" />
            <input
              className="w-full bg-transparent text-xs text-[var(--fg)] placeholder:text-[var(--muted)] outline-none"
              placeholder="Tìm theo mã slot (ví dụ: A01), biển số xe, tên khách…"
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
            Hiển thị: <strong>{displayedSpaces.length}</strong> / {allSpaces.length} slots
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
                        ⚡ Realloc từ {space.requestedSlotCode}
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
            Không tìm thấy slot đỗ xe nào phù hợp với bộ lọc hiện tại.
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