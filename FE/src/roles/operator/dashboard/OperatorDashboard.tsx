import { useEffect, useState, useMemo } from "react"
import { useApp } from "../../../context/AppContext"
import { CheckInOut } from "../check-in-out/CheckInOut"
import { LotStatus } from "../parking-lot/LotStatus"
import { EmergencyPanel } from "../emergency/EmergencyPanel"
import { SlotManagement } from "../parking-slots/SlotManagement"
import { TicketManagement } from "../tickets/TicketManagement"
import { OperatorAssignments } from "./OperatorAssignments"
import { OperatorOverview } from "./OperatorOverview"
import type { OperatorAccessRole, SupportTicket, AppNotification } from "../../../lib/types"
import { DashboardSidebar, type DashboardNavGroup } from "../../../components/layout/DashboardSidebar"
import { PaymentMethodLogo } from "../../../components/payment/PaymentMethodLogo"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import {
  loadOperatorSites,
  operatorOperationsApi,
  reservationApi,
  type OperatorSite,
} from "../../../lib/operatorApi"
import type { Booking } from "../../../lib/types"

export type OperatorTab =
  | "overview"
  | "checkin"
  | "checkout"
  | "status"
  | "slots"
  | "tickets"
  | "emergency"
  | "assignments"
  | "finance"

