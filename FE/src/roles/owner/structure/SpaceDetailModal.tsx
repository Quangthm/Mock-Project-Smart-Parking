import { useState } from "react"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import type {
  ParkingSpaceItem,
  SpaceStatus,
  PhysicalState,
  ReservationProtectionState,
} from "../../../lib/structureTypes"

interface SpaceDetailModalProps {
  space: ParkingSpaceItem | null
  floorName: string
  floorCode: string
  zoneName: string
  onClose: () => void
  onUpdateStatus: (
    spaceId: string,
    newStatus: SpaceStatus,
    note?: string,
    options?: {
      physicalState?: PhysicalState
      reservationState?: ReservationProtectionState
      isProtected?: boolean
      isBackup?: boolean
    },
  ) => void
}

export function SpaceDetailModal({
  space,
  floorName,
  floorCode,
  zoneName,
  onClose,
  onUpdateStatus,
}: SpaceDetailModalProps) {
  const [maintenanceNote, setMaintenanceNote] = useState("")
  const [showMaintInput, setShowMaintInput] = useState(false)

  if (!space) return null

  const physical: PhysicalState =
    space.physicalState ||
    (space.status === "occupied"
      ? "OCCUPIED"
      : space.status === "maintenance"
        ? "MAINTENANCE"
        : space.status === "disabled"
          ? "UNAVAILABLE"
          : "AVAILABLE")

  const reservation: ReservationProtectionState =
    space.reservationState ||
    (space.isProtected
      ? "PROTECTED"
      : space.isPendingPayment
        ? "PENDING_PAYMENT"
        : space.isBackup
          ? "BACKUP"
          : space.status === "reserved"
            ? "RESERVED"
            : "NONE")

  const getPhysicalBadge = (state: PhysicalState) => {
    switch (state) {
      case "AVAILABLE":
        return {
          label: "Physically Vacant",
          icon: "check-circle",
          color: "#16a34a",
          bg: "#22c55e15",
          border: "#22c55e40",
        }
      case "OCCUPIED":
        return {
          label: "Physically Occupied",
          icon: "car",
          color: "#dc2626",
          bg: "#ef444415",
          border: "#ef444440",
        }
      case "MAINTENANCE":
        return {
          label: "Under Maintenance",
          icon: "wrench",
          color: "#ea580c",
          bg: "#f9731615",
          border: "#f9731640",
        }
      case "UNAVAILABLE":
        return {
          label: "Out of Service",
          icon: "x",
          color: "#64748b",
          bg: "#64748b15",
          border: "#64748b40",
        }
      case "UNKNOWN":
      default:
        return {
          label: "Condition Unknown",
          icon: "alert",
          color: "#eab308",
          bg: "#eab30815",
          border: "#eab30840",
        }
    }
  }

  const getReservationBadge = (res: ReservationProtectionState) => {
    switch (res) {
      case "PROTECTED":
        return {
          label: "Protected Hold",
          icon: "shield",
          color: "#6366f1",
          bg: "#6366f115",
          border: "#6366f140",
        }
      case "RESERVED":
        return {
          label: "Confirmed Booking",
          icon: "clock",
          color: "#d97706",
          bg: "#f59e0b15",
          border: "#f59e0b40",
        }
      case "PENDING_PAYMENT":
        return {
          label: "Pending Payment Hold",
          icon: "banknote",
          color: "#ca8a04",
          bg: "#eab30815",
          border: "#eab30840",
        }
      case "BACKUP":
        return {
          label: "Designated Backup",
          icon: "shield",
          color: "#7c3aed",
          bg: "#8b5cf615",
          border: "#8b5cf640",
        }
      case "NONE":
      default:
        return {
          label: "Open / Unreserved",
          icon: "check",
          color: "var(--muted)",
          bg: "var(--bg)",
          border: "var(--border)",
        }
    }
  }

  const pBadge = getPhysicalBadge(physical)
  const rBadge = getReservationBadge(reservation)
  const isSimultaneous =
    physical === "OCCUPIED" &&
    (reservation === "PROTECTED" || space.isProtected)
  const isReallocated = Boolean(
    space.requestedSlotCode && space.requestedSlotCode !== space.code,
  )

  return (
    <div
      role="presentation"
      onMouseDown={(e) => {
        if (e.target === e.currentTarget) onClose()
      }}
      style={{
        position: "fixed",
        inset: 0,
        zIndex: 1000,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: "1rem",
        background: "rgba(2, 6, 23, 0.65)",
        backdropFilter: "blur(4px)",
      }}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="space-modal-title"
        className="card"
        style={{
          width: "100%",
          maxWidth: 560,
          background: "var(--card)",
          color: "var(--fg)",
          border: "1px solid var(--border)",
          borderRadius: "1rem",
          boxShadow: "0 20px 50px rgba(0, 0, 0, 0.35)",
          padding: "1.5rem",
          maxHeight: "90vh",
          overflowY: "auto",
        }}
      >
        {/* Header */}
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "flex-start",
            marginBottom: "1.25rem",
          }}
        >
          <div>
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: "0.5rem",
                marginBottom: "0.25rem",
              }}
            >
              <span
                style={{
                  fontSize: "0.72rem",
                  fontWeight: 700,
                  color: "var(--primary)",
                  letterSpacing: "0.08em",
                  textTransform: "uppercase",
                }}
              >
                Floor {floorCode} · {zoneName}
              </span>
            </div>
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: "0.75rem",
                flexWrap: "wrap",
              }}
            >
              <h3
                id="space-modal-title"
                style={{
                  fontFamily: "Outfit",
                  fontWeight: 800,
                  fontSize: "1.6rem",
                  margin: 0,
                }}
              >
                Slot {space.code}
              </h3>
              <div
                style={{ display: "flex", gap: "0.35rem", flexWrap: "wrap" }}
              >
                <span
                  style={{
                    display: "inline-flex",
                    alignItems: "center",
                    gap: "0.3rem",
                    fontSize: "0.72rem",
                    fontWeight: 700,
                    padding: "0.2rem 0.55rem",
                    borderRadius: "999px",
                    background: pBadge.bg,
                    color: pBadge.color,
                    border: `1px solid ${pBadge.border}`,
                  }}
                >
                  <UntitledIcon name={pBadge.icon} size={13} />
                  {pBadge.label}
                </span>

                {reservation !== "NONE" && (
                  <span
                    style={{
                      display: "inline-flex",
                      alignItems: "center",
                      gap: "0.3rem",
                      fontSize: "0.72rem",
                      fontWeight: 700,
                      padding: "0.2rem 0.55rem",
                      borderRadius: "999px",
                      background: rBadge.bg,
                      color: rBadge.color,
                      border: `1px solid ${rBadge.border}`,
                    }}
                  >
                    <UntitledIcon name={rBadge.icon} size={13} />
                    {rBadge.label}
                  </span>
                )}
              </div>
            </div>
          </div>
          <button
            type="button"
            aria-label="Close"
            onClick={onClose}
            style={{
              width: 32,
              height: 32,
              borderRadius: "50%",
              border: "1px solid var(--border)",
              background: "var(--bg)",
              color: "var(--fg)",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              cursor: "pointer",
            }}
          >
            <UntitledIcon name="x" size={16} />
          </button>
        </div>

        {/* Simultaneous OCCUPIED + PROTECTED Alert / Callout */}
        {isSimultaneous && (
          <div
            style={{
              padding: "0.85rem 1rem",
              borderRadius: "0.75rem",
              background: "#818cf818",
              border: "1.5px solid #818cf860",
              marginBottom: "1rem",
            }}
          >
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: "0.5rem",
                color: "#6366f1",
                marginBottom: "0.3rem",
              }}
            >
              <UntitledIcon name="shield" size={16} />
              <strong style={{ fontSize: "0.85rem" }}>
                SRS State Combination: OCCUPIED + PROTECTED
              </strong>
            </div>
            <p
              style={{
                margin: 0,
                fontSize: "0.78rem",
                color: "var(--fg)",
                lineHeight: 1.5,
              }}
            >
              This slot is physically occupied while its Reservation Protection
              Window is active for an upcoming confirmed booking. Per SRS
              policy, ordinary walk-ins are blocked, occupying drivers are not
              displaced prematurely, and backend conflict resolution is tracking
              fulfillment.
            </p>
          </div>
        )}

        {/* Reallocation Notice */}
        {isReallocated && (
          <div
            style={{
              padding: "0.85rem 1rem",
              borderRadius: "0.75rem",
              background: "#3b82f615",
              border: "1px solid #3b82f640",
              marginBottom: "1rem",
            }}
          >
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: "0.5rem",
                color: "var(--primary)",
                marginBottom: "0.2rem",
              }}
            >
              <UntitledIcon name="arrow-right" size={16} />
              <strong style={{ fontSize: "0.85rem" }}>
                Reallocated Assignment (SRS 3.4.6)
              </strong>
            </div>
            <p
              style={{
                margin: 0,
                fontSize: "0.78rem",
                color: "var(--fg)",
                lineHeight: 1.45,
              }}
            >
              Driver requested slot <strong>{space.requestedSlotCode}</strong>.
              Reallocated to <strong>Slot {space.code}</strong> under
              deterministic ranking rules (same-zone compatible capacity).
            </p>
          </div>
        )}

        {/* Space Meta Details */}
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(2, 1fr)",
            gap: "0.75rem",
            background: "var(--bg)",
            padding: "1rem",
            borderRadius: "0.75rem",
            border: "1px solid var(--border)",
            marginBottom: "1.25rem",
          }}
        >
          <div>
            <span
              style={{
                fontSize: "0.72rem",
                color: "var(--muted)",
                display: "block",
              }}
            >
              Vehicle Category
            </span>
            <strong
              style={{
                fontSize: "0.88rem",
                display: "flex",
                alignItems: "center",
                gap: "0.35rem",
                marginTop: "0.15rem",
              }}
            >
              <UntitledIcon
                name={space.vehicleType === "car" ? "car" : "motorcycle"}
                size={15}
              />
              {space.vehicleType === "car"
                ? "Car (Ô tô)"
                : "Motorcycle (Xe máy)"}
            </strong>
          </div>

          <div>
            <span
              style={{
                fontSize: "0.72rem",
                color: "var(--muted)",
                display: "block",
              }}
            >
              Physical State
            </span>
            <span
              style={{
                fontSize: "0.85rem",
                fontWeight: 600,
                color: pBadge.color,
                display: "block",
                marginTop: "0.15rem",
              }}
            >
              {pBadge.label}
            </span>
          </div>

          <div>
            <span
              style={{
                fontSize: "0.72rem",
                color: "var(--muted)",
                display: "block",
              }}
            >
              Reservation State
            </span>
            <span
              style={{
                fontSize: "0.85rem",
                fontWeight: 600,
                color: rBadge.color,
                display: "block",
                marginTop: "0.15rem",
              }}
            >
              {rBadge.label}
            </span>
          </div>

          <div>
            <span
              style={{
                fontSize: "0.72rem",
                color: "var(--muted)",
                display: "block",
              }}
            >
              Floor Level
            </span>
            <strong
              style={{
                fontSize: "0.88rem",
                marginTop: "0.15rem",
                display: "block",
              }}
            >
              {floorName} ({floorCode})
            </strong>
          </div>

          <div>
            <span
              style={{
                fontSize: "0.72rem",
                color: "var(--muted)",
                display: "block",
              }}
            >
              Space UID
            </span>
            <code
              style={{
                fontSize: "0.75rem",
                color: "var(--muted)",
                wordBreak: "break-all",
              }}
            >
              {space.id}
            </code>
          </div>

          <div>
            <span
              style={{
                fontSize: "0.72rem",
                color: "var(--muted)",
                display: "block",
              }}
            >
              Observation Time
            </span>
            <span style={{ fontSize: "0.78rem", color: "var(--fg)" }}>
              {new Date(space.lastUpdated).toLocaleTimeString([], {
                hour: "2-digit",
                minute: "2-digit",
              })}{" "}
              ({new Date(space.lastUpdated).toLocaleDateString()})
            </span>
          </div>
        </div>

        {/* Associated Booking Information */}
        {space.currentBooking && (
          <div
            style={{
              padding: "1rem",
              borderRadius: "0.75rem",
              background: "var(--bg)",
              border: "1px solid var(--border)",
              marginBottom: "1.25rem",
            }}
          >
            <div
              style={{
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
                marginBottom: "0.6rem",
              }}
            >
              <div
                style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}
              >
                <UntitledIcon name="clipboard" size={15} />
                <strong style={{ fontSize: "0.85rem", color: "var(--fg)" }}>
                  Associated Booking & Driver
                </strong>
              </div>
              <span
                style={{
                  fontSize: "0.72rem",
                  fontFamily: "monospace",
                  background: "var(--card)",
                  padding: "0.15rem 0.45rem",
                  borderRadius: "4px",
                  border: "1px solid var(--border)",
                  fontWeight: 700,
                }}
              >
                {space.currentBooking.bookingId}
              </span>
            </div>

            <div
              style={{
                display: "grid",
                gridTemplateColumns: "repeat(2, 1fr)",
                gap: "0.5rem",
                fontSize: "0.8rem",
              }}
            >
              <div>
                <span style={{ color: "var(--muted)" }}>Driver:</span>{" "}
                <strong>{space.currentBooking.driverName}</strong>
              </div>
              <div>
                <span style={{ color: "var(--muted)" }}>Vehicle Plate:</span>{" "}
                <span
                  style={{
                    fontFamily: "monospace",
                    fontWeight: 700,
                    background: "var(--card)",
                    padding: "0.1rem 0.35rem",
                    borderRadius: "4px",
                    border: "1px solid var(--border)",
                  }}
                >
                  {space.currentBooking.licensePlate}
                </span>
              </div>
              <div>
                <span style={{ color: "var(--muted)" }}>Start Time:</span>{" "}
                <span>
                  {new Date(space.currentBooking.startTime).toLocaleTimeString(
                    [],
                    { hour: "2-digit", minute: "2-digit" },
                  )}
                </span>
              </div>
              <div>
                <span style={{ color: "var(--muted)" }}>End Time:</span>{" "}
                <span>
                  {new Date(space.currentBooking.endTime).toLocaleTimeString(
                    [],
                    { hour: "2-digit", minute: "2-digit" },
                  )}
                </span>
              </div>
              {space.protectionStartTime && (
                <div style={{ gridColumn: "span 2" }}>
                  <span style={{ color: "var(--muted)" }}>
                    Protection Began:
                  </span>{" "}
                  <span style={{ color: "#6366f1", fontWeight: 600 }}>
                    {new Date(space.protectionStartTime).toLocaleTimeString(
                      [],
                      { hour: "2-digit", minute: "2-digit" },
                    )}
                  </span>
                </div>
              )}
            </div>
          </div>
        )}

        {/* Maintenance Note info */}
        {physical === "MAINTENANCE" && (
          <div
            style={{
              padding: "1rem",
              borderRadius: "0.75rem",
              background: "#f9731610",
              border: "1px solid #f9731630",
              marginBottom: "1.25rem",
            }}
          >
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: "0.5rem",
                marginBottom: "0.35rem",
              }}
            >
              <span style={{ color: "#ea580c" }}>
                <UntitledIcon name="wrench" size={16} />
              </span>
              <strong style={{ fontSize: "0.85rem", color: "#ea580c" }}>
                Maintenance Work In Progress
              </strong>
            </div>
            <p
              style={{
                margin: 0,
                fontSize: "0.8rem",
                color: "var(--fg)",
                lineHeight: 1.5,
              }}
            >
              {space.maintenanceNote ||
                "Physical sensor recalibration or slot repainting."}
            </p>
          </div>
        )}

        {/* Status Actions */}
        <div style={{ marginTop: "0.5rem" }}>
          <span
            style={{
              display: "block",
              fontSize: "0.78rem",
              fontWeight: 600,
              color: "var(--muted)",
              marginBottom: "0.6rem",
            }}
          >
            Authorized Operational Actions:
          </span>

          {showMaintInput ? (
            <div
              style={{
                display: "grid",
                gap: "0.5rem",
                padding: "0.75rem",
                background: "var(--bg)",
                borderRadius: "0.5rem",
                border: "1px solid var(--border)",
                marginBottom: "0.75rem",
              }}
            >
              <label style={{ fontSize: "0.78rem", fontWeight: 600 }}>
                Reason / Note for Maintenance Work:
              </label>
              <input
                className="input"
                placeholder="e.g. Ultrasonic sensor fault, camera blocked, line repaint..."
                value={maintenanceNote}
                onChange={(e) => setMaintenanceNote(e.target.value)}
                autoFocus
              />
              <div
                style={{
                  display: "flex",
                  justifyContent: "flex-end",
                  gap: "0.5rem",
                }}
              >
                <button
                  type="button"
                  className="btn-outline"
                  style={{ fontSize: "0.78rem", padding: "0.35rem 0.75rem" }}
                  onClick={() => setShowMaintInput(false)}
                >
                  Cancel
                </button>
                <button
                  type="button"
                  className="btn-primary"
                  style={{
                    fontSize: "0.78rem",
                    padding: "0.35rem 0.75rem",
                    background: "#ea580c",
                  }}
                  onClick={() => {
                    onUpdateStatus(
                      space.id,
                      "maintenance",
                      maintenanceNote || "Under scheduled maintenance",
                      {
                        physicalState: "MAINTENANCE",
                      },
                    )
                    setShowMaintInput(false)
                    onClose()
                  }}
                >
                  Confirm Maintenance
                </button>
              </div>
            </div>
          ) : (
            <div style={{ display: "flex", flexWrap: "wrap", gap: "0.5rem" }}>
              {/* Mark Available */}
              <button
                type="button"
                className="btn-outline"
                style={{
                  fontSize: "0.78rem",
                  padding: "0.35rem 0.75rem",
                  color: "#16a34a",
                }}
                onClick={() => {
                  onUpdateStatus(space.id, "available", undefined, {
                    physicalState: "AVAILABLE",
                    reservationState: "NONE",
                    isProtected: false,
                    isBackup: false,
                  })
                  onClose()
                }}
              >
                <UntitledIcon name="check-circle" size={13} /> Mark Vacant
              </button>

              {/* Set Maintenance */}
              <button
                type="button"
                className="btn-outline"
                style={{
                  fontSize: "0.78rem",
                  padding: "0.35rem 0.75rem",
                  color: "#ea580c",
                }}
                onClick={() => setShowMaintInput(true)}
              >
                <UntitledIcon name="wrench" size={13} /> Set Maintenance
              </button>

              {/* Designate Backup */}
              <button
                type="button"
                className="btn-outline"
                style={{
                  fontSize: "0.78rem",
                  padding: "0.35rem 0.75rem",
                  color: "#7c3aed",
                }}
                onClick={() => {
                  onUpdateStatus(space.id, space.status, undefined, {
                    reservationState: space.isBackup ? "NONE" : "BACKUP",
                    isBackup: !space.isBackup,
                  })
                  onClose()
                }}
              >
                <UntitledIcon name="shield" size={13} />{" "}
                {space.isBackup ? "Release Backup" : "Designate Backup"}
              </button>

              {/* Toggle Simulated Protected */}
              <button
                type="button"
                className="btn-outline"
                style={{
                  fontSize: "0.78rem",
                  padding: "0.35rem 0.75rem",
                  color: "#6366f1",
                }}
                onClick={() => {
                  onUpdateStatus(
                    space.id,
                    space.status === "available" ? "reserved" : space.status,
                    undefined,
                    {
                      reservationState: space.isProtected
                        ? "NONE"
                        : "PROTECTED",
                      isProtected: !space.isProtected,
                    },
                  )
                  onClose()
                }}
              >
                <UntitledIcon name="shield" size={13} />{" "}
                {space.isProtected ? "Clear Protection" : "Set Protected"}
              </button>

              {/* Disable Slot */}
              {space.status !== "disabled" && (
                <button
                  type="button"
                  className="btn-outline"
                  style={{ fontSize: "0.78rem", padding: "0.35rem 0.75rem" }}
                  onClick={() => {
                    onUpdateStatus(space.id, "disabled", undefined, {
                      physicalState: "UNAVAILABLE",
                    })
                    onClose()
                  }}
                >
                  <UntitledIcon name="x" size={13} /> Disable Slot
                </button>
              )}
            </div>
          )}
        </div>

        <div
          style={{
            display: "flex",
            justifyContent: "flex-end",
            marginTop: "1.25rem",
            borderTop: "1px solid var(--border)",
            paddingTop: "1rem",
          }}
        >
          <button
            type="button"
            className="btn-outline"
            onClick={onClose}
            style={{ fontSize: "0.82rem" }}
          >
            Close Details
          </button>
        </div>
      </div>
    </div>
  )
}
