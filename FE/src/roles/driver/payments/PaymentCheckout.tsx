import { useCallback, useEffect, useRef, useState, type FormEvent } from "react"
import { QRCodeSVG } from "qrcode.react"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { classifyPaymentStatus, DemoPaymentUnavailableError, paymentApi, isPaymentOrderId, type CheckoutResponse } from "../../../lib/paymentApi"
import { useServerRefresh } from "../../../lib/serverRefresh"
import { getSessionGeneration } from "../../../lib/authApi"
import qrLogo from "../../../assets/payment-qr.png"
import "./payment-checkout.css"

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
  const demoMode = !showMethodPicker && Boolean(providedOrderId) &&
    (import.meta.env.DEV || import.meta.env.VITE_ENABLE_DEMO_PAYMENT === "true")
  const autoGenerate = !showMethodPicker && Boolean(providedOrderId)
  const [selected, setSelected] = useState(!showMethodPicker)
  const [busy, setBusy] = useState(false)
  const submitting = useRef(false)
  const autoStartedFor = useRef<string | null>(null)
  const mounted = useRef(false)
  const [error, setError] = useState("")
  const [demoBusy, setDemoBusy] = useState(false)
  const [demoError, setDemoError] = useState("")
  const [demoUnavailable, setDemoUnavailable] = useState(false)
  const [demoConfirmed, setDemoConfirmed] = useState(false)
  const [checkout, setCheckout] = useState<CheckoutResponse | null>(null)
  const [status, setStatus] = useState<"pending" | "success" | "failed">("pending")
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
    if (paymentConfirmed || demoConfirmed || !selected || !isPaymentOrderId(orderId) || submitting.current) return
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
  }, [paymentConfirmed, demoConfirmed, selected, orderId, holdExpiresAt, providedOrderId, reservationRequestId])

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
  const confirmed = paymentConfirmed || demoConfirmed
  const canDemoPay = demoMode && !confirmed && !demoUnavailable && !holdExpired &&
    status !== "success" && isPaymentOrderId(orderId)
  const actionBusy = busy || demoBusy

  async function demoPay() {
    if (!canDemoPay || submitting.current) return
    if (holdExpiresAt && !(Date.parse(holdExpiresAt) > Date.now())) {
      setNow(Date.now())
      return
    }
    const generation = getSessionGeneration()
    const current = () => mounted.current && generation === getSessionGeneration()
    submitting.current = true
    setDemoBusy(true)
    setDemoError("")
    try {
      await paymentApi.demoPay(orderId)
      if (current()) setDemoConfirmed(true)
    } catch (failure) {
      if (current()) {
        if (failure instanceof DemoPaymentUnavailableError) setDemoUnavailable(true)
        setDemoError(failure instanceof Error ? failure.message : "Demo payment could not be confirmed.")
      }
    } finally {
      submitting.current = false
      if (current()) setDemoBusy(false)
    }
  }

  const expired = holdExpired || (checkout ? new Date(checkout.expiresAt).getTime() <= now : false)
  const payable = !!checkout && status === "pending" && !expired
  const showQr = payable && Boolean(checkout?.qrCode?.trim())
  const paymentStatus = status === "success" ? "Payment confirmed" : status === "failed" ? "Payment failed" : expired ? "Link expired" : "Awaiting confirmation"
  const demoButton = canDemoPay && <button type="button" className="btn-outline payos-bypass-button" disabled={actionBusy}
    aria-busy={demoBusy} title="Simulate payment without transferring money" onClick={() => void demoPay()}>
    <UntitledIcon name="zap" size={16} />
    {demoBusy ? "Confirming…" : "ByPass"}
  </button>

  return <section className="card payos-checkout" aria-labelledby="payos-checkout-title">
    <header className="payos-checkout-heading">
      <h3 id="payos-checkout-title">Pay with payOS VietQR</h3>
      {!confirmed && !checkout && !autoGenerate && !demoMode && <p>Generate a QR code for this payment order.</p>}
    </header>
    {confirmed ? <div className="payos-transfer-summary">
      <strong>Payment received</strong>
      <span className="payos-transfer-status" data-status="success" role="status">{demoConfirmed ? "Demo payment confirmed" : "Payment confirmed"}</span>
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
        ? !holdExpired && !demoBusy && (busy || !error
          ? <p role="status">Generating payment QR…</p>
          : error && holdExpiresAt && <button type="submit" className="btn-primary payos-pay-button">Retry generating QR</button>)
        : <button type="submit" className="btn-primary payos-pay-button" disabled={!selected || !isPaymentOrderId(orderId) || actionBusy || holdExpired}>{busy ? "Generating QR…" : "Generate payment QR"}</button>}
      {demoButton && <div className="payos-checkout-actions">{demoButton}</div>}
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
        {payable && <button type="button" className="btn-primary" disabled={actionBusy} onClick={() => window.location.assign(checkout.checkoutUrl)}>Open payOS checkout</button>}
        <button type="button" className="btn-outline" disabled={actionBusy} onClick={async () => {
          try {
            const result = await paymentApi.status(checkout.paymentTransactionId)
            if (result.orderId.toLowerCase() !== orderId.trim().toLowerCase()) throw new Error("Payment Service returned a different order.")
            setStatus(classifyPaymentStatus(result))
            setError("")
          } catch (failure) { setError(failure instanceof Error ? failure.message : "Could not check payment status. Please try again.") }
        }}>Check payment status</button>
        {demoButton}
      </div>
      {error && <p role="alert" className="payos-checkout-error">{error}</p>}
    </div>}
    {!confirmed && demoError && <p role="alert" className="payos-checkout-error">{demoError}</p>}
    {!confirmed && <p className="payos-checkout-note">SmartParking confirms payment only after Payment Service verifies the transaction. Scanning the QR alone does not mark it paid.</p>}
  </section>
}
