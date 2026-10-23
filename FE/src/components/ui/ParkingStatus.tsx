import { UntitledIcon } from "../icon/UntitledIcon"

export type ParkingStatusKind = "available" | "reserved" | "occupied" | "maintenance"

const statusPresentation: Record<ParkingStatusKind, { label: string; icon: string }> = {
  available: { label: "Available", icon: "check-circle" },
  reserved: { label: "Reserved", icon: "calendar" },
  occupied: { label: "In use", icon: "car" },
  maintenance: { label: "Maintenance", icon: "wrench" },
}

export function ParkingStatus({ status, compact = false }: { status: ParkingStatusKind; compact?: boolean }) {
  const presentation = statusPresentation[status]
  return (
    <span className="parking-status" data-status={status} aria-label={presentation.label}>
      <UntitledIcon name={presentation.icon} size={compact ? 14 : 16} />
      <span>{compact && status === "maintenance" ? "Service" : presentation.label}</span>
    </span>
  )
}
