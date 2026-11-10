/**
 * Pure helpers that decide which 3D template meshes correspond to registered slots.
 *
 * A registered slot is the only thing the Owner may see as interactive: an unregistered
 * template mesh is presentation only, is never counted in capacity and is never claimed
 * by a booking (A7/A12). Nothing in this module touches Three.js so the matching rules
 * can be exercised without a WebGL context.
 */

/**
 * Normalizes a slot identifier for robust matching.
 * Examples:
 *   "Slot_A-001" -> "A-001"
 *   "A_001"      -> "A-001"
 *   "a-001"      -> "A-001"
 *   "A-001.001"  -> "A-001" (Blender copy duplicate suffix)
 *   "Slot-101"   -> "101"
 */
export function normalizeSlotIdentifier(raw: string): string {
  if (!raw) return ""

  let str = raw.trim()

  // Strip Blender duplicate suffixes like ".001" or ".002"
  str = str.replace(/\.\d{3}$/, "")

  // Strip common prefixes: "slot_", "slot-", "carparkingspace_", "car_parking_space_", "carparking_", "parking_", "bay_"
  str = str.replace(
    /^(?:carparkingspace|car_parking_space|carparking|car|parking|slot|bay|mesh|object)[_\s-]?/i,
    "",
  )

  // Replace underscores between letter and numbers with hyphen (e.g., A_001 -> A-001)
  str = str.replace(/^([a-zA-Z]+)_+(\d+)/, "$1-$2")

  return str.toUpperCase()
}

/**
 * Checks whether an object name resembles a parking slot.
 * Matches patterns such as:
 * - A-001, B-023, M-005
 * - 001, 101, 202
 * - Slot_A-001, slot-B-012, CarParkingSpace_001
 */
export function isParkingSlotObjectName(name: string): boolean {
  if (!name) return false

  const normalized = normalizeSlotIdentifier(name)

  const slotPattern = /^([A-Z]{1,3}-\d{1,4}|\d{1,4}|[A-Z]{1,2}\d{1,4})$/

  return slotPattern.test(normalized)
}

const NON_SLOT_NAME = /sign|post|text|arrow|pole|camera|sensor|barrier|line|screen|logo|body|bay_\d|plinth|led|light/i

/**
 * Name-only classification used both by the scene traversal and by tests. A mesh is a
 * slot candidate when its name follows the template naming convention; a mesh that only
 * carries `userData.isSlot` is classified by the caller.
 */
export function isSlotCandidateName(name: string): boolean {
  if (!name) return false
  if (NON_SLOT_NAME.test(name)) return false
  if (/carparkingspace/i.test(name)) return true
  if (/parking_ev_car/i.test(name)) return true
  if (/parking_ev/i.test(name)) return true
  if (/parkingspace/i.test(name)) return true
  if (/\b(?:slot|bay)\b/i.test(name)) return true
  if (/car_[g|l\d]_\d+/i.test(name)) return true
  if (/b\d+_car_\d+/i.test(name)) return true
  if (/b\d+_ev_car_\d+/i.test(name)) return true
  return isParkingSlotObjectName(name)
}

/**
 * A stable default slot code for a selected template mesh. The Owner can still edit it
 * before registration; this only removes the retyping for meshes that already follow the
 * `[AREA]-[NUMBER]` convention.
 */
export function suggestSlotCode(meshName: string): string {
  const normalized = normalizeSlotIdentifier(meshName)
  return (normalized || meshName.trim()).slice(0, 50)
}

export interface MeshCandidate {
  /** Stable per-load identity of the mesh (Three.js object uuid in the viewer). */
  key: string
  /** Raw template name, e.g. `Slot_A-001`. */
  name: string
}

export interface RegisteredSlotRef {
  id: string
  code: string
}

export interface MeshSlotMatch {
  key: string
  slotId: string
  code: string
}

export interface MeshSlotMapping {
  /** Meshes that map to a live registered slot and may therefore be interactive. */
  matches: MeshSlotMatch[]
  /** Template meshes with no registered slot. They stay inert. */
  unregistered: MeshCandidate[]
  /** Registered slots that no template mesh could be matched to. */
  unmatchedSlots: RegisteredSlotRef[]
}

function aliasIdentifier(value: string): string {
  return normalizeSlotIdentifier(value).replace(/-/g, "")
}

/**
 * Maps template meshes to registered slots.
 *
 * 1. Exact normalized name/code matches always win (`Slot_A-001` -> `A-001`).
 * 2. A hyphen-insensitive alias pass resolves `A001` to `A-001`.
 * 3. Only when every remaining mesh can be paired with exactly one remaining slot is a
 *    positional fallback used. A partial pairing would make an arbitrary offline mesh
 *    interactive, which would contradict the rule that unregistered meshes are inert.
 */
export function mapMeshCandidates(
  candidates: MeshCandidate[],
  slots: RegisteredSlotRef[],
): MeshSlotMapping {
  const usedSlots = new Set<string>()
  const matched: MeshSlotMatch[] = []
  const pending: MeshCandidate[] = []
  const seenMeshes = new Set<string>()

  const exactIndex = new Map<string, RegisteredSlotRef[]>()
  const aliasIndex = new Map<string, RegisteredSlotRef[]>()
  for (const slot of slots) {
    const exact = normalizeSlotIdentifier(slot.code)
    if (exact) exactIndex.set(exact, [...(exactIndex.get(exact) ?? []), slot])
    const alias = aliasIdentifier(slot.code)
    if (alias) aliasIndex.set(alias, [...(aliasIndex.get(alias) ?? []), slot])
  }

  function take(index: Map<string, RegisteredSlotRef[]>, key: string): RegisteredSlotRef | null {
    if (!key) return null
    const options = index.get(key) ?? []
    const available = options.find((slot) => !usedSlots.has(slot.id))
    if (!available) return null
    usedSlots.add(available.id)
    return available
  }

  for (const candidate of candidates) {
    if (seenMeshes.has(candidate.key)) continue
    seenMeshes.add(candidate.key)
    const exact = take(exactIndex, normalizeSlotIdentifier(candidate.name))
    const slot = exact ?? take(aliasIndex, aliasIdentifier(candidate.name))
    if (slot) matched.push({ key: candidate.key, slotId: slot.id, code: slot.code })
    else pending.push(candidate)
  }

  const remainingSlots = slots.filter((slot) => !usedSlots.has(slot.id))

  // One mesh per remaining slot: pairing is unambiguous, so keep a fully registered
  // template interactive even when its mesh names do not follow the slot codes.
  if (pending.length > 0 && pending.length === remainingSlots.length) {
    pending.forEach((candidate, index) => {
      const slot = remainingSlots[index]
      matched.push({ key: candidate.key, slotId: slot.id, code: slot.code })
    })
    return { matches: matched, unregistered: [], unmatchedSlots: [] }
  }

  return { matches: matched, unregistered: pending, unmatchedSlots: remainingSlots }
}
