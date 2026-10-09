import { useState, useMemo } from "react"

import { useApp } from "../../../context/AppContext"

import { ownerData as store } from "../data/data"

import type { Booking, ParkingLot } from "../../../lib/types"

import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from "recharts"

import { UntitledIcon } from "../../../components/icon/UntitledIcon"

import { PaymentMethodLogo } from "../../../components/payment/PaymentMethodLogo"

function buildRevenueData(bookings: Booking[]) {
  const MONTHS = [
    "Jan",
    "Feb",
    "Mar",
    "Apr",
    "May",
    "Jun",
    "Jul",
    "Aug",
    "Sep",
    "Oct",
    "Nov",
    "Dec",
  ]

  const now = new Date()

  const result = []

  for (let i = 5; i >= 0; i--) {
    const d = new Date(now.getFullYear(), now.getMonth() - i, 1)

    const month = d.getMonth()

    const year = d.getFullYear()

    const monthly = bookings.filter((b) => {
      const bd = new Date(b.createdAt)

      return (
        bd.getMonth() === month &&
        bd.getFullYear() === year &&
        b.status === "completed"
      )
    })

    const revenue = monthly.reduce((s, b) => s + b.amount, 0)

    result.push({
      month: MONTHS[month],
      revenue,
      bookings: monthly.length,
      profit: Math.round(revenue * 0.85),
    })
  }

  return result
}

