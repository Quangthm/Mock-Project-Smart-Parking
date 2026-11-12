import { useEffect, useState } from "react"
import { driverReservationApi, type DriverReservation } from "../../../lib/driverReservationApi"
import { formatStatus } from "../../../lib/formatStatus"
import { useServerRefresh } from "../../../lib/serverRefresh"
import { readReservationAttempt } from "./ReservationCheckout"

import { useApp } from "../../../context/AppContext"
import type { Booking } from "../../../lib/types"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { canonicalVietnamesePlate, vehiclesApi, type RegisteredVehicle } from "../../../lib/vehiclesApi"
import { driverData as store } from "../data/data"
import { FindMyCarCard } from "../parking/FindMyCarCard"
import "../../../booking-support.css"

const dateTime = new Intl.DateTimeFormat("en-US", { dateStyle: "medium", timeStyle: "short" })
const currency = new Intl.NumberFormat("vi-VN")

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
  const upcoming = bookings.filter((booking) => booking.status === "confirmed" || booking.status === "pending")
    .sort((a, b) => a.startTime.localeCompare(b.startTime))
  const lots = store.getLots()
  const vehicles = user ? store.getVehiclesByDriver(user.id) : []

  return <div className="current-bookings driver-current-bookings">
    {user && <FindMyCarCard key={user.id} />}
    {user && <ServerReservations key={user.id} userId={user.id} onResume={onResumeReservation} />}
    {([
      { id: "parking", title: "Parking now", description: "Vehicles currently checked in", records: parking, icon: "parking" as const },
      { id: "upcoming", title: "Upcoming bookings", description: "Confirmed and pending reservations", records: upcoming, icon: "calendar" as const },
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

  return <section className="card space-y-3" aria-labelledby="server-reservations">
    <h2 id="server-reservations" className="text-lg font-bold">Recent reservations</h2>
    {error && <div className="space-y-2"><p role="alert">{error}</p>
      <button type="button" className="btn-outline" onClick={() => setRetry(value => value + 1)}>Retry</button>
    </div>}
    {!error && !result && <p role="status">Loading reservations…</p>}
    {result?.length === 0 && !error && <p>No reservations yet.</p>}
    {result && result.length > 0 && <ul className="divide-y divide-[var(--border)]">
      {result.map(item => {
        const site = store.getLots().find(lot => lot.id === item.siteId)
        const attempt = readReservationAttempt(`sp_reservation_attempt:${userId}:${item.siteId}`)
        return <li key={item.reservationId} className="flex flex-wrap items-center justify-between gap-3 py-3">
          <div className="min-w-0 break-words">
            <strong>{site?.name || "Parking reservation"}</strong>
            <p className="text-sm text-[var(--muted)]">{new Date(item.start).toLocaleString("en-VN")} – {new Date(item.end).toLocaleString("en-VN")}</p>
            <p className="text-sm">{formatStatus(item.state)}</p>
          </div>
          {onResume && site && attempt?.reservationId === item.reservationId && item.state === "PENDING_PAYMENT" && <button type="button" className="btn-outline" onClick={() => onResume(site.id)}>Resume reservation</button>}
        </li>
      })}
    </ul>}
  </section>
}
