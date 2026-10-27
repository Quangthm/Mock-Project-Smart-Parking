import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { createPortal } from "react-dom"
import { SiteForm } from "./SiteForm"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import type { SiteManagementModel } from "./useSiteManagement"

export function SiteManagementDialogs({ model }: { model: SiteManagementModel }) {
  const {
    sites, operators, editingSite, setEditingSite, showFormModal, setShowFormModal,
    inspectSite, setInspectSite, assignModal, setAssignModal, assignBusy, assignError,
    handleSavedSite, handleConfirmAssign,
  } = model
  return (
    <>
      {/* Modal 1: Site Create/Edit Modal */}
      {showFormModal &&
        typeof document !== "undefined" &&
        createPortal(
          <div
            role="dialog"
            aria-modal="true"
            className="fixed inset-0 z-[1000] flex items-center justify-center p-4 bg-black/75 backdrop-blur-xs"
          >
            <div className="relative w-full max-w-2xl rounded-2xl border border-[var(--border)] bg-[var(--card)] p-6 shadow-2xl">
              <SiteForm
                key={editingSite?.id ?? "new"}
                site={editingSite}
                onSaved={handleSavedSite}
                onCancel={() => {
                  setShowFormModal(false)
                  setEditingSite(undefined)
                }}
              />
            </div>
          </div>,
          document.body,
        )}

      {/* Modal 2: Assign Operator to Site with Confirmation (Requirement C & F) */}
      {assignModal &&
        typeof document !== "undefined" &&
        createPortal(
          <div
            role="dialog"
            aria-modal="true"
            className="fixed inset-0 z-[1000] flex items-center justify-center p-4 bg-black/75 backdrop-blur-xs"
            onClick={(e) => {
              if (e.target === e.currentTarget && !assignBusy) setAssignModal(null)
            }}
          >
            <div className="relative w-full max-w-md rounded-2xl border border-[var(--border)] bg-[var(--card)] p-6 shadow-2xl text-[var(--fg)] space-y-4 animate-in">
              <div className="flex items-start justify-between border-b border-[var(--border)] pb-3">
                <div>
                  <h3 className="text-base font-bold text-[var(--fg)]">
                    Assign Staff to Facility
                  </h3>
                  <p className="text-xs text-[var(--muted)]">
                    Facility: <strong>{assignModal.targetSite.name}</strong>
                  </p>
                </div>
                <button
                  type="button"
                  className="text-[var(--muted)] hover:text-[var(--fg)] cursor-pointer"
                  onClick={() => setAssignModal(null)}
                  disabled={assignBusy}
                >
                  <UntitledIcon name="x" size={16} />
                </button>
              </div>

              {assignError && (
                <div role="alert" className="rounded-lg border border-red-500/30 bg-red-500/10 p-3 text-xs text-red-600 dark:text-red-400">
                  {assignError}
                </div>
              )}

              <div className="rounded-xl border border-[var(--border)] bg-[var(--bg)] p-3.5 space-y-3 text-xs">
                <div>
                  <label className="label mb-1" htmlFor="assign-operator-select">
                    Select Operator:
                  </label>
                  <RoundedSelect
                    id="assign-operator-select"
                    className="input"
                    value={assignModal.selectedOpId}
                    onChange={(e) =>
                      setAssignModal({
                        ...assignModal,
                        selectedOpId: e.target.value,
                      })
                    }
                    disabled={assignBusy}
                  >
                    {operators.map((op) => (
                      <option key={op.id} value={op.id}>
                        {op.fullName} ({op.email})
                      </option>
                    ))}
                  </RoundedSelect>
                </div>

                {/* Information preview */}
                {(() => {
                  const selected = operators.find(
                    (o) => o.id === assignModal.selectedOpId,
                  )
                  if (!selected) return null
                  const currentSiteNames = selected.siteIds
                    .map((id) => sites.find((s) => s.id === id)?.name ?? id)
                    .join(", ")

                  return (
                    <div className="border-t border-[var(--border)] pt-2 space-y-1">
                      <span className="text-[var(--muted)] block text-[11px]">
                        Current site duties:
                      </span>
                      <span className="font-medium text-[var(--fg)]">
                        {currentSiteNames || "None"}
                      </span>
                    </div>
                  )
                })()}
              </div>

              {/* Warning confirmation notice */}
              <div className="flex items-start gap-2 rounded-lg border border-amber-500/30 bg-amber-500/10 p-3 text-xs text-amber-700 dark:text-amber-400">
                <UntitledIcon name="alert" size={15} className="shrink-0 mt-0.5" />
                <p className="leading-relaxed">
                  Confirming assignment authorizes this operator to monitor barrier controls, manage check-in tickets, and supervise slots at <strong>{assignModal.targetSite.name}</strong>.
                </p>
              </div>

              <div className="flex items-center justify-end gap-2.5 pt-2 border-t border-[var(--border)]">
                <button
                  type="button"
                  className="btn-outline text-xs px-3.5 py-2"
                  onClick={() => setAssignModal(null)}
                  disabled={assignBusy}
                >
                  Cancel
                </button>
                <button
                  type="button"
                  className="btn-primary text-xs px-4 py-2 font-medium"
                  onClick={handleConfirmAssign}
                  disabled={assignBusy || !assignModal.selectedOpId}
                >
                  {assignBusy ? "Confirming…" : "Confirm Assignment"}
                </button>
              </div>
            </div>
          </div>,
          document.body,
        )}

      {/* Modal 3: Full Site Personnel Roster Detail */}
      {inspectSite &&
        typeof document !== "undefined" &&
        createPortal(
          <div
            role="dialog"
            aria-modal="true"
            className="fixed inset-0 z-[1000] flex items-center justify-center p-4 bg-black/75 backdrop-blur-xs"
            onClick={(e) => {
              if (e.target === e.currentTarget) setInspectSite(null)
            }}
          >
            <div className="relative w-full max-w-lg max-h-[85vh] flex flex-col rounded-2xl border border-[var(--border)] bg-[var(--card)] p-6 shadow-2xl text-[var(--fg)] space-y-4 animate-in">
              <div className="flex items-start justify-between border-b border-[var(--border)] pb-3">
                <div>
                  <h3 className="text-base font-bold text-[var(--fg)]">
                    {inspectSite.name} — Staff Roster
                  </h3>
                  <p className="text-xs text-[var(--muted)]">
                    {inspectSite.address}
                  </p>
                </div>
                <button
                  type="button"
                  className="text-[var(--muted)] hover:text-[var(--fg)] cursor-pointer"
                  onClick={() => setInspectSite(null)}
                >
                  <UntitledIcon name="x" size={16} />
                </button>
              </div>

              <div className="flex-1 overflow-y-auto space-y-3 text-xs">
                {(() => {
                  const assigned = operators.filter((op) =>
                    op.siteIds.includes(inspectSite.id),
                  )
                  if (assigned.length === 0) {
                    return (
                      <div className="rounded-xl border border-[var(--border)] bg-[var(--bg)] p-6 text-center text-[var(--muted)]">
                        No operators are currently assigned to this site.
                      </div>
                    )
                  }

                  return assigned.map((op) => (
                    <div
                      key={op.id}
                      className="rounded-xl border border-[var(--border)] bg-[var(--bg)] p-3 space-y-2"
                    >
                      <div className="flex items-center justify-between">
                        <div className="flex items-center gap-2">
                          <div className="flex h-7 w-7 items-center justify-center rounded-lg bg-[var(--primary)]/10 font-bold text-[var(--primary)] text-xs">
                            {op.fullName.charAt(0)}
                          </div>
                          <div>
                            <div className="font-semibold text-xs text-[var(--fg)]">
                              {op.fullName}
                            </div>
                            <div className="text-[10px] text-[var(--muted)] font-mono">
                              {op.email}
                            </div>
                          </div>
                        </div>
                        <span className="rounded bg-emerald-500/10 px-2 py-0.5 text-[10px] font-semibold text-emerald-600 dark:text-emerald-400">
                          {op.status}
                        </span>
                      </div>
                    </div>
                  ))
                })()}
              </div>

              <div className="flex justify-end pt-2 border-t border-[var(--border)]">
                <button
                  type="button"
                  className="btn-outline text-xs px-4 py-2"
                  onClick={() => setInspectSite(null)}
                >
                  Close
                </button>
              </div>
            </div>
          </div>,
          document.body,
        )}
    </>
  )
}
