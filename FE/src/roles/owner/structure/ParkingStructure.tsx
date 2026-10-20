import { useEffect, useMemo, useRef, useState, type FormEvent } from "react"
import { parkingApi, type Site, type Slot, type Structure, type Unit } from "../../../lib/parkingApi"
import { BackupCapacityPanel } from "../dashboard/BackupCapacityPanel"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"

interface ParkingStructureProps {
  sites?: Site[]
  selectedSiteId?: string
  onChanged?: () => void
}

const unitTypes = ["FLOOR", "ZONE", "BLOCK"] as const
const vehicleTypes = ["CAR", "MOTORCYCLE", "OVERSIZED"] as const
const slotTypes = ["STANDARD", "VIP", "EV", "DISABLED"] as const

const inputClass = "input w-full text-sm"
const labelClass = "space-y-1 text-xs font-medium text-[var(--muted)]"

const metricStyles = {
  available: { color: "text-emerald-700 dark:text-emerald-300", bg: "rgba(34,197,94,.10)", border: "rgba(34,197,94,.3)", icon: "check-circle" },
  occupied: { color: "text-red-700 dark:text-red-300", bg: "rgba(239,68,68,.10)", border: "rgba(239,68,68,.3)", icon: "car" },
  protected: { color: "text-indigo-700 dark:text-indigo-300", bg: "rgba(99,102,241,.10)", border: "rgba(99,102,241,.3)", icon: "shield" },
  payment: { color: "text-amber-700 dark:text-amber-300", bg: "rgba(245,158,11,.10)", border: "rgba(245,158,11,.3)", icon: "banknote" },
  backup: { color: "text-purple-700 dark:text-purple-300", bg: "rgba(147,51,234,.10)", border: "rgba(147,51,234,.3)", icon: "shield" },
} as const

function Metric({ label, value, tone }: { label: string; value: string | number; tone: keyof typeof metricStyles }) {
  const style = metricStyles[tone]
  return <div className="min-w-0 rounded-xl border p-3" style={{ background: style.bg, borderColor: style.border }}><div className={`flex items-center justify-between gap-2 text-[11px] font-semibold ${style.color}`}><span>{label}</span><UntitledIcon name={style.icon} size={14} /></div><div className={`mt-1 truncate text-xl font-bold ${style.color}`}>{value}</div></div>
}

function errorText(error: unknown, fallback: string) {
  return error instanceof Error ? error.message : fallback
}

function slotAppearance(slot: Slot) {
  if (slot.isPhysicallyOccupied && slot.reservationState === "PROTECTED") return { label: "Occupied + protected", color: "#be123c", bg: "rgba(225,29,72,.12)", border: "rgba(225,29,72,.55)" }
  if (slot.isPhysicallyOccupied) return { label: "Occupied", color: "#dc2626", bg: "rgba(239,68,68,.10)", border: "rgba(239,68,68,.4)" }
  if (slot.reservationState === "PROTECTED") return { label: "Protected", color: "#4f46e5", bg: "rgba(99,102,241,.10)", border: "rgba(99,102,241,.4)" }
  if (slot.reservationState === "PENDING_PAYMENT") return { label: "Pending payment", color: "#d97706", bg: "rgba(245,158,11,.10)", border: "rgba(245,158,11,.4)" }
  if (slot.reservationState && slot.reservationState !== "NONE") return { label: slot.reservationState.replace(/_/g, " "), color: "#2563eb", bg: "rgba(37,99,235,.10)", border: "rgba(37,99,235,.4)" }
  if (slot.operationalStatus !== "ACTIVE" && slot.operationalStatus !== "AVAILABLE") return { label: slot.operationalStatus.replace(/_/g, " "), color: "#9333ea", bg: "rgba(147,51,234,.10)", border: "rgba(147,51,234,.4)" }
  return { label: slot.operationalStatus, color: "#16a34a", bg: "rgba(34,197,94,.10)", border: "rgba(34,197,94,.4)" }
}

