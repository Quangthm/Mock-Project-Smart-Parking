import { useApp } from "../../../context/AppContext"
import { driverData as store } from "../data/data"
import { BookingCard } from "./BookingCard"
import type { Booking } from "../../../lib/types"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"

// Bookings tab
export function ActiveBookings({ onPayNow }: { onPayNow: (booking: Booking) => void }) {
  const { user } = useApp()
  const bookings = store
    .getBookingsByDriver(user?.id ?? "")
    .filter((b) => !["completed", "cancelled"].includes(b.status))

  if (!bookings.length)
    return (
      <div
        style={{ textAlign: "center", padding: "3rem", color: "var(--muted)" }}
      >
        <div style={{ color: 'var(--primary)', marginBottom: "0.75rem" }}><UntitledIcon name="parking" size={32} /></div>
        <p>No active bookings. Find a lot and book now!</p>
      </div>
    )

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "0.875rem" }}>
      {bookings.map((b) => (
        <BookingCard key={b.id} booking={b} onPayNow={onPayNow} />
      ))}
    </div>
  )
}
