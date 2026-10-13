import { adminData as store } from "../data/data"

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
  const users = store.getUsers()

  const lots = store.getLots()

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
      count: users.filter((u) => u.role === "driver").length,
      color: "#2563eb",
    },

    {
      role: "Owners",
      count: users.filter((u) => u.role === "owner").length,
      color: "#f59e0b",
    },

    {
      role: "Operators",
      count: users.filter((u) => u.role === "operator").length,
      color: "#a855f7",
    },
  ]

  const ROLE_COLORS: Record<string, string> = {
    admin: "#ef4444",
    owner: "#f59e0b",
    driver: "#2563eb",
    operator: "#a855f7",
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Top KPI row - Enterprise cards without aggressive gradients */}
      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
          gap: "1rem",
        }}
      >
        {[
          {
            label: "Platform Revenue",
            value: `${totalRevenue.toLocaleString("vi-VN")}₫`,
            sub: "All-time booking settlements",
            icon: "banknote",
            badge: "Financial",
          },

          {
            label: "Total Registered Users",
            value: users.length,
            sub: `${users.filter((u) => u.role === "driver").length} drivers · ${users.filter((u) => u.role === "owner").length} owners · ${users.filter((u) => u.role === "operator").length} operators`,
            icon: "users",
            badge: "Accounts",
          },

          {
            label: "Active Facilities",
            value: lots.filter((l) => l.status === "active").length,
            sub: `${lots.reduce((s, l) => s + l.totalSlots, 0)} slots across ${lots.length} lots`,
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
            className="card"
            style={{
              padding: "1.15rem",
              display: "flex",
              flexDirection: "column",
              justifyContent: "space-between",
              borderLeft: k.alert ? "3px solid #dc2626" : undefined,
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
                  fontSize: "0.72rem",
                  fontWeight: 600,
                  color: "var(--muted)",
                  textTransform: "uppercase",
                  letterSpacing: "0.05em",
                }}
              >
                {k.label}
              </span>
              <span
                style={{
                  fontSize: "0.68rem",
                  padding: "0.15rem 0.5rem",
                  borderRadius: "4px",
                  background: k.alert
                    ? "#fee2e2"
                    : "color-mix(in srgb, var(--primary) 12%, transparent)",
                  color: k.alert ? "#dc2626" : "var(--primary)",
                  fontWeight: 600,
                }}
              >
                {k.badge}
              </span>
            </div>
            <div
              style={{
                fontFamily: "Outfit",
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
            </div>
          </div>
        ))}
      </div>

      {/* Charts row */}
      <div
        style={{
          display: "grid",
          gridTemplateColumns: "minmax(0, 2fr) minmax(0, 1fr)",
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
                  fontFamily: "Outfit",
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
                stroke="#3b82f6"
                fill="#3b82f620"
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
                fontFamily: "Outfit",
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
              Active platform roles breakdown
            </p>
          </div>
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
        </div>
      </div>

      {/* Facilities summary + recent activity */}
      <div
        style={{
          display: "grid",
          gridTemplateColumns: "minmax(0, 1fr) minmax(0, 1fr)",
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
                  fontFamily: "Outfit",
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
                Overview of managed parking lots and capacity
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
              {lots.length} Registered
            </span>
          </div>

          <div
            style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}
          >
            {lots.slice(0, 4).map((lot) => {
              const freeCount = lot.slots.filter(
                (s) => s.status === "available",
              ).length

              return (
                <div
                  key={lot.id}
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    padding: "0.6rem 0.75rem",
                    background: "var(--bg)",
                    border: "1px solid var(--border)",
                    borderRadius: "6px",
                  }}
                >
                  <div style={{ minWidth: 0, flex: 1, paddingRight: "0.5rem" }}>
                    <div
                      style={{
                        fontSize: "0.82rem",
                        fontWeight: 600,
                        color: "var(--fg)",
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                        whiteSpace: "nowrap",
                      }}
                    >
                      {lot.name}
                    </div>
                    <div style={{ fontSize: "0.72rem", color: "var(--muted)" }}>
                      {lot.type} · {lot.totalSlots} capacity
                    </div>
                  </div>
                  <div
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: "0.5rem",
                      flexShrink: 0,
                    }}
                  >
                    <span
                      style={{
                        fontSize: "0.72rem",
                        color: "#16a34a",
                        fontWeight: 600,
                      }}
                    >
                      {freeCount} vacant
                    </span>
                    <span
                      style={{
                        fontSize: "0.68rem",
                        padding: "0.1rem 0.4rem",
                        borderRadius: "3px",
                        background:
                          lot.status === "active" ? "#22c55e15" : "#ef444415",
                        color: lot.status === "active" ? "#16a34a" : "#dc2626",
                        fontWeight: 700,
                      }}
                    >
                      {lot.status.toUpperCase()}
                    </span>
                  </div>
                </div>
              )
            })}
            {!lots.length && (
              <div
                style={{
                  textAlign: "center",
                  color: "var(--muted)",
                  fontSize: "0.82rem",
                  padding: "1rem",
                }}
              >
                No parking facilities registered.
              </div>
            )}
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
                  fontFamily: "Outfit",
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
                    background: (ROLE_COLORS[log.userRole] || "#64748b") + "20",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    flexShrink: 0,
                    fontSize: "0.68rem",
                    fontWeight: 700,
                    color: ROLE_COLORS[log.userRole] || "#64748b",
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
