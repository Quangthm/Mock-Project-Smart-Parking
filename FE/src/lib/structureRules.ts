/**
 * Pure rules for the Owner structure screens. They mirror the server contract in
 * `docs/03-design/api/parking-structure-api-design.md` (SPARK-238 section) but never
 * replace the server's decision: the UI explains a change before sending it, and a
 * rejection from the backend is still displayed verbatim.
 */

export const UNIT_TYPES = ["FLOOR", "ZONE", "BLOCK"] as const
export type StructureUnitType = (typeof UNIT_TYPES)[number]

export const VEHICLE_CATEGORIES = ["CAR", "MOTORCYCLE", "OVERSIZED"] as const
export type StructureVehicleCategory = (typeof VEHICLE_CATEGORIES)[number]

export const SLOT_TYPES = ["STANDARD", "VIP", "EV", "DISABLED"] as const

export const MAX_SLOT_CATEGORIES = 3
export const MAX_UNIT_NAME_LENGTH = 100
export const MAX_SLOT_CODE_LENGTH = 50

export function isVehicleCategory(value: string): value is StructureVehicleCategory {
  return (VEHICLE_CATEGORIES as readonly string[]).includes(value)
}

/**
 * A child type must strictly follow its parent in FLOOR -> ZONE -> BLOCK order.
 * The lot root is allowed for a FLOOR or an outdoor root ZONE, never for a BLOCK.
 */
export function allowedParentTypes(type: StructureUnitType): StructureUnitType[] {
  switch (type) {
    case "FLOOR":
      return []
    case "ZONE":
      return ["FLOOR"]
    case "BLOCK":
      return ["FLOOR", "ZONE"]
  }
}

export function parentUnitOptions<T extends { id: string; type: string }>(
  type: StructureUnitType,
  units: T[],
): T[] {
  const allowed = allowedParentTypes(type)
  return units.filter((unit) => (allowed as string[]).includes(unit.type))
}

export interface NamedSibling {
  id: string
  name: string
}

/**
 * Case-insensitive sibling uniqueness, mirroring `uq_active_unit_name_ci`. `siblings`
 * must contain only live units that share the same parent.
 */
export function validateUnitName(
  name: string,
  siblings: NamedSibling[],
  excludeId?: string | null,
): string | null {
  const trimmed = name.trim()
  if (!trimmed) return "Enter a unit name."
  if (trimmed.length > MAX_UNIT_NAME_LENGTH) {
    return `Unit names can contain at most ${MAX_UNIT_NAME_LENGTH} characters.`
  }
  const clash = siblings.find(
    (sibling) =>
      sibling.id !== excludeId &&
      sibling.name.trim().toLowerCase() === trimmed.toLowerCase(),
  )
  return clash ? `A unit named “${clash.name}” already exists in this location.` : null
}

export interface UnitReference {
  id: string
  parentId: string | null
  name: string
}

export interface SlotReference {
  unitId: string
}

export interface PathReference {
  fromUnitId: string | null
  toUnitId: string | null
}

/**
 * Human-readable reasons a unit cannot be removed. The server answers `UNIT_NOT_EMPTY`
 * with the same cause; the UI shows it before the request so the Owner knows what to
 * remove first instead of only seeing a generic error.
 */
export function unitRemovalBlockers(
  unitId: string,
  units: UnitReference[],
  slots: SlotReference[],
  paths: PathReference[],
): string[] {
  const children = units.filter((unit) => unit.parentId === unitId)
  const unitSlots = slots.filter((slot) => slot.unitId === unitId)
  const unitPaths = paths.filter(
    (path) => path.fromUnitId === unitId || path.toUnitId === unitId,
  )
  const reasons: string[] = []
  if (children.length > 0) {
    reasons.push(
      `${children.length} child unit${children.length === 1 ? "" : "s"}`,
    )
  }
  if (unitSlots.length > 0) {
    reasons.push(
      `${unitSlots.length} registered slot${unitSlots.length === 1 ? "" : "s"}`,
    )
  }
  if (unitPaths.length > 0) {
    reasons.push(
      `${unitPaths.length} access path${unitPaths.length === 1 ? "" : "s"}`,
    )
  }
  return reasons
}

export interface SlotPrimaryResolution {
  primary: string | null
  error: string | null
}

/**
 * Resolves the category a slot is counted in. One category has exactly one possible
 * primary; two or more must be declared explicitly and are never inferred from the set
 * (F05). This mirrors `SlotCompatibility.Primary` on the server.
 */
export function resolveSlotPrimary(
  categories: string[],
  declaredPrimary?: string | null,
): SlotPrimaryResolution {
  const values = Array.from(new Set(categories.map((value) => value.trim().toUpperCase())))
  if (values.length === 0) {
    return { primary: null, error: "Select at least one vehicle category." }
  }
  if (values.length > MAX_SLOT_CATEGORIES) {
    return {
      primary: null,
      error: `A slot can support at most ${MAX_SLOT_CATEGORIES} categories.`,
    }
  }
  const unknown = values.find((value) => !isVehicleCategory(value))
  if (unknown) return { primary: null, error: `Unsupported vehicle category “${unknown}”.` }

  if (declaredPrimary) {
    const primary = declaredPrimary.trim().toUpperCase()
    if (!values.includes(primary)) {
      return {
        primary: null,
        error: "The primary category must be one of the selected categories.",
      }
    }
    return { primary, error: null }
  }
  if (values.length === 1) return { primary: values[0], error: null }
  return {
    primary: null,
    error: "Choose the primary category this slot is counted in.",
  }
}

/**
 * Adds or removes a category while never exceeding the three-category limit.
 * Returns the next set and a message when the change was refused.
 */
