import * as React from "react"
import { Car, Bike, Edit2, Check } from "lucide-react"
import { Card, CardContent } from "@/components/ui/card"
import { Button } from "@/components/ui/button"
import type { RegisteredVehicle } from "@/types/domain"
import { formatVietnamesePlateValue } from "../utils/plate-formatter"

export interface VehicleCardProps {
  vehicle: RegisteredVehicle
  onEdit?: (vehicle: RegisteredVehicle) => void
  onSelect?: (vehicle: RegisteredVehicle) => void
  selected?: boolean
}

export function VehicleCard({
  vehicle,
  onEdit,
  onSelect,
  selected = false,
}: VehicleCardProps) {
  const isCar = vehicle.vehicleType === "CAR"
  const formattedPlate = formatVietnamesePlateValue(
    vehicle.canonicalPlate || vehicle.rawPlate,
    vehicle.vehicleType
  )

  return (
    <Card
      className={`transition-colors ${
        selected
          ? "border-[var(--primary)] bg-[var(--primary)]/[0.04]"
          : "hover:border-[var(--border)]"
      }`}
    >
      <CardContent className="p-4 flex items-center justify-between gap-3">
        {/* Left: Icon & Plate details */}
        <div className="flex items-center gap-3 min-w-0">
          <div className="size-10 rounded-[var(--radius-md)] flex items-center justify-center shrink-0 bg-[var(--muted)]/10 text-[var(--fg)]">
            {isCar ? <Car size={18} strokeWidth={1.75} /> : <Bike size={18} strokeWidth={1.75} />}
          </div>

          <div className="min-w-0">
            <span className="text-sm font-semibold tracking-wider text-[var(--fg)] tabular-nums">
              {formattedPlate}
            </span>
            <p className="text-sm text-[var(--muted)] truncate">
              {vehicle.canonicalPlate}
            </p>
          </div>
        </div>

        {/* Right: Actions */}
        <div className="flex items-center gap-2 shrink-0">
          {onSelect && (
            <Button
              intent={selected ? "primary" : "outline"}
              size="sm"
              onClick={() => onSelect(vehicle)}
              className="text-sm h-8 px-3"
            >
              {selected ? (
                <>
                  <Check size={14} strokeWidth={2.5} />
                  <span>Đã chọn</span>
                </>
              ) : (
                "Chọn xe"
              )}
            </Button>
          )}

          {onEdit && (
            <Button
              intent="ghost"
              size="icon"
              className="size-8 text-[var(--muted)] hover:text-[var(--fg)]"
              onClick={() => onEdit(vehicle)}
              title="Chỉnh sửa thông tin xe"
            >
              <Edit2 size={14} />
            </Button>
          )}
        </div>
      </CardContent>
    </Card>
  )
}
