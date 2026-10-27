import * as React from "react"
import { Compass, MapPin, Car, Bike, Clock, CheckCircle2, ArrowRight } from "lucide-react"
import { Button, MorphButton } from "@/components/ui/button"
import type { RegisteredVehicle } from "@/types/domain"
import { formatVietnamesePlateValue } from "@/features/vehicle/utils/plate-formatter"

export interface BookingTerminalProps {
  vehicles: RegisteredVehicle[]
  onConfirmBooking?: (data: {
    lotName: string
    slot: string
    locationPath: string
    vehicle: RegisteredVehicle
    startTime: string
    endTime: string
    durationHours: number
    totalPrice: number
  }) => void
}

export function BookingTerminal({ vehicles, onConfirmBooking }: BookingTerminalProps) {
  // 1. Slot & Location State
  const slots = [
    { number: "P-24", path: "Khu A · Tầng B2" },
    { number: "C1", path: "Khu B · Tầng B1" },
    { number: "A-08", path: "Khu A · Tầng B1" },
  ]
  const [slotIndex, setSlotIndex] = React.useState(0)
  const currentSlot = slots[slotIndex]

  // 2. Vehicle State
  const [vehicleIndex, setVehicleIndex] = React.useState(0)
  const currentVehicle = vehicles[vehicleIndex] || vehicles[0]
  const isCar = currentVehicle?.vehicleType === "CAR"
  const formattedPlate = currentVehicle
    ? formatVietnamesePlateValue(currentVehicle.rawPlate, currentVehicle.vehicleType)
    : "51A-123.45"
  const vehicleCategoryLabel = isCar ? "Ô tô 4 – 7 chỗ" : "Xe máy cá nhân"

  // 3. Time Window State
  const timePresets = [
    { start: "06:00", end: "12:00", hours: 6 },
    { start: "14:00", end: "18:00", hours: 4 },
    { start: "08:00", end: "10:00", hours: 2 },
  ]
  const [timeIndex, setTimeIndex] = React.useState(0)
  const currentTime = timePresets[timeIndex]

  const hourlyRate = 25000
  const totalAmount = currentTime.hours * hourlyRate

  // Confirmation state
  const [isConfirmed, setIsConfirmed] = React.useState(false)

  // Interaction handlers
  const handleToggleSlot = () => {
    setSlotIndex((prev) => (prev + 1) % slots.length)
    setIsConfirmed(false)
  }

  const handleToggleVehicle = () => {
    setVehicleIndex((prev) => (prev + 1) % vehicles.length)
    setIsConfirmed(false)
  }

  const handleToggleTime = () => {
    setTimeIndex((prev) => (prev + 1) % timePresets.length)
    setIsConfirmed(false)
  }

  const handleConfirm = () => {
    setIsConfirmed(true)
    onConfirmBooking?.({
      lotName: "Bãi đỗ Vincom Center Đồng Khởi",
      slot: currentSlot.number,
      locationPath: currentSlot.path,
      vehicle: currentVehicle,
      startTime: currentTime.start,
      endTime: currentTime.end,
      durationHours: currentTime.hours,
      totalPrice: totalAmount,
    })
  }

  return (
    <div className="max-w-2xl rounded-[var(--radius-2xl)] border border-[var(--border)] bg-[var(--card)] shadow-xs overflow-hidden">
      {/* 1. Header: Context Xác nhận bãi xe */}
      <div className="p-6 border-b border-[var(--border)]">
        <h3 className="font-semibold text-lg sm:text-xl text-[var(--fg)] tracking-tight">
          Bãi đỗ Vincom Center Đồng Khởi
        </h3>
        <p className="text-sm text-[var(--muted)] mt-1.5 flex items-center gap-1.5">
          <MapPin size={15} className="shrink-0 text-[var(--primary)]" />
          <span>72 Lê Thánh Tôn, Bến Nghé, Quận 1, TP.HCM · 0.8 km từ bạn</span>
        </p>
      </div>

      {/* 2. Configuration Bay: 3 Ô dàn ngang cùng format phẳng, thoáng đãng */}
      <div className="p-6 bg-[var(--muted)]/[0.03] border-b border-[var(--border)]">
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-3.5">

          {/* Ô 1: Vị trí & Đường dẫn không gian tích hợp */}
          <button
            type="button"
            onClick={handleToggleSlot}
            className="p-5 rounded-[var(--radius-xl)] border border-[var(--border)] bg-[var(--card)] hover:border-[var(--primary)] hover:shadow-xs transition-all text-center flex flex-col items-center justify-between min-h-[145px] cursor-pointer group"
            title="Bấm để đổi ô đỗ"
          >
            <span className="text-sm font-semibold uppercase tracking-wider text-[var(--muted)] group-hover:text-[var(--primary)]">
              Vị trí ô đỗ
            </span>

            {/* Big Slot Hero */}
            <div className="my-auto py-1">
              <span className="text-3xl sm:text-4xl font-extrabold tracking-tight text-[var(--fg)] group-hover:scale-105 transition-transform inline-block">
                {currentSlot.number}
              </span>
            </div>

            {/* Integrated Spatial Path */}
            <span className="text-sm text-[var(--muted)] font-medium">
              {currentSlot.path}
            </span>
          </button>

          {/* Ô 2: Phương tiện với biển số định dạng chuẩn */}
          <button
            type="button"
            onClick={handleToggleVehicle}
            className="p-5 rounded-[var(--radius-xl)] border border-[var(--border)] bg-[var(--card)] hover:border-[var(--primary)] hover:shadow-xs transition-all text-center flex flex-col items-center justify-between min-h-[145px] cursor-pointer group"
            title="Bấm để đổi phương tiện"
          >
            <span className="text-sm font-semibold uppercase tracking-wider text-[var(--muted)] group-hover:text-[var(--primary)]">
              Phương tiện
            </span>

            {/* Formatted License Plate Hero */}
            <div className="my-auto py-1">
              <div className="inline-flex items-center gap-2">
                {isCar ? (
                  <Car size={18} className="text-[var(--fg)] shrink-0" />
                ) : (
                  <Bike size={18} className="text-[var(--fg)] shrink-0" />
                )}
                <span className="text-lg font-bold tracking-wider text-[var(--fg)] tabular-nums">
                  {formattedPlate}
                </span>
              </div>
            </div>

            {/* Vehicle Type Description */}
            <span className="text-sm text-[var(--muted)] font-medium">
              {vehicleCategoryLabel}
            </span>
          </button>

          {/* Ô 3: Thời gian phẳng, đồng bộ style với 2 ô trước */}
          <button
            type="button"
            onClick={handleToggleTime}
            className="p-5 rounded-[var(--radius-xl)] border border-[var(--border)] bg-[var(--card)] hover:border-[var(--primary)] hover:shadow-xs transition-all text-center flex flex-col items-center justify-between min-h-[145px] cursor-pointer group"
            title="Bấm để đổi thời lượng đỗ"
          >
            <span className="text-sm font-semibold uppercase tracking-wider text-[var(--muted)] group-hover:text-[var(--primary)]">
              Khung giờ đỗ
            </span>

            {/* Time Window Hero */}
            <div className="my-auto py-1">
              <span className="text-lg font-bold tracking-wide text-[var(--fg)] tabular-nums group-hover:text-[var(--primary)] transition-colors">
                {currentTime.start} – {currentTime.end}
              </span>
            </div>

            {/* Duration */}
            <span className="text-sm text-[var(--muted)] font-medium">
              {currentTime.hours} tiếng
            </span>
          </button>

        </div>
      </div>

      {/* 3. Receipt Summary: Cụm tóm tắt chi phí thanh mảnh (1 dòng duy nhất) */}
      <div className="p-6 border-b border-[var(--border)]">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 p-4 rounded-[var(--radius-lg)] bg-[var(--muted)]/[0.04] border border-[var(--border)] text-sm">
          <div className="flex flex-wrap items-center gap-2 text-[var(--muted)] font-medium">
            <span>Đơn giá: {hourlyRate.toLocaleString("vi-VN")} VND / giờ</span>
            <span>·</span>
            <span>{currentTime.hours} tiếng</span>
            <span>➔</span>
            <span className="text-[var(--fg)] uppercase font-semibold text-sm">TẠM TÍNH:</span>
            <strong className="text-lg font-bold tabular-nums text-[var(--primary)]">
              {totalAmount.toLocaleString("vi-VN")} VND
            </strong>
          </div>
        </div>
      </div>

      {/* 4. Terminal Footer Actions: Cam kết & Cặp nút hành động */}
      <div className="p-6 bg-[var(--muted)]/[0.02] flex flex-col sm:flex-row items-center justify-between gap-4">
        <p className="text-sm text-[var(--muted)]">
          Miễn phí hủy trước giờ vào 15 phút.
        </p>

        <div className="flex items-center gap-3 w-full sm:w-auto">
          <MorphButton
            intent="secondary"
            size="md"
            text="Chỉ đường"
            icon={<Compass size={18} />}
            minWidth="min-w-[130px]"
            onClick={() => alert("Đang mở bản đồ dẫn đường GPS...")}
          />

          <MorphButton
            intent="primary"
            size="md"
            text={isConfirmed ? `Đã giữ ô ${currentSlot.number}` : "Xác nhận giữ chỗ"}
            icon={isConfirmed ? <CheckCircle2 size={18} /> : <ArrowRight size={18} />}
            minWidth={isConfirmed ? "min-w-[170px]" : "min-w-[160px]"}
            onClick={handleConfirm}
          />
        </div>
      </div>
    </div>
  )
}
