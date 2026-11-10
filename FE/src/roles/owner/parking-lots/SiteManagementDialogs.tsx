import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { createPortal } from "react-dom"
import { SiteForm } from "./SiteForm"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import type { SiteManagementModel } from "./useSiteManagement"
import { formatStatus } from "../../../lib/formatStatus"
import { useDialogKeyboard } from "../../../components/ui/useDialogKeyboard"

export function SiteManagementDialogs({ model }: { model: SiteManagementModel }) {
  const {
    sites, operators, editingSite, setEditingSite, showFormModal, setShowFormModal,
    inspectSite, setInspectSite, assignModal, setAssignModal, assignBusy, assignError,
    handleSavedSite, handleConfirmAssign,
  } = model
  const activeDialog = showFormModal ? "form" : assignModal ? "assign" : inspectSite ? "roster" : null
  const dialogRef = useDialogKeyboard(activeDialog, () => {
    if (showFormModal) { setShowFormModal(false); setEditingSite(undefined) }
    else if (assignModal && !assignBusy) setAssignModal(null)
    else if (inspectSite) setInspectSite(null)
  })
  return (
    <>
      {/* Modal 1: Site Create/Edit Modal */}
      {showFormModal &&
        typeof document !== "undefined" &&
        createPortal(
          <div
            className="fixed inset-0 z-[1000] flex items-center justify-center p-4 bg-black/75 backdrop-blur-xs"
          >
            <div ref={dialogRef} role="dialog" aria-modal="true" aria-label={editingSite ? `Edit ${editingSite.name}` : "Add parking site"} tabIndex={-1} className="relative flex max-h-[calc(100dvh-2rem)] w-full max-w-2xl flex-col overflow-hidden rounded-2xl border border-[var(--border)] bg-[var(--card)] shadow-2xl">
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
            className="fixed inset-0 z-[1000] flex items-center justify-center p-4 bg-black/75 backdrop-blur-xs"
            onClick={(e) => {
              if (e.target === e.currentTarget && !assignBusy) setAssignModal(null)
            }}
          >
            <div ref={dialogRef} role="dialog" aria-modal="true" aria-label="Assign staff to facility" tabIndex={-1} className="relative max-h-[calc(100dvh-2rem)] w-full max-w-md overflow-y-auto rounded-2xl border border-[var(--border)] bg-[var(--card)] p-6 shadow-2xl text-[var(--fg)] space-y-4 animate-in">
              <div className="flex items-start justify-between border-b border-[var(--border)] pb-3">
                <div>
                  <h3 className="text-base font-bold text-[var(--fg)]">
                    Assign Staff to Facility
                  </h3>
                  <p className="text-base sm:text-sm text-[var(--muted)]">
                    Facility: <strong>{assignModal.targetSite.name}</strong>
                  </p>
                </div>
                <button
                  type="button"
                  aria-label="Close assignment dialog"
                  className="grid size-11 shrink-0 place-items-center rounded-full text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)] cursor-pointer"
                  onClick={() => setAssignModal(null)}
                  disabled={assignBusy}
                >
                  <UntitledIcon name="x" size={16} />
                </button>
              </div>

              {assignError && (
                <div role="alert" className="rounded-lg border border-red-500/30 bg-red-500/10 p-3 text-base sm:text-sm text-[var(--occupied)]">
                  {assignError}
                </div>
              )}

              <div className="rounded-xl border border-[var(--border)] bg-[var(--bg)] p-3.5 space-y-3 text-base sm:text-sm">
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
                      <span className="text-[var(--muted)] block text-base sm:text-sm">
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
              <div className="flex items-start gap-2 rounded-lg border border-amber-500/30 bg-amber-500/10 p-3 text-base sm:text-sm text-[var(--pending)]">
                <UntitledIcon name="alert" size={15} className="shrink-0 mt-0.5" />
                <p className="leading-relaxed">
                  Site reassignment is unavailable until the service provides an assignment endpoint. Existing operator assignments are unchanged.
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
                  disabled
                >
                  Assignment unavailable
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
            className="fixed inset-0 z-[1000] flex items-center justify-center p-4 bg-black/75 backdrop-blur-xs"
            onClick={(e) => {
              if (e.target === e.currentTarget) setInspectSite(null)
            }}
          >
            <div ref={dialogRef} role="dialog" aria-modal="true" aria-label={`${inspectSite.name} staff roster`} tabIndex={-1} className="relative w-full max-w-lg max-h-[85vh] flex flex-col rounded-2xl border border-[var(--border)] bg-[var(--card)] p-6 shadow-2xl text-[var(--fg)] space-y-4 animate-in">
              <div className="flex items-start justify-between border-b border-[var(--border)] pb-3">
                <div>
                  <h3 className="text-base font-bold text-[var(--fg)]">
                    {inspectSite.name} — Staff Roster
                  </h3>
                  <p className="text-base sm:text-sm text-[var(--muted)]">
                    {inspectSite.address}
                  </p>
                </div>
                <button
                  type="button"
                  aria-label="Close staff roster"
                  className="grid size-11 shrink-0 place-items-center rounded-full text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)] cursor-pointer"
                  onClick={() => setInspectSite(null)}
                >
                  <UntitledIcon name="x" size={16} />
                </button>
              </div>

              <div className="flex-1 overflow-y-auto space-y-3 text-base sm:text-sm">
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
                            <div className="font-semibold text-base sm:text-sm text-[var(--fg)]">
                              {op.fullName}
                            </div>
                            <div className="text-base sm:text-sm text-[var(--muted)] font-mono break-all">
                              {op.email}
                            </div>
                          </div>
                        </div>
                        <span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${op.status === "active" ? "bg-emerald-500/10 text-emerald-700 dark:text-emerald-400" : "bg-amber-500/10 text-amber-700 dark:text-amber-300"}`}>
                          {formatStatus(op.status)}
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
