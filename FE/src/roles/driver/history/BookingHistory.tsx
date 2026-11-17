import { useState } from "react"
import { useApp } from "../../../context/AppContext"
import { getSessionGeneration } from "../../../lib/authApi"
import type { OwnReservation } from "../../../lib/reservationsApi"
import { formatSlotLabel } from "../../../lib/operationalLayout"
import { ParkingHistory, type ParkingHistoryRecord } from "../../../components/history/ParkingHistory"
import { PageHeader } from "../../../components/ui/PageHeader"
import { Modal } from "../../../components/ui/Modal"
import { Badge, type BadgeTone } from "../../../components/ui/Badge"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { useReservationHistory, type HistorySite } from "./useReservationHistory"
import "../../../booking-support.css"
import "./reservation-history.css"

const dateTime = new Intl.DateTimeFormat("en-US", { dateStyle: "medium", timeStyle: "short" })
const labels = (value?: string | null) => value ? value.replace(/[_-]/g, " ").toLowerCase().replace(/\b\w/g, letter => letter.toUpperCase()) : "Unknown"
const date = (value?: string | null) => value && Number.isFinite(Date.parse(value)) ? dateTime.format(new Date(value)) : "—"
export function historyMoney(amount: number | null | undefined, currency: string | null) {
  if (amount == null || !Number.isFinite(amount)) return "Unavailable"
  return `${new Intl.NumberFormat("en-US", { maximumFractionDigits: 2 }).format(amount)} ${currency || "(currency unavailable)"}`
}
const siteName = (item: OwnReservation, site?: HistorySite | null) => item.siteName || site?.name || `Parking site · ${item.siteId.slice(0, 8)}`
const slotName = (id: string | null | undefined, site?: HistorySite | null) => id ? formatSlotLabel(site?.slots[id] || id).replace(/_/g, " ") : "Not allocated"
function tone(state: string): BadgeTone {
  if (state === "COMPLETED") return "success"
  if (["NO_SHOW", "UNFULFILLABLE"].includes(state)) return "danger"
  if (["CANCELLED", "EXPIRED"].includes(state)) return "muted"
  return state === "PENDING_PAYMENT" ? "warning" : "primary"
}
function Fields({ values }: { values: [string, string][] }) {
  return <dl className="reservation-history-fields">{values.map(([label, value]) => <div key={label}><dt>{label}</dt><dd translate="no">{value}</dd></div>)}</dl>
}
export function ReservationScan({ label, src }: { label: string; src?: string | null }) {
  const [failed, setFailed] = useState(false)
  return <figure className="reservation-history-scan"><figcaption>{label}</figcaption>
    {src && !failed ? <a href={src} target="_blank" rel="noopener noreferrer" aria-label={`Open ${label.toLowerCase()} image in a new tab`}>
      <img src={src} alt={`${label} vehicle scan`} width={640} height={400} loading="lazy" onError={() => setFailed(true)} />
      <span>View full image<UntitledIcon name="arrow-right" size={15} /></span>
    </a> : <p role="status">{failed ? "The scan image could not be loaded." : "No scan image was provided by the reservation service."}</p>}
  </figure>
}

