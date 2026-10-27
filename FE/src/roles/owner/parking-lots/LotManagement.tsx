import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { PageHeader } from "../../../components/ui/PageHeader"
import { useLotManagement } from "./useLotManagement"
import { LotDetailModal } from "./LotDetailModal"
import { LotEditModal } from "./LotEditModal"

export function LotManagement({ siteFilterId = "all" }: { siteFilterId?: string }) {
  const model = useLotManagement(siteFilterId)
  const {
    loading, error, searchTerm, setSearchTerm, statusFilter, setStatusFilter,
    sortBy, setSortBy, setDetailSite, setEditSite,
    filteredData, totals, summaryValue, handleNavigateToStructure, refresh,
  } = model

  return (
    <div className="space-y-6 text-[var(--fg)]">
      <PageHeader title="My Parking Lots (Operational Overview)" variant="compact" divider description="Live capacity status, physical vehicle counts, and reservation protection across your facilities." actions={<div className="flex items-center gap-2">
          <button
            type="button"
            className="btn-outline text-xs px-3 py-2"
            disabled={loading}
            onClick={() => refresh()}
          >
            Refresh Data
          </button>
        </div>} />

      {error && (
        <div role="alert" className="flex items-center gap-2 rounded-lg border border-red-500/30 bg-red-500/10 p-3 text-xs text-red-600 dark:text-red-400">
          <UntitledIcon name="alert" size={14} />
          <span>{error}</span>
        </div>
      )}

      {/* Structured Operational Metric KPIs */}
      <div className="grid gap-3 grid-cols-2 sm:grid-cols-3 lg:grid-cols-6">
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-3.5 space-y-1">
          <span className="text-[11px] font-medium text-[var(--muted)] block">Configured Capacity</span>
          <span className="text-lg font-bold text-[var(--fg)]">{summaryValue(totals.totalCapacity)}</span>
          <span className="text-[10px] text-[var(--muted)] block">{totals.totalSites} active facilities</span>
        </div>

        <div className="rounded-xl border border-emerald-500/20 bg-emerald-500/5 p-3.5 space-y-1">
          <span className="text-[11px] font-medium text-emerald-600 dark:text-emerald-400 block">Available (Free)</span>
          <span className="text-lg font-bold text-emerald-600 dark:text-emerald-400">{summaryValue(totals.totalAvailable)}</span>
          <span className="text-[10px] text-emerald-600/70 dark:text-emerald-400/70 block">Ready for parking</span>
        </div>

        <div className="rounded-xl border border-slate-500/20 bg-slate-500/5 p-3.5 space-y-1">
          <span className="text-[11px] font-medium text-[var(--fg)] block">Physically Occupied</span>
          <span className="text-lg font-bold text-[var(--fg)]">{summaryValue(totals.totalOccupied)}</span>
          <span className="text-[10px] text-[var(--muted)] block">Vehicle present in slot</span>
        </div>

        <div className="rounded-xl border border-blue-500/20 bg-blue-500/5 p-3.5 space-y-1">
          <span className="text-[11px] font-medium text-blue-600 dark:text-blue-400 block">Reserved / Protected</span>
          <span className="text-lg font-bold text-blue-600 dark:text-blue-400">{summaryValue(totals.totalProtected)}</span>
          <span className="text-[10px] text-blue-600/70 dark:text-blue-400/70 block">Guaranteed bookings</span>
        </div>

        <div className="rounded-xl border border-amber-500/20 bg-amber-500/5 p-3.5 space-y-1">
          <span className="text-[11px] font-medium text-amber-600 dark:text-amber-400 block">Pending Payment</span>
          <span className="text-lg font-bold text-amber-600 dark:text-amber-400">{summaryValue(totals.totalPendingPayment)}</span>
          <span className="text-[10px] text-amber-600/70 dark:text-amber-400/70 block">Awaiting checkout</span>
        </div>

        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-3.5 space-y-1">
          <span className="text-[11px] font-medium text-[var(--muted)] block">Backup Buffer</span>
          <span className="text-lg font-bold text-[var(--fg)]">{summaryValue(totals.totalBackup)}</span>
          <span className="text-[10px] text-[var(--muted)] block">Overflow guarantee</span>
        </div>
      </div>

      {/* Controls & Search Filter Bar */}
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-[var(--border)] bg-[var(--card)] p-3">
        <div className="flex items-center gap-2 flex-1 min-w-[220px] max-w-sm">
          <UntitledIcon name="search" size={14} className="text-[var(--muted)] shrink-0" />
          <input
            className="w-full bg-transparent text-xs text-[var(--fg)] placeholder:text-[var(--muted)] outline-none"
            placeholder="Search lot by name, code, or address…"
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

        <div className="flex flex-wrap items-center gap-3 text-xs">
          <div className="flex items-center gap-1.5">
            <span className="text-[var(--muted)]">Status:</span>
            <RoundedSelect
              className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-2.5 py-1 text-xs text-[var(--fg)] outline-none"
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value as "all" | "active" | "inactive")}
            >
              <option value="all">All Statuses</option>
              <option value="active">Active Only</option>
              <option value="inactive">Inactive Only</option>
            </RoundedSelect>
          </div>

          <div className="flex items-center gap-1.5">
            <span className="text-[var(--muted)]">Sort:</span>
            <RoundedSelect
              className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-2.5 py-1 text-xs text-[var(--fg)] outline-none"
              value={sortBy}
              onChange={(e) => setSortBy(e.target.value as "name" | "capacity" | "occupancy")}
            >
              <option value="name">Lot Name</option>
              <option value="capacity">Total Capacity</option>
              <option value="occupancy">Occupancy Rate</option>
            </RoundedSelect>
          </div>
        </div>
      </div>

      {/* Main Structured Lots Comparison Table */}
      {loading ? (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-8 text-center text-xs text-[var(--muted)]">
          Aggregating live lot capacities and occupancy metrics…
        </div>
      ) : filteredData.length > 0 ? (
        <div className="overflow-x-auto rounded-xl border border-[var(--border)] bg-[var(--card)]">
          <table className="w-full text-left text-xs min-w-[840px]">
            <thead className="border-b border-[var(--border)] bg-[var(--bg)] text-[11px] uppercase tracking-wider text-[var(--muted)]">
              <tr>
                <th className="px-4 py-3 font-semibold">Parking Facility</th>
                <th className="px-3 py-3 font-semibold text-center">Status</th>
                <th className="px-3 py-3 font-semibold text-right">Capacity</th>
                <th className="px-3 py-3 font-semibold text-right">Available</th>
                <th className="px-3 py-3 font-semibold text-right">Occupied</th>
                <th className="px-3 py-3 font-semibold text-right">Reserved</th>
                <th className="px-3 py-3 font-semibold text-right">Pending Pay</th>
                <th className="px-3 py-3 font-semibold text-right">Backup</th>
                <th className="px-4 py-3 font-semibold text-right">Occupancy</th>
                <th className="px-4 py-3 font-semibold text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[var(--border)]">
              {filteredData.map(({ site, metrics, structure, structureError }) => {
                const totalCap = metrics?.total || 1
                const occupiedPercent = metrics ? Math.min(
                  100,
                  Math.round(((metrics.occupied + metrics.protected) / totalCap) * 100),
                ) : null

                return (
                  <tr key={site.id} className="hover:bg-[var(--bg)]/50 transition-colors">
                    {/* Facility Identification */}
                    <td className="px-4 py-3.5">
                      <div className="font-semibold text-xs text-[var(--fg)]">
                        {site.name}
                      </div>
                      <div className="text-[11px] text-[var(--muted)] font-mono">
                        {site.code} · {site.address}
                      </div>
                      {structureError && <div className="mt-1 text-[11px] text-amber-600 dark:text-amber-400" title={structureError}>Capacity unavailable · Refresh Data to retry</div>}
                    </td>

                    {/* Operational Status */}
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

                    {/* Configured Capacity */}
                    <td className="px-3 py-3.5 text-right font-medium text-[var(--fg)]">
                      {metrics?.total ?? "—"}
                    </td>

                    {/* Available Spots */}
                    <td className="px-3 py-3.5 text-right font-bold text-emerald-600 dark:text-emerald-400">
                      {metrics?.available ?? "—"}
                    </td>

                    {/* Occupied */}
                    <td className="px-3 py-3.5 text-right font-medium text-[var(--fg)]">
                      {metrics?.occupied ?? "—"}
                    </td>

                    {/* Reserved/Protected */}
                    <td className="px-3 py-3.5 text-right font-medium text-blue-600 dark:text-blue-400">
                      {metrics?.protected ?? "—"}
                    </td>

                    {/* Pending Payment */}
                    <td className="px-3 py-3.5 text-right font-medium text-amber-600 dark:text-amber-400">
                      {metrics?.pendingPayment ?? "—"}
                    </td>

                    {/* Backup */}
                    <td className="px-3 py-3.5 text-right font-medium text-[var(--fg)]">
                      {metrics?.backup ?? "—"}
                    </td>

                    {/* Occupancy Rate Bar */}
                    <td className="px-4 py-3.5 text-right">
                      <div className="flex items-center justify-end gap-2">
                        {occupiedPercent !== null && <div className="w-14 h-1.5 rounded-full bg-[var(--border)] overflow-hidden">
                          <div
                            className={`h-full rounded-full ${
                              occupiedPercent > 85
                                ? "bg-red-500"
                                : occupiedPercent > 60
                                  ? "bg-amber-500"
                                  : "bg-emerald-500"
                            }`}
                            style={{ width: `${occupiedPercent}%` }}
                          />
                        </div>}
                        <span className="font-mono text-[11px] font-semibold text-[var(--fg)]">
                          {occupiedPercent === null ? "—" : `${occupiedPercent}%`}
                        </span>
                      </div>
                    </td>

                    {/* Actions */}
                    <td className="px-4 py-3.5 text-right">
                      <div className="flex items-center justify-end gap-1.5">
                        <button
                          type="button"
                          className="btn-outline text-[11px] py-1 px-2.5"
                          onClick={() => setDetailSite({ site, metrics, structure, structureError })}
                          title="View detailed floor and vehicle breakdown"
                        >
                          Breakdown
                        </button>
                        <button
                          type="button"
                          className="btn-outline text-[11px] py-1 px-2.5"
                          onClick={() => handleNavigateToStructure(site.id)}
                          title="Configure physical parking structure"
                        >
                          Structure
                        </button>
                        <button
                          type="button"
                          className="btn-outline text-[11px] py-1 px-2"
                          onClick={() => setEditSite(site)}
                          title="Edit parking facility information"
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
            ? "No parking facilities found matching current filters."
            : "No parking facilities registered. Complete onboarding or add a site in Site Management."}
        </div>
      )}

      <LotDetailModal model={model} />
      <LotEditModal model={model} />
    </div>
  )
}
