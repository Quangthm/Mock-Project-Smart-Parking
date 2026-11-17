import { useCallback, useEffect, useMemo, useState } from "react"

import { Badge } from "../../../components/ui/Badge"
import { Button } from "../../../components/ui/Button"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { useConfirmation } from "../../../components/ui/useConfirmation"
import {
  activateRevision,
  getCatalogue,
  getRevisionById,
  getRevisions,
  type PlatformPolicyCatalogue,
  type PlatformPolicyKeyMetadata,
  type PolicyRevisionListItem,
  type PolicyRevisionPage,
  type PolicyRevisionStatus,
} from "../../../lib/adminPolicyApi"
import {
  CANONICAL_POLICY_GROUPS,
  CANONICAL_UNIT_SHORT,
  formatPolicyDateTime,
} from "../../../lib/policyDisplay"
import { AuthApiError } from "../../../lib/authApi"
import { PolicyDraftModal } from "./PolicyDraftModal"

const PAGE_SIZE = 20

const STATUS_OPTIONS: Array<PolicyRevisionStatus | "ALL"> = [
  "ALL",
  "DRAFT",
  "SCHEDULED",
  "CURRENT",
  "SUPERSEDED",
]

const SCOPE_OPTIONS = ["ALL", "PLATFORM"] as const

const STATUS_TONE: Record<PolicyRevisionStatus, "warning" | "primary" | "success" | "muted"> = {
  DRAFT: "warning",
  SCHEDULED: "primary",
  CURRENT: "success",
  SUPERSEDED: "muted",
}

const ERROR_MESSAGES: Record<string, string> = {
  RESOURCE_VERSION_CONFLICT:
    "This draft was changed by another administrator. Reload the latest state and try again.",
  CANNOT_MODIFY_ACTIVE_POLICY:
    "This revision has already been activated and can no longer be edited.",
  OVERLAPPING_POLICY_INTERVAL:
    "That activation time overlaps another active or scheduled revision. Choose a different time.",
  CANNOT_ACTIVATE_NON_DRAFT:
    "This revision is no longer a draft, so it cannot be activated.",
  FORBIDDEN_SCOPE_WRITE:
    "Administrators can only manage platform-scope policies. Site policies belong to the lot owner.",
  POLICY_BOUNDS_VIOLATED:
    "One or more override bounds are invalid. Check the minimum and maximum ranges.",
  VALIDATION_FAILED: "Some values are invalid. Review the highlighted fields and try again.",
  POLICY_NOT_FOUND: "No active policy revision was found for this site at that timestamp.",
  SITE_TENANT_MISMATCH: "The tenant and site identifiers do not match.",
  INVALID_REQUEST: "The request could not be processed. Check the supplied values and try again.",
}

function describePolicyError(error: unknown): string {
  if (error instanceof AuthApiError) {
    if (error.code && ERROR_MESSAGES[error.code]) return ERROR_MESSAGES[error.code]
    return error.message
  }
  return error instanceof Error ? error.message : "Something went wrong. Please try again."
}

const POLICY_GROUPS = CANONICAL_POLICY_GROUPS
const UNIT_SHORT = CANONICAL_UNIT_SHORT
const formatDateTime = formatPolicyDateTime

function Skeleton({ className = "" }: { className?: string }) {
  return (
    <div
      className={`animate-pulse rounded-lg ${className}`.trim()}
      style={{ background: "var(--border)" }}
    />
  )
}

function RevisionStatusBadge({ status }: { status: PolicyRevisionStatus }) {
  return <Badge tone={STATUS_TONE[status]}>{status}</Badge>
}

