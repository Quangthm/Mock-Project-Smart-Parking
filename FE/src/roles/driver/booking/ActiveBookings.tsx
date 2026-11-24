import { useEffect, useState } from "react"
import { driverReservationApi, type DriverReservation } from "../../../lib/driverReservationApi"
import { formatStatus } from "../../../lib/formatStatus"
import { useServerRefresh } from "../../../lib/serverRefresh"
import { readReservationAttempt } from "./ReservationCheckout"

import { useApp } from "../../../context/AppContext"
import type { Booking } from "../../../lib/types"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { Badge, type BadgeTone } from "../../../components/ui/Badge"
import { canonicalVietnamesePlate, vehiclesApi, type RegisteredVehicle } from "../../../lib/vehiclesApi"
import { driverData as store } from "../data/data"
import { FindMyCarCard } from "../parking/FindMyCarCard"
import { LiveReservations } from "./LiveReservations"
import "../../../booking-support.css"
import "./recent-reservations.css"

const dateTime = new Intl.DateTimeFormat("en-US", { dateStyle: "medium", timeStyle: "short" })
const currency = new Intl.NumberFormat("vi-VN")
const reservationDate = new Intl.DateTimeFormat("en-US", { month: "short", day: "numeric", year: "numeric" })
const reservationTime = new Intl.DateTimeFormat("en-US", { hour: "numeric", minute: "2-digit" })
function reservationStamp(value: string, formatter: Intl.DateTimeFormat) {
  return Number.isFinite(Date.parse(value)) ? formatter.format(new Date(value)) : "Unavailable"
}
const reservationTone: Record<string, BadgeTone> = {
  COMPLETED: "success", PENDING_PAYMENT: "warning", NO_SHOW: "danger", UNFULFILLABLE: "danger",
  CONFIRMED: "primary", ALLOCATED: "primary", PARKING: "primary",
}

function BookingRow({ booking, vehicleType, address }: { booking: Booking; vehicleType?: string; address?: string }) {
  const parked = booking.status === "checked-in"
  const status = parked ? "Parking now" : booking.status === "confirmed" ? "Confirmed" : "Pending"

  return <article className="driver-booking-row">
    <div className="driver-booking-cell driver-booking-site">
      <span className="driver-booking-cell-label">Parking site</span>
      <h3>{booking.lotName}</h3>
      {address && <p><UntitledIcon name="map-pin" size={15} />{address}</p>}
    </div>
    <div className="driver-booking-cell">
      <span className="driver-booking-cell-label">Vehicle</span>
      <strong className="driver-booking-plate" translate="no">{booking.licensePlate}</strong>
      <span className="driver-booking-secondary">{vehicleType === "motorcycle" ? "Motorcycle" : vehicleType === "car" ? "Car" : "Vehicle"} · Space {booking.slotNumber}</span>
    </div>
    <div className="driver-booking-cell">
      <span className="driver-booking-cell-label">Parking time</span>
      <strong>{dateTime.format(new Date(booking.startTime))}</strong>
      <span className="driver-booking-secondary">Until {dateTime.format(new Date(booking.endTime))}</span>
    </div>
    <div className="driver-booking-cell driver-booking-outcome">
      <span className="driver-booking-cell-label">Status & total</span>
      <span className={`driver-booking-status ${parked ? "is-parking" : booking.status === "pending" ? "is-pending" : "is-upcoming"}`}><span aria-hidden="true" />{status}</span>
      <strong>{currency.format(booking.amount)} VND</strong>
    </div>
  </article>
}

