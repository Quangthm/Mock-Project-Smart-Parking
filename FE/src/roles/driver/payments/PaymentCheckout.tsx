import { useEffect, useState, type FormEvent } from "react"
import { QRCodeSVG } from "qrcode.react"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { Modal } from "../../../components/ui/Modal"
import { classifyPaymentStatus, paymentApi, isPaymentOrderId, type CheckoutResponse } from "../../../lib/paymentApi"
import { useServerRefresh } from "../../../lib/serverRefresh"
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
  holdExpiresAt?: string | null
}

export function PaymentCheckout({
  showMethodPicker = true,
  reservationOrderId,
  bookingOrderId,
  holdExpiresAt,
}: PaymentCheckoutProps) {
  const [manualOrderId, setManualOrderId] = useState(() =>
    showMethodPicker
      ? new URLSearchParams(window.location.search).get("orderId") ??
        sessionStorage.getItem("sp_payos_latest_order") ?? ""
      : ""
  )
  const providedOrderId = reservationOrderId ?? bookingOrderId
  const orderId = providedOrderId ?? manualOrderId
  const [selected, setSelected] = useState(!showMethodPicker)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState("")
  const [checkout, setCheckout] = useState<CheckoutResponse | null>(null)
  const [status, setStatus] = useState<"pending" | "success" | "failed">("pending")
  const [demoOpen, setDemoOpen] = useState(false)
  const [now, setNow] = useState(Date.now)

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
  useServerRefresh(status === "pending" && transactionId ? transactionId + ":" + orderId : undefined, async (isCurrent) => {
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

  async function pay(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!selected || !isPaymentOrderId(orderId) || busy) return
    if (holdExpiresAt && Date.parse(holdExpiresAt) <= Date.now()) {
      setError("The reservation payment deadline has passed. Refresh the reservation status.")
      return
    }
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

  const holdExpired = !!holdExpiresAt && Date.parse(holdExpiresAt) <= now
  const expired = holdExpired || (checkout ? new Date(checkout.expiresAt).getTime() <= now : false)
  const payable = !!checkout && status === "pending" && !expired
  const showDemo = import.meta.env.DEV && ["localhost", "127.0.0.1", "[::1]"].includes(window.location.hostname)

  return <section className="card payos-checkout" aria-labelledby="payos-checkout-title">
    <div className="payos-checkout-heading"><span>PAYMENT SERVICE</span><h3 id="payos-checkout-title">Pay by QR</h3><p>{showMethodPicker ? "Generate a real VietQR from an existing Payment Service order." : "Your payment details will appear here when the booking creates a payment order."}</p></div>
    {!checkout && !showMethodPicker && !providedOrderId ? <div className="payos-checkout-unavailable" role="status">
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
      <button type="submit" className="btn-primary payos-pay-button" disabled={!selected || !isPaymentOrderId(orderId) || busy || holdExpired}>{busy ? "Generating QR…" : "Generate payment QR"}</button>
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
    {showDemo && <button type="button" className="btn-outline min-h-11 justify-self-start" aria-haspopup="dialog" onClick={() => setDemoOpen(true)}>ByPass · Demo</button>}
    {showDemo && demoOpen && <PaymentDemo onClose={() => setDemoOpen(false)} />}
  </section>
}
