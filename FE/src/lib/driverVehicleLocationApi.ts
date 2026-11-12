import { request, AuthApiError } from "./authApi"

const server = (import.meta.env.VITE_VEHICLE_LOCATION_API_BASE_URL ?? "http://localhost:5045").replace(/\/$/, "")

export type LocationPrecision = "SLOT" | "ZONE" | "UNKNOWN"
export type AllocationStatus = "RESERVED" | "OCCUPIED"
export type VehicleLocationScenario = "slotPrecision" | "zonePrecision" | "unknownPrecision" | "staleVariant"

export interface RecordedLocation {
  precision: LocationPrecision
  slotId: string | null
  slotCode: string | null
  zoneId: string | null
  zoneCode: string | null
  floorCode: string | null
}
export interface LocationFreshness {
  generatedAt: string
  isStale: boolean
  stalenessThresholdSeconds: number
  source: string
}
export interface VehicleLocationData {
  sessionId: string
  siteId: string
  siteCode: string
  allocationStatus: AllocationStatus | null
  recordedLocation: RecordedLocation
  recordedAt: string | null
  freshness: LocationFreshness
}
export type VehicleLocationEnvelope = { success: boolean; data: VehicleLocationData }

export function normalizeAllocationStatus(status: string | null | undefined): AllocationStatus | null {
  if (status === "RESERVED") return "RESERVED"
  if (status === "OCCUPIED") return "OCCUPIED"
  return null
}

export function normalizeRecordedLocation(location: RecordedLocation): RecordedLocation {
  if (location.precision === "SLOT") {
    return {
      precision: "SLOT",
      slotId: location.slotId ?? null,
      slotCode: location.slotCode ?? null,
      zoneId: location.zoneId ?? null,
      zoneCode: location.zoneCode ?? null,
      floorCode: location.floorCode ?? null,
    }
  }

  if (location.precision === "ZONE") {
    return {
      precision: "ZONE",
      slotId: null,
      slotCode: null,
      zoneId: location.zoneId ?? null,
      zoneCode: location.zoneCode ?? null,
      floorCode: location.floorCode ?? null,
    }
  }

  return {
    precision: "UNKNOWN",
    slotId: null,
    slotCode: null,
    zoneId: null,
    zoneCode: null,
    floorCode: null,
  }
}

export function locationDetailLines(location: RecordedLocation): string[] {
  if (location.precision === "SLOT") {
    const lines: string[] = []
    if (location.floorCode) lines.push(`Tầng: ${location.floorCode}`)
    if (location.zoneCode) lines.push(`Khu vực: ${location.zoneCode}`)
    if (location.slotCode) lines.push(`Số ô đỗ: ${location.slotCode}`)
    return lines
  }

  if (location.precision === "ZONE") {
    const lines: string[] = []
    if (location.floorCode) lines.push(`Tầng: ${location.floorCode}`)
    if (location.zoneCode) lines.push(`Khu vực: ${location.zoneCode}`)
    return lines
  }

  return []
}

export function allocationStatusLabel(status: AllocationStatus | null): string | null {
  if (status === "OCCUPIED") return "Đang đỗ (OCCUPIED)"
  if (status === "RESERVED") return "Đã đặt trước (RESERVED)"
  return null
}

function normalizeData(data: VehicleLocationData): VehicleLocationData {
  return {
    ...data,
    allocationStatus: normalizeAllocationStatus(data.allocationStatus),
    recordedLocation: normalizeRecordedLocation(data.recordedLocation),
  }
}

export async function getCurrentVehicleLocation(
  fixtureScenario: VehicleLocationScenario = "slotPrecision",
): Promise<VehicleLocationData | null> {
  if (import.meta.env.VITE_VEHICLE_LOCATION_FIXTURE === "true") {
    const module = (await import("../fixtures/driver-vehicle-location-fixture.json")) as {
      default?: Record<VehicleLocationScenario, VehicleLocationEnvelope>
    } & Partial<Record<VehicleLocationScenario, VehicleLocationEnvelope>>
    const fixtures = (module.default ?? module) as Record<VehicleLocationScenario, VehicleLocationEnvelope>
    return normalizeData(fixtures[fixtureScenario].data)
  }

  try {
    const envelope = await request<VehicleLocationEnvelope>(
      "/current/location",
      "GET",
      undefined,
      "/api/sessions",
      server,
    )
    if (envelope.success === false) {
      throw new Error("Vehicle location request failed.")
    }
    return normalizeData(envelope.data)
  } catch (error) {
    if (error instanceof AuthApiError && error.status === 404) return null
    throw error
  }
}