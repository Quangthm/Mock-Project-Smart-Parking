import { useEffect, useRef, useState } from "react"
import * as maptilersdk from "@maptiler/sdk"
import "@maptiler/sdk/dist/maptiler-sdk.css"
import { useApp } from "../../context/AppContext"

export type ParkingCoordinates = { lat: number; lng: number }

type ReverseGeocodingFeature = {
  place_name: string
  place_type?: string[]
}

type LocationSearchResult = {
  center?: [number, number]
  place_name?: string
  place_type?: string[]
}

export function validParkingCoordinates(point: ParkingCoordinates | null | undefined): point is ParkingCoordinates {
  return !!point && Number.isFinite(point.lat) && Number.isFinite(point.lng) &&
    Math.abs(point.lat) <= 90 && Math.abs(point.lng) <= 180 &&
    !(point.lat === 0 && point.lng === 0)
}

export function ParkingLocationPicker({ name, address, center, initial, onConfirm, onCancel }: {
  name: string
  address: string
  center: ParkingCoordinates
  initial?: ParkingCoordinates | null
  onConfirm: (point: ParkingCoordinates) => void
  onCancel: () => void
}) {
  const { theme } = useApp()
  const mapElement = useRef<HTMLDivElement>(null)
  const mapRef = useRef<maptilersdk.Map | null>(null)
  const markerRef = useRef<maptilersdk.Marker | null>(null)
  const findAbort = useRef<AbortController | null>(null)
  const appliedTheme = useRef(theme)
  const [latitude, setLatitude] = useState(validParkingCoordinates(initial) ? String(initial.lat) : "")
  const [longitude, setLongitude] = useState(validParkingCoordinates(initial) ? String(initial.lng) : "")
  const [nearbyAddress, setNearbyAddress] = useState("")
  const [lookupStatus, setLookupStatus] = useState<"idle" | "loading" | "found" | "unavailable">("idle")
  const [finding, setFinding] = useState(false)
  const [findMessage, setFindMessage] = useState("")
  const [searchQuery, setSearchQuery] = useState(name.trim() || address.split(",")[0]?.trim() || "")
  const [searchResults, setSearchResults] = useState<LocationSearchResult[]>([])
  const apiKey = import.meta.env.VITE_MAPTILER_API_KEY as string | undefined
  const candidate = latitude.trim() && longitude.trim()
    ? { lat: Number(latitude), lng: Number(longitude) }
    : null
  const confirmedCandidate = validParkingCoordinates(candidate) ? candidate : null

  useEffect(() => {
    if (!apiKey || !mapElement.current) return
    maptilersdk.config.apiKey = apiKey
    const start = validParkingCoordinates(initial) ? initial : center
    const style = theme === "dark" ? "streets-v4-dark" : "streets-v4"
    const map = new maptilersdk.Map({
      container: mapElement.current,
      style: `https://api.maptiler.com/maps/${style}/style.json?key=${encodeURIComponent(apiKey)}`,
      center: [start.lng, start.lat],
      zoom: validParkingCoordinates(initial) ? 16 : 12,
      navigationControl: true,
      geolocateControl: false,
      fullscreenControl: false,
    })
    mapRef.current = map
    map.on("click", event => {
      setLatitude(event.lngLat.lat.toFixed(6))
      setLongitude(event.lngLat.lng.toFixed(6))
      setFindMessage("Pin moved. Confirm that it marks the vehicle entrance.")
    })
    return () => {
      findAbort.current?.abort()
      markerRef.current?.remove()
      markerRef.current = null
      map.remove()
      mapRef.current = null
    }
  }, [])

  useEffect(() => {
    if (!apiKey || !mapRef.current || appliedTheme.current === theme) return
    const style = theme === "dark" ? "streets-v4-dark" : "streets-v4"
    mapRef.current.setStyle(`https://api.maptiler.com/maps/${style}/style.json?key=${encodeURIComponent(apiKey)}`)
    appliedTheme.current = theme
  }, [apiKey, theme])

  useEffect(() => {
    if (!mapRef.current) return
    if (!confirmedCandidate) {
      markerRef.current?.remove()
      markerRef.current = null
      return
    }
    if (!markerRef.current) {
      const marker = new maptilersdk.Marker({ color: "#16b88a", draggable: true })
        .setLngLat([confirmedCandidate.lng, confirmedCandidate.lat]).addTo(mapRef.current)
      marker.on("dragend", () => {
        const point = marker.getLngLat()
        setLatitude(point.lat.toFixed(6))
        setLongitude(point.lng.toFixed(6))
        setFindMessage("Pin moved. Confirm that it marks the vehicle entrance.")
      })
      markerRef.current = marker
    } else markerRef.current.setLngLat([confirmedCandidate.lng, confirmedCandidate.lat])
  }, [latitude, longitude])

  useEffect(() => {
    if (!apiKey || !confirmedCandidate) {
      setNearbyAddress("")
      setLookupStatus("idle")
      return
    }
    const controller = new AbortController()
    const point = confirmedCandidate
    setNearbyAddress("")
    setLookupStatus("loading")
    const timeout = window.setTimeout(async () => {
      try {
        const url = `https://api.maptiler.com/geocoding/${point.lng},${point.lat}.json?${new URLSearchParams({ key: apiKey, language: "vi" })}`
        const response = await fetch(url, { signal: controller.signal })
        if (!response.ok) throw new Error("Reverse geocoding unavailable")
        const result = await response.json() as { features?: ReverseGeocodingFeature[] }
        if (controller.signal.aborted) return
        const features = (result.features ?? []).filter(feature => feature?.place_name)
        const nearby = features.find(feature => feature.place_type?.includes("address"))
          ?? features.find(feature => feature.place_type?.includes("road"))
          ?? features[0]
        setNearbyAddress(nearby?.place_name ?? "")
        setLookupStatus(nearby ? "found" : "unavailable")
      } catch {
        if (!controller.signal.aborted) setLookupStatus("unavailable")
      }
    }, 350)
    return () => {
      window.clearTimeout(timeout)
      controller.abort()
    }
  }, [apiKey, latitude, longitude])

  async function findAddress() {
    if (!apiKey || !searchQuery.trim()) return
    findAbort.current?.abort()
    const controller = new AbortController()
    findAbort.current = controller
    setFinding(true)
    setFindMessage("")
    setSearchResults([])
    try {
      const parameters = new URLSearchParams({
        key: apiKey,
        country: "vn",
        proximity: `${center.lng},${center.lat}`,
        language: "vi",
        types: "poi,address",
        limit: "10",
      })
      const response = await fetch(`https://api.maptiler.com/geocoding/${encodeURIComponent(searchQuery.trim())}.json?${parameters}`, { signal: controller.signal })
      if (!response.ok) throw new Error("Place search is unavailable. Place the entrance pin manually.")
      const result = await response.json() as { features?: LocationSearchResult[] }
      if (controller.signal.aborted) return
      const matches = (result.features ?? []).filter(feature => feature.center && feature.place_name &&
        validParkingCoordinates({ lat: feature.center[1], lng: feature.center[0] }) &&
        feature.place_type?.some(type => type === "poi" || type === "address"))
      setSearchResults(matches)
      if (!matches.length) setFindMessage("No mapped place found. Try its building name or street address, or place the entrance pin manually.")
    } catch (error) {
      if (!controller.signal.aborted) setFindMessage(error instanceof Error ? error.message : "Place search failed. Move the map manually.")
    } finally {
      if (!controller.signal.aborted) setFinding(false)
    }
  }

  return <div className="rounded-2xl border border-[var(--border)] bg-[var(--card)] p-4 space-y-3" aria-label="Confirm parking entrance on the map">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <div><h3 className="font-semibold text-[var(--fg)]">Confirm the parking entrance</h3><p className="text-sm text-[var(--muted)]">Search by parking site, building, or street address. Check the result, then place the pin at the vehicle entrance.</p></div>
    </div>
    {apiKey && <div className="flex flex-wrap gap-2">
      <input className="input min-w-0 flex-1" aria-label="Search place or address" placeholder="Parking site, building, or street address" value={searchQuery} onChange={event => { findAbort.current?.abort(); setFinding(false); setSearchQuery(event.target.value); setSearchResults([]); setFindMessage("") }} onKeyDown={event => { if (event.key === "Enter") { event.preventDefault(); void findAddress() } }} />
      <button type="button" className="btn-outline" disabled={finding || !searchQuery.trim()} onClick={() => void findAddress()}>{finding ? "Finding…" : "Find Address"}</button>
    </div>}
    {searchResults.length > 0 && <div className="max-h-48 space-y-1 overflow-y-auto rounded-xl border border-[var(--border)] p-2" aria-label="Place search results">
      {searchResults.map((result, index) => <button key={`${result.place_name}-${index}`} type="button" className="block min-h-11 w-full rounded-lg px-3 py-2 text-left text-sm text-[var(--fg)] hover:bg-[var(--bg)] focus-visible:outline-2 focus-visible:outline-[var(--primary)]" onClick={() => {
        if (!result.center || !mapRef.current) return
        mapRef.current.flyTo({ center: result.center, zoom: 17, essential: false })
        setLatitude(result.center[1].toFixed(6))
        setLongitude(result.center[0].toFixed(6))
        setSearchResults([])
        setFindMessage(`Pin placed at ${result.place_name}. Check the vehicle entrance before confirming.`)
      }}>{result.place_name}</button>)}
    </div>}
    {apiKey ? <div ref={mapElement} className="h-[280px] w-full overflow-hidden rounded-xl border border-[var(--border)] sm:h-[340px]" aria-label="Parking location map" /> : <p className="text-sm text-[var(--muted)]">Map preview is unavailable. Enter verified coordinates below.</p>}
    {findMessage && <p role="status" className="text-sm text-[var(--muted)]">{findMessage}</p>}
    <div role="status" className="rounded-xl border border-[var(--border)] bg-[var(--bg)] p-3 text-sm text-[var(--fg)]">
      <p className="break-words"><strong>Entered address:</strong> {address || "Not provided"}</p>
      <p className="mt-1 break-words"><strong>Near the pin:</strong> {lookupStatus === "loading" ? "Identifying nearby address…" : lookupStatus === "found" ? nearbyAddress : lookupStatus === "unavailable" ? "No mapped address available here. You can still confirm the pin." : "Click the map to place a pin."}</p>
      <p className="mt-1 text-[var(--muted)]">The nearby address is map data for reference. Your entered address will not be replaced.</p>
    </div>
    <div className="grid gap-3 sm:grid-cols-2">
      <label className="text-sm font-medium text-[var(--fg)]">Latitude<input className="input mt-1" type="number" step="any" min={-90} max={90} value={latitude} onChange={event => setLatitude(event.target.value)} placeholder="e.g. 10.776900" /></label>
      <label className="text-sm font-medium text-[var(--fg)]">Longitude<input className="input mt-1" type="number" step="any" min={-180} max={180} value={longitude} onChange={event => setLongitude(event.target.value)} placeholder="e.g. 106.700900" /></label>
    </div>
    <div className="flex flex-wrap justify-end gap-3">
      <button type="button" className="btn-outline" onClick={onCancel}>Cancel</button>
      <button type="button" className="btn-primary" disabled={!confirmedCandidate} onClick={() => { if (validParkingCoordinates(candidate)) onConfirm(candidate) }}>Use this position</button>
    </div>
  </div>
}
