import { Badge, type BadgeTone } from "../../../components/ui/Badge"
import type { Structure } from "../../../lib/parkingApi"

function projectionTone(state?: string): BadgeTone {
  if (state === "consistent") return "success"
  if (state === "inconsistent") return "warning"
  if (state === "unavailable") return "danger"
  return "muted"
}

function Cell({
  label,
  value,
  caption,
}: {
  label: string
  value: string
  caption: string
}) {
  return (
    <div className="min-w-0 border-r border-[var(--border)] p-3 last:border-r-0">
      <div className="text-xs font-medium text-[var(--muted)]">{label}</div>
      <div className="mt-2 break-words text-2xl font-semibold tabular-nums text-[var(--fg)]">{value}</div>
      <p className="mt-1 text-[11px] text-[var(--muted)]">{caption}</p>
    </div>
  )
}

/**
 * Owns the three capacity numbers separately. The contract forbids merging them and
 * forbids inventing one: an unknown availability is shown as unknown, with the reason
 * the projection gave, never as zero or as the registered count.
 */
export function CapacitySummary({ structure }: { structure: Structure }) {
  const capacity = structure.capacity
  const siteViews = (structure.capacityViews ?? []).filter((view) => view.unitId === null)
  const sum = (field: "occupied" | "protected" | "pendingPayment" | "effectiveBackup") =>
    siteViews.length ? siteViews.reduce((total, view) => total + (view[field] ?? 0), 0) : null

  const registered = capacity ? capacity.registeredPhysicalCount : structure.slots.length
  const limit = capacity?.configuredLimit
  const operational = capacity?.operationalAvailable
  const siteView = siteViews[0]
  const projectionState = capacity?.projectionState ?? siteView?.projectionState
  const reason = capacity?.reason ?? siteView?.reason ?? null

  return (
    <section className="space-y-2" aria-label="Capacity">
      <div className="grid overflow-hidden rounded-lg border border-[var(--border)] bg-[var(--card)] sm:grid-cols-3">
        <Cell
          label="Registered physical slots"
          value={registered === null || registered === undefined ? "Unavailable" : String(registered)}
          caption={
            registered === null || registered === undefined
              ? reason || "The projection did not return a registered count."
              : capacity
                ? `Derived count, basis “${capacity.basis}”. Unregistered meshes are excluded.`
                : "Counted from the live registered slots in this read. The response did not include the capacity block."
          }
        />
        <Cell
          label="Configured limit"
          value={
            limit === null || limit === undefined
              ? capacity
                ? "Not configured"
                : "Not reported"
              : String(limit)
          }
          caption="Owner-declared ceiling. It is a limit, never a count and never bookable capacity."
        />
        <Cell
          label="Operational availability"
          value={operational === null || operational === undefined ? "Unknown" : String(operational)}
          caption={
            operational === null || operational === undefined
              ? reason || "Operations did not supply a trustworthy availability figure for this read."
              : "Operations-sourced availability. It is not derived from the registered count."
          }
        />
      </div>

      <div className="flex flex-wrap items-center gap-2 text-[11px] text-[var(--muted)]">
        <span>
          Live state: {structure.slots.filter((slot) => slot.isPhysicallyOccupied).length} occupied
          {sum("protected") !== null ? ` · ${sum("protected")} protected` : ""}
          {sum("pendingPayment") !== null ? ` · ${sum("pendingPayment")} pending payment` : ""}
          {sum("effectiveBackup") !== null ? ` · ${sum("effectiveBackup")} effective backup` : ""}
        </span>
        {projectionState && (
          <Badge tone={projectionTone(projectionState)}>
            projection {projectionState}
          </Badge>
        )}
        {reason && projectionState !== "consistent" && (
          <span className="text-amber-700 dark:text-amber-300">{reason}</span>
        )}
        {!capacity && (
          <span className="text-amber-700 dark:text-amber-300">
            Configured limit and operational availability are unavailable because the structure read has no capacity
            projection.
          </span>
        )}
      </div>
    </section>
  )
}
