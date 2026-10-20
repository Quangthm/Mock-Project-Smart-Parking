import { useEffect, useMemo, useRef, useState } from "react"
import * as maptilersdk from "@maptiler/sdk"
import "@maptiler/sdk/dist/maptiler-sdk.css"
import { driverData as store } from "../data/data"
import type { LotType, ParkingLot } from "../../../lib/types"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { getDrivingRoute, type DrivingRoute } from "../../../lib/routingApi"
import { ParkingDetailsPanel, type VehicleCapacityInfo } from "./ParkingDetailsPanel"

type LatLng = { lat: number; lng: number }
type MapState = "loading" | "ready" | "missing-key" | "error"
type MapStyleName = "streets" | "satellite" | "hybrid"
const mapStyles = { streets: maptilersdk.MapStyle.STREETS, satellite: maptilersdk.MapStyle.SATELLITE, hybrid: maptilersdk.MapStyle.HYBRID }
const lotTypeLabels: Record<LotType, string> = { outdoor: "Outdoor", basement: "Underground", "multi-storey": "Indoor (Multi-storey)" }

function validCoordinates(point: LatLng) {
  return Number.isFinite(point.lat) && Number.isFinite(point.lng) && Math.abs(point.lat) <= 90 && Math.abs(point.lng) <= 180
}
function distanceKm(from: LatLng, to: LatLng) {
  const radians = (degrees: number) => degrees * Math.PI / 180
  const latDistance = radians(to.lat - from.lat)
  const lngDistance = radians(to.lng - from.lng)
  const a = Math.sin(latDistance / 2) ** 2 + Math.cos(radians(from.lat)) * Math.cos(radians(to.lat)) * Math.sin(lngDistance / 2) ** 2
  return 6371 * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a))
}
function availableSpaces(lot: ParkingLot) { return Array.isArray(lot.slots) ? lot.slots.filter((slot) => slot.status === "available").length : 0 }
function hasAvailabilityData(lot: ParkingLot) { return Array.isArray(lot.slots) }
function formatRate(rate: number) { return Number.isFinite(rate) ? `${rate.toLocaleString("vi-VN")} VND` : "Rate unavailable" }
const routeSourceId = "smartparking-driving-route"
const routeLayerId = "smartparking-driving-route-line"

