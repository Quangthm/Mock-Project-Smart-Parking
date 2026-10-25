import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { useEffect, useMemo, useRef, useState } from "react"
import * as maptilersdk from "@maptiler/sdk"
import { animate } from "animejs"
import "@maptiler/sdk/dist/maptiler-sdk.css"
import { driverData as store } from "../data/data"
import type { LotType, ParkingLot } from "../../../lib/types"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { PageHeader } from "../../../components/ui/PageHeader"
import { getDrivingRoute, type DrivingRoute } from "../../../lib/routingApi"
import { ParkingDetailsPanel, type VehicleCapacityInfo } from "./ParkingDetailsPanel"
import { useApp } from "../../../context/AppContext"

type LatLng = { lat: number; lng: number }
type MapState = "loading" | "ready" | "missing-key" | "error"
type MapStyleName = "streets" | "satellite" | "hybrid"
const mapStyleIds: Record<MapStyleName, string> = { streets: "streets-v4", satellite: "satellite-v4", hybrid: "hybrid-v4" }
function mapStyleUrl(style: MapStyleName, apiKey: string, theme: "light" | "dark") {
  const styleId = style === "streets" && theme === "dark" ? "streets-v4-dark" : mapStyleIds[style]
  return `https://api.maptiler.com/maps/${styleId}/style.json?key=${encodeURIComponent(apiKey)}`
}
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
function availabilityLevel(lot: ParkingLot) {
  if (!hasAvailabilityData(lot)) return "unknown"
  const available = availableSpaces(lot)
  if (available === 0) return "full"
  const capacity = Math.max(lot.slots.length, lot.totalSlots || 0)
  return capacity > 0 && available / capacity < 0.3 ? "limited" : "open"
}
function formatRate(rate: number) { return Number.isFinite(rate) ? `${rate.toLocaleString("vi-VN")} VND` : "Rate unavailable" }
const routeSourceId = "smartparking-driving-route"
const routeLayerId = "smartparking-driving-route-line"

