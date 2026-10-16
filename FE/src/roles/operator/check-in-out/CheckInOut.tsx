import { useState, useMemo, useEffect } from "react"
import { operatorData as store } from "../data/data"
import { useApp } from "../../../context/AppContext"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { PaymentMethodLogo } from "../../../components/payment/PaymentMethodLogo"
import type { Booking, ParkingLot, BookingStatus } from "../../../lib/types"

interface CheckInOutProps {
  lot: ParkingLot | null
  initialPlateQuery?: string
}

export function CheckInOut({ lot, initialPlateQuery = "" }: CheckInOutProps) {
  const { user } = useApp()

  // Quick lookup state
  const [quickInput, setQuickInput] = useState(initialPlateQuery)
  const [quickResult, setQuickResult] = useState<{
    booking: Booking
    action: "check-in" | "check-out"
  } | null>(null)
  const [quickError, setQuickError] = useState("")

  // Table filters & search
  const [searchQuery, setSearchQuery] = useState("")
  const [statusFilter, setStatusFilter] = useState<"all" | BookingStatus>("all")
  const [vehicleTypeFilter, setVehicleTypeFilter] = useState<"all" | "car" | "motorcycle">("all")
  const [currentPage, setCurrentPage] = useState(1)
  const itemsPerPage = 8

  // Selected booking for detailed view modal
  const [selectedBooking, setSelectedBooking] = useState<Booking | null>(null)

  // Confirmation modal state for check-in / check-out
  const [confirmingAction, setConfirmingAction] = useState<{
    booking: Booking
    action: "check-in" | "check-out"
  } | null>(null)
  const [actionLoading, setActionLoading] = useState(false)
  const [actionFeedback, setActionFeedback] = useState<{
    type: "success" | "error"
    message: string
  } | null>(null)

  // All bookings for the selected lot
  const [bookings, setBookings] = useState<Booking[]>(() =>
    lot ? store.getBookingsByLot(lot.id) : [],
  )

  useEffect(() => {
    if (lot) {
      setBookings(store.getBookingsByLot(lot.id))
    }
  }, [lot])

  useEffect(() => {
    const refresh = () => {
      if (lot) setBookings(store.getBookingsByLot(lot.id))
    }
    window.addEventListener("sp-data-change", refresh)
    window.addEventListener("storage", refresh)
    return () => {
      window.removeEventListener("sp-data-change", refresh)
      window.removeEventListener("storage", refresh)
    }
  }, [lot])

  useEffect(() => {
    if (initialPlateQuery) {
      setQuickInput(initialPlateQuery)
      handleQuickSearch(initialPlateQuery)
    }
  }, [initialPlateQuery])

  function handleQuickSearch(inputToUse?: string) {
    setQuickError("")
    setQuickResult(null)
    const term = (inputToUse ?? quickInput).trim().toUpperCase().replace(/[^A-Z0-9]/g, "")

    if (!lot) {
      setQuickError("Please select a parking lot first.")
      return
    }
    if (!term) {
      setQuickError("Please enter a valid license plate or booking ID.")
      return
    }

    const currentBookings = store.getBookingsByLot(lot.id)
    const match = currentBookings.find((b) => {
      const plateClean = b.licensePlate.toUpperCase().replace(/[^A-Z0-9]/g, "")
      const idClean = b.id.toUpperCase().replace(/[^A-Z0-9]/g, "")
      return (plateClean === term || idClean === term || plateClean.includes(term)) &&
        ["confirmed", "checked-in"].includes(b.status)
    })

    if (!match) {
      setQuickError(`No active booking found matching "${inputToUse ?? quickInput}". Ensure booking is confirmed or checked-in.`)
      return
    }

    const action = match.status === "confirmed" ? "check-in" : "check-out"
    setQuickResult({ booking: match, action })
  }

  function executeCheckAction(booking: Booking, action: "check-in" | "check-out") {
    setActionLoading(true)
    setActionFeedback(null)

    setTimeout(() => {
      try {
        const updatedBooking: Booking = {
          ...booking,
          status: action === "check-in" ? "checked-in" : "completed",
          completedAt: action === "check-out" ? new Date().toISOString() : booking.completedAt,
        }

        store.saveBooking(updatedBooking)

        if (lot) {
          const updatedLot: ParkingLot = {
            ...lot,
            slots: lot.slots.map((s) =>
              s.id === booking.slotId
                ? {
                    ...s,
                    status: action === "check-in" ? "occupied" : "available",
                    reservedUntil: action === "check-out" ? undefined : s.reservedUntil,
                    reservedBy: action === "check-out" ? undefined : s.reservedBy,
                  }
                : s,
            ),
          }
          store.saveLot(updatedLot)

          if (action === "check-in") {
            store.notifyUser(
              lot.ownerId,
              "DRIVER_PARKED",
              "Driver vehicle checked-in",
              `Vehicle ${booking.licensePlate} has parked in slot ${booking.slotNumber} at ${lot.name}.`,
              booking.id,
            )
          }
        }

        store.addAuditLog({
          userId: user?.id ?? "op",
          userName: user?.name ?? "Operator",
          userRole: user?.role ?? "operator",
          action: action === "check-in" ? "VEHICLE_CHECK_IN" : "VEHICLE_CHECK_OUT",
          details: `${action === "check-in" ? "Check-in" : "Check-out"} processed for plate ${booking.licensePlate} at slot ${booking.slotNumber}`,
        })

        setActionFeedback({
          type: "success",
          message: `${action === "check-in" ? "Check-in" : "Check-out"} successfully executed for vehicle ${booking.licensePlate} (Slot ${booking.slotNumber}).`,
        })

        // Refresh state
        if (lot) setBookings(store.getBookingsByLot(lot.id))
        setConfirmingAction(null)
        setQuickResult(null)
        setQuickInput("")
      } catch (err) {
        setActionFeedback({
          type: "error",
          message: err instanceof Error ? err.message : "Failed to execute gate action.",
        })
      } finally {
        setActionLoading(false)
      }
    }, 400)
  }

  // Filtered Bookings for the Table
  const filteredBookings = useMemo(() => {
    return bookings.filter((b) => {
      if (statusFilter !== "all" && b.status !== statusFilter) return false
      // Vehicle type filter if slot or plate matches
      if (vehicleTypeFilter !== "all") {
        const isMoto = b.slotNumber.startsWith("M") || b.licensePlate.includes("MD")
        if (vehicleTypeFilter === "motorcycle" && !isMoto) return false
        if (vehicleTypeFilter === "car" && isMoto) return false
      }
      if (searchQuery.trim()) {
        const q = searchQuery.trim().toLowerCase()
        const matchesPlate = b.licensePlate.toLowerCase().includes(q)
        const matchesId = b.id.toLowerCase().includes(q)
        const matchesSlot = b.slotNumber.toLowerCase().includes(q)
        const matchesDriver = b.driverId.toLowerCase().includes(q)
        if (!matchesPlate && !matchesId && !matchesSlot && !matchesDriver) return false
      }
      return true
    })
  }, [bookings, statusFilter, vehicleTypeFilter, searchQuery])

  // Pagination calculation
  const totalPages = Math.max(1, Math.ceil(filteredBookings.length / itemsPerPage))
  const paginatedBookings = useMemo(() => {
    const start = (currentPage - 1) * itemsPerPage
    return filteredBookings.slice(start, start + itemsPerPage)
  }, [filteredBookings, currentPage])

  const getStatusBadge = (status: BookingStatus) => {
    switch (status) {
      case "confirmed":
        return { label: "Confirmed", bg: "bg-blue-500/10", text: "text-blue-600", border: "border-blue-500/20" }
      case "checked-in":
        return { label: "Parked / In-Use", bg: "bg-red-500/10", text: "text-red-600", border: "border-red-500/20" }
      case "completed":
        return { label: "Completed", bg: "bg-green-500/10", text: "text-green-600", border: "border-green-500/20" }
      case "pending":
        return { label: "Pending Payment", bg: "bg-amber-500/10", text: "text-amber-600", border: "border-amber-500/20" }
      case "cancelled":
        return { label: "Cancelled", bg: "bg-slate-500/10", text: "text-slate-500", border: "border-slate-500/20" }
      default:
        return { label: status, bg: "bg-gray-500/10", text: "text-gray-500", border: "border-gray-500/20" }
    }
  }

  if (!lot) {
    return (
      <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-8 text-center text-sm text-[var(--muted)]">
        No active parking lot selected. Please choose a parking lot from the site selector.
      </div>
    )
  }

  return (
    <div className="space-y-6">
      {/* Page Title */}
      <div>
        <h1 className="text-2xl font-bold tracking-tight text-[var(--fg)]">
          Booking & Vehicle Management
        </h1>
        <p className="mt-1 text-sm text-[var(--muted)]">
          Manage gate check-in / check-out, inspect booking records, and verify driver credentials for {lot.name}.
        </p>
      </div>

      {/* Action Feedback Banner */}
      {actionFeedback && (
        <div
          role="status"
          className={`flex items-center justify-between rounded-xl border p-4 text-sm ${
            actionFeedback.type === "success"
              ? "border-green-500/30 bg-green-500/10 text-green-600"
              : "border-red-500/30 bg-red-500/10 text-red-600"
          }`}
        >
          <div className="flex items-center gap-2">
            <UntitledIcon
              name={actionFeedback.type === "success" ? "check-circle" : "alert"}
              size={18}
            />
            <span>{actionFeedback.message}</span>
          </div>
          <button
            type="button"
            onClick={() => setActionFeedback(null)}
            className="text-xs font-semibold hover:underline"
          >
            Dismiss
          </button>
        </div>
      )}

      {/* SECTION 1: Fast Gate Check-In / Out Terminal */}
      <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5">
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-2">
            <div className="rounded-lg bg-[var(--primary)]/10 p-2 text-[var(--primary)]">
              <UntitledIcon name="car" size={20} />
            </div>
            <div>
              <h2 className="text-base font-bold text-[var(--fg)]">Gate Terminal / Fast Lookup</h2>
              <p className="text-xs text-[var(--muted)]">
                Scan or enter vehicle license plate or booking ID for instant gate arrival/departure
              </p>
            </div>
          </div>
          <span className="text-xs font-semibold text-[var(--muted)]">Gate Mode: Active</span>
        </div>

        <div className="flex flex-col sm:flex-row gap-3">
          <div className="relative flex-1">
            <input
              className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2.5 text-sm text-[var(--fg)] outline-none focus:border-[var(--primary)] uppercase font-mono tracking-wider"
              value={quickInput}
              onChange={(e) => setQuickInput(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && handleQuickSearch()}
              placeholder="e.g. 51A-123.45 or BOOKING ID"
            />
          </div>
          <button
            type="button"
            className="btn-primary text-sm px-6 py-2.5 justify-center"
            onClick={() => handleQuickSearch()}
          >
            <UntitledIcon name="search" size={16} /> Search Gate
          </button>
        </div>

        {quickError && (
          <div role="alert" className="mt-3 text-xs text-red-600 font-medium flex items-center gap-1.5">
            <UntitledIcon name="alert" size={14} />
            <span>{quickError}</span>
          </div>
        )}

        {/* Quick Result Preview Card */}
        {quickResult && (
          <div className="mt-4 rounded-xl border border-[var(--border)] bg-[var(--bg)] p-4 animate-in">
            <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border)] pb-3">
              <div className="flex items-center gap-3">
                <span className="font-mono text-base font-bold bg-[var(--card)] px-2.5 py-1 rounded border border-[var(--border)] text-[var(--fg)]">
                  {quickResult.booking.licensePlate}
                </span>
                <span
                  className={`rounded-full px-2.5 py-0.5 text-xs font-bold uppercase ${
                    quickResult.action === "check-in"
                      ? "bg-green-500/15 text-green-600"
                      : "bg-blue-500/15 text-blue-600"
                  }`}
                >
                  Action: {quickResult.action.toUpperCase()}
                </span>
              </div>
              <span className="text-xs text-[var(--muted)] font-mono">
                Booking #{quickResult.booking.id}
              </span>
            </div>

            <div className="mt-3 grid grid-cols-2 gap-3 sm:grid-cols-4 text-xs">
              <div>
                <span className="text-[var(--muted)] block">Assigned Slot:</span>
                <span className="font-bold text-[var(--fg)] text-sm">{quickResult.booking.slotNumber}</span>
              </div>
              <div>
                <span className="text-[var(--muted)] block">Booking Window:</span>
                <span className="font-medium text-[var(--fg)]">
                  {new Date(quickResult.booking.startTime).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })} –{" "}
                  {new Date(quickResult.booking.endTime).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
                </span>
              </div>
              <div>
                <span className="text-[var(--muted)] block">Driver ID:</span>
                <span className="font-mono text-[var(--fg)]">{quickResult.booking.driverId.slice(0, 10)}...</span>
              </div>
              <div>
                <span className="text-[var(--muted)] block">Amount & Payment:</span>
                <span className="font-bold text-[var(--fg)]">
                  {quickResult.booking.amount.toLocaleString("vi-VN")} ₫
                </span>
              </div>
            </div>

            <div className="mt-4 flex justify-end gap-2">
              <button
                type="button"
                className="btn-outline text-xs py-1.5 px-3"
                onClick={() => setQuickResult(null)}
              >
                Cancel
              </button>
              <button
                type="button"
                className={`text-xs py-1.5 px-4 font-semibold text-white rounded-lg flex items-center gap-1.5 transition ${
                  quickResult.action === "check-in" ? "bg-green-600 hover:bg-green-700" : "bg-blue-600 hover:bg-blue-700"
                }`}
                onClick={() => setConfirmingAction(quickResult)}
              >
                <UntitledIcon
                  name={quickResult.action === "check-in" ? "check-circle" : "car"}
                  size={14}
                />
                Confirm {quickResult.action === "check-in" ? "Gate Check-In" : "Gate Check-Out"}
              </button>
            </div>
          </div>
        )}
      </div>

      {/* SECTION 2: Comprehensive Bookings Table & Filters */}
      <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between mb-4">
          <div>
            <h2 className="text-base font-bold text-[var(--fg)]">All Booking Records</h2>
            <p className="text-xs text-[var(--muted)]">
              Showing {filteredBookings.length} bookings for {lot.name}
            </p>
          </div>

          {/* Search & Filter Bar */}
          <div className="flex flex-wrap items-center gap-2">
            <div className="relative">
              <input
                className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-1.5 text-xs text-[var(--fg)] outline-none focus:border-[var(--primary)] w-48"
                placeholder="Search plate, id, slot..."
                value={searchQuery}
                onChange={(e) => {
                  setSearchQuery(e.target.value)
                  setCurrentPage(1)
                }}
              />
            </div>

            <select
              className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-1.5 text-xs text-[var(--fg)] outline-none focus:border-[var(--primary)]"
              value={statusFilter}
              onChange={(e) => {
                setStatusFilter(e.target.value as "all" | BookingStatus)
                setCurrentPage(1)
              }}
            >
              <option value="all">All Statuses</option>
              <option value="confirmed">Confirmed</option>
              <option value="checked-in">Parked (Checked-In)</option>
              <option value="completed">Completed</option>
              <option value="pending">Pending Payment</option>
              <option value="cancelled">Cancelled</option>
            </select>

            <select
              className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-1.5 text-xs text-[var(--fg)] outline-none focus:border-[var(--primary)]"
              value={vehicleTypeFilter}
              onChange={(e) => {
                setVehicleTypeFilter(e.target.value as "all" | "car" | "motorcycle")
                setCurrentPage(1)
              }}
            >
              <option value="all">All Vehicles</option>
              <option value="car">Car (Ô tô)</option>
              <option value="motorcycle">Motorcycle (Xe máy)</option>
            </select>
          </div>
        </div>

        {/* Table Content */}
        <div className="overflow-x-auto rounded-lg border border-[var(--border)] bg-[var(--bg)]">
          <table className="w-full min-w-[760px] text-left text-xs">
            <thead className="border-b border-[var(--border)] bg-[var(--card)] text-[var(--muted)] font-semibold">
              <tr>
                <th className="px-4 py-3">Booking ID</th>
                <th className="px-4 py-3">Vehicle Plate</th>
                <th className="px-4 py-3">Slot</th>
                <th className="px-4 py-3">Schedule Window</th>
                <th className="px-4 py-3">Amount</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3 text-right">Gate Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[var(--border)]">
              {paginatedBookings.length === 0 ? (
                <tr>
                  <td colSpan={7} className="px-4 py-8 text-center text-[var(--muted)]">
                    No bookings found matching the selected filter criteria.
                  </td>
                </tr>
              ) : (
                paginatedBookings.map((b) => {
                  const badge = getStatusBadge(b.status)
                  return (
                    <tr key={b.id} className="hover:bg-[var(--card)]/50 transition">
                      <td className="px-4 py-3 font-mono text-[var(--muted)]">
                        {b.id}
                      </td>
                      <td className="px-4 py-3 font-semibold text-[var(--fg)]">
                        <span className="font-mono bg-[var(--card)] px-2 py-0.5 rounded border border-[var(--border)]">
                          {b.licensePlate}
                        </span>
                      </td>
                      <td className="px-4 py-3 font-bold text-[var(--fg)]">
                        {b.slotNumber}
                      </td>
                      <td className="px-4 py-3 text-[var(--muted)]">
                        <div>
                          {new Date(b.startTime).toLocaleDateString([], { month: "short", day: "numeric" })}
                        </div>
                        <div className="text-[11px]">
                          {new Date(b.startTime).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })} –{" "}
                          {new Date(b.endTime).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
                        </div>
                      </td>
                      <td className="px-4 py-3 font-medium text-[var(--fg)]">
                        {b.amount.toLocaleString("vi-VN")} ₫
                      </td>
                      <td className="px-4 py-3">
                        <span
                          className={`inline-block rounded-full px-2.5 py-0.5 text-[10px] font-bold uppercase border ${badge.bg} ${badge.text} ${badge.border}`}
                        >
                          {badge.label}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-right space-x-1.5">
                        <button
                          type="button"
                          className="btn-outline text-[11px] py-1 px-2"
                          onClick={() => setSelectedBooking(b)}
                        >
                          Details
                        </button>
                        {b.status === "confirmed" && (
                          <button
                            type="button"
                            className="text-[11px] py-1 px-2.5 rounded bg-green-600 hover:bg-green-700 text-white font-semibold transition"
                            onClick={() => setConfirmingAction({ booking: b, action: "check-in" })}
                          >
                            Check-In
                          </button>
                        )}
                        {b.status === "checked-in" && (
                          <button
                            type="button"
                            className="text-[11px] py-1 px-2.5 rounded bg-blue-600 hover:bg-blue-700 text-white font-semibold transition"
                            onClick={() => setConfirmingAction({ booking: b, action: "check-out" })}
                          >
                            Check-Out
                          </button>
                        )}
                      </td>
                    </tr>
                  )
                })
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination Controls */}
        {totalPages > 1 && (
          <div className="mt-4 flex items-center justify-between text-xs text-[var(--muted)]">
            <span>
              Page {currentPage} of {totalPages} ({filteredBookings.length} total)
            </span>
            <div className="flex gap-1.5">
              <button
                type="button"
                className="btn-outline text-xs py-1 px-2.5"
                disabled={currentPage === 1}
                onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
              >
                Previous
              </button>
              <button
                type="button"
                className="btn-outline text-xs py-1 px-2.5"
                disabled={currentPage === totalPages}
                onClick={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
              >
                Next
              </button>
            </div>
          </div>
        )}
      </div>

      {/* MODAL 1: Booking Details Modal */}
      {selectedBooking && (
        <div
          role="presentation"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
          onMouseDown={(e) => e.target === e.currentTarget && setSelectedBooking(null)}
        >
          <div
            role="dialog"
            aria-modal="true"
            aria-labelledby="booking-detail-title"
            className="w-full max-w-lg rounded-2xl border border-[var(--border)] bg-[var(--card)] p-6 shadow-2xl animate-in text-sm text-[var(--fg)]"
          >
            <div className="flex items-center justify-between border-b border-[var(--border)] pb-3">
              <div>
                <span className="text-[10px] font-bold uppercase tracking-wider text-[var(--primary)]">
                  Booking Inspection
                </span>
                <h3 id="booking-detail-title" className="text-lg font-bold text-[var(--fg)]">
                  #{selectedBooking.id}
                </h3>
              </div>
              <button
                type="button"
                onClick={() => setSelectedBooking(null)}
                className="h-8 w-8 rounded-full border border-[var(--border)] bg-[var(--bg)] flex items-center justify-center text-[var(--muted)] hover:text-[var(--fg)]"
              >
                <UntitledIcon name="x" size={16} />
              </button>
            </div>

            <div className="mt-4 space-y-3">
              <div className="grid grid-cols-2 gap-3 rounded-lg border border-[var(--border)] bg-[var(--bg)] p-3">
                <div>
                  <span className="text-xs text-[var(--muted)] block">License Plate</span>
                  <span className="font-mono font-bold text-base text-[var(--fg)]">
                    {selectedBooking.licensePlate}
                  </span>
                </div>
                <div>
                  <span className="text-xs text-[var(--muted)] block">Allocated Slot</span>
                  <span className="font-bold text-base text-[var(--primary)]">
                    {selectedBooking.slotNumber}
                  </span>
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3 text-xs">
                <div>
                  <span className="text-[var(--muted)] block">Parking Site:</span>
                  <span className="font-medium">{selectedBooking.lotName}</span>
                </div>
                <div>
                  <span className="text-[var(--muted)] block">Driver ID:</span>
                  <span className="font-mono">{selectedBooking.driverId}</span>
                </div>
                <div>
                  <span className="text-[var(--muted)] block">Start Time:</span>
                  <span className="font-medium">{new Date(selectedBooking.startTime).toLocaleString()}</span>
                </div>
                <div>
                  <span className="text-[var(--muted)] block">End Time:</span>
                  <span className="font-medium">{new Date(selectedBooking.endTime).toLocaleString()}</span>
                </div>
                <div>
                  <span className="text-[var(--muted)] block">Payment Amount:</span>
                  <span className="font-bold text-[var(--fg)]">
                    {selectedBooking.amount.toLocaleString("vi-VN")} ₫
                  </span>
                </div>
                <div>
                  <span className="text-[var(--muted)] block">Status:</span>
                  <span className="font-bold uppercase text-[var(--primary)]">
                    {selectedBooking.status}
                  </span>
                </div>
              </div>

              {selectedBooking.completedAt && (
                <div className="text-xs rounded border border-[var(--border)] bg-[var(--bg)] p-2 text-[var(--muted)]">
                  Completed at: {new Date(selectedBooking.completedAt).toLocaleString()}
                </div>
              )}
            </div>

            <div className="mt-6 flex justify-end gap-2 border-t border-[var(--border)] pt-3">
              <button
                type="button"
                className="btn-outline text-xs py-1.5 px-3"
                onClick={() => setSelectedBooking(null)}
              >
                Close
              </button>
              {selectedBooking.status === "confirmed" && (
                <button
                  type="button"
                  className="btn-primary text-xs py-1.5 px-3 bg-green-600 hover:bg-green-700"
                  onClick={() => {
                    const b = selectedBooking
                    setSelectedBooking(null)
                    setConfirmingAction({ booking: b, action: "check-in" })
                  }}
                >
                  Proceed to Check-In
                </button>
              )}
              {selectedBooking.status === "checked-in" && (
                <button
                  type="button"
                  className="btn-primary text-xs py-1.5 px-3 bg-blue-600 hover:bg-blue-700"
                  onClick={() => {
                    const b = selectedBooking
                    setSelectedBooking(null)
                    setConfirmingAction({ booking: b, action: "check-out" })
                  }}
                >
                  Proceed to Check-Out
                </button>
              )}
            </div>
          </div>
        </div>
      )}

      {/* MODAL 2: Action Confirmation Dialog */}
      {confirmingAction && (
        <div
          role="presentation"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
          onMouseDown={(e) => !actionLoading && e.target === e.currentTarget && setConfirmingAction(null)}
        >
          <div
            role="dialog"
            aria-modal="true"
            aria-labelledby="confirm-gate-title"
            className="w-full max-w-md rounded-2xl border border-[var(--border)] bg-[var(--card)] p-6 shadow-2xl animate-in text-sm text-[var(--fg)]"
          >
            <div className="flex items-center gap-3">
              <div
                className={`rounded-xl p-3 ${
                  confirmingAction.action === "check-in"
                    ? "bg-green-500/10 text-green-600"
                    : "bg-blue-500/10 text-blue-600"
                }`}
              >
                <UntitledIcon
                  name={confirmingAction.action === "check-in" ? "check-circle" : "car"}
                  size={24}
                />
              </div>
              <div>
                <h3 id="confirm-gate-title" className="text-base font-bold text-[var(--fg)]">
                  Confirm Gate {confirmingAction.action === "check-in" ? "Check-In" : "Check-Out"}
                </h3>
                <p className="text-xs text-[var(--muted)]">
                  Verify vehicle details before opening barrier gate
                </p>
              </div>
            </div>

            <div className="mt-4 rounded-xl border border-[var(--border)] bg-[var(--bg)] p-3.5 space-y-2 text-xs">
              <div className="flex justify-between">
                <span className="text-[var(--muted)]">Vehicle Plate:</span>
                <span className="font-mono font-bold text-[var(--fg)]">
                  {confirmingAction.booking.licensePlate}
                </span>
              </div>
              <div className="flex justify-between">
                <span className="text-[var(--muted)]">Slot Number:</span>
                <span className="font-bold text-[var(--primary)]">
                  {confirmingAction.booking.slotNumber}
                </span>
              </div>
              <div className="flex justify-between">
                <span className="text-[var(--muted)]">Facility:</span>
                <span>{confirmingAction.booking.lotName}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-[var(--muted)]">Current Status:</span>
                <span className="font-semibold uppercase">{confirmingAction.booking.status}</span>
              </div>
            </div>

            <p className="mt-3 text-xs text-[var(--muted)]">
              {confirmingAction.action === "check-in"
                ? "This will set slot status to OCCUPIED and mark the booking as CHECKED-IN."
                : "This will release the slot back to AVAILABLE and complete the booking."}
            </p>

            <div className="mt-5 flex justify-end gap-2">
              <button
                type="button"
                className="btn-outline text-xs py-1.5 px-3"
                disabled={actionLoading}
                onClick={() => setConfirmingAction(null)}
              >
                Cancel
              </button>
              <button
                type="button"
                className={`text-xs py-1.5 px-4 font-semibold text-white rounded-lg flex items-center gap-1.5 ${
                  confirmingAction.action === "check-in"
                    ? "bg-green-600 hover:bg-green-700"
                    : "bg-blue-600 hover:bg-blue-700"
                }`}
                disabled={actionLoading}
                onClick={() => executeCheckAction(confirmingAction.booking, confirmingAction.action)}
              >
                {actionLoading ? "Processing..." : `Confirm ${confirmingAction.action === "check-in" ? "Check-In" : "Check-Out"}`}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
