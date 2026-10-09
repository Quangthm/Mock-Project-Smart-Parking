import { useEffect, useState } from "react"
import { useApp } from "../../../context/AppContext"
import {
  usersApi,
  type UserDetail,
  type UsersPage,
} from "../../../lib/usersApi"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"

export function Users() {
  const { user: admin } = useApp()
  const [search, setSearch] = useState("")
  const [query, setQuery] = useState({
    search: "",
    role: "",
    status: "",
    page: 1,
  })
  const [page, setPage] = useState<UsersPage | null>(null)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [detail, setDetail] = useState<UserDetail | null>(null)
  const [loading, setLoading] = useState(false)
  const [detailLoading, setDetailLoading] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState("")
  const [detailError, setDetailError] = useState("")
  const [notice, setNotice] = useState("")
  const [reload, setReload] = useState(0)
  const [detailReload, setDetailReload] = useState(0)

  useEffect(() => {
    if (admin?.role !== "admin") return
    let current = true
    setLoading(true)
    setError("")
    usersApi
      .list(query)
      .then((result) => {
        if (current) setPage(result)
      })
      .catch((err) => {
        if (current)
          setError(err instanceof Error ? err.message : "Cannot load users.")
      })
      .finally(() => {
        if (current) setLoading(false)
      })
    return () => {
      current = false
    }
  }, [query, reload, admin?.id, admin?.role])

  useEffect(() => {
    setDetail(null)
    setDetailError("")
    setNotice("")
    setDetailLoading(false)
    if (!selectedId || admin?.role !== "admin") return
    let current = true
    setDetailLoading(true)
    usersApi
      .get(selectedId)
      .then((result) => {
        if (current) setDetail(result)
      })
      .catch((err) => {
        if (current)
          setDetailError(
            err instanceof Error ? err.message : "Cannot load user details.",
          )
      })
      .finally(() => {
        if (current) setDetailLoading(false)
      })
    return () => {
      current = false
    }
  }, [selectedId, detailReload, admin?.id, admin?.role])

  async function changeStatus() {
    if (!detail || saving) return
    const status = detail.user.status === "locked" ? "active" : "locked"
    setSaving(true)
    setDetailError("")
    setNotice("")
    try {
      const updated = await usersApi.setStatus(detail.user.id, status)
      setDetail((current) =>
        current?.user.id === updated.id
          ? {
              user: updated,
              loginActivity: current.loginActivity.map((activity) =>
                status === "locked"
                  ? { ...activity, isRevoked: true }
                  : activity,
              ),
            }
          : current,
      )
      setNotice(
        status === "locked"
          ? "Account locked. Existing sessions have been revoked."
          : "Account unlocked. The user can sign in again.",
      )
      setReload((value) => value + 1)
    } catch (err) {
      setDetailError(
        err instanceof Error ? err.message : "Cannot update account status.",
      )
    } finally {
      setSaving(false)
    }
  }

  function handleSearchSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (saving) return
    setSelectedId(null)
    setQuery((current) => ({ ...current, search: search.trim(), page: 1 }))
  }

  function handleResetFilters() {
    setSearch("")
    setSelectedId(null)
    setQuery({ search: "", role: "", status: "", page: 1 })
  }

  if (admin?.role !== "admin") {
    return (
      <div role="alert" className="p-6 text-sm text-[var(--fg)]">
        Administrator access required.
      </div>
    )
  }

  const managed = detail?.user
  const canChange =
    managed &&
    managed.id !== admin.id &&
    !managed.roles.includes("admin") &&
    ["active", "locked"].includes(managed.status)

  // Pagination calculation
  const totalItems = page?.total ?? 0
  const pageSize = page?.pageSize ?? 10
  const currentPage = page?.page ?? query.page
  const totalPages = Math.max(1, Math.ceil(totalItems / pageSize))
  const startIdx = totalItems === 0 ? 0 : (currentPage - 1) * pageSize + 1
  const endIdx = Math.min(totalItems, currentPage * pageSize)

  const getStatusBadge = (status: string) => {
    switch (status) {
      case "active":
        return {
          label: "Active",
          bg: "#22c55e18",
          color: "#16a34a",
          border: "#22c55e40",
        }
      case "locked":
        return {
          label: "Locked",
          bg: "#ef444418",
          color: "#dc2626",
          border: "#ef444440",
        }
      case "pendingVerification":
        return {
          label: "Pending Verify",
          bg: "#f59e0b18",
          color: "#d97706",
          border: "#f59e0b40",
        }
      case "pendingApproval":
        return {
          label: "Pending Approval",
          bg: "#f59e0b18",
          color: "#d97706",
          border: "#f59e0b40",
        }
      case "rejected":
        return {
          label: "Rejected",
          bg: "#64748b18",
          color: "#64748b",
          border: "#64748b40",
        }
      default:
        return {
          label: status,
          bg: "#64748b18",
          color: "#64748b",
          border: "#64748b40",
        }
    }
  }

  const getRoleBadge = (role: string) => {
    switch (role.toLowerCase()) {
      case "admin":
        return { label: "Admin", color: "#ef4444", bg: "#ef444415" }
      case "owner":
        return { label: "Owner", color: "#d97706", bg: "#f59e0b15" }
      case "operator":
        return { label: "Operator", color: "#7c3aed", bg: "#8b5cf615" }
      case "driver":
      default:
        return { label: "Driver", color: "#2563eb", bg: "#3b82f615" }
    }
  }

  return (
    <section className="w-full space-y-5" aria-label="User Management">
      {/* Page Header */}
      <header className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="font-['Outfit'] text-2xl font-bold text-[var(--fg)]">
              User Management
            </h1>
            <span className="rounded-full bg-[var(--primary)]/10 px-2.5 py-0.5 text-xs font-semibold text-[var(--primary)]">
              {totalItems} accounts
            </span>
          </div>
          <p className="mt-1 text-sm text-[var(--muted)]">
            Search platform accounts, view registration data, manage security
            lockouts, and audit login sessions.
          </p>
        </div>
      </header>

      {/* Redesigned Search & Filters Toolbar */}
      <form
        className="card flex flex-wrap items-center gap-3 p-4"
        onSubmit={handleSearchSubmit}
      >
        {/* Search Field */}
        <div className="relative min-w-[240px] flex-1">
          <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-[var(--muted)]">
            <UntitledIcon name="search" size={16} />
          </span>
          <input
            className="input w-full pl-9 pr-8"
            placeholder="Search by name, email, or phone number..."
            maxLength={200}
            disabled={saving}
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
          {search && (
            <button
              type="button"
              className="absolute right-2.5 top-1/2 -translate-y-1/2 text-[var(--muted)] hover:text-[var(--fg)]"
              onClick={() => setSearch("")}
              title="Clear search"
            >
              <UntitledIcon name="x" size={14} />
            </button>
          )}
        </div>

        {/* Role Filter */}
        <div className="w-auto min-w-[140px]">
          <select
            className="input w-full py-2 text-sm"
            disabled={saving}
            value={query.role}
            onChange={(event) => {
              setSelectedId(null)
              setQuery((current) => ({
                ...current,
                role: event.target.value,
                page: 1,
              }))
            }}
          >
            <option value="">All Roles</option>
            <option value="driver">Driver</option>
            <option value="owner">Owner</option>
            <option value="operator">Operator</option>
            <option value="admin">Admin</option>
          </select>
        </div>

        {/* Status Filter */}
        <div className="w-auto min-w-[150px]">
          <select
            className="input w-full py-2 text-sm"
            disabled={saving}
            value={query.status}
            onChange={(event) => {
              setSelectedId(null)
              setQuery((current) => ({
                ...current,
                status: event.target.value,
                page: 1,
              }))
            }}
          >
            <option value="">All Statuses</option>
            <option value="active">Active</option>
            <option value="locked">Locked</option>
            <option value="pendingVerification">Pending Verification</option>
            <option value="pendingApproval">Pending Approval</option>
            <option value="rejected">Rejected</option>
          </select>
        </div>

        {/* Action Buttons */}
        <div className="flex items-center gap-2">
          <button className="btn-primary" type="submit" disabled={saving}>
            Search
          </button>
          {(query.search || query.role || query.status || search) && (
            <button
              className="btn-outline text-xs"
              type="button"
              onClick={handleResetFilters}
              disabled={saving}
            >
              Reset
            </button>
          )}
        </div>
      </form>

      {error && (
        <div
          role="alert"
          className="flex items-center justify-between rounded-lg border border-red-500/30 bg-red-500/10 p-3 text-sm text-red-600"
        >
          <span>{error}</span>
          <button
            className="btn-outline text-xs"
            onClick={() => setReload((v) => v + 1)}
          >
            Retry
          </button>
        </div>
      )}

      {/* Main Container: Accounts Table (Left) + Master Detail (Right) */}
      <div className="grid grid-cols-1 gap-5 lg:grid-cols-12">
        {/* Accounts Table Container */}
        <div
          className={`${
            selectedId ? "lg:col-span-7 xl:col-span-8" : "lg:col-span-12"
          } flex flex-col space-y-3`}
        >
          <div className="card overflow-hidden p-0" aria-busy={loading}>
            {loading && (
              <div
                className="p-8 text-center text-sm text-[var(--muted)]"
                role="status"
              >
                Loading account records…
              </div>
            )}

            {!loading && page && (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[620px] text-left text-sm">
                  <thead className="border-b border-[var(--border)] bg-[var(--bg)]/50 text-xs uppercase tracking-wider text-[var(--muted)]">
                    <tr>
                      <th className="px-4 py-3 font-semibold">User</th>
                      <th className="px-4 py-3 font-semibold">Contact</th>
                      <th className="px-4 py-3 font-semibold">Role</th>
                      <th className="px-4 py-3 font-semibold">Status</th>
                      <th className="px-4 py-3 font-semibold text-right">
                        Action
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border)]">
                    {page.items.map((account) => {
                      const isSelected = selectedId === account.id
                      const statusBadge = getStatusBadge(account.status)
                      return (
                        <tr
                          key={account.id}
                          className={`transition-colors hover:bg-[var(--bg)]/40 ${
                            isSelected ? "bg-blue-500/5 font-medium" : ""
                          }`}
                        >
                          {/* User info */}
                          <td className="px-4 py-3.5">
                            <div className="flex items-center gap-2.5">
                              <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-[var(--primary)]/15 font-['Outfit'] text-xs font-bold text-[var(--primary)]">
                                {account.fullName?.charAt(0).toUpperCase() ||
                                  "?"}
                              </span>
                              <div className="min-w-0">
                                <div className="truncate font-semibold text-[var(--fg)]">
                                  {account.fullName}
                                </div>
                                <div className="font-mono text-xs text-[var(--muted)] truncate">
                                  ID: {account.id.slice(0, 8)}...
                                </div>
                              </div>
                            </div>
                          </td>

                          {/* Contact */}
                          <td className="px-4 py-3.5 text-xs text-[var(--fg)]">
                            <div>{account.email || "—"}</div>
                            <div className="text-[var(--muted)]">
                              {account.phone || "—"}
                            </div>
                          </td>

                          {/* Roles */}
                          <td className="px-4 py-3.5">
                            <div className="flex flex-wrap gap-1">
                              {account.roles.map((r) => {
                                const rb = getRoleBadge(r)
                                return (
                                  <span
                                    key={r}
                                    style={{
                                      color: rb.color,
                                      background: rb.bg,
                                    }}
                                    className="rounded px-2 py-0.5 text-xs font-semibold capitalize"
                                  >
                                    {rb.label}
                                  </span>
                                )
                              })}
                            </div>
                          </td>

                          {/* Status */}
                          <td className="px-4 py-3.5">
                            <span
                              style={{
                                color: statusBadge.color,
                                background: statusBadge.bg,
                                borderColor: statusBadge.border,
                              }}
                              className="inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-semibold"
                            >
                              {statusBadge.label}
                            </span>
                          </td>

                          {/* Action Button */}
                          <td className="px-4 py-3.5 text-right">
                            <button
                              type="button"
                              className={`btn-outline text-xs px-2.5 py-1 ${
                                isSelected
                                  ? "border-[var(--primary)] bg-[var(--primary)] text-white"
                                  : ""
                              }`}
                              onClick={() => setSelectedId(account.id)}
                            >
                              {isSelected ? "Viewing" : "Details"}
                            </button>
                          </td>
                        </tr>
                      )
                    })}

                    {!page.items.length && (
                      <tr>
                        <td
                          colSpan={5}
                          className="py-12 text-center text-sm text-[var(--muted)]"
                        >
                          No accounts matching the search criteria.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          {/* Balanced Pagination Bar */}
          {page && (
            <div className="card flex flex-wrap items-center justify-between gap-3 p-3.5 text-xs">
              {/* Account Count Indicator */}
              <div className="text-[var(--muted)]">
                {totalItems === 0 ? (
                  <span>No records</span>
                ) : (
                  <span>
                    Showing{" "}
                    <strong className="text-[var(--fg)]">{startIdx}</strong>–
                    <strong className="text-[var(--fg)]">{endIdx}</strong> of{" "}
                    <strong className="text-[var(--fg)]">{totalItems}</strong>{" "}
                    accounts
                  </span>
                )}
              </div>

              {/* Page Number & Navigation Controls */}
              <div className="flex items-center gap-2">
                <span className="mr-1 text-[var(--muted)]">
                  Page{" "}
                  <strong className="text-[var(--fg)]">{currentPage}</strong> of{" "}
                  <strong className="text-[var(--fg)]">{totalPages}</strong>
                </span>

                <button
                  type="button"
                  className="btn-outline px-2.5 py-1 text-xs"
                  disabled={currentPage <= 1 || saving}
                  onClick={() => {
                    setSelectedId(null)
                    setQuery((c) => ({ ...c, page: 1 }))
                  }}
                  title="First Page"
                >
                  First
                </button>

                <button
                  type="button"
                  className="btn-outline px-2.5 py-1 text-xs"
                  disabled={currentPage <= 1 || saving}
                  onClick={() => {
                    setSelectedId(null)
                    setQuery((c) => ({ ...c, page: c.page - 1 }))
                  }}
                  title="Previous Page"
                >
                  Previous
                </button>

                <button
                  type="button"
                  className="btn-outline px-2.5 py-1 text-xs"
                  disabled={currentPage >= totalPages || saving}
                  onClick={() => {
                    setSelectedId(null)
                    setQuery((c) => ({ ...c, page: c.page + 1 }))
                  }}
                  title="Next Page"
                >
                  Next
                </button>

                <button
                  type="button"
                  className="btn-outline px-2.5 py-1 text-xs"
                  disabled={currentPage >= totalPages || saving}
                  onClick={() => {
                    setSelectedId(null)
                    setQuery((c) => ({ ...c, page: totalPages }))
                  }}
                  title="Last Page"
                >
                  Last
                </button>
              </div>
            </div>
          )}
        </div>

        {/* Selected Account Detail Panel */}
        {selectedId && (
          <aside
            className="lg:col-span-5 xl:col-span-4"
            aria-busy={detailLoading || saving}
          >
            <div className="card sticky top-20 space-y-4 p-5">
              <div className="flex items-start justify-between border-b border-[var(--border)] pb-3">
                <div>
                  <h2 className="font-['Outfit'] text-lg font-bold text-[var(--fg)]">
                    Account Profile
                  </h2>
                  <p className="text-xs text-[var(--muted)]">
                    Security, roles, and session activity
                  </p>
                </div>
                <button
                  type="button"
                  className="rounded p-1 text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)]"
                  onClick={() => setSelectedId(null)}
                  title="Close Detail"
                >
                  <UntitledIcon name="x" size={16} />
                </button>
              </div>

              {detailLoading && (
                <div
                  className="py-8 text-center text-sm text-[var(--muted)]"
                  role="status"
                >
                  Loading account profile…
                </div>
              )}

              {detailError && (
                <div
                  role="alert"
                  className="rounded-lg border border-red-500/30 bg-red-500/10 p-3 text-xs text-red-600"
                >
                  {detailError}
                  {!managed && (
                    <button
                      className="btn-outline ml-2 text-xs"
                      onClick={() => setDetailReload((v) => v + 1)}
                    >
                      Retry
                    </button>
                  )}
                </div>
              )}

              {notice && (
                <div
                  role="status"
                  className="rounded-lg border border-green-500/30 bg-green-500/10 p-3 text-xs text-green-600"
                >
                  {notice}
                </div>
              )}

              {managed && (
                <div className="space-y-4">
                  {/* Basic Info */}
                  <div className="flex items-center gap-3">
                    <span className="flex h-12 w-12 items-center justify-center rounded-full bg-[var(--primary)] text-base font-bold text-white">
                      {managed.fullName?.charAt(0).toUpperCase() || "?"}
                    </span>
                    <div className="min-w-0">
                      <div className="font-bold text-[var(--fg)]">
                        {managed.fullName}
                      </div>
                      <div className="text-xs text-[var(--muted)]">
                        {managed.email || "No email provided"}
                      </div>
                      <div className="text-xs text-[var(--muted)]">
                        {managed.phone || "No phone provided"}
                      </div>
                    </div>
                  </div>

                  {/* Attributes Table */}
                  <div className="rounded-lg border border-[var(--border)] bg-[var(--bg)] p-3 text-xs space-y-2">
                    <div className="flex justify-between">
                      <span className="text-[var(--muted)]">Status</span>
                      <span className="font-semibold capitalize text-[var(--fg)]">
                        {managed.status}
                      </span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-[var(--muted)]">
                        Assigned Roles
                      </span>
                      <span className="font-semibold text-[var(--fg)]">
                        {managed.roles.join(", ") || "None"}
                      </span>
                    </div>
                    {managed.createdAt && (
                      <div className="flex justify-between">
                        <span className="text-[var(--muted)]">
                          Registration Date
                        </span>
                        <span className="text-[var(--fg)]">
                          {new Date(managed.createdAt).toLocaleDateString(
                            "vi-VN",
                          )}
                        </span>
                      </div>
                    )}
                    {managed.lockedUntil && (
                      <div className="flex justify-between text-red-600">
                        <span>Locked Until</span>
                        <span>
                          {new Date(managed.lockedUntil).toLocaleString(
                            "vi-VN",
                          )}
                        </span>
                      </div>
                    )}
                  </div>

                  {/* Lock / Unlock Control */}
                  {canChange && (
                    <div className="border-t border-[var(--border)] pt-3">
                      <button
                        type="button"
                        className={`btn-outline w-full justify-center text-xs py-2 ${
                          managed.status === "locked"
                            ? "border-green-600 text-green-600 hover:bg-green-600 hover:text-white"
                            : "border-red-600 text-red-600 hover:bg-red-600 hover:text-white"
                        }`}
                        disabled={saving || detailLoading}
                        onClick={changeStatus}
                      >
                        {saving
                          ? "Updating…"
                          : managed.status === "locked"
                            ? "Unlock Account & Allow Sign-in"
                            : "Lock Account & Revoke Sessions"}
                      </button>
                    </div>
                  )}

                  {/* Recent Login Sessions */}
                  <div className="border-t border-[var(--border)] pt-3 space-y-2">
                    <h3 className="font-semibold text-xs text-[var(--fg)] flex items-center justify-between">
                      <span>Recent Login Sessions</span>
                      <span className="text-[var(--muted)] font-normal">
                        {detail?.loginActivity.length ?? 0} records
                      </span>
                    </h3>

                    <div className="max-h-48 space-y-1.5 overflow-y-auto pr-1">
                      {detail?.loginActivity.map((activity, idx) => {
                        const isRevoked = activity.isRevoked
                        const isExpired =
                          new Date(activity.expiresAt).getTime() <= Date.now()
                        const sessionState = isRevoked
                          ? "Revoked"
                          : isExpired
                            ? "Expired"
                            : "Active"
                        return (
                          <div
                            key={idx}
                            className="flex items-center justify-between rounded border border-[var(--border)] bg-[var(--bg)] px-2.5 py-1.5 text-xs"
                          >
                            <div>
                              <div className="text-[var(--fg)]">
                                {new Date(activity.createdAt).toLocaleString(
                                  "vi-VN",
                                  { dateStyle: "short", timeStyle: "short" },
                                )}
                              </div>
                              <div className="text-[10px] text-[var(--muted)]">
                                Expires:{" "}
                                {new Date(
                                  activity.expiresAt,
                                ).toLocaleTimeString([], {
                                  hour: "2-digit",
                                  minute: "2-digit",
                                })}
                              </div>
                            </div>
                            <span
                              className={`text-[10px] font-semibold uppercase px-1.5 py-0.5 rounded ${
                                sessionState === "Active"
                                  ? "bg-green-500/15 text-green-600"
                                  : sessionState === "Revoked"
                                    ? "bg-red-500/15 text-red-600"
                                    : "bg-slate-500/15 text-[var(--muted)]"
                              }`}
                            >
                              {sessionState}
                            </span>
                          </div>
                        )
                      })}
                      {!detail?.loginActivity.length && (
                        <p className="text-xs text-[var(--muted)] py-2 text-center">
                          No login sessions recorded.
                        </p>
                      )}
                    </div>
                  </div>
                </div>
              )}
            </div>
          </aside>
        )}
      </div>
    </section>
  )
}
