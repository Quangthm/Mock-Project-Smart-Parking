import { useEffect, useRef, useState } from "react"
import { ParkingDateField } from "../../../components/forms/ParkingDateField"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { PageHeader } from "../../../components/ui/PageHeader"
import { useApp } from "../../../context/AppContext"
import { paymentApi, type DriverInvoice, type DriverInvoicePage } from "../../../lib/paymentApi"
import { formatStatus } from "../../../lib/formatStatus"

export function PaymentHistory() {
  const { user } = useApp()
  return user && <InvoiceHistory key={user.id} />
}

const issuedDate = new Intl.DateTimeFormat("en-VN", { dateStyle: "medium", timeZone: "Asia/Ho_Chi_Minh" })
const invoiceColumns = "lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)_minmax(0,0.85fr)_minmax(0,1fr)_8.5rem]"
const emptyFilters = { status: "", fromDate: "", toDate: "", pageNumber: 1 }

function InvoiceHistory() {
  const [filters, setFilters] = useState(emptyFilters)
  const [result, setResult] = useState<DriverInvoicePage | null>(null)
  const [error, setError] = useState("")
  const [retry, setRetry] = useState(0)
  const [downloadError, setDownloadError] = useState("")
  const [downloading, setDownloading] = useState<string | null>(null)
  const download = useRef<AbortController | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    setResult(null)
    setError("")
    paymentApi.invoiceHistory(filters, controller.signal).then(page => {
      if (!controller.signal.aborted) setResult(page)
    }).catch(cause => {
      if (!controller.signal.aborted) setError(cause instanceof Error ? cause.message : "Could not load invoices.")
    })
    return () => controller.abort()
  }, [filters, retry])
  useEffect(() => () => download.current?.abort(), [])

  function filter(field: "status" | "fromDate" | "toDate", value: string) {
    setFilters(current => ({ ...current, [field]: value, pageNumber: 1 }))
  }

  async function savePdf(invoice: DriverInvoice) {
    if (download.current) return
    const controller = new AbortController()
    download.current = controller
    const { invoiceId, invoiceNumber } = invoice.invoiceIdentity
    setDownloading(invoiceId)
    setDownloadError("")
    try {
      const blob = await paymentApi.downloadInvoice(invoiceId, controller.signal)
      if (controller.signal.aborted) return
      const url = URL.createObjectURL(blob)
      const link = document.createElement("a")
      link.href = url
      link.download = `HD_${invoiceNumber.replace(/[^a-zA-Z0-9_-]/g, "_")}.pdf`
      link.click()
      window.setTimeout(() => URL.revokeObjectURL(url), 1000)
    } catch (cause) {
      if (!controller.signal.aborted) setDownloadError(cause instanceof Error ? cause.message : "Could not download the invoice.")
    } finally {
      download.current = null
      if (!controller.signal.aborted) setDownloading(null)
    }
  }

  const hasFilters = Boolean(filters.status || filters.fromDate || filters.toDate)

  return (
    <section className="space-y-5 text-[var(--fg)]">
      <PageHeader title="Payment History" variant="default" description="Find parking invoices and download receipts for settled payments." />
      <section className="min-w-0 rounded-2xl border border-[var(--border)] bg-[var(--card)]" aria-labelledby="invoice-history-title">
        <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-1 px-4 pt-5 sm:px-6">
          <h2 id="invoice-history-title" className="text-lg font-semibold">Invoices</h2>
          <div className="flex flex-wrap items-center gap-x-3">
            {result && <p className="text-sm tabular-nums text-[var(--muted)]">{result.totalCount} {result.totalCount === 1 ? "invoice" : "invoices"}</p>}
            {hasFilters && <button type="button" className="btn-ghost min-h-11" onClick={() => setFilters(emptyFilters)}>Clear filters</button>}
          </div>
        </div>
        <div className="grid items-start gap-3 border-b border-[var(--border)] px-4 py-4 sm:grid-cols-3 sm:px-6 [&_.booking-datetime-picker]:border-0! [&_.booking-datetime-field]:min-h-12! [&_.sp-rounded-select-trigger]:min-h-12! [&_.sp-rounded-select-trigger]:rounded-2xl!">
          <label className="grid min-w-0 gap-2 text-sm font-medium">Payment status
            <RoundedSelect className="w-full" aria-label="Payment status" value={filters.status} onChange={event => filter("status", event.target.value)}>
              <option value="">All statuses</option>
              <option value="PAID">Paid</option>
              <option value="PENDING">Pending</option>
              <option value="FAILED">Failed</option>
              <option value="CANCELLED">Cancelled</option>
              <option value="REFUNDED">Refunded</option>
            </RoundedSelect>
          </label>
          <ParkingDateField label="From date" value={filters.fromDate} max={filters.toDate || undefined} onChange={value => filter("fromDate", value)} />
          <ParkingDateField label="To date" value={filters.toDate} min={filters.fromDate || undefined} onChange={value => filter("toDate", value)} />
        </div>
        {error ? <div className="space-y-3 px-4 py-8 sm:px-6">
          <p role="alert">{error}</p>
          <button type="button" className="btn-outline" onClick={() => setRetry(value => value + 1)}>Retry</button>
        </div> : !result ? <p role="status" className="px-4 py-8 text-sm text-[var(--muted)] sm:px-6">Loading invoices…</p> : result.items.length === 0 ? <div className="px-4 py-10 text-center sm:px-6">
          <p role="status" className="font-semibold">{hasFilters ? "No invoices match these filters." : "No invoices yet."}</p>
        </div> : <>
          <div aria-hidden="true" className={`hidden gap-4 border-b border-[var(--border)] px-6 py-3 text-sm font-medium text-[var(--muted)] lg:grid ${invoiceColumns}`}>
            <span>Parking invoice</span><span>Issued</span><span>Status</span><span className="text-right">Amount</span><span className="text-right">Receipt</span>
          </div>
          <ul className="divide-y divide-[var(--border)]">
            {result.items.map(invoice => {
              const { invoiceIdentity, financialStatus, financialSummary, parkingSessionReference } = invoice
              const statusTone = financialStatus.status === "PAID" ? "text-[var(--badge-success-fg)]" :
                financialStatus.status === "PENDING" ? "text-[var(--badge-warning-fg)]" : "text-[var(--muted)]"
              return <li key={invoiceIdentity.invoiceId} className={`grid min-w-0 grid-cols-2 items-center gap-x-3 gap-y-2 px-4 py-4 sm:px-6 lg:gap-x-4 ${invoiceColumns}`}>
                <div className="col-span-2 min-w-0 space-y-1 wrap-anywhere lg:col-span-1">
                  <p className="font-semibold">{parkingSessionReference.siteName}</p>
                  <p className="text-sm text-[var(--muted)]">{invoiceIdentity.invoiceNumber} · {parkingSessionReference.plateNumber}</p>
                </div>
                <time className="text-sm tabular-nums text-[var(--muted)]" dateTime={invoiceIdentity.issuedAt}>{issuedDate.format(new Date(invoiceIdentity.issuedAt))}</time>
                <p className={`justify-self-end text-sm font-semibold lg:justify-self-start ${statusTone}`}>{formatStatus(financialStatus.status)}</p>
                <div className="min-w-0 wrap-anywhere tabular-nums lg:text-right">
                  <p className="font-semibold">{financialSummary.finalTotalAmount.toLocaleString("en-VN")} <span className="text-xs font-medium text-[var(--muted)]">{financialSummary.currency}</span></p>
                  {financialSummary.surplusRefund > 0 && <p className="text-xs text-[var(--muted)]">Refund {financialSummary.surplusRefund.toLocaleString("en-VN")} {financialSummary.currency}</p>}
                </div>
                <div className="min-w-0 justify-self-end">
                  {financialStatus.status === "PAID" && financialStatus.isSettled && <button type="button" className="btn-outline min-h-11 gap-2 px-3 text-sm" aria-label={`Download PDF for invoice ${invoiceIdentity.invoiceNumber}`} disabled={downloading !== null} onClick={() => void savePdf(invoice)}>
                    <UntitledIcon name="file" size={16} />
                    {downloading === invoiceIdentity.invoiceId ? "Downloading…" : "Download PDF"}
                  </button>}
                </div>
              </li>
            })}
          </ul>
          {result.totalPages > 1 && <nav className="flex flex-wrap items-center justify-center gap-2 border-t border-[var(--border)] px-4 py-3 text-sm sm:justify-end sm:px-6" aria-label="Invoice pages">
            <button type="button" className="btn-outline" disabled={result.pageNumber <= 1} onClick={() => setFilters(current => ({ ...current, pageNumber: result.pageNumber - 1 }))}>Previous</button>
            <span>Page {result.pageNumber} of {result.totalPages}</span>
            <button type="button" className="btn-outline" disabled={result.pageNumber >= result.totalPages} onClick={() => setFilters(current => ({ ...current, pageNumber: result.pageNumber + 1 }))}>Next</button>
          </nav>}
        </>}
        {downloadError && <p className="border-t border-[var(--border)] px-4 py-3 text-sm text-[var(--badge-danger-strong)] sm:px-6" role="alert">{downloadError}</p>}
      </section>
    </section>
  )
}