export function toggleSlotCategory(
  categories: string[],
  category: string,
): { categories: string[]; error: string | null } {
  if (categories.includes(category)) {
    return { categories: categories.filter((value) => value !== category), error: null }
  }
  if (categories.length >= MAX_SLOT_CATEGORIES) {
    return {
      categories,
      error: `A slot can support at most ${MAX_SLOT_CATEGORIES} categories.`,
    }
  }
  return { categories: [...categories, category], error: null }
}

export interface RegistrationDraft {
  key: string
  code: string
}

/**
 * Per-item code validation for the mesh multi-select. Duplicate codes in one selection
 * would produce one success and one 409, so they are refused before any request.
 */
export function registrationCodeIssues(
  drafts: RegistrationDraft[],
): Record<string, string> {
  const issues: Record<string, string> = {}
  const seen = new Map<string, string>()
  for (const draft of drafts) {
    const code = draft.code.trim()
    if (!code) {
      issues[draft.key] = "Enter a slot code."
      continue
    }
    if (code.length > MAX_SLOT_CODE_LENGTH) {
      issues[draft.key] = `Slot codes can contain at most ${MAX_SLOT_CODE_LENGTH} characters.`
      continue
    }
    const normalized = code.toUpperCase()
    const other = seen.get(normalized)
    if (other) {
      issues[draft.key] = "This code is used by another selected mesh."
      issues[other] = issues[other] ?? "This code is used by another selected mesh."
      continue
    }
    seen.set(normalized, draft.key)
  }
  return issues
}

export type RegistrationStatus = "succeeded" | "failed" | "released" | "resolved"

export interface RegistrationOutcomeLike {
  key: string
  status: RegistrationStatus
}

export interface RegistrationSummary<T extends RegistrationOutcomeLike> {
  total: number
  succeeded: T[]
  failed: T[]
  released: T[]
  resolved: T[]
  /** Only a plain per-item failure may be re-sent automatically. */
  retryable: T[]
  /** Requires an explicit Owner decision (restore or choose another code). */
  decisions: T[]
}

/**
 * Per-item registration aggregation. `IDENTIFIER_RELEASED` is deliberately excluded from
 * `retryable`: retrying is never an implicit restore (F06).
 */
export function summarizeRegistration<T extends RegistrationOutcomeLike>(
  outcomes: T[],
): RegistrationSummary<T> {
  return {
    total: outcomes.length,
    succeeded: outcomes.filter((outcome) => outcome.status === "succeeded"),
    failed: outcomes.filter((outcome) => outcome.status === "failed"),
    released: outcomes.filter((outcome) => outcome.status === "released"),
    resolved: outcomes.filter((outcome) => outcome.status === "resolved"),
    retryable: outcomes.filter((outcome) => outcome.status === "failed"),
    decisions: outcomes.filter((outcome) => outcome.status === "released"),
  }
}

/**
 * Half-open `[start, end)` validation. A commitment ending exactly at `start` does not
 * overlap, and a window that ends exactly at `start` is empty and rejected.
 */
export function validateMaintenanceWindow(
  startIso: string | null | undefined,
  endIso: string | null | undefined,
  now: Date = new Date(),
): string | null {
  if (!startIso) return "Choose a start date and time."
  if (!endIso) return "Choose an end date and time."
  const start = Date.parse(startIso)
  const end = Date.parse(endIso)
  if (Number.isNaN(start) || Number.isNaN(end)) {
    return "Enter valid start and end dates."
  }
  if (!(start < end)) {
    return "The end must be after the start. A window is half-open [start, end), so an empty window is refused."
  }
  if (start < now.getTime()) return "A maintenance window cannot start in the past."
  return null
}

/**
 * A window may only grow. Extending is allowed while `now < end`; the start never moves
 * and the end only moves later. Shortening is a cancellation plus a new window.
 */
export function validateWindowExtension(
  window: { start: string; end: string; cancelledAt?: string | null },
  newEndIso: string | null | undefined,
  now: Date = new Date(),
): string | null {
  if (window.cancelledAt) return "This window has been cancelled and cannot be extended."
  if (!newEndIso) return "Choose the new end date and time."
  const end = Date.parse(window.end)
  const nextEnd = Date.parse(newEndIso)
  if (Number.isNaN(end) || Number.isNaN(nextEnd)) return "Enter a valid end date."
  if (now.getTime() >= end) {
    return "Only a window that has not ended can be extended."
  }
  if (nextEnd <= end) {
    return "The end can only move later. Shortening or moving the start means cancelling and creating a new window."
  }
  return null
}

/** True inside a non-cancelled half-open window; `end` itself is outside. */
export function windowCovers(
  window: { start: string; end: string; cancelledAt?: string | null },
  now: Date = new Date(),
): boolean {
  if (window.cancelledAt) return false
  const start = Date.parse(window.start)
  const end = Date.parse(window.end)
  if (Number.isNaN(start) || Number.isNaN(end)) return false
  return start <= now.getTime() && now.getTime() < end
}

/** Converts a `datetime-local` value in the browser's zone to a UTC instant. */
export function localInputToIso(value: string | null | undefined): string | null {
  if (!value) return null
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? null : date.toISOString()
}

/** Converts an ISO instant to the local `datetime-local` input format. */
export function isoToLocalInput(value: string | null | undefined): string {
  if (!value) return ""
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ""
  const pad = (part: number) => String(part).padStart(2, "0")
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`
}

/** The default end offered when extending: one hour after the current end. */
export function defaultExtensionEnd(endIso: string): string {
  const end = Date.parse(endIso)
  if (Number.isNaN(end)) return ""
  return new Date(end + 60 * 60 * 1000).toISOString()
}
