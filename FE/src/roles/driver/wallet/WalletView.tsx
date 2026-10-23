import { useState } from "react"

import { useApp } from "../../../context/AppContext"

import { driverData as store } from "../data/data"

import { TopUpModal } from "./TopUpModal"
import { PageHeader } from "../../../components/ui/PageHeader"

export function WalletView() {
  const { user, setUser } = useApp()

  const balance = user?.wallet ?? 0

  const [showTopUp, setShowTopUp] = useState(false)

  function handleTopUp(amount: number) {
    if (!user) return

    const updated = { ...user, wallet: balance + amount }

    store.saveUser(updated)

    store.addWalletTransaction({
      userId: user.id,
      description: "Wallet top-up",
      amount,
    })

    setUser(updated)

    store.addAuditLog({
      userId: user.id,

      userName: user.name,

      userRole: "driver",

      action: "WALLET_TOPUP",

      details: `Added ${amount.toLocaleString("vi-VN")} VND to wallet`,
    })

    setShowTopUp(false)
  }

  const transactions = user ? store.getWalletTransactions(user.id) : []

  return (
    <div style={{ maxWidth: "560px" }}>
      {showTopUp && (
        <TopUpModal
          current={balance}
          onClose={() => setShowTopUp(false)}
          onTopUp={handleTopUp}
        />
      )}
      <div
        style={{
          background: "var(--card)",

          borderRadius: "var(--radius)",

          padding: "1.75rem",

          marginBottom: "1.5rem",

          color: "var(--fg)",
          border: "1px solid var(--border)",
        }}
      >
        <PageHeader title="SmartParking Wallet" variant="default" />
        <div
          style={{
            fontFamily: "Be Vietnam Pro",

            fontWeight: 800,

            fontSize: "2rem",

            marginBottom: "0.25rem",
          }}
        >
          {balance.toLocaleString("vi-VN")} VND
        </div>
        <div style={{ fontSize: "0.78rem", opacity: 0.75 }}>{user?.name}</div>
        <button
          onClick={() => setShowTopUp(true)}
          style={{
            marginTop: "1.25rem",

            background: "var(--action)",

            border: "1px solid var(--action)",

            borderRadius: "var(--radius)",

            padding: "0.5rem 1.25rem",

            color: "var(--action-fg)",

            fontWeight: 600,

            fontSize: "0.875rem",

            cursor: "pointer",
          }}
        >
          + Top Up
        </button>
      </div>
      <div className="card">
        <h3
          style={{
            fontFamily: "Be Vietnam Pro",

            fontWeight: 700,

            fontSize: "1rem",

            marginBottom: "1rem",

            color: "var(--fg)",
          }}
        >
          Transaction History
        </h3>
        <div
          style={{ display: "flex", flexDirection: "column", gap: "0.625rem" }}
        >
          {transactions.map((t) => (
            <div
              key={t.id}
              style={{
                display: "flex",

                justifyContent: "space-between",

                alignItems: "center",

                padding: "0.625rem 0",

                borderBottom: "1px solid var(--border)",
              }}
            >
              <div>
                <div
                  style={{
                    fontWeight: 500,

                    fontSize: "0.875rem",

                    color: "var(--fg)",
                  }}
                >
                  {t.description}
                </div>
                <div style={{ fontSize: "0.75rem", color: "var(--muted)" }}>
                  {new Date(t.timestamp).toLocaleString()}
                </div>
              </div>
              <div
                style={{
                  fontFamily: "Be Vietnam Pro",

                  fontWeight: 700,

                  fontSize: "0.9rem",

                  color: t.amount > 0 ? "#22c55e" : "#ef4444",
                }}
              >
                {t.amount > 0 ? "+" : ""}
                {t.amount.toLocaleString("vi-VN")} VND
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}
