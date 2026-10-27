import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { useState } from "react"

import { parkingApi, type Structure } from "../../../lib/parkingApi"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"

export function BackupCapacityPanel({
  structure,
  onChanged,
}: {
  structure: Structure
  onChanged: () => void
}) {
  const [form, setForm] = useState({
    unitId: "",
    vehicleType: "CAR",
    capacity: 0,
  })
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState("")
  const [notice, setNotice] = useState("")
  const visibleViews = structure.capacityViews.filter((view) => view.total > 0)

  function scopeName(unitId: string | null) {
    if (!unitId) return "Whole site"
    const names: string[] = []
    const seen = new Set<string>()
    let current = structure.units.find((unit) => unit.id === unitId)
    while (current && !seen.has(current.id)) {
      seen.add(current.id)
      names.unshift(current.name)
      current = structure.units.find((unit) => unit.id === current?.parentId)
    }
    return names.join(" / ") || "Unknown location"
  }

  async function save(e: React.FormEvent) {
    e.preventDefault()
    if (busy) return
    setBusy(true)
    setError("")
    setNotice("")
    try {
      await parkingApi.edit(structure.site.id, {
        action: "backup",
        ...form,
        unitId: form.unitId || "00000000-0000-0000-0000-000000000000",
      })
      setNotice("Backup configuration saved.")
      onChanged()
    } catch (e) {
      setError(e instanceof Error ? e.message : "Cannot update backup.")
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="space-y-5 text-[var(--fg)]">
      <div className="flex items-start gap-3">
        <span className="flex size-11 shrink-0 items-center justify-center rounded-lg bg-[var(--raised)] text-[var(--maintenance)]">
          <UntitledIcon name="shield" size={19} />
        </span>
        <div>
          <h3 className="text-sm font-semibold">Capacity overview</h3>
          <p className="mt-1 text-xs leading-relaxed text-[var(--muted)]">
            Configured and marked backup share the same inventory. Overlap is counted once.
          </p>
        </div>
      </div>

      <div className="grid gap-5 xl:grid-cols-[minmax(0,1fr)_280px]">
        <div className="min-w-0 space-y-3">
          {visibleViews.length === 0 ? (
            <div className="rounded-xl border border-dashed border-[var(--border)] bg-[var(--bg)] px-5 py-10 text-center text-xs text-[var(--muted)]">
              No capacity records are available for this site.
            </div>
          ) : visibleViews.map((view) => (
            <article key={`${view.unitId}-${view.vehicleType}`} className="overflow-hidden rounded-xl border border-[var(--border)] bg-[var(--bg)]">
              <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border)] px-4 py-3">
                <div className="flex min-w-0 items-center gap-2">
                  <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-[var(--primary)]/10 text-[var(--primary)]"><UntitledIcon name="parking" size={16} /></span>
                  <div className="min-w-0"><p className="truncate text-xs font-semibold" title={scopeName(view.unitId)}>{scopeName(view.unitId)}</p><p className="text-[11px] text-[var(--muted)]">{view.vehicleType}</p></div>
                </div>
                <span className="rounded-full bg-emerald-500/10 px-2.5 py-1 text-xs font-semibold text-emerald-700 dark:text-emerald-300">{view.available === null ? "Availability unavailable" : `${view.available} available`}</span>
              </div>
              <dl className="grid grid-cols-2 gap-px bg-[var(--border)] sm:grid-cols-3">
                {[
                  ["Total", view.total],
                  ["Occupied", view.occupied],
                  ["Protected", view.protected],
                  ["Pending payment", view.pendingPayment],
                  ["Backup", view.effectiveBackup],
                  ["Unavailable", view.unavailable],
                ].map(([label, value]) => (
                  <div key={label} className="bg-[var(--bg)] px-4 py-3"><dt className="text-[11px] text-[var(--muted)]">{label}</dt><dd className="mt-0.5 text-base font-semibold tabular-nums">{value}</dd></div>
                ))}
              </dl>
              {view.inheritedBackup && <p className="border-t border-[var(--border)] px-4 py-2 text-[11px] text-[var(--muted)]">Backup configured at a parent location.</p>}
            </article>
          ))}
        </div>

        <form onSubmit={save} className="h-fit space-y-4 rounded-xl border border-[var(--border)] bg-[var(--bg)] p-4 xl:sticky xl:top-4">
          <div><h4 className="text-sm font-semibold">Configure backup</h4><p className="mt-1 text-xs text-[var(--muted)]">Reserve capacity by location and vehicle category.</p></div>
          <label className="block space-y-1.5 text-xs font-medium text-[var(--muted)]">
            <span>Scope</span>
            <RoundedSelect className="input w-full text-sm" value={form.unitId} onChange={(e) => setForm({ ...form, unitId: e.target.value })}>
              <option value="">Whole site</option>
              {structure.units.map((unit) => <option key={unit.id} value={unit.id}>{scopeName(unit.id)}</option>)}
            </RoundedSelect>
          </label>
          <label className="block space-y-1.5 text-xs font-medium text-[var(--muted)]">
            <span>Vehicle category</span>
            <RoundedSelect className="input w-full text-sm" value={form.vehicleType} onChange={(e) => setForm({ ...form, vehicleType: e.target.value })}>
              {["CAR", "MOTORCYCLE"].map((type) => <option key={type} value={type}>{type}</option>)}
            </RoundedSelect>
          </label>
          <label className="block space-y-1.5 text-xs font-medium text-[var(--muted)]">
            <span>Configured backup</span>
            <input className="input w-full text-sm" type="number" min={0} step={1} required value={form.capacity} onChange={(e) => setForm({ ...form, capacity: Number(e.target.value) })} />
          </label>
          <button type="submit" className="btn-primary w-full justify-center text-sm" disabled={busy}>{busy ? "Saving…" : "Save configuration"}</button>
          {notice && <p role="status" className="rounded-lg bg-emerald-500/10 px-3 py-2 text-xs text-emerald-700 dark:text-emerald-300">{notice}</p>}
          {error && <p role="alert" className="rounded-lg bg-red-500/10 px-3 py-2 text-xs text-red-700 dark:text-red-300">{error}</p>}
        </form>
      </div>
    </section>
  )
}
