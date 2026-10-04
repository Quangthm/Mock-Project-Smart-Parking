import type { Booking } from "../../../lib/types"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"

export function BookingCard({ booking, onPayNow }: { booking: Booking; onPayNow?: (booking: Booking) => void }) {
  const paymentStatus = booking.paymentStatus ?? (booking.status === "completed" || booking.status === "checked-in" || booking.status === "confirmed" ? "paid" : "pending")
  const bookingColor = booking.status === "completed" ? "#22c55e" : booking.status === "cancelled" ? "#ef4444" : booking.status === "checked-in" ? "#2563eb" : booking.status === "confirmed" ? "#22c55e" : "#f59e0b"
  const paymentColor = paymentStatus === "paid" ? "#22c55e" : paymentStatus === "failed" ? "#ef4444" : "#f59e0b"
  return (
    <div
      className="card"
      style={{
        display: "flex",
        justifyContent: "space-between",
        gap: "1rem",
        flexWrap: "wrap",
      }}
      >
        <div style={{ fontSize: "0.72rem", color: "var(--muted)", marginBottom: "0.25rem" }}>Booking ID: {booking.id}</div>
      <div>
        <div
          style={{
            fontFamily: "Outfit",
            fontWeight: 600,
            fontSize: "0.95rem",
            color: "var(--fg)",
            marginBottom: "0.25rem",
          }}
        >
          {booking.lotName}
        </div>
        <div style={{ fontSize: "0.82rem", color: "var(--muted)" }}>
          Slot {booking.slotNumber} · {booking.licensePlate}{" "}
          <UntitledIcon name={booking.plateType === "foreign" ? "globe" : "map-pin"} size={14} />
        </div>
        <div
          style={{
            fontSize: "0.78rem",
            color: "var(--muted)",
            marginTop: "0.2rem",
          }}
        >
          {new Date(booking.startTime).toLocaleString("en-GB", {
            dateStyle: "short",
            timeStyle: "short",
          })}{" "}
          –{" "}
          {new Date(booking.endTime).toLocaleString("en-GB", {
            dateStyle: "short",
            timeStyle: "short",
          })}
        </div>
        {booking.status === "completed" && booking.completedAt && <div style={{ marginTop: "0.25rem", fontSize: "0.75rem", color: "var(--muted)" }}>Completed: {new Date(booking.completedAt).toLocaleString("en-GB", { dateStyle: "short", timeStyle: "short" })}</div>}
      </div>
      <div style={{ textAlign: "right" }}>
        <div
          style={{
            display: "inline-block",
            padding: "0.2rem 0.625rem",
            borderRadius: "999px",
            fontSize: "0.75rem",
            fontWeight: 600,
            background: `${bookingColor}20`,
            color: bookingColor,
          }}
        >
        {booking.status.replace("-", " ").toUpperCase()}
        </div>
        <div style={{ display: "block", marginTop: "0.35rem", padding: "0.2rem 0.625rem", borderRadius: "999px", fontSize: "0.7rem", fontWeight: 600, background: `${paymentColor}20`, color: paymentColor }}>Payment {paymentStatus.toUpperCase()}</div>
        <div
          style={{
            fontFamily: "Outfit",
            fontWeight: 700,
            fontSize: "0.95rem",
            color: "var(--fg)",
            marginTop: "0.375rem",
          }}
        >
          {booking.amount.toLocaleString("vi-VN")}₫
        </div>
        {onPayNow && paymentStatus !== "paid" && booking.status !== "completed" && booking.status !== "cancelled" && <button type="button" className="btn-primary" onClick={() => onPayNow(booking)} style={{ marginTop: "0.5rem", padding: "0.4rem 0.75rem", fontSize: "0.78rem", background: "#f59e0b", color: "#111827" }}>Pay Now</button>}
      </div>
    </div>
  )
}
