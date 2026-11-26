import { useConfirmation } from "../../../components/ui/useConfirmation"
import { ParkingDateField } from "../../../components/forms/ParkingDateField"
import { useEffect, useState, type FormEvent } from "react"
import { Badge, type BadgeTone } from "../../../components/ui/Badge"
import { parkingApi, type MaintenanceWindow, type Structure } from "../../../lib/parkingApi"
import { formatSlotLabel } from "../../../lib/operationalLayout"
import {
  defaultExtensionEnd,
  formatMaintenanceScope,
  isoToLocalInput,
  localInputToIso,
  nextMaintenanceBoundaryMs,
  validateMaintenanceWindow,
  validateWindowExtension,
} from "../../../lib/structureRules"
import { StructureFeedback } from "./StructureFeedback"
import { useStructureAction } from "./useStructureAction"

const inputClass = "input w-full text-sm"
const labelClass = "space-y-1 text-xs font-medium text-[var(--muted)]"

const SERVICE_TONE: Record<string, BadgeTone> = {
  ACTIVE: "success",
  SCHEDULED: "warning",
  MAINTENANCE: "danger",
  MAINTENANCE_UNCERTAIN: "warning",
  INACTIVE: "muted",
}

function formatInstant(value: string | null | undefined) {
  if (!value) return "—"
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString()
}

type RunAction = (action: () => Promise<unknown>, success?: string | ((result: unknown) => string)) => Promise<boolean>

/** The UI reflects committed Facility maintenance schedules and async Operations sync state. */
function enforcementNotice(_result: unknown, action: "scheduled" | "extended") {
  return `Maintenance window ${action} and published to Operations.`
}

function enforcementCopy(window: MaintenanceWindow) {
  if (window.enforcementState === "acknowledged") {
    return { tone: "success" as BadgeTone, label: "Active schedule", help: "Committed by Facility and published to Operations." }
  }
  if (window.enforcementState === "failed") {
    return {
      tone: "danger" as BadgeTone,
      label: "Enforcement failed",
      help: "Legacy window could not be confirmed; re-read and reconcile before claiming the lot is closed.",
    }
  }
  return {
    tone: "warning" as BadgeTone,
    label: "Pending sync",
    help: "Legacy pending window. Recreate or extend to publish via the event stream.",
  }
}

function WindowRow({
  siteId,
  window,
  units,
  slots,
  busy,
  run,
}: {
  siteId: string
  window: MaintenanceWindow
  units: Structure["units"]
  slots: Structure["slots"]
  busy: boolean
  run: RunAction
}) {
  const { confirm, confirmation } = useConfirmation()
  const [end, setEnd] = useState(isoToLocalInput(defaultExtensionEnd(window.end)))
  const [error, setError] = useState("")
  const enforcement = enforcementCopy(window)
  const scope = formatMaintenanceScope(window, units, slots, formatSlotLabel)

  async function extend() {
    const iso = localInputToIso(end)
    const invalid = validateWindowExtension(window, iso, new Date())
    if (invalid) {
      setError(invalid)
      return
    }
    setError("")
    await run(
      () => parkingApi.extendMaintenanceWindow(siteId, window.id, iso!),
      (result) => enforcementNotice(result, "extended"),
    )
  }

  async function cancel() {
    if (!await confirm({ title: "Cancel maintenance?", description: "Reopening is refused if another live closure still covers now.", confirmLabel: "Cancel window", danger: true })) return
    setError("")
    await run(
      () => parkingApi.cancelMaintenanceWindow(siteId, window.id),
      "Maintenance window cancelled.",
    )
  }

  return (
    <li className="space-y-2 px-3 py-3">
      {confirmation}
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0 break-words">
          <p className="text-sm font-semibold text-[var(--fg)]">
            {formatInstant(window.start)} <span className="text-[var(--muted)]">→</span> {formatInstant(window.end)}
          </p>
          <p className="mt-0.5 text-xs text-[var(--muted)]">{window.reason || "No reason recorded."}</p>
          <p className="mt-1">
            <Badge tone="muted">{scope}</Badge>
          </p>
          <p className="mt-1 text-[11px] text-[var(--muted)]">{enforcement.help}</p>
        </div>
        <Badge tone={enforcement.tone}>{enforcement.label}</Badge>
      </div>
      <div className="flex flex-wrap items-end gap-2">
        <div className="min-w-0 flex-[1_1_16rem]">
          <ParkingDateField label="New end" withTime value={end} min={isoToLocalInput(window.end)}
            disabled={busy} onChange={value => { setEnd(value); setError("") }} />
        </div>
        <button type="button" className="btn-outline px-2.5 py-1.5 text-xs" onClick={extend} disabled={busy}>
          Extend
        </button>
        <button type="button" className="btn-ghost px-2.5 py-1.5 text-xs text-[var(--occupied)]" onClick={cancel} disabled={busy}>
          Cancel window
        </button>
      </div>
      {error && (
        <p role="alert" className="text-[11px] text-red-600 dark:text-red-400">
          {error}
        </p>
      )}
    </li>
  )
}

