import { useEffect, useRef, useState } from "react"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { getSessionGeneration } from "../../../lib/authApi"
import { formatStatus } from "../../../lib/formatStatus"
import {
  reservationsApi,
  type ReservationProgress,
  type ReservationReceipt,
  type ReviewedReservationIntent,
  type ReviewedReservationQuote,
  type ReviewedReservationSelection,
} from "../../../lib/reservationsApi"
import { useReservationProgress } from "../../../lib/reservationProgress"
import { PaymentCheckout } from "../payments/PaymentCheckout"

type ReviewedSubmission = {
  key: string
  intent: ReviewedReservationIntent
}

type Attempt =
  | (ReviewedSubmission & { requestId?: undefined; reservationId?: undefined })
  | { requestId: string; reservationId?: string }

const isId = (value: unknown): value is string =>
  typeof value === "string" && /^(?!00000000-0000-0000-0000-000000000000$)[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}$/i.test(value)

const terminal = new Set(["REJECTED", "DENIED", "EXPIRED", "FAILED_DEPENDENCY", "TIMED_OUT", "DEPENDENCY_UNAVAILABLE", "CANCELLED", "UNFULFILLABLE", "INVALIDATED"])
const dateTime = new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" })
const dateOnly = new Intl.DateTimeFormat(undefined, { dateStyle: "medium" })
const timeOnly = new Intl.DateTimeFormat(undefined, { timeStyle: "short" })
const policyGroups = [
  { title: "Booking & payment", terms: [
    ["RESERVATION_MIN_DURATION", "Minimum stay"], ["RESERVATION_MAX_DURATION", "Maximum stay"],
    ["MAX_ADVANCE_BOOKING", "Book up to"], ["INTER_BOOKING_BUFFER", "Gap between bookings"],
    ["PAYMENT_HOLD_DURATION", "Time to pay"],
  ] },
  { title: "Arrival & parking", terms: [
    ["ARRIVAL_LATE_TOLERANCE", "Late arrival allowance"], ["RESERVATION_PROTECTION_WINDOW", "Reservation protection window"],
    ["ALLOCATION_LEAD_TIME", "Space allocation lead time"], ["OCCUPANCY_VALIDITY_TIMESPAN", "Occupancy validity"],
  ] },
  { title: "Cancellations & refunds", terms: [
    ["FACILITY_FAILURE_REFUND_LEAD_TIME", "Facility failure refund lead time"],
    ["DAILY_CANCELLATION_LIMIT", "Daily cancellation limit", "count"],
    ["RAPID_CANCELLATION_COUNT", "Rapid cancellation threshold", "count"],
    ["RAPID_CANCELLATION_WINDOW", "Rapid cancellation window"],
    ["RAPID_CANCELLATION_RESTRICTION_DURATION", "Restriction duration"],
  ] },
] as const
const knownPolicyKeys = new Set<string>(policyGroups.flatMap(group => group.terms.map(([key]) => key)))

const reservationCheckLabels = [
  ["account", "Account"],
  ["vehicle", "Registered plate"],
  ["facility", "Parking space"],
  ["booking", "Booking review"],
] as const satisfies ReadonlyArray<readonly [keyof ReservationProgress["checks"], string]>

function ReservationChecks({ snapshot }: { snapshot: ReservationProgress }) {
  const passed = reservationCheckLabels.filter(([key]) => snapshot.checks[key] === "SUCCEEDED").length

  return <section className="reservation-checks" aria-labelledby="reservation-checks-title">
    <header className="reservation-checks-header">
      <h4 id="reservation-checks-title">Reservation checks</h4>
      <span>{passed} of {reservationCheckLabels.length} passed</span>
    </header>
    <dl className="reservation-checks-grid">
      {reservationCheckLabels.map(([key, label]) => {
        const state = snapshot.checks[key]
        const succeeded = state === "SUCCEEDED"
        const failed = state === "REJECTED" || state === "FAILED_DEPENDENCY" || state === "DENIED"
        return <div key={key}>
          <dt>{label}</dt>
          <dd key={state} data-tone={succeeded ? "success" : failed ? "error" : "neutral"}>
            <UntitledIcon name={succeeded ? "check-circle" : failed || state === "SKIPPED" ? "x" : "clock"} size={16} />
            {succeeded ? "Passed" : state === "PENDING" ? "Waiting" : formatStatus(state)}
          </dd>
        </div>
      })}
    </dl>
  </section>
}

