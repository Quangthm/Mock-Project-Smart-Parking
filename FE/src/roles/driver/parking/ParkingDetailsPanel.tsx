import type { ParkingLot, LotType } from "../../../lib/types"
import type { DrivingRoute } from "../../../lib/routingApi"
import { useState } from "react"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { LotPolicyModal } from "../../../components/modals/LotPolicyModal"

type RouteStatus = "idle" | "locating" | "loading" | "ready" | "error"
export type VehicleCapacityInfo = { status: "loading" | "ready" | "unavailable"; car: number | null; motorcycle: number | null }

const lotTypeLabels: Record<LotType, string> = {
  outdoor: "Outdoor",
  basement: "Underground",
  "multi-storey": "Indoor (Multi-storey)",
}

export function ParkingDetailsPanel({ lot, vehicleCapacity, route, routeStatus, routeError, coordinateError, onDirection, onCloseRoute, onBook, onClose }: {
  lot: ParkingLot
  vehicleCapacity: VehicleCapacityInfo
  route: DrivingRoute | null
  routeStatus: RouteStatus
  routeError: string
  coordinateError: string
  onDirection: () => void
  onCloseRoute: () => void
  onBook: () => void
  onClose: () => void
}) {
  const [policyOpen, setPolicyOpen] = useState(false)
  const hasSlots = Array.isArray(lot.slots)
  const available = hasSlots ? lot.slots.filter((slot) => slot.status === "available").length : null
  const canBook = lot.status === "active" && available !== null && available > 0
  const hourlyRate = Number.isFinite(lot.hourlyRate) ? `${lot.hourlyRate.toLocaleString("vi-VN")} VND / hour` : "Rate unavailable"
  const dailyRate = Number.isFinite(lot.dailyRate) ? `${lot.dailyRate.toLocaleString("vi-VN")} VND / day` : null

  return (
    <aside className="find-parking-details-panel" aria-label="Parking details">
      <div className="find-parking-details-header">
        <div className="find-parking-details-heading">
          <span className="find-parking-eyebrow">Parking details</span>
          <div className="find-parking-details-title-row">
            <h2>{lot.name}</h2>
            <button type="button" className="find-parking-policy-button" onClick={() => setPolicyOpen(true)} aria-label={`View policies for ${lot.name}`}>
              <span className="find-parking-policy-button-label"><UntitledIcon name="file" size={14} /> Policy</span>
            </button>
          </div>
        </div>
        <button type="button" className="find-parking-icon-button" onClick={onClose} aria-label="Close parking details"><UntitledIcon name="x" size={18} /></button>
      </div>
      <div className="find-parking-details-scroll">
        <p className="find-parking-details-address"><UntitledIcon name="map-pin" size={16} />{lot.address}</p>
        {(() => {
          const AVAILABILITY_STATUS_MAP: Record<string, { className: string; label: string }> = {
            unknown: { className: "is-unknown", label: "Availability unknown" },
            available: { className: "is-available", label: "Available" },
            full: { className: "is-full", label: "Full" },
          }
          const statusKey = available === null ? "unknown" : available > 0 ? "available" : "full"
          const statusInfo = lot.status !== "active"
            ? { className: "is-unknown", label: "Unavailable" }
            : (AVAILABILITY_STATUS_MAP[statusKey] ?? AVAILABILITY_STATUS_MAP.unknown)
          return (
            <span className={`find-parking-status ${statusInfo.className}`}>
              {statusInfo.label}
            </span>
          )
        })()}

        <div className="find-parking-details-section">
          <h3>Parking spaces</h3>
          <dl className="find-parking-details-list">
            <div><dt>All available spaces</dt><dd>{available === null ? "Unknown" : available}</dd></div>
            <div><dt>Car spaces</dt><dd>{vehicleCapacity.status === "loading" ? "Loading…" : vehicleCapacity.car ?? "Not provided"}</dd></div>
            <div><dt>Motorcycle spaces</dt><dd>{vehicleCapacity.status === "loading" ? "Loading…" : vehicleCapacity.motorcycle ?? "Not provided"}</dd></div>
            {Number.isFinite(lot.totalSlots) && <div><dt>Total capacity</dt><dd>{lot.totalSlots}</dd></div>}
          </dl>
          <p className="find-parking-data-note">{vehicleCapacity.status === "ready" ? "Vehicle counts come from the parking site's capacity data when available." : "A verified car and motorcycle breakdown is unavailable for this lot."}</p>
        </div>

        <div className="find-parking-details-section">
          <h3>Parking rates</h3>
          <dl className="find-parking-details-list">
            <div><dt>Published hourly rate</dt><dd>{hourlyRate}</dd></div>
            {dailyRate && <div><dt>Published daily rate</dt><dd>{dailyRate}</dd></div>}
            <div><dt>Car rate</dt><dd>Not provided</dd></div>
            <div><dt>Motorcycle rate</dt><dd>Not provided</dd></div>
            <div><dt>Parking type</dt><dd>{lotTypeLabels[lot.type] ?? "Unknown"}</dd></div>
          </dl>
        </div>

        {coordinateError && <p className="find-parking-feedback is-error" role="alert">{coordinateError}</p>}
        {routeStatus !== "idle" && <div className="find-parking-directions" role="status" aria-live="polite">
          {routeStatus === "locating" && "Finding your location…"}
          {routeStatus === "loading" && "Calculating driving route…"}
          {routeStatus === "error" && <span className="find-parking-route-error">{routeError} <button type="button" onClick={onDirection}>Retry</button></span>}
          {routeStatus === "ready" && route && <span>Driving route · {route.distanceKm.toFixed(1)} km · about {Math.round(route.durationMinutes)} min <small>via OpenStreetMap / Valhalla</small></span>}
        </div>}
      </div>
      <div className="find-parking-details-actions">
        <button type="button" className="btn-outline" onClick={routeStatus === "idle" ? onDirection : onCloseRoute} disabled={!!coordinateError}>
          <UntitledIcon name="map-pin" size={16} />{routeStatus === "idle" ? "Direction" : "Close directions"}
        </button>
        <button type="button" className="btn-primary" onClick={onBook} disabled={!canBook}>Book Now</button>
        {!canBook && <p>{available === null ? "Booking is unavailable while availability is unknown." : lot.status !== "active" ? "This parking lot is unavailable." : "No spaces are currently available."}</p>}
      </div>
      {policyOpen && <LotPolicyModal lot={lot} onClose={() => setPolicyOpen(false)} />}
    </aside>
  )
}
