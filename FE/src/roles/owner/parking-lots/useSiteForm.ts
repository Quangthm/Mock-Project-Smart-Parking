import { useState, useId, useMemo, type FormEvent } from "react"
import { parkingApi, type Site } from "../../../lib/parkingApi"
import { AuthApiError } from "../../../lib/authApi"
import { ownerData as store } from "../data/data"
import { useApp } from "../../../context/AppContext"
import type { LotType } from "../../../lib/types"
import { ALLOWED_PARKING_MODELS } from "../../../lib/3d/parking3DConfig"

export const LOT_TYPE_INFO: Record<LotType, {
  icon: string
  label: string
  hasFloors: boolean
  desc: string
}> = {
  outdoor: { icon: "🌳", label: "Outdoor Lot", hasFloors: false, desc: "Outdoor ground-level parking" },
  basement: { icon: "🏢", label: "Basement Parking", hasFloors: true, desc: "Underground parking with multiple levels" },
  "multi-storey": { icon: "🏗️", label: "Multi-Storey", hasFloors: true, desc: "Multi-storey parking building" },
}

export const CITIES = [
  { code: "HCM", name: "TP. Hồ Chí Minh", defaultLat: 10.7769, defaultLng: 106.7009, districts: ["Quận 1", "Quận 3", "TP. Thủ Đức", "Quận Bình Thạnh", "Quận 7", "Quận Tân Bình"] },
  { code: "HN", name: "Hà Nội", defaultLat: 21.0285, defaultLng: 105.8542, districts: ["Quận Hoàn Kiếm", "Quận Ba Đình", "Quận Cầu Giấy", "Quận Đống Đa", "Quận Nam Từ Liêm"] },
  { code: "DN", name: "Đà Nẵng", defaultLat: 16.0544, defaultLng: 108.2022, districts: ["Quận Hải Châu", "Quận Sơn Trà", "Quận Thanh Khê", "Quận Ngũ Hành Sơn"] },
  { code: "BD", name: "Bình Dương", defaultLat: 10.9804, defaultLng: 106.6519, districts: ["TP. Thủ Dầu Một", "TP. Thuận An", "TP. Dĩ An"] },
  { code: "HP", name: "Hải Phòng", defaultLat: 20.8449, defaultLng: 106.6881, districts: ["Quận Hồng Bàng", "Quận Ngô Quyền", "Quận Lê Chân"] },
  { code: "CT", name: "Cần Thơ", defaultLat: 10.0452, defaultLng: 105.7469, districts: ["Quận Ninh Kiều", "Quận Bình Thủy", "Quận Cái Răng"] },
]

