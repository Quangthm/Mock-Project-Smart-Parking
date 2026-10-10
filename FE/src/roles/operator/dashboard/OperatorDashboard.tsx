import { useEffect, useState, useMemo } from "react"
import { useApp } from "../../../context/AppContext"
import { operatorData as store } from "../data/data"
import { CheckInOut } from "../check-in-out/CheckInOut"
import { LotStatus } from "../parking-lot/LotStatus"
import { EmergencyPanel } from "../emergency/EmergencyPanel"
import { SlotManagement } from "../parking-slots/SlotManagement"
import { TicketManagement } from "../tickets/TicketManagement"
import { OperatorAssignments } from "./OperatorAssignments"
import { OperatorOverview } from "./OperatorOverview"
import type { OperatorAccessRole, ParkingLot, SupportTicket, AppNotification } from "../../../lib/types"
import { DashboardSidebar, type DashboardNavGroup } from "../../../components/layout/DashboardSidebar"
import { PaymentMethodLogo } from "../../../components/payment/PaymentMethodLogo"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"

export type OperatorTab =
  | "overview"
  | "checkin"
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

  // Available lots
  const [allLots, setAllLots] = useState<ParkingLot[]>(() => store.getLots())
  const [activeSiteId, setActiveSiteId] = useState<string>("")

  // Real-time synchronization
  const [tickets, setTickets] = useState<SupportTicket[]>(() => store.getTickets())
  const [notifications, setNotifications] = useState<AppNotification[]>(() =>
    user ? store.getNotifications(user.id) : [],
  )

  useEffect(() => {
    const refresh = () => {
      setAllLots(store.getLots())
      setTickets(store.getTickets())
      if (user) setNotifications(store.getNotifications(user.id))
    }
    window.addEventListener("sp-data-change", refresh)
    window.addEventListener("storage", refresh)
    return () => {
      window.removeEventListener("sp-data-change", refresh)
      window.removeEventListener("storage", refresh)
    }
  }, [user])

  // Listen to navigation events from topbar search or notifications
  useEffect(() => {
    const onNavigate = (event: Event) => {
      const detail = (event as CustomEvent<string>).detail as OperatorTab
      if (
        [
          "overview",
          "checkin",
          "status",
          "emergency",
          "slots",
          "tickets",
          "assignments",
          "finance",
        ].includes(detail)
      ) {
        setTab(detail)
      }
    }

    window.addEventListener("sp:dashboard-nav", onNavigate)
    return () => window.removeEventListener("sp:dashboard-nav", onNavigate)
  }, [])

  const assignedOwnerId = user?.role === "owner" ? user.id : user?.ownerId

  const ownerLots = useMemo(() => {
    const filtered = allLots.filter(
      (lot) =>
        (!assignedOwnerId || lot.ownerId === assignedOwnerId) &&
        (user?.role === "owner" ||
          !user?.operatorSiteId ||
          user.operatorSiteId === "all" ||
          lot.id === user?.operatorSiteId),
    )
    return filtered.length > 0 ? filtered : allLots
  }, [allLots, assignedOwnerId, user])

  const selectedLot = useMemo(() => {
    return (
      (activeSiteId ? ownerLots.find((l) => l.id === activeSiteId) : null) ??
      ownerLots[0] ??
      null
    )
  }, [activeSiteId, ownerLots])

  // All bookings for current selected lot
  const currentLotBookings = useMemo(() => {
    return selectedLot ? store.getBookingsByLot(selectedLot.id) : []
  }, [selectedLot])

  // Cashier role shortcut view
  if (accessRole === "cashier") {
    return <CashierPOS lotName={selectedLot?.name ?? "Assigned parking lot"} />
  }

  // Financial reconciliation metrics
  const reportLots =
    accessRole === "financial" ? ownerLots : selectedLot ? [selectedLot] : []
  const allReportBookings = reportLots.flatMap((lot) => store.getBookingsByLot(lot.id))
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
                label: "Gate Check-In / Out",
                icon: "check-circle",
                active: tab === "checkin",
                onClick: () => {
                  setTargetPlateQuery("")
                  setTab("checkin")
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
    if (nextTab === "checkin" && payload) {
      setTargetPlateQuery(payload)
      setTab("checkin")
      return
    }
    setTab(nextTab as OperatorTab)
  }

  return (
    <DashboardSidebar groups={sidebarGroups}>
      <main className="min-w-0 overflow-y-auto p-4 sm:p-7">
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
          {accessRole === "operation" && tab === "checkin" && (
            <CheckInOut
              lot={selectedLot}
              initialPlateQuery={targetPlateQuery}
            />
          )}

          {/* TAB 3: LOT STATUS & 3D STRUCTURE */}
          {accessRole === "operation" && tab === "status" && (
            <LotStatus lot={selectedLot} />
          )}

          {/* TAB 4: BACKUP SLOTS & BUFFER */}
          {accessRole === "operation" && tab === "slots" && (
            <SlotManagement lot={selectedLot} />
          )}

          {/* TAB 5: TICKETS */}
          {accessRole === "operation" && tab === "tickets" && (
            <TicketManagement />
          )}

          {/* TAB 6: EMERGENCY */}
          {accessRole === "operation" && tab === "emergency" && (
            <EmergencyPanel />
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

function CashierPOS({ lotName }: { lotName: string }) {
  const [plate, setPlate] = useState("")
  const [amount, setAmount] = useState("")
  const [notice, setNotice] = useState("")

  function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!plate.trim() || Number(amount) <= 0) {
      setNotice("Enter a license plate and a payment amount greater than zero.")
      return
    }

    setNotice(`Payment of ${Number(amount).toLocaleString("vi-VN")} ₫ recorded for plate ${plate.trim().toUpperCase()} at ${lotName}.`)
    setPlate("")
    setAmount("")
  }

  return (
    <main className="mx-auto flex min-h-[calc(100vh-60px)] w-full max-w-xl items-center px-5 py-10">
      <section className="w-full rounded-2xl border border-[var(--border)] bg-[var(--card)] p-6 shadow-sm sm:p-8">
        <p className="text-xs font-bold uppercase tracking-wider text-[var(--primary)]">{lotName}</p>
        <h1 className="mt-1 text-2xl font-bold text-[var(--fg)]">
          Cashier POS Terminal
        </h1>
        <p className="mt-2 text-sm text-[var(--muted)]">
          Direct collection terminal for vehicle egress payments and parking passes.
        </p>

        {notice && (
          <div className="mt-4 rounded-xl border border-green-500/30 bg-green-500/10 p-3.5 text-xs text-green-600 font-medium">
            {notice}
          </div>
        )}

        <form className="mt-6 space-y-4" onSubmit={handleSubmit}>
          <label className="block space-y-1.5 text-xs font-semibold text-[var(--fg)]">
            Vehicle License Plate
            <input
              className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm text-[var(--fg)] outline-none focus:border-[var(--primary)] font-mono uppercase"
              value={plate}
              onChange={(event) => setPlate(event.target.value)}
              placeholder="e.g. 51A-123.45"
              required
            />
          </label>
          <label className="block space-y-1.5 text-xs font-semibold text-[var(--fg)]">
            Payment Amount (₫)
            <input
              className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm text-[var(--fg)] outline-none focus:border-[var(--primary)]"
              type="number"
              min="1"
              value={amount}
              onChange={(event) => setAmount(event.target.value)}
              placeholder="Enter amount"
              required
            />
          </label>
          <button type="submit" className="btn-primary w-full justify-center py-2.5 text-sm mt-2">
            <UntitledIcon name="banknote" size={16} /> Collect Payment & Open Gate
          </button>
        </form>
      </section>
    </main>
  )
}
