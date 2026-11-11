export type LatLng = { lat: number; lng: number }

export function validCoordinates(point: LatLng) {
  return Number.isFinite(point.lat) && Number.isFinite(point.lng) && Math.abs(point.lat) <= 90 && Math.abs(point.lng) <= 180 && !(point.lat === 0 && point.lng === 0)
}

export function distanceKm(from: LatLng, to: LatLng) {
  const radians = (degrees: number) => degrees * Math.PI / 180
  const latDistance = radians(to.lat - from.lat)
  const lngDistance = radians(to.lng - from.lng)
  const a = Math.min(1, Math.sin(latDistance / 2) ** 2 + Math.cos(radians(from.lat)) * Math.cos(radians(to.lat)) * Math.sin(lngDistance / 2) ** 2)
  return 6371 * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a))
}