function formatPolicyDuration(minutes: number) {
  if (minutes > 0 && minutes % 1440 === 0) return `${minutes / 1440} d`
  if (minutes > 0 && minutes % 60 === 0) return `${minutes / 60} hr`
  return `${minutes} min`
}

function OperationalTerms({ policy }: { policy: ReviewedReservationQuote["operationalPolicy"] }) {
  const additional = Object.entries(policy.values).filter(([key]) => !knownPolicyKeys.has(key))
  return <details className="reservation-terms">
    <summary>
      <UntitledIcon name="file" size={18} />
      <span>Operational terms</span>
      <UntitledIcon name="chevron-down" size={18} className="reservation-terms-chevron" />
    </summary>
    <div className="reservation-terms-content">
      {policyGroups.map(group => {
        const terms = group.terms.filter(([key]) => policy.values[key] !== undefined)
        if (!terms.length) return null
        return <section key={group.title}>
          <h4>{group.title}</h4>
          <dl>{terms.map(([key, label, unit]) => {
            const value = policy.values[key]
            const text = key === "ARRIVAL_LATE_TOLERANCE" && policy.arrivalLateToleranceEnabled === false
              ? "Disabled" : unit === "count" ? String(value) : formatPolicyDuration(value)
            return <div key={key}><dt>{label}</dt><dd>{text}</dd></div>
          })}</dl>
        </section>
      })}
      {additional.length > 0 && <section>
        <h4>Other terms</h4>
        <dl>{additional.map(([key, value]) => <div key={key}><dt>{formatStatus(key)}</dt><dd>{value}</dd></div>)}</dl>
      </section>}
    </div>
  </details>
}

export function readReservationAttempt(storageKey: string): Attempt | null {
  try {
    const saved = JSON.parse(sessionStorage.getItem(storageKey) ?? "null") as {
      requestId?: unknown; reservationId?: unknown; key?: unknown; intent?: Partial<ReviewedReservationIntent>
    } | null
    if (isId(saved?.requestId)) {
      return { requestId: saved.requestId, reservationId: isId(saved.reservationId) ? saved.reservationId : undefined }
    }
    if (saved?.requestId != null || !isId(saved?.key)) return null

    const intent = saved.intent
    if (!intent || !isId(intent.quoteId) || !isId(intent.requestedSlotId) ||
        typeof intent.licensePlate !== "string" || !intent.licensePlate.trim() ||
        intent.paymentMethod !== "PAYOS" || typeof intent.start !== "string" || typeof intent.end !== "string" ||
        !Number.isFinite(Date.parse(intent.start)) || !(Date.parse(intent.end) > Date.parse(intent.start))) return null
    const { quoteId, licensePlate, requestedSlotId, start, end, paymentMethod } = intent
    return { key: saved.key, intent: { quoteId, licensePlate, requestedSlotId, start, end, paymentMethod } }
  } catch {
    return null
  }
}

