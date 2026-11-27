import { useEffect, useState } from "react"
import { getSessionGeneration } from "../../../lib/authApi"
import { passPlansApi, type PassVehicleType } from "../../../lib/passPlansApi"
import { Modal } from "../../../components/ui/Modal"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"

const vehicleLabels: Record<PassVehicleType, string> = {
  CAR: "Ô tô", MOTORCYCLE: "Xe máy", OVERSIZED: "Xe quá khổ",
}

export function PassOffersDialog({ siteId, name, address, onClose }: {
  siteId: string
  name: string
  address: string
  onClose: () => void
}) {
  return <Modal title={name + " — Gói gửi xe"} lang="vi" className="max-w-[820px]!" onClose={onClose}>
    <p className="mb-4 flex items-start gap-2 text-sm text-[var(--muted)]">
      <UntitledIcon name="map-pin" size={16} aria-hidden="true" /> {address}
    </p>
    <PassOffers key={siteId} siteId={siteId} />
  </Modal>
}

function PassOffers({ siteId }: { siteId: string }) {
  const [result, setResult] = useState<Awaited<ReturnType<typeof passPlansApi.offers>> | null>(null)
  const [error, setError] = useState("")
  const [retry, setRetry] = useState(0)

  useEffect(() => {
    let active = true
    const generation = getSessionGeneration()
    const current = () => active && generation === getSessionGeneration()
    void passPlansApi.offers(siteId)
      .then(value => { if (current()) setResult(value) })
      .catch(() => { if (current()) setError("Không tải được gói đang mở bán. Vui lòng thử lại.") })
    return () => { active = false }
  }, [siteId, retry])

  if (error) return <div className="flex flex-wrap items-center gap-3">
    <p role="alert">{error}</p>
    <button type="button" className="btn-outline" onClick={() => {
      setError("")
      setRetry(value => value + 1)
    }}>Thử lại</button>
  </div>
  if (!result) return <p role="status">Đang tải gói gửi xe…</p>

  return <div className="grid gap-3">
    {result.offers.length === 0 && <p role="status">Bãi này hiện chưa có gói gửi xe mở bán.</p>}
    {result.offers.map(offer => {
      const coverage = offer.ordinaryCoverageRules
      return <article key={`${offer.planId}:${offer.vehicleType}`} className="rounded-xl border border-[var(--border)] bg-[var(--bg)] p-4">
        <div className="flex flex-wrap items-start justify-between gap-2">
          <div>
            <h3 className="font-semibold">{offer.productType === "MONTHLY" ? "Gói tháng" : "Gói năm"}</h3>
            <p className="text-sm text-[var(--muted)]">{vehicleLabels[offer.vehicleType] ?? offer.vehicleType}</p>
          </div>
          <strong className="text-lg text-[var(--primary)]">{offer.fullPeriodPrice.toLocaleString("vi-VN")} {offer.currency}</strong>
        </div>
        <p className="mt-2 text-sm">
          {coverage.dayCovered && coverage.nightCovered ? "Ban ngày và qua đêm" : coverage.dayCovered ? "Ban ngày" : "Qua đêm"}
          {" · "}{offer.zoneId ? "Áp dụng tại khu vực chỉ định" : coverage.excludedZoneIds.length ? "Áp dụng tại các khu vực đủ điều kiện" : "Áp dụng toàn bãi"}
        </p>
      </article>
    })}
    {result.offers.length > 0 && <p className="text-sm text-[var(--muted)]">Gói chỉ sử dụng khi còn chỗ. Tính năng mua và kích hoạt gói chưa khả dụng.</p>}
    {result.yearlyBlockedByQ05 && <p className="text-sm text-[var(--muted)]">Gói năm hiện chưa mở bán.</p>}
  </div>
}
