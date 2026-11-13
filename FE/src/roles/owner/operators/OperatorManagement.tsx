import { useConfirmation } from "../../../components/ui/useConfirmation"
import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { Button } from "../../../components/ui/Button"
import { useEffect, useState, useMemo } from "react"
import { Modal } from "../../../components/ui/Modal"
import { PageHeader } from "../../../components/ui/PageHeader"
import { useApp } from "../../../context/AppContext"
import {
  operatorsApi,
  parkingApi,
  type Operator,
  type Site,
} from "../../../lib/parkingApi"
import { CreateOperatorForm } from "./CreateOperatorForm"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { watchOperators } from "../../../lib/operatorLive"
import { showUiToast } from "../../../lib/uiToast"
import { formatStatus } from "../../../lib/formatStatus"

export function OperatorManagement({
  selectedSiteId,
  onOperatorsChanged,
}: {
  selectedSiteId: string
  onOperatorsChanged: () => void
}) {
  const { user } = useApp()
  const { confirm, confirmation } = useConfirmation()
  const [operators, setOperators] = useState<Operator[]>([])
  const [sites, setSites] = useState<Site[]>([])
  const [showCreate, setShowCreate] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState("")
  const [message, setMessage] = useState("")
  const [reload, setReload] = useState(0)
  const [searchTerm, setSearchTerm] = useState("")
  const [filterSite, setFilterSite] = useState(selectedSiteId)
  const [inviting, setInviting] = useState<string | null>(null)
  useEffect(() => {
    let active = true
    const stop = watchOperators(() => {
      void operatorsApi.list().then(accounts => {
        if (active) setOperators(accounts)
      }).catch(err => {
        if (active) setError(err instanceof Error ? err.message : "Cannot refresh operators.")
      })
    })
    return () => { active = false; stop() }
  }, [user?.id])

  async function resendInvitation(id: string) {
    setInviting(id)
    setError("")
    try {
      await operatorsApi.reissueInvitation(id)
      setMessage("A new activation invitation is queued. Previous links are no longer valid.")
      showUiToast("Invitation queued", "A new activation invitation is on its way.")
    } catch (err) { setError(err instanceof Error ? err.message : "Cannot send invitation.") }
    finally { setInviting(null) }
  }

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
      current.some(existing => existing.id === operator.id) ? current : [...current, operator].sort((a, b) =>
        a.fullName.localeCompare(b.fullName),
      ),
    )
    setShowCreate(false)
    setMessage(
      `Invitation queued for ${operator.email}. The operator must verify their email and choose their own password before signing in.`,
    )
    onOperatorsChanged()
    showUiToast("Operator invited", `An activation invitation is queued for ${operator.email}.`)
  }

  async function handleDeleteOperator(operator: Operator) {
    if (!await confirm({ title: "Remove operator?", description: `Delete the operator account for ${operator.fullName}?`, confirmLabel: "Remove operator", danger: true })) return

    setError("")
    setMessage("")
    try {
      await operatorsApi.remove(operator.id)
      setOperators((current) => current.filter((item) => item.id !== operator.id))
      setMessage(`Operator account ${operator.email} was deleted.`)
      showUiToast("Operator deleted", `${operator.email} was removed.`)
      onOperatorsChanged()
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to delete operator account.")
    }
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
      <PageHeader title="Operator Management" variant="compact" divider description="Create login accounts for your operators and assign their work sites." actions={<div className="flex items-center gap-2.5">
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
        </div>} />

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
            className="grid size-11 place-items-center rounded-full text-emerald-700 hover:bg-emerald-500/10 dark:text-emerald-300 focus-visible:outline-2 focus-visible:outline-[var(--primary)]"
            aria-label="Dismiss operator notice"
            onClick={() => setMessage("")}
          >
            <UntitledIcon name="x" size={12} />
          </button>
        </div>
      )}

      {/* Filters Bar */}
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-[var(--border)] bg-[var(--card)] p-3">
        <div className="sp-search-field flex w-full min-w-0 basis-full items-center gap-2 sm:w-auto sm:min-w-[200px] sm:basis-auto sm:flex-1 sm:max-w-sm">
          <UntitledIcon name="search" size={14} className="text-[var(--muted)] shrink-0" />
          <input
            className="w-full bg-transparent text-xs text-[var(--fg)] placeholder:text-[var(--muted)] outline-none"
            type="search"
            aria-label="Search operators by name or email"
            autoComplete="off"
            placeholder="Search operator by name or email…"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
          />
          {searchTerm && (
            <button
              type="button"
              className="grid size-11 place-items-center rounded-full text-[var(--muted)] hover:bg-[var(--raised)] hover:text-[var(--fg)] focus-visible:outline-2 focus-visible:outline-[var(--primary)]"
              aria-label="Clear operator search"
              onClick={() => setSearchTerm("")}
            >
              <UntitledIcon name="x" size={12} />
            </button>
          )}
        </div>

        <div className="flex items-center gap-2 text-xs">
          <span className="text-[var(--muted)] font-medium">Filter Site:</span>
          <RoundedSelect
            className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-2.5 py-1 text-xs text-[var(--fg)] outline-none"
            value={filterSite}
            onChange={(e) => setFilterSite(e.target.value)}
          >
            <option value="all">All Sites ({operators.length} operators)</option>
            {sites.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
          </RoundedSelect>
        </div>
      </div>

      {/* Operators List / Table */}
      {loading ? (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-8 text-center text-xs text-[var(--muted)]">
          Loading operator accounts and site rosters…
        </div>
      ) : visibleOperators.length > 0 ? (
        <div className="overflow-x-auto rounded-xl border border-[var(--border)] bg-[var(--card)]">
          <table role="table" className="sp-adaptive-table w-full text-left text-xs min-w-[640px]">
            <thead className="border-b border-[var(--border)] bg-[var(--bg)] text-xs uppercase tracking-wider text-[var(--muted)]">
              <tr role="row">
                <th scope="col" role="columnheader" className="px-4 py-3 font-semibold">Operator</th>
                <th scope="col" role="columnheader" className="px-4 py-3 font-semibold">Status</th>
                <th scope="col" role="columnheader" className="px-4 py-3 font-semibold">Assigned Facilities</th>
                <th scope="col" role="columnheader" className="px-4 py-3 font-semibold text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[var(--border)]">
              {visibleOperators.map((op) => {
                const assignedNames = op.siteIds
                  .map((id) => sites.find((s) => s.id === id)?.name ?? id)
                  .join(", ")

                return (
                  <tr role="row" key={op.id} className="hover:bg-[var(--bg)]/50 transition-colors">
                    <td role="cell" data-label="Operator" className="px-4 py-3.5">
                      <div className="flex items-center gap-3">
                        <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-[var(--primary)]/10 font-bold text-[var(--primary)] text-xs">
                          {op.fullName.charAt(0).toUpperCase()}
                        </div>
                        <div>
                          <div className="font-semibold text-xs text-[var(--fg)]">
                            {op.fullName}
                          </div>
                          <div className="text-xs text-[var(--muted)]">
                            {op.email}
                          </div>
                        </div>
                      </div>
                    </td>

                    <td role="cell" data-label="Status" className="px-4 py-3.5">
                      <span className={`inline-flex items-center gap-1 rounded-full border px-2.5 py-1 text-xs font-semibold ${op.status === "active" ? "border-emerald-500/20 bg-emerald-500/10 text-emerald-700 dark:text-emerald-400" : "border-amber-500/30 bg-amber-500/10 text-amber-700 dark:text-amber-300"}`}>
                        {formatStatus(op.status)}
                      </span>
                    </td>

                    <td role="cell" data-label="Assigned facilities" className="px-4 py-3.5">
                      <div className="max-w-xs truncate text-[var(--fg)] font-medium" title={assignedNames}>
                        {assignedNames || "None"}
                      </div>
                      <div className="text-xs text-[var(--muted)]">
                        {op.siteIds.length} site(s) assigned
                      </div>
                    </td>

                    <td role="cell" data-label="Actions" className="px-4 py-3.5 text-right">
                      <div className="flex items-center justify-end gap-1.5">
                        {op.status === "pending_verification" && (
                          <button type="button" className="btn-outline text-xs py-1 px-2.5"
                            disabled={inviting !== null} onClick={() => void resendInvitation(op.id)}>
                            {inviting === op.id ? "Sending…" : "Resend invitation"}
                          </button>
                        )}
                        <button
                          type="button"
                          className="btn-outline text-xs py-1 px-2.5"
                          onClick={() => openTransferModal(op)}
                          title="Change assigned parking site for this operator"
                        >
                          Change Site
                        </button>
                        <Button
                          type="button"
                          variant="danger"
                          className="px-3 text-xs"
                          onClick={() => void handleDeleteOperator(op)}
                          title="Remove this operator account"
                        >
                          Remove Operator
                        </Button>
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

      {confirmation}
      {/* Modal 1: Create Operator Form Popup */}
      <CreateOperatorForm
        isOpen={showCreate}
        sites={sites}
        existingOperators={operators}
        onCreated={onCreated}
        onClose={() => setShowCreate(false)}
      />

      {/* Modal 3: Transfer / Reassign Site Confirmation Modal (Requirement C) */}
      {transferOp && <Modal title="Change Site" onClose={closeTransferModal}>
        <div className="space-y-4">
          <p className="text-sm text-[var(--muted)]">Update facility assignment for operator account.</p>
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
                  <span className="text-[var(--muted)] text-xs block">{transferOp.email}</span>
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
                  <RoundedSelect
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
                        {s.name}
                      </option>
                    ))}
                  </RoundedSelect>
                </div>
              </div>

              {/* Action Buttons */}
              <div className="flex flex-wrap items-center justify-end gap-3 pt-2">
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
      </Modal>}
    </div>
  )
}
