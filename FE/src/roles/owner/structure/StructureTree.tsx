import { useMemo, useState } from "react"
import { Badge } from "../../../components/ui/Badge"
import { parkingApi, type AccessPath, type Slot, type Unit } from "../../../lib/parkingApi"
import { unitRemovalBlockers, type StructureUnitType } from "../../../lib/structureRules"
import { StructureFeedback } from "./StructureFeedback"
import { UnitDialog, type UnitDialogModel } from "./UnitDialog"
import { useStructureAction } from "./useStructureAction"

const CHILD_TYPE: Partial<Record<StructureUnitType, StructureUnitType>> = {
  FLOOR: "ZONE",
  ZONE: "BLOCK",
}

function unitTypeTone(type: string): "primary" | "muted" | "purple" {
  if (type === "FLOOR") return "primary"
  if (type === "ZONE") return "muted"
  return "purple"
}

export function StructureTree({
  siteId,
  units,
  slots,
  paths,
  modelUrl,
  onChanged,
}: {
  siteId: string
  units: Unit[]
  slots: Slot[]
  paths: AccessPath[]
  modelUrl: string
  onChanged: () => void
}) {
  const [dialog, setDialog] = useState<UnitDialogModel | null>(null)
  const { busy, error, notice, run, setError, setNotice } = useStructureAction(onChanged)

  const childrenByParent = useMemo(() => {
    const map = new Map<string | null, Unit[]>()
    for (const unit of units) {
      const key = unit.parentId ?? null
      map.set(key, [...(map.get(key) ?? []), unit])
    }
    return map
  }, [units])

  const roots = childrenByParent.get(null) ?? []

  function blockers(unitId: string) {
    return unitRemovalBlockers(unitId, units, slots, paths)
  }

  function removeUnit(unit: Unit) {
    const reasons = blockers(unit.id)
    if (reasons.length > 0) {
      setError(
        new Error(
          `“${unit.name}” cannot be deleted yet. Remove its ${reasons.join(", ")} first. The server also refuses to cascade a delete.`,
        ),
      )
      setNotice("")
      return
    }
    if (!confirm(`Delete unit “${unit.name}”? Its row is released and can only be restored explicitly.`)) return
    void run(
      () => parkingApi.edit(siteId, { action: "removeUnit", resourceId: unit.id }),
      "Unit released.",
    )
  }

  function addRoot(type: StructureUnitType) {
    setDialog({ mode: "create", type, parentId: null })
  }

  function renderBranch(parentId: string | null, depth: number) {
    const branch = childrenByParent.get(parentId) ?? []
    if (branch.length === 0) return null
    return (
      <ul className={depth > 0 ? "ml-3 mt-1 space-y-1 border-l border-[var(--border)] pl-3" : "space-y-1"}>
        {branch.map((unit) => {
          const reasons = blockers(unit.id)
          const childType = CHILD_TYPE[unit.type]
          const directSlots = slots.filter((slot) => slot.unitId === unit.id).length
          return (
            <li key={unit.id}>
              <div className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2">
                <div className="grid gap-2">
                  <div className="flex min-w-0 flex-wrap items-center gap-2">
                    <Badge tone={unitTypeTone(unit.type)}>{unit.type}</Badge>
                    <span className="min-w-0 truncate text-sm font-semibold text-[var(--fg)]" title={unit.name}>{unit.name}</span>
                    <span className="text-[11px] text-[var(--muted)]">
                      {unit.maxCapacity === 0 ? "No layout limit" : `Layout limit ${unit.maxCapacity}`}
                    </span>
                    {directSlots > 0 && (
                      <span className="text-[11px] text-[var(--muted)]">
                        {directSlots} slot{directSlots === 1 ? "" : "s"}
                      </span>
                    )}
                  </div>
                  <div className="grid grid-cols-2 gap-1.5">
                    {childType && (
                      <button
                        type="button"
                        className="btn-ghost w-full px-2! py-1.5! text-xs! [@media(pointer:fine)]:min-h-8!"
                        onClick={() => setDialog({ mode: "create", type: childType, parentId: unit.id })}
                        aria-label={`Add ${childType.toLowerCase()} under ${unit.name}`}
                      >
                        + {childType}
                      </button>
                    )}
                    <button
                      type="button"
                      className="btn-ghost w-full px-2! py-1.5! text-xs! [@media(pointer:fine)]:min-h-8!"
                      onClick={() => setDialog({ mode: "rename", unit })}
                    >
                      Rename
                    </button>
                    <button
                      type="button"
                      className="btn-ghost w-full px-2! py-1.5! text-xs! [@media(pointer:fine)]:min-h-8!"
                      onClick={() => setDialog({ mode: "capacity", unit })}
                    >
                      Capacity
                    </button>
                    <button
                      type="button"
                      className={`btn-danger w-full px-2! py-1.5! text-xs! [@media(pointer:fine)]:min-h-8! ${childType ? "" : "col-start-2"}`}
                      onClick={() => removeUnit(unit)}
                      disabled={busy}
                      aria-describedby={reasons.length > 0 ? `unit-${unit.id}-blockers` : undefined}
                    >
                      Delete
                    </button>
                  </div>
                </div>
                {reasons.length > 0 && (
                  <p id={`unit-${unit.id}-blockers`} className="mt-1.5 text-[11px] text-[var(--muted)]">
                    Cannot be deleted yet: {reasons.join(", ")}. Remove these first.
                  </p>
                )}
              </div>
              {renderBranch(unit.id, depth + 1)}
            </li>
          )
        })}
      </ul>
    )
  }

  return (
    <section className="space-y-3 rounded-2xl border border-[var(--border)] bg-[var(--card)] p-4" aria-label="Spatial tree">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h2 className="text-base font-semibold">Spatial tree</h2>
          <p className="mt-1 text-xs text-[var(--muted)]">
            FLOOR → ZONE → BLOCK, at most three levels. A child type must follow its parent, and a slot belongs to exactly one unit.
          </p>
        </div>
        <div className="flex flex-wrap gap-1.5">
          <button type="button" className="btn-outline px-2.5 py-1.5 text-xs" onClick={() => addRoot("FLOOR")}>
            + Floor
          </button>
          <button type="button" className="btn-outline px-2.5 py-1.5 text-xs" onClick={() => addRoot("ZONE")}>
            + Outdoor zone
          </button>
        </div>
      </div>

      <StructureFeedback error={error} notice={notice} fallback="The unit change failed." />

      {roots.length === 0 ? (
        <p className="rounded-xl border border-dashed border-[var(--border)] p-6 text-xs text-[var(--muted)]">
          This site has no spatial units yet. Add a floor, or a root zone for an outdoor lot.
        </p>
      ) : (
        <div aria-label="Spatial units" className="max-h-[26rem] overflow-y-auto pr-1">
          {renderBranch(null, 0)}
        </div>
      )}

      {dialog && (
        <UnitDialog
          siteId={siteId}
          units={units}
          modelUrl={modelUrl}
          model={dialog}
          onClose={() => setDialog(null)}
          onSaved={() => {
            setDialog(null)
            onChanged()
          }}
        />
      )}
    </section>
  )
}
