import { useState, useId, useMemo, useEffect, useRef, type FormEvent } from "react"
import { parkingApi, type Site } from "../../../lib/parkingApi"
import { AuthApiError } from "../../../lib/authApi"
import { ownerData as store } from "../data/data"
import { useApp } from "../../../context/AppContext"
import type { LotType } from "../../../lib/types"
import { ALLOWED_PARKING_MODELS, validateModelUpload } from "../../../lib/3d/parking3DConfig"
import { useModelCarCapacity, useModelStructure } from "../../../lib/3d/modelSlotCapacity"
import { deleteOwnerModel, loadOwnerModelUrl, saveOwnerModel } from "../../../lib/3d/ownerModelStorage"
import { CITIES } from "../../../lib/vietnamAdministrativeUnits"
import { validParkingCoordinates, type ParkingCoordinates } from "../../../components/ui/ParkingLocationPicker"

export const LOT_TYPE_INFO: Record<LotType, {
  icon: string
  label: string
  hasFloors: boolean
  desc: string
}> = {
  outdoor: { icon: "leaf", label: "Outdoor Lot", hasFloors: false, desc: "Outdoor ground-level parking" },
  basement: { icon: "parking", label: "Basement Parking", hasFloors: true, desc: "Underground parking with multiple levels" },
  "multi-storey": { icon: "building", label: "Multi-Storey", hasFloors: true, desc: "Multi-storey parking building" },
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
  locationConfirmed?: boolean
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
  const savedLot = site ? store.getLotsByOwner(user?.id ?? "").find(lot => lot.id === site.id) : undefined
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
    if (savedLot?.type) return savedLot.type
    if (site?.name.toLowerCase().includes("hầm") || site?.name.toLowerCase().includes("basement")) return "basement"
    if (site?.name.toLowerCase().includes("tầng") || site?.name.toLowerCase().includes("storey")) return "multi-storey"
    return "outdoor"
  })
  const [customModelUrl, setCustomModelUrl] = useState<string | null>(null)
  const [customModelFileName, setCustomModelFileName] = useState<string | null>(null)
  const [customModelFile, setCustomModelFile] = useState<File | null>(null)
  const [modelStorageReady, setModelStorageReady] = useState(false)
  const modelChoiceChangedRef = useRef(false)
  const [modelError, setModelError] = useState("")
  const activeModelUrl = customModelUrl ?? ALLOWED_PARKING_MODELS[lotType].path
  const modelCapacity = useModelCarCapacity(activeModelUrl)
  const modelStructure = useModelStructure(activeModelUrl)
  const modelFloors = modelStructure.analysis?.floors ?? []

  useEffect(() => {
    if (!site?.id) return
    let active = true
    loadOwnerModelUrl(site.id).then(url => {
      if (!active || !url || modelChoiceChangedRef.current) return
      setCustomModelUrl(url)
      setCustomModelFileName(savedLot?.modelFileName ?? "Owner model.glb")
    }).catch(() => {}).finally(() => { if (active) setModelStorageReady(true) })
    return () => { active = false }
  }, [site?.id, user?.id])

  function chooseLotType(type: LotType) {
    if (type === lotType) return
    modelChoiceChangedRef.current = true
    setLotType(type)
    setCustomModelUrl(null)
    setCustomModelFileName(null)
    setCustomModelFile(null)
    setModelStorageReady(true)
    setModelError("")
    const defaultFloors = type === "basement" ? "2" : type === "multi-storey" ? "3" : "1"
    const defaultSlots = type === "basement" ? "48" : type === "multi-storey" ? "64" : "48"
    setForm((cur) => ({
      ...cur,
      floors: defaultFloors,
      totalSlots: defaultSlots,
    }))
  }

  // Auto-calculate capacity based on active floors
  useEffect(() => {
    if (site && !modelChoiceChangedRef.current) return
    const floorsCount = LOT_TYPE_INFO[lotType].hasFloors ? parseInt(form.floors, 10) || 1 : 1
    if (modelFloors.length > 0) {
      const activeFloors = modelFloors.slice(0, Math.min(floorsCount, modelFloors.length))
      const sumCapacity = activeFloors.reduce((sum, f) => sum + f.capacity, 0)
      if (sumCapacity > 0) {
        setForm((cur) => ({
          ...cur,
          totalSlots: String(sumCapacity),
        }))
      }
    }
  }, [lotType, form.floors, modelFloors, site])

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
    setModelStorageReady(true)
  }

  // Address Parsing
  const addressParts = site?.address.split(",").map(part => part.trim()) ?? []
  const savedCity = CITIES.find(city => addressParts.at(-1) === city.name || (city.code === "HCM" && ["TP.HCM", "Bình Dương", "Bà Rịa - Vũng Tàu"].includes(addressParts.at(-1) ?? "")))
  const savedDistrict = savedCity && addressParts.length >= 3 ? addressParts.at(-2) : undefined
  const [selectedCity, setSelectedCity] = useState(draft?.selectedCity === "BD" ? "HCM" : draft?.selectedCity ?? savedCity?.code ?? (site ? "LEGACY" : CITIES[0].code))
  const [district, setDistrict] = useState(draft?.district ?? savedDistrict ?? "")
  const [streetAddress, setStreetAddress] = useState(draft?.streetAddress ?? (site?.address
    ? savedCity && savedDistrict ? site.address.split(",").slice(0, -2).join(",").trim() : site.address
    : ""))
  const [addressChanged, setAddressChanged] = useState(false)
  const [locationConfirmed, setLocationConfirmed] = useState(() => {
    if (site) return validParkingCoordinates({ lat: site.latitude ?? 0, lng: site.longitude ?? 0 })
    return Boolean(draft?.locationConfirmed && validParkingCoordinates({ lat: draft.form.latitude, lng: draft.form.longitude }))
  })
  const isManualCode = Boolean(site?.code)

  const [form, setForm] = useState({
    code: site?.code ?? "",
    name: site?.name ?? "",
    address: site?.address ?? "",
    totalSlots: savedLot ? String(savedLot.totalSlots) : site?.totalPhysicalCapacity ? String(site.totalPhysicalCapacity) : "30",
    floors: savedLot ? String(savedLot.floors) : "1",
    latitude: site?.latitude ?? CITIES[0].defaultLat,
    longitude: site?.longitude ?? CITIES[0].defaultLng,
    isActive: site?.isActive ?? true,
    hourlyRate: savedLot?.hourlyRate ?? 15000,
    dailyRate: savedLot?.dailyRate ?? 100000,
    nightRate: savedLot?.nightRate ?? 50000,
    gracePeriodMinutes: savedLot?.gracePeriodMinutes ?? 15,
    subscriptionMonthly: savedLot?.subscriptionMonthly ?? 800000,
    subscriptionYearly: savedLot?.subscriptionYearly ?? 8000000,
    ...draft?.form,
  })

  const [devices, setDevices] = useState<Record<string, boolean>>({
    camera: true,
    barrier: true,
    sensor: true,
    rfid: false,
    "ev-charger": false,
    ...Object.fromEntries(savedLot?.devices.map(device => [device.type, device.enabled]) ?? []),
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
    return site && !addressChanged ? site.address : parts.join(", ")
  }, [site, addressChanged, streetAddress, district, selectedCity])

  function handleCityChange(newCityCode: string) {
    setAddressChanged(true)
    setLocationConfirmed(false)
    setSelectedCity(newCityCode)
    const found = CITIES.find((c) => c.code === newCityCode)
    if (found) {
      setDistrict("")
      setForm((cur) => ({
        ...cur,
        latitude: found.defaultLat,
        longitude: found.defaultLng,
        code: isManualCode ? cur.code : `SPK-${newCityCode}-${generateSlug(cur.name) || "PARK"}`,
      }))
    }
  }

  function markAddressChanged() {
    setAddressChanged(true)
    setLocationConfirmed(false)
  }

  function setConfirmedLocation(point: ParkingCoordinates) {
    setForm(current => ({ ...current, latitude: point.lat, longitude: point.lng }))
    setLocationConfirmed(true)
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
    if ((!site || addressChanged) && (selectedCity === "LEGACY" || !district.trim())) {
      setError("Choose a province or city and enter its district or area.")
      return
    }
    if (!locationConfirmed || !validParkingCoordinates({ lat: form.latitude, lng: form.longitude })) {
      setError("Confirm the parking entrance on the map before saving.")
      return
    }

    const totalSlotsNum = parseInt(form.totalSlots, 10)
    const floorsNum = LOT_TYPE_INFO[lotType].hasFloors ? parseInt(form.floors, 10) || 1 : 1

    if (site && savedLot && savedLot.modelFileName !== ALLOWED_PARKING_MODELS[lotType].fileName && !customModelUrl) {
      setError(modelStorageReady ? "The saved Owner model is unavailable in this browser. Upload it again before changing setup details." : "Loading the saved Owner model. Please wait.")
      return
    }

    if ((!site || savedLot) && modelCapacity.capacity === null) {
      setError(modelCapacity.error || "Checking the 3D model's car bay capacity. Please wait.")
      return
    }
    if ((!site || savedLot) && (!Number.isInteger(totalSlotsNum) || totalSlotsNum < 1 || totalSlotsNum > modelCapacity.capacity!)) {
      setError(`Planned car spaces must be between 1 and ${modelCapacity.capacity}, the number of bays in this 3D model.`)
      return
    }

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
        if (savedLot) try {
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
            totalSlots: totalSlotsNum,
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
            nightRate: form.nightRate,
            gracePeriodMinutes: form.gracePeriodMinutes,
            subscriptionMonthly: form.subscriptionMonthly,
            subscriptionYearly: form.subscriptionYearly,
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
          totalPhysicalCapacity: savedLot ? totalSlotsNum : site.totalPhysicalCapacity,
          isActive: form.isActive,
        })
      } else {
        // Create new site via API
        const createdSite = await parkingApi.create({
          code: siteCode,
          name: siteName,
          address: finalAddress,
          latitude: form.latitude,
          longitude: form.longitude,
        })
        if (!createdSite || typeof createdSite.id !== "string" || !createdSite.id.trim()) {
          throw new Error("The server did not return a valid site ID. Please try again.")
        }

        // Auto-create FLOOR units based on active floors
        const targetFloors = modelFloors.slice(0, Math.min(floorsNum, modelFloors.length))
        for (const fl of targetFloors) {
          try {
            await parkingApi.unit(createdSite.id, {
              type: "FLOOR",
              name: fl.displayName,
              parentId: null,
              capacity: fl.capacity,
            })
          } catch (unitErr) {
            console.warn("Could not auto-create floor unit:", unitErr)
          }
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
            totalSlots: totalSlotsNum,
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
            nightRate: form.nightRate,
            gracePeriodMinutes: form.gracePeriodMinutes,
            subscriptionMonthly: form.subscriptionMonthly,
            subscriptionYearly: form.subscriptionYearly,
            status: "active",
            lat: form.latitude,
            lng: form.longitude,
          })
        } catch {
          // ignore local store sync errors
        }

        sessionStorage.removeItem(draftKey)
        onSaved({ ...createdSite, latitude: createdSite.latitude ?? form.latitude, longitude: createdSite.longitude ?? form.longitude })
      }
    } catch (err) {
      if (err instanceof AuthApiError) {
        if (!site && err.status === 401) {
          sessionStorage.setItem(draftKey, JSON.stringify({ lotType, selectedCity, district, streetAddress, locationConfirmed, form, devices }))
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
    formId, hasSavedSetup: Boolean(savedLot), lotType, chooseLotType, selectedCity, district, setDistrict,
    streetAddress, setStreetAddress, addressChanged, isManualCode, form, setForm, locationConfirmed, markAddressChanged, setConfirmedLocation,
    devices, setDevices, busy, error, computedCode, fullAddress, modelCapacity, modelError, customModelFileName, uploadModel,
    handleCityChange, handleNameChange, submit, cancel,
  }
}