function WindowComposer({
  siteId,
  units,
  slots,
  busy,
  run,
}: {
  siteId: string
  units: Structure["units"]
  slots: Structure["slots"]
  busy: boolean
  run: RunAction
}) {
  const oneHour = 60 * 60 * 1000
  const [start, setStart] = useState(() => isoToLocalInput(new Date(Date.now() + oneHour).toISOString()))
  const [end, setEnd] = useState(() => isoToLocalInput(new Date(Date.now() + oneHour * 2).toISOString()))
  const [reason, setReason] = useState("")
  const [scope, setScope] = useState<"site" | "unit" | "slot">("site")
  const [unitId, setUnitId] = useState("")
  const [slotId, setSlotId] = useState("")
  const [error, setError] = useState("")

  async function submit(event: FormEvent) {
    event.preventDefault()
    const startIso = localInputToIso(start)
    const endIso = localInputToIso(end)
    const invalid = validateMaintenanceWindow(startIso, endIso, new Date())
    if (invalid) {
      setError(invalid)
      return
    }
    if (!reason.trim()) {
      setError("Provide a reason (1–2000 characters).")
      return
    }
    const selectedSlot = scope === "slot" ? slots.find((candidate) => candidate.id === slotId) : undefined
    if (scope === "unit" && !unitId) {
      setError("Select the floor, zone or block this closure covers.")
      return
    }
    if (scope === "slot" && !selectedSlot) {
      setError("Select the bay this closure covers.")
      return
    }
    setError("")
    const ok = await run(
      () =>
        parkingApi.createMaintenanceWindow(siteId, {
          start: startIso!,
          end: endIso!,
          reason: reason.trim(),
          ...(scope === "unit" ? { unitId } : {}),
          // A slot-scoped window must also name its unit (`ck_window_scope`).
          ...(scope === "slot" ? { unitId: selectedSlot!.unitId, slotId: selectedSlot!.id } : {}),
        }),
      (result) => enforcementNotice(result, "scheduled"),
    )
    if (ok) {
      setReason("")
      setStart(isoToLocalInput(new Date(Date.now() + oneHour).toISOString()))
      setEnd(isoToLocalInput(new Date(Date.now() + oneHour * 2).toISOString()))
    }
  }

  return (
    <form onSubmit={submit} className="space-y-2 rounded-xl border border-[var(--border)] bg-[var(--bg)] p-3">
      <div className="grid gap-2 sm:grid-cols-2">
        <ParkingDateField label="Start" withTime value={start} disabled={busy}
          onChange={value => { setStart(value); setError("") }} />
        <ParkingDateField label="End" withTime value={end} min={start} disabled={busy}
          onChange={value => { setEnd(value); setError("") }} />
      </div>
      <div className="grid gap-2 sm:grid-cols-2">
        <label className={labelClass}>
          <span>Scope</span>
          <select
            className={inputClass}
            value={scope}
            disabled={busy}
            onChange={(event) => {
              setScope(event.target.value as "site" | "unit" | "slot")
              setError("")
            }}
          >
            <option value="site">Whole site</option>
            <option value="unit">Floor / Zone / Block</option>
            <option value="slot">Specific bay</option>
          </select>
        </label>
        {scope === "unit" && (
          <label className={labelClass}>
            <span>Unit</span>
            <select
              className={inputClass}
              value={unitId}
              disabled={busy}
              onChange={(event) => { setUnitId(event.target.value); setError("") }}
            >
              <option value="">Select a unit…</option>
              {units.map((unit) => (
                <option key={unit.id} value={unit.id}>{`${unit.type}: ${unit.name}`}</option>
              ))}
            </select>
          </label>
        )}
        {scope === "slot" && (
          <label className={labelClass}>
            <span>Bay</span>
            <select
              className={inputClass}
              value={slotId}
              disabled={busy}
              onChange={(event) => { setSlotId(event.target.value); setError("") }}
            >
              <option value="">Select a bay…</option>
              {slots.map((slot) => {
                const parent = units.find((unit) => unit.id === slot.unitId)
                return (
                  <option key={slot.id} value={slot.id}>
                    {`${formatSlotLabel(slot.code)} — ${parent?.name ?? "Unassigned"}`}
                  </option>
                )
              })}
            </select>
          </label>
        )}
      </div>
      <p className="text-[11px] text-[var(--muted)]">
        Half-open <strong>[start, end)</strong>: the lot stops accepting new commitments at the start and serves again at
        the end. The end instant is already outside the window; an empty window is refused.
      </p>
      <label className={labelClass}>
        <span>Reason</span>
        <input
          className={inputClass}
          value={reason}
          onChange={(event) => {
            setReason(event.target.value)
            setError("")
          }}
          maxLength={2000}
          placeholder="Resurfacing level 2"
          required
        />
      </label>
      {error && (
        <p role="alert" className="text-[11px] text-red-600 dark:text-red-400">
          {error}
        </p>
      )}
      <div className="flex justify-end pt-3">
        <button type="submit" className="btn-primary min-h-11" disabled={busy}>
          {busy ? "Recording…" : "Schedule closure"}
        </button>
      </div>
    </form>
  )
}

