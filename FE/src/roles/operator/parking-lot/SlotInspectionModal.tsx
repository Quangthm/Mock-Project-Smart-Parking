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
            className="w-full max-w-2xl max-h-[90vh] overflow-y-auto rounded-2xl border border-[var(--border)] bg-[var(--card)] p-5 shadow-2xl animate-in text-sm text-[var(--fg)]"
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
                <span className="text-[var(--muted)]">AI plate result:</span>
                <span className="font-mono font-bold text-[var(--primary)]">
                  {inspectedSlot.demoPlate ?? "Not scanned"}
                </span>
              </div>

              <div className="flex justify-between items-center">
                <span className="text-[var(--muted)]">Location:</span>
                <span className="font-medium text-[var(--fg)]">
                  {isOutdoor ? "Ground Zone" : `Floor Level ${inspectedSlot.floor}`} · {lot.name}
                </span>
              </div>

            </div>

            {(inspectedSlot.demoSession || inspectedSlot.demoImageUrl) && (
              <section className="mt-4">
                <div className="mb-2 flex items-center justify-between">
                  <div>
                    <span className="text-[10px] font-bold uppercase tracking-wider text-[var(--primary)]">
                      Parking Session
                    </span>
                    <h4 className="text-sm font-bold text-[var(--fg)]">
                      {inspectedSlot.demoSession ? "Session detail after AI assignment" : "Check-in image awaiting scan"}
                    </h4>
                  </div>
                  {inspectedSlot.demoSession && (
                    <span className="rounded px-2 py-1 text-[10px] font-bold uppercase text-green-500 bg-green-500/10">
                      {inspectedSlot.demoSession.status}
                    </span>
                  )}
                </div>
                <div className="grid gap-3 md:grid-cols-2">
                  {inspectedSlot.demoSession ? (
                    <div className="grid grid-cols-2 gap-x-4 gap-y-3 rounded-xl border border-[var(--border)] bg-[var(--bg)] p-4 text-xs">
                      <SessionField label="Session ID" value={inspectedSlot.demoSession.id} mono />
                      <SessionField label="Ticket Reference" value={inspectedSlot.demoSession.ticketReference} mono />
                      <SessionField label="Original Plate" value={inspectedSlot.demoSession.originalPlate} mono />
                      <SessionField label="Normalized Plate" value={inspectedSlot.demoSession.normalizedPlate} mono />
                      <SessionField label="Vehicle Type" value={inspectedSlot.demoSession.vehicleType} />
                      <SessionField label="Entry Time" value={formatDate(inspectedSlot.demoSession.entryTime)} />
                      <SessionField label="Exit Time" value={inspectedSlot.demoSession.exitTime ? formatDate(inspectedSlot.demoSession.exitTime) : "Still inside"} />
                      <SessionField label="Parking Slot" value={inspectedSlot.number} mono />
                    </div>
                  ) : (
                    <div className="rounded-xl border border-[var(--border)] bg-[var(--bg)] p-4 text-xs text-[var(--muted)]">
                      Plate recognition has not run yet. Scan this slot to confirm the vehicle and mark it occupied.
                    </div>
                  )}
                  <div className="overflow-hidden rounded-xl border border-[var(--border)] bg-[var(--bg)]">
                    {inspectedSlot.demoSession?.entryImageUrl || inspectedSlot.demoImageUrl ? (
                      <img
                        src={inspectedSlot.demoSession?.entryImageUrl ?? inspectedSlot.demoImageUrl}
                        alt={`Check-in scan for slot ${inspectedSlot.number}`}
                        className="h-full min-h-48 w-full object-cover"
                      />
                    ) : (
                      <div className="flex min-h-48 items-center justify-center p-4 text-xs text-[var(--muted)]">
                        No check-in image saved for this session.
                      </div>
                    )}
                    <p className="border-t border-[var(--border)] px-3 py-2 text-[10px] text-[var(--muted)]">
                      Check-in scan / demo camera image
                    </p>
                  </div>
                </div>
              </section>
            )}

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

function formatDate(value: string) {
  return new Date(value).toLocaleString()
}

function SessionField({ label, value, mono = false }: {
  label: string
  value: string
  mono?: boolean
}) {
  return (
    <div className="min-w-0">
      <span className="block text-[10px] text-[var(--muted)]">{label}</span>
      <strong className={`block truncate text-[11px] text-[var(--fg)] ${mono ? "font-mono" : ""}`}>
        {value}
      </strong>
    </div>
  )
}
