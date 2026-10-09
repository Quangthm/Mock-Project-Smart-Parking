import { useState } from "react"

import { useApp } from "../context/AppContext"

import { supportData as store } from "../roles/support/data"

import { UntitledIcon } from "../components/icon/UntitledIcon"

export function Support() {
  const { user } = useApp()

  const [form, setForm] = useState({ subject: "", message: "" })

  const [sent, setSent] = useState(false)

  function submit(e: React.FormEvent) {
    e.preventDefault()

    if (!form.subject || !form.message) return

    const ticket = store.createTicket({
      userId: user?.id ?? "anonymous",
      userName: user?.name ?? "Guest",
      subject: form.subject,
      message: form.message,
    })

    if (user?.role === "driver")
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

    setForm({ subject: "", message: "" })
  }

  return (
    <div
      style={{ maxWidth: "900px", margin: "0 auto", padding: "3rem 1.5rem" }}
    >
      <h1 className="section-title" style={{ marginBottom: "0.5rem" }}>
        Help & Support
      </h1>
      <p className="section-sub" style={{ marginBottom: "3rem" }}>
        Contact our team for help with your parking experience.
      </p>
      {/* Contact form */}
      <section>
        <h2
          style={{
            fontFamily: "Outfit",
            fontWeight: 700,
            fontSize: "1.25rem",
            marginBottom: "1.25rem",
            color: "var(--fg)",
          }}
        >
          Contact Support
        </h2>
        {sent ? (
          <div
            style={{
              background: "#f0fdf4",
              border: "1px solid #86efac",
              borderRadius: "var(--radius)",
              padding: "1.5rem",
              textAlign: "center",
            }}
          >
            <div style={{ color: "#16a34a", marginBottom: "0.5rem" }}>
              <UntitledIcon name="check-circle" size={28} />
            </div>
            <p style={{ color: "#166534", fontWeight: 600 }}>
              Ticket submitted successfully!
            </p>
            <p style={{ color: "#166534", fontSize: "0.875rem" }}>
              We'll respond within 24 hours via email.
            </p>
            <button
              className="btn-outline"
              style={{ marginTop: "1rem" }}
              onClick={() => setSent(false)}
            >
              Submit Another
            </button>
          </div>
        ) : (
          <form
            onSubmit={submit}
            className="card"
            style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
          >
            <div>
              <label className="label">Subject</label>
              <input
                className="input"
                value={form.subject}
                onChange={(e) =>
                  setForm((f) => ({ ...f, subject: e.target.value }))
                }
                placeholder="Describe your issue briefly"
                required
              />
            </div>
            <div>
              <label className="label">Message</label>
              <textarea
                className="input"
                rows={5}
                value={form.message}
                onChange={(e) =>
                  setForm((f) => ({ ...f, message: e.target.value }))
                }
                placeholder="Provide details about your issue..."
                required
                style={{ resize: "vertical" }}
              />
            </div>
            <button
              type="submit"
              className="btn-primary"
              style={{ alignSelf: "flex-start" }}
            >
              Send Ticket
            </button>
          </form>
        )}
      </section>

      <div
        className="card"
        style={{
          marginTop: "2rem",
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))",
          gap: "1rem",
        }}
      >
        {[
          { icon: "📞", label: "Driver Hotline", value: "1900-xxxx" },

          { icon: "📞", label: "Business (B2B)", value: "090xxxxxxx" },

          {
            icon: "✉️",
            label: "Email",
            value: "support.smartparkingvn@gmail.com",
          },
        ].map((c) => (
          <div key={c.label} style={{ textAlign: "center" }}>
            <div style={{ color: "var(--primary)", marginBottom: "0.25rem" }}>
              <UntitledIcon name={c.icon} size={22} />
            </div>
            <div
              style={{
                fontSize: "0.8rem",
                color: "var(--muted)",
                marginBottom: "0.2rem",
              }}
            >
              {c.label}
            </div>
            <div
              style={{
                fontWeight: 600,
                color: "var(--fg)",
                fontSize: "0.9rem",
              }}
            >
              {c.value}
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
