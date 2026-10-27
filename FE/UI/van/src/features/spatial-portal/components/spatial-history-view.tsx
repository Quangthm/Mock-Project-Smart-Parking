import * as React from "react"
import { Clock, RotateCcw, CheckCircle2, XCircle, ArrowLeft, MapPin, Calendar } from "lucide-react"
import { Button } from "@/components/ui/button"

export interface BookingHistoryItem {
  id: string
  lotName: string
  slotNumber: string
  floor: number
  plateNumber: string
  vehicleType: "CAR" | "MOTORCYCLE"
  date: string
  startTime: string
  endTime: string
  amount: number
  status: "active" | "upcoming" | "completed" | "cancelled"
}

interface SpatialHistoryViewProps {
  onClose: () => void
  onRebookSlot: (item: BookingHistoryItem) => void
  onOpen3DMap: (item: BookingHistoryItem) => void
  sidebarOffset?: number
}

const MOCK_HISTORY: BookingHistoryItem[] = [
  {
    id: "BK-2026-9901",
    lotName: "Vincom Center Đồng Khởi",
    slotNumber: "A-08",
    floor: 1,
    plateNumber: "59-B1 678.90",
    vehicleType: "MOTORCYCLE",
    date: "Hôm nay",
    startTime: "14:00",
    endTime: "18:00",
    amount: 100000,
    status: "active",
  },
  {
    id: "BK-2026-9902",
    lotName: "Saigon Centre (Takashimaya)",
    slotNumber: "P-04",
    floor: 2,
    plateNumber: "51A-123.45",
    vehicleType: "CAR",
    date: "Ngày mai",
    startTime: "09:00",
    endTime: "13:00",
    amount: 120000,
    status: "upcoming",
  },
  {
    id: "BK-2026-8712",
    lotName: "Saigon Centre (Takashimaya)",
    slotNumber: "P-12",
    floor: 2,
    plateNumber: "51A-123.45",
    vehicleType: "CAR",
    date: "19/10/2026",
    startTime: "09:30",
    endTime: "13:30",
    amount: 120000,
    status: "completed",
  },
  {
    id: "BK-2026-7643",
    lotName: "Bitexco Financial Tower",
    slotNumber: "B-04",
    floor: 1,
    plateNumber: "51A-123.45",
    vehicleType: "CAR",
    date: "15/10/2026",
    startTime: "18:00",
    endTime: "21:00",
    amount: 105000,
    status: "completed",
  },
  {
    id: "BK-2026-6520",
    lotName: "Diamond Plaza",
    slotNumber: "C-09",
    floor: 1,
    plateNumber: "59-B1 678.90",
    vehicleType: "MOTORCYCLE",
    date: "12/10/2026",
    startTime: "08:00",
    endTime: "11:00",
    amount: 75000,
    status: "cancelled",
  },
]

