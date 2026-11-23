import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { useEffect, useState, useMemo, useRef } from "react"
import { useApp } from "../../../context/AppContext"
import { CheckInOut } from "../check-in-out/CheckInOut"
import { LotStatus } from "../parking-lot/LotStatus"
import type { SlotStatusFilter } from "../parking-lot/useLotStatus"
import { EmergencyPanel } from "../emergency/EmergencyPanel"
import { TicketManagement } from "../tickets/TicketManagement"
import { OperatorAssignments } from "./OperatorAssignments"
import { PageHeader } from "../../../components/ui/PageHeader"
import { OperatorOverview } from "./OperatorOverview"
import type { OperatorAccessRole, SupportTicket, AppNotification } from "../../../lib/types"
import { DashboardSidebar, type DashboardNavGroup } from "../../../components/layout/DashboardSidebar"
import { PaymentMethodLogo } from "../../../components/payment/PaymentMethodLogo"
import { showUiToast } from "../../../lib/uiToast"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import {
  operatorOperationsApi,
  reservationApi,
  type OperatorSite,
} from "../../../lib/operatorApi"
import {
  clearOperatorRealtimeResources,
  getOperatorRealtimeMetrics,
  invalidateOperatorBookings,
  invalidateOperatorGateResources,
  invalidateOperatorParkingSessions,
  loadAssignedOperatorSitesCoordinated,
  reconcileOperatorBookingLookup,
  reconcileOperatorLayout,
  subscribeOperatorBookings,
  subscribeOperatorLayout,
} from "../../../lib/operatorRealtimeResources"
import {
  reconcileOperatorSiteSnapshot,
  watchOperatorLayout,
  type LayoutConnectionState,
  type LayoutSyncDiagnostics,
} from "../../../lib/operatorLayoutLive"
import type { Booking } from "../../../lib/types"

export type OperatorTab =
  | "overview"
  | "checkin"
  | "checkout"
  | "status"
  | "tickets"
  | "emergency"
  | "assignments"
  | "finance"

type ResourceState<T> = {
  scope: string
  data: T[]
  loading: boolean
  error: string
}

function requestError(reason: unknown, fallback: string) {
  return reason instanceof Error ? reason.message : fallback
}

