import { useEffect, useState } from "react"
import { getSessionGeneration } from "../../../lib/authApi"
import { reservationsApi, type OwnReservation } from "../../../lib/reservationsApi"
import { useServerRefresh } from "../../../lib/serverRefresh"
import { driverData as store } from "../data/data"
import { PaymentCheckout } from "../payments/PaymentCheckout"

export function LiveReservations({ accountScope }: { accountScope: string }) {
  const [items, setItems] = useState<OwnReservation[]>([])
  const [error, setError] = useState("")
  const [loading, setLoading] = useState(true)
  const [paying, setPaying] = useState<string | null>(null)
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
    setItems([]); setLoading(true); setPaying(null)
    const run = () => { void refresh(() => active) }
    run()
    window.addEventListener("sp-data-change", run)
    return () => { active = false; window.removeEventListener("sp-data-change", run) }
  }, [accountScope])
  useServerRefresh(accountScope, refresh, 5000)
  const active = items.filter(item => ["PENDING_PAYMENT", "CONFIRMED"].includes(item.state))
    .sort((a, b) => a.start.localeCompare(b.start))
  const lots = store.getLots()
  return <section className="driver-booking-group" aria-label="Upcoming reservations">
    <header className="driver-booking-group-header"><div><h2>Upcoming bookings</h2><p>Reservations and confirmation from SmartParking</p></div><span>{active.length}</span></header>
    {error && <p role="alert">{error}</p>}
    {loading ? <p role="status">Loading reservations…</p> : !active.length && !error ? <div className="driver-booking-empty">Your next booking will appear here.</div> : null}
    <div className="driver-booking-list">{active.map(item => {
      const lot = lots.find(lot => lot.id === item.siteId)
      const slot = lot?.slots.find(slot => slot.id === item.requestedSlotId)
      return <article key={item.reservationId} className="card" style={{ display: "grid", gap: 16 }}>
        <div className="driver-booking-row">
          <div className="driver-booking-cell"><span className="driver-booking-cell-label">Parking site</span><h3>{lot?.name ?? "Parking site"}</h3><p>{lot?.address ?? item.siteId}</p></div>
          <div className="driver-booking-cell"><span className="driver-booking-cell-label">Requested space</span><strong>{slot?.number ?? item.requestedSlotId ?? "Not assigned"}</strong></div>
          <div className="driver-booking-cell"><span className="driver-booking-cell-label">Parking time</span><strong>{new Date(item.start).toLocaleString()}</strong><span>Until {new Date(item.end).toLocaleString()}</span></div>
          <div className="driver-booking-cell"><span className="driver-booking-status">{item.state === "CONFIRMED" ? "Confirmed" : "Pending payment"}</span><strong>{item.bookedAmount == null ? "Price unavailable" : `${item.bookedAmount.toLocaleString("vi-VN")} VND`}</strong></div>
        </div>
        {item.state === "PENDING_PAYMENT" && <>
          {item.holdExpiresAt && <p>Payment deadline: {new Date(item.holdExpiresAt).toLocaleString()}</p>}
          {item.orderId ? <>
            <button type="button" className="btn-outline" aria-expanded={paying === item.reservationId} onClick={() => setPaying(paying === item.reservationId ? null : item.reservationId)}>{paying === item.reservationId ? "Hide payment" : "View payment"}</button>
            {paying === item.reservationId && <PaymentCheckout key={item.orderId} showMethodPicker={false} reservationOrderId={item.orderId} holdExpiresAt={item.holdExpiresAt} />}
          </> : <p role="status">Preparing secure payment…</p>}
        </>}
      </article>
    })}</div>
  </section>
}
