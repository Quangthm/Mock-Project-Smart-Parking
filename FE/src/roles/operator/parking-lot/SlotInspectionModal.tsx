import type { OperatorSite, OperatorSlot } from "../../../lib/operatorApi"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"

export function SlotInspectionModal({ lot, inspectedSlot, isOutdoor, onClose }: {
  lot: OperatorSite
  inspectedSlot: OperatorSlot | null
  isOutdoor: boolean
  onClose: () => void
}) {
  return (
    <>
{inspectedSlot && (
        <div
          role="presentation"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
          onMouseDown={(e) => e.target === e.currentTarget && onClose()}
        >
          <div
            role="dialog"
            aria-modal="true"
            aria-labelledby="slot-inspect-title"
            className="w-full max-w-lg rounded-2xl border border-[var(--border)] bg-[var(--card)] p-6 shadow-2xl animate-in text-sm text-[var(--fg)]"
          >
            <div className="flex items-center justify-between border-b border-[var(--border)] pb-3">
              <div>
                <span className="text-[10px] font-bold uppercase tracking-wider text-[var(--primary)]">
                  Space Telemetry
                </span>
                <h3 id="slot-inspect-title" className="text-xl font-bold font-mono text-[var(--fg)]">
                  Slot {inspectedSlot.number}
                </h3>
              </div>
              <button
                type="button"
                onClick={() => onClose()}
                className="h-8 w-8 rounded-full border border-[var(--border)] bg-[var(--bg)] flex items-center justify-center text-[var(--muted)] hover:text-[var(--fg)]"
              >
                <UntitledIcon name="x" size={16} />
              </button>
            </div>

            {/* Status Dimensions Table */}
            <div className="mt-4 rounded-xl border border-[var(--border)] bg-[var(--bg)] p-4 space-y-2.5 text-xs">
              <div className="flex justify-between items-center">
                <span className="text-[var(--muted)]">Physical Occupancy:</span>
                <span
                  className={`font-bold px-2 py-0.5 rounded text-[11px] uppercase ${
                    inspectedSlot.status === "occupied"
                      ? "bg-red-500/15 text-red-600"
                      : (inspectedSlot.status as string) === "maintenance"
                        ? "bg-orange-500/15 text-orange-600"
                        : "bg-green-500/15 text-green-600"
                  }`}
                >
                  {inspectedSlot.status === "occupied" ? "OCCUPIED" : (inspectedSlot.status as string) === "maintenance" ? "UNDER MAINTENANCE" : "VACANT"}
                </span>
              </div>

              <div className="flex justify-between items-center">
                <span className="text-[var(--muted)]">Reservation / Hold:</span>
                <span className="font-semibold text-[var(--fg)]">
                  {inspectedSlot.status === "reserved" ? "Active Reservation Hold" : "None"}
                </span>
              </div>

              <div className="flex justify-between items-center">
                <span className="text-[var(--muted)]">Vehicle Category:</span>
                <span className="font-medium text-[var(--fg)]">
                  {inspectedSlot.vehicleType}
                </span>
              </div>

              <div className="flex justify-between items-center">
                <span className="text-[var(--muted)]">Location:</span>
                <span className="font-medium text-[var(--fg)]">
                  {isOutdoor ? "Ground Zone" : `Floor Level ${inspectedSlot.floor}`} · {lot.name}
                </span>
              </div>
            </div>

            <p className="mt-4 rounded-lg border border-[var(--border)] bg-[var(--bg)] p-3 text-xs text-[var(--muted)]">
              This view is read-only. Physical occupancy and maintenance state must be changed through their authoritative service.
            </p>

            <div className="mt-6 flex justify-end border-t border-[var(--border)] pt-3">
              <button
                type="button"
                className="btn-outline text-xs py-1.5 px-3"
                onClick={() => onClose()}
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}
