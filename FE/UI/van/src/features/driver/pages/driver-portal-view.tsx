import * as React from "react"
import { MapPin, Layers, Car, Bike, Sparkles, Compass, CheckCircle2, RotateCcw } from "lucide-react"
import { Button, MorphButton } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import { CapacityBar } from "@/components/ui/capacity-bar"
import { Parking3DViewer } from "@/components/3d/parking-3d-viewer"
import { BookingTerminal, DigitalPassCard } from "@/features/booking"
import type { ParkingSlot, RegisteredVehicle } from "@/types/domain"
import { formatVietnamesePlateValue } from "@/features/vehicle/utils/plate-formatter"

const mockSlots: ParkingSlot[] = [
  { id: "slot-a08", number: "A-08", floor: 1, status: "available" },
  { id: "slot-p24", number: "P-24", floor: 1, status: "available" },
  { id: "slot-c01", number: "C-01", floor: 1, status: "available" },
  { id: "slot-a01", number: "A-01", floor: 1, status: "occupied" },
  { id: "slot-a02", number: "A-02", floor: 1, status: "occupied" },
  { id: "slot-a03", number: "A-03", floor: 1, status: "reserved" },
  { id: "slot-a04", number: "A-04", floor: 1, status: "available" },
  { id: "slot-a05", number: "A-05", floor: 1, status: "occupied" },
  { id: "slot-b01", number: "B-01", floor: 2, status: "available" },
  { id: "slot-b02", number: "B-02", floor: 2, status: "occupied" },
]

interface DriverPortalViewProps {
  vehicles: RegisteredVehicle[]
}

