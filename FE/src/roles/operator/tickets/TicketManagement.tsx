import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { useEffect, useState, useMemo, useRef } from "react"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { PageHeader } from "../../../components/ui/PageHeader"
import { operatorOperationsApi } from "../../../lib/operatorApi"
import type { SupportTicket } from "../../../lib/types"
import { store } from "../../../lib/store"
import { showUiToast } from "../../../lib/uiToast"
import { formatTicketDecision, ticketPresentation } from "../../../lib/ticketPresentation"
import "../../../booking-support.css"

const localTicketsForSite = (siteId: string) => store.getTickets().filter(ticket => !ticket.siteId || ticket.siteId === siteId)
const newestFirst = (items: SupportTicket[]) => items.sort((a, b) => b.createdAt.localeCompare(a.createdAt))

export function TicketManagement({ siteId }: { siteId: string }) {
  const [tickets, setTickets] = useState<SupportTicket[]>([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState("")
  const [busyTicketId, setBusyTicketId] = useState<string | null>(null)

  const [searchQuery, setSearchQuery] = useState("")
  const [statusFilter, setStatusFilter] = useState<"all" | "open" | "in-progress" | "resolved" | "approved" | "rejected">("all")
  const [selectedTicket, setSelectedTicket] = useState<SupportTicket | null>(null)
  const [decision, setDecision] = useState<"approved" | "rejected">("approved")
  const [decisionReason, setDecisionReason] = useState("")
  const [decisionDetail, setDecisionDetail] = useState("")
  const [actionFeedback, setActionFeedback] = useState<string | null>(null)
  const dialogRef = useRef<HTMLDivElement>(null)
  const closeRef = useRef<HTMLButtonElement>(null)
  const returnFocusRef = useRef<HTMLElement | null>(null)

  useEffect(() => {
    if (!selectedTicket) return
    returnFocusRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null
    closeRef.current?.focus()
    return () => returnFocusRef.current?.focus()
  }, [selectedTicket?.id])

  function handleDialogKeyDown(event: React.KeyboardEvent<HTMLDivElement>) {
    if (event.key === "Escape") { setSelectedTicket(null); return }
    if (event.key !== "Tab") return
    const controls = dialogRef.current?.querySelectorAll<HTMLElement>('button:not(:disabled), input:not(:disabled), textarea:not(:disabled)')
    if (!controls?.length) return
    const first = controls[0]
    const last = controls[controls.length - 1]
    if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus() }
    else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus() }
  }

  function openTicket(ticket: SupportTicket) {
    setSelectedTicket(ticket)
    setDecision("approved")
    setDecisionReason("")
    setDecisionDetail("")
  }

  useEffect(() => {
    if (!siteId) {
      setTickets([])
      setLoadError("No assigned parking site is available.")
      setLoading(false)
      return
    }
    let active = true
    setLoading(true)
    setLoadError("")
    operatorOperationsApi.listTickets(siteId)
      .then((result) => {
        if (active) setTickets(newestFirst([...result, ...localTicketsForSite(siteId).filter(local => !result.some(server => server.id === local.id))]))
      })
      .catch((error: unknown) => {
        if (active) {
          setTickets(newestFirst(localTicketsForSite(siteId)))
          setLoadError(error instanceof Error ? error.message : "Could not load server tickets. Local tickets remain available.")
        }
      })
      .finally(() => {
        if (active) setLoading(false)
      })
    return () => {
      active = false
    }
  }, [siteId])

  useEffect(() => {
    const refreshLocal = () => setTickets(current => {
      const serverTickets = current.filter(ticket => !store.getTickets().some(local => local.id === ticket.id))
      return newestFirst([...serverTickets, ...localTicketsForSite(siteId)])
    })
    window.addEventListener("sp-data-change", refreshLocal)
    return () => window.removeEventListener("sp-data-change", refreshLocal)
  }, [siteId])

  async function updateTicket(
    ticket: SupportTicket,
    status: SupportTicket["status"],
    note?: string,
    ticketDecision?: "approved" | "rejected",
  ) {
    if (!siteId || busyTicketId) return
    setBusyTicketId(ticket.id)
    setLoadError("")
    try {
      const local = store.getTickets().find(item => item.id === ticket.id)
      const updated = local
        ? { ...local, status, decision: ticketDecision, resolutionNote: note }
        : await operatorOperationsApi.updateTicket(siteId, ticket.id, status, note)
      if (local) store.saveTicket(updated)
      setTickets((current) => current.map((item) => item.id === ticket.id ? updated : item))
      if (selectedTicket?.id === ticket.id) setSelectedTicket(updated)
      setActionFeedback(`Ticket #${ticket.id} ${ticketDecision || `updated to ${status}`}.`)
      showUiToast("Ticket updated", `Ticket #${ticket.id} ${ticketDecision || `updated to ${status}`}.`)
      if (local && ticketDecision) store.createNotification({ recipientId: ticket.userId, recipientRole: "driver", type: "TICKET_RESOLVED", title: `Ticket ${ticketDecision}`, message: note || "Your ticket was reviewed.", relatedEntityId: ticket.id })
      setDecisionReason("")
      setDecisionDetail("")
    } catch (error) {
      const message = error instanceof Error ? error.message : "Could not update the support ticket."
      setLoadError(message)
      showUiToast("Ticket not updated", message, "error")
    } finally {
      setBusyTicketId(null)
    }
  }

  const TICKET_STATUS_FILTER_PREDICATES: Record<string, (t: SupportTicket) => boolean> = {
    approved: (t) => ticketPresentation(t).decision === "approved",
    rejected: (t) => ticketPresentation(t).decision === "rejected",
    open: (t) => t.status === "open",
    "in-progress": (t) => t.status === "in-progress",
    resolved: (t) => t.status === "resolved",
  }

  // Filtered tickets
  const filteredTickets = useMemo(() => {
    return tickets.filter((t) => {
      if (statusFilter !== "all") {
        const predicate = TICKET_STATUS_FILTER_PREDICATES[statusFilter]
        if (predicate && !predicate(t)) return false
      }
      if (searchQuery.trim()) {
        const q = searchQuery.trim().toLowerCase()
        const matchSubject = t.subject.toLowerCase().includes(q)
        const matchMessage = t.message.toLowerCase().includes(q)
        const matchDriver = t.userName.toLowerCase().includes(q)
        const matchId = t.id.toLowerCase().includes(q)
        const matchBooking = t.bookingId ? t.bookingId.toLowerCase().includes(q) : false
        if (!matchSubject && !matchMessage && !matchDriver && !matchId && !matchBooking) {
          return false
        }
      }
      return true
    })
  }, [tickets, statusFilter, searchQuery])

  const openCount = tickets.filter((t) => t.status === "open").length
  const inProgressCount = tickets.filter((t) => t.status === "in-progress").length
  const resolvedCount = tickets.filter((t) => t.status === "resolved").length

  return (
    <div className="operator-ticket-management space-y-6">
      <PageHeader title="Support Ticket Management" description="Review, investigate, and resolve support requests submitted by drivers in real-time." />

      {/* Feedback Banner */}
      {loadError && (
        <div role="alert" className="rounded-xl border border-red-500/30 bg-red-500/10 p-3.5 text-xs text-red-600">
          {loadError}
        </div>
      )}
      {loading && (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-8 text-center text-xs text-[var(--muted)]">
          Loading support tickets…
        </div>
      )}
      {actionFeedback && (
        <div role="status" className="flex items-center justify-between rounded-xl border border-green-500/30 bg-green-500/10 p-3.5 text-xs text-green-600 font-medium">
          <div className="flex items-center gap-2">
            <UntitledIcon name="check-circle" size={16} />
            <span>{actionFeedback}</span>
          </div>
          <button
            type="button"
            className="hover:underline font-semibold"
            onClick={() => setActionFeedback(null)}
          >
            Dismiss
          </button>
        </div>
      )}

      {/* Counter Cards */}
      <div className="grid gap-3 sm:grid-cols-3">
        <button
          type="button"
          onClick={() => setStatusFilter("open")}
          className={`rounded-xl border p-4 text-left transition-colors ${
            statusFilter === "open"
              ? "border-blue-500 bg-blue-500/5 ring-1 ring-blue-500"
              : "border-[var(--border)] bg-[var(--card)] hover:border-[var(--primary)]"
          }`}
        >
          <div className="flex items-center justify-between text-xs font-semibold text-blue-600">
            <span>Awaiting Review (Open)</span>
            <span className="h-2 w-2 rounded-full bg-blue-500" />
          </div>
          <div className="mt-2 text-2xl font-bold text-[var(--fg)]">{openCount}</div>
        </button>

        <button
          type="button"
          onClick={() => setStatusFilter("in-progress")}
          className={`rounded-xl border p-4 text-left transition-colors ${
            statusFilter === "in-progress"
              ? "border-amber-500 bg-amber-500/5 ring-1 ring-amber-500"
              : "border-[var(--border)] bg-[var(--card)] hover:border-[var(--primary)]"
          }`}
        >
          <div className="flex items-center justify-between text-xs font-semibold text-amber-600">
            <span>In Progress</span>
            <span className="h-2 w-2 rounded-full bg-amber-500" />
          </div>
          <div className="mt-2 text-2xl font-bold text-[var(--fg)]">{inProgressCount}</div>
        </button>

        <button
          type="button"
          onClick={() => setStatusFilter("resolved")}
          className={`rounded-xl border p-4 text-left transition-colors ${
            statusFilter === "resolved"
              ? "border-green-500 bg-green-500/5 ring-1 ring-green-500"
              : "border-[var(--border)] bg-[var(--card)] hover:border-[var(--primary)]"
          }`}
        >
          <div className="flex items-center justify-between text-xs font-semibold text-green-600">
            <span>Decided / Resolved</span>
            <span className="h-2 w-2 rounded-full bg-green-500" />
          </div>
          <div className="mt-2 text-2xl font-bold text-[var(--fg)]">{resolvedCount}</div>
        </button>
      </div>

      {/* Search & Filter Bar */}
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-[var(--border)] bg-[var(--card)] p-4">
        <div className="flex flex-wrap items-center gap-2 flex-1">
          <label className="sr-only" htmlFor="operator-ticket-search">Search support tickets</label>
          <input
            id="operator-ticket-search"
            name="ticketSearch"
            autoComplete="off"
            className="sp-search-input w-full px-3 sm:w-64"
            placeholder="Search tickets by subject, driver, or ID…"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
          />

          <RoundedSelect
            aria-label="Filter ticket status"
            className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-1.5 text-xs text-[var(--fg)] outline-none focus:border-[var(--primary)]"
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value as typeof statusFilter)}
          >
            <option value="all">All Ticket Statuses</option>
            <option value="open">Open Only</option>
            <option value="in-progress">In Progress</option>
            <option value="resolved">Resolved Only</option>
            <option value="approved">Approved</option>
            <option value="rejected">Rejected</option>
          </RoundedSelect>
        </div>

        <span className="text-xs text-[var(--muted)]">
          Showing {filteredTickets.length} tickets
        </span>
      </div>

      {/* Ticket Cards List */}
      <div className="space-y-3">
        {filteredTickets.length === 0 ? (
          <div className="rounded-xl border border-dashed border-[var(--border)] p-8 text-center text-xs text-[var(--muted)]">
            No support tickets found matching the filter criteria.
          </div>
        ) : (
          filteredTickets.map((ticket) => (
            <article
              key={ticket.id}
              className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-4 transition-colors hover:border-[var(--primary)]/60"
            >
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="space-y-1">
                  <div className="flex items-center gap-2">
                    <span className="font-mono text-xs font-semibold text-[var(--primary)]">
                      #{ticket.id}
                    </span>
                    <h2 className="font-bold text-sm text-[var(--fg)]">
                      {ticket.subject}
                    </h2>
                  </div>
                  <p className="text-xs text-[var(--muted)]">
                    Submitted by <strong className="text-[var(--fg)]">{ticket.userName}</strong> ·{" "}
                    {new Date(ticket.createdAt).toLocaleString()}
                  </p>
                </div>

                <div className="flex items-center gap-2">
                  {ticket.bookingId && (
                    <span className="rounded bg-[var(--bg)] border border-[var(--border)] px-2 py-0.5 text-xs font-mono font-medium text-[var(--muted)]">
                      Booking #{ticket.bookingId}
                    </span>
                  )}
                  <span
                    className={`ticket-decision is-${ticketPresentation(ticket).decision || ticket.status}`}
                  >
                    {ticketPresentation(ticket).statusLabel}
                  </span>
                </div>
              </div>

              <p className="mt-3 text-xs text-[var(--fg)] line-clamp-2 leading-relaxed">
                  {ticketPresentation(ticket).inquiry}
              </p>

              <div className="mt-4 flex flex-wrap items-center justify-between gap-2 border-t border-[var(--border)] pt-3">
                <button
                  type="button"
                  className="btn-outline text-xs py-1 px-3"
                  onClick={() => openTicket(ticket)}
                >
                  View details
                </button>

                <div className="flex items-center gap-2">
                  {ticket.status === "open" && (
                    <button
                      type="button"
                      className="text-xs font-semibold text-amber-600 hover:bg-amber-500/10 border border-amber-500/30 rounded px-2.5 py-1 transition-colors"
                      disabled={busyTicketId !== null}
                      onClick={() => void updateTicket(ticket, "in-progress")}
                    >
                      Start Investigation
                    </button>
                  )}
                  {ticket.status !== "resolved" && (
                    <button
                      type="button"
                      className="text-xs font-semibold text-white bg-green-600 hover:bg-green-700 rounded px-3 py-1 transition-colors"
                      disabled={busyTicketId !== null}
                      onClick={() => openTicket(ticket)}
                    >
                      Review ticket
                    </button>
                  )}
                </div>
              </div>
            </article>
          ))
        )}
      </div>

      {/* MODAL: Ticket Detail & Resolution */}
      {selectedTicket && (
        <div
          role="presentation"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
          onMouseDown={(e) => e.target === e.currentTarget && setSelectedTicket(null)}
        >
          <div
            ref={dialogRef}
            role="dialog"
            aria-modal="true"
            aria-labelledby="ticket-modal-title"
            onKeyDown={handleDialogKeyDown}
            className="w-full max-w-lg max-h-[88dvh] overflow-y-auto rounded-2xl border border-[var(--border)] bg-[var(--card)] p-6 shadow-2xl animate-in text-sm text-[var(--fg)]"
          >
            <div className="flex items-center justify-between border-b border-[var(--border)] pb-3">
              <div>
                <span className="font-mono text-xs font-bold text-[var(--primary)]">
                  Ticket #{selectedTicket.id}
                </span>
                <h3 id="ticket-modal-title" className="text-base font-bold text-[var(--fg)]">
                  {selectedTicket.subject}
                </h3>
              </div>
              <button
                ref={closeRef}
                type="button"
                onClick={() => setSelectedTicket(null)}
                className="h-11 w-11 rounded-full border border-[var(--border)] bg-[var(--bg)] flex items-center justify-center text-[var(--muted)] hover:text-[var(--fg)]"
                aria-label="Close ticket details"
              >
                <UntitledIcon name="x" size={16} />
              </button>
            </div>

            <div className="mt-4 space-y-3">
              <div className="rounded-xl border border-[var(--border)] bg-[var(--bg)] p-3 text-xs space-y-1.5">
                <div className="flex justify-between">
                  <span className="text-[var(--muted)]">Driver Name:</span>
                  <strong className="text-[var(--fg)]">{selectedTicket.userName}</strong>
                </div>
                <div className="flex justify-between">
                  <span className="text-[var(--muted)]">Driver User ID:</span>
                  <span className="font-mono text-[var(--fg)]">{selectedTicket.userId}</span>
                </div>
                {selectedTicket.bookingId && (
                  <div className="flex justify-between">
                    <span className="text-[var(--muted)]">Associated Booking:</span>
                    <span className="font-mono font-bold text-[var(--primary)]">
                      #{selectedTicket.bookingId}
                    </span>
                  </div>
                )}
                <div className="flex justify-between">
                  <span className="text-[var(--muted)]">Submitted At:</span>
                  <span>{new Date(selectedTicket.createdAt).toLocaleString()}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-[var(--muted)]">Current Status:</span>
                  <span className="font-bold uppercase text-[var(--primary)]">
                    {ticketPresentation(selectedTicket).statusLabel}
                  </span>
                </div>
              </div>

              <div>
                <span className="text-xs font-semibold text-[var(--muted)] block mb-1">
                  Driver Inquiry Message:
                </span>
                <div className="rounded-xl border border-[var(--border)] bg-[var(--bg)] p-3 text-xs whitespace-pre-wrap leading-relaxed">
                  {ticketPresentation(selectedTicket).inquiry}
                </div>
              </div>

              {ticketPresentation(selectedTicket).note && <div className="operator-ticket-existing-response"><strong>Operator response</strong><p>{ticketPresentation(selectedTicket).note}</p></div>}

              {selectedTicket.status !== "resolved" && (
                <div className="operator-ticket-decision-form">
                  <fieldset><legend>Decision</legend><div className="operator-ticket-decision-options">
                    <label><input type="radio" name="ticket-decision" checked={decision === "approved"} onChange={() => setDecision("approved")} />Approve</label>
                    <label><input type="radio" name="ticket-decision" checked={decision === "rejected"} onChange={() => setDecision("rejected")} />Reject</label>
                  </div></fieldset>
                  <label htmlFor="operator-ticket-reason">Reason for {decision === "approved" ? "approval" : "rejection"}</label>
                  <textarea id="operator-ticket-reason" name="decisionReason" autoComplete="off" className="input" rows={2} maxLength={900} placeholder="Explain why this ticket is approved or rejected…" value={decisionReason} onChange={event => setDecisionReason(event.target.value)} />
                  <label htmlFor="operator-ticket-detail">{decision === "approved" ? "What was fixed" : "Outstanding issue"}</label>
                  <textarea id="operator-ticket-detail" name="decisionDetail" autoComplete="off" className="input" rows={2} maxLength={900} placeholder={decision === "approved" ? "Describe the completed fix…" : "Explain what remains unresolved or prevents approval…"} value={decisionDetail} onChange={event => setDecisionDetail(event.target.value)} />
                </div>
              )}
            </div>

            <div className="mt-5 flex justify-end gap-2 border-t border-[var(--border)] pt-3">
              <button
                type="button"
                className="btn-outline text-xs py-1.5 px-3"
                onClick={() => setSelectedTicket(null)}
              >
                Close
              </button>

              {selectedTicket.status === "open" && (
                <button
                  type="button"
                  className="btn-outline text-xs py-1.5 px-3 text-amber-600 border-amber-500/30 hover:bg-amber-500/10"
                  disabled={busyTicketId !== null}
                  onClick={() => void updateTicket(selectedTicket, "in-progress")}
                >
                  Mark In Progress
                </button>
              )}

              {selectedTicket.status !== "resolved" && (
                <button
                  type="button"
                  className="btn-primary"
                  disabled={busyTicketId !== null || !decisionReason.trim() || !decisionDetail.trim()}
                  onClick={() => void updateTicket(selectedTicket, "resolved", formatTicketDecision(decision, decisionReason, decisionDetail), decision)}
                >
                  {busyTicketId === selectedTicket.id ? "Saving…" : decision === "approved" ? "Approve ticket" : "Reject ticket"}
                </button>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
