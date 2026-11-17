import { useMemo, useState } from "react"

import { Badge } from "../../../components/ui/Badge"
import { Button } from "../../../components/ui/Button"
import { Modal } from "../../../components/ui/Modal"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import {
  createDraft,
  updateDraft,
  type PolicyBoundDto,
  type PolicyDraftValues,
  type PolicyRevisionListItem,
} from "../../../lib/adminPolicyApi"
import { CANONICAL_POLICY_LABELS } from "../../../lib/policyDisplay"
import {
  OVERRIDABLE_POLICY_KEYS,
  hasBlockingIssues,
  validateDraftValues,
  type ValidationIssue,
} from "../../../lib/adminPolicyValidation"
import { AuthApiError } from "../../../lib/authApi"

const TIMEZONE = "Asia/Ho_Chi_Minh"

type NumericValueKey =
  | "paymentHoldDurationMinutes"
  | "minReservationDurationMinutes"
  | "maxReservationDurationHours"
  | "maxAdvanceBookingDays"
  | "interBookingBufferMinutes"
  | "reservationProtectionWindowHours"
  | "allocationLeadTimeMinutes"
  | "facilityFailureRefundLeadMinutes"
  | "occupancyValidityTimespanHours"
  | "arrivalLateToleranceMinutes"
  | "rapidCancellationCount"
  | "rapidCancellationWindowMinutes"
  | "rapidCancellationRestrictionMinutes"
  | "dailyCancellationLimit"

const FIELD_GROUPS: Array<{
  title: string
  note?: string
  fields: Array<{ key: NumericValueKey; label: string; unit: string }>
}> = [
  {
    title: "Duration & Booking",
    fields: [
      { key: "minReservationDurationMinutes", label: "Minimum reservation duration", unit: "minutes" },
      { key: "maxReservationDurationHours", label: "Maximum reservation duration", unit: "hours" },
      { key: "maxAdvanceBookingDays", label: "Maximum advance booking horizon", unit: "days" },
      { key: "interBookingBufferMinutes", label: "Inter-booking buffer", unit: "minutes" },
    ],
  },
  {
    title: "Payment & Protection",
    fields: [
      { key: "paymentHoldDurationMinutes", label: "Payment hold duration", unit: "minutes" },
      { key: "reservationProtectionWindowHours", label: "Reservation protection window", unit: "hours" },
      { key: "occupancyValidityTimespanHours", label: "Occupancy validity timespan", unit: "hours" },
    ],
  },
  {
    title: "Allocation & Refunds",
    fields: [
      { key: "allocationLeadTimeMinutes", label: "Allocation lead time", unit: "minutes" },
      { key: "facilityFailureRefundLeadMinutes", label: "Facility failure refund lead time", unit: "minutes" },
    ],
  },
  {
    title: "Arrival Tolerance",
    note: "Enablement is decided per site; platform drafts do not toggle it.",
    fields: [{ key: "arrivalLateToleranceMinutes", label: "Arrival late tolerance", unit: "minutes" }],
  },
  {
    title: "Anti-Abuse (platform-only)",
    note: "Site owners cannot override these values.",
    fields: [
      { key: "rapidCancellationCount", label: "Rapid cancellation threshold count", unit: "submissions" },
      { key: "rapidCancellationWindowMinutes", label: "Rapid cancellation window", unit: "minutes" },
      { key: "rapidCancellationRestrictionMinutes", label: "Rapid cancellation restriction duration", unit: "minutes" },
      { key: "dailyCancellationLimit", label: "Daily cancellation limit", unit: "submissions" },
    ],
  },
]

const DEFAULTS: Record<NumericValueKey, number> = {
  paymentHoldDurationMinutes: 5,
  minReservationDurationMinutes: 15,
  maxReservationDurationHours: 24,
  maxAdvanceBookingDays: 7,
  interBookingBufferMinutes: 0,
  reservationProtectionWindowHours: 4,
  allocationLeadTimeMinutes: 60,
  facilityFailureRefundLeadMinutes: 15,
  occupancyValidityTimespanHours: 4,
  arrivalLateToleranceMinutes: 15,
  rapidCancellationCount: 2,
  rapidCancellationWindowMinutes: 15,
  rapidCancellationRestrictionMinutes: 180,
  dailyCancellationLimit: 5,
}

