import { request } from "./authApi"

export type OriginSource = "CURRENT_GPS" | "NETWORK" | "MANUAL"

export interface DiscoverySearchParams {
  originSource: OriginSource
  latitude: number
  longitude: number
  manualOrigin?: string
  radiusKm?: number
  vehicleType?: "CAR" | "MOTORCYCLE" | "OVERSIZED"
  lotType?: "OUTDOOR" | "BASEMENT" | "MULTI_STOREY"
  minPrice?: number
  maxPrice?: number
  currency?: string
  availabilityOnly?: boolean
  intervalStart?: string
  intervalEnd?: string
  sort?: "DISTANCE" | "PRICE" | "AVAILABILITY"
  limit?: number
  offset?: number
}

export interface DiscoveryCoordinates { latitude: number; longitude: number }
export interface DiscoveryAvailability { status: string; availableCount: number; vehicleType: string }
export interface DiscoveryTariff { tariffRef: string; billingUnit: string; currentAmountMinor: number; currency: string }
export interface DiscoveryHandoff { siteId: string; layoutApiPath: string }
export interface DiscoveryFreshness { generatedAt: string; isStale: boolean; stalenessThresholdSeconds: number; source: string }
export interface DiscoveryLotItem {
  siteId: string
  siteCode: string
  siteName: string
  addressLine: string
  coordinates: DiscoveryCoordinates
  lotType: string
  distanceMeters: number
  availability: DiscoveryAvailability
  tariff: DiscoveryTariff
  directions: { handoff: string }
  handoff: DiscoveryHandoff
  freshness: DiscoveryFreshness
}
export interface DiscoveryPage { limit: number; offset: number; returned: number; total: number; hasMore: boolean }
export interface DiscoveryResponse { items: DiscoveryLotItem[]; page: DiscoveryPage }
export interface DiscoveryHandoffContext { siteId: string; siteCode: string; layoutApiPath: string }

type DiscoveryEnvelope = { success: boolean; data: DiscoveryResponse }

/**
 * Serializes discovery search parameters into a deterministic REST query string.
 * `originSource`, `latitude` and `longitude` are always emitted; every other
 * parameter is emitted only when it is defined. A `MANUAL` origin without a
 * `manualOrigin` is a caller error and throws rather than querying blindly.
 */
export function buildDiscoveryQuery(params: DiscoverySearchParams): string {
  if (params.originSource === "MANUAL" && (params.manualOrigin ?? "").trim() === "") {
    throw new Error("A manual origin requires a manualOrigin value.")
  }

  const parts: string[] = []
  const add = (key: string, value: string | number | boolean | undefined) => {
    if (value === undefined || value === null) return
    parts.push(`${key}=${encodeURIComponent(String(value))}`)
  }

  add("originSource", params.originSource)
  add("latitude", params.latitude)
  add("longitude", params.longitude)
  add("manualOrigin", params.manualOrigin)
  add("radiusKm", params.radiusKm)
  add("vehicleType", params.vehicleType)
  add("lotType", params.lotType)
  add("minPrice", params.minPrice)
  add("maxPrice", params.maxPrice)
  add("currency", params.currency)
  add("availabilityOnly", params.availabilityOnly)
  add("intervalStart", params.intervalStart)
  add("intervalEnd", params.intervalEnd)
  add("sort", params.sort)
  add("limit", params.limit)
  add("offset", params.offset)

  return parts.join("&")
}

/**
 * Offline fixture mode short-circuits the network. Otherwise the real envelope
 * is fetched through the shared `request` helper, mirroring `parkingApi`.
 */
export async function searchDiscoveryLots(params: DiscoverySearchParams): Promise<DiscoveryResponse> {
  if (import.meta.env.VITE_DISCOVERY_FIXTURE === "true") {
    const module = (await import("../fixtures/driver-discovery-fixture.json")) as {
      default?: DiscoveryEnvelope
    } & Partial<DiscoveryEnvelope>
    const envelope = (module.default ?? module) as DiscoveryEnvelope
    return envelope.data
  }

  const serverUrl = (
    import.meta.env.VITE_DISCOVERY_API_BASE_URL ?? "http://localhost:5045"
  ).replace(/\/$/, "")

  const result = await request<DiscoveryEnvelope>(
    "/parking-lots?" + buildDiscoveryQuery(params),
    "GET",
    undefined,
    "/api/discovery",
    serverUrl,
  )

  return result.data
}

/**
 * Deterministic external directions deep link. `destinationName` is accepted for
 * call-site symmetry but deliberately not embedded so the URL stays stable.
 */
export function buildExternalDirectionsUrl(latitude: number, longitude: number, destinationName?: string): string {
  return `https://www.google.com/maps/dir/?api=1&destination=${latitude},${longitude}`
}

/**
 * Builds the cross-boundary handoff for a discovered lot. `siteId` is copied
 * verbatim so the layout service resolves the authoritative site.
 */
export function createHandoffContext(lot: DiscoveryLotItem): DiscoveryHandoffContext {
  return {
    siteId: lot.siteId,
    siteCode: lot.siteCode,
    layoutApiPath: "/api/parking-lots/" + encodeURIComponent(lot.siteId) + "/operational-layout",
  }
}
