import { useEffect, useState } from "react"
import { createPortal } from "react-dom"
import { useApp } from "../../../context/AppContext"
import { Parking3DViewer } from "../../../components/3d/Parking3DViewer"
import { ErrorBoundary } from "../../../components/feedback/ErrorBoundary"
import { ALLOWED_PARKING_MODELS } from "../../../lib/3d/parking3DConfig"
import { loadOwnerModelUrl } from "../../../lib/3d/ownerModelStorage"
import { contractDraftStorageKey, loadContractDraft, type ContractDraftRecord } from "../../../lib/partnerContract"

import { ownerApi, type OwnerApplicationRecord } from "../../../lib/ownerApi"

import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { PageHeader } from "../../../components/ui/PageHeader"

import { request } from "../../../lib/authApi"
import { showUiToast } from "../../../lib/uiToast"
import { formatStatus } from "../../../lib/formatStatus"
import { useDialogKeyboard } from "../../../components/ui/useDialogKeyboard"
import { useServerRefresh } from "../../../lib/serverRefresh"

export function Applications() {
  const { accentColor, user } = useApp()
  const [apps, setApps] = useState<OwnerApplicationRecord[]>([])
  const [contractDrafts, setContractDrafts] = useState<Record<string, ContractDraftRecord | null>>({})
  const [modelApp, setModelApp] = useState<OwnerApplicationRecord | null>(null)
  const modelDialogRef = useDialogKeyboard<HTMLElement>(modelApp?.id ?? null, () => setModelApp(null))
  const [modelUrl, setModelUrl] = useState<string | null>(null)
  const [modelLoading, setModelLoading] = useState(false)
  const [modelError, setModelError] = useState("")
  const [reviewFileUrl, setReviewFileUrl] = useState<string | null>(null)
  const [reviewFileName, setReviewFileName] = useState("")

  const [notes, setNotes] = useState<Record<string, string>>({})

  const [loading, setLoading] = useState(true)

  const [busy, setBusy] = useState<string | null>(null)

  const [error, setError] = useState("")

  const [reload, setReload] = useState(0)
  const [page, setPage] = useState(1)
  const [refreshing, setRefreshing] = useState(false)
  const [backgroundRevision, setBackgroundRevision] = useState(0)
  useServerRefresh(user?.role === "admin" ? user.id : undefined, () => {
    if (!busy && !loading && !refreshing) setBackgroundRevision(value => value + 1)
  })

  useEffect(() => {
    let active = true

    if (busy) return
    if (!apps.length && !backgroundRevision) setLoading(true)
    setRefreshing(true)

    setError("")

    ownerApi
      .list()
      .then((data) => {
        if (active) {
          setApps(data)
          setContractDrafts(Object.fromEntries(data.map(app => [app.id, loadContractDraft(app.id)])))
        }
      })

      .catch((e) => {
        if (active)
          setError(e instanceof Error ? e.message : "Cannot load applications.")
      })

      .finally(() => {
        if (active) { setLoading(false); setRefreshing(false) }
      })

    return () => {
      active = false
    }
  }, [reload, backgroundRevision, busy, user?.id])

  useEffect(() => {
    const refreshDrafts = () => setContractDrafts(Object.fromEntries(apps.map(app => [app.id, loadContractDraft(app.id)])))
    const onStorage = (event: StorageEvent) => {
      if (apps.some(app => event.key === contractDraftStorageKey(app.id))) refreshDrafts()
    }
    window.addEventListener("storage", onStorage)
    window.addEventListener("focus", refreshDrafts)
    return () => {
      window.removeEventListener("storage", onStorage)
      window.removeEventListener("focus", refreshDrafts)
    }
  }, [apps])

  function openContract(applicationId: string) {
    const tab = window.open(`/admin/applications/${encodeURIComponent(applicationId)}/contract`, "_blank")
    if (!tab) {
      showUiToast("Could not open agreement", "Allow pop-ups for SmartParking and try again.", "error")
      return
    }
    // The new same-origin tab inherits this session's credentials before its opener is detached.
    tab.opener = null
  }

  useEffect(() => {
    if (!modelApp) { setReviewFileUrl(null); setReviewFileName(""); return }
    let active = true
    setModelUrl(null)
    setModelError("")
    setReviewFileUrl(null)
    setReviewFileName("")
    setModelLoading(true)
    loadOwnerModelUrl(modelApp.id)
      .then(url => { if (active) setModelUrl(url) })
      .catch(() => { if (active) setModelError("The Owner model is unavailable in this browser.") })
      .finally(() => { if (active) setModelLoading(false) })
    return () => { active = false }
  }, [modelApp])

  useEffect(() => () => { if (reviewFileUrl) URL.revokeObjectURL(reviewFileUrl) }, [reviewFileUrl])

  async function review(
    app: OwnerApplicationRecord,
    status: "approved" | "rejected",
  ) {
    if (busy) return

    setBusy(app.id)

    setError("")

    try {
      const updated = await ownerApi.review(app.id, status, notes[app.id] ?? "")

      setApps((current) =>
        current.map((a) => (a.id === updated.id ? updated : a)),
      )

      setNotes((current) => ({ ...current, [app.id]: "" }))
      showUiToast("Application reviewed", `Owner application ${status}.`)
    } catch (e) {
      const message = e instanceof Error ? e.message : "Review failed. Refresh and try again."
      setError(message)
      showUiToast("Review failed", message, "error")
    } finally {
      setBusy(null)
    }
  }

  const orderedApps = [...apps.filter(a => a.status === "pending"), ...apps.filter(a => a.status !== "pending")]
  const pageCount = Math.max(1, Math.ceil(orderedApps.length / 8))
  const currentPage = Math.min(page, pageCount)
  const pageItems = orderedApps.slice((currentPage - 1) * 8, currentPage * 8)
  const pending = pageItems.filter((a) => a.status === "pending")
  const reviewed = pageItems.filter((a) => a.status !== "pending")

  return (
    <div className="admin-applications">
      <div className="admin-applications-toolbar">
        <PageHeader title="Owner Partnership Applications" variant="compact" />
        <button
          className="btn-secondary"
          disabled={loading || !!busy}
          onClick={() => setReload((n) => n + 1)}
        >
          Refresh applications
        </button>
      </div>
      {loading && <p role="status">Loading applications…</p>}
      {error && (
        <p role="alert" style={{ color: "#dc2626" }}>
          {error}
        </p>
      )}
      {!loading && !error && apps.length === 0 && <p>No applications yet.</p>}
      {pending.length > 0 && (
        <section className="application-section">
          <h3
            style={{
              display: "flex",
              alignItems: "center",
              gap: 6,
              fontSize: "0.9rem",
              fontWeight: 600,
              color: "#f59e0b",
              marginBottom: "0.875rem",
            }}
          >
            <UntitledIcon name="clock" size={16} /> Pending Review (
            {pending.length})
          </h3>
          <div
            style={{
              display: "flex",
              flexDirection: "column",
              gap: "0.875rem",
            }}
          >
            {pending.map((app) => (
              <details key={app.id} className="card application-card application-card-pending application-collapsible">
                <summary className="application-collapse-summary">
                  <span className="application-collapse-name"><strong>{app.lotSetup?.name || app.businessName}</strong><small>{app.businessName}</small></span>
                  <span className="application-pending-badge"><UntitledIcon name="clock" size={15} /> Pending review</span>
                  <span className="application-collapse-delivery" data-status={app.deliveryStatus || "none"}><UntitledIcon name="mail" size={14} /> {formatStatus(app.deliveryStatus || "Not queued")}</span>
                  <UntitledIcon name="chevron-down" size={18} />
                </summary>
                <div className="application-expanded">
                <div className="application-card-header">
                  <div>
                    <p className="application-eyebrow">Owner application · {new Date(app.submittedAt).toLocaleDateString("en-GB")}</p>
                    <h4 className="application-business-name">{app.businessName}</h4>
                    <p className="application-owner-contact">{app.ownerName} · {app.email} · {app.phone}</p>
                  </div>
                  <span className="application-pending-badge"><UntitledIcon name="clock" size={15} /> Pending review</span>
                </div>
                <div className="application-review-signals" aria-label="Approval checks">
                  <div className={app.contactVerified ? "application-signal is-ready" : "application-signal is-blocked"}><span>Contact</span><strong>{app.contactVerified ? "Verified" : "Not verified"}</strong></div>
                  <div className={app.lotSetupComplete && app.lotSetup ? "application-signal is-ready" : "application-signal is-blocked"}><span>Lot setup</span><strong>{app.lotSetupComplete && app.lotSetup ? "Complete" : "Incomplete"}</strong></div>
                  <div className={app.lotSetup?.policyAccepted ? "application-signal is-ready" : "application-signal is-blocked"}><span>Terms</span><strong>{app.lotSetup?.policyAccepted ? "Accepted" : "Not accepted"}</strong></div>
                  <div className={contractDrafts[app.id]?.ownerAcknowledgedAt ? "application-signal is-ready" : "application-signal is-blocked"}><span>Owner agreement</span><strong>{contractDrafts[app.id]?.ownerAcknowledgedAt ? "Signed" : contractDrafts[app.id] ? "Awaiting signature" : "Unavailable here"}</strong></div>
                </div>
                {app.lotSetup ? (
                  <section className="application-lot-summary" aria-label="Submitted parking lot">
                    <div className="application-lot-heading"><span>Submitted parking lot</span><strong>{app.lotSetup.name}</strong><span>{app.lotSetup.address}</span></div>
                    <div className="application-lot-facts">
                      <span><strong>Type</strong>{app.lotSetup.lotType.replace("-", " ")}</span>
                      <span><strong>Capacity</strong>{app.lotSetup.totalSlots} slots · {app.lotSetup.floors} floor(s)</span>
                      <span><strong>3D model</strong>{app.lotSetup.modelFileName}</span>
                      <span><strong>Hourly rate</strong>{app.lotSetup.hourlyRate.toLocaleString("en-US")} VND</span>
                    </div>
                    <details className="application-lot-details">
                      <summary>More setup details</summary>
                      <div className="application-lot-extra">
                        <p><strong>Devices:</strong> {app.lotSetup.devices.map(device => device.label).join(", ") || "None"}</p>
                        <p><strong>Day / night:</strong> {app.lotSetup.dailyRate.toLocaleString("en-US")} / {app.lotSetup.nightRate.toLocaleString("en-US")} VND</p>
                        <p><strong>Monthly / yearly:</strong> {app.lotSetup.subscriptionMonthly.toLocaleString("en-US")} / {app.lotSetup.subscriptionYearly.toLocaleString("en-US")} VND</p>
                        <p><strong>Grace period:</strong> {app.lotSetup.gracePeriodMinutes} minutes</p>
                      </div>
                    </details>
                  </section>
                ) : <p className="application-setup-missing">Parking lot setup is incomplete. Review cannot be approved yet.</p>}
                {app.lotSetup && <div className="application-document-actions">
                  <button type="button" className="btn-outline" onClick={() => openContract(app.id)}>Review contract</button>
                  <button type="button" className="btn-outline" onClick={() => setModelApp(app)}>View 3D model</button>
                  {contractDrafts[app.id]?.adminAcknowledgedAt && <span>Admin acknowledged</span>}
                  {contractDrafts[app.id]?.ownerAcknowledgedAt && !contractDrafts[app.id]?.adminAcknowledgedAt && <span>Sign the draft before approving this application.</span>}
                  {!contractDrafts[app.id] && <span>Contract draft is not available in this browser.</span>}
                </div>}
                <div className="application-review-form"
                  style={{
                    display: "flex",
                    gap: "0.625rem",
                    alignItems: "flex-end",
                    flexWrap: "wrap",
                  }}
                >
                  <div className="application-review-note" style={{ flex: 1, minWidth: "200px" }}>
                    <label className="label" htmlFor={`application-review-note-${app.id}`}>Review Note (optional)</label>
                    <input
                      id={`application-review-note-${app.id}`}
                      className="input"
                      value={notes[app.id] ?? ""}
                      maxLength={2000}
                      onChange={(e) =>
                        setNotes((current) => ({
                          ...current,
                          [app.id]: e.target.value,
                        }))
                      }
                      placeholder="Add a note..."
                    />
                  </div>
                  <div className="application-review-actions">
                  <button
                    disabled={!!busy || loading || !app.contactVerified || !app.lotSetupComplete || !app.lotSetup || (!!contractDrafts[app.id] && !contractDrafts[app.id]?.adminAcknowledgedAt)}
                    onClick={() => review(app, "approved")}
                    className="application-approve"
                    style={{
                      padding: "0.5rem 1rem",
                      background: "#22c55e",
                      color: "#fff",
                      border: "none",
                      borderRadius: "var(--radius)",
                      cursor: "pointer",
                      fontWeight: 600,
                      fontSize: "0.875rem",
                    }}
                  >
                    <>
                      <UntitledIcon name="check" size={16} /> Approve
                    </>
                  </button>
                  <button
                    disabled={!!busy || loading}
                    onClick={() => review(app, "rejected")}
                    className="application-reject"
                    style={{
                      padding: "0.5rem 1rem",
                      background: "#fee2e2",
                      color: "#dc2626",
                      border: "1px solid #fca5a5",
                      borderRadius: "var(--radius)",
                      cursor: "pointer",
                      fontWeight: 600,
                      fontSize: "0.875rem",
                    }}
                  >
                    <>
                      <UntitledIcon name="x" size={16} /> Reject
                    </>
                  </button>
                  </div>
                </div>
                </div>
              </details>
            ))}
          </div>
        </section>
      )}
      <section className="application-section application-reviewed">
        <h3
          style={{
            fontSize: "0.9rem",
            fontWeight: 600,
            color: "var(--muted)",
            marginBottom: "0.875rem",
          }}
        >
          Previously Reviewed
        </h3>
        <div
          style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}
        >
          {reviewed.map((app) => (
            <details
              key={app.id}
              className="card application-reviewed-card application-collapsible"
            >
              <summary className="application-collapse-summary">
                <span className="application-collapse-name"><strong>{app.lotSetup?.name || app.businessName}</strong><small>{app.businessName}</small></span>
                <span className="application-reviewed-status" data-status={app.status}>{app.status === "approved" ? "Active" : formatStatus(app.status)}</span>
                <span className="application-collapse-delivery" data-status={app.deliveryStatus || "none"}><UntitledIcon name="mail" size={14} /> {formatStatus(app.deliveryStatus || "Not queued")}</span>
                <UntitledIcon name="chevron-down" size={18} />
              </summary>
              <div className="application-expanded">
              <div>
                <div
                  style={{
                    fontWeight: 600,
                    fontSize: "0.875rem",
                    color: "var(--fg)",
                  }}
                >
                  {app.businessName}
                </div>
                <div style={{ fontSize: "0.78rem", color: "var(--muted)" }}>
                  {app.ownerName} · {app.lotType}
                </div>
                {app.lotSetup && <div className="application-document-actions">
                  <button type="button" className="btn-outline" onClick={() => openContract(app.id)}>View contract</button>
                  <button type="button" className="btn-outline" onClick={() => setModelApp(app)}>View 3D model</button>
                </div>}
                {app.deliveryStatus && (
                  <p className="application-delivery-detail" role="status"><UntitledIcon name="mail" size={16} /> Notification delivery: <strong>{formatStatus(app.deliveryStatus)}</strong></p>
                )}
                {app.deliveryStatus === "failed" && app.deliveryId && (
                  <button
                    className="btn-outline"
                    disabled={!!busy}
                    onClick={async () => {
                      setBusy(app.id)
                      try {
                        await request(
                          `/deliveries/${app.deliveryId}/retry`,
                          "POST",
                        )
                        setReload((n) => n + 1)
                        showUiToast("Notification retried", "The delivery request was submitted again.")
                      } catch (e) {
                        setError(
                          e instanceof Error ? e.message : "Retry failed.",
                        )
                      } finally {
                        setBusy(null)
                      }
                    }}
                  >
                    Retry notification
                  </button>
                )}
                {app.reviewNote && (
                  <div style={{ fontSize: "0.78rem", color: "var(--muted)" }}>
                    {app.reviewNote}
                  </div>
                )}
              </div>
              <span
                style={{
                  padding: "0.2rem 0.625rem",
                  borderRadius: "999px",
                  fontSize: "0.75rem",
                  fontWeight: 700,
                  background:
                    app.status === "approved" ? "#22c55e20" : "#ef444420",
                  color: app.status === "approved" ? "#22c55e" : "#ef4444",
                }}
              >
                {app.status === "approved" ? "Active" : formatStatus(app.status)}
              </span>
              </div>
            </details>
          ))}
        </div>
      </section>
      {!loading && pageCount > 1 && <nav className="application-pagination" aria-label="Application pages">
        <button type="button" className="btn-outline" disabled={currentPage === 1} onClick={() => setPage(currentPage - 1)}>Previous</button>
        <span>Page {currentPage} of {pageCount} · {orderedApps.length} applications</span>
        <button type="button" className="btn-outline" disabled={currentPage === pageCount} onClick={() => setPage(currentPage + 1)}>Next</button>
      </nav>}
      {modelApp && createPortal(<div className="dashboard-theme partner-contract-backdrop" data-accent={accentColor} onMouseDown={event => { if (event.target === event.currentTarget) setModelApp(null) }}>
        <section ref={modelDialogRef} className="partner-contract-dialog" role="dialog" aria-modal="true" aria-label="Parking lot 3D model" tabIndex={-1}>
          <header className="partner-contract-header"><div><p className="partner-contract-eyebrow">Owner application</p><h2>{modelApp.lotSetup?.name || "Parking lot"} · 3D model</h2></div><button type="button" className="partner-contract-close" aria-label="Close 3D model" onClick={() => setModelApp(null)}>×</button></header>
          <div className="partner-contract-content">
            <p>{contractDrafts[modelApp.id]?.modelFileName ? `Owner import: ${contractDrafts[modelApp.id]?.modelFileName}` : `System model: ${modelApp.lotSetup?.modelFileName || "Not specified"}`}</p>
            {modelError && <p role="alert">{modelError}</p>}
            <label className="owner-model-import application-model-import"><span className="btn-outline">Open GLB for local review</span><input className="sr-only" type="file" accept=".glb,model/gltf-binary" onChange={event => {
              const file = event.target.files?.[0]
              if (file && file.name.toLowerCase().endsWith(".glb") && file.size <= 30 * 1024 * 1024) {
                setReviewFileUrl(URL.createObjectURL(file))
                setReviewFileName(file.name)
                setModelError("")
              } else if (file) setModelError("Choose a GLB file smaller than 30 MB.")
              event.target.value = ""
            }} /></label>
            {reviewFileName && <p>Previewing local file: {reviewFileName}</p>}
            {modelLoading ? <p role="status">Loading 3D model…</p> : contractDrafts[modelApp.id]?.modelFileName && !modelUrl && !reviewFileUrl ? <p>The imported model is stored only in the Owner's browser. Ask the Owner for the GLB file to review it on this device.</p> : <ErrorBoundary name="Parking 3D viewer"><Parking3DViewer modelUrl={reviewFileUrl || modelUrl || ALLOWED_PARKING_MODELS[modelApp.lotType].path} lotType={modelApp.lotType} interactive={false} showLegend={false} showControls={true} height={340} /></ErrorBoundary>}
          </div>
        </section>
      </div>, document.body)}
    </div>
  )
}
