import { useEffect, useRef, useState } from "react"
import { useApp } from "../../../context/AppContext"
import { AuthApiError, getSessionGeneration } from "../../../lib/authApi"
import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { showUiToast } from "../../../lib/uiToast"
import { SavedVehicleImage, VehicleImageInput } from "../../../components/VehicleImageInput"

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
  const [busy, setBusy] = useState(true)
  const [error, setError] = useState("")
  const [removalPlate, setRemovalPlate] = useState("")
  const removalDialog = useRef<HTMLDialogElement>(null)
  const [form, setForm] = useState<VehicleInput>({
    plate: "",
    vehicleType: "CAR",
    imageAssetId: "",
  })

  useEffect(() => {
    let active = true
    setBusy(true)
    setError("")
    removalDialog.current?.close()
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

  useEffect(() => { setForm({ plate: "", vehicleType: "CAR", imageAssetId: "" }) }, [user])

  async function save(e: React.FormEvent) {
    e.preventDefault()
    if (busy || !form.imageAssetId) return
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
      })
      if (getSessionGeneration() !== generation) return
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
    <section aria-label="Vehicle registration">

      <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1.1fr)_minmax(0,1fr)]">
        <form className="min-w-0 space-y-5 rounded-2xl bg-[var(--card)] p-4 sm:p-6" onSubmit={save}>
          <header>
            <h3 className="text-lg font-semibold">Register a Vehicle</h3>
            <p className="mt-1 text-sm text-[var(--muted)]">
              Add the plate, vehicle type, and a clear photo.
            </p>
          </header>
          <div className="grid gap-3 sm:grid-cols-[minmax(0,1.25fr)_minmax(0,1fr)]">
            <label className="label min-w-0">
              License plate
              <input className="input mt-1 w-full" name="plate" autoComplete="off"
                spellCheck={false} autoCapitalize="characters" required maxLength={30} value={form.plate}
                onChange={event => setForm(current => ({ ...current, plate: event.target.value }))}
                placeholder={form.vehicleType === "CAR" ? "e.g. 51A-123.45…" : "e.g. 59-B1 123.45…"}
                aria-describedby="driver-vehicle-plate-preview" />
              <span id="driver-vehicle-plate-preview" className="mt-1.5 block text-sm font-normal text-[var(--muted)]">
                {isSupportedVietnamesePlate(form.plate, form.vehicleType)
                  ? `Plate preview · ${formatVietnamesePlateValue(form.plate, form.vehicleType)}`
                  : "Use a Vietnamese five-digit plate."}
              </span>
            </label>
            <label className="label min-w-0">
              Vehicle type
              <RoundedSelect className="input mt-1 w-full" name="vehicleType" value={form.vehicleType}
                onChange={event => setForm({ ...form, vehicleType: event.target.value as VehicleInput["vehicleType"] })}>
                <option value="CAR">Car</option>
                <option value="MOTORCYCLE">Motorcycle</option>
              </RoundedSelect>
            </label>
          </div>

          <section aria-label="Vehicle photo" className="space-y-3 pt-2">
            <h4 className="font-semibold">Vehicle Photo</h4>
            <VehicleImageInput key={`${getSessionGeneration()}-new-${reload}`}
              showEmptyPreview disabled={busy}
              onUploaded={assetId => setForm(current => ({ ...current, imageAssetId: assetId }))} />
          </section>

          {error && <p role="alert" className="rounded-xl bg-[var(--bg)] p-3 text-sm">{error}</p>}
          <div className="flex flex-wrap gap-2 pt-2">
            <button className="btn-primary min-h-11" disabled={busy || !form.imageAssetId}>
              {busy ? "Please Wait…" : "Register Vehicle"}
            </button>
          </div>
        </form>

        <div className="min-w-0 space-y-5">
          <section className="space-y-3" aria-label="Active vehicles">
            <header className="flex flex-wrap items-center justify-between gap-2">
              <h3 className="text-lg font-semibold">Active Vehicles</h3>
              <button type="button" className="btn-outline min-h-11" disabled={busy}
                onClick={() => setReload(n => n + 1)}>Refresh</button>
            </header>
            {busy && !vehicles.length && <p role="status" className="text-sm">Loading vehicles…</p>}
            {!busy && !vehicles.length && !error &&
              <div className="rounded-xl bg-[var(--card)] p-4">
                <p className="font-medium">No vehicles yet</p>
                <p className="mt-1 text-sm text-[var(--muted)]">Add your first vehicle to see it here.</p>
              </div>}
            {vehicles.length > 0 && <ul className="space-y-2">
              {vehicles.map(vehicle => (
                <li className="flex flex-wrap items-center gap-3 rounded-xl bg-[var(--card)] p-3" key={vehicle.id}>
                  <div className="w-28 shrink-0">
                    <SavedVehicleImage key={`${getSessionGeneration()}-${vehicle.imageAssetId}`} assetId={vehicle.imageAssetId}
                      plate={formatVietnamesePlate(vehicle)} compact />
                  </div>
                  <div className="min-w-[8rem] flex-1">
                    <strong className="break-words tabular-nums" translate="no">{formatVietnamesePlate(vehicle)}</strong>
                    <p className="text-sm text-[var(--muted)]">{vehicle.vehicleType === "CAR" ? "Car" : "Motorcycle"}</p>
                  </div>
                  <button type="button" className="btn-danger min-h-11" disabled={busy}
                    aria-label={`Remove vehicle ${formatVietnamesePlate(vehicle)}`} onClick={() => {
                      setRemovalPlate(formatVietnamesePlate(vehicle))
                      removalDialog.current?.showModal()
                    }}>Remove</button>
                </li>
              ))}
            </ul>}
          </section>
          {claims.length > 0 && <section className="space-y-3" aria-label="Pending registrations">
            <h3 className="font-semibold">Pending registrations</h3>
            {claims.map(claim => <article className="card space-y-2" key={claim.id}>
              <strong>{formatVietnamesePlateValue(claim.rawPlate, claim.vehicleType)}</strong>
              <p className="rounded-lg bg-amber-100 p-2 text-sm text-amber-950">{pendingRegistrationMessage}</p>
            </article>)}
          </section>}
        </div>
      </div>
      <dialog ref={removalDialog} aria-labelledby="vehicle-removal-title" aria-describedby="vehicle-removal-description"
        className="fixed inset-0 m-auto w-[calc(100%-2rem)] max-w-md rounded-2xl border border-[var(--border)] bg-[var(--card)] p-5 text-[var(--fg)] shadow-xl backdrop:bg-black/55 sm:p-6">
        <h3 id="vehicle-removal-title" className="text-lg font-semibold">Remove Vehicle?</h3>
        <p id="vehicle-removal-description" className="mt-2 text-sm text-[var(--muted)]">
          Are you sure you want to remove vehicle <strong translate="no">{removalPlate}</strong>?
        </p>
        <div className="mt-5 flex flex-wrap justify-end gap-2">
          <button type="button" className="btn-outline min-h-11" onClick={() => removalDialog.current?.close()}>Cancel</button>
          <button type="button" className="btn-danger min-h-11" onClick={() => {
            removalDialog.current?.close()
            // TODO(SPARK-243): Connect removal once UserService implements and documents DELETE.
            showUiToast("Vehicle removal unavailable", `Vehicle ${removalPlate} is still registered.`, "info")
          }}>Remove</button>
        </div>
      </dialog>
    </section>
  )
}