export function ServiceAvailabilityPanel({
  siteId,
  structure,
  onChanged,
}: {
  siteId: string
  structure: Structure
  onChanged: () => void
}) {
  const { busy, error, notice, run } = useStructureAction(onChanged)
  const serviceState = structure.serviceState
  const state = serviceState?.state ?? "UNKNOWN"
  const [fallbackWindows, setFallbackWindows] = useState<MaintenanceWindow[] | null>(null)

  // `GET /structure` already carries serviceState.windows. The explicit window route is
  // used when a server build omits that block, so the panel never invents a window list.
  useEffect(() => {
    if (serviceState) {
      setFallbackWindows(null)
      return
    }
    let active = true
    parkingApi
      .maintenanceWindows(siteId)
      .then((windows) => { if (active) setFallbackWindows(windows) })
      .catch(() => { if (active) setFallbackWindows([]) })
    return () => { active = false }
  }, [siteId, serviceState])

  const windows = serviceState?.windows ?? fallbackWindows ?? []
  const hasPendingWindow = windows.some((window) => !window.cancelledAt && window.enforcementState === "pending")

  // Exactly one single-shot timer at the next window boundary; no polling interval.
  // The extra second lets the server clock pass the boundary before the re-read.
  useEffect(() => {
    const delayMs = nextMaintenanceBoundaryMs(windows)
    if (delayMs === null) return
    const timer = window.setTimeout(() => onChanged(), Math.min(delayMs + 1000, 2_147_483_647))
    return () => window.clearTimeout(timer)
  }, [windows, onChanged])

  const lifecycleInactive =
    structure.site.isActive === false || structure.site.status === "INACTIVE" || structure.site.status === "SUSPENDED"

  const banner = (() => {
    switch (state) {
      case "INACTIVE":
        return "Lifecycle status is INACTIVE. The lot accepts no new commitments; nothing was deleted."
      case "SCHEDULED":
        return "A closure is scheduled. The lot keeps serving until the window starts, and its lifecycle status is unchanged."
      case "MAINTENANCE":
        return `Closed for maintenance until ${formatInstant(serviceState?.closedUntil)}. It serves again automatically when the window ends.`
      case "MAINTENANCE_UNCERTAIN":
        return `A closure is in progress but enforcement is not confirmed. Admission may still accept bookings — this is not the same as closed. Ends ${formatInstant(serviceState?.closedUntil)}.`
      default:
        return serviceState
          ? "No maintenance window is in force. The lot is serving."
          : "Service availability is not reported for this site, so the current state is unknown."
    }
  })()

  const { confirm, confirmation } = useConfirmation()

  async function toggleLifecycle() {
    if (lifecycleInactive) {
      await run(
        () => parkingApi.edit(siteId, { action: "active", resourceId: siteId, active: true }),
        "Lot activated. It can accept new commitments again.",
      )
      return
    }
    if (
      !await confirm({ title: "Deactivate parking lot?", description: "This changes lifecycle status only and is allowed only when no live commitment exists.", confirmLabel: "Deactivate lot", danger: true })
    )
      return
    await run(
      () => parkingApi.edit(siteId, { action: "active", resourceId: siteId, active: false }),
      "Lot deactivated. No window was created, cancelled or implied.",
    )
  }

  return (
    <section className="space-y-3 rounded-2xl border border-[var(--border)] bg-[var(--card)] p-4" aria-label="Deactivation and maintenance windows">
      {confirmation}
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <h2 className="text-base font-semibold">Service availability</h2>
            <Badge tone={SERVICE_TONE[state] ?? "muted"}>{state.replace(/_/g, " ")}</Badge>
            {lifecycleInactive && <Badge tone="muted">Lifecycle inactive</Badge>}
          </div>
          <p className="mt-1 max-w-3xl text-xs text-[var(--muted)]">{banner}</p>
          {!serviceState && (
            <p className="mt-1 text-xs text-amber-700 dark:text-amber-300">
              This structure response does not include service availability. The window list is read from the
              maintenance-window route, and the current state is unknown rather than assumed to be ACTIVE.
            </p>
          )}
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {hasPendingWindow && (
            <button
              type="button"
              className="btn-ghost px-3 py-1.5 text-xs"
              onClick={onChanged}
              disabled={busy}
            >
              Refresh status
            </button>
          )}
          <button
            type="button"
            className={lifecycleInactive ? "btn-primary px-3 py-1.5 text-xs" : "btn-outline px-3 py-1.5 text-xs"}
            onClick={toggleLifecycle}
            disabled={busy}
          >
            {lifecycleInactive ? "Activate lot" : "Deactivate lot now"}
          </button>
        </div>
      </div>

      <StructureFeedback error={error} notice={notice} fallback="The availability change failed." />

      <div className="grid gap-3 lg:grid-cols-2">
        <div className="space-y-2">
          <h3 className="text-xs font-semibold uppercase tracking-wide text-[var(--muted)]">Maintenance windows</h3>
          {windows.length === 0 ? (
            <p className="rounded-xl border border-dashed border-[var(--border)] p-4 text-xs text-[var(--muted)]">
              No maintenance windows are recorded for this site.
            </p>
          ) : (
            <ul className="divide-y divide-[var(--border)] rounded-xl border border-[var(--border)]">
              {windows.map((window) => (
                <WindowRow key={window.id} siteId={siteId} window={window} units={structure.units} slots={structure.slots} busy={busy} run={run} />
              ))}
            </ul>
          )}
        </div>
        <div className="space-y-2">
          <h3 className="text-xs font-semibold uppercase tracking-wide text-[var(--muted)]">Schedule a closure</h3>
          <WindowComposer siteId={siteId} units={structure.units} slots={structure.slots} busy={busy} run={run} />
        </div>
      </div>
    </section>
  )
}
