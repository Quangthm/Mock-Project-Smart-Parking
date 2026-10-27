import * as React from "react"
import { Sun, Moon, Compass, ArrowRight, MapPin, CheckCircle2, X } from "lucide-react"
import { Button, MorphButton } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import { CapacityBar } from "@/components/ui/capacity-bar"
import { CapacityMeter } from "@/components/ui/capacity-meter"
import { BookingTerminal } from "@/features/booking"
import { VehicleSelect } from "@/features/vehicle"
import { SpatialPortalRoot } from "@/features/spatial-portal"
import type { RegisteredVehicle } from "@/types/domain"

const mockVehicles: RegisteredVehicle[] = [
  {
    id: "veh-1",
    rawPlate: "51A12345",
    canonicalPlate: "51A12345",
    vehicleType: "CAR",
    imageReference: null,
  },
  {
    id: "veh-2",
    rawPlate: "59B167890",
    canonicalPlate: "59B167890",
    vehicleType: "MOTORCYCLE",
    imageReference: null,
  },
]

export default function App() {
  const [activeTab, setActiveTab] = React.useState<"driver" | "design-spec">("driver")
  const [isDark, setIsDark] = React.useState(false)
  const [isDriverMode, setIsDriverMode] = React.useState(true)
  const [selectedVehicleId, setSelectedVehicleId] = React.useState("veh-1")
  const [bookingConfirmed, setBookingConfirmed] = React.useState(false)
  const [fontWeight, setFontWeight] = React.useState<"regular" | "semibold" | "bold">("semibold")
  const [morphMode, setMorphMode] = React.useState<"text-to-icon" | "icon-to-text">("text-to-icon")

  if (activeTab === "driver") {
    return <SpatialPortalRoot onOpenDesignSpec={() => setActiveTab("design-spec")} />
  }

  return (
    <div className={`min-h-screen transition-colors ${isDark ? "dark" : ""} ${isDriverMode ? "theme-driver" : ""}`}>
      <div className="bg-[var(--bg)] text-[var(--fg)] min-h-screen py-8 px-4 sm:px-8 text-sm">
        <main className="max-w-5xl mx-auto space-y-10">

          {/* Top Bar with Dual-View Navigation */}
          <div className="flex items-center justify-between text-sm text-[var(--muted)] border-b border-[var(--border)] pb-4">
            <div className="flex items-center gap-3">
              <span className="font-bold tracking-tight text-base text-[var(--fg)]">
                SmartParking
              </span>
              <div className="flex items-center gap-1 p-1 rounded-full bg-[var(--muted)]/10 border border-[var(--border)] text-xs">
                <button
                  type="button"
                  onClick={() => setActiveTab("driver")}
                  className="px-3 py-1 rounded-full font-medium transition-all cursor-pointer text-[var(--muted)] hover:text-[var(--fg)]"
                >
                  🚗 Spatial Portal
                </button>
                <button
                  type="button"
                  onClick={() => setActiveTab("design-spec")}
                  className="px-3 py-1 rounded-full font-medium transition-all cursor-pointer bg-[var(--primary)] text-[var(--primary-fg)] shadow-xs"
                >
                  📐 Design Spec
                </button>
              </div>
            </div>

            <div className="flex items-center gap-3">
              <Button
                intent="ghost"
                size="icon"
                onClick={() => setIsDark((prev) => !prev)}
                className="size-8 text-[var(--muted)] hover:text-[var(--fg)]"
                title="Bật/Tắt Dark mode"
              >
                {isDark ? <Sun size={16} /> : <Moon size={16} />}
              </Button>
            </div>
          </div>

          {/* Design System Spec Showcase */}
          <div className="space-y-16">
              {/* Editorial Specimen Heading */}
              <div className="space-y-4">
                <div className="inline-flex">
                  <span className="px-3.5 py-1 rounded-full text-sm font-medium border border-[var(--border)] text-[var(--fg)]">
                    typography
                  </span>
                </div>
                <h1 className="text-3xl sm:text-5xl font-medium tracking-tight leading-[1.15]">
                  <span className="text-[var(--muted)] block font-normal">clean, modern</span>
                  typography for clarity.
                </h1>
              </div>

          {/* Specimen Showcase: Plus Jakarta Sans & Nature Grid */}
          <section className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">

              {/* Card 1: Impact Green Banner */}
              <div className="bg-[#B4E33F] text-[#1E3B27] p-8 rounded-[var(--radius-xl)] flex flex-col justify-between min-h-[220px]">
                <div>
                  <h3 className="text-2xl sm:text-3xl font-medium tracking-tight">
                    plus jakarta sans
                  </h3>
                  <p className="text-xl sm:text-2xl font-normal opacity-90">
                    font family
                  </p>
                </div>
                <div className="space-y-1 text-sm opacity-85 tabular-nums">
                  <p>1234567890</p>
                  <p className="tracking-wider">abcdefghijklmnopqrstuvwxyz</p>
                </div>
              </div>

              {/* Card 2: Interactive Specimen with Pills */}
              <div className="bg-[#285337] text-white p-8 rounded-[var(--radius-xl)] flex flex-col justify-between min-h-[220px] relative overflow-hidden">
                <div className="flex items-center justify-between z-10">
                  <span className="text-sm uppercase tracking-wider text-[#B4E33F] font-semibold">
                    Type Weights
                  </span>
                  <div className="flex items-center gap-2">
                    {(["regular", "semibold", "bold"] as const).map((w) => (
                      <button
                        key={w}
                        type="button"
                        onClick={() => setFontWeight(w)}
                        className={`px-3 py-1 rounded-full text-sm border transition-colors cursor-pointer capitalize ${
                          fontWeight === w
                            ? "bg-[#B4E33F] text-[#1E3B27] border-[#B4E33F] font-semibold"
                            : "border-white/30 text-white/80 hover:border-white"
                        }`}
                      >
                        {w}
                      </button>
                    ))}
                  </div>
                </div>

                <div className="my-auto py-2 z-10">
                  <span
                    className={`text-6xl sm:text-7xl tracking-tighter ${
                      fontWeight === "regular"
                        ? "font-normal"
                        : fontWeight === "semibold"
                        ? "font-semibold"
                        : "font-bold"
                    }`}
                  >
                    Aa
                  </span>
                </div>

                <div className="space-y-0.5 text-sm text-white/70 tabular-nums z-10">
                  <p>SmartParking Urban Mobility Control</p>
                </div>
              </div>

            </div>
          </section>

          {/* Colours Specimen (The 4 Canonical Swatches) */}
          <section className="space-y-6">
            <div className="flex flex-col sm:flex-row sm:items-end justify-between gap-2">
              <span className="px-3.5 py-1 rounded-full text-sm font-medium border border-[var(--border)] text-[var(--fg)] w-fit">
                colours
              </span>
              <p className="text-sm text-[var(--muted)] max-w-md sm:text-right">
                the minimalist color palette balances technical precision with organic tones to keep complex metrics clear and accessible.
              </p>
            </div>

            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3.5">
              {/* Swatch 1: Deep Forest */}
              <div className="bg-[#285337] text-white p-5 rounded-[var(--radius-xl)] h-56 flex flex-col justify-between">
                <div className="size-7 rounded-full bg-white/20 flex items-center justify-center text-sm font-semibold">
                  1
                </div>
                <div>
                  <p className="text-sm font-medium">deep forest</p>
                  <p className="text-sm opacity-75 tabular-nums">285337</p>
                </div>
              </div>

              {/* Swatch 2: Impact Green */}
              <div className="bg-[#B4E33F] text-[#1E3B27] p-5 rounded-[var(--radius-xl)] h-56 flex flex-col justify-between">
                <div className="size-7 rounded-full bg-black/10 flex items-center justify-center text-sm font-semibold">
                  2
                </div>
                <div>
                  <p className="text-sm font-medium">impact green</p>
                  <p className="text-sm opacity-75 tabular-nums">B4E33F</p>
                </div>
              </div>

              {/* Swatch 3: Soft Ash */}
              <div className="bg-[#D9D9D9] text-[#1F1F1F] p-5 rounded-[var(--radius-xl)] h-56 flex flex-col justify-between border border-[var(--border)]">
                <div className="size-7 rounded-full bg-black/10 flex items-center justify-center text-sm font-semibold">
                  3
                </div>
                <div>
                  <p className="text-sm font-medium">soft ash</p>
                  <p className="text-sm opacity-75 tabular-nums">D9D9D9</p>
                </div>
              </div>

              {/* Swatch 4: Carbon Grey */}
              <div className="bg-[#343434] text-white p-5 rounded-[var(--radius-xl)] h-56 flex flex-col justify-between">
                <div className="size-7 rounded-full bg-white/20 flex items-center justify-center text-sm font-semibold">
                  4
                </div>
                <div>
                  <p className="text-sm font-medium">carbon grey</p>
                  <p className="text-sm opacity-75 tabular-nums">343434</p>
                </div>
              </div>
            </div>
          </section>

          {/* Section: Applied UI - Button Matrix (Carbon Adaptation) */}
          <section className="space-y-4 pt-6 border-t border-[var(--border)]">
            <div className="flex flex-col sm:flex-row sm:items-baseline justify-between gap-3">
              <div>
                <h2 className="text-base font-semibold tracking-tight text-[var(--fg)]">
                  Applied UI · Button Matrix (Carbon Adaptation)
                </h2>
                <p className="text-sm text-[var(--muted)] mt-0.5">
                  Cột &quot;Hover Swap ⇄&quot; tương tác mượt mà: di chuột vào nút để chuyển đổi mượt mà giữa Chữ và Icon (dạng ticker roll, không giật layout).
                </p>
              </div>
              <div className="flex items-center gap-2">
                <Button
                  intent="outline"
                  size="sm"
                  onClick={() =>
                    setMorphMode((prev) => (prev === "text-to-icon" ? "icon-to-text" : "text-to-icon"))
                  }
                  className="h-8 text-sm font-medium"
                >
                  {morphMode === "text-to-icon" ? "Morph: Chữ ➔ Icon" : "Morph: Icon ➔ Chữ"}
                </Button>
                <Button
                  intent="outline"
                  size="sm"
                  onClick={() => setIsDriverMode((prev) => !prev)}
                  className="h-8 text-sm"
                >
                  {isDriverMode ? "Radius: 16px" : "Radius: 12px"}
                </Button>
              </div>
            </div>

            <div className="overflow-x-auto rounded-[var(--radius-lg)] border border-[var(--border)] bg-[var(--card)]">
              <table className="w-full text-left text-sm border-collapse">
                <thead>
                  <tr className="border-b border-[var(--border)] bg-[var(--muted)]/[0.04] text-[var(--muted)]">
                    <th className="py-3 px-4 font-medium w-40">Variant / Intent</th>
                    <th className="py-3 px-4 font-medium">Default</th>
                    <th className="py-3 px-4 font-medium text-[var(--primary)] font-semibold">
                      Hover Swap (Text ⇄ Icon)
                    </th>
                    <th className="py-3 px-4 font-medium">Small (sm)</th>
                    <th className="py-3 px-4 font-medium">Disabled</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[var(--border)]">
                  <tr>
                    <td className="py-3.5 px-4 font-medium text-[var(--fg)]">primary</td>
                    <td className="py-3.5 px-4">
                      <Button intent="primary" size="md">Xác nhận đặt chỗ</Button>
                    </td>
                    <td className="py-3.5 px-4">
                      <MorphButton
                        intent="primary"
                        size="md"
                        text="Tiếp tục"
                        icon={<ArrowRight size={17} />}
                        mode={morphMode}
                      />
                    </td>
                    <td className="py-3.5 px-4">
                      <Button intent="primary" size="sm">Chọn ô</Button>
                    </td>
                    <td className="py-3.5 px-4">
                      <Button intent="primary" size="md" disabled>Xác nhận</Button>
                    </td>
                  </tr>

                  <tr>
                    <td className="py-3.5 px-4 font-medium text-[var(--fg)]">secondary</td>
                    <td className="py-3.5 px-4">
                      <Button intent="secondary" size="md">Xem chi tiết</Button>
                    </td>
                    <td className="py-3.5 px-4">
                      <MorphButton
                        intent="secondary"
                        size="md"
                        text="Chỉ đường"
                        icon={<Compass size={17} />}
                        mode={morphMode}
                      />
                    </td>
                    <td className="py-3.5 px-4">
                      <Button intent="secondary" size="sm">Đổi xe</Button>
                    </td>
                    <td className="py-3.5 px-4">
                      <Button intent="secondary" size="md" disabled>Xem lại</Button>
                    </td>
                  </tr>

                  <tr>
                    <td className="py-3.5 px-4 font-medium text-[var(--fg)]">outline</td>
                    <td className="py-3.5 px-4">
                      <Button intent="outline" size="md">Quy chế bãi</Button>
                    </td>
                    <td className="py-3.5 px-4">
                      <MorphButton
                        intent="outline"
                        size="md"
                        text="Vị trí"
                        icon={<MapPin size={17} />}
                        mode={morphMode}
                      />
                    </td>
                    <td className="py-3.5 px-4">
                      <Button intent="outline" size="sm">Bộ lọc</Button>
                    </td>
                    <td className="py-3.5 px-4">
                      <Button intent="outline" size="md" disabled>Quy chế</Button>
                    </td>
                  </tr>

                  {/* Destructive: Subtle Neutral Outline */}
                  <tr>
                    <td className="py-3.5 px-4 font-medium text-[var(--fg)]">
                      <div>
                        <span>destructive</span>
                        <p className="text-xs text-[var(--muted)] font-normal">Subtle Outline</p>
                      </div>
                    </td>
                    <td className="py-3.5 px-4">
                      <Button intent="destructive" size="md">Hủy đặt chỗ</Button>
                    </td>
                    <td className="py-3.5 px-4">
                      <MorphButton
                        intent="destructive"
                        size="md"
                        text="Hủy phiên"
                        icon={<X size={17} />}
                        mode={morphMode}
                      />
                    </td>
                    <td className="py-3.5 px-4">
                      <Button intent="destructive" size="sm">Xóa xe</Button>
                    </td>
                    <td className="py-3.5 px-4">
                      <Button intent="destructive" size="md" disabled>Đã hủy</Button>
                    </td>
                  </tr>

                  <tr>
                    <td className="py-3.5 px-4 font-medium text-[var(--fg)]">ghost</td>
                    <td className="py-3.5 px-4">
                      <Button intent="ghost" size="md">Quay lại</Button>
                    </td>
                    <td className="py-3.5 px-4">
                      <MorphButton
                        intent="ghost"
                        size="md"
                        text="Đóng"
                        icon={<X size={17} />}
                        mode={morphMode}
                      />
                    </td>
                    <td className="py-3.5 px-4">
                      <Button intent="ghost" size="sm">Sửa</Button>
                    </td>
                    <td className="py-3.5 px-4">
                      <Button intent="ghost" size="md" disabled>Bỏ qua</Button>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </section>

          {/* Section: Status Signals (Solid Pills - No Borders, No Pastel, Same Family) */}
          <section className="space-y-4">
            <div>
              <div className="flex flex-col sm:flex-row sm:items-baseline justify-between gap-1">
                <h2 className="text-base font-semibold tracking-tight text-[var(--fg)]">
                  Applied UI · Status Signals (Solid Borderless Capsules)
                </h2>
                <span className="text-xs text-[var(--muted)]">
                  Solid Tones · No Border · Same-Family Color Contrast
                </span>
              </div>
              <p className="text-sm text-[var(--muted)] mt-0.5">
                Triệt tiêu hoàn toàn lỗi &quot;bạc màu và gồng viền&quot;. Sử dụng màu no sắc, nền và chữ cùng gia đình màu, không dùng viền tương phản bóp nghẹt badge.
              </p>
            </div>

            <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3.5">
              <div className="p-4 rounded-[var(--radius-lg)] border border-[var(--border)] bg-[var(--card)] space-y-3">
                <p className="text-xs text-[var(--muted)] uppercase font-semibold tracking-wider">available</p>
                <div>
                  <Badge variant="available">Còn chỗ</Badge>
                </div>
                <p className="text-xs text-[var(--muted)]">Impact Green #B4E33F</p>
              </div>

              <div className="p-4 rounded-[var(--radius-lg)] border border-[var(--border)] bg-[var(--card)] space-y-3">
                <p className="text-xs text-[var(--muted)] uppercase font-semibold tracking-wider">almostFull</p>
                <div>
                  <Badge variant="almostFull">Sắp hết</Badge>
                </div>
                <p className="text-xs text-[var(--muted)]">Amber Ochre #FDE68A</p>
              </div>

              <div className="p-4 rounded-[var(--radius-lg)] border border-[var(--border)] bg-[var(--card)] space-y-3">
                <p className="text-xs text-[var(--muted)] uppercase font-semibold tracking-wider">occupied</p>
                <div>
                  <Badge variant="occupied">Hết chỗ</Badge>
                </div>
                <p className="text-xs text-[var(--muted)]">Carbon Grey #343434</p>
              </div>

              <div className="p-4 rounded-[var(--radius-lg)] border border-[var(--border)] bg-[var(--card)] space-y-3">
                <p className="text-xs text-[var(--muted)] uppercase font-semibold tracking-wider">primary</p>
                <div>
                  <Badge variant="primary">Đang đỗ</Badge>
                </div>
                <p className="text-xs text-[var(--muted)]">Deep Forest #285337</p>
              </div>

              <div className="p-4 rounded-[var(--radius-lg)] border border-[var(--border)] bg-[var(--card)] space-y-3">
                <p className="text-xs text-[var(--muted)] uppercase font-semibold tracking-wider">neutral</p>
                <div>
                  <Badge variant="neutral">Bảo trì</Badge>
                </div>
                <p className="text-xs text-[var(--muted)]">Soft Ash #D9D9D9</p>
              </div>
            </div>
          </section>

          {/* Section: Capacity Indicators (Driver B2C vs Manager B2B) */}
          <section className="space-y-4">
            <div>
              <div className="flex flex-col sm:flex-row sm:items-baseline justify-between gap-1">
                <h2 className="text-base font-semibold tracking-tight text-[var(--fg)]">
                  Capacity Indicators (Trực Quan Hóa Sức Chứa: B2C vs B2B)
                </h2>
                <span className="text-sm text-[var(--muted)]">
                  Tách bạch góc nhìn Tài xế (CapacityBar) và Quản trị viên (CapacityMeter)
                </span>
              </div>
              <p className="text-sm text-[var(--muted)] mt-0.5">
                Driver B2C nhận diện nhanh qua nhãn ngắn gọn & capsule bar. Manager B2B giám sát số liệu định lượng chi tiết và tỷ lệ lấp đầy.
              </p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {/* Cột 1: Driver B2C View */}
              <div className="p-6 rounded-[var(--radius-xl)] border border-[var(--border)] bg-[var(--card)] space-y-6">
                <div className="border-b border-[var(--border)] pb-2">
                  <span className="text-sm font-semibold text-[var(--fg)]">Góc nhìn Tài xế (Driver B2C)</span>
                  <p className="text-sm text-[var(--muted)]">Tên khu vực & số chỗ trống</p>
                </div>
                <CapacityBar name="Khu A" available={65} total={100} />
                <CapacityBar name="Khu B" available={11} total={100} />
                <CapacityBar name="Khu C" available={0} total={100} />
              </div>

              {/* Cột 2: Manager B2B View */}
              <div className="p-6 rounded-[var(--radius-xl)] border border-[var(--border)] bg-[var(--card)] space-y-6">
                <div className="border-b border-[var(--border)] pb-2">
                  <span className="text-sm font-semibold text-[var(--fg)]">Góc nhìn Quản lý (Manager B2B)</span>
                  <p className="text-sm text-[var(--muted)]">Số liệu định lượng & tỷ lệ lấp đầy</p>
                </div>
                <CapacityMeter name="Khu A (Tầng B1)" occupied={35} total={100} />
                <CapacityMeter name="Khu B (Tầng B2)" occupied={89} total={100} />
                <CapacityMeter name="Khu C (Tầng G)" occupied={100} total={100} />
              </div>
            </div>
          </section>

          {/* Section: Realistic Booking Terminal (Trio Configuration Cards & Spatial Path) */}
          <section className="space-y-4">
            <div>
              <div className="flex flex-col sm:flex-row sm:items-baseline justify-between gap-1">
                <h2 className="text-base font-semibold tracking-tight text-[var(--fg)]">
                  Applied UI · Interactive Booking Terminal
                </h2>
                <span className="text-xs text-[var(--muted)]">
                  Form: Spatial Path + 3 Horizontal Cards (Slot · Phương tiện · Thời gian)
                </span>
              </div>
              <p className="text-sm text-[var(--muted)] mt-0.5">
                Biểu mẫu đặt chỗ áp dụng đường dẫn định vị không gian và dàn 3 ô thông số ngang tương tác trực quan.
              </p>
            </div>

            <BookingTerminal vehicles={mockVehicles} />
          </section>
            </div>

        </main>
      </div>
    </div>
  )
}
