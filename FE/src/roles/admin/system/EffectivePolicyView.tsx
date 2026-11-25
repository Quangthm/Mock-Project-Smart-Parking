import { useCallback, useEffect, useMemo, useState } from "react"

import { Badge } from "../../../components/ui/Badge"
import { Button } from "../../../components/ui/Button"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import {
  getEffective,
  getAdminFacilityDirectory,
  type AdminFacilityDirectory,
  type EffectivePolicyValue,
  type EffectiveReservationPolicy,
} from "../../../lib/adminPolicyApi"
import {
  CANONICAL_POLICY_GROUPS,
  CANONICAL_POLICY_LABELS,
  formatPolicyDateTime,
} from "../../../lib/policyDisplay"
import { AuthApiError } from "../../../lib/authApi"

function toIsoOrUndefined(localValue: string): string | undefined {
  const raw = localValue.trim()
  if (!raw) return undefined
  const parsed = new Date(raw)
  if (Number.isNaN(parsed.getTime())) return undefined
  return parsed.toISOString()
}

function describeEffectiveError(error: unknown): string {
  if (error instanceof AuthApiError) {
    if (error.code === "POLICY_NOT_FOUND") {
      return "This site has no active policy revision at that time. The lot owner must publish an initial revision to establish an effective policy."
    }
    if (error.code === "SITE_TENANT_MISMATCH") {
      return "The selected tenant and parking site do not match. Choose the pair again."
    }
    if (error.code === "INVALID_REQUEST") {
      return "Both tenantId and siteId must be supplied together, or both omitted."
    }
    return error.message
  }
  return error instanceof Error ? error.message : "Something went wrong. Please try again."
}

function ProvenanceBadge({ value }: { value: EffectivePolicyValue }) {
  if (value.source === "SITE_OVERRIDE") {
    return <Badge tone="purple">Site override</Badge>
  }
  return <Badge tone="muted">Platform default</Badge>
}

function EffectiveValueCell({ value }: { value: EffectivePolicyValue | undefined }) {
  if (!value) {
    return (
      <span className="text-xs" style={{ color: "var(--muted)" }}>
        Not present
      </span>
    )
  }
  return (
    <span className="font-semibold" style={{ color: "var(--fg)" }}>
      {value.value} {value.unit}
      {value.isZeroNotDisabled && value.value === 0 && (
        <span className="ml-1 text-xs" style={{ color: "var(--badge-success-fg)" }}>
          (zero value, not disabled)
        </span>
      )}
    </span>
  )
}

