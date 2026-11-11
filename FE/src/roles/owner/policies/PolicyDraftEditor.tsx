import { useEffect, useState } from "react"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { Modal } from "../../../components/ui/Modal"
import type { Site } from "../../../lib/parkingApi"
import { PolicyPencil } from "./PolicyPencil"

export const policyTopics = [
  ["rules", "General Parking Rules", "Entry, exit, parking tickets, vehicle identification, and customer responsibilities."],
  ["booking", "Booking and Cancellation", "Booking, cancellations, late arrivals, and reservation expiry."],
  ["overtime", "Pricing and Overtime", "Rates by vehicle and duration, overtime, and additional charges."],
  ["refund", "Deposit and Refund", "Deposit calculation, refund eligibility, and cancellation handling."],
  ["safety", "Vehicle Safety", "Vehicle protection, lost tickets, incidents, damage, and disputes."],
  ["noShow", "No-show Policy", "No-show criteria, reservation expiry, and deposit handling."],
] as const

const rateFields = ["Hourly rate", "Daily rate", "Overnight rate", "Overtime fee"] as const
const vehicles = ["Motorcycle", "Car"] as const
const pricingFields = vehicles.flatMap(vehicle => rateFields.map(label => ({ key: vehicle + label, vehicle, label })))
const tabs = ["Policies", "Pricing", "Deposit"] as const
const vnd = new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND" })

export type PolicyDraft = Partial<Record<string, string>>
export const starterPolicyDraft = {
  rules: [
    "Ra vào bằng lối được chỉ dẫn và tuân theo biển báo, vạch kẻ cùng hướng dẫn của nhân viên.",
    "Chuẩn bị vé, mã đặt chỗ hoặc thông tin biển số để đối chiếu khi cần. Giữ vé hoặc mã xác nhận đến khi rời bãi.",
    "Đỗ đúng vị trí được chỉ định. Giữ thông thoáng lối đi, lối thoát hiểm và khu vực dành riêng.",
  ].join("\n\n"),
  booking: [
    "Kiểm tra bãi đỗ, loại xe, biển số và khung giờ trước khi xác nhận đặt chỗ.",
    "Nếu cần đổi hoặc hủy, xem điều kiện của lượt đặt và gửi yêu cầu qua mục quản lý đặt chỗ.",
    "Nếu đến muộn, kiểm tra trạng thái đặt chỗ trước khi vào bãi. Thời hạn giữ chỗ được ghi trong thông tin đặt chỗ.",
  ].join("\n\n"),
  overtime: [
    "Xem biểu giá đang áp dụng cho loại xe và thời lượng dự kiến trước khi đặt chỗ hoặc vào bãi.",
    "Nếu đỗ quá giờ, xem mức phí quá giờ và phụ phí đã được công bố cho bãi. Hỏi nhân viên nếu cần đối chiếu khoản thu.",
  ].join("\n\n"),
  refund: [
    "Nếu lượt đặt yêu cầu đặt cọc, kiểm tra số tiền và điều kiện áp dụng được hiển thị trước khi xác nhận.",
    "Khi yêu cầu hủy hoặc hoàn tiền, cung cấp mã đặt chỗ và thông tin giao dịch. Việc xử lý căn cứ vào điều kiện của lượt đặt và Chính sách Thanh toán & Hoàn tiền của SmartParking.",
  ].join("\n\n"),
  safety: [
    "Khóa xe, đóng cửa kính và mang theo đồ có giá trị khi rời xe.",
    "Nếu mất vé hoặc mã vào bãi, liên hệ nhân viên để xác minh thông tin xe trước khi ra bãi.",
    "Báo ngay cho nhân viên khi phát hiện hư hỏng, va chạm hoặc nguy cơ mất an toàn. Giữ lại mã đặt chỗ, thời gian và thông tin liên quan để hỗ trợ xử lý.",
  ].join("\n\n"),
  noShow: [
    "Nếu không thể đến, kiểm tra điều kiện hủy và gửi yêu cầu sớm qua mục quản lý đặt chỗ.",
    "Nếu không đến trong thời hạn giữ chỗ, kiểm tra trạng thái trên SmartParking trước khi tiếp tục hành trình. Điều kiện xử lý tiền cọc được ghi trong lượt đặt và chính sách nền tảng áp dụng.",
  ].join("\n\n"),
} satisfies Record<(typeof policyTopics)[number][0], string>

