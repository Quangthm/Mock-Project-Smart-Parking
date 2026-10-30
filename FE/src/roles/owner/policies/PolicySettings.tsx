import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { useEffect, useState, useRef, useMemo } from "react"

import { useApp } from "../../../context/AppContext"
import { PageHeader } from "../../../components/ui/PageHeader"

import { store } from "../../../lib/store"

import type { LotPolicyPdf, ParkingLot } from "../../../lib/types"

import { UntitledIcon } from "../../../components/icon/UntitledIcon"

import { LotPolicyModal } from "../../../components/modals/LotPolicyModal"

import { GroupedNumberInput } from "../../../components/forms/GroupedNumberInput"

import { digitsOnly } from "../../../lib/groupedDigits"
import { extractOwnerPolicyPdfSections } from "../../../lib/ownerPolicyPdfTemplate"
import { deleteOwnerPolicySectionDocument, deleteOwnerPolicySectionText, getOwnerPolicySectionDocuments, getOwnerPolicySectionTexts, ownerPolicySections, saveOwnerPolicySectionText, starterOwnerPolicyText, type OwnerPolicySectionDocuments, type OwnerPolicySectionId, type OwnerPolicySectionTexts } from "../../../lib/policySections"

type DepositMode = "percentage" | "fixed"

interface PolicyState {
  pricingEnabled: boolean

  dayRate: string

  nightRate: string

  depositEnabled: boolean

  depositMode: DepositMode

  depositValue: string
}

interface PreviewState {
  total: number

  deposit: number

  checkout: number
}

const initialPolicy: PolicyState = {
  pricingEnabled: true,

  dayRate: "15000",

  nightRate: "25000",

  depositEnabled: true,

  depositMode: "percentage",

  depositValue: "20",
}

const inputClass =
  "w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm text-[var(--fg)] outline-none transition focus:border-[var(--primary)] focus:ring-2 focus:ring-blue-500/20"

function policyDrafts(ownerId: string, scope: string): Record<OwnerPolicySectionId, string> {
  const saved = getOwnerPolicySectionTexts(ownerId, scope)
  return Object.fromEntries(ownerPolicySections.map(section => [section.id, saved[section.id]?.content ?? starterOwnerPolicyText[section.id]])) as Record<OwnerPolicySectionId, string>
}

interface PolicySettingsProps {
  selectedSiteId?: string
  onSelectSite?: (siteId: string) => void
}

