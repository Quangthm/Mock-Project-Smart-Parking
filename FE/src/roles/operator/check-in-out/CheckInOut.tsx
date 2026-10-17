import { useState, useMemo, useEffect } from "react"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { PaymentMethodLogo } from "../../../components/payment/PaymentMethodLogo"
import { AuthApiError } from "../../../lib/authApi"
import { reservationApi, type OperatorSite, type WalkInParkingSession } from "../../../lib/operatorApi"
import { PlateScanPanel } from "./PlateScanPanel"
import type { Booking, BookingStatus } from "../../../lib/types"

type GateAction = "check-in" | "check-out"
type QuickGateResult =
  | { kind: "reservation"; booking: Booking; action: GateAction }
  | { kind: "walk-in"; plate: string; session: WalkInParkingSession | null; action: GateAction; vehicleType?: string }

interface CheckInOutProps {
  mode: "check-in" | "check-out"
  lot: OperatorSite | null
  initialPlateQuery?: string
  onDataChanged?: () => void
}

export function CheckInOut({
  mode,
  lot,
  initialPlateQuery = "",
  onDataChanged,
}: CheckInOutProps) {
  // Quick lookup state
  const [quickInput, setQuickInput] = useState(initialPlateQuery)
  const [quickResult, setQuickResult] = useState<QuickGateResult | null>(null)
  const [quickError, setQuickError] = useState("")
  const [latestScanImage, setLatestScanImage] = useState("")
  const [latestImageIsUpload, setLatestImageIsUpload] = useState(false)

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
    imageData?: string
  } | null>(null)
  const [actionLoading, setActionLoading] = useState(false)
  const [actionFeedback, setActionFeedback] = useState<{
    type: "success" | "error"
    message: string
  } | null>(null)
  const [lookupLoading, setLookupLoading] = useState(false)
  const [bookingsLoading, setBookingsLoading] = useState(false)
  const [bookingsError, setBookingsError] = useState("")

  // All bookings for the selected lot
  const [bookings, setBookings] = useState<Booking[]>([])

  function slotLabel(booking: Booking) {
    return lot?.slots.find((slot) => slot.id === booking.slotId)?.number ?? booking.slotNumber
  }

  useEffect(() => {
    if (!lot) {
      setBookings([])
      return
    }
    let active = true
    setBookingsLoading(true)
    setBookingsError("")
    reservationApi
      .list(lot.id)
      .then((items) => {
        if (active) setBookings(items)
      })
      .catch((error) => {
        if (active)
          setBookingsError(error instanceof Error ? error.message : "Could not load reservations.")
      })
      .finally(() => {
        if (active) setBookingsLoading(false)
      })
    return () => {
      active = false
    }
  }, [lot?.id])

  useEffect(() => {
    if (initialPlateQuery && lot) {
      setQuickInput(initialPlateQuery)
      void handleQuickSearch(initialPlateQuery)
    }
  }, [initialPlateQuery, lot?.id])

  async function handleQuickSearch(
    inputToUse?: string,
    autoCheckIn = false,
    imageData?: string,
    vehicleType?: string,
  ) {
    setQuickError("")
    setQuickResult(null)
    const term = (inputToUse ?? quickInput).trim()

    if (!lot) {
      setQuickError("Please select a parking lot first.")
      return
    }
    if (!term) {
      setQuickError("Please enter a valid license plate or booking ID.")
      return
    }

    setLookupLoading(true)
    try {
      const match = await reservationApi.lookup(lot.id, term)
      const expectedStatus = mode === "check-in" ? "confirmed" : "checked-in"
      if (match.status !== expectedStatus) {
        setQuickError(`Reservation ${match.id} is ${match.status} and cannot use this gate.`)
        return
      }
      const result: QuickGateResult = {
        kind: "reservation",
        booking: match,
        action: mode,
      }
      setQuickResult(result)
      if (autoCheckIn && mode === "check-in") {
        await executeCheckAction(match, "check-in", imageData)
      }
    } catch (error) {
      if (!(error instanceof AuthApiError) || error.status !== 404) {
        setQuickError(error instanceof Error ? error.message : "Could not find an active reservation.")
        return
      }

      if (/^[0-9a-f]{8}-[0-9a-f-]{27}$/i.test(term)) {
        setQuickError("No reservation was found for this ID.")
        return
      }

      try {
        const activeSession = await reservationApi.walkInLookup(lot.id, term)
        if (mode === "check-in") {
          setQuickError("This vehicle already has an active parking session. Use Gate Check-Out.")
          return
        }
        const assignedSlot = activeSession.slotId
          ? lot.slots.find((slot) => slot.id === activeSession.slotId)
          : undefined
        setQuickResult({
          kind: "walk-in",
          plate: activeSession.licensePlate,
          session: assignedSlot
            ? { ...activeSession, slotNumber: assignedSlot.number }
            : activeSession,
          action: mode,
        })
      } catch (walkInError) {
        if (!(walkInError instanceof AuthApiError) || walkInError.status !== 404) {
          setQuickError(walkInError instanceof Error ? walkInError.message : "Could not look up the active parking session.")
          return
        }
        if (mode === "check-out") {
          setQuickError("No active parking session was found for this plate.")
          return
        }
        const result: QuickGateResult = {
          kind: "walk-in",
          plate: term,
          session: null,
          action: "check-in",
          vehicleType,
        }
        setQuickResult(result)
        if (autoCheckIn) await executeWalkInAction(result, "check-in", imageData, vehicleType)
      }
    } finally {
      setLookupLoading(false)
    }
  }

  async function executeCheckAction(
    booking: Booking,
    action: "check-in" | "check-out",
    imageData?: string,
  ) {
    setActionLoading(true)
    setActionFeedback(null)

    try {
      if (!lot) throw new Error("Select a parking site before processing the gate action.")
      const updatedBooking = action === "check-in"
        ? await reservationApi.checkIn(lot.id, booking.id, booking.licensePlate, imageData)
        : await reservationApi.checkOut(lot.id, booking.id, booking.licensePlate, imageData)

      setBookings((current) =>
        current.map((item) => item.id === updatedBooking.id ? updatedBooking : item),
      )
      setActionFeedback({
        type: "success",
        message: `${action === "check-in" ? "Check-in" : "Check-out"} completed for ${updatedBooking.licensePlate} (${slotLabel(updatedBooking)}).`,
      })
      setConfirmingAction(null)
      setQuickResult(null)
      setQuickInput("")
      setLatestScanImage("")
      setLatestImageIsUpload(false)
      onDataChanged?.()
    } catch (error) {
      setActionFeedback({
        type: "error",
        message: error instanceof Error ? error.message : "Failed to execute gate action.",
      })
    } finally {
      setActionLoading(false)
    }
  }

  async function executeWalkInAction(
    result: Extract<QuickGateResult, { kind: "walk-in" }>,
    action: GateAction,
    imageData?: string,
    vehicleType?: string,
  ) {
    setActionLoading(true)
    setActionFeedback(null)

    try {
      if (!lot) throw new Error("Select a parking site before processing the gate action.")
      let session: WalkInParkingSession
      if (action === "check-in") {
        session = await reservationApi.walkInCheckIn(
          lot.id,
          result.plate,
          imageData,
          vehicleType ?? result.vehicleType,
        )
      } else {
        if (!result.session) throw new Error("The active walk-in session was not found.")
        session = await reservationApi.walkInCheckOut(lot.id, result.session.id, result.plate, imageData)
      }
      setActionFeedback({
        type: "success",
        message: action === "check-in"
          ? `Walk-in check-in completed for ${session.licensePlate} (${session.slotNumber ?? "No slot assigned"}).`
          : `Walk-in check-out completed for ${session.licensePlate} (${session.slotNumber ?? "No slot assigned"}).`,
      })
      setQuickResult(null)
      setQuickInput("")
      setLatestScanImage("")
      setLatestImageIsUpload(false)
      onDataChanged?.()
    } catch (error) {
      setActionFeedback({
        type: "error",
        message: error instanceof Error ? error.message : "Failed to execute walk-in gate action.",
      })
    } finally {
      setActionLoading(false)
    }
  }

  // Filtered Bookings for the Table
  const filteredBookings = useMemo(() => {
    return bookings.filter((b) => {
      if (mode === "check-in" && b.status !== "confirmed") return false
      if (mode === "check-out" && b.status !== "checked-in") return false
      if (statusFilter !== "all" && b.status !== statusFilter) return false
      if (vehicleTypeFilter !== "all") {
        const slot = lot?.slots.find((item) => item.id === b.slotId)
        if (!slot) return false
        const isMoto = slot.vehicleType.toLowerCase().includes("motorcycle")
        if (vehicleTypeFilter === "motorcycle" && !isMoto) return false
        if (vehicleTypeFilter === "car" && isMoto) return false
      }
      if (searchQuery.trim()) {
        const q = searchQuery.trim().toLowerCase()
        const matchesPlate = b.licensePlate.toLowerCase().includes(q)
        const matchesId = b.id.toLowerCase().includes(q)
        const matchesSlot = slotLabel(b).toLowerCase().includes(q)
        const matchesDriver = b.driverId.toLowerCase().includes(q)
        if (!matchesPlate && !matchesId && !matchesSlot && !matchesDriver) return false
      }
      return true
    })
  }, [bookings, lot, mode, statusFilter, vehicleTypeFilter, searchQuery])

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
          {mode === "check-in" ? "Gate Check-In" : "Gate Check-Out"}
        </h1>
        <p className="mt-1 text-sm text-[var(--muted)]">
          {mode === "check-in"
            ? `Register incoming vehicles for ${lot.name}.`
            : `Verify outgoing vehicles and compare their entry image for ${lot.name}.`}
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

      {/* Gate terminal for this direction */}
      <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5">
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-2">
            <div className="rounded-lg bg-[var(--primary)]/10 p-2 text-[var(--primary)]">
              <UntitledIcon name="car" size={20} />
            </div>
            <div>
              <h2 className="text-base font-bold text-[var(--fg)]">
                {mode === "check-in" ? "Check-In Terminal" : "Check-Out Terminal"}
              </h2>
              <p className="text-xs text-[var(--muted)]">
                {mode === "check-in"
                  ? "Scan or enter the vehicle's plate to start a parking session."
                  : "Scan or enter the plate to find an active session and compare its check-in photo."}
              </p>
            </div>
          </div>
          <span className="text-xs font-semibold text-[var(--muted)]">Gate Mode: Active</span>
        </div>

        <div className="flex flex-col sm:flex-row gap-3">
          <div className="relative flex-1">
            <label htmlFor="gate-plate-input" className="sr-only">Enter vehicle license plate manually</label>
            <input
              id="gate-plate-input"
              className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2.5 text-sm text-[var(--fg)] outline-none focus:border-[var(--primary)] uppercase font-mono tracking-wider"
              value={quickInput}
              onChange={(e) => {
                setQuickInput(e.target.value)
                if (!latestImageIsUpload) setLatestScanImage("")
              }}
              onKeyDown={(e) => e.key === "Enter" && handleQuickSearch()}
              placeholder={mode === "check-in" ? "AI unavailable? Enter the license plate" : "AI unavailable? Enter the license plate to check out"}
            />
          </div>
          <button
            type="button"
            className="btn-primary text-sm px-6 py-2.5 justify-center"
            onClick={() => handleQuickSearch()}
          >
            <UntitledIcon name="search" size={16} /> {mode === "check-in" ? "Find Check-In" : "Find Check-Out"}
          </button>
        </div>

        <PlateScanPanel
          siteId={lot.id}
          tenantId={lot.tenantId}
          direction={mode === "check-in" ? "INBOUND" : "OUTBOUND"}
          mode={mode}
          onImageCaptured={(imageData) => {
            setLatestScanImage(imageData)
            setLatestImageIsUpload(true)
          }}
          onPlateRecognized={async (plate, confidence, imageData, vehicleType, isUploadedImage) => {
            setLatestScanImage(imageData ?? "")
            setLatestImageIsUpload(isUploadedImage ?? false)
            setQuickInput(plate)
            await handleQuickSearch(
              plate,
              mode === "check-in" &&
                typeof confidence === "number" &&
                Number.isFinite(confidence) &&
                confidence >= 0.9,
              imageData,
              vehicleType,
            )
          }}
        />

        {quickError && (
          <div role="alert" className="mt-3 text-xs text-red-600 font-medium flex items-center gap-1.5">
            <UntitledIcon name="alert" size={14} />
            <span>{quickError}</span>
          </div>
        )}

        {/* Lookup result is limited to the active direction. */}
        {quickResult && (
          <div className="mt-4 rounded-xl border border-[var(--border)] bg-[var(--bg)] p-4 animate-in">
            <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border)] pb-3">
              <div className="flex items-center gap-3">
                <span className="font-mono text-base font-bold bg-[var(--card)] px-2.5 py-1 rounded border border-[var(--border)] text-[var(--fg)]">
                  {quickResult.kind === "reservation" ? quickResult.booking.licensePlate : quickResult.plate}
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
                {quickResult.kind === "reservation"
                  ? `Booking #${quickResult.booking.id}`
                  : quickResult.session
                    ? `Walk-in session #${quickResult.session.id}`
                    : "No reservation — walk-in vehicle"}
              </span>
            </div>

            {mode === "check-out" && (
              <div className="mt-4 grid gap-3 sm:grid-cols-2">
                <EvidenceImage
                  label="Check-in image"
                  image={quickResult.kind === "reservation"
                    ? quickResult.booking.entryImageUrl ?? null
                    : quickResult.session?.entryImageUrl ?? null}
                />
                <EvidenceImage label="Current check-out scan" image={latestScanImage || null} />
              </div>
            )}

            {quickResult.kind === "reservation" ? (
              <div className="mt-3 grid grid-cols-2 gap-3 sm:grid-cols-4 text-xs">
                <div>
                  <span className="text-[var(--muted)] block">Assigned Slot:</span>
                  <span className="font-bold text-[var(--fg)] text-sm">{slotLabel(quickResult.booking)}</span>
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
                    {quickResult.booking.paymentStatus
                      ? `${quickResult.booking.amount.toLocaleString("vi-VN")} VND`
                      : "Managed by Payment Service"}
                  </span>
                </div>
              </div>
            ) : (
              <div className="mt-3 grid grid-cols-2 gap-3 text-xs">
                <div>
                  <span className="text-[var(--muted)] block">Slot:</span>
                  <span className="font-bold text-[var(--fg)] text-sm">
                    {quickResult.session?.slotNumber ?? "Not assigned"}
                  </span>
                </div>
                {quickResult.session && (
                  <div>
                    <span className="text-[var(--muted)] block">Entry Time:</span>
                    <span className="font-medium text-[var(--fg)]">
                      {new Date(quickResult.session.entryTime).toLocaleString()}
                    </span>
                  </div>
                )}
                <p className="col-span-2 text-[var(--muted)]">
                  Walk-in parking fees are handled by the payment workflow at check-out.
                </p>
              </div>
            )}

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
                disabled={actionLoading || lookupLoading}
                onClick={() => quickResult.kind === "reservation"
                  ? setConfirmingAction({ ...quickResult, imageData: latestScanImage || undefined })
                  : void executeWalkInAction(
                      quickResult,
                      quickResult.action,
                      latestScanImage || undefined,
                      quickResult.vehicleType,
                    )}
              >
                <UntitledIcon
                  name={quickResult.action === "check-in" ? "check-circle" : "car"}
                  size={14}
                />
                {quickResult.kind === "walk-in"
                  ? `${quickResult.action === "check-in" ? "Check In" : "Check Out"} Walk-In`
                  : `Confirm ${quickResult.action === "check-in" ? "Gate Check-In" : "Gate Check-Out"}`}
              </button>
            </div>
          </div>
        )}
      </div>

      {/* SECTION 2: Comprehensive Bookings Table & Filters */}
      <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between mb-4">
          <div>
            <h2 className="text-base font-bold text-[var(--fg)]">Parking History</h2>
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

        {bookingsError && (
          <div role="alert" className="mb-4 rounded-lg border border-red-500/30 bg-red-500/10 p-3 text-xs text-red-600">
            {bookingsError}
          </div>
        )}
        {bookingsLoading && (
          <p role="status" className="mb-4 text-xs text-[var(--muted)]">Loading reservations…</p>
        )}

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
                        {slotLabel(b)}
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
                        {b.paymentStatus
                          ? `${b.amount.toLocaleString("vi-VN")} VND`
                          : "Managed by Payment Service"}
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
                        {mode === "check-in" && b.status === "confirmed" && (
                          <button
                            type="button"
                            className="text-[11px] py-1 px-2.5 rounded bg-green-600 hover:bg-green-700 text-white font-semibold transition"
                            onClick={() => setConfirmingAction({ booking: b, action: "check-in" })}
                          >
                            Check-In
                          </button>
                        )}
                        {mode === "check-out" && b.status === "checked-in" && (
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
                    {slotLabel(selectedBooking)}
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
                    {selectedBooking.paymentStatus
                      ? `${selectedBooking.amount.toLocaleString("vi-VN")} VND`
                      : "Managed by Payment Service"}
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
              {mode === "check-in" && selectedBooking.status === "confirmed" && (
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
              {mode === "check-out" && selectedBooking.status === "checked-in" && (
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
                  {confirmingAction.booking.slotNumber || "Not assigned"}
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

            {confirmingAction.action === "check-out" && (
              <div className="mt-4 grid gap-3 sm:grid-cols-2">
                <EvidenceImage
                  label="Check-in image"
                  image={confirmingAction.booking.entryImageUrl ?? null}
                />
                <EvidenceImage label="Current check-out scan" image={confirmingAction.imageData ?? null} />
              </div>
            )}

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
                onClick={() => executeCheckAction(
                  confirmingAction.booking,
                  confirmingAction.action,
                  confirmingAction.imageData,
                )}
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

function EvidenceImage({ label, image }: { label: string; image: string | null }) {
  return (
    <div className="min-w-0">
      <p className="mb-1 text-xs font-semibold text-[var(--muted)]">{label}</p>
      {image ? (
        <img
          src={image}
          alt={label}
          className="h-40 w-full rounded-lg border border-[var(--border)] bg-black object-contain"
        />
      ) : (
        <div className="flex h-40 items-center justify-center rounded-lg border border-dashed border-[var(--border)] bg-[var(--card)] px-3 text-center text-xs text-[var(--muted)]">
          No image saved for this session.
        </div>
      )}
    </div>
  )
}