export function OperatorDashboard({
  accessRoleOverride,
}: {
  accessRoleOverride?: OperatorAccessRole
} = {}) {
  const { user } = useApp()

  const accessRole = accessRoleOverride ?? user?.operatorRole ?? "operation"

  const [tab, setTab] = useState<OperatorTab>(
    accessRole === "financial" ? "finance" : new URLSearchParams(window.location.search).get("checkoutReturn") === "payos" ? "checkout" : "overview",
  )

  const [targetPlateQuery, setTargetPlateQuery] = useState("")
  const [targetSlotStatus, setTargetSlotStatus] = useState<
    "all" | "available" | "occupied" | "reserved" | "backup" | "maintenance"
  >("all")

  const [allLots, setAllLots] = useState<OperatorSite[]>([])
  const [activeSiteId, setActiveSiteId] = useState<string>(() => {
    const query = new URLSearchParams(window.location.search)
    return query.get("checkoutReturn") === "payos" ? query.get("checkoutSiteId") ?? "" : ""
  })
  const [bookingsState, setBookingsState] = useState<ResourceState<Booking>>({ scope: "", data: [], loading: false, error: "" })
  const [ticketsState, setTicketsState] = useState<ResourceState<SupportTicket>>({ scope: "", data: [], loading: false, error: "" })
  const [notificationsState, setNotificationsState] = useState<ResourceState<AppNotification>>({ scope: "", data: [], loading: false, error: "" })
  const [reportsState, setReportsState] = useState<ResourceState<Booking>>({ scope: "", data: [], loading: false, error: "" })
  const [sitesError, setSitesError] = useState("")
  const [sitesLoading, setSitesLoading] = useState(true)
  const [refreshKey, setRefreshKey] = useState(0)
  const [checkoutRevision, setCheckoutRevision] = useState(0)
  const [layoutConnectionState, setLayoutConnectionState] = useState<LayoutConnectionState>("connecting")
  const [layoutSyncState, setLayoutSyncState] = useState<LayoutSyncDiagnostics["lastSyncOutcome"]>("pending")
  const [lastLayoutSyncedAt, setLastLayoutSyncedAt] = useState<number>(0)
  const [coordinatorReads, setCoordinatorReads] = useState<number>(0)
  const [layoutConnectionAttempt, setLayoutConnectionAttempt] = useState(0)
  const [loadedScope, setLoadedScope] = useState("")
  const userScope = user ? user.id + ":" + user.role + ":" + accessRole : ""

  useEffect(() => {
    if (!user) clearOperatorRealtimeResources()
  }, [user?.id])

  useEffect(() => {
    const onNavigate = (event: Event) => {
      const detail = (event as CustomEvent<string>).detail as OperatorTab
      if (detail === "status") setTargetSlotStatus("all")
      if (["overview", "checkin", "checkout", "status", "emergency", "tickets", "assignments", "finance"].includes(detail))
        setTab(detail)
    }
    window.addEventListener("sp:dashboard-nav", onNavigate)
    return () => window.removeEventListener("sp:dashboard-nav", onNavigate)
  }, [])

  useEffect(() => {
    let active = true
    if (loadedScope !== userScope) setSitesLoading(true)
    setSitesError("")
    loadAssignedOperatorSitesCoordinated(user, { force: refreshKey > 0 })
      .then((sites) => {
        if (active) {
          setAllLots(sites)
          setLoadedScope(userScope)
          setLayoutSyncState("synced")
        }
      })
      .catch((error) => {
        if (active) {
          setAllLots([])
          setSitesError(requestError(error, "Could not load assigned parking sites."))
        }
      })
      .finally(() => {
        if (active) {
          setSitesLoading(false)
          setCoordinatorReads(getOperatorRealtimeMetrics().totalReadsStarted)
        }
      })
    return () => {
      active = false
    }
  }, [userScope, refreshKey])

  const ownerLots = loadedScope === userScope ? allLots : []

  const selectedLot = useMemo(() => {
    return (
      (activeSiteId ? ownerLots.find((site) => site.id === activeSiteId) : null) ??
      ownerLots[0] ??
      null
    )
  }, [activeSiteId, ownerLots])

  const selectedLotRef = useRef(selectedLot)
  selectedLotRef.current = selectedLot
  const prevWatchedSiteIdRef = useRef<string | null>(null)
  const reportSiteIds = accessRole === "financial" ? ownerLots.map((site) => site.id) : []
  const reportSiteIdsKey = reportSiteIds.join(",")
  const reportSiteIdsRef = useRef(reportSiteIds)
  reportSiteIdsRef.current = reportSiteIds

  useEffect(() => {
    const siteId = selectedLot?.id
    if (!siteId) return
    let active = true
    const unsubscribe = subscribeOperatorLayout(siteId, (snapshot) => {
      if (!active) return
      setCoordinatorReads(getOperatorRealtimeMetrics().totalReadsStarted)
      if (snapshot.lastOutcome === "error") {
        setLayoutSyncState("error")
        setSitesError(requestError(snapshot.lastError, "Could not refresh this parking site's layout."))
        return
      }
      if (snapshot.lastOutcome === "synced" && snapshot.data) {
        setAllLots((current) => current.map((item) => (item.id === siteId ? snapshot.data! : item)))
        setSitesError("")
        setLayoutSyncState("synced")
        if (snapshot.lastSyncedAt > 0) setLastLayoutSyncedAt(snapshot.lastSyncedAt)
      }
    })
    return () => {
      active = false
      unsubscribe()
    }
  }, [selectedLot?.id, userScope])

  useEffect(() => {
    const siteId = selectedLot?.id
    if (!siteId) {
      prevWatchedSiteIdRef.current = null
      return
    }
    let active = true
    if (prevWatchedSiteIdRef.current !== null && prevWatchedSiteIdRef.current !== siteId) {
      setLastLayoutSyncedAt(0)
      setLayoutSyncState("pending")
    }
    prevWatchedSiteIdRef.current = siteId
    const stop = watchOperatorLayout(
      siteId,
      (refreshBookings, details) =>
        reconcileOperatorSiteSnapshot({
          expectedSiteId: siteId,
          isActive: () => active,
          getCurrentSite: () => selectedLotRef.current,
          refreshSite: async (currentSite) => {
            const reason = details?.refreshReason ?? "invalidation"
            const snap = await reconcileOperatorLayout(currentSite.id, reason)
            if (snap.lastOutcome === "error") {
              throw snap.lastError instanceof Error
                ? snap.lastError
                : new Error("Could not refresh this parking site's layout.")
            }
            if (snap.lastOutcome === "stale" || !snap.data) {
              return null
            }
            return snap.data
          },
          applySite: (updated) => {
            setAllLots(current => current.map(item => item.id === siteId ? updated : item))
          },
          onError: (error) => {
            setLayoutSyncState("error")
            setSitesError(requestError(error, "Could not refresh this parking site's layout."))
          },
          onClearError: () => {
            setSitesError("")
          },
          onBookingsInvalidated: () => {
            setCheckoutRevision(value => value + 1)
            if (accessRole === "financial") {
              for (const id of reportSiteIdsRef.current) invalidateOperatorBookings(id, "invalidation")
            } else {
              invalidateOperatorBookings(siteId, "invalidation")
              invalidateOperatorParkingSessions(siteId, "invalidation")
            }
          },
          onSyncSuccess: (syncedAt) => {
            if (active) setLastLayoutSyncedAt(syncedAt)
          },
          refreshBookings,
          details,
        }),
      state => { if (active) setLayoutConnectionState(state) },
      diag => {
        if (!active) return
        setLayoutSyncState(diag.lastSyncOutcome)
        if (diag.lastSyncedAt > 0) setLastLayoutSyncedAt(diag.lastSyncedAt)
        setCoordinatorReads(getOperatorRealtimeMetrics().totalReadsStarted)
      },
    )
    return () => { active = false; stop() }
  }, [selectedLot?.id, userScope, accessRole, layoutConnectionAttempt])

  const resourceScope = `${userScope}:${selectedLot?.id ?? ""}`
  const reportScope = `${userScope}:${reportSiteIdsKey}`
  const currentLotBookings = bookingsState.scope === resourceScope ? bookingsState.data : []
  const tickets = ticketsState.scope === resourceScope ? ticketsState.data : []
  const notifications = notificationsState.scope === resourceScope ? notificationsState.data : []
  const allReportBookings = reportsState.scope === reportScope ? reportsState.data : []
  const reportsLoading = reportSiteIds.length > 0 && (reportsState.scope !== reportScope || reportsState.loading)
  const reportsError = reportsState.scope === reportScope ? reportsState.error : ""

  const resourceStatus = [
    ["Reservations", bookingsState],
    ["Support tickets", ticketsState],
    ["Notifications", notificationsState],
  ] as const

  useEffect(() => {
    if (!selectedLot || accessRole !== "operation") return
    let active = true
    const siteId = selectedLot.id
    setBookingsState(current => ({ ...current, scope: resourceScope, loading: current.scope !== resourceScope }))
    const unsubscribe = subscribeOperatorBookings(siteId, (snapshot) => {
      if (!active) return
      if (snapshot.lastOutcome === "error") {
        setBookingsState((current) => ({
          scope: resourceScope,
          data: current.scope === resourceScope ? current.data : [],
          loading: false,
          error: requestError(snapshot.lastError, "Could not load reservations."),
        }))
        return
      }
      if (snapshot.lastOutcome === "synced" && snapshot.data) {
        setBookingsState({ scope: resourceScope, data: snapshot.data, loading: false, error: "" })
      }
    })
    invalidateOperatorBookings(siteId, "initial")
    return () => {
      active = false
      unsubscribe()
    }
  }, [accessRole, resourceScope, selectedLot?.id])

  useEffect(() => {
    const initialState = { scope: resourceScope, data: [], loading: Boolean(selectedLot && accessRole === "operation"), error: "" }
    setTicketsState(current => initialState.loading && current.scope === resourceScope ? { ...current, loading: true } : initialState)
    setNotificationsState(current => initialState.loading && current.scope === resourceScope ? { ...current, loading: true } : initialState)
    if (!selectedLot || accessRole !== "operation") return
    let active = true
    Promise.allSettled([
      operatorOperationsApi.listTickets(selectedLot.id),
      operatorOperationsApi.listNotifications(selectedLot.id),
    ])
      .then(([nextTickets, nextNotifications]) => {
        if (!active) return
        setTicketsState({
          scope: resourceScope,
          data: nextTickets.status === "fulfilled" ? nextTickets.value : [],
          loading: false,
          error: nextTickets.status === "rejected" ? requestError(nextTickets.reason, "Could not load support tickets.") : "",
        })
        setNotificationsState({
          scope: resourceScope,
          data: nextNotifications.status === "fulfilled" ? nextNotifications.value : [],
          loading: false,
          error: nextNotifications.status === "rejected" ? requestError(nextNotifications.reason, "Could not load notifications.") : "",
        })
      })
    return () => {
      active = false
    }
  }, [accessRole, resourceScope])

  useEffect(() => {
    setReportsState(current => current.scope === reportScope && reportSiteIds.length
      ? { ...current, loading: true }
      : { scope: reportScope, data: [], loading: reportSiteIds.length > 0, error: "" })
    if (accessRole !== "financial" || !reportSiteIds.length) return
    let active = true
    const bySite = new Map<string, { synced: boolean; data: Booking[]; error?: string }>()
    const unsubs = reportSiteIds.map((siteId) =>
      subscribeOperatorBookings(siteId, (snapshot) => {
        if (!active) return
        if (snapshot.lastOutcome === "error") {
          bySite.set(siteId, {
            synced: true,
            data: [],
            error: requestError(snapshot.lastError, "Could not load reservation reports."),
          })
        } else if (snapshot.lastOutcome === "synced" && snapshot.data) {
          bySite.set(siteId, { synced: true, data: snapshot.data })
        }
        const entries = reportSiteIds.map((id) => bySite.get(id))
        const anyError = entries.find((item) => item?.error)?.error ?? ""
        const allSettled = entries.every((item) => item?.synced)
        setReportsState({
          scope: reportScope,
          data: anyError ? [] : entries.flatMap((item) => item?.data ?? []),
          loading: !allSettled,
          error: anyError,
        })
      }),
    )
    for (const siteId of reportSiteIds) invalidateOperatorBookings(siteId, "initial")
    return () => {
      active = false
      for (const unsub of unsubs) unsub()
    }
  }, [accessRole, reportScope])

  // Cashier role shortcut view
  if (accessRole === "cashier") {
    return (
      <CashierPOS
        key={selectedLot?.id ?? "none"}
        lot={selectedLot}
        realtimeRevision={checkoutRevision}
        sitesLoading={sitesLoading}
        sitesError={sitesError}
        onDataChanged={() => {
          if (selectedLot) invalidateOperatorGateResources(selectedLot.id, "mutation")
          else setRefreshKey((value) => value + 1)
        }}
      />
    )
  }

  // Financial reconciliation metrics
  const completedBookings = allReportBookings.filter((b) => b.status === "completed")
  const totalRevenue = completedBookings.reduce((sum, b) => sum + b.amount, 0)

  // Sidebar navigation groups
  const sidebarGroups: DashboardNavGroup[] =
    accessRole === "financial"
      ? [
          {
            items: [
              {
                id: "finance",
                label: "Financial Reports",
                icon: "banknote",
                active: tab === "finance",
                onClick: () => setTab("finance"),
              },
            ],
          },
        ]
      : [
          {
            items: [
              {
                id: "overview",
                label: "Dashboard Overview",
                icon: "house",
                active: tab === "overview",
                onClick: () => setTab("overview"),
              },
            ],
          },
          {
            label: "Parking Operations",
            items: [
              {
                id: "checkin",
                label: "Gate Check-In",
                icon: "check-circle",
                active: tab === "checkin",
                onClick: () => {
                  setTargetPlateQuery("")
                  setTab("checkin")
                },
              },
              {
                id: "checkout",
                label: "Gate Check-Out",
                icon: "log-out",
                active: tab === "checkout",
                onClick: () => {
                  setTargetPlateQuery("")
                  setTab("checkout")
                },
              },
              {
                id: "status",
                label: "Lot Status & 3D Map",
                icon: "grid",
                active: tab === "status",
                onClick: () => handleOverviewNavigation("status"),
              },
              {
                id: "tickets",
                label: "Driver Support Tickets",
                icon: "ticket",
                active: tab === "tickets",
                onClick: () => setTab("tickets"),
              },
              {
                id: "emergency",
                label: "Emergency Dispatch",
                icon: "alert",
                active: tab === "emergency",
                onClick: () => setTab("emergency"),
              },
              {
                id: "assignments",
                label: "My Assignments",
                icon: "building",
                active: tab === "assignments",
                onClick: () => setTab("assignments"),
              },
            ],
          },
        ]

  function handleOverviewNavigation(nextTab: string, payload?: string) {
    if ((nextTab === "checkin" || nextTab === "checkout") && payload) {
      setTargetPlateQuery(payload)
      setTab("checkin")
      return
    }
    const VALID_SLOT_STATUS_MAP: Record<string, SlotStatusFilter> = {
      available: "available",
      occupied: "occupied",
      reserved: "reserved",
      backup: "backup",
      maintenance: "maintenance",
    }
    if (nextTab === "status") {
      setTargetSlotStatus(payload ? VALID_SLOT_STATUS_MAP[payload] ?? "all" : "all")
      setTab("status")
      return
    }
    setTab(nextTab as OperatorTab)
  }

  return (
    <DashboardSidebar groups={sidebarGroups}>
      <main
        className="min-w-0 overflow-y-auto p-4 sm:p-7"
        data-layout-connection={selectedLot ? layoutConnectionState : undefined}
        data-layout-sync-state={selectedLot ? layoutSyncState : undefined}
        data-last-synced-at={lastLayoutSyncedAt > 0 ? String(lastLayoutSyncedAt) : undefined}
        data-coordinator-reads={String(coordinatorReads)}
      >
        {selectedLot && layoutConnectionState === "unavailable" && (
          <div role="status" className="mb-5 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-amber-500/30 bg-amber-500/10 p-3 text-sm text-[var(--fg)]">
            <span>Live updates are unavailable. Showing the last loaded parking layout.</span>
            <button type="button" className="btn-outline" onClick={() => setLayoutConnectionAttempt(value => value + 1)}>Retry live updates</button>
          </div>
        )}
        {selectedLot && layoutConnectionState === "connected" && layoutSyncState === "error" && !sitesError && (
          <div role="alert" className="mb-5 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-amber-500/30 bg-amber-500/10 p-3 text-sm text-[var(--fg)]">
            <span>Latest parking layout synchronization failed. Showing the last synchronized layout.</span>
            <button type="button" className="btn-outline" onClick={() => void reconcileOperatorLayout(selectedLot.id, "mutation", { force: true })}>
              Retry sync
            </button>
          </div>
        )}
        {sitesError && (
          <div role="alert" className="mb-5 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-red-500/30 bg-red-500/10 p-3 text-sm text-red-600">
            <span>{sitesError}</span>
            <button
              type="button"
              className="btn-outline"
              onClick={() => {
                if (selectedLot) {
                  void reconcileOperatorLayout(selectedLot.id, "mutation", { force: true })
                } else {
                  setRefreshKey((value) => value + 1)
                }
              }}
            >
              Retry
            </button>
          </div>
        )}
        {sitesLoading && (
          <p role="status" className="sr-only">Loading assigned parking sites…</p>
        )}
        {accessRole === "operation" && selectedLot && resourceStatus.map(([label, state]) => (
          state.scope !== resourceScope || state.loading ? (
            <p key={label} role="status" className="sr-only">Loading {label.toLowerCase()}…</p>
          ) : state.error ? (
            <div key={label} role="alert" className="mb-3 rounded-xl border border-red-500/30 bg-red-500/10 p-3 text-sm text-red-600">
              {label}: {state.error}
            </div>
          ) : null
        ))}
        {accessRole === "financial" && reportsError && (
          <div role="alert" className="mb-5 rounded-xl border border-red-500/30 bg-red-500/10 p-3 text-sm text-red-600">
            {reportsError}
          </div>
        )}
        {tab === "status" && (
          <div className="mb-4 flex justify-end">
            <button
              type="button"
              className="btn-outline px-3 py-1.5 text-xs"
              disabled={sitesLoading}
              onClick={() => {
                if (selectedLot) void reconcileOperatorLayout(selectedLot.id, "mutation", { force: true })
                else setRefreshKey((value) => value + 1)
              }}
            >
              Refresh sites and slots
            </button>
          </div>
        )}

        {/* Site Switcher Header (When multiple lots or all sites assigned) */}
        {ownerLots.length > 1 && (
          <div className="mb-6 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-[var(--border)] bg-[var(--card)] p-3.5 shadow-sm">
            <div className="flex items-center gap-2">
              <span className="flex h-2 w-2 rounded-full bg-green-500 animate-pulse" />
              <span className="text-xs font-bold text-[var(--fg)]">
                Active Operating Facility:
              </span>
              <span className="text-xs text-[var(--muted)]">
                (Assigned to {ownerLots.length} sites)
              </span>
            </div>
            <RoundedSelect
              className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-1.5 text-xs font-semibold text-[var(--fg)] outline-none focus:border-[var(--primary)]"
              value={selectedLot?.id ?? ""}
              onChange={(e) => setActiveSiteId(e.target.value)}
            >
              {ownerLots.map((lot) => (
                <option key={lot.id} value={lot.id}>
                  {lot.name} ({lot.totalSlots} slots)
                </option>
              ))}
            </RoundedSelect>
          </div>
        )}

        {/* Tab View Container */}
        <div key={tab} className="animate-in">
          {/* TAB 1: OVERVIEW */}
          {accessRole === "operation" && tab === "overview" && !sitesLoading && (Boolean(selectedLot) || !sitesError) && (
            <OperatorOverview
              lot={selectedLot}
              allLots={ownerLots}
              onSelectLot={(lotId) => setActiveSiteId(lotId)}
              onNavigateTab={handleOverviewNavigation}
              bookings={currentLotBookings}
              tickets={tickets}
              notifications={notifications}
            />
          )}

          {/* TAB 2: CHECK-IN / CHECK-OUT */}
          {accessRole === "operation" && (tab === "checkin" || tab === "checkout") && (
            <CheckInOut
              key={tab}
              mode={tab === "checkin" ? "check-in" : "check-out"}
              lot={selectedLot}
              initialPlateQuery={targetPlateQuery}
              realtimeRevision={checkoutRevision}
              onDataChanged={() => {
                if (selectedLot) invalidateOperatorGateResources(selectedLot.id, "mutation")
                else setRefreshKey((value) => value + 1)
              }}
            />
          )}

          {/* TAB 3: LOT STATUS & 3D STRUCTURE */}
          {accessRole === "operation" && tab === "status" && (
            <LotStatus
              key={(selectedLot?.id ?? "none") + ":" + targetSlotStatus}
              lot={selectedLot}
              initialStatusFilter={targetSlotStatus}
              refreshOnResume={false}
              connectionState={layoutConnectionState}
              syncState={layoutSyncState}
            />
          )}

          {/* TAB 5: TICKETS */}
          {accessRole === "operation" && tab === "tickets" && (
            <TicketManagement siteId={selectedLot?.id ?? ""} />
          )}

          {/* TAB 6: EMERGENCY */}
          {accessRole === "operation" && tab === "emergency" && (
            <EmergencyPanel siteId={selectedLot?.id ?? ""} />
          )}

          {/* TAB 7: ASSIGNMENTS */}
          {accessRole === "operation" && tab === "assignments" && (
            <OperatorAssignments currentLot={selectedLot} sites={ownerLots} />
          )}

          {/* TAB 8: FINANCIAL REPORTS */}
          {accessRole === "financial" && tab === "finance" && (
            <section className="mx-auto w-full max-w-5xl space-y-6">
              <PageHeader eyebrow={user?.role === "owner" || user?.operatorSiteId === "all" ? "All Assigned Sites" : (selectedLot?.name ?? "Assigned parking lot")} title="Financial Reports & Reconciliation" description="Completed booking revenue, collection channels, and reconciliation registry." />

              {!sitesLoading && !sitesError && reportSiteIds.length === 0 && (
                <p className="text-sm text-[var(--muted)]">No assigned parking sites are available.</p>
              )}
              {!sitesLoading && reportsLoading && (
                <p role="status" className="sr-only">Loading reservation reports…</p>
              )}

              {!sitesLoading && !sitesError && !reportsLoading && !reportsError && reportSiteIds.length > 0 && (<>
              <div className="grid gap-4 sm:grid-cols-3">
                <FinanceMetric
                  label="Completed Bookings"
                  value={completedBookings.length.toLocaleString("vi-VN")}
                />
                <FinanceMetric
                  label="Total Recorded Revenue"
                  value={`${totalRevenue.toLocaleString("vi-VN")} VND`}
                />
                <FinanceMetric
                  label="Pending Settlement"
                  value={allReportBookings
                    .filter((b) => b.status === "completed" && !b.paymentMethod)
                    .length.toLocaleString("vi-VN")}
                />
              </div>

              <div className="overflow-x-auto rounded-xl border border-[var(--border)] bg-[var(--card)]">
                <table className="w-full min-w-[600px] text-left text-sm">
                  <thead className="border-b border-[var(--border)] text-[var(--muted)]">
                    <tr>
                      <th className="px-4 py-3 font-medium">Booking ID</th>
                      <th className="px-4 py-3 font-medium">Vehicle Plate</th>
                      <th className="px-4 py-3 font-medium">Payment Method</th>
                      <th className="px-4 py-3 text-right font-medium">Amount (VND)</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border)]">
                    {completedBookings.map((b) => (
                      <tr key={b.id} className="hover:bg-[var(--bg)]/50 transition">
                        <td className="px-4 py-3 font-mono text-[var(--fg)]">
                          {b.id}
                        </td>
                        <td className="px-4 py-3 font-mono font-semibold text-[var(--fg)]">
                          {b.licensePlate}
                        </td>
                        <td className="px-4 py-3 text-[var(--muted)]">
                          {b.paymentMethod ? (
                            <PaymentMethodLogo method={b.paymentMethod} size="xs" showName />
                          ) : (
                            <span className="text-amber-600 font-medium">Pending Settlement</span>
                          )}
                        </td>
                        <td className="px-4 py-3 text-right font-bold text-[var(--fg)]">
                          {b.amount.toLocaleString("vi-VN")} VND
                        </td>
                      </tr>
                    ))}
                    {!completedBookings.length && (
                      <tr>
                        <td colSpan={4} className="px-4 py-8 text-center text-[var(--muted)]">
                          No completed bookings to reconcile for this facility.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
              </>)}
            </section>
          )}
        </div>
      </main>
    </DashboardSidebar>
  )
}