const BOUND_LABELS: Record<string, string> = {
  RESERVATION_MIN_DURATION: "Minimum reservation duration",
  RESERVATION_MAX_DURATION: "Maximum reservation duration",
  MAX_ADVANCE_BOOKING: "Maximum advance booking horizon",
  INTER_BOOKING_BUFFER: "Inter-booking buffer",
  PAYMENT_HOLD_DURATION: "Payment hold duration",
  RESERVATION_PROTECTION_WINDOW: "Reservation protection window",
  ALLOCATION_LEAD_TIME: "Allocation lead time",
  FACILITY_FAILURE_REFUND_LEAD_TIME: "Facility failure refund lead time",
  OCCUPANCY_VALIDITY_TIMESPAN: "Occupancy validity timespan",
  ARRIVAL_LATE_TOLERANCE: "Arrival late tolerance",
}

const ERROR_MESSAGES: Record<string, string> = {
  RESOURCE_VERSION_CONFLICT:
    "This draft was changed by another administrator. Reload the latest state and try again.",
  CANNOT_MODIFY_ACTIVE_POLICY:
    "This revision has already been activated and can no longer be edited.",
  OVERLAPPING_POLICY_INTERVAL:
    "The activation schedule overlaps another revision. Choose a different effective time.",
  FORBIDDEN_SCOPE_WRITE:
    "Administrators can only manage platform-scope policies. Site policies belong to the lot owner.",
  POLICY_BOUNDS_VIOLATED:
    "One or more override bounds are invalid. Check the minimum and maximum ranges.",
  VALIDATION_FAILED: "Some values are invalid. Review the highlighted fields and try again.",
  POLICY_NOT_FOUND: "The draft could not be found. It may have been removed.",
  INVALID_REQUEST: "The request could not be processed. Check the supplied values and try again.",
}

function describeDraftError(error: unknown): string {
  if (error instanceof AuthApiError) {
    if (error.code && ERROR_MESSAGES[error.code]) return ERROR_MESSAGES[error.code]
    return error.message
  }
  return error instanceof Error ? error.message : "Something went wrong. Please try again."
}

type BoundEditorState = Record<string, { enabled: boolean; minimum: string; maximum: string }>

function initialForm(revision: PolicyRevisionListItem | null): Record<string, string> {
  const next: Record<string, string> = {}
  for (const group of FIELD_GROUPS) {
    for (const field of group.fields) {
      const value = revision ? revision[field.key] : null
      next[field.key] = String(value ?? DEFAULTS[field.key])
    }
  }
  return next
}

function initialBounds(revision: PolicyRevisionListItem | null): BoundEditorState {
  const next: BoundEditorState = {}
  for (const key of OVERRIDABLE_POLICY_KEYS) {
    const bound = revision?.overrideBounds?.[key]
    next[key] = bound
      ? { enabled: true, minimum: String(bound.minimum), maximum: String(bound.maximum) }
      : { enabled: false, minimum: "", maximum: "" }
  }
  return next
}