export function FindParking({ onBook, onDetailsOpenChange, visible = true }: {
  onBook: (lot: ParkingLot) => void
  onDetailsOpenChange?: (open: boolean) => void
  visible?: boolean
}) {
  const { theme, user } = useApp()
  const [sourceLots, setSourceLots] = useState(() => store.getLots())
  const lots = useMemo(() => sourceLots.filter((lot) => lot.status === "active"), [sourceLots])
  const validLots = useMemo(() => lots.filter(validCoordinates), [lots])
  const mapElement = useRef<HTMLDivElement>(null)
  const mapFrameRef = useRef<HTMLDivElement>(null)
  const mapMenuRef = useRef<HTMLDivElement>(null)
  const mapStyleRef = useRef<HTMLDivElement>(null)
  const searchPanelRef = useRef<HTMLElement>(null)
  const detailsPanelRef = useRef<HTMLDivElement>(null)
  const mapInstance = useRef<maptilersdk.Map | null>(null)
  const appliedMapStyleRef = useRef("")
  const markersRef = useRef(new Map<string, maptilersdk.Marker>())
  const locationMarkerRef = useRef<maptilersdk.Marker | null>(null)
  const routeRequestRef = useRef<AbortController | null>(null)
  const routeSequenceRef = useRef(0)
  const [selected, setSelected] = useState<ParkingLot | null>(null)
  const [detailsOpen, setDetailsOpen] = useState(false)
  const [searchCollapsed, setSearchCollapsed] = useState(true)
  const [resultsOpen, setResultsOpen] = useState(false)
  const [filtersOpen, setFiltersOpen] = useState(false)
  const [searchHistory, setSearchHistory] = useState<string[]>([])
  const [coordinateError, setCoordinateError] = useState("")
  const vehicleCapacity: VehicleCapacityInfo = { status: "unavailable", car: null, motorcycle: null }
  const [query, setQuery] = useState("")
  const [availableOnly, setAvailableOnly] = useState(false)
  const [sortByPrice, setSortByPrice] = useState(false)
  const [maxPrice, setMaxPrice] = useState<number | null>(null)
  const [maxDistance, setMaxDistance] = useState<number | null>(null)
  const [lotType, setLotType] = useState<LotType | "all">("all")
  const [location, setLocation] = useState<LatLng | null>(null)
  const [locationError, setLocationError] = useState("")
  const [mapState, setMapState] = useState<MapState>("loading")
  const [mapStyle, setMapStyle] = useState<MapStyleName>("streets")
  const [mapMenuOpen, setMapMenuOpen] = useState(false)
  const [mapStyleOpen, setMapStyleOpen] = useState(false)
  const [route, setRoute] = useState<DrivingRoute | null>(null)
  const [routeStatus, setRouteStatus] = useState<"idle" | "locating" | "loading" | "ready" | "error">("idle")
  const [routeError, setRouteError] = useState("")
  useEffect(() => {
    if (detailsOpen) {
      setMapMenuOpen(false)
      setMapStyleOpen(false)
    }
  }, [detailsOpen])
  useEffect(() => {
    if (searchCollapsed || !searchPanelRef.current || window.matchMedia("(prefers-reduced-motion: reduce)").matches) return
    const entrance = animate(searchPanelRef.current, { opacity: [0, 1], y: [-8, 0], duration: 220, ease: "outCubic" })
    return () => { entrance.cancel() }
  }, [searchCollapsed])
  const parkingCenter = useMemo(() => validLots.length ? {
    lat: validLots.reduce((sum, lot) => sum + lot.lat, 0) / validLots.length,
    lng: validLots.reduce((sum, lot) => sum + lot.lng, 0) / validLots.length,
  } : null, [validLots])
  const priceOptions = useMemo(() => [...new Set(lots.map((lot) => lot.hourlyRate).filter(Number.isFinite))].sort((a, b) => a - b), [lots])
  const historyKey = `sp_parking_search_history_${user?.id ?? "guest"}`
  useEffect(() => {
    try { setSearchHistory(JSON.parse(localStorage.getItem(historyKey) || "[]")) }
    catch { setSearchHistory([]) }
  }, [historyKey])
  function rememberSearch(value: string) {
    const term = value.trim()
    if (!term) return
    setSearchHistory((previous) => {
      const next = [term, ...previous.filter((item) => item.toLocaleLowerCase() !== term.toLocaleLowerCase())].slice(0, 5)
      localStorage.setItem(historyKey, JSON.stringify(next))
      return next
    })
  }
  function clearSearchHistory() {
    try { localStorage.removeItem(historyKey) }
    catch { /* The in-memory list can still be cleared if storage is unavailable. */ }
    setSearchHistory([])
  }
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
      ? { top: searchCollapsed ? 116 : (searchRect?.height ?? 0) + 32, right: 102, bottom: detailsOpen ? (detailsRect?.height ?? 0) + 32 : 108, left: 102 }
      : { top: 128, right: detailsOpen ? (detailsRect?.width ?? 0) + 48 : 112, bottom: 122, left: searchCollapsed ? 112 : (searchRect?.width ?? 0) + 48 }
  }

  useEffect(() => { onDetailsOpenChange?.(detailsOpen) }, [onDetailsOpenChange, detailsOpen])
  useEffect(() => {
    if (!mapMenuOpen) return
    const dismiss = (event: PointerEvent) => {
      if (!mapMenuRef.current?.contains(event.target as Node)) setMapMenuOpen(false)
    }
    const escape = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setMapMenuOpen(false)
        mapMenuRef.current?.querySelector<HTMLButtonElement>(".find-parking-map-menu-trigger")?.focus()
      }
    }
    document.addEventListener("pointerdown", dismiss)
    document.addEventListener("keydown", escape)
    return () => { document.removeEventListener("pointerdown", dismiss); document.removeEventListener("keydown", escape) }
  }, [mapMenuOpen])
  useEffect(() => {
    if (!mapStyleOpen) return
    const dismiss = (event: PointerEvent) => {
      if (!mapStyleRef.current?.contains(event.target as Node)) setMapStyleOpen(false)
    }
    const escape = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setMapStyleOpen(false)
        mapStyleRef.current?.querySelector<HTMLButtonElement>(".find-parking-map-style-trigger")?.focus()
      }
    }
    document.addEventListener("pointerdown", dismiss)
    document.addEventListener("keydown", escape)
    return () => { document.removeEventListener("pointerdown", dismiss); document.removeEventListener("keydown", escape) }
  }, [mapStyleOpen])
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
      const initialStyle = mapStyleUrl("streets", apiKey, theme)
      appliedMapStyleRef.current = initialStyle
      const map = new maptilersdk.Map({
        container: mapElement.current, style: initialStyle,
        ...(parkingCenter ? { center: [parkingCenter.lng, parkingCenter.lat] as [number, number], zoom: 13 } : {}),
        navigationControl: false, geolocateControl: false, fullscreenControl: false,
      })
      mapInstance.current = map
      map.on("load", () => {
        if (cancelled) return
        if (validLots.length) {
          const bounds = new maptilersdk.LngLatBounds()
          validLots.forEach((lot) => bounds.extend([lot.lng, lot.lat]))
          map.fitBounds(bounds, { padding: cameraPadding(), maxZoom: 14, duration: 0 })
        }
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
    const map = mapInstance.current
    const apiKey = import.meta.env.VITE_MAPTILER_API_KEY
    if (!map || !apiKey || mapState !== "ready") return
    const nextStyle = mapStyleUrl(mapStyle, apiKey, theme)
    if (appliedMapStyleRef.current === nextStyle) return
    appliedMapStyleRef.current = nextStyle
    map.setStyle(nextStyle)
  }, [mapStyle, mapState, theme])
  useEffect(() => {
    if (mapState !== "ready" || !mapInstance.current) return
    const map = mapInstance.current
    markersRef.current.forEach((marker) => marker.remove())
    markersRef.current.clear()
    validLots.forEach((lot) => {
      const level = availabilityLevel(lot)
      const element = document.createElement("div")
      element.className = `parking-map-marker is-${level}`
      const button = document.createElement("button")
      button.type = "button"
      button.className = `parking-map-label is-${level}`
      button.setAttribute("aria-label", `${lot.name}, ${hasAvailabilityData(lot) ? `${availableSpaces(lot)} spaces available` : "availability unknown"}. View parking details`)
      button.setAttribute("aria-describedby", `parking-hover-${lot.id.replace(/[^a-zA-Z0-9_-]/g, "-")}`)
      const count = document.createElement("span")
      count.className = "parking-map-label-count"
      count.textContent = hasAvailabilityData(lot) ? String(availableSpaces(lot)) : "?"
      const copy = document.createElement("span")
      copy.className = "parking-map-label-copy"
      const name = document.createElement("strong")
      name.textContent = lot.name
      const available = document.createElement("span")
      available.className = "parking-map-label-meta"
      available.textContent = hasAvailabilityData(lot) ? `${availableSpaces(lot)} free` : "Availability unknown"
      copy.append(name, available)
      button.append(count, copy)
      const hover = document.createElement("div")
      hover.className = "parking-map-hover"
      hover.id = `parking-hover-${lot.id.replace(/[^a-zA-Z0-9_-]/g, "-")}`
      const hoverName = document.createElement("strong")
      hoverName.textContent = lot.name
      const hoverAddress = document.createElement("span")
      hoverAddress.textContent = lot.address
      const hoverStatus = document.createElement("span")
      hoverStatus.className = `parking-map-hover-status is-${level}`
      hoverStatus.textContent = `${level === "open" ? "Available" : level === "limited" ? "Limited spaces" : level === "full" ? "Full" : "Availability unknown"} · ${hasAvailabilityData(lot) ? `${availableSpaces(lot)} spaces free` : "No live capacity"}`
      hover.append(hoverName, hoverAddress, hoverStatus)
      element.append(button, hover)
      button.addEventListener("click", () => selectLot(lot))
      const positionHover = () => {
        const frame = map.getContainer().getBoundingClientRect()
        const marker = button.getBoundingClientRect()
        element.classList.toggle("hover-below", marker.top - frame.top < 185)
        element.classList.toggle("hover-align-right", marker.left - frame.left > frame.width - 270)
      }
      element.addEventListener("mouseenter", positionHover)
      button.addEventListener("focus", positionHover)
      // Keep the marker's tip on its geographic point at every zoom level.
      const marker = new maptilersdk.Marker({ element, anchor: "bottom", offset: [0, 0] })
        .setLngLat([lot.lng, lot.lat])
        .addTo(map)
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
    markersRef.current.forEach((marker, id) => marker.getElement().querySelector(".parking-map-label")?.classList.toggle("is-selected-parking-marker", id === selected?.id))
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
  function findMyLocation(applyNearFilter = false) {
    if (!navigator.geolocation) { setLocationError("Location is unavailable in this browser."); return }
    setLocationError("")
    navigator.geolocation.getCurrentPosition(({ coords }) => {
      const next = { lat: coords.latitude, lng: coords.longitude }
      if (validCoordinates(next)) {
        setLocation(next)
        if (applyNearFilter) setMaxDistance(5)
        mapInstance.current?.flyTo({ center: [next.lng, next.lat], zoom: 14, duration: window.matchMedia("(prefers-reduced-motion: reduce)").matches ? 0 : 850 })
      }
    }, () => { setLocationError("Allow location access to find parking near you."); setRouteError("Your current location could not be determined.") }, { enableHighAccuracy: true, timeout: 10000 })
  }
  function changeMapStyle(style: MapStyleName) {
    setMapStyle(style)
  }
  function useMapControl(action: "zoom-in" | "zoom-out" | "reset" | "location" | "fullscreen") {
    mapMenuRef.current?.querySelector<HTMLButtonElement>(".find-parking-map-menu-trigger")?.focus()
    setMapMenuOpen(false)
    const map = mapInstance.current
    const duration = window.matchMedia("(prefers-reduced-motion: reduce)").matches ? 0 : 300
    if (action === "zoom-in") map?.zoomIn({ duration })
    if (action === "zoom-out") map?.zoomOut({ duration })
    if (action === "reset") map?.easeTo({ bearing: 0, pitch: 0, duration })
    if (action === "location") findMyLocation()
    if (action === "fullscreen") {
      if (document.fullscreenElement) void document.exitFullscreen()
      else void mapFrameRef.current?.requestFullscreen()
      window.setTimeout(() => map?.resize(), 200)
    }
  }
  function selectLot(lot: ParkingLot) {
    if (selected?.id !== lot.id) closeDirections()
    setSelected(lot)
    setCoordinateError(validCoordinates(lot) ? "" : "This parking lot has invalid coordinates. The map cannot move to it.")
    setDetailsOpen(true)
    if (window.matchMedia("(max-width: 900px)").matches) collapseSearch()
  }

  function collapseSearch() {
    setSearchCollapsed(true)
    setQuery("")
    setResultsOpen(false)
  }

  return (
    <section className={`find-parking${searchCollapsed ? " is-search-collapsed" : ""}${detailsOpen ? " has-details" : ""}`} aria-label="Find parking">
      {searchCollapsed && <button type="button" className="find-parking-reopen" onClick={() => { setSearchCollapsed(false); setResultsOpen(false); if (window.matchMedia("(max-width: 900px)").matches) { closeDirections(); setDetailsOpen(false) } }} aria-label="Open parking search"><UntitledIcon name="search" size={18} /><span>Search Parking</span></button>}
      <aside ref={searchPanelRef} className="find-parking-panel" aria-label="Search parking" hidden={searchCollapsed}>
        <div className="find-parking-search">
          <div className="find-parking-search-heading">
            <PageHeader title="Search Parking" variant="compact" />
            <div className="find-parking-search-tools">
              <button type="button" className="find-parking-location" onClick={() => findMyLocation()} aria-label="Use my location" title="Use my location"><UntitledIcon name="map-pin" size={18} /></button>
              <button type="button" className="find-parking-location" onClick={collapseSearch} aria-label="Collapse parking search" title="Collapse parking search"><span className="find-parking-collapse-icon"><UntitledIcon name="chevron-down" size={18} /></span></button>
            </div>
          </div>
          <label className="sr-only" htmlFor="find-parking-query">Search by parking name or address</label>
          <input id="find-parking-query" name="parking-search" className="input" value={query} onFocus={() => setResultsOpen(true)} onChange={(event) => { setQuery(event.target.value); setResultsOpen(true) }} onKeyDown={(event) => { if (event.key === "Enter") rememberSearch(query) }} placeholder="Search name or address…" autoComplete="off" />
          {searchHistory.length > 0 && <div className="find-parking-history" aria-label="Recent searches">
            <div className="find-parking-history-heading">
              <strong>Recent Searches</strong>
              <button type="button" className="find-parking-history-clear" aria-label="Clear recent searches" title="Clear recent searches" onClick={clearSearchHistory}>
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d="M4 7h16M10 3h4l1 4H9l1-4ZM6.5 7l.8 13h9.4l.8-13M10 11v5m4-5v5" /></svg>
              </button>
            </div>
            {searchHistory.map((item) => <button key={item} type="button" onClick={() => { setQuery(item); setResultsOpen(true) }}><UntitledIcon name="clock" size={16} />{item}</button>)}
          </div>}
          <button type="button" className="find-parking-filter-toggle" aria-expanded={filtersOpen} aria-controls="find-parking-filter-options" onClick={() => setFiltersOpen((open) => !open)}><UntitledIcon name="filter" size={16} />Filters<UntitledIcon name="chevron-down" size={15} /></button>
          {filtersOpen && <div id="find-parking-filter-options" className="find-parking-filters" aria-label="Parking filters">
            <button type="button" className={availableOnly ? "is-active" : ""} aria-pressed={availableOnly} onClick={() => setAvailableOnly((value) => !value)}>Available</button>
            <button type="button" className={sortByPrice ? "is-active" : ""} aria-pressed={sortByPrice} onClick={() => setSortByPrice((value) => !value)}>Lowest price</button>
            <RoundedSelect aria-label="Maximum hourly price" value={maxPrice ?? "all"} onChange={(event) => setMaxPrice(event.target.value === "all" ? null : Number(event.target.value))}>
              <option value="all">Any price</option>
              {priceOptions.map((price) => <option key={price} value={price}>Up to {formatRate(price)}/hr</option>)}
            </RoundedSelect>
            <RoundedSelect aria-label="Maximum distance" value={maxDistance ?? "all"} disabled={!location} title={!location ? "Use your location to filter by distance" : undefined} onChange={(event) => setMaxDistance(event.target.value === "all" ? null : Number(event.target.value))}>
              <option value="all">Any distance</option>
              {[1, 3, 5, 10].map((km) => <option key={km} value={km}>Within {km} km</option>)}
            </RoundedSelect>
            <RoundedSelect aria-label="Parking type" value={lotType} onChange={(event) => setLotType(event.target.value as LotType | "all")}>
              <option value="all">All parking types</option>
              {Object.entries(lotTypeLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
            </RoundedSelect>
            <button type="button" className={maxDistance === 5 ? "is-active" : ""} aria-pressed={maxDistance === 5} onClick={() => { if (maxDistance === 5) setMaxDistance(null); else if (location) setMaxDistance(5); else findMyLocation(true) }}>Near You (&lt; 5 km)</button>
          </div>}
          {locationError && <p className="find-parking-location-error" role="status">{locationError}</p>}
        </div>
        <div className={`find-parking-results${resultsOpen ? " is-open" : ""}`}>
          <button type="button" className="find-parking-result-count" aria-expanded={resultsOpen} onClick={() => setResultsOpen((open) => !open)}>
            <span>{results.length} parking {results.length === 1 ? "lot" : "lots"}</span>
            <span className={resultsOpen ? "find-parking-chevron-up" : undefined}><UntitledIcon name="chevron-down" size={16} /></span>
          </button>
          <div className="find-parking-result-list" aria-live="polite" aria-hidden={!resultsOpen} inert={!resultsOpen}>
            {results.map(({ lot, distance }) => <button key={lot.id} type="button" className={`find-parking-result${selected?.id === lot.id ? " is-selected" : ""}`} onClick={() => { rememberSearch(query || lot.name); selectLot(lot); setResultsOpen(false) }}>
              <span className="find-parking-result-top"><strong>{lot.name}</strong>{distance !== null && <span>{distance.toFixed(1)} km</span>}</span>
              <span className="find-parking-address">{lot.address}</span>
              <span className="find-parking-result-bottom"><span className={availableSpaces(lot) ? "is-available" : "is-full"}>{hasAvailabilityData(lot) ? `${availableSpaces(lot)} spaces available` : "Availability unknown"} · {lotTypeLabels[lot.type]}</span><span>{formatRate(lot.hourlyRate)}/hr</span></span>
              <span className="find-parking-view">View <UntitledIcon name="arrow-right" size={14} /></span>
            </button>)}
            {!results.length && <p className="find-parking-empty">No parking lots match. Try another name or parking type.</p>}
          </div>
        </div>
      </aside>
      <div ref={mapFrameRef} className="find-parking-map">
        <div ref={mapElement} className="find-parking-map-canvas" aria-label="MapTiler parking locations" />
        <div className="find-parking-map-toolbar">
          <div ref={mapStyleRef} className="find-parking-map-style">
            <button type="button" className="find-parking-map-style-trigger" aria-label={`Map type: ${mapStyle === "streets" ? "Street" : mapStyle === "satellite" ? "Satellite" : "Hybrid"}`} aria-haspopup="menu" aria-expanded={mapStyleOpen} aria-controls="find-parking-map-style-options" disabled={mapState !== "ready"} onClick={() => { setMapMenuOpen(false); setMapStyleOpen((open) => !open) }} onKeyDown={(event) => {
              if (event.key === "ArrowDown" || event.key === "ArrowUp") {
                event.preventDefault()
                const fromStart = event.key === "ArrowDown"
                setMapMenuOpen(false)
                setMapStyleOpen(true)
                requestAnimationFrame(() => {
                  const choices = mapStyleRef.current?.querySelectorAll<HTMLButtonElement>('[role="menuitemradio"]')
                  if (choices?.length) choices[fromStart ? 0 : choices.length - 1]?.focus()
                })
              }
            }}>
              <span className="find-parking-map-style-label">Map</span>
              <span className="find-parking-map-style-value">{mapStyle === "streets" ? "Street" : mapStyle === "satellite" ? "Satellite" : "Hybrid"}</span>
              <UntitledIcon name="chevron-down" size={16} />
            </button>
            <div id="find-parking-map-style-options" className="find-parking-map-style-list" role="menu" aria-label="Map type" data-open={mapStyleOpen} aria-hidden={!mapStyleOpen} inert={!mapStyleOpen} onKeyDown={(event) => {
              if (event.key !== "ArrowDown" && event.key !== "ArrowUp" && event.key !== "Home" && event.key !== "End") return
              event.preventDefault()
              const choices = [...event.currentTarget.querySelectorAll<HTMLButtonElement>('[role="menuitemradio"]')]
              const current = choices.indexOf(document.activeElement as HTMLButtonElement)
              const next = event.key === "Home" ? 0 : event.key === "End" ? choices.length - 1 : (current + (event.key === "ArrowDown" ? 1 : -1) + choices.length) % choices.length
              choices[next]?.focus()
            }}>
              {([ ["streets", "Street"], ["satellite", "Satellite"], ["hybrid", "Hybrid"] ] as const).map(([value, label]) => (
                <button key={value} type="button" role="menuitemradio" aria-checked={mapStyle === value} onClick={() => { changeMapStyle(value); setMapStyleOpen(false); mapStyleRef.current?.querySelector<HTMLButtonElement>(".find-parking-map-style-trigger")?.focus() }}>
                  <span>{label}</span><span className="find-parking-map-style-check" aria-hidden="true">{mapStyle === value ? "✓" : ""}</span>
                </button>
              ))}
            </div>
          </div>
          <div ref={mapMenuRef} className="find-parking-map-menu">
            <button type="button" className="find-parking-map-menu-trigger" aria-label="Map controls" aria-expanded={mapMenuOpen} aria-controls="find-parking-map-controls" onClick={() => { setMapStyleOpen(false); setMapMenuOpen((open) => !open) }}>
              <UntitledIcon name="menu" size={20} />
            </button>
            {mapMenuOpen && <div id="find-parking-map-controls" className="find-parking-map-menu-list" role="group" aria-label="Map controls">
              <button type="button" onClick={() => useMapControl("zoom-in")} disabled={mapState !== "ready"}><UntitledIcon name="plus" size={17} />Zoom in</button>
              <button type="button" onClick={() => useMapControl("zoom-out")} disabled={mapState !== "ready"}><span className="find-parking-minus" aria-hidden="true">−</span>Zoom out</button>
              <button type="button" onClick={() => useMapControl("reset")} disabled={mapState !== "ready"}><UntitledIcon name="target" size={17} />Reset direction</button>
              <button type="button" onClick={() => useMapControl("location")} disabled={mapState !== "ready"}><UntitledIcon name="map-pin" size={17} />My location</button>
              <button type="button" onClick={() => useMapControl("fullscreen")} disabled={mapState !== "ready"}><UntitledIcon name="grid" size={17} />Fullscreen</button>
            </div>}
          </div>
        </div>
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
