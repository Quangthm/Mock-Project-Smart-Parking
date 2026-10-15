import { useEffect, useState, useMemo } from "react"
import { useApp } from "../../../context/AppContext"
import { operatorData as store } from "../data/data"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import type { SupportTicket, Booking } from "../../../lib/types"

export function TicketManagement() {
  const { user } = useApp()

  const [tickets, setTickets] = useState<SupportTicket[]>(() =>
    store.getTickets().sort((a, b) => b.createdAt.localeCompare(a.createdAt)),
  )

  const [searchQuery, setSearchQuery] = useState("")
  const [statusFilter, setStatusFilter] = useState<"all" | "open" | "in-progress" | "resolved">("all")
  const [selectedTicket, setSelectedTicket] = useState<SupportTicket | null>(null)
  const [resolutionNote, setResolutionNote] = useState("")
  const [actionFeedback, setActionFeedback] = useState<string | null>(null)

  useEffect(() => {
    const refresh = () =>
      setTickets(
        store.getTickets().sort((a, b) => b.createdAt.localeCompare(a.createdAt)),
      )

    window.addEventListener("sp-data-change", refresh)
    window.addEventListener("storage", refresh)

    return () => {
      window.removeEventListener("sp-data-change", refresh)
      window.removeEventListener("storage", refresh)
    }
  }, [])

  function updateTicket(
    ticket: SupportTicket,
    status: SupportTicket["status"],
    note?: string,
  ) {
    const updated = {
      ...ticket,
      status,
      message: note ? `${ticket.message}\n\n[Operator Resolution Note (${new Date().toLocaleString()})]: ${note}` : ticket.message,
    }

    store.saveTicket(updated)

    if (status === "resolved" && ticket.status !== "resolved") {
      store.notifyUser(
        ticket.userId,
        "TICKET_RESOLVED",
        "Ticket Resolved by Operator",
        `Your support request #${ticket.id} (${ticket.subject}) has been addressed by the facility Operator.`,
        ticket.id,
      )
    }

    store.addAuditLog({
      userId: user?.id ?? "op",
      userName: user?.name ?? "Operator",
      userRole: user?.role ?? "operator",
      action: "SUPPORT_TICKET_UPDATED",
      details: `Updated ticket #${ticket.id} to status ${status.toUpperCase()}`,
    })

    setTickets((current) =>
      current.map((item) => (item.id === ticket.id ? updated : item)),
    )

    if (selectedTicket?.id === ticket.id) {
      setSelectedTicket(updated)
    }

    setActionFeedback(`Ticket #${ticket.id} successfully updated to ${status.toUpperCase()}.`)
    setResolutionNote("")
  }

  // Filtered tickets
  const filteredTickets = useMemo(() => {
    return tickets.filter((t) => {
      if (statusFilter !== "all" && t.status !== statusFilter) return false
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
    <div className="space-y-6">
      {/* Title */}
      <div>
        <h1 className="text-2xl font-bold tracking-tight text-[var(--fg)]">
          Support Ticket Management
        </h1>
        <p className="mt-1 text-sm text-[var(--muted)]">
          Review, investigate, and resolve support requests submitted by drivers in real-time.
        </p>
      </div>

      {/* Feedback Banner */}
      {actionFeedback && (
        <div className="flex items-center justify-between rounded-xl border border-green-500/30 bg-green-500/10 p-3.5 text-xs text-green-600 font-medium">
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
          className={`rounded-xl border p-4 text-left transition ${
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
          className={`rounded-xl border p-4 text-left transition ${
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
          className={`rounded-xl border p-4 text-left transition ${
            statusFilter === "resolved"
              ? "border-green-500 bg-green-500/5 ring-1 ring-green-500"
              : "border-[var(--border)] bg-[var(--card)] hover:border-[var(--primary)]"
          }`}
        >
          <div className="flex items-center justify-between text-xs font-semibold text-green-600">
            <span>Resolved</span>
            <span className="h-2 w-2 rounded-full bg-green-500" />
          </div>
          <div className="mt-2 text-2xl font-bold text-[var(--fg)]">{resolvedCount}</div>
        </button>
      </div>

      {/* Search & Filter Bar */}
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-[var(--border)] bg-[var(--card)] p-4">
        <div className="flex flex-wrap items-center gap-2 flex-1">
          <input
            className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-1.5 text-xs text-[var(--fg)] outline-none focus:border-[var(--primary)] w-full sm:w-64"
            placeholder="Search tickets by subject, driver, or ID..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
          />

          <select
            className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-1.5 text-xs text-[var(--fg)] outline-none focus:border-[var(--primary)]"
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value as typeof statusFilter)}
          >
            <option value="all">All Ticket Statuses</option>
            <option value="open">Open Only</option>
            <option value="in-progress">In Progress</option>
            <option value="resolved">Resolved Only</option>
          </select>
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
              className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-4 transition hover:border-[var(--primary)]/60"
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
                    <span className="rounded bg-[var(--bg)] border border-[var(--border)] px-2 py-0.5 text-[10px] font-mono font-medium text-[var(--muted)]">
                      Booking #{ticket.bookingId}
                    </span>
                  )}
                  <span
                    className={`rounded-full px-2.5 py-0.5 text-[10px] font-bold uppercase ${
                      ticket.status === "resolved"
                        ? "bg-green-500/15 text-green-600"
                        : ticket.status === "in-progress"
                          ? "bg-amber-500/15 text-amber-600"
                          : "bg-blue-500/15 text-blue-600"
                    }`}
                  >
                    {ticket.status}
                  </span>
                </div>
              </div>

              <p className="mt-3 text-xs text-[var(--fg)] line-clamp-2 leading-relaxed">
                {ticket.message}
              </p>

              <div className="mt-4 flex flex-wrap items-center justify-between gap-2 border-t border-[var(--border)] pt-3">
                <button
                  type="button"
                  className="btn-outline text-xs py-1 px-3"
                  onClick={() => setSelectedTicket(ticket)}
                >
                  View Details & History
                </button>

                <div className="flex items-center gap-2">
                  {ticket.status === "open" && (
                    <button
                      type="button"
                      className="text-xs font-semibold text-amber-600 hover:bg-amber-500/10 border border-amber-500/30 rounded px-2.5 py-1 transition"
                      onClick={() => updateTicket(ticket, "in-progress")}
                    >
                      Start Investigation
                    </button>
                  )}
                  {ticket.status !== "resolved" && (
                    <button
                      type="button"
                      className="text-xs font-semibold text-white bg-green-600 hover:bg-green-700 rounded px-3 py-1 transition"
                      onClick={() => setSelectedTicket(ticket)}
                    >
                      Resolve Ticket
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
            role="dialog"
            aria-modal="true"
            aria-labelledby="ticket-modal-title"
            className="w-full max-w-lg rounded-2xl border border-[var(--border)] bg-[var(--card)] p-6 shadow-2xl animate-in text-sm text-[var(--fg)]"
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
                type="button"
                onClick={() => setSelectedTicket(null)}
                className="h-8 w-8 rounded-full border border-[var(--border)] bg-[var(--bg)] flex items-center justify-center text-[var(--muted)] hover:text-[var(--fg)]"
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
                    {selectedTicket.status}
                  </span>
                </div>
              </div>

              <div>
                <span className="text-xs font-semibold text-[var(--muted)] block mb-1">
                  Driver Inquiry Message:
                </span>
                <div className="rounded-xl border border-[var(--border)] bg-[var(--bg)] p-3 text-xs whitespace-pre-wrap leading-relaxed">
                  {selectedTicket.message}
                </div>
              </div>

              {selectedTicket.status !== "resolved" && (
                <div>
                  <label className="text-xs font-semibold text-[var(--muted)] block mb-1">
                    Add Operator Resolution Note:
                  </label>
                  <textarea
                    className="w-full rounded-xl border border-[var(--border)] bg-[var(--bg)] p-2.5 text-xs text-[var(--fg)] outline-none focus:border-[var(--primary)]"
                    rows={3}
                    placeholder="Describe resolution steps taken (e.g. Barrier restarted, slot reassigned, fee confirmed)..."
                    value={resolutionNote}
                    onChange={(e) => setResolutionNote(e.target.value)}
                  />
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
                  onClick={() => updateTicket(selectedTicket, "in-progress", resolutionNote)}
                >
                  Mark In Progress
                </button>
              )}

              {selectedTicket.status !== "resolved" && (
                <button
                  type="button"
                  className="btn-primary text-xs py-1.5 px-4 bg-green-600 hover:bg-green-700"
                  onClick={() => updateTicket(selectedTicket, "resolved", resolutionNote)}
                >
                  Resolve & Notify Driver
                </button>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
