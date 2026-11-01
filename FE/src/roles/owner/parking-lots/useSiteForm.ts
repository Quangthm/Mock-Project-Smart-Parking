import { useState, useId, useMemo, useEffect, useRef, type FormEvent } from "react"
import { parkingApi, type Site } from "../../../lib/parkingApi"
import { AuthApiError } from "../../../lib/authApi"
import { ownerData as store } from "../data/data"
import { useApp } from "../../../context/AppContext"
import type { LotType } from "../../../lib/types"
import { ALLOWED_PARKING_MODELS, validateModelUpload } from "../../../lib/3d/parking3DConfig"
import { useModelCarCapacity } from "../../../lib/3d/modelSlotCapacity"
import { deleteOwnerModel, loadOwnerModelUrl, saveOwnerModel } from "../../../lib/3d/ownerModelStorage"
import { CITIES, resolveSiteAddress, parseSiteAddress } from "./addressComposition"

export { CITIES }

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
  const [customModelUrl, setCustomModelUrl] = useState<string | null>(null)
  const [customModelFileName, setCustomModelFileName] = useState<string | null>(null)
  const [customModelFile, setCustomModelFile] = useState<File | null>(null)
  const modelChoiceChangedRef = useRef(false)
  const [modelError, setModelError] = useState("")
  const activeModelUrl = customModelUrl ?? ALLOWED_PARKING_MODELS[lotType].path
  const modelCapacity = useModelCarCapacity(activeModelUrl)

  useEffect(() => {
    if (!site?.id) return
    let active = true
    loadOwnerModelUrl(site.id).then(url => {
      if (!active || !url || modelChoiceChangedRef.current) return
      setCustomModelUrl(url)
      setCustomModelFileName(store.getLotsByOwner(user?.id ?? "").find(lot => lot.id === site.id)?.modelFileName ?? "Owner model.glb")
    }).catch(() => {})
    return () => { active = false }
  }, [site?.id, user?.id])

  function chooseLotType(type: LotType) {
    if (type === lotType) return
    modelChoiceChangedRef.current = true
    setLotType(type)
    setCustomModelUrl(null)
    setCustomModelFileName(null)
    setCustomModelFile(null)
    setModelError("")
  }

  function uploadModel(file: File | undefined) {
    if (!file) return
    const result = validateModelUpload(file.name, lotType)
    if (!result.isValid && !file.name.toLowerCase().endsWith(".glb")) { setModelError("Choose a GLB parking model."); return }
    modelChoiceChangedRef.current = true
    setModelError("")
    if (result.modelDef && result.modelDef.lotType !== lotType) setLotType(result.modelDef.lotType)
    setCustomModelUrl(URL.createObjectURL(file))
    setCustomModelFileName(file.name)
    setCustomModelFile(file)
  }

  // Address parsing. When editing, the stored address is split back into its
  // structured parts so that a name-only save does not rewrite the district or
  // city. The previous hardcoded Quận 1 default rewrote every non-HCM address.
  const parsedAddress = useMemo(() => parseSiteAddress(site?.address), [site?.address])
  const initialCityCode = draft?.selectedCity ?? parsedAddress.cityCode ?? CITIES[0].code
  const initialCity = CITIES.find((city) => city.code === initialCityCode) ?? CITIES[0]
  const [selectedCity, setSelectedCity] = useState(initialCityCode)
  const [district, setDistrict] = useState(draft?.district ?? parsedAddress.district ?? initialCity.districts[0])
  const [streetAddress, setStreetAddress] = useState(draft?.streetAddress ?? parsedAddress.street ?? "")
  // Compose a new address only after the Owner edits city, district or street.
  const [addressTouched, setAddressTouched] = useState(false)
  const isManualCode = Boolean(site?.code)

  // IoT devices and base pricing are not part of the Facility contract, so they
  // are read back from the Owner store this form already writes on save.
  const cachedLot = useMemo(
    () => (site ? store.getLotsByOwner(user?.id ?? "owner-001").find((lot) => lot.id === site.id) : undefined),
    [site, user?.id],
  )

  const [form, setForm] = useState({
    code: site?.code ?? "",
    name: site?.name ?? "",
    address: site?.address ?? "",
    floors: "1",
    latitude: site?.latitude ?? CITIES[0].defaultLat,
    longitude: site?.longitude ?? CITIES[0].defaultLng,
    isActive: site?.isActive ?? true,
    hourlyRate: cachedLot?.hourlyRate ?? 15000,
    dailyRate: cachedLot?.dailyRate ?? 100000,
    ...draft?.form,
  })

  const [devices, setDevices] = useState<Record<string, boolean>>(() => {
    const defaults: Record<string, boolean> = {
      camera: true,
      barrier: true,
      sensor: true,
      rfid: false,
      "ev-charger": false,
    }
    if (draft?.devices) return { ...defaults, ...draft.devices }
    if (cachedLot) {
      const installed = new Set<string>(cachedLot.devices.map((device) => device.type))
      for (const type of Object.keys(defaults)) defaults[type] = installed.has(type)
    }
    return defaults
  })

  const [busy, setBusy] = useState(false)
  const [error, setError] = useState("")

  // Auto-generate Site Code based on Smart Parking logic: SPK-[CITY]-[SLUG]
  const computedCode = useMemo(() => {
    const slug = generateSlug(form.name) || "PARK"
    return `SPK-${selectedCity}-${slug}`
  }, [selectedCity, form.name])

  // Computed full address. An untouched edit keeps the server address verbatim;
  // a real city/district/street change is recomposed from the structured fields.
  const fullAddress = useMemo(() => resolveSiteAddress({
    storedAddress: site?.address,
    addressTouched,
    street: streetAddress,
    district,
    cityCode: selectedCity,
  }), [site?.address, addressTouched, streetAddress, district, selectedCity])

  function handleCityChange(newCityCode: string) {
    setAddressTouched(true)
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

  function updateDistrict(value: string) {
    setAddressTouched(true)
    setDistrict(value)
  }

  function updateStreetAddress(value: string) {
    setAddressTouched(true)
    setStreetAddress(value)
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

    // Capacity is derived from the slots the Owner registers, not from this form.
    // The 3D model's bay count is shown read-only, so it must never gate submission.
    const modelCapacityNum = modelCapacity.capacity ?? site?.totalPhysicalCapacity ?? 0
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

        // `profile` never carries the active flag, so a status change has to be
        // persisted through the dedicated `active` action. Reporting success
        // without this left the toggle unapplied after the refresh.
        if (form.isActive !== site.isActive) {
          await parkingApi.edit(site.id, { action: "active", active: form.isActive })
        }

        // Re-read the persisted record so success is only reported for what the
        // server actually stored; a silent no-op must never look like a save.
        const persistedSite = (await parkingApi.list()).find((item) => item.id === site.id)
        if (!persistedSite) {
          throw new Error("The saved parking facility could not be reloaded. Refresh and try again.")
        }
        if (
          persistedSite.code.toUpperCase() !== siteCode.toUpperCase() ||
          persistedSite.name !== siteName ||
          persistedSite.address !== finalAddress ||
          persistedSite.isActive !== form.isActive
        ) {
          throw new Error("The parking facility changes were not saved. Refresh and try again.")
        }

        // Also sync local store lot
        try {
          const cachedLot = store.getLotsByOwner(user?.id ?? "owner-001").find((lot) => lot.id === site.id)
          if (!customModelFile && cachedLot?.type && cachedLot.type !== lotType) await deleteOwnerModel(site.id).catch(() => {})
          const savedModelUrl = customModelFile ? await saveOwnerModel(site.id, customModelFile).catch(() => null) : null
          store.saveLot({
            // Local UI cache timestamp; this is not the backend site creation time.
            createdAt: cachedLot?.createdAt ?? new Date().toISOString(),
            id: site.id,
            ownerId: user?.id ?? "owner-001",
            name: siteName,
            type: lotType,
            address: finalAddress,
            totalSlots: modelCapacityNum,
            floors: floorsNum,
            modelUrl: savedModelUrl ?? (cachedLot?.type === lotType ? cachedLot.modelUrl : activeModelUrl),
            modelFileName: customModelFileName ?? (cachedLot?.type === lotType ? cachedLot.modelFileName : activeModelDef.fileName),
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

        onSaved(persistedSite)
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
          const savedModelUrl = customModelFile ? await saveOwnerModel(createdSite.id, customModelFile).catch(() => null) : null
          store.saveLot({
            id: createdSite.id,
            createdAt: new Date().toISOString(),
            ownerId: user?.id ?? "owner-001",
            name: siteName,
            type: lotType,
            address: finalAddress,
            totalSlots: modelCapacityNum,
            floors: floorsNum,
            modelUrl: savedModelUrl ?? activeModelUrl,
            modelFileName: customModelFileName ?? activeModelDef.fileName,
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
    formId, lotType, chooseLotType, selectedCity, district, setDistrict: updateDistrict,
    streetAddress, setStreetAddress: updateStreetAddress, isManualCode, form, setForm,
    devices, setDevices, busy, error, computedCode, fullAddress, modelCapacity, modelError, customModelFileName, uploadModel,
    handleCityChange, handleNameChange, submit, cancel,
  }
}
