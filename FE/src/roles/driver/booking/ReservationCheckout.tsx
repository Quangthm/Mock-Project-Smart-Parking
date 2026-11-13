import { useEffect, useRef, useState } from "react"
import { getSessionGeneration } from "../../../lib/authApi"
import { reservationsApi, type OwnReservation, type ReservationReceipt, type ReservationSelection, type ReservationTerms } from "../../../lib/reservationsApi"
import { useServerRefresh } from "../../../lib/serverRefresh"
import { PaymentCheckout } from "../payments/PaymentCheckout"

interface Attempt { key: string; selection: ReservationSelection; terms: ReservationTerms; receipt?: ReservationReceipt }
const terminalReceipts = ["DENIED", "REJECTED", "FAILED_DEPENDENCY"]
export function ReservationCheckout({ selection, accountScope, onDone }: {
  selection: ReservationSelection; accountScope: string; onDone: () => void
}) {
  const storageKey = `sp:reservation:${accountScope}:${selection.siteId}`
  const [attempt, setAttempt] = useState<Attempt | null>(() => {
    try { return JSON.parse(sessionStorage.getItem(storageKey) ?? "null") } catch { return null }
  })
  const [terms, setTerms] = useState<ReservationTerms | null>(null)
  const [booking, setBooking] = useState<OwnReservation | null>(null)
  const [accepted, setAccepted] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState("")
  const [now, setNow] = useState(Date.now)
  const mounted = useRef(true)
  const lock = useRef(false)
  const generation = useRef(getSessionGeneration())
  const current = () => mounted.current && generation.current === getSessionGeneration()
  useEffect(() => { mounted.current = true; return () => { mounted.current = false } }, [])
  const save = (value: Attempt) => {
    if (!current()) return
    sessionStorage.setItem(storageKey, JSON.stringify(value))
    setAttempt(value)
  }
  async function refresh(isCurrent = current) {
    if (!attempt?.receipt || terminalReceipts.includes(attempt.receipt.state)) return
    try {
      const receipt = await reservationsApi.receipt(attempt.receipt.requestId)
      if (!current() || !isCurrent()) return
      save({ ...attempt, receipt })
      if (receipt.reservationId) {
        const result = await reservationsApi.booking(receipt.reservationId)
        if (!current() || !isCurrent()) return
        setBooking(result)
      }
      setError("")
    } catch (failure) {
      if (current() && isCurrent()) setError(failure instanceof Error ? failure.message : "Could not refresh reservation.")
    }
  }
  useEffect(() => { void refresh(); /* Resume the same receipt after reload. */ }, [attempt?.receipt?.requestId])
  useServerRefresh(attempt?.receipt?.requestId, refresh, 5000)
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(timer)
  }, [])

  async function review() {
    if (lock.current) return
    lock.current = true; setBusy(true); setError(""); setAccepted(false); setTerms(null)
    try {
      const result = await reservationsApi.review(selection)
      if (current()) setTerms(result)
    } catch (failure) {
      if (current()) setError(failure instanceof Error ? failure.message : "Could not load reservation terms.")
    } finally { lock.current = false; if (current()) setBusy(false) }
  }
  async function submit() {
    if (lock.current || (!attempt && (!terms || !accepted))) return
    if (!attempt && terms && Date.parse(terms.validUntil) <= Date.now()) {
      setError("These terms have expired. Review the latest price and terms."); return
    }
    const intent = attempt ?? { key: crypto.randomUUID(), selection, terms: terms! }
    lock.current = true; setBusy(true); setError("")
    try {
      // Persist before sending so an ambiguous response can only retry the same intent.
      save(intent)
      const receipt = await reservationsApi.submit(intent.selection, intent.terms, intent.key)
      if (current()) save({ ...intent, receipt })
    } catch (failure) {
      if (current()) setError(failure instanceof Error ? failure.message : "Could not submit reservation. Retry the same request.")
    } finally { lock.current = false; if (current()) setBusy(false) }
  }
  const failed = attempt?.receipt && terminalReceipts.includes(attempt.receipt.state)
  const expired = booking?.holdExpiresAt ? Date.parse(booking.holdExpiresAt) <= now : false
  const money = (value: number | null) => value == null ? "Not available" : `${value.toLocaleString("vi-VN")} VND`
  return <section className="card" style={{ display: "grid", gap: 16 }}>
    <h3>Reservation checkout</h3>
    {error && <p role="alert">{error}</p>}
    {!attempt ? <>
      <button type="button" className="btn-primary" disabled={busy} onClick={() => void review()}>{busy ? "Loading…" : "Review price and terms"}</button>
      {terms && <>
        <dl style={{ display: "grid", gap: 12 }}>
          <div><dt>Booked total</dt><dd>{money(terms.commercial.bookedAmount)}</dd></div>
          <div><dt>Due now</dt><dd>{money(terms.commercial.depositAmount)}</dd></div>
          <div><dt>Terms version</dt><dd>{terms.termsVersion}</dd></div>
          <div><dt>Valid until</dt><dd>{new Date(terms.validUntil).toLocaleString()}</dd></div>
        </dl>
        <p>{terms.commercial.disclosure}</p>
        <details><summary>Operational terms</summary><dl>{Object.entries(terms.operationalPolicy.values).map(([key, value]) => <div key={key}><dt>{key.replace(/_/g, " ")}</dt><dd>{value}</dd></div>)}</dl></details>
        <p>Prepayment: {terms.commercial.prepaymentMode}. Any amount due now must be paid before the reservation can be confirmed.</p>
        <label style={{ display: "flex", gap: 12, alignItems: "center" }}><input type="checkbox" checked={accepted} onChange={event => setAccepted(event.target.checked)} />I accept this price and the reservation terms.</label>
        <button type="button" className="btn-primary" disabled={busy || !accepted || Date.parse(terms.validUntil) <= now} onClick={() => void submit()}>Accept and reserve</button>
      </>}
    </> : <>
      <p role="status">{booking?.state === "CONFIRMED" ? "Reservation confirmed" : failed ? `Request ${attempt.receipt?.state}: ${attempt.receipt?.code ?? "Please review your selection."}` : booking ? `Reservation: ${booking.state.replace(/_/g, " ")}` : "Processing reservation request…"}</p>
      {!attempt.receipt && <button type="button" className="btn-outline" disabled={busy} onClick={() => void submit()}>{busy ? "Sending…" : "Retry same request"}</button>}
      {booking && <>
        <p>Booked total: {money(booking.bookedAmount)} · Due now: {money(booking.depositAmount)}</p>
        {booking.holdExpiresAt && <p>Payment deadline: {new Date(booking.holdExpiresAt).toLocaleString()}</p>}
        {booking.state === "PENDING_PAYMENT" && (booking.orderId
          ? <PaymentCheckout key={booking.orderId} showMethodPicker={false} reservationOrderId={booking.orderId} holdExpiresAt={booking.holdExpiresAt} />
          : <p role="status">{expired ? "The payment deadline has passed. Checking the reservation outcome." : "Preparing secure payment…"}</p>)}
      </>}
      {attempt.receipt && <button type="button" className="btn-outline" onClick={() => void refresh()}>Refresh reservation status</button>}
      {(failed || booking && ["CONFIRMED", "EXPIRED", "CANCELLED", "REJECTED"].includes(booking.state)) && <button type="button" className="btn-primary" onClick={() => { sessionStorage.removeItem(storageKey); window.dispatchEvent(new Event("sp-data-change")); onDone() }}>{booking?.state === "CONFIRMED" ? "Done" : "Back to booking"}</button>}
    </>}
  </section>
}