export const policyPanelClass = "rounded-xl border border-[var(--border)] bg-[var(--card)] p-4 sm:p-5"
const fieldClass = "input w-full rounded-lg border border-[var(--border)] px-3 py-2"
const rateStepButtonClass = "group grid size-11 place-items-center rounded-full text-sm font-semibold"
const rateStepIconClass = "grid size-8 place-items-center rounded-full border border-[var(--control)] bg-[var(--card)] group-hover:bg-[var(--raised)] motion-safe:transition-colors"
const depositKey = "depositPercentage"

export function PolicyDraftEditor({ site, defaults, overrides, onClose, onKeep }: {
  site?: Site
  defaults: PolicyDraft
  overrides: PolicyDraft
  onClose: () => void
  onKeep: (draft: PolicyDraft) => void
}) {
  const [draft, setDraft] = useState(site ? overrides : defaults)
  const [tab, setTab] = useState<(typeof tabs)[number]>("Policies")
  const [openTopic, setOpenTopic] = useState<string>()
  const [mode, setMode] = useState<"edit" | "preview" | "reset">("edit")
  const [error, setError] = useState("")
  const effective = site ? { ...defaults, ...draft } : draft
  const dirty = draft !== (site ? overrides : defaults)

  useEffect(() => {
    if (!dirty) return
    function warn(event: BeforeUnloadEvent) {
      event.preventDefault()
      event.returnValue = ""
    }
    window.addEventListener("beforeunload", warn)
    return () => window.removeEventListener("beforeunload", warn)
  }, [dirty])

  function update(key: string, value: string) {
    setError("")
    setDraft(current => ({ ...current, [key]: value }))
  }

  function inherit(key: string) {
    setError("")
    setDraft(current => {
      const next = { ...current }
      delete next[key]
      return next
    })
  }

  function keepDraft() {
    for (const [key, title] of policyTopics) {
      if (draft[key] !== undefined && !draft[key].trim()) {
        setTab("Policies")
        setOpenTopic(key)
        setError("Enter text for " + title + " or leave the section unset.")
        return
      }
    }
    for (const { key, vehicle, label } of pricingFields) {
      const value = draft[key]
      const amount = Number(value)
      if (value !== undefined && (!/^\d+$/.test(value) || !Number.isSafeInteger(amount) || amount % 1000 !== 0)) {
        setTab("Pricing")
        setError(vehicle + " " + label.toLowerCase() + " must be a non-negative multiple of 1,000 VND.")
        return
      }
    }
    const deposit = draft[depositKey]
    if (deposit !== undefined && (!/^\d+$/.test(deposit) || Number(deposit) > 100)) {
      setTab("Deposit")
      setError("Enter a whole deposit percentage from 0 to 100.")
      return
    }
    onKeep(draft)
  }

  function fieldSource(key: string) {
    if (site) {
      return draft[key] === undefined
        ? <small className="text-[var(--muted)]">Inherited from default draft</small>
        : <div className="mt-3 flex flex-wrap items-center justify-between gap-3">
            <small className="text-[var(--muted)]">Customized in this draft</small>
            <button type="button" className="btn-outline text-xs" onClick={() => inherit(key)}>Use default</button>
          </div>
    }
    return draft[key] !== undefined && <div className="mt-2 flex justify-start">
      <button type="button" className="btn-outline inline-flex items-center gap-2 text-xs" onClick={() => inherit(key)}>
        <UntitledIcon name="x" size={14} /> Clear draft field
      </button>
    </div>
  }

  const status = Object.keys(draft).length ? "Customized draft" : "Default draft"

  return <Modal title={site ? "Edit Parking Lot Policy" : "Edit Default Policy"} className="max-w-3xl!" onClose={onClose}>
    <div className="grid gap-4">
      {site && <header className="break-words">
        <p className="font-semibold">{site.name}</p>
        <p className="text-sm text-[var(--muted)]">{site.address}</p>
        <p className="mt-1 text-sm text-[var(--muted)]">{status}</p>
      </header>}
      <p className="rounded-lg bg-[var(--bg)] p-3 text-sm text-[var(--muted)]">Blocked by Backend. Drafts stay on this page and do not change published policies, prices, or deposits.</p>
      {mode === "reset" ? <section className={policyPanelClass} aria-labelledby="policy-reset-heading">
        <h3 id="policy-reset-heading" className="font-semibold">Reset to Default?</h3>
        <p className="mt-2 text-sm">This removes all {Object.keys(draft).length} draft overrides for this parking lot. Its draft will inherit the current default values.</p>
        <div className="mt-4 flex flex-wrap gap-2">
          <button type="button" className="btn-outline" onClick={() => setMode("edit")}>Cancel reset</button>
          <button type="button" className="btn-danger" onClick={() => { setDraft({}); setError(""); setMode("edit") }}>Reset draft overrides</button>
        </div>
      </section> : mode === "preview" ? <section className="grid gap-4" aria-label="Draft preview">
        {policyTopics.map(([key, title]) => <article key={key} className={policyPanelClass}>
          <h3 className="font-semibold">{title}</h3>
          <p lang="vi" className="mt-2 whitespace-pre-wrap break-words text-sm text-[var(--muted)]">{effective[key]?.trim() || "Not set"}</p>
        </article>)}
        <article className={policyPanelClass}>
          <h3 className="font-semibold">Pricing and Deposit</h3>
          <dl className="mt-2 grid gap-3 text-sm sm:grid-cols-2">
            {pricingFields.map(({ key, vehicle, label }) => <div key={key}><dt>{vehicle} · {label}</dt><dd>{effective[key] ? vnd.format(Number(effective[key])) : "Not set"}</dd></div>)}
            <div><dt>Deposit percentage</dt><dd>{effective[depositKey] ? effective[depositKey] + "%" : "Not set"}</dd></div>
          </dl>
        </article>
        <button type="button" className="btn-outline justify-self-start" onClick={() => setMode("edit")}>Back to editor</button>
      </section> : <>
        <div role="group" aria-label="Editor sections" className="flex flex-wrap gap-2">
          {tabs.map(item => <button key={item} type="button" className={tab === item ? "btn-primary" : "btn-outline"} aria-pressed={tab === item} onClick={() => setTab(item)}>{item}</button>)}
        </div>
        {tab === "Policies" && <div className="grid gap-2">
          {policyTopics.map(([key, title, description]) => <section key={key} className={policyPanelClass}>
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <h3 className="font-semibold">{title}</h3>
                <p className="mt-1 text-sm text-[var(--muted)]">{description}</p>
              </div>
              <button type="button" className="btn-outline shrink-0" title="Edit Policy" aria-label={"Edit " + title} aria-expanded={openTopic === key} onClick={() => setOpenTopic(openTopic === key ? undefined : key)}><PolicyPencil /></button>
            </div>
            {openTopic === key && <div className="mt-3 grid gap-2">
              <textarea lang="vi" className={fieldClass} rows={6} aria-label={title} value={effective[key] ?? ""} onChange={event => update(key, event.target.value)} />
              {fieldSource(key)}
            </div>}
          </section>)}
        </div>}
        {tab === "Pricing" && <div className="grid gap-4 sm:grid-cols-2">
          {vehicles.map(vehicle => <section key={vehicle} className={policyPanelClass + " min-w-0"}>
            <h3 className="flex items-center gap-2 border-b border-[var(--border)] pb-3 font-semibold">
              <span aria-hidden="true" className="grid size-9 place-items-center rounded-lg bg-[var(--bg)] text-[var(--primary)]"><UntitledIcon name={vehicle === "Car" ? "car" : "motorcycle"} size={18} /></span>
              {vehicle}
            </h3>
            <div className="mt-4 grid gap-5">
              {pricingFields.filter(field => field.vehicle === vehicle).map(({ key, label }, index) => {
                const amount = Number(effective[key]) || 0
                const id = vehicle + "-rate-" + index
                return <div key={key} className="grid gap-2">
                  <label className="text-sm font-medium" htmlFor={id}>{label} (VND)</label>
                  <div className="grid grid-cols-[44px_minmax(0,1fr)_44px] items-center gap-2">
                    <button type="button" className={rateStepButtonClass} aria-label={"Decrease " + vehicle + " " + label + " by 1,000 VND"} disabled={amount <= 0}
                      onClick={() => update(key, String(Math.max(0, (Math.ceil(amount / 1000) - 1) * 1000)))}><span aria-hidden="true" className={rateStepIconClass}>−</span></button>
                    <input id={id} className={fieldClass + " min-h-11 min-w-0 appearance-none tabular-nums [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none [appearance:textfield]"} type="number" inputMode="numeric" min={0} step={1000} value={effective[key] ?? ""} onChange={event => update(key, event.target.value)} />
                    <button type="button" className={rateStepButtonClass} aria-label={"Increase " + vehicle + " " + label + " by 1,000 VND"}
                      onClick={() => update(key, String(Math.max(0, (Math.floor(amount / 1000) + 1) * 1000)))}><span aria-hidden="true" className={rateStepIconClass}><UntitledIcon name="plus" size={14} /></span></button>
                  </div>
                  {effective[key] && <small className="text-[var(--muted)]">{vnd.format(amount)}</small>}
                  {fieldSource(key)}
                </div>
              })}
            </div>
          </section>)}
          <p className="rounded-lg bg-[var(--bg)] px-3 py-2 text-sm text-[var(--muted)] sm:col-span-2">Adjust rates in steps of 1,000 VND using the buttons or arrow keys.</p>
        </div>}
        {tab === "Deposit" && <section className={policyPanelClass}>
          <h3 className="font-semibold">Owner Deposit Configuration</h3>
          <label className="mt-3 block text-sm" htmlFor="policy-draft-deposit">Deposit percentage (0–100%)</label>
          <input id="policy-draft-deposit" className={fieldClass + " mt-1"} type="number" min={0} max={100} step={1} value={effective[depositKey] ?? ""} onChange={event => update(depositKey, event.target.value)} />
          {fieldSource(depositKey)}
          <p className="mt-3 text-sm">Booking conditions: {effective.booking?.trim() || "Not set"}</p>
          <p className="mt-1 text-sm">Refund conditions: {effective.refund?.trim() || "Not set"}</p>
          <h4 className="mt-4 font-semibold">Platform Deposit Rules</h4>
          <p className="mt-1 text-sm text-[var(--muted)]">Read-only. See Payment &amp; Refund in Platform Policies. Live calculation preview requires a supported deposit quote.</p>
        </section>}
        {error && <p role="alert" className="rounded-lg border border-red-500/40 p-3">{error}</p>}
        <footer className="flex flex-wrap items-center justify-end gap-3 border-t border-[var(--border)] pt-4">
          {site && Object.keys(draft).length > 0 && <button type="button" className="btn-ghost mr-auto" onClick={() => setMode("reset")}>Reset to Default</button>}
          <button type="button" className="btn-outline" onClick={onClose}>Cancel</button>
          <button type="button" className="btn-outline" onClick={() => setMode("preview")}>Preview</button>
          <button type="button" className="btn-outline" disabled>Save Changes</button>
          <button type="button" className="btn-primary" onClick={keepDraft}>Keep Draft</button>
        </footer>
      </>}
    </div>
  </Modal>
}