export function EffectivePolicyView() {
  const [at, setAt] = useState("")
  const [tenantId, setTenantId] = useState("")
  const [siteId, setSiteId] = useState("")
  const [directory, setDirectory] = useState<AdminFacilityDirectory>({ tenants: [], sites: [] })
  const [directoryLoading, setDirectoryLoading] = useState(true)
  const [directoryError, setDirectoryError] = useState(false)

  const [data, setData] = useState<EffectiveReservationPolicy | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const pairMismatch = (tenantId.trim() === "") !== (siteId.trim() === "")

  const load = useCallback(
    async (override?: { at?: string; tenantId?: string; siteId?: string }) => {
      const nextTenant = override?.tenantId ?? tenantId
      const nextSite = override?.siteId ?? siteId
      const nextAt = override?.at ?? at

      if ((nextTenant.trim() === "") !== (nextSite.trim() === "")) {
        setError("Both tenantId and siteId must be supplied together, or both omitted.")
        return
      }

      setLoading(true)
      setError(null)
      try {
        setData(
          await getEffective({
            tenantId: nextTenant.trim() || undefined,
            siteId: nextSite.trim() || undefined,
            at: toIsoOrUndefined(nextAt),
          }),
        )
      } catch (err) {
        setData(null)
        setError(describeEffectiveError(err))
      } finally {
        setLoading(false)
      }
    },
    [at, tenantId, siteId],
  )

  useEffect(() => {
    void load({ at: "", tenantId: "", siteId: "" })
    // Initial platform evaluation only.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useEffect(() => {
    let active = true
    void getAdminFacilityDirectory()
      .then(result => { if (active) setDirectory(result) })
      .catch(() => { if (active) setDirectoryError(true) })
      .finally(() => { if (active) setDirectoryLoading(false) })
    return () => { active = false }
  }, [])

  const sites = directory.sites.filter(site => !tenantId || site.tenantId === tenantId)
  const arrival = data?.arrivalLateTolerance
  const totalValues = useMemo(() => Object.keys(data?.values ?? {}).length, [data])

  return (
    <div className="space-y-5">
      <section className="card" style={{ borderRadius: 20, padding: "1.25rem" }}>
        <div className="flex items-center gap-2">
          <span style={{ color: "var(--primary)" }}>
            <UntitledIcon name="target" size={18} />
          </span>
          <h2 className="text-base font-semibold" style={{ color: "var(--fg)" }}>
            Effective Policy Simulation
          </h2>
        </div>
        <p className="mt-1 text-xs" style={{ color: "var(--muted)" }}>
          Evaluate the policy a driver would see at a specific moment. Use platform scope or choose
          a tenant and parking site to resolve a site policy.
        </p>

        <div className="mt-4 grid items-end gap-3 sm:grid-cols-3">
          <div>
            <label className="label" htmlFor="effective-at">
              Evaluate at (optional)
            </label>
            <input
              id="effective-at"
              className="input"
              type="datetime-local"
              value={at}
              onChange={(event) => setAt(event.target.value)}
            />
          </div>
          <div>
            <label className="label" htmlFor="effective-tenant">
              Tenant (optional)
            </label>
            <select
              id="effective-tenant"
              className="input"
              value={tenantId}
              disabled={directoryLoading || directoryError}
              onChange={(event) => {
                const nextTenantId = event.target.value
                setTenantId(nextTenantId)
                if (!nextTenantId || !directory.sites.some(site => site.id === siteId && site.tenantId === nextTenantId))
                  setSiteId("")
              }}
            >
              <option value="">Platform scope (all tenants)</option>
              {directory.tenants.map(tenant => (
                <option key={tenant.id} value={tenant.id}>
                  {tenant.name}{tenant.code ? ` (${tenant.code})` : ""}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="label" htmlFor="effective-site">
              Parking site (optional)
            </label>
            <select
              id="effective-site"
              className="input"
              value={siteId}
              disabled={directoryLoading || directoryError}
              onChange={(event) => {
                const site = directory.sites.find(item => item.id === event.target.value)
                setSiteId(site?.id ?? "")
                setTenantId(site?.tenantId ?? "")
              }}
            >
              <option value="">{tenantId ? "Select a parking site…" : "Platform scope (all sites)"}</option>
              {sites.map(site => (
                <option key={site.id} value={site.id}>
                  {site.name}{site.code ? ` (${site.code})` : ""}{site.isActive ? "" : " [INACTIVE]"}
                </option>
              ))}
            </select>
          </div>
        </div>

        {(directoryLoading || directoryError) && (
          <p role="status" className="mt-2 text-xs" style={{ color: "var(--muted)" }}>
            {directoryLoading ? "Loading tenants and parking sites…" : "Tenant and parking site directory is unavailable. Platform evaluation is still available."}
          </p>
        )}

        {pairMismatch && (
          <p className="mt-2 text-xs" style={{ color: "var(--badge-warning-fg)" }}>
            Select a parking site for this tenant, or reset to platform.
          </p>
        )}

        <div className="mt-3 flex flex-wrap gap-2">
          <Button
            variant="primary"
            loading={loading}
            leadingIcon={<UntitledIcon name="target" size={16} />}
            disabled={pairMismatch}
            onClick={() => void load()}
          >
            Evaluate
          </Button>
          <Button
            variant="outline"
            leadingIcon={<UntitledIcon name="activity" size={16} />}
            disabled={loading}
            onClick={() => {
              setAt("")
              setTenantId("")
              setSiteId("")
              void load({ at: "", tenantId: "", siteId: "" })
            }}
          >
            Reset to platform
          </Button>
        </div>
      </section>

      {error && (
        <div
          role="alert"
          className="flex flex-wrap items-center gap-3 rounded-xl border p-3 text-sm"
          style={{
            borderColor: "var(--occupied)",
            background: "color-mix(in srgb, var(--occupied) 10%, var(--card))",
            color: "var(--badge-danger-strong)",
          }}
        >
          <UntitledIcon name="alert" size={18} />
          <span className="min-w-0 flex-1">{error}</span>
          <Button variant="outline" className="text-xs" onClick={() => void load()}>
            Retry
          </Button>
        </div>
      )}

      {loading ? (
        <section className="card" style={{ borderRadius: 20, padding: "1.25rem" }}>
          <div className="grid gap-3">
            {[0, 1, 2, 3, 4].map((n) => (
              <div
                key={n}
                className="animate-pulse rounded-lg"
                style={{ height: 40, background: "var(--border)" }}
              />
            ))}
          </div>
        </section>
      ) : data ? (
        <>
          <section className="card" style={{ borderRadius: 20, padding: "1.25rem" }}>
            <div className="flex flex-wrap items-center gap-3">
              <Badge tone={data.scope === "SITE" ? "purple" : "primary"}>{data.scope} scope</Badge>
              <span className="text-xs" style={{ color: "var(--muted)" }}>
                Evaluated at {formatPolicyDateTime(data.evaluatedAt)}
              </span>
              <span className="text-xs" style={{ color: "var(--muted)" }}>
                Platform revision v{data.platformRevision.version}
              </span>
              <span className="text-xs" style={{ color: "var(--muted)" }}>
                Site revision {data.siteRevision ? `v${data.siteRevision.version}` : "—"}
              </span>
              <span className="text-xs" style={{ color: "var(--muted)" }}>
                {totalValues} values
              </span>
            </div>

            <div
              className="mt-4 flex flex-wrap items-center gap-3 rounded-xl border p-3"
              style={{ borderColor: "var(--border)", background: "var(--bg)" }}
            >
              <UntitledIcon name="clock" size={18} />
              <span className="text-sm font-semibold" style={{ color: "var(--fg)" }}>
                Arrival late tolerance
              </span>
              {arrival?.enabled === true && <Badge tone="success">Enabled</Badge>}
              {arrival?.enabled === false && <Badge tone="danger">Disabled</Badge>}
              {arrival?.enabled == null && <Badge tone="muted">Not set at platform level</Badge>}
              <span className="text-xs" style={{ color: "var(--muted)" }}>
                {arrival ? `${arrival.minutes} minutes` : "—"}
                {arrival?.inherited ? " · inherited" : ""}
              </span>
            </div>
          </section>

          <section className="card" style={{ borderRadius: 20, padding: "1.25rem" }}>
            <h2 className="text-base font-semibold" style={{ color: "var(--fg)" }}>
              Resolved values
            </h2>
            <div
              className="mt-3"
              style={{ overflowX: "auto", border: "1px solid var(--border)", borderRadius: 16, background: "var(--card)" }}
            >
              <table
                role="table"
                className="sp-adaptive-table"
                style={{ width: "100%", minWidth: 640, borderCollapse: "collapse", fontSize: "0.82rem" }}
              >
                <thead>
                  <tr role="row" style={{ borderBottom: "2px solid var(--border)" }}>
                    {["Policy", "Value", "Source"].map((heading) => (
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
                  {CANONICAL_POLICY_GROUPS.map((group) => (
                    <GroupRows key={group.title} title={group.title} keys={group.keys} values={data.values} />
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        </>
      ) : null}
    </div>
  )
}

function GroupRows({
  title,
  keys,
  values,
}: {
  title: string
  keys: string[]
  values: Record<string, EffectivePolicyValue>
}) {
  const present = keys.filter((key) => values[key])
  if (present.length === 0) return null
  return (
    <>
      <tr role="row">
        <td
          role="cell"
          colSpan={3}
          style={{
            padding: "0.5rem 0.75rem",
            background: "var(--bg)",
            color: "var(--muted)",
            fontWeight: 700,
            fontSize: "0.72rem",
            letterSpacing: "0.04em",
            textTransform: "uppercase",
          }}
        >
          {title}
        </td>
      </tr>
      {present.map((key) => {
        const value = values[key]
        return (
          <tr key={key} role="row" style={{ borderBottom: "1px solid var(--border)" }}>
            <td role="cell" data-label="Policy" style={{ padding: "0.625rem 0.75rem" }}>
              <div className="font-medium" style={{ color: "var(--fg)" }}>
                {CANONICAL_POLICY_LABELS[key] ?? key}
              </div>
              <code className="text-xs" style={{ color: "var(--muted)", fontFamily: "monospace" }}>
                {key}
              </code>
            </td>
            <td role="cell" data-label="Value" style={{ padding: "0.625rem 0.75rem" }}>
              <EffectiveValueCell value={value} />
              {value.inherited && (
                <span className="ml-2 text-xs" style={{ color: "var(--muted)" }}>
                  inherited
                </span>
              )}
            </td>
            <td role="cell" data-label="Source" style={{ padding: "0.625rem 0.75rem" }}>
              <ProvenanceBadge value={value} />
            </td>
          </tr>
        )
      })}
    </>
  )
}
