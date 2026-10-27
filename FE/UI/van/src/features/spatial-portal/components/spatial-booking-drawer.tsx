import * as React from "react"
import { X, Clock, MapPin, Car, Bike, ShieldCheck, ChevronDown, CheckCircle2, ArrowRight, Calendar } from "lucide-react"
import { Button, MorphButton } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import type { ParkingSlot, RegisteredVehicle } from "@/types/domain"
import { formatVietnamesePlateValue } from "@/features/vehicle/utils/plate-formatter"

export interface SpatialBookingDrawerProps {
  isOpen: boolean
  onClose: () => void
  lotName: string
  lotAddress: string
  slot: ParkingSlot | null
  floor: number
  hourlyRate: number
  vehicles: RegisteredVehicle[]
  selectedVehicle: RegisteredVehicle
  sidebarOffset?: number
  onSelectVehicle: (vehicle: RegisteredVehicle) => void
  onConfirmBooking: (data: {
    slotNumber: string
    vehicle: RegisteredVehicle
    date: string
    entryTime: string
    hours: number
    totalAmount: number
  }) => void
}

export function SpatialBookingDrawer({
  isOpen,
  onClose,
  lotName,
  lotAddress,
  slot,
  floor,
  hourlyRate,
  vehicles,
  selectedVehicle,
  sidebarOffset = 92,
  onSelectVehicle,
  onConfirmBooking,
}: SpatialBookingDrawerProps) {
  // Date & Time picker states
  const [bookingDate, setBookingDate] = React.useState<"today" | "tomorrow" | "custom">("today")
  const [customDate, setCustomDate] = React.useState(new Date().toISOString().split("T")[0])
  const [entryMode, setEntryMode] = React.useState<"now" | "scheduled">("now")
  const [scheduledTime, setScheduledTime] = React.useState("08:30")
  const [durationHours, setDurationHours] = React.useState<number>(4)
  const [isVehicleDropdownOpen, setIsVehicleDropdownOpen] = React.useState<boolean>(false)

  if (!isOpen || !slot) return null

  const totalAmount = durationHours * hourlyRate
  const isCar = selectedVehicle.vehicleType === "CAR"
  const formattedPlate = formatVietnamesePlateValue(
    selectedVehicle.rawPlate,
    selectedVehicle.vehicleType
  )

  const getDateLabel = () => {
    if (bookingDate === "today") return "Hôm nay"
    if (bookingDate === "tomorrow") return "Ngày mai"
    return customDate
  }

  const getTimeLabel = () => {
    if (entryMode === "now") return "Bây giờ"
    return scheduledTime
  }

  const handleConfirm = () => {
    onConfirmBooking({
      slotNumber: slot.number,
      vehicle: selectedVehicle,
      date: getDateLabel(),
      entryTime: getTimeLabel(),
      hours: durationHours,
      totalAmount,
    })
  }

  return (
    <div
      className="fixed top-4 bottom-4 w-[440px] max-w-[calc(100vw-110px)] z-20 rounded-2xl backdrop-blur-2xl bg-white/95 text-slate-900 border border-slate-200/90 shadow-2xl dark:bg-[#181918]/95 dark:text-white dark:border-white/15 p-6 flex flex-col justify-between overflow-hidden select-none animate-in fade-in slide-in-from-left-6 duration-300 transition-[left]"
      style={{ left: sidebarOffset }}
    >
      {/* 1. Drawer Header */}
      <div>
        <div className="flex items-center justify-between pb-3 border-b border-slate-200/80 dark:border-white/10">
          <div className="flex items-center gap-2">
            <span className="text-sm font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
              ĐẶT CHỖ NHANH
            </span>
            <Badge variant="available">Tầng B{floor}</Badge>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="size-8 rounded-full bg-slate-100 hover:bg-slate-200 text-slate-600 dark:bg-white/10 dark:hover:bg-white/20 dark:text-slate-300 hover:text-slate-900 dark:hover:text-white flex items-center justify-center transition-colors cursor-pointer"
          >
            <X size={16} />
          </button>
        </div>

        {/* Big Slot Hero & Lot Address */}
        <div className="py-4 flex items-center justify-between gap-3">
          <div className="overflow-hidden">
            <h3 className="font-extrabold text-xl text-slate-900 dark:text-white tracking-tight truncate">{lotName}</h3>
            <p className="text-sm text-slate-500 dark:text-slate-400 flex items-center gap-1.5 mt-0.5 truncate">
              <MapPin size={15} className="text-[#285337] dark:text-[#B4E33F] shrink-0" />
              <span className="truncate">{lotAddress}</span>
            </p>
          </div>

          {/* Slot Badge: min-w-[88px] and whitespace-nowrap guarantees "A-022" never breaks */}
          <div className="min-w-[88px] px-3.5 h-14 rounded-2xl bg-[#B4E33F] text-[#1E3B27] flex flex-col items-center justify-center font-extrabold shadow-sm shrink-0 whitespace-nowrap">
            <span className="text-sm tracking-wider uppercase opacity-85 leading-tight font-bold">Ô ĐỖ</span>
            <span className="text-2xl leading-none tracking-tight">{slot.number}</span>
          </div>
        </div>
      </div>

      {/* 2. Interactive Form Controls (Scrollable inside drawer) */}
      <div className="space-y-4 overflow-y-auto pr-1 my-auto">

        {/* Vehicle Selection Dropdown */}
        <div className="space-y-1.5">
          <label className="text-sm font-bold uppercase tracking-wider text-slate-600 dark:text-slate-300">
            Phương tiện đỗ xe
          </label>

          <div className="relative">
            <button
              type="button"
              onClick={() => setIsVehicleDropdownOpen((prev) => !prev)}
              className="w-full p-3 rounded-xl bg-slate-50 dark:bg-white/5 border border-slate-200 dark:border-white/15 hover:border-[#285337] dark:hover:border-[#B4E33F] flex items-center justify-between text-left transition-all cursor-pointer"
            >
              <div className="flex items-center gap-3">
                <div className="size-9 rounded-lg bg-emerald-500/15 text-[#285337] dark:text-[#B4E33F] flex items-center justify-center">
                  {isCar ? <Car size={18} /> : <Bike size={18} />}
                </div>
                <div>
                  <p className="font-bold text-base text-slate-900 dark:text-white tabular-nums">{formattedPlate}</p>
                </div>
              </div>
              <ChevronDown size={18} className="text-slate-400" />
            </button>

            {/* Dropdown Menu */}
            {isVehicleDropdownOpen && (
              <div className="absolute top-full left-0 right-0 mt-1.5 p-1.5 rounded-xl bg-white dark:bg-[#181918] border border-slate-200 dark:border-white/20 shadow-2xl z-30 space-y-1 animate-in fade-in zoom-in-95 duration-150">
                {vehicles.map((veh) => {
                  const plate = formatVietnamesePlateValue(veh.rawPlate, veh.vehicleType)
                  const isSelected = selectedVehicle.id === veh.id
                  return (
                    <button
                      key={veh.id}
                      type="button"
                      onClick={() => {
                        onSelectVehicle(veh)
                        setIsVehicleDropdownOpen(false)
                      }}
                      className={`w-full p-2.5 rounded-lg flex items-center justify-between text-left transition-colors cursor-pointer ${
                        isSelected
                          ? "bg-[#285337] text-white dark:bg-[#B4E33F] dark:text-[#152A1B] font-bold"
                          : "hover:bg-slate-100 dark:hover:bg-white/10 text-slate-800 dark:text-white"
                      }`}
                    >
                      <div className="flex items-center gap-2.5">
                        {veh.vehicleType === "CAR" ? <Car size={18} /> : <Bike size={18} />}
                        <span className="text-sm font-semibold tabular-nums">{plate}</span>
                      </div>
                      {isSelected && <CheckCircle2 size={18} />}
                    </button>
                  )
                })}
              </div>
            )}
          </div>
        </div>

        {/* Date Selector */}
        <div className="space-y-1.5">
          <label className="text-sm font-bold uppercase tracking-wider text-slate-600 dark:text-slate-300 flex items-center gap-1.5">
            <Calendar size={15} className="text-[#285337] dark:text-[#B4E33F]" />
            <span>Ngày đỗ xe</span>
          </label>
          <div className="grid grid-cols-3 gap-2">
            <button
              type="button"
              onClick={() => setBookingDate("today")}
              className={`py-2.5 px-3 rounded-xl text-sm font-bold transition-all cursor-pointer text-center ${
                bookingDate === "today"
                  ? "bg-[#285337] text-white dark:bg-[#B4E33F] dark:text-[#152A1B] shadow-xs"
                  : "bg-slate-100 dark:bg-white/5 border border-slate-200 dark:border-white/10 text-slate-700 dark:text-slate-300 hover:text-slate-950 dark:hover:text-white"
              }`}
            >
              Hôm nay
            </button>
            <button
              type="button"
              onClick={() => setBookingDate("tomorrow")}
              className={`py-2.5 px-3 rounded-xl text-sm font-bold transition-all cursor-pointer text-center ${
                bookingDate === "tomorrow"
                  ? "bg-[#285337] text-white dark:bg-[#B4E33F] dark:text-[#152A1B] shadow-xs"
                  : "bg-slate-100 dark:bg-white/5 border border-slate-200 dark:border-white/10 text-slate-700 dark:text-slate-300 hover:text-slate-950 dark:hover:text-white"
              }`}
            >
              Ngày mai
            </button>
            <button
              type="button"
              onClick={() => setBookingDate("custom")}
              className={`py-2.5 px-3 rounded-xl text-sm font-bold transition-all cursor-pointer text-center ${
                bookingDate === "custom"
                  ? "bg-[#285337] text-white dark:bg-[#B4E33F] dark:text-[#152A1B] shadow-xs"
                  : "bg-slate-100 dark:bg-white/5 border border-slate-200 dark:border-white/10 text-slate-700 dark:text-slate-300 hover:text-slate-950 dark:hover:text-white"
              }`}
            >
              Chọn ngày
            </button>
          </div>
          {bookingDate === "custom" && (
            <input
              type="date"
              value={customDate}
              onChange={(e) => setCustomDate(e.target.value)}
              className="w-full mt-1.5 px-3.5 py-2 rounded-xl bg-slate-50 dark:bg-white/5 border border-slate-200 dark:border-white/15 text-sm text-slate-900 dark:text-white focus:outline-none"
            />
          )}
        </div>

        {/* Entry Time Selector */}
        <div className="space-y-1.5">
          <label className="text-sm font-bold uppercase tracking-wider text-slate-600 dark:text-slate-300 flex items-center gap-1.5">
            <Clock size={15} className="text-[#285337] dark:text-[#B4E33F]" />
            <span>Giờ bắt đầu vào</span>
          </label>
          <div className="grid grid-cols-2 gap-2">
            <button
              type="button"
              onClick={() => setEntryMode("now")}
              className={`py-2.5 px-3 rounded-xl text-sm font-bold transition-all cursor-pointer text-center ${
                entryMode === "now"
                  ? "bg-[#285337] text-white dark:bg-[#B4E33F] dark:text-[#152A1B] shadow-xs"
                  : "bg-slate-100 dark:bg-white/5 border border-slate-200 dark:border-white/10 text-slate-700 dark:text-slate-300 hover:text-slate-950 dark:hover:text-white"
              }`}
            >
              Vào ngay (Bây giờ)
            </button>
            <button
              type="button"
              onClick={() => setEntryMode("scheduled")}
              className={`py-2.5 px-3 rounded-xl text-sm font-bold transition-all cursor-pointer text-center ${
                entryMode === "scheduled"
                  ? "bg-[#285337] text-white dark:bg-[#B4E33F] dark:text-[#152A1B] shadow-xs"
                  : "bg-slate-100 dark:bg-white/5 border border-slate-200 dark:border-white/10 text-slate-700 dark:text-slate-300 hover:text-slate-950 dark:hover:text-white"
              }`}
            >
              Hẹn giờ vào
            </button>
          </div>
          {entryMode === "scheduled" && (
            <input
              type="time"
              value={scheduledTime}
              onChange={(e) => setScheduledTime(e.target.value)}
              className="w-full mt-1.5 px-3.5 py-2 rounded-xl bg-slate-50 dark:bg-white/5 border border-slate-200 dark:border-white/15 text-sm text-slate-900 dark:text-white font-mono focus:outline-none"
            />
          )}
        </div>

        {/* Duration Selection (Chips) */}
        <div className="space-y-1.5">
          <label className="text-sm font-bold uppercase tracking-wider text-slate-600 dark:text-slate-300">
            Thời lượng dự kiến
          </label>
          <div className="grid grid-cols-4 gap-2">
            {[1, 2, 4, 8].map((h) => (
              <button
                key={h}
                type="button"
                onClick={() => setDurationHours(h)}
                className={`py-2.5 px-2.5 rounded-xl text-sm font-bold transition-all cursor-pointer text-center ${
                  durationHours === h
                    ? "bg-[#285337] text-white dark:bg-[#B4E33F] dark:text-[#152A1B] shadow-xs"
                    : "bg-slate-100 dark:bg-white/5 border border-slate-200 dark:border-white/10 text-slate-700 dark:text-slate-300 hover:text-slate-950 dark:hover:text-white"
                }`}
              >
                {h} tiếng
              </button>
            ))}
          </div>
        </div>

        {/* Pricing Summary (1-line Receipt) */}
        <div className="p-4 rounded-xl bg-slate-50 dark:bg-white/5 border border-slate-200 dark:border-white/10 text-sm space-y-2">
          <div className="flex items-center justify-between text-slate-600 dark:text-slate-300">
            <span>Đơn giá niêm yết:</span>
            <span className="font-semibold text-slate-900 dark:text-white">
              {hourlyRate.toLocaleString("vi-VN")} VND / giờ
            </span>
          </div>
          <div className="flex items-center justify-between text-slate-600 dark:text-slate-300">
            <span>Thời gian đỗ:</span>
            <span className="font-semibold text-slate-900 dark:text-white">
              {getDateLabel()} · {getTimeLabel()} ({durationHours} tiếng)
            </span>
          </div>
          <div className="pt-2 border-t border-slate-200 dark:border-white/10 flex items-baseline justify-between">
            <span className="text-sm uppercase font-bold text-slate-500 dark:text-slate-400">TỔNG TẠM TÍNH:</span>
            <span className="text-xl font-extrabold text-[#285337] dark:text-[#B4E33F] tabular-nums">
              {totalAmount.toLocaleString("vi-VN")} VND
            </span>
          </div>
        </div>

        <div className="flex items-center gap-2 text-sm text-slate-500 dark:text-slate-400">
          <ShieldCheck size={16} className="text-emerald-500 shrink-0" />
          <span>Miễn phí hủy trước giờ vào 15 phút. Camera ANPR nhận diện xe tự động.</span>
        </div>
      </div>

      {/* 3. Drawer Bottom CTA */}
      <div className="pt-4 border-t border-slate-200/80 dark:border-white/10">
        <MorphButton
          intent="primary"
          size="lg"
          text="Xác nhận & Giữ chỗ"
          icon={<ArrowRight size={20} />}
          onClick={handleConfirm}
          className="w-full h-12 text-base font-bold shadow-xl"
        />
      </div>

    </div>
  )
}
