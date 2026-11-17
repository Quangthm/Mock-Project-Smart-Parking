import type { PolicyDraftValues } from "./adminPolicyApi"

export const OVERRIDABLE_POLICY_KEYS: readonly string[] = [
  "RESERVATION_MIN_DURATION",
  "RESERVATION_MAX_DURATION",
  "MAX_ADVANCE_BOOKING",
  "INTER_BOOKING_BUFFER",
  "PAYMENT_HOLD_DURATION",
  "RESERVATION_PROTECTION_WINDOW",
  "ALLOCATION_LEAD_TIME",
  "FACILITY_FAILURE_REFUND_LEAD_TIME",
  "OCCUPANCY_VALIDITY_TIMESPAN",
  "ARRIVAL_LATE_TOLERANCE",
]

export const POLICY_UNIT_DIVISOR: Record<string, number> = {
  RESERVATION_MIN_DURATION: 1,
  RESERVATION_MAX_DURATION: 60,
  MAX_ADVANCE_BOOKING: 1440,
  INTER_BOOKING_BUFFER: 1,
  PAYMENT_HOLD_DURATION: 1,
  RESERVATION_PROTECTION_WINDOW: 60,
  ALLOCATION_LEAD_TIME: 1,
  FACILITY_FAILURE_REFUND_LEAD_TIME: 1,
  OCCUPANCY_VALIDITY_TIMESPAN: 60,
  ARRIVAL_LATE_TOLERANCE: 1,
  RAPID_CANCELLATION_COUNT: 1,
  RAPID_CANCELLATION_WINDOW: 1,
  RAPID_CANCELLATION_RESTRICTION_DURATION: 1,
  DAILY_CANCELLATION_LIMIT: 1,
}

export interface ValidationIssue {
  property: string
  message: string
}

const INTEGER_MESSAGE = "Value must be an integer."

// Technical representability ceilings, mirroring the backend PolicyDraftValidationRules: an hour
// column stores floor(max / 60) hours and a day column stores floor(max / 1440) days so the
// converted minute value (value * unit) never overflows a signed 32-bit integer.
export const MAX_POLICY_HOUR_VALUE = Math.floor(2147483647 / 60)
export const MAX_POLICY_DAY_VALUE = Math.floor(2147483647 / 1440)

const POSITIVE_MINIMUM_KEYS: readonly string[] = [
  "RESERVATION_MIN_DURATION",
  "PAYMENT_HOLD_DURATION",
]

function isFiniteInteger(value: number): boolean {
  return Number.isFinite(value) && Number.isInteger(value)
}

export function isOverridablePolicyKey(key: string): boolean {
  return OVERRIDABLE_POLICY_KEYS.includes(key)
}

export function validateOverrideBound(
  key: string,
  bound: { minimum: number; maximum: number },
): ValidationIssue[] {
  const property = `overrideBounds.${key}`

  if (!isOverridablePolicyKey(key)) {
    return [{ property, message: "Only overridable policy keys may define override bounds." }]
  }

  const { minimum, maximum } = bound
  if (!isFiniteInteger(minimum) || !isFiniteInteger(maximum)) {
    return [{ property, message: "Bound values must be integers." }]
  }

  const issues: ValidationIssue[] = []

  if (POSITIVE_MINIMUM_KEYS.includes(key) && minimum < 1) {
    issues.push({ property, message: "Bound minimum must be at least 1." })
  } else if (minimum < 0) {
    issues.push({ property, message: "Bound minimum must be greater than or equal to 0." })
  }

  if (maximum < minimum) {
    issues.push({ property, message: "Bound maximum must be greater than or equal to the minimum." })
  }

  const divisor = POLICY_UNIT_DIVISOR[key] ?? 1
  const minimumFloor = (POSITIVE_MINIMUM_KEYS.includes(key) || key === "RESERVATION_MAX_DURATION") ? 1 : 0
  const requiredMinimum = Math.max(minimumFloor, Math.ceil(minimum / divisor))
  if (Math.floor(maximum / divisor) < requiredMinimum) {
    issues.push({
      property,
      message: "Bound range cannot be represented with the policy's integer storage unit.",
    })
  }

  if (key === "ARRIVAL_LATE_TOLERANCE" && (minimum < 15 || maximum > 30)) {
    issues.push({
      property,
      message: "Arrival late tolerance bounds must stay within 15 to 30 minutes.",
    })
  }

  return issues
}

