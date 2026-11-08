import { useApp } from "../../../context/AppContext"
import { AuthApiError, getSessionGeneration } from "../../../lib/authApi"
import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { useEffect, useState } from "react"
import { showUiToast } from "../../../lib/uiToast"
import { VehicleImageInput } from "../../../components/VehicleImageInput"

import {
  formatVietnamesePlate,
  formatVietnamesePlateValue,
  isSupportedVietnamesePlate,
  vehiclesApi,
  type RegisteredVehicle,
  type RegistrationClaim,
  pendingRegistrationMessage,
  canonicalVietnamesePlate,
  type VehicleInput,
} from "../../../lib/vehiclesApi"

export function VehicleManager() {
  const { user } = useApp()
  const [claims, setClaims] = useState<RegistrationClaim[]>([])
  const [vehicles, setVehicles] = useState<RegisteredVehicle[]>([])
  const [reload, setReload] = useState(0)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState("")
  const [id, setId] = useState<string | null>(null)
  const [form, setForm] = useState<VehicleInput>({
    plate: "",
    vehicleType: "CAR",
    imageAssetId: "",
  })

  useEffect(() => {
    let active = true
    setBusy(true)
    setError("")
    setVehicles([])
    setClaims([])
    Promise.all([vehiclesApi.list(), vehiclesApi.claims()])
      .then(([items, pending]) => {
        if (active) { setVehicles(items); setClaims(pending) }
      })
      .catch((e) => {
        if (active) setError(e.message)
      })
      .finally(() => {
        if (active) setBusy(false)
      })
    return () => {
      active = false
    }
  }, [reload, user])

  useEffect(() => { setId(null); setForm({ plate: "", vehicleType: "CAR", imageAssetId: "" }) }, [user])

  async function save(e: React.FormEvent) {
    e.preventDefault()
    const generation = getSessionGeneration()
    if (!isSupportedVietnamesePlate(form.plate, form.vehicleType)) {
      setError("Use a supported Vietnamese plate: 51A-123.45 for cars or 59-B1 123.45 for motorcycles.")
      return
    }
    setBusy(true)
    setError("")
    try {
      await vehiclesApi.save({
        plate: form.plate,
        vehicleType: form.vehicleType,
        imageAssetId: form.imageAssetId,
        expectedImageAssetId: form.expectedImageAssetId,
      }, id ?? undefined)
      if (getSessionGeneration() !== generation) return
      setId(null)
      setForm({ plate: "", vehicleType: "CAR", imageAssetId: "" })
      setReload((n) => n + 1)
      showUiToast("Vehicle saved", "Your registered vehicle is saved.")
    } catch (e) {
      if (getSessionGeneration() !== generation) return
      if (e instanceof AuthApiError && e.code === "PLATE_EXISTS") {
        try {
          const [items, pending] = await Promise.all([vehiclesApi.list(), vehiclesApi.claims()])
          if (getSessionGeneration() !== generation) return
          setVehicles(items); setClaims(pending)
          setError(pending.some(c => c.canonicalPlate === canonicalVietnamesePlate(form.plate))
            ? pendingRegistrationMessage : "This plate is already registered. Your current vehicles are shown above.")
        } catch { if (getSessionGeneration() === generation) setError("Could not confirm registration status. Please refresh.") }
      } else {
        setError(e instanceof Error ? e.message : "Cannot save vehicle.")
      }
    } finally {
      if (getSessionGeneration() === generation) setBusy(false)
    }
  }

  return (
    <section className="card">
      <h3>Registered vehicles</h3>
      <p>
        Standard five-digit Vietnamese car and motorcycle plates are supported.
        Non-plated parking uses an Operator ticket.
      </p>
      {vehicles.map((v) => (
        <div className="card" key={v.id}>
          <strong>{formatVietnamesePlate(v)}</strong>
          <p>
            {v.vehicleType} · {v.canonicalPlate}
          </p>
          <button
            className="btn-outline"
            disabled={busy}
            onClick={() => {
              setId(v.id)
              setForm({
                plate: v.rawPlate,
                vehicleType: v.vehicleType,
                imageAssetId: v.imageAssetId ?? "",
                expectedImageAssetId: v.imageAssetId,
              })
            }}
          >
            Edit
          </button>
        </div>
      ))}
      {claims.length > 0 && <section aria-label="Pending registrations">
        <h4>Pending registrations</h4>
        {claims.map(claim => <div className="card" key={claim.id}>
          <strong>{claim.rawPlate}</strong><p>{claim.vehicleType}</p>
          <p>{pendingRegistrationMessage}</p>
        </div>)}
      </section>}
      <button type="button" className="btn-outline" disabled={busy} onClick={() => setReload(n => n + 1)}>Refresh registrations</button>
      <form onSubmit={save}>
        <label className="label">
          Plate
          <input
            className="input"
            required
            maxLength={30}
            value={form.plate}
            onChange={(e) => setForm({ ...form, plate: e.target.value })}
            placeholder={form.vehicleType === "CAR" ? "51A-123.45" : "59-B1 123.45"}
          />
        </label>
        <p>Plate preview: {formatVietnamesePlateValue(form.plate, form.vehicleType)}</p>
        <label className="label">
          Vehicle type
          <RoundedSelect
            className="input"
            value={form.vehicleType}
            onChange={(e) => setForm({ ...form, vehicleType: e.target.value as VehicleInput["vehicleType"] })}
          >
            <option>CAR</option>
            <option>MOTORCYCLE</option>
          </RoundedSelect>
        </label>
        <div className="label">
          Vehicle image
          <VehicleImageInput key={`${getSessionGeneration()}-${id ?? `new-${reload}`}`} vehicleId={id ?? undefined}
            onUploaded={assetId => setForm(current => ({ ...current, imageAssetId: assetId }))} />
        </div>
        <button className="btn-primary" disabled={busy || !form.imageAssetId}>
          {id ? "Save vehicle" : "Register vehicle"}
        </button>
      </form>
      <p role="alert">{error}</p>
    </section>
  )
}
