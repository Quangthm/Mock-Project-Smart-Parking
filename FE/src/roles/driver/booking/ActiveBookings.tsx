import { useEffect, useState } from "react"

import { useApp } from "../../../context/AppContext"
import type { Booking } from "../../../lib/types"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { driverData as store } from "../data/data"

type BookingGroup = "parking" | "upcoming"

const dateTime = new Intl.DateTimeFormat("en-US", { dateStyle: "medium", timeStyle: "short" })
const currency = new Intl.NumberFormat("vi-VN")

export function ActiveBookings() {
  const { user } = useApp()
  const [group, setGroup] = useState<BookingGroup>("parking")
  const [bookings, setBookings] = useState<Booking[]>(() => user ? store.getBookingsByDriver(user.id) : [])

  useEffect(() => {
    const refresh = () => setBookings(user ? store.getBookingsByDriver(user.id) : [])
    refresh()
    window.addEventListener("sp-data-change", refresh)
    return () => window.removeEventListener("sp-data-change", refresh)
  }, [user?.id])

  const parking = bookings.filter((booking) => booking.status === "checked-in")
    .sort((a, b) => b.startTime.localeCompare(a.startTime))
  const upcoming = bookings.filter((booking) => booking.status === "confirmed" || booking.status === "pending")
    .sort((a, b) => a.startTime.localeCompare(b.startTime))
  const visible = group === "parking" ? parking : upcoming
  const lots = store.getLots()

  return (
    <section className="current-bookings" aria-label="Current bookings">
      <div className="current-bookings-tabs" role="group" aria-label="Booking status">
        <button type="button" aria-pressed={group === "parking"} className={group === "parking" ? "is-active" : ""} onClick={() => setGroup("parking")}>
          <span className="current-bookings-tab-dot" aria-hidden="true" />Parking now <span className="current-bookings-count">{parking.length}</span>
        </button>
        <button type="button" aria-pressed={group === "upcoming"} className={group === "upcoming" ? "is-active" : ""} onClick={() => setGroup("upcoming")}>
          <UntitledIcon name="calendar" size={17} />Upcoming <span className="current-bookings-count">{upcoming.length}</span>
        </button>
      </div>

      <div className="current-bookings-panel" role="region" aria-label={group === "parking" ? "Parking now" : "Upcoming bookings"}>
        {visible.length ? visible.map((booking) => {
          const lot = lots.find((item) => item.id === booking.lotId)
          return <article className="current-booking-card" key={booking.id}>
            <div className="current-booking-space" aria-label={`Space ${booking.slotNumber}`}>
              <small>SPACE</small><strong>{booking.slotNumber}</strong>
            </div>
            <div className="current-booking-info">
              <div className="current-booking-eyebrow"><span>{group === "parking" ? "Parking now" : "Upcoming"}</span><span>Booking {booking.id}</span></div>
              <h3>{booking.lotName}</h3>
              {lot?.address && <p className="current-booking-address"><UntitledIcon name="map-pin" size={16} />{lot.address}</p>}
              <dl className="current-booking-details">
                <div><dt>License plate</dt><dd>{booking.licensePlate}</dd></div>
                <div><dt>Time</dt><dd>{dateTime.format(new Date(booking.startTime))} – {dateTime.format(new Date(booking.endTime))}</dd></div>
                <div><dt>Estimated total</dt><dd>{currency.format(booking.amount)} VND</dd></div>
              </dl>
            </div>
          </article>
        }) : <div className="current-bookings-empty" role="status">
          <UntitledIcon name={group === "parking" ? "parking" : "calendar"} size={24} />
          <strong>{group === "parking" ? "No parking session in progress" : "No upcoming bookings"}</strong>
          <p>{group === "parking" ? "Your active parking session will appear here after check-in." : "Your next confirmed reservation will appear here."}</p>
        </div>}
      </div>
    </section>
  )
}