export function ActiveBookings({ onResumeReservation }: { onResumeReservation?: (siteId: string) => void } = {}) {
  const { user } = useApp()
  const [bookings, setBookings] = useState<Booking[]>(() => user ? store.getBookingsByDriver(user.id) : [])
  const [registeredVehicles, setRegisteredVehicles] = useState<RegisteredVehicle[]>([])
  const [, setDataRevision] = useState(0)

  useEffect(() => {
    const refresh = () => {
      setBookings(user ? store.getBookingsByDriver(user.id) : [])
      setDataRevision(current => current + 1)
    }
    refresh()
    window.addEventListener("sp-data-change", refresh)
    return () => window.removeEventListener("sp-data-change", refresh)
  }, [user?.id])

  useEffect(() => {
    setRegisteredVehicles([])
    if (!user) return
    let active = true
    vehiclesApi.list()
      .then(items => { if (active) setRegisteredVehicles(items) })
      .catch(() => { if (active) setRegisteredVehicles([]) })
    return () => { active = false }
  }, [user?.id])

  const parking = bookings.filter((booking) => booking.status === "checked-in")
    .sort((a, b) => b.startTime.localeCompare(a.startTime))
  const lots = store.getLots()
  const vehicles = user ? store.getVehiclesByDriver(user.id) : []

  return <div className="current-bookings driver-current-bookings">
    {user && <FindMyCarCard key={user.id} />}
    {user && <ServerReservations key={user.id} userId={user.id} onResume={onResumeReservation} />}
    {([
      { id: "parking", title: "Parking now", description: "Vehicles currently checked in", records: parking, icon: "parking" as const },
    ]).map(group => <section key={group.id} className="driver-booking-group" aria-labelledby={`driver-booking-${group.id}`}>
      <header className="driver-booking-group-header">
        <div><h2 id={`driver-booking-${group.id}`}>{group.title}</h2><p>{group.description}</p></div>
        <div className="driver-booking-group-count">
          <strong>{group.records.length}</strong>
          <span>{group.id === "parking" ? "parked" : "scheduled"}</span>
        </div>
      </header>
      <div className="driver-booking-list">
        {group.records.length ? group.records.map(booking => <BookingRow
          key={booking.id}
          booking={booking}
          vehicleType={registeredVehicles.find(vehicle => canonicalVietnamesePlate(vehicle.canonicalPlate) === canonicalVietnamesePlate(booking.licensePlate))?.vehicleType.toLowerCase()
            ?? vehicles.find(vehicle => canonicalVietnamesePlate(vehicle.licensePlate) === canonicalVietnamesePlate(booking.licensePlate))?.type}
          address={lots.find(lot => lot.id === booking.lotId)?.address}
        />) : <div className="driver-booking-empty" role="status">
          <UntitledIcon name={group.icon} size={21} />
          <span>{group.id === "parking" ? "No checked-in bookings in this list." : "Your next booking will appear here."}</span>
        </div>}
      </div>
    </section>)}
    {user && <LiveReservations key={`${user.id}:${user.role}`} accountScope={`${user.id}:${user.role}`} />}
  </div>
}

function ServerReservations({ userId, onResume }: { userId: string; onResume?: (siteId: string) => void }) {
  const [result, setResult] = useState<DriverReservation[] | null>(null)
  const [error, setError] = useState("")
  const [retry, setRetry] = useState(0)

  async function load(isCurrent: () => boolean) {
    try {
      const reservations = await driverReservationApi.list()
      if (isCurrent()) { setResult(reservations); setError("") }
    } catch (cause) {
      if (isCurrent()) setError(cause instanceof Error ? cause.message : "Could not load reservations.")
    }
  }

  useEffect(() => {
    let current = true
    void load(() => current)
    return () => { current = false }
  }, [retry])
  useServerRefresh(userId, load)

  return <details className="card driver-recent-reservations">
    <summary className="recent-reservations-header">
      <div><div className="recent-reservations-title"><h2>Recent reservations</h2>
        {result && !error && <span className="recent-reservations-count" aria-label={`${result.length} reservations`}>{result.length}</span>}
      </div><p>Your latest bookings, at a glance.</p></div>
      <span className="recent-reservations-toggle" aria-hidden="true"><UntitledIcon name="chevron-down" size={20} /></span>
    </summary>
    {error && <div className="recent-reservations-message"><p role="alert">{error}</p>
      <button type="button" className="btn-outline" onClick={() => setRetry(value => value + 1)}>Retry</button>
    </div>}
    {!error && !result && <p className="recent-reservations-message" role="status">Loading reservations…</p>}
    {result?.length === 0 && !error && <p className="recent-reservations-message">No reservations yet. Your next booking will appear here.</p>}
    {result && result.length > 0 && <ul className="recent-reservations-list">
      {result.map(item => {
        const site = store.getLots().find(lot => lot.id === item.siteId)
        const attempt = readReservationAttempt(`sp_reservation_attempt:${userId}:${item.siteId}`)
        return <li key={item.reservationId} className="recent-reservation-row">
          <div className="recent-reservation-site">
            <span className="recent-reservation-mark" aria-hidden="true"><UntitledIcon name="parking" size={22} /></span>
            <div><strong>{site?.name || "Parking reservation"}</strong><span className="recent-reservation-date"><UntitledIcon name="calendar" size={14} />{reservationStamp(item.start, reservationDate)}</span></div>
          </div>
          <dl className="recent-reservation-schedule">
            <div><dt>Starts</dt><dd><time dateTime={item.start}>{reservationStamp(item.start, reservationTime)}</time></dd></div>
            <div><dt>Ends</dt><dd><time dateTime={item.end}>{reservationStamp(item.end, reservationTime)}</time>{reservationStamp(item.start, reservationDate) !== reservationStamp(item.end, reservationDate) && <small>{reservationStamp(item.end, reservationDate)}</small>}</dd></div>
          </dl>
          <div className="recent-reservation-outcome"><Badge tone={reservationTone[item.state] || "muted"} className="recent-reservation-status"><span aria-hidden="true" />{formatStatus(item.state)}</Badge>
            {onResume && site && attempt?.reservationId === item.reservationId && item.state === "PENDING_PAYMENT" && <button type="button" className="btn-outline" onClick={() => onResume(site.id)}>Resume reservation<UntitledIcon name="arrow-right" size={16} /></button>}
          </div>
        </li>
      })}
    </ul>}
  </details>
}
