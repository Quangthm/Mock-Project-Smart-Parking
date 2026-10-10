import { useEffect, useState, useRef, useMemo } from "react"

import { useApp } from "../../../context/AppContext"

import { store } from "../../../lib/store"

import type { LotPolicyPdf, ParkingLot } from "../../../lib/types"

import { UntitledIcon } from "../../../components/icon/UntitledIcon"

import { LotPolicyModal } from "../../../components/modals/LotPolicyModal"

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

interface PolicySettingsProps {
  selectedSiteId?: string
  onSelectSite?: (siteId: string) => void
}

export function PolicySettings({
  selectedSiteId: externalSiteId,
  onSelectSite,
}: PolicySettingsProps = {}) {
  const { user } = useApp()

  const [policy, setPolicy] = useState<PolicyState>(() =>
    user ? (store.getOwnerPolicy(user.id) ?? initialPolicy) : initialPolicy,
  )

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
  const [previewLot, setPreviewLot] = useState<ParkingLot | null>(null)
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
          title: `Chính sách & Quy định Bãi đỗ: ${lot?.name || "SmartPark"}`,
        })
      } else {
        setCurrentPdf(lotPdf)
      }
    }
  }, [selectedLotId, user, ownerLots])

  function handleFileSelect(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file || !user) return

    if (
      !file.type.includes("pdf") &&
      !file.name.toLowerCase().endsWith(".pdf")
    ) {
      setPdfNotice("Vui lòng chọn định dạng file PDF hợp lệ (.pdf).")
      return
    }

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
            ? "Chính sách Vận hành Bãi đỗ Toàn hệ thống"
            : `Chính sách & Quy định Bãi đỗ: ${lot?.name || "SmartPark"}`,
        summaryText: `Quy định chính thức được cập nhật cho cơ sở ${lot?.name || "toàn bộ bãi đỗ của chủ bãi"}.`,
      }

      if (selectedLotId === "all") {
        store.saveOwnerPolicyPdf(user.id, payload)
      } else {
        store.saveLotPolicyPdf(selectedLotId, payload)
      }

      setCurrentPdf(payload)
      setPdfNotice(
        `Đã tải lên file PDF chính sách "${file.name}" thành công! Tài xế giờ có thể đọc tài liệu này.`,
      )
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
        "Chính sách bãi đỗ được cập nhật",
        `Bãi đỗ ${lot?.name || "SmartPark"} vừa cập nhật tài liệu chính sách vận hành mới.`,
      )
    }

    reader.readAsDataURL(file)
  }

  function handleDeletePdf() {
    if (!user) return
    if (
      confirm(
        "Bạn có chắc muốn xóa file PDF chính sách này và quay về chính sách mặc định?",
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
            title: `Chính sách & Quy định Bãi đỗ: ${lot?.name || "SmartPark"}`,
          })
        } else {
          const fallback = store.getLotPolicyPdf(selectedLotId, user.id)
          setCurrentPdf(fallback)
        }
      }
      setPdfNotice(
        "Đã xóa file PDF tùy chỉnh. Hệ thống chuyển về chính sách mặc định.",
      )
      setTimeout(() => setPdfNotice(null), 4000)
    }
  }

  function handleOpenPreview() {
    const lot = ownerLots.find((l) => l.id === selectedLotId) ||
      ownerLots[0] || {
        id: "sample",
        name: "Bãi đỗ Xe Mẫu SmartPark",
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
      <header>
        <p className="text-sm font-medium text-[var(--primary)]">Owner tools</p>
        <h1
          id="policy-settings-title"
          className="mt-1 text-2xl font-bold text-[var(--fg)]"
        >
          Policy Settings
        </h1>
        <p className="mt-2 text-sm text-[var(--muted)]">
          Configure pricing, deposit rules, and upload official PDF policy
          documents for your parking lots.
        </p>
      </header>

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
        aria-label="Cập nhật văn bản chính sách bãi đỗ PDF"
      >
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 border-b border-[var(--border)] pb-4">
          <div>
            <div className="flex items-center gap-2">
              <h2 className="text-base font-bold text-[var(--fg)]">
                Tài Liệu Chính Sách Bãi Đỗ (File PDF)
              </h2>
              <span className="rounded px-2 py-0.5 text-xs font-semibold bg-blue-500/15 text-blue-500">
                Update Policy (PDF)
              </span>
            </div>
            <p className="mt-1 text-xs text-[var(--muted)]">
              Tải lên hoặc cập nhật văn bản chính sách đỗ xe chính thức. Tài xế
              sẽ có thể trực tiếp đọc tài liệu này khi tìm kiếm và đặt bãi.
            </p>
          </div>

          {/* Scope / Lot selector */}
          <div className="flex items-center gap-2">
            <label
              htmlFor="policy-lot-scope"
              className="text-xs font-medium text-[var(--muted)] whitespace-nowrap"
            >
              Áp dụng cho:
            </label>
            <select
              id="policy-lot-scope"
              className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-1.5 text-xs text-[var(--fg)] outline-none"
              value={selectedLotId}
              onChange={(e) => handleLotChange(e.target.value)}
            >
              <option value="all">Tất cả cơ sở (Mặc định)</option>
              {ownerLots.map((lot) => (
                <option key={lot.id} value={lot.id}>
                  {lot.name}
                </option>
              ))}
            </select>
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
                  {currentPdf?.fileName ||
                    `Chinh_Sach_Quy_Dinh_${(
                      ownerLots.find((l) => l.id === selectedLotId)?.name ||
                      "SmartPark"
                    ).replace(/\s+/g, "_")}.pdf`}
                </span>
                <span
                  className={`rounded px-1.5 py-0.5 text-[11px] font-semibold ${
                    currentPdf?.fileData
                      ? "bg-green-500/15 text-green-500"
                      : "bg-blue-500/15 text-blue-500"
                  }`}
                >
                  {currentPdf?.fileData
                    ? "File đã tải lên"
                    : "Tài liệu chuẩn SmartPark"}
                </span>
              </div>
              <p className="mt-1 text-xs text-[var(--muted)]">
                {currentPdf?.fileSize
                  ? `Dung lượng: ${currentPdf.fileSize} · `
                  : ""}
                Cập nhật:{" "}
                {new Date(
                  currentPdf?.uploadedAt || Date.now(),
                ).toLocaleDateString("vi-VN")}
              </p>
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
              <UntitledIcon name="upload" size={14} /> Tải lên File PDF mới
            </button>
            <button
              type="button"
              className="btn-outline text-xs py-2 px-3"
              onClick={handleOpenPreview}
            >
              <UntitledIcon name="eye" size={14} /> Xem & Đọc trước PDF
            </button>
            {currentPdf?.fileData && (
              <>
                <a
                  href={currentPdf.fileData}
                  download={currentPdf.fileName}
                  className="btn-outline text-xs py-2 px-3 no-underline inline-flex items-center gap-1.5"
                >
                  <UntitledIcon name="download" size={14} /> Tải về
                </a>
                <button
                  type="button"
                  className="rounded-lg border border-red-500/30 bg-red-500/10 px-3 py-2 text-xs font-semibold text-red-500 hover:bg-red-500/20"
                  onClick={handleDeletePdf}
                >
                  Xóa
                </button>
              </>
            )}
          </div>
        </div>

        <p className="text-xs text-[var(--muted)] flex items-center gap-1.5">
          <UntitledIcon name="check" size={13} />
          Văn bản chính sách PDF này được đồng bộ tức thì để Tài xế (Driver) có
          thể xem và tải về tại màn hình Tìm kiếm và Đặt chỗ.
        </p>
      </section>

      <section
        className="overflow-hidden rounded-xl border border-[var(--border)] bg-[var(--card)]"
        aria-label="Pricing and deposit policy settings"
      >
        <div className="overflow-x-auto">
          <table className="w-full min-w-[680px] border-collapse text-left">
            <thead>
              <tr className="border-b border-[var(--border)] bg-[var(--bg)]/50 text-xs uppercase tracking-wide text-[var(--muted)]">
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
                      <label className="space-y-1.5 text-xs font-medium text-[var(--muted)]">
                        Day Rate (₫ / hour)
                        <input
                          className={inputClass}
                          type="number"
                          min="0"
                          step="1000"
                          value={policy.dayRate}
                          onChange={(event) =>
                            updatePolicy("dayRate", event.target.value)
                          }
                        />
                      </label>
                      <label className="space-y-1.5 text-xs font-medium text-[var(--muted)]">
                        Night Rate (₫ / hour)
                        <input
                          className={inputClass}
                          type="number"
                          min="0"
                          step="1000"
                          value={policy.nightRate}
                          onChange={(event) =>
                            updatePolicy("nightRate", event.target.value)
                          }
                        />
                      </label>
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
                        <select
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
                          <option value="fixed">Fixed amount (₫)</option>
                        </select>
                      </label>
                      <label className="space-y-1.5 text-xs font-medium text-[var(--muted)]">
                        {policy.depositMode === "percentage"
                          ? "Deposit Percentage (%)"
                          : "Deposit Amount (₫)"}
                        <input
                          className={inputClass}
                          type="number"
                          min="0"
                          max={
                            policy.depositMode === "percentage"
                              ? 100
                              : undefined
                          }
                          step={policy.depositMode === "percentage" ? 1 : 1000}
                          value={policy.depositValue}
                          onChange={(event) =>
                            updatePolicy("depositValue", event.target.value)
                          }
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
          <p className="mt-4 text-xs leading-5 text-[var(--muted)]">
            Total = (day rate × day hours) + (night rate × night hours). Deposit
            is capped at the total; checkout collects the remaining balance.
          </p>
        </div>
      </section>

      {/* Policy document preview modal */}
      {previewLot && (
        <LotPolicyModal
          lot={previewLot}
          onClose={() => setPreviewLot(null)}
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
        {Math.round(amount).toLocaleString("vi-VN")} ₫
      </p>
    </div>
  )
}
