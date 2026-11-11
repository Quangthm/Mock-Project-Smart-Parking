import { useEffect, useState } from "react"
import { LotPolicyModal } from "../../../components/modals/LotPolicyModal"
import { PageHeader } from "../../../components/ui/PageHeader"
import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { useApp } from "../../../context/AppContext"
import { parkingApi, type Site } from "../../../lib/parkingApi"
import { PolicyDraftEditor, policyPanelClass, policyTopics, starterPolicyDraft, type PolicyDraft } from "./PolicyDraftEditor"
import { PolicyPencil } from "./PolicyPencil"

type PlatformSection = "terms" | "privacy" | "payment-refund"
type DraftFilter = "all" | "default" | "custom"
type SiteResult = { status: "loading" | "error" } | { status: "ready"; sites: Site[] }
const emptyDraft: PolicyDraft = {}

export function PolicySettings() {
  const { user } = useApp()
  return user?.id ? <OwnerPolicyWorkspace key={user.id} /> : null
}

function OwnerPolicyWorkspace() {
  const [result, setResult] = useState<SiteResult>({ status: "loading" })
  const [retry, setRetry] = useState(0)
  const [search, setSearch] = useState("")
  const [filter, setFilter] = useState<DraftFilter>("all")
  const [defaults, setDefaults] = useState<PolicyDraft>(starterPolicyDraft)
  const [overrides, setOverrides] = useState<Record<string, PolicyDraft>>({})
  const [editor, setEditor] = useState<{ site?: Site } | null>(null)
  const [platform, setPlatform] = useState<PlatformSection>()
  const [notice, setNotice] = useState("")

  useEffect(() => {
    let current = true
    parkingApi.list().then(
      sites => { if (current) setResult({ status: "ready", sites }) },
      () => { if (current) setResult({ status: "error" }) },
    )
    return () => { current = false }
  }, [retry])

  function keepDraft(draft: PolicyDraft) {
    const site = editor?.site
    if (site) setOverrides(current => ({ ...current, [site.id]: draft }))
    else setDefaults(draft)
    setEditor(null)
    setNotice("Draft kept on this page. Nothing was published.")
  }

  const sites = result.status === "ready" ? result.sites : []
  const query = search.trim().toLowerCase()
  const visibleSites = sites.filter(site => {
    const customized = Object.keys(overrides[site.id] ?? emptyDraft).length > 0
    return site.name.toLowerCase().includes(query) && (filter === "all" || (filter === "custom" ? customized : !customized))
  })

  return <section aria-labelledby="policy-settings-title" className="grid min-w-0 gap-5 text-[var(--fg)]">
    <PageHeader id="policy-settings-title" eyebrow="Owner tools" title="Policy Settings" description="Prepare one default policy and customize individual parking lots." />

    <section className={policyPanelClass} aria-labelledby="default-policy-heading">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 id="default-policy-heading" className="text-lg font-semibold">Default Policy</h2>
          <p className="mt-1 text-sm text-[var(--muted)]">Vietnamese starter text is ready to edit. Changes carry over to fields that inherit this draft.</p>
        </div>
        <button type="button" className="btn-outline inline-flex items-center gap-2" title="Edit Policy" onClick={() => setEditor({})}><PolicyPencil /> Edit Default</button>
      </div>
      <dl className="mt-4 grid gap-3 sm:grid-cols-3">
        <div className="rounded-lg bg-[var(--bg)] p-3"><dt className="font-semibold">Parking Policies</dt><dd className="mt-1 text-sm text-[var(--muted)]">{policyTopics.filter(([key]) => defaults[key]).length} of {policyTopics.length} sections drafted</dd></div>
        <div className="rounded-lg bg-[var(--bg)] p-3"><dt className="font-semibold">Pricing</dt><dd className="mt-1 text-sm text-[var(--muted)]">Car and motorcycle rates, including overtime.</dd></div>
        <div className="rounded-lg bg-[var(--bg)] p-3"><dt className="font-semibold">Deposit</dt><dd className="mt-1 text-sm text-[var(--muted)]">{defaults.depositPercentage === undefined ? "Percentage not set" : defaults.depositPercentage + "% (draft)"}</dd></div>
      </dl>
      <p className="mt-3 text-sm text-[var(--muted)]">Blocked by Backend. Draft edits reset to the starter template when you leave this page.</p>
      <p role="status" className={notice ? "mt-2 text-sm" : "sr-only"}>{notice}</p>
    </section>

    <section className={policyPanelClass} aria-labelledby="lot-policy-heading">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 id="lot-policy-heading" className="text-lg font-semibold">Policies by Parking Lot</h2>
          <p className="mt-1 text-sm text-[var(--muted)]">The lots are live. The filter and labels describe draft settings.</p>
        </div>
        <div className="flex w-full flex-wrap gap-2 lg:w-auto">
          <input type="search" aria-label="Search by parking lot name" placeholder="Search parking lots…" className="input sp-search-input min-w-0 flex-1 px-3" value={search} onChange={event => setSearch(event.target.value)} />
          <RoundedSelect aria-label="Filter by draft policy type" value={filter} onChange={event => setFilter(event.target.value as DraftFilter)}>
            <option value="all">All drafts</option><option value="default">Default draft</option><option value="custom">Customized draft</option>
          </RoundedSelect>
        </div>
      </div>
      {result.status === "loading" && <p role="status" className="mt-4 text-sm">Loading parking lots…</p>}
      {result.status === "error" && <div role="alert" className="mt-4 flex flex-wrap items-center gap-3"><p>Could not load your parking lots.</p><button type="button" className="btn-outline" onClick={() => { setResult({ status: "loading" }); setRetry(value => value + 1) }}>Try again</button></div>}
      {result.status === "ready" && !visibleSites.length && <p className="mt-4 text-sm text-[var(--muted)]">{sites.length ? "No lots match your search or filter." : "You have no parking lots yet."}</p>}
      {visibleSites.length > 0 && <div role="table" aria-label="Parking lot policy drafts" className="mt-4 grid gap-2">
        <div role="row" className="sr-only md:not-sr-only md:grid md:grid-cols-[minmax(0,2fr)_1fr_1fr_1fr_auto] md:gap-3 md:px-3 md:text-sm md:font-semibold">
          {["Parking Lot", "Policy Type", "Pricing", "Deposit", "Action"].map(label => <span role="columnheader" key={label}>{label}</span>)}
        </div>
        {visibleSites.map(site => {
          const draft = overrides[site.id] ?? emptyDraft
          return <div key={site.id} role="row" className="grid grid-cols-[minmax(0,1fr)_auto] items-center gap-3 rounded-lg border border-[var(--border)] p-3 md:grid-cols-[minmax(0,2fr)_1fr_1fr_1fr_auto]">
            <div role="cell" className="min-w-0 break-words"><p className="font-semibold">{site.name}</p><p className="text-sm text-[var(--muted)]">{site.address}</p></div>
            <span role="cell" className="col-start-1 text-sm md:col-start-auto">{Object.keys(draft).length ? "Customized draft" : "Default draft"}</span>
            <span role="cell" className="col-start-1 text-sm text-[var(--muted)] md:col-start-auto"><span className="md:hidden">Pricing · </span>{Object.keys(draft).some(key => key.startsWith("Car") || key.startsWith("Motorcycle")) ? "Custom" : "Inherited"}</span>
            <span role="cell" className="col-start-1 text-sm text-[var(--muted)] md:col-start-auto"><span className="md:hidden">Deposit · </span>{draft.depositPercentage !== undefined ? "Custom" : "Inherited"}</span>
            <div role="cell" className="col-start-2 row-start-1 md:col-start-auto md:row-start-auto"><button type="button" className="btn-outline grid w-11 place-items-center px-0!" title="Edit Policy" aria-label={"Edit policy for " + site.name} onClick={() => setEditor({ site })}><PolicyPencil /></button></div>
          </div>
        })}
      </div>}
    </section>

    <section id="platform-policy" className="policy-content-panel rounded-xl border border-[var(--border)] bg-[var(--card)] p-5 sm:p-6" aria-labelledby="platform-policy-heading">
      <h2 id="platform-policy-heading" className="text-lg font-semibold text-[var(--fg)]">Platform Policies</h2>
      <p className="mt-1 text-sm text-[var(--muted)]">SmartParking manages these documents. Owners can review them here; site uploads do not replace them.</p>
      <div className="mt-4 flex flex-wrap gap-2">{([{ label: "Terms of Service", view: "terms" }, { label: "Privacy Policy", view: "privacy" }, { label: "Payment & Refund", view: "payment-refund" }] as const).map(item => <button key={item.view} type="button" className="btn-outline" onClick={() => setPlatform(item.view)}>{item.label}</button>)}</div>
    </section>

    {editor && <PolicyDraftEditor site={editor.site} defaults={defaults} overrides={editor.site ? overrides[editor.site.id] ?? emptyDraft : emptyDraft} onClose={() => setEditor(null)} onKeep={keepDraft} />}
    {platform && <LotPolicyModal initialSource="platform" initialPlatformSection={platform} onClose={() => setPlatform(undefined)} />}
  </section>
}
