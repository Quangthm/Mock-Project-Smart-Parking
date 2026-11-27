import { useCallback, useEffect, useRef, useState, type FormEvent } from "react"
import { QRCodeSVG } from "qrcode.react"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { Modal } from "../../../components/ui/Modal"
import { classifyPaymentStatus, paymentApi, isPaymentOrderId, type CheckoutResponse } from "../../../lib/paymentApi"
import { useServerRefresh } from "../../../lib/serverRefresh"
import { getSessionGeneration } from "../../../lib/authApi"
import qrLogo from "../../../assets/payment-qr.png"
import "./payment-checkout.css"

const paymentDemoScenarios = {
  reservation: { label: "Reservation deposit", outcome: "Reservation confirmed" },
  pass: { label: "Parking pass purchase", outcome: "Parking pass activated" },
  checkout: { label: "Parking check-out fee", outcome: "Check-out completed" },
}

function PaymentDemo({ onClose }: { onClose: () => void }) {
  const [scenario, setScenario] = useState<keyof typeof paymentDemoScenarios>("reservation")
  const [completed, setCompleted] = useState(false)

  return <Modal title="Payment simulation" onClose={onClose}>
    <div className="grid gap-4">
      <p className="text-sm text-[var(--muted)]">Preview the payment outcome for each demo scenario. No money is transferred or order updated.</p>
      <label className="grid gap-2 text-sm font-semibold">
        Payment flow
        <select className="input min-h-11 w-full" value={scenario} onChange={event => {
          setScenario(event.target.value as keyof typeof paymentDemoScenarios)
          setCompleted(false)
        }}>
          {Object.entries(paymentDemoScenarios).map(([value, { label }]) => <option key={value} value={value}>{label}</option>)}
        </select>
      </label>
      <div role="status" className="flex items-start gap-3 rounded-xl border border-[var(--border)] bg-[var(--bg)] p-4">
        <UntitledIcon name={completed ? "check-circle" : "clock"} size={20} className={completed ? "shrink-0 text-[var(--badge-success-fg)]" : "shrink-0 text-[var(--muted)]"} />
        <div className="grid gap-1">
          <p className="font-semibold">{completed ? "Simulated payment successful" : "Awaiting simulated payment"}</p>
          {completed && <p className="text-sm text-[var(--muted)]">{paymentDemoScenarios[scenario].outcome} in this demo.</p>}
        </div>
      </div>
      <div className="flex flex-wrap justify-end gap-2">
        <button type="button" className="btn-outline min-h-11" onClick={onClose}>{completed ? "Done" : "Cancel"}</button>
        {!completed && <button type="button" className="btn-primary min-h-11" onClick={() => setCompleted(true)}>Simulate success</button>}
      </div>
    </div>
  </Modal>
}

// Both prop names are used by reservation checkout callers during the branch merge.
// Keep one authoritative order ID; remove the alias after callers are standardized.
type PaymentCheckoutProps = {
  showMethodPicker?: boolean
  reservationOrderId?: string
  bookingOrderId?: string
  reservationRequestId?: string
  holdExpiresAt?: string | null
  paymentConfirmed?: boolean
}

