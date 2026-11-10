import { AuthApiError, request } from "./authApi"
import type { OperationalLayoutProjection } from "./operationalLayout"

const server = (
  import.meta.env.VITE_PARKING_API_BASE_URL ?? "http://localhost:5045"
).replace(/\/$/, "")

type Response<T> = { success: boolean; data: T }

export interface OperationalLayoutOptions {
  vehicleType?: string
  floor?: string
}

/**
 * Lazily loaded so the existing CommonJS test harnesses can stub
 * `./operationalLayout` without parsing a second `import.meta.env` reference.
 */
let operationalLayoutModule: Promise<typeof import("./operationalLayout")> | null = null

async function operationalLayoutFixture(): Promise<OperationalLayoutProjection | null> {
  operationalLayoutModule ??= import("./operationalLayout")
  return (await operationalLayoutModule).readOperationalLayoutFixture()
}

export interface Site {
  id: string
  tenantId: string
  code: string
  name: string
  address: string
  latitude: number | null
  longitude: number | null
  status: string
  isActive: boolean
  totalPhysicalCapacity: number
}

export type StructureVehicleType = "CAR" | "MOTORCYCLE" | "OVERSIZED"
export type StructureSlotType = "STANDARD" | "VIP" | "EV" | "DISABLED"

export interface Unit {
  id: string
  parentId: string | null
  name: string
  type: "FLOOR" | "ZONE" | "BLOCK"
  maxCapacity: number
}

export interface Slot {
  id: string
  unitId: string
  code: string
  /** The single projection column. With several categories this is the primary. */
  vehicleType: string
  /** Categories this slot may be allocated to (compatibility table). */
  vehicleTypes?: string[]
  /** Contract alias for the projection column; the current service emits `vehicleType`. */
  primaryVehicleType?: string | null
  compatibilitySource?: "compatibility-table" | "projection" | "unclassified" | string
  type: string
  operationalStatus: string
  isPhysicallyOccupied: boolean
  reservationState: string | null
  /** Server-owned: read-only, never editable by the Owner. */
  coordinates3D?: string | null
  /** Server-owned: read-only, never editable by the Owner. */
  features?: string | null
}

export interface AccessPath {
  id: string
  code: string
  fromUnitId: string | null
  toUnitId: string | null
  mapData?: string | null
}

export type ServiceStateName =
  | "ACTIVE"
  | "INACTIVE"
  | "SCHEDULED"
  | "MAINTENANCE"
  | "MAINTENANCE_UNCERTAIN"

export interface MaintenanceWindow {
  id: string
  start: string
  end: string
  /** `pending` until an external acknowledgement exists; never "in force" by default. */
  enforcementState: "pending" | "acknowledged" | "failed" | string
  reason: string
  unitId?: string | null
  slotId?: string | null
  cancelledAt?: string | null
  acknowledgedAt?: string | null
}

export interface StructureServiceState {
  state: ServiceStateName | string
  closedUntil: string | null
  windows: MaintenanceWindow[]
}

/**
 * Three separate numbers the contract forbids merging or fabricating:
 * the derived registered count, the Owner-declared limit and Operations' availability.
 * Optional because the current `GET /structure` route does not emit the block yet.
 */
export interface StructureCapacity {
  registeredPhysicalCount: number | null
  configuredLimit: number | null
  operationalAvailable: number | null
  basis: string
  projectionState: "consistent" | "inconsistent" | "unavailable" | string
  reason: string | null
}

export interface CapacityView {
  unitId: string | null
  vehicleType: string
  total: number
  occupied: number
  protected: number
  pendingPayment: number
  configuredBackup: number
  markedBackup: number
  effectiveBackup: number
  unavailable: number
  available: number | null
  inheritedBackup: boolean
  projectionState?: "consistent" | "inconsistent" | "unavailable" | string
  reason?: string | null
}

export interface Structure {
  site: Site
  units: Unit[]
  slots: Slot[]
  paths: AccessPath[]
  capacityViews: CapacityView[]
  serviceState?: StructureServiceState
  capacity?: StructureCapacity
}

export interface ReleasedIdentifier {
  id: string
  code: string
  deletedAt: string
}

export interface StructureCommitment {
  id: string
  type: string
  start: string
  end: string | null
  unitId: string | null
  slotId: string | null
}

export interface StructureBatchFailure {
  resourceId: string
  code: string
  message: string
}

export type BatchDeactivationAction =
  | "active"
  | "removeUnit"
  | "removeSlot"
  | "removePath"
  | "markBackup"

export interface BatchDeactivationItem {
  action: BatchDeactivationAction
  resourceId: string
  /** Only send a token that a read actually returned (DE-16). */
  expectedUpdatedAt?: string
}

