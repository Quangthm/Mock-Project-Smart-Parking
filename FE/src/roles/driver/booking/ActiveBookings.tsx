import { UntitledIcon } from "../../../components/icon/UntitledIcon"

// Bookings tab

export function ActiveBookings() {
  return (
      <div
        style={{ textAlign: "center", padding: "3rem", color: "var(--muted)" }}
      >
        <div style={{ color: "var(--primary)", marginBottom: "0.75rem" }}>
          <UntitledIcon name="parking" size={32} />
        </div>
        <p>Active bookings are not available for your account yet.</p>
      </div>
  )
}
