import { useEffect, useRef, useState } from "react"
import { GroupedNumberInput } from "../../../components/forms/GroupedNumberInput"
import { PageHeader } from "../../../components/ui/PageHeader"
import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { getSessionGeneration } from "../../../lib/authApi"
import { parkingApi, type Site, type Unit } from "../../../lib/parkingApi"
import { passPlansApi, passVehicleLabels, passVehicleTypes, type PassPlan, type PassVehicleType } from "../../../lib/passPlansApi"

export function OwnerPassPlans({ sites }: { sites: Site[] }) {
  const [selectedId, setSelectedId] = useState("")
  const siteId = sites.find(site => site.id === selectedId)?.id ?? sites[0]?.id

  return <div className="mx-auto grid max-w-4xl gap-4">
    <PageHeader title="Parking passes" description="Publish a monthly plan, set prices and open it for sale." />
    {siteId ? <>
      <label className="grid max-w-sm gap-1 text-sm font-semibold">
        Parking site
        <RoundedSelect aria-label="Parking site" value={siteId} onChange={event => setSelectedId(event.target.value)}>
          {sites.map(site => <option key={site.id} value={site.id}>{site.name}</option>)}
        </RoundedSelect>
      </label>
      <MonthlyPlan key={`${getSessionGeneration()}:${siteId}`} siteId={siteId} />
    </> : <p role="status">No parking sites are available.</p>}
  </div>
}

