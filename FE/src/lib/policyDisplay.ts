/**
 * Canonical display metadata, groups and labels for Platform Policies.
 * Shared across AdminPolicyConsole, PolicyDraftModal, and EffectivePolicyView.
 */

export interface PolicyGroupDefinition {
  title: string
  hint?: string
  keys: string[]
}

export const CANONICAL_POLICY_GROUPS: readonly PolicyGroupDefinition[] = [
  {
    title: "Duration & Booking",
    keys: [
      "RESERVATION_MIN_DURATION",
      "RESERVATION_MAX_DURATION",
      "MAX_ADVANCE_BOOKING",
      "INTER_BOOKING_BUFFER",
    ],
  },
  {
    title: "Payment & Protection",
    keys: [
      "PAYMENT_HOLD_DURATION",
      "RESERVATION_PROTECTION_WINDOW",
      "OCCUPANCY_VALIDITY_TIMESPAN",
    ],
  },
  {
    title: "Allocation & Refunds",
    keys: ["ALLOCATION_LEAD_TIME", "FACILITY_FAILURE_REFUND_LEAD_TIME"],
  },
  {
    title: "Arrival Tolerance",
    keys: ["ARRIVAL_LATE_TOLERANCE"],
  },
  {
    title: "Anti-Abuse (platform-only)",
    hint: "Platform-only. Site owners cannot override these.",
    keys: [
      "RAPID_CANCELLATION_COUNT",
      "RAPID_CANCELLATION_WINDOW",
      "RAPID_CANCELLATION_RESTRICTION_DURATION",
      "DAILY_CANCELLATION_LIMIT",
    ],
  },
]

export const CANONICAL_POLICY_LABELS: Record<string, string> = {
  RESERVATION_MIN_DURATION: "Minimum reservation duration",
  RESERVATION_MAX_DURATION: "Maximum reservation duration",
  MAX_ADVANCE_BOOKING: "Maximum advance booking horizon",
  INTER_BOOKING_BUFFER: "Inter-booking buffer",
  PAYMENT_HOLD_DURATION: "Payment hold duration",
  RESERVATION_PROTECTION_WINDOW: "Reservation protection window",
  OCCUPANCY_VALIDITY_TIMESPAN: "Occupancy validity timespan",
  ALLOCATION_LEAD_TIME: "Allocation lead time",
  FACILITY_FAILURE_REFUND_LEAD_TIME: "Facility failure refund lead time",
  ARRIVAL_LATE_TOLERANCE: "Arrival late tolerance",
  RAPID_CANCELLATION_COUNT: "Rapid cancellation threshold count",
  RAPID_CANCELLATION_WINDOW: "Rapid cancellation window",
  RAPID_CANCELLATION_RESTRICTION_DURATION: "Rapid cancellation restriction duration",
  DAILY_CANCELLATION_LIMIT: "Daily cancellation limit",
}

export const CANONICAL_UNIT_SHORT: Record<string, string> = {
  minutes: "min",
  hours: "h",
  days: "d",
  submissions: "submissions",
}

export function formatPolicyDateTime(value: string | null | undefined): string {
  if (!value) return "—"
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return "—"
  return date.toLocaleString("en-GB", { dateStyle: "medium", timeStyle: "short" })
}
