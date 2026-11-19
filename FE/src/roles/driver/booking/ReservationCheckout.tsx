import { useEffect, useRef, useState } from "react"
import { getSessionGeneration } from "../../../lib/authApi"
import { driverReservationApi, type ReservationSelection } from "../../../lib/driverReservationApi"
import { formatStatus } from "../../../lib/formatStatus"
import {
  reservationsApi,
  type ReservationReceipt,
  type ReservationTerms,
} from "../../../lib/reservationsApi"
import { useReservationProgress } from "../../../lib/reservationProgress"
import { PaymentCheckout } from "../payments/PaymentCheckout"

type Attempt = {
  key: string
  intent: ReservationSelection & {
    mode: "SPECIFIC_SLOT"
    termsId: string
    termsVersion: string
  }
  requestId?: string
  reservationId?: string
}

const terminal = new Set(["REJECTED", "DENIED", "EXPIRED", "FAILED_DEPENDENCY", "TIMED_OUT", "DEPENDENCY_UNAVAILABLE", "CANCELLED", "UNFULFILLABLE"])

export function readReservationAttempt(storageKey: string): Attempt | null {
  try {
    const saved = JSON.parse(sessionStorage.getItem(storageKey) ?? "null") as Attempt | null
    return saved?.key && saved.intent?.termsId ? saved : null
  } catch {
    return null
  }
}

