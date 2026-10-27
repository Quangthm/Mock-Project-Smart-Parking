import { useEffect, useState } from "react"

import { useApp } from "../../../context/AppContext"
import { ParkingHistory, type ParkingHistoryRecord } from "../../../components/history/ParkingHistory"
import { PageHeader } from "../../../components/ui/PageHeader"
import "../../../booking-support.css"

import { driverData as store } from "../data/data"

const dateTime = new Intl.DateTimeFormat("en-US", { dateStyle: "medium", timeStyle: "short" })
const currency = new Intl.NumberFormat("vi-VN")

export function BookingHistory() {
  const { user } = useApp()

  const [, setDataRevision] = useState(0)

  useEffect(() => {
    const refresh = () => setDataRevision(current => current + 1)
    window.addEventListener("sp-data-change", refresh)
    return () => window.removeEventListener("sp-data-change", refresh)
  }, [])

  const bookings = user ? store.getBookingsByDriver(user.id) : []
  const lots = store.getLots()
  const historyRecords: ParkingHistoryRecord[] = bookings
    .filter((booking) => booking.status === "completed" || booking.status === "cancelled")
    .sort((a, b) => (b.completedAt || b.endTime).localeCompare(a.completedAt || a.endTime))
    .map((booking) => ({
      id: booking.id,
      plate: booking.licensePlate,
      title: booking.lotName,
      subtitle: lots.find((lot) => lot.id === booking.lotId)?.address,
      slot: booking.slotNumber,
      status: booking.status.toUpperCase(),
      kind: "Reserved",
      startedAt: booking.startTime,
      endedAt: booking.endTime,
      details: [
        { label: "Booking ID", value: booking.id },
        { label: "Payment status", value: booking.paymentStatus || "—" },
        { label: "Amount", value: `${currency.format(booking.amount)} VND` },
        { label: "Created at", value: Number.isNaN(new Date(booking.createdAt).getTime()) ? "—" : dateTime.format(new Date(booking.createdAt)) },
      ],
    }))

  return (
    <div className="driver-history-list">
      <PageHeader title="Booking History" variant="compact" />
      <div className="driver-history-content">
        <ParkingHistory
          title="Your Parking History"
          scope="Completed and cancelled bookings"
          scopeKey={user?.id ?? ""}
          records={historyRecords}
          dateLabel="Booked for"
          emptyMessage="Your completed or cancelled bookings will appear here."
        />
      </div>
    </div>
  )
}
