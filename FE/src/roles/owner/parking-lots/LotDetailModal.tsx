import { createPortal } from "react-dom"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import type { LotManagementModel } from "./useLotManagement"

export function LotDetailModal({ model }: { model: LotManagementModel }) {
  const { detailSite, setDetailSite, refresh } = model
  return (
    <>
{detailSite &&
        typeof document !== "undefined" &&
        createPortal(
          <div
            role="dialog"
            aria-modal="true"
            className="fixed inset-0 z-[1000] flex items-center justify-center p-4 bg-black/75 backdrop-blur-xs"
            onClick={(e) => {
              if (e.target === e.currentTarget) setDetailSite(null)
            }}
          >
            <div className="relative w-full max-w-xl max-h-[85vh] flex flex-col rounded-2xl border border-[var(--border)] bg-[var(--card)] shadow-2xl p-6 space-y-4 text-[var(--fg)] animate-in">
              <div className="flex items-start justify-between border-b border-[var(--border)] pb-3">
                <div>
                  <h3 className="text-base font-bold text-[var(--fg)]">
                    {detailSite.site.name} — Capacity Detail
                  </h3>
                  <p className="text-xs text-[var(--muted)]">
                    {detailSite.site.code} · {detailSite.site.address}
                  </p>
                </div>
                <button
                  type="button"
                  className="text-[var(--muted)] hover:text-[var(--fg)] cursor-pointer"
                  onClick={() => setDetailSite(null)}
                >
                  <UntitledIcon name="x" size={16} />
                </button>
              </div>

              <div className="flex-1 overflow-y-auto space-y-4 text-xs">
                {detailSite.structureError && (
                  <div role="alert" className="rounded-lg border border-amber-500/30 bg-amber-500/10 p-3 text-amber-700 dark:text-amber-300">
                    <span>{detailSite.structureError}</span>
                    <button type="button" className="ml-2 underline" onClick={() => { setDetailSite(null); refresh() }}>Retry</button>
                  </div>
                )}
                {/* Overall stats grid */}
                <div className="grid grid-cols-1 gap-2.5 rounded-xl border border-[var(--border)] bg-[var(--bg)] p-3 sm:grid-cols-3">
                  <div>
                    <span className="text-[var(--muted)] block">Capacity:</span>
                    <span className="font-bold text-sm text-[var(--fg)]">{detailSite.metrics?.total ?? "—"}</span>
                  </div>
                  <div>
                    <span className="text-emerald-600 dark:text-emerald-400 block font-medium">Available:</span>
                    <span className="font-bold text-sm text-emerald-600 dark:text-emerald-400">{detailSite.metrics?.available ?? "—"}</span>
                  </div>
                  <div>
                    <span className="text-[var(--fg)] block font-medium">Occupied:</span>
                    <span className="font-bold text-sm text-[var(--fg)]">{detailSite.metrics?.occupied ?? "—"}</span>
                  </div>
                  <div>
                    <span className="text-blue-600 dark:text-blue-400 block font-medium">Reserved:</span>
                    <span className="font-bold text-sm text-blue-600 dark:text-blue-400">{detailSite.metrics?.protected ?? "—"}</span>
                  </div>
                  <div>
                    <span className="text-amber-600 dark:text-amber-400 block font-medium">Pending Pay:</span>
                    <span className="font-bold text-sm text-amber-600 dark:text-amber-400">{detailSite.metrics?.pendingPayment ?? "—"}</span>
                  </div>
                  <div>
                    <span className="text-[var(--muted)] block font-medium">Backup Buffer:</span>
                    <span className="font-bold text-sm text-[var(--fg)]">{detailSite.metrics?.backup ?? "—"}</span>
                  </div>
                </div>

                {/* Vehicle Type Breakdown if available */}
                {detailSite.structure?.capacityViews && detailSite.structure.capacityViews.length > 0 && (
                  <div className="space-y-2">
                    <h4 className="font-semibold text-base text-[var(--fg)]">
                      Vehicle Type Allocations
                    </h4>
                    <div className="overflow-x-auto rounded-lg border border-[var(--border)]">
                      <table className="w-full text-left text-xs">
                        <thead className="bg-[var(--bg)] border-b border-[var(--border)] text-[10px] text-[var(--muted)] uppercase">
                          <tr>
                            <th className="p-2.5">Vehicle Type</th>
                            <th className="p-2.5 text-right">Total</th>
                            <th className="p-2.5 text-right">Available</th>
                            <th className="p-2.5 text-right">Occupied</th>
                            <th className="p-2.5 text-right">Reserved</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-[var(--border)]">
                          {detailSite.structure.capacityViews
                            .filter((v) => v.unitId === null)
                            .map((cv) => (
                              <tr key={cv.vehicleType}>
                                <td className="p-2.5 font-medium">{cv.vehicleType}</td>
                                <td className="p-2.5 text-right">{cv.total}</td>
                                <td className="p-2.5 text-right text-emerald-600 font-bold">{cv.available ?? "-"}</td>
                                <td className="p-2.5 text-right">{cv.occupied}</td>
                                <td className="p-2.5 text-right text-blue-600">{cv.protected}</td>
                              </tr>
                            ))}
                        </tbody>
                      </table>
                    </div>
                  </div>
                )}

                {/* Units / Physical Levels Breakdown */}
                {detailSite.structure?.units && detailSite.structure.units.length > 0 && (
                  <div className="space-y-2">
                    <h4 className="font-semibold text-base text-[var(--fg)]">
                      Configured Units & Levels ({detailSite.structure.units.length})
                    </h4>
                    <div className="grid gap-2 sm:grid-cols-2">
                      {detailSite.structure.units.map((u) => (
                        <div
                          key={u.id}
                          className="rounded-lg border border-[var(--border)] bg-[var(--bg)] p-2.5 flex items-center justify-between"
                        >
                          <div>
                            <div className="font-semibold text-xs text-[var(--fg)]">{u.name}</div>
                            <div className="text-[10px] text-[var(--muted)] font-mono">{u.type}</div>
                          </div>
                          <span className="rounded bg-[var(--card)] px-2 py-1 text-[11px] font-mono border border-[var(--border)]">
                            {u.maxCapacity} slots
                          </span>
                        </div>
                      ))}
                    </div>
                  </div>
                )}
              </div>

              <div className="flex justify-end pt-2 border-t border-[var(--border)]">
                <button
                  type="button"
                  className="btn-outline text-xs px-4 py-2"
                  onClick={() => setDetailSite(null)}
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