function NoticeBanner({
  tone,
  icon,
  children,
  action,
}: {
  tone: "danger" | "success" | "warning"
  icon: string
  children: React.ReactNode
  action?: React.ReactNode
}) {
  const styles = {
    danger: { border: "var(--occupied)", background: "color-mix(in srgb, var(--occupied) 10%, var(--card))", color: "var(--badge-danger-strong)" },
    warning: { border: "var(--reserved)", background: "color-mix(in srgb, var(--reserved) 12%, var(--card))", color: "var(--badge-warning-fg)" },
    success: { border: "var(--available)", background: "color-mix(in srgb, var(--available) 10%, var(--card))", color: "var(--badge-success-fg)" },
  }[tone]
  return (
    <div
      role={tone === "danger" ? "alert" : "status"}
      className="flex flex-wrap items-center gap-3 rounded-xl border p-3 text-sm"
      style={styles}
    >
      <UntitledIcon name={icon} size={18} />
      <span className="min-w-0 flex-1">{children}</span>
      {action}
    </div>
  )
}

export function AdminPolicyConsole() {
  const { confirm, confirmation } = useConfirmation()

  const [catalogue, setCatalogue] = useState<PlatformPolicyCatalogue | null>(null)
  const [catalogueLoading, setCatalogueLoading] = useState(true)
  const [catalogueError, setCatalogueError] = useState<string | null>(null)

  const [currentRevision, setCurrentRevision] = useState<PolicyRevisionListItem | null>(null)

  const [revisionPage, setRevisionPage] = useState<PolicyRevisionPage | null>(null)
  const [revisionsLoading, setRevisionsLoading] = useState(true)
  const [revisionsError, setRevisionsError] = useState<string | null>(null)

  const [statusFilter, setStatusFilter] = useState<PolicyRevisionStatus | "ALL">("ALL")
  const [scopeFilter, setScopeFilter] = useState<(typeof SCOPE_OPTIONS)[number]>("ALL")
  const [page, setPage] = useState(1)

  const [modal, setModal] = useState<{ mode: "create" | "edit"; revision: PolicyRevisionListItem | null } | null>(null)

  const [activationTime, setActivationTime] = useState("")
  const [activatingId, setActivatingId] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [actionNotice, setActionNotice] = useState<string | null>(null)

  const loadCatalogue = useCallback(async () => {
    setCatalogueLoading(true)
    setCatalogueError(null)
    try {
      setCatalogue(await getCatalogue())
    } catch (error) {
      setCatalogueError(describePolicyError(error))
    } finally {
      setCatalogueLoading(false)
    }
  }, [])

  const loadRevisions = useCallback(async () => {
    setRevisionsLoading(true)
    setRevisionsError(null)
    try {
      setRevisionPage(
        await getRevisions({
          status: statusFilter,
          scope: scopeFilter,
          page,
          pageSize: PAGE_SIZE,
        }),
      )
    } catch (error) {
      setRevisionsError(describePolicyError(error))
    } finally {
      setRevisionsLoading(false)
    }
  }, [statusFilter, scopeFilter, page])

  useEffect(() => {
    void loadCatalogue()
    let alive = true
    getRevisions({ status: "CURRENT", scope: "PLATFORM", page: 1, pageSize: 1 })
      .then((result) => {
        if (alive) setCurrentRevision(result.items[0] ?? null)
      })
      .catch(() => {
        /* Annotation is best-effort; the catalogue still renders without it. */
      })
    return () => {
      alive = false
    }
  }, [loadCatalogue])

  useEffect(() => {
    void loadRevisions()
  }, [loadRevisions])

  const policyByKey = useMemo(() => {
    const map = new Map<string, PlatformPolicyKeyMetadata>()
    catalogue?.policies.forEach((policy) => map.set(policy.key, policy))
    return map
  }, [catalogue])

  const arrivalEnabled = currentRevision?.arrivalLateToleranceEnabled ?? null

  async function handleActivate(item: PolicyRevisionListItem) {
    setActionError(null)
    setActionNotice(null)

    const raw = activationTime.trim()
    let effectiveFrom: string | undefined
    if (raw) {
      const parsed = new Date(raw)
      if (Number.isNaN(parsed.getTime())) {
        setActionError("Enter a valid activation time, or leave it blank to activate immediately.")
        return
      }
      if (parsed.getTime() < Date.now()) {
        setActionError("Activation time cannot be in the past.")
        return
      }
      effectiveFrom = parsed.toISOString()
    }

    const ok = await confirm({
      title: `Activate version ${item.version}?`,
      description: effectiveFrom
        ? `This platform policy revision will become effective from ${formatDateTime(effectiveFrom)}. Any revision currently covering that time will be closed.`
        : "This platform policy revision will be activated immediately and replace the policy currently in effect.",
      confirmLabel: effectiveFrom ? "Schedule activation" : "Activate now",
      danger: false,
    })
    if (!ok) return

    setActivatingId(item.id)
    try {
      const result = await activateRevision(
        item.id,
        effectiveFrom ? { effectiveFrom } : undefined,
      )
      setActionNotice(
        `Version ${result.version} is now ${result.status === "CURRENT" ? "in effect" : "scheduled"}.`,
      )
      await loadRevisions()
      await loadCatalogue()
      try {
        const active = await getRevisions({ status: "CURRENT", scope: "PLATFORM", page: 1, pageSize: 1 })
        setCurrentRevision(active.items[0] ?? null)
      } catch {
        // best effort
      }
    } catch (error) {
      setActionError(describePolicyError(error))
    } finally {
      setActivatingId(null)
    }
  }

  const totalPages = revisionPage?.totalPages ?? 0
  const items = revisionPage?.items ?? []

  return (
    <div className="space-y-5">
      {/* ── Section A · Catalogue ─────────────────────────────────────── */}
      <section className="card" style={{ borderRadius: 20, padding: "1.25rem" }}>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-2">
            <span style={{ color: "var(--primary)" }}>
              <UntitledIcon name="clipboard" size={18} />
            </span>
            <h2 className="text-base font-semibold" style={{ color: "var(--fg)" }}>
              Platform Policy Catalogue
            </h2>
            {catalogue && <Badge tone="muted">v{catalogue.version}</Badge>}
          </div>
          {catalogue && (
            <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs" style={{ color: "var(--muted)" }}>
              <span>Effective from {formatDateTime(catalogue.effectiveFrom)}</span>
              <span>Timezone {catalogue.cancellationTimezone}</span>
            </div>
          )}
        </div>

        {catalogueLoading ? (
          <div className="mt-4 grid gap-3">
            {[0, 1, 2].map((n) => (
              <Skeleton key={n} className="h-20 w-full" />
            ))}
          </div>
        ) : catalogueError ? (
          <div className="mt-4">
            <NoticeBanner
              tone="danger"
              icon="alert"
              action={
                <Button variant="outline" className="text-xs" onClick={() => void loadCatalogue()}>
                  Retry
                </Button>
              }
            >
              {catalogueError}
            </NoticeBanner>
          </div>
        ) : !catalogue || catalogue.policies.length === 0 ? (
          <EmptyState icon="clipboard" message="No platform policies are published yet." />
        ) : (
          <div className="mt-4 grid gap-4">
            {POLICY_GROUPS.map((group) => {
              const rows = group.keys
                .map((key) => policyByKey.get(key))
                .filter((policy): policy is PlatformPolicyKeyMetadata => Boolean(policy))
              if (rows.length === 0) return null
              return (
                <div key={group.title} className="grid gap-2">
                  <div className="flex flex-wrap items-baseline gap-2">
                    <h3
                      className="text-sm font-semibold"
                      style={{ color: "var(--fg)" }}
                    >
                      {group.title}
                    </h3>
                    {group.hint && (
                      <span className="text-xs" style={{ color: "var(--muted)" }}>
                        {group.hint}
                      </span>
                    )}
                  </div>
                  <div className="grid gap-2">
                    {rows.map((policy) => (
                      <PolicyRow
                        key={policy.key}
                        policy={policy}
                        arrivalEnabled={
                          policy.key === "ARRIVAL_LATE_TOLERANCE" ? arrivalEnabled : undefined
                        }
                      />
                    ))}
                  </div>
                </div>
              )
            })}
          </div>
        )}
      </section>

      {/* ── Section B · Revisions ─────────────────────────────────────── */}
      <section className="card" style={{ borderRadius: 20, padding: "1.25rem" }}>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-2">
            <span style={{ color: "var(--primary)" }}>
              <UntitledIcon name="clock" size={18} />
            </span>
            <h2 className="text-base font-semibold" style={{ color: "var(--fg)" }}>
              Policy Revisions
            </h2>
          </div>
          <Button
            variant="primary"
            leadingIcon={<UntitledIcon name="plus" size={16} />}
            onClick={() => setModal({ mode: "create", revision: null })}
          >
            Create draft
          </Button>
        </div>

        <div className="mt-3 flex flex-wrap items-end gap-3">
          <div>
            <label className="label" htmlFor="policy-status-filter">
              Status
            </label>
            <select
              id="policy-status-filter"
              className="input"
              style={{ minWidth: 150 }}
              value={statusFilter}
              onChange={(event) => {
                setStatusFilter(event.target.value as PolicyRevisionStatus | "ALL")
                setPage(1)
              }}
            >
              {STATUS_OPTIONS.map((status) => (
                <option key={status} value={status}>
                  {status}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="label" htmlFor="policy-scope-filter">
              Scope
            </label>
            <select
              id="policy-scope-filter"
              className="input"
              style={{ minWidth: 150 }}
              value={scopeFilter}
              onChange={(event) => {
                setScopeFilter(event.target.value as (typeof SCOPE_OPTIONS)[number])
                setPage(1)
              }}
            >
              {SCOPE_OPTIONS.map((scope) => (
                <option key={scope} value={scope}>
                  {scope}
                </option>
              ))}
            </select>
          </div>
          <div className="min-w-0 flex-1" style={{ minWidth: 220 }}>
            <label className="label" htmlFor="policy-activation-time">
              Activate from (optional)
            </label>
            <input
              id="policy-activation-time"
              className="input"
              type="datetime-local"
              value={activationTime}
              onChange={(event) => setActivationTime(event.target.value)}
            />
            <p className="mt-1 text-xs" style={{ color: "var(--muted)" }}>
              Leave blank to activate immediately. Times are local to this device.
            </p>
          </div>
          <Button
            variant="ghost"
            className="text-sm"
            leadingIcon={<UntitledIcon name="activity" size={16} />}
            disabled={revisionsLoading}
            onClick={() => void loadRevisions()}
          >
            Reload
          </Button>
        </div>

        {actionError && (
          <div className="mt-3">
            <NoticeBanner tone="danger" icon="alert">
              {actionError}
            </NoticeBanner>
          </div>
        )}
        {actionNotice && (
          <div className="mt-3">
            <NoticeBanner tone="success" icon="check-circle">
              {actionNotice}
            </NoticeBanner>
          </div>
        )}

        <div className="mt-4">
          {revisionsLoading ? (
            <div className="grid gap-2">
              {[0, 1, 2, 3].map((n) => (
                <Skeleton key={n} className="h-14 w-full" />
              ))}
            </div>
          ) : revisionsError ? (
            <NoticeBanner
              tone="danger"
              icon="alert"
              action={
                <Button variant="outline" className="text-xs" onClick={() => void loadRevisions()}>
                  Retry
                </Button>
              }
            >
              {revisionsError}
            </NoticeBanner>
          ) : items.length === 0 ? (
            <EmptyState icon="clock" message="No revisions yet." />
          ) : (
            <>
              <div
                style={{
                  overflowX: "auto",
                  border: "1px solid var(--border)",
                  borderRadius: 16,
                  background: "var(--card)",
                }}
              >
                <table
                  role="table"
                  className="sp-adaptive-table"
                  style={{ width: "100%", minWidth: 860, borderCollapse: "collapse", fontSize: "0.82rem" }}
                >
                  <thead>
                    <tr role="row" style={{ borderBottom: "2px solid var(--border)" }}>
                      {["Version", "Scope", "Status", "Active", "Effective from", "Effective until", "Created", "Updated", "Actions"].map((heading) => (
                        <th
                          key={heading}
                          scope="col"
                          role="columnheader"
                          style={{
                            padding: "0.625rem 0.75rem",
                            textAlign: "left",
                            color: "var(--muted)",
                            fontWeight: 600,
                            whiteSpace: "nowrap",
                          }}
                        >
                          {heading}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {items.map((item) => {
                      const isPlatformDraft = item.scope === "PLATFORM" && item.status === "DRAFT"
                      const busy = activatingId === item.id
                      return (
                        <tr key={item.id} role="row" style={{ borderBottom: "1px solid var(--border)" }}>
                          <td role="cell" data-label="Version" style={{ padding: "0.625rem 0.75rem", fontWeight: 700, color: "var(--fg)" }}>
                            v{item.version}
                          </td>
                          <td role="cell" data-label="Scope" style={{ padding: "0.625rem 0.75rem" }}>
                            <Badge tone="muted">{item.scope}</Badge>
                          </td>
                          <td role="cell" data-label="Status" style={{ padding: "0.625rem 0.75rem" }}>
                            <RevisionStatusBadge status={item.status} />
                          </td>
                          <td role="cell" data-label="Active" style={{ padding: "0.625rem 0.75rem", color: item.isActive ? "var(--badge-success-fg)" : "var(--muted)", fontWeight: 600 }}>
                            {item.isActive ? "Active" : "—"}
                          </td>
                          <td role="cell" data-label="Effective from" style={{ padding: "0.625rem 0.75rem", color: "var(--muted)", whiteSpace: "nowrap" }}>
                            {formatDateTime(item.effectiveFrom)}
                          </td>
                          <td role="cell" data-label="Effective until" style={{ padding: "0.625rem 0.75rem", color: "var(--muted)", whiteSpace: "nowrap" }}>
                            {formatDateTime(item.effectiveUntil)}
                          </td>
                          <td role="cell" data-label="Created" style={{ padding: "0.625rem 0.75rem", color: "var(--muted)", whiteSpace: "nowrap" }}>
                            {formatDateTime(item.createdAt)}
                          </td>
                          <td role="cell" data-label="Updated" style={{ padding: "0.625rem 0.75rem", color: "var(--muted)", whiteSpace: "nowrap" }}>
                            {formatDateTime(item.updatedAt)}
                          </td>
                          <td role="cell" data-label="Actions" style={{ padding: "0.625rem 0.75rem" }}>
                            {isPlatformDraft ? (
                              <div className="flex flex-wrap gap-2">
                                <Button
                                  variant="outline"
                                  className="text-xs"
                                  leadingIcon={<UntitledIcon name="file" size={15} />}
                                  onClick={() => setModal({ mode: "edit", revision: item })}
                                >
                                  Edit draft
                                </Button>
                                <Button
                                  variant="primary"
                                  className="text-xs"
                                  loading={busy}
                                  leadingIcon={<UntitledIcon name="zap" size={15} />}
                                  onClick={() => void handleActivate(item)}
                                >
                                  Activate
                                </Button>
                              </div>
                            ) : (
                              <span className="text-xs" style={{ color: "var(--muted)" }}>
                                —
                              </span>
                            )}
                          </td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              </div>

              <div className="mt-3 flex flex-wrap items-center justify-between gap-3">
                <span className="text-xs" style={{ color: "var(--muted)" }}>
                  {revisionPage?.totalCount ?? 0} revision{(revisionPage?.totalCount ?? 0) === 1 ? "" : "s"} · page {page} of {Math.max(1, totalPages)}
                </span>
                <div className="flex gap-2">
                  <Button
                    variant="outline"
                    className="text-sm"
                    disabled={page <= 1 || revisionsLoading}
                    leadingIcon={<UntitledIcon name="arrow-left" size={16} />}
                    onClick={() => setPage((current) => Math.max(1, current - 1))}
                  >
                    Previous
                  </Button>
                  <Button
                    variant="outline"
                    className="text-sm"
                    disabled={page >= totalPages || revisionsLoading}
                    trailingIcon={<UntitledIcon name="arrow-right" size={16} />}
                    onClick={() => setPage((current) => current + 1)}
                  >
                    Next
                  </Button>
                </div>
              </div>
            </>
          )}
        </div>
      </section>

      {modal && (
        <PolicyDraftModal
          mode={modal.mode}
          revision={modal.revision}
          baseRevision={currentRevision}
          onClose={() => setModal(null)}
          onSaved={() => {
            setModal(null)
            setActionNotice(null)
            void loadRevisions()
          }}
          onReload={async (id) => {
            try {
              return await getRevisionById(id)
            } catch {
              return null
            }
          }}
        />
      )}

      {confirmation}
    </div>
  )
}

function EmptyState({ icon, message }: { icon: string; message: string }) {
  return (
    <div
      className="flex items-center gap-2 rounded-xl border border-dashed p-4 text-sm"
      style={{ borderColor: "var(--border)", color: "var(--muted)" }}
    >
      <UntitledIcon name={icon} size={18} />
      {message}
    </div>
  )
}

function PolicyRow({
  policy,
  arrivalEnabled,
}: {
  policy: PlatformPolicyKeyMetadata
  /**
   * Tri-state enablement for the arrival late tolerance capability. `undefined`
   * means the row is not a capability toggle. `null` means the platform does not
   * decide it (each site does).
   */
  arrivalEnabled?: boolean | null
}) {
  const isZero = policy.defaultValue === 0
  const unitShort = UNIT_SHORT[policy.unit] ?? policy.unit

  return (
    <div
      className="flex flex-wrap items-start justify-between gap-3"
      style={{ border: "1px solid var(--border)", borderRadius: 14, padding: "0.75rem 0.9rem", background: "var(--bg)" }}
    >
      <div className="min-w-0" style={{ flex: "1 1 260px" }}>
        <div className="flex flex-wrap items-center gap-2">
          <strong className="text-sm" style={{ color: "var(--fg)" }}>
            {policy.name}
          </strong>
          {policy.isOverridableByOwner ? (
            <Badge tone="primary">Overridable by site</Badge>
          ) : (
            <Badge tone="muted">Platform only</Badge>
          )}
        </div>
        <code className="mt-1 block text-xs" style={{ color: "var(--primary)", fontFamily: "monospace" }}>
          {policy.key}
        </code>
        <p className="mt-1 text-xs" style={{ color: "var(--muted)", lineHeight: 1.5 }}>
          {policy.description}
        </p>
      </div>
      <div className="text-right" style={{ minWidth: 150 }}>
        <p className="text-xs" style={{ color: "var(--muted)" }}>
          Platform default
        </p>
        <p className="text-sm font-semibold" style={{ color: "var(--fg)" }}>
          {policy.defaultValue} {policy.unit}
        </p>
        {isZero && (
          <p className="text-xs" style={{ color: "var(--badge-success-fg)" }}>
            Zero value, not disabled
          </p>
        )}
        {policy.bound && (
          <p className="text-xs" style={{ color: "var(--muted)" }}>
            Site range {policy.bound.minimum}–{policy.bound.maximum} {unitShort}
          </p>
        )}
        {arrivalEnabled !== undefined && <ArrivalEnablement enabled={arrivalEnabled} />}
      </div>
    </div>
  )
}

function ArrivalEnablement({ enabled }: { enabled: boolean | null }) {
  if (enabled === true) {
    return (
      <p className="mt-1">
        <Badge tone="success">Enabled</Badge>
      </p>
    )
  }
  if (enabled === false) {
    return (
      <p className="mt-1">
        <Badge tone="danger">Disabled</Badge>
      </p>
    )
  }
  return (
    <p className="mt-1 text-xs" style={{ color: "var(--muted)" }}>
      Enabled / disabled per site
    </p>
  )
}
