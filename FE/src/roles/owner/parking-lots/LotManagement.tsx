import { Fragment, useState, type ReactNode } from "react"
import { Modal } from "../../../components/ui/Modal"
import { LotActionMenu } from "./LotActionMenu"
import { useApp } from "../../../context/AppContext"
import { useServerRefresh } from "../../../lib/serverRefresh"
import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { PageHeader } from "../../../components/ui/PageHeader"
import { useLotManagement } from "./useLotManagement"
import { LotDetailModal } from "./LotDetailModal"
import { LotEditModal } from "./LotEditModal"

const lotColumns = [
  { id: "setup", label: "Setup", width: 200 },
  { id: "status", label: "Status", width: 95 },
  { id: "capacity", label: "Capacity", width: 90 },
  { id: "available", label: "Available / Capacity", width: 160 },
  { id: "occupied", label: "Occupied", width: 90 },
  { id: "reserved", label: "Reserved", width: 90 },
  { id: "pending", label: "Pending Pay", width: 112 },
  { id: "backup", label: "Backup", width: 80 },
  { id: "occupancy", label: "Occupancy", width: 130 },
] as const

type LotColumnId = typeof lotColumns[number]["id"]
type TableSettings = {
  columns: LotColumnId[]
  pagination: "numbered" | "load-more"
}

const defaultSettings: TableSettings = {
  columns: lotColumns.map(column => column.id),
  pagination: "numbered",
}
const pageSize = 10