export function validateDraftValues(values: PolicyDraftValues): ValidationIssue[] {
  const issues: ValidationIssue[] = []

  const requirePositive = (property: string, value: number, message: string) => {
    if (!isFiniteInteger(value)) issues.push({ property, message: INTEGER_MESSAGE })
    else if (value <= 0) issues.push({ property, message })
  }

  const requireNonNegative = (property: string, value: number, message: string) => {
    if (!isFiniteInteger(value)) issues.push({ property, message: INTEGER_MESSAGE })
    else if (value < 0) issues.push({ property, message })
  }

  const requireAtMost = (property: string, value: number, max: number, message: string) => {
    if (isFiniteInteger(value) && value > max) issues.push({ property, message })
  }

  requirePositive(
    "minReservationDurationMinutes",
    values.minReservationDurationMinutes,
    "Minimum reservation duration must be greater than 0.",
  )
  requirePositive(
    "maxReservationDurationHours",
    values.maxReservationDurationHours,
    "Maximum reservation duration must be greater than 0.",
  )
  if (
    isFiniteInteger(values.maxReservationDurationHours) &&
    isFiniteInteger(values.minReservationDurationMinutes) &&
    values.maxReservationDurationHours * 60 < values.minReservationDurationMinutes
  ) {
    issues.push({
      property: "maxReservationDurationHours",
      message: "Maximum reservation duration must be at least the minimum duration.",
    })
  }

  requirePositive(
    "paymentHoldDurationMinutes",
    values.paymentHoldDurationMinutes,
    "Payment hold duration must be greater than 0.",
  )
  requireNonNegative(
    "maxAdvanceBookingDays",
    values.maxAdvanceBookingDays,
    "Maximum advance booking days must be greater than or equal to 0.",
  )
  requireNonNegative(
    "interBookingBufferMinutes",
    values.interBookingBufferMinutes,
    "Inter-booking buffer must be greater than or equal to 0.",
  )
  requireNonNegative(
    "reservationProtectionWindowHours",
    values.reservationProtectionWindowHours,
    "Reservation protection window must be greater than or equal to 0.",
  )
  requireNonNegative(
    "allocationLeadTimeMinutes",
    values.allocationLeadTimeMinutes,
    "Allocation lead time must be greater than or equal to 0.",
  )
  requireNonNegative(
    "facilityFailureRefundLeadMinutes",
    values.facilityFailureRefundLeadMinutes,
    "Facility failure refund lead time must be greater than or equal to 0.",
  )
  requireNonNegative(
    "occupancyValidityTimespanHours",
    values.occupancyValidityTimespanHours,
    "Occupancy validity timespan must be greater than or equal to 0.",
  )

  // Technical upper ceilings only; business bounds are Admin-configured and validated at publish.
  requireAtMost(
    "maxReservationDurationHours",
    values.maxReservationDurationHours,
    MAX_POLICY_HOUR_VALUE,
    `Maximum reservation duration cannot exceed ${MAX_POLICY_HOUR_VALUE} hours.`,
  )
  requireAtMost(
    "reservationProtectionWindowHours",
    values.reservationProtectionWindowHours,
    MAX_POLICY_HOUR_VALUE,
    `Reservation protection window cannot exceed ${MAX_POLICY_HOUR_VALUE} hours.`,
  )
  requireAtMost(
    "occupancyValidityTimespanHours",
    values.occupancyValidityTimespanHours,
    MAX_POLICY_HOUR_VALUE,
    `Occupancy validity timespan cannot exceed ${MAX_POLICY_HOUR_VALUE} hours.`,
  )
  requireAtMost(
    "maxAdvanceBookingDays",
    values.maxAdvanceBookingDays,
    MAX_POLICY_DAY_VALUE,
    `Maximum advance booking days cannot exceed ${MAX_POLICY_DAY_VALUE} days.`,
  )

  if (!isFiniteInteger(values.arrivalLateToleranceMinutes)) {
    issues.push({ property: "arrivalLateToleranceMinutes", message: INTEGER_MESSAGE })
  } else if (values.arrivalLateToleranceMinutes < 15 || values.arrivalLateToleranceMinutes > 30) {
    issues.push({
      property: "arrivalLateToleranceMinutes",
      message: "Arrival late tolerance must be between 15 and 30 minutes.",
    })
  }

  requirePositive(
    "rapidCancellationCount",
    values.rapidCancellationCount,
    "Rapid cancellation count must be greater than 0.",
  )
  requirePositive(
    "rapidCancellationWindowMinutes",
    values.rapidCancellationWindowMinutes,
    "Rapid cancellation window must be greater than 0.",
  )
  requirePositive(
    "rapidCancellationRestrictionMinutes",
    values.rapidCancellationRestrictionMinutes,
    "Rapid cancellation restriction duration must be greater than 0.",
  )
  requirePositive(
    "dailyCancellationLimit",
    values.dailyCancellationLimit,
    "Daily cancellation limit must be greater than 0.",
  )

  if (values.cancellationTimezone !== "Asia/Ho_Chi_Minh") {
    issues.push({
      property: "cancellationTimezone",
      message: "Only the Asia/Ho_Chi_Minh timezone is supported.",
    })
  }

  if (values.overrideBounds) {
    for (const [key, bound] of Object.entries(values.overrideBounds)) {
      issues.push(...validateOverrideBound(key, bound))
    }
  }

  return issues
}

export function hasBlockingIssues(issues: ValidationIssue[]): boolean {
  return issues.length > 0
}
