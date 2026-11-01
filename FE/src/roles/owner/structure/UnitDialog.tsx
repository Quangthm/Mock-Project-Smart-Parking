import { useMemo, useState, type FormEvent } from "react"
import { Modal } from "../../../components/ui/Modal"
import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import {
  StructureApiError,
  parkingApi,
  type ReleasedIdentifier,
  type Unit,
} from "../../../lib/parkingApi"
import {
  UNIT_TYPES,
  parentUnitOptions,
  validateUnitName,
  type StructureUnitType,
} from "../../../lib/structureRules"
import { useModelCarCapacity } from "../../../lib/3d/modelSlotCapacity"
import { ReleasedIdentifierDialog } from "./ReleasedIdentifierDialog"
import { StructureFeedback } from "./StructureFeedback"

const inputClass = "input w-full text-sm"
const labelClass = "space-y-1 text-xs font-medium text-[var(--muted)]"

export type UnitDialogModel =
  | {
      mode: "create"
      type: StructureUnitType
      parentId: string | null
    }
  | { mode: "rename"; unit: Unit }
  | { mode: "capacity"; unit: Unit }

export function UnitDialog({
  siteId,
  units,
  modelUrl,
  model,
  onClose,
  onSaved,
}: {
  siteId: string
  units: Unit[]
  modelUrl: string
  model: UnitDialogModel
  onClose: () => void
  onSaved: () => void
}) {
  const editing = model.mode === "create" ? null : model.unit
  const [type, setType] = useState<StructureUnitType>(
    model.mode === "create" ? model.type : model.unit.type,
  )
  const [parentId, setParentId] = useState(
    model.mode === "create" ? (model.parentId ?? "") : "",
  )
  const [name, setName] = useState(editing?.name ?? "")
  const [capacity, setCapacity] = useState(String(editing?.maxCapacity ?? 0))
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>(null)
  const [notice, setNotice] = useState("")
  const [released, setReleased] = useState<ReleasedIdentifier | null>(null)
  const [nameTouched, setNameTouched] = useState(model.mode !== "create")

  const isCreate = model.mode === "create"
  const isRename = model.mode === "rename"
  const isCapacity = model.mode === "capacity"

  const parentOptions = useMemo(
    () => (isCreate ? parentUnitOptions(type, units) : []),
    [isCreate, type, units],
  )

  const effectiveParentId =
    !isCreate || type === "FLOOR" ? null : parentId || null

  const siblings = useMemo(
    () =>
      units.filter(
        (unit) =>
          unit.id !== editing?.id &&
          (isCreate ? unit.parentId === effectiveParentId : unit.parentId === editing?.parentId),
      ),
    [units, editing, isCreate, effectiveParentId],
  )

  const nameError = useMemo(
    () => validateUnitName(name, siblings),
    [name, siblings],
  )
  const showNameError = Boolean(nameError) && (nameTouched || name.trim().length > 0)

  // The site's 3D template is the physical ceiling: a unit cannot declare a layout
  // limit above the number of bays that model actually provides. `0` still means "no
  // layout limit" and is always accepted; a failed model read leaves the limit open
  // and the backend stays the final authority.
  const modelCapacity = useModelCarCapacity(modelUrl)
  const bayLimit = modelCapacity.capacity

  const capacityValue = Number(capacity)
  const capacityError = (() => {
    if (isRename) return null
    if (!Number.isInteger(capacityValue) || capacityValue < 0) {
      return "Enter a non-negative whole number. Zero means no layout limit."
    }
    if (bayLimit !== null && capacityValue > bayLimit) {
      return `This model has ${bayLimit} physical bays, so the layout limit cannot be higher.`
    }
    return null
  })()

  const missingParent = isCreate && type === "BLOCK" && !parentId
  const canSubmit =
    !busy &&
    !nameError &&
    !capacityError &&
    !missingParent &&
    (isCreate ? name.trim().length > 0 : true)

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (!canSubmit) return
    setBusy(true)
    setError(null)
    setNotice("")
    try {
      if (isCreate) {
        await parkingApi.unit(siteId, {
          type,
          name: name.trim(),
          parentId: effectiveParentId,
          capacity: Number(capacity) || 0,
        })
        setBusy(false)
        onSaved()
        return
      }
      if (isRename && model.mode === "rename") {
        await parkingApi.edit(siteId, {
          action: "renameUnit",
          resourceId: model.unit.id,
          name: name.trim(),
        })
      } else if (isCapacity && model.mode === "capacity") {
        await parkingApi.edit(siteId, {
          action: "capacity",
          resourceId: model.unit.id,
          capacity: capacityValue,
        })
      }
      setBusy(false)
      onSaved()
    } catch (cause) {
      setBusy(false)
      if (
        cause instanceof StructureApiError &&
        cause.code === "IDENTIFIER_RELEASED" &&
        cause.released
      ) {
        setReleased(cause.released)
        return
      }
      setError(cause)
    }
  }

  const title = isCreate
    ? "Add floor, zone or block"
    : isRename
      ? `Rename ${editing?.name ?? "unit"}`
      : `Set capacity for ${editing?.name ?? "unit"}`

  return (
    <>
      {!released && (
        <Modal title={title} onClose={busy ? () => {} : onClose}>
        <form className="space-y-4" onSubmit={submit}>
          {isCreate && (
            <div className="grid gap-3 sm:grid-cols-2">
              <label className={labelClass}>
                <span>Type</span>
                <RoundedSelect
                  className={inputClass}
                  value={type}
                  onChange={(event) => {
                    const next = event.target.value as StructureUnitType
                    setType(next)
                    setParentId("")
                    setError(null)
                  }}
                >
                  {UNIT_TYPES.map((option) => (
                    <option key={option} value={option}>
                      {option}
                    </option>
                  ))}
                </RoundedSelect>
              </label>
              {type !== "FLOOR" && (
                <label className={labelClass}>
                  <span>
                    Parent {type === "ZONE" ? "(optional — leave empty for an outdoor root zone)" : "(required)"}
                  </span>
                  <RoundedSelect
                    className={inputClass}
                    value={parentId}
                    onChange={(event) => {
                      setParentId(event.target.value)
                      setError(null)
                    }}
                    required={type === "BLOCK"}
                  >
                    <option value="">{type === "ZONE" ? "Lot root" : "Select a parent"}</option>
                    {parentOptions.map((unit) => (
                      <option key={unit.id} value={unit.id}>
                        {unit.name} ({unit.type})
                      </option>
                    ))}
                  </RoundedSelect>
                </label>
              )}
            </div>
          )}

          {!isCapacity && (
            <label className={labelClass}>
              <span>Name</span>
              <input
                className={inputClass}
                value={name}
                onChange={(event) => {
                  setName(event.target.value)
                  setNameTouched(true)
                  setError(null)
                }}
                maxLength={100}
                aria-invalid={showNameError || undefined}
                aria-describedby={showNameError ? "unit-name-error" : undefined}
                autoFocus
                required
              />
              {showNameError && (
                <span id="unit-name-error" className="block text-[11px] font-normal text-red-600 dark:text-red-400">
                  {nameError}
                </span>
              )}
              {!showNameError && isCreate && siblings.length > 0 && (
                <span className="block text-[11px] font-normal text-[var(--muted)]">
                  Names are unique among live siblings, ignoring case.
                </span>
              )}
            </label>
          )}

          {!isRename && (
            <label className={labelClass}>
              <span>Capacity (0 = no layout limit)</span>
              <input
                className={inputClass}
                type="number"
                min="0"
                max={bayLimit === null ? undefined : bayLimit}
                step="1"
                value={capacity}
                onChange={(event) => {
                  setCapacity(event.target.value)
                  setError(null)
                }}
                aria-invalid={Boolean(capacityError) || undefined}
                aria-describedby={capacityError ? "unit-capacity-error" : "unit-capacity-help"}
                required
                autoFocus={isCapacity}
              />
              {capacityError ? (
                <span id="unit-capacity-error" className="block text-[11px] font-normal text-red-600 dark:text-red-400">
                  {capacityError}
                </span>
              ) : (
                <span id="unit-capacity-help" className="block text-[11px] font-normal text-[var(--muted)]">
                  This is a layout limit, not bookable capacity. It cannot be lower than the live slot count in this unit and its descendants
                  {bayLimit === null ? "." : `, nor higher than the ${bayLimit} physical bays in this site's 3D model.`}
                </span>
              )}
            </label>
          )}

          {missingParent && (
            <p role="alert" className="text-xs text-red-600 dark:text-red-400">
              A block must sit under a floor or zone.
            </p>
          )}

          <StructureFeedback error={error} notice={notice} fallback="The unit change failed." />

          <div className="flex justify-end gap-2">
            <button type="button" className="btn-outline" onClick={onClose} disabled={busy}>
              Cancel
            </button>
            <button type="submit" className="btn-primary" disabled={!canSubmit} aria-busy={busy || undefined}>
              {busy ? "Saving…" : isCreate ? "Add unit" : isRename ? "Save name" : "Save capacity"}
            </button>
          </div>
        </form>
        </Modal>
      )}

      {released && (
        <ReleasedIdentifierDialog
          kind="unit"
          released={released}
          onClose={() => setReleased(null)}
          onChooseAnother={() => {
            setReleased(null)
            setNotice("Enter a different unit name, then add the unit again.")
          }}
          onRestore={async () => {
            await parkingApi.edit(siteId, {
              action: "restoreUnit",
              resourceId: released.id,
            })
            onSaved()
          }}
        />
      )}
    </>
  )
}
