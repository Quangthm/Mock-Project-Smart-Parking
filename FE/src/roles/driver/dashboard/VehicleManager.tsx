import { useEffect, useState } from "react"

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
    imageReference: "",
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
        imageReference: form.imageReference,
      }, id ?? undefined)
      setId(null)
      setForm({ plate: "", vehicleType: "CAR", imageReference: "" })
      setReload((n) => n + 1)
    } catch (e) {
      setError(e instanceof Error ? e.message : "Cannot save vehicle.")
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
                imageReference: v.imageReference ?? "",
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
          <select
            className="input"
            value={form.vehicleType}
            onChange={(e) => setForm({ ...form, vehicleType: e.target.value as VehicleInput["vehicleType"] })}
          >
            <option>CAR</option>
            <option>MOTORCYCLE</option>
          </select>
        </label>
        <label className="label">
          Vehicle image (HTTPS URL)
          <input
            className="input"
            type="url"
            required
            maxLength={2048}
            value={form.imageReference}
            onChange={(e) =>
              setForm({ ...form, imageReference: e.target.value })
            }
          />
        </label>
        <button className="btn-primary" disabled={busy}>
          {id ? "Save vehicle" : "Register vehicle"}
        </button>
      </form>
      <p role="alert">{error}</p>
    </section>
  )
}
