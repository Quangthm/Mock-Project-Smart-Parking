import { useEffect, useState } from "react"

import { useApp } from "../../../context/AppContext"

import { driverData as store } from "../data/data"

import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { PageHeader } from "../../../components/ui/PageHeader"
import { ticketPresentation } from "../../../lib/ticketPresentation"
import "../../../booking-support.css"

const ticketDate = new Intl.DateTimeFormat("en-US", { dateStyle: "medium" })


export function DriverSupport() {
  const { user } = useApp()

  const [form, setForm] = useState({ subject: "", message: "" })


  const [sent, setSent] = useState(false)
  const [bookingId, setBookingId] = useState("")
  const [, setDataRevision] = useState(0)

  useEffect(() => {
    const refresh = () => setDataRevision(current => current + 1)
    window.addEventListener("sp-data-change", refresh)
    return () => window.removeEventListener("sp-data-change", refresh)
  }, [])

  const bookings = user ? store.getBookingsByDriver(user.id) : []
  const tickets = user ? store.getTickets().filter(ticket => ticket.userId === user.id)
    .sort((a, b) => b.createdAt.localeCompare(a.createdAt)) : []

  function submit(e: React.FormEvent) {
    e.preventDefault()

    if (!user) return

    const linkedBooking = bookings.find(booking => booking.id === bookingId)
    const ticket = store.createTicket({
      userId: user.id,

      userName: user.name,

      subject: form.subject,

      message: form.message,
      bookingId: linkedBooking?.id,
      siteId: linkedBooking?.lotId,

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
    setForm({ subject: "", message: "" })
    setBookingId("")
  }

  return <div className="driver-support-page">
    <PageHeader title="Support & Tickets" description="Send an issue to the parking team and track their decision here." variant="compact" />
    <div className="driver-support-layout">
      <form className="driver-support-form" onSubmit={submit}>
        <h2>New ticket</h2>
        <p>Describe what happened so the operator can investigate.</p>
        {sent && <div role="status" className="driver-support-success"><UntitledIcon name="check-circle" size={18} />Ticket submitted. You can follow its status below.</div>}
        <label htmlFor="driver-ticket-booking">Related booking <span>(optional)</span></label>
        <select id="driver-ticket-booking" name="bookingId" className="input" value={bookingId} onChange={event => setBookingId(event.target.value)}>
          <option value="">General support</option>
          {bookings.map(booking => <option key={booking.id} value={booking.id}>{booking.lotName} · {booking.licensePlate}</option>)}
        </select>
        <label htmlFor="driver-ticket-subject">Subject</label>
        <input id="driver-ticket-subject" name="subject" autoComplete="off" className="input" value={form.subject} onChange={event => { setSent(false); setForm(current => ({ ...current, subject: event.target.value })) }} placeholder="e.g. Incorrect charge" required />
        <label htmlFor="driver-ticket-message">What happened?</label>
        <textarea id="driver-ticket-message" name="message" autoComplete="off" className="input" rows={5} value={form.message} onChange={event => { setSent(false); setForm(current => ({ ...current, message: event.target.value })) }} placeholder="Include the time, location, and what you need help with…" required />
        <button type="submit" className="btn-primary">Submit ticket</button>
      </form>
      <section className="driver-ticket-history" aria-labelledby="driver-ticket-history-title">
        <div className="driver-ticket-history-header"><div><h2 id="driver-ticket-history-title">Your tickets</h2><p>Operator decisions and reasons appear with each request.</p></div><span>{tickets.length}</span></div>
        {tickets.length ? <div className="driver-ticket-list">{tickets.map(ticket => {
          const view = ticketPresentation(ticket)
          return <article key={ticket.id} className="driver-ticket-card">
            <div className="driver-ticket-card-head"><div><h3>{ticket.subject}</h3><p>#{ticket.id} · {ticketDate.format(new Date(ticket.createdAt))}</p></div><span className={`ticket-decision is-${view.decision || ticket.status}`}>{view.statusLabel}</span></div>
            <p className="driver-ticket-inquiry">{view.inquiry}</p>
            {view.note && <div className="driver-ticket-response"><strong>Operator response</strong>{view.reason ? <p><b>Reason:</b> {view.reason}</p> : null}{view.detail ? <p><b>{view.decision === "approved" ? "Fixed:" : "Outstanding issue:"}</b> {view.detail}</p> : null}{!view.reason && !view.detail ? <p>{view.note}</p> : null}</div>}
            {ticket.status !== "resolved" && <p className="driver-ticket-awaiting">{ticket.status === "in-progress" ? "The operator is investigating this ticket." : "Waiting for operator review."}</p>}
          </article>
        })}</div> : <div className="driver-ticket-empty" role="status">No tickets yet. Your requests and their decisions will appear here.</div>}
      </section>
    </div>
  </div>
}