export function OperatorDashboard({
  accessRoleOverride,
}: {
  accessRoleOverride?: OperatorAccessRole
} = {}) {
  const { user } = useApp()

  const accessRole = accessRoleOverride ?? user?.operatorRole ?? "operation"

  const [tab, setTab] = useState<OperatorTab>(
    accessRole === "financial" ? "finance" : "overview",
  )

  const [targetPlateQuery, setTargetPlateQuery] = useState("")
  const [targetSlotStatus, setTargetSlotStatus] = useState<
    "all" | "available" | "occupied" | "reserved" | "backup" | "maintenance"
  >("all")

  const [allLots, setAllLots] = useState<OperatorSite[]>([])
  const [activeSiteId, setActiveSiteId] = useState<string>("")
  const [tickets, setTickets] = useState<SupportTicket[]>([])
  const [notifications, setNotifications] = useState<AppNotification[]>([])
  const [currentLotBookings, setCurrentLotBookings] = useState<Booking[]>([])
  const [allReportBookings, setAllReportBookings] = useState<Booking[]>([])
  const [dataError, setDataError] = useState("")
  const [sitesLoading, setSitesLoading] = useState(true)
  const [refreshKey, setRefreshKey] = useState(0)

  useEffect(() => {
    const refresh = () => setRefreshKey((value) => value + 1)
    window.addEventListener("sp-operator-refresh", refresh)
    return () => window.removeEventListener("sp-operator-refresh", refresh)
  }, [user])

  useEffect(() => {
    const onNavigate = (event: Event) => {
      const detail = (event as CustomEvent<string>).detail as OperatorTab
      if (["overview", "checkin", "checkout", "status", "emergency", "slots", "tickets", "assignments", "finance"].includes(detail))
        setTab(detail)
    }
    window.addEventListener("sp:dashboard-nav", onNavigate)
    return () => window.removeEventListener("sp:dashboard-nav", onNavigate)
  }, [])

  useEffect(() => {
    let active = true
    setSitesLoading(true)
    setDataError("")
    loadOperatorSites(user)
      .then((sites) => {
        if (active) setAllLots(sites)
      })
      .catch((error) => {
        if (active) setDataError(error instanceof Error ? error.message : "Could not load assigned parking sites.")
      })
      .finally(() => {
        if (active) setSitesLoading(false)
      })
    return () => {
      active = false
    }
  }, [user])

  const ownerLots = allLots

  const selectedLot = useMemo(() => {
    return (
      (activeSiteId ? ownerLots.find((site) => site.id === activeSiteId) : null) ??
      ownerLots[0] ??
      null
    )
  }, [activeSiteId, ownerLots])

  useEffect(() => {
    if (!selectedLot) {
      setCurrentLotBookings([])
      setTickets([])
      setNotifications([])
      return
    }
    let active = true
    setDataError("")
    Promise.all([
      reservationApi.list(selectedLot.id),
      operatorOperationsApi.listTickets(selectedLot.id),
      operatorOperationsApi.listNotifications(selectedLot.id),
    ])
      .then(([bookings, nextTickets, nextNotifications]) => {
        if (!active) return
        setCurrentLotBookings(bookings)
        setTickets(nextTickets)
        setNotifications(nextNotifications)
      })
      .catch((error) => {
        if (active) setDataError(error instanceof Error ? error.message : "Could not load operator data.")
      })
    return () => {
      active = false
    }
  }, [selectedLot?.id, refreshKey])

  useEffect(() => {
    const reportSites = accessRole === "financial" ? ownerLots : selectedLot ? [selectedLot] : []
    if (!reportSites.length) {
      setAllReportBookings([])
      return
    }
    let active = true
    Promise.all(reportSites.map((site) => reservationApi.list(site.id)))
      .then((bookings) => {
        if (active) setAllReportBookings(bookings.flat())
      })
      .catch((error) => {
        if (active) setDataError(error instanceof Error ? error.message : "Could not load reservation reports.")
      })
    return () => {
      active = false
    }
  }, [accessRole, ownerLots, selectedLot?.id, refreshKey])

  // Cashier role shortcut view
  if (accessRole === "cashier") {
    return <CashierPOS lot={selectedLot} onDataChanged={() => setRefreshKey((value) => value + 1)} />
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
                onClick: () => setTab("status"),
              },
              {
                id: "slots",
                label: "Backup Slots & Buffer",
                icon: "shield",
                active: tab === "slots",
                onClick: () => setTab("slots"),
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
    if (nextTab === "status" && payload) {
      switch (payload) {
        case "available":
        case "occupied":
        case "reserved":
        case "backup":
        case "maintenance":
          setTargetSlotStatus(payload)
          setTab("status")
          return
      }
    }
    setTab(nextTab as OperatorTab)
  }

  return (
    <DashboardSidebar groups={sidebarGroups}>
      <main className="min-w-0 overflow-y-auto p-4 sm:p-7">
        {dataError && (
          <div role="alert" className="mb-5 rounded-xl border border-red-500/30 bg-red-500/10 p-3 text-sm text-red-600">
            {dataError}
          </div>
        )}
        {sitesLoading && (
          <p role="status" className="mb-5 text-sm text-[var(--muted)]">Loading assigned parking sites…</p>
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
            <select
              className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-1.5 text-xs font-semibold text-[var(--fg)] outline-none focus:border-[var(--primary)]"
              value={selectedLot?.id ?? ""}
              onChange={(e) => setActiveSiteId(e.target.value)}
            >
              {ownerLots.map((lot) => (
                <option key={lot.id} value={lot.id}>
                  {lot.name} ({lot.totalSlots} slots)
                </option>
              ))}
            </select>
          </div>
        )}

        {/* Tab View Container */}
        <div className="animate-in">
          {/* TAB 1: OVERVIEW */}
          {accessRole === "operation" && tab === "overview" && (
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
              onDataChanged={() => setRefreshKey((value) => value + 1)}
            />
          )}

          {/* TAB 3: LOT STATUS & 3D STRUCTURE */}
          {accessRole === "operation" && tab === "status" && (
            <LotStatus lot={selectedLot} initialStatusFilter={targetSlotStatus} />
          )}

          {/* TAB 4: BACKUP SLOTS & BUFFER */}
          {accessRole === "operation" && tab === "slots" && (
            <SlotManagement lot={selectedLot} />
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
            <OperatorAssignments currentLot={selectedLot} />
          )}

          {/* TAB 8: FINANCIAL REPORTS */}
          {accessRole === "financial" && tab === "finance" && (
            <section className="mx-auto w-full max-w-5xl space-y-6">
              <header>
                <p className="text-sm font-medium text-[var(--primary)]">
                  {user?.role === "owner" || user?.operatorSiteId === "all"
                    ? "All Assigned Sites"
                    : (selectedLot?.name ?? "Assigned parking lot")}
                </p>
                <h1 className="mt-1 text-2xl font-bold text-[var(--fg)]">
                  Financial Reports & Reconciliation
                </h1>
                <p className="mt-2 text-sm text-[var(--muted)]">
                  Completed booking revenue, collection channels, and reconciliation registry.
                </p>
              </header>

              <div className="grid gap-4 sm:grid-cols-3">
                <FinanceMetric
                  label="Completed Bookings"
                  value={completedBookings.length.toLocaleString("vi-VN")}
                />
                <FinanceMetric
                  label="Total Recorded Revenue"
                  value={`${totalRevenue.toLocaleString("vi-VN")} ₫`}
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
                      <th className="px-4 py-3 text-right font-medium">Amount (₫)</th>
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
                          {b.amount.toLocaleString("vi-VN")} ₫
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
      <p className="text-xs font-semibold text-[var(--muted)] uppercase tracking-wider">{label}</p>
      <p className="mt-2 text-2xl font-bold text-[var(--fg)]">{value}</p>
    </div>
  )
}

function CashierPOS({
  lot,
  onDataChanged,
}: {
  lot: OperatorSite | null
  onDataChanged: () => void
}) {
  const [plate, setPlate] = useState("")
  const [booking, setBooking] = useState<Booking | null>(null)
  const [feedback, setFeedback] = useState<{ type: "success" | "error"; message: string } | null>(null)
  const [busy, setBusy] = useState(false)

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
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
    if (!lot || !booking || busy) return
    setBusy(true)
    setFeedback(null)
    try {
      await reservationApi.checkOut(lot.id, booking.id, booking.licensePlate)
      setFeedback({ type: "success", message: `Checkout completed for ${booking.licensePlate}.` })
      setBooking(null)
      setPlate("")
      onDataChanged()
    } catch (error) {
      setFeedback({ type: "error", message: error instanceof Error ? error.message : "Could not complete checkout." })
    } finally {
      setBusy(false)
    }
  }

  return (
    <main className="mx-auto flex min-h-[calc(100vh-60px)] w-full max-w-xl items-center px-5 py-10">
      <section className="w-full rounded-2xl border border-[var(--border)] bg-[var(--card)] p-6 shadow-sm sm:p-8">
        <p className="text-xs font-bold uppercase tracking-wider text-[var(--primary)]">{lot?.name ?? "No assigned site"}</p>
        <h1 className="mt-1 text-2xl font-bold text-[var(--fg)]">
          Cashier POS Terminal
        </h1>
        <p className="mt-2 text-sm text-[var(--muted)]">
          Look up the active parking session and confirm its checkout. Payment collection is not simulated.
        </p>

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
              disabled={!lot || busy}
            />
          </label>
          <button type="submit" className="btn-primary w-full justify-center py-2.5 text-sm mt-2" disabled={!lot || busy}>
            <UntitledIcon name="search" size={16} /> {busy ? "Checking…" : "Find active session"}
          </button>
        </form>

        {booking && (
          <div className="mt-5 rounded-xl border border-[var(--border)] bg-[var(--bg)] p-4">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <strong className="font-mono text-lg text-[var(--fg)]">{booking.licensePlate}</strong>
              <span className="rounded-full bg-green-500/10 px-2.5 py-1 text-xs font-semibold text-green-700">Checked in</span>
            </div>
            <dl className="mt-3 grid grid-cols-2 gap-3 text-xs">
              <div><dt className="text-[var(--muted)]">Reservation</dt><dd className="mt-1 font-mono text-[var(--fg)]">{booking.id}</dd></div>
              <div><dt className="text-[var(--muted)]">Slot</dt><dd className="mt-1 font-semibold text-[var(--fg)]">{booking.slotNumber}</dd></div>
              <div><dt className="text-[var(--muted)]">Recorded amount</dt><dd className="mt-1 font-semibold text-[var(--fg)]">{booking.amount.toLocaleString("vi-VN")} ₫</dd></div>
              <div><dt className="text-[var(--muted)]">Payment status</dt><dd className="mt-1 font-semibold text-[var(--fg)]">{booking.paymentStatus ?? "unknown"}</dd></div>
            </dl>
            <button type="button" className="btn-primary mt-4 w-full justify-center" onClick={() => void completeCheckout()} disabled={busy}>
              <UntitledIcon name="check-circle" size={16} /> Confirm vehicle checkout
            </button>
          </div>
        )}
      </section>
    </main>
  )
}
