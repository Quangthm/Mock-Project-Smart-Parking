import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import type { Site } from "../../../lib/parkingApi"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { PageHeader } from "../../../components/ui/PageHeader"
import { useSiteManagement } from "./useSiteManagement"
import { SiteManagementDialogs } from "./SiteManagementDialogs"

export function SiteManagement({ onSiteCreated }: { onSiteCreated: (site: Site, created: boolean) => void }) {
  const model = useSiteManagement(onSiteCreated)
  const {
    sites, operators, loading, error, message, reload, setReload,
    editingSite, setEditingSite, showFormModal, setShowFormModal,
    inspectSite, setInspectSite, assignModal, setAssignModal,
    assignBusy, assignError, searchTerm, setSearchTerm, statusFilter, setStatusFilter,
    filteredSites, handleSavedSite, handleOpenAssignModal, handleConfirmAssign,
    handleNavigateToStructure,
  } = model

  return (
    <section className="space-y-5 text-[var(--fg)]">
      <PageHeader title="Site & Staff Management" variant="compact" divider description="Configure parking branches, manage physical locations, and supervise assigned operators." actions={<div className="flex items-center gap-2.5">
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
        </div>} />

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
        <div className="sp-search-field flex w-full min-w-0 basis-full items-center gap-2 sm:w-auto sm:min-w-[220px] sm:basis-auto sm:flex-1 sm:max-w-sm">
          <UntitledIcon name="search" size={14} className="text-[var(--muted)] shrink-0" />
          <input
            className="w-full bg-transparent text-xs text-[var(--fg)] placeholder:text-[var(--muted)] outline-none"
            type="search"
            aria-label="Search parking sites by name or location"
            autoComplete="off"
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
          <RoundedSelect
            className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-2.5 py-1 text-xs text-[var(--fg)] outline-none"
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value as "all" | "active" | "inactive")}
          >
            <option value="all">All Sites ({sites.length})</option>
            <option value="active">Active Only</option>
            <option value="inactive">Inactive Only</option>
          </RoundedSelect>
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

      <SiteManagementDialogs model={model} />
    </section>
  )
}
