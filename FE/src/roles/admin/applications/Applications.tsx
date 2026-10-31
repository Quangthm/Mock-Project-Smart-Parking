import { useEffect, useState } from "react"
import { useApp } from "../../../context/AppContext"
import { PartnerContractDialog } from "../../../components/modals/PartnerContractDialog"
import { Parking3DViewer } from "../../../components/3d/Parking3DViewer"
import { ALLOWED_PARKING_MODELS } from "../../../lib/3d/parking3DConfig"
import { loadOwnerModelUrl } from "../../../lib/3d/ownerModelStorage"
import { loadContractDraft, saveContractDraft, type ContractDraftRecord } from "../../../lib/partnerContract"

import { ownerApi, type OwnerApplicationRecord } from "../../../lib/ownerApi"

import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { PageHeader } from "../../../components/ui/PageHeader"

import { request } from "../../../lib/authApi"
import { showUiToast } from "../../../lib/uiToast"

export function Applications() {
  const { user } = useApp()
  const [apps, setApps] = useState<OwnerApplicationRecord[]>([])
  const [contractDrafts, setContractDrafts] = useState<Record<string, ContractDraftRecord | null>>({})
  const [contractApp, setContractApp] = useState<OwnerApplicationRecord | null>(null)
  const [modelApp, setModelApp] = useState<OwnerApplicationRecord | null>(null)
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

  useEffect(() => {
    let active = true

    setLoading(true)

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
        if (active) setLoading(false)
      })

    return () => {
      active = false
    }
  }, [reload])

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

  const pending = apps.filter((a) => a.status === "pending")

  const reviewed = apps.filter((a) => a.status !== "pending")

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
      {loading && <p role="status">Loading applications...</p>}
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
              <div
                key={app.id}
                className="card application-card"
                style={{ border: "1.5px solid #fde68a" }}
              >
                <div className="application-card-header"
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    marginBottom: "0.75rem",
                    flexWrap: "wrap",
                    gap: "0.5rem",
                  }}
                >
                  <div>
                    <div
                      style={{
                        fontFamily: "Be Vietnam Pro",
                        fontWeight: 700,
                        fontSize: "0.95rem",
                        color: "var(--fg)",
                      }}
                    >
                      {app.businessName}
                    </div>
                    <div style={{ fontSize: "0.8rem", color: "var(--muted)" }}>
                      {app.ownerName} ·{" "}
                      {new Date(app.submittedAt).toLocaleDateString("en-GB")}
                    </div>
                    <div style={{ fontSize: "0.8rem", color: "var(--muted)" }}>
                      {app.email} - {app.phone}
                    </div>
                    <div style={{ fontSize: "0.8rem", color: "var(--muted)" }}>
                      Type: {app.lotType.replace("-", " ")} · Contacts:{" "}
                      {app.contactVerified
                        ? "Verified"
                        : "Awaiting verification"}
                    </div>
                    <div style={{ fontSize: "0.8rem", color: "var(--muted)", marginTop: "0.4rem" }}>
                      {app.lotSetupComplete && app.lotSetup ? (
                        <>
                          <strong>Parking lot:</strong> {app.lotSetup.name} · {app.lotSetup.address}
                          <br />
                          {app.lotSetup.totalSlots} slots · {app.lotSetup.floors} floor(s) ·
                          {" "}{app.lotSetup.hourlyRate.toLocaleString("en-US")} VND/hour
                          <details style={{ marginTop: "0.35rem" }}>
                            <summary style={{ cursor: "pointer" }}>View lot setup</summary>
                            <div>Type: {app.lotSetup.lotType.replace("-", " ")}</div>
                            <div>3D model: {app.lotSetup.modelFileName}</div>
                            <div>Devices: {app.lotSetup.devices.map((device) => device.label).join(", ") || "None"}</div>
                            <div>Daily: {app.lotSetup.dailyRate.toLocaleString("en-US")} VND · Night: {app.lotSetup.nightRate.toLocaleString("en-US")} VND</div>
                            <div>Monthly: {app.lotSetup.subscriptionMonthly.toLocaleString("en-US")} VND · Yearly: {app.lotSetup.subscriptionYearly.toLocaleString("en-US")} VND</div>
                            <div>Grace period: {app.lotSetup.gracePeriodMinutes} minutes · Policy: {app.lotSetup.policyAccepted ? "Accepted" : "Not accepted"}</div>
                          </details>
                        </>
                      ) : (
                        "Parking lot setup incomplete"
                      )}
                    </div>
                  </div>
                </div>
                {app.lotSetup && <div className="application-document-actions">
                  <button type="button" className="btn-outline" onClick={() => setContractApp(app)}>Review contract</button>
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
                    <label className="label">Review Note (optional)</label>
                    <input
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
            <div
              key={app.id}
              className="card application-reviewed-card"
              style={{
                padding: "0.75rem 1rem",
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
              }}
            >
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
                  <button type="button" className="btn-outline" onClick={() => setContractApp(app)}>View contract</button>
                  <button type="button" className="btn-outline" onClick={() => setModelApp(app)}>View 3D model</button>
                </div>}
                {app.deliveryStatus && (
                  <p>Notification: {app.deliveryStatus}</p>
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
                {app.status.toUpperCase()}
              </span>
            </div>
          ))}
        </div>
      </section>
      {contractApp && <PartnerContractDialog
        applicationId={contractApp.id}
        ownerName={contractApp.ownerName || contractDrafts[contractApp.id]?.ownerName || "Owner"}
        businessName={contractApp.businessName}
        lotName={contractApp.lotSetup?.name || "Parking lot"}
        record={contractDrafts[contractApp.id] ?? null}
        signer="admin"
        signerName={user?.name || ""}
        onClose={() => setContractApp(null)}
        onSign={(name) => {
          const existing = contractDrafts[contractApp.id]
          if (!existing?.ownerAcknowledgedAt) return
          const next = { ...existing, ownerEmail: contractApp.email, businessName: contractApp.businessName,
            lotName: contractApp.lotSetup?.name || "Parking lot", adminName: name, adminAcknowledgedAt: new Date().toISOString() }
          saveContractDraft(contractApp.id, next)
          setContractDrafts(current => ({ ...current, [contractApp.id]: next }))
          setContractApp(null)
        }} />}
      {modelApp && <div className="partner-contract-backdrop" onMouseDown={event => { if (event.target === event.currentTarget) setModelApp(null) }}>
        <section className="partner-contract-dialog" role="dialog" aria-modal="true" aria-label="Parking lot 3D model">
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
            {modelLoading ? <p role="status">Loading 3D model…</p> : contractDrafts[modelApp.id]?.modelFileName && !modelUrl && !reviewFileUrl ? <p>The imported model is stored only in the Owner's browser. Ask the Owner for the GLB file to review it on this device.</p> : <Parking3DViewer modelUrl={reviewFileUrl || modelUrl || ALLOWED_PARKING_MODELS[modelApp.lotType].path} lotType={modelApp.lotType} interactive={false} showLegend={false} showControls={true} height={340} />}
          </div>
        </section>
      </div>}
    </div>
  )
}
