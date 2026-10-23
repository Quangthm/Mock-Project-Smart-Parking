import { Badge, type BadgeTone } from "../../../components/ui/Badge"
import type { UsersViewModel } from "./useUsers"

const roleTones: Record<string, BadgeTone> = { admin: "danger", owner: "warning", operator: "purple", driver: "primary" }
const roleTone = (role: string): BadgeTone => roleTones[role.toLowerCase()] || "primary"
const roleLabel = (role: string) => ({ admin: "Admin", owner: "Owner", operator: "Operator", driver: "Driver" })[role.toLowerCase() as "admin" | "owner" | "operator" | "driver"] || "Driver"
const statusTone = (status: string): BadgeTone => ({ active: "success", locked: "danger", pendingVerification: "warning", pendingApproval: "warning", rejected: "muted" })[status as "active" | "locked" | "pendingVerification" | "pendingApproval" | "rejected"] || "muted"
const statusLabel = (status: string) => ({ active: "Active", locked: "Locked", pendingVerification: "Pending Verify", pendingApproval: "Pending Approval", rejected: "Rejected" })[status as "active" | "locked" | "pendingVerification" | "pendingApproval" | "rejected"] || status

export function UsersTable({ model }: { model: UsersViewModel }) {
  const { selectedId, setSelectedId, loading, page, saving, totalItems, currentPage, totalPages, startIdx, endIdx, setPageNumber } = model
  return (
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
                  <thead className="border-b border-[var(--border)] text-sm text-[var(--muted)]">
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
                              <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-[var(--primary)]/15 font-['Be_Vietnam_Pro'] text-xs font-bold text-[var(--primary)]">
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
                                
                                return (
                                  <Badge key={r} tone={roleTone(r)} className="rounded capitalize">
                                    {roleLabel(r)}
                                  </Badge>
                                )
                              })}
                            </div>
                          </td>

                          {/* Status */}
                          <td className="px-4 py-3.5">
                            <Badge tone={statusTone(account.status)} className={account.status === "locked" ? "!text-[var(--badge-danger-strong)]" : ""}>
                              {statusLabel(account.status)}
                            </Badge>
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
                    setPageNumber(1)
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
                    setPageNumber(currentPage - 1)
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
                    setPageNumber(currentPage + 1)
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
                    setPageNumber(totalPages)
                  }}
                  title="Last Page"
                >
                  Last
                </button>
              </div>
            </div>
          )}
        </div>

  )
}
