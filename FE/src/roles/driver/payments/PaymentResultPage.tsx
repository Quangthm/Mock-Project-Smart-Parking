import { useEffect, useState } from "react"
import { useNavigate } from "react-router-dom"
import { BrandLogo, BrandWordmark } from "../../../components/brand/BrandLogo"
import { useApp } from "../../../context/AppContext"
import { classifyPaymentStatus, isPaymentOrderId, paymentApi, type PaymentStatusResponse } from "../../../lib/paymentApi"
import { formatStatus } from "../../../lib/formatStatus"
import "./payment-checkout.css"

type ResultState = "checking" | "success" | "failed" | "pending" | "unavailable"

export function PaymentResultPage() {
  const { user, setView } = useApp()
  const navigate = useNavigate()
  const orderId = new URLSearchParams(window.location.search).get("orderId") ?? ""
  const transactionId = new URLSearchParams(window.location.search).get("transactionId") ?? ""
  const [result, setResult] = useState<ResultState>("checking")
  const [detail, setDetail] = useState<PaymentStatusResponse | null>(null)
  const [error, setError] = useState("")
  const [refresh, setRefresh] = useState(0)
  const [returnCountdown, setReturnCountdown] = useState<number | null>(null)

  useEffect(() => {
    let active = true
    let timeout: number | undefined
    const checkout = isPaymentOrderId(orderId) ? paymentApi.rememberedCheckout(orderId) : null
    const statusTransactionId = isPaymentOrderId(transactionId)
      ? transactionId
      : checkout?.paymentTransactionId ?? ""
    if (!isPaymentOrderId(statusTransactionId)) {
      setResult("unavailable")
      setError("This browser has no matching checkout reference. Payment status cannot be confirmed here.")
      return
    }
    setResult("checking")
    setError("")
    let attempts = 0
    async function check() {
      try {
        const status = await paymentApi.status(statusTransactionId)
        if (!active) return
        if (isPaymentOrderId(orderId) && status.orderId.toLowerCase() !== orderId.toLowerCase())
          throw new Error("Payment Service returned a different order.")
        setDetail(status)
        const outcome = classifyPaymentStatus(status)
        if (outcome === "success" || outcome === "failed") { setResult(outcome); return }
        if (++attempts < 12) { timeout = window.setTimeout(check, 5000); return }
        setResult("pending")
      } catch (failure) {
        if (!active) return
        setError(failure instanceof Error ? failure.message : "Payment status is unavailable.")
        setResult("unavailable")
      }
    }
    void check()
    return () => { active = false; if (timeout !== undefined) window.clearTimeout(timeout) }
  }, [orderId, refresh, transactionId])

  useEffect(() => {
    if (result !== "success") {
      setReturnCountdown(null)
      return
    }
    let pending: { siteId?: string; paymentMethod?: string } | null = null
    try {
      pending = JSON.parse(localStorage.getItem("smartpark.pendingCheckout") ?? "null")
    } catch {
      localStorage.removeItem("smartpark.pendingCheckout")
    }
    if (!pending?.siteId || !["PAYOS", "PAYOS_TEST"].includes(pending.paymentMethod ?? "")) return

    setReturnCountdown(5)
    let remaining = 5
    const tick = window.setInterval(() => {
      remaining -= 1
      setReturnCountdown(remaining)
      if (remaining <= 0) {
        window.clearInterval(tick)
        navigate("/dashboard/operation?tab=checkout", { replace: true })
      }
    }, 1000)
    return () => window.clearInterval(tick)
  }, [result, navigate])

  const title = result === "success" ? "Payment successful" : result === "failed" ? "Payment not completed" : result === "checking" ? "Checking your payment" : result === "pending" ? "Payment is still processing" : "Payment status unavailable"
  const description = result === "success" ? "Payment Service confirmed your transaction with payOS." : result === "failed" ? "Payment Service reported that this transaction was cancelled or failed." : result === "checking" ? "We are asking Payment Service for the confirmed result. This may take a moment." : result === "pending" ? "The payment has not been confirmed yet. Please check again before trying to pay." : error

  return <main className="payment-result-page"><section className="card payment-result-card" aria-live="polite">
    <div className="payment-result-brand"><BrandLogo height={38} /><BrandWordmark height={21} /></div>
    <span className={`payment-result-icon is-${result}`} aria-hidden="true">{result === "success" ? "✓" : result === "failed" ? "×" : "·"}</span>
    <h1>{title}</h1><p>{description}</p>
    {detail && <dl className="payment-result-facts"><div><dt>Order code</dt><dd>{detail.orderCode}</dd></div><div><dt>Amount</dt><dd>{detail.amount.toLocaleString("vi-VN")} VND</dd></div><div><dt>Status</dt><dd>{formatStatus(detail.status)}</dd></div></dl>}
    {returnCountdown !== null && <p>Returning to checkout in {returnCountdown} seconds…</p>}
    {(orderId || transactionId) && <p className="payment-result-reference">Payment reference: {orderId || transactionId}</p>}
    <div className="payment-result-actions">
      {(result === "pending" || (result === "unavailable" && (!!paymentApi.rememberedCheckout(orderId) || isPaymentOrderId(transactionId)))) && <button type="button" className="btn-primary" onClick={() => setRefresh(value => value + 1)}>Check status again</button>}
      <button type="button" className="btn-outline" onClick={() => { setView(user?.role === "driver" ? "driver" : "sign-in"); navigate("/") }}>Back to SmartParking</button>
    </div>
  </section></main>
}