export function PolicySettings({
  selectedSiteId: externalSiteId,
  onSelectSite,
}: PolicySettingsProps = {}) {
  const { user } = useApp()

  const [policy, setPolicy] = useState<PolicyState>(() => {
    const stored = user ? store.getOwnerPolicy(user.id) : null
    return stored
      ? {
          ...stored,
          dayRate: digitsOnly(String(stored.dayRate)),
          nightRate: digitsOnly(String(stored.nightRate)),
        }
      : initialPolicy
  })

  const [dayHours, setDayHours] = useState("8")

  const [nightHours, setNightHours] = useState("2")

  const [preview, setPreview] = useState<PreviewState>({
    total: 170000,
    deposit: 34000,
    checkout: 136000,
  })

  const [saved, setSaved] = useState(false)

  // Owner parking lots & PDF Policy state
  const ownerLots = useMemo(
    () => (user ? store.getLotsByOwner(user.id) : []),
    [user],
  )
  const [selectedLotId, setSelectedLotId] = useState<string>(
    externalSiteId || "all",
  )
  const [currentPdf, setCurrentPdf] = useState<LotPolicyPdf | null>(null)
  const [sectionDocuments, setSectionDocuments] = useState<OwnerPolicySectionDocuments>(() => user ? getOwnerPolicySectionDocuments(user.id, externalSiteId || "all") : {})
  const [sectionTexts, setSectionTexts] = useState<OwnerPolicySectionTexts>(() => user ? getOwnerPolicySectionTexts(user.id, externalSiteId || "all") : {})
  const [sectionDrafts, setSectionDrafts] = useState<Record<OwnerPolicySectionId, string>>(() => policyDrafts(user?.id ?? "", externalSiteId || "all"))
  const [importedSectionIds, setImportedSectionIds] = useState<OwnerPolicySectionId[]>([])
  const [previewLot, setPreviewLot] = useState<ParkingLot | null>(null)
  const [previewSource, setPreviewSource] = useState<"owner" | "platform">("owner")
  const [previewPlatformSection, setPreviewPlatformSection] = useState<"terms" | "privacy" | "payment-refund">("terms")
  const [pdfNotice, setPdfNotice] = useState<string | null>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    if (externalSiteId !== undefined) {
      setSelectedLotId(externalSiteId)
    }
  }, [externalSiteId])

  const handleLotChange = (newLotId: string) => {
    setSelectedLotId(newLotId)
    onSelectSite?.(newLotId)
  }

  // Sync current PDF when selected lot changes, inheriting all-sites policy if site has none
  useEffect(() => {
    if (user) {
      setSectionDocuments(getOwnerPolicySectionDocuments(user.id, selectedLotId))
      setSectionTexts(getOwnerPolicySectionTexts(user.id, selectedLotId))
      setSectionDrafts(policyDrafts(user.id, selectedLotId))
      setImportedSectionIds([])
    }
    if (selectedLotId === "all") {
      const ownerPdf = user ? store.getOwnerPolicyPdf(user.id) : null
      setCurrentPdf(ownerPdf)
    } else {
      const lotPdf = store.getLotPolicyPdf(selectedLotId, user?.id)
      const ownerPdf = user ? store.getOwnerPolicyPdf(user.id) : null
      if ((!lotPdf || !lotPdf.fileData) && ownerPdf?.fileData) {
        const lot = ownerLots.find((l) => l.id === selectedLotId)
        setCurrentPdf({
          ...ownerPdf,
          lotId: selectedLotId,
          title: `Parking Policies & Rules: ${lot?.name || "SmartParking"}`,
        })
      } else {
        setCurrentPdf(lotPdf)
      }
    }
  }, [selectedLotId, user, ownerLots])

  async function handleFileSelect(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file || !user) return
    e.currentTarget.value = ""

    if (
      !file.type.includes("pdf") &&
      !file.name.toLowerCase().endsWith(".pdf")
    ) {
      setPdfNotice("Please select a valid PDF file (.pdf).")
      return
    }

    let importedSections: Partial<Record<OwnerPolicySectionId, string>> = {}
    try {
      importedSections = extractOwnerPolicyPdfSections(await file.arrayBuffer())
    } catch {
      setPdfNotice("The PDF could not be read. Select it again.")
      return
    }
    const importedIds = Object.keys(importedSections) as OwnerPolicySectionId[]

    const reader = new FileReader()
    reader.onload = (event) => {
      const dataUrl = event.target?.result as string
      const lot = ownerLots.find((l) => l.id === selectedLotId)

      const payload: LotPolicyPdf = {
        lotId: selectedLotId === "all" ? undefined : selectedLotId,
        ownerId: user.id,
        fileName: file.name,
        fileSize: `${(file.size / 1024).toFixed(0)} KB`,
        fileData: dataUrl,
        uploadedAt: new Date().toISOString(),
        title:
          selectedLotId === "all"
            ? "Owner Policies: All Parking Sites"
            : `Owner Policies: ${lot?.name || "SmartParking"}`,
        summaryText: `Official updated rules for ${lot?.name || "all of the owner's parking sites"}.`,
      }

      try {
        if (selectedLotId === "all") {
          store.saveOwnerPolicyPdf(user.id, payload)
        } else {
          store.saveLotPolicyPdf(selectedLotId, payload)
        }
      } catch {
        setPdfNotice("The PDF could not be saved in this browser. Try a smaller file.")
        return
      }

      setCurrentPdf(payload)
      if (importedIds.length) {
        setSectionDrafts(current => ({ ...current, ...importedSections }))
        setImportedSectionIds(importedIds)
      }
      setPdfNotice(importedIds.length
        ? `Uploaded "${file.name}". ${importedIds.length} template sections were loaded as drafts. Review and save each section to publish its text.`
        : `Uploaded "${file.name}". Drivers can view the PDF; use the SmartParking template to import editable sections.`)
      setTimeout(() => setPdfNotice(null), 5000)

      store.addAuditLog({
        userId: user.id,
        userName: user.name,
        userRole: "owner",
        action: "POLICY_PDF_UPLOADED",
        details: `Uploaded policy PDF ${file.name} for ${
          selectedLotId === "all" ? "all lots" : lot?.name
        }`,
      })

      store.notifyRole(
        "driver",
        "POLICY_UPDATED",
        "Parking Policy Updated",
        `Parking site ${lot?.name || "SmartParking"} has updated its operations policy document.`,
      )
    }

    reader.onerror = () => setPdfNotice("The PDF could not be read. Select it again.")
    reader.readAsDataURL(file)
  }

  function handleDeletePdf() {
    if (!user) return
    if (
      confirm(
        "Delete this policy PDF and restore the default policy?",
      )
    ) {
      if (selectedLotId === "all") {
        localStorage.removeItem(`sp_owner_policy_pdf_${user.id}`)
        setCurrentPdf(null)
      } else {
        store.deleteLotPolicyPdf(selectedLotId)
        const ownerPdf = store.getOwnerPolicyPdf(user.id)
        if (ownerPdf?.fileData) {
          const lot = ownerLots.find((l) => l.id === selectedLotId)
          setCurrentPdf({
            ...ownerPdf,
            lotId: selectedLotId,
            title: `Parking Policies & Rules: ${lot?.name || "SmartParking"}`,
          })
        } else {
          const fallback = store.getLotPolicyPdf(selectedLotId, user.id)
          setCurrentPdf(fallback)
        }
      }
      setPdfNotice(
        "General owner PDF removed. An all-sites owner PDF applies where available.",
      )
      setTimeout(() => setPdfNotice(null), 4000)
    }
  }

  function handleSectionDelete(section: OwnerPolicySectionId) {
    if (!user || !confirm(`Remove the ${ownerPolicySections.find(item => item.id === section)?.title} PDF?`)) return
    deleteOwnerPolicySectionDocument(user.id, selectedLotId, section)
    setSectionDocuments(getOwnerPolicySectionDocuments(user.id, selectedLotId))
    setPdfNotice("Section PDF removed.")
  }

  function handleSectionTextSave(section: OwnerPolicySectionId) {
    if (!user) return
    const content = sectionDrafts[section].trim()
    if (!content) { setPdfNotice("Enter policy text before saving this section."); return }
    try {
      saveOwnerPolicySectionText(user.id, selectedLotId, section, content)
      setSectionTexts(getOwnerPolicySectionTexts(user.id, selectedLotId))
      setImportedSectionIds(current => current.filter(id => id !== section))
      setPdfNotice(`${ownerPolicySections.find(item => item.id === section)?.title} saved for ${selectedLotId === "all" ? "all sites" : "this site"}.`)
    } catch {
      setPdfNotice("The policy text could not be saved in this browser. Try again.")
    }
  }

  function handleSectionTextDelete(section: OwnerPolicySectionId) {
    if (!user || !confirm(`Remove the saved text for ${ownerPolicySections.find(item => item.id === section)?.title}?`)) return
    deleteOwnerPolicySectionText(user.id, selectedLotId, section)
    setSectionTexts(getOwnerPolicySectionTexts(user.id, selectedLotId))
    setSectionDrafts(policyDrafts(user.id, selectedLotId))
    setPdfNotice("Saved section text removed. The starter draft is available for editing.")
  }

  function handleOpenPreview(source: "owner" | "platform" = "owner", platformSection: "terms" | "privacy" | "payment-refund" = "terms") {
    setPreviewSource(source)
    setPreviewPlatformSection(platformSection)
    const lot = ownerLots.find((l) => l.id === selectedLotId) ||
      ownerLots[0] || {
        id: "sample",
        name: "SmartParking Sample Parking Site",
        address: "72 Lê Thánh Tôn, Quận 1, TP.HCM",
        type: "basement",
        slots: [],
        totalSlots: 100,
        floors: 2,
        devices: [],
        hourlyRate: Number(policy.dayRate) || 15000,
        dailyRate: 150000,
        nightRate: Number(policy.nightRate) || 25000,
        gracePeriodMinutes: 15,
        subscriptionMonthly: 1200000,
        subscriptionYearly: 12000000,
        status: "active",
        lat: 10.7769,
        lng: 106.7009,
        ownerId: user?.id || "",
        createdAt: new Date().toISOString(),
      }
    setPreviewLot(lot as ParkingLot)
  }

  useEffect(() => {
    const dayCost = policy.pricingEnabled
      ? (Number(policy.dayRate) || 0) * (Number(dayHours) || 0)
      : 0

    const nightCost = policy.pricingEnabled
      ? (Number(policy.nightRate) || 0) * (Number(nightHours) || 0)
      : 0

    const total = dayCost + nightCost

    // Calculate the advance deposit as a percentage of the total or as a fixed amount.

    const requestedDeposit = !policy.depositEnabled
      ? 0
      : policy.depositMode === "percentage"
        ? total * ((Number(policy.depositValue) || 0) / 100)
        : Number(policy.depositValue) || 0

    const deposit = Math.min(total, Math.max(0, requestedDeposit))

    // Checkout collects the remaining balance after applying the advance deposit.

    const checkout = Math.max(0, total - deposit)

    setPreview({ total, deposit, checkout })
  }, [policy, dayHours, nightHours])

  const updatePolicy = <K extends keyof PolicyState,>(
    key: K,
    value: PolicyState[K],
  ) => {
    setSaved(false)

    setPolicy((current) => ({ ...current, [key]: value }))
  }

  function stepRate(key: "dayRate" | "nightRate", direction: -1 | 1) {
    const current = Number(policy[key]) || 0
    updatePolicy(key, String(Math.max(0, Math.round(current / 1000) * 1000 + direction * 1000)))
  }

  function savePolicy() {
    if (!user) return

    store.saveOwnerPolicy(user.id, policy)

    if (policy.pricingEnabled) {
      store.getLotsByOwner(user.id).forEach((lot) =>
        store.saveLot({
          ...lot,
          hourlyRate: Number(policy.dayRate) || 0,
          nightRate: Number(policy.nightRate) || 0,
        }),
      )
    }

    store.createNotification({
      recipientId: user.id,
      recipientRole: "owner",
      type: "POLICY_UPDATED",
      title: "Parking policy updated",
      message: "The parking policy has been updated successfully.",
    })

    setSaved(true)
  }

  return (
    <section
      className="mx-auto w-full max-w-5xl space-y-6"
      aria-labelledby="policy-settings-title"
    >
      <PageHeader id="policy-settings-title" eyebrow="Owner tools" title="Policy Settings" description="Manage owner documents by topic and review separate SmartParking platform policies." />

      {pdfNotice && (
        <div
          role="status"
          className="animate-in flex items-center gap-2 rounded-lg border border-green-500/30 bg-green-500/10 px-4 py-3 text-sm font-medium text-green-500"
        >
          <UntitledIcon name="check" size={16} />
          <span>{pdfNotice}</span>
        </div>
      )}

      {/* PDF Policy Document Upload & Management Section */}
      <section
        className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5 sm:p-6 space-y-4"
        aria-label="Owner policy documents"
      >
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 border-b border-[var(--border)] pb-4">
          <div>
            <div className="flex items-center gap-2">
              <h2 className="text-base font-bold text-[var(--fg)]">
                Owner Policies · Single PDF
              </h2>
              <span className="rounded px-2 py-0.5 text-xs font-semibold bg-blue-500/15 text-blue-500">
                Owner document
              </span>
            </div>
            <p className="mt-1 text-xs text-[var(--muted)]">
              Complete the Owner policy template, upload one PDF, then review the imported section drafts below.
            </p>
          </div>

          {/* Scope / Lot selector */}
          <div className="flex items-center gap-2">
            <label
              htmlFor="policy-lot-scope"
              className="text-xs font-medium text-[var(--muted)] whitespace-nowrap"
            >
              Applies to:
            </label>
            <RoundedSelect
              id="policy-lot-scope"
              className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-1.5 text-xs text-[var(--fg)] outline-none"
              value={selectedLotId}
              onChange={(e) => handleLotChange(e.target.value)}
            >
              <option value="all">All Sites (Default)</option>
              {ownerLots.map((lot) => (
                <option key={lot.id} value={lot.id}>
                  {lot.name}
                </option>
              ))}
            </RoundedSelect>
          </div>
        </div>

        {/* Current Document Status Card */}
        <div className="rounded-lg border border-[var(--border)] bg-[var(--bg)] p-4 flex flex-col md:flex-row md:items-center md:justify-between gap-4">
          <div className="flex items-center gap-3">
            <div className="flex h-12 w-12 flex-shrink-0 items-center justify-center rounded-lg bg-red-500/10 text-red-500 font-bold text-xs border border-red-500/20">
              PDF
            </div>
            <div>
              <div className="flex items-center gap-2">
                <span className="font-semibold text-sm text-[var(--fg)]">
                  {currentPdf?.fileData ? currentPdf.fileName : "Owner policy PDF template"}
                </span>
                <span
                  className={`rounded px-1.5 py-0.5 text-[11px] font-semibold ${
                    currentPdf?.fileData
                      ? "bg-green-500/15 text-green-500"
                      : "bg-[var(--raised)] text-[var(--muted)]"
                  }`}
                >
                  {currentPdf?.fileData
                    ? "Uploaded File"
                    : "Template ready"}
                </span>
              </div>
              {currentPdf?.fileData && <p className="mt-1 text-xs text-[var(--muted)]">{currentPdf.fileSize ? `Size: ${currentPdf.fileSize} · ` : ""}Updated: {new Date(currentPdf.uploadedAt).toLocaleDateString("en-US")}</p>}
            </div>
          </div>

          {/* Action buttons */}
          <div className="flex flex-wrap items-center gap-2">
            <input
              type="file"
              ref={fileInputRef}
              accept=".pdf,application/pdf"
              onChange={handleFileSelect}
              className="hidden"
            />
            <button
              type="button"
              className="btn-primary text-xs py-2 px-3.5"
              onClick={() => fileInputRef.current?.click()}
            >
              <UntitledIcon name="upload" size={14} /> Upload New PDF
            </button>
            <button
              type="button"
              className="btn-outline text-xs py-2 px-3"
              onClick={() => handleOpenPreview()}
            >
              <UntitledIcon name="eye" size={14} /> Preview PDF
            </button>
            {currentPdf?.fileData && (
              <>
                <a
                  href={currentPdf.fileData}
                  download={currentPdf.fileName}
                  className="btn-outline text-xs py-2 px-3 no-underline inline-flex items-center gap-1.5"
                >
                  <UntitledIcon name="download" size={14} /> Download
                </a>
                <button
                  type="button"
                  className="rounded-lg border border-red-500/30 bg-red-500/10 px-3 py-2 text-xs font-semibold text-red-500 hover:bg-red-500/20"
                  onClick={handleDeletePdf}
                >
                  Delete
                </button>
              </>
            )}
          </div>
        </div>

        <div className="overflow-hidden rounded-xl border border-[var(--border)] bg-[var(--bg)]">
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-[var(--border)] px-4 py-3">
            <div><h3 className="text-sm font-semibold text-[var(--fg)]">{currentPdf?.fileData ? "Current Owner PDF" : "Owner policy PDF template"}</h3>{!currentPdf?.fileData && <p className="mt-1 text-xs text-[var(--muted)]">Complete this starter PDF, then upload it to publish your Owner policy.</p>}</div>
            <a className="btn-outline inline-flex items-center gap-2 no-underline" href="/templates/owner-policy-template.pdf" download="smartparking-owner-policy-template.pdf"><UntitledIcon name="download" size={15} /> Download template</a>
          </div>
          <iframe className="h-[360px] w-full bg-[var(--card)] sm:h-[480px]" title={currentPdf?.fileData ? "Current Owner policy PDF" : "Owner policy PDF template"} src={currentPdf?.fileData || "/templates/owner-policy-template.pdf"} />
        </div>
        <p className="text-xs text-[var(--muted)] flex items-center gap-1.5">
          <UntitledIcon name="check" size={13} />
          Owner PDFs saved in this browser appear in the Driver policy viewer for the same browser profile.
        </p>
      </section>

      <section className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5 sm:p-6" aria-labelledby="owner-policy-sections-heading">
        <h2 id="owner-policy-sections-heading" className="text-lg font-semibold text-[var(--fg)]">Owner Policies by Section</h2>
        <p className="mt-1 text-sm text-[var(--muted)]">Edit the starter text directly on this page, then save each section. Site-specific content takes priority over the all-sites version.</p>
        <p className="mt-1 text-xs text-[var(--muted)]">Saved text is visible to Drivers in this browser profile.</p>
        <div className="mt-5 grid gap-3 sm:grid-cols-2">
          {ownerPolicySections.map(section => {
            const document = sectionDocuments[section.id]
            const inherited = selectedLotId !== "all" && document && !document.lotId
            const savedText = sectionTexts[section.id]
            const inheritedText = selectedLotId !== "all" && savedText && !savedText.lotId
            return <article key={section.id} className="min-w-0 rounded-xl border border-[var(--border)] bg-[var(--bg)] p-4">
              <h3 className="font-semibold text-[var(--fg)]">{section.title}</h3>
              <p className="mt-1 text-sm text-[var(--muted)]">{section.description}</p>
              <label htmlFor={`owner-policy-text-${section.id}`} className="mt-4 block text-sm font-semibold text-[var(--fg)]">Policy text</label>
              <textarea id={`owner-policy-text-${section.id}`} className="input mt-2 min-h-36 w-full resize-y rounded-xl text-sm leading-6" rows={5} value={sectionDrafts[section.id]} onChange={event => setSectionDrafts(current => ({ ...current, [section.id]: event.target.value }))} aria-describedby={`owner-policy-state-${section.id}`} />
              <p id={`owner-policy-state-${section.id}`} className="mt-1 text-xs text-[var(--muted)]">{importedSectionIds.includes(section.id) ? "Imported from PDF · review and save to publish this text." : savedText ? inheritedText ? "Published from all sites; save to override for this site." : "Saved for this scope." : "Starter draft · not visible to Drivers until saved."}</p>
              <div className="mt-3 flex flex-wrap gap-2"><button type="button" className="btn-primary" onClick={() => handleSectionTextSave(section.id)}>Save section text</button>{savedText && !inheritedText && <button type="button" className="btn-ghost" onClick={() => handleSectionTextDelete(section.id)}>Remove saved text</button>}</div>
              {document?.fileData && <div className="mt-4 flex flex-wrap items-center gap-2 border-t border-[var(--border)] pt-4"><span className="text-xs text-[var(--muted)]">Previously attached section PDF{inherited ? " · From all sites" : ""}</span><a className="btn-outline no-underline" href={document.fileData} target="_blank" rel="noopener noreferrer">View PDF</a>{!inherited && <button type="button" className="btn-ghost" onClick={() => handleSectionDelete(section.id)}>Remove</button>}</div>}
            </article>
          })}
        </div>
      </section>

      <section className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5 sm:p-6" aria-labelledby="platform-policy-heading">
        <h2 id="platform-policy-heading" className="text-lg font-semibold text-[var(--fg)]">Platform Policies</h2>
        <p className="mt-1 text-sm text-[var(--muted)]">SmartParking manages these documents. Owners can review them here; site uploads do not replace them.</p>
        <div className="mt-4 flex flex-wrap gap-2">{([{ label: "Terms of Service", view: "terms" }, { label: "Privacy Policy", view: "privacy" }, { label: "Payment & Refund", view: "payment-refund" }] as const).map(item => <button key={item.view} type="button" className="btn-outline" onClick={() => handleOpenPreview("platform", item.view)}>{item.label}</button>)}</div>
      </section>

      <section
        className="overflow-hidden rounded-xl border border-[var(--border)] bg-[var(--card)]"
        aria-label="Pricing and deposit policy settings"
      >
        <div className="overflow-x-auto">
          <table className="w-full min-w-[680px] border-collapse text-left">
            <thead>
              <tr className="border-b border-[var(--border)] text-sm text-[var(--muted)]">
                <th className="px-5 py-3 font-semibold">Policy</th>
                <th className="px-5 py-3 font-semibold">Enabled</th>
                <th className="px-5 py-3 font-semibold">Configuration</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[var(--border)]">
              <tr>
                <td className="px-5 py-4 align-top">
                  <span className="block font-semibold text-[var(--fg)]">
                    Pricing Policy
                  </span>
                  <span className="mt-1 block text-xs text-[var(--muted)]">
                    Day and night rates
                  </span>
                </td>
                <td className="px-5 py-4 align-top">
                  <input
                    type="checkbox"
                    className="mt-1 h-4 w-4 accent-[var(--primary)]"
                    aria-label="Enable pricing policy"
                    checked={policy.pricingEnabled}
                    onChange={(event) =>
                      updatePolicy("pricingEnabled", event.target.checked)
                    }
                  />
                </td>
                <td className="px-5 py-4">
                  {policy.pricingEnabled ? (
                    <div className="grid gap-3 sm:grid-cols-2">
                      <div className="space-y-1.5 text-xs font-medium text-[var(--muted)]">
                        <span className="block">Day Rate (VND / hour)</span>
                        <div className="flex items-center gap-2"><button type="button" className="btn-outline shrink-0" aria-label="Decrease day rate by 1,000 VND" onClick={() => stepRate("dayRate", -1)} disabled={Number(policy.dayRate) <= 0}>−</button><span className="min-w-0 flex-1"><GroupedNumberInput className={inputClass} ariaLabel="Day rate in VND per hour" step={1000} value={policy.dayRate} onValueChange={(digits) => updatePolicy("dayRate", digits.slice(0, 9))} /></span><button type="button" className="btn-outline shrink-0" aria-label="Increase day rate by 1,000 VND" onClick={() => stepRate("dayRate", 1)}>+</button></div>
                      </div>
                      <div className="space-y-1.5 text-xs font-medium text-[var(--muted)]">
                        <span className="block">Night Rate (VND / hour)</span>
                        <div className="flex items-center gap-2"><button type="button" className="btn-outline shrink-0" aria-label="Decrease night rate by 1,000 VND" onClick={() => stepRate("nightRate", -1)} disabled={Number(policy.nightRate) <= 0}>−</button><span className="min-w-0 flex-1"><GroupedNumberInput className={inputClass} ariaLabel="Night rate in VND per hour" step={1000} value={policy.nightRate} onValueChange={(digits) => updatePolicy("nightRate", digits.slice(0, 9))} /></span><button type="button" className="btn-outline shrink-0" aria-label="Increase night rate by 1,000 VND" onClick={() => stepRate("nightRate", 1)}>+</button></div>
                      </div>
                    </div>
                  ) : (
                    <span className="text-sm text-[var(--muted)]">
                      Pricing is disabled
                    </span>
                  )}
                </td>
              </tr>
              <tr>
                <td className="px-5 py-4 align-top">
                  <span className="block font-semibold text-[var(--fg)]">
                    Deposit Policy
                  </span>
                  <span className="mt-1 block text-xs text-[var(--muted)]">
                    Advance payment
                  </span>
                </td>
                <td className="px-5 py-4 align-top">
                  <input
                    type="checkbox"
                    className="mt-1 h-4 w-4 accent-[var(--primary)]"
                    aria-label="Enable deposit policy"
                    checked={policy.depositEnabled}
                    onChange={(event) =>
                      updatePolicy("depositEnabled", event.target.checked)
                    }
                  />
                </td>
                <td className="px-5 py-4">
                  {policy.depositEnabled ? (
                    <div className="grid gap-3 sm:grid-cols-2">
                      <label className="space-y-1.5 text-xs font-medium text-[var(--muted)]">
                        Deposit type
                        <RoundedSelect
                          className={inputClass}
                          value={policy.depositMode}
                          onChange={(event) =>
                            updatePolicy(
                              "depositMode",
                              event.target.value as DepositMode,
                            )
                          }
                        >
                          <option value="percentage">Percentage (%)</option>
                          <option value="fixed">Fixed amount (VND)</option>
                        </RoundedSelect>
                      </label>
                      <label className="space-y-1.5 text-xs font-medium text-[var(--muted)]">
                        {policy.depositMode === "percentage"
                          ? "Deposit Percentage (%)"
                          : "Deposit Amount (VND)"}
                        <input
                          className={inputClass}
                          type="number"
                          min="0"
                          max={
                            policy.depositMode === "percentage"
                              ? 100
                              : 999999000
                          }
                          step={policy.depositMode === "percentage" ? 1 : 1000}
                          value={policy.depositValue}
                          onChange={(event) => {
                            const digits = event.target.value.replace(/\D/g, "")
                            updatePolicy("depositValue", policy.depositMode === "percentage" ? (digits ? String(Math.min(100, Number(digits))) : "") : digits.slice(0, 9))
                          }}
                          onBlur={() => { if (policy.depositMode === "fixed") updatePolicy("depositValue", String(Math.min(999999000, Math.round((Number(policy.depositValue) || 0) / 1000) * 1000))) }}
                        />
                      </label>
                    </div>
                  ) : (
                    <span className="text-sm text-[var(--muted)]">
                      Deposit is disabled
                    </span>
                  )}
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div
          className="border-t border-[var(--border)] p-5"
          aria-labelledby="policy-preview-title"
        >
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <h2
                id="policy-preview-title"
                className="text-lg font-semibold text-[var(--fg)]"
              >
                Payment Preview
              </h2>
              <p className="mt-1 text-sm text-[var(--muted)]">
                Adjust the sample stay duration to preview the calculation.
              </p>
            </div>
            <span className="rounded-full bg-[var(--primary)]/10 px-3 py-1 text-xs font-semibold text-[var(--primary)]">
              Preview only
            </span>
          </div>
          <div className="mt-5 grid gap-4 sm:grid-cols-2">
            <label className="space-y-2 text-sm font-medium text-[var(--fg)]">
              Day hours
              <input
                className={inputClass}
                type="number"
                min="0"
                step="1"
                value={dayHours}
                onChange={(event) => setDayHours(event.target.value)}
              />
            </label>
            <label className="space-y-2 text-sm font-medium text-[var(--fg)]">
              Night hours
              <input
                className={inputClass}
                type="number"
                min="0"
                step="1"
                value={nightHours}
                onChange={(event) => setNightHours(event.target.value)}
              />
            </label>
          </div>
          <p className="mt-3 text-sm leading-6 text-[var(--muted)]">
            Total = (day rate × day hours) + (night rate × night hours). Deposit
            is capped at the total; checkout collects the remaining balance.
          </p>
          <div className="mt-5 grid gap-3 border-t border-[var(--border)] pt-4 sm:grid-cols-3">
            <PreviewAmount label="Estimated total" amount={preview.total} />
            <PreviewAmount label="Deposit due now" amount={preview.deposit} />
            <PreviewAmount
              label="Due at checkout"
              amount={preview.checkout}
              emphasized
            />
          </div>
          <div className="mt-4 flex items-center justify-end gap-3 border-t border-[var(--border)] pt-4">
            {saved && (
              <span
                role="status"
                className="text-sm font-medium text-green-600"
              >
                Policy saved.
              </span>
            )}
            <button type="button" className="btn-primary" onClick={savePolicy}>
              Save Policy
            </button>
          </div>
        </div>
      </section>

      {/* Policy document preview modal */}
      {previewLot && (
        <LotPolicyModal
          lot={previewLot}
          onClose={() => setPreviewLot(null)}
          initialSource={previewSource}
          initialPlatformSection={previewPlatformSection}
        />
      )}
    </section>
  )
}

function PreviewAmount({
  label,
  amount,
  emphasized = false,
}: {
  label: string
  amount: number
  emphasized?: boolean
}) {
  return (
    <div className="rounded-lg bg-[var(--bg)] p-4">
      <p className="text-sm text-[var(--muted)]">{label}</p>
      <p
        className={`mt-2 text-lg font-bold ${
          emphasized ? "text-[var(--primary)]" : "text-[var(--fg)]"
        }`}
      >
        {Math.round(amount).toLocaleString("vi-VN")} VND
      </p>
    </div>
  )
}
