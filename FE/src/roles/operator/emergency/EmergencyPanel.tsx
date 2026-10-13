import { useState } from "react"
import { operatorData as store } from "../data/data"
import { useApp } from "../../../context/AppContext"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"

export function EmergencyPanel() {
  const { user } = useApp()
  type IncidentStatus = "open" | "resolved"

  const [incidents, setIncidents] = useState<{
    id: number
    type: string
    plate: string
    slot: string
    time: string
    notes?: string
    status: IncidentStatus
  }[]>([
    {
      id: 1,
      type: "wrong-spot",
      plate: "51A-99999",
      slot: "A-005",
      time: "14:23",
      notes: "Driver parked in wrong numbered slot; reported by lot patrol.",
      status: "open",
    },
    {
      id: 2,
      type: "overtime",
      plate: "51AB-12345",
      slot: "B-012",
      time: "13:45",
      notes: "Session expired over 45 minutes; barrier egress alert triggered.",
      status: "open",
    },
  ])

  const [form, setForm] = useState({
    plate: "",
    slot: "",
    type: "wrong-spot",
    notes: "",
  })

  const [feedback, setFeedback] = useState("")

  function resolve(id: number) {
    setIncidents((inc) =>
      inc.map((i) => (i.id === id ? { ...i, status: "resolved" as const } : i)),
    )

    store.addAuditLog({
      userId: user?.id ?? "op",
      userName: user?.name ?? "Operator",
      userRole: "operator",
      action: "INCIDENT_RESOLVED",
      details: `Emergency incident #${id} marked as resolved`,
    })

    setFeedback(`Incident #${id} resolved successfully.`)
  }

  function reportIncident(e: React.FormEvent) {
    e.preventDefault()
    if (!form.plate.trim() || !form.slot.trim()) return

    const newId = Date.now()
    const nowTime = new Date().toLocaleTimeString("en-GB", {
      hour: "2-digit",
      minute: "2-digit",
    })

    setIncidents((inc) => [
      {
        id: newId,
        type: form.type,
        plate: form.plate.trim().toUpperCase(),
        slot: form.slot.trim().toUpperCase(),
        time: nowTime,
        notes: form.notes,
        status: "open",
      },
      ...inc,
    ])

    store.addAuditLog({
      userId: user?.id ?? "op",
      userName: user?.name ?? "Operator",
      userRole: "operator",
      action: "INCIDENT_REPORTED",
      details: `Reported emergency incident (${form.type}) for plate ${form.plate} at slot ${form.slot}`,
    })

    setFeedback(`New incident reported for plate ${form.plate.toUpperCase()}.`)
    setForm({ plate: "", slot: "", type: "wrong-spot", notes: "" })
  }

  const openIncidents = incidents.filter((i) => i.status === "open")
  const resolvedIncidents = incidents.filter((i) => i.status === "resolved")

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight text-[var(--fg)]">
          Emergency & Incident Dispatch
        </h1>
        <p className="mt-1 text-sm text-[var(--muted)]">
          Log facility discrepancies, overstay warnings, incorrect parking positions, and vehicle damage.
        </p>
      </div>

      {feedback && (
        <div className="flex items-center justify-between rounded-xl border border-green-500/30 bg-green-500/10 p-3.5 text-xs text-green-600 font-medium">
          <div className="flex items-center gap-2">
            <UntitledIcon name="check-circle" size={16} />
            <span>{feedback}</span>
          </div>
          <button type="button" onClick={() => setFeedback("")} className="hover:underline">
            Dismiss
          </button>
        </div>
      )}

      <div className="grid gap-6 grid-cols-1 lg:grid-cols-2">
        {/* Left: Active Incidents List */}
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <h2 className="text-base font-bold text-[var(--fg)]">
              Active Security Alerts ({openIncidents.length})
            </h2>
            <span className="text-xs text-[var(--muted)]">
              {resolvedIncidents.length} resolved today
            </span>
          </div>

          <div className="space-y-3">
            {openIncidents.map((inc) => (
              <div
                key={inc.id}
                className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-4 space-y-2.5 transition"
              >
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <span
                      className={`h-2.5 w-2.5 rounded-full ${
                        inc.type === "wrong-spot" ? "bg-red-500" : inc.type === "damage" ? "bg-orange-500" : "bg-amber-500"
                      }`}
                    />
                    <span
                      className={`text-xs font-bold uppercase ${
                        inc.type === "wrong-spot" ? "text-red-600" : inc.type === "damage" ? "text-orange-600" : "text-amber-600"
                      }`}
                    >
                      {inc.type === "wrong-spot" ? "Wrong Spot Alert" : inc.type === "damage" ? "Vehicle Damage" : "Overtime Stay"}
                    </span>
                  </div>
                  <span className="text-xs text-[var(--muted)]">{inc.time}</span>
                </div>

                <div className="flex items-center gap-3 text-xs">
                  <div>
                    <span className="text-[var(--muted)] block">Plate:</span>
                    <strong className="font-mono text-sm text-[var(--fg)]">{inc.plate}</strong>
                  </div>
                  <div>
                    <span className="text-[var(--muted)] block">Slot:</span>
                    <strong className="font-mono text-sm text-[var(--primary)]">{inc.slot}</strong>
                  </div>
                </div>

                {inc.notes && (
                  <p className="text-xs text-[var(--muted)] bg-[var(--bg)] p-2 rounded border border-[var(--border)]">
                    {inc.notes}
                  </p>
                )}

                <div className="flex justify-end pt-2">
                  <button
                    type="button"
                    className="btn-outline text-xs py-1 px-3 text-green-600 border-green-500/30 hover:bg-green-500/10"
                    onClick={() => resolve(inc.id)}
                  >
                    <UntitledIcon name="check" size={13} /> Mark Resolved
                  </button>
                </div>
              </div>
            ))}

            {openIncidents.length === 0 && (
              <div className="rounded-xl border border-dashed border-[var(--border)] p-8 text-center text-xs text-[var(--muted)]">
                <UntitledIcon name="check-circle" size={24} />
                <p className="mt-2 font-medium">No open incidents or security alarms.</p>
                <p className="text-[11px]">All vehicle lots are operating within standard parameters.</p>
              </div>
            )}
          </div>
        </div>

        {/* Right: Report Incident Form */}
        <div>
          <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5">
            <h2 className="text-base font-bold text-[var(--fg)] mb-1">
              File Incident Report
            </h2>
            <p className="text-xs text-[var(--muted)] mb-4">
              Dispatch security or flag violation for cashier reconciliation
            </p>

            <form onSubmit={reportIncident} className="space-y-4">
              <div>
                <label className="block text-xs font-semibold text-[var(--fg)] mb-1">Incident Classification</label>
                <select
                  className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-xs text-[var(--fg)] outline-none focus:border-[var(--primary)]"
                  value={form.type}
                  onChange={(e) => setForm((f) => ({ ...f, type: e.target.value }))}
                >
                  <option value="wrong-spot">Wrong Slot (Red Alert)</option>
                  <option value="overtime">Overstay / Expired Session (Amber Alert)</option>
                  <option value="damage">Vehicle Damage / Hazard</option>
                  <option value="other">Other Operational Incident</option>
                </select>
              </div>

              <div>
                <label className="block text-xs font-semibold text-[var(--fg)] mb-1">License Plate Number</label>
                <input
                  className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-xs text-[var(--fg)] outline-none focus:border-[var(--primary)] font-mono uppercase"
                  value={form.plate}
                  onChange={(e) => setForm((f) => ({ ...f, plate: e.target.value }))}
                  placeholder="e.g. 51A-999.99"
                  required
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-[var(--fg)] mb-1">Observed Slot Number</label>
                <input
                  className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-xs text-[var(--fg)] outline-none focus:border-[var(--primary)] font-mono uppercase"
                  value={form.slot}
                  onChange={(e) => setForm((f) => ({ ...f, slot: e.target.value }))}
                  placeholder="e.g. A-105"
                  required
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-[var(--fg)] mb-1">Incident Notes & Evidence</label>
                <textarea
                  className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-xs text-[var(--fg)] outline-none focus:border-[var(--primary)]"
                  rows={3}
                  value={form.notes}
                  onChange={(e) => setForm((f) => ({ ...f, notes: e.target.value }))}
                  placeholder="Describe location details, physical damage or driver interaction..."
                />
              </div>

              <button type="submit" className="btn-primary text-xs py-2 px-4 w-full justify-center">
                <UntitledIcon name="alert" size={14} /> Submit Incident Dispatch
              </button>
            </form>
          </div>
        </div>
      </div>
    </div>
  )
}
