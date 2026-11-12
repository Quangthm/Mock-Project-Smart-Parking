import { useEffect, useRef, useState } from "react"
import { AuthApiError } from "../../../lib/authApi"
import { driverReservationApi, type DriverReservation, type ReservationIntent, type ReservationReceipt, type ReservationSelection, type ReservationTerms } from "../../../lib/driverReservationApi"
import { useServerRefresh } from "../../../lib/serverRefresh"
import { formatStatus } from "../../../lib/formatStatus"
import { PaymentCheckout } from "../payments/PaymentCheckout"

type Attempt = { key: string; intent: ReservationIntent; requestId?: string; reservationId?: string }
const terminal = new Set(["REJECTED", "DENIED", "EXPIRED", "FAILED_DEPENDENCY", "CANCELLED"])

export function readReservationAttempt(storageKey: string): Attempt | null {
  try {
    const saved = JSON.parse(sessionStorage.getItem(storageKey) ?? "null") as Attempt | null
    return saved?.key && saved.intent?.termsId ? saved : null
  } catch { return null }
}

export function ReservationCheckout({ storageKey, selection, onBack, onDone }: {
  storageKey: string
  selection: ReservationSelection | null
  onBack: () => void
  onDone: () => void
}) {
  const [attempt, setAttempt] = useState(() => readReservationAttempt(storageKey))
  const [reviewed, setReviewed] = useState<{ terms: ReservationTerms; selection: ReservationSelection } | null>(null)
  const terms = reviewed?.terms
  const [receipt, setReceipt] = useState<ReservationReceipt | null>(null)
  const [booking, setBooking] = useState<DriverReservation | null>(null)
  const [error, setError] = useState("")
  const [busy, setBusy] = useState(false)
  const locked = useRef(false)
  const mounted = useRef(false)
  useEffect(() => {
    mounted.current = true
    return () => { mounted.current = false }
  }, [])

  function save(value: Attempt) {
    sessionStorage.setItem(storageKey, JSON.stringify(value))
    if (mounted.current) setAttempt(value)
  }

  async function run(action: () => Promise<void>) {
    if (locked.current) return
    locked.current = true
    setBusy(true)
    setError("")
    try { await action() }
    catch (cause) { if (mounted.current) setError(cause instanceof Error ? cause.message : "Reservation could not be loaded.") }
    finally {
      locked.current = false
      if (mounted.current) setBusy(false)
    }
  }

  async function refresh(value: Attempt, submit = false) {
    if (!value.requestId && !submit) return
    let next: ReservationReceipt
    try {
      next = value.requestId
        ? await driverReservationApi.receipt(value.requestId)
        : await driverReservationApi.create(value.intent, value.key)
    } catch (cause) {
      if (!value.requestId && cause instanceof AuthApiError && [400, 422].includes(cause.status)) {
        sessionStorage.removeItem(storageKey)
        if (mounted.current) { setAttempt(null); setReviewed(null) }
      }
      throw cause
    }
    if (!value.requestId || next.reservationId && value.reservationId !== next.reservationId) {
      save({ ...value, requestId: next.requestId, reservationId: next.reservationId ?? undefined })
    }
    if (!mounted.current) return
    setReceipt(next)
    if (next.reservationId && !terminal.has(next.state)) {
      const current = await driverReservationApi.read(next.reservationId)
      if (mounted.current) setBooking(current)
    }
  }

  function review() {
    if (!selection) return
    void run(async () => {
      const data = await driverReservationApi.review(selection)
      if (mounted.current) setReviewed({ terms: data, selection })
    })
  }

  function confirm() {
    if (!reviewed || !terms) return
    if (JSON.stringify(selection) !== JSON.stringify(reviewed.selection) || !(Date.parse(terms.validUntil) > Date.now())) {
      setReviewed(null)
      setError("The selection changed or the terms expired. Review the terms again before confirming.")
      return
    }
    void run(async () => {
      const value: Attempt = { key: crypto.randomUUID(), intent: {
        ...reviewed.selection, mode: "SPECIFIC_SLOT", termsId: terms.termsId, termsVersion: terms.termsVersion,
      } }
      save(value)
      await refresh(value, true)
    })
  }

  useEffect(() => {
    if (attempt?.requestId) void run(() => refresh(attempt))
  // Resume an acknowledged request when opening this checkout.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const failed = Boolean(receipt && terminal.has(receipt.state) || booking && terminal.has(booking.state))
  const confirmed = !failed && booking?.state === "CONFIRMED"
  useServerRefresh(attempt?.requestId && !failed && !confirmed ? attempt.requestId : undefined,
    () => attempt ? run(() => refresh(attempt)) : undefined, 5000)

  const orderId = !failed && booking?.state === "PENDING_PAYMENT" && booking.holdExpiresAt &&
    Date.parse(booking.holdExpiresAt) > Date.now() && attempt?.intent.paymentMethod === "PAYOS" ? booking.orderId : null
  useEffect(() => {
    if (!orderId || !booking?.reservationId) return
    try { sessionStorage.setItem(`sp_reservation_order:${orderId}`, booking.reservationId) }
    catch { /* Current Bookings remains available if this browser cannot save the return reference. */ }
  }, [orderId, booking?.reservationId])

  function closeAttempt() {
    sessionStorage.removeItem(storageKey)
    onDone()
  }

  return <section className="card booking-stage-right space-y-4" aria-labelledby="reservation-checkout-title">
    <h3 id="reservation-checkout-title" className="text-lg font-bold">Reservation checkout</h3>
    {error && <p role="alert">{error}</p>}
    {!attempt && !terms && <p>Load the current price and deposit terms before confirming your reservation.</p>}
    {!attempt && terms && <>
      <dl className="grid gap-3 sm:grid-cols-2">
        <div><dt>Booking total</dt><dd>{terms.commercial.bookedAmount.toLocaleString("vi-VN")} {terms.commercial.currency}</dd></div>
        <div><dt>Deposit</dt><dd>{terms.commercial.depositAmount.toLocaleString("vi-VN")} {terms.commercial.currency}</dd></div>
      </dl>
      <p className="whitespace-pre-wrap break-words">{terms.commercial.disclosure}</p>
    </>}
    {attempt && <p role="status">{confirmed ? "Reservation confirmed." : failed
      ? formatStatus(receipt?.code || booking?.state || receipt?.state || "Reservation failed")
      : booking?.state === "PENDING_PAYMENT"
        ? orderId ? "Your payment order is ready." : attempt.intent.paymentMethod === "CASH"
          ? "Waiting for Operations to confirm the cash reservation."
          : "Waiting for the reservation payment order."
        : attempt.requestId ? "Operations is processing your reservation." : "The request has no receipt yet. Retry to check the same request."}</p>}
    {orderId && <PaymentCheckout key={orderId} showMethodPicker={false} bookingOrderId={orderId} holdExpiresAt={booking?.holdExpiresAt ?? undefined} />}
    <div className="flex flex-wrap gap-3">
      {!attempt && <button type="button" className="btn-outline" disabled={busy} onClick={onBack}>Back to payment methods</button>}
      {!attempt && <button type="button" className="btn-primary" disabled={busy || !selection} onClick={terms ? confirm : review}>
        {busy ? "Loading…" : terms ? "Accept terms and reserve" : "Review price and terms"}
      </button>}
      {attempt && !failed && !confirmed && <button type="button" className="btn-outline" disabled={busy} onClick={() => void run(() => refresh(attempt, true))}>
        {busy ? "Checking…" : attempt.requestId ? "Check reservation status" : "Retry same request"}
      </button>}
      {(failed || confirmed) && <button type="button" className="btn-primary" onClick={closeAttempt}>Done</button>}
    </div>
  </section>
}
