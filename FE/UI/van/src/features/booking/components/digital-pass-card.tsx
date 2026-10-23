import * as React from "react"
import { Compass, CheckCircle2, QrCode, ArrowLeft, Clock, ShieldCheck, AlertTriangle } from "lucide-react"
import { Button, MorphButton } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"

export interface DigitalPassCardProps {
  lotName: string
  slotNumber: string
  locationPath: string
  plateNumber: string
  vehicleCategory: string
  startTime: string
  endTime: string
  durationHours: number
  totalAmount: number
  onBackToMap?: () => void
  onCancelBooking?: () => void
}

export function DigitalPassCard({
  lotName,
  slotNumber,
  locationPath,
  plateNumber,
  vehicleCategory,
  startTime,
  endTime,
  durationHours,
  totalAmount,
  onBackToMap,
  onCancelBooking,
}: DigitalPassCardProps) {
  const [showCancelModal, setShowCancelModal] = React.useState(false)

  return (
    <div className="max-w-2xl mx-auto rounded-[var(--radius-2xl)] border border-[var(--border)] bg-[var(--card)] shadow-xs overflow-hidden">
      {/* 1. Header: Context & Pass Status */}
      <div className="p-6 border-b border-[var(--border)] flex flex-col sm:flex-row sm:items-center justify-between gap-3">
        <div>
          <div className="flex items-center gap-2 mb-1">
            <Badge variant="available">Đang giữ chỗ</Badge>
            <span className="text-sm text-[var(--muted)] font-mono tabular-nums">Mã vé: SP-2026-8892</span>
          </div>
          <h3 className="font-semibold text-lg sm:text-xl text-[var(--fg)] tracking-tight">
            {lotName}
          </h3>
        </div>

        <button
          type="button"
          onClick={onBackToMap}
          className="inline-flex items-center gap-1.5 text-sm text-[var(--muted)] hover:text-[var(--fg)] font-medium cursor-pointer transition-colors"
        >
          <ArrowLeft size={16} />
          <span>Về sơ đồ 3D</span>
        </button>
      </div>

      {/* 2. QR Code Barrier Section */}
      <div className="p-6 bg-[var(--muted)]/[0.02] border-b border-[var(--border)] flex flex-col items-center justify-center text-center">
        <div className="size-48 p-3 rounded-[var(--radius-xl)] bg-white border border-[var(--border)] shadow-xs flex flex-col items-center justify-center">
          {/* Simulated High-Res Barrier QR */}
          <div className="size-full bg-neutral-900 rounded-[var(--radius-md)] flex flex-col items-center justify-center text-white p-3 relative overflow-hidden">
            <QrCode size={110} className="text-white" />
            <span className="text-[10px] tracking-widest font-mono text-neutral-400 mt-1 uppercase">
              SCAN AT BARRIER
            </span>
          </div>
        </div>
        <p className="text-sm font-medium text-[var(--fg)] mt-3 flex items-center gap-1.5">
          <ShieldCheck size={16} className="text-[var(--primary)]" />
          <span>Quét mã QR tại cổng barrier tự động để mở cần chắn</span>
        </p>
        <p className="text-sm text-[var(--muted)] mt-0.5">
          Cổng nhận diện biển số tự động qua camera ANPR kết hợp mã QR.
        </p>
      </div>

      {/* 3. Three-Bay Synchronized Summary Cards */}
      <div className="p-6 border-b border-[var(--border)] bg-[var(--muted)]/[0.03]">
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-3.5">
          {/* Bay 1: Slot */}
          <div className="p-4 rounded-[var(--radius-xl)] border border-[var(--border)] bg-[var(--card)] text-center flex flex-col items-center justify-between min-h-[120px]">
            <span className="text-sm font-semibold uppercase tracking-wider text-[var(--muted)]">
              Vị trí ô đỗ
            </span>
            <span className="text-3xl font-extrabold tracking-tight text-[var(--fg)] tabular-nums my-1">
              {slotNumber}
            </span>
            <span className="text-sm text-[var(--muted)] font-medium">{locationPath}</span>
          </div>

          {/* Bay 2: Vehicle */}
          <div className="p-4 rounded-[var(--radius-xl)] border border-[var(--border)] bg-[var(--card)] text-center flex flex-col items-center justify-between min-h-[120px]">
            <span className="text-sm font-semibold uppercase tracking-wider text-[var(--muted)]">
              Phương tiện
            </span>
            <span className="text-lg font-bold tracking-wider text-[var(--fg)] tabular-nums my-1">
              {plateNumber}
            </span>
            <span className="text-sm text-[var(--muted)] font-medium">{vehicleCategory}</span>
          </div>

          {/* Bay 3: Time */}
          <div className="p-4 rounded-[var(--radius-xl)] border border-[var(--border)] bg-[var(--card)] text-center flex flex-col items-center justify-between min-h-[120px]">
            <span className="text-sm font-semibold uppercase tracking-wider text-[var(--muted)]">
              Khung giờ đỗ
            </span>
            <span className="text-lg font-bold tracking-wide text-[var(--fg)] tabular-nums my-1">
              {startTime} – {endTime}
            </span>
            <span className="text-sm text-[var(--muted)] font-medium">{durationHours} tiếng</span>
          </div>
        </div>
      </div>

      {/* 4. Single-line Receipt Summary */}
      <div className="p-6 border-b border-[var(--border)]">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 p-4 rounded-[var(--radius-lg)] bg-[var(--muted)]/[0.04] border border-[var(--border)] text-sm">
          <div className="flex items-center gap-2 text-[var(--muted)]">
            <Clock size={16} className="text-[var(--primary)]" />
            <span>Giữ chỗ trước giờ vào:</span>
            <strong className="text-[var(--fg)] font-semibold">15 phút</strong>
            <span>·</span>
            <span>Tổng cộng:</span>
            <strong className="text-[var(--fg)] font-semibold">{durationHours} tiếng</strong>
          </div>

          <div className="flex items-baseline gap-2 shrink-0">
            <span className="text-sm text-[var(--muted)] uppercase font-semibold">ĐÃ THANH TOÁN:</span>
            <span className="text-xl font-bold tabular-nums text-[var(--primary)]">
              {totalAmount.toLocaleString("vi-VN")} VND
            </span>
          </div>
        </div>
      </div>

      {/* 5. Actions Footer (Subtle Cancel Outline + Morph Direction Button) */}
      <div className="p-6 bg-[var(--muted)]/[0.02] flex flex-col sm:flex-row items-center justify-between gap-4">
        <Button
          intent="destructive"
          size="md"
          onClick={() => setShowCancelModal(true)}
          className="w-full sm:w-auto text-sm"
        >
          Hủy giữ chỗ
        </Button>

        <div className="flex items-center gap-3 w-full sm:w-auto">
          <MorphButton
            intent="primary"
            size="md"
            text="Chỉ đường GPS"
            icon={<Compass size={18} />}
            minWidth="min-w-[150px]"
            onClick={() => alert("Đang mở bản đồ dẫn đường GPS tới bãi đỗ...")}
          />
        </div>
      </div>

      {/* Modal xác nhận hủy: Màu đỏ cảnh báo CHỈ ĐƯỢC PHÉP xuất hiện ở đây (Rule #1) */}
      {showCancelModal && (
        <div className="fixed inset-0 z-50 bg-black/50 backdrop-blur-xs flex items-center justify-center p-4">
          <div className="bg-[var(--card)] border border-[var(--border)] rounded-[var(--radius-2xl)] max-w-md w-full p-6 space-y-4 shadow-xl">
            <div className="flex items-center gap-3 text-amber-600">
              <div className="size-10 rounded-full bg-amber-500/10 flex items-center justify-center shrink-0">
                <AlertTriangle size={20} />
              </div>
              <h4 className="font-semibold text-base text-[var(--fg)]">
                Xác nhận hủy giữ chỗ?
              </h4>
            </div>
            <p className="text-sm text-[var(--muted)]">
              Ô đỗ <strong>{slotNumber}</strong> sẽ được mở lại cho tài xế khác. Bạn được miễn phí hủy trước giờ vào 15 phút.
            </p>
            <div className="flex items-center justify-end gap-3 pt-2">
              <Button
                intent="ghost"
                size="md"
                onClick={() => setShowCancelModal(false)}
              >
                Giữ lại
              </Button>
              <Button
                intent="secondary"
                size="md"
                className="hover:border-red-500 hover:text-red-500 font-semibold"
                onClick={() => {
                  setShowCancelModal(false)
                  onCancelBooking?.()
                }}
              >
                Xác nhận hủy
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