/**
 * Failure of a structure request, including the contract members the generic
 * `request` helper would otherwise drop.
 */
export class StructureApiError extends Error {
  constructor(
    message: string,
    public status: number,
    public code?: string,
    public released?: ReleasedIdentifier,
    public commitments?: StructureCommitment[],
    public items?: StructureBatchFailure[],
  ) {
    super(message)
    this.name = "StructureApiError"
  }
}

function normalizeReleased(raw: unknown): ReleasedIdentifier | undefined {
  if (!raw || typeof raw !== "object") return undefined
  const value = raw as Record<string, unknown>
  const id = value.id ?? value.slotId ?? value.unitId ?? value.pathId
  if (typeof id !== "string" || !id) return undefined
  return {
    id,
    code: typeof value.code === "string" ? value.code : "",
    deletedAt: typeof value.deletedAt === "string" ? value.deletedAt : "",
  }
}

function structureError(error: unknown): never {
  if (error instanceof StructureApiError) throw error
  // The check is written defensively so a partially mocked module graph in tests cannot
  // turn a plain failure into a TypeError.
  if (typeof AuthApiError === "function" && error instanceof AuthApiError) {
    const payload =
      error.payload && typeof error.payload === "object"
        ? (error.payload as Record<string, unknown>)
        : {}
    throw new StructureApiError(
      error.message,
      error.status,
      error.code,
      normalizeReleased(payload.released),
      Array.isArray(payload.commitments)
        ? (payload.commitments as StructureCommitment[])
        : undefined,
      Array.isArray(payload.items)
        ? (payload.items as StructureBatchFailure[])
        : undefined,
    )
  }
  throw error
}

async function call<T>(
  path: string,
  method = "GET",
  body?: unknown,
): Promise<T> {
  try {
    const result = await request<Response<T>>(
      path,
      method,
      body,
      "/api/parking-lots",
      server,
    )

    if (!result.success) throw new StructureApiError("Parking request failed.", 200)

    return result.data
  } catch (error) {
    structureError(error)
  }
}