function FinanceMetric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5">
      <p className="text-sm font-medium text-[var(--muted)]">{label}</p>
      <p className="mt-2 text-2xl font-semibold tabular-nums text-[var(--fg)]">{value}</p>
    </div>
  )
}

function CashierPOS({
  lot,
  sitesLoading,
  sitesError,
  onDataChanged,
  readOnly = false,
  realtimeRevision = 0,
}: {
  lot: OperatorSite | null
  sitesLoading: boolean
  sitesError: string
  onDataChanged: () => void
  readOnly?: boolean
  realtimeRevision?: number
}) {
  const [plate, setPlate] = useState("")
  const [booking, setBooking] = useState<Booking | null>(null)
  const [feedback, setFeedback] = useState<{ type: "success" | "error"; message: string } | null>(null)
  const [busy, setBusy] = useState(false)
  const bookingRef = useRef(booking)
  bookingRef.current = booking
  useEffect(() => {
    const current = bookingRef.current
    if (!lot || !current || current.lotId !== lot.id) return
    let active = true
    reconcileOperatorBookingLookup(lot.id, current.id, "invalidation").then(snapshot => {
      if (!active || bookingRef.current?.id !== current.id) return
      if (snapshot.lastOutcome === "synced" && snapshot.data) {
        setBooking(snapshot.data)
      } else if (snapshot.lastOutcome === "error") {
        setFeedback({ type: "error", message: requestError(snapshot.lastError, "Could not refresh this session.") })
      }
    }).catch(error => {
      if (active && bookingRef.current?.id === current.id) setFeedback({ type: "error", message: requestError(error, "Could not refresh this session.") })
    })
    return () => { active = false }
  }, [lot?.id, realtimeRevision])

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (sitesLoading) return
    if (!lot) {
      setFeedback({ type: "error", message: "No assigned parking site is available." })
      return
    }
    if (!plate.trim()) {
      setFeedback({ type: "error", message: "Enter a license plate or reservation ID." })
      return
    }
    setBusy(true)
    setFeedback(null)
    setBooking(null)
    try {
      const result = await reservationApi.lookup(lot.id, plate.trim())
      if (result.status !== "checked-in") {
        setBooking(null)
        setFeedback({ type: "error", message: `Reservation ${result.id} is not currently checked in.` })
        return
      }
      setBooking(result)
    } catch (error) {
      setFeedback({ type: "error", message: error instanceof Error ? error.message : "Could not find the active parking session." })
    } finally {
      setBusy(false)
    }
  }

  async function completeCheckout() {
    if (readOnly || !lot || !booking || booking.lotId !== lot.id || busy || sitesLoading) return
    setBusy(true)
    setFeedback(null)
    try {
      await reservationApi.checkOut(lot.id, booking.id, booking.licensePlate)
      setFeedback({ type: "success", message: `Checkout completed for ${booking.licensePlate}.` })
      showUiToast("Checkout complete", `Checkout completed for ${booking.licensePlate}.`)
      setBooking(null)
      setPlate("")
      onDataChanged()
    } catch (error) {
      const message = error instanceof Error ? error.message : "Could not complete checkout."
      setFeedback({ type: "error", message })
      showUiToast("Checkout failed", message, "error")
    } finally {
      setBusy(false)
    }
  }

  const Container = readOnly ? "div" : "main"

  return (
    <Container className={readOnly ? "mx-auto flex w-full max-w-xl items-start py-4 sm:py-8" : "mx-auto flex min-h-[calc(100vh-60px)] w-full max-w-xl items-center px-5 py-10"}>
      <section className="w-full rounded-2xl border border-[var(--border)] bg-[var(--card)] p-6 shadow-sm sm:p-8">
        <PageHeader eyebrow={lot?.name ?? "No assigned site"} title={readOnly ? "Cashier View" : "Cashier POS Terminal"} description={readOnly ? "Look up the active parking session and view its recorded payment details." : "Look up the active parking session and confirm its checkout. Payment collection is not simulated."} />

        {sitesLoading && (
          <p role="status" className="sr-only">Loading assigned parking sites…</p>
        )}
        {sitesError && (
          <div role="alert" className="mt-4 rounded-xl border border-red-500/30 bg-red-500/10 p-3 text-sm text-red-600">
            {sitesError}
          </div>
        )}
        {!sitesLoading && !sitesError && !lot && (
          <p role="status" className="mt-4 text-sm text-[var(--muted)]">No assigned parking site is available.</p>
        )}

        {feedback && (
          <div role={feedback.type === "error" ? "alert" : "status"} className={`mt-4 rounded-xl border p-3.5 text-xs font-medium ${feedback.type === "error" ? "border-red-500/30 bg-red-500/10 text-red-600" : "border-green-500/30 bg-green-500/10 text-green-600"}`}>
            {feedback.message}
          </div>
        )}

        <form className="mt-6 space-y-4" onSubmit={handleSubmit}>
          <label className="block space-y-1.5 text-xs font-semibold text-[var(--fg)]">
            Vehicle License Plate or Reservation ID
            <input
              className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm text-[var(--fg)] outline-none focus:border-[var(--primary)] font-mono uppercase"
              value={plate}
              onChange={(event) => setPlate(event.target.value)}
              placeholder="e.g. 51A-123.45"
              required
              disabled={!lot || busy || sitesLoading}
            />
          </label>
          <button type="submit" className="btn-primary w-full justify-center py-2.5 text-sm mt-2" disabled={!lot || busy || sitesLoading}>
            <UntitledIcon name="search" size={16} /> {busy ? "Checking…" : "Find active session"}
          </button>
        </form>

        {booking && !sitesLoading && booking.lotId === lot?.id && (
          <div className="mt-5 rounded-xl border border-[var(--border)] bg-[var(--bg)] p-4">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <strong className="font-mono text-lg text-[var(--fg)]">{booking.licensePlate}</strong>
              <span className="rounded-full bg-green-500/10 px-2.5 py-1 text-xs font-semibold text-green-700">{booking.status === "checked-in" ? "Checked in" : booking.status.replace(/[-_]/g, " ")}</span>
            </div>
            <dl className="mt-3 grid grid-cols-2 gap-3 text-xs">
              <div><dt className="text-[var(--muted)]">Reservation</dt><dd className="mt-1 font-mono text-[var(--fg)]">{booking.id}</dd></div>
              <div><dt className="text-[var(--muted)]">Slot</dt><dd className="mt-1 font-semibold text-[var(--fg)]">{booking.slotNumber}</dd></div>
              <div><dt className="text-[var(--muted)]">Recorded amount</dt><dd className="mt-1 font-semibold text-[var(--fg)]">{booking.amount.toLocaleString("vi-VN")} VND</dd></div>
              <div><dt className="text-[var(--muted)]">Payment status</dt><dd className="mt-1 font-semibold text-[var(--fg)]">{booking.paymentStatus ?? "unknown"}</dd></div>
            </dl>
            {!readOnly && booking.status === "checked-in" && (
              <button type="button" className="btn-primary mt-4 w-full justify-center" onClick={() => void completeCheckout()} disabled={busy}>
                <UntitledIcon name="check-circle" size={16} /> Confirm vehicle checkout
              </button>
            )}
          </div>
        )}
      </section>
    </Container>
  )
}
