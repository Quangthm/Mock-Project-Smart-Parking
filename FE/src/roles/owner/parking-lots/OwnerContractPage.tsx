import { useState } from "react"
import { useNavigate, useParams } from "react-router-dom"
import { PartnerContractDialog } from "../../../components/modals/PartnerContractDialog"
import { listOwnerContractCopies, loadContractDraft, loadOwnerContractHandoff, saveContractDraft } from "../../../lib/partnerContract"
import { useApp } from "../../../context/AppContext"

export function OwnerContractPage() {
  const { applicationId } = useParams()
  const navigate = useNavigate()
  const { user } = useApp()
  const [handoff] = useState(() => applicationId ? loadOwnerContractHandoff(applicationId) : null)
  const [record, setRecord] = useState(() => applicationId ? loadContractDraft(applicationId) : null)
  const [notice, setNotice] = useState("")
  const savedCopy = user?.role === "owner" && applicationId
    ? listOwnerContractCopies(user.email).find(copy => copy.applicationId === applicationId)?.record
    : undefined

  function closeTab() {
    if (savedCopy) {
      navigate(-1)
      return
    }
    window.close()
    window.setTimeout(() => {
      if (!window.closed) setNotice("You can close this tab and continue in your previous SmartParking tab.")
    }, 150)
  }

  if (!applicationId || (!handoff && !savedCopy)) return <div className="partner-contract-page"><div className="card mx-auto max-w-xl p-6" role="alert"><h1>Agreement unavailable</h1><p>Open the agreement from your parking lot setup or Owner settings.</p></div></div>

  const activeRecord = handoff ? record?.setupFingerprint === handoff.setupFingerprint ? record : null : savedCopy ?? null
  const ownerName = handoff?.ownerName ?? savedCopy!.ownerName
  const businessName = handoff?.businessName ?? savedCopy?.businessName ?? "Owner business"
  const lotName = handoff?.lotName ?? savedCopy?.lotName ?? "Parking lot"

  return <>
    <PartnerContractDialog page applicationId={applicationId}
      ownerName={ownerName} businessName={businessName} lotName={lotName}
      record={activeRecord} signer="owner" signerName={ownerName} readOnly={!handoff || !!savedCopy}
      onClose={closeTab}
      onSign={name => {
        if (!handoff) return
        const next = {
          ...activeRecord,
          ownerName: name,
          ownerEmail: handoff.ownerEmail,
          businessName: handoff.businessName,
          lotName: handoff.lotName,
          ownerAcknowledgedAt: new Date().toISOString(),
          modelFileName: handoff.modelFileName,
          setupFingerprint: handoff.setupFingerprint,
        }
        saveContractDraft(applicationId, next)
        setRecord(next)
        closeTab()
      }} />
    {notice && <p className="partner-contract-page-notice" role="status">{notice}</p>}
  </>
}
