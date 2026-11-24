import { formatSlotLabel, type PhysicalState } from "./operationalLayout"
import type {
  CreateEmergencyRestrictionInput,
  EmergencyRestriction,
  EmergencyRestrictionMode,
  EmergencyRestrictionScope,
  OperatorSite,
} from "./operatorApi"

export interface EmergencyUnitTarget {
  unitId: string
  label: string
  kind: "FLOOR" | "ZONE"
}

export interface EmergencySlotTarget {
  slotId: string
  unitId: string
  label: string
  slotCode: string
  floorCode: string
  zoneCode: string | null
  physicalState: PhysicalState
}

export interface EmergencyScopeTargets {
  units: EmergencyUnitTarget[]
  slots: EmergencySlotTarget[]
}

export interface EmergencyRestrictionDraft {
  mode: EmergencyRestrictionMode
  scope: EmergencyRestrictionScope
  unitId: string
  slotId: string
  reason: string
  effectiveUntilLocal: string
  targets: EmergencyScopeTargets
}

export type EmergencyRestrictionValidation =
  | { ok: true; payload: CreateEmergencyRestrictionInput }
  | { ok: false; error: string }

/**
 * Backend unit/slot ids are UUIDs. The operator site model also contains synthesized ids
 * (`zone:<floorId>:<zoneCode>`) that the API rejects on model binding, so every target handed to
 * a request must be matched against this shape first.
 */
const BACKEND_ID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
const MAX_REASON_LENGTH = 2000

interface RawUnit {
  id: string
  parentId: string | null
  name: string
  type: string
}

function isBackendId(value: string | null | undefined): value is string {
  return typeof value === "string" && BACKEND_ID.test(value)
}

function addUnit(
  units: EmergencyUnitTarget[],
  seen: Set<string>,
  unitId: string,
  label: string,
  kind: "FLOOR" | "ZONE",
): void {
  if (!isBackendId(unitId) || seen.has(unitId)) return
  seen.add(unitId)
  units.push({ unitId, label, kind })
}

/**
 * Collects the real backend unit and slot ids an operator may target. The operational layout is
 * authoritative when present; the legacy unit/slot arrays are only a fallback and are filtered so
 * synthesized ids never reach the API.
 */
export function extractEmergencyScopeTargets(
  lot: OperatorSite | null | undefined,
): EmergencyScopeTargets {
  if (!lot) return { units: [], slots: [] }

  const projection = lot.operationalLayout ?? lot.layout ?? null
  if (!projection) return extractFromLegacyArrays(lot)

  const units: EmergencyUnitTarget[] = []
  const seen = new Set<string>()
  for (const floor of projection.floors ?? []) {
    addUnit(units, seen, floor.floorId, `Floor ${floor.name} (${floor.floorCode})`, "FLOOR")
  }

  const zones = projection.metrics?.zoneCapacities ?? []
  for (const zone of zones) {
    addUnit(units, seen, zone.zoneId, `${zone.floorCode} · ${zone.name ?? zone.zoneCode}`, "ZONE")
  }

  // Floor code + zone code identify the zone's real id, which is what the backend stores.
  const zoneLookup = new Map<string, string>()
  for (const zone of zones) {
    if (!isBackendId(zone.zoneId)) continue
    zoneLookup.set(`${zone.floorCode}:${zone.zoneCode}`, zone.zoneId)
  }

  const slots: EmergencySlotTarget[] = []
  for (const slot of projection.slots ?? []) {
    if (!isBackendId(slot.slotId)) continue
    const zoneId = slot.zoneCode ? zoneLookup.get(`${slot.floorCode}:${slot.zoneCode}`) : undefined
    const unitId = zoneId ?? slot.floorId
    if (!isBackendId(unitId)) continue
    const zoneSuffix = slot.zoneCode ? ` · ${slot.zoneCode}` : ""
    slots.push({
      slotId: slot.slotId,
      unitId,
      label: `${formatSlotLabel(slot.slotCode)} (${slot.floorCode}${zoneSuffix})`,
      slotCode: slot.slotCode,
      floorCode: slot.floorCode,
      zoneCode: slot.zoneCode ?? null,
      physicalState: slot.physicalState,
    })
  }

  return { units, slots }
}

