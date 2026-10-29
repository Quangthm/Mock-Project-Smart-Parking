import type { SupportTicket } from "./types"

const operatorNoteMarker = "\n\n[Operator resolution note]: "

const DECISION_CONFIG: Record<"approved" | "rejected", { label: string; detailPrefix: string; detailMarker: string }> = {
  approved: { label: "Approved", detailPrefix: "Fixed", detailMarker: "\nFixed: " },
  rejected: { label: "Rejected", detailPrefix: "Outstanding issue", detailMarker: "\nOutstanding issue: " },
}

const TICKET_STATUS_LABEL_MAP: Record<string, string> = {
  "in-progress": "In progress",
  resolved: "Resolved",
  open: "Open",
}

export function formatTicketDecision(decision: "approved" | "rejected", reason: string, detail: string) {
  const config = DECISION_CONFIG[decision] ?? DECISION_CONFIG.approved
  return `Decision: ${config.label}\nReason: ${reason.trim()}\n${config.detailPrefix}: ${detail.trim()}`
}

export function ticketPresentation(ticket: SupportTicket) {
  const markerIndex = ticket.message.lastIndexOf(operatorNoteMarker)
  const inquiry = markerIndex >= 0 ? ticket.message.slice(0, markerIndex) : ticket.message
  const note = ticket.resolutionNote || (markerIndex >= 0 ? ticket.message.slice(markerIndex + operatorNoteMarker.length) : "")
  const decision = ticket.decision || (note.startsWith("Decision: Approved\n") ? "approved" : note.startsWith("Decision: Rejected\n") ? "rejected" : null)
  const reasonStart = note.indexOf("\nReason: ")
  const detailMarker = decision ? DECISION_CONFIG[decision]?.detailMarker : "\nOutstanding issue: "
  const detailStart = note.lastIndexOf(detailMarker)
  const reason = reasonStart >= 0 && detailStart > reasonStart ? note.slice(reasonStart + 9, detailStart).trim() : ""
  const detail = detailStart >= 0 ? note.slice(detailStart + detailMarker.length).trim() : ""
  const statusLabel = (decision && DECISION_CONFIG[decision]?.label) || TICKET_STATUS_LABEL_MAP[ticket.status] || "Open"

  return { inquiry, note, decision, reason, detail, statusLabel }
}