export function ParkingStructure({ sites = [], selectedSiteId, onChanged }: ParkingStructureProps) {
  const [siteId, setSiteId] = useState("")
  const [structure, setStructure] = useState<Structure | null>(null)
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)
  const savingRef = useRef(false)
  const [error, setError] = useState("")
  const [notice, setNotice] = useState("")
  const [reload, setReload] = useState(0)
  const [search, setSearch] = useState("")
  const [unitFilter, setUnitFilter] = useState("")
  const [floorFilter, setFloorFilter] = useState("")
  const [statusFilter, setStatusFilter] = useState("")
  const [createModal, setCreateModal] = useState<"unit" | "slot" | null>(null)
  const [selectedSlot, setSelectedSlot] = useState<Slot | null>(null)
  const [unitForm, setUnitForm] = useState({ type: "FLOOR", name: "", parentId: "", capacity: "0" })
  const [slotForm, setSlotForm] = useState({ unitId: "", code: "", vehicleType: "CAR", type: "STANDARD" })

  useEffect(() => {
    setSiteId((current) => {
      if (selectedSiteId && selectedSiteId !== "all" && sites.some((site) => site.id === selectedSiteId)) return selectedSiteId
      return sites.some((site) => site.id === current) ? current : (sites[0]?.id ?? "")
    })
  }, [selectedSiteId, sites])

  useEffect(() => {
    let current = true
    setStructure(null)
    setSelectedSlot(null)
    setError("")
    setUnitFilter("")
    setFloorFilter("")
    setStatusFilter("")
    setCreateModal(null)
    if (!siteId) {
      setLoading(false)
      return () => { current = false }
    }
    setLoading(true)
    parkingApi.structure(siteId)
      .then((data) => {
        if (!current) return
        setStructure(data)
        setSlotForm((form) => ({ ...form, unitId: data.units.some((unit) => unit.id === form.unitId) ? form.unitId : (data.units[0]?.id ?? "") }))
      })
      .catch((cause) => { if (current) setError(errorText(cause, "Cannot load parking structure.")) })
      .finally(() => { if (current) setLoading(false) })
    return () => { current = false }
  }, [siteId, reload])

  const units = structure?.units ?? []
  const slots = structure?.slots ?? []
  const unitById = useMemo(() => new Map(units.map((unit) => [unit.id, unit])), [units])
  const parentOptions = units.filter((unit) =>
    unitForm.type === "ZONE" ? unit.type === "FLOOR" : unitForm.type === "BLOCK" && (unit.type === "FLOOR" || unit.type === "ZONE"),
  )

  function location(unitId: string) {
    const names: string[] = []
    const seen = new Set<string>()
    let cursor: Unit | undefined = unitById.get(unitId)
    while (cursor && !seen.has(cursor.id)) {
      seen.add(cursor.id)
      names.unshift(cursor.name)
      cursor = cursor.parentId ? unitById.get(cursor.parentId) : undefined
    }
    return names.length ? names.join(" / ") : "Unknown location"
  }

  function insideUnit(slotUnitId: string, selectedUnitId: string) {
    const seen = new Set<string>()
    let cursor: Unit | undefined = unitById.get(slotUnitId)
    while (cursor && !seen.has(cursor.id)) {
      if (cursor.id === selectedUnitId) return true
      seen.add(cursor.id)
      cursor = cursor.parentId ? unitById.get(cursor.parentId) : undefined
    }
    return false
  }

  const shownSlots = slots.filter((slot) =>
    (!floorFilter || insideUnit(slot.unitId, floorFilter)) &&
    (!unitFilter || insideUnit(slot.unitId, unitFilter)) &&
    (!statusFilter || (statusFilter === "occupied" ? slot.isPhysicallyOccupied : statusFilter === "empty" ? !slot.isPhysicallyOccupied : statusFilter === "protected" ? slot.reservationState === "PROTECTED" : statusFilter === "payment" ? slot.reservationState === "PENDING_PAYMENT" : slot.operationalStatus === statusFilter)) &&
    slot.code.toLowerCase().includes(search.trim().toLowerCase()),
  )
  const siteCapacity = (structure?.capacityViews ?? []).filter((view) => view.unitId === null)
  const floors = units.filter((unit) => unit.type === "FLOOR")
  const visibleUnits = units.filter((unit) => !floorFilter || insideUnit(unit.id, floorFilter))
  const total = (field: "available" | "protected" | "pendingPayment" | "effectiveBackup") => siteCapacity.reduce((sum, view) => sum + (view[field] ?? 0), 0)

  async function mutate(action: () => Promise<unknown>, success: string) {
    if (savingRef.current || !siteId) return false
    savingRef.current = true
    setSaving(true)
    setError("")
    setNotice("")
    try {
      await action()
      setNotice(success)
      setReload((value) => value + 1)
      try {
        const fresh = await parkingApi.structure(siteId)
        setStructure(fresh)
      } catch {
        // Handled on next effect reload
      }
      onChanged?.()
      return true
    } catch (cause) {
      setError(errorText(cause, "Cannot save parking structure."))
      return false
    } finally {
      savingRef.current = false
      setSaving(false)
    }
  }

  async function addUnit(event: FormEvent) {
    event.preventDefault()
    const name = unitForm.name.trim()
    const capacity = Number(unitForm.capacity)
    if (!name || !Number.isInteger(capacity) || capacity < 0) {
      setError("Enter a unit name and a non-negative whole-number capacity.")
      return
    }
    const parentId = unitForm.type === "FLOOR" ? null : (unitForm.parentId || null)
    if (unitForm.type === "BLOCK" && !parentId) {
      setError("Select a parent floor or zone for the block.")
      return
    }
    const saved = await mutate(
      () => parkingApi.unit(siteId, { type: unitForm.type, name, parentId, capacity }),
      "Unit created.",
    )
    if (saved) {
      setUnitForm((form) => ({ ...form, name: "" }))
      setCreateModal(null)
    }
  }

  async function addSlot(event: FormEvent) {
    event.preventDefault()
    const code = slotForm.code.trim()
    if (!slotForm.unitId || !code) {
      setError("Select a location and enter a slot code.")
      return
    }
    const saved = await mutate(
      () => parkingApi.slot(siteId, { ...slotForm, code }),
      "Slot created.",
    )
    if (saved) {
      setSlotForm((form) => ({ ...form, code: "" }))
      setCreateModal(null)
    }
  }

  return (
    <section className="space-y-5 text-[var(--fg)]">
      <header className="flex flex-col gap-3 border-b border-[var(--border)] pb-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-xl font-bold">Parking Structure</h1>
          <p className="mt-1 text-xs text-[var(--muted)]">Manage floors, zones, blocks and parking slots.</p>
        </div>
        <label className="min-w-0 space-y-1 text-xs font-medium text-[var(--muted)] sm:min-w-56">
          <span>Site</span>
          <select className={inputClass} value={siteId} onChange={(event) => setSiteId(event.target.value)} disabled={!sites.length || saving}>
            {!sites.length && <option value="">No sites available</option>}
            {sites.map((site) => <option key={site.id} value={site.id}>{site.name}</option>)}
          </select>
        </label>
      </header>

      {notice && <p role="status" className="rounded-lg border border-emerald-500/30 bg-emerald-500/10 px-4 py-2 text-sm text-emerald-700 dark:text-emerald-300">{notice}</p>}
      {error && <div role="alert" className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-2 text-sm text-red-700 dark:text-red-300"><span>{error}</span>{siteId && <button type="button" className="underline" onClick={() => setReload((value) => value + 1)}>Retry</button>}</div>}
      {!siteId && <p className="rounded-xl border border-[var(--border)] p-6 text-sm text-[var(--muted)]">No parking sites are available for this account.</p>}
      {loading && <p role="status" className="rounded-xl border border-[var(--border)] p-6 text-sm text-[var(--muted)]">Loading parking structure…</p>}

      {structure && !loading && (
        <>
          {!slots.length && <p className="rounded-xl border border-amber-500/30 bg-amber-500/10 px-4 py-3 text-xs text-amber-700 dark:text-amber-300">No backend parking slots are configured for this site yet. Add a unit, then add slots here for operators to use.</p>}
          <div className="grid grid-cols-2 gap-2.5 sm:grid-cols-3 lg:grid-cols-6">
            <Metric label="Physical slots" value={slots.length} tone="available" />
            <Metric label="Available" value={siteCapacity.length > 0 && siteCapacity.every((view) => view.available !== null) ? total("available") : "—"} tone="available" />
            <Metric label="Occupied" value={slots.filter((slot) => slot.isPhysicallyOccupied).length} tone="occupied" />
            <Metric label="Protected" value={siteCapacity.length ? total("protected") : "—"} tone="protected" />
            <Metric label="Pending payment" value={siteCapacity.length ? total("pendingPayment") : "—"} tone="payment" />
            <Metric label="Backup" value={siteCapacity.length ? total("effectiveBackup") : "—"} tone="backup" />
          </div>

          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-[var(--border)] pb-2">
            <div className="flex max-w-full items-center gap-1.5 overflow-x-auto pb-1">
              <button type="button" onClick={() => { setFloorFilter(""); setUnitFilter("") }} className={`shrink-0 rounded-lg px-3 py-1.5 text-xs font-semibold ${!floorFilter ? "bg-[var(--primary)] text-white" : "border border-[var(--border)] bg-[var(--card)] text-[var(--muted)]"}`}>All areas</button>
              {floors.map((floor) => <button key={floor.id} type="button" onClick={() => { setFloorFilter(floor.id); setUnitFilter("") }} className={`shrink-0 rounded-lg px-3 py-1.5 text-xs font-semibold ${floorFilter === floor.id ? "bg-[var(--primary)] text-white" : "border border-[var(--border)] bg-[var(--card)] text-[var(--muted)]"}`}>{floor.name}</button>)}
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <label className="flex items-center gap-2 text-xs text-[var(--muted)]"><span>Zone:</span><select className="input max-w-48 text-xs" value={unitFilter} onChange={(event) => setUnitFilter(event.target.value)}><option value="">All locations</option>{visibleUnits.filter((unit) => unit.type !== "FLOOR").map((unit) => <option key={unit.id} value={unit.id}>{location(unit.id)}</option>)}</select></label>
              <button type="button" className="btn-outline px-2.5 py-1 text-xs" onClick={() => { setUnitForm((form) => ({ ...form, type: floorFilter ? "ZONE" : "FLOOR", parentId: floorFilter })); setCreateModal("unit") }}><UntitledIcon name="plus" size={12} /> Add unit</button>
              <button type="button" className="btn-primary px-2.5 py-1 text-xs" onClick={() => setCreateModal("slot")} disabled={!units.length}><UntitledIcon name="plus" size={12} /> Add slot</button>
            </div>
          </div>

          {createModal && <div role="dialog" aria-modal="true" aria-label={createModal === "unit" ? "Add unit" : "Add slot"} className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4" onMouseDown={(event) => { if (event.target === event.currentTarget && !saving) setCreateModal(null) }} onKeyDown={(event) => { if (event.key === "Escape" && !saving) setCreateModal(null) }}>
            {createModal === "unit" ? <form onSubmit={addUnit} className="w-full max-w-lg space-y-4 rounded-xl border border-[var(--border)] bg-[var(--card)] p-5 shadow-xl">
              <h2 className="font-semibold">Add floor, zone or block</h2>
              <div className="grid gap-3 sm:grid-cols-2">
                <label className={labelClass}><span>Type</span><select className={inputClass} value={unitForm.type} onChange={(event) => setUnitForm((form) => ({ ...form, type: event.target.value, parentId: "" }))}>{unitTypes.map((type) => <option key={type} value={type}>{type}</option>)}</select></label>
                <label className={labelClass}><span>Name</span><input className={inputClass} value={unitForm.name} onChange={(event) => setUnitForm((form) => ({ ...form, name: event.target.value }))} required /></label>
                {unitForm.type !== "FLOOR" && <label className={labelClass}><span>Parent {unitForm.type === "ZONE" ? "(optional)" : "(required)"}</span><select className={inputClass} value={unitForm.parentId} onChange={(event) => setUnitForm((form) => ({ ...form, parentId: event.target.value }))} required={unitForm.type === "BLOCK"}><option value="">{unitForm.type === "ZONE" ? "Site root" : "Select a parent"}</option>{parentOptions.map((unit) => <option key={unit.id} value={unit.id}>{location(unit.id)}</option>)}</select></label>}
                <label className={labelClass}><span>Capacity (0 = unlimited)</span><input className={inputClass} type="number" min="0" step="1" value={unitForm.capacity} onChange={(event) => setUnitForm((form) => ({ ...form, capacity: event.target.value }))} required /></label>
              </div>
              {error && <p role="alert" className="text-xs text-red-600">{error}</p>}
              <div className="flex justify-end gap-2"><button type="button" className="btn-outline" onClick={() => setCreateModal(null)} disabled={saving}>Cancel</button><button type="submit" className="btn-primary" disabled={saving}>{saving ? "Saving…" : "Add unit"}</button></div>
            </form> : <form onSubmit={addSlot} className="w-full max-w-lg space-y-4 rounded-xl border border-[var(--border)] bg-[var(--card)] p-5 shadow-xl">
              <h2 className="font-semibold">Add parking slot</h2>
              <div className="grid gap-3 sm:grid-cols-2">
                <label className={labelClass}><span>Location</span><select className={inputClass} value={slotForm.unitId} onChange={(event) => setSlotForm((form) => ({ ...form, unitId: event.target.value }))} required><option value="">Select a unit</option>{units.map((unit) => <option key={unit.id} value={unit.id}>{location(unit.id)}</option>)}</select></label>
                <label className={labelClass}><span>Slot code</span><input className={inputClass} value={slotForm.code} onChange={(event) => setSlotForm((form) => ({ ...form, code: event.target.value }))} required /></label>
                <label className={labelClass}><span>Vehicle type</span><select className={inputClass} value={slotForm.vehicleType} onChange={(event) => setSlotForm((form) => ({ ...form, vehicleType: event.target.value }))}>{vehicleTypes.map((type) => <option key={type} value={type}>{type}</option>)}</select></label>
                <label className={labelClass}><span>Slot type</span><select className={inputClass} value={slotForm.type} onChange={(event) => setSlotForm((form) => ({ ...form, type: event.target.value }))}>{slotTypes.map((type) => <option key={type} value={type}>{type}</option>)}</select></label>
              </div>
              {error && <p role="alert" className="text-xs text-red-600">{error}</p>}
              <div className="flex justify-end gap-2"><button type="button" className="btn-outline" onClick={() => setCreateModal(null)} disabled={saving}>Cancel</button><button type="submit" className="btn-primary" disabled={saving || !units.length}>{saving ? "Saving…" : "Add slot"}</button></div>
            </form>}
          </div>}

          <section className="space-y-3">
            <div className="flex flex-col gap-3 rounded-xl border border-[var(--border)] bg-[var(--card)] p-3 sm:flex-row sm:items-center sm:justify-between">
              <label className="flex min-w-0 flex-1 items-center gap-2 sm:max-w-sm">
                <UntitledIcon name="search" size={15} className="shrink-0 text-[var(--muted)]" />
                <span className="sr-only">Search slot code</span>
                <input className="min-w-0 flex-1 bg-transparent text-xs text-[var(--fg)] outline-none placeholder:text-[var(--muted)]" type="search" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search by slot code…" />
              </label>
              <div className="flex items-center justify-between gap-3 sm:justify-end">
                <label className="flex items-center gap-2 text-xs text-[var(--muted)]"><span>Status</span><select className="input max-w-36 text-xs" value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)}><option value="">All</option><option value="occupied">Occupied</option><option value="empty">Empty</option><option value="protected">Protected</option><option value="payment">Pending payment</option>{[...new Set(slots.map((slot) => slot.operationalStatus))].map((status) => <option key={status} value={status}>{status.replace(/_/g, " ")}</option>)}</select></label>
                <span className="whitespace-nowrap text-[11px] text-[var(--muted)]">Showing <strong className="text-[var(--fg)]">{shownSlots.length}</strong> / {slots.length}</span>
              </div>
            </div>
            {!shownSlots.length ? <p className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-10 text-center text-xs text-[var(--muted)]">{slots.length ? "No parking slots match the current filters." : "No parking slots have been created for this site."}</p> :
              <div className="grid grid-cols-2 gap-3 sm:grid-cols-4 md:grid-cols-6 xl:grid-cols-8">
                {shownSlots.map((slot) => {
                  const appearance = slotAppearance(slot)
                  return <button key={slot.id} type="button" onClick={() => setSelectedSlot(slot)} className="flex min-h-28 min-w-0 flex-col justify-between rounded-xl border-2 p-3 text-left transition-transform hover:scale-[1.02] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-500" style={{ background: appearance.bg, borderColor: appearance.border }}>
                    <span className="flex w-full items-start justify-between gap-1"><strong className="truncate font-mono text-sm text-[var(--fg)]">{slot.code}</strong><span className="text-xs" aria-label={slot.vehicleType} title={slot.vehicleType}>{slot.vehicleType === "MOTORCYCLE" ? "🏍️" : slot.vehicleType === "CAR" ? "🚗" : "🚐"}</span></span>
                    <span className="my-1.5 text-[10px] font-bold uppercase leading-tight" style={{ color: appearance.color }}>{appearance.label}</span>
                    <span className="w-full truncate text-[10px] text-[var(--muted)]" title={location(slot.unitId)}>{location(slot.unitId)}</span>
                  </button>
                })}
              </div>}
          </section>

          {selectedSlot && <div role="dialog" aria-modal="true" aria-label={`Slot ${selectedSlot.code}`} className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4" onMouseDown={(event) => { if (event.target === event.currentTarget) setSelectedSlot(null) }}><div className="w-full max-w-md space-y-3 rounded-xl border border-[var(--border)] bg-[var(--bg)] p-5 shadow-xl"><div className="flex items-center justify-between"><h2 className="text-lg font-semibold">Slot {selectedSlot.code}</h2><button type="button" className="rounded px-2 py-1 text-sm hover:bg-[var(--border)]" onClick={() => setSelectedSlot(null)} aria-label="Close slot details">Close</button></div><dl className="grid grid-cols-2 gap-2 text-sm"><dt className="text-[var(--muted)]">Location</dt><dd>{location(selectedSlot.unitId)}</dd><dt className="text-[var(--muted)]">Vehicle type</dt><dd>{selectedSlot.vehicleType}</dd><dt className="text-[var(--muted)]">Slot type</dt><dd>{selectedSlot.type}</dd><dt className="text-[var(--muted)]">Physical state</dt><dd>{selectedSlot.isPhysicallyOccupied ? "Occupied" : "Empty"}</dd><dt className="text-[var(--muted)]">Operational status</dt><dd>{selectedSlot.operationalStatus}</dd><dt className="text-[var(--muted)]">Reservation state</dt><dd>{selectedSlot.reservationState ?? "None"}</dd></dl></div></div>}

          <details className="group overflow-hidden rounded-xl border border-[var(--border)] bg-[var(--card)]">
            <summary className="flex cursor-pointer list-none items-center justify-between gap-3 px-4 py-4 marker:hidden hover:bg-violet-500/5">
              <span className="flex min-w-0 items-center gap-3"><span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-violet-500/10 text-violet-600 dark:text-violet-300"><UntitledIcon name="shield" size={17} /></span><span><strong className="block text-sm">Capacity and backup settings</strong><span className="mt-0.5 block text-xs text-[var(--muted)]">View inventory and configure backup capacity</span></span></span>
              <UntitledIcon name="chevron-down" size={16} className="shrink-0 text-[var(--muted)] transition-transform group-open:rotate-180" />
            </summary>
            <div className="border-t border-[var(--border)] p-4"><BackupCapacityPanel structure={structure} onChanged={() => { setReload((value) => value + 1); onChanged?.() }} /></div>
          </details>
        </>
      )}
    </section>
  )
}