function MonthlyPlan({ siteId }: { siteId: string }) {
  const [plan, setPlan] = useState<PassPlan | null>(null)
  const [zones, setZones] = useState<Unit[]>([])
  const [zonesUnavailable, setZonesUnavailable] = useState(false)
  const [zoneId, setZoneId] = useState("")
  const [excluded, setExcluded] = useState<string[]>([])
  const [vehicles, setVehicles] = useState<PassVehicleType[]>([])
  const [day, setDay] = useState(true)
  const [night, setNight] = useState(true)
  const [prices, setPrices] = useState<Partial<Record<PassVehicleType, string>>>({})
  const [busy, setBusy] = useState(true)
  const [dirty, setDirty] = useState(false)
  const [error, setError] = useState("")
  const [notice, setNotice] = useState("")
  const running = useRef(false)
  const mounted = useRef(false)
  const generation = useRef(getSessionGeneration())
  const keys = useRef<Partial<Record<"publish" | "prices", { payload: string; key: string }>>>({})
  const current = () => mounted.current && generation.current === getSessionGeneration()

  function keyFor(action: "publish" | "prices", payload: string) {
    const previous = keys.current[action]
    if (previous?.payload === payload) return previous.key
    const key = crypto.randomUUID()
    keys.current[action] = { payload, key }
    return key
  }

  async function read(fillForm: boolean) {
    const result = await passPlansApi.list(siteId)
    const monthly = result.plans.find(item => item.productType === "MONTHLY")
    if (!monthly) throw new Error("The service did not return a monthly plan.")
    if (!current()) return
    setPlan(monthly)
    if (fillForm) {
      setZoneId(monthly.zoneId ?? "")
      setVehicles(monthly.vehicleTypes)
      setExcluded(monthly.ordinaryCoverageRules?.excludedZoneIds ?? [])
      setDay(monthly.ordinaryCoverageRules?.dayCovered ?? true)
      setNight(monthly.ordinaryCoverageRules?.nightCovered ?? true)
      setDirty(false)
    }
  }

  async function run(action: () => Promise<void>) {
    if (running.current || !current()) return
    running.current = true
    setBusy(true)
    setError("")
    setNotice("")
    try { await action() }
    catch (cause) { if (current()) setError(cause instanceof Error ? cause.message : "Could not update this plan.") }
    finally {
      running.current = false
      if (current()) setBusy(false)
    }
  }

  useEffect(() => {
    mounted.current = true
    let active = true
    void run(() => read(true))
    void parkingApi.structure(siteId).then(structure => {
      if (active && current()) setZones(structure.units.filter(unit => unit.type === "ZONE"))
    }).catch(() => { if (active && current()) setZonesUnavailable(true) })
    return () => { active = false; mounted.current = false }
  }, [siteId])

  async function update(action: () => Promise<unknown>, message: string, fillForm = false) {
    await action()
    if (!current()) return
    setNotice(message)
    try { await read(fillForm) }
    catch {
      if (current()) setPlan(null)
      throw new Error("The change was saved, but the plan could not be refreshed. Reload the plan before making another change.")
    }
  }

  const revision = plan?.activeRevision ?? 0
  const pricesToSave = plan?.vehicleTypes.map(vehicleType => ({ vehicleType, fullPeriodPrice: Number(prices[vehicleType]) })) ?? []
  const validPrices = pricesToSave.length > 0 && pricesToSave.every(item => Number.isSafeInteger(item.fullPeriodPrice) && item.fullPeriodPrice > 0)

  return <section className="card grid gap-4 p-4 sm:p-6" aria-busy={busy}>
    <div className="flex flex-wrap items-start justify-between gap-3">
      <div>
        <h2 className="text-lg font-bold">Monthly pass</h2>
        <p className="text-sm text-[var(--muted)]">{plan?.isEnabled ? "On sale" : "Off sale"} · {revision ? `Published version ${revision}` : "Unpublished"}</p>
      </div>
      <button type="button" className="btn-outline" disabled={busy} onClick={() => void run(() => read(true))}>Reload plan</button>
    </div>
    {busy && <p role="status">Updating plan…</p>}
    {error && <p role="alert" className="text-[var(--badge-danger-strong)]">{error}</p>}
    {notice && <p role="status">{notice}</p>}
    {plan && <>
      <form className="grid gap-3 border-t border-[var(--border)] pt-4" onSubmit={event => {
        event.preventDefault()
        if (!vehicles.length || (!day && !night)) return
        void run(() => update(async () => {
          await passPlansApi.saveDraft(siteId, "MONTHLY", {
            zoneId: zoneId || null, vehicleTypes: vehicles, isAvailableOnly: true,
            ordinaryCoverageRules: { dayCovered: day, nightCovered: night, excludedZoneIds: excluded },
          })
          if (current()) setDirty(false)
          delete keys.current.publish
        }, "Draft saved. Publish it when ready."))
      }}>
        <fieldset disabled={busy} className="grid min-w-0 gap-3">
          <legend className="mb-2 font-semibold">1. Set coverage</legend>
          <p className="text-sm text-[var(--muted)]">The pass can be used when there is a free space. It does not reserve a slot.</p>
          {revision > 0 && plan.hasDraft && <p className="text-sm text-[var(--muted)]">A draft is saved on the server. Reloading shows the published settings until the draft is published.</p>}
          <label className="grid max-w-sm gap-1 text-sm">Zone
            <RoundedSelect aria-label="Coverage zone" value={zoneId} disabled={zonesUnavailable} onChange={event => {
              setZoneId(event.target.value)
              setExcluded(value => value.filter(id => id !== event.target.value))
              setDirty(true)
            }}>
              <option value="">Entire site</option>
              {zoneId && !zones.some(zone => zone.id === zoneId) && <option value={zoneId}>Current zone</option>}
              {zones.map(zone => <option key={zone.id} value={zone.id}>{zone.name}</option>)}
            </RoundedSelect>
          </label>
          {zonesUnavailable && <p className="text-sm text-[var(--muted)]">Zones could not be loaded. The current scope is preserved.</p>}
          {zones.length > 0 && <details><summary className="cursor-pointer text-sm font-medium">Excluded zones</summary>
            <div className="mt-2 flex flex-wrap gap-3">
              {zones.filter(zone => zone.id !== zoneId).map(zone => <label key={zone.id} className="flex items-center gap-2 text-sm">
                <input type="checkbox" checked={excluded.includes(zone.id)} onChange={event => {
                  setExcluded(value => event.target.checked ? [...value, zone.id] : value.filter(id => id !== zone.id))
                  setDirty(true)
                }} />{zone.name}
              </label>)}
            </div>
          </details>}
          <div className="flex flex-wrap gap-4">
            {passVehicleTypes.map(vehicle => <label key={vehicle} className="flex items-center gap-2 text-sm">
              <input type="checkbox" checked={vehicles.includes(vehicle)} onChange={event => {
                setVehicles(value => event.target.checked ? [...value, vehicle] : value.filter(item => item !== vehicle))
                setDirty(true)
              }} />{passVehicleLabels[vehicle]}
            </label>)}
          </div>
          <div className="flex flex-wrap gap-4">
            <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={day} onChange={event => { setDay(event.target.checked); setDirty(true) }} />Daytime</label>
            <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={night} onChange={event => { setNight(event.target.checked); setDirty(true) }} />Overnight</label>
          </div>
          <div className="flex flex-wrap gap-2">
            <button type="submit" className="btn-outline" disabled={!vehicles.length || (!day && !night)}>Save draft</button>
            <button type="button" className="btn-primary" disabled={!plan.hasDraft || dirty} onClick={() => void run(() => update(async () => {
              await passPlansApi.publish(siteId, "MONTHLY", keyFor("publish", String(revision)))
              delete keys.current.publish
              setPrices({})
            }, "Monthly plan published.", true))}>Publish saved draft</button>
          </div>
        </fieldset>
      </form>
      <form className="grid gap-3 border-t border-[var(--border)] pt-4" onSubmit={event => {
        event.preventDefault()
        const { planId } = plan
        if (!planId || !validPrices) return
        void run(() => update(async () => {
          await passPlansApi.saveMonthlyPrices(siteId, planId, revision, pricesToSave, keyFor("prices", JSON.stringify([revision, pricesToSave])))
          delete keys.current.prices
        }, "Monthly prices saved."))
      }}>
        <fieldset disabled={busy || !revision} className="grid min-w-0 gap-3">
          <legend className="mb-2 font-semibold">2. Set monthly prices</legend>
          <p className="text-sm text-[var(--muted)]">{revision ? "Enter a full-period price for every vehicle type in the published plan." : "Publish the plan to set prices."}</p>
          {revision > 0 && <div className="grid gap-3 sm:grid-cols-2">
            {plan.vehicleTypes.map(vehicle => <label key={vehicle} className="grid gap-1 text-sm">
              {passVehicleLabels[vehicle]} (VND)
              <GroupedNumberInput className="input" required value={prices[vehicle] ?? ""}
                onValueChange={digits => setPrices(values => ({ ...values, [vehicle]: digits }))} />
            </label>)}
          </div>}
          <p className="text-sm text-[var(--muted)]">Monthly refund rates follow the platform policy (100% before start, 50% in period, 100% for facility closure, applied to the eligible amount).</p>
          <button type="submit" className="btn-outline justify-self-start" disabled={!validPrices}>Save prices</button>
        </fieldset>
      </form>
      <section className="flex flex-wrap items-center justify-between gap-3 border-t border-[var(--border)] pt-4">
        <div>
          <h3 className="font-semibold">3. Sales availability</h3>
          <p className="text-sm text-[var(--muted)]">{plan.financialReadiness.hasCompleteFinancialTerms ? "Financial terms are ready." : "Publish a plan and save prices before turning on sales."}</p>
        </div>
        <button type="button" className={plan.isEnabled ? "btn-outline" : "btn-primary"}
          disabled={busy || !revision || (!plan.isEnabled && !plan.financialReadiness.hasCompleteFinancialTerms)}
          onClick={() => void run(() => update(
            () => passPlansApi.setEnabled(siteId, "MONTHLY", !plan.isEnabled, revision),
            plan.isEnabled ? "Monthly sales turned off." : "Monthly pass is now on sale.",
          ))}>{plan.isEnabled ? "Turn off sales" : "Turn on sales"}</button>
      </section>
    </>}
    <p className="text-sm text-[var(--muted)]">Annual pass sales are blocked pending approval (Q05).</p>
  </section>
}
