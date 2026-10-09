import { useState, useId } from "react"

import { useApp } from "../../../context/AppContext"

import { ownerData as store } from "../data/data"

import type { ParkingLot, LotType } from "../../../lib/types"

import { UntitledIcon } from "../../../components/icon/UntitledIcon"

import {
  ALLOWED_PARKING_MODELS,
  validateModelUpload,
  ALLOWED_MODEL_FILE_NAMES,
} from "../../../lib/3d/parking3DConfig"

type OnboardStep = 1 | 2 | 3 | 4 | 5

const LOT_TYPE_INFO: Record<LotType, {
  icon: string
  label: string
  hasFloors: boolean
}> = {
  outdoor: { icon: "🌳", label: "Outdoor Lot", hasFloors: false },

  basement: { icon: "🏢", label: "Basement Parking", hasFloors: true },

  "multi-storey": { icon: "🏗️", label: "Multi-Storey", hasFloors: true },
}

const DEVICE_OPTIONS = [
  {
    type: "camera",
    label: "AI Camera System",
    desc: "Automatic license plate recognition",
  },

  {
    type: "barrier",
    label: "Barrier Gate",
    desc: "Automated entry/exit control",
  },

  {
    type: "sensor",
    label: "Slot Sensors",
    desc: "Real-time occupancy detection",
  },

  {
    type: "rfid",
    label: "RFID Card Reader",
    desc: "Card-based access control",
  },

  {
    type: "ev-charger",
    label: "EV Charging Station",
    desc: "Electric vehicle charging",
  },
]

function Row({ label, value }: { label: string value: string }) {
  return (
    <div
      style={{
        display: "flex",
        justifyContent: "space-between",
        padding: "0.5rem 0",
        borderBottom: "1px solid var(--border)",
      }}
    >
      <span style={{ fontSize: "0.85rem", color: "var(--muted)" }}>
        {label}
      </span>
      <span
        style={{
          fontSize: "0.85rem",
          fontWeight: 600,
          color: "var(--fg)",
          textAlign: "right",
          maxWidth: "60%",
        }}
      >
        {value}
      </span>
    </div>
  )
}

// 5-step onboarding wizard for new owners

