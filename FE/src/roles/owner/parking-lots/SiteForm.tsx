import { useState, useId, useMemo, type FormEvent } from "react"
import { parkingApi, type Site } from "../../../lib/parkingApi"
import { ownerData as store } from "../data/data"
import { useApp } from "../../../context/AppContext"
import type { LotType } from "../../../lib/types"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { ALLOWED_PARKING_MODELS } from "../../../lib/3d/parking3DConfig"

const LOT_TYPE_INFO: Record<LotType, {
  icon: string
  label: string
  hasFloors: boolean
  desc: string
}> = {
  outdoor: { icon: "🌳", label: "Outdoor Lot", hasFloors: false, desc: "Bãi đỗ ngoài trời mặt đất" },
  basement: { icon: "🏢", label: "Basement Parking", hasFloors: true, desc: "Hầm gửi xe ngầm có phân tầng" },
  "multi-storey": { icon: "🏗️", label: "Multi-Storey", hasFloors: true, desc: "Tòa nhà nhà để xe cao tầng" },
}

const CITIES = [
  { code: "HCM", name: "TP. Hồ Chí Minh", defaultLat: 10.7769, defaultLng: 106.7009, districts: ["Quận 1", "Quận 3", "TP. Thủ Đức", "Quận Bình Thạnh", "Quận 7", "Quận Tân Bình"] },
  { code: "HN", name: "Hà Nội", defaultLat: 21.0285, defaultLng: 105.8542, districts: ["Quận Hoàn Kiếm", "Quận Ba Đình", "Quận Cầu Giấy", "Quận Đống Đa", "Quận Nam Từ Liêm"] },
  { code: "DN", name: "Đà Nẵng", defaultLat: 16.0544, defaultLng: 108.2022, districts: ["Quận Hải Châu", "Quận Sơn Trà", "Quận Thanh Khê", "Quận Ngũ Hành Sơn"] },
  { code: "BD", name: "Bình Dương", defaultLat: 10.9804, defaultLng: 106.6519, districts: ["TP. Thủ Dầu Một", "TP. Thuận An", "TP. Dĩ An"] },
  { code: "HP", name: "Hải Phòng", defaultLat: 20.8449, defaultLng: 106.6881, districts: ["Quận Hồng Bàng", "Quận Ngô Quyền", "Quận Lê Chân"] },
  { code: "CT", name: "Cần Thơ", defaultLat: 10.0452, defaultLng: 105.7469, districts: ["Quận Ninh Kiều", "Quận Bình Thủy", "Quận Cái Răng"] },
]

const DEVICE_OPTIONS = [
  { type: "camera", label: "AI Camera System", desc: "Tự động nhận diện biển số (ANPR)" },
  { type: "barrier", label: "Barrier Gate", desc: "Hệ thống cổng chắn tự động đóng/mở" },
  { type: "sensor", label: "Slot Sensors", desc: "Cảm biến sóng âm/quang học nhận diện xe tại chỗ" },
  { type: "rfid", label: "RFID Card Reader", desc: "Đầu đọc thẻ từ kiểm soát ra vào" },
  { type: "ev-charger", label: "EV Charging Station", desc: "Trụ sạc xe điện thông minh" },
]

function generateSlug(text: string): string {
  return text
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d").replace(/Đ/g, "D")
    .toUpperCase()
    .replace(/[^A-Z0-9]/g, "")
    .slice(0, 8)
}

