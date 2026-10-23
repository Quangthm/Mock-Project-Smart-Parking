import { useEffect, useState } from "react"

import { useApp } from "../../../context/AppContext"
import { ParkingHistory, type ParkingHistoryRecord } from "../../../components/history/ParkingHistory"
import { PageHeader } from "../../../components/ui/PageHeader"

import { driverData as store } from "../data/data"

type HistorySection = "bookings" | "tickets"
const dateTime = new Intl.DateTimeFormat("en-US", { dateStyle: "medium", timeStyle: "short" })
const currency = new Intl.NumberFormat("vi-VN")

export function BookingHistory() {
  const { user } = useApp()

  const [section, setSection] = useState<HistorySection>("bookings")
  const [, setDataRevision] = useState(0)

  useEffect(() => {
    const refresh = () => setDataRevision(current => current + 1)
    window.addEventListener("sp-data-change", refresh)
    return () => window.removeEventListener("sp-data-change", refresh)
  }, [])

  const bookings = user ? store.getBookingsByDriver(user.id) : []
  const lots = store.getLots()
  const historyRecords: ParkingHistoryRecord[] = bookings
    .filter((booking) => booking.status === "completed" || booking.status === "cancelled")
    .sort((a, b) => (b.completedAt || b.endTime).localeCompare(a.completedAt || a.endTime))
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
        { label: "Created at", value: Number.isNaN(new Date(booking.createdAt).getTime()) ? "—" : dateTime.format(new Date(booking.createdAt)) },
      ],
    }))

  const tickets = store
    .getTickets()
    .filter((ticket) => ticket.userId === user?.id)

    .sort((a, b) => b.createdAt.localeCompare(a.createdAt))

  return (
    <div>
      <PageHeader title="Booking History" variant="compact" />
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
          Past Bookings <span>{historyRecords.length}</span>
        </button>
        <button
          type="button"
          aria-pressed={section === "tickets"}
          className={section === "tickets" ? "is-active" : ""}
          onClick={() => setSection("tickets")}
        >
          Ticket History <span>{tickets.length}</span>
        </button>
      </div>
      {section === "bookings" ? (
        <ParkingHistory
          title="Your Parking History"
          scope="Completed and cancelled bookings"
          scopeKey={user?.id ?? ""}
          records={historyRecords}
          dateLabel="Booked for"
          emptyMessage="Your completed or cancelled bookings will appear here."
        />
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
