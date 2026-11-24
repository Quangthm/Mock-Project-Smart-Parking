import { useEffect, useRef, useState } from "react"
import { Modal } from "../../../components/ui/Modal"
import { paymentApi } from "../../../lib/paymentApi"
import { getSessionGeneration } from "../../../lib/authApi"

export function ReservationInvoice({ sessionId, orderId }: { sessionId?: string | null; orderId?: string | null }) {
  const [busy, setBusy] = useState(false)
  const [open, setOpen] = useState(false)
  const [error, setError] = useState("")
  const [pdfUrl, setPdfUrl] = useState("")
  const request = useRef<AbortController | null>(null)

  useEffect(() => () => request.current?.abort(), [])
  useEffect(() => () => { if (pdfUrl) URL.revokeObjectURL(pdfUrl) }, [pdfUrl])

  async function viewInvoice() {
    if (request.current) return
    const controller = new AbortController()
    const generation = getSessionGeneration()
    request.current = controller
    setOpen(true)
    setBusy(true)
    setError("")
    try {
      const pdf = await paymentApi.reservationInvoicePdf({ sessionId, orderId }, controller.signal)
      if (!controller.signal.aborted && generation === getSessionGeneration()) setPdfUrl(URL.createObjectURL(pdf))
    } catch (cause) {
      if (!controller.signal.aborted && generation === getSessionGeneration()) {
        setError(cause instanceof Error ? cause.message : "Could not load the invoice. Try again.")
      }
    } finally {
      if (request.current === controller) {
        request.current = null
        setBusy(false)
      }
    }
  }

  function closeInvoice() {
    request.current?.abort()
    request.current = null
    setBusy(false)
    setOpen(false)
    setPdfUrl("")
  }

  return <>
    <button type="button" className="parking-history-details" aria-haspopup="dialog" onClick={() => void viewInvoice()}>
      View Invoice
    </button>
    {open && <Modal title="Reservation invoice" className="reservation-history-modal" onClose={closeInvoice}>
      {busy && <p role="status">Loading invoice…</p>}
      {error && <div className="reservation-history-error">
        <p role="alert">{error}</p>
        <button type="button" className="btn-outline" onClick={() => void viewInvoice()}>Try again</button>
      </div>}
      {pdfUrl && <>
        <iframe className="reservation-invoice-preview" title="Reservation invoice PDF" src={pdfUrl} />
        <a className="btn-outline" href={pdfUrl} download="SmartParking-invoice.pdf">Download PDF</a>
      </>}
    </Modal>}
  </>
}
