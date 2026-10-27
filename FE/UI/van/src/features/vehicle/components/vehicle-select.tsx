import * as React from "react"
import { Car, Bike, Plus, Check } from "lucide-react"
import { Button } from "@/components/ui/button"
import type { RegisteredVehicle } from "@/types/domain"
import { formatVietnamesePlateValue } from "../utils/plate-formatter"

export interface VehicleSelectProps {
  vehicles: RegisteredVehicle[]
  selectedId: string
  onSelect: (vehicleId: string) => void
  onAddNew?: () => void
  disabled?: boolean
}

export function VehicleSelect({
  vehicles,
  selectedId,
  onSelect,
  onAddNew,
  disabled = false,
}: VehicleSelectProps) {
  if (vehicles.length === 0) {
    return (
      <div className="rounded-[var(--radius-md)] border border-dashed border-[var(--border)] p-4 text-center">
        <p className="text-sm text-[var(--muted)] mb-3">
          Chưa có phương tiện nào được lưu.
        </p>
        {onAddNew && (
          <Button
            intent="outline"
            size="sm"
            onClick={onAddNew}
            disabled={disabled}
            className="w-full justify-center text-sm"
          >
            <Plus size={15} />
            Đăng ký xe mới
          </Button>
        )}
      </div>
    )
  }

  return (
    <div className="space-y-2">
      <div className="grid gap-2">
        {vehicles.map((v) => {
          const isSelected = v.id === selectedId
          const isCar = v.vehicleType === "CAR"
          const plate = formatVietnamesePlateValue(v.canonicalPlate || v.rawPlate, v.vehicleType)

          return (
            <button
              key={v.id}
              type="button"
              disabled={disabled}
              onClick={() => onSelect(v.id)}
              className={`w-full flex items-center justify-between p-3 rounded-[var(--radius-md)] border text-left transition-colors cursor-pointer ${
                isSelected
                  ? "border-[var(--primary)] bg-[var(--primary)]/[0.04]"
                  : "border-[var(--border)] bg-[var(--card)] hover:border-[var(--muted)]/50"
              }`}
            >
              <div className="flex items-center gap-3">
                <span className="p-1.5 rounded-[var(--radius-sm)] bg-[var(--muted)]/10 text-[var(--fg)]">
                  {isCar ? <Car size={16} /> : <Bike size={16} />}
                </span>
                <span className="text-sm font-medium tracking-wider text-[var(--fg)] tabular-nums">
                  {plate}
                </span>
              </div>

              {isSelected && (
                <span className="text-[var(--primary)]">
                  <Check size={16} strokeWidth={2.5} />
                </span>
              )}
            </button>
          )
        })}
      </div>

      {onAddNew && (
        <Button
          intent="ghost"
          size="sm"
          onClick={onAddNew}
          disabled={disabled}
          className="w-full justify-center text-sm text-[var(--muted)] hover:text-[var(--fg)] h-9"
        >
          <Plus size={15} />
          Thêm phương tiện khác
        </Button>
      )}
    </div>
  )
}
