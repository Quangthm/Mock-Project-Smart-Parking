import type { SupportTicket } from "./types"

const operatorNoteMarker = "\n\n[Operator resolution note]: "

export function formatTicketDecision(decision: "approved" | "rejected", reason: string, detail: string) {
  return `Decision: ${decision === "approved" ? "Approved" : "Rejected"}\nReason: ${reason.trim()}\n${decision === "approved" ? "Fixed" : "Outstanding issue"}: ${detail.trim()}`
}

export function ticketPresentation(ticket: SupportTicket) {
  const markerIndex = ticket.message.lastIndexOf(operatorNoteMarker)
  const inquiry = markerIndex >= 0 ? ticket.message.slice(0, markerIndex) : ticket.message
  const note = ticket.resolutionNote || (markerIndex >= 0 ? ticket.message.slice(markerIndex + operatorNoteMarker.length) : "")
  const decision = ticket.decision || (note.startsWith("Decision: Approved\n") ? "approved" : note.startsWith("Decision: Rejected\n") ? "rejected" : null)
  const reasonStart = note.indexOf("\nReason: ")
  const detailMarker = decision === "approved" ? "\nFixed: " : "\nOutstanding issue: "
  const detailStart = note.lastIndexOf(detailMarker)
  const reason = reasonStart >= 0 && detailStart > reasonStart ? note.slice(reasonStart + 9, detailStart).trim() : ""
  const detail = detailStart >= 0 ? note.slice(detailStart + detailMarker.length).trim() : ""
  const statusLabel = decision === "approved" ? "Approved" : decision === "rejected" ? "Rejected"
    : ticket.status === "in-progress" ? "In progress" : ticket.status === "resolved" ? "Resolved" : "Open"

  return { inquiry, note, decision, reason, detail, statusLabel }
}
