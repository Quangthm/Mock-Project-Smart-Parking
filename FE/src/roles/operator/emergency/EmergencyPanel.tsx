import { useEffect, useState } from "react"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { operatorOperationsApi, type OperatorIncident } from "../../../lib/operatorApi"

export function EmergencyPanel({ siteId }: { siteId: string }) {
  const [incidents, setIncidents] = useState<OperatorIncident[]>([])
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState("")

  const [form, setForm] = useState({
    plate: "",
    slot: "",
    type: "wrong-spot",
    notes: "",
  })

  const [feedback, setFeedback] = useState("")

  useEffect(() => {
    if (!siteId) {
      setIncidents([])
      setError("No assigned parking site is available.")
      setLoading(false)
      return
    }
    let active = true
    setLoading(true)
    setError("")
    operatorOperationsApi.listIncidents(siteId)
      .then((items) => {
        if (active) setIncidents(items)
      })
      .catch((reason: unknown) => {
        if (active) setError(reason instanceof Error ? reason.message : "Could not load incident reports.")
      })
      .finally(() => {
        if (active) setLoading(false)
      })
    return () => {
      active = false
    }
  }, [siteId])

  async function resolve(incident: OperatorIncident) {
    if (!siteId || busy) return
    setBusy(true)
    setError("")
    try {
      const resolved = await operatorOperationsApi.resolveIncident(siteId, incident.id)
      setIncidents((items) => items.map((item) => item.id === resolved.id ? resolved : item))
      setFeedback(`Incident #${incident.id} resolved.`)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not resolve this incident.")
    } finally {
      setBusy(false)
    }
  }

  async function reportIncident(e: React.FormEvent) {
    e.preventDefault()
    if (!siteId || !form.plate.trim() || !form.slot.trim() || busy) return
    setBusy(true)
    setError("")
    try {
      const details = [
        `Plate: ${form.plate.trim().toUpperCase()}`,
        `Slot: ${form.slot.trim().toUpperCase()}`,
        form.notes.trim() ? `Notes: ${form.notes.trim()}` : "",
      ].filter(Boolean).join("\n")
      const incident = await operatorOperationsApi.createIncident(siteId, { type: form.type, details })
      setIncidents((items) => [incident, ...items])
      setFeedback("Incident report submitted.")
      setForm({ plate: "", slot: "", type: "wrong-spot", notes: "" })
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not submit the incident report.")
    } finally {
      setBusy(false)
    }
  }

  const openIncidents = incidents.filter((i) => i.status === "reported")
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

      {error && (
        <div role="alert" className="rounded-xl border border-red-500/30 bg-red-500/10 p-3.5 text-xs text-red-600">
          {error}
        </div>
      )}

      {loading && (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-8 text-center text-xs text-[var(--muted)]">
          Loading facility incidents…
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
              {resolvedIncidents.length} resolved
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
                  <span className="text-xs text-[var(--muted)]">{new Date(inc.createdAt).toLocaleString()}</span>
                </div>

                <p className="whitespace-pre-wrap text-xs text-[var(--muted)] bg-[var(--bg)] p-2 rounded border border-[var(--border)]">
                  {inc.details}
                </p>

                <div className="flex justify-end pt-2">
                  <button
                    type="button"
                    className="btn-outline text-xs py-1 px-3 text-green-600 border-green-500/30 hover:bg-green-500/10"
                    disabled={busy}
                    onClick={() => void resolve(inc)}
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
                <p className="text-[11px]">No incidents have been reported for this facility.</p>
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
                  disabled={!siteId || busy}
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
                  disabled={!siteId || busy}
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
                  disabled={!siteId || busy}
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
                  disabled={!siteId || busy}
                />
              </div>

              <button type="submit" className="btn-primary text-xs py-2 px-4 w-full justify-center" disabled={!siteId || busy}>
                <UntitledIcon name="alert" size={14} /> {busy ? "Submitting…" : "Submit Incident Dispatch"}
              </button>
            </form>
          </div>
        </div>
      </div>
    </div>
  )
}
