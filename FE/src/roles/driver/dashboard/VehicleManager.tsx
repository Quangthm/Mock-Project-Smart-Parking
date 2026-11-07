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
  type VehicleInput,
} from "../../../lib/vehiclesApi"

export function VehicleManager() {
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
    vehiclesApi.list()
      .then((items) => {
        if (active) setVehicles(items)
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
  }, [reload])

  async function save(e: React.FormEvent) {
    e.preventDefault()
    if (!isSupportedVietnamesePlate(form.plate, form.vehicleType)) {
      setError("Use a supported Vietnamese plate: 51A-123.45 for cars or 59-B1 123.45 for motorcycles.")
      return
    }
    setBusy(true)
    setError("")
    try {
      await vehiclesApi.save({
        plate: form.plate.trim().toUpperCase(),
        vehicleType: form.vehicleType,
        imageAssetId: form.imageAssetId,
        expectedImageAssetId: form.expectedImageAssetId,
      }, id ?? undefined)
      setId(null)
      setForm({ plate: "", vehicleType: "CAR", imageAssetId: "" })
      setReload((n) => n + 1)
      showUiToast("Vehicle saved", "Your vehicle details are ready to use.")
    } catch (e) {
      const message = e instanceof Error ? e.message : "Cannot save vehicle."
      setError(message)
      showUiToast("Vehicle not saved", message, "error")
    } finally {
      setBusy(false)
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
      <form onSubmit={save}>
        <label className="label">
          Plate
          <input
            className="input"
            required
            maxLength={30}
            value={form.plate}
            onChange={(e) => setForm({ ...form, plate: e.target.value })}
            onBlur={() => setForm((current) => ({
              ...current,
              plate: formatVietnamesePlateValue(current.plate, current.vehicleType),
            }))}
            placeholder={form.vehicleType === "CAR" ? "51A-123.45" : "59-B1 123.45"}
          />
        </label>
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
          <VehicleImageInput key={id ?? `new-${reload}`} vehicleId={id ?? undefined}
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
