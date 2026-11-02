import { useEffect, useState, useMemo } from "react"
import { useApp } from "../../../context/AppContext"
import { parkingApi, type Site, type Structure, type CapacityView } from "../../../lib/parkingApi"

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
  metrics: LotCapacityMetrics | null
  structureError: string | null
}

function describeLoadFailure(error: unknown): string {
  if (error instanceof Error) {
    const status = (error as { status?: number }).status
    if (status === 401)
      return "Your session has expired. Sign in again to load your parking facilities."
    if (status === 403)
      return "You are not authorized to view these parking facilities. Complete owner onboarding or ask an administrator to link your account to a site."
    return error.message || "Failed to load parking lots."
  }
  return "Failed to load parking lots."
}

export function useLotManagement(siteFilterId = "all") {
  const { user, authReady } = useApp()
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

    // Wait for session restoration before requesting protected data, otherwise the
    // first render can fire an unauthenticated request and leave the page empty.
    if (!authReady) {
      return () => {
        current = false
      }
    }

    if (!user?.id) {
      setLoading(false)
      setError("Sign in to load your parking facilities.")
      return () => {
        current = false
      }
    }

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
              const metrics = calculateMetrics(structure.capacityViews)
              return { site, structure, metrics, structureError: metrics ? null : "Capacity data is unavailable for this site." }
            } catch (cause) {
              return {
                site,
                structure: null,
                metrics: null,
                structureError: cause instanceof Error ? cause.message : "Could not load parking structure.",
              }
            }
          }),
        )

        if (current) setData(sitesWithMetrics)
      })
      .catch((err) => {
        if (current) setError(describeLoadFailure(err))
      })
      .finally(() => {
        if (current) setLoading(false)
      })

    return () => {
      current = false
    }
  }, [authReady, user?.id, reload])

  function calculateMetrics(views: CapacityView[] = []): LotCapacityMetrics | null {
    if (!views?.length) return null

    // Top-level site views have unitId === null
    const siteViews = views.filter((v) => v.unitId === null)
    if (!siteViews.length) return null
    const targetViews = siteViews
    if (targetViews.some((view) => view.available === null)) return null

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
      total,
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
        if (sortBy === "capacity") return (b.metrics?.total ?? -1) - (a.metrics?.total ?? -1)
        if (sortBy === "occupancy") {
          const occA = a.metrics && a.metrics.total > 0 ? a.metrics.occupied / a.metrics.total : -1
          const occB = b.metrics && b.metrics.total > 0 ? b.metrics.occupied / b.metrics.total : -1
          return occB - occA
        }
        return 0
      })
  }, [data, siteFilterId, statusFilter, searchTerm, sortBy])

  // Aggregated totals across all lots
  const totals = useMemo(() => {
    return filteredData.reduce(
      (acc, curr) => ({
        totalSites: acc.totalSites + 1,
        totalCapacity: acc.totalCapacity + (curr.metrics?.total ?? 0),
        totalAvailable: acc.totalAvailable + (curr.metrics?.available ?? 0),
        totalOccupied: acc.totalOccupied + (curr.metrics?.occupied ?? 0),
        totalProtected: acc.totalProtected + (curr.metrics?.protected ?? 0),
        totalPendingPayment: acc.totalPendingPayment + (curr.metrics?.pendingPayment ?? 0),
        totalBackup: acc.totalBackup + (curr.metrics?.backup ?? 0),
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
  }, [filteredData])
  const hasUnknownCapacity = filteredData.some((item) => !item.metrics)
  const summaryValue = (value: number) => hasUnknownCapacity ? "—" : value.toLocaleString()

  function handleNavigateToStructure(siteId: string) {
    window.dispatchEvent(
      new CustomEvent("sp:dashboard-nav", { detail: "structure" }),
    )
    // Small dispatch to sync selected site
    window.dispatchEvent(
      new CustomEvent("sp:structure-select-site", { detail: siteId }),
    )
  }

  function refresh() { setReload((value) => value + 1) }
  function saveSite(updatedSite: Site) {
    setData((current) => current.map((item) => item.site.id === updatedSite.id
      ? { ...item, site: updatedSite } : item))
    setEditSite(null)
    // The reload is the source of truth; the optimistic merge only avoids a flash.
    refresh()
  }

  return {
    data, loading, error, searchTerm, setSearchTerm, statusFilter, setStatusFilter,
    sortBy, setSortBy, detailSite, setDetailSite, editSite, setEditSite,
    filteredData, totals, summaryValue, handleNavigateToStructure, refresh, saveSite,
  }
}

export type LotManagementModel = ReturnType<typeof useLotManagement>
