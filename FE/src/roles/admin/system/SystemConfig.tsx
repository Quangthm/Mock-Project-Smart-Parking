import { useState } from "react"

import { UntitledIcon } from "../../../components/icon/UntitledIcon"

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

  function save() {
    setSaved(true)
    setTimeout(() => setSaved(false), 2000)
  }

  return (
    <div style={{ maxWidth: "700px" }}>
      <h2
        style={{
          fontFamily: "Outfit",
          fontWeight: 700,
          fontSize: "1.3rem",
          marginBottom: "1.5rem",
          color: "var(--fg)",
        }}
      >
        System Configuration
      </h2>
      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
        <section className="card">
          <h3
            style={{
              fontFamily: "Outfit",
              fontWeight: 600,
              fontSize: "0.95rem",
              marginBottom: "1rem",
              color: "var(--fg)",
            }}
          >
            Device Connectivity
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
                  checked={(config as any)[d.key]}
                  onChange={(e) =>
                    setConfig((c) => ({ ...c, [d.key]: e.target.checked }))
                  }
                />
                <span
                  style={{
                    fontSize: "0.8rem",
                    color: (config as any)[d.key] ? "#22c55e" : "#ef4444",
                    fontWeight: 600,
                  }}
                >
                  {(config as any)[d.key] ? "Online" : "Offline"}
                </span>
              </label>
            </div>
          ))}
        </section>
        <section className="card">
          <h3
            style={{
              fontFamily: "Outfit",
              fontWeight: 600,
              fontSize: "0.95rem",
              marginBottom: "1rem",
              color: "var(--fg)",
            }}
          >
            Platform Parameters
          </h3>
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "1fr 1fr",
              gap: "0.875rem",
            }}
          >
            <div>
              <label className="label">Platform Fee (%)</label>
              <input
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
              <label className="label">Default Grace Period (min)</label>
              <input
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
            "Save Configuration"
          )}
        </button>
      </div>
    </div>
  )
}