export function SiteForm({
  site,
  onSaved,
  onCancel,
}: {
  site?: Site
  onSaved: (site: Site) => void
  onCancel: () => void
}) {
  const { user } = useApp()
  const formId = useId()

  // Infer lot type from existing site or default to outdoor
  const [lotType, setLotType] = useState<LotType>(() => {
    if (site?.name.toLowerCase().includes("hầm") || site?.name.toLowerCase().includes("basement")) return "basement"
    if (site?.name.toLowerCase().includes("tầng") || site?.name.toLowerCase().includes("storey")) return "multi-storey"
    return "outdoor"
  })

  // Address Parsing
  const [selectedCity, setSelectedCity] = useState(CITIES[0].code)
  const [district, setDistrict] = useState(CITIES[0].districts[0])
  const [streetAddress, setStreetAddress] = useState(site?.address ? site.address.split(",")[0].trim() : "")
  const [isManualCode, setIsManualCode] = useState(Boolean(site?.code))

  const [form, setForm] = useState({
    code: site?.code ?? "",
    name: site?.name ?? "",
    address: site?.address ?? "",
    totalSlots: site?.totalPhysicalCapacity ? String(site.totalPhysicalCapacity) : "50",
    floors: "1",
    latitude: site?.latitude ?? CITIES[0].defaultLat,
    longitude: site?.longitude ?? CITIES[0].defaultLng,
    isActive: site?.isActive ?? true,
    hourlyRate: 15000,
    dailyRate: 100000,
  })

  const [devices, setDevices] = useState<Record<string, boolean>>({
    camera: true,
    barrier: true,
    sensor: true,
    rfid: false,
    "ev-charger": false,
  })

  const [busy, setBusy] = useState(false)
  const [error, setError] = useState("")

  // Auto-generate Site Code based on Smart Parking logic: SPK-[CITY]-[SLUG]
  const computedCode = useMemo(() => {
    const slug = generateSlug(form.name) || "PARK"
    return `SPK-${selectedCity}-${slug}`
  }, [selectedCity, form.name])

  // Computed full address
  const fullAddress = useMemo(() => {
    const cityObj = CITIES.find((c) => c.code === selectedCity)
    const parts = [streetAddress.trim(), district, cityObj?.name].filter(Boolean)
    return parts.join(", ")
  }, [streetAddress, district, selectedCity])

  function handleCityChange(newCityCode: string) {
    setSelectedCity(newCityCode)
    const found = CITIES.find((c) => c.code === newCityCode)
    if (found) {
      setDistrict(found.districts[0] || "")
      setForm((cur) => ({
        ...cur,
        latitude: found.defaultLat,
        longitude: found.defaultLng,
        code: isManualCode ? cur.code : `SPK-${newCityCode}-${generateSlug(cur.name) || "PARK"}`,
      }))
    }
  }

  function handleNameChange(newName: string) {
    setForm((cur) => ({
      ...cur,
      name: newName,
      code: isManualCode ? cur.code : `SPK-${selectedCity}-${generateSlug(newName) || "PARK"}`,
    }))
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (busy) return

    const siteCode = (isManualCode && form.code.trim() ? form.code.trim() : computedCode).toUpperCase()
    const siteName = form.name.trim()
    const finalAddress = fullAddress || form.address.trim() || `${form.name}, ${district}, ${selectedCity}`

    if (!siteCode || !siteName || !finalAddress) {
      setError("Vui lòng nhập đầy đủ Mã bãi đỗ, Tên bãi đỗ và Địa chỉ.")
      return
    }

    const totalSlotsNum = parseInt(form.totalSlots, 10) || 50
    const floorsNum = LOT_TYPE_INFO[lotType].hasFloors ? parseInt(form.floors, 10) || 1 : 1

    setError("")
    setBusy(true)

    try {
      const activeModelDef = ALLOWED_PARKING_MODELS[lotType]

      if (site) {
        // Edit existing site
        await parkingApi.edit(site.id, {
          action: "profile",
          code: siteCode,
          name: siteName,
          address: finalAddress,
          latitude: form.latitude,
          longitude: form.longitude,
        })

        // Also sync local store lot
        try {
          store.saveLot({
            id: site.id,
            ownerId: user?.id ?? "owner-001",
            name: siteName,
            type: lotType,
            address: finalAddress,
            totalSlots: totalSlotsNum,
            floors: floorsNum,
            modelUrl: activeModelDef.path,
            modelFileName: activeModelDef.fileName,
            slots: store.generateSlots(totalSlotsNum, floorsNum),
            devices: Object.entries(devices).filter(([, v]) => v).map(([k]) => ({
              type: k as any,
              label: DEVICE_OPTIONS.find((d) => d.type === k)?.label ?? k,
              enabled: true,
            })),
            hourlyRate: form.hourlyRate,
            dailyRate: form.dailyRate,
            nightRate: Math.round(form.dailyRate / 2),
            gracePeriodMinutes: 15,
            subscriptionMonthly: 800000,
            subscriptionYearly: 8000000,
            status: form.isActive ? "active" : "inactive",
            lat: form.latitude,
            lng: form.longitude,
          })
        } catch {
          // ignore local store sync failures
        }

        onSaved({
          ...site,
          code: siteCode,
          name: siteName,
          address: finalAddress,
          latitude: form.latitude,
          longitude: form.longitude,
          totalPhysicalCapacity: totalSlotsNum,
          isActive: form.isActive,
        })
      } else {
        // Create new site via API
        let createdSite: Site
        try {
          createdSite = await parkingApi.create({
            code: siteCode,
            name: siteName,
            address: finalAddress,
          })
        } catch {
          // Fallback if backend API offline: generate local client Site
          createdSite = {
            id: `site-${Date.now()}`,
            tenantId: user?.id ?? "tenant-001",
            code: siteCode,
            name: siteName,
            address: finalAddress,
            latitude: form.latitude,
            longitude: form.longitude,
            status: "ACTIVE",
            isActive: true,
            totalPhysicalCapacity: totalSlotsNum,
          }
        }

        // Also persist to owner store for visual lot manager
        try {
          store.createLot({
            ownerId: user?.id ?? "owner-001",
            name: siteName,
            type: lotType,
            address: finalAddress,
            totalSlots: totalSlotsNum,
            floors: floorsNum,
            modelUrl: activeModelDef.path,
            modelFileName: activeModelDef.fileName,
            slots: store.generateSlots(totalSlotsNum, floorsNum),
            devices: Object.entries(devices).filter(([, v]) => v).map(([k]) => ({
              type: k as any,
              label: DEVICE_OPTIONS.find((d) => d.type === k)?.label ?? k,
              enabled: true,
            })),
            hourlyRate: form.hourlyRate,
            dailyRate: form.dailyRate,
            nightRate: Math.round(form.dailyRate / 2),
            gracePeriodMinutes: 15,
            subscriptionMonthly: 800000,
            subscriptionYearly: 8000000,
            status: "active",
            lat: form.latitude,
            lng: form.longitude,
          })
        } catch {
          // ignore local store sync errors
        }

        onSaved(createdSite)
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể lưu thông tin bãi đỗ xe.")
    } finally {
      setBusy(false)
    }
  }

  return (
    <form className="space-y-5 text-[var(--fg)] max-h-[85vh] overflow-y-auto pr-1" onSubmit={submit}>
      {/* Header */}
      <div className="border-b border-[var(--border)] pb-3">
        <h3 className="text-lg font-bold text-[var(--fg)] flex items-center gap-2">
          <span>{site ? "Chỉnh sửa bãi đỗ xe" : "Thiết lập bãi đỗ xe mới"}</span>
          <span className="text-xs px-2 py-0.5 rounded-full bg-[var(--primary)]/10 text-[var(--primary)] font-semibold">
            Smart Facility
          </span>
        </h3>
        <p className="text-xs text-[var(--muted)] mt-1">
          {site
            ? "Cập nhật loại hình, cấu trúc bãi, quy tắc định danh chuẩn và thiết bị vận hành."
            : "Khởi tạo cơ sở đỗ xe thông minh theo chuẩn cấu trúc Owner. Các thông số sẽ đồng bộ tự động với sơ đồ vận hành."}
        </p>
      </div>

      <fieldset disabled={busy} className="space-y-4.5" style={{ border: 0, padding: 0, margin: 0 }}>
        {/* Section 1: Choose Lot Type (Giống form Owner) */}
        <div className="space-y-2">
          <label className="label font-semibold text-xs">Loại hình bãi đỗ (Structural Type) *</label>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-2.5">
            {(Object.entries(LOT_TYPE_INFO) as [LotType, (typeof LOT_TYPE_INFO)[LotType]][]).map(
              ([type, info]) => {
                const selected = lotType === type
                return (
                  <div
                    key={type}
                    onClick={() => setLotType(type)}
                    className={`p-3 rounded-xl border-2 cursor-pointer transition-all flex flex-col justify-between ${
                      selected
                        ? "border-[var(--primary)] bg-[var(--primary)]/10 text-[var(--fg)] shadow-xs"
                        : "border-[var(--border)] bg-[var(--bg)] text-[var(--muted)] hover:border-[var(--border)]/80"
                    }`}
                  >
                    <div className="flex items-center justify-between mb-1.5">
                      <span className="text-xl">{info.icon}</span>
                      {selected && (
                        <span className="text-[var(--primary)]">
                          <UntitledIcon name="check" size={14} />
                        </span>
                      )}
                    </div>
                    <div>
                      <div className="font-semibold text-xs text-[var(--fg)]">{info.label}</div>
                      <div className="text-[10px] text-[var(--muted)] line-clamp-1">{info.desc}</div>
                    </div>
                  </div>
                )
              },
            )}
          </div>
        </div>

        {/* Section 2: Site Identification with Smart Code Logic */}
        <div className="rounded-xl border border-[var(--border)] bg-[var(--bg)]/50 p-4 space-y-3.5">
          <div className="font-semibold text-xs text-[var(--fg)] flex items-center justify-between">
            <span>Định danh cơ sở đỗ xe (Site Identification)</span>
            <span className="text-[11px] font-mono text-[var(--primary)]">
              Mã chuẩn: {isManualCode && form.code ? form.code.toUpperCase() : computedCode}
            </span>
          </div>

          <div className="grid gap-3 sm:grid-cols-2">
            <div>
              <label className="label" htmlFor={`${formId}-site-name`}>
                Tên bãi đỗ xe (Site Name) *
              </label>
              <input
                id={`${formId}-site-name`}
                className="input"
                maxLength={255}
                placeholder="Ví dụ: Vincom Center Đồng Khởi"
                value={form.name}
                onChange={(e) => handleNameChange(e.target.value)}
                required
              />
            </div>

            <div>
              <div className="flex items-center justify-between mb-1">
                <label className="label mb-0" htmlFor={`${formId}-site-code`}>
                  Mã bãi đỗ (Site Code) *
                </label>
                <button
                  type="button"
                  className="text-[10px] text-[var(--primary)] hover:underline"
                  onClick={() => {
                    setIsManualCode(!isManualCode)
                    if (isManualCode) {
                      setForm((c) => ({ ...c, code: computedCode }))
                    }
                  }}
                >
                  {isManualCode ? "Tự động sinh mã" : "Sửa mã thủ công"}
                </button>
              </div>
              <input
                id={`${formId}-site-code`}
                className="input font-mono uppercase"
                maxLength={50}
                placeholder={computedCode}
                value={isManualCode ? form.code : computedCode}
                onChange={(e) => {
                  setIsManualCode(true)
                  setForm((c) => ({ ...c, code: e.target.value.toUpperCase() }))
                }}
                required
              />
            </div>
          </div>
        </div>

        {/* Section 3: Structured Address */}
        <div className="rounded-xl border border-[var(--border)] bg-[var(--bg)]/50 p-4 space-y-3.5">
          <div className="font-semibold text-xs text-[var(--fg)]">
            Địa chỉ & Tọa độ định vị (Location & Coordinates)
          </div>

          <div className="grid gap-3 sm:grid-cols-2">
            <div>
              <label className="label" htmlFor={`${formId}-city`}>
                Tỉnh / Thành phố *
              </label>
              <select
                id={`${formId}-city`}
                className="input"
                value={selectedCity}
                onChange={(e) => handleCityChange(e.target.value)}
              >
                {CITIES.map((c) => (
                  <option key={c.code} value={c.code}>
                    {c.name} ({c.code})
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label className="label" htmlFor={`${formId}-district`}>
                Quận / Huyện *
              </label>
              <select
                id={`${formId}-district`}
                className="input"
                value={district}
                onChange={(e) => setDistrict(e.target.value)}
              >
                {(CITIES.find((c) => c.code === selectedCity)?.districts ?? []).map((d) => (
                  <option key={d} value={d}>
                    {d}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div>
            <label className="label" htmlFor={`${formId}-street`}>
              Số nhà, tên đường, khu phức hợp *
            </label>
            <input
              id={`${formId}-street`}
              className="input"
              maxLength={500}
              placeholder="Ví dụ: Số 72 Lê Thánh Tôn, Phường Bến Nghé"
              value={streetAddress}
              onChange={(e) => setStreetAddress(e.target.value)}
              required
            />
          </div>

          {/* Full address summary preview */}
          <div className="p-2.5 rounded-lg border border-[var(--border)] bg-[var(--card)] text-xs text-[var(--muted)] flex items-start gap-2">
            <UntitledIcon name="map-pin" size={14} className="text-[var(--primary)] mt-0.5 shrink-0" />
            <div className="leading-relaxed">
              <span className="font-semibold text-[var(--fg)]">Địa chỉ hiển thị hoàn chỉnh: </span>
              <span>{fullAddress || "Chưa hoàn tất nhập địa chỉ"}</span>
            </div>
          </div>
        </div>

        {/* Section 4: Physical Capacity & Structure */}
        <div className="rounded-xl border border-[var(--border)] bg-[var(--bg)]/50 p-4 space-y-3.5">
          <div className="font-semibold text-xs text-[var(--fg)]">
            Quy mô & Cấu trúc phân tầng
          </div>

          <div
            className={`grid gap-3 ${
              LOT_TYPE_INFO[lotType].hasFloors ? "sm:grid-cols-2" : "grid-cols-1"
            }`}
          >
            <div>
              <label className="label" htmlFor={`${formId}-slots`}>
                Tổng số slot đỗ xe (Total Slots) *
              </label>
              <input
                id={`${formId}-slots`}
                className="input"
                type="number"
                min={1}
                max={5000}
                value={form.totalSlots}
                onChange={(e) => setForm((c) => ({ ...c, totalSlots: e.target.value }))}
                required
              />
            </div>

            {LOT_TYPE_INFO[lotType].hasFloors && (
              <div>
                <label className="label" htmlFor={`${formId}-floors`}>
                  Số tầng / Hầm (Number of Levels) *
                </label>
                <input
                  id={`${formId}-floors`}
                  className="input"
                  type="number"
                  min={1}
                  max={20}
                  value={form.floors}
                  onChange={(e) => setForm((c) => ({ ...c, floors: e.target.value }))}
                  required
                />
                {form.totalSlots && parseInt(form.floors, 10) > 0 && (
                  <p className="mt-1 text-[11px] text-[var(--muted)]">
                    ~ {Math.ceil(parseInt(form.totalSlots, 10) / parseInt(form.floors, 10))} slots / tầng
                  </p>
                )}
              </div>
            )}
          </div>
        </div>

        {/* Section 5: Installed IoT Devices */}
        <div className="rounded-xl border border-[var(--border)] bg-[var(--bg)]/50 p-4 space-y-3">
          <div className="font-semibold text-xs text-[var(--fg)]">
            Hạ tầng thiết bị thông minh (Installed IoT Devices)
          </div>
          <div className="grid gap-2 sm:grid-cols-2">
            {DEVICE_OPTIONS.map((dev) => {
              const checked = Boolean(devices[dev.type])
              return (
                <label
                  key={dev.type}
                  className={`flex items-start gap-2.5 p-2 rounded-lg border cursor-pointer transition-colors ${
                    checked
                      ? "border-[var(--primary)]/50 bg-[var(--card)]"
                      : "border-[var(--border)] bg-[var(--card)]/50 opacity-75"
                  }`}
                >
                  <input
                    type="checkbox"
                    className="mt-0.5 accent-[var(--primary)]"
                    checked={checked}
                    onChange={(e) =>
                      setDevices((cur) => ({ ...cur, [dev.type]: e.target.checked }))
                    }
                  />
                  <div>
                    <div className="text-xs font-semibold text-[var(--fg)]">{dev.label}</div>
                    <div className="text-[10px] text-[var(--muted)]">{dev.desc}</div>
                  </div>
                </label>
              )
            })}
          </div>
        </div>

        {/* Section 6: Base Pricing & Active Status */}
        <div className="rounded-xl border border-[var(--border)] bg-[var(--bg)]/50 p-4 space-y-3.5">
          <div className="flex items-center justify-between">
            <span className="font-semibold text-xs text-[var(--fg)]">
              Bảng giá cơ bản & Trạng thái bãi
            </span>
            <label className="flex items-center gap-2 cursor-pointer text-xs">
              <input
                type="checkbox"
                className="accent-emerald-500"
                checked={form.isActive}
                onChange={(e) => setForm((c) => ({ ...c, isActive: e.target.checked }))}
              />
              <span className={`font-semibold ${form.isActive ? "text-emerald-600 dark:text-emerald-400" : "text-[var(--muted)]"}`}>
                {form.isActive ? "Hoạt động (Active)" : "Tạm ngưng (Inactive)"}
              </span>
            </label>
          </div>

          <div className="grid gap-3 sm:grid-cols-2">
            <div>
              <label className="label" htmlFor={`${formId}-hourly-rate`}>
                Giá theo giờ (VND / giờ)
              </label>
              <input
                id={`${formId}-hourly-rate`}
                className="input"
                type="number"
                step={1000}
                min={0}
                value={form.hourlyRate}
                onChange={(e) => setForm((c) => ({ ...c, hourlyRate: Number(e.target.value) }))}
              />
            </div>
            <div>
              <label className="label" htmlFor={`${formId}-daily-rate`}>
                Giá theo ngày (VND / ngày)
              </label>
              <input
                id={`${formId}-daily-rate`}
                className="input"
                type="number"
                step={5000}
                min={0}
                value={form.dailyRate}
                onChange={(e) => setForm((c) => ({ ...c, dailyRate: Number(e.target.value) }))}
              />
            </div>
          </div>
        </div>

        {error && (
          <div role="alert" className="flex items-center gap-2 rounded-lg border border-red-500/30 bg-red-500/10 p-3 text-xs text-red-600 dark:text-red-400">
            <UntitledIcon name="alert" size={14} />
            <span>{error}</span>
          </div>
        )}

        {/* Buttons */}
        <div className="flex items-center justify-end gap-2.5 pt-2 border-t border-[var(--border)]">
          <button type="button" className="btn-outline text-xs px-4 py-2" onClick={onCancel} disabled={busy}>
            Hủy bỏ
          </button>
          <button type="submit" className="btn-primary text-xs px-5 py-2 font-medium" disabled={busy}>
            {busy ? "Đang lưu…" : site ? "Lưu thay đổi bãi đỗ" : "Khởi tạo bãi đỗ xe"}
          </button>
        </div>
      </fieldset>
    </form>
  )
}