import { useEffect, useState, useMemo } from "react"
import { createPortal } from "react-dom"
import { useApp } from "../../../context/AppContext"
import {
  operatorsApi,
  parkingApi,
  type Operator,
  type Site,
} from "../../../lib/parkingApi"
import { CreateOperatorForm } from "./CreateOperatorForm"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"

export function OperatorManagement({
  selectedSiteId,
  onOperatorsChanged,
}: {
  selectedSiteId: string
  onOperatorsChanged: () => void
}) {
  const { user } = useApp()
  const [operators, setOperators] = useState<Operator[]>([])
  const [sites, setSites] = useState<Site[]>([])
  const [showCreate, setShowCreate] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState("")
  const [message, setMessage] = useState("")
  const [reload, setReload] = useState(0)
  const [searchTerm, setSearchTerm] = useState("")
  const [filterSite, setFilterSite] = useState(selectedSiteId)

  // Reassign Site State & Confirmation
  const [transferOp, setTransferOp] = useState<Operator | null>(null)
  const [targetSiteId, setTargetSiteId] = useState<string>("")
  const [transferError, setTransferError] = useState("")

  useEffect(() => {
    setFilterSite(selectedSiteId)
  }, [selectedSiteId])

  useEffect(() => {
    let current = true
    setLoading(true)
    setError("")
    setMessage("")
    Promise.all([operatorsApi.list(), parkingApi.list()])
      .then(([accounts, parkingSites]) => {
        if (!current) return
        setOperators(accounts)
        setSites(
          parkingSites.filter(
            (site) => site.isActive && site.status === "ACTIVE",
          ),
        )
      })
      .catch((err) => {
        if (current)
          setError(
            err instanceof Error
              ? err.message
              : "Cannot load operators and sites.",
          )
      })
      .finally(() => {
        if (current) setLoading(false)
      })
    return () => {
      current = false
    }
  }, [user?.id, reload])

  function onCreated(operator: Operator) {
    setOperators((current) =>
      [...current, operator].sort((a, b) =>
        a.fullName.localeCompare(b.fullName),
      ),
    )
    setShowCreate(false)
    setMessage(
      `Account created for ${operator.email}. The operator can sign in with this email and the password you provided.`,
    )
    onOperatorsChanged()
  }

  function openTransferModal(op: Operator) {
    setTransferOp(op)
    setTargetSiteId(op.siteIds[0] ?? sites[0]?.id ?? "")
    setTransferError("")
  }

  function closeTransferModal() {
    setTransferOp(null)
    setTargetSiteId("")
    setTransferError("")
  }

  function handleConfirmTransfer() {
    if (!transferOp || !sites.some((site) => site.id === targetSiteId)) return
    // The current backend exposes creation and read-only assignments only.
    // Keep the existing assignment until a reassignment endpoint is available.
    setTransferError(
      "Site changes cannot be saved yet because the server does not support reassignment. The current site assignment has been kept.",
    )
  }

  const visibleOperators = useMemo(() => {
    return operators.filter((op) => {
      const matchSite =
        filterSite === "all" || op.siteIds.includes(filterSite)
      const matchSearch =
        !searchTerm.trim() ||
        op.fullName.toLowerCase().includes(searchTerm.toLowerCase()) ||
        op.email.toLowerCase().includes(searchTerm.toLowerCase())
      return matchSite && matchSearch
    })
  }, [operators, filterSite, searchTerm])

  return (
    <div className="space-y-5 text-[var(--fg)]">
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 border-b border-[var(--border)] pb-4">
        <div>
          <h2 className="text-xl font-bold tracking-tight text-[var(--fg)]">
            Operator Management
          </h2>
          <p className="mt-1 text-xs text-[var(--muted)]">
            Create login accounts for your operators and assign their work sites.
          </p>
        </div>

        <div className="flex items-center gap-2.5">
          <button
            type="button"
            className="btn-outline text-xs px-3 py-2"
            disabled={loading}
            onClick={() => setReload((v) => v + 1)}
          >
            Refresh
          </button>
          <button
            type="button"
            className="btn-primary text-xs px-3.5 py-2 font-medium flex items-center gap-1.5"
            disabled={loading}
            onClick={() => setShowCreate(true)}
          >
            <UntitledIcon name="plus" size={14} />
            <span>Create Operator</span>
          </button>
        </div>
      </div>

      {!loading && !error && !sites.length && (
        <div className="flex flex-wrap items-center gap-3 rounded-lg border border-[var(--border)] p-3 text-xs">
          <p>Create an active site in Sites before assigning an operator.</p>
        </div>
      )}

      {/* Status Banners */}
      {error && (
        <div role="alert" className="flex items-center gap-2 rounded-lg border border-red-500/30 bg-red-500/10 p-3 text-xs text-red-600 dark:text-red-400">
          <UntitledIcon name="alert" size={14} />
          <span>{error}</span>
        </div>
      )}

      {message && (
        <div role="status" className="flex items-center justify-between gap-2 rounded-lg border border-emerald-500/30 bg-emerald-500/10 p-3 text-xs text-emerald-700 dark:text-emerald-300">
          <div className="flex items-center gap-2">
            <UntitledIcon name="check" size={14} />
            <span>{message}</span>
          </div>
          <button
            type="button"
            className="text-emerald-700 dark:text-emerald-300 hover:opacity-75"
            onClick={() => setMessage("")}
          >
            <UntitledIcon name="x" size={12} />
          </button>
        </div>
      )}

      {/* Filters Bar */}
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-[var(--border)] bg-[var(--card)] p-3">
        <div className="flex items-center gap-2 flex-1 min-w-[200px] max-w-sm">
          <UntitledIcon name="search" size={14} className="text-[var(--muted)] shrink-0" />
          <input
            className="w-full bg-transparent text-xs text-[var(--fg)] placeholder:text-[var(--muted)] outline-none"
            placeholder="Search operator by name or email…"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
          />
          {searchTerm && (
            <button
              type="button"
              className="text-[var(--muted)] hover:text-[var(--fg)]"
              onClick={() => setSearchTerm("")}
            >
              <UntitledIcon name="x" size={12} />
            </button>
          )}
        </div>

        <div className="flex items-center gap-2 text-xs">
          <span className="text-[var(--muted)] font-medium">Filter Site:</span>
          <select
            className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-2.5 py-1 text-xs text-[var(--fg)] outline-none"
            value={filterSite}
            onChange={(e) => setFilterSite(e.target.value)}
          >
            <option value="all">All Sites ({operators.length} operators)</option>
            {sites.map((s) => (
              <option key={s.id} value={s.id}>
                {s.code} - {s.name}
              </option>
            ))}
          </select>
        </div>
      </div>

      {/* Operators List / Table */}
      {loading ? (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-8 text-center text-xs text-[var(--muted)]">
          Loading operator accounts and site rosters…
        </div>
      ) : visibleOperators.length > 0 ? (
        <div className="overflow-x-auto rounded-xl border border-[var(--border)] bg-[var(--card)]">
          <table className="w-full text-left text-xs min-w-[640px]">
            <thead className="border-b border-[var(--border)] bg-[var(--bg)] text-[11px] uppercase tracking-wider text-[var(--muted)]">
              <tr>
                <th className="px-4 py-3 font-semibold">Operator</th>
                <th className="px-4 py-3 font-semibold">Status</th>
                <th className="px-4 py-3 font-semibold">Assigned Facilities</th>
                <th className="px-4 py-3 font-semibold text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[var(--border)]">
              {visibleOperators.map((op) => {
                const assignedNames = op.siteIds
                  .map((id) => sites.find((s) => s.id === id)?.name ?? id)
                  .join(", ")

                return (
                  <tr key={op.id} className="hover:bg-[var(--bg)]/50 transition-colors">
                    <td className="px-4 py-3.5">
                      <div className="flex items-center gap-3">
                        <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-[var(--primary)]/10 font-bold text-[var(--primary)] text-xs">
                          {op.fullName.charAt(0).toUpperCase()}
                        </div>
                        <div>
                          <div className="font-semibold text-xs text-[var(--fg)]">
                            {op.fullName}
                          </div>
                          <div className="text-[11px] text-[var(--muted)] font-mono">
                            {op.email}
                          </div>
                        </div>
                      </div>
                    </td>

                    <td className="px-4 py-3.5">
                      <span className="inline-flex items-center gap-1 rounded-full bg-emerald-500/10 px-2 py-0.5 text-[10px] font-semibold text-emerald-600 dark:text-emerald-400 border border-emerald-500/20 uppercase">
                        {op.status || "active"}
                      </span>
                    </td>

                    <td className="px-4 py-3.5">
                      <div className="max-w-xs truncate text-[var(--fg)] font-medium" title={assignedNames}>
                        {assignedNames || "None"}
                      </div>
                      <div className="text-[10px] text-[var(--muted)]">
                        {op.siteIds.length} site(s) assigned
                      </div>
                    </td>

                    <td className="px-4 py-3.5 text-right">
                      <div className="flex items-center justify-end gap-1.5">
                        <button
                          type="button"
                          className="btn-outline text-[11px] py-1 px-2.5"
                          onClick={() => openTransferModal(op)}
                          title="Change assigned parking site for this operator"
                        >
                          Change Site
                        </button>
                      </div>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-8 text-center text-xs text-[var(--muted)]">
          {searchTerm || filterSite !== "all"
            ? "No operators matching the selected filter criteria."
            : "No operator accounts yet. Click 'Create Operator' to add personnel."}
        </div>
      )}

      {/* Modal 1: Create Operator Form Popup */}
      <CreateOperatorForm
        isOpen={showCreate}
        sites={sites}
        existingOperators={operators}
        onCreated={onCreated}
        onClose={() => setShowCreate(false)}
      />

      {/* Modal 3: Transfer / Reassign Site Confirmation Modal (Requirement C) */}
      {transferOp &&
        typeof document !== "undefined" &&
        createPortal(
          <div
            role="dialog"
            aria-modal="true"
            aria-labelledby="transfer-title"
            className="fixed inset-0 z-[1000] flex items-center justify-center p-4 bg-black/75 backdrop-blur-xs"
            onClick={(e) => {
              if (e.target === e.currentTarget) closeTransferModal()
            }}
          >
            <div className="relative w-full max-w-md rounded-2xl border border-[var(--border)] bg-[var(--card)] p-6 shadow-2xl text-[var(--fg)] space-y-4 animate-in">
              <div className="flex items-start justify-between">
                <div>
                  <h3 id="transfer-title" className="text-base font-bold text-[var(--fg)]">
                    Change Site
                  </h3>
                  <p className="text-xs text-[var(--muted)] mt-0.5">
                    Update facility assignment for operator account.
                  </p>
                </div>
                <button
                  type="button"
                  className="text-[var(--muted)] hover:text-[var(--fg)] cursor-pointer"
                  onClick={closeTransferModal}
                >
                  <UntitledIcon name="x" size={16} />
                </button>
              </div>

              {transferError && (
                <div role="alert" className="rounded-lg border border-red-500/30 bg-red-500/10 p-3 text-xs text-red-600 dark:text-red-400">
                  {transferError}
                </div>
              )}

              {/* Operator and Site details */}
              <div className="rounded-xl border border-[var(--border)] bg-[var(--bg)] p-3.5 space-y-2.5 text-xs">
                <div>
                  <span className="text-[var(--muted)] block">Operator:</span>
                  <span className="font-semibold text-[var(--fg)] text-sm">{transferOp.fullName}</span>
                  <span className="text-[var(--muted)] text-[11px] block">{transferOp.email}</span>
                </div>

                <div className="border-t border-[var(--border)] pt-2">
                  <span className="text-[var(--muted)] block">Current Site(s):</span>
                  <span className="font-medium text-[var(--fg)]">
                    {transferOp.siteIds
                      .map((id) => sites.find((s) => s.id === id)?.name ?? id)
                      .join(", ") || "None"}
                  </span>
                </div>

                <div className="border-t border-[var(--border)] pt-2">
                  <label className="label mb-1" htmlFor="transfer-target-site">
                    New Assigned Site:
                  </label>
                  <select
                    id="transfer-target-site"
                    className="input"
                    value={targetSiteId}
                    onChange={(e) => {
                      setTargetSiteId(e.target.value)
                      setTransferError("")
                    }}
                  >
                    {sites.map((s) => (
                      <option key={s.id} value={s.id}>
                        {s.code} - {s.name}
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              {/* Action Buttons */}
              <div className="flex items-center justify-end gap-2.5 pt-2">
                <button
                  type="button"
                  className="btn-outline text-xs px-3.5 py-2"
                  onClick={closeTransferModal}
                >
                  Cancel
                </button>
                <button
                  type="button"
                  className="btn-primary text-xs px-4 py-2 font-medium"
                  onClick={handleConfirmTransfer}
                  disabled={!targetSiteId || (transferOp.siteIds.length === 1 && transferOp.siteIds[0] === targetSiteId)}
                >
                  Confirm Changes
                </button>
              </div>
            </div>
          </div>,
          document.body,
        )}
    </div>
  )
}