export function ReservationCheckout({ storageKey, selection, onBack, onDone }: {
  storageKey: string
  selection: ReviewedReservationSelection | null
  onBack: () => void
  onDone: () => void
}) {
  const [attempt, setAttempt] = useState<Attempt | null>(() => readReservationAttempt(storageKey))
  const [reviewed, setReviewed] = useState<{ quote: ReviewedReservationQuote; selection: ReviewedReservationSelection } | null>(null)
  const [accepted, setAccepted] = useState(false)
  const [receipt, setReceipt] = useState<ReservationReceipt | null>(null)
  const { snapshot, checkout, connected, reconnect } = useReservationProgress(attempt?.requestId, true)
  const [error, setError] = useState("")
  const [busy, setBusy] = useState(false)
  const [now, setNow] = useState(Date.now)
  const [progressHidden, setProgressHidden] = useState(false)
  const progressDialog = useRef<HTMLDialogElement>(null)
  const progressButton = useRef<HTMLButtonElement>(null)
  const locked = useRef(false)
  const mounted = useRef(false)
  const generation = useRef(getSessionGeneration())
  const current = () => mounted.current && generation.current === getSessionGeneration()

  useEffect(() => {
    mounted.current = true
    return () => { mounted.current = false }
  }, [])

  const showProgress = Boolean(attempt) && !progressHidden
  useEffect(() => {
    const dialog = progressDialog.current
    if (!showProgress) {
      if (progressHidden) progressButton.current?.focus()
      return
    }
    if (!dialog) return
    if (!dialog.open) dialog.showModal()
    return () => { if (dialog.open) dialog.close() }
  }, [showProgress, progressHidden])

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

  async function submit(value: ReviewedSubmission) {
    // An ambiguous create response is retried only with its persisted intent and key.
    const next = await reservationsApi.submitQuote(value.intent, value.key)
    if (!current()) return

    save({ requestId: next.requestId, reservationId: next.reservationId ?? undefined })
    setReceipt(next)
  }

  function review() {
    if (!selection) return
    void run(async () => {
      const quote = await reservationsApi.reviewQuote(selection)
      if (quote.canonicalPlate !== selection.licensePlate ||
          quote.requestedSlotId.toLowerCase() !== selection.requestedSlotId.toLowerCase() ||
          Date.parse(quote.start) !== Date.parse(selection.start) ||
          Date.parse(quote.end) !== Date.parse(selection.end) ||
          quote.paymentMethod !== selection.paymentMethod ||
          !Number.isFinite(Date.parse(quote.validUntil))) {
        throw new Error("The quote does not match your selection. Review it again.")
      }
      if (current()) {
        setNow(Date.now())
        setReviewed({ quote, selection })
        setAccepted(false)
      }
    })
  }

  function confirm() {
    if (!reviewed || !accepted) return
    if (JSON.stringify(selection) !== JSON.stringify(reviewed.selection) ||
        Date.parse(reviewed.quote.validUntil) <= Date.now()) {
      setReviewed(null)
      setAccepted(false)
      setError("The selection changed or the terms expired. Review the terms again before confirming.")
      return
    }

    void run(async () => {
      const { quote } = reviewed
      const value: ReviewedSubmission = {
        key: crypto.randomUUID(),
        intent: {
          licensePlate: quote.canonicalPlate,
          requestedSlotId: quote.requestedSlotId,
          start: quote.start,
          end: quote.end,
          paymentMethod: quote.paymentMethod,
          quoteId: quote.quoteId,
        },
      }
      // Persist before the HTTP request so retries cannot create a second intent.
      save(value)
      await submit(value)
    })
  }

  useEffect(() => {
    if (attempt?.requestId && snapshot?.reservationId && attempt.reservationId !== snapshot.reservationId) {
      save({ ...attempt, reservationId: snapshot.reservationId })
    }
  }, [attempt, snapshot?.reservationId])

  const failed = Boolean(
    terminal.has(snapshot?.admissionState ?? receipt?.state ?? "") || terminal.has(snapshot?.reservationState ?? ""),
  )
  const confirmed = !failed && snapshot?.reservationState === "CONFIRMED"

  const activeDeadline = attempt ? snapshot?.holdExpiresAt : reviewed?.quote.validUntil
  useEffect(() => {
    if (!activeDeadline || confirmed || failed) return
    setNow(Date.now())
    const timer = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(timer)
  }, [activeDeadline, confirmed, failed])

  const holdExpired = Boolean(snapshot?.holdExpiresAt && Date.parse(snapshot.holdExpiresAt) <= now)
  const paymentState = checkout === "UNAVAILABLE" ? checkout : checkout?.state
  const orderId = connected && !failed && snapshot?.reservationState === "PENDING_PAYMENT" &&
    checkout && checkout !== "UNAVAILABLE" && checkout.state === "PAYABLE" && checkout.holdExpiresAt &&
    Date.parse(checkout.holdExpiresAt) === Date.parse(snapshot.holdExpiresAt ?? "") && snapshot.holdExpiresAt &&
    Date.parse(snapshot.holdExpiresAt) > now ? checkout.orderId : null
  const needsReview = reviewed && (Date.parse(reviewed.quote.validUntil) <= now ||
    JSON.stringify(selection) !== JSON.stringify(reviewed.selection))

  function closeAttempt() {
    if (!current()) return
    sessionStorage.removeItem(storageKey)
    window.dispatchEvent(new Event("sp-data-change"))
    onDone()
  }

  const money = (amount: number | null, currency = "VND") =>
    amount == null ? "Not available" : `${amount.toLocaleString("vi-VN")} ${currency}`

  const quote = reviewed?.quote
  const start = quote ? new Date(quote.start) : null
  const end = quote ? new Date(quote.end) : null
  const sameDay = start && end && start.toDateString() === end.toDateString()
  const progressTone = failed ? "error" : confirmed || orderId ? "success"
    : busy || (attempt?.requestId && connected && !holdExpired) ? "loading" : "paused"

  return <>
  <section className="card reservation-checkout" aria-labelledby="reservation-checkout-title">
    <header className="reservation-checkout-header">
      <span className="reservation-checkout-icon"><UntitledIcon name="clipboard" size={23} /></span>
      <h3 id="reservation-checkout-title">Reservation checkout</h3>
    </header>
    {error && !attempt && <p className="reservation-checkout-notice is-error" role="alert"><UntitledIcon name="alert" size={18} />{error}</p>}

    {!attempt && !reviewed && <p>Load the current price and deposit terms before confirming your reservation.</p>}
    {!attempt && quote && start && end && <div className="reservation-quote">
      <dl className="reservation-quote-price">
        <div className="reservation-quote-due"><dt>Deposit due now</dt><dd>{money(quote.depositAmount, quote.currency)}</dd></div>
        <div className="reservation-quote-total"><dt>Booking total</dt><dd>{money(quote.bookedAmount, quote.currency)}</dd></div>
      </dl>
      <dl className="reservation-quote-facts">
        <div><dt><UntitledIcon name="car" size={18} />Vehicle</dt><dd>{quote.canonicalPlate}</dd></div>
        <div><dt><UntitledIcon name="calendar" size={18} />Parking time</dt><dd>
          {sameDay ? <>
            <span>{dateOnly.format(start)}</span>
            <span><time dateTime={quote.start}>{timeOnly.format(start)}</time> – <time dateTime={quote.end}>{timeOnly.format(end)}</time></span>
          </> : <>
            <time dateTime={quote.start}>{dateTime.format(start)}</time>
            <span>to <time dateTime={quote.end}>{dateTime.format(end)}</time></span>
          </>}
        </dd></div>
        <div><dt><UntitledIcon name="credit-card" size={18} />Payment method</dt><dd>payOS VietQR</dd></div>
        <div><dt><UntitledIcon name="clock" size={18} />Quote valid until</dt><dd><time dateTime={quote.validUntil}>{dateTime.format(new Date(quote.validUntil))}</time></dd></div>
      </dl>
      {quote.disclosure && <p className="reservation-checkout-notice"><UntitledIcon name="alert" size={18} />{quote.disclosure}</p>}
      <OperationalTerms policy={quote.operationalPolicy} />
      {needsReview && <p className="reservation-checkout-notice" role="status">Review the price again before reserving this space.</p>}
      <label className="reservation-quote-consent" data-accepted={accepted && !needsReview}>
        <input type="checkbox" checked={accepted && !needsReview} disabled={Boolean(needsReview) || busy} onChange={event => setAccepted(event.target.checked)} />
        I accept this price and the reservation terms.
      </label>
    </div>}

    {attempt ? <button ref={progressButton} type="button" className="btn-outline" onClick={() => setProgressHidden(false)}>View reservation progress</button>
      : <div className="reservation-checkout-actions">
        <button type="button" className="btn-outline" disabled={busy} onClick={onBack}>Back to payment methods</button>
        <button type="button" className="btn-primary" disabled={busy || !selection || Boolean(reviewed && !needsReview && !accepted)} onClick={reviewed && !needsReview ? confirm : review}>
          {busy ? "Loading…" : reviewed && !needsReview ? "Accept terms and reserve" : "Review price and terms"}
        </button>
      </div>}
  </section>

  {attempt && <dialog ref={progressDialog} className="reservation-progress-dialog" data-payment-ready={Boolean(orderId)} aria-labelledby="reservation-progress-title"
    onCancel={event => { event.preventDefault(); setProgressHidden(true) }}>
    <header className="reservation-progress-header">
      <h3 id="reservation-progress-title">Reservation progress</h3>
      <button type="button" className="icon-button" aria-label="Hide reservation progress" onClick={() => setProgressHidden(true)}><UntitledIcon name="x" size={20} /></button>
    </header>
    <div className="reservation-progress-body">
    <div className="reservation-progress-status" role="status" data-tone={progressTone}>
      <span className="reservation-progress-indicator" key={progressTone} aria-hidden="true">
        <UntitledIcon name={progressTone === "error" ? "x" : progressTone === "success" ? "check" : "clock"} size={23} />
      </span>
      <p>{confirmed ? "Reservation confirmed." : failed
        ? formatStatus(snapshot?.outcomeCode || receipt?.code || snapshot?.reservationState || snapshot?.admissionState || receipt?.state || "Reservation failed")
        : attempt.requestId && !connected ? "Waiting for live reservation updates."
        : snapshot?.reservationState === "PENDING_PAYMENT"
          ? paymentState === "PAID" ? "Payment received. Awaiting reservation confirmation."
            : holdExpired ? "The payment deadline has passed. Awaiting the reservation outcome."
            : orderId ? "Your payment order is ready."
            : paymentState === "FAILED" ? "Payment order creation failed."
            : paymentState === "CLOSED" ? "This payment order is closed."
            : paymentState === "UNAVAILABLE" ? "Payment Service is unavailable. Reconnect to check checkout."
            : "Waiting for the reservation payment order."
          : attempt.requestId ? "We’re checking your reservation."
          : busy ? "Sending your reservation…" : "The request has no receipt yet. Retry with the same request key."}</p>
    </div>
    {error && <p className="reservation-checkout-notice is-error" role="alert"><UntitledIcon name="alert" size={18} />{error}</p>}

    {snapshot && <ReservationChecks snapshot={snapshot} />}

    {snapshot && (snapshot.reservationId || snapshot.reservationState || paymentState) &&
      <section className="reservation-payment-summary" aria-labelledby="reservation-payment-summary-title">
        <h4 id="reservation-payment-summary-title">Payment summary</h4>
        {(snapshot.reservationState || paymentState) && <dl className="reservation-payment-states">
          {snapshot.reservationState && <div>
            <dt>Reservation</dt>
            <dd data-status={snapshot.reservationState}>{formatStatus(snapshot.reservationState)}</dd>
          </div>}
          {paymentState && <div>
            <dt>Payment order</dt>
            <dd data-status={paymentState}>{formatStatus(paymentState)}</dd>
          </div>}
        </dl>}
        {snapshot.reservationId && <dl className="reservation-payment-totals">
          <div className="reservation-payment-due"><dt>Due now</dt><dd>{money(snapshot.depositAmount, snapshot.currency ?? "VND")}</dd></div>
          <div><dt>Booked total</dt><dd>{money(snapshot.bookedAmount, snapshot.currency ?? "VND")}</dd></div>
        </dl>}
        {snapshot.reservationId && snapshot.holdExpiresAt && <p className="reservation-payment-deadline">
          <UntitledIcon name="clock" size={16} />
          <span>Payment deadline</span>
          <time dateTime={snapshot.holdExpiresAt}>{new Date(snapshot.holdExpiresAt).toLocaleString()}</time>
        </p>}
      </section>}

    {orderId && <PaymentCheckout key={orderId} showMethodPicker={false} reservationOrderId={orderId}
      reservationRequestId={attempt?.requestId} holdExpiresAt={snapshot?.holdExpiresAt} />}

    {(failed || confirmed || !attempt.requestId || !connected || paymentState === "UNAVAILABLE") && <div className="reservation-checkout-actions">
      {attempt && !failed && !confirmed && (!attempt.requestId || !connected || paymentState === "UNAVAILABLE") && <button type="button" className="btn-outline" disabled={busy} onClick={() => "intent" in attempt ? void run(() => submit(attempt)) : reconnect()}>
        {busy ? "Checking…" : attempt.requestId ? "Reconnect" : "Retry same request"}
      </button>}
      {(failed || confirmed) && <button type="button" className="btn-primary" onClick={closeAttempt}>Done</button>}
    </div>}
    </div>
  </dialog>}
  </>
}
