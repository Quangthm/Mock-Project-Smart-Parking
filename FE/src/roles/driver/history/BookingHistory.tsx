import { useState } from "react"
import { useApp } from "../../../context/AppContext"
import { driverData as store } from "../data/data"
import { BookingCard } from "../booking/BookingCard"

type HistorySection = "bookings" | "tickets"

export function BookingHistory() {
  const { user } = useApp()
  const [section, setSection] = useState<HistorySection>("bookings")
  const bookings = store.getBookingsByDriver(user?.id ?? "")
    .filter(booking => booking.status === "completed")
    .sort((a, b) => (b.completedAt ?? b.createdAt).localeCompare(a.completedAt ?? a.createdAt))
  const tickets = store.getTickets().filter(ticket => ticket.userId === user?.id)
    .sort((a, b) => b.createdAt.localeCompare(a.createdAt))

  return <div>
    <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap", marginBottom: "1rem" }}>
      <button type="button" className={section === "bookings" ? "btn-primary" : "btn-outline"} onClick={() => setSection("bookings")}>Completed Bookings ({bookings.length})</button>
      <button type="button" className={section === "tickets" ? "btn-primary" : "btn-outline"} onClick={() => setSection("tickets")}>Ticket History ({tickets.length})</button>
    </div>
    {section === "bookings" ? bookings.length ? <div style={{ display: "grid", gap: "0.875rem" }}>
      {bookings.map(booking => <BookingCard key={booking.id} booking={booking} />)}
    </div> : <EmptyHistory message="No completed bookings yet." /> : tickets.length ? <div style={{ display: "grid", gap: "0.75rem" }}>
      {tickets.map(ticket => {
        const color = ticket.status === "resolved" ? "#22c55e" : "#f59e0b"
        return <article key={ticket.id} className="card">
          <div style={{ display: "flex", justifyContent: "space-between", gap: "1rem", flexWrap: "wrap" }}><strong>{ticket.subject}</strong><span style={{ color, fontWeight: 700 }}>{ticket.status === "resolved" ? "Resolved" : "Processing"}</span></div>
          <p style={{ margin: "0.5rem 0", color: "var(--muted)", fontSize: "0.82rem" }}>Ticket ID: {ticket.id} · {new Date(ticket.createdAt).toLocaleString()}{ticket.bookingId ? ` · Booking ${ticket.bookingId}` : ""}</p>
          <p style={{ margin: 0, whiteSpace: "pre-wrap", fontSize: "0.875rem" }}>{ticket.message}</p>
        </article>
      })}
    </div> : <EmptyHistory message="No tickets yet." />}
  </div>
}

function EmptyHistory({ message }: { message: string }) {
  return <div className="card" style={{ textAlign: "center", padding: "2rem", color: "var(--muted)" }}>{message}</div>
}
