import * as React from "react"
import { cn } from "@/lib/utils"

/**
 * ============================================================================
 * CapacityMeter Component (B2B Manager View Standard)
 * Document Ref: tempfe/design-system.md Section 2.3 & tempfe/ai-ui-rules.md Section 6
 * Purpose:
 *   - Dedicated operational dashboard meter for parking facility managers.
 *   - Displays quantitative metrics: `{occupied} / {total} xe` and `{occupancyRate}% lấp đầy`.
 *   - Strictly maintains typography >= 14px (text-sm) and semantic CSS tokens.
 * ============================================================================
 */
export interface CapacityMeterProps extends React.HTMLAttributes<HTMLDivElement> {
  name: string
  occupied: number
  total: number
  className?: string
}

export function CapacityMeter({
  name,
  occupied,
  total,
  className,
  ...props
}: CapacityMeterProps) {
  const safeTotal = Math.max(1, total)
  const safeOccupied = Math.max(0, Math.min(occupied, safeTotal))
  const available = Math.max(0, safeTotal - safeOccupied)
  const occupancyRate = Math.min(100, Math.round((safeOccupied / safeTotal) * 100))

  const isFull = safeOccupied >= safeTotal
  const isAlmostFull = !isFull && occupancyRate >= 85

  // Semantic token progress fill
  const meterFillColor = isFull
    ? "bg-[var(--badge-danger-bg)]"
    : isAlmostFull
    ? "bg-[var(--badge-warning-bg)]"
    : "bg-[var(--primary)]"

  return (
    <div className={cn("space-y-2", className)} {...props}>
      {/* Top Header: Zone Name & Quantitative Count */}
      <div className="flex items-baseline justify-between gap-2">
        <span className="font-semibold text-sm sm:text-base text-[var(--fg)] tracking-tight">
          {name}
        </span>
        <div className="text-sm tabular-nums text-right font-medium text-[var(--fg)]">
          <strong className="font-bold">{safeOccupied}</strong>
          <span className="text-[var(--muted)]"> / {safeTotal} xe</span>
        </div>
      </div>

      {/* Progress Track */}
      <div className="h-2 w-full rounded-full bg-[var(--border)] overflow-hidden">
        <div
          className={cn("h-full rounded-full transition-all duration-500", meterFillColor)}
          style={{ width: `${occupancyRate}%` }}
          role="progressbar"
          aria-valuenow={occupancyRate}
          aria-valuemin={0}
          aria-valuemax={100}
        />
      </div>

      {/* Bottom Sub-metrics: Occupancy Rate & Remaining Available */}
      <div className="flex items-center justify-between text-sm text-[var(--muted)] tabular-nums">
        <span>{occupancyRate}% lấp đầy</span>
        <span>{available} chỗ còn trống</span>
      </div>
    </div>
  )
}
