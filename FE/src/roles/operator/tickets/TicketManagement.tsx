import { useEffect, useState } from "react"

import { useApp } from "../../../context/AppContext"

import type { SupportTicket } from "../../../lib/types"

import { operatorData as store } from "../data/data"

export function TicketManagement() {
  const { user } = useApp()

  const [tickets, setTickets] = useState<SupportTicket[]>(() =>
    store.getTickets().sort((a, b) => b.createdAt.localeCompare(a.createdAt)),
  )

  useEffect(() => {
    const refresh = () =>
      setTickets(
        store
          .getTickets()
          .sort((a, b) => b.createdAt.localeCompare(a.createdAt)),
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
  ) {
    const updated = { ...ticket, status }

    store.saveTicket(updated)

    if (status === "resolved" && ticket.status !== "resolved") {
      store.notifyUser(
        ticket.userId,
        "TICKET_RESOLVED",
        "Ticket resolved",
        "Your ticket has been resolved by the Operator.",
        ticket.id,
      )
    }

    store.addAuditLog({
      userId: user?.id ?? "",
      userName: user?.name ?? "",
      userRole: user?.role ?? "operator",
      action: "SUPPORT_TICKET_UPDATED",
      details: `Set ticket ${ticket.id} to ${status}`,
    })

    setTickets((current) =>
      current.map((item) => (item.id === ticket.id ? updated : item)),
    )
  }

  const openCount = tickets.filter((ticket) => ticket.status === "open").length

  const activeCount = tickets.filter(
    (ticket) => ticket.status === "in-progress",
  ).length

  const resolvedCount = tickets.filter(
    (ticket) => ticket.status === "resolved",
  ).length

  return (
    <section className="mx-auto w-full max-w-6xl space-y-5">
      <header>
        <p className="text-sm font-medium text-[var(--primary)]">
          Driver support
        </p>
        <h1 className="mt-1 text-2xl font-bold text-[var(--fg)]">
          Support Tickets
        </h1>
        <p className="mt-2 text-sm text-[var(--muted)]">
          Review driver requests and update each ticket as you work through it.
        </p>
      </header>

      <div className="grid gap-3 sm:grid-cols-3">
        <TicketCount label="Open" count={openCount} />
        <TicketCount label="In progress" count={activeCount} />
        <TicketCount label="Resolved" count={resolvedCount} />
      </div>

      <div className="space-y-3">
        {tickets.map((ticket) => (
          <article
            key={ticket.id}
            className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-4 sm:p-5"
          >
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <h2 className="font-semibold text-[var(--fg)]">
                  {ticket.subject}
                </h2>
                <p className="mt-1 text-xs text-[var(--muted)]">
                  {ticket.userName} ·{" "}
                  {new Date(ticket.createdAt).toLocaleString("vi-VN")}
                </p>
              </div>
              <span
                className={`rounded-full px-2.5 py-1 text-xs font-semibold ${
                  ticket.status === "resolved"
                    ? "bg-green-500/10 text-green-600"
                    : ticket.status === "in-progress"
                      ? "bg-amber-500/10 text-amber-600"
                      : "bg-blue-500/10 text-blue-600"
                }`}
              >
                {ticket.status === "in-progress"
                  ? "In progress"
                  : ticket.status === "open"
                    ? "Open"
                    : "Resolved"}
              </span>
            </div>
            <p className="mt-4 whitespace-pre-wrap text-sm leading-6 text-[var(--fg)]">
              {ticket.message}
            </p>
            <div className="mt-4 flex flex-wrap gap-2">
              {ticket.status !== "in-progress" && (
                <button
                  type="button"
                  className="btn-outline"
                  style={{ fontSize: "0.8rem", padding: "0.4rem 0.75rem" }}
                  onClick={() => updateTicket(ticket, "in-progress")}
                >
                  Mark In Progress
                </button>
              )}
              {ticket.status !== "resolved" && (
                <button
                  type="button"
                  className="btn-primary"
                  style={{ fontSize: "0.8rem", padding: "0.4rem 0.75rem" }}
                  onClick={() => updateTicket(ticket, "resolved")}
                >
                  Resolve Ticket
                </button>
              )}
              {ticket.status !== "open" && (
                <button
                  type="button"
                  className="btn-outline"
                  style={{ fontSize: "0.8rem", padding: "0.4rem 0.75rem" }}
                  onClick={() => updateTicket(ticket, "open")}
                >
                  Reopen
                </button>
              )}
            </div>
          </article>
        ))}
        {!tickets.length && (
          <div className="rounded-xl border border-dashed border-[var(--border)] p-10 text-center text-sm text-[var(--muted)]">
            No driver tickets yet.
          </div>
        )}
      </div>
    </section>
  )
}

function TicketCount({ label, count }: { label: string count: number }) {
  return (
    <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-4">
      <p className="text-sm text-[var(--muted)]">{label}</p>
      <p className="mt-1 text-xl font-bold text-[var(--fg)]">{count}</p>
    </div>
  )
}
