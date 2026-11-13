import { useEffect, useRef, useState } from "react"
import { ParkingDateField } from "../../../components/forms/ParkingDateField"
import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { PageHeader } from "../../../components/ui/PageHeader"
import { useApp } from "../../../context/AppContext"
import { paymentApi, type DriverInvoice, type DriverInvoicePage } from "../../../lib/paymentApi"
import { formatStatus } from "../../../lib/formatStatus"
import { PaymentCheckout } from "./PaymentCheckout"

export function PaymentHistory() {
  const { user } = useApp()
  return user && <InvoiceHistory key={user.id} />
}

const issuedDate = new Intl.DateTimeFormat("en-VN", { dateStyle: "medium", timeZone: "Asia/Ho_Chi_Minh" })

function InvoiceHistory() {
  const [filters, setFilters] = useState({ status: "", fromDate: "", toDate: "", pageNumber: 1 })
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

  return (
    <section className="space-y-5 text-[var(--fg)]">
      <PageHeader eyebrow="Driver Account · Billing & Payments" title="Payment History" variant="compact" description="Review your invoices and download PDFs for settled payments." />
      <section className="card space-y-5" aria-labelledby="invoice-history-title">
        <h2 id="invoice-history-title" className="text-lg font-bold">Your invoices</h2>
        <div className="grid items-start gap-4 sm:grid-cols-3">
          <label className="grid gap-2 text-sm font-semibold">Payment status
            <RoundedSelect aria-label="Payment status" value={filters.status} onChange={event => filter("status", event.target.value)}>
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
        {(filters.status || filters.fromDate || filters.toDate) && <button type="button" className="btn-outline" onClick={() => setFilters({ status: "", fromDate: "", toDate: "", pageNumber: 1 })}>Clear filters</button>}
        {error ? <div className="space-y-3">
          <p role="alert">{error}</p>
          <button type="button" className="btn-outline" onClick={() => setRetry(value => value + 1)}>Retry</button>
        </div> : !result ? <p role="status">Loading invoices…</p> : result.items.length === 0 ? <p role="status">No invoices match these filters.</p> : <>
          <ul className="divide-y divide-[var(--border)]">
            {result.items.map(invoice => {
              const { invoiceIdentity, financialStatus, financialSummary, parkingSessionReference } = invoice
              return <li key={invoiceIdentity.invoiceId} className="grid min-w-0 gap-3 py-4 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center">
                <div className="min-w-0 space-y-1 break-words">
                  <p className="font-semibold">{parkingSessionReference.siteName}</p>
                  <p className="text-sm text-[var(--muted)]">{invoiceIdentity.invoiceNumber} · {parkingSessionReference.plateNumber} · {issuedDate.format(new Date(invoiceIdentity.issuedAt))}</p>
                  <p className="text-sm tabular-nums">{formatStatus(financialStatus.status)} · {financialSummary.finalTotalAmount.toLocaleString("en-VN")} {financialSummary.currency}
                    {financialSummary.surplusRefund > 0 && ` · Refund: ${financialSummary.surplusRefund.toLocaleString("en-VN")} ${financialSummary.currency}`}
                  </p>
                </div>
                {financialStatus.status === "PAID" && financialStatus.isSettled && <button type="button" className="btn-outline justify-self-start" disabled={downloading !== null} onClick={() => void savePdf(invoice)}>
                  {downloading === invoiceIdentity.invoiceId ? "Downloading…" : "Download PDF"}
                </button>}
              </li>
            })}
          </ul>
          {result.totalPages > 1 && <nav className="flex flex-wrap items-center gap-3" aria-label="Invoice pages">
            <button type="button" className="btn-outline" disabled={result.pageNumber <= 1} onClick={() => setFilters(current => ({ ...current, pageNumber: result.pageNumber - 1 }))}>Previous</button>
            <span>Page {result.pageNumber} of {result.totalPages}</span>
            <button type="button" className="btn-outline" disabled={result.pageNumber >= result.totalPages} onClick={() => setFilters(current => ({ ...current, pageNumber: result.pageNumber + 1 }))}>Next</button>
          </nav>}
        </>}
        {downloadError && <p role="alert">{downloadError}</p>}
      </section>
      <PaymentCheckout />
    </section>
  )
}