function extractFromLegacyArrays(lot: OperatorSite): EmergencyScopeTargets {
  const rawUnits = new Map<string, RawUnit>()
  for (const unit of lot.units ?? []) rawUnits.set(unit.id, unit)

  const units: EmergencyUnitTarget[] = []
  const seen = new Set<string>()
  for (const unit of lot.units ?? []) {
    if (!isBackendId(unit.id)) continue
    addUnit(units, seen, unit.id, unit.name, unit.type === "ZONE" ? "ZONE" : "FLOOR")
  }

  const slots: EmergencySlotTarget[] = []
  for (const slot of lot.slots ?? []) {
    if (!isBackendId(slot.id)) continue
    const unitId = resolveLegacyUnitId(slot.unitId, rawUnits)
    if (!isBackendId(unitId)) continue
    const unit = rawUnits.get(unitId)
    const zoneCode = unit?.type === "ZONE" ? unit.name : null
    const floorCode =
      unit?.type === "ZONE"
        ? (unit.parentId ? (rawUnits.get(unit.parentId)?.name ?? "") : "")
        : (unit?.name ?? "")
    const suffix = floorCode ? ` (${floorCode}${zoneCode ? ` · ${zoneCode}` : ""})` : ""
    slots.push({
      slotId: slot.id,
      unitId,
      label: `${formatSlotLabel(slot.number)}${suffix}`,
      slotCode: slot.number,
      floorCode,
      zoneCode,
      physicalState: slot.physicalState,
    })
  }

  return { units, slots }
}

/** A synthesized zone id is resolved to its floor (a real backend unit) instead of being sent. */
function resolveLegacyUnitId(
  unitId: string | null | undefined,
  rawUnits: Map<string, RawUnit>,
): string | null {
  if (isBackendId(unitId)) return unitId
  if (!unitId) return null
  const synthetic = rawUnits.get(unitId)
  if (synthetic && isBackendId(synthetic.parentId)) return synthetic.parentId
  return null
}

/**
 * Restriction control requires the site-wide slot override privilege. When the assignment did not
 * carry a permission list the backend stays authoritative, so the controls are left enabled.
 */
export function canManageEmergencyRestrictions(lot: OperatorSite | null | undefined): boolean {
  if (!lot) return false
  const permissions = lot.assignmentPermissions
  if (!Array.isArray(permissions)) return true
  return permissions.includes("SLOT_OVERRIDE")
}

export function validateEmergencyRestrictionDraft(
  draft: EmergencyRestrictionDraft,
  nowMs: number = Date.now(),
): EmergencyRestrictionValidation {
  const reason = draft.reason.trim()
  if (reason.length === 0) return { ok: false, error: "A reason is required." }
  if (reason.length > MAX_REASON_LENGTH) {
    return { ok: false, error: `Reason must be ${MAX_REASON_LENGTH} characters or fewer.` }
  }

  let unitId: string | null = null
  let slotId: string | null = null

  if (draft.scope === "UNIT") {
    const requestedUnitId = draft.unitId.trim()
    if (!isBackendId(requestedUnitId)) {
      return { ok: false, error: "Select a floor or zone to restrict." }
    }
    unitId = requestedUnitId
  } else if (draft.scope === "SLOT") {
    const requestedSlotId = draft.slotId.trim()
    const target = draft.targets.slots.find((slot) => slot.slotId === requestedSlotId)
    const resolvedSlotId = target?.slotId ?? requestedSlotId
    const resolvedUnitId = target?.unitId ?? draft.unitId.trim()
    if (!isBackendId(resolvedSlotId) || !isBackendId(resolvedUnitId)) {
      return { ok: false, error: "Select a specific bay to restrict." }
    }
    unitId = resolvedUnitId
    slotId = resolvedSlotId
  }

  let effectiveUntil: string | null = null
  const untilInput = draft.effectiveUntilLocal.trim()
  if (untilInput.length > 0) {
    const parsed = new Date(untilInput)
    const parsedMs = parsed.getTime()
    if (Number.isNaN(parsedMs)) {
      return { ok: false, error: "Effective until must be a valid date and time." }
    }
    if (parsedMs <= nowMs) return { ok: false, error: "Effective until must be in the future." }
    effectiveUntil = parsed.toISOString()
  }

  return {
    ok: true,
    payload: { mode: draft.mode, scope: draft.scope, unitId, slotId, reason, effectiveUntil },
  }
}

export function formatEmergencyRestrictionScope(
  restriction: EmergencyRestriction,
  targets: EmergencyScopeTargets,
): string {
  if (restriction.scope === "SITE") return "Whole site"

  if (restriction.scope === "UNIT") {
    const unit = restriction.unitId
      ? targets.units.find((candidate) => candidate.unitId === restriction.unitId)
      : undefined
    return unit?.label ?? `Unit ${restriction.unitId?.slice(0, 8) ?? ""}`
  }

  const slot = restriction.slotId
    ? targets.slots.find((candidate) => candidate.slotId === restriction.slotId)
    : undefined
  return slot?.label ?? `Slot ${restriction.slotId?.slice(0, 8) ?? ""}`
}