export function OnboardingWizard({
  onComplete,
}: {
  onComplete: (lot: ParkingLot) => void
}) {
  const { user, setUser } = useApp()

  const [step, setStep] = useState<OnboardStep>(1)

  const [lotType, setLotType] = useState<LotType>("outdoor")

  const [lotConfig, setLotConfig] = useState({
    name: "",
    address: "",
    totalSlots: "",
    floors: "",
  })

  const [devices, setDevices] = useState<Record<string, boolean>>({
    camera: false,
    barrier: false,
    sensor: false,
    rfid: false,
    "ev-charger": false,
  })

  const [pricing, setPricing] = useState({
    hourlyRate: 15000,
    dailyRate: 100000,
    nightRate: 50000,
    gracePeriodMinutes: 15,
    subscriptionMonthly: 800000,
    subscriptionYearly: 8000000,
  })

  const [systemPolicyAccepted, setSystemPolicyAccepted] = useState(false)

  const [ownerPolicyFile, setOwnerPolicyFile] = useState<string | null>(null)

  const [completionError, setCompletionError] = useState("")

  const [isCompleting, setIsCompleting] = useState(false)

  // 3D Model integration state (Mapping layer without heavy canvas preview in form)

  const [customModelUrl, setCustomModelUrl] = useState<string | null>(null)

  const [customModelFileName, setCustomModelFileName] = useState<string | null>(
    null,
  )

  const [modelUploadError, setModelUploadError] = useState<string | null>(null)

  const activeModelDef = ALLOWED_PARKING_MODELS[lotType]

  const activeModelUrl = customModelUrl || activeModelDef.path

  const activeModelFileName = customModelFileName || activeModelDef.fileName

  function handleLotTypeSelect(type: LotType) {
    setLotType(type)

    setCustomModelUrl(null)

    setCustomModelFileName(null)

    setModelUploadError(null)
  }

  function handleModelFileUpload(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]

    if (!file) return

    const validation = validateModelUpload(file.name, lotType)

    if (!validation.isValid) {
      setModelUploadError(
        validation.message ||
          `Tên file "${file.name}" không hợp lệ! Hệ thống chỉ chấp nhận đúng 3 file mô hình chuẩn: outdoor_parking_lot.glb, indoor_parking_lot.glb hoặc underground_parking_lot.glb.`,
      )

      e.target.value = ""

      return
    }

    setModelUploadError(null)

    if (validation.modelDef && validation.modelDef.lotType !== lotType) {
      setLotType(validation.modelDef.lotType)
    }

    const objectUrl = URL.createObjectURL(file)

    setCustomModelUrl(objectUrl)

    setCustomModelFileName(file.name)
  }

  function handleComplete() {
    if (!user) {
      setCompletionError(
        "Please sign in to an Owner account before activating a parking lot.",
      )

      return
    }

    setCompletionError("")

    setIsCompleting(true)

    try {
      const totalSlots = parseInt(String(lotConfig.totalSlots)) || 50

      const floors = LOT_TYPE_INFO[lotType].hasFloors
        ? parseInt(String(lotConfig.floors)) || 1
        : 1

      const lot = store.createLot({
        ownerId: user.id,
        name: lotConfig.name,
        type: lotType,

        address: lotConfig.address,
        totalSlots,

        floors,

        modelUrl: activeModelUrl,

        modelFileName: activeModelFileName,

        slots: store.generateSlots(totalSlots, floors),

        devices: Object.entries(devices)
          .filter(([, v]) => v)
          .map(([k]) => ({
            type: k as any,
            label: DEVICE_OPTIONS.find((d) => d.type === k)?.label ?? k,
            enabled: true,
          })),

        ...pricing,
        status: "active",

        lat: 10.77 + Math.random() * 0.1,
        lng: 106.69 + Math.random() * 0.1,
      })

      const updatedUser = {
        ...user,
        onboardingComplete: true,
        policyAccepted: true,
      }

      store.saveUser(updatedUser)

      setUser(updatedUser)

      setIsCompleting(false)

      onComplete(lot)

      // Audit logging is secondary to completing onboarding; it must not strand the owner on this screen.

      try {
        store.addAuditLog({
          userId: user.id,
          userName: user.name,
          userRole: "owner",
          action: "ONBOARDING_COMPLETE",
          details: `Lot created: ${lot.name}`,
        })
      } catch (error) {
        console.error(
          "Owner onboarding completed, but its audit log could not be saved.",
          error,
        )
      }
    } catch (error) {
      setIsCompleting(false)

      setCompletionError(
        error instanceof Error
          ? error.message
          : "Could not save your parking lot. Please try again.",
      )
    }
  }

  const stepLabels = [
    "Lot Type",
    "Structure",
    "Devices",
    "Pricing & Policy",
    "Confirm",
  ]

  return (
    <div style={{ maxWidth: "680px", margin: "0 auto", padding: "2rem 1rem" }}>
      <div style={{ textAlign: "center", marginBottom: "2rem" }}>
        <h2
          style={{
            fontFamily: "Outfit",
            fontWeight: 800,
            fontSize: "1.5rem",
            color: "var(--fg)",
            marginBottom: "0.375rem",
          }}
        >
          Set Up Your Parking Lot
        </h2>
        <p style={{ color: "var(--muted)", fontSize: "0.9rem" }}>
          Complete setup to start accepting bookings.
        </p>
      </div>

      {/* Step progress */}
      <div style={{ display: "flex", gap: "0.25rem", marginBottom: "2rem" }}>
        {stepLabels.map((label, i) => {
          const idx = i + 1

          const done = idx < step

          const cur = idx === step

          return (
            <div
              key={label}
              style={{
                flex: 1,
                display: "flex",
                flexDirection: "column",
                alignItems: "center",
                gap: "0.3rem",
              }}
            >
              <div
                style={{
                  width: "100%",
                  height: "3px",
                  background: done
                    ? "#22c55e"
                    : cur
                      ? "var(--primary)"
                      : "var(--border)",
                  borderRadius: "2px",
                  transition: "background 0.3s",
                }}
              />
              <span
                style={{
                  fontSize: "0.65rem",
                  color: cur
                    ? "var(--primary)"
                    : done
                      ? "#22c55e"
                      : "var(--muted)",
                  fontWeight: 600,
                }}
              >
                {done ? <UntitledIcon name="check" size={14} /> : idx}. {label}
              </span>
            </div>
          )
        })}
      </div>

      <div className="card animate-in">
        {step === 1 && (
          <div>
            <h3
              style={{
                fontFamily: "Outfit",
                fontWeight: 700,
                fontSize: "1.1rem",
                marginBottom: "1.25rem",
                color: "var(--fg)",
              }}
            >
              Choose Lot Type & 3D Model
            </h3>
            <div
              style={{
                display: "flex",
                flexDirection: "column",
                gap: "0.75rem",
                marginBottom: "1.25rem",
              }}
            >
              {(Object.entries(
                LOT_TYPE_INFO,
              ) as [LotType, typeof LOT_TYPE_INFO[LotType]][]).map(
                ([type, info]) => (
                  <div
                    key={type}
                    onClick={() => handleLotTypeSelect(type)}
                    style={{
                      padding: "1rem 1.25rem",
                      borderRadius: "var(--radius)",
                      border: `2px solid ${
                        lotType === type ? "var(--primary)" : "var(--border)"
                      }`,
                      cursor: "pointer",
                      background:
                        lotType === type ? "var(--primary)08" : "var(--bg)",
                      display: "flex",
                      alignItems: "center",
                      gap: "1rem",
                      transition: "all 0.2s",
                    }}
                  >
                    <span
                      style={{
                        color: "var(--primary)",
                        display: "inline-flex",
                      }}
                    >
                      <UntitledIcon name={info.icon} size={28} />
                    </span>
                    <div>
                      <div
                        style={{
                          fontWeight: 600,
                          color: "var(--fg)",
                          fontSize: "0.95rem",
                        }}
                      >
                        {info.label}
                      </div>
                      <div
                        style={{ fontSize: "0.8rem", color: "var(--muted)" }}
                      >
                        {type === "outdoor"
                          ? "Open ground-level parking"
                          : type === "basement"
                            ? "Underground parking with floors"
                            : "Multi-level parking structure"}
                      </div>
                    </div>
                    {lotType === type && (
                      <span
                        style={{
                          marginLeft: "auto",
                          color: "var(--primary)",
                          display: "inline-flex",
                        }}
                      >
                        <UntitledIcon name="check" size={18} />
                      </span>
                    )}
                  </div>
                ),
              )}
            </div>

            {/* 3D Model Integration Section */}
            <div
              style={{
                marginTop: "1.25rem",
                marginBottom: "1.5rem",
                padding: "1.1rem",
                background: "var(--bg)",
                border: "1.5px solid var(--border)",
                borderRadius: "var(--radius)",
              }}
            >
              <div
                style={{
                  display: "flex",
                  justifyContent: "space-between",
                  alignItems: "flex-start",
                  marginBottom: "0.75rem",
                  flexWrap: "wrap",
                  gap: "0.5rem",
                }}
              >
                <div>
                  <div
                    style={{
                      fontWeight: 700,
                      fontSize: "0.95rem",
                      color: "var(--fg)",
                      display: "flex",
                      alignItems: "center",
                      gap: "0.5rem",
                    }}
                  >
                    <span>🎮 Tích Hợp Mô Hình 3D Bãi Đỗ Xe</span>
                    <span
                      style={{
                        fontSize: "0.7rem",
                        padding: "0.1rem 0.45rem",
                        borderRadius: "999px",
                        background: "var(--primary)15",
                        color: "var(--primary)",
                        fontWeight: 600,
                      }}
                    >
                      Tự động nhận diện
                    </span>
                  </div>
                  <div
                    style={{
                      fontSize: "0.8rem",
                      color: "var(--muted)",
                      marginTop: "0.2rem",
                    }}
                  >
                    Mô hình liên kết:{" "}
                    <strong style={{ color: "var(--primary)" }}>
                      {activeModelFileName}
                    </strong>
                  </div>
                </div>

                <label style={{ cursor: "pointer" }}>
                  <span
                    className="btn-outline"
                    style={{
                      fontSize: "0.78rem",
                      padding: "0.35rem 0.65rem",
                      display: "inline-flex",
                      alignItems: "center",
                      gap: 4,
                    }}
                  >
                    <UntitledIcon name="file" size={14} /> Tải file .glb tùy
                    chỉnh
                  </span>
                  <input
                    type="file"
                    accept=".glb"
                    style={{ display: "none" }}
                    onChange={handleModelFileUpload}
                  />
                </label>
              </div>

              {/* Strict file rule notification */}
              <div
                style={{
                  padding: "0.55rem 0.75rem",
                  background: "var(--primary)08",
                  border: "1px solid var(--primary)20",
                  borderRadius: "var(--radius)",
                  fontSize: "0.76rem",
                  color: "var(--fg)",
                  marginBottom: "0.85rem",
                  lineHeight: 1.5,
                }}
              >
                <strong style={{ color: "var(--primary)" }}>
                  Quy định 3D:
                </strong>{" "}
                Chỉ chấp nhận đúng 3 file mô hình:
                <span
                  style={{
                    fontWeight: 600,
                    color:
                      lotType === "outdoor" ? "var(--primary)" : "var(--muted)",
                    marginLeft: 4,
                  }}
                >
                  outdoor_parking_lot.glb
                </span>
                ,
                <span
                  style={{
                    fontWeight: 600,
                    color:
                      lotType === "multi-storey"
                        ? "var(--primary)"
                        : "var(--muted)",
                    marginLeft: 4,
                  }}
                >
                  indoor_parking_lot.glb
                </span>
                ,
                <span
                  style={{
                    fontWeight: 600,
                    color:
                      lotType === "basement"
                        ? "var(--primary)"
                        : "var(--muted)",
                    marginLeft: 4,
                  }}
                >
                  underground_parking_lot.glb
                </span>
                .
              </div>

              {modelUploadError && (
                <div
                  role="alert"
                  style={{
                    padding: "0.65rem 0.85rem",
                    background: "#fef2f2",
                    border: "1.5px solid #f87171",
                    borderRadius: "var(--radius)",
                    color: "#dc2626",
                    fontSize: "0.8rem",
                    marginBottom: "0.85rem",
                    lineHeight: 1.45,
                    fontWeight: 500,
                  }}
                >
                  {modelUploadError}
                </div>
              )}

              {/* Digital Twin Mapping Details */}
              <div
                style={{
                  borderRadius: "calc(var(--radius) - 2px)",
                  padding: "1rem",
                  border: "1px solid var(--border)",
                  background: "var(--card)",
                }}
              >
                <div
                  style={{
                    display: "grid",
                    gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))",
                    gap: "0.75rem",
                    fontSize: "0.8rem",
                  }}
                >
                  <div>
                    <span
                      style={{
                        color: "var(--muted)",
                        display: "block",
                        fontSize: "0.72rem",
                      }}
                    >
                      Arch. Model Template:
                    </span>
                    <strong
                      style={{ color: "var(--fg)", fontFamily: "monospace" }}
                    >
                      {activeModelFileName}
                    </strong>
                  </div>
                  <div>
                    <span
                      style={{
                        color: "var(--muted)",
                        display: "block",
                        fontSize: "0.72rem",
                      }}
                    >
                      Structural Type:
                    </span>
                    <strong style={{ color: "var(--fg)" }}>
                      {LOT_TYPE_INFO[lotType].label}
                    </strong>
                  </div>
                  <div>
                    <span
                      style={{
                        color: "var(--muted)",
                        display: "block",
                        fontSize: "0.72rem",
                      }}
                    >
                      Digital Twin Synchronization:
                    </span>
                    <span style={{ color: "#16a34a", fontWeight: 600 }}>
                      ● Authoritative backend binding
                    </span>
                  </div>
                </div>
                <p
                  style={{
                    margin: "0.75rem 0 0",
                    fontSize: "0.75rem",
                    color: "var(--muted)",
                    lineHeight: 1.45,
                    borderTop: "1px solid var(--border)",
                    paddingTop: "0.5rem",
                  }}
                >
                  ℹ️ <em>SRS Architecture Note:</em> The 3D Digital Twin
                  visualization reflects backend-authoritative spot occupancy
                  and will be rendered automatically in the live operations view
                  without overhead during facility creation.
                </p>
              </div>
            </div>

            <button className="btn-primary" onClick={() => setStep(2)}>
              Continue
            </button>
          </div>
        )}

        {step === 2 && (
          <div>
            <h3
              style={{
                fontFamily: "Outfit",
                fontWeight: 700,
                fontSize: "1.1rem",
                marginBottom: "1.25rem",
                color: "var(--fg)",
              }}
            >
              Configure {LOT_TYPE_INFO[lotType].label} Structure
            </h3>
            <div
              style={{
                display: "flex",
                flexDirection: "column",
                gap: "1rem",
                marginBottom: "1.5rem",
              }}
            >
              <div>
                <label className="label">Lot Name *</label>
                <input
                  className="input"
                  value={lotConfig.name}
                  onChange={(e) =>
                    setLotConfig((c) => ({ ...c, name: e.target.value }))
                  }
                  placeholder="e.g. Vincom Center Parking"
                />
              </div>
              <div>
                <label className="label">Address *</label>
                <input
                  className="input"
                  value={lotConfig.address}
                  onChange={(e) =>
                    setLotConfig((c) => ({ ...c, address: e.target.value }))
                  }
                  placeholder="Full address"
                />
              </div>
              <div
                style={{
                  display: "grid",
                  gridTemplateColumns: LOT_TYPE_INFO[lotType].hasFloors
                    ? "1fr 1fr"
                    : "1fr",
                  gap: "1rem",
                }}
              >
                <div>
                  <label className="label">Total Parking Slots *</label>
                  <input
                    className="input"
                    type="number"
                    min={1}
                    max={2000}
                    value={lotConfig.totalSlots}
                    placeholder="e.g. 50"
                    onChange={(e) =>
                      setLotConfig((c) => ({
                        ...c,
                        totalSlots: e.target.value,
                      }))
                    }
                  />
                </div>
                {LOT_TYPE_INFO[lotType].hasFloors && (
                  <div>
                    <label className="label">Number of Floors / Levels *</label>
                    <input
                      className="input"
                      type="number"
                      min={1}
                      max={20}
                      value={lotConfig.floors}
                      placeholder="e.g. 3"
                      onChange={(e) =>
                        setLotConfig((c) => ({ ...c, floors: e.target.value }))
                      }
                    />
                    {lotConfig.totalSlots &&
                      lotConfig.floors &&
                      parseInt(String(lotConfig.floors)) > 0 && (
                        <p
                          style={{
                            fontSize: "0.75rem",
                            color: "var(--muted)",
                            marginTop: "0.25rem",
                          }}
                        >
                          ~
                          {Math.ceil(
                            parseInt(String(lotConfig.totalSlots)) /
                              parseInt(String(lotConfig.floors)),
                          )}{" "}
                          slots per floor
                        </p>
                      )}
                  </div>
                )}
              </div>
            </div>
            <div style={{ display: "flex", gap: "0.75rem" }}>
              <button className="btn-outline" onClick={() => setStep(1)}>
                Back
              </button>
              <button
                className="btn-primary"
                onClick={() => setStep(3)}
                disabled={
                  !lotConfig.name ||
                  !lotConfig.address ||
                  !lotConfig.totalSlots ||
                  (LOT_TYPE_INFO[lotType].hasFloors && !lotConfig.floors)
                }
              >
                Continue
              </button>
            </div>
          </div>
        )}

        {step === 3 && (
          <div>
            <h3
              style={{
                fontFamily: "Outfit",
                fontWeight: 700,
                fontSize: "1.1rem",
                marginBottom: "0.375rem",
                color: "var(--fg)",
              }}
            >
              Equipment & Devices
            </h3>
            <p
              style={{
                fontSize: "0.85rem",
                color: "var(--muted)",
                marginBottom: "1.25rem",
              }}
            >
              Check the devices your lot currently has installed.
            </p>
            <div
              style={{
                display: "flex",
                flexDirection: "column",
                gap: "0.625rem",
                marginBottom: "1.5rem",
              }}
            >
              {DEVICE_OPTIONS.map((d) => (
                <label
                  key={d.type}
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: "0.875rem",
                    padding: "0.875rem",
                    border: `1.5px solid ${
                      devices[d.type] ? "var(--primary)" : "var(--border)"
                    }`,
                    borderRadius: "var(--radius)",
                    cursor: "pointer",
                    background: devices[d.type]
                      ? "var(--primary)08"
                      : "var(--bg)",
                    transition: "all 0.2s",
                  }}
                >
                  <input
                    type="checkbox"
                    checked={devices[d.type] || false}
                    onChange={(e) =>
                      setDevices((dev) => ({
                        ...dev,
                        [d.type]: e.target.checked,
                      }))
                    }
                    style={{ width: 16, height: 16, flexShrink: 0 }}
                  />
                  <div>
                    <div
                      style={{
                        fontWeight: 600,
                        fontSize: "0.9rem",
                        color: "var(--fg)",
                      }}
                    >
                      {d.label}
                    </div>
                    <div style={{ fontSize: "0.78rem", color: "var(--muted)" }}>
                      {d.desc}
                    </div>
                  </div>
                </label>
              ))}
            </div>
            <div style={{ display: "flex", gap: "0.75rem" }}>
              <button className="btn-outline" onClick={() => setStep(2)}>
                Back
              </button>
              <button className="btn-primary" onClick={() => setStep(4)}>
                Continue
              </button>
            </div>
          </div>
        )}

        {step === 4 && (
          <div>
            <h3
              style={{
                fontFamily: "Outfit",
                fontWeight: 700,
                fontSize: "1.1rem",
                marginBottom: "1.25rem",
                color: "var(--fg)",
              }}
            >
              Pricing & Policy Configuration
            </h3>
            <div
              style={{
                display: "grid",
                gridTemplateColumns: "1fr 1fr",
                gap: "0.875rem",
                marginBottom: "1rem",
              }}
            >
              {[
                { label: "Hourly Rate (₫)", key: "hourlyRate" },

                { label: "Daily Rate (₫)", key: "dailyRate" },

                { label: "Night Rate (₫)", key: "nightRate" },

                { label: "Grace Period (min)", key: "gracePeriodMinutes" },

                { label: "Monthly Pass (₫)", key: "subscriptionMonthly" },

                { label: "Annual Pass (₫)", key: "subscriptionYearly" },
              ].map((f) => (
                <div key={f.key}>
                  <label className="label">{f.label}</label>
                  <input
                    className="input"
                    type="number"
                    min={0}
                    value={(pricing as any)[f.key]}
                    onChange={(e) =>
                      setPricing((p) => ({
                        ...p,
                        [f.key]: parseFloat(e.target.value) || 0,
                      }))
                    }
                  />
                </div>
              ))}
            </div>

            {/* System policy acceptance */}
            <div
              style={{
                background: "var(--bg)",
                border: "1px solid var(--border)",
                borderRadius: "var(--radius)",
                padding: "1rem",
                marginBottom: "0.875rem",
              }}
            >
              <div
                style={{
                  fontWeight: 600,
                  fontSize: "0.9rem",
                  color: "var(--fg)",
                  marginBottom: "0.5rem",
                }}
              >
                SmartParking Partner Agreement
              </div>
              <div
                style={{
                  maxHeight: "120px",
                  overflowY: "auto",
                  fontSize: "0.78rem",
                  color: "var(--muted)",
                  lineHeight: 1.7,
                  marginBottom: "0.75rem",
                }}
              >
                By joining SmartParking, you agree to: (1) Revenue sharing at
                15% platform fee per booking; (2) Maintaining lot quality
                standards; (3) Providing accurate availability data; (4)
                Honoring the refund policy; (5) Allowing SmartParking to display
                your lot information on the platform.
              </div>
              <label
                style={{
                  display: "flex",
                  gap: "0.5rem",
                  alignItems: "center",
                  fontSize: "0.85rem",
                  cursor: "pointer",
                }}
              >
                <input
                  type="checkbox"
                  checked={systemPolicyAccepted}
                  onChange={(e) => setSystemPolicyAccepted(e.target.checked)}
                />
                I have read and agree to the SmartParking Partner Agreement
              </label>
            </div>

            {/* Owner policy upload */}
            <div
              style={{
                background: "var(--bg)",
                border: "1px solid var(--border)",
                borderRadius: "var(--radius)",
                padding: "1rem",
                marginBottom: "1.25rem",
              }}
            >
              <div
                style={{
                  fontWeight: 600,
                  fontSize: "0.9rem",
                  color: "var(--fg)",
                  marginBottom: "0.375rem",
                }}
              >
                Your Lot Policy Document
              </div>
              <p
                style={{
                  fontSize: "0.78rem",
                  color: "var(--muted)",
                  marginBottom: "0.75rem",
                }}
              >
                Download our template, fill it in, and upload your completed
                policy document.
              </p>
              <div
                style={{
                  display: "flex",
                  gap: "0.625rem",
                  alignItems: "center",
                }}
              >
                <button
                  className="btn-outline"
                  style={{ fontSize: "0.82rem", padding: "0.4rem 0.75rem" }}
                  onClick={() =>
                    alert("Policy template downloaded (simulated)")
                  }
                >
                  <>
                    <UntitledIcon name="file" size={15} /> Download Template
                  </>
                </button>
                <label style={{ cursor: "pointer" }}>
                  <span
                    className="btn-primary"
                    style={{ fontSize: "0.82rem", padding: "0.4rem 0.75rem" }}
                  >
                    <>
                      <UntitledIcon name="file" size={15} /> Upload Policy
                    </>
                  </span>
                  <input
                    type="file"
                    accept=".pdf,.docx"
                    style={{ display: "none" }}
                    onChange={() => setOwnerPolicyFile("policy_uploaded.pdf")}
                  />
                </label>
                {ownerPolicyFile && (
                  <span
                    style={{
                      fontSize: "0.78rem",
                      color: "#22c55e",
                      display: "inline-flex",
                      alignItems: "center",
                      gap: 4,
                    }}
                  >
                    <UntitledIcon name="check" size={14} /> {ownerPolicyFile}
                  </span>
                )}
              </div>
            </div>

            <div style={{ display: "flex", gap: "0.75rem" }}>
              <button className="btn-outline" onClick={() => setStep(3)}>
                Back
              </button>
              <button
                className="btn-primary"
                onClick={() => setStep(5)}
                disabled={!systemPolicyAccepted}
              >
                Continue to Review
              </button>
            </div>
          </div>
        )}

        {step === 5 && (
          <div>
            <h3
              style={{
                fontFamily: "Outfit",
                fontWeight: 700,
                fontSize: "1.1rem",
                marginBottom: "1.25rem",
                color: "var(--fg)",
              }}
            >
              Review & Confirm
            </h3>
            <div
              style={{
                display: "flex",
                flexDirection: "column",
                gap: "0.625rem",
                marginBottom: "1.5rem",
              }}
            >
              <Row label="Lot Type" value={LOT_TYPE_INFO[lotType].label} />
              <Row
                label="3D Digital Twin"
                value={`${activeModelFileName} (Tích hợp)`}
              />
              <Row label="Name" value={lotConfig.name} />
              <Row label="Address" value={lotConfig.address} />
              <Row
                label="Total Slots"
                value={String(parseInt(String(lotConfig.totalSlots)) || 0)}
              />
              {LOT_TYPE_INFO[lotType].hasFloors && (
                <Row
                  label="Floors"
                  value={String(parseInt(String(lotConfig.floors)) || 0)}
                />
              )}
              <Row
                label="Devices"
                value={
                  Object.entries(devices)
                    .filter(([, v]) => v)
                    .map(([k]) => k)
                    .join(", ") || "None selected"
                }
              />
              <Row
                label="Hourly Rate"
                value={`${pricing.hourlyRate.toLocaleString("vi-VN")}₫`}
              />
              <Row
                label="Daily Rate"
                value={`${pricing.dailyRate.toLocaleString("vi-VN")}₫`}
              />
              <Row
                label="Monthly Pass"
                value={`${pricing.subscriptionMonthly.toLocaleString("vi-VN")}₫`}
              />
              <Row
                label="Grace Period"
                value={`${pricing.gracePeriodMinutes} minutes`}
              />
            </div>
            <div style={{ display: "flex", gap: "0.75rem" }}>
              <button className="btn-outline" onClick={() => setStep(4)}>
                Back
              </button>
              <button
                className="btn-accent"
                onClick={handleComplete}
                disabled={isCompleting}
                style={{
                  flex: 1,
                  justifyContent: "center",
                  opacity: isCompleting ? 0.7 : 1,
                }}
              >
                {isCompleting ? (
                  "Activating…"
                ) : (
                  <>
                    <UntitledIcon name="sparkles" size={16} /> Activate My
                    Parking Lot
                  </>
                )}
              </button>
            </div>
            {completionError && (
              <p
                role="alert"
                style={{
                  color: "#dc2626",
                  fontSize: "0.85rem",
                  margin: "0.75rem 0 0",
                }}
              >
                {completionError}
              </p>
            )}
          </div>
        )}
      </div>
    </div>
  )
}
