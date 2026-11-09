import { useCallback, useEffect, useState } from "react"

import { useApp } from "../../../context/AppContext"
import { ParkingHistory, type ParkingHistoryRecord } from "../../../components/history/ParkingHistory"
import { PageHeader } from "../../../components/ui/PageHeader"
import { driverApi, type DriverParkingSession } from "../../../lib/driverApi"

import { driverData as store } from "../data/data"

type HistorySection = "bookings" | "tickets"
const dateTime = new Intl.DateTimeFormat("en-US", { dateStyle: "medium", timeStyle: "short" })
const currency = new Intl.NumberFormat("vi-VN")

export function BookingHistory() {
  const { user } = useApp()

  const [section, setSection] = useState<HistorySection>("bookings")
  const [sessions, setSessions] = useState<DriverParkingSession[]>([])
  const [loading, setLoading] = useState<boolean>(true)
  const [error, setError] = useState<string | null>(null)

  const fetchHistory = useCallback(async () => {
    if (!user) {
      setLoading(false)
      return
    }

    setLoading(true)
    setError(null)
    try {
      const liveSessions = await driverApi.getParkingHistory()
      setSessions(liveSessions)
    } catch (err: unknown) {
      // Fallback gracefully to local booking store if backend service unavailable in offline dev
      const message = err instanceof Error ? err.message : "Unable to reach session operations."
      setError(message)
    } finally {
      setLoading(false)
    }
  }, [user])

  useEffect(() => {
    fetchHistory()
    const refresh = () => fetchHistory()
    window.addEventListener("sp-data-change", refresh)
    return () => window.removeEventListener("sp-data-change", refresh)
  }, [fetchHistory])

  const lots = store.getLots()

  // Transform authoritative parking sessions from OperationService
  const liveRecords: ParkingHistoryRecord[] = sessions.map((s) => {
    const lot = lots.find((l) => l.id === s.siteId)
    const isCompleted = s.status === "COMPLETED" || !!s.exitTime
    return {
      id: s.id,
      plate: s.originalPlate || s.normalizedPlate || "—",
      title: lot?.name || `Parking Site ${s.siteId.slice(0, 8)}`,
      subtitle: lot?.address || undefined,
      slot: s.slotNumber || (s.currentSlotId ? `Slot ${s.currentSlotId.slice(0, 4)}` : "Unassigned"),
      vehicleType: s.vehicleType,
      status: isCompleted ? "COMPLETED" : "ACTIVE",
      kind: s.reservationId ? "Reserved" : "Drive-in",
      startedAt: s.entryTime,
      endedAt: s.exitTime,
      details: [
        { label: "Session ID", value: s.id },
        { label: "Ticket Ref", value: s.ticketReference || "—" },
        { label: "Vehicle Type", value: s.vehicleType },
        {
          label: "Actual Entry",
          value: Number.isNaN(new Date(s.entryTime).getTime()) ? "—" : dateTime.format(new Date(s.entryTime)),
        },
        {
          label: "Actual Exit",
          value: s.exitTime && !Number.isNaN(new Date(s.exitTime).getTime()) ? dateTime.format(new Date(s.exitTime)) : "In Progress",
        },
      ],
      evidence: [
        { label: "Entry Photo", image: s.entryImageUrl ?? null },
        { label: "Exit Photo", image: s.exitImageUrl ?? null },
      ].filter((ev) => !!ev.image),
      showDuration: true,
    }
  })

  // If live backend had error or returned empty, we merge/fallback with driver reservation store
  const bookings = user ? store.getBookingsByDriver(user.id) : []
  const bookingRecords: ParkingHistoryRecord[] = bookings
    .filter((booking) => booking.status === "completed" || booking.status === "cancelled")
    .map((booking) => ({
      id: booking.id,
      plate: booking.licensePlate,
      title: booking.lotName,
      subtitle: lots.find((lot) => lot.id === booking.lotId)?.address,
      slot: booking.slotNumber,
      status: booking.status.toUpperCase(),
      kind: "Reserved",
      startedAt: booking.startTime,
      endedAt: booking.endTime,
      details: [
        { label: "Booking ID", value: booking.id },
        { label: "Payment status", value: booking.paymentStatus || "—" },
        { label: "Amount", value: `${currency.format(booking.amount)} VND` },
        {
          label: "Created at",
          value: Number.isNaN(new Date(booking.createdAt).getTime()) ? "—" : dateTime.format(new Date(booking.createdAt)),
        },
      ],
    }))

  // Use authoritative sessions if available; otherwise booking fallback when error occurs
  const historyRecords = liveRecords.length > 0 ? liveRecords : (error ? bookingRecords : liveRecords)

  const tickets = store
    .getTickets()
    .filter((ticket) => ticket.userId === user?.id)
    .sort((a, b) => b.createdAt.localeCompare(a.createdAt))

  return (
    <div className="driver-history-list">
      <PageHeader title="Booking & Parking History" variant="compact" />
      <div
        className="booking-history-tabs driver-history-tabs"
        role="group"
        aria-label="History category"
        style={{
          display: "flex",
          gap: "0.75rem",
          flexWrap: "wrap",
          marginTop: "1.25rem",
          marginBottom: "1.5rem",
        }}
      >
        <button
          type="button"
          aria-pressed={section === "bookings"}
          className={section === "bookings" ? "is-active" : ""}
          onClick={() => setSection("bookings")}
        >
          Past Sessions & Bookings <span>{historyRecords.length}</span>
        </button>
        <button
          type="button"
          aria-pressed={section === "tickets"}
          className={section === "tickets" ? "is-active" : ""}
          onClick={() => setSection("tickets")}
        >
          Support Tickets <span>{tickets.length}</span>
        </button>
      </div>

      {section === "bookings" ? (
        <div className="driver-history-content">
          <ParkingHistory
            title="Your Parking History"
            scope="Active and completed parking sessions"
            scopeKey={user?.id ?? ""}
            records={historyRecords}
            dateLabel="Entry Time"
            emptyMessage="Your active and completed parking sessions will appear here."
            loading={loading}
            error={error && historyRecords.length === 0 ? error : undefined}
            onRetry={fetchHistory}
            showVehicleFilter={true}
          />
        </div>
      ) : tickets.length ? (
        <div style={{ display: "grid", gap: "0.75rem" }}>
          {tickets.map((ticket) => {
            const color = ticket.status === "resolved" ? "#22c55e" : "#f59e0b"

            return (
              <article key={ticket.id} className="card">
                <div
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    gap: "1rem",
                    flexWrap: "wrap",
                  }}
                >
                  <strong>{ticket.subject}</strong>
                  <span style={{ color, fontWeight: 700 }}>
                    {ticket.status === "resolved" ? "Resolved" : "Processing"}
                  </span>
                </div>
                <p
                  style={{
                    margin: "0.5rem 0",
                    color: "var(--muted)",
                    fontSize: "0.82rem",
                  }}
                >
                  Ticket ID: {ticket.id} ·{" "}
                  {new Date(ticket.createdAt).toLocaleString()}
                  {ticket.bookingId ? ` · Booking ${ticket.bookingId}` : ""}
                </p>
                <p
                  style={{
                    margin: 0,
                    whiteSpace: "pre-wrap",
                    fontSize: "0.875rem",
                  }}
                >
                  {ticket.message}
                </p>
              </article>
            )
          })}
        </div>
      ) : (
        <EmptyHistory message="No tickets yet." />
      )}
    </div>
  )
}

function EmptyHistory({ message }: { message: string }) {
  return (
    <div
      className="card"
      style={{ textAlign: "center", padding: "2rem", color: "var(--muted)" }}
    >
      {message}
    </div>
  )
}
