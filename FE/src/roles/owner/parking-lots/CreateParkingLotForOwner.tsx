import { useEffect, useState, useId } from "react"

import { useApp } from "../../../context/AppContext"
import { parkingApi } from "../../../lib/parkingApi"
import { ownerApi, type OwnerLotSetup } from "../../../lib/ownerApi"
import { AuthApiError } from "../../../lib/authApi"
import { restoreOwnerRegistration, ownerRegistrationStage, type PendingOwnerRegistration } from "../../../lib/ownerRegistration"
import { OwnerRegistrationRecovery } from "../../authentications/Sign Up/OwnerRegistrationRecovery"
import { GroupedNumberInput } from "../../../components/forms/GroupedNumberInput"
import { PageHeader } from "../../../components/ui/PageHeader"
import { formatGroupedDigits } from "../../../lib/groupedDigits"

import { ownerData as store } from "../data/data"

import type { ParkingLot, LotType } from "../../../lib/types"

import { UntitledIcon } from "../../../components/icon/UntitledIcon"

import {
  ALLOWED_PARKING_MODELS,
  validateModelUpload,
} from "../../../lib/3d/parking3DConfig"
import { useModelCarCapacity } from "../../../lib/3d/modelSlotCapacity"
import { saveOwnerModel } from "../../../lib/3d/ownerModelStorage"
import { loadContractDraft, saveContractDraft, type ContractDraftRecord } from "../../../lib/partnerContract"
import { PartnerContractDialog } from "../../../components/modals/PartnerContractDialog"
import { Parking3DViewer } from "../../../components/3d/Parking3DViewer"

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