function buildValues(form: Record<string, string>, bounds: BoundEditorState): PolicyDraftValues {
  const num = (key: string) => {
    const raw = (form[key] ?? "").trim()
    return raw === "" ? NaN : Number(raw)
  }
  const overrideBounds: Record<string, PolicyBoundDto> = {}
  for (const [key, entry] of Object.entries(bounds)) {
    if (!entry.enabled) continue
    overrideBounds[key] = {
      minimum: entry.minimum.trim() === "" ? NaN : Number(entry.minimum),
      maximum: entry.maximum.trim() === "" ? NaN : Number(entry.maximum),
    }
  }
  return {
    paymentHoldDurationMinutes: num("paymentHoldDurationMinutes"),
    minReservationDurationMinutes: num("minReservationDurationMinutes"),
    maxReservationDurationHours: num("maxReservationDurationHours"),
    maxAdvanceBookingDays: num("maxAdvanceBookingDays"),
    interBookingBufferMinutes: num("interBookingBufferMinutes"),
    reservationProtectionWindowHours: num("reservationProtectionWindowHours"),
    allocationLeadTimeMinutes: num("allocationLeadTimeMinutes"),
    facilityFailureRefundLeadMinutes: num("facilityFailureRefundLeadMinutes"),
    occupancyValidityTimespanHours: num("occupancyValidityTimespanHours"),
    arrivalLateToleranceMinutes: num("arrivalLateToleranceMinutes"),
    rapidCancellationCount: num("rapidCancellationCount"),
    rapidCancellationWindowMinutes: num("rapidCancellationWindowMinutes"),
    rapidCancellationRestrictionMinutes: num("rapidCancellationRestrictionMinutes"),
    dailyCancellationLimit: num("dailyCancellationLimit"),
    arrivalLateToleranceEnabled: null,
    cancellationTimezone: TIMEZONE,
    overrideBounds,
  }
}

