import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { useEffect, useMemo, useRef, useState } from "react"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { PageHeader } from "../../../components/ui/PageHeader"
import {
  operatorOperationsApi,
  type EmergencyRestriction,
  type EmergencyRestrictionMode,
  type EmergencyRestrictionScope,
  type OperatorIncident,
  type OperatorSite,
} from "../../../lib/operatorApi"
import {
  canManageEmergencyRestrictions,
  extractEmergencyScopeTargets,
  formatEmergencyRestrictionScope,
  validateEmergencyRestrictionDraft,
} from "../../../lib/emergencyRestrictionRules"
import { showUiToast } from "../../../lib/uiToast"

const incidentLabels: Record<string, string> = {
  "wrong-spot": "Wrong spot",
  overtime: "Overstay",
  damage: "Vehicle damage",
  other: "Other incident",
}

const modeLabels: Record<EmergencyRestrictionMode, string> = {
  EMERGENCY: "Emergency",
  FORCED_MAINTENANCE: "Forced maintenance",
}

export function EmergencyPanel({
  siteId,
  lot = null,
  onRestrictionsChanged,
}: {
  siteId: string
  lot?: OperatorSite | null
  onRestrictionsChanged?: () => void
}) {
  const [incidents, setIncidents] = useState<OperatorIncident[]>([])
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState("")
  const [incidentView, setIncidentView] = useState<"reported" | "resolved">("reported")

  const [form, setForm] = useState({
    plate: "",
    slot: "",
    type: "wrong-spot",
    notes: "",
  })

  const [feedback, setFeedback] = useState("")

  const [restrictions, setRestrictions] = useState<EmergencyRestriction[]>([])
  const [restrictionsLoading, setRestrictionsLoading] = useState(true)
  const [restrictionsError, setRestrictionsError] = useState("")
  const [restrictionFeedback, setRestrictionFeedback] = useState("")
  const [restrictionBusyId, setRestrictionBusyId] = useState("")
  const [siteClosurePending, setSiteClosurePending] = useState(false)
  const [reload, setReload] = useState(0)
  const [restrictionDraft, setRestrictionDraft] = useState({
    mode: "EMERGENCY" as EmergencyRestrictionMode,
    scope: "SITE" as EmergencyRestrictionScope,
    unitId: "",
    slotId: "",
    reason: "",
    effectiveUntilLocal: "",
  })

  // Track the previous site so a manual Refresh or the expiry timer (reload) never clears an
  // in-progress draft — only an actual site switch does.
  const prevSiteIdRef = useRef(siteId)

  useEffect(() => {
    // Switching sites must never leak a half-filled draft, a pending whole-site confirmation or a
    // stale success banner into the next site's panel.
    if (prevSiteIdRef.current !== siteId) {
      prevSiteIdRef.current = siteId
      resetRestrictionDraft()
      setRestrictionFeedback("")
    }
    if (!siteId) {
      setIncidents([])
      setRestrictions([])
      setError("No assigned parking site is available.")
      setRestrictionsError("No assigned parking site is available.")
      setLoading(false)
      setRestrictionsLoading(false)
      return
    }
    let active = true
    setLoading(true)
    setRestrictionsLoading(true)
    setError("")
    setRestrictionsError("")
    void operatorOperationsApi
      .listIncidents(siteId)
      .then((items) => {
        if (active) setIncidents(items)
      })
      .catch((reason: unknown) => {
        if (active)
          setError(reason instanceof Error ? reason.message : "Could not load incident reports.")
      })
      .finally(() => {
        if (active) setLoading(false)
      })
    // Restrictions are loaded independently so a failure in one queue never hides the other.
    void operatorOperationsApi
      .listEmergencyRestrictions(siteId)
      .then((items) => {
        if (active) setRestrictions(items)
      })
      .catch((reason: unknown) => {
        if (active)
          setRestrictionsError(
            reason instanceof Error ? reason.message : "Could not load emergency restrictions.",
          )
      })
      .finally(() => {
        if (active) setRestrictionsLoading(false)
      })
    return () => {
      active = false
    }
  }, [siteId, reload])

  async function resolve(incident: OperatorIncident) {
    if (!siteId || busy) return
    setBusy(true)
    setError("")
    try {
      const resolved = await operatorOperationsApi.resolveIncident(siteId, incident.id)
      setIncidents((items) => items.map((item) => (item.id === resolved.id ? resolved : item)))
      setFeedback(`Incident #${incident.id} resolved.`)
      showUiToast("Incident resolved", `Incident #${incident.id} was resolved.`)
    } catch (reason) {
      const message = reason instanceof Error ? reason.message : "Could not resolve this incident."
      setError(message)
      showUiToast("Could not resolve incident", message, "error")
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
      ]
        .filter(Boolean)
        .join("\n")
      const incident = await operatorOperationsApi.createIncident(siteId, {
        type: form.type,
        details,
      })
      setIncidents((items) => [incident, ...items])
      setFeedback("Incident report submitted.")
      showUiToast("Incident reported", "Incident report submitted.")
      setForm({ plate: "", slot: "", type: "wrong-spot", notes: "" })
    } catch (reason) {
      const message =
        reason instanceof Error ? reason.message : "Could not submit the incident report."
      setError(message)
      showUiToast("Report not submitted", message, "error")
    } finally {
      setBusy(false)
    }
  }

  const targets = useMemo(() => extractEmergencyScopeTargets(lot), [lot])
  const canManage = useMemo(() => canManageEmergencyRestrictions(lot), [lot])

  function resetRestrictionDraft() {
    setRestrictionDraft({
      mode: "EMERGENCY",
      scope: "SITE",
      unitId: "",
      slotId: "",
      reason: "",
      effectiveUntilLocal: "",
    })
    setSiteClosurePending(false)
  }

  async function submitRestriction(e: React.FormEvent) {
    e.preventDefault()
    if (!siteId || !canManage || busy || restrictionBusyId) return

    const validation = validateEmergencyRestrictionDraft({ ...restrictionDraft, targets })
    if (!validation.ok) {
      setRestrictionsError(validation.error)
      showUiToast("Restriction not activated", validation.error, "error")
      return
    }

    // A whole-site closure is the most destructive action on this screen, so it always takes a
    // second explicit click before the request is sent.
    if (restrictionDraft.scope === "SITE" && !siteClosurePending) {
      setRestrictionsError("")
      setSiteClosurePending(true)
      return
    }

    setRestrictionsError("")
    setRestrictionBusyId("new")
    try {
      const created = await operatorOperationsApi.createEmergencyRestriction(
        siteId,
        validation.payload,
      )
      setRestrictions((items) => [created, ...items.filter((item) => item.id !== created.id)])
      const summary = `${created.affectedSlotIds.length} bay(s) withdrawn from sale.`
      setRestrictionFeedback(`${modeLabels[created.mode]} restriction activated — ${summary}`)
      showUiToast("Restriction activated", summary)
      resetRestrictionDraft()
      onRestrictionsChanged?.()
    } catch (reason) {
      const message =
        reason instanceof Error ? reason.message : "Could not activate this restriction."
      setRestrictionsError(message)
      showUiToast("Restriction not activated", message, "error")
    } finally {
      setRestrictionBusyId("")
    }
  }

  async function releaseRestriction(restriction: EmergencyRestriction) {
    if (!siteId || !canManage || busy || restrictionBusyId) return
    setRestrictionBusyId(restriction.id)
    setRestrictionsError("")
    try {
      const released = await operatorOperationsApi.releaseEmergencyRestriction(
        siteId,
        restriction.id,
      )
      setRestrictions((items) => items.filter((item) => item.id !== restriction.id))
      const reopened = released.affectedSlotIds.length || restriction.affectedSlotIds.length
      setRestrictionFeedback(`Restriction released — ${reopened} bay(s) open for bookings again.`)
      showUiToast("Restriction released", "Affected bays are open for bookings again.")
      onRestrictionsChanged?.()
    } catch (reason) {
      const message = reason instanceof Error ? reason.message : "Could not release this restriction."
      setRestrictionsError(message)
      showUiToast("Could not release restriction", message, "error")
    } finally {
      setRestrictionBusyId("")
    }
  }

  const openIncidents = incidents.filter((i) => i.status !== "resolved")
  const resolvedIncidents = incidents.filter((i) => i.status === "resolved")
  const visibleIncidents = incidentView === "reported" ? openIncidents : resolvedIncidents
  const activeRestrictions = restrictions.filter((item) => !item.releasedAt)

  const onRestrictionsChangedRef = useRef(onRestrictionsChanged)
  onRestrictionsChangedRef.current = onRestrictionsChanged

  // Exactly one single-shot timer at the earliest effective_until across the active restrictions —
  // never a polling interval. When that instant passes, the restriction list and the lot map both
  // refresh once so an auto-expired closure disappears without a manual click.
  useEffect(() => {
    const soonest = restrictions.reduce<number | null>((earliest, item) => {
      if (item.releasedAt || !item.effectiveUntil) return earliest
      const at = new Date(item.effectiveUntil).getTime()
      if (Number.isNaN(at)) return earliest
      return earliest === null || at < earliest ? at : earliest
    }, null)
    if (soonest === null) return
    const delayMs = Math.max(1000, soonest - Date.now() + 2500)
    const timer = window.setTimeout(() => {
      setReload((value) => value + 1)
      onRestrictionsChangedRef.current?.()
    }, Math.min(delayMs, 2_147_483_647))
    return () => window.clearTimeout(timer)
  }, [restrictions])

  return (
    <div className="space-y-6">
      <PageHeader title="Emergency & Incident Dispatch" description="Log facility discrepancies, overstay warnings, incorrect parking positions, and vehicle damage." />

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
        <div role="status" className="sr-only">
          Loading facility incidents...
        </div>
      )}

      <div className="grid gap-6 grid-cols-1 lg:grid-cols-2">
        {/* Left: Active Incidents List */}
        <div className="space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <h2 className="text-base font-bold text-[var(--fg)]">Incident queue</h2>
            <div className="flex flex-wrap gap-2" aria-label="Incident status">
              <button type="button" className={incidentView === "reported" ? "btn-primary" : "btn-outline"}
                aria-pressed={incidentView === "reported"} onClick={() => setIncidentView("reported")}>
                Open ({openIncidents.length})
              </button>
              <button type="button" className={incidentView === "resolved" ? "btn-primary" : "btn-outline"}
                aria-pressed={incidentView === "resolved"} onClick={() => setIncidentView("resolved")}>
                Resolved ({resolvedIncidents.length})
              </button>
            </div>
          </div>

          <div className="space-y-3">
            {visibleIncidents.map((inc) => (
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
                      {incidentLabels[inc.type] ?? inc.type}
                    </span>
                  </div>
                  <span className="text-xs text-[var(--muted)]">{new Date(inc.createdAt).toLocaleString()}</span>
                </div>

                <span className="text-xs font-semibold text-[var(--muted)]">
                  {inc.status === "resolved" ? "Resolved" : inc.status === "reported" ? "Reported" : "Status unavailable"}
                </span>

                <p className="whitespace-pre-wrap text-xs text-[var(--muted)] bg-[var(--bg)] p-2 rounded border border-[var(--border)]">
                  {inc.details}
                </p>

                {incidentView === "reported" && inc.status === "reported" && <div className="flex justify-end pt-2">
                  <button
                    type="button"
                    className="btn-outline text-xs py-1 px-3 text-green-600 border-green-500/30 hover:bg-green-500/10"
                    disabled={busy}
                    onClick={() => void resolve(inc)}
                  >
                    <UntitledIcon name="check" size={13} /> Mark Resolved
                  </button>
                </div>}
              </div>
            ))}

            {!loading && visibleIncidents.length === 0 && (
              <div className="rounded-xl border border-dashed border-[var(--border)] p-8 text-center text-xs text-[var(--muted)]">
                <UntitledIcon name="check-circle" size={24} />
                <p className="mt-2 font-medium">{incidentView === "reported" ? "No open incidents." : "No resolved incidents yet."}</p>
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
                <RoundedSelect
                  className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-xs text-[var(--fg)] outline-none focus:border-[var(--primary)]"
                  value={form.type}
                  onChange={(e) => setForm((f) => ({ ...f, type: e.target.value }))}
                  disabled={!siteId || busy}
                >
                  <option value="wrong-spot">Wrong Slot (Red Alert)</option>
                  <option value="overtime">Overstay / Expired Session (Amber Alert)</option>
                  <option value="damage">Vehicle Damage / Hazard</option>
                  <option value="other">Other Operational Incident</option>
                </RoundedSelect>
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
                <UntitledIcon name="alert" size={14} /> {busy ? "Submitting..." : "Submit Incident Dispatch"}
              </button>
            </form>
          </div>
        </div>
      </div>

      {/* Emergency Closure & Slot Restrictions */}
      <div className="space-y-4 border-t border-[var(--border)] pt-6">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h2 className="text-base font-bold text-[var(--fg)]">Emergency Closure & Slot Restrictions</h2>
            <p className="text-xs text-[var(--muted)]">
              Withdraw capacity immediately. Operations reconciles every affected future booking.
            </p>
          </div>
          <div className="flex items-center gap-2">
            <span className="rounded-full border border-[var(--border)] bg-[var(--card)] px-3 py-1 text-xs font-semibold text-[var(--fg)]">
              Active ({activeRestrictions.length})
            </span>
            <button
              type="button"
              className="btn-outline text-xs py-1 px-3"
              disabled={loading || restrictionsLoading || busy || restrictionBusyId !== ""}
              onClick={() => {
                setReload((value) => value + 1)
                onRestrictionsChanged?.()
              }}
            >
              <UntitledIcon name="activity" size={13} /> Refresh
            </button>
          </div>
        </div>

        <div className="rounded-xl border border-amber-500/30 bg-amber-500/10 p-4 space-y-2 text-xs text-amber-700">
          <div className="flex items-start gap-2">
            <UntitledIcon name="alert" size={16} />
            <div className="space-y-1.5">
              <p className="font-bold">
                Applying a restriction withdraws bays from sale immediately.
              </p>
              <p>
                An <span className="font-semibold">Emergency</span> closure marks the bays as
                UNAVAILABLE; a <span className="font-semibold">Forced maintenance</span> closure marks
                them as MAINTENANCE. Both stop new bookings straight away.
              </p>
              <p>
                Vehicles already parked (OCCUPIED) are never displaced — the driver can still check
                out normally.
              </p>
              <p>
                Conflicting future bookings are reconciled automatically by Operations: the booking is
                moved to another suitable bay when one is available, otherwise it is cancelled under
                FACILITY_FAILURE and the prepaid amount is refunded.
              </p>
            </div>
          </div>
        </div>

        {!canManage && (
          <div role="alert" className="rounded-xl border border-red-500/30 bg-red-500/10 p-3.5 text-xs text-red-600">
            The SLOT_OVERRIDE permission is required to create or release emergency restrictions. Ask a
            site administrator for access.
          </div>
        )}

        {restrictionFeedback && (
          <div className="flex items-center justify-between rounded-xl border border-green-500/30 bg-green-500/10 p-3.5 text-xs text-green-600 font-medium">
            <div className="flex items-center gap-2">
              <UntitledIcon name="check-circle" size={16} />
              <span>{restrictionFeedback}</span>
            </div>
            <button type="button" onClick={() => setRestrictionFeedback("")} className="hover:underline">
              Dismiss
            </button>
          </div>
        )}

        {restrictionsError && (
          <div role="alert" className="rounded-xl border border-red-500/30 bg-red-500/10 p-3.5 text-xs text-red-600">
            {restrictionsError}
          </div>
        )}

        {restrictionsLoading && (
          <div role="status" className="sr-only">
            Loading emergency restrictions...
          </div>
        )}

        <div className="grid gap-6 grid-cols-1 lg:grid-cols-2">
          {/* Left: Active restrictions */}
          <div className="space-y-3">
            <h3 className="text-sm font-bold text-[var(--fg)]">Active restrictions</h3>

            {activeRestrictions.map((item) => (
              <div
                key={item.id}
                className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-4 space-y-2.5"
              >
                <div className="flex items-center justify-between gap-2">
                  <span className="rounded-full border border-red-500/30 bg-red-500/10 px-2.5 py-0.5 text-[11px] font-bold uppercase text-red-600">
                    {modeLabels[item.mode] ?? item.mode}
                  </span>
                  <span className="text-xs text-[var(--muted)]">
                    {item.affectedSlotIds.length} bay(s) in scope
                  </span>
                </div>

                <p className="text-xs font-semibold text-[var(--fg)]">
                  {formatEmergencyRestrictionScope(item, targets)}
                </p>

                <p className="whitespace-pre-wrap text-xs text-[var(--muted)] bg-[var(--bg)] p-2 rounded border border-[var(--border)]">
                  {item.reason}
                </p>

                <div className="space-y-0.5 text-[11px] text-[var(--muted)]">
                  <p>From {new Date(item.effectiveFrom).toLocaleString()}</p>
                  <p>
                    {item.effectiveUntil
                      ? `Until ${new Date(item.effectiveUntil).toLocaleString()}`
                      : "Until manually released"}
                  </p>
                </div>

                <div className="flex justify-end pt-1">
                  <button
                    type="button"
                    className="btn-outline text-xs py-1 px-3 text-green-600 border-green-500/30 hover:bg-green-500/10"
                    disabled={!canManage || restrictionBusyId === item.id || busy}
                    onClick={() => void releaseRestriction(item)}
                  >
                    <UntitledIcon name="check" size={13} />{" "}
                    {restrictionBusyId === item.id ? "Releasing..." : "Release Restriction"}
                  </button>
                </div>
              </div>
            ))}

            {!restrictionsLoading && activeRestrictions.length === 0 && (
              <div className="rounded-xl border border-dashed border-[var(--border)] p-8 text-center text-xs text-[var(--muted)]">
                <UntitledIcon name="check-circle" size={24} />
                <p className="mt-2 font-medium">No active emergency restrictions.</p>
              </div>
            )}
          </div>

          {/* Right: Create restriction */}
          <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5">
            <h3 className="text-sm font-bold text-[var(--fg)] mb-1">Apply restriction</h3>
            <p className="text-xs text-[var(--muted)] mb-4">
              Close a whole site, a floor or zone, or a single bay.
            </p>

            <form onSubmit={submitRestriction} className="space-y-4">
              <div>
                <label className="block text-xs font-semibold text-[var(--fg)] mb-1">Closure type</label>
                <RoundedSelect
                  className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-xs text-[var(--fg)] outline-none focus:border-[var(--primary)]"
                  value={restrictionDraft.mode}
                  onChange={(e) =>
                    setRestrictionDraft((draft) => ({
                      ...draft,
                      mode: e.target.value as EmergencyRestrictionMode,
                    }))
                  }
                  disabled={!canManage || !siteId || restrictionBusyId !== ""}
                >
                  <option value="EMERGENCY">Emergency Closure (UNAVAILABLE)</option>
                  <option value="FORCED_MAINTENANCE">Forced Maintenance (MAINTENANCE)</option>
                </RoundedSelect>
              </div>

              <div>
                <label className="block text-xs font-semibold text-[var(--fg)] mb-1">Scope</label>
                <RoundedSelect
                  className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-xs text-[var(--fg)] outline-none focus:border-[var(--primary)]"
                  value={restrictionDraft.scope}
                  onChange={(e) => {
                    const scope = e.target.value as EmergencyRestrictionScope
                    setRestrictionDraft((draft) => ({
                      ...draft,
                      scope,
                      unitId: "",
                      slotId: "",
                    }))
                    setSiteClosurePending(false)
                  }}
                  disabled={!canManage || !siteId || restrictionBusyId !== ""}
                >
                  <option value="SITE">Whole site</option>
                  <option value="UNIT">Floor or Zone</option>
                  <option value="SLOT">Specific bay</option>
                </RoundedSelect>
              </div>

              {restrictionDraft.scope === "UNIT" && (
                <div>
                  <label className="block text-xs font-semibold text-[var(--fg)] mb-1">Floor / Zone</label>
                  <RoundedSelect
                    className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-xs text-[var(--fg)] outline-none focus:border-[var(--primary)]"
                    value={restrictionDraft.unitId}
                    onChange={(e) =>
                      setRestrictionDraft((draft) => ({ ...draft, unitId: e.target.value }))
                    }
                    disabled={!canManage || !siteId || restrictionBusyId !== ""}
                  >
                    <option value="">Select a floor or zone</option>
                    {targets.units.map((unit) => (
                      <option key={unit.unitId} value={unit.unitId}>
                        {unit.label}
                      </option>
                    ))}
                  </RoundedSelect>
                </div>
              )}

              {restrictionDraft.scope === "SLOT" && (
                <div>
                  <label className="block text-xs font-semibold text-[var(--fg)] mb-1">Parking bay</label>
                  <RoundedSelect
                    className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-xs text-[var(--fg)] outline-none focus:border-[var(--primary)]"
                    value={restrictionDraft.slotId}
                    onChange={(e) => {
                      const slotId = e.target.value
                      const target = targets.slots.find((slot) => slot.slotId === slotId)
                      setRestrictionDraft((draft) => ({
                        ...draft,
                        slotId,
                        unitId: target?.unitId ?? "",
                      }))
                    }}
                    disabled={!canManage || !siteId || restrictionBusyId !== ""}
                  >
                    <option value="">Select a bay</option>
                    {targets.slots.map((slot) => (
                      <option key={slot.slotId} value={slot.slotId}>
                        {slot.physicalState === "OCCUPIED"
                          ? `${slot.label} (OCCUPIED — car stays parked; future bookings reconciled)`
                          : slot.label}
                      </option>
                    ))}
                  </RoundedSelect>
                </div>
              )}

              <div>
                <label className="block text-xs font-semibold text-[var(--fg)] mb-1">
                  Effective until (optional)
                </label>
                <input
                  type="datetime-local"
                  className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-xs text-[var(--fg)] outline-none focus:border-[var(--primary)]"
                  value={restrictionDraft.effectiveUntilLocal}
                  onChange={(e) =>
                    setRestrictionDraft((draft) => ({
                      ...draft,
                      effectiveUntilLocal: e.target.value,
                    }))
                  }
                  disabled={!canManage || !siteId || restrictionBusyId !== ""}
                />
                <p className="mt-1 text-[11px] text-[var(--muted)]">
                  Leave empty to keep the closure until it is manually released.
                </p>
              </div>

              <div>
                <label className="block text-xs font-semibold text-[var(--fg)] mb-1">Reason</label>
                <textarea
                  className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-xs text-[var(--fg)] outline-none focus:border-[var(--primary)]"
                  rows={3}
                  maxLength={2000}
                  value={restrictionDraft.reason}
                  onChange={(e) =>
                    setRestrictionDraft((draft) => ({ ...draft, reason: e.target.value }))
                  }
                  placeholder="Burst pipe in Zone A — closing bays immediately"
                  required
                  disabled={!canManage || !siteId || restrictionBusyId !== ""}
                />
              </div>

              {restrictionDraft.scope === "SITE" && siteClosurePending && (
                <div role="alert" className="rounded-xl border border-red-500/40 bg-red-500/10 p-3.5 space-y-3 text-xs text-red-600">
                  <p className="font-bold">Confirm whole-site closure</p>
                  <p>
                    All unoccupied bays will close immediately (UNAVAILABLE or MAINTENANCE) and every
                    conflicting future booking will be cancelled under FACILITY_FAILURE with a refund
                    request. Parked vehicles stay where they are.
                  </p>
                  <div className="flex flex-wrap gap-2">
                    <button
                      type="submit"
                      className="btn-primary text-xs py-1.5 px-3"
                      disabled={restrictionBusyId !== ""}
                    >
                      <UntitledIcon name="alert" size={13} />{" "}
                      {restrictionBusyId === "new" ? "Activating..." : "Confirm closure"}
                    </button>
                    <button
                      type="button"
                      className="btn-outline text-xs py-1.5 px-3"
                      onClick={() => setSiteClosurePending(false)}
                    >
                      Cancel
                    </button>
                  </div>
                </div>
              )}

              <button
                type="submit"
                className="btn-primary text-xs py-2 px-4 w-full justify-center"
                disabled={!canManage || !siteId || restrictionBusyId !== ""}
              >
                <UntitledIcon name="alert" size={14} />{" "}
                {restrictionBusyId === "new"
                  ? "Activating..."
                  : restrictionDraft.scope === "SITE"
                    ? "Activate whole-site closure"
                    : "Activate restriction"}
              </button>
            </form>
          </div>
        </div>
      </div>
    </div>
  )
}