export function DriverPortalView({ vehicles }: DriverPortalViewProps) {
  // State
  const [selectedFloor, setSelectedFloor] = React.useState<number>(1)
  const [selectedSlot, setSelectedSlot] = React.useState<ParkingSlot>(mockSlots[0])
  const [selectedVehicle, setSelectedVehicle] = React.useState<RegisteredVehicle>(vehicles[0])
  const [confirmedBookingData, setConfirmedBookingData] = React.useState<{
    lotName: string
    slot: string
    locationPath: string
    vehicle: RegisteredVehicle
    startTime: string
    endTime: string
    durationHours: number
    totalPrice: number
  } | null>(null)

  const handleSelectSlotFrom3D = (slot: ParkingSlot) => {
    setSelectedSlot(slot)
  }

  const handleConfirmBooking = (data: {
    lotName: string
    slot: string
    locationPath: string
    vehicle: RegisteredVehicle
    startTime: string
    endTime: string
    durationHours: number
    totalPrice: number
  }) => {
    setConfirmedBookingData(data)
  }

  const handleCancelBooking = () => {
    setConfirmedBookingData(null)
  }

  return (
    <div className="space-y-8">
      {/* 1. Context Header: Vincom Center Đồng Khởi + Live Capacity */}
      <div className="rounded-[var(--radius-2xl)] border border-[var(--border)] bg-[var(--card)] p-6 shadow-xs flex flex-col md:flex-row md:items-center justify-between gap-6">
        <div className="space-y-2">
          <div className="flex items-center gap-2">
            <Badge variant="available">Đang mở cửa</Badge>
            <span className="text-xs text-[var(--muted)]">Hầm B1 · B2 thông minh</span>
          </div>
          <h1 className="text-2xl sm:text-3xl font-extrabold text-[var(--fg)] tracking-tight">
            Bãi đỗ Vincom Center Đồng Khởi
          </h1>
          <p className="text-sm text-[var(--muted)] flex items-center gap-1.5">
            <MapPin size={15} className="text-[var(--primary)] shrink-0" />
            <span>72 Lê Thánh Tôn, Bến Nghé, Quận 1, TP.HCM · 0.8 km từ bạn</span>
          </p>
        </div>

        {/* Live Capacity Card (Using hand-drawn CapacityBar standard) */}
        <div className="w-full md:w-80 p-4 rounded-[var(--radius-xl)] bg-[var(--muted)]/[0.04] border border-[var(--border)] space-y-3">
          <div className="flex items-center justify-between text-sm text-[var(--muted)] font-medium">
            <span>TÌNH TRẠNG LẤP ĐẦY</span>
            <span className="text-[var(--primary)] font-semibold">Cập nhật tức thì</span>
          </div>
          <CapacityBar name="Khu A (Tầng B1)" available={65} total={100} />
          <div className="flex items-center justify-between text-sm text-[var(--muted)] pt-1 border-t border-[var(--border)]">
            <span>Đơn giá niêm yết:</span>
            <strong className="text-[var(--fg)] tabular-nums">25.000 VND / giờ</strong>
          </div>
        </div>
      </div>

      {/* 2. Main Experience Flow (3D Map-First Architecture) */}
      {confirmedBookingData ? (
        /* Khi đã chốt giữ chỗ -> Hiển thị Vé Điện Tử với QR & Hủy tinh tế */
        <div className="space-y-4 animate-in fade-in duration-300">
          <div className="flex items-center justify-between max-w-2xl mx-auto">
            <div className="flex items-center gap-2 text-sm text-[var(--muted)]">
              <CheckCircle2 size={16} className="text-[var(--primary)]" />
              <span>Giao dịch hoàn tất · Phiên giữ chỗ có hiệu lực ngay</span>
            </div>
          </div>

          <DigitalPassCard
            lotName={confirmedBookingData.lotName}
            slotNumber={confirmedBookingData.slot}
            locationPath={confirmedBookingData.locationPath}
            plateNumber={formatVietnamesePlateValue(
              confirmedBookingData.vehicle.rawPlate,
              confirmedBookingData.vehicle.vehicleType
            )}
            vehicleCategory={
              confirmedBookingData.vehicle.vehicleType === "CAR"
                ? "Ô tô 4 – 7 chỗ"
                : "Xe máy cá nhân"
            }
            startTime={confirmedBookingData.startTime}
            endTime={confirmedBookingData.endTime}
            durationHours={confirmedBookingData.durationHours}
            totalAmount={confirmedBookingData.totalPrice}
            onBackToMap={() => setConfirmedBookingData(null)}
            onCancelBooking={handleCancelBooking}
          />
        </div>
      ) : (
        /* Khi đang chọn bãi & ô -> 3D Map là Primary Surface */
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-8 items-start">
          {/* Left Column: 3D Interactive Map Surface (7 cols) */}
          <div className="lg:col-span-7 space-y-4">
            <div className="rounded-[var(--radius-2xl)] border border-[var(--border)] bg-[var(--card)] shadow-xs overflow-hidden">
              {/* 3D Map Header Toolbar */}
              <div className="p-4 border-b border-[var(--border)] flex flex-wrap items-center justify-between gap-3 bg-[var(--muted)]/[0.02]">
                <div className="flex items-center gap-2">
                  <div className="size-8 rounded-lg bg-[var(--primary)]/10 text-[var(--primary)] flex items-center justify-center">
                    <Layers size={17} />
                  </div>
                  <div>
                    <h3 className="font-semibold text-sm text-[var(--fg)]">
                      Sơ đồ không gian 3D Digital Twin
                    </h3>
                    <p className="text-xs text-[var(--muted)]">
                      Xoay, zoom và click trực tiếp vào ô đỗ trên mô hình
                    </p>
                  </div>
                </div>

                {/* Floor Switcher */}
                <div className="flex items-center gap-1.5 p-1 rounded-lg bg-[var(--muted)]/10 border border-[var(--border)] text-xs font-medium">
                  <button
                    type="button"
                    onClick={() => setSelectedFloor(1)}
                    className={`px-3 py-1 rounded-md transition-colors cursor-pointer ${
                      selectedFloor === 1
                        ? "bg-[var(--card)] text-[var(--fg)] shadow-xs font-semibold"
                        : "text-[var(--muted)] hover:text-[var(--fg)]"
                    }`}
                  >
                    Tầng B1
                  </button>
                  <button
                    type="button"
                    onClick={() => setSelectedFloor(2)}
                    className={`px-3 py-1 rounded-md transition-colors cursor-pointer ${
                      selectedFloor === 2
                        ? "bg-[var(--card)] text-[var(--fg)] shadow-xs font-semibold"
                        : "text-[var(--muted)] hover:text-[var(--fg)]"
                    }`}
                  >
                    Tầng B2
                  </button>
                </div>
              </div>

              {/* 3D Canvas Container */}
              <div className="relative min-h-[440px] bg-[#0c121e]">
                <Parking3DViewer
                  lotType="basement"
                  slots={mockSlots}
                  selectedSlotId={selectedSlot?.id}
                  focusSlotId={selectedSlot?.id}
                  onSelectSlot={handleSelectSlotFrom3D}
                  height={440}
                  className="w-full"
                />

                {/* 3D Quick Legend Overlay */}
                <div className="absolute bottom-3 left-3 bg-black/70 backdrop-blur-md border border-white/10 rounded-lg py-1.5 px-3 flex items-center gap-3 text-xs text-white">
                  <div className="flex items-center gap-1.5">
                    <span className="size-2.5 rounded-full bg-[#B4E33F]" />
                    <span>Ô trống</span>
                  </div>
                  <div className="flex items-center gap-1.5">
                    <span className="size-2.5 rounded-full bg-[#343434] border border-white/30" />
                    <span>Đã có xe</span>
                  </div>
                  <div className="flex items-center gap-1.5">
                    <span className="size-2.5 rounded-full bg-[#3b82f6]" />
                    <span>Đang chọn</span>
                  </div>
                </div>
              </div>

              {/* Quick Pick Slots Recommendation Chips */}
              <div className="p-4 border-t border-[var(--border)] bg-[var(--muted)]/[0.02] flex items-center justify-between gap-3 text-xs">
                <span className="text-[var(--muted)] font-medium shrink-0 flex items-center gap-1">
                  <Sparkles size={14} className="text-[var(--primary)]" />
                  Gợi ý ô tối ưu:
                </span>
                <div className="flex items-center gap-2 overflow-x-auto">
                  {mockSlots
                    .filter((s) => s.status === "available")
                    .slice(0, 4)
                    .map((slot) => (
                      <button
                        key={slot.id}
                        type="button"
                        onClick={() => setSelectedSlot(slot)}
                        className={`px-3 py-1.5 rounded-lg border text-xs font-semibold transition-all cursor-pointer ${
                          selectedSlot?.id === slot.id
                            ? "bg-[var(--primary)] text-[var(--primary-fg)] border-[var(--primary)] shadow-xs"
                            : "bg-[var(--card)] text-[var(--fg)] border-[var(--border)] hover:border-[var(--primary)]"
                        }`}
                      >
                        {slot.number} · {slot.floor === 1 ? "Tầng B1" : "Tầng B2"}
                      </button>
                    ))}
                </div>
              </div>
            </div>
          </div>

          {/* Right Column: Checkout Terminal (5 cols) */}
          <div className="lg:col-span-5 space-y-4">
            <BookingTerminal
              vehicles={vehicles}
              onConfirmBooking={handleConfirmBooking}
            />
          </div>
        </div>
      )}

      {/* 3. Fast Vehicle Switcher Bar (Bottom assurance) */}
      <div className="rounded-[var(--radius-xl)] border border-[var(--border)] bg-[var(--card)] p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <div className="size-9 rounded-full bg-[var(--muted)]/10 flex items-center justify-center text-[var(--primary)]">
            {selectedVehicle.vehicleType === "CAR" ? <Car size={18} /> : <Bike size={18} />}
          </div>
          <div>
            <div className="flex items-center gap-2">
              <span className="font-semibold text-sm text-[var(--fg)]">
                {formatVietnamesePlateValue(selectedVehicle.rawPlate, selectedVehicle.vehicleType)}
              </span>
              <span className="text-xs text-[var(--muted)]">
                {selectedVehicle.vehicleType === "CAR" ? "Ô tô" : "Xe máy"}
              </span>
            </div>
            <p className="text-xs text-[var(--muted)]">Phương tiện đang kết nối nhận diện Barrier</p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          {vehicles.map((veh) => (
            <Button
              key={veh.id}
              intent={selectedVehicle.id === veh.id ? "primary" : "outline"}
              size="sm"
              onClick={() => setSelectedVehicle(veh)}
              className="text-xs h-8"
            >
              {veh.vehicleType === "CAR" ? <Car size={14} /> : <Bike size={14} />}
              <span>{formatVietnamesePlateValue(veh.rawPlate, veh.vehicleType)}</span>
            </Button>
          ))}
        </div>
      </div>
    </div>
  )
}
