import { useEffect, useMemo, useState } from "react"

import { adminData as store } from "../data/data"

import type { AuditLog } from "../../../lib/types"
import { PageHeader } from "../../../components/ui/PageHeader"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"

export function AuditLogView() {
  const [logs, setLogs] = useState<AuditLog[]>(store.getAuditLogs)

  const [filter, setFilter] = useState("")

  useEffect(() => {
    const refresh = () => setLogs(store.getAuditLogs())
    window.addEventListener("sp-data-change", refresh)
    window.addEventListener("storage", refresh)
    return () => { window.removeEventListener("sp-data-change", refresh); window.removeEventListener("storage", refresh) }
  }, [])

  const filtered = useMemo(() => logs
    .filter(log => `${log.action} ${log.userName} ${log.userRole} ${log.details}`.toLocaleLowerCase().includes(filter.trim().toLocaleLowerCase()))
    .sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime()), [logs, filter])

  const ROLE_COLORS: Record<string, string> = {
    admin: "#ef4444",
    owner: "#f59e0b",
    driver: "#2563eb",
    operator: "var(--muted)",
  }

  return (
    <div className="space-y-5">
      <PageHeader title="Audit Log" variant="compact" description="Review recent account and system actions in one timeline." />
      <div className="audit-log-toolbar">
        <span className="audit-log-count"><UntitledIcon name="activity" size={17} />{filtered.length} events</span>
        <input
        className="input sp-search-input audit-log-search"
        aria-label="Search audit log by action or user"
        autoComplete="off"
        value={filter}
        onChange={(e) => setFilter(e.target.value)}
        placeholder="Search by action or user…"
      />
      </div>
      <div className="audit-log-table-wrap">
        <table
          className="audit-log-table"
          style={{
            width: "100%",
            minWidth: 640,
            borderCollapse: "collapse",
            fontSize: "0.82rem",
          }}
        >
          <thead>
            <tr style={{ borderBottom: "2px solid var(--border)" }}>
              {["Timestamp", "User", "Role", "Action", "Details"].map((h) => (
                <th
                  key={h}
                  style={{
                    padding: "0.625rem 0.75rem",
                    textAlign: "left",
                    color: "var(--muted)",
                    fontWeight: 600,
                    whiteSpace: "nowrap",
                  }}
                >
                  {h}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {filtered.slice(0, 100).map((log) => (
              <tr
                key={log.id}
                style={{ borderBottom: "1px solid var(--border)" }}
              >
                <td title={log.details}
                  style={{
                    padding: "0.5rem 0.75rem",
                    color: "var(--muted)",
                    whiteSpace: "nowrap",
                  }}
                >
                  {new Date(log.timestamp).toLocaleString("en-GB", {
                    dateStyle: "short",
                    timeStyle: "short",
                  })}
                </td>
                <td
                  style={{
                    padding: "0.5rem 0.75rem",
                    fontWeight: 500,
                    color: "var(--fg)",
                  }}
                >
                  {log.userName}
                </td>
                <td style={{ padding: "0.5rem 0.75rem" }}>
                  <span
                    style={{
                      padding: "0.15rem 0.5rem",
                      borderRadius: "999px",
                      fontSize: "0.7rem",
                      fontWeight: 700,
                      background:
                        (ROLE_COLORS[log.userRole] || "var(--muted)") + "20",
                      color: ROLE_COLORS[log.userRole] || "var(--muted)",
                    }}
                  >
                    {log.userRole.toUpperCase()}
                  </span>
                </td>
                <td
                  style={{
                    padding: "0.5rem 0.75rem",
                    fontFamily: "monospace",
                    fontSize: "0.75rem",
                    color: "var(--primary)",
                  }}
                >
                  {log.action}
                </td>
                <td
                  style={{
                    padding: "0.5rem 0.75rem",
                    color: "var(--muted)",
                    maxWidth: "300px",
                    overflow: "hidden",
                    textOverflow: "ellipsis",
                    whiteSpace: "nowrap",
                  }}
                >
                  {log.details}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {!filtered.length && (
          <div
            style={{
              textAlign: "center",
              padding: "2rem",
              color: "var(--muted)",
            }}
          >
            No logs found.
          </div>
        )}
      </div>
    </div>
  )
}