export function PolicyDraftModal({
  mode,
  revision = null,
  baseRevision = null,
  onClose,
  onSaved,
  onReload,
}: {
  mode: "create" | "edit"
  revision?: PolicyRevisionListItem | null
  baseRevision?: PolicyRevisionListItem | null
  onClose: () => void
  onSaved: () => void
  onReload?: (id: string) => Promise<PolicyRevisionListItem | null>
}) {
  const seed = mode === "create" ? (baseRevision ?? revision) : revision
  const [form, setForm] = useState<Record<string, string>>(() => initialForm(seed))
  const [bounds, setBounds] = useState<BoundEditorState>(() => initialBounds(seed))
  const [expectedUpdatedAt, setExpectedUpdatedAt] = useState(revision?.updatedAt ?? "")
  const [touched, setTouched] = useState<Set<string>>(() => new Set())
  const [submitted, setSubmitted] = useState(false)
  const [saving, setSaving] = useState(false)
  const [reloading, setReloading] = useState(false)
  const [conflict, setConflict] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const values = useMemo(() => buildValues(form, bounds), [form, bounds])
  const issues = useMemo(() => validateDraftValues(values), [values])
  const issuesByProperty = useMemo(() => {
    const map: Record<string, ValidationIssue[]> = {}
    for (const issue of issues) {
      ;(map[issue.property] ??= []).push(issue)
    }
    return map
  }, [issues])

  function showMessage(property: string): boolean {
    return submitted || touched.has(property)
  }

  function markTouched(property: string) {
    setTouched((current) => {
      if (current.has(property)) return current
      const next = new Set(current)
      next.add(property)
      return next
    })
  }

  async function save() {
    setSaving(true)
    setError(null)
    try {
      if (mode === "create") {
        await createDraft({ ...values })
      } else {
        if (!revision) throw new Error("This draft is missing its revision identifier.")
        await updateDraft(revision.id, { ...values }, expectedUpdatedAt)
      }
      onSaved()
      onClose()
    } catch (err) {
      const isConflict =
        err instanceof AuthApiError &&
        (err.code === "RESOURCE_VERSION_CONFLICT" ||
          (err.status === 409 && err.code === "RESOURCE_VERSION_CONFLICT"))
      if (isConflict) {
        setConflict(true)
        return
      }
      setError(describeDraftError(err))
    } finally {
      setSaving(false)
    }
  }

  function handleSubmit(event: React.FormEvent) {
    event.preventDefault()
    setSubmitted(true)
    setError(null)
    if (hasBlockingIssues(validateDraftValues(buildValues(form, bounds)))) return
    void save()
  }

  async function reloadLatest() {
    if (!onReload || !revision) return
    setReloading(true)
    setError(null)
    try {
      const latest = await onReload(revision.id)
      if (!latest) {
        setConflict(false)
        setError("This draft no longer exists. It may have been activated or removed.")
        return
      }
      setForm(initialForm(latest))
      setBounds(initialBounds(latest))
      setExpectedUpdatedAt(latest.updatedAt)
      setTouched(new Set())
      setSubmitted(false)
      setConflict(false)
    } catch (err) {
      setError(describeDraftError(err))
    } finally {
      setReloading(false)
    }
  }

  function updateBound(key: string, patch: Partial<{ enabled: boolean; minimum: string; maximum: string }>) {
    setBounds((current) => ({ ...current, [key]: { ...current[key], ...patch } }))
  }

  const title =
    mode === "create" ? "Create policy draft" : `Edit draft · version ${revision?.version ?? ""}`

  return (
    <Modal title={title} onClose={onClose} className="max-w-3xl">
      <form onSubmit={handleSubmit} className="grid gap-5">
        {conflict && (
          <div
            role="alert"
            className="flex flex-wrap items-center gap-3 rounded-xl border p-3 text-sm"
            style={{
              borderColor: "var(--reserved)",
              background: "color-mix(in srgb, var(--reserved) 12%, var(--card))",
              color: "var(--badge-warning-fg)",
            }}
          >
            <UntitledIcon name="alert" size={18} />
            <span className="min-w-0 flex-1">
              This draft was changed by another administrator. Reload the latest state before saving.
            </span>
            <Button
              type="button"
              variant="outline"
              className="text-xs"
              loading={reloading}
              leadingIcon={<UntitledIcon name="activity" size={15} />}
              onClick={() => void reloadLatest()}
            >
              Reload latest state
            </Button>
          </div>
        )}

        {error && (
          <div
            role="alert"
            className="flex items-center gap-2 rounded-xl border p-3 text-sm"
            style={{
              borderColor: "var(--occupied)",
              background: "color-mix(in srgb, var(--occupied) 10%, var(--card))",
              color: "var(--badge-danger-strong)",
            }}
          >
            <UntitledIcon name="alert" size={18} />
            <span className="min-w-0 flex-1">{error}</span>
          </div>
        )}

        {FIELD_GROUPS.map((group) => (
          <fieldset key={group.title} className="grid gap-3">
            <legend className="flex flex-wrap items-baseline gap-2">
              <span className="text-sm font-semibold" style={{ color: "var(--fg)" }}>
                {group.title}
              </span>
              {group.note && (
                <span className="text-xs" style={{ color: "var(--muted)" }}>
                  {group.note}
                </span>
              )}
            </legend>
            <div className="grid gap-3 sm:grid-cols-2">
              {group.fields.map((field) => {
                const fieldIssues = issuesByProperty[field.key] ?? []
                const show = showMessage(field.key) && fieldIssues.length > 0
                return (
                  <div key={field.key}>
                    <label className="label" htmlFor={`policy-${field.key}`}>
                      {field.label}{" "}
                      <span style={{ color: "var(--muted)", fontWeight: 400 }}>({field.unit})</span>
                    </label>
                    <input
                      id={`policy-${field.key}`}
                      className="input"
                      type="number"
                      inputMode="numeric"
                      aria-invalid={show ? "true" : "false"}
                      aria-describedby={show ? `policy-${field.key}-error` : undefined}
                      value={form[field.key] ?? ""}
                      onChange={(event) =>
                        setForm((current) => ({ ...current, [field.key]: event.target.value }))
                      }
                      onBlur={() => markTouched(field.key)}
                      style={
                        show ? { borderColor: "var(--occupied)" } : undefined
                      }
                    />
                    {field.key === "interBookingBufferMinutes" && (
                      <p className="mt-1 text-xs" style={{ color: "var(--muted)" }}>
                        0 means no buffer required — a real value, not disabled.
                      </p>
                    )}
                    {field.key === "maxAdvanceBookingDays" && (
                      <p className="mt-1 text-xs" style={{ color: "var(--muted)" }}>
                        0 closes advance booking entirely.
                      </p>
                    )}
                    {show && (
                      <p
                        id={`policy-${field.key}-error`}
                        role="alert"
                        className="mt-1 text-xs"
                        style={{ color: "var(--badge-danger-strong)" }}
                      >
                        {fieldIssues[0].message}
                      </p>
                    )}
                  </div>
                )
              })}
            </div>
          </fieldset>
        ))}

        <fieldset className="grid gap-3">
          <legend className="text-sm font-semibold" style={{ color: "var(--fg)" }}>
            Override bounds (site limits)
          </legend>
          <p className="text-xs" style={{ color: "var(--muted)" }}>
            Bounds are expressed in minutes and only apply to the {OVERRIDABLE_POLICY_KEYS.length} overridable
            policy keys. Leave a key unchecked to leave it without a site range.
          </p>
          <div
            className="grid gap-2 rounded-xl border p-3"
            style={{ borderColor: "var(--border)", background: "var(--bg)" }}
          >
            <div className="grid items-center gap-2 text-xs sm:grid-cols-[minmax(0,1fr)_110px_110px]" style={{ color: "var(--muted)", fontWeight: 600 }}>
              <span>Policy</span>
              <span>Minimum (minutes)</span>
              <span>Maximum (minutes)</span>
            </div>
            {OVERRIDABLE_POLICY_KEYS.map((key) => {
              const entry = bounds[key]
              const property = `overrideBounds.${key}`
              const boundIssue = issuesByProperty[property]?.[0]
              const show = showMessage(property) && Boolean(boundIssue)
              return (
                <div key={key} className="grid items-center gap-2 sm:grid-cols-[minmax(0,1fr)_110px_110px]">
                  <label className="flex items-center gap-2 text-sm" style={{ color: "var(--fg)" }}>
                    <input
                      type="checkbox"
                      checked={entry.enabled}
                      onChange={(event) => {
                        markTouched(property)
                        updateBound(key, { enabled: event.target.checked })
                      }}
                    />
                    <span className="min-w-0">{CANONICAL_POLICY_LABELS[key] ?? key}</span>
                  </label>
                  <input
                    className="input"
                    type="number"
                    inputMode="numeric"
                    aria-label={`${CANONICAL_POLICY_LABELS[key] ?? key} minimum bound in minutes`}
                    aria-invalid={show ? "true" : "false"}
                    aria-describedby={show ? `policy-bound-${key}-error` : undefined}
                    disabled={!entry.enabled}
                    value={entry.minimum}
                    onChange={(event) => updateBound(key, { minimum: event.target.value })}
                    onBlur={() => markTouched(property)}
                  />
                  <input
                    className="input"
                    type="number"
                    inputMode="numeric"
                    aria-label={`${CANONICAL_POLICY_LABELS[key] ?? key} maximum bound in minutes`}
                    aria-invalid={show ? "true" : "false"}
                    aria-describedby={show ? `policy-bound-${key}-error` : undefined}
                    disabled={!entry.enabled}
                    value={entry.maximum}
                    onChange={(event) => updateBound(key, { maximum: event.target.value })}
                    onBlur={() => markTouched(property)}
                  />
                  {show && (
                    <p
                      id={`policy-bound-${key}-error`}
                      role="alert"
                      className="text-xs sm:col-span-3"
                      style={{ color: "var(--badge-danger-strong)" }}
                    >
                      {boundIssue.message}
                    </p>
                  )}
                </div>
              )
            })}
          </div>
        </fieldset>

        <div className="grid gap-2">
          <label className="label" htmlFor="policy-timezone">
            Cancellation timezone
          </label>
          <input
            id="policy-timezone"
            className="input"
            value={TIMEZONE}
            readOnly
            disabled
            aria-describedby="policy-timezone-hint"
          />
          <p id="policy-timezone-hint" className="text-xs" style={{ color: "var(--muted)" }}>
            Fixed by the platform. <Badge tone="muted">Read-only</Badge>
          </p>
        </div>

        <div className="flex flex-wrap justify-end gap-3 border-t pt-4" style={{ borderColor: "var(--border)" }}>
          <Button type="button" variant="outline" onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button
            type="submit"
            variant="primary"
            loading={saving}
            leadingIcon={<UntitledIcon name="check" size={16} />}
          >
            {mode === "create" ? "Create draft" : "Save changes"}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
