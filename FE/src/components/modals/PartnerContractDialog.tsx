import { useEffect, useRef, useState } from "react"
import contract from "../../lib/partnerContract.json"
import type { ContractDraftRecord } from "../../lib/partnerContract"
import "./partner-contract.css"

interface Props {
  applicationId: string
  businessName: string
  lotName: string
  record: ContractDraftRecord | null
  signer: "owner" | "admin"
  signerName: string
  readOnly?: boolean
  onClose: () => void
  onSign: (name: string) => void
}

export function PartnerContractDialog({ applicationId, businessName, lotName, record, signer, signerName, readOnly = false, onClose, onSign }: Props) {
  const [readToEnd, setReadToEnd] = useState(false)
  const [confirmed, setConfirmed] = useState(false)
  const [name, setName] = useState(signerName)
  const [signError, setSignError] = useState("")
  const contentRef = useRef<HTMLDivElement>(null)
  const closeRef = useRef<HTMLButtonElement>(null)
  const dialogRef = useRef<HTMLElement>(null)
  const onCloseRef = useRef(onClose)
  onCloseRef.current = onClose
  const signed = signer === "owner" ? !!record?.ownerAcknowledgedAt : !!record?.adminAcknowledgedAt

  useEffect(() => {
    const previousFocus = document.activeElement as HTMLElement | null
    closeRef.current?.focus()
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") onCloseRef.current()
      if (event.key !== "Tab") return
      const focusable = [...(dialogRef.current?.querySelectorAll<HTMLElement>('a[href], button:not([disabled]), input:not([disabled])') ?? [])]
      if (!focusable.length) return
      const first = focusable[0]
      const last = focusable[focusable.length - 1]
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus() }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus() }
    }
    document.addEventListener("keydown", onKeyDown)
    const scrollArea = contentRef.current
    if (scrollArea && scrollArea.scrollHeight <= scrollArea.clientHeight + 8) setReadToEnd(true)
    return () => { document.removeEventListener("keydown", onKeyDown); previousFocus?.focus() }
  }, [])

  return <div className="partner-contract-backdrop" onMouseDown={event => { if (event.target === event.currentTarget) onClose() }}>
    <section ref={dialogRef} className="partner-contract-dialog partner-contract-dialog--agreement" role="dialog" aria-modal="true" aria-labelledby="partner-contract-title">
      <header className="partner-contract-header">
        <div>
          <p className="partner-contract-eyebrow">SmartParking partnership · Local draft</p>
          <h2 id="partner-contract-title">Partnership agreement</h2>
        </div>
        <button ref={closeRef} type="button" className="partner-contract-close" onClick={onClose} aria-label="Close agreement">×</button>
      </header>
      <div className="partner-contract-meta">
        <span><strong>Owner</strong> {businessName}</span>
        <span><strong>Parking lot</strong> {lotName}</span>
        <span><strong>Application</strong> {applicationId.slice(0, 8)}</span>
      </div>
      <div className="partner-contract-content" ref={contentRef} onScroll={event => {
        const node = event.currentTarget
        if (node.scrollTop + node.clientHeight >= node.scrollHeight - 12) setReadToEnd(true)
      }}>
        <h3>{contract.title}</h3>
        <p>{contract.intro}</p>
        {contract.sections.map(section => <section key={section.heading}>
          <h4>{section.heading}</h4>
          {section.paragraphs.map(paragraph => <p key={paragraph}>{paragraph}</p>)}
        </section>)}
        <div className="partner-contract-signatures">
          <div><span>Owner draft signature</span><strong>{record?.ownerAcknowledgedAt ? record.ownerName : "Awaiting Owner"}</strong>{record?.ownerAcknowledgedAt && <small>{new Date(record.ownerAcknowledgedAt).toLocaleString("en-GB")}</small>}</div>
          <div><span>Admin draft signature</span><strong>{record?.adminAcknowledgedAt ? record.adminName : "Awaiting Admin"}</strong>{record?.adminAcknowledgedAt && <small>{new Date(record.adminAcknowledgedAt).toLocaleString("en-GB")}</small>}{record?.adminAcknowledgedAt && <b className="partner-contract-stamp">SMARTPARKING<br />REVIEWED</b>}</div>
        </div>
      </div>
      <footer className="partner-contract-footer">
        <p>This browser-only draft is for review. It is not a verified electronic signature or a completed legal contract.</p>
        {!readOnly && !signed && <label className="partner-contract-name">Name shown on the draft signature<input className="input" value={name} maxLength={120} onChange={event => setName(event.target.value)} placeholder="Full name" /></label>}
        <div className="partner-contract-actions">
          <a className="btn-outline" href="/templates/smartparking-partnership-agreement-draft.docx" download>Download Word draft</a>
          {!readOnly && !signed && <label><input type="checkbox" checked={confirmed} onChange={event => setConfirmed(event.target.checked)} /> I have read this draft</label>}
          {!readOnly && <button type="button" className="btn-primary" disabled={signed || !readToEnd || !confirmed || !name.trim() || (signer === "admin" && !record?.ownerAcknowledgedAt)} onClick={() => {
            try { onSign(name.trim()) } catch { setSignError("Could not save this acknowledgement in your browser. Check browser storage and try again.") }
          }}>
            {signed ? "Signed locally" : `Sign draft as ${signer === "owner" ? "Owner" : "Admin"}`}
          </button>}
          {readOnly && <button type="button" className="btn-primary" onClick={onClose}>Close</button>}
        </div>
        {!readOnly && !readToEnd && !signed && <span className="partner-contract-hint">Scroll to the end of the agreement to continue.</span>}
        {signError && <span role="alert" className="partner-contract-hint">{signError}</span>}
        {!readOnly && signer === "admin" && !record?.ownerAcknowledgedAt && <span className="partner-contract-hint">Owner acknowledgement is required first.</span>}
      </footer>
    </section>
  </div>
}