export function ReservationCheckout({ storageKey, selection, onBack, onDone }: {
  storageKey: string
  selection: ReservationSelection | null
  onBack: () => void
  onDone: () => void
}) {
  const [attempt, setAttempt] = useState<Attempt | null>(() => readReservationAttempt(storageKey))
  const [reviewed, setReviewed] = useState<{ terms: ReservationTerms; selection: ReservationSelection } | null>(null)
  const [accepted, setAccepted] = useState(false)
  const [receipt, setReceipt] = useState<ReservationReceipt | null>(null)
  const { snapshot, connected, reconnect } = useReservationProgress(attempt?.requestId)
  const [error, setError] = useState("")
  const [busy, setBusy] = useState(false)
  const [now, setNow] = useState(Date.now)
  const locked = useRef(false)
  const mounted = useRef(false)
  const generation = useRef(getSessionGeneration())
  const current = () => mounted.current && generation.current === getSessionGeneration()

  useEffect(() => {
    mounted.current = true
    return () => { mounted.current = false }
  }, [])

  function save(value: Attempt) {
    if (!current()) return
    sessionStorage.setItem(storageKey, JSON.stringify(value))
    setAttempt(value)
  }

  async function run(action: () => Promise<void>) {
    if (locked.current || !current()) return
    locked.current = true
    setBusy(true)
    setError("")
    try {
      await action()
    } catch (cause) {
      if (current()) setError(cause instanceof Error ? cause.message : "Reservation could not be loaded.")
    } finally {
      locked.current = false
      if (current()) setBusy(false)
    }
  }

  async function submit(value: Attempt) {
    // An ambiguous create response is retried only with its persisted intent and key.
    const next = await driverReservationApi.create(value.intent, value.key)
    if (!current()) return

    save({ ...value, requestId: next.requestId, reservationId: next.reservationId ?? undefined })
    setReceipt(next)
  }

  function review() {
    if (!selection) return
    void run(async () => {
      const terms = await reservationsApi.review(selection)
      if (current()) {
        setReviewed({ terms, selection })
        setAccepted(false)
      }
    })
  }

  function confirm() {
    if (!reviewed || !accepted) return
    if (JSON.stringify(selection) !== JSON.stringify(reviewed.selection) ||
        Date.parse(reviewed.terms.validUntil) <= Date.now()) {
      setReviewed(null)
      setAccepted(false)
      setError("The selection changed or the terms expired. Review the terms again before confirming.")
      return
    }

    void run(async () => {
      const value: Attempt = {
        key: crypto.randomUUID(),
        intent: {
          ...reviewed.selection,
          mode: "SPECIFIC_SLOT",
          termsId: reviewed.terms.termsId,
          termsVersion: reviewed.terms.termsVersion,
        },
      }
      // Persist before the HTTP request so retries cannot create a second intent.
      save(value)
      await submit(value)
    })
  }

  useEffect(() => {
    if (attempt && snapshot?.reservationId && attempt.reservationId !== snapshot.reservationId) {
      save({ ...attempt, reservationId: snapshot.reservationId })
    }
  }, [attempt, snapshot?.reservationId])

  const failed = Boolean(
    terminal.has(snapshot?.admissionState ?? receipt?.state ?? "") || terminal.has(snapshot?.reservationState ?? ""),
  )
  const confirmed = !failed && snapshot?.reservationState === "CONFIRMED"

  useEffect(() => {
    if (!snapshot?.holdExpiresAt || confirmed || failed) return
    const timer = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(timer)
  }, [snapshot?.holdExpiresAt, confirmed, failed])

  const holdExpired = Boolean(snapshot?.holdExpiresAt && Date.parse(snapshot.holdExpiresAt) <= now)
  const orderId = connected && !failed && snapshot?.reservationState === "PENDING_PAYMENT" &&
    snapshot.paymentReadiness === "AVAILABLE" && !holdExpired && attempt?.intent.paymentMethod === "PAYOS" ? snapshot.orderId : null

  function closeAttempt() {
    if (!current()) return
    sessionStorage.removeItem(storageKey)
    window.dispatchEvent(new Event("sp-data-change"))
    onDone()
  }

  const money = (amount: number | null, currency = "VND") =>
    amount == null ? "Not available" : `${amount.toLocaleString("vi-VN")} ${currency}`

  return <section className="card booking-stage-right space-y-4" aria-labelledby="reservation-checkout-title">
    <h3 id="reservation-checkout-title" className="text-lg font-bold">Reservation checkout</h3>
    {error && <p role="alert">{error}</p>}

    {!attempt && !reviewed && <p>Load the current price and deposit terms before confirming your reservation.</p>}
    {!attempt && reviewed && <>
      <dl className="grid gap-3 sm:grid-cols-2">
        <div><dt>Booking total</dt><dd>{money(reviewed.terms.commercial.bookedAmount, reviewed.terms.commercial.currency)}</dd></div>
        <div><dt>Deposit due now</dt><dd>{money(reviewed.terms.commercial.depositAmount, reviewed.terms.commercial.currency)}</dd></div>
        <div><dt>Terms version</dt><dd>{reviewed.terms.termsVersion}</dd></div>
        <div><dt>Valid until</dt><dd>{new Date(reviewed.terms.validUntil).toLocaleString()}</dd></div>
      </dl>
      <p className="whitespace-pre-wrap break-words">{reviewed.terms.commercial.disclosure}</p>
      <details>
        <summary>Operational terms</summary>
        <dl>{Object.entries(reviewed.terms.operationalPolicy.values).map(([key, value]) =>
          <div key={key}><dt>{key.replace(/_/g, " ")}</dt><dd>{value}</dd></div>
        )}</dl>
      </details>
      <p>Prepayment: {reviewed.terms.commercial.prepaymentMode}. Payment alone does not confirm a reservation.</p>
      <label className="flex gap-3">
        <input type="checkbox" checked={accepted} onChange={event => setAccepted(event.target.checked)} />
        I accept this price and the reservation terms.
      </label>
    </>}

    {attempt && <p role="status">
      {confirmed ? "Reservation confirmed." : failed
        ? formatStatus(snapshot?.outcomeCode || receipt?.code || snapshot?.reservationState || snapshot?.admissionState || receipt?.state || "Reservation failed")
        : attempt.requestId && !connected ? "Waiting for live reservation updates."
        : snapshot?.reservationState === "PENDING_PAYMENT"
          ? holdExpired ? "The payment deadline has passed. Awaiting the reservation outcome."
            : orderId ? "Your payment order is ready."
            : attempt.intent.paymentMethod === "CASH" ? "Cash reservation acceptance is not available in this flow."
            : "Waiting for the reservation payment order."
          : attempt.requestId ? "Operations is processing your reservation."
          : "The request has no receipt yet. Retry with the same request key."}
    </p>}

    {snapshot && <dl aria-label="Reservation checks" className="grid gap-2 sm:grid-cols-2">
      {([["Account", snapshot.checks.account], ["Registered plate", snapshot.checks.vehicle],
        ["Facility slot", snapshot.checks.facility], ["Booking evaluation", snapshot.checks.booking]] as const)
        .map(([label, status]) => <div key={label}><dt>{label}</dt><dd>{formatStatus(status)}</dd></div>)}
      {snapshot.reservationState && <div><dt>Reservation</dt><dd>{formatStatus(snapshot.reservationState)}</dd></div>}
      {snapshot.paymentReadiness && <div><dt>Payment order</dt><dd>{formatStatus(snapshot.paymentReadiness)}</dd></div>}
    </dl>}

    {snapshot?.reservationId && <>
      <p>Booked total: {money(snapshot.bookedAmount, snapshot.currency ?? "VND")} · Due now: {money(snapshot.depositAmount, snapshot.currency ?? "VND")}</p>
      {snapshot.holdExpiresAt && <p>Payment deadline: {new Date(snapshot.holdExpiresAt).toLocaleString()}</p>}
    </>}

    {orderId && <PaymentCheckout key={orderId} showMethodPicker={false} reservationOrderId={orderId}
      reservationRequestId={attempt?.requestId} holdExpiresAt={snapshot?.holdExpiresAt} />}

    <div className="flex flex-wrap gap-3">
      {!attempt && <button type="button" className="btn-outline" disabled={busy} onClick={onBack}>Back to payment methods</button>}
      {!attempt && <button type="button" className="btn-primary" disabled={busy || !selection || (Boolean(reviewed) && (!accepted || Date.parse(reviewed!.terms.validUntil) <= now))} onClick={reviewed ? confirm : review}>
        {busy ? "Loading…" : reviewed ? "Accept terms and reserve" : "Review price and terms"}
      </button>}
      {attempt && !failed && !confirmed && (!attempt.requestId || !connected) && <button type="button" className="btn-outline" disabled={busy} onClick={() => attempt.requestId ? reconnect() : void run(() => submit(attempt))}>
        {busy ? "Checking…" : attempt.requestId ? "Reconnect" : "Retry same request"}
      </button>}
      {(failed || confirmed) && <button type="button" className="btn-primary" onClick={closeAttempt}>Done</button>}
    </div>
  </section>
}
