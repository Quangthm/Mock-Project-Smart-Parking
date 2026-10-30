import { useEffect, useState, type FormEvent } from "react"
import { QRCodeSVG } from "qrcode.react"
import { classifyPaymentStatus, paymentApi, isPaymentOrderId, type CheckoutResponse } from "../../../lib/paymentApi"
import qrLogo from "../../../assets/payment-qr.png"
import "./payment-checkout.css"

export function PaymentCheckout({ showMethodPicker = true }: { showMethodPicker?: boolean }) {
  const [orderId, setOrderId] = useState(() => showMethodPicker ? new URLSearchParams(window.location.search).get("orderId") ?? sessionStorage.getItem("sp_payos_latest_order") ?? "" : "")
  const [selected, setSelected] = useState(!showMethodPicker)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState("")
  const [checkout, setCheckout] = useState<CheckoutResponse | null>(null)
  const [status, setStatus] = useState<"pending" | "success" | "failed">("pending")
  const [now, setNow] = useState(Date.now)

  useEffect(() => {
    if (!checkout || status !== "pending") return
    let active = true
    const checkStatus = async () => {
      try {
        const result = await paymentApi.status(checkout.paymentTransactionId)
        if (active && result.orderId.toLowerCase() === orderId.trim().toLowerCase()) {
          setStatus(classifyPaymentStatus(result))
        }
      } catch {
        // An unavailable status lookup does not prove payment succeeded or failed.
      }
    }
    const interval = window.setInterval(() => { setNow(Date.now()); void checkStatus() }, 5000)
    return () => { active = false; window.clearInterval(interval) }
  }, [checkout, orderId, status])

  async function pay(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!selected || !isPaymentOrderId(orderId) || busy) return
    setBusy(true)
    setError("")
    try {
      const normalized = orderId.trim().toLowerCase()
      const previous = paymentApi.rememberedCheckout(normalized)
      if (previous) {
        const result = await paymentApi.status(previous.paymentTransactionId)
        if (result.orderId.toLowerCase() !== normalized) throw new Error("Payment Service returned a different order.")
        if (classifyPaymentStatus(result) === "success") throw new Error("This payment order has already been paid.")
        if (result.status === "PENDING" && new Date(previous.expiresAt).getTime() > Date.now()) {
          setCheckout(previous)
          setStatus("pending")
          return
        }
        if (result.status === "PENDING" || result.status === "UNKNOWN") {
          throw new Error("This payment is still being checked. Do not start another payment for this order.")
        }
        if (result.status === "FAILED" || result.status === "CANCELLED") paymentApi.resetAttempt(normalized)
      }
      const created = await paymentApi.checkout(normalized)
      setCheckout(created)
      setStatus("pending")
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : "Payment could not be started.")
    } finally {
      setBusy(false)
    }
  }

  const expired = checkout ? new Date(checkout.expiresAt).getTime() <= now : false
  const payable = !!checkout && status === "pending" && !expired

  return <section className="card payos-checkout" aria-labelledby="payos-checkout-title">
    <div className="payos-checkout-heading"><span>PAYMENT SERVICE</span><h3 id="payos-checkout-title">Pay by QR</h3><p>{showMethodPicker ? "Generate a real VietQR from an existing Payment Service order." : "Your payment details will appear here when the booking creates a payment order."}</p></div>
    {!checkout && !showMethodPicker ? <div className="payos-checkout-unavailable" role="status">
      <strong>QR payment is not available for this booking yet.</strong>
      <p>SmartParking needs a payment order from the booking service before it can request a real payOS QR code. Your slot has not been reserved and no payment has been started.</p>
    </div> : !checkout ? <form onSubmit={pay} className="payos-checkout-form">
      <label htmlFor="payment-order-id">Payment order ID</label>
      <input id="payment-order-id" className="input" value={orderId} onChange={event => { setOrderId(event.target.value); setError("") }} placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" autoComplete="off" spellCheck={false} required aria-describedby="payment-order-hint" />
      <p id="payment-order-hint">The booking service has not yet supplied a payment order. When it does, this ID can be passed here automatically. A booking ID is not a payment order ID.</p>
      {showMethodPicker && <div className="payos-methods" role="group" aria-label="Payment method">
        <button type="button" className={`payos-method${selected ? " is-selected" : ""}`} aria-pressed={selected} onClick={() => setSelected(true)}>
          <img src={qrLogo} alt="" /><span><strong>payOS VietQR</strong><small>Bank transfer via payOS</small></span><span className="payos-method-check" aria-hidden="true">{selected ? "✓" : ""}</span>
        </button>
      </div>}
      {error && <p role="alert" className="payos-checkout-error">{error}</p>}
      <button type="submit" className="btn-primary payos-pay-button" disabled={!selected || !isPaymentOrderId(orderId) || busy}>{busy ? "Generating QR…" : "Generate payment QR"}</button>
    </form> : <div className="payos-checkout-details">
      {payable && checkout.qrCode?.trim() ? <div className="payos-qr-panel"><QRCodeSVG value={checkout.qrCode} size={224} marginSize={2} title="Scan this payOS VietQR to pay" /><p>Scan with your banking app</p></div> : <p role="status">{expired ? "This payment link has expired. Check its status before starting another payment." : status === "success" ? "Payment confirmed by Payment Service." : status === "failed" ? "This payment attempt failed." : "payOS did not return a QR image. Use its secure checkout page to pay."}</p>}
      <dl className="payos-checkout-facts">
        <div><dt>Amount</dt><dd>{checkout.amount.toLocaleString("vi-VN")} VND</dd></div>
        <div><dt>Order code</dt><dd>{checkout.orderCode}</dd></div>
        <div><dt>Bank BIN</dt><dd>{checkout.bin || "—"}</dd></div>
        <div><dt>Account number</dt><dd>{checkout.accountNumber || "—"}</dd></div>
        <div><dt>Account name</dt><dd>{checkout.accountName || "—"}</dd></div>
        <div><dt>Expires</dt><dd>{new Date(checkout.expiresAt).toLocaleString("en-GB")}</dd></div>
        <div><dt>Status</dt><dd>{status === "success" ? "Payment confirmed" : status === "failed" ? "Payment failed" : "Awaiting confirmation"}</dd></div>
      </dl>
      <div className="payos-checkout-actions">
        {payable && <button type="button" className="btn-primary" onClick={() => window.location.assign(checkout.checkoutUrl)}>Open payOS checkout</button>}
        <button type="button" className="btn-outline" onClick={async () => {
          try {
            const result = await paymentApi.status(checkout.paymentTransactionId)
            if (result.orderId.toLowerCase() !== orderId.trim().toLowerCase()) throw new Error("Payment Service returned a different order.")
            setStatus(classifyPaymentStatus(result))
            setError("")
          } catch { setError("Could not check payment status. Please try again.") }
        }}>Check payment status</button>
      </div>
      {error && <p role="alert" className="payos-checkout-error">{error}</p>}
    </div>}
    <p className="payos-checkout-note">SmartParking confirms payment only after Payment Service verifies the transaction. Scanning the QR alone does not mark it paid.</p>
  </section>
}
