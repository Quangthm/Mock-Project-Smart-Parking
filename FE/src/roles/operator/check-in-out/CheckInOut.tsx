import { useState, useMemo, useEffect } from "react"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { PageHeader } from "../../../components/ui/PageHeader"
import { AuthApiError } from "../../../lib/authApi"
import {
  reservationApi,
  type OperatorSite,
  type ParkingSessionHistoryEntry,
  type WalkInParkingSession,
} from "../../../lib/operatorApi"
import { PlateScanPanel } from "./PlateScanPanel"
import type { Booking } from "../../../lib/types"

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
  const [statusFilter, setStatusFilter] = useState("all")
  const [vehicleTypeFilter, setVehicleTypeFilter] = useState("all")
  const [currentPage, setCurrentPage] = useState(1)
  const itemsPerPage = 8

  const [selectedSession, setSelectedSession] = useState<ParkingSessionHistoryEntry | null>(null)
  const [historyRefresh, setHistoryRefresh] = useState(0)

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
  const [sessionsLoading, setSessionsLoading] = useState(false)
  const [sessionsError, setSessionsError] = useState("")

  const [sessions, setSessions] = useState<ParkingSessionHistoryEntry[]>([])

  function slotLabel(booking: Booking) {
    return lot?.slots.find((slot) => slot.id === booking.slotId)?.number ?? booking.slotNumber
  }

  function sessionSlotLabel(session: ParkingSessionHistoryEntry) {
    return (
      lot?.slots.find((slot) => slot.id === session.currentSlotId)?.number ??
      session.slotNumber ??
      "—"
    )
  }

  useEffect(() => {
    if (!lot) {
      setSessions([])
      setSelectedSession(null)
      return
    }
    let active = true
    setSessions([])
    setSelectedSession(null)
    setSessionsLoading(true)
    setSessionsError("")
    reservationApi
      .listParkingSessions(lot.id)
      .then((items) => {
        if (active) setSessions(items)
      })
      .catch((error) => {
        if (active)
          setSessionsError(error instanceof Error ? error.message : "Could not load parking history.")
      })
      .finally(() => {
        if (active) setSessionsLoading(false)
      })
    return () => {
      active = false
    }
  }, [lot?.id, historyRefresh])

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

      setHistoryRefresh((current) => current + 1)
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
      setHistoryRefresh((current) => current + 1)
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

  const filteredSessions = useMemo(() => {
    return sessions.filter((session) => {
      if (statusFilter !== "all" && session.status !== statusFilter) return false
      if (vehicleTypeFilter !== "all" && session.vehicleType !== vehicleTypeFilter) return false
      if (searchQuery.trim()) {
        const q = searchQuery.replace(/[^a-z0-9]/gi, "").toUpperCase()
        const plate = session.originalPlate ?? session.normalizedPlate ?? ""
        if (q && !plate.replace(/[^a-z0-9]/gi, "").toUpperCase().includes(q)) return false
      }
      return true
    })
  }, [sessions, statusFilter, vehicleTypeFilter, searchQuery])

  const totalPages = Math.max(1, Math.ceil(filteredSessions.length / itemsPerPage))
  useEffect(() => setCurrentPage((page) => Math.min(page, totalPages)), [totalPages])
  const paginatedSessions = useMemo(() => {
    const start = (currentPage - 1) * itemsPerPage
    return filteredSessions.slice(start, start + itemsPerPage)
  }, [filteredSessions, currentPage])

  const getStatusBadge = (status: string) => {
    switch (status) {
      case "ACTIVE":
        return { label: "Active", bg: "bg-green-500/10", text: "text-green-600", border: "border-green-500/20" }
      case "PENDING_PAYMENT":
        return { label: "Pending Payment", bg: "bg-amber-500/10", text: "text-amber-600", border: "border-amber-500/20" }
      case "COMPLETED":
        return { label: "Completed", bg: "bg-green-500/10", text: "text-green-600", border: "border-green-500/20" }
      case "OVERSTAYED":
      case "DISPUTED":
        return { label: status.replace(/_/g, " "), bg: "bg-amber-500/10", text: "text-amber-600", border: "border-amber-500/20" }
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
    <div className="min-w-0 space-y-5">
      <PageHeader title={mode === "check-in" ? "Gate Check-In" : "Gate Check-Out"} description={mode === "check-in" ? `Register incoming vehicles for ${lot.name}.` : `Verify outgoing vehicles and compare their entry image for ${lot.name}.`} />

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

      {/* Manual entry follows the camera workflow in both directions. */}
      <div className="min-w-0 rounded-xl border border-[var(--border)] bg-[var(--card)] p-4 sm:p-5">
        <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
          <div className="flex min-w-0 items-center gap-3">
            <div className="rounded-lg bg-[var(--primary)]/10 p-2 text-[var(--primary)]">
              <UntitledIcon name="car" size={20} />
            </div>
            <div>
              <h2 className="text-base font-bold text-[var(--fg)]">
                {mode === "check-in" ? "Manual Check-In" : "Manual Check-Out"}
              </h2>
              <p className="text-xs text-[var(--muted)]">
                {mode === "check-in"
                  ? "Enter a license plate when you need to check in a vehicle manually."
                  : "Enter a license plate to find and confirm its check-out manually."}
              </p>
            </div>
          </div>
        </div>

        <div className="flex flex-col gap-3 sm:flex-row sm:items-stretch">
          <div className="relative min-w-0 flex-1">
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
              placeholder="Enter license plate"
            />
          </div>
          <button
            type="button"
            className="btn-primary justify-center px-6 py-2.5 text-sm sm:shrink-0"
            onClick={() => handleQuickSearch()}
          >
            <UntitledIcon name="search" size={16} /> {mode === "check-in" ? "Check-In" : "Check-Out"}
          </button>
        </div>
      </div>

      {quickError && (
        <div role="alert" className="flex items-center gap-1.5 text-xs font-medium text-red-600">
          <UntitledIcon name="alert" size={14} />
          <span>{quickError}</span>
        </div>
      )}

        {/* Lookup result is limited to the active direction. */}
        {quickResult && (
          <div className="min-w-0 rounded-xl border border-[var(--border)] bg-[var(--card)] p-4 animate-in sm:p-5">
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
      {/* SECTION 2: Comprehensive Bookings Table & Filters */}
      <div className="min-w-0 rounded-xl border border-[var(--border)] bg-[var(--card)] p-4 sm:p-5">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between mb-4">
          <div>
            <h2 className="text-base font-bold text-[var(--fg)]">Parking History</h2>
            <p className="text-xs text-[var(--muted)]">
              Showing {filteredSessions.length} parking sessions for {lot.name}
            </p>
          </div>

          {/* Parking history filters */}
          <div className="flex min-w-0 flex-wrap items-center gap-2">
            <div className="relative w-full sm:w-auto">
              <label htmlFor="parking-history-plate-search" className="sr-only">Search parking history by license plate</label>
              <input
                id="parking-history-plate-search"
                type="search"
                className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm text-[var(--fg)] outline-none focus:border-[var(--primary)] sm:w-56"
                placeholder="Search license plate"
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
                setStatusFilter(e.target.value)
                setCurrentPage(1)
              }}
            >
              <option value="all">All Statuses</option>
              <option value="ACTIVE">Active</option>
              <option value="PENDING_PAYMENT">Pending Payment</option>
              <option value="COMPLETED">Completed</option>
              <option value="OVERSTAYED">Overstayed</option>
              <option value="DISPUTED">Disputed</option>
            </select>

            <select
              className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-1.5 text-xs text-[var(--fg)] outline-none focus:border-[var(--primary)]"
              value={vehicleTypeFilter}
              onChange={(e) => {
                setVehicleTypeFilter(e.target.value)
                setCurrentPage(1)
              }}
            >
              <option value="all">All Vehicles</option>
              <option value="CAR">Car (Ô tô)</option>
              <option value="MOTORCYCLE">Motorcycle (Xe máy)</option>
              <option value="OVERSIZED">Oversized</option>
            </select>
          </div>
        </div>

        {sessionsError && (
          <div role="alert" className="mb-4 rounded-lg border border-red-500/30 bg-red-500/10 p-3 text-xs text-red-600">
            {sessionsError}
          </div>
        )}
        {sessionsLoading && (
          <p role="status" className="mb-4 text-xs text-[var(--muted)]">Loading parking history…</p>
        )}

        {/* Table Content */}
        <div className="overflow-x-auto rounded-lg border border-[var(--border)] bg-[var(--bg)]">
          <table className="w-full min-w-[760px] text-left text-xs">
            <thead className="border-b border-[var(--border)] bg-[var(--card)] text-[var(--muted)] font-semibold">
              <tr>
                <th className="px-4 py-3">User Name</th>
                <th className="px-4 py-3">Vehicle Plate</th>
                <th className="px-4 py-3">Slot</th>
                <th className="px-4 py-3">Vehicle Type</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3">Type</th>
                <th className="px-4 py-3 text-right">Details</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[var(--border)]">
              {paginatedSessions.length === 0 ? (
                <tr>
                  <td colSpan={7} className="px-4 py-8 text-center text-[var(--muted)]">
                    {searchQuery.trim() ? "No parking history matches that license plate." : "No parking sessions found matching the selected filters."}
                  </td>
                </tr>
              ) : (
                paginatedSessions.map((session) => {
                  const badge = getStatusBadge(session.status)
                  return (
                    <tr key={session.id} className="cursor-pointer hover:bg-[var(--card)]/50 transition" onClick={() => setSelectedSession(session)}>
                      <td className="px-4 py-3 text-[var(--muted)]">
                        {session.accountId ? <span title={session.accountId}>Account {session.accountId.slice(0, 8)}</span> : "—"}
                      </td>
                      <td className="px-4 py-3 font-semibold text-[var(--fg)]">
                        <span className="font-mono bg-[var(--card)] px-2 py-0.5 rounded border border-[var(--border)]">
                          {session.originalPlate || session.normalizedPlate || "—"}
                        </span>
                      </td>
                      <td className="px-4 py-3 font-bold text-[var(--fg)]">
                        {sessionSlotLabel(session)}
                      </td>
                      <td className="px-4 py-3 text-[var(--muted)]">
                        {session.vehicleType.replace(/_/g, " ")}
                      </td>
                      <td className="px-4 py-3">
                        <span
                          className={`inline-block rounded-full px-2.5 py-0.5 text-[10px] font-bold uppercase border ${badge.bg} ${badge.text} ${badge.border}`}
                        >
                          {badge.label}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-[var(--muted)]">
                        {session.reservationId ? "Reserved" : "Walk-in"}
                      </td>
                      <td className="px-4 py-3 text-right">
                        <button
                          type="button"
                          className="btn-outline text-[11px] py-1 px-2"
                          onClick={(event) => {
                            event.stopPropagation()
                            setSelectedSession(session)
                          }}
                        >
                          Details
                        </button>
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
              Page {currentPage} of {totalPages} ({filteredSessions.length} total)
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

      {selectedSession && (
        <div
          role="presentation"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
          onMouseDown={(e) => e.target === e.currentTarget && setSelectedSession(null)}
        >
          <div
            role="dialog"
            aria-modal="true"
            aria-labelledby="session-detail-title"
            className="max-h-[90vh] w-full max-w-3xl overflow-y-auto rounded-2xl border border-[var(--border)] bg-[var(--card)] p-6 shadow-2xl animate-in text-sm text-[var(--fg)]"
          >
            <div className="flex items-center justify-between border-b border-[var(--border)] pb-3">
              <div>
                <span className="text-[10px] font-bold uppercase tracking-wider text-[var(--primary)]">
                  Parking Session
                </span>
                <h3 id="session-detail-title" className="text-lg font-bold text-[var(--fg)]">
                  #{selectedSession.id}
                </h3>
              </div>
              <button
                type="button"
                onClick={() => setSelectedSession(null)}
                className="h-8 w-8 rounded-full border border-[var(--border)] bg-[var(--bg)] flex items-center justify-center text-[var(--muted)] hover:text-[var(--fg)]"
              >
                <UntitledIcon name="x" size={16} />
              </button>
            </div>

            <div className="mt-4 space-y-4">
              <div className="grid grid-cols-1 gap-3 rounded-lg border border-[var(--border)] bg-[var(--bg)] p-3 text-xs sm:grid-cols-2">
                <SessionField label="Reservation ID" value={selectedSession.reservationId} />
                <SessionField label="Tenant ID" value={selectedSession.tenantId} />
                <SessionField label="Site ID" value={selectedSession.siteId} />
                <SessionField label="Access Item ID" value={selectedSession.accessItemId} />
                <SessionField label="Account ID" value={selectedSession.accountId} />
                <SessionField label="Session Type" value={selectedSession.reservationId ? "Reserved" : "Walk-in"} />
                <SessionField label="Slot" value={sessionSlotLabel(selectedSession)} />
                <SessionField label="Original Plate" value={selectedSession.originalPlate} />
                <SessionField label="Normalized Plate" value={selectedSession.normalizedPlate} />
                <SessionField label="Vehicle Type" value={selectedSession.vehicleType} />
                <SessionField label="Ticket Reference" value={selectedSession.ticketReference} />
                <SessionField label="Session Status" value={selectedSession.status} />
                <SessionField label="Entry Time" value={new Date(selectedSession.entryTime).toLocaleString()} />
                <SessionField label="Exit Time" value={selectedSession.exitTime ? new Date(selectedSession.exitTime).toLocaleString() : null} />
                <SessionField label="Created At" value={new Date(selectedSession.createdAt).toLocaleString()} />
              </div>
              <div className="grid gap-3 sm:grid-cols-2">
                <EvidenceImage label="Check-in scan" image={selectedSession.entryImageUrl} />
                <EvidenceImage label="Check-out scan" image={selectedSession.exitImageUrl} />
              </div>
            </div>

            <div className="mt-6 flex justify-end gap-2 border-t border-[var(--border)] pt-3">
              <button
                type="button"
                className="btn-outline text-xs py-1.5 px-3"
                onClick={() => setSelectedSession(null)}
              >
                Close
              </button>
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

function SessionField({ label, value }: { label: string; value: string | null }) {
  return (
    <div className="min-w-0">
      <span className="block text-[var(--muted)]">{label}</span>
      <span className="break-all font-medium">{value ?? "—"}</span>
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