export function BookingHistory() {
  const { user } = useApp()
  const scope = user?.role === "driver" ? `${user.id}:${getSessionGeneration()}` : ""
  const history = useReservationHistory(scope)
  const records: ParkingHistoryRecord[] = history.items.map(item => ({
    id: item.reservationId, plate: item.licensePlate || "", title: siteName(item, history.sites[item.siteId]),
    subtitle: item.siteAddress || undefined, slot: slotName(item.allocatedSlotId, history.sites[item.siteId]),
    status: item.state, kind: "Reservation", startedAt: item.start, endedAt: item.end,
  }))
  const detail = history.detail.data
  const detailSite = detail ? history.sites[detail.siteId] : undefined
  if (!scope) return <p role="status">Sign in as a Driver to view your reservation history.</p>
  return <div className="driver-history-list reservation-history">
    <PageHeader title="Booking History" variant="compact" description="Your reservations and their recorded outcomes." actions={
      <button type="button" className="btn-outline" disabled={history.loading} onClick={history.refresh}>
        <UntitledIcon name="activity" size={17} /> Refresh history
      </button>
    } />
    <p className="reservation-history-hint">Newest reservations first. Search and status filters apply to the current page.</p>
    {Object.values(history.sites).some(site => site === null) && <p className="reservation-history-hint" role="status">Some parking-site details are unavailable. Your reservation records are still shown.</p>}
    <ParkingHistory title="Reservation history" scope="Your account only" scopeKey={`${scope}:${history.pageNumber}`}
      records={records} dateLabel="Booked for" emptyMessage="Your reservations will appear here after you make a booking."
      loading={history.loading} error={history.error} onRetry={history.refresh} onViewDetails={history.openDetails}
      pagination={{ page: history.pageNumber, hasNext: history.hasNext, onPrevious: history.previous, onNext: history.next }} />
    {history.selectedId && <Modal title="Reservation details" className="reservation-history-modal" onClose={history.closeDetails}>
      {history.detail.loading ? <p role="status" className="reservation-history-loading">Loading reservation details…</p>
        : history.detail.error ? <div role="alert" className="reservation-history-error"><p>{history.detail.error}</p><button type="button" className="btn-outline" onClick={history.retryDetails}>Retry details</button></div>
        : detail && <div className="reservation-history-detail">
          <header className="reservation-history-detail-header"><div><p>Reservation</p><h3>{siteName(detail, detailSite)}</h3><span>{detail.siteAddress || "Address unavailable"}</span></div><Badge tone={tone(detail.state)}>{labels(detail.state)}</Badge></header>
          <section aria-labelledby="reservation-history-booking"><h3 id="reservation-history-booking">Booking information</h3><Fields values={[
            ["License plate", detail.licensePlate || "Unavailable"], ["Reservation ID", detail.reservationId],
            ["Scheduled start", date(detail.start)], ["Scheduled end", date(detail.end)],
            ["Created", date(detail.createdAt)], ["Confirmed", date(detail.confirmedAt)],
          ]} /></section>
          <section aria-labelledby="reservation-history-allocation"><h3 id="reservation-history-allocation">Parking & allocation</h3><Fields values={[
            ["Site ID", detail.siteId], ["Requested slot ID", detail.requestedSlotId || "None"],
            ["Allocated space", slotName(detail.allocatedSlotId, detailSite)], ["Allocated slot ID", detail.allocatedSlotId || "None"],
            ["Allocation status", labels(detail.allocationStatus)], ["Parking session ID", detail.parkingSessionId || "No linked visit"],
          ]} /></section>
          {(detail.parkingSessionId || detail.entryImageUrl || detail.exitImageUrl) && <section aria-labelledby="reservation-history-scans">
            <h3 id="reservation-history-scans">Parking scans</h3>
            <div className="reservation-history-scans">
              <ReservationScan key={`entry:${detail.reservationId}:${detail.entryImageUrl}`} label="Check-in" src={detail.entryImageUrl} />
              <ReservationScan key={`exit:${detail.reservationId}:${detail.exitImageUrl}`} label="Check-out" src={detail.exitImageUrl} />
            </div>
          </section>}
          <section aria-labelledby="reservation-history-payment"><h3 id="reservation-history-payment">Price & payment</h3><Fields values={[
            ["Booked amount", historyMoney(detail.bookedAmount, detail.currency)], ["Deposit", historyMoney(detail.depositAmount, detail.currency)],
            ["Payment status", labels(detail.paymentStatus)], ["Payment order ID", detail.orderId || "None"],
            ["Order provisioning", labels(detail.orderProvisioningState)],
          ]} /><p className="reservation-history-hint">Reservation status and payment status are separate. Unknown payment status does not confirm or deny a payment.</p></section>
        </div>}
    </Modal>}
  </div>
}
