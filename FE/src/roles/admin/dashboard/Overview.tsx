import { useEffect, useState } from "react"

import { adminData as store } from "../data/data"
import { usersApi } from "../../../lib/usersApi"
import { PageHeader } from "../../../components/ui/PageHeader"

import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  BarChart,
  Bar,
  Cell,
} from "recharts"

import { UntitledIcon } from "../../../components/icon/UntitledIcon"

export function Overview() {
  const [userStats, setUserStats] = useState<{
    total: number
    drivers: number
    owners: number
    operators: number
  } | null>(null)
  const [userStatsLoading, setUserStatsLoading] = useState(true)
  const [userStatsError, setUserStatsError] = useState("")
  const [userStatsReload, setUserStatsReload] = useState(0)

  useEffect(() => {
    let current = true
    const query = (role: string) =>
      usersApi.list({ search: "", role, status: "", page: 1 })

    setUserStatsLoading(true)
    setUserStats(null)
    setUserStatsError("")
    Promise.all([query(""), query("driver"), query("owner"), query("operator")])
      .then(([all, drivers, owners, operators]) => {
        if (!current) return
        const totals = [all.total, drivers.total, owners.total, operators.total]
        if (totals.some((value) => !Number.isInteger(value) || value < 0)) {
          throw new Error("Invalid user count response.")
        }
        setUserStats({
          total: all.total,
          drivers: drivers.total,
          owners: owners.total,
          operators: operators.total,
        })
      })
      .catch((error) => {
        if (!current) return
        setUserStats(null)
        setUserStatsError(
          error instanceof Error ? error.message : "Cannot load user counts.",
        )
      })
      .finally(() => {
        if (current) setUserStatsLoading(false)
      })

    return () => {
      current = false
    }
  }, [userStatsReload])

  const bookings = store.getBookings()

  const apps = store.getApplications()

  const logs = store.getAuditLogs().slice(0, 5)

  const totalRevenue = bookings
    .filter((b) => b.status === "completed")
    .reduce((s, b) => s + b.amount, 0)

  const pendingApps = apps.filter((a) => a.status === "pending").length

  // Build booking trend: last 7 days

  const trendData = Array.from({ length: 7 }, (_, i) => {
    const d = new Date()

    d.setDate(d.getDate() - (6 - i))

    const label = d.toLocaleDateString("en-GB", { weekday: "short" })

    const dayBookings = bookings.filter((b) => {
      const bd = new Date(b.createdAt)

      return bd.toDateString() === d.toDateString()
    })

    return {
      day: label,
      bookings: dayBookings.length,
      revenue: dayBookings.reduce((s, b) => s + b.amount, 0),
    }
  })

  const userRoleData = [
    {
      role: "Drivers",
      count: userStats?.drivers ?? 0,
      color: "var(--accent)",
    },

    {
      role: "Owners",
      count: userStats?.owners ?? 0,
      color: "var(--reserved)",
    },

    {
      role: "Operators",
      count: userStats?.operators ?? 0,
      color: "var(--maintenance)",
    },
  ]

  const ROLE_COLORS: Record<string, string> = {
    admin: "var(--occupied)",
    owner: "var(--reserved)",
    driver: "var(--accent)",
    operator: "var(--maintenance)",
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      <PageHeader title="Admin Overview" />
      {/* Top KPI row - Enterprise cards without aggressive gradients */}
      <div className="admin-kpi-strip">
        {[
          {
            label: "Platform Revenue",
            value: `${totalRevenue.toLocaleString("vi-VN")} VND`,
            sub: "All-time booking settlements",
            icon: "banknote",
            badge: "Financial",
          },

          {
            label: "Total Registered Users",
            value: userStatsLoading ? "Loading…" : userStats?.total ?? "—",
            sub: userStatsLoading
              ? "Loading account counts"
              : userStatsError
                ? "Cannot load account counts"
                : `${userStats?.drivers} drivers · ${userStats?.owners} owners · ${userStats?.operators} operators`,
            icon: "users",
            badge: "Accounts",
            retry: !!userStatsError,
          },

          {
            label: "Active Facilities",
            value: "—",
            sub: "Platform-wide facility data is unavailable to Admin",
            icon: "building",
            badge: "Facilities",
          },

          {
            label: "Pending Applications",
            value: pendingApps,
            sub:
              pendingApps > 0
                ? "Requires administrative review"
                : "All applications reviewed",
            icon: "clipboard",
            badge: pendingApps > 0 ? "Action required" : "Up to date",
            alert: pendingApps > 0,
          },
        ].map((k) => (
          <div
            key={k.label}
            className="admin-kpi"
            style={{
              padding: "1.15rem",
              display: "flex",
              flexDirection: "column",
              justifyContent: "space-between",
            }}
          >
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                marginBottom: "0.6rem",
              }}
            >
              <span
                style={{
                  fontSize: "0.875rem",
                  fontWeight: 500,
                  color: "var(--muted)",
                }}
              >
                {k.label}
              </span>
              {k.alert && <span
                style={{
                  fontSize: "0.8rem",
                  padding: "0.15rem 0.5rem",
                  borderRadius: "4px",
                  background: "var(--status-occupied-bg)",
                  color: "var(--occupied)",
                  fontWeight: 600,
                }}
              >
                {k.badge}
              </span>}
            </div>
            <div
              style={{
                fontFamily: "Be Vietnam Pro",
                fontWeight: 700,
                fontSize: "1.65rem",
                color: "var(--fg)",
                lineHeight: 1.2,
                marginBottom: "0.35rem",
              }}
            >
              {k.value}
            </div>
            <div
              style={{
                fontSize: "0.75rem",
                color: k.alert ? "#dc2626" : "var(--muted)",
                display: "flex",
                alignItems: "center",
                gap: "0.35rem",
              }}
            >
              <UntitledIcon name={k.icon} size={13} />
              <span>{k.sub}</span>
              {k.retry && (
                <button
                  type="button"
                  onClick={() => setUserStatsReload((value) => value + 1)}
                  style={{ color: "var(--primary)", textDecoration: "underline" }}
                >
                  Retry
                </button>
              )}
            </div>
          </div>
        ))}
      </div>

      {/* Charts row */}
      <div
        className="admin-overview-charts"
        style={{
          display: "grid",
          gap: "1.25rem",
        }}
      >
        <div className="card" style={{ padding: "1.25rem" }}>
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              marginBottom: "1rem",
            }}
          >
            <div>
              <h3
                style={{
                  fontFamily: "Be Vietnam Pro",
                  fontWeight: 700,
                  fontSize: "0.95rem",
                  color: "var(--fg)",
                  margin: 0,
                }}
              >
                Booking Activity (Last 7 Days)
              </h3>
              <p
                style={{
                  margin: "0.2rem 0 0",
                  fontSize: "0.75rem",
                  color: "var(--muted)",
                }}
              >
                Completed and active driver reservations across all sites
              </p>
            </div>
            <span style={{ fontSize: "0.72rem", color: "var(--muted)" }}>
              Daily aggregation
            </span>
          </div>
          <ResponsiveContainer width="100%" height={200}>
            <AreaChart data={trendData}>
              <CartesianGrid
                strokeDasharray="3 3"
                stroke="var(--border)"
                vertical={false}
              />
              <XAxis
                dataKey="day"
                tick={{ fontSize: 11, fill: "var(--muted)" }}
                tickLine={false}
                axisLine={{ stroke: "var(--border)" }}
              />
              <YAxis
                tick={{ fontSize: 11, fill: "var(--muted)" }}
                allowDecimals={false}
                tickLine={false}
                axisLine={{ stroke: "var(--border)" }}
              />
              <Tooltip
                contentStyle={{
                  background: "var(--card)",
                  border: "1px solid var(--border)",
                  borderRadius: "6px",
                  fontSize: "0.8rem",
                  color: "var(--fg)",
                }}
              />
              <Area
                type="monotone"
                dataKey="bookings"
                stroke="var(--accent)"
                fill="transparent"
                strokeWidth={2}
                name="Bookings"
              />
            </AreaChart>
          </ResponsiveContainer>
        </div>

        <div
          className="card"
          style={{
            padding: "1.25rem",
            display: "flex",
            flexDirection: "column",
            justifyContent: "space-between",
          }}
        >
          <div>
            <h3
              style={{
                fontFamily: "Be Vietnam Pro",
                fontWeight: 700,
                fontSize: "0.95rem",
                color: "var(--fg)",
                margin: 0,
              }}
            >
              User Account Distribution
            </h3>
            <p
              style={{
                margin: "0.2rem 0 0.75rem",
                fontSize: "0.75rem",
                color: "var(--muted)",
              }}
            >
              Platform roles breakdown
            </p>
          </div>
          {userStats ? (
            <>
              <ResponsiveContainer width="100%" height={150}>
                <BarChart data={userRoleData} barSize={28}>
                  <XAxis
                    dataKey="role"
                    tick={{ fontSize: 11, fill: "var(--muted)" }}
                    tickLine={false}
                    axisLine={{ stroke: "var(--border)" }}
                  />
                  <YAxis
                    tick={{ fontSize: 11, fill: "var(--muted)" }}
                    allowDecimals={false}
                    tickLine={false}
                    axisLine={{ stroke: "var(--border)" }}
                  />
                  <Tooltip
                    contentStyle={{
                      background: "var(--card)",
                      border: "1px solid var(--border)",
                      borderRadius: "6px",
                      fontSize: "0.8rem",
                      color: "var(--fg)",
                    }}
                  />
                  <Bar dataKey="count" name="Users" radius={[4, 4, 0, 0]}>
                    {userRoleData.map((entry, i) => (
                      <Cell key={i} fill={entry.color} />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
              <div
                style={{
                  display: "flex",
                  gap: "0.875rem",
                  marginTop: "0.75rem",
                  justifyContent: "center",
                  borderTop: "1px solid var(--border)",
                  paddingTop: "0.6rem",
                }}
              >
                {userRoleData.map((r) => (
                  <div
                    key={r.role}
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: "0.35rem",
                      fontSize: "0.72rem",
                    }}
                  >
                    <div
                      style={{
                        width: 8,
                        height: 8,
                        borderRadius: "2px",
                        background: r.color,
                      }}
                    />
                    <span style={{ color: "var(--muted)" }}>
                      {r.role}:{" "}
                      <strong style={{ color: "var(--fg)" }}>{r.count}</strong>
                    </span>
                  </div>
                ))}
              </div>
            </>
          ) : (
            <div
              role="status"
              style={{ color: "var(--muted)", fontSize: "0.82rem", padding: "2rem 0" }}
            >
              {userStatsLoading ? "Loading user distribution…" : "User distribution is unavailable."}
            </div>
          )}
        </div>
      </div>

      {/* Facilities summary + recent activity */}
      <div
        className="admin-overview-details"
        style={{
          display: "grid",
          gap: "1.25rem",
        }}
      >
        <div className="card" style={{ padding: "1.25rem" }}>
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              marginBottom: "0.85rem",
            }}
          >
            <div>
              <h3
                style={{
                  fontFamily: "Be Vietnam Pro",
                  fontWeight: 700,
                  fontSize: "0.95rem",
                  color: "var(--fg)",
                  margin: 0,
                }}
              >
                Parking Facilities Operational Summary
              </h3>
              <p
                style={{
                  margin: "0.2rem 0 0",
                  fontSize: "0.75rem",
                  color: "var(--muted)",
                }}
              >
                Platform-wide facility data requires an Admin data source
              </p>
            </div>
            <span
              style={{
                fontSize: "0.72rem",
                padding: "0.15rem 0.5rem",
                borderRadius: "4px",
                background:
                  "color-mix(in srgb, var(--primary) 10%, transparent)",
                color: "var(--primary)",
                fontWeight: 600,
              }}
            >
              Unavailable
            </span>
          </div>

          <div
            style={{
              color: "var(--muted)",
              fontSize: "0.82rem",
              padding: "1rem",
              textAlign: "center",
            }}
          >
            Platform-wide parking facility data is unavailable to Admin.
          </div>
        </div>

        <div className="card" style={{ padding: "1.25rem" }}>
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              marginBottom: "0.85rem",
            }}
          >
            <div>
              <h3
                style={{
                  fontFamily: "Be Vietnam Pro",
                  fontWeight: 700,
                  fontSize: "0.95rem",
                  color: "var(--fg)",
                  margin: 0,
                }}
              >
                Recent System Audit Events
              </h3>
              <p
                style={{
                  margin: "0.2rem 0 0",
                  fontSize: "0.75rem",
                  color: "var(--muted)",
                }}
              >
                Authorized administrative audit record
              </p>
            </div>
            <span style={{ fontSize: "0.72rem", color: "var(--muted)" }}>
              Live feed
            </span>
          </div>

          <div
            style={{
              display: "flex",
              flexDirection: "column",
              gap: "0.625rem",
            }}
          >
            {logs.map((log) => (
              <div
                key={log.id}
                style={{
                  display: "flex",
                  gap: "0.75rem",
                  alignItems: "center",
                  padding: "0.45rem 0",
                  borderBottom: "1px solid var(--border)",
                }}
              >
                <div
                  style={{
                    width: 28,
                    height: 28,
                    borderRadius: "50%",
                    background: "var(--raised)",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    flexShrink: 0,
                    fontSize: "0.68rem",
                    fontWeight: 700,
                    color: ROLE_COLORS[log.userRole] || "var(--muted)",
                  }}
                >
                  {log.userName.charAt(0)}
                </div>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div
                    style={{
                      fontSize: "0.8rem",
                      color: "var(--fg)",
                      fontWeight: 500,
                      overflow: "hidden",
                      textOverflow: "ellipsis",
                      whiteSpace: "nowrap",
                    }}
                  >
                    {log.details}
                  </div>
                  <div style={{ fontSize: "0.7rem", color: "var(--muted)" }}>
                    <span
                      style={{
                        textTransform: "capitalize",
                        fontWeight: 600,
                        color: ROLE_COLORS[log.userRole] || "var(--muted)",
                      }}
                    >
                      {log.userRole}
                    </span>{" "}
                    ·{" "}
                    {new Date(log.timestamp).toLocaleString("vi-VN", {
                      dateStyle: "short",
                      timeStyle: "short",
                    })}
                  </div>
                </div>
              </div>
            ))}
            {!logs.length && (
              <div
                style={{
                  textAlign: "center",
                  color: "var(--muted)",
                  fontSize: "0.82rem",
                  padding: "1rem",
                }}
              >
                No recent audit events recorded.
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
