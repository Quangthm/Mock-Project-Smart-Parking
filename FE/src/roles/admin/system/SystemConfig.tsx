import { useState } from "react"

import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { PageHeader } from "../../../components/ui/PageHeader"
import { AdminPolicyConsole } from "./AdminPolicyConsole"
import { EffectivePolicyView } from "./EffectivePolicyView"
import { IntegrationSettingsView } from "./IntegrationSettingsView"

type TabId = "policies" | "effective" | "integrations" | "devices" | "parameters"

const TABS: Array<{ id: TabId; label: string; icon: string }> = [
  { id: "policies", label: "Operational Policies", icon: "clipboard" },
  { id: "effective", label: "Effective Policy", icon: "target" },
  { id: "integrations", label: "Integrations", icon: "link" },
  { id: "devices", label: "Device Connectivity", icon: "link" },
  { id: "parameters", label: "Platform Parameters", icon: "sliders" },
]

export function SystemConfig() {
  const [config, setConfig] = useState({
    cameraAI: true,
    barrier: true,
    sensorNetwork: true,

    lotCapacity: 500,
    maintenanceMode: false,

    defaultGracePeriod: 15,
    platformFee: 15,
  })

  const [saved, setSaved] = useState(false)
  const [tab, setTab] = useState<TabId>("policies")

  function save() {
    setSaved(true)
    setTimeout(() => setSaved(false), 2000)
  }

  function onTabKeyDown(event: React.KeyboardEvent<HTMLButtonElement>, index: number) {
    if (event.key !== "ArrowRight" && event.key !== "ArrowLeft") return
    event.preventDefault()
    const next = (index + (event.key === "ArrowRight" ? 1 : -1) + TABS.length) % TABS.length
    setTab(TABS[next].id)
    document.getElementById(`system-tab-${TABS[next].id}`)?.focus()
  }

  return (
    <div className="system-config-page">
      <PageHeader
        title="System Configuration"
        variant="compact"
        description="Operational policies, integrations, device connectivity, and platform defaults."
      />

      <div
        role="tablist"
        aria-label="System configuration sections"
        className="mt-4 flex flex-wrap gap-1.5"
        style={{ border: "1px solid var(--border)", borderRadius: 999, background: "var(--card)", padding: 5, width: "fit-content", maxWidth: "100%" }}
      >
        {TABS.map((item, index) => {
          const active = tab === item.id
          return (
            <button
              key={item.id}
              type="button"
              role="tab"
              id={`system-tab-${item.id}`}
              aria-selected={active}
              aria-controls={`system-panel-${item.id}`}
              tabIndex={active ? 0 : -1}
              onClick={() => setTab(item.id)}
              onKeyDown={(event) => onTabKeyDown(event, index)}
              className="inline-flex items-center gap-2 rounded-full px-3.5 py-2 text-sm font-semibold transition-colors focus-visible:outline-2 focus-visible:outline-offset-2"
              style={{
                border: "none",
                cursor: "pointer",
                background: active ? "var(--primary)" : "transparent",
                color: active ? "var(--primary-fg)" : "var(--muted)",
              }}
            >
              <UntitledIcon name={item.icon} size={16} />
              {item.label}
            </button>
          )
        })}
      </div>

      <div
        key={tab}
        id={`system-panel-${tab}`}
        role="tabpanel"
        aria-labelledby={`system-tab-${tab}`}
        className="animate-in"
        style={{ marginTop: "1.25rem" }}
      >
        {tab === "policies" && <AdminPolicyConsole />}

        {tab === "effective" && <EffectivePolicyView />}

        {tab === "integrations" && <IntegrationSettingsView />}

        {tab === "devices" && (
          <div className="system-config-grid">
            <section className="card system-config-card">
              <h3
                style={{
                  fontFamily: "Be Vietnam Pro",
                  fontWeight: 600,
                  fontSize: "0.95rem",
                  marginBottom: "1rem",
                  color: "var(--fg)",
                }}
              >
                <UntitledIcon name="link" size={18} /> Device Connectivity
              </h3>
              {[
                {
                  key: "cameraAI",
                  label: "AI Camera System",
                  desc: "License plate recognition for all lots",
                },

                {
                  key: "barrier",
                  label: "Barrier Gates",
                  desc: "Automated entry/exit control",
                },

                {
                  key: "sensorNetwork",
                  label: "Sensor Network",
                  desc: "Real-time occupancy sensors",
                },
              ].map((d) => (
                <div
                  key={d.key}
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    padding: "0.625rem 0",
                    borderBottom: "1px solid var(--border)",
                  }}
                >
                  <div>
                    <div
                      style={{
                        fontWeight: 500,
                        fontSize: "0.875rem",
                        color: "var(--fg)",
                      }}
                    >
                      {d.label}
                    </div>
                    <div style={{ fontSize: "0.75rem", color: "var(--muted)" }}>
                      {d.desc}
                    </div>
                  </div>
                  <label
                    style={{
                      display: "flex",
                      alignItems: "center",
                      cursor: "pointer",
                      gap: "0.5rem",
                    }}
                  >
                    <input
                      type="checkbox"
                      checked={config[d.key as "cameraAI" | "barrier" | "sensorNetwork"]}
                      onChange={(e) =>
                        setConfig((c) => ({ ...c, [d.key]: e.target.checked }))
                      }
                    />
                    <span
                      style={{
                        fontSize: "0.8rem",
                        color: config[d.key as "cameraAI" | "barrier" | "sensorNetwork"] ? "var(--badge-success-fg)" : "var(--badge-danger-strong)",
                        fontWeight: 600,
                      }}
                    >
                      {config[d.key as "cameraAI" | "barrier" | "sensorNetwork"] ? "Online" : "Offline"}
                    </span>
                  </label>
                </div>
              ))}
            </section>
          </div>
        )}

        {tab === "parameters" && (
          <div className="system-config-grid">
            <section className="card system-config-card">
              <h3
                style={{
                  fontFamily: "Be Vietnam Pro",
                  fontWeight: 600,
                  fontSize: "0.95rem",
                  marginBottom: "1rem",
                  color: "var(--fg)",
                }}
              >
                <UntitledIcon name="sliders" size={18} /> Platform Parameters
              </h3>
              <div
                className="system-config-parameters"
                style={{
                  display: "grid",
                  gap: "0.875rem",
                }}
              >
                <div>
                  <label className="label" htmlFor="system-platform-fee">Platform Fee (%)</label>
                  <input
                    id="system-platform-fee"
                    className="input"
                    type="number"
                    min={0}
                    max={50}
                    value={config.platformFee}
                    onChange={(e) =>
                      setConfig((c) => ({ ...c, platformFee: +e.target.value }))
                    }
                  />
                </div>
                <div>
                  <label className="label" htmlFor="system-grace-period">Default Grace Period (min)</label>
                  <input
                    id="system-grace-period"
                    className="input"
                    type="number"
                    min={0}
                    max={60}
                    value={config.defaultGracePeriod}
                    onChange={(e) =>
                      setConfig((c) => ({
                        ...c,
                        defaultGracePeriod: +e.target.value,
                      }))
                    }
                  />
                </div>
              </div>
              <div
                style={{
                  marginTop: "0.875rem",
                  display: "flex",
                  alignItems: "center",
                  gap: "0.75rem",
                }}
              >
                <label
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: "0.5rem",
                    cursor: "pointer",
                    fontSize: "0.875rem",
                  }}
                >
                  <input
                    type="checkbox"
                    checked={config.maintenanceMode}
                    onChange={(e) =>
                      setConfig((c) => ({
                        ...c,
                        maintenanceMode: e.target.checked,
                      }))
                    }
                  />
                  <span
                    style={{
                      color: config.maintenanceMode ? "#ef4444" : "var(--fg)",
                      fontWeight: 500,
                    }}
                  >
                    Maintenance Mode
                  </span>
                </label>
                {config.maintenanceMode && (
                  <span
                    style={{
                      display: "inline-flex",
                      alignItems: "center",
                      gap: 4,
                      fontSize: "0.75rem",
                      color: "#ef4444",
                    }}
                  >
                    <UntitledIcon name="alert" size={14} /> Platform is in
                    maintenance
                  </span>
                )}
              </div>
            </section>
            <button
              className="btn-primary"
              style={{ alignSelf: "flex-start" }}
              onClick={save}
            >
              {saved ? (
                <>
                  <UntitledIcon name="check" size={16} /> Saved
                </>
              ) : (
                "Save Preview"
              )}
            </button>
          </div>
        )}
      </div>
    </div>
  )
}
