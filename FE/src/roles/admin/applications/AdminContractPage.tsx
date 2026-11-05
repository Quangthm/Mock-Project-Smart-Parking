import { useEffect, useState } from "react"
import { useNavigate, useParams } from "react-router-dom"
import { useApp } from "../../../context/AppContext"
import { PartnerContractDialog } from "../../../components/modals/PartnerContractDialog"
import { loadContractDraft, saveContractDraft, type ContractDraftRecord } from "../../../lib/partnerContract"
import { ownerApi, type OwnerApplicationRecord } from "../../../lib/ownerApi"

export function AdminContractPage() {
  const { applicationId } = useParams()
  const { user } = useApp()
  const navigate = useNavigate()
  const [application, setApplication] = useState<OwnerApplicationRecord | null>(null)
  const [record, setRecord] = useState<ContractDraftRecord | null>(null)
  const [error, setError] = useState("")
  const [notice, setNotice] = useState("")

  useEffect(() => {
    setApplication(null)
    setRecord(null)
    setError("")
    if (!applicationId) { setError("Application not found."); return }
    let active = true
    ownerApi.get(applicationId).then(match => {
      if (!active) return
      setApplication(match)
      setRecord(loadContractDraft(match.id))
    }).catch(cause => { if (active) setError(cause instanceof Error ? cause.message : "Cannot load application.") })
    return () => { active = false }
  }, [applicationId])

  const back = () => navigate("/", { state: { adminTab: "applications" } })
  const closeTab = (completed = false) => {
    window.close()
    window.setTimeout(() => {
      if (!window.closed) setNotice(completed ? "Agreement saved. Close this tab to continue in Applications, or use the button below." : "Close this tab to continue in Applications, or use the button below.")
    }, 150)
  }
  if (error) return <div role="alert" className="card mx-auto my-8 max-w-xl"><p>{error}</p><button type="button" className="btn-outline" onClick={back}>Back to Applications</button></div>
  if (!application) return <p role="status" className="p-8">Loading agreement…</p>

  return <><PartnerContractDialog page applicationId={application.id}
    ownerName={application.ownerName || record?.ownerName || "Owner"}
    businessName={application.businessName} lotName={application.lotSetup?.name || "Parking lot"}
    record={record} signer="admin" signerName={user?.name || ""} onClose={() => closeTab()}
    onSign={name => {
      if (!record?.ownerAcknowledgedAt) return
      const next = { ...record, ownerEmail: application.email, businessName: application.businessName,
        lotName: application.lotSetup?.name || "Parking lot", adminName: name, adminAcknowledgedAt: new Date().toISOString() }
      saveContractDraft(application.id, next)
      setRecord(next)
      closeTab(true)
    }} />
    {notice && <div className="partner-contract-page-notice" role="status"><p>{notice}</p><button type="button" className="btn-outline" onClick={back}>Back to Applications</button></div>}
  </>
}
