import { RoundedSelect } from "../ui/RoundedSelect"
import { useEffect, useMemo, useRef, useState } from "react"
import { createPortal } from "react-dom"
import { UntitledIcon } from "../icon/UntitledIcon"
import "./ParkingHistory.css"

export interface ParkingHistoryRecord {
  id: string
  plate: string
  title: string
  subtitle?: string
  slot: string
  vehicleType?: string
  status: string
  kind?: string
  startedAt: string
  endedAt?: string | null
  details?: { label: string; value: string | null | undefined }[]
  evidence?: { label: string; image: string | null }[]
  showDuration?: boolean
}

interface ParkingHistoryProps {
  title: string
  scope: string
  scopeKey: string
  records: ParkingHistoryRecord[]
  dateLabel: string
  emptyMessage: string
  loading?: boolean
  error?: string
  onRetry?: () => void
  showVehicleFilter?: boolean
}

const dateTime = new Intl.DateTimeFormat("en-US", { dateStyle: "medium", timeStyle: "short" })
const pageSize = 8

function formatDate(value?: string | null) {
  if (!value) return "—"
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? "—" : dateTime.format(date)
}

function formatDuration(start: string, end?: string | null, now = Date.now()) {
  const startTime = new Date(start).getTime()
  const endTime = end ? new Date(end).getTime() : now
  if (!Number.isFinite(startTime) || !Number.isFinite(endTime)) return "—"
  const seconds = Math.max(0, Math.floor((endTime - startTime) / 1000))
  const days = Math.floor(seconds / 86400)
  const hours = Math.floor((seconds % 86400) / 3600)
  const minutes = Math.floor((seconds % 3600) / 60)
  const rest = seconds % 60
  const time = [hours, minutes, rest].map(value => String(value).padStart(2, "0")).join(":")
  return days ? `${days}d ${time}` : time
}

function statusLabel(status: string) {
  return status.replace(/[_-]/g, " ").toLowerCase().replace(/\b\w/g, letter => letter.toUpperCase())
}

const STATUS_TONE_MAP: Record<string, "completed" | "active" | "neutral" | "attention" | "pending"> = {
  COMPLETED: "completed",
  ACTIVE: "active",
  CANCELLED: "neutral",
  DISPUTED: "attention",
  OVERSTAYED: "attention",
}

function statusTone(status: string) {
  return STATUS_TONE_MAP[status] ?? "pending"
}

function normalizePlate(value: string) {
  return value.replace(/[^a-z0-9]/gi, "").toUpperCase()
}

