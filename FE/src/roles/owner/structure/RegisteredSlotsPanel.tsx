import { useState } from "react"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { Badge } from "../../../components/ui/Badge"
import { parkingApi, type Slot, type Unit } from "../../../lib/parkingApi"
import { categoriesOfSlot as categoriesOf, primaryCategoryOfSlot as primaryOf } from "../../../lib/structureRules"
import { StructureFeedback } from "./StructureFeedback"
import { useStructureAction } from "./useStructureAction"
import { useStructureConfirmation } from "./useStructureConfirmation"

const MAX_BATCH = 50


export function RegisteredSlotsPanel({
  siteId,
  units,
  slots,
  selectedSlotId,
  onSelect,
  onEditCompatibility,
  onChanged,
}: {
  siteId: string
  units: Unit[]
  slots: Slot[]
  selectedSlotId: string | null
  onSelect: (slotId: string) => void
  onEditCompatibility: (slot: Slot) => void
  onChanged: () => void
}) {
  const [selected, setSelected] = useState<string[]>([])
  const [search, setSearch] = useState("")
  const { busy, error, notice, run, setError } = useStructureAction(onChanged)
  const { ask, dialog: confirmation } = useStructureConfirmation()

  const moved = selected.filter((id) => slots.some((slot) => slot.id === id))
  const shown = slots.filter((slot) =>
    slot.code.toLowerCase().includes(search.trim().toLowerCase()),
  )
  const unitName = (id: string) => units.find((unit) => unit.id === id)?.name ?? "Unknown unit"

  function toggle(id: string) {
    setSelected((current) =>
      current.includes(id) ? current.filter((value) => value !== id) : [...current, id],
    )
  }

  function removeSlots(ids: string[]) {
    if (ids.length === 0) return
    if (ids.length > MAX_BATCH) {
      setError(new Error(`A deactivation batch accepts at most ${MAX_BATCH} items.`))
      return
    }
    const names = slots
      .filter((slot) => ids.includes(slot.id))
      .map((slot) => slot.code)
      .slice(0, 3)
      .join(", ")
    const count = `${ids.length} slot${ids.length === 1 ? "" : "s"}`
    ask({
      title: "Remove Parking Slots?",
      description: `Are you sure you want to remove ${count} (${names}${ids.length > 3 ? ", …" : ""})? If any slot cannot be removed, the entire change is rejected.`,
      label: "Remove",
      confirm: async () => {
        const ok = await run(
          () => parkingApi.batch(siteId, ids.map(resourceId => ({ action: "removeSlot", resourceId }))),
          `${count} removed.`,
        )
        if (ok) setSelected([])
      },
    })
  }

  return (
    <details className="group rounded-2xl border border-[var(--border)] bg-[var(--card)]" aria-label="Registered slots">
      <summary className="flex min-h-14 cursor-pointer list-none items-center justify-between gap-3 rounded-2xl px-4 py-3 hover:bg-[var(--raised)] [&::-webkit-details-marker]:hidden">
        <h2 className="text-base font-semibold">Registered slots</h2>
        <span aria-hidden="true" className="grid size-8 shrink-0 place-items-center rounded-full border border-[var(--border)] text-[var(--muted)] motion-safe:transition-transform group-open:rotate-180">
          <UntitledIcon name="chevron-down" size={16} />
        </span>
      </summary>
      <div className="space-y-3 border-t border-[var(--border)] p-4">
        <div className="flex flex-wrap items-start justify-between gap-2">
          <div>
            <p className="mt-1 text-xs text-[var(--muted)]">
              Only these slots exist for capacity and booking. Selecting several and removing them uses one atomic batch:
              if any item fails, none is removed.
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <label className="sp-search-field flex min-w-0 items-center gap-2">
              <span className="sr-only">Search registered slots</span>
              <input
                className="min-w-0 bg-transparent text-xs text-[var(--fg)] outline-none placeholder:text-[var(--muted)]"
                type="search"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Search slot code…"
              />
            </label>
            <button
              type="button"
              className="btn-outline structure-action structure-action-danger min-h-11 text-xs"
              onClick={() => void removeSlots(moved)}
              disabled={busy || moved.length === 0}
            >
              Remove selected ({moved.length})
            </button>
          </div>
        </div>

        <StructureFeedback error={error} notice={notice} fallback="The slot change failed." />

        {slots.length === 0 ? (
          <p className="rounded-xl border border-dashed border-[var(--border)] p-4 text-xs text-[var(--muted)]">
            No live slot is registered for this site. Select unregistered template meshes to register the first one.
          </p>
        ) : shown.length === 0 ? (
          <p className="rounded-xl border border-[var(--border)] p-4 text-xs text-[var(--muted)]">
            No registered slot matches “{search}”.
          </p>
        ) : (
          <ul className="divide-y divide-[var(--border)] rounded-xl border border-[var(--border)]">
            {shown.map((slot) => {
              const categories = categoriesOf(slot)
              const primary = primaryOf(slot)
              const locked = slot.isPhysicallyOccupied || Boolean(slot.reservationState && slot.reservationState !== "NONE")
              const isSelected = selectedSlotId === slot.id
              return (
                <li
                  key={slot.id}
                  className={`flex flex-wrap items-center gap-2 px-3 py-2 ${isSelected ? "bg-[var(--raised)]" : ""}`}
                >
                  <input
                    type="checkbox"
                    className="size-4 shrink-0 accent-[var(--primary)]"
                    checked={selected.includes(slot.id)}
                    onChange={() => toggle(slot.id)}
                    aria-label={`Select slot ${slot.code} for removal`}
                  />
                  <button
                    type="button"
                    className="min-w-0 break-words text-left"
                    onClick={() => onSelect(slot.id)}
                    aria-pressed={isSelected}
                    aria-label={`Focus slot ${slot.code} in the 3D view`}
                  >
                    <span className="block break-words text-xs font-semibold tabular-nums text-[var(--fg)]">{slot.code}</span>
                    <span className="block text-[11px] text-[var(--muted)]">{unitName(slot.unitId)}</span>
                  </button>

                  <span className="flex min-w-0 flex-wrap items-center gap-1 text-[11px]">
                    {categories.length === 0 ? (
                      <Badge tone="warning">Unclassified</Badge>
                    ) : (
                      categories.map((category) => (
                        <Badge key={category} tone={category === primary ? "primary" : "muted"}>
                          {category}
                          {category === primary ? " · primary" : ""}
                        </Badge>
                      ))
                    )}
                    <Badge tone="muted">{slot.type}</Badge>
                    {slot.isPhysicallyOccupied ? (
                      <Badge tone="danger">Occupied</Badge>
                    ) : slot.reservationState && slot.reservationState !== "NONE" ? (
                      <Badge tone="warning">{slot.reservationState.replace(/_/g, " ")}</Badge>
                    ) : (
                      <Badge tone={slot.operationalStatus === "OPERATIONAL" ? "success" : "muted"}>
                        {slot.operationalStatus.replace(/_/g, " ")}
                      </Badge>
                    )}
                  </span>

                  <span className="ml-auto flex flex-wrap items-center gap-1">
                    <button
                      type="button"
                      className="btn-outline px-2 py-1 text-[11px]"
                      onClick={() => onEditCompatibility(slot)}
                      disabled={busy}
                      title={
                        locked
                          ? "An occupied or protected slot cannot be reconfigured; the backend would refuse the change."
                          : undefined
                      }
                    >
                      Edit compatibility
                    </button>
                    <button
                      type="button"
                      className="btn-outline structure-action structure-action-danger min-h-11 text-xs"
                      onClick={() => void removeSlots([slot.id])}
                      disabled={busy}
                      aria-label={`Remove slot ${slot.code}`}
                    >
                      Remove
                    </button>
                  </span>
                </li>
              )
            })}
          </ul>
        )}
        {confirmation}
      </div>
    </details>
  )
}
