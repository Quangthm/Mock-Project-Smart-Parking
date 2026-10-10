import { useEffect, useMemo, useState } from "react"

import { useApp } from "../../../context/AppContext"

import { ownerData as store } from "../data/data"

import { OnboardingWizard } from "../parking-lots/CreateParkingLotForOwner"

import { OwnerOverview } from "./OwnerOverview"

import { LotManagement } from "../parking-lots/LotManagement"

import { OperatorManagement } from "../operators/OperatorManagement"

import { PolicySettings } from "../policies/PolicySettings"

import { SiteManagement } from "../parking-lots/SiteManagement"

import { RevenueDashboard } from "../revenue/RevenueDashboard"

import { ParkingStructure } from "../structure/ParkingStructure"

import { DashboardSidebar } from "../../../components/layout/DashboardSidebar"

type Tab = "dashboard" | "sites" | "lots" | "structure" | "operators" | "finance" | "policy"

export function OwnerDashboard() {
  const { user } = useApp()

  const [tab, setTab] = useState<Tab>("dashboard")

  const [selectedSiteId, setSelectedSiteId] = useState("all")

  const [siteRefresh, setSiteRefresh] = useState(0)

  const sites = useMemo(
    () => store.getLotsByOwner(user?.id ?? ""),
    [user?.id, siteRefresh],
  )

  const operators = useMemo(
    () =>
      store
        .getUsers()
        .filter(
          (operator) =>
            operator.role === "operator" && operator.ownerId === user?.id,
        ),
    [user?.id, siteRefresh],
  )

  useEffect(() => {
    const onNavigate = (event: Event) =>
      setTab((event as CustomEvent<Tab>).detail)

    window.addEventListener("sp:dashboard-nav", onNavigate)

    return () => window.removeEventListener("sp:dashboard-nav", onNavigate)
  }, [])

  if (!user?.onboardingComplete) {
    return (
      <div style={{ minHeight: "calc(100vh - 60px)", background: "var(--bg)" }}>
        <OnboardingWizard onComplete={() => setTab("dashboard")} />
      </div>
    )
  }

  return (
    <DashboardSidebar
      groups={[
        {
          items: [
            {
              id: "dashboard",
              label: "Dashboard",
              icon: "⌂",
              active: tab === "dashboard",
              onClick: () => setTab("dashboard"),
            },
          ],
        },

        {
          label: "Parking management",
          items: [
            {
              id: "sites",
              label: "Sites",
              icon: "⌖",
              active: tab === "sites",
              onClick: () => setTab("sites"),
            },

            {
              id: "lots",
              label: "My parking lots",
              icon: "▦",
              active: tab === "lots",
              onClick: () => setTab("lots"),
            },

            {
              id: "structure",
              label: "Parking Structure",
              icon: "🏢",
              active: tab === "structure",
              onClick: () => setTab("structure"),
            },
          ],
        },

        {
          label: "Business",
          items: [
            {
              id: "operators",
              label: "Operators",
              icon: "♙",
              active: tab === "operators",
              onClick: () => setTab("operators"),
            },

            {
              id: "finance",
              label: "Financial",
              icon: "₫",
              active: tab === "finance",
              onClick: () => setTab("finance"),
            },

            {
              id: "policy",
              label: "Policies",
              icon: "⚖",
              active: tab === "policy",
              onClick: () => setTab("policy"),
            },
          ],
        },
      ]}
    >
      <main className="min-w-0 overflow-y-auto p-4 sm:p-7">
        {(tab === "dashboard" || tab === "finance") && (
          <div className="mb-6 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-[var(--border)] bg-[var(--card)] p-4">
            <div>
              <p className="text-xs font-medium uppercase tracking-wide text-[var(--muted)]">
                Multi-site
              </p>
              <p className="mt-1 font-semibold text-[var(--fg)]">
                Filter Owner Dashboard
              </p>
            </div>
            <label className="flex items-center gap-3 text-sm font-medium text-[var(--fg)]">
              <span>Site</span>
              <select
                className="input min-w-[210px]"
                value={selectedSiteId}
                onChange={(event) => setSelectedSiteId(event.target.value)}
              >
                <option value="all">All Sites</option>
                {sites.map((site) => (
                  <option key={site.id} value={site.id}>
                    {site.name}
                  </option>
                ))}
              </select>
            </label>
          </div>
        )}
        <div className="animate-in">
          {tab === "dashboard" && (
            <OwnerOverview selectedSiteId={selectedSiteId} sites={sites} />
          )}
          {tab === "sites" && (
            <SiteManagement
              sites={sites}
              operators={operators}
              onSiteCreated={() => setSiteRefresh((value) => value + 1)}
            />
          )}
          {tab === "lots" && <LotManagement siteFilterId={selectedSiteId} />}
          {tab === "structure" && (
            <ParkingStructure sites={sites} selectedSiteId={selectedSiteId} />
          )}
          {tab === "operators" && (
            <OperatorManagement
              sites={sites}
              selectedSiteId={selectedSiteId}
              onOperatorsChanged={() => setSiteRefresh((value) => value + 1)}
            />
          )}
          {tab === "finance" && (
            <RevenueDashboard selectedSiteId={selectedSiteId} sites={sites} />
          )}
          {tab === "policy" && (
            <PolicySettings
              selectedSiteId={selectedSiteId}
              onSelectSite={setSelectedSiteId}
            />
          )}
        </div>
      </main>
    </DashboardSidebar>
  )
}
