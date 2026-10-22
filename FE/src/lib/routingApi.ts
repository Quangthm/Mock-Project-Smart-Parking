export type RoutePoint = { lat: number; lng: number }

export type DrivingRoute = {
  coordinates: [number, number][]
  distanceKm: number
  durationMinutes: number
}

// The public Valhalla endpoint is suitable for this mock project. A dedicated
// routing service can replace it through the Vite environment when deployed.
const routingServer = (import.meta.env.VITE_ROUTING_API_BASE_URL ?? "https://valhalla1.openstreetmap.de").replace(/\/$/, "")

export async function getDrivingRoute(from: RoutePoint, to: RoutePoint, signal: AbortSignal): Promise<DrivingRoute> {
  const request = {
    locations: [{ lat: from.lat, lon: from.lng }, { lat: to.lat, lon: to.lng }],
    costing: "auto",
    format: "osrm",
    shape_format: "geojson",
    units: "kilometers",
    directions_type: "none",
  }
  const response = await fetch(`${routingServer}/route?json=${encodeURIComponent(JSON.stringify(request))}`, {
    signal,
    headers: { "X-Client-Id": "smartparking-mock-project" },
  })
  if (!response.ok) throw new Error("The route service is unavailable. Please try again.")
  const body: unknown = await response.json()
  const route = (body as { routes?: Array<{ geometry?: { coordinates?: unknown }; distance?: number; duration?: number }> }).routes?.[0]
  const points = route?.geometry?.coordinates
  if (!Array.isArray(points) || points.length < 2 || !points.every((point) =>
    Array.isArray(point) && point.length >= 2 && Number.isFinite(point[0]) && Number.isFinite(point[1]))) {
    throw new Error("No driving route was found for this parking lot.")
  }
  return {
    coordinates: points.map((point) => [point[0], point[1]] as [number, number]),
    distanceKm: (route?.distance ?? 0) / 1000,
    durationMinutes: (route?.duration ?? 0) / 60,
  }
}
