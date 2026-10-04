import { useState } from "react"
import { useApp } from "../../../context/AppContext"
import type { PackageOrder, PaymentMethod } from "../../../lib/types"
import { driverData as store } from "../data/data"
import { PAYMENT_OPTIONS } from "../data/paymentOptions"

type PlanChoice = { lotId: string; plan: PackageOrder["plan"] }

export function Subscriptions() {
  const { user } = useApp()
  const [choice, setChoice] = useState<PlanChoice | null>(null)
  const [method, setMethod] = useState<PaymentMethod | null>(null)
  const [message, setMessage] = useState("")
  const [lastOrderId, setLastOrderId] = useState("")
  const lots = store.getLots().filter(lot => lot.status === "active")
  const passes = user ? store.getPassesByDriver(user.id) : []
  const orders = user ? store.getPackageOrdersByDriver(user.id) : []
  const selectedLot = choice ? lots.find(lot => lot.id === choice.lotId) : null

  function beginCheckout(lotId: string, plan: PackageOrder["plan"]) {
    setChoice({ lotId, plan })
    setMethod(null)
    setMessage("")
    setLastOrderId("")
  }

  function createOrder() {
    if (!user || !choice || !selectedLot || !method) return
    const amount = choice.plan === "monthly" ? selectedLot.subscriptionMonthly : selectedLot.subscriptionYearly
    const order = store.createPackageOrder({ driverId: user.id, lotId: selectedLot.id, lotName: selectedLot.name, plan: choice.plan, amount, paymentMethod: method })
    setLastOrderId(order.id)
    setMessage(`Payment order ${order.id} is pending. The pass activates after ${PAYMENT_OPTIONS.find(option => option.id === method)?.name} confirms the payment.`)
  }

  return <div>
    <h3 style={{ fontFamily: "Outfit", fontWeight: 700, fontSize: "1.15rem", marginBottom: "1.25rem", color: "var(--fg)" }}>Monthly & Annual Passes</h3>
    <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))", gap: "1rem" }}>
      {lots.map(lot => <div key={lot.id} className="card">
        <div style={{ fontFamily: "Outfit", fontWeight: 700, fontSize: "0.95rem", marginBottom: "0.375rem", color: "var(--fg)" }}>{lot.name}</div>
        <div style={{ fontSize: "0.8rem", color: "var(--muted)", marginBottom: "1rem" }}>{lot.address}</div>
        <div style={{ display: "grid", gap: "0.5rem" }}>
          <PlanPrice label="Monthly Pass" detail="30-day unlimited" amount={lot.subscriptionMonthly} />
          <PlanPrice label="Annual Pass" detail="365 days — save 2 months" amount={lot.subscriptionYearly} />
        </div>
        <div style={{ display: "flex", gap: "0.5rem", marginTop: "0.875rem" }}>
          <button type="button" className="btn-primary" style={{ flex: 1, justifyContent: "center", fontSize: "0.78rem", padding: "0.5rem" }} onClick={() => beginCheckout(lot.id, "monthly")}>Buy Monthly</button>
          <button type="button" className="btn-primary" style={{ flex: 1, justifyContent: "center", fontSize: "0.78rem", padding: "0.5rem" }} onClick={() => beginCheckout(lot.id, "annual")}>Buy Annual</button>
        </div>
      </div>)}
    </div>

    {choice && selectedLot && <section className="card" style={{ marginTop: "1.25rem" }}>
      <h4 style={{ margin: "0 0 0.35rem", fontFamily: "Outfit", color: "var(--fg)" }}>Choose payment method</h4>
      <p style={{ margin: "0 0 0.9rem", color: "var(--muted)", fontSize: "0.82rem" }}>{choice.plan === "monthly" ? "Monthly" : "Annual"} pass for {selectedLot.name} · {(choice.plan === "monthly" ? selectedLot.subscriptionMonthly : selectedLot.subscriptionYearly).toLocaleString("vi-VN")}₫</p>
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(105px, 1fr))", gap: "0.6rem" }}>
        {PAYMENT_OPTIONS.map(option => <button key={option.id} type="button" aria-pressed={method === option.id} onClick={() => { setMethod(option.id); setMessage(""); setLastOrderId("") }} style={{ display: "grid", justifyItems: "center", gap: "0.4rem", padding: "0.75rem 0.4rem", borderRadius: "var(--radius)", border: `2px solid ${method === option.id ? "var(--primary)" : "var(--border)"}`, background: "var(--card)", cursor: "pointer" }}>
          <img src={option.logo} alt="" style={{ maxWidth: 64, height: 26, objectFit: "contain" }} /><span style={{ fontSize: "0.72rem", color: "var(--fg)" }}>{option.name}</span>
        </button>)}
      </div>
      <div style={{ display: "flex", gap: "0.6rem", marginTop: "1rem" }}>
        <button type="button" className="btn-outline" onClick={() => { setChoice(null); setMessage(""); setLastOrderId("") }}>Cancel</button>
        <button type="button" className="btn-primary" disabled={!method || !!lastOrderId} onClick={createOrder}>Continue to payment</button>
      </div>
      {message && <p role="status" style={{ marginBottom: 0, color: "#f59e0b", fontSize: "0.82rem" }}>{message}<br />A live payment gateway is not configured, so no pass has been activated or marked as paid.</p>}
    </section>}

    {orders.length > 0 && <section style={{ marginTop: "1.5rem" }}><h4 style={{ fontFamily: "Outfit", color: "var(--fg)" }}>Package payment orders</h4><div style={{ display: "grid", gap: "0.5rem" }}>{orders.map(order => <div className="card" key={order.id} style={{ display: "flex", justifyContent: "space-between", gap: "1rem", flexWrap: "wrap" }}><span>{order.plan === "monthly" ? "Monthly" : "Annual"} · {order.lotName} · {order.paymentMethod.toUpperCase()} · {order.amount.toLocaleString("vi-VN")}₫</span><span style={{ color: order.paymentStatus === "paid" ? "#22c55e" : order.paymentStatus === "failed" ? "#ef4444" : "#f59e0b", fontWeight: 700 }}>{order.paymentStatus === "pending" ? "Payment Pending" : order.paymentStatus}</span></div>)}</div></section>}
    {passes.length > 0 && <section style={{ marginTop: "1.5rem" }}><h4 style={{ fontFamily: "Outfit", color: "var(--fg)" }}>Your active passes</h4><div style={{ display: "grid", gap: "0.5rem" }}>{passes.map(pass => <div className="card" key={pass.id} style={{ display: "flex", justifyContent: "space-between", gap: "1rem", flexWrap: "wrap" }}><span>{pass.plan === "monthly" ? "Monthly" : "Annual"} · {pass.lotName}</span><span style={{ color: new Date(pass.expiresAt) > new Date() ? "#22c55e" : "var(--muted)" }}>{new Date(pass.expiresAt) > new Date() ? "Active" : "Expired"} · Expires {new Date(pass.expiresAt).toLocaleDateString()}</span></div>)}</div></section>}
  </div>
}

function PlanPrice({ label, detail, amount }: { label: string; detail: string; amount: number }) {
  return <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: "0.5rem", background: "var(--bg)", padding: "0.625rem", borderRadius: "var(--radius)", border: "1px solid var(--border)" }}>
    <span><strong style={{ display: "block", fontSize: "0.875rem", color: "var(--fg)" }}>{label}</strong><span style={{ fontSize: "0.75rem", color: "var(--muted)" }}>{detail}</span></span>
    <strong style={{ color: "var(--primary)", whiteSpace: "nowrap" }}>{amount.toLocaleString("vi-VN")}₫</strong>
  </div>
}
