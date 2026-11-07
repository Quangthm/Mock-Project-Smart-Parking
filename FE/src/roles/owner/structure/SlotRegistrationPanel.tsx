import { useEffect, useId, useMemo, useState } from "react"
import { Badge, type BadgeTone } from "../../../components/ui/Badge"
import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import {
  StructureApiError,
  parkingApi,
  type ReleasedIdentifier,
  type Slot,
  type Unit,
} from "../../../lib/parkingApi"
import {
  MAX_SLOT_CATEGORIES,
  SLOT_TYPES,
  VEHICLE_CATEGORIES,
  registrationCodeIssues,
  resolveSlotPrimary,
  summarizeRegistration,
  toggleSlotCategory,
  type RegistrationStatus,
} from "../../../lib/structureRules"
import { suggestSlotCode } from "../../../lib/3d/meshSlotMapping"
import { ReleasedIdentifierDialog } from "./ReleasedIdentifierDialog"
import { StructureFeedback } from "./StructureFeedback"

const inputClass = "input w-full text-sm"
const labelClass = "space-y-1 text-xs font-medium text-[var(--muted)]"

export interface RegistrationMesh {
  key: string
  name: string
  code: string
}

export interface RegistrationOutcome {
  key: string
  code: string
  status: RegistrationStatus
  message?: string
  codeLabel?: string
  slotId?: string
  released?: ReleasedIdentifier
}

const STATUS_BADGE: Record<RegistrationStatus, { tone: BadgeTone; label: string }> = {
  succeeded: { tone: "success", label: "Registered" },
  failed: { tone: "danger", label: "Failed" },
  released: { tone: "warning", label: "Released code" },
  resolved: { tone: "primary", label: "Restored" },
}

function statusBadge(outcome?: RegistrationOutcome) {
  if (!outcome) return { tone: "muted" as BadgeTone, label: "Not registered" }
  return STATUS_BADGE[outcome.status]
}

