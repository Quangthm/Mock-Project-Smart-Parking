import { useEffect, useState, useMemo } from "react"
import { createPortal } from "react-dom"
import { useApp } from "../../../context/AppContext"
import {
  parkingApi,
  operatorsApi,
  type Site,
  type Operator,
} from "../../../lib/parkingApi"
import { SiteForm } from "./SiteForm"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"

export function SiteManagement({
  onSiteCreated,
}: {
  onSiteCreated: (site: Site, created: boolean) => void
}) {
  const { user } = useApp()
  const [sites, setSites] = useState<Site[]>([])
  const [operators, setOperators] = useState<Operator[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState("")
  const [message, setMessage] = useState("")
  const [reload, setReload] = useState(0)

  // Modals
  const [editingSite, setEditingSite] = useState<Site | undefined>()
  const [showFormModal, setShowFormModal] = useState(false)
  const [inspectSite, setInspectSite] = useState<Site | null>(null)

  // Operator Reassignment Modal (Requirement C & F)
  const [assignModal, setAssignModal] = useState<{
    targetSite: Site
    selectedOpId: string
  } | null>(null)
  const [assignBusy, setAssignBusy] = useState(false)
  const [assignError, setAssignError] = useState("")

  // Search & Filter
  const [searchTerm, setSearchTerm] = useState("")
  const [statusFilter, setStatusFilter] = useState<"all" | "active" | "inactive">("all")

  useEffect(() => {
    let current = true
    setLoading(true)
    setError("")
    setShowFormModal(false)

    Promise.allSettled([parkingApi.list(), operatorsApi.list()])
      .then(([siteResult, operatorResult]) => {
        if (!current) return
        if (siteResult.status === "fulfilled") setSites(siteResult.value)
        else setError(siteResult.reason instanceof Error ? siteResult.reason.message : "Cannot load sites.")
        if (operatorResult.status === "fulfilled") setOperators(operatorResult.value)
        else if (siteResult.status === "fulfilled")
          setError(operatorResult.reason instanceof Error ? operatorResult.reason.message : "Cannot load operators.")
      })
      .finally(() => {
        if (current) setLoading(false)
      })

    return () => {
      current = false
    }
  }, [user?.id, reload])

  function handleSavedSite(site: Site) {
    const created = !editingSite
    setSites((current) =>
      [...current.filter((s) => s.id !== site.id), site].sort((a, b) =>
        a.name.localeCompare(b.name),
      ),
    )
    setShowFormModal(false)
    setEditingSite(undefined)
    setMessage(`Successfully saved parking facility: ${site.name}`)
    setReload((value) => value + 1)
    onSiteCreated(site, created)
  }

  function handleOpenAssignModal(site: Site) {
    // Find first operator not already assigned to this site, or default to first operator
    const unassigned = operators.find((op) => !op.siteIds.includes(site.id))
    setAssignModal({
      targetSite: site,
      selectedOpId: unassigned?.id ?? operators[0]?.id ?? "",
    })
    setAssignError("")
  }

  async function handleConfirmAssign() {
    if (!assignModal || !assignModal.selectedOpId) return
    const op = operators.find((o) => o.id === assignModal.selectedOpId)
    if (!op) return

    setAssignBusy(true)
    setAssignError("")

    // Add target site to operator's site list if not present
    const updatedSiteIds = Array.from(
      new Set([...op.siteIds, assignModal.targetSite.id]),
    )

    try {
      const updated = await operatorsApi.reassignSite(op.id, updatedSiteIds)
      setOperators((current) =>
        current.map((item) => (item.id === updated.id ? updated : item)),
      )
      setMessage(
        `Assigned operator ${op.fullName} to site ${assignModal.targetSite.name}.`,
      )
      setAssignModal(null)
    } catch (err: unknown) {
      const msg =
        err instanceof Error
          ? err.message
          : "Site assignment update failed. Backend endpoint may not be available yet."
      setAssignError(
        `${msg} (Previous operator assignments were kept unchanged).`,
      )
    } finally {
      setAssignBusy(false)
    }
  }

  function handleNavigateToStructure(siteId: string) {
    window.dispatchEvent(
      new CustomEvent("sp:dashboard-nav", { detail: "structure" }),
    )
    window.dispatchEvent(
      new CustomEvent("sp:structure-select-site", { detail: siteId }),
    )
  }

  const filteredSites = useMemo(() => {
    return sites.filter((site) => {
      const matchStatus =
        statusFilter === "all" ||
        (statusFilter === "active" ? site.isActive : !site.isActive)
      const matchSearch =
        !searchTerm.trim() ||
        site.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        site.address.toLowerCase().includes(searchTerm.toLowerCase())
      return matchStatus && matchSearch
    })
  }, [sites, statusFilter, searchTerm])

  return (
    <section className="space-y-5 text-[var(--fg)]">
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 border-b border-[var(--border)] pb-4">
        <div>
          <h1 className="text-xl font-bold tracking-tight text-[var(--fg)]">
            Site & Staff Management
          </h1>
          <p className="mt-1 text-xs text-[var(--muted)]">
            Configure parking branches, manage physical locations, and supervise assigned operators.
          </p>
        </div>

        <div className="flex items-center gap-2.5">
          <button
            type="button"
            className="btn-outline text-xs px-3 py-2"
            disabled={loading}
            onClick={() => setReload((v) => v + 1)}
          >
            Refresh
          </button>
          <button
            type="button"
            className="btn-primary text-xs px-3.5 py-2 font-medium flex items-center gap-1.5"
            disabled={loading}
            onClick={() => {
              setEditingSite(undefined)
              setShowFormModal(true)
            }}
          >
            <UntitledIcon name="plus" size={14} />
            <span>Add New Site</span>
          </button>
        </div>
      </div>

      {/* Messages */}
      {error && (
        <div role="alert" className="flex items-center gap-2 rounded-lg border border-red-500/30 bg-red-500/10 p-3 text-xs text-red-600 dark:text-red-400">
          <UntitledIcon name="alert" size={14} />
          <span>{error}</span>
        </div>
      )}

      {message && (
        <div role="status" className="flex items-center justify-between gap-2 rounded-lg border border-emerald-500/30 bg-emerald-500/10 p-3 text-xs text-emerald-700 dark:text-emerald-300">
          <div className="flex items-center gap-2">
            <UntitledIcon name="check" size={14} />
            <span>{message}</span>
          </div>
          <button
            type="button"
            className="text-emerald-700 dark:text-emerald-300 hover:opacity-75"
            onClick={() => setMessage("")}
          >
            <UntitledIcon name="x" size={12} />
          </button>
        </div>
      )}

      {/* Filter and Search Bar */}
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-[var(--border)] bg-[var(--card)] p-3">
        <div className="flex items-center gap-2 flex-1 min-w-[220px] max-w-sm">
          <UntitledIcon name="search" size={14} className="text-[var(--muted)] shrink-0" />
          <input
            className="w-full bg-transparent text-xs text-[var(--fg)] placeholder:text-[var(--muted)] outline-none"
            placeholder="Search site by name or location…"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
          />
          {searchTerm && (
            <button
              type="button"
              className="text-[var(--muted)] hover:text-[var(--fg)]"
              onClick={() => setSearchTerm("")}
            >
              <UntitledIcon name="x" size={12} />
            </button>
          )}
        </div>

        <div className="flex items-center gap-2 text-xs">
          <span className="text-[var(--muted)]">Status:</span>
          <select
            className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-2.5 py-1 text-xs text-[var(--fg)] outline-none"
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value as "all" | "active" | "inactive")}
          >
            <option value="all">All Sites ({sites.length})</option>
            <option value="active">Active Only</option>
            <option value="inactive">Inactive Only</option>
          </select>
        </div>
      </div>

      {/* Sites & Assigned Operators Structured Table */}
      {loading ? (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-8 text-center text-xs text-[var(--muted)]">
          Loading parking sites and operator rosters…
        </div>
      ) : filteredSites.length > 0 ? (
        <div className="overflow-x-auto rounded-xl border border-[var(--border)] bg-[var(--card)]">
          <table className="w-full min-w-[820px] text-left text-xs">
            <thead className="border-b border-[var(--border)] bg-[var(--bg)] text-[11px] uppercase tracking-wider text-[var(--muted)]">
              <tr>
                <th className="px-4 py-3 font-semibold">Site Identification</th>
                <th className="px-4 py-3 font-semibold">Address</th>
                <th className="px-3 py-3 font-semibold text-center">Status</th>
                <th className="px-3 py-3 font-semibold text-right">Physical Capacity</th>
                <th className="px-4 py-3 font-semibold">Assigned Operators</th>
                <th className="px-4 py-3 font-semibold text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[var(--border)]">
              {filteredSites.map((site) => {
                const siteOperators = operators.filter((op) =>
                  op.siteIds.includes(site.id),
                )

                return (
                  <tr key={site.id} className="hover:bg-[var(--bg)]/50 transition-colors">
                    {/* Site Identification */}
                    <td className="px-4 py-3.5">
                      <div className="font-semibold text-xs text-[var(--fg)]">
                        {site.name}
                      </div>
                    </td>

                    {/* Address */}
                    <td className="px-4 py-3.5 max-w-[200px] truncate text-[var(--muted)]" title={site.address}>
                      {site.address}
                    </td>

                    {/* Status */}
                    <td className="px-3 py-3.5 text-center">
                      <span
                        className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[10px] font-semibold border uppercase ${
                          site.isActive
                            ? "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/20"
                            : "bg-red-500/10 text-red-600 dark:text-red-400 border-red-500/20"
                        }`}
                      >
                        {site.isActive ? "ACTIVE" : "INACTIVE"}
                      </span>
                    </td>

                    {/* Capacity */}
                    <td className="px-3 py-3.5 text-right font-semibold text-[var(--fg)]">
                      {site.totalPhysicalCapacity} slots
                    </td>

                    {/* Assigned Operators Relationship */}
                    <td className="px-4 py-3.5">
                      <div className="flex flex-wrap items-center gap-1.5">
                        {siteOperators.length > 0 ? (
                          <>
                            {siteOperators.slice(0, 3).map((op) => (
                              <span
                                key={op.id}
                                className="inline-flex items-center gap-1 rounded-md border border-[var(--border)] bg-[var(--bg)] px-2 py-0.5 text-[11px] font-medium text-[var(--fg)]"
                                title={`${op.fullName} (${op.email})`}
                              >
                                <span className="h-1.5 w-1.5 rounded-full bg-[var(--primary)]" />
                                {op.fullName}
                              </span>
                            ))}
                            {siteOperators.length > 3 && (
                              <span className="text-[10px] text-[var(--muted)] font-medium self-center">
                                +{siteOperators.length - 3} more
                              </span>
                            )}
                          </>
                        ) : (
                          <span className="text-[11px] italic text-[var(--muted)]">
                            No operator assigned
                          </span>
                        )}
                      </div>
                    </td>

                    {/* Actions */}
                    <td className="px-4 py-3.5 text-right">
                      <div className="flex items-center justify-end gap-1.5">
                        <button
                          type="button"
                          className="btn-outline text-[11px] py-1 px-2.5"
                          onClick={() => handleOpenAssignModal(site)}
                          title="Assign or dispatch an operator to this facility"
                        >
                          Dispatch Staff
                        </button>
                        <button
                          type="button"
                          className="btn-outline text-[11px] py-1 px-2.5"
                          onClick={() => setInspectSite(site)}
                          title="View facility details and full personnel roster"
                        >
                          Roster
                        </button>
                        <button
                          type="button"
                          className="btn-outline text-[11px] py-1 px-2.5"
                          onClick={() => handleNavigateToStructure(site.id)}
                          title="Configure physical floor layout"
                        >
                          Layout
                        </button>
                        <button
                          type="button"
                          className="btn-outline text-[11px] py-1 px-2"
                          onClick={() => {
                            setEditingSite(site)
                            setShowFormModal(true)
                          }}
                          title="Edit site details"
                        >
                          Edit
                        </button>
                      </div>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-8 text-center text-xs text-[var(--muted)]">
          {searchTerm || statusFilter !== "all"
            ? "No sites matched your search."
            : "No parking sites configured. Click 'Add New Site' to create your first site."}
        </div>
      )}

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
                  <select
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
                  </select>
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
    </section>
  )
}
