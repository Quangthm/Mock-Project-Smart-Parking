import { useState } from "react"
import type { PaymentMethod } from "../../../lib/types"
import { PAYMENT_OPTIONS } from "../data/paymentOptions"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { PaymentMethodLogo } from "../../../components/payment/PaymentMethodLogo"

// Top-up modal
export function TopUpModal({
  current,
  onClose,
  onTopUp,
}: {
  current: number
  onClose: () => void
  onTopUp: (amt: number) => void
}) {
  const presets = [50000, 100000, 200000, 500000]
  const [custom, setCustom] = useState("")
  const [selected, setSelected] = useState<number | null>(null)
  const [payMethod, setPayMethod] = useState<PaymentMethod | null>(null)

  const amount = selected ?? (parseInt(custom) || 0)

  function confirm() {
    if (amount < 10000 || !payMethod) return
    onTopUp(amount)
  }

  return (
    <div
      style={{
        position: "fixed",
        inset: 0,
        background: "rgba(0,0,0,0.5)",
        display: "flex",
        alignItems: "left",
        justifyContent: "left",
        zIndex: 1000,
        padding: "1rem",
      }}
    >
      <div
        className="card animate-in"
        style={{ maxWidth: "420px", width: "100%", padding: "1.5rem" }}
      >
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            marginBottom: "1.25rem",
          }}
        >
          <h3
            style={{
              fontFamily: "Outfit",
              fontWeight: 700,
              fontSize: "1.1rem",
              color: "var(--fg)",
              margin: 0,
            }}
          >
            Top Up Wallet
          </h3>
          <button
            onClick={onClose}
            style={{
              background: "none",
              border: "none",
              cursor: "pointer",
              fontSize: "1.1rem",
              color: "var(--muted)",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              width: 32,
              height: 32,
              padding: 0,
            }}
          >
            <UntitledIcon name="x" size={18} />
          </button>
        </div>
        <div
          style={{
            background: "var(--primary)10",
            border: "1px solid var(--primary)30",
            borderRadius: "var(--radius)",
            padding: "0.75rem",
            marginBottom: "1.25rem",
            textAlign: "center",
          }}
        >
          <div style={{ fontSize: "0.78rem", color: "var(--muted)" }}>
            Current Balance
          </div>
          <div
            style={{
              fontFamily: "Outfit",
              fontWeight: 800,
              fontSize: "1.4rem",
              color: "var(--primary)",
            }}
          >
            {current.toLocaleString("vi-VN")}₫
          </div>
        </div>
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "1fr 1fr",
            gap: "0.5rem",
            marginBottom: "1rem",
          }}
        >
          {presets.map((p) => (
            <button
              key={p}
              onClick={() => {
                setSelected(p)
                setCustom("")
              }}
              style={{
                padding: "0.625rem",
                borderRadius: "var(--radius)",
                border: `2px solid ${
                  selected === p ? "var(--primary)" : "var(--border)"
                }`,
                background: selected === p ? "var(--primary)10" : "var(--card)",
                cursor: "pointer",
                fontWeight: 600,
                fontSize: "0.875rem",
                color: "var(--fg)",
              }}
            >
              {p.toLocaleString("vi-VN")}₫
            </button>
          ))}
        </div>
        <div style={{ marginBottom: "1rem" }}>
          <label className="label">Or enter custom amount (₫)</label>
          <input
            className="input"
            type="number"
            min={10000}
            step={10000}
            placeholder="e.g. 150000"
            value={custom}
            onChange={(e) => {
              setCustom(e.target.value)
              setSelected(null)
            }}
          />
        </div>
        <div style={{ marginBottom: "1rem" }}>
          <label className="label">Payment Method</label>
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(3, 1fr)",
              gap: "0.375rem",
            }}
          >
            {PAYMENT_OPTIONS.map((pm) => (
              <button
                key={pm.id}
                onClick={() => setPayMethod(pm.id)}
                style={{
                  padding: "0.5rem 0.35rem",
                  borderRadius: "var(--radius)",
                  border: `1.5px solid ${
                    payMethod === pm.id ? "var(--primary)" : "var(--border)"
                  }`,
                  background:
                    payMethod === pm.id ? "color-mix(in srgb, var(--primary) 10%, var(--card))" : "var(--card)",
                  cursor: "pointer",
                  display: "flex",
                  flexDirection: "column",
                  alignItems: "center",
                  gap: "0.35rem",
                  transition: "all 0.15s ease",
                }}
              >
                <PaymentMethodLogo method={pm.id} size="sm" />
                <span style={{ fontSize: "0.68rem", fontWeight: 600, color: "var(--fg)", textAlign: "center" }}>
                  {pm.name.split(" ")[0]}
                </span>
              </button>
            ))}
          </div>
        </div>
        {amount > 0 && payMethod && (
          <div
            style={{
              background: "#f0fdf4",
              border: "1px solid #86efac",
              borderRadius: "var(--radius)",
              padding: "0.625rem",
              marginBottom: "1rem",
              fontSize: "0.82rem",
              color: "#166534",
            }}
          >
            New balance after top-up:{" "}
            <strong>{(current + amount).toLocaleString("vi-VN")}₫</strong>
          </div>
        )}
        <button
          className="btn-primary"
          style={{ width: "100%", justifyContent: "center" }}
          onClick={confirm}
          disabled={amount < 10000 || !payMethod}
        >
          Confirm Top Up
        </button>
      </div>
    </div>
  )
}
