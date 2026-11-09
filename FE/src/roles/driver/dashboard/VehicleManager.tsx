import { useEffect, useState, type FormEvent } from "react"
import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { VehicleImageUpload } from "../../../components/forms/VehicleImageUpload"
import { useApp } from "../../../context/AppContext"
import { showUiToast } from "../../../lib/uiToast"
import {
  formatVietnamesePlate,
  formatVietnamesePlateValue,
  isSupportedVietnamesePlate,
  listPendingClaims,
  vehicleAccountKey,
  vehiclesApi,
  type RegisteredVehicle,
  type RegistrationClaim,
  type VehicleInput,
} from "../../../lib/vehiclesApi"

const emptyForm: VehicleInput = { plate: "", vehicleType: "CAR" }

export function VehicleManager() {
  const { user } = useApp()
  const accountKey = user ? vehicleAccountKey() : ""
  return accountKey ? <AccountVehicles key={accountKey} accountKey={accountKey} /> : null
}

function AccountVehicles({ accountKey }: { accountKey: string }) {
  const [vehicles, setVehicles] = useState<RegisteredVehicle[]>([])
  const [claims, setClaims] = useState<RegistrationClaim[]>([])
  const [reload, setReload] = useState(0)
  const [busy, setBusy] = useState(true)
  const [error, setError] = useState("")
  const [editing, setEditing] = useState<RegisteredVehicle>()
  const [form, setForm] = useState(emptyForm)
  const [image, setImage] = useState<string | null>()
  const [uploadVersion, setUploadVersion] = useState(0)
  const [listFailed, setListFailed] = useState(false)

  useEffect(() => {
    let active = true
    setBusy(true)
    setListFailed(false)
    setError("")
    Promise.allSettled([vehiclesApi.listForRegistration(accountKey), listPendingClaims(accountKey)])
      .then(([items, pending]) => {
        if (!active || accountKey !== vehicleAccountKey()) return
        if (items.status === "fulfilled") setVehicles(items.value.filter(vehicle => vehicle.status === "ACTIVE"))
        else {
          setError("Cannot load active vehicles. Refresh to try again.")
          setListFailed(true)
        }
        if (pending.status === "fulfilled") setClaims(pending.value)
        setBusy(false)
      })
    return () => { active = false }
  }, [accountKey, reload])

  function reset(vehicle?: RegisteredVehicle) {
    setEditing(vehicle)
    setForm(vehicle ? { plate: vehicle.rawPlate, vehicleType: vehicle.vehicleType } : emptyForm)
    setImage(undefined)
    setUploadVersion(version => version + 1)
    setError("")
  }

  function save(event: FormEvent) {
    event.preventDefault()
    if (busy || listFailed || image === null || (!editing && !image)) return
    if (!isSupportedVietnamesePlate(form.plate, form.vehicleType)) {
      setError("Use a supported Vietnamese plate: 51A-123.45 for cars or 59-B1 123.45 for motorcycles.")
      return
    }
    try {
      const result = vehiclesApi.save(accountKey, { ...form, imageAssetId: image }, vehicles, editing)
      reset()
      if (result.status === "PENDING_DISPUTE") {
        setClaims(current => current.some(claim => claim.id === result.id) ? current : [...current, result])
      } else {
        setVehicles(current => [...current.filter(vehicle => vehicle.id !== result.id), result])
        showUiToast("Vehicle saved", "Saved in this demo session.")
      }
    } catch (error) {
      setError(error instanceof Error ? error.message : "Cannot save vehicle.")
    }
  }

  return (
    <section className="card space-y-6">
      <header>
        <h3>Registered vehicles</h3>
        <p className="text-sm text-[var(--muted)]">Vehicle registration changes are saved in this demo session.</p>
      </header>
      <section className="space-y-3">
        <h4>Active Vehicles</h4>
        {busy && <p role="status">Loading vehicles…</p>}
        {!busy && !listFailed && !vehicles.length && <p>No active vehicles yet.</p>}
        {vehicles.map(vehicle => (
          <div className="card flex flex-wrap items-center justify-between gap-3" key={vehicle.id}>
            <div><strong>{formatVietnamesePlate(vehicle)}</strong><p>{vehicle.vehicleType}</p></div>
            <button type="button" className="btn-outline" disabled={busy || listFailed} onClick={() => reset(vehicle)}>Edit</button>
          </div>
        ))}
      </section>
      <section className="space-y-3">
        <h4>Pending Claims</h4>
        {!busy && !claims.length && <p>No pending claims.</p>}
        {claims.map(claim => (
          <article className="card space-y-2" key={claim.id}>
            <strong>{formatVietnamesePlateValue(claim.plate, claim.vehicleType)}</strong>
            <p>{claim.vehicleType} · {new Date(claim.submittedAt).toLocaleDateString("en-GB")}</p>
            {claim.imageThumbnailUrl && <img className="max-h-24 rounded-lg" src={claim.imageThumbnailUrl} alt="Submitted vehicle" />}
            <span className="inline-block rounded-lg bg-amber-100 px-3 py-2 text-sm text-amber-950">
              Registration pending dispute review. This vehicle can’t be used for booking.
            </span>
          </article>
        ))}
      </section>
      <form className="grid min-w-0 gap-3 sm:grid-cols-2" onSubmit={save}>
        <h4 className="col-span-full">{editing ? "Edit vehicle" : "Register a vehicle"}</h4>
        <label className="label">
          Plate
          <input className="input" required maxLength={30} value={form.plate}
            onChange={event => setForm({ ...form, plate: event.target.value })}
            placeholder={form.vehicleType === "CAR" ? "51A-123.45" : "59-B1 123.45"} />
          {isSupportedVietnamesePlate(form.plate, form.vehicleType) &&
            <small className="block">Plate preview: {formatVietnamesePlateValue(form.plate, form.vehicleType)}</small>}
        </label>
        <label className="label">
          Vehicle type
          <RoundedSelect className="input" value={form.vehicleType}
            onChange={event => setForm({ ...form, vehicleType: event.target.value as VehicleInput["vehicleType"] })}>
            <option>CAR</option>
            <option>MOTORCYCLE</option>
          </RoundedSelect>
        </label>
        <VehicleImageUpload key={uploadVersion} onReady={setImage} editing={!!editing} disabled={busy || listFailed} />
        <div className="col-span-full flex flex-wrap gap-2">
          <button className="btn-primary" disabled={busy || listFailed || image === null || (!editing && !image)}>
            {editing ? "Save vehicle" : "Register vehicle"}
          </button>
          {editing && <button type="button" className="btn-outline" onClick={() => reset()}>Cancel edit</button>}
          {listFailed && <button type="button" className="btn-outline" onClick={() => setReload(value => value + 1)}>Refresh vehicles</button>}
        </div>
        {error && <p className="col-span-full" role="alert">{error}</p>}
      </form>
    </section>
  )
}