function Row({ label, value }: { label: string; value: string }) {
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
  registration,
}: {
  onComplete?: (lot: ParkingLot) => void
  registration?: {
    id: string
    email: string
    ownerName?: string
    businessName?: string
    setupToken?: string
    lotType: LotType
    onComplete: (application: PendingOwnerRegistration) => void
  }
}) {
  const { user, setUser, setView } = useApp()
  const [setupToken, setSetupToken] = useState(registration?.setupToken)
  const [needsSetupRecovery, setNeedsSetupRecovery] = useState(false)

  const [step, setStep] = useState<OnboardStep>(1)

  const [lotType, setLotType] = useState<LotType>(registration?.lotType ?? "outdoor")

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
    hourlyRate: "",
    dailyRate: "",
    nightRate: "",
    gracePeriodMinutes: "15",
    subscriptionMonthly: "",
    subscriptionYearly: "",
  })

  const [systemPolicyAccepted, setSystemPolicyAccepted] = useState(false)
  const [contractDraft, setContractDraft] = useState<ContractDraftRecord | null>(() => registration ? loadContractDraft(registration.id) : null)
  const [showContract, setShowContract] = useState(false)
  const [showModelPreview, setShowModelPreview] = useState(false)

  const [completionError, setCompletionError] = useState("")

  const [isCompleting, setIsCompleting] = useState(false)

  // 3D Model integration state (Mapping layer without heavy canvas preview in form)

  const [customModelUrl, setCustomModelUrl] = useState<string | null>(null)

  const [customModelFileName, setCustomModelFileName] = useState<string | null>(
    null,
  )
  const [customModelFile, setCustomModelFile] = useState<File | null>(null)

  const [modelUploadError, setModelUploadError] = useState<string | null>(null)

  const activeModelDef = ALLOWED_PARKING_MODELS[lotType]

  const activeModelUrl = customModelUrl || activeModelDef.path

  const activeModelFileName = customModelFileName || activeModelDef.fileName
  const setupFingerprint = JSON.stringify({ lotType, lotConfig, devices, pricing, customModelFileName })
  const activeContractDraft = contractDraft?.setupFingerprint === setupFingerprint ? contractDraft : null

  useEffect(() => () => { if (customModelUrl) URL.revokeObjectURL(customModelUrl) }, [customModelUrl])
  const modelCapacity = useModelCarCapacity(activeModelUrl)

  function handleLotTypeSelect(type: LotType) {
    setLotType(type)

    setCustomModelUrl(null)

    setCustomModelFileName(null)
    setCustomModelFile(null)

    setModelUploadError(null)
  }

  function handleModelFileUpload(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]

    if (!file) return
    if (file.size > 30 * 1024 * 1024) {
      setModelUploadError("Choose a GLB file smaller than 30 MB.")
      e.target.value = ""
      return
    }

    const validation = validateModelUpload(file.name, lotType)

    if (!validation.isValid && !file.name.toLowerCase().endsWith(".glb")) {
      setModelUploadError(
        "Choose a GLB 3D parking model.",
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
    setCustomModelFile(file)
  }

  async function handleComplete() {
    if (isCompleting) return
    if (!registration && !user) {
      setCompletionError(
        "Please sign in to an Owner account before activating a parking lot.",
      )

      return
    }

    setCompletionError("")
    if (registration && !activeContractDraft?.ownerAcknowledgedAt) {
      setCompletionError("Read and acknowledge the partnership draft before submitting your parking lot.")
      return
    }

    setIsCompleting(true)

    try {
      const name = lotConfig.name.trim()
      const address = lotConfig.address.trim()
      if (!name || !address) {
        throw new Error("Please enter the parking lot name and address.")
      }

      const totalSlots = Number(lotConfig.totalSlots)

      const floors = LOT_TYPE_INFO[lotType].hasFloors
        ? Number(lotConfig.floors)
        : 1

      if (modelCapacity.capacity === null) {
        throw new Error(modelCapacity.error || "Checking the selected 3D model's car bay capacity. Please wait.")
      }
      if (!Number.isInteger(totalSlots) || totalSlots < 1 || totalSlots > modelCapacity.capacity ||
          !Number.isInteger(floors) || floors < 1 || floors > 20) {
        throw new Error(`Enter 1–${modelCapacity.capacity} car spaces, matching the selected 3D model, and a valid floor count.`)
      }

      const numericPricing = {
        hourlyRate: Number(pricing.hourlyRate || 15000),
        dailyRate: Number(pricing.dailyRate || 100000),
        nightRate: Number(pricing.nightRate || 50000),
        gracePeriodMinutes: Number(pricing.gracePeriodMinutes || 15),
        subscriptionMonthly: Number(pricing.subscriptionMonthly || 800000),
        subscriptionYearly: Number(pricing.subscriptionYearly || 8000000),
      }
      if (Object.values(numericPricing).some((value) =>
        !Number.isSafeInteger(value) || value < 0)) {
        throw new Error("Enter valid whole-number pricing values.")
      }

      if (registration) {
        if (customModelFile) await saveOwnerModel(registration.id, customModelFile)
        const setup: OwnerLotSetup = {
          name,
          address,
          lotType,
          totalSlots,
          floors,
          devices: Object.entries(devices)
            .filter(([, enabled]) => enabled)
            .map(([type]) => ({
              type: type as OwnerLotSetup["devices"][number]["type"],
              label: DEVICE_OPTIONS.find((device) => device.type === type)?.label ?? type,
              enabled: true,
            })),
          // Registration accepts only a canonical file name; the Owner GLB is kept for browser preview.
          modelFileName: activeModelDef.fileName,
          ...numericPricing,
          policyAccepted: systemPolicyAccepted,
        }
        const application = await ownerApi.submitLotSetup(
          registration.id,
          setupToken ?? "",
          setup,
        )
        registration.onComplete(application)
        return
      }

      const site = await parkingApi.create({
        code: `SPK-ONB-${crypto.randomUUID().slice(0, 8).toUpperCase()}`,
        name,
        address,
      })
      if (!site || typeof site.id !== "string" || !site.id.trim()) {
        throw new Error("The server did not return a valid site ID. Please try again.")
      }

      const savedModelUrl = customModelFile ? await saveOwnerModel(site.id, customModelFile).catch(() => null) : null

      const lot: ParkingLot = {
        id: site.id,
        createdAt: new Date().toISOString(),
        ownerId: user!.id,
        name,
        type: lotType,

        address,
        totalSlots,

        floors,

        modelUrl: savedModelUrl ?? activeModelUrl,

        modelFileName: activeModelFileName,

        slots: [],

        devices: Object.entries(devices)
          .filter(([, v]) => v)
          .map(([k]) => ({
            type: k as any,
            label: DEVICE_OPTIONS.find((d) => d.type === k)?.label ?? k,
            enabled: true,
          })),

        ...numericPricing,
        status: "active",

        lat: 10.77 + Math.random() * 0.1,
        lng: 106.69 + Math.random() * 0.1,
      }

      try {
        store.saveLot(lot)
      } catch (error) {
        console.error("Site created, but the local lot could not be saved.", error)
      }

      const updatedUser = {
        ...user!,
        onboardingComplete: true,
        policyAccepted: true,
      }

      store.saveUser(updatedUser)

      setUser(updatedUser)

      onComplete?.(lot)

      // Audit logging is secondary to completing onboarding; it must not strand the owner on this screen.

      try {
        store.addAuditLog({
          userId: user!.id,
          userName: user!.name,
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
      if (registration && error instanceof AuthApiError && error.code === "SETUP_TOKEN_INVALID")
        setNeedsSetupRecovery(true)
      setCompletionError(
        error instanceof Error
          ? error.message
          : "Could not save your parking lot. Please try again.",
      )
    } finally {
      setIsCompleting(false)
    }
  }

  const stepLabels = [
    "Lot Type",
    "Structure",
    "Devices",
    "Pricing & Policy",
    "Confirm",
  ]

  if (registration && needsSetupRecovery) {
    return <OwnerRegistrationRecovery registration={{ email: registration.email }}
      onRecovered={(application) => {
        sessionStorage.setItem("sp_pending_owner_registration", JSON.stringify(application))
        if (ownerRegistrationStage(application) === "setup") {
          setSetupToken(application.setupToken)
          setNeedsSetupRecovery(false)
          setCompletionError("")
        } else if (ownerRegistrationStage(application) === "verification") {
          registration.onComplete(application)
        }
      }} onBack={() => setView("sign-in")} />
  }

  return (
    <div style={{ maxWidth: "680px", margin: "0 auto", padding: "2rem 1rem" }}>
      <div style={{ textAlign: "center", marginBottom: "2rem" }}>
        <PageHeader title="Set Up Your Parking Lot" variant="compact" align="center" description={registration
          ? "Set up your lot before verifying your email and submitting it for Admin review."
          : "Complete setup to start accepting bookings."} />

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
                fontFamily: "Be Vietnam Pro",
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
                    <span><UntitledIcon name="parking" size={18} /> 3D Parking Model</span>
                    <span
                      style={{
                        fontSize: "0.7rem",
                        padding: "0.1rem 0.45rem",
                        borderRadius: "999px",
                        background: "var(--status-available-bg)",
                        color: "var(--primary)",
                        fontWeight: 600,
                      }}
                    >
                      Auto-detected
                    </span>
                  </div>
                  <div
                    style={{
                      fontSize: "0.8rem",
                      color: "var(--muted)",
                      marginTop: "0.2rem",
                    }}
                  >
                    Linked model:{" "}
                    <strong style={{ color: "var(--primary)" }}>
                      {activeModelFileName}
                    </strong>
                  </div>
                </div>

                <label className="owner-model-import" style={{ cursor: "pointer" }}>
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
                    <UntitledIcon name="file" size={14} /> Import your 3D model (.glb)
                  </span>
                  <input
                    type="file"
                    accept=".glb"
                    className="sr-only"
                    onChange={handleModelFileUpload}
                  />
                </label>
              </div>

              {registration && customModelFileName && <p className="text-xs text-[var(--muted)]">This model is stored in this browser for preview. The registration service still records the standard model for this lot type.</p>}

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
                  3D Model Requirements:
                </strong>{" "}
                The supplied models are:
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
                . Your own GLB is also accepted when its car bays have recognizable names, such as Slot_001 or CarParkingSpace_001.
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
                    <span style={{ color: "var(--primary)", fontWeight: 600 }}>
                      {customModelFile ? "Owner model · local preview" : "System model"}
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
                  {customModelFile ? "This imported model is previewed in this browser. A backend upload is needed before other users can see it." : "The system model can display live space occupancy when operators open the parking site."}
                </p>
              </div>
              <button type="button" className="btn-outline mt-4" onClick={() => setShowModelPreview(value => !value)} aria-expanded={showModelPreview}>
                {showModelPreview ? "Hide 3D preview" : "Preview selected 3D model"}
              </button>
              {showModelPreview && <div className="mt-4 overflow-hidden rounded-2xl border border-[var(--border)]"><Parking3DViewer modelUrl={activeModelUrl} lotType={lotType} interactive={false} showLegend={false} height={300} /></div>}
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
                fontFamily: "Be Vietnam Pro",
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
                  <label className="label">Planned Car Spaces *</label>
                  <input
                    className="input"
                    type="number"
                    min={1}
                    max={modelCapacity.capacity ?? undefined}
                    value={lotConfig.totalSlots}
                    placeholder="e.g. 50"
                    onFocus={(e) => e.target.select()}
                    onChange={(e) =>
                      setLotConfig((c) => ({
                        ...c,
                        totalSlots: e.target.value,
                      }))
                    }
                  />
                  <p className="mt-1 text-xs text-[var(--muted)]">{modelCapacity.error || (modelCapacity.capacity === null ? "Checking car bays in the selected 3D model…" : `Maximum ${modelCapacity.capacity} car spaces in this model.`)}</p>
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
                      onFocus={(e) => e.target.select()}
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
                fontFamily: "Be Vietnam Pro",
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
                fontFamily: "Be Vietnam Pro",
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
              {([
                {
                  label: "Hourly Rate (VND)",
                  key: "hourlyRate",
                  placeholder: "15,000",
                },
                {
                  label: "Daily Rate (VND)",
                  key: "dailyRate",
                  placeholder: "100,000",
                },
                {
                  label: "Night Rate (VND)",
                  key: "nightRate",
                  placeholder: "50,000",
                },
                {
                  label: "Grace Period (min)",
                  key: "gracePeriodMinutes",
                  placeholder: "15",
                },
                {
                  label: "Monthly Pass (VND)",
                  key: "subscriptionMonthly",
                  placeholder: "800,000",
                },
                {
                  label: "Annual Pass (VND)",
                  key: "subscriptionYearly",
                  placeholder: "8,000,000",
                },
              ] as const).map((f) => (
                <div key={f.key}>
                  <label className="label">{f.label}</label>
                  {f.key === "gracePeriodMinutes" ? (
                    <input
                      className="input"
                      type="number"
                      min={0}
                      placeholder={f.placeholder}
                      value={pricing.gracePeriodMinutes}
                      onFocus={(e) => e.target.select()}
                      onChange={(e) =>
                        setPricing((p) => ({
                          ...p,
                          gracePeriodMinutes: e.target.value,
                        }))
                      }
                    />
                  ) : (
                    <GroupedNumberInput
                      className="input"
                      placeholder={f.placeholder}
                      step={1000}
                      value={pricing[f.key]}
                      onValueChange={(digits) =>
                        setPricing((p) => ({ ...p, [f.key]: digits.slice(0, 9) }))
                      }
                    />
                  )}
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
              <ol className="owner-partner-terms">
                <li>Keep parking availability, prices, opening hours, and site details accurate.</li>
                <li>Operate the lot safely and help resolve booking and on-site complaints.</li>
                <li>Follow the agreed cancellation and refund terms. Commercial rates belong in a separate signed schedule.</li>
                <li>Allow SmartParking to display approved lot details and a 3D preview.</li>
              </ol>
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

            {/* Owner policy upload is only available after account approval. */}
            {!registration && <div
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
                <a className="btn-outline inline-flex items-center gap-2 no-underline" href="/templates/owner-policy-template.pdf" download="smartparking-owner-policy-template.pdf"><UntitledIcon name="file" size={15} /> Download Owner PDF template</a>
                <span className="text-xs text-[var(--muted)]">Upload the completed PDF in Policy Settings after this site is created.</span>
              </div>
            </div>}

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
                fontFamily: "Be Vietnam Pro",
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
                value={`${activeModelFileName} (${customModelFile ? "Owner import · local preview" : "System model"})`}
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
                value={`${formatGroupedDigits(pricing.hourlyRate || "15000")} VND`}
              />
              <Row
                label="Daily Rate"
                value={`${formatGroupedDigits(pricing.dailyRate || "100000")} VND`}
              />
              <Row
                label="Night Rate"
                value={`${formatGroupedDigits(pricing.nightRate || "50000")} VND`}
              />
              <Row
                label="Monthly Pass"
                value={`${formatGroupedDigits(pricing.subscriptionMonthly || "800000")} VND`}
              />
              <Row
                label="Annual Pass"
                value={`${formatGroupedDigits(pricing.subscriptionYearly || "8000000")} VND`}
              />
              <Row
                label="Grace Period"
                value={`${pricing.gracePeriodMinutes || "15"} minutes`}
              />
            </div>
            {registration && <div className="owner-contract-step">
              <div>
                <strong>Partnership contract draft</strong>
                <p>Read the agreement and add your draft signature before submitting your parking lot for review.</p>
                <span>{activeContractDraft?.ownerAcknowledgedAt ? `Draft signed by ${activeContractDraft.ownerName}` : "Owner draft signature required"}</span>
              </div>
              <button type="button" className="btn-outline" onClick={() => setShowContract(true)}>{activeContractDraft?.ownerAcknowledgedAt ? "View contract" : "Read and sign draft"}</button>
            </div>}
            <div style={{ display: "flex", gap: "0.75rem" }}>
              <button className="btn-outline" onClick={() => setStep(4)}>
                Back
              </button>
              <button
                className="btn-accent"
                onClick={handleComplete}
                disabled={isCompleting || (!!registration && !activeContractDraft?.ownerAcknowledgedAt)}
                style={{
                  flex: 1,
                  justifyContent: "center",
                  opacity: isCompleting ? 0.7 : 1,
                }}
              >
                {isCompleting ? (
                  (registration ? "Saving setup…" : "Activating…")
                ) : (
                  <>
                    <UntitledIcon name="sparkles" size={16} />{" "}
                    {registration ? "Save Lot and Continue to Email Verification" : "Activate My Parking Lot"}
                  </>
                )}
              </button>
            </div>
            <p className="mt-3 text-xs text-[var(--muted)]">
              {registration
                ? "Your lot will remain inactive until email verification and Admin approval."
                : "Activation creates your site. Review its 3D model and live capacity in Parking Structure next."}
            </p>
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
      {registration && showContract && <PartnerContractDialog
        applicationId={registration.id}
        ownerName={registration.ownerName || user?.name || "Owner"}
        businessName={registration.businessName || "Owner business"}
        lotName={lotConfig.name || "Parking lot"}
        record={activeContractDraft}
        signer="owner"
        signerName={registration.ownerName || user?.name || ""}
        onClose={() => setShowContract(false)}
        onSign={(name) => {
          const next: ContractDraftRecord = {
            ...activeContractDraft,
            ownerName: name,
            ownerEmail: registration.email,
            businessName: registration.businessName || "Owner business",
            lotName: lotConfig.name || "Parking lot",
            ownerAcknowledgedAt: new Date().toISOString(),
            modelFileName: customModelFileName || undefined,
            setupFingerprint,
          }
          saveContractDraft(registration.id, next)
          setContractDraft(next)
          setShowContract(false)
        }} />}
    </div>
  )
}

export function BusinessLotSetupPage() {
  const { setView } = useApp()
  const [registration] = useState(restoreOwnerRegistration)

  if (!registration) {
    return (
      <div className="card mx-auto my-12 max-w-lg text-center">
        <PageHeader title="No Business registration in progress" variant="compact" align="center" />
        <p>Register your Business account before setting up a parking lot.</p>
        <button className="btn-primary" onClick={() => setView("sign-up")}>
          Go to Sign Up
        </button>
      </div>
    )
  }

  if (ownerRegistrationStage(registration) !== "setup") {
    return <OwnerRegistrationRecovery registration={registration}
      onRecovered={(application) => {
        sessionStorage.setItem("sp_pending_owner_registration", JSON.stringify(application))
        setView("sign-up")
      }} onBack={() => setView("sign-in")} />
  }

  return (
    <OnboardingWizard
      registration={{
        id: registration.id!,
        email: registration.email,
        ownerName: registration.ownerName,
        businessName: registration.businessName,
        setupToken: registration.setupToken,
        lotType: registration.lotType!,
        onComplete: (application) => {
          sessionStorage.setItem(
            "sp_pending_owner_registration",
            JSON.stringify({
              id: application.id,
              email: application.email,
              lotType: application.lotType,
              lotSetupComplete: true,
              verification: application.verification,
            }),
          )
          setView("sign-up")
        },
      }}
    />
  )
}
