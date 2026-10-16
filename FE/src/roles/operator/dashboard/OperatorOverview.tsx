import { useMemo } from "react"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import type { ParkingLot, Booking, SupportTicket, AppNotification } from "../../../lib/types"
import { structureStore } from "../../owner/data/structureStore"

interface OperatorOverviewProps {
  lot: ParkingLot | null
  allLots: ParkingLot[]
  onSelectLot: (lotId: string) => void
  onNavigateTab: (tab: string, filter?: string) => void
  bookings: Booking[]
  tickets: SupportTicket[]
  notifications: AppNotification[]
}

export function OperatorOverview({
  lot,
  allLots,
  onSelectLot,
  onNavigateTab,
  bookings,
  tickets,
  notifications,
}: OperatorOverviewProps) {
  // Structure data for current lot to get canonical stats if available
  const structureStats = useMemo(() => {
    if (!lot) return null
    try {
      return structureStore.getCapacityStats(lot.id)
    } catch {
      return null
    }
  }, [lot])

  const totalSlots = lot?.totalSlots ?? 0
  const availableSlots = structureStats?.availableCapacity ?? lot?.slots.filter((s) => s.status === "available").length ?? 0
  const occupiedSlots = structureStats?.occupiedSpaces ?? lot?.slots.filter((s) => s.status === "occupied").length ?? 0
  const reservedSlots = (structureStats?.reservedSpaces ?? 0) + (structureStats?.pendingPaymentHolds ?? 0) || (lot?.slots.filter((s) => s.status === "reserved").length ?? 0)
  const backupSlots = structureStats?.backupCapacity ?? 0
  const maintenanceSlots = (structureStats?.maintenanceSpaces ?? 0) + (structureStats?.disabledSpaces ?? 0) || (lot?.slots.filter((s) => s.status === "maintenance" || s.status === "disabled").length ?? 0)

  const occupancyRate = totalSlots > 0 ? Math.min(100, Math.round(((occupiedSlots + reservedSlots) / totalSlots) * 100)) : 0

  const activeBookings = useMemo(() => {
    return bookings.filter(
      (b) => (!lot || b.lotId === lot.id) && ["confirmed", "checked-in"].includes(b.status),
    )
  }, [bookings, lot])

  const openTickets = useMemo(() => {
    return tickets.filter((t) => t.status === "open" || t.status === "in-progress")
  }, [tickets])

  const recentNotifications = useMemo(() => {
    return notifications.slice(0, 5)
  }, [notifications])

  if (!lot) {
    return (
      <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-8 text-center text-sm text-[var(--muted)]">
        <UntitledIcon name="building" size={32} />
        <p className="mt-2 font-medium">No assigned parking site found.</p>
        <p className="text-xs">Please contact your facility administrator or owner.</p>
      </div>
    )
  }

  return (
    <div className="space-y-6">
      {/* Site Header & Selector */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <div className="flex items-center gap-2">
            <span className="inline-flex items-center gap-1.5 rounded-md bg-[var(--primary)]/10 px-2 py-0.5 text-xs font-semibold text-[var(--primary)]">
              <UntitledIcon name="building" size={13} />
              {lot.type === "outdoor" ? "Outdoor Lot" : lot.type === "basement" ? "Underground Lot" : "Multi-Storey Lot"}
            </span>
            <span className="text-xs text-[var(--muted)]">·</span>
            <span className="text-xs text-[var(--muted)]">{lot.address}</span>
          </div>
          <h1 className="mt-1 text-2xl font-bold tracking-tight text-[var(--fg)]">
            {lot.name}
          </h1>
          <p className="text-sm text-[var(--muted)]">
            Live operational dashboard, parking occupancy, and gate controls.
          </p>
        </div>

        {allLots.length > 1 && (
          <div className="flex items-center gap-2 rounded-lg border border-[var(--border)] bg-[var(--card)] p-1.5">
            <span className="text-xs font-medium text-[var(--muted)] pl-2">Site:</span>
            <select
              className="rounded-md border border-[var(--border)] bg-[var(--bg)] px-3 py-1.5 text-sm font-medium text-[var(--fg)] outline-none focus:border-[var(--primary)]"
              value={lot.id}
              onChange={(e) => onSelectLot(e.target.value)}
            >
              {allLots.map((l) => (
                <option key={l.id} value={l.id}>
                  {l.name}
                </option>
              ))}
            </select>
          </div>
        )}
      </div>

      {/* Primary KPI Grid (clickable cards) */}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
        <button
          type="button"
          onClick={() => onNavigateTab("status")}
          className="group flex flex-col justify-between rounded-xl border border-[var(--border)] bg-[var(--card)] p-4 text-left transition hover:border-[var(--primary)]"
        >
          <div className="flex items-center justify-between text-xs font-medium text-[var(--muted)]">
            <span>Total Capacity</span>
            <UntitledIcon name="building" size={16} />
          </div>
          <div className="mt-3">
            <div className="text-2xl font-bold text-[var(--fg)]">{totalSlots}</div>
            <div className="mt-0.5 text-[11px] text-[var(--muted)]">Configured spaces</div>
          </div>
        </button>

        <button
          type="button"
          onClick={() => onNavigateTab("status", "available")}
          className="group flex flex-col justify-between rounded-xl border border-[var(--border)] bg-[var(--card)] p-4 text-left transition hover:border-green-500"
        >
          <div className="flex items-center justify-between text-xs font-medium text-green-600">
            <span>Available Vacant</span>
            <UntitledIcon name="check-circle" size={16} />
          </div>
          <div className="mt-3">
            <div className="text-2xl font-bold text-green-600">{availableSlots}</div>
            <div className="mt-0.5 text-[11px] text-[var(--muted)]">Ready for walk-in/booking</div>
          </div>
        </button>

        <button
          type="button"
          onClick={() => onNavigateTab("status", "occupied")}
          className="group flex flex-col justify-between rounded-xl border border-[var(--border)] bg-[var(--card)] p-4 text-left transition hover:border-red-500"
        >
          <div className="flex items-center justify-between text-xs font-medium text-red-600">
            <span>In Use / Parked</span>
            <UntitledIcon name="car" size={16} />
          </div>
          <div className="mt-3">
            <div className="text-2xl font-bold text-red-600">{occupiedSlots}</div>
            <div className="mt-0.5 text-[11px] text-[var(--muted)]">Vehicles physically in lot</div>
          </div>
        </button>

        <button
          type="button"
          onClick={() => onNavigateTab("status", "reserved")}
          className="group flex flex-col justify-between rounded-xl border border-[var(--border)] bg-[var(--card)] p-4 text-left transition hover:border-amber-500"
        >
          <div className="flex items-center justify-between text-xs font-medium text-amber-600">
            <span>Reserved / Holds</span>
            <UntitledIcon name="clock" size={16} />
          </div>
          <div className="mt-3">
            <div className="text-2xl font-bold text-amber-600">{reservedSlots}</div>
            <div className="mt-0.5 text-[11px] text-[var(--muted)]">Incoming & payment holds</div>
          </div>
        </button>

        <button
          type="button"
          onClick={() => onNavigateTab("slots")}
          className="group flex flex-col justify-between rounded-xl border border-[var(--border)] bg-[var(--card)] p-4 text-left transition hover:border-purple-500"
        >
          <div className="flex items-center justify-between text-xs font-medium text-purple-600">
            <span>Backup Designated</span>
            <UntitledIcon name="shield" size={16} />
          </div>
          <div className="mt-3">
            <div className="text-2xl font-bold text-purple-600">{backupSlots}</div>
            <div className="mt-0.5 text-[11px] text-[var(--muted)]">Emergency / overflow pool</div>
          </div>
        </button>

        <button
          type="button"
          onClick={() => onNavigateTab("status", "maintenance")}
          className="group flex flex-col justify-between rounded-xl border border-[var(--border)] bg-[var(--card)] p-4 text-left transition hover:border-slate-500"
        >
          <div className="flex items-center justify-between text-xs font-medium text-[var(--muted)]">
            <span>Maintenance / Off</span>
            <UntitledIcon name="wrench" size={16} />
          </div>
          <div className="mt-3">
            <div className="text-2xl font-bold text-[var(--fg)]">{maintenanceSlots}</div>
            <div className="mt-0.5 text-[11px] text-[var(--muted)]">Unavailable slots</div>
          </div>
        </button>
      </div>

      {/* Quick Action Toolbar */}
      <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-2">
            <span className="font-semibold text-sm text-[var(--fg)]">Quick Actions:</span>
            <span className="text-xs text-[var(--muted)]">Direct access to critical operator modules</span>
          </div>
          <div className="flex flex-wrap gap-2">
            <button
              type="button"
              className="btn-primary text-xs py-1.5 px-3"
              onClick={() => onNavigateTab("checkin")}
            >
              <UntitledIcon name="check-circle" size={14} /> Gate Check-In / Out
            </button>
            <button
              type="button"
              className="btn-outline text-xs py-1.5 px-3"
              onClick={() => onNavigateTab("status")}
            >
              <UntitledIcon name="grid" size={14} /> 3D Digital Twin Map
            </button>
            <button
              type="button"
              className="btn-outline text-xs py-1.5 px-3"
              onClick={() => onNavigateTab("slots")}
            >
              <UntitledIcon name="shield" size={14} /> Manage Backup Slots
            </button>
            <button
              type="button"
              className="btn-outline text-xs py-1.5 px-3"
              onClick={() => onNavigateTab("tickets")}
            >
              <UntitledIcon name="ticket" size={14} /> Driver Tickets ({openTickets.length})
            </button>
          </div>
        </div>
      </div>

      {/* Main Grid: Occupancy Bar & Active Bookings */}
      <div className="grid gap-6 lg:grid-cols-3">
        {/* Left Column (2 cols): Occupancy Breakdown & Active Queue */}
        <div className="space-y-6 lg:col-span-2">
          {/* Real-time Occupancy Progress Bar */}
          <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5">
            <div className="flex items-center justify-between mb-3">
              <div>
                <h3 className="font-semibold text-[var(--fg)]">Lot Occupancy Rate</h3>
                <p className="text-xs text-[var(--muted)]">Ratio of occupied and reserved capacity</p>
              </div>
              <div className="text-right">
                <span className="text-xl font-extrabold text-[var(--fg)]">{occupancyRate}%</span>
                <span className="text-xs text-[var(--muted)] ml-1">utilized</span>
              </div>
            </div>

            {/* Segmented Progress Bar */}
            <div className="h-3.5 w-full overflow-hidden rounded-full bg-[var(--border)] flex">
              <div
                style={{ width: `${totalSlots ? (occupiedSlots / totalSlots) * 100 : 0}%` }}
                className="bg-red-500 transition-all duration-500"
                title={`Occupied: ${occupiedSlots}`}
              />
              <div
                style={{ width: `${totalSlots ? (reservedSlots / totalSlots) * 100 : 0}%` }}
                className="bg-amber-500 transition-all duration-500"
                title={`Reserved: ${reservedSlots}`}
              />
              <div
                style={{ width: `${totalSlots ? (backupSlots / totalSlots) * 100 : 0}%` }}
                className="bg-purple-500 transition-all duration-500"
                title={`Backup: ${backupSlots}`}
              />
              <div
                style={{ width: `${totalSlots ? (maintenanceSlots / totalSlots) * 100 : 0}%` }}
                className="bg-slate-400 transition-all duration-500"
                title={`Maintenance: ${maintenanceSlots}`}
              />
            </div>

            <div className="mt-3 flex flex-wrap items-center gap-4 text-xs text-[var(--muted)]">
              <div className="flex items-center gap-1.5">
                <span className="h-2.5 w-2.5 rounded-full bg-red-500" />
                <span>Occupied ({occupiedSlots})</span>
              </div>
              <div className="flex items-center gap-1.5">
                <span className="h-2.5 w-2.5 rounded-full bg-amber-500" />
                <span>Reserved ({reservedSlots})</span>
              </div>
              <div className="flex items-center gap-1.5">
                <span className="h-2.5 w-2.5 rounded-full bg-purple-500" />
                <span>Backup ({backupSlots})</span>
              </div>
              <div className="flex items-center gap-1.5">
                <span className="h-2.5 w-2.5 rounded-full bg-green-500" />
                <span>Available ({availableSlots})</span>
              </div>
            </div>
          </div>

          {/* Active Bookings Requiring Attention */}
          <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5">
            <div className="flex items-center justify-between mb-4">
              <div>
                <h3 className="font-semibold text-[var(--fg)]">Active Parking Operations</h3>
                <p className="text-xs text-[var(--muted)]">
                  Vehicles currently in lot or confirmed for immediate check-in
                </p>
              </div>
              <button
                type="button"
                className="text-xs font-semibold text-[var(--primary)] hover:underline"
                onClick={() => onNavigateTab("checkin")}
              >
                View all ({activeBookings.length}) →
              </button>
            </div>

            {activeBookings.length === 0 ? (
              <div className="rounded-lg border border-dashed border-[var(--border)] p-6 text-center text-xs text-[var(--muted)]">
                No active bookings currently in or arriving at this site.
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-left text-sm">
                  <thead>
                    <tr className="border-b border-[var(--border)] text-xs text-[var(--muted)]">
                      <th className="pb-2 font-medium">Plate / Vehicle</th>
                      <th className="pb-2 font-medium">Slot</th>
                      <th className="pb-2 font-medium">Time Window</th>
                      <th className="pb-2 font-medium">Status</th>
                      <th className="pb-2 text-right font-medium">Action</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border)]">
                    {activeBookings.slice(0, 5).map((booking) => (
                      <tr key={booking.id} className="text-xs">
                        <td className="py-2.5 font-semibold text-[var(--fg)]">
                          <span className="font-mono bg-[var(--bg)] px-1.5 py-0.5 rounded border border-[var(--border)]">
                            {booking.licensePlate}
                          </span>
                        </td>
                        <td className="py-2.5 text-[var(--fg)] font-medium">
                          {booking.slotNumber}
                        </td>
                        <td className="py-2.5 text-[var(--muted)]">
                          {new Date(booking.startTime).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })} –{" "}
                          {new Date(booking.endTime).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
                        </td>
                        <td className="py-2.5">
                          <span
                            className={`rounded-full px-2 py-0.5 text-[10px] font-bold uppercase ${
                              booking.status === "checked-in"
                                ? "bg-red-500/10 text-red-600"
                                : "bg-green-500/10 text-green-600"
                            }`}
                          >
                            {booking.status === "checked-in" ? "Parked" : "Confirmed"}
                          </span>
                        </td>
                        <td className="py-2.5 text-right">
                          <button
                            type="button"
                            className="btn-outline text-[11px] py-1 px-2.5"
                            onClick={() => onNavigateTab("checkin", booking.licensePlate)}
                          >
                            {booking.status === "checked-in" ? "Check-Out" : "Check-In"}
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>

        {/* Right Column (1 col): Driver Tickets & Recent Notifications */}
        <div className="space-y-6">
          {/* Driver Support Queue */}
          <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5">
            <div className="flex items-center justify-between mb-3">
              <div>
                <h3 className="font-semibold text-[var(--fg)]">Pending Driver Tickets</h3>
                <p className="text-xs text-[var(--muted)]">Support issues needing response</p>
              </div>
              <span className="rounded-full bg-amber-500/10 px-2 py-0.5 text-xs font-bold text-amber-600">
                {openTickets.length} open
              </span>
            </div>

            {openTickets.length === 0 ? (
              <div className="rounded-lg border border-dashed border-[var(--border)] p-5 text-center text-xs text-[var(--muted)]">
                All driver support requests are currently resolved.
              </div>
            ) : (
              <div className="space-y-2.5">
                {openTickets.slice(0, 3).map((ticket) => (
                  <div
                    key={ticket.id}
                    className="rounded-lg border border-[var(--border)] bg-[var(--bg)] p-3 text-xs"
                  >
                    <div className="flex items-center justify-between font-semibold text-[var(--fg)]">
                      <span>{ticket.subject}</span>
                      <span
                        className={`rounded px-1.5 py-0.5 text-[10px] uppercase font-bold ${
                          ticket.status === "in-progress"
                            ? "bg-amber-500/15 text-amber-600"
                            : "bg-blue-500/15 text-blue-600"
                        }`}
                      >
                        {ticket.status}
                      </span>
                    </div>
                    <p className="mt-1 line-clamp-2 text-[var(--muted)]">{ticket.message}</p>
                    <div className="mt-2 flex items-center justify-between text-[11px] text-[var(--muted)]">
                      <span>{ticket.userName}</span>
                      <button
                        type="button"
                        className="text-[var(--primary)] hover:underline font-medium"
                        onClick={() => onNavigateTab("tickets")}
                      >
                        Resolve →
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Operational Notifications / Feed */}
          <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5">
            <div className="flex items-center justify-between mb-3">
              <h3 className="font-semibold text-[var(--fg)]">Operational Feed</h3>
              <UntitledIcon name="activity" size={16} />
            </div>

            {recentNotifications.length === 0 ? (
              <p className="text-xs text-[var(--muted)]">No recent operational notifications.</p>
            ) : (
              <div className="space-y-3">
                {recentNotifications.map((notif) => (
                  <div key={notif.id} className="text-xs border-b border-[var(--border)] pb-2.5 last:border-0 last:pb-0">
                    <div className="flex items-center justify-between">
                      <strong className="text-[var(--fg)] font-semibold">{notif.title}</strong>
                      <span className="text-[10px] text-[var(--muted)]">
                        {new Date(notif.timestamp).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
                      </span>
                    </div>
                    <p className="mt-0.5 text-[var(--muted)] line-clamp-2">{notif.message}</p>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
