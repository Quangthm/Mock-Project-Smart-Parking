import { useState } from "react"

import { useApp } from "../../../context/AppContext"

import { driverData as store } from "../data/data"

import { UntitledIcon } from "../../../components/icon/UntitledIcon"

// Support form

export function DriverSupport() {
  const { user } = useApp()

  const [form, setForm] = useState({ subject: "", message: "" })

  const [bookingId, setBookingId] = useState("")

  const [sent, setSent] = useState(false)

  function submit(e: React.FormEvent) {
    e.preventDefault()

    if (!user) return

    const ticket = store.createTicket({
      userId: user.id,

      userName: user.name,

      subject: form.subject,

      message: form.message,

      bookingId: bookingId || undefined,
    })

    store.createNotification({
      recipientId: user.id,
      recipientRole: "driver",
      type: "TICKET_SUBMITTED",
      title: "Ticket submitted",
      message:
        "Your ticket has been submitted and is waiting for Operator review.",
      relatedEntityId: ticket.id,
    })

    setSent(true)
  }

  if (sent)
    return (
      <div style={{ textAlign: "center", padding: "3rem" }}>
        <div style={{ color: "#16a34a", marginBottom: "0.5rem" }}>
          <UntitledIcon name="check-circle" size={32} />
        </div>
        <p style={{ color: "#22c55e", fontWeight: 600 }}>
          Support ticket submitted!
        </p>
        <button
          className="btn-outline"
          style={{ marginTop: "1rem" }}
          onClick={() => {
            setSent(false)

            setForm({ subject: "", message: "" })

            setBookingId("")
          }}
        >
          New Ticket
        </button>
      </div>
    )

  return (
    <form
      onSubmit={submit}
      style={{
        maxWidth: "500px",

        display: "flex",

        flexDirection: "column",

        gap: "1rem",
      }}
    >
      <h3
        style={{
          fontFamily: "Outfit",

          fontWeight: 700,

          fontSize: "1.1rem",

          color: "var(--fg)",

          margin: 0,
        }}
      >
        Support & Appeals
      </h3>
      <div>
        <label className="label">Subject</label>
        <input
          className="input"
          value={form.subject}
          onChange={(e) => setForm((f) => ({ ...f, subject: e.target.value }))}
          placeholder="e.g. Incorrect charge, appeal"
          required
        />
      </div>
      <div>
        <label className="label">Related Booking (optional)</label>
        <select
          className="input"
          value={bookingId}
          onChange={(e) => setBookingId(e.target.value)}
        >
          <option value="">No related booking</option>
          {store.getBookingsByDriver(user?.id ?? "").map((booking) => (
            <option key={booking.id} value={booking.id}>
              {booking.id} · {booking.lotName}
            </option>
          ))}
        </select>
      </div>
      <div>
        <label className="label">Message</label>
        <textarea
          className="input"
          rows={5}
          value={form.message}
          onChange={(e) => setForm((f) => ({ ...f, message: e.target.value }))}
          placeholder="Describe your issue..."
          required
          style={{ resize: "vertical" }}
        />
      </div>
      <button
        type="submit"
        className="btn-primary"
        style={{ alignSelf: "flex-start" }}
      >
        Submit Ticket
      </button>
    </form>
  )
}
