import { useEffect, useState } from "react"

import { useApp } from "../../../context/AppContext"
import type { Booking, DriverVehicle } from "../../../lib/types"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { driverData as store } from "../data/data"
import "../../../booking-support.css"

const dateTime = new Intl.DateTimeFormat("en-US", { dateStyle: "medium", timeStyle: "short" })
const currency = new Intl.NumberFormat("vi-VN")

function BookingRow({ booking, vehicle, address }: { booking: Booking; vehicle?: DriverVehicle; address?: string }) {
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
      <span className="driver-booking-secondary">{vehicle?.type === "motorcycle" ? "Motorcycle" : vehicle?.type === "car" ? "Car" : "Vehicle"} · Space {booking.slotNumber}</span>
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

export function ActiveBookings() {
  const { user } = useApp()
  const [bookings, setBookings] = useState<Booking[]>(() => user ? store.getBookingsByDriver(user.id) : [])
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

  const parking = bookings.filter((booking) => booking.status === "checked-in")
    .sort((a, b) => b.startTime.localeCompare(a.startTime))
  const upcoming = bookings.filter((booking) => booking.status === "confirmed" || booking.status === "pending")
    .sort((a, b) => a.startTime.localeCompare(b.startTime))
  const lots = store.getLots()
  const vehicles = user ? store.getVehiclesByDriver(user.id) : []

  return <div className="current-bookings driver-current-bookings">
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
          vehicle={vehicles.find(vehicle => vehicle.licensePlate === booking.licensePlate)}
          address={lots.find(lot => lot.id === booking.lotId)?.address}
        />) : <div className="driver-booking-empty" role="status">
          <UntitledIcon name={group.icon} size={21} />
          <span>{group.id === "parking" ? "No vehicle is parked right now." : "Your next booking will appear here."}</span>
        </div>}
      </div>
    </section>)}
  </div>
}