export function SpatialHistoryView({
  onClose,
  onRebookSlot,
  onOpen3DMap,
  sidebarOffset = 92,
}: SpatialHistoryViewProps) {
  const [activeTab, setActiveTab] = React.useState<"active" | "upcoming" | "history" | "passes">("active")
  const [bookingTabBeforePasses, setBookingTabBeforePasses] = React.useState<"active" | "upcoming" | "history">("active")

  const togglePasses = () => {
    if (activeTab === "passes") {
      setActiveTab(bookingTabBeforePasses)
      return
    }
    setBookingTabBeforePasses(activeTab)
    setActiveTab("passes")
  }

  React.useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        onClose()
      }
    }
    window.addEventListener("keydown", handleKeyDown)
    return () => window.removeEventListener("keydown", handleKeyDown)
  }, [onClose])

  const activeBookings = MOCK_HISTORY.filter((b) => b.status === "active")
  const upcomingBookings = MOCK_HISTORY.filter((b) => b.status === "upcoming")
  const pastBookings = MOCK_HISTORY.filter((b) => b.status === "completed" || b.status === "cancelled")

  return (
    <div
      className="fixed right-4 top-4 bottom-4 z-20 overflow-y-auto rounded-2xl border border-[var(--border)] bg-[var(--card)] p-4 text-[var(--fg)] shadow-lg animate-in fade-in zoom-in-95 duration-200 transition-[left] sm:right-6 sm:p-6 lg:p-8"
      style={{ left: sidebarOffset }}
    >
      {/* Workspace Header */}
      <div className="mb-6 flex flex-col gap-4 border-b border-[var(--border)] pb-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={onClose}
            className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-[var(--surface-muted,var(--border))] text-[var(--fg)] transition-colors hover:opacity-80 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--focus,var(--primary))]"
            aria-label="Quay lại bản đồ"
          >
            <ArrowLeft size={18} />
          </button>
          <div className="min-w-0">
            <h2 className="text-xl font-semibold leading-tight tracking-tight sm:text-2xl">
              {activeTab === "passes" ? "Gói gửi xe" : "Lịch sử booking"}
            </h2>
            <p className="mt-1 text-sm leading-relaxed text-[var(--muted)]">
              {activeTab === "passes" ? "Thông tin vé định kỳ và gói gửi xe." : "Theo dõi lượt đang đỗ, lịch đặt trước và các phiên đã hoàn tất."}
            </p>
            {activeTab !== "passes" && <span className="mt-2 inline-flex rounded-full border border-[var(--border)] px-3 py-1 text-xs font-medium text-[var(--muted)] md:hidden">Dữ liệu mẫu</span>}
          </div>
        </div>

        <div className="flex items-center gap-2 pl-14 sm:pl-0">
          <span className="hidden rounded-full border border-[var(--border)] px-3 py-1.5 text-xs font-medium text-[var(--muted)] md:inline-flex">Dữ liệu mẫu</span>
          <Button type="button" intent={activeTab === "passes" ? "secondary" : "outline"} onClick={togglePasses} className="min-h-11 px-4">
            {activeTab === "passes" ? "Quay lại lượt đặt" : "Gói gửi xe"}
          </Button>
          <button
            type="button"
            onClick={onClose}
            className="min-h-11 rounded-xl bg-[var(--surface-muted,var(--border))] px-4 text-sm font-semibold text-[var(--fg)] transition-colors hover:opacity-80 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--focus,var(--primary))]"
          >
            Đóng
          </button>
        </div>
      </div>

      {/* Navigation Tabs (3 Dedicated Sections: Đang đỗ, Sắp tới, Lịch sử) */}
      {activeTab !== "passes" && <nav aria-label="Lọc lượt đặt và lịch sử" className="mb-6 flex flex-wrap gap-2 border-b border-[var(--border)] pb-4">
        <button
          type="button"
          aria-pressed={activeTab === "active"}
          onClick={() => setActiveTab("active")}
          className={`flex h-11 shrink-0 items-center gap-2 rounded-xl px-3 text-sm font-semibold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--focus)] sm:px-4 ${
            activeTab === "active"
              ? "bg-[var(--primary)] text-[var(--primary-fg)]"
              : "bg-[var(--surface-muted,var(--border))] text-[var(--fg)] hover:opacity-80"
          }`}
        >
          <span
            aria-hidden="true"
            className={`size-2 shrink-0 rounded-full ${activeTab === "active" ? "bg-[var(--primary-fg)]" : "bg-[var(--badge-success-bg)]"}`}
          />
          <span>Đang đỗ</span>
          <span className="px-2 py-0.5 rounded-full bg-white/20 text-sm font-semibold">{activeBookings.length}</span>
        </button>

        <button
          type="button"
          aria-pressed={activeTab === "upcoming"}
          onClick={() => setActiveTab("upcoming")}
          className={`flex h-11 shrink-0 items-center gap-2 rounded-xl px-3 text-sm font-semibold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--focus)] sm:px-4 ${
            activeTab === "upcoming"
              ? "bg-[var(--primary)] text-[var(--primary-fg)]"
              : "bg-[var(--surface-muted,var(--border))] text-[var(--fg)] hover:opacity-80"
          }`}
        >
          <Calendar size={15} />
          <span>Sắp diễn ra</span>
          <span className="px-2 py-0.5 rounded-full bg-white/20 text-sm font-semibold">{upcomingBookings.length}</span>
        </button>

        <button
          type="button"
          aria-pressed={activeTab === "history"}
          onClick={() => setActiveTab("history")}
          className={`flex h-11 shrink-0 items-center gap-2 rounded-xl px-3 text-sm font-semibold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--focus)] sm:px-4 ${
            activeTab === "history"
              ? "bg-[var(--primary)] text-[var(--primary-fg)]"
              : "bg-[var(--surface-muted,var(--border))] text-[var(--fg)] hover:opacity-80"
          }`}
        >
          <Clock size={15} />
          <span>Lịch sử đã đỗ</span>
          <span className="px-2 py-0.5 rounded-full bg-white/20 text-sm font-semibold">{pastBookings.length}</span>
        </button>
      </nav>}

      {activeTab === "passes" && (
        <section aria-labelledby="pass-overview-title" className="max-w-5xl space-y-6">
          <div className="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
            <div>
              <h3 id="pass-overview-title" className="text-lg font-semibold tracking-tight">Vé định kỳ của bạn</h3>
              <p className="mt-1 text-sm text-[var(--muted)]">Tổng quan vé tháng và thông tin thời hạn.</p>
            </div>
            <span className="w-fit rounded-full border border-[var(--border)] bg-[var(--surface-muted,var(--border))] px-3 py-1.5 text-xs font-medium text-[var(--muted)]">Bản minh họa · Dữ liệu mẫu</span>
          </div>

          <article className="rounded-2xl border border-[var(--border)] bg-[var(--card)] p-4 sm:p-6">
            <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
              <div className="min-w-0">
                <p className="text-sm font-medium text-[var(--muted)]">Vé tháng · 30 ngày</p>
                <h4 className="mt-1 text-lg font-semibold">Vincom Center Đồng Khởi</h4>
                <p className="mt-2 text-sm text-[var(--muted)]">Xe 51A-123.45 · Ô tô cá nhân</p>
              </div>
              <span className="w-fit rounded-full border border-[var(--border)] px-3 py-1.5 text-sm font-medium">Minh họa trạng thái: còn hạn</span>
            </div>

            <div className="mt-6 grid grid-cols-1 gap-4 border-t border-[var(--border)] pt-4 sm:grid-cols-3">
              <div>
                <p className="text-sm text-[var(--muted)]">Thời hạn còn lại</p>
                <p className="mt-1 font-semibold tabular-nums">18 ngày</p>
              </div>
              <div>
                <p className="text-sm text-[var(--muted)]">Chu kỳ</p>
                <p className="mt-1 font-semibold">30 ngày</p>
              </div>
              <div>
                <p className="text-sm text-[var(--muted)]">Giá tham khảo</p>
                <p className="mt-1 font-semibold tabular-nums">1.800.000 VND / tháng</p>
              </div>
            </div>
          </article>

          <div className="rounded-xl border border-[var(--border)] bg-[var(--surface-muted,var(--border))] p-4 sm:p-5">
            <h4 className="text-sm font-semibold">Giao dịch chưa khả dụng</h4>
            <p className="mt-1 text-sm leading-relaxed text-[var(--muted)]">Thông tin trên chỉ minh họa giao diện. Chức năng tra cứu gói thật và gia hạn chưa được kết nối, vì vậy hiện chưa thể xác nhận giá hoặc thực hiện thanh toán.</p>
          </div>
        </section>
      )}

      {/* TAB 1: ĐANG ĐỖ (ACTIVE) - NO QR CODE */}
      {activeTab === "active" && (
        <div className="space-y-4">
          {activeBookings.length === 0 ? (
            <p className="text-sm text-slate-500 py-8 text-center">Hiện tại bạn không có xe nào đang đỗ trong bãi.</p>
          ) : (
            activeBookings.map((item) => (
              <div
                key={item.id}
                className="flex flex-col items-start gap-4 rounded-2xl border border-[var(--border)] bg-[var(--card)] p-4 sm:p-6 md:flex-row md:items-center md:justify-between"
              >
                <div className="flex w-full min-w-0 flex-col items-start gap-4 sm:flex-row">
                  {/* Hero Bay Badge (concentric & borderless) */}
                  <div className="flex min-h-11 w-full items-center justify-between rounded-xl border border-[var(--border)] bg-[var(--surface-muted,var(--border))] px-3 text-sm font-semibold sm:min-h-16 sm:w-auto sm:min-w-20 sm:flex-col sm:justify-center sm:px-4">
                    <span className="text-[var(--muted)]">Ô đỗ</span>
                    <span className="text-base tabular-nums">{item.slotNumber}</span>
                  </div>

                  <div className="min-w-0 flex-1 space-y-2">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="rounded-full border border-[var(--border)] px-3 py-1 text-sm font-medium">Đang đỗ xe</span>
                      <span className="break-all text-sm font-mono text-[var(--muted)]">{item.id}</span>
                    </div>

                    <h3 className="text-lg font-semibold leading-snug">{item.lotName}</h3>
                    <p className="flex items-center gap-2 text-sm text-[var(--muted)]">
                      <MapPin size={16} className="shrink-0 text-[var(--primary)]" />
                      <span>Khu A · Tầng B{item.floor}</span>
                    </p>

                    <div className="grid grid-cols-1 gap-3 border-t border-[var(--border)] pt-4 text-sm sm:grid-cols-3 sm:gap-4">
                      <div>
                        <span className="block text-sm font-medium text-[var(--muted)]">Biển số xe</span>
                        <span className="font-semibold tabular-nums">{item.plateNumber}</span>
                      </div>
                      <div>
                        <span className="block text-sm font-medium text-[var(--muted)]">Giờ vào – ra dự kiến</span>
                        <span className="font-semibold tabular-nums">
                          {item.startTime} ➔ {item.endTime}
                        </span>
                      </div>
                      <div>
                        <span className="block text-sm font-medium text-[var(--muted)]">Tổng tạm tính</span>
                        <span className="font-semibold tabular-nums">
                          {item.amount.toLocaleString("vi-VN")} VND
                        </span>
                      </div>
                    </div>
                  </div>
                </div>

                {/* Quick Actions */}
                <div className="flex w-full shrink-0 flex-col gap-2 md:w-auto">
                  <Button
                    intent="secondary"
                    size="md"
                    onClick={() => onOpen3DMap(item)}
                    className="h-11 text-sm font-semibold"
                  >
                    Xem lại sơ đồ bãi
                  </Button>
                </div>
              </div>
            ))
          )}
        </div>
      )}

      {/* TAB 2: SẮP DIỄN RA (UPCOMING / RESERVED) */}
      {activeTab === "upcoming" && (
        <div className="space-y-4">
          {upcomingBookings.length === 0 ? (
            <p className="text-sm text-slate-500 py-8 text-center">Bạn chưa có lịch đặt chỗ trước nào trong tương lai.</p>
          ) : (
            upcomingBookings.map((item) => (
              <div
                key={item.id}
                className="p-5 rounded-2xl bg-slate-50 dark:bg-white/5 border border-slate-200 dark:border-white/10 shadow-sm flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4"
              >
                <div className="flex items-start gap-4">
                  <div className="min-w-[80px] px-3.5 h-16 rounded-xl bg-slate-200 dark:bg-white/10 text-slate-800 dark:text-white flex flex-col items-center justify-center font-bold text-base shrink-0 whitespace-nowrap">
                    <span className="text-sm text-slate-500 dark:text-slate-400 uppercase font-semibold">Ô ĐỖ</span>
                    <span>{item.slotNumber}</span>
                  </div>

                  <div className="space-y-1.5">
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-semibold px-2.5 py-0.5 rounded-full bg-blue-500/15 text-blue-700 dark:text-blue-400">
                        Đặt trước: {item.date}
                      </span>
                      <span className="text-sm text-slate-400 font-mono">{item.id}</span>
                    </div>
                    <h4 className="font-bold text-base text-slate-900 dark:text-white">{item.lotName}</h4>
                    <p className="text-sm text-slate-500 dark:text-slate-400">
                      Tầng B{item.floor} · {item.plateNumber} · Giờ vào: {item.startTime} ({item.amount.toLocaleString("vi-VN")} VND)
                    </p>
                  </div>
                </div>

                <div className="flex items-center gap-2 self-end sm:self-center">
                  <Button
                    intent="outline"
                    size="sm"
                    disabled
                    title="Chức năng hủy đặt chỗ chưa được kết nối trong bản prototype"
                    className="h-11 px-4 text-sm"
                  >
                    Hủy đặt chỗ · Chưa khả dụng
                  </Button>
                </div>
              </div>
            ))
          )}
        </div>
      )}

      {/* TAB 3: LỊCH SỬ ĐÃ ĐỖ (PAST SESSIONS) */}
      {activeTab === "history" && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {pastBookings.map((item) => {
            const isCompleted = item.status === "completed"
            return (
              <div
                key={item.id}
                className="p-5 rounded-xl bg-slate-50 dark:bg-white/5 border border-slate-200 dark:border-white/10 hover:border-slate-300 dark:hover:border-white/20 transition-all flex flex-col justify-between gap-3.5"
              >
                <div className="flex items-start justify-between gap-2">
                  <div>
                    <p className="font-bold text-base text-slate-900 dark:text-white">{item.lotName}</p>
                    <p className="text-sm text-slate-500 dark:text-slate-400">
                      Ô {item.slotNumber} · Tầng B{item.floor} · {item.plateNumber}
                    </p>
                  </div>

                  <span
                    className={`text-sm font-bold px-2.5 py-1 rounded-full flex items-center gap-1.5 ${
                      isCompleted
                        ? "bg-emerald-500/15 text-emerald-700 dark:text-emerald-400"
                        : "bg-slate-200 text-slate-600 dark:bg-white/10 dark:text-slate-400"
                    }`}
                  >
                    {isCompleted ? <CheckCircle2 size={14} /> : <XCircle size={14} />}
                    <span>{isCompleted ? "Hoàn tất" : "Đã hủy"}</span>
                  </span>
                </div>

                <div className="flex items-center justify-between text-sm pt-2.5 border-t border-slate-200 dark:border-white/5 text-slate-500 dark:text-slate-400">
                  <span className="tabular-nums">{item.date}, {item.startTime}</span>
                  <span className="text-slate-900 dark:text-white font-bold tabular-nums">
                    {item.amount.toLocaleString("vi-VN")} VND
                  </span>
                </div>

                {/* Rebook CTA with high contrast border & readable text */}
                <div className="pt-1">
                  <button
                    type="button"
                    onClick={() => onRebookSlot(item)}
                    className="w-full h-9 rounded-lg border border-slate-300 hover:border-slate-400 bg-white hover:bg-slate-50 text-slate-800 dark:border-white/20 dark:bg-white/5 dark:hover:bg-white/10 dark:text-slate-200 text-sm font-semibold flex items-center justify-center gap-2 transition-colors cursor-pointer"
                  >
                    <RotateCcw size={14} />
                    <span>Đặt lại chỗ này</span>
                  </button>
                </div>
              </div>
            )
          })}
        </div>
      )}
    </div>
  )
}
