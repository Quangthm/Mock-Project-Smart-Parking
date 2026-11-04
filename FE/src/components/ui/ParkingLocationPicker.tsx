import { useEffect, useRef, useState } from "react"
import * as maptilersdk from "@maptiler/sdk"
import "@maptiler/sdk/dist/maptiler-sdk.css"
import { useApp } from "../../context/AppContext"

export type ParkingCoordinates = { lat: number; lng: number }

export function validParkingCoordinates(point: ParkingCoordinates | null | undefined): point is ParkingCoordinates {
  return !!point && Number.isFinite(point.lat) && Number.isFinite(point.lng) &&
    Math.abs(point.lat) <= 90 && Math.abs(point.lng) <= 180 &&
    !(point.lat === 0 && point.lng === 0)
}

export function ParkingLocationPicker({ address, center, initial, onConfirm, onCancel }: {
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
  const searchAbort = useRef<AbortController | null>(null)
  const appliedTheme = useRef(theme)
  const [latitude, setLatitude] = useState(validParkingCoordinates(initial) ? String(initial.lat) : "")
  const [longitude, setLongitude] = useState(validParkingCoordinates(initial) ? String(initial.lng) : "")
  const [searching, setSearching] = useState(false)
  const [notice, setNotice] = useState("")
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
      setNotice("Review the pin, then confirm this parking entrance location.")
    })
    return () => {
      searchAbort.current?.abort()
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
      })
      markerRef.current = marker
    } else markerRef.current.setLngLat([confirmedCandidate.lng, confirmedCandidate.lat])
  }, [latitude, longitude])

  async function findAddress() {
    if (!apiKey || !address.trim()) return
    searchAbort.current?.abort()
    const controller = new AbortController()
    searchAbort.current = controller
    setSearching(true)
    setNotice("")
    try {
      const url = `https://api.maptiler.com/geocoding/${encodeURIComponent(address.trim())}.json?key=${encodeURIComponent(apiKey)}&limit=1`
      const response = await fetch(url, { signal: controller.signal })
      if (!response.ok) throw new Error("Address search is unavailable. Place the pin on the map instead.")
      const result = await response.json() as { features?: Array<{ center?: [number, number]; place_name?: string }> }
      const found = result.features?.[0]
      if (!found?.center || !Number.isFinite(found.center[0]) || !Number.isFinite(found.center[1])) {
        setNotice("No exact address match was found. Place the pin on the map instead.")
        return
      }
      const [lng, lat] = found.center
      setLatitude(lat.toFixed(6))
      setLongitude(lng.toFixed(6))
      mapRef.current?.flyTo({ center: [lng, lat], zoom: 17, essential: false })
      setNotice(`Suggested location: ${found.place_name || address}. Check the pin before confirming.`)
    } catch (error) {
      if (!controller.signal.aborted) setNotice(error instanceof Error ? error.message : "Address search failed. Place the pin manually.")
    } finally {
      if (!controller.signal.aborted) setSearching(false)
    }
  }

  return <div className="rounded-2xl border border-[var(--border)] bg-[var(--card)] p-4 space-y-3" aria-label="Confirm parking entrance on the map">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <div><h3 className="font-semibold text-[var(--fg)]">Confirm the parking entrance</h3><p className="text-sm text-[var(--muted)]">Search the address, then move the pin to the exact entrance. Click the map or drag the pin.</p></div>
      {apiKey && <button type="button" className="btn-outline" disabled={searching || !address.trim()} onClick={() => void findAddress()}>{searching ? "Finding…" : "Find address"}</button>}
    </div>
    {apiKey ? <div ref={mapElement} className="h-[280px] w-full overflow-hidden rounded-xl border border-[var(--border)] sm:h-[340px]" aria-label="Parking location map" /> : <p className="text-sm text-[var(--muted)]">Map preview is unavailable. Enter verified coordinates below.</p>}
    {notice && <p role="status" className="text-sm text-[var(--muted)]">{notice}</p>}
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