export function LotManagement({ siteFilterId = "all" }: { siteFilterId?: string }) {
  const model = useLotManagement(siteFilterId)
  const { user } = useApp()
  useServerRefresh(user?.role === "owner" ? user.id : undefined, () => {
    if (!model.loading && !model.refreshing && !model.editSite && !model.detailSite) model.refresh()
  }, 45_000)
  const [settings, setSettings] = useState(defaultSettings)
  const [draft, setDraft] = useState<TableSettings | null>(null)
  const [page, setPage] = useState(1)
  const [dragColumn, setDragColumn] = useState<LotColumnId | null>(null)
  const [dropColumn, setDropColumn] = useState<LotColumnId | null>(null)
  const visibleColumns = settings.columns.map(id => lotColumns.find(column => column.id === id)!)
  const tableWidth = 320 + visibleColumns.reduce((sum, column) => sum + column.width, 0)

  const {
    loading, error, searchTerm, setSearchTerm, statusFilter, setStatusFilter,
    sortBy, setSortBy, setDetailSite, setEditSite,
    filteredData, totals, summaryValue, handleNavigateToStructure, refresh,
  } = model

  const pageCount = Math.max(1, Math.ceil(filteredData.length / pageSize))
  const currentPage = Math.min(page, pageCount)
  const start = settings.pagination === "numbered" ? (currentPage - 1) * pageSize : 0
  const rows = filteredData.slice(start, currentPage * pageSize)
  const draftColumns = draft?.columns.map(id => lotColumns.find(column => column.id === id)!) ?? []

  function moveColumn(id: LotColumnId, target: LotColumnId) {
    if (id === target) return
    setDraft(current => {
      if (!current) return current
      const columns = current.columns.filter(column => column !== id)
      columns.splice(current.columns.indexOf(target), 0, id)
      return { ...current, columns }
    })
  }

  function clearDrag() {
    setDragColumn(null)
    setDropColumn(null)
  }

  return (
    <div className="space-y-6 text-[var(--fg)]">
      <PageHeader title="My Parking Lots (Operational Overview)" variant="compact" divider description="Live capacity status, physical vehicle counts, and reservation protection across your facilities." actions={<div className="flex items-center gap-2">
          <button
            type="button"
            className="btn-outline text-xs px-3 py-2"
            disabled={loading}
            onClick={() => refresh()}
          >
            Refresh Data
          </button>
        </div>} />

      {error && (
        <div role="alert" className="flex items-center gap-2 rounded-lg border border-red-500/30 bg-red-500/10 p-3 text-xs text-red-600 dark:text-red-400">
          <UntitledIcon name="alert" size={14} />
          <span className="flex-1">{error}</span>
          <button
            type="button"
            className="btn-outline text-[11px] py-1 px-2"
            disabled={loading}
            onClick={() => refresh()}
          >
            Retry
          </button>
        </div>
      )}

      {/* Structured Operational Metric KPIs */}
      <div className="grid gap-3 grid-cols-2 sm:grid-cols-3 lg:grid-cols-6">
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-3.5 space-y-1">
          <span className="text-[11px] font-medium text-[var(--muted)] block">Configured Capacity</span>
          <span className="text-lg font-bold text-[var(--fg)]">{summaryValue(totals.totalCapacity)}</span>
          <span className="text-[10px] text-[var(--muted)] block">{totals.totalSites} active facilities</span>
        </div>

        <div className="rounded-xl border border-emerald-500/20 bg-emerald-500/5 p-3.5 space-y-1">
          <span className="text-[11px] font-medium text-emerald-600 dark:text-emerald-400 block">Available Slots</span>
          <span className="text-lg font-bold tabular-nums text-emerald-600 dark:text-emerald-400">{summaryValue(totals.totalAvailable)}/{summaryValue(totals.totalCapacity)}</span>
          <span className="text-[10px] text-emerald-600/70 dark:text-emerald-400/70 block">Ready for parking</span>
        </div>

        <div className="rounded-xl border border-slate-500/20 bg-slate-500/5 p-3.5 space-y-1">
          <span className="text-[11px] font-medium text-[var(--fg)] block">Physically Occupied</span>
          <span className="text-lg font-bold text-[var(--fg)]">{summaryValue(totals.totalOccupied)}</span>
          <span className="text-[10px] text-[var(--muted)] block">Vehicle present in slot</span>
        </div>

        <div className="rounded-xl border border-blue-500/20 bg-blue-500/5 p-3.5 space-y-1">
          <span className="text-[11px] font-medium text-blue-600 dark:text-blue-400 block">Reserved / Protected</span>
          <span className="text-lg font-bold text-blue-600 dark:text-blue-400">{summaryValue(totals.totalProtected)}</span>
          <span className="text-[10px] text-blue-600/70 dark:text-blue-400/70 block">Guaranteed bookings</span>
        </div>

        <div className="rounded-xl border border-amber-500/20 bg-amber-500/5 p-3.5 space-y-1">
          <span className="text-[11px] font-medium text-amber-600 dark:text-amber-400 block">Pending Payment</span>
          <span className="text-lg font-bold text-amber-600 dark:text-amber-400">{summaryValue(totals.totalPendingPayment)}</span>
          <span className="text-[10px] text-amber-600/70 dark:text-amber-400/70 block">Awaiting checkout</span>
        </div>

        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-3.5 space-y-1">
          <span className="text-[11px] font-medium text-[var(--muted)] block">Backup Buffer</span>
          <span className="text-lg font-bold text-[var(--fg)]">{summaryValue(totals.totalBackup)}</span>
          <span className="text-[10px] text-[var(--muted)] block">Overflow guarantee</span>
        </div>
      </div>

      {/* Controls & Search Filter Bar */}
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-[var(--border)] bg-[var(--card)] p-3">
        <div className="sp-search-field flex w-full min-w-0 basis-full items-center gap-2 sm:w-auto sm:min-w-[220px] sm:basis-auto sm:flex-1 sm:max-w-sm">
          <UntitledIcon name="search" size={14} className="text-[var(--muted)] shrink-0" />
          <input
            className="w-full bg-transparent text-xs text-[var(--fg)] placeholder:text-[var(--muted)] outline-none"
            type="search"
            aria-label="Search parking lots by name, code, or address"
            autoComplete="off"
            placeholder="Search lot by name, code, or address…"
            value={searchTerm}
            onChange={(e) => { setSearchTerm(e.target.value); setPage(1) }}
          />
          {searchTerm && (
            <button
              type="button"
              aria-label="Clear parking lot search"
              className="text-[var(--muted)] hover:text-[var(--fg)]"
              onClick={() => { setSearchTerm(""); setPage(1) }}
            >
              <UntitledIcon name="x" size={12} />
            </button>
          )}
        </div>

        <div className="flex flex-wrap items-center gap-3 text-xs">
          <div className="flex items-center gap-1.5">
            <span className="text-[var(--muted)]">Status:</span>
            <RoundedSelect
              className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-2.5 py-1 text-xs text-[var(--fg)] outline-none"
              value={statusFilter}
              onChange={(e) => { setStatusFilter(e.target.value as "all" | "active" | "inactive"); setPage(1) }}
            >
              <option value="all">All Statuses</option>
              <option value="active">Active Only</option>
              <option value="inactive">Inactive Only</option>
            </RoundedSelect>
          </div>

          <div className="flex items-center gap-1.5">
            <span className="text-[var(--muted)]">Sort:</span>
            <RoundedSelect
              className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-2.5 py-1 text-xs text-[var(--fg)] outline-none"
              value={sortBy}
              onChange={(e) => { setSortBy(e.target.value as "name" | "capacity" | "occupancy"); setPage(1) }}
            >
              <option value="name">Lot Name</option>
              <option value="capacity">Total Capacity</option>
              <option value="occupancy">Occupancy Rate</option>
            </RoundedSelect>
          </div>
        </div>
      </div>

      {/* Main Structured Lots Comparison Table */}
      {loading ? (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-8 text-center text-xs text-[var(--muted)]">
          Aggregating live lot capacities and occupancy metrics…
        </div>
      ) : filteredData.length > 0 ? (
        <div className="overflow-x-auto rounded-xl border border-[var(--border)] bg-[var(--card)]">
          <table className="sp-adaptive-table owner-lots-comparison-table w-full table-fixed text-left text-xs tabular-nums" style={{ minWidth: tableWidth }}>
            <colgroup>
              <col style={{ width: 240 }} />
              {visibleColumns.map(column => <col key={column.id} style={{ width: column.width }} />)}
              <col style={{ width: 80 }} />
            </colgroup>
            <thead className="border-b border-[var(--border)] bg-[var(--bg)] text-[11px] uppercase tracking-wider text-[var(--muted)]">
              <tr>
                <th scope="col" className="px-4 py-3 font-semibold">Parking Facility</th>
                {visibleColumns.map(column => (
                  <th key={column.id} scope="col" className={`whitespace-nowrap px-3 py-3 font-semibold ${column.id === "setup" ? "text-left" : column.id === "status" ? "text-center" : "text-right"}`}>
                    {column.label}
                  </th>
                ))}
                <th scope="col" className="sticky right-0 z-10 bg-[var(--bg)] px-3 py-3 text-center font-semibold">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[var(--border)]">
              {rows.map(({ site, setup, metrics, structure, structureError }) => {
                const totalCap = metrics?.total || 1
                const occupiedPercent = metrics ? Math.min(
                  100,
                  Math.round(((metrics.occupied + metrics.protected) / totalCap) * 100),
                ) : null

                const cells: Record<LotColumnId, ReactNode> = {
                  setup: <td data-label="Setup" className="px-4 py-3.5 text-xs text-[var(--fg)] break-words">
                    {setup ? <><span className="block font-semibold capitalize">{setup.type.replace("-", " ")}</span><span className="block text-[var(--muted)]">{setup.modelFileName ?? "Model not recorded"}</span><span className="block tabular-nums">{setup.hourlyRate.toLocaleString("en-US")} VND/hour</span></> : <span className="text-[var(--muted)]">Setup details unavailable in this browser</span>}
                  </td>,
                  status: <td data-label="Status" className="px-3 py-3.5 text-center">
                    <span
                      className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[10px] font-semibold border uppercase ${
                        site.isActive
                          ? "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/20"
                          : "bg-red-500/10 text-red-600 dark:text-red-400 border-red-500/20"
                      }`}
                    >
                      {site.isActive ? "ACTIVE" : "INACTIVE"}
                    </span>
                  </td>,
                  capacity: <td data-label="Capacity" className="px-3 py-3.5 text-right font-medium text-[var(--fg)]">
                    {metrics?.total ?? "—"}
                  </td>,
                  available: <td data-label="Available / capacity" className="px-3 py-3.5 text-right font-bold tabular-nums whitespace-nowrap text-emerald-600 dark:text-emerald-400">
                    {metrics ? `${metrics.available.toLocaleString()}/${metrics.total.toLocaleString()}` : "—"}
                  </td>,
                  occupied: <td data-label="Occupied" className="px-3 py-3.5 text-right font-medium text-[var(--fg)]">
                    {metrics?.occupied ?? "—"}
                  </td>,
                  reserved: <td data-label="Reserved" className="px-3 py-3.5 text-right font-medium text-blue-600 dark:text-blue-400">
                    {metrics?.protected ?? "—"}
                  </td>,
                  pending: <td data-label="Pending pay" className="px-3 py-3.5 text-right font-medium tabular-nums whitespace-nowrap text-amber-600 dark:text-amber-400">
                    {metrics?.pendingPayment ?? "—"}
                  </td>,
                  backup: <td data-label="Backup" className="px-3 py-3.5 text-right font-medium text-[var(--fg)]">
                    {metrics?.backup ?? "—"}
                  </td>,
                  occupancy: <td data-label="Occupancy" className="px-4 py-3.5 text-right">
                    <div className="flex items-center justify-end gap-2">
                      {occupiedPercent !== null && <div className="w-14 h-1.5 rounded-full bg-[var(--border)] overflow-hidden">
                        <div
                          className={`h-full rounded-full ${
                            occupiedPercent > 85
                              ? "bg-red-500"
                              : occupiedPercent > 60
                                ? "bg-amber-500"
                                : "bg-emerald-500"
                          }`}
                          style={{ width: `${occupiedPercent}%` }}
                        />
                      </div>}
                      <span className="tabular-nums text-[11px] font-semibold text-[var(--fg)]">
                        {occupiedPercent === null ? "—" : `${occupiedPercent}%`}
                      </span>
                    </div>
                  </td>,
                }

                return (
                  <tr key={site.id} className="hover:bg-[var(--bg)]/50 transition-colors">
                    {/* Facility Identification */}
                    <td data-label="Parking facility" className="px-4 py-3.5">
                      <div className="font-semibold text-sm text-[var(--fg)] break-words">
                        {site.name}
                      </div>
                      <div className="mt-1 text-xs leading-relaxed text-[var(--muted)] break-words">
                        {site.address}
                      </div>
                      {structureError && <div className="mt-1 text-[11px] text-amber-600 dark:text-amber-400" title={structureError}>Capacity unavailable · Refresh Data to retry</div>}
                    </td>

                    {visibleColumns.map(column => <Fragment key={column.id}>{cells[column.id]}</Fragment>)}

                    <td data-label="Actions" className="sticky right-0 z-10 bg-[var(--card)] px-3 py-2.5 text-center align-middle!">
                      <LotActionMenu name={site.name}
                        actions={[
                          { label: "Breakdown", icon: "chart", run: () => setDetailSite({ site, setup, metrics, structure, structureError }) },
                          { label: "Structure", icon: "grid", run: () => handleNavigateToStructure(site.id) },
                          { label: "Edit", icon: "clipboard", run: () => setEditSite(site) },
                        ]}
                        onMore={() => setDraft(settings)} />
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
          {pageCount > 1 && (
            <nav aria-label="Parking lots pagination" className="sticky left-0 flex w-full flex-wrap justify-center gap-2 border-t border-[var(--border)] p-3">
              {settings.pagination === "numbered"
                ? Array.from({ length: pageCount }, (_, index) => index + 1).map(number => (
                  <button key={number} type="button" aria-label={`Page ${number}`} aria-current={number === currentPage ? "page" : undefined}
                    className={number === currentPage ? "btn-primary" : "btn-outline"} onClick={() => setPage(number)}>{number}</button>
                ))
                : currentPage < pageCount && <button type="button" className="btn-outline" onClick={() => setPage(currentPage + 1)}>Load More</button>}
            </nav>
          )}
        </div>
      ) : error ? null : (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-8 text-center text-xs text-[var(--muted)]">
          {searchTerm || statusFilter !== "all"
            ? "No parking facilities found matching current filters."
            : "No parking facilities registered. Complete onboarding or add a site in Site Management."}
        </div>
      )}

      {draft && <Modal title="Table Settings" className="max-w-[710px]!" onClose={() => { clearDrag(); setDraft(null) }}>
        <div className="grid gap-4 text-sm">
          <section aria-labelledby="lot-settings-columns">
            <h3 id="lot-settings-columns" className="font-bold">Columns</h3>
            <p className="mt-1 text-[var(--muted)]">Drag to reorder. Add or remove columns to customize your view.</p>
            <ul className="mt-3 flex flex-wrap gap-2 rounded-lg border border-[var(--border)] bg-[var(--bg)] p-2.5">
              <li className="flex min-h-11 items-center gap-2 rounded-lg border border-[var(--border)] px-3 text-[var(--muted)]">
                <span aria-hidden="true">⠿</span> Parking Facility
              </li>
              {draftColumns.map((column, index) => (
                <li key={column.id} className={`flex min-h-11 items-center gap-1 rounded-lg border bg-[var(--card)] px-1 ${dropColumn === column.id ? "border-[var(--primary)]" : "border-[var(--border)]"}`}
                  onDragOver={event => {
                    if (!dragColumn || dragColumn === column.id) return
                    event.preventDefault()
                    event.dataTransfer.dropEffect = "move"
                    setDropColumn(column.id)
                  }}
                  onDrop={event => {
                    event.preventDefault()
                    if (dragColumn) moveColumn(dragColumn, column.id)
                    clearDrag()
                  }}>
                  <button type="button" draggable className="grid min-h-11 w-8 cursor-grab place-items-center rounded-md text-lg text-[var(--muted)] active:cursor-grabbing"
                    aria-label={`Reorder ${column.label} with arrow keys`}
                    onKeyDown={event => {
                      if (event.key !== "ArrowLeft" && event.key !== "ArrowRight" && event.key !== "ArrowUp" && event.key !== "ArrowDown") return
                      event.preventDefault()
                      const direction = event.key === "ArrowLeft" || event.key === "ArrowUp" ? -1 : 1
                      const target = draftColumns[index + direction]
                      if (target) moveColumn(column.id, target.id)
                    }}
                    onDragStart={event => {
                      event.dataTransfer.effectAllowed = "move"
                      event.dataTransfer.setData("text/plain", column.id)
                      setDragColumn(column.id)
                    }}
                    onDragEnd={clearDrag}><span aria-hidden="true">⠿</span></button>
                  <span className="font-semibold">{column.label}</span>
                  <button type="button" aria-label={`Remove ${column.label} column`}
                    className="grid min-h-11 w-9 place-items-center rounded-md hover:bg-[var(--raised)]"
                    onClick={() => setDraft({ ...draft, columns: draft.columns.filter(id => id !== column.id) })}>
                    <UntitledIcon name="x" size={16} />
                  </button>
                </li>
              ))}
              <li className="flex min-h-11 items-center gap-2 rounded-lg border border-[var(--border)] px-3 text-[var(--muted)]">
                <span aria-hidden="true">⠿</span> Actions
              </li>
            </ul>
          </section>
          <section aria-labelledby="lot-settings-add">
            <h3 id="lot-settings-add" className="font-bold">Add Columns</h3>
            <div className="mt-2 flex flex-wrap gap-2">
              {lotColumns.filter(column => !draft.columns.includes(column.id)).map(column => (
                <button key={column.id} type="button" className="flex items-center gap-1.5 rounded-lg bg-[var(--bg)] px-3 transition-colors hover:bg-[var(--raised)]"
                  onClick={() => setDraft({ ...draft, columns: [...draft.columns, column.id] })}>
                  <UntitledIcon name="plus" size={14} />{column.label}
                </button>
              ))}
              {draft.columns.length === lotColumns.length && <p className="text-[var(--muted)]">All columns are visible.</p>}
            </div>
          </section>
          <fieldset className="border-t border-[var(--border)] pt-4">
            <p className="font-bold" id="lot-settings-pagination">Pagination</p>
            <div role="radiogroup" aria-labelledby="lot-settings-pagination" className="mt-1 flex flex-wrap gap-x-6">
              {(["numbered", "load-more"] as const).map(mode => (
                <label key={mode} className="flex min-h-11 cursor-pointer items-center gap-2">
                  <input type="radio" name="lot-table-pagination" checked={draft.pagination === mode}
                    className="size-4 accent-[var(--fg)]" onChange={() => setDraft({ ...draft, pagination: mode })} />
                  {mode === "numbered" ? "Numbered pagination" : "Load More"}
                </label>
              ))}
            </div>
          </fieldset>
          <div className="flex flex-wrap items-center justify-between gap-2">
            <button type="button" className="rounded-lg px-2 font-semibold hover:bg-[var(--raised)]" onClick={() => setDraft(defaultSettings)}>Reset to default</button>
            <div className="ml-auto flex gap-2">
              <button type="button" className="rounded-lg px-3 text-[var(--muted)] hover:bg-[var(--raised)]" onClick={() => { clearDrag(); setDraft(null) }}>Cancel</button>
              <button type="button" className="inline-flex items-center gap-2 rounded-lg bg-[var(--fg)] px-4 font-semibold text-[var(--bg)] hover:opacity-90"
                onClick={() => { setSettings(draft); setPage(1); clearDrag(); setDraft(null) }}>
                <UntitledIcon name="check" size={16} /> Apply
              </button>
            </div>
          </div>
        </div>
      </Modal>}
      <LotDetailModal model={model} />
      <LotEditModal model={model} />
    </div>
  )
}
