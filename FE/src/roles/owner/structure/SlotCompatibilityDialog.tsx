import { useMemo, useState, type FormEvent } from "react"
import { Modal } from "../../../components/ui/Modal"
import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { parkingApi, type Slot } from "../../../lib/parkingApi"
import {
  MAX_SLOT_CATEGORIES,
  VEHICLE_CATEGORIES,
  resolveSlotPrimary,
  toggleSlotCategory,
} from "../../../lib/structureRules"
import { StructureFeedback } from "./StructureFeedback"

const labelClass = "space-y-1 text-xs font-medium text-[var(--muted)]"

function storedCategories(slot: Slot): string[] {
  if (slot.vehicleTypes && slot.vehicleTypes.length > 0) return slot.vehicleTypes
  const primary = slot.primaryVehicleType ?? slot.vehicleType
  return primary ? [primary] : []
}

export function SlotCompatibilityDialog({
  siteId,
  slot,
  onClose,
  onChanged,
}: {
  siteId: string
  slot: Slot
  onClose: () => void
  onChanged: () => void
}) {
  const initialCategories = useMemo(() => storedCategories(slot), [slot])
  const [categories, setCategories] = useState<string[]>(initialCategories)
  const [primary, setPrimary] = useState(
    slot.primaryVehicleType ?? (initialCategories.length === 1 ? initialCategories[0] : slot.vehicleType ?? ""),
  )
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>(null)
  const [categoryError, setCategoryError] = useState("")

  const resolution = resolveSlotPrimary(categories, primary || null)
  const blocked =
    slot.isPhysicallyOccupied ||
    Boolean(slot.reservationState && slot.reservationState !== "NONE")

  function toggle(category: string) {
    const next = toggleSlotCategory(categories, category)
    setCategoryError(next.error ?? "")
    setCategories(next.categories)
    if (next.categories.length === 1) {
      setPrimary(next.categories[0])
    } else if (!next.categories.includes(primary)) {
      setPrimary("")
    }
  }

  async function save(event: FormEvent) {
    event.preventDefault()
    if (resolution.error || blocked) return
    setBusy(true)
    setError(null)
    try {
      await parkingApi.edit(siteId, {
        action: "configureSlot",
        resourceId: slot.id,
        slotType: slot.type,
        vehicleTypes: categories,
        primaryVehicleType: resolution.primary,
      })
      setBusy(false)
      onChanged()
      onClose()
    } catch (cause) {
      setBusy(false)
      setError(cause)
    }
  }

  return (
    <Modal title={`Compatibility for ${slot.code}`} onClose={busy ? () => {} : onClose}>
      <form className="space-y-4" onSubmit={save}>
        <div>
          <p className="text-xs text-[var(--muted)]">
            Pick the categories this slot may be allocated to. The primary category is the only pool the slot is counted
            in: a dual-category slot still supplies one allocatable space, never two.
          </p>
        </div>

        <fieldset className="space-y-2" disabled={busy}>
          <legend className="text-xs font-medium text-[var(--muted)]">Vehicle categories (1–{MAX_SLOT_CATEGORIES})</legend>
          <div className="flex flex-wrap gap-2">
            {VEHICLE_CATEGORIES.map((category) => {
              const checked = categories.includes(category)
              return (
                <label
                  key={category}
                  className={`flex cursor-pointer items-center gap-2 rounded-lg border px-3 py-2 text-xs font-semibold ${
                    checked
                      ? "border-[var(--primary)] bg-[var(--primary)]/10 text-[var(--fg)]"
                      : "border-[var(--border)] bg-[var(--bg)] text-[var(--muted)]"
                  }`}
                >
                  <input
                    type="checkbox"
                    checked={checked}
                    onChange={() => toggle(category)}
                    className="size-4 accent-[var(--primary)]"
                  />
                  {category}
                </label>
              )
            })}
          </div>
          {categoryError && (
            <p role="alert" className="text-[11px] text-red-600 dark:text-red-400">
              {categoryError}
            </p>
          )}
        </fieldset>

        <label className={labelClass}>
          <span>Primary category (counted in this pool)</span>
          <RoundedSelect
            value={primary}
            onChange={(event) => setPrimary(event.target.value)}
            disabled={busy || categories.length === 0}
            required
          >
            <option value="">Choose explicitly</option>
            {categories.map((category) => (
              <option key={category} value={category}>
                {category}
              </option>
            ))}
          </RoundedSelect>
          {categories.length === 1 && (
            <span className="block text-[11px] font-normal text-[var(--muted)]">
              A single category is its own primary.
            </span>
          )}
          {resolution.error && (
            <span className="block text-[11px] font-normal text-red-600 dark:text-red-400">{resolution.error}</span>
          )}
        </label>

        <div className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-[11px] text-[var(--muted)]">
          <p>
            Geometry and features come from the lot&apos;s fixed 3D template. They are read-only here; this form cannot
            change slot coordinates or add untyped attributes.
          </p>
          {slot.compatibilitySource === "unclassified" && (
            <p className="mt-1 font-medium text-amber-700 dark:text-amber-300">
              This slot is unclassified: it is not counted in any category pool, and the site reports an inconsistent
              projection until it is classified.
            </p>
          )}
        </div>

        {blocked && (
          <p role="status" className="rounded-lg border border-amber-500/30 bg-amber-500/10 px-3 py-2 text-[11px] text-amber-800 dark:text-amber-200">
            This slot is physically occupied or holds an accepted protection state. Reassigning its categories would
            rewrite an accepted commitment, so the change stays disabled until the slot is free.
          </p>
        )}

        <StructureFeedback error={error} fallback="The slot compatibility change failed." />

        <div className="flex justify-end gap-2">
          <button type="button" className="btn-outline" onClick={onClose} disabled={busy}>
            Cancel
          </button>
          <button
            type="submit"
            className="btn-primary"
            disabled={busy || blocked || Boolean(resolution.error)}
            aria-busy={busy || undefined}
          >
            {busy ? "Saving…" : "Save compatibility"}
          </button>
        </div>
      </form>
    </Modal>
  )
}
