import { useEffect, useState } from "react"
import { getSessionGeneration } from "../../../lib/authApi"
import { reservationsApi, type OwnReservation } from "../../../lib/reservationsApi"
import { formatStatus } from "../../../lib/formatStatus"
import { useReservationProgress } from "../../../lib/reservationProgress"
import { driverData as store } from "../data/data"
import { PaymentCheckout } from "../payments/PaymentCheckout"

export function LiveReservations({ accountScope }: { accountScope: string }) {
  const [items, setItems] = useState<OwnReservation[]>([])
  const [error, setError] = useState("")
  const [loading, setLoading] = useState(true)
  async function refresh(isCurrent: () => boolean) {
    const generation = getSessionGeneration()
    try {
      const result = await reservationsApi.list()
      if (!isCurrent() || generation !== getSessionGeneration()) return
      setItems(result); setError("")
    } catch (failure) {
      if (isCurrent() && generation === getSessionGeneration()) setError(failure instanceof Error ? failure.message : "Could not load reservations.")
    } finally { if (isCurrent() && generation === getSessionGeneration()) setLoading(false) }
  }
  useEffect(() => {
    let active = true
    setItems([]); setLoading(true)
    const run = () => { void refresh(() => active) }
    const restore = (event: PageTransitionEvent) => { if (event.persisted) run() }
    run()
    window.addEventListener("sp-data-change", run)
    window.addEventListener("pageshow", restore)
    return () => {
      active = false
      window.removeEventListener("sp-data-change", run)
      window.removeEventListener("pageshow", restore)
    }
  }, [accountScope])
  const active = items.filter(item => ["PENDING_PAYMENT", "CONFIRMED"].includes(item.state))
    .sort((a, b) => a.start.localeCompare(b.start))
  return <section className="driver-booking-group" aria-label="Upcoming reservations">
    <header className="driver-booking-group-header"><div><h2>Upcoming bookings</h2><p>Reservations and confirmation from SmartParking</p></div><span>{active.length}</span></header>
    {error && <p role="alert">{error}</p>}
    {loading ? <p role="status">Loading reservations…</p> : !active.length && !error ? <div className="driver-booking-empty">Your next booking will appear here.</div> : null}
    <div className="driver-booking-list">{active.map(item =>
      <UpcomingReservation key={item.reservationId} booking={item} />
    )}</div>
  </section>
}

function UpcomingReservation({ booking }: { booking: OwnReservation }) {
  const [paying, setPaying] = useState(false)
  const { snapshot, connected, reconnect } = useReservationProgress(
    booking.state === "PENDING_PAYMENT" ? booking.requestId : undefined,
  )
  const item = snapshot?.reservationId === booking.reservationId ? {
    ...booking, state: snapshot.reservationState ?? booking.state, orderId: snapshot.orderId,
    holdExpiresAt: snapshot.holdExpiresAt,
  } : booking
  const lot = store.getLots().find(lot => lot.id === item.siteId)
  const slot = lot?.slots.find(slot => slot.id === item.requestedSlotId)
  return <article className="card" style={{ display: "grid", gap: 16 }}>
    <div className="driver-booking-row">
      <div className="driver-booking-cell"><span className="driver-booking-cell-label">Parking site</span><h3>{lot?.name ?? "Parking site"}</h3><p>{lot?.address ?? item.siteId}</p></div>
      <div className="driver-booking-cell"><span className="driver-booking-cell-label">Requested space</span><strong>{slot?.number ?? item.requestedSlotId ?? "Not assigned"}</strong></div>
      <div className="driver-booking-cell"><span className="driver-booking-cell-label">Parking time</span><strong>{new Date(item.start).toLocaleString()}</strong><span>Until {new Date(item.end).toLocaleString()}</span></div>
      <div className="driver-booking-cell"><span className="driver-booking-status">{formatStatus(item.state)}</span><strong>{item.bookedAmount == null ? "Price unavailable" : `${item.bookedAmount.toLocaleString("vi-VN")} VND`}</strong></div>
    </div>
    {item.state === "PENDING_PAYMENT" && <>
      {item.holdExpiresAt && <p>Payment deadline: {new Date(item.holdExpiresAt).toLocaleString()}</p>}
      {!connected ? <button type="button" className="btn-outline" onClick={reconnect}>Reconnect for payment updates</button> : item.orderId && snapshot?.paymentReadiness === "AVAILABLE" ? <>
        <button type="button" className="btn-outline" aria-expanded={paying} onClick={() => setPaying(value => !value)}>{paying ? "Hide payment" : "View payment"}</button>
        {paying && <PaymentCheckout key={item.orderId} showMethodPicker={false} reservationOrderId={item.orderId} reservationRequestId={item.requestId ?? undefined} holdExpiresAt={item.holdExpiresAt} />}
      </> : <p role="status">Preparing secure payment…</p>}
    </>}
  </article>
}
