import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import type { UsersViewModel } from "./useUsers"

export function UserDetailPanel({ model }: { model: UsersViewModel }) {
  const { selectedId, setSelectedId, detailLoading, saving, detailError, notice, managed, canChange, detail, changeStatus, retryDetail } = model
  return (
        selectedId && (
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
                      onClick={() => retryDetail()}
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

                  {/* Locking disables sign-in access and revokes active sessions. */}
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
                            ? "Unlock Account · Restore Access"
                            : "Lock Account · Disable Access"}
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
        )
  )
}