export const parkingApi = {
  list: () => call<Site[]>(""),

  create: (body: { code: string; name: string; address: string; latitude?: number; longitude?: number }) =>
    call<Site>("", "POST", body),

  structure: (id: string) => call<Structure>(`/${id}/structure`),

  operatorLayout: (id: string) =>
    call<Structure>(`/${encodeURIComponent(id)}/operator-layout`),

  async operationalLayout(
    id: string,
    opts?: OperationalLayoutOptions,
  ): Promise<OperationalLayoutProjection> {
    const fixture = await operationalLayoutFixture()
    if (fixture) {
      if (fixture.siteId !== id) throw new Error("Fixture site ID does not match the requested parking lot.")
      return fixture
    }

    const query: string[] = []
    if (opts?.vehicleType) query.push(`vehicleType=${encodeURIComponent(opts.vehicleType)}`)
    if (opts?.floor) query.push(`floor=${encodeURIComponent(opts.floor)}`)
    const suffix = query.length ? `?${query.join("&")}` : ""

    return call<OperationalLayoutProjection>(
      `/${encodeURIComponent(id)}/operational-layout${suffix}`,
    )
  },

  markBackup: (siteId: string, slotId: string, reason: string) =>
    call<void>(
      `/${encodeURIComponent(siteId)}/slots/${encodeURIComponent(slotId)}/backup`,
      "POST",
      { reason },
    ),

  releaseBackup: (siteId: string, slotId: string) =>
    call<void>(
      `/${encodeURIComponent(siteId)}/slots/${encodeURIComponent(slotId)}/backup`,
      "DELETE",
    ),

  unit: (
    id: string,
    body: {
      type: string
      name: string
      parentId: string | null
      capacity: number
    },
  ) => call<string>(`/${id}/units`, "POST", body),

  slot: (
    id: string,
    body: {
      unitId: string
      code: string
      vehicleType?: string
      vehicleTypes?: string[]
      primaryVehicleType?: string
      type?: string
    },
  ) => call<string>(`/${encodeURIComponent(id)}/slots`, "POST", body),

  path: (
    id: string,
    body: { code: string; from: string; to: string },
  ) => call<string>(`/${encodeURIComponent(id)}/paths`, "POST", body),

  maintenanceWindows: (id: string) =>
    call<MaintenanceWindow[]>(
      `/${encodeURIComponent(id)}/maintenance-windows`,
    ),

  createMaintenanceWindow: (
    id: string,
    body: {
      start: string
      end: string
      reason: string
      unitId?: string
      slotId?: string
    },
  ) =>
    call<MaintenanceWindow>(
      `/${encodeURIComponent(id)}/maintenance-windows`,
      "POST",
      body,
    ),

  extendMaintenanceWindow: (id: string, windowId: string, end: string) =>
    call<MaintenanceWindow>(
      `/${encodeURIComponent(id)}/maintenance-windows/${encodeURIComponent(windowId)}`,
      "PATCH",
      { end },
    ),

  cancelMaintenanceWindow: (id: string, windowId: string) =>
    call<void>(
      `/${encodeURIComponent(id)}/maintenance-windows/${encodeURIComponent(windowId)}`,
      "DELETE",
    ),

  /**
   * Same-lot, all-or-nothing deactivation. One call, one site gate, one audit record:
   * N independent single-action calls cannot express "all selected or none".
   */
  async batch(id: string, items: BatchDeactivationItem[]) {
    if (items.length < 1 || items.length > 50)
      throw new StructureApiError(
        "A batch must contain 1–50 items.",
        400,
        "INVALID_STRUCTURE",
      )

    const body = { items }
    const storageKey = `sp_structure_batch_${id}_${JSON.stringify(body)}`
    const idempotencyKey =
      sessionStorage.getItem(storageKey) ?? crypto.randomUUID()
    sessionStorage.setItem(storageKey, idempotencyKey)

    try {
      const result = await request<
        Response<{
          operationId: string
          applied: Array<{ resourceId: string; action: string }>
        }>
      >(
        `/${encodeURIComponent(id)}/structure/batch`,
        "POST",
        body,
        "/api/parking-lots",
        server,
        { "Idempotency-Key": idempotencyKey },
      )

      if (!result.success)
        throw new StructureApiError(
          "The batch was not applied.",
          409,
          "STRUCTURE_CONFLICT",
        )

      sessionStorage.removeItem(storageKey)
      return result.data
    } catch (error) {
      structureError(error)
    }
  },

  async edit(id: string, body: Record<string, unknown>) {
    if (
      [
        "removeSlot",
        "removeUnit",
        "capacity",
        "active",
        "moveSlot",
        "configureSlot",
      ].includes(String(body.action))
    ) {
      const storageKey = `sp_structure_operation_${id}_${JSON.stringify(body)}`

      const idempotencyKey =
        sessionStorage.getItem(storageKey) ?? crypto.randomUUID()
      sessionStorage.setItem(storageKey, idempotencyKey)

      try {
        const result = await request<Response<{
          id: string
          status: string
          reason?: string
          impact?: string
        }>>(
          `/${encodeURIComponent(id)}/operations`,
          "POST",
          { idempotencyKey, edit: body },
          "/api/parking-lots",
          server,
        )

        if (result.data.status !== "completed")
          throw new StructureApiError(
            `Operation ${result.data.id}: ${result.data.status}. ${result.data.reason ?? ""}`.trim(),
            202,
          )

        sessionStorage.removeItem(storageKey)
        return
      } catch (error) {
        structureError(error)
      }
    }

    // Restore actions and the remaining edits are PATCH /structure actions.
    await call<void>(`/${encodeURIComponent(id)}/structure`, "PATCH", body)
  },
}

export interface Operator {
  id: string
  fullName: string
  email: string
  status: string
  siteIds: string[]
  permissions: string[]
  deliveryId?: string
  deliveryStatus: string
}

export interface OperatorAssignment {
  siteId: string
  tenantId: string
  permissions: string[]
}

export const operatorsApi = {
  async reissueInvitation(id: string) {
    return request(`/${encodeURIComponent(id)}/invitation`, "POST", undefined, "/api/operators")
  },
  async assignments() {
    const assignments = (
      await request<Response<OperatorAssignment[]>>(
        "/me",
        "GET",
        undefined,
        "/api/operators",
      )
    ).data
    if (!Array.isArray(assignments) || assignments.some((assignment) =>
      !assignment || typeof assignment.siteId !== "string" || typeof assignment.tenantId !== "string" ||
      !Array.isArray(assignment.permissions))) {
      throw new Error("The assignment service returned an invalid site list. Please try again.")
    }
    return assignments
  },

  async list() {
    return (
      await request<Response<Operator[]>>(
        "",
        "GET",
        undefined,
        "/api/operators",
      )
    ).data
  },

  async create(body: {
    fullName: string
    email: string
    siteIds: string[]
    permissions: string[]
  }) {
    const result = await request<Response<Operator>>(
      "",
      "POST",
      body,
      "/api/users",
    )

    if (!result.success) throw new Error("Operator creation failed.")

    return result.data
  },


  async remove(operatorId: string) {
    await request<void>(`/${operatorId}`, "DELETE", undefined, "/api/operators")
  },
}