export function FindParking({ onBook, onDetailsOpenChange, visible = true }: {
  onBook: (lot: ParkingLot) => void
  onDetailsOpenChange?: (open: boolean) => void
  visible?: boolean
}) {
  const [sourceLots, setSourceLots] = useState(() => store.getLots())
  const lots = useMemo(() => sourceLots.filter((lot) => lot.status === "active"), [sourceLots])
  const validLots = useMemo(() => lots.filter(validCoordinates), [lots])
  const mapElement = useRef<HTMLDivElement>(null)
  const searchPanelRef = useRef<HTMLElement>(null)
  const detailsPanelRef = useRef<HTMLDivElement>(null)
  const mapInstance = useRef<maptilersdk.Map | null>(null)
  const markersRef = useRef(new Map<string, maptilersdk.Marker>())
  const locationMarkerRef = useRef<maptilersdk.Marker | null>(null)
  const routeRequestRef = useRef<AbortController | null>(null)
  const routeSequenceRef = useRef(0)
  const [selected, setSelected] = useState<ParkingLot | null>(null)
  const [detailsOpen, setDetailsOpen] = useState(false)
  const [searchCollapsed, setSearchCollapsed] = useState(false)
  const [coordinateError, setCoordinateError] = useState("")
  const vehicleCapacity: VehicleCapacityInfo = { status: "unavailable", car: null, motorcycle: null }
  const [query, setQuery] = useState("")
  const [availableOnly, setAvailableOnly] = useState(false)
  const [sortByPrice, setSortByPrice] = useState(false)
  const [maxPrice, setMaxPrice] = useState<number | null>(null)
  const [maxDistance, setMaxDistance] = useState<number | null>(null)
  const [lotType, setLotType] = useState<LotType | "all">("all")
  const [location, setLocation] = useState<LatLng | null>(null)
  const [mapState, setMapState] = useState<MapState>("loading")
  const [mapStyle, setMapStyle] = useState<MapStyleName>("streets")
  const [route, setRoute] = useState<DrivingRoute | null>(null)
  const [routeStatus, setRouteStatus] = useState<"idle" | "locating" | "loading" | "ready" | "error">("idle")
  const [routeError, setRouteError] = useState("")
  const parkingCenter = useMemo(() => validLots.length ? {
    lat: validLots.reduce((sum, lot) => sum + lot.lat, 0) / validLots.length,
    lng: validLots.reduce((sum, lot) => sum + lot.lng, 0) / validLots.length,
  } : null, [validLots])
  const priceOptions = useMemo(() => [...new Set(lots.map((lot) => lot.hourlyRate).filter(Number.isFinite))].sort((a, b) => a - b), [lots])
  const results = useMemo(() => {
    const search = query.trim().toLocaleLowerCase()
    return lots.filter((lot) => (!search || `${lot.name} ${lot.address}`.toLocaleLowerCase().includes(search)) &&
      (!availableOnly || (hasAvailabilityData(lot) && availableSpaces(lot) > 0)) &&
      (lotType === "all" || lot.type === lotType) &&
      (maxPrice === null || (Number.isFinite(lot.hourlyRate) && lot.hourlyRate <= maxPrice)) &&
      (maxDistance === null || (location && validCoordinates(lot) && distanceKm(location, lot) <= maxDistance)))
      .map((lot) => ({ lot, distance: location && validCoordinates(lot) ? distanceKm(location, lot) : null }))
      .sort((a, b) => sortByPrice ? (Number.isFinite(a.lot.hourlyRate) ? a.lot.hourlyRate : Infinity) - (Number.isFinite(b.lot.hourlyRate) ? b.lot.hourlyRate : Infinity) :
        location ? (a.distance ?? Infinity) - (b.distance ?? Infinity) : a.lot.name.localeCompare(b.lot.name))
  }, [availableOnly, location, lotType, lots, maxDistance, maxPrice, query, sortByPrice])

  function cameraPadding() {
    const mobile = window.matchMedia("(max-width: 900px)").matches
    const searchRect = searchPanelRef.current?.getBoundingClientRect()
    const detailsRect = detailsPanelRef.current?.getBoundingClientRect()
    return mobile
      ? { top: searchCollapsed ? 68 : (searchRect?.height ?? 0) + 24, right: 28, bottom: detailsOpen ? (detailsRect?.height ?? 0) + 24 : 68, left: 28 }
      : { top: 70, right: detailsOpen ? (detailsRect?.width ?? 0) + 32 : 55, bottom: 70, left: searchCollapsed ? 68 : (searchRect?.width ?? 0) + 32 }
  }

  useEffect(() => { onDetailsOpenChange?.(detailsOpen) }, [onDetailsOpenChange, detailsOpen])
  useEffect(() => {
    const refresh = () => setSourceLots(store.getLots())
    window.addEventListener("sp-data-change", refresh)
    return () => window.removeEventListener("sp-data-change", refresh)
  }, [])
  useEffect(() => {
    if (!selected) return
    const latest = lots.find((lot) => lot.id === selected.id)
    if (!latest) {
      closeDirections()
      setSelected(null)
      setDetailsOpen(false)
    } else if (latest !== selected) {
      closeDirections()
      setSelected(latest)
    }
  }, [lots])
  useEffect(() => () => {
    routeSequenceRef.current += 1
    routeRequestRef.current?.abort()
  }, [])
  useEffect(() => {
    if (!visible) return
    const frame = requestAnimationFrame(() => mapInstance.current?.resize())
    const timer = window.setTimeout(() => mapInstance.current?.resize(), 260)
    return () => { cancelAnimationFrame(frame); window.clearTimeout(timer) }
  }, [visible, searchCollapsed, detailsOpen])
  useEffect(() => {
    const apiKey = import.meta.env.VITE_MAPTILER_API_KEY
    if (!apiKey) { setMapState("missing-key"); return }
    if (!mapElement.current) return
    let cancelled = false
    try {
      maptilersdk.config.apiKey = apiKey
      const map = new maptilersdk.Map({
        container: mapElement.current, style: mapStyles.streets,
        ...(parkingCenter ? { center: [parkingCenter.lng, parkingCenter.lat] as [number, number], zoom: 13 } : {}),
        navigationControl: true, geolocateControl: true, fullscreenControl: true,
      })
      mapInstance.current = map
      map.on("load", () => {
        if (cancelled) return
        setMapState("ready")
      })
      map.on("error", () => { if (!cancelled && !map.loaded()) setMapState("error") })
    } catch { if (!cancelled) setMapState("error") }
    return () => {
      cancelled = true
      markersRef.current.forEach((marker) => marker.remove())
      markersRef.current.clear()
      locationMarkerRef.current?.remove()
      locationMarkerRef.current = null
      mapInstance.current?.remove()
      mapInstance.current = null
      routeRequestRef.current?.abort()
    }
  }, [])
  useEffect(() => {
    if (mapState !== "ready" || !mapInstance.current) return
    const map = mapInstance.current
    markersRef.current.forEach((marker) => marker.remove())
    markersRef.current.clear()
    validLots.forEach((lot) => {
      const marker = new maptilersdk.Marker({ color: !hasAvailabilityData(lot) ? "#64748b" : availableSpaces(lot) ? "#16a34a" : "#dc2626" })
        .setLngLat([lot.lng, lot.lat])
        .setPopup(new maptilersdk.Popup({ offset: 25, closeButton: true }).setText(lot.name))
        .addTo(map)
      const element = marker.getElement()
      element.style.cursor = "pointer"
      element.setAttribute("aria-label", `Select ${lot.name}`)
      element.setAttribute("role", "button")
      element.setAttribute("tabindex", "0")
      element.addEventListener("click", () => selectLot(lot))
      element.addEventListener("keydown", (event) => {
        if (event.key === "Enter" || event.key === " ") { event.preventDefault(); selectLot(lot) }
      })
      markersRef.current.set(lot.id, marker)
    })
    return () => {
      markersRef.current.forEach((marker) => marker.remove())
      markersRef.current.clear()
    }
  }, [mapState, validLots])
  useEffect(() => {
    const element = mapElement.current
    if (!element || !window.ResizeObserver) return
    const observer = new ResizeObserver(() => mapInstance.current?.resize())
    observer.observe(element)
    return () => observer.disconnect()
  }, [])
  useEffect(() => {
    markersRef.current.forEach((marker, id) => marker.getElement().classList.toggle("is-selected-parking-marker", id === selected?.id))
    if (!visible || !selected || !validCoordinates(selected)) return
    const map = mapInstance.current
    if (!map) return
    const reduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches
    map.flyTo({ center: [selected.lng, selected.lat], zoom: Math.max(map.getZoom(), 15),
      padding: cameraPadding(), duration: reduced ? 0 : 850, essential: false })
  }, [selected, mapState, searchCollapsed, detailsOpen, visible])
  useEffect(() => {
    const visibleIds = new Set(results.map(({ lot }) => lot.id))
    markersRef.current.forEach((marker, id) => { marker.getElement().style.display = visibleIds.has(id) || id === selected?.id ? "" : "none" })
  }, [results, selected, mapState])
  useEffect(() => {
    locationMarkerRef.current?.remove()
    locationMarkerRef.current = null
    if (!location || mapState !== "ready" || !mapInstance.current) return
    const marker = new maptilersdk.Marker({ color: "#2563eb" }).setLngLat([location.lng, location.lat]).addTo(mapInstance.current)
    marker.getElement().setAttribute("aria-label", "Your current location")
    locationMarkerRef.current = marker
    return () => { marker.remove(); if (locationMarkerRef.current === marker) locationMarkerRef.current = null }
  }, [location, mapState])
  function closeDirections() {
    routeSequenceRef.current += 1
    routeRequestRef.current?.abort()
    routeRequestRef.current = null
    setRoute(null)
    setRouteStatus("idle")
    setRouteError("")
  }
  function startDirections() {
    if (!selected) return
    if (!validCoordinates(selected)) {
      setRouteStatus("error")
      setRouteError("This parking lot has invalid coordinates. Directions are unavailable.")
      return
    }
    routeRequestRef.current?.abort()
    const controller = new AbortController()
    routeRequestRef.current = controller
    const sequence = ++routeSequenceRef.current
    setRoute(null)
    setRouteStatus("locating")
    setRouteError("")
    if (!navigator.geolocation) {
      setRouteStatus("error")
      setRouteError("Location is unavailable in this browser.")
      return
    }
    navigator.geolocation.getCurrentPosition(async ({ coords }) => {
      if (sequence !== routeSequenceRef.current) return
      const current = { lat: coords.latitude, lng: coords.longitude }
      if (!validCoordinates(current)) {
        setRouteStatus("error")
        setRouteError("Your current location could not be determined.")
        return
      }
      setLocation(current)
      setRouteStatus("loading")
      let timedOut = false
      const timeout = window.setTimeout(() => { timedOut = true; controller.abort() }, 12000)
      try {
        const nextRoute = await getDrivingRoute(current, selected, controller.signal)
        if (sequence !== routeSequenceRef.current) return
        setRoute(nextRoute)
        setRouteStatus("ready")
      } catch (error) {
        if (sequence !== routeSequenceRef.current) return
        setRouteStatus("error")
        setRouteError(timedOut ? "Directions timed out. Please try again." : error instanceof Error ? error.message : "Directions are unavailable. Please try again.")
      } finally {
        window.clearTimeout(timeout)
      }
    }, (error) => {
      if (sequence !== routeSequenceRef.current) return
      setRouteStatus("error")
      setRouteError(error.code === 1 ? "Allow location access to show driving directions." : "Your current location could not be determined. Please try again.")
    }, { enableHighAccuracy: true, timeout: 10000, maximumAge: 60000 })
  }
  useEffect(() => {
    const map = mapInstance.current
    if (!map || mapState !== "ready") return
    const draw = () => {
      if (map.getLayer(routeLayerId)) map.removeLayer(routeLayerId)
      if (map.getSource(routeSourceId)) map.removeSource(routeSourceId)
      if (!route) return
      map.addSource(routeSourceId, { type: "geojson", data: { type: "Feature", properties: {}, geometry: { type: "LineString", coordinates: route.coordinates } } })
      map.addLayer({ id: routeLayerId, type: "line", source: routeSourceId, layout: { "line-cap": "round", "line-join": "round" }, paint: { "line-color": "#2563eb", "line-width": 6, "line-opacity": 0.9 } })
      const bounds = new maptilersdk.LngLatBounds()
      route.coordinates.forEach((point) => bounds.extend(point))
      if (!bounds.isEmpty() && visible) map.fitBounds(bounds, { padding: cameraPadding(), maxZoom: 16, duration: window.matchMedia("(prefers-reduced-motion: reduce)").matches ? 0 : 700 })
    }
    if (map.isStyleLoaded()) draw()
    map.on("style.load", draw)
    return () => { map.off("style.load", draw) }
  }, [route, mapState, mapStyle, searchCollapsed, detailsOpen, visible])
  function findMyLocation() {
    if (!navigator.geolocation) return
    navigator.geolocation.getCurrentPosition(({ coords }) => {
      const next = { lat: coords.latitude, lng: coords.longitude }
      if (validCoordinates(next)) {
        setLocation(next)
        mapInstance.current?.flyTo({ center: [next.lng, next.lat], zoom: 14, duration: 850 })
      }
    }, () => { setRouteError("Your current location could not be determined.") }, { enableHighAccuracy: true, timeout: 10000 })
  }
  function changeMapStyle(style: MapStyleName) { setMapStyle(style); mapInstance.current?.setStyle(mapStyles[style]) }
  function selectLot(lot: ParkingLot) {
    if (selected?.id !== lot.id) closeDirections()
    setSelected(lot)
    setCoordinateError(validCoordinates(lot) ? "" : "This parking lot has invalid coordinates. The map cannot move to it.")
    setDetailsOpen(true)
    if (window.matchMedia("(max-width: 900px)").matches) setSearchCollapsed(true)
  }

  return (
    <section className={`find-parking${searchCollapsed ? " is-search-collapsed" : ""}${detailsOpen ? " has-details" : ""}`} aria-label="Find parking">
      {searchCollapsed && <button type="button" className="find-parking-reopen" onClick={() => { setSearchCollapsed(false); if (window.matchMedia("(max-width: 900px)").matches) { closeDirections(); setDetailsOpen(false) } }} aria-label="Open parking search"><UntitledIcon name="search" size={19} /><span>Search parking</span></button>}
      <aside ref={searchPanelRef} className="find-parking-panel" aria-label="Search parking" hidden={searchCollapsed}>
        <div className="find-parking-search">
          <div className="find-parking-search-heading">
            <div><h1>Search parking</h1><p>Find a space near you</p></div>
            <div className="find-parking-search-tools">
              <button type="button" className="find-parking-location" onClick={findMyLocation} aria-label="Use my location" title="Use my location"><UntitledIcon name="map-pin" size={18} /></button>
              <button type="button" className="find-parking-location" onClick={() => setSearchCollapsed(true)} aria-label="Collapse parking search" title="Collapse parking search"><span className="find-parking-collapse-icon"><UntitledIcon name="chevron-down" size={18} /></span></button>
            </div>
          </div>
          <label className="sr-only" htmlFor="find-parking-query">Search by parking name or address</label>
          <input id="find-parking-query" className="input" value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Search name or address" autoComplete="off" />
          <div className="find-parking-filters" aria-label="Parking filters">
            <button type="button" className={availableOnly ? "is-active" : ""} aria-pressed={availableOnly} onClick={() => setAvailableOnly((value) => !value)}>Available</button>
            <button type="button" className={sortByPrice ? "is-active" : ""} aria-pressed={sortByPrice} onClick={() => setSortByPrice((value) => !value)}>Lowest price</button>
            <select aria-label="Maximum hourly price" value={maxPrice ?? "all"} onChange={(event) => setMaxPrice(event.target.value === "all" ? null : Number(event.target.value))}>
              <option value="all">Any price</option>
              {priceOptions.map((price) => <option key={price} value={price}>Up to {formatRate(price)}/hr</option>)}
            </select>
            <select aria-label="Maximum distance" value={maxDistance ?? "all"} disabled={!location} title={!location ? "Use your location to filter by distance" : undefined} onChange={(event) => setMaxDistance(event.target.value === "all" ? null : Number(event.target.value))}>
              <option value="all">Any distance</option>
              {[1, 3, 5, 10].map((km) => <option key={km} value={km}>Within {km} km</option>)}
            </select>
            <select aria-label="Parking type" value={lotType} onChange={(event) => setLotType(event.target.value as LotType | "all")}>
              <option value="all">All parking types</option>
              {Object.entries(lotTypeLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
            </select>
          </div>
        </div>
        <div className="find-parking-results">
          <div className="find-parking-result-count">{results.length} parking {results.length === 1 ? "lot" : "lots"}</div>
          <div className="find-parking-result-list" aria-live="polite">
            {results.map(({ lot, distance }) => <button key={lot.id} type="button" className={`find-parking-result${selected?.id === lot.id ? " is-selected" : ""}`} onClick={() => selectLot(lot)}>
              <span className="find-parking-result-top"><strong>{lot.name}</strong>{distance !== null && <span>{distance.toFixed(1)} km</span>}</span>
              <span className="find-parking-address">{lot.address}</span>
              <span className="find-parking-result-bottom"><span className={availableSpaces(lot) ? "is-available" : "is-full"}>{hasAvailabilityData(lot) ? `${availableSpaces(lot)} spaces available` : "Availability unknown"} · {lotTypeLabels[lot.type]}</span><span>{formatRate(lot.hourlyRate)}/hr</span></span>
              <span className="find-parking-view">View <UntitledIcon name="arrow-right" size={14} /></span>
            </button>)}
            {!results.length && <p className="find-parking-empty">No parking lots match. Try another name or parking type.</p>}
          </div>
        </div>
      </aside>
      <div className="find-parking-map">
        <div ref={mapElement} className="find-parking-map-canvas" aria-label="MapTiler parking locations" />
        <label className="find-parking-map-style">Map type
          <select aria-label="Map type" value={mapStyle} onChange={(event) => changeMapStyle(event.target.value as MapStyleName)} disabled={mapState !== "ready"}>
            <option value="streets">Street</option><option value="satellite">Satellite</option><option value="hybrid">Hybrid</option>
          </select>
        </label>
        {mapState !== "ready" && <div className="find-parking-map-status" role="status">
          <UntitledIcon name="map" size={24} />
          <strong>{mapState === "missing-key" ? "MapTiler API key required" : mapState === "error" ? "MapTiler is unavailable" : "Loading MapTiler…"}</strong>
          <p>{mapState === "missing-key" ? "Add VITE_MAPTILER_API_KEY to use the map. Parking search remains available." : mapState === "error" ? "Check the map key or connection. Parking search remains available." : "Loading the interactive map."}</p>
        </div>}
        {location && <span className="find-parking-distance-note">Distances from your location</span>}
      </div>
      {selected && detailsOpen && <div ref={detailsPanelRef} className="find-parking-details-wrap">
        <ParkingDetailsPanel
          lot={selected}
          vehicleCapacity={vehicleCapacity}
          route={route}
          routeStatus={routeStatus}
          routeError={routeError}
          coordinateError={coordinateError}
          onDirection={startDirections}
          onCloseRoute={closeDirections}
          onBook={() => onBook(selected)}
          onClose={() => { closeDirections(); setDetailsOpen(false) }}
        />
      </div>}
    </section>
  )
}
