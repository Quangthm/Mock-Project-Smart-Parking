import { useEffect, useState, useMemo } from "react"
import { createPortal } from "react-dom"
import { useApp } from "../../../context/AppContext"
import {
  parkingApi,
  type Site,
  type Structure,
  type CapacityView,
} from "../../../lib/parkingApi"
import { SiteForm } from "./SiteForm"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"

interface LotCapacityMetrics {
  total: number
  available: number
  occupied: number
  protected: number
  pendingPayment: number
  backup: number
  unavailable: number
}

interface SiteWithStructure {
  site: Site
  structure: Structure | null
  metrics: LotCapacityMetrics
}

export function LotManagement({
  siteFilterId = "all",
}: {
  siteFilterId?: string
}) {
  const { user } = useApp()
  const [data, setData] = useState<SiteWithStructure[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState("")
  const [reload, setReload] = useState(0)

  // Filters
  const [searchTerm, setSearchTerm] = useState("")
  const [statusFilter, setStatusFilter] = useState<"all" | "active" | "inactive">("all")
  const [sortBy, setSortBy] = useState<"name" | "capacity" | "occupancy">("name")

  // Selected lot for detail inspection modal
  const [detailSite, setDetailSite] = useState<SiteWithStructure | null>(null)
  // Editing lot profile
  const [editSite, setEditSite] = useState<Site | null>(null)

  useEffect(() => {
    let current = true
    setLoading(true)
    setError("")

    parkingApi
      .list()
      .then(async (sites) => {
        if (!current) return

        // Fetch structures in parallel for live capacity breakdown
        const sitesWithMetrics = await Promise.all(
          sites.map(async (site) => {
            try {
              const structure = await parkingApi.structure(site.id)
              const metrics = calculateMetrics(site, structure.capacityViews)
              return { site, structure, metrics }
            } catch {
              // Fallback to site base capacity if structure query fails
              return {
                site,
                structure: null,
                metrics: {
                  total: site.totalPhysicalCapacity,
                  available: site.totalPhysicalCapacity,
                  occupied: 0,
                  protected: 0,
                  pendingPayment: 0,
                  backup: 0,
                  unavailable: 0,
                },
              }
            }
          }),
        )

        if (current) setData(sitesWithMetrics)
      })
      .catch((err) => {
        if (current)
          setError(
            err instanceof Error ? err.message : "Failed to load parking lots.",
          )
      })
      .finally(() => {
        if (current) setLoading(false)
      })

    return () => {
      current = false
    }
  }, [user?.id, reload])

  function calculateMetrics(
    site: Site,
    views: CapacityView[] = [],
  ): LotCapacityMetrics {
    if (!views || views.length === 0) {
      return {
        total: site.totalPhysicalCapacity,
        available: site.totalPhysicalCapacity,
        occupied: 0,
        protected: 0,
        pendingPayment: 0,
        backup: 0,
        unavailable: 0,
      }
    }

    // Top-level site views have unitId === null
    const siteViews = views.filter((v) => v.unitId === null)
    const targetViews = siteViews.length > 0 ? siteViews : views

    const total = targetViews.reduce((acc, v) => acc + (v.total || 0), 0)
    const occupied = targetViews.reduce((acc, v) => acc + (v.occupied || 0), 0)
    const protectedCount = targetViews.reduce((acc, v) => acc + (v.protected || 0), 0)
    const pendingPayment = targetViews.reduce(
      (acc, v) => acc + (v.pendingPayment || 0),
      0,
    )
    const backup = targetViews.reduce(
      (acc, v) => acc + (v.effectiveBackup || v.configuredBackup || 0),
      0,
    )
    const unavailable = targetViews.reduce(
      (acc, v) => acc + (v.unavailable || 0),
      0,
    )
    const available = targetViews.reduce(
      (acc, v) => acc + (v.available !== null ? v.available : 0),
      0,
    )

    return {
      total: total || site.totalPhysicalCapacity,
      available: Math.max(0, available),
      occupied,
      protected: protectedCount,
      pendingPayment,
      backup,
      unavailable,
    }
  }

  // Filter and sort items
  const filteredData = useMemo(() => {
    return data
      .filter(({ site }) => {
        const matchSiteFilter =
          siteFilterId === "all" || site.id === siteFilterId
        const matchStatus =
          statusFilter === "all" ||
          (statusFilter === "active" ? site.isActive : !site.isActive)
        const matchSearch =
          !searchTerm.trim() ||
          site.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
          site.code.toLowerCase().includes(searchTerm.toLowerCase()) ||
          site.address.toLowerCase().includes(searchTerm.toLowerCase())
        return matchSiteFilter && matchStatus && matchSearch
      })
      .sort((a, b) => {
        if (sortBy === "name") return a.site.name.localeCompare(b.site.name)
        if (sortBy === "capacity") return b.metrics.total - a.metrics.total
        if (sortBy === "occupancy") {
          const occA = a.metrics.total > 0 ? a.metrics.occupied / a.metrics.total : 0
          const occB = b.metrics.total > 0 ? b.metrics.occupied / b.metrics.total : 0
          return occB - occA
        }
        return 0
      })
  }, [data, siteFilterId, statusFilter, searchTerm, sortBy])

  // Aggregated totals across all lots
  const totals = useMemo(() => {
    return data.reduce(
      (acc, curr) => ({
        totalSites: acc.totalSites + 1,
        totalCapacity: acc.totalCapacity + curr.metrics.total,
        totalAvailable: acc.totalAvailable + curr.metrics.available,
        totalOccupied: acc.totalOccupied + curr.metrics.occupied,
        totalProtected: acc.totalProtected + curr.metrics.protected,
        totalPendingPayment:
          acc.totalPendingPayment + curr.metrics.pendingPayment,
        totalBackup: acc.totalBackup + curr.metrics.backup,
      }),
      {
        totalSites: 0,
        totalCapacity: 0,
        totalAvailable: 0,
        totalOccupied: 0,
        totalProtected: 0,
        totalPendingPayment: 0,
        totalBackup: 0,
      },
    )
  }, [data])

  function handleNavigateToStructure(siteId: string) {
    window.dispatchEvent(
      new CustomEvent("sp:dashboard-nav", { detail: "structure" }),
    )
    // Small dispatch to sync selected site
    window.dispatchEvent(
      new CustomEvent("sp:structure-select-site", { detail: siteId }),
    )
  }

  return (
    <div className="space-y-6 text-[var(--fg)]">
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 border-b border-[var(--border)] pb-4">
        <div>
          <h2 className="text-xl font-bold tracking-tight text-[var(--fg)]">
            My Parking Lots (Operational Overview)
          </h2>
          <p className="mt-1 text-xs text-[var(--muted)]">
            Live capacity status, physical vehicle counts, and reservation protection across your facilities.
          </p>
        </div>

        <div className="flex items-center gap-2">
          <button
            type="button"
            className="btn-outline text-xs px-3 py-2"
            disabled={loading}
            onClick={() => setReload((v) => v + 1)}
          >
            Refresh Data
          </button>
        </div>
      </div>

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
          <span className="text-lg font-bold text-[var(--fg)]">{totals.totalCapacity.toLocaleString()}</span>
          <span className="text-[10px] text-[var(--muted)] block">{totals.totalSites} active facilities</span>
        </div>

        <div className="rounded-xl border border-emerald-500/20 bg-emerald-500/5 p-3.5 space-y-1">
          <span className="text-[11px] font-medium text-emerald-600 dark:text-emerald-400 block">Available (Free)</span>
          <span className="text-lg font-bold text-emerald-600 dark:text-emerald-400">{totals.totalAvailable.toLocaleString()}</span>
          <span className="text-[10px] text-emerald-600/70 dark:text-emerald-400/70 block">Ready for parking</span>
        </div>

        <div className="rounded-xl border border-slate-500/20 bg-slate-500/5 p-3.5 space-y-1">
          <span className="text-[11px] font-medium text-[var(--fg)] block">Physically Occupied</span>
          <span className="text-lg font-bold text-[var(--fg)]">{totals.totalOccupied.toLocaleString()}</span>
          <span className="text-[10px] text-[var(--muted)] block">Vehicle present in slot</span>
        </div>

        <div className="rounded-xl border border-blue-500/20 bg-blue-500/5 p-3.5 space-y-1">
          <span className="text-[11px] font-medium text-blue-600 dark:text-blue-400 block">Reserved / Protected</span>
          <span className="text-lg font-bold text-blue-600 dark:text-blue-400">{totals.totalProtected.toLocaleString()}</span>
          <span className="text-[10px] text-blue-600/70 dark:text-blue-400/70 block">Guaranteed bookings</span>
        </div>

        <div className="rounded-xl border border-amber-500/20 bg-amber-500/5 p-3.5 space-y-1">
          <span className="text-[11px] font-medium text-amber-600 dark:text-amber-400 block">Pending Payment</span>
          <span className="text-lg font-bold text-amber-600 dark:text-amber-400">{totals.totalPendingPayment.toLocaleString()}</span>
          <span className="text-[10px] text-amber-600/70 dark:text-amber-400/70 block">Awaiting checkout</span>
        </div>

        <div className="rounded-xl border border-purple-500/20 bg-purple-500/5 p-3.5 space-y-1">
          <span className="text-[11px] font-medium text-purple-600 dark:text-purple-400 block">Backup Buffer</span>
          <span className="text-lg font-bold text-purple-600 dark:text-purple-400">{totals.totalBackup.toLocaleString()}</span>
          <span className="text-[10px] text-purple-600/70 dark:text-purple-400/70 block">Overflow guarantee</span>
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
            <select
              className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-2.5 py-1 text-xs text-[var(--fg)] outline-none"
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value as "all" | "active" | "inactive")}
            >
              <option value="all">All Statuses</option>
              <option value="active">Active Only</option>
              <option value="inactive">Inactive Only</option>
            </select>
          </div>

          <div className="flex items-center gap-1.5">
            <span className="text-[var(--muted)]">Sort:</span>
            <select
              className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-2.5 py-1 text-xs text-[var(--fg)] outline-none"
              value={sortBy}
              onChange={(e) => setSortBy(e.target.value as "name" | "capacity" | "occupancy")}
            >
              <option value="name">Lot Name</option>
              <option value="capacity">Total Capacity</option>
              <option value="occupancy">Occupancy Rate</option>
            </select>
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
              {filteredData.map(({ site, metrics, structure }) => {
                const totalCap = metrics.total || 1
                const occupiedPercent = Math.min(
                  100,
                  Math.round(((metrics.occupied + metrics.protected) / totalCap) * 100),
                )

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
                      {metrics.total}
                    </td>

                    {/* Available Spots */}
                    <td className="px-3 py-3.5 text-right font-bold text-emerald-600 dark:text-emerald-400">
                      {metrics.available}
                    </td>

                    {/* Occupied */}
                    <td className="px-3 py-3.5 text-right font-medium text-[var(--fg)]">
                      {metrics.occupied}
                    </td>

                    {/* Reserved/Protected */}
                    <td className="px-3 py-3.5 text-right font-medium text-blue-600 dark:text-blue-400">
                      {metrics.protected}
                    </td>

                    {/* Pending Payment */}
                    <td className="px-3 py-3.5 text-right font-medium text-amber-600 dark:text-amber-400">
                      {metrics.pendingPayment}
                    </td>

                    {/* Backup */}
                    <td className="px-3 py-3.5 text-right font-medium text-purple-600 dark:text-purple-400">
                      {metrics.backup}
                    </td>

                    {/* Occupancy Rate Bar */}
                    <td className="px-4 py-3.5 text-right">
                      <div className="flex items-center justify-end gap-2">
                        <div className="w-14 h-1.5 rounded-full bg-[var(--border)] overflow-hidden">
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
                        </div>
                        <span className="font-mono text-[11px] font-semibold text-[var(--fg)]">
                          {occupiedPercent}%
                        </span>
                      </div>
                    </td>

                    {/* Actions */}
                    <td className="px-4 py-3.5 text-right">
                      <div className="flex items-center justify-end gap-1.5">
                        <button
                          type="button"
                          className="btn-outline text-[11px] py-1 px-2.5"
                          onClick={() => setDetailSite({ site, metrics, structure })}
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

      {/* Modal 1: Lot Breakdown Details Modal */}
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
                {/* Overall stats grid */}
                <div className="grid grid-cols-3 gap-2.5 rounded-xl border border-[var(--border)] bg-[var(--bg)] p-3">
                  <div>
                    <span className="text-[var(--muted)] block">Capacity:</span>
                    <span className="font-bold text-sm text-[var(--fg)]">{detailSite.metrics.total}</span>
                  </div>
                  <div>
                    <span className="text-emerald-600 dark:text-emerald-400 block font-medium">Available:</span>
                    <span className="font-bold text-sm text-emerald-600 dark:text-emerald-400">{detailSite.metrics.available}</span>
                  </div>
                  <div>
                    <span className="text-[var(--fg)] block font-medium">Occupied:</span>
                    <span className="font-bold text-sm text-[var(--fg)]">{detailSite.metrics.occupied}</span>
                  </div>
                  <div>
                    <span className="text-blue-600 dark:text-blue-400 block font-medium">Reserved:</span>
                    <span className="font-bold text-sm text-blue-600 dark:text-blue-400">{detailSite.metrics.protected}</span>
                  </div>
                  <div>
                    <span className="text-amber-600 dark:text-amber-400 block font-medium">Pending Pay:</span>
                    <span className="font-bold text-sm text-amber-600 dark:text-amber-400">{detailSite.metrics.pendingPayment}</span>
                  </div>
                  <div>
                    <span className="text-purple-600 dark:text-purple-400 block font-medium">Backup Buffer:</span>
                    <span className="font-bold text-sm text-purple-600 dark:text-purple-400">{detailSite.metrics.backup}</span>
                  </div>
                </div>

                {/* Vehicle Type Breakdown if available */}
                {detailSite.structure?.capacityViews && detailSite.structure.capacityViews.length > 0 && (
                  <div className="space-y-2">
                    <h4 className="font-semibold text-xs text-[var(--fg)] uppercase tracking-wide">
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
                    <h4 className="font-semibold text-xs text-[var(--fg)] uppercase tracking-wide">
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

      {/* Modal 2: Edit Site Information */}
      {editSite &&
        typeof document !== "undefined" &&
        createPortal(
          <div
            role="dialog"
            aria-modal="true"
            className="fixed inset-0 z-[1000] flex items-center justify-center p-4 bg-black/75 backdrop-blur-xs"
          >
            <div className="relative w-full max-w-2xl rounded-2xl border border-[var(--border)] bg-[var(--card)] p-6 shadow-2xl">
              <SiteForm
                site={editSite}
                onSaved={(updatedSite) => {
                  setData((current) =>
                    current.map((item) =>
                      item.site.id === updatedSite.id
                        ? { ...item, site: updatedSite }
                        : item,
                    ),
                  )
                  setEditSite(null)
                }}
                onCancel={() => setEditSite(null)}
              />
            </div>
          </div>,
          document.body,
        )}
    </div>
  )
}
