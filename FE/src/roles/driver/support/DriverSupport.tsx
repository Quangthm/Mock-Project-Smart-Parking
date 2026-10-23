import { useState } from "react"

import { useApp } from "../../../context/AppContext"

import { driverData as store } from "../data/data"

import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { PageHeader } from "../../../components/ui/PageHeader"

// Support form

export function DriverSupport() {
  const { user } = useApp()

  const [form, setForm] = useState({ subject: "", message: "" })


  const [sent, setSent] = useState(false)

  function submit(e: React.FormEvent) {
    e.preventDefault()

    if (!user) return

    const ticket = store.createTicket({
      userId: user.id,

      userName: user.name,

      subject: form.subject,

      message: form.message,

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
        <PageHeader title="Support ticket submitted!" variant="compact" align="center" />
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
      <PageHeader title="Support & Appeals" variant="compact" />
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
