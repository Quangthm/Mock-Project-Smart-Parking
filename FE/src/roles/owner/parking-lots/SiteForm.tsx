import { useState } from "react"
import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { GroupedNumberInput } from "../../../components/forms/GroupedNumberInput"
import { CITIES, DISTRICTS_BY_PROVINCE } from "../../../lib/vietnamAdministrativeUnits"
import type { Site } from "../../../lib/parkingApi"
import type { LotType } from "../../../lib/types"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { ParkingLocationPicker } from "../../../components/ui/ParkingLocationPicker"
import { useSiteForm, LOT_TYPE_INFO, DEVICE_OPTIONS } from "./useSiteForm"

export function SiteForm({ site, onSaved, onCancel }: {
  site?: Site
  onSaved: (site: Site) => void
  onCancel: () => void
}) {
  const {
    formId, hasSavedSetup, lotType, chooseLotType, selectedCity, district, setDistrict,
    streetAddress, setStreetAddress, addressChanged, isManualCode, form, setForm, locationConfirmed, markAddressChanged, setConfirmedLocation,
    devices, setDevices, busy, error, computedCode, fullAddress, modelCapacity, modelError, customModelFileName, uploadModel,
    handleCityChange, handleNameChange, submit, cancel,
  } = useSiteForm(site, onSaved, onCancel)
  const [otherDistrict, setOtherDistrict] = useState(() => Boolean(district && !DISTRICTS_BY_PROVINCE[selectedCity]?.includes(district)))
  const [pickerOpen, setPickerOpen] = useState(false)
  const districtOptions = DISTRICTS_BY_PROVINCE[selectedCity] ?? []

  return (
    <form className="space-y-5 text-[var(--fg)] max-h-[85dvh] overflow-y-auto pr-1" onSubmit={submit}>
      {/* Header */}
      <div className="border-b border-[var(--border)] pb-3">
        <h3 className="text-lg font-bold text-[var(--fg)] flex items-center gap-2">
          <span>{site ? "Edit Parking Site" : "Create Parking Site"}</span>
          <span className="text-xs px-2 py-0.5 rounded-full bg-[var(--primary)]/10 text-[var(--primary)] font-semibold">
            Smart Facility
          </span>
        </h3>
        <p className="text-xs text-[var(--muted)] mt-1">
          {site
            ? "Update the site type, layout, identification, and operating equipment."
            : "Create a parking site and review its live model and capacity in Parking Structure."}
        </p>
      </div>

      <fieldset disabled={busy} className="space-y-4.5" style={{ border: 0, padding: 0, margin: 0 }}>
        {site && !hasSavedSetup && <p role="status" className="rounded-xl border border-[var(--border)] p-3 text-sm text-[var(--muted)]">This browser has no saved setup for this site. Only the site name and address can be edited here; type, 3D model and pricing cannot be restored from the current Owner API.</p>}
        {/* Section 1: Choose Lot Type (Giống form Owner) */}
        {(!site || hasSavedSetup) && <>
        <div className="space-y-2">
          <label className="label font-semibold text-xs">Parking Structure Type *</label>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-2.5">
            {(Object.entries(LOT_TYPE_INFO) as [LotType, (typeof LOT_TYPE_INFO)[LotType]][]).map(
              ([type, info]) => {
                const selected = lotType === type
                return (
                  <button
                    key={type}
                    type="button"
                    aria-pressed={selected}
                    onClick={() => chooseLotType(type)}
                    className={`p-3 rounded-xl border-2 cursor-pointer text-left transition-[border-color,background-color,transform] duration-200 hover:-translate-y-0.5 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--primary)] flex flex-col justify-between ${
                      selected
                        ? "border-[var(--primary)] bg-[var(--primary)]/10 text-[var(--fg)] shadow-xs"
                        : "border-[var(--border)] bg-[var(--bg)] text-[var(--muted)] hover:border-[var(--border)]/80"
                    }`}
                  >
                    <div className="flex items-center justify-between mb-1.5">
                      <span className="text-[var(--primary)]"><UntitledIcon name={info.icon} size={22} /></span>
                      {selected && (
                        <span className="text-[var(--primary)]">
                          <UntitledIcon name="check" size={14} />
                        </span>
                      )}
                    </div>
                    <div>
                      <div className="font-semibold text-xs text-[var(--fg)]">{info.label}</div>
                      <div className="text-[10px] text-[var(--muted)] line-clamp-1">{info.desc}</div>
                    </div>
                  </button>
                )
              },
            )}
          </div>
        </div>
        </>}

        {/* Section 2: Site Identification with Smart Code Logic */}
        <div className="rounded-xl border border-[var(--border)] bg-[var(--bg)]/50 p-4 space-y-3.5">
          <div className="font-semibold text-xs text-[var(--fg)] flex items-center justify-between">
            <span>Site Identification</span>
          </div>

          <div className="grid gap-3">
            <div>
              <label className="label" htmlFor={`${formId}-site-name`}>
                Site Name *
              </label>
              <input
                id={`${formId}-site-name`}
                className="input"
                maxLength={255}
                placeholder="e.g. Vincom Center Dong Khoi"
                value={form.name}
                onChange={(e) => handleNameChange(e.target.value)}
                required
              />
            </div>

          </div>
        </div>

        {/* Section 3: Structured Address */}
        <div className="rounded-xl border border-[var(--border)] bg-[var(--bg)]/50 p-4 space-y-3.5">
          <div className="font-semibold text-xs text-[var(--fg)]">
            Location & Coordinates
          </div>

          <div className="grid gap-3 sm:grid-cols-[minmax(0,0.85fr)_minmax(0,1.15fr)]">
            <div>
              <label className="label" htmlFor={`${formId}-city`}>
                Province *
              </label>
              <RoundedSelect
                id={`${formId}-city`}
                className="input"
                value={selectedCity}
                onChange={(e) => { setOtherDistrict(false); setPickerOpen(false); handleCityChange(e.target.value) }}
              >
                {selectedCity === "LEGACY" && <option value="LEGACY" disabled>Existing address — choose a current province or city to update</option>}
                {CITIES.map((c) => (
                  <option key={c.code} value={c.code}>
                    {c.code === "HCM" ? "TP.HCM" : c.name}
                  </option>
                ))}
              </RoundedSelect>
            </div>

            <div>
              <label className="label" htmlFor={`${formId}-district`}>
                District *
              </label>
              {districtOptions.length > 0 ? <>
                <RoundedSelect id={`${formId}-district`} className="input" value={otherDistrict ? "__other__" : district}
                  required={!site || addressChanged}
                  onChange={(event) => {
                    const isOther = event.target.value === "__other__"
                    setOtherDistrict(isOther)
                    setDistrict(isOther ? "" : event.target.value)
                    markAddressChanged()
                    setPickerOpen(false)
                  }}>
                  <option value="" disabled>Select a district</option>
                  {districtOptions.map(districtName => <option key={districtName} value={districtName}>{districtName}</option>)}
                  <option value="__other__">Other district / area</option>
                </RoundedSelect>
                {otherDistrict && <input className="input mt-2" aria-label="Other district or area" placeholder="Enter district or area"
                  value={district} onChange={event => { setDistrict(event.target.value); markAddressChanged(); setPickerOpen(false) }}
                  maxLength={120} required={!site || addressChanged} />}
              </> : <input id={`${formId}-district`} className="input" placeholder="Enter a district"
                value={district} onChange={event => { setDistrict(event.target.value); markAddressChanged(); setPickerOpen(false) }}
                maxLength={120} required={!site || addressChanged} />}
            </div>
          </div>

          <div>
            <label className="label" htmlFor={`${formId}-street`}>
              Street Address / Complex *
            </label>
            <input
              id={`${formId}-street`}
              className="input"
              maxLength={500}
              placeholder="e.g. 72 Le Thanh Ton Street"
              value={streetAddress}
              onChange={(e) => { setStreetAddress(e.target.value); markAddressChanged(); setPickerOpen(false) }}
              required
            />
          </div>

          {/* Full address summary preview */}
          <div className="p-2.5 rounded-lg border border-[var(--border)] bg-[var(--card)] text-xs text-[var(--muted)] flex items-start gap-2">
            <UntitledIcon name="map-pin" size={14} className="text-[var(--primary)] mt-0.5 shrink-0" />
            <div className="leading-relaxed">
              <span className="font-semibold text-[var(--fg)]">Full address: </span>
              <span>{fullAddress || "Address is incomplete"}</span>
            </div>
          </div>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <p className="text-sm text-[var(--muted)]">{locationConfirmed ? `Entrance pin confirmed · ${form.latitude.toFixed(6)}, ${form.longitude.toFixed(6)}` : "Confirm the exact entrance pin so Drivers see this site in the right place."}</p>
            <button type="button" className="btn-outline" onClick={() => setPickerOpen(open => !open)}>{pickerOpen ? "Close map" : locationConfirmed ? "Move entrance pin" : "Set entrance pin"}</button>
          </div>
          {pickerOpen && <ParkingLocationPicker
            address={fullAddress}
            center={CITIES.find(city => city.code === selectedCity) ? { lat: CITIES.find(city => city.code === selectedCity)!.defaultLat, lng: CITIES.find(city => city.code === selectedCity)!.defaultLng } : { lat: form.latitude, lng: form.longitude }}
            initial={locationConfirmed ? { lat: form.latitude, lng: form.longitude } : null}
            onConfirm={point => { setConfirmedLocation(point); setPickerOpen(false) }}
            onCancel={() => setPickerOpen(false)}
          />}
        </div>

        {/* Section 4: Physical Capacity & Structure */}
        {(!site || hasSavedSetup) && <>
        <div className="rounded-xl border border-[var(--border)] bg-[var(--bg)]/50 p-4 space-y-3.5">
          <div className="font-semibold text-xs text-[var(--fg)]">
            Capacity & Levels
          </div>
          <label className="block text-xs text-[var(--muted)]">3D model for car capacity<input className="input mt-2" type="file" accept=".glb,model/gltf-binary" onChange={event => { uploadModel(event.target.files?.[0]); event.target.value = "" }} /><span className="mt-1 block">{customModelFileName ? `Owner model: ${customModelFileName}` : "Using the supplied model for this parking type."}</span></label>
          {modelError && <p role="alert" className="text-xs text-[var(--badge-danger-strong)]">{modelError}</p>}

          <div
            className={`grid gap-3 ${
              LOT_TYPE_INFO[lotType].hasFloors ? "sm:grid-cols-2" : "grid-cols-1"
            }`}
          >
            <div>
              <label className="label" htmlFor={`${formId}-slots`}>
                Planned Car Spaces
              </label>
              <input
                id={`${formId}-slots`}
                className="input"
                type="number"
                value={modelCapacity.capacity ?? site?.totalPhysicalCapacity ?? ""}
                placeholder="—"
                readOnly
                aria-readonly="true"
              />
              <p className="mt-1 text-xs text-[var(--muted)]">
                Capacity is counted from the slots you register. Set per-area limits in Parking Structure.
              </p>
              {modelCapacity.error && (
                <p role="status" className="mt-1 text-xs text-[var(--muted)]">{modelCapacity.error}</p>
              )}
            </div>

            {LOT_TYPE_INFO[lotType].hasFloors && (
              <div>
                <label className="label" htmlFor={`${formId}-floors`}>
                  Number of Levels *
                </label>
                <input
                  id={`${formId}-floors`}
                  className="input"
                  type="number"
                  min={1}
                  max={20}
                  value={form.floors}
                  onChange={(e) => setForm((c) => ({ ...c, floors: e.target.value }))}
                  required
                />
                {modelCapacity.capacity !== null && parseInt(form.floors, 10) > 0 && (
                  <p className="mt-1 text-[11px] text-[var(--muted)]">
                    ~ {Math.ceil(modelCapacity.capacity / parseInt(form.floors, 10))} spaces / level
                  </p>
                )}
              </div>
            )}
          </div>
        </div>

        {/* Section 5: Installed IoT Devices */}
        <div className="rounded-xl border border-[var(--border)] bg-[var(--bg)]/50 p-4 space-y-3">
          <div className="font-semibold text-xs text-[var(--fg)]">
            Installed IoT Devices
          </div>
          <p className="text-[10px] text-[var(--muted)]">
            Saved to this Owner profile. The Facility service does not store installed devices yet.
          </p>
          <div className="grid gap-2 sm:grid-cols-2">
            {DEVICE_OPTIONS.map((dev) => {
              const checked = Boolean(devices[dev.type])
              return (
                <label
                  key={dev.type}
                  className={`flex items-start gap-2.5 p-2 rounded-lg border cursor-pointer transition-colors ${
                    checked
                      ? "border-[var(--primary)]/50 bg-[var(--card)]"
                      : "border-[var(--border)] bg-[var(--card)]/50 opacity-75"
                  }`}
                >
                  <input
                    type="checkbox"
                    className="mt-0.5 accent-[var(--primary)]"
                    checked={checked}
                    onChange={(e) =>
                      setDevices((cur) => ({ ...cur, [dev.type]: e.target.checked }))
                    }
                  />
                  <div>
                    <div className="text-xs font-semibold text-[var(--fg)]">{dev.label}</div>
                    <div className="text-[10px] text-[var(--muted)]">{dev.desc}</div>
                  </div>
                </label>
              )
            })}
          </div>
        </div>

        {/* Section 6: Base Pricing & Active Status */}
        <div className="rounded-xl border border-[var(--border)] bg-[var(--bg)]/50 p-4 space-y-3.5">
          <div className="flex items-center justify-between">
            <span className="font-semibold text-xs text-[var(--fg)]">
              Base Pricing & Site Status
            </span>
            <label className="flex items-center gap-2 cursor-pointer text-xs">
              <input
                type="checkbox"
                className="accent-emerald-500"
                checked={form.isActive}
                onChange={(e) => setForm((c) => ({ ...c, isActive: e.target.checked }))}
              />
              <span className={`font-semibold ${form.isActive ? "text-emerald-600 dark:text-emerald-400" : "text-[var(--muted)]"}`}>
                {form.isActive ? "Active" : "Inactive"}
              </span>
            </label>
          </div>

          <p className="text-[10px] text-[var(--muted)]">
            Site status is saved to the Facility service. Base rates are kept in this Owner profile (not persisted by the Facility service).
          </p>

          <div className="grid gap-3 sm:grid-cols-2">
            <div>
              <label className="label" htmlFor={`${formId}-hourly-rate`}>
                Hourly Rate (VND / hour)
              </label>
              <GroupedNumberInput
                id={`${formId}-hourly-rate`}
                className="input"
                step={1000}
                value={String(form.hourlyRate)}
                onValueChange={(digits) => setForm((c) => ({ ...c, hourlyRate: Number(digits) }))}
              />
            </div>
            <div>
              <label className="label" htmlFor={`${formId}-daily-rate`}>
                Daily Rate (VND / day)
              </label>
              <GroupedNumberInput
                id={`${formId}-daily-rate`}
                className="input"
                step={1000}
                value={String(form.dailyRate)}
                onValueChange={(digits) => setForm((c) => ({ ...c, dailyRate: Number(digits) }))}
              />
            </div>
            {([
              ["nightRate", "Night Rate (VND)", 1000],
              ["gracePeriodMinutes", "Grace Period (minutes)", 1],
              ["subscriptionMonthly", "Monthly Pass (VND)", 1000],
              ["subscriptionYearly", "Yearly Pass (VND)", 1000],
            ] as const).map(([key, label, step]) => <div key={key}>
              <label className="label" htmlFor={`${formId}-${key}`}>{label}</label>
              {key === "gracePeriodMinutes"
                ? <input id={`${formId}-${key}`} className="input" type="number" step={step} min={0}
                    value={form[key]} onChange={event => setForm(current => ({ ...current, [key]: Number(event.target.value) }))} />
                : <GroupedNumberInput id={`${formId}-${key}`} className="input" step={step}
                    value={String(form[key])} onValueChange={digits => setForm(current => ({ ...current, [key]: Number(digits) }))} />}
            </div>)}
          </div>
        </div>

        {error && (
          <div role="alert" className="flex items-center gap-2 rounded-lg border border-red-500/30 bg-red-500/10 p-3 text-xs text-red-600 dark:text-red-400">
            <UntitledIcon name="alert" size={14} />
            <span>{error}</span>
          </div>
        )}

        {/* Buttons */}
        </>}
        <div className="flex items-center justify-end gap-2.5 pt-2 border-t border-[var(--border)]">
          <button type="button" className="btn-outline text-xs px-4 py-2" onClick={cancel} disabled={busy}>
            Cancel
          </button>
          <button type="submit" className="btn-primary text-xs px-5 py-2 font-medium" disabled={busy}>
            {busy ? "Saving…" : site ? "Save Changes" : "Create Parking Site"}
          </button>
        </div>
      </fieldset>
    </form>
  )
}
