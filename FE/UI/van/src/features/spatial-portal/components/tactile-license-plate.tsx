import * as React from "react"
import { Car, Bike } from "lucide-react"
import type { VehicleType } from "@/types/domain"
import { formatVietnamesePlateValue } from "@/features/vehicle/utils/plate-formatter"

export interface TactileLicensePlateProps {
  plate: string
  vehicleType?: VehicleType
  size?: "sm" | "md" | "lg"
  className?: string
}

/**
 * ============================================================================
 * TactileLicensePlate (Modern Minimalist License Plate Badge)
 * Design Standards:
 * - tempfe/design-system.md: Flat clean card, hairline border, concentric radius
 * - Pure typography & subtle iconography (>= 14px), zero fake chrome / screws
 * - Strictly formatted via formatVietnamesePlateValue
 * ============================================================================
 */
export function TactileLicensePlate({
  plate,
  vehicleType = "CAR",
  size = "md",
  className = "",
}: TactileLicensePlateProps) {
  const formattedPlate = formatVietnamesePlateValue(plate, vehicleType)
  const isCar = vehicleType === "CAR"

  const sizeStyles = {
    sm: "px-3 py-1.5 text-sm gap-2",
    md: "px-4 py-2 text-base gap-2.5",
    lg: "px-5 py-2.5 text-lg gap-3",
  }[size]

  const iconSizes = {
    sm: 15,
    md: 17,
    lg: 19,
  }[size]

  return (
    <div
      className={`inline-flex items-center justify-center select-none rounded-[var(--radius-md)] bg-[var(--card)] text-[var(--fg)] border border-[var(--border)] shadow-xs transition-colors shrink-0 ${sizeStyles} ${className}`}
      aria-label={`Biển số xe ${formattedPlate}`}
    >
      {/* Subtle Vehicle Icon indicator */}
      <span className="text-[var(--muted)] flex items-center justify-center shrink-0">
        {isCar ? <Car size={iconSizes} /> : <Bike size={iconSizes} />}
      </span>

      {/* Formatted Vietnamese License Plate Number */}
      <span className="font-mono font-bold tracking-wider tabular-nums text-[var(--fg)] whitespace-nowrap">
        {formattedPlate}
      </span>
    </div>
  )
}