export function RevenueDashboard({
  selectedSiteId = "all",
  sites = [],
}: {
  selectedSiteId?: string
  sites?: ParkingLot[]
}) {
  const { user } = useApp()

  const [search, setSearch] = useState("")

  const [filterMethod, setFilterMethod] = useState("all")

  const lots = useMemo(() => {
    const all = sites.length > 0 ? sites : store.getLotsByOwner(user?.id ?? "")

    return selectedSiteId === "all"
      ? all
      : all.filter((l) => l.id === selectedSiteId)
  }, [sites, selectedSiteId, user?.id])

  const allBookings = useMemo(() => {
    return lots.flatMap((l) => store.getBookingsByLot(l.id))
  }, [lots])

  const completedBookings = useMemo(() => {
    return allBookings.filter((b) => b.status === "completed")
  }, [allBookings])

  const totalRevenue = useMemo(() => {
    return completedBookings.reduce((s, b) => s + b.amount, 0)
  }, [completedBookings])

  const netProfit = Math.round(totalRevenue * 0.85)

  const platformFee = Math.round(totalRevenue * 0.15)

  const revenueData = useMemo(
    () => buildRevenueData(allBookings),
    [allBookings],
  )

  const hasData = revenueData.some((d) => d.revenue > 0)

  const filteredTransactions = useMemo(() => {
    return completedBookings.filter((b) => {
      const q = search.trim().toLowerCase()

      const matchQuery =
        !q ||
        b.id.toLowerCase().includes(q) ||
        b.licensePlate.toLowerCase().includes(q) ||
        b.lotName.toLowerCase().includes(q)

      const matchMethod =
        filterMethod === "all" ||
        (b.paymentMethod ?? "unknown") === filterMethod

      return matchQuery && matchMethod
    })
  }, [completedBookings, search, filterMethod])

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      <header>
        <p className="text-sm font-medium text-[var(--primary)]">
          {selectedSiteId === "all"
            ? `All Sites (${lots.length})`
            : (lots[0]?.name ?? "Selected Site")}
        </p>
        <h1 className="mt-1 text-2xl font-bold text-[var(--fg)]">
          Financial & Revenue Center
        </h1>
        <p className="mt-1 text-sm text-[var(--muted)]">
          Comprehensive revenue reconciliation, platform settlements, and
          booking audit for your parking assets.
        </p>
      </header>

      {/* KPI METRIC CARDS */}
      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(210px, 1fr))",
          gap: "1rem",
        }}
      >
        {[
          {
            label: "Gross Revenue",
            value: `${totalRevenue.toLocaleString("vi-VN")}₫`,
            icon: "💰",
            color: "#22c55e",
            sub: "From completed bookings",
          },

          {
            label: "Net Earnings (85%)",
            value: `${netProfit.toLocaleString("vi-VN")}₫`,
            icon: "📈",
            color: "#2563eb",
            sub: "After platform fee settlement",
          },

          {
            label: "Platform Fee (15%)",
            value: `${platformFee.toLocaleString("vi-VN")}₫`,
            icon: "📊",
            color: "#f59e0b",
            sub: "SmartParking system service fee",
          },

          {
            label: "Completed Transactions",
            value: String(completedBookings.length),
            icon: "📋",
            color: "#a855f7",
            sub: `Across ${lots.length} active sites`,
          },
        ].map((stat) => (
          <div key={stat.label} className="card" style={{ padding: "1.15rem" }}>
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                marginBottom: "0.5rem",
              }}
            >
              <span
                style={{
                  fontSize: "0.82rem",
                  color: "var(--muted)",
                  fontWeight: 500,
                }}
              >
                {stat.label}
              </span>
              <span style={{ color: stat.color }}>
                <UntitledIcon name={stat.icon} size={20} />
              </span>
            </div>
            <div
              style={{
                fontFamily: "Outfit",
                fontWeight: 800,
                fontSize: "1.4rem",
                color: stat.color,
                marginBottom: "0.25rem",
              }}
            >
              {stat.value}
            </div>
            <div style={{ fontSize: "0.74rem", color: "var(--muted)" }}>
              {stat.sub}
            </div>
          </div>
        ))}
      </div>

      {/* REVENUE TREND CHART */}
      <div className="card" style={{ padding: "1.25rem" }}>
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: "1.25rem",
            flexWrap: "wrap",
            gap: "0.5rem",
          }}
        >
          <div>
            <h3
              style={{
                fontFamily: "Outfit",
                fontWeight: 700,
                fontSize: "1.05rem",
                margin: 0,
                color: "var(--fg)",
              }}
            >
              6-Month Revenue Trend & Net Settlement
            </h3>
            <span style={{ fontSize: "0.8rem", color: "var(--muted)" }}>
              Comparing gross revenue vs 85% net earnings
            </span>
          </div>
          <div style={{ display: "flex", gap: "1rem", fontSize: "0.78rem" }}>
            <span
              style={{
                display: "inline-flex",
                alignItems: "center",
                gap: "0.35rem",
                color: "#2563eb",
                fontWeight: 600,
              }}
            >
              <span
                style={{
                  width: 10,
                  height: 10,
                  borderRadius: 2,
                  background: "#2563eb",
                }}
              />{" "}
              Gross Revenue
            </span>
            <span
              style={{
                display: "inline-flex",
                alignItems: "center",
                gap: "0.35rem",
                color: "#22c55e",
                fontWeight: 600,
              }}
            >
              <span
                style={{
                  width: 10,
                  height: 10,
                  borderRadius: 2,
                  background: "#22c55e",
                }}
              />{" "}
              Net Settlement (85%)
            </span>
          </div>
        </div>

        {hasData ? (
          <ResponsiveContainer width="100%" height={260}>
            <AreaChart data={revenueData}>
              <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" />
              <XAxis
                dataKey="month"
                tick={{ fontSize: 12, fill: "var(--muted)" }}
              />
              <YAxis
                tickFormatter={(v) =>
                  v === 0 ? "0" : `${(Number(v) / 1000).toFixed(0)}K`
                }
                tick={{ fontSize: 12, fill: "var(--muted)" }}
              />
              <Tooltip
                formatter={(v) => `${Number(v).toLocaleString("vi-VN")}₫`}
                contentStyle={{
                  background: "var(--card)",
                  border: "1px solid var(--border)",
                  borderRadius: "8px",
                  fontSize: "0.82rem",
                }}
              />
              <Area
                type="monotone"
                dataKey="revenue"
                stroke="#2563eb"
                fill="#2563eb18"
                strokeWidth={2}
                name="Gross Revenue"
              />
              <Area
                type="monotone"
                dataKey="profit"
                stroke="#22c55e"
                fill="#22c55e18"
                strokeWidth={2}
                name="Net Revenue"
              />
            </AreaChart>
          </ResponsiveContainer>
        ) : (
          <div
            style={{
              height: 200,
              display: "flex",
              flexDirection: "column",
              alignItems: "center",
              justifyContent: "center",
              color: "var(--muted)",
              gap: "0.5rem",
            }}
          >
            <div style={{ color: "var(--primary)" }}>
              <UntitledIcon name="chart" size={30} />
            </div>
            <div style={{ fontWeight: 500, fontSize: "0.9rem" }}>
              No booking revenue data yet
            </div>
            <div style={{ fontSize: "0.8rem" }}>
              Revenue charts will automatically reflect completed bookings.
            </div>
          </div>
        )}
      </div>

      {/* RECONCILIATION & TRANSACTION TABLE */}
      <div className="card" style={{ padding: "1.25rem" }}>
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: "1rem",
            flexWrap: "wrap",
            gap: "0.75rem",
          }}
        >
          <div>
            <h3
              style={{
                fontFamily: "Outfit",
                fontWeight: 700,
                fontSize: "1.05rem",
                margin: 0,
                color: "var(--fg)",
              }}
            >
              Completed Transactions & Settlement Audit
            </h3>
            <span style={{ fontSize: "0.8rem", color: "var(--muted)" }}>
              Detailed audit trail of all verified completed parking sessions (
              {filteredTransactions.length} records)
            </span>
          </div>

          <div
            style={{
              display: "flex",
              gap: "0.5rem",
              flexWrap: "wrap",
              alignItems: "center",
            }}
          >
            <input
              className="input"
              placeholder="Search by ID, plate, or lot..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              style={{
                padding: "0.35rem 0.65rem",
                fontSize: "0.8rem",
                width: 220,
              }}
            />
            <select
              className="input"
              value={filterMethod}
              onChange={(e) => setFilterMethod(e.target.value)}
              style={{
                padding: "0.35rem 0.65rem",
                fontSize: "0.8rem",
                width: "auto",
              }}
            >
              <option value="all">All Payment Methods</option>
              <option value="momo">MoMo</option>
              <option value="vnpay">VNPay</option>
              <option value="zalopay">ZaloPay</option>
              <option value="visa">Visa / Card</option>
              <option value="applepay">Apple Pay</option>
              <option value="qr">VietQR</option>
            </select>
          </div>
        </div>

        <div
          style={{
            overflowX: "auto",
            border: "1px solid var(--border)",
            borderRadius: "0.625rem",
          }}
        >
          <table
            style={{
              width: "100%",
              minWidth: 640,
              textAlign: "left",
              fontSize: "0.82rem",
              borderCollapse: "collapse",
            }}
          >
            <thead>
              <tr
                style={{
                  borderBottom: "1px solid var(--border)",
                  background: "var(--card)",
                  color: "var(--muted)",
                }}
              >
                <th style={{ padding: "0.65rem 0.85rem" }}>Booking ID</th>
                <th style={{ padding: "0.65rem 0.85rem" }}>Parking Lot</th>
                <th style={{ padding: "0.65rem 0.85rem" }}>Vehicle</th>
                <th style={{ padding: "0.65rem 0.85rem" }}>Payment Method</th>
                <th style={{ padding: "0.65rem 0.85rem", textAlign: "right" }}>
                  Gross Amount
                </th>
                <th style={{ padding: "0.65rem 0.85rem", textAlign: "right" }}>
                  Net (85%)
                </th>
              </tr>
            </thead>
            <tbody>
              {filteredTransactions.map((booking) => {
                const net = Math.round(booking.amount * 0.85)

                return (
                  <tr
                    key={booking.id}
                    style={{
                      borderBottom: "1px solid var(--border)",
                      color: "var(--fg)",
                    }}
                  >
                    <td
                      style={{
                        padding: "0.65rem 0.85rem",
                        fontWeight: 600,
                        fontFamily: "monospace",
                      }}
                    >
                      {booking.id}
                    </td>
                    <td style={{ padding: "0.65rem 0.85rem" }}>
                      {booking.lotName}
                    </td>
                    <td style={{ padding: "0.65rem 0.85rem" }}>
                      <strong>{booking.licensePlate}</strong>
                    </td>
                    <td style={{ padding: "0.65rem 0.85rem" }}>
                      <PaymentMethodLogo
                        method={booking.paymentMethod ?? "qr"}
                        size="xs"
                        showName
                      />
                    </td>
                    <td
                      style={{
                        padding: "0.65rem 0.85rem",
                        textAlign: "right",
                        fontWeight: 600,
                      }}
                    >
                      {booking.amount.toLocaleString("vi-VN")} ₫
                    </td>
                    <td
                      style={{
                        padding: "0.65rem 0.85rem",
                        textAlign: "right",
                        fontWeight: 700,
                        color: "#22c55e",
                      }}
                    >
                      {net.toLocaleString("vi-VN")} ₫
                    </td>
                  </tr>
                )
              })}
              {filteredTransactions.length === 0 && (
                <tr>
                  <td
                    colSpan={6}
                    style={{
                      padding: "2rem",
                      textAlign: "center",
                      color: "var(--muted)",
                    }}
                  >
                    No completed transactions matching your filter.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  )
}
