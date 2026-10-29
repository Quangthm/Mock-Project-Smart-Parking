import { useEffect, useRef, useState } from "react"
import { createPortal } from "react-dom"
import type { ParkingLot } from "../../lib/types"
import { store } from "../../lib/store"
import { getOwnerPolicySectionDocuments, getOwnerPolicySectionTexts, ownerPolicySections, type OwnerPolicySectionId } from "../../lib/policySections"
import { TermsOfService } from "../../public pages/policies/TermsOfService"
import { PrivacyPolicy } from "../../public pages/policies/PrivacyPolicy"
import { PaymentRefund } from "../../public pages/policies/PaymentRefund"
import { UntitledIcon } from "../icon/UntitledIcon"

type OwnerSection = "general" | OwnerPolicySectionId
type PlatformSection = "terms" | "privacy" | "payment-refund"

export function LotPolicyModal({ lot, onClose, initialSource = "owner", initialPlatformSection = "terms" }: { lot: ParkingLot; onClose: () => void; initialSource?: "owner" | "platform"; initialPlatformSection?: PlatformSection }) {
  const [source, setSource] = useState<"owner" | "platform">(initialSource)
  const [ownerSection, setOwnerSection] = useState<OwnerSection>(() => {
    if (store.getLotPolicyPdf(lot.id, lot.ownerId)?.fileData) return "general"
    const texts = getOwnerPolicySectionTexts(lot.ownerId, lot.id)
    const documents = getOwnerPolicySectionDocuments(lot.ownerId, lot.id)
    return ownerPolicySections.find(section => texts[section.id]?.content || documents[section.id]?.fileData)?.id ?? "general"
  })
  const [platformSection, setPlatformSection] = useState<PlatformSection>(initialPlatformSection)
  const closeRef = useRef<HTMLButtonElement>(null)
  const dialogRef = useRef<HTMLDivElement>(null)
  const ownerDocuments = getOwnerPolicySectionDocuments(lot.ownerId, lot.id)
  const ownerTexts = getOwnerPolicySectionTexts(lot.ownerId, lot.id)
  const generalDocument = store.getLotPolicyPdf(lot.id, lot.ownerId)
  const selectedDocument = ownerSection === "general" ? generalDocument : ownerDocuments[ownerSection]
  const selectedText = ownerSection === "general" ? null : ownerTexts[ownerSection]
  const selectedTitle = ownerSection === "general" ? "General owner policy" : ownerPolicySections.find(section => section.id === ownerSection)?.title

  useEffect(() => {
    const previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = "hidden"
    closeRef.current?.focus()
    return () => { document.body.style.overflow = previousOverflow; previousFocus?.focus() }
  }, [])

  function handleKeyDown(event: React.KeyboardEvent<HTMLDivElement>) {
    if (event.key === "Escape") { onClose(); return }
    if (event.key !== "Tab" || !dialogRef.current) return
    const focusable = Array.from(dialogRef.current.querySelectorAll<HTMLElement>("button:not(:disabled), a[href], iframe"))
    if (!focusable.length) return
    if (event.shiftKey && document.activeElement === focusable[0]) { event.preventDefault(); focusable[focusable.length - 1].focus() }
    else if (!event.shiftKey && document.activeElement === focusable[focusable.length - 1]) { event.preventDefault(); focusable[0].focus() }
  }

  return createPortal(<div className="fixed inset-0 z-[999] flex items-center justify-center bg-black/70 p-3 sm:p-6" onMouseDown={event => { if (event.target === event.currentTarget) onClose() }}>
    <div ref={dialogRef} role="dialog" aria-modal="true" aria-labelledby="policy-modal-title" onKeyDown={handleKeyDown} className="flex max-h-[92dvh] w-full max-w-5xl min-w-0 flex-col overflow-hidden rounded-2xl border border-[var(--border)] bg-[var(--card)] text-[var(--fg)] shadow-2xl">
      <header className="flex items-start justify-between gap-4 border-b border-[var(--border)] p-5">
        <div className="min-w-0"><h2 id="policy-modal-title" className="text-xl font-semibold">Parking policies</h2><p className="mt-1 break-words text-sm text-[var(--muted)]">{lot.name} · Owner documents and SmartParking platform documents</p></div>
        <button ref={closeRef} type="button" className="icon-button shrink-0" aria-label="Close parking policies" onClick={onClose}><UntitledIcon name="x" size={20} /></button>
      </header>
      <div className="flex flex-wrap gap-2 border-b border-[var(--border)] px-5 py-3" role="group" aria-label="Policy source">
        <button type="button" className={source === "owner" ? "btn-primary" : "btn-outline"} aria-pressed={source === "owner"} onClick={() => setSource("owner")}>Owner Policies</button>
        <button type="button" className={source === "platform" ? "btn-primary" : "btn-outline"} aria-pressed={source === "platform"} onClick={() => setSource("platform")}>Platform Policies</button>
      </div>
      <div className="min-h-0 overflow-y-auto overscroll-contain p-5">
        {source === "owner" ? <div className="grid min-w-0 gap-5 md:grid-cols-[220px_minmax(0,1fr)]">
          <nav className="flex flex-wrap content-start gap-2 md:flex-col" aria-label="Owner policy sections">
            {([{ id: "general", title: "General policy" }, ...ownerPolicySections] as const).map(section => <button key={section.id} type="button" className={`min-h-11 rounded-xl border px-3 py-2 text-left text-sm font-semibold ${ownerSection === section.id ? "border-[var(--primary)] bg-[var(--active)] text-[var(--fg)]" : "border-[var(--border)] bg-[var(--bg)] text-[var(--muted)]"}`} aria-current={ownerSection === section.id ? "true" : undefined} onClick={() => setOwnerSection(section.id)}>{section.title}</button>)}
          </nav>
          <section className="min-w-0 rounded-xl border border-[var(--border)] bg-[var(--bg)] p-4 sm:p-5" aria-live="polite">
            <p className="text-xs font-semibold text-[var(--primary)]">OWNER POLICY</p>
            <h3 className="mt-1 text-lg font-semibold">{selectedTitle}</h3>
            {selectedText?.content && <div className="mt-4 rounded-lg border border-[var(--border)] bg-[var(--card)] p-4"><p className="text-xs font-semibold text-[var(--muted)]">{selectedText.lotId ? "This parking site" : "All owner sites"} · Saved policy text</p><p className="mt-2 whitespace-pre-wrap text-sm leading-7 text-[var(--fg)]">{selectedText.content}</p></div>}
            {selectedDocument?.fileData ? <>
              <p className="mt-2 break-all text-sm text-[var(--muted)]">{selectedDocument.fileName} · Updated {new Intl.DateTimeFormat("en-US", { dateStyle: "medium" }).format(new Date(selectedDocument.uploadedAt))}</p>
              <iframe className="mt-4 h-[55dvh] w-full rounded-lg border border-[var(--border)] bg-white" src={selectedDocument.fileData} title={`${selectedTitle} PDF`} />
              <a className="btn-outline mt-4 inline-flex no-underline" href={selectedDocument.fileData} download={selectedDocument.fileName}>Download PDF</a>
            </> : ownerSection === "general" ? <>
              <p className="mt-2 text-sm text-[var(--muted)]">The owner has not uploaded a general policy PDF for this site. These are the site's currently listed rates.</p>
              <dl className="mt-4 grid gap-3 sm:grid-cols-2"><div className="rounded-lg border border-[var(--border)] p-3"><dt className="text-sm text-[var(--muted)]">Daytime rate</dt><dd className="mt-1 font-semibold tabular-nums">{lot.hourlyRate.toLocaleString("vi-VN")} VND/hour</dd></div><div className="rounded-lg border border-[var(--border)] p-3"><dt className="text-sm text-[var(--muted)]">Nighttime rate</dt><dd className="mt-1 font-semibold tabular-nums">{lot.nightRate.toLocaleString("vi-VN")} VND/hour</dd></div></dl>
            </> : <p className="mt-3 text-sm text-[var(--muted)]">{selectedText?.content ? "No PDF is attached to this section." : "No owner policy has been saved for this section. Check the other sections or contact the parking site for details."}</p>}
          </section>
        </div> : <div className="grid min-w-0 gap-5 md:grid-cols-[220px_minmax(0,1fr)]">
          <nav className="flex flex-wrap content-start gap-2 md:flex-col" aria-label="Platform policy sections">
            {([{ id: "terms", title: "Terms of Service" }, { id: "privacy", title: "Privacy Policy" }, { id: "payment-refund", title: "Payment & Refund" }] as const).map(section => <button key={section.id} type="button" className={`min-h-11 rounded-xl border px-3 py-2 text-left text-sm font-semibold ${platformSection === section.id ? "border-[var(--primary)] bg-[var(--active)] text-[var(--fg)]" : "border-[var(--border)] bg-[var(--bg)] text-[var(--muted)]"}`} aria-current={platformSection === section.id ? "true" : undefined} onClick={() => setPlatformSection(section.id)}>{section.title}</button>)}
          </nav>
          <section className="min-w-0 overflow-hidden rounded-xl border border-[var(--border)] bg-[var(--bg)]" aria-label="SmartParking platform policy">
            <p className="px-5 pt-4 text-xs font-semibold text-[var(--primary)]">SMARTPARKING PLATFORM POLICY</p>
            {platformSection === "terms" ? <TermsOfService /> : platformSection === "privacy" ? <PrivacyPolicy /> : <PaymentRefund />}
          </section>
        </div>}
      </div>
    </div>
  </div>, document.body)
}
