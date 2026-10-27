import * as React from "react"
import { cn } from "@/lib/utils"

/**
 * ============================================================================
 * CapacityBar Component (Driver B2C Specimen Standard)
 * Document Ref: User Hand-Drawn Specimen (Khu A · 65 trống · Capsule Bar)
 * Layout:
 *   - Left:  Zone Name (e.g., "Khu A") - semibold text (>= 14px)
 *   - Right: Available Count (e.g., "65 trống") - bold number + clean label (>= 14px)
 *   - Below: Sleek Capsule Progress Bar (rounded-full h-2.5) using semantic CSS tokens
 * ============================================================================
 */
export interface CapacityBarProps extends React.HTMLAttributes<HTMLDivElement> {
  name: string
  available: number
  total: number
  className?: string
}

export function CapacityBar({
  name,
  available,
  total,
  className,
  ...props
}: CapacityBarProps) {
  const occupied = Math.max(0, total - available)
  const occupancyRate = Math.min(100, Math.round((occupied / total) * 100))

  const isFull = available === 0
  const isAlmostFull = !isFull && occupancyRate >= 80

  // Semantic Design Tokens: Danger for full, Warning for almost full, Primary for healthy
  const barColor = isFull
    ? "bg-[var(--badge-danger-bg)]"
    : isAlmostFull
    ? "bg-[var(--badge-warning-bg)]"
    : "bg-[var(--primary)]"

  return (
    <div className={cn("space-y-2.5", className)} {...props}>
      <div className="flex items-baseline justify-between gap-2">
        {/* Left: Zone Name */}
        <span className="font-semibold text-base text-[var(--fg)] tracking-tight">
          {name}
        </span>

        {/* Right: Available Count (e.g., 65 trống) */}
        <div className="text-sm tabular-nums text-right shrink-0">
          <strong className="font-bold text-base text-[var(--fg)] mr-1">
            {available}
          </strong>
          <span className="text-[var(--muted)] font-medium">trống</span>
        </div>
      </div>

      {/* Capsule Progress Bar (rounded-full 10px track) */}
      <div className="h-2.5 w-full rounded-full bg-[var(--border)] overflow-hidden">
        <div
          className={cn("h-full rounded-full transition-all duration-500", barColor)}
          style={{ width: `${occupancyRate}%` }}
        />
      </div>
    </div>
  )
}