export function SlotRegistrationPanel({
  siteId,
  units,
  registeredSlots,
  registerMode,
  onRegisterModeChange,
  selection,
  onSelectionChange,
  census,
  onChanged,
  activeFloor = null,
}: {
  siteId: string
  units: Unit[]
  registeredSlots: Slot[]
  registerMode: boolean
  onRegisterModeChange: (value: boolean) => void
  selection: RegistrationMesh[]
  onSelectionChange: (next: RegistrationMesh[]) => void
  census: {
    registeredMeshes: number
    unregisteredMeshes: number
    unmatchedSlots: number
    unregistered: Array<{ key: string; name: string }>
  }
  onChanged: () => void
  activeFloor?: string | null
}) {
  const [unitId, setUnitId] = useState(units[0]?.id ?? "")
  const [slotType, setSlotType] = useState<string>("STANDARD")
  const [categories, setCategories] = useState<string[]>(["CAR"])
  const [primary, setPrimary] = useState("CAR")
  const [categoryError, setCategoryError] = useState("")
  const [outcomes, setOutcomes] = useState<Record<string, RegistrationOutcome>>({})
  const [busy, setBusy] = useState(false)
  const [currentKey, setCurrentKey] = useState<string | null>(null)
  const [formError, setFormError] = useState("")
  const [notice, setNotice] = useState("")
  const [solution, setSolution] = useState<{ mesh: RegistrationMesh; released: ReleasedIdentifier } | null>(null)
  // Nothing selectable: every template bay is already registered, or the 3D model
  // has not loaded and the fallback only draws bays for already-registered slots.
  const noneSelectable = census.unregisteredMeshes === 0
  const emptyStateId = useId()

  useEffect(() => {
    if (activeFloor && activeFloor !== "all" && units.length > 0) {
      const clean = activeFloor.trim().toUpperCase()
      const matched = units.find(
        (u) =>
          u.name.trim().toUpperCase() === clean ||
          u.name.trim().toUpperCase() === `TẦNG ${clean}` ||
          u.name.trim().toUpperCase() === `FLOOR_${clean}` ||
          u.name.trim().toUpperCase().includes(clean) ||
          ((clean === "GROUND" || clean === "OUTDOOR" || clean === "OUTDOORPARKING") &&
            (u.name.toLowerCase().includes("ngoài trời") ||
              u.name.toLowerCase().includes("trệt") ||
              u.name.toLowerCase().includes("ground"))),
      )
      if (matched) {
        setUnitId(matched.id)
        return
      }
    }
    if (!units.some((unit) => unit.id === unitId)) setUnitId(units[0]?.id ?? "")
  }, [units, unitId, activeFloor])

  const issues = useMemo(() => registrationCodeIssues(selection), [selection])
  const resolution = resolveSlotPrimary(categories, primary || null)
  const summary = summarizeRegistration(Object.values(outcomes))
  const selectedKeys = useMemo(() => new Set(selection.map((item) => item.key)), [selection])
  const finishedKeys = useMemo(
    () =>
      Object.values(outcomes)
        .filter((outcome) => outcome.status === "succeeded" || outcome.status === "resolved")
        .map((outcome) => outcome.key),
    [outcomes],
  )
  const pendingSelection = selection.filter((item) => {
    const status = outcomes[item.key]?.status
    // A released identifier is a decision, never an automatic retry. It leaves the
    // register/retry path until the Owner chooses another code or restores the row.
    return status !== "succeeded" && status !== "resolved" && status !== "released"
  })
  const retryableKeys = summary.retryable
    .map((outcome) => outcome.key)
    .filter((key) => selectedKeys.has(key))

  function updateCode(key: string, code: string) {
    onSelectionChange(
      selection.map((item) => (item.key === key ? { ...item, code } : item)),
    )
    setFormError("")
  }

  function toggleMeshFromList(mesh: { key: string; name: string }) {
    if (selection.some((item) => item.key === mesh.key)) {
      onSelectionChange(selection.filter((item) => item.key !== mesh.key))
      return
    }
    if (!registerMode) onRegisterModeChange(true)
    onSelectionChange([
      ...selection,
      { key: mesh.key, name: mesh.name, code: suggestSlotCode(mesh.name) },
    ])
  }

  function toggleCategory(category: string) {
    const next = toggleSlotCategory(categories, category)
    setCategoryError(next.error ?? "")
    setCategories(next.categories)
    // Two or more categories must declare their primary explicitly. Changes to the set
    // clear the previous choice instead of silently keeping or inferring one.
    if (next.categories.length === 1) setPrimary(next.categories[0])
    else setPrimary("")
  }

  async function register(keys: string[]) {
    if (busy) return
    const items = selection.filter((item) => keys.includes(item.key))
    if (items.length === 0) return
    if (!unitId) {
      setFormError("Choose the unit that will own these slots.")
      return
    }
    const itemIssues = registrationCodeIssues(items)
    if (Object.keys(itemIssues).length > 0) {
      setFormError("Fix the slot codes marked below before registering.")
      return
    }
    if (resolution.error) {
      setFormError(resolution.error)
      return
    }

    setBusy(true)
    setFormError("")
    setNotice("")
    const next = { ...outcomes }
    let changed = false

    // One request per mesh. A multi-select can partially complete, so every outcome is
    // reported per item and only plain failures are offered for a retry.
    for (const item of items) {
      setCurrentKey(item.key)
      try {
        const slotId = await parkingApi.slot(siteId, {
          unitId,
          code: item.code.trim(),
          vehicleType: resolution.primary ?? undefined,
          primaryVehicleType: resolution.primary ?? undefined,
          vehicleTypes: categories,
          type: slotType,
        })
        next[item.key] = {
          key: item.key,
          code: item.code.trim(),
          status: "succeeded",
          slotId,
        }
        changed = true
      } catch (cause) {
        if (
          cause instanceof StructureApiError &&
          cause.code === "IDENTIFIER_RELEASED" &&
          cause.released
        ) {
          next[item.key] = {
            key: item.key,
            code: item.code.trim(),
            status: "released",
            message: cause.message,
            released: cause.released,
          }
        } else {
          next[item.key] = {
            key: item.key,
            code: item.code.trim(),
            status: "failed",
            message: cause instanceof Error ? cause.message : "Registration failed.",
            codeLabel: cause instanceof StructureApiError ? cause.code : undefined,
          }
        }
      }
      setOutcomes({ ...next })
    }

    setCurrentKey(null)
    setBusy(false)

    // Requirement 5: Automatically remove successfully registered meshes from selection
    // so the panel is clear and ready for the next slot selections.
    const succeededKeys = new Set(
      Object.values(next)
        .filter((outcome) => outcome.status === "succeeded")
        .map((outcome) => outcome.key),
    )

    if (succeededKeys.size > 0) {
      const remainingSelection = selection.filter((item) => !succeededKeys.has(item.key))
      onSelectionChange(remainingSelection)

      const remainingOutcomes = Object.fromEntries(
        Object.entries(next).filter(([key]) => !succeededKeys.has(key)),
      )
      setOutcomes(remainingOutcomes)
    }

    if (changed) onChanged()
    const failed = Object.values(next).filter(
      (outcome) => items.some((item) => item.key === outcome.key) && outcome.status === "failed",
    ).length
    const released = Object.values(next).filter(
      (outcome) => items.some((item) => item.key === outcome.key) && outcome.status === "released",
    ).length
    if (failed === 0 && released === 0) setNotice("Every selected mesh was registered.")
  }

  function chooseAnotherCode(mesh: RegistrationMesh) {
    setOutcomes((current) => ({
      ...current,
      [mesh.key]: {
        key: mesh.key,
        code: mesh.code,
        status: "failed",
        message: "Enter a different code, then retry this mesh.",
      },
    }))
    setSolution(null)
    setFormError("")
    setNotice("Enter a different slot code for the released mesh.")
  }

  function clearFinished() {
    const removable = new Set(finishedKeys)
    onSelectionChange(selection.filter((item) => !removable.has(item.key)))
    setOutcomes((current) =>
      Object.fromEntries(
        Object.entries(current).filter(([key]) => !removable.has(key)),
      ),
    )
  }

  const canRegister = pendingSelection.length > 0 && !busy && !resolution.error

  return (
    <section className="space-y-3 rounded-2xl border border-[var(--border)] bg-[var(--card)] p-4" aria-label="Slot registration">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-base font-semibold">Slot registration</h2>
          <p className="mt-1 max-w-2xl text-xs text-[var(--muted)]">
            Select unregistered template bays in the 3D view. Each selected mesh becomes one registration request; a
            partial result is reported per mesh, and a retry re-sends only the meshes that failed.
          </p>
          <p className="mt-1 text-[11px] text-[var(--muted)]">
            {census.unregisteredMeshes} unregistered mesh{census.unregisteredMeshes === 1 ? "" : "es"} ·{" "}
            {registeredSlots.length} registered slot{registeredSlots.length === 1 ? "" : "s"} ·{" "}
            {census.unmatchedSlots} registered slot{census.unmatchedSlots === 1 ? "" : "s"} without a matching mesh
          </p>
          {census.unmatchedSlots > 0 && census.unregisteredMeshes > 0 && (
            <p className="mt-1 text-[11px] text-amber-700 dark:text-amber-300">
              Some registered codes do not match a template mesh name. Those meshes stay unregistered rather than being
              guessed, so capacity and booking cannot drift.
            </p>
          )}
        </div>
        <button
          type="button"
          className={registerMode ? "btn-primary px-3 py-1.5 text-xs" : "btn-outline px-3 py-1.5 text-xs"}
          onClick={() => onRegisterModeChange(!registerMode)}
          aria-pressed={registerMode}
          disabled={noneSelectable && !registerMode}
          aria-describedby={noneSelectable ? emptyStateId : undefined}
          title={
            noneSelectable && !registerMode
              ? "No unregistered bays are available to select in this site's 3D model."
              : undefined
          }
        >
          {registerMode ? "Finish selecting" : "Select meshes in 3D"}
        </button>
      </div>

      <StructureFeedback error={formError || undefined} notice={notice} fallback="Registration failed." />

      {noneSelectable && (
        <div
          id={emptyStateId}
          className="rounded-xl border border-dashed border-[var(--border)] bg-[var(--bg)] p-4 text-xs text-[var(--muted)]"
        >
          <p className="font-semibold text-[var(--fg)]">No unregistered bays to select</p>
          <p className="mt-1">
            This site's 3D model has no unregistered template bay. That happens when every template bay mesh is already
            mapped to a registered slot code, or when the 3D model has not loaded — the fallback scene only draws bays
            for slots that are already registered.
          </p>
          <p className="mt-1">
            Next: choose another lot or 3D model, or register bays from the mesh list on a site that still has unmapped
            bays.
          </p>
        </div>
      )}

      {census.unregistered.length > 0 && (
        <details className="rounded-xl border border-[var(--border)] bg-[var(--bg)]">
          <summary className="cursor-pointer px-3 py-2 text-xs font-medium text-[var(--fg)]">
            Choose meshes from a list (keyboard-friendly) · {census.unregisteredMeshes}
          </summary>
          <ul className="grid max-h-56 gap-1 overflow-y-auto border-t border-[var(--border)] p-2 sm:grid-cols-2">
            {census.unregistered.map((mesh) => {
              const checked = selection.some((item) => item.key === mesh.key)
              return (
                <li key={mesh.key}>
                  <label className="flex cursor-pointer items-center gap-2 rounded-md px-2 py-1 text-xs text-[var(--fg)] hover:bg-[var(--raised)]">
                    <input
                      type="checkbox"
                      checked={checked}
                      onChange={() => toggleMeshFromList(mesh)}
                      className="size-4 accent-[var(--primary)]"
                    />
                    <span className="min-w-0 truncate font-mono" title={mesh.name}>{mesh.name}</span>
                  </label>
                </li>
              )
            })}
          </ul>
        </details>
      )}

      {selection.length === 0 ? (
        noneSelectable ? null : (
          <p className="rounded-xl border border-dashed border-[var(--border)] p-4 text-xs text-[var(--muted)]">
            No template mesh is selected. {registerMode ? "Click an unregistered bay in the 3D view." : "Start selecting to register a bay."}
          </p>
        )
      ) : (
        <>
          <ul className="space-y-2">
            {selection.map((mesh) => {
              const outcome = outcomes[mesh.key]
              const badge = statusBadge(outcome)
              const running = busy && currentKey === mesh.key
              const codeIssue = issues[mesh.key]
              return (
                <li key={mesh.key} className="rounded-xl border border-[var(--border)] bg-[var(--bg)] px-3 py-2">
                  <div className="grid gap-2 sm:grid-cols-[minmax(6rem,10rem)_minmax(0,1fr)_auto] sm:items-start">
                    <div className="min-w-0">
                      <p className="truncate text-xs font-semibold text-[var(--fg)]" title={mesh.name}>
                        {mesh.name}
                      </p>
                      <Badge tone={badge.tone} className="mt-1">
                        {running ? "Registering…" : badge.label}
                      </Badge>
                    </div>
                    <label className="space-y-1 text-[11px] font-medium text-[var(--muted)]">
                      <span>Slot code</span>
                      <input
                        className={inputClass}
                        value={mesh.code}
                        onChange={(event) => updateCode(mesh.key, event.target.value)}
                        maxLength={50}
                        disabled={busy || outcome?.status === "succeeded" || outcome?.status === "resolved"}
                        aria-invalid={Boolean(codeIssue) || undefined}
                        aria-label={`Slot code for mesh ${mesh.name}`}
                      />
                      {codeIssue && (
                        <span className="block text-[11px] font-normal text-red-600 dark:text-red-400">{codeIssue}</span>
                      )}
                      {outcome?.message && (
                        <span className="block text-[11px] font-normal text-[var(--muted)]">
                          {outcome.codeLabel && (
                            <span className="mr-1 font-mono text-[10px] uppercase">{outcome.codeLabel}</span>
                          )}
                          {outcome.message}
                        </span>
                      )}
                    </label>
                    <div className="flex flex-wrap items-start gap-1.5 sm:justify-end">
                      {outcome?.status === "released" && outcome.released && (
                        <button
                          type="button"
                          className="btn-outline px-2 py-1 text-[11px]"
                          onClick={() => setSolution({ mesh, released: outcome.released! })}
                          disabled={busy}
                        >
                          Review released code
                        </button>
                      )}
                      <button
                        type="button"
                        className="btn-ghost px-2 py-1 text-[11px]"
                        onClick={() => {
                          onSelectionChange(selection.filter((item) => item.key !== mesh.key))
                          setOutcomes((current) => {
                            const next = { ...current }
                            delete next[mesh.key]
                            return next
                          })
                        }}
                        disabled={busy}
                        aria-label={`Remove ${mesh.name} from the selection`}
                      >
                        Remove
                      </button>
                    </div>
                  </div>
                </li>
              )
            })}
          </ul>

          <div className="grid gap-3 rounded-xl border border-[var(--border)] bg-[var(--bg)] p-3 sm:grid-cols-2 lg:grid-cols-4">
            <label className={labelClass}>
              <span>Owning unit</span>
              <RoundedSelect className={inputClass} value={unitId} onChange={(event) => setUnitId(event.target.value)} required>
                <option value="">Select a unit</option>
                {units.map((unit) => (
                  <option key={unit.id} value={unit.id}>
                    {unit.name} ({unit.type})
                  </option>
                ))}
              </RoundedSelect>
            </label>
            <label className={labelClass}>
              <span>Slot type</span>
              <RoundedSelect className={inputClass} value={slotType} onChange={(event) => setSlotType(event.target.value)}>
                {SLOT_TYPES.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </RoundedSelect>
            </label>
            <fieldset className="space-y-1">
              <legend className="text-xs font-medium text-[var(--muted)]">Categories (1–{MAX_SLOT_CATEGORIES})</legend>
              <div className="flex flex-wrap gap-1.5">
                {VEHICLE_CATEGORIES.map((category) => {
                  const checked = categories.includes(category)
                  return (
                    <label
                      key={category}
                      className={`flex cursor-pointer items-center gap-1.5 rounded-lg border px-2 py-1.5 text-[11px] font-semibold ${
                        checked
                          ? "border-[var(--primary)] bg-[var(--primary)]/10 text-[var(--fg)]"
                          : "border-[var(--border)] bg-[var(--card)] text-[var(--muted)]"
                      }`}
                    >
                      <input
                        type="checkbox"
                        checked={checked}
                        onChange={() => toggleCategory(category)}
                        className="size-3.5 accent-[var(--primary)]"
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
              <span>Primary category</span>
              <RoundedSelect
                className={inputClass}
                value={primary}
                onChange={(event) => setPrimary(event.target.value)}
                disabled={categories.length === 0}
                required
              >
                <option value="">Choose explicitly</option>
                {categories.map((category) => (
                  <option key={category} value={category}>
                    {category}
                  </option>
                ))}
              </RoundedSelect>
              {resolution.error && (
                <span className="block text-[11px] font-normal text-red-600 dark:text-red-400">{resolution.error}</span>
              )}
            </label>
          </div>

          <div className="flex flex-wrap items-center justify-between gap-2">
            <p className="text-[11px] text-[var(--muted)]">
              {summary.succeeded.length} registered · {summary.failed.length} failed · {summary.released.length} released
              decision{summary.released.length === 1 ? "" : "s"}
              {summary.resolved.length > 0 ? ` · ${summary.resolved.length} restored` : ""}
            </p>
            <div className="flex flex-wrap gap-2">
              {finishedKeys.length > 0 && (
                <button type="button" className="btn-ghost px-2.5 py-1.5 text-xs" onClick={clearFinished} disabled={busy}>
                  Clear finished
                </button>
              )}
              {retryableKeys.length > 0 && (
                <button
                  type="button"
                  className="btn-outline px-2.5 py-1.5 text-xs"
                  onClick={() => void register(retryableKeys)}
                  disabled={busy || Boolean(resolution.error)}
                >
                  Retry failed ({retryableKeys.length})
                </button>
              )}
              <button
                type="button"
                className="btn-primary px-3 py-1.5 text-xs"
                onClick={() => void register(pendingSelection.map((item) => item.key))}
                disabled={!canRegister}
                aria-busy={busy || undefined}
              >
                {busy ? "Registering…" : `Register ${pendingSelection.length} slot${pendingSelection.length === 1 ? "" : "s"}`}
              </button>
            </div>
          </div>
        </>
      )}

      {solution && (
        <ReleasedIdentifierDialog
          kind="slot"
          released={solution.released}
          onClose={() => setSolution(null)}
          onChooseAnother={() => chooseAnotherCode(solution.mesh)}
          onRestore={async () => {
            await parkingApi.edit(siteId, {
              action: "restoreSlot",
              resourceId: solution.released.id,
            })
            setOutcomes((current) => ({
              ...current,
              [solution.mesh.key]: {
                key: solution.mesh.key,
                code: solution.mesh.code,
                status: "resolved",
                message:
                  "Released slot restored. It keeps its original unit, code and categories; this mesh was not registered as a new row.",
              },
            }))
            setSolution(null)
            setNotice("Released slot restored. Re-read the structure before editing further.")
            onChanged()
          }}
        />
      )}
    </section>
  )
}