export const DEVICE_OPTIONS = [
  { type: "camera", label: "AI Camera System", desc: "Automatic license plate recognition (ANPR)" },
  { type: "barrier", label: "Barrier Gate", desc: "Automatic barrier gate system" },
  { type: "sensor", label: "Slot Sensors", desc: "Ultrasonic or optical vehicle occupancy sensors" },
  { type: "rfid", label: "RFID Card Reader", desc: "RFID reader for entry and exit control" },
  { type: "ev-charger", label: "EV Charging Station", desc: "Smart electric vehicle charging station" },
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

type SiteDraft = {
  lotType: LotType
  selectedCity: string
  district: string
  streetAddress: string
  form: {
    code: string
    name: string
    address: string
    totalSlots: string
    floors: string
    latitude: number
    longitude: number
    isActive: boolean
    hourlyRate: number
    dailyRate: number
  }
  devices: Record<string, boolean>
}

export function useSiteForm(site: Site | undefined, onSaved: (site: Site) => void, onCancel: () => void) {
  const { user } = useApp()
  const formId = useId()
  const draftKey = `sp_site_create_draft_${user?.id ?? "anonymous"}`
  const [draft] = useState(() => {
    try {
      return site ? null : JSON.parse(sessionStorage.getItem(draftKey) ?? "null") as SiteDraft | null
    } catch {
      return null
    }
  })

  // Infer lot type from existing site or default to outdoor
  const [lotType, setLotType] = useState<LotType>(() => {
    if (draft?.lotType) return draft.lotType
    if (site?.name.toLowerCase().includes("hầm") || site?.name.toLowerCase().includes("basement")) return "basement"
    if (site?.name.toLowerCase().includes("tầng") || site?.name.toLowerCase().includes("storey")) return "multi-storey"
    return "outdoor"
  })

  // Address Parsing
  const [selectedCity, setSelectedCity] = useState(draft?.selectedCity ?? CITIES[0].code)
  const [district, setDistrict] = useState(draft?.district ?? CITIES[0].districts[0])
  const [streetAddress, setStreetAddress] = useState(draft?.streetAddress ?? (site?.address ? site.address.split(",")[0].trim() : ""))
  const isManualCode = Boolean(site?.code)

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
    ...draft?.form,
  })

  const [devices, setDevices] = useState<Record<string, boolean>>({
    camera: true,
    barrier: true,
    sensor: true,
    rfid: false,
    "ev-charger": false,
    ...draft?.devices,
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
      setError("Please enter the site name and address.")
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
          const cachedLot = store.getLotsByOwner(user?.id ?? "owner-001").find((lot) => lot.id === site.id)
          store.saveLot({
            // Local UI cache timestamp; this is not the backend site creation time.
            createdAt: cachedLot?.createdAt ?? new Date().toISOString(),
            id: site.id,
            ownerId: user?.id ?? "owner-001",
            name: siteName,
            type: lotType,
            address: finalAddress,
            totalSlots: totalSlotsNum,
            floors: floorsNum,
            modelUrl: activeModelDef.path,
            modelFileName: activeModelDef.fileName,
            slots: store.getLotsByOwner(user?.id ?? "owner-001").find((lot) => lot.id === site.id)?.slots ?? [],
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
        const createdSite = await parkingApi.create({
          code: siteCode,
          name: siteName,
          address: finalAddress,
        })
        if (!createdSite || typeof createdSite.id !== "string" || !createdSite.id.trim()) {
          throw new Error("The server did not return a valid site ID. Please try again.")
        }

        // Also persist to owner store for visual lot manager
        try {
          store.saveLot({
            id: createdSite.id,
            createdAt: new Date().toISOString(),
            ownerId: user?.id ?? "owner-001",
            name: siteName,
            type: lotType,
            address: finalAddress,
            totalSlots: totalSlotsNum,
            floors: floorsNum,
            modelUrl: activeModelDef.path,
            modelFileName: activeModelDef.fileName,
            slots: [],
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

        sessionStorage.removeItem(draftKey)
        onSaved(createdSite)
      }
    } catch (err) {
      if (err instanceof AuthApiError) {
        if (!site && err.status === 401) {
          sessionStorage.setItem(draftKey, JSON.stringify({ lotType, selectedCity, district, streetAddress, form, devices }))
        }
        const messages: Record<number, string> = {
          400: "Please check the site details and try again.",
          401: "Your session has expired. Please sign in again.",
          403: "You do not have permission to create this parking site.",
          409: "A parking site with these details already exists.",
          500: "The parking server could not create the site. Please try again later.",
        }
        setError(err.status === 400 && err.message !== "Authentication request failed."
          ? err.message
          : messages[err.status] ?? err.message)
      } else if (err instanceof Error && err.message.startsWith("Cannot reach the authentication server.")) {
        setError("Cannot connect to the parking server. Please try again.")
      } else {
        setError(err instanceof Error ? err.message : "Unable to save the parking site.")
      }
    } finally {
      setBusy(false)
    }
  }

  function cancel() {
    if (!site) sessionStorage.removeItem(draftKey)
    onCancel()
  }

  return {
    formId, lotType, setLotType, selectedCity, district, setDistrict,
    streetAddress, setStreetAddress, isManualCode, form, setForm,
    devices, setDevices, busy, error, computedCode, fullAddress,
    handleCityChange, handleNameChange, submit, cancel,
  }
}