export function PaymentCheckout({
  showMethodPicker = true,
  reservationOrderId,
  bookingOrderId,
  reservationRequestId,
  holdExpiresAt,
  paymentConfirmed = false,
}: PaymentCheckoutProps) {
  const [manualOrderId, setManualOrderId] = useState(() =>
    showMethodPicker
      ? new URLSearchParams(window.location.search).get("orderId") ??
        sessionStorage.getItem("sp_payos_latest_order") ?? ""
      : ""
  )
  const providedOrderId = reservationOrderId ?? bookingOrderId
  const orderId = providedOrderId ?? manualOrderId
  const autoGenerate = !showMethodPicker && Boolean(providedOrderId)
  const [selected, setSelected] = useState(!showMethodPicker)
  const [busy, setBusy] = useState(false)
  const submitting = useRef(false)
  const autoStartedFor = useRef<string | null>(null)
  const mounted = useRef(false)
  const [error, setError] = useState("")
  const [checkout, setCheckout] = useState<CheckoutResponse | null>(null)
  const [status, setStatus] = useState<"pending" | "success" | "failed">("pending")
  const [demoOpen, setDemoOpen] = useState(false)
  const [now, setNow] = useState(Date.now)

  useEffect(() => {
    mounted.current = true
    return () => { mounted.current = false }
  }, [])

  useEffect(() => {
    if ((!checkout && !holdExpiresAt) || status !== "pending") return
    const timer = window.setInterval(() => setNow(Date.now()), 5000)
    return () => window.clearInterval(timer)
  }, [checkout, status, holdExpiresAt])

  useEffect(() => {
    if (!checkout) return
    setNow(Date.now())
  }, [checkout])

  const transactionId = checkout?.paymentTransactionId
  useServerRefresh(!providedOrderId && status === "pending" && transactionId ? transactionId + ":" + orderId : undefined, async (isCurrent) => {
      if (!transactionId) return
      try {
        const result = await paymentApi.status(transactionId)
        if (isCurrent() && result.orderId.toLowerCase() === orderId.trim().toLowerCase()) {
          setStatus(current => current === "pending" ? classifyPaymentStatus(result) : current)
        }
      } catch {
        // An unavailable status lookup does not prove payment succeeded or failed.
      }
  }, 5000)

  const startCheckout = useCallback(async (automatic: boolean) => {
    if (paymentConfirmed || !selected || !isPaymentOrderId(orderId) || submitting.current) return
    const deadline = holdExpiresAt ? Date.parse(holdExpiresAt) : null
    if (automatic && deadline === null) {
      setError("The reservation payment deadline is unavailable. Refresh the reservation status.")
      return
    }
    if (deadline !== null && !(deadline > Date.now())) {
      setError("The reservation payment deadline has passed. Refresh the reservation status.")
      return
    }
    const generation = getSessionGeneration()
    const current = () => mounted.current && generation === getSessionGeneration()
    submitting.current = true
    setBusy(true)
    setError("")
    try {
      const normalized = orderId.trim().toLowerCase()
      const previous = paymentApi.rememberedCheckout(normalized)
      if (previous) {
        const result = await paymentApi.status(previous.paymentTransactionId)
        if (!current()) return
        if (result.orderId.toLowerCase() !== normalized) throw new Error("Payment Service returned a different order.")
        if (classifyPaymentStatus(result) === "success") throw new Error("This payment order has already been paid.")
        if (result.status === "PENDING" && new Date(previous.expiresAt).getTime() > Date.now()) {
          if (providedOrderId) paymentApi.bindReservationCheckout(normalized, previous.paymentTransactionId)
          setCheckout(previous)
          setStatus("pending")
          return
        }
        if (result.status === "PENDING" || result.status === "UNKNOWN") {
          throw new Error("This payment is still being checked. Do not start another payment for this order.")
        }
        if (result.status === "FAILED" || result.status === "CANCELLED") {
          if (automatic) throw new Error("The previous payment did not complete. Try again to create a new QR code.")
          paymentApi.resetAttempt(normalized)
        }
      }
      if (deadline !== null && !(deadline > Date.now())) {
        throw new Error("The reservation payment deadline has passed. Refresh the reservation status.")
      }
      const created = await paymentApi.checkout(normalized, providedOrderId ? reservationRequestId ?? null : undefined)
      if (!current()) return
      setCheckout(created)
      setStatus("pending")
    } catch (failure) {
      if (current()) setError(failure instanceof Error ? failure.message : "Payment could not be started.")
    } finally {
      submitting.current = false
      if (current()) setBusy(false)
    }
  }, [paymentConfirmed, selected, orderId, holdExpiresAt, providedOrderId, reservationRequestId])

  useEffect(() => {
    if (paymentConfirmed || !autoGenerate || !isPaymentOrderId(orderId)) return
    let active = true
    const normalized = orderId.trim().toLowerCase()
    // Defer until the Strict Mode probe has cleaned up.
    void Promise.resolve().then(() => {
      if (!active || autoStartedFor.current === normalized) return
      autoStartedFor.current = normalized
      void startCheckout(true)
    })
    return () => { active = false }
  }, [paymentConfirmed, autoGenerate, orderId, startCheckout])

  function pay(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    void startCheckout(false)
  }

  const holdExpired = !!holdExpiresAt && !(Date.parse(holdExpiresAt) > now)
  const expired = holdExpired || (checkout ? new Date(checkout.expiresAt).getTime() <= now : false)
  const payable = !!checkout && status === "pending" && !expired
  const showQr = payable && Boolean(checkout?.qrCode?.trim())
  const paymentStatus = status === "success" ? "Payment confirmed" : status === "failed" ? "Payment failed" : expired ? "Link expired" : "Awaiting confirmation"

  return <section className="card payos-checkout" aria-labelledby="payos-checkout-title">
    <header className="payos-checkout-heading">
      <h3 id="payos-checkout-title">Pay with payOS VietQR</h3>
      {!paymentConfirmed && !checkout && !autoGenerate && <p>Generate a QR code for this payment order.</p>}
    </header>
    {paymentConfirmed ? <div className="payos-transfer-summary">
      <strong>Payment received</strong>
      <span className="payos-transfer-status" data-status="success" role="status">Payment confirmed</span>
    </div> : !checkout && !showMethodPicker && !isPaymentOrderId(orderId) ? <div className="payos-checkout-unavailable" role="status">
      <strong>QR payment is not available for this booking yet.</strong>
      <p>SmartParking needs a payment order from the booking service before it can request a real payOS QR code. Check your reservation status before trying again.</p>
    </div> : !checkout ? <form onSubmit={pay} className="payos-checkout-form">
      {!providedOrderId && <>
        <label htmlFor="payment-order-id">Payment order ID</label>
        <input id="payment-order-id" className="input" value={orderId} onChange={event => { setManualOrderId(event.target.value); setError("") }} placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" autoComplete="off" spellCheck={false} required aria-describedby="payment-order-hint" />
        <p id="payment-order-hint">The booking service has not yet supplied a payment order. When it does, this ID can be passed here automatically. A booking ID is not a payment order ID.</p>
      </>}
      {showMethodPicker && <div className="payos-methods" role="group" aria-label="Payment method">
        <button type="button" className={`payos-method${selected ? " is-selected" : ""}`} aria-pressed={selected} onClick={() => setSelected(true)}>
          <img src={qrLogo} alt="" /><span><strong>payOS VietQR</strong><small>Bank transfer via payOS</small></span><span className="payos-method-check" aria-hidden="true">{selected ? "✓" : ""}</span>
        </button>
      </div>}
      {error && <p role="alert" className="payos-checkout-error">{error}</p>}
      {holdExpired && <p role="status">The reservation payment deadline has passed.</p>}
      {autoGenerate
        ? !holdExpired && (busy || !error
          ? <p role="status">Generating payment QR…</p>
          : error && holdExpiresAt && <button type="submit" className="btn-primary payos-pay-button">Retry generating QR</button>)
        : <button type="submit" className="btn-primary payos-pay-button" disabled={!selected || !isPaymentOrderId(orderId) || busy || holdExpired}>{busy ? "Generating QR…" : "Generate payment QR"}</button>}
    </form> : <div className="payos-checkout-details" data-qr={showQr}>
      {showQr && <figure className="payos-qr-panel">
        <div><QRCodeSVG value={checkout.qrCode} size={232} marginSize={4} title="Scan this payOS VietQR to pay" /></div>
        <figcaption>Scan with your banking app</figcaption>
      </figure>}
      <div className="payos-transfer">
        <div className="payos-transfer-summary">
          <div><span>Amount to pay</span><strong>{checkout.amount.toLocaleString("vi-VN")} <small>VND</small></strong></div>
          <span className="payos-transfer-status" data-status={status === "pending" && expired ? "expired" : status} role="status">{paymentStatus}</span>
        </div>
        {!showQr && status === "pending" && <p className="payos-transfer-message">
          {expired ? "Check the payment status before starting another payment." : "Open payOS checkout to pay."}
        </p>}
        <dl className="payos-checkout-facts">
          <div><dt>Account name</dt><dd>{checkout.accountName || "—"}</dd></div>
          <div><dt>Account number</dt><dd>{checkout.accountNumber || "—"}</dd></div>
          <div><dt>Order code</dt><dd>{checkout.orderCode}</dd></div>
          <div><dt>Bank BIN</dt><dd>{checkout.bin || "—"}</dd></div>
          <div className="payos-transfer-expiry"><dt>Expires</dt><dd><time dateTime={checkout.expiresAt}>{new Date(checkout.expiresAt).toLocaleString("en-GB")}</time></dd></div>
        </dl>
      </div>
      <div className="payos-checkout-actions">
        {payable && <button type="button" className="btn-primary" onClick={() => window.location.assign(checkout.checkoutUrl)}>Open payOS checkout</button>}
        <button type="button" className="btn-outline" onClick={async () => {
          try {
            const result = await paymentApi.status(checkout.paymentTransactionId)
            if (result.orderId.toLowerCase() !== orderId.trim().toLowerCase()) throw new Error("Payment Service returned a different order.")
            setStatus(classifyPaymentStatus(result))
            setError("")
          } catch (failure) { setError(failure instanceof Error ? failure.message : "Could not check payment status. Please try again.") }
        }}>Check payment status</button>
      </div>
      {error && <p role="alert" className="payos-checkout-error">{error}</p>}
    </div>}
    {!paymentConfirmed && <p className="payos-checkout-note">SmartParking confirms payment only after Payment Service verifies the transaction. Scanning the QR alone does not mark it paid.</p>}
  </section>
}
