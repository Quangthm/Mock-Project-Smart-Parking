import * as React from "react"
import * as maptilersdk from "@maptiler/sdk"
import "@maptiler/sdk/dist/maptiler-sdk.css"
import type { ParkingLotSummary } from "./living-canvas"

export interface MapTilerViewerProps {
  lots: ParkingLotSummary[]
  selectedLotId?: string
  isDark?: boolean
  onSelectLot: (lot: ParkingLotSummary) => void
  focusLot?: ParkingLotSummary | null
}

const escapeHtml = (value: string) => value.replace(/[&<>"']/g, (character) => {
  const entities: Record<string, string> = {
    "&": "&amp;",
    "<": "&lt;",
    ">": "&gt;",
    '"': "&quot;",
    "'": "&#39;",
  }
  return entities[character]
})

export function MapTilerViewer({
  lots,
  selectedLotId,
  isDark = true,
  onSelectLot,
  focusLot,
}: MapTilerViewerProps) {
  const mapContainer = React.useRef<HTMLDivElement>(null)
  const mapInstance = React.useRef<maptilersdk.Map | null>(null)
  const markersRef = React.useRef<maptilersdk.Marker[]>([])

  // 1. Initialize MapTiler Map
  React.useEffect(() => {
    if (!mapContainer.current) return
    const apiKey = import.meta.env.VITE_MAPTILER_API_KEY || "tpX28VpW87LtzYmUWk8A"
    maptilersdk.config.apiKey = apiKey

    const initialCenter: [number, number] = [106.7018, 10.7781] // District 1, HCMC

    const map = new maptilersdk.Map({
      container: mapContainer.current,
      style: isDark ? maptilersdk.MapStyle.DATAVIZ.DARK : maptilersdk.MapStyle.STREETS,
      center: initialCenter,
      zoom: 14.8,
      forceNoAttributionControl: true,
      navigationControl: false,
      geolocateControl: false,
    })

    mapInstance.current = map

    map.on("load", () => {
      // Map loaded successfully
    })

    return () => {
      markersRef.current.forEach((m) => m.remove())
      markersRef.current = []
      map.remove()
      mapInstance.current = null
    }
  }, [])

  // 2. Reactive Style Switcher for Dark/Light
  React.useEffect(() => {
    if (!mapInstance.current) return
    const targetStyle = isDark ? maptilersdk.MapStyle.DATAVIZ.DARK : maptilersdk.MapStyle.STREETS
    try {
      mapInstance.current.setStyle(targetStyle)
    } catch {
      // Ignore if map is currently loading
    }
  }, [isDark])

  // 3. Render Custom Interactive Markers
  React.useEffect(() => {
    const map = mapInstance.current
    if (!map) return

    // Clear old markers
    markersRef.current.forEach((m) => m.remove())
    markersRef.current = []

    lots.forEach((lot) => {
      const isSelected = lot.id === selectedLotId

      // A focusable marker exposes details on hover and keyboard focus.
      const el = document.createElement("div")
      const lotName = escapeHtml(lot.name)
      const lotId = escapeHtml(lot.id)
      const lotDescriptionId = `lot-details-${lotId}`
      const lotAddress = escapeHtml(lot.address)
      const hourlyRate = lot.hourlyRate.toLocaleString("vi-VN")
      const distance = lot.distanceKm.toLocaleString("vi-VN", { minimumFractionDigits: 1, maximumFractionDigits: 1 })
      const addressLabel = lot.structure === "basement" ? "Lối vào hầm (tham chiếu)" : "Lối vào bãi xe (tham chiếu)"
      const occupancy = lot.totalSlots > 0 ? Math.max(0, Math.min(100, (lot.availableSlots / lot.totalSlots) * 100)) : 0
      const occupancyLabel = `${lot.availableSlots}/${lot.totalSlots} chỗ còn trống`
      const markerTone = lot.availableSlots === 0
        ? "bg-slate-500 text-white"
        : lot.availableSlots <= 15
          ? "bg-orange-400 text-[#35200D]"
          : lot.availableSlots > 20
            ? "bg-[#B4E33F] text-[#1E3B27]"
            : "bg-[#FDE68A] text-[#78350F]"
      el.className = `group relative select-none ${isSelected ? "z-20" : "z-10"}`

      el.innerHTML = `
        <div class="relative pb-2">
          <button type="button" data-lot-marker class="flex max-w-[250px] items-center gap-2 rounded-full border border-white/15 ${
            isDark ? "bg-[#181918]/95 text-white" : "border-slate-300 bg-white/95 text-slate-900"
          } px-2.5 py-1.5 shadow-lg backdrop-blur-sm transition-transform duration-150 hover:scale-[1.03] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 ${isDark ? "focus-visible:outline-[#B4E33F]" : "focus-visible:outline-[#285337]"}" aria-label="${lotName}, ${occupancyLabel}, dữ liệu mẫu" aria-describedby="${lotDescriptionId}">
            <span class="flex size-8 shrink-0 items-center justify-center rounded-full ${markerTone} text-sm font-bold tabular-nums">${lot.availableSlots}</span>
            <span class="max-w-[170px] truncate pr-1 text-left text-sm font-semibold">${lotName}</span>
          </button>
          <span aria-hidden="true" class="pointer-events-none absolute bottom-[3px] left-1/2 size-2.5 -translate-x-1/2 rotate-45 border-b border-r ${isDark ? "border-white/15 bg-[#181918]" : "border-slate-300 bg-white"}"></span>
          <span aria-hidden="true" class="pointer-events-none absolute bottom-0 left-1/2 size-1 -translate-x-1/2 rounded-full ${isDark ? "bg-[#B4E33F]" : "bg-[#285337]"}"></span>
          <div class="invisible pointer-events-none absolute bottom-full left-1/2 z-[1000] w-[min(300px,calc(100vw-32px))] -translate-x-1/2 pb-2 opacity-0 transition-[opacity,visibility] delay-100 duration-150 group-hover:visible group-hover:pointer-events-auto group-hover:opacity-100 group-focus-within:visible group-focus-within:pointer-events-auto group-focus-within:opacity-100">
            <div id="${lotDescriptionId}" class="rounded-2xl border p-4 text-left shadow-xl backdrop-blur-xl ${
              isDark ? "border-white/15 bg-[#181918]/[.98] text-white" : "border-slate-200 bg-white/[.98] text-slate-900"
            }">
              <p class="text-base font-bold leading-snug">${lotName}</p>
              <p class="mt-1.5 text-xs leading-relaxed ${isDark ? "text-slate-300" : "text-slate-600"}">${addressLabel} · ${lotAddress}</p>
              <div class="mt-3 flex items-center justify-between gap-3 text-xs">
                <span class="font-medium ${isDark ? "text-slate-300" : "text-slate-600"}">${occupancyLabel}</span>
                <span class="font-semibold tabular-nums">${Math.round(occupancy)}%</span>
              </div>
              <div class="mt-1.5 h-1.5 overflow-hidden rounded-full ${isDark ? "bg-white/10" : "bg-slate-200"}" role="img" aria-label="Còn ${Math.round(occupancy)} phần trăm chỗ trống">
                <div class="h-full rounded-full ${lot.availableSlots === 0 ? "bg-slate-400" : lot.availableSlots <= 15 ? "bg-orange-400" : "bg-[#B4E33F]"}" style="width:${occupancy}%"></div>
              </div>
              <p class="mt-3 text-sm font-bold">${hourlyRate} VND / giờ</p>
              <button type="button" data-lot-3d class="mt-3 flex min-h-10 w-full items-center justify-center rounded-xl bg-[#285337] px-3 py-2 text-sm font-semibold text-white transition-colors hover:bg-[#346744] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#B4E33F] dark:bg-[#B4E33F] dark:text-[#152A1B] dark:hover:bg-[#c6ec61]">
                Xem mô hình 3D Digital Twin
              </button>
              <p class="mt-2 text-[11px] ${isDark ? "text-slate-400" : "text-slate-500"}">Dữ liệu mẫu · Cách ${distance} km</p>
            </div>
          </div>
        </div>
      `

      const selectLot = (event: Event) => {
        event.stopPropagation()
        map.flyTo({ center: [lot.lng, lot.lat], zoom: 16.5, duration: 1000 })
        onSelectLot(lot)
      }
      const markerButton = el.querySelector<HTMLButtonElement>("[data-lot-marker]")
      const digitalTwinButton = el.querySelector<HTMLButtonElement>("[data-lot-3d]")
      markerButton?.addEventListener("click", selectLot)
      digitalTwinButton?.addEventListener("click", selectLot)
      const setMarkerPriority = () => { el.style.zIndex = "1000" }
      const clearMarkerPriority = () => { el.style.zIndex = isSelected ? "20" : "10" }
      el.addEventListener("mouseenter", setMarkerPriority)
      el.addEventListener("mouseleave", clearMarkerPriority)
      el.addEventListener("focusin", setMarkerPriority)
      el.addEventListener("focusout", (event) => {
        if (!el.contains(event.relatedTarget as Node | null)) clearMarkerPriority()
      })

      const marker = new maptilersdk.Marker({ element: el, anchor: "bottom" })
        .setLngLat([lot.lng, lot.lat])
        .addTo(map)

      markersRef.current.push(marker)
    })
  }, [lots, selectedLotId, isDark, onSelectLot])

  // 4. Focus FlyTo on Search / Geolocation
  React.useEffect(() => {
    if (!focusLot || !mapInstance.current) return
    mapInstance.current.flyTo({
      center: [focusLot.lng, focusLot.lat],
      zoom: 16.2,
      duration: 1200,
      essential: true,
    })
  }, [focusLot])

  return (
    <div className="relative w-full h-full select-none">
      <div ref={mapContainer} className="w-full h-full" />
    </div>
  )
}