export function ParkingHistory({ title, scope, scopeKey, records, dateLabel, emptyMessage, loading = false, error, onRetry, showVehicleFilter = false }: ParkingHistoryProps) {
  const [query, setQuery] = useState("")
  const [status, setStatus] = useState("all")
  const [vehicle, setVehicle] = useState("all")
  const [page, setPage] = useState(1)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [clock, setClock] = useState(() => Date.now())
  const closeRef = useRef<HTMLButtonElement>(null)
  const previousFocus = useRef<HTMLElement | null>(null)

  const statuses = useMemo(() => Array.from(new Set(records.map(record => record.status))), [records])
  const vehicles = useMemo(() => Array.from(new Set(records.map(record => record.vehicleType).filter((item): item is string => !!item))), [records])
  const filtered = useMemo(() => records.filter(record => {
    if (status !== "all" && record.status !== status) return false
    if (showVehicleFilter && vehicle !== "all" && record.vehicleType !== vehicle) return false
    const term = query.trim()
    if (!term) return true
    const normalized = normalizePlate(term)
    return (normalized && normalizePlate(record.plate).includes(normalized)) || record.title.toLowerCase().includes(term.toLowerCase())
  }), [records, status, vehicle, query, showVehicleFilter])
  const pageCount = Math.max(1, Math.ceil(filtered.length / pageSize))
  const visible = filtered.slice((Math.min(page, pageCount) - 1) * pageSize, Math.min(page, pageCount) * pageSize)
  const selected = records.find(record => record.id === selectedId) ?? null

  useEffect(() => {
    setQuery("")
    setStatus("all")
    setVehicle("all")
    setSelectedId(null)
  }, [scopeKey])
  useEffect(() => {
    if (selectedId && !records.some(record => record.id === selectedId)) setSelectedId(null)
  }, [records, selectedId])
  useEffect(() => setPage(1), [query, status, vehicle])
  useEffect(() => setPage(current => Math.min(current, pageCount)), [pageCount])
  useEffect(() => {
    if (!selectedId) return
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = "hidden"
    closeRef.current?.focus()
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") setSelectedId(null)
      if (event.key === "Tab") {
        event.preventDefault()
        closeRef.current?.focus()
      }
    }
    window.addEventListener("keydown", onKeyDown)
    return () => {
      document.body.style.overflow = previousOverflow
      window.removeEventListener("keydown", onKeyDown)
      previousFocus.current?.focus()
    }
  }, [selectedId])
  useEffect(() => {
    if (!selected?.showDuration || selected.endedAt) return
    const timer = window.setInterval(() => setClock(Date.now()), 1000)
    return () => window.clearInterval(timer)
  }, [selected?.id, selected?.showDuration, selected?.endedAt])

  function openDetails(id: string) {
    previousFocus.current = document.activeElement instanceof HTMLElement ? document.activeElement : null
    setClock(Date.now())
    setSelectedId(id)
  }

  return <section className="parking-history" aria-label={title}>
    <div className="parking-history-header">
      <div>
        <h2>{title}</h2>
        <p aria-live="polite">{scope} <span aria-hidden="true">·</span> {loading ? "Loading…" : `${filtered.length} ${filtered.length === 1 ? "record" : "records"}`}</p>
      </div>
      <span className="parking-history-total" aria-label={`${records.length} total records`}>{records.length} total</span>
    </div>

    <div className="parking-history-filters" aria-label="Filter parking history">
      <div className="parking-history-search sp-search-field">
        <UntitledIcon name="search" size={18} aria-hidden="true" />
        <label className="sr-only" htmlFor="parking-history-search">Search license plate or {dateLabel === "Entry" ? "account" : "parking site"}</label>
        <input id="parking-history-search" type="search" name="parking-history-search" autoComplete="off" placeholder={dateLabel === "Entry" ? "Search plate or account…" : "Search plate or parking site…"} value={query} onChange={event => setQuery(event.target.value)} />
      </div>
      <label className="parking-history-select"><span>Status</span><RoundedSelect value={status} onChange={event => setStatus(event.target.value)}><option value="all">All statuses</option>{statuses.map(item => <option key={item} value={item}>{statusLabel(item)}</option>)}</RoundedSelect></label>
      {showVehicleFilter && <label className="parking-history-select"><span>Vehicle</span><RoundedSelect value={vehicle} onChange={event => setVehicle(event.target.value)}><option value="all">All vehicles</option>{vehicles.map(item => <option key={item} value={item}>{statusLabel(item)}</option>)}</RoundedSelect></label>}
    </div>

    {error && <div role="alert" className="parking-history-message is-error"><UntitledIcon name="alert" size={19} /><span>{error}</span>{onRetry && <button type="button" onClick={onRetry}>Retry</button>}</div>}
    {loading ? <div role="status" className="parking-history-message"><UntitledIcon name="clock" size={19} /><span>Loading parking history…</span></div> : error ? null : visible.length === 0 ? <div role="status" className="parking-history-empty"><UntitledIcon name="parking" size={28} /><strong>{records.length ? "No matching records" : "No history yet"}</strong><p>{records.length ? "Try another plate, location, or filter." : emptyMessage}</p></div> : <>
      <div className="parking-history-table-wrap">
        <table className="parking-history-table">
          <thead><tr><th scope="col">Vehicle</th><th scope="col">{dateLabel === "Entry" ? "Account / visit" : "Parking site"}</th><th scope="col">Space</th><th scope="col">{dateLabel}</th><th scope="col">Status</th><th scope="col"><span className="sr-only">Details</span></th></tr></thead>
          <tbody>{visible.map(record => <tr key={record.id}>
            <td><strong className="parking-history-plate" translate="no">{record.plate || "—"}</strong><small>{record.vehicleType ? statusLabel(record.vehicleType) : record.kind || "Parking"}</small></td>
            <td><strong className="parking-history-place">{record.title}</strong>{record.subtitle && <small>{record.subtitle}</small>}</td>
            <td className="parking-history-number">{record.slot || "—"}</td>
            <td className="parking-history-number">{formatDate(record.startedAt)}</td>
            <td><Status status={record.status} /></td>
            <td><button type="button" className="parking-history-details" onClick={() => openDetails(record.id)} aria-label={`View details for ${record.plate || record.id}`}>Details <UntitledIcon name="arrow-right" size={15} /></button></td>
          </tr>)}</tbody>
        </table>
      </div>
      <div className="parking-history-cards">{visible.map(record => <article className="parking-history-card" key={record.id}>
        <div className="parking-history-card-top"><div><strong className="parking-history-plate" translate="no">{record.plate || "—"}</strong><span>{record.title}</span></div><Status status={record.status} /></div>
        <dl><div><dt>Space</dt><dd>{record.slot || "—"}</dd></div><div><dt>{dateLabel}</dt><dd>{formatDate(record.startedAt)}</dd></div></dl>
        <button type="button" className="parking-history-details" onClick={() => openDetails(record.id)}>View details <UntitledIcon name="arrow-right" size={16} /></button>
      </article>)}</div>
    </>}

    {!loading && pageCount > 1 && <div className="parking-history-pagination"><span>Showing {(Math.min(page, pageCount) - 1) * pageSize + 1}–{Math.min(Math.min(page, pageCount) * pageSize, filtered.length)} of {filtered.length}</span><div><button type="button" disabled={page <= 1} onClick={() => setPage(current => Math.max(1, current - 1))}>Previous</button><button type="button" disabled={page >= pageCount} onClick={() => setPage(current => Math.min(pageCount, current + 1))}>Next</button></div></div>}

    {selected && createPortal(<div className="parking-history-backdrop" onMouseDown={event => { if (event.target === event.currentTarget) setSelectedId(null) }}><div role="dialog" aria-modal="true" aria-labelledby="parking-history-detail-title" className="parking-history-dialog">
      <div className="parking-history-dialog-header"><div><p>Parking record</p><h3 id="parking-history-detail-title">{selected.plate || selected.id}</h3><span>{selected.title}</span></div><button ref={closeRef} type="button" className="parking-history-close" onClick={() => setSelectedId(null)} aria-label="Close parking details"><UntitledIcon name="x" size={19} /></button></div>
      <div className="parking-history-dialog-content"><Status status={selected.status} /><dl className="parking-history-detail-grid">
        <div><dt>{dateLabel}</dt><dd>{formatDate(selected.startedAt)}</dd></div><div><dt>{dateLabel === "Entry" ? "Exit" : "Scheduled end"}</dt><dd>{formatDate(selected.endedAt)}</dd></div>
        <div><dt>Space</dt><dd>{selected.slot || "—"}</dd></div><div><dt>Visit type</dt><dd>{selected.kind || "Reserved"}</dd></div>
        {selected.showDuration && <div><dt>Parking duration</dt><dd>{formatDuration(selected.startedAt, selected.endedAt, clock)}</dd></div>}
        {selected.details?.map(field => <div key={field.label}><dt>{field.label}</dt><dd>{field.value || "—"}</dd></div>)}
      </dl>
      {!!selected.evidence?.length && <div className="parking-history-evidence">{selected.evidence.map(item => <div key={item.label}><h4>{item.label}</h4>{item.image ? <img src={item.image} alt={item.label} width={320} height={150} loading="lazy" /> : <p>No image saved for this session.</p>}</div>)}</div>}
      </div>
    </div></div>, document.body)}
  </section>
}

function Status({ status }: { status: string }) {
  const TONE_ICON_MAP: Record<string, "check-circle" | "car" | "alert" | "x" | "clock"> = {
    completed: "check-circle",
    active: "car",
    attention: "alert",
    neutral: "x",
    pending: "clock",
  }
  const tone = statusTone(status)
  const icon = TONE_ICON_MAP[tone] ?? "clock"
  return <span className={`parking-history-status is-${tone}`}><UntitledIcon name={icon} size={14} />{statusLabel(status)}</span>
}
