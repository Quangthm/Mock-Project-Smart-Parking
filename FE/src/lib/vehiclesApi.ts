import { request } from "./authApi"

export type RegisteredVehicle = {
  id: string
  rawPlate: string
  canonicalPlate: string
  vehicleType: "CAR" | "MOTORCYCLE"
  imageAssetId?: string
  status?: "ACTIVE"
}

export type VehicleInput = {
  plate: string
  vehicleType: RegisteredVehicle["vehicleType"]
  imageAssetId?: string
}

export type RegistrationClaim = {
  id: string
  plate: string
  vehicleType: RegisteredVehicle["vehicleType"]
  status: "PENDING_DISPUTE"
  submittedAt: string
  imageThumbnailUrl?: string
}

export const vehicleImageTypes = "image/jpeg,image/png,image/webp"

export function validateVehicleImage(file: File) {
  if (!vehicleImageTypes.split(",").includes(file.type))
    return "Choose a JPEG, PNG, or WebP image."
  if (!file.size || file.size > 5_242_880)
    return "Choose a non-empty image up to 5 MiB."
  return ""
}

export function vehicleAccountKey() {
  try {
    const payload = sessionStorage.getItem("sp_access_token")?.split(".")[1]
    if (!payload) return ""
    const { account_id } = JSON.parse(atob(payload.replace(/-/g, "+").replace(/_/g, "/")))
    return typeof account_id === "string" ? account_id : ""
  } catch {
    return ""
  }
}

const mockVehicles = new Map<string, RegisteredVehicle[]>()
const mockClaims = new Map<string, RegistrationClaim[]>()

// TODO: [SPARK-243] Connect to live BE endpoint when available
export async function uploadVehicleImage(file: File, idempotencyKey: string) {
  const error = validateVehicleImage(file)
  if (error) throw new Error(error)
  await new Promise(resolve => setTimeout(resolve, 450))
  return { assetId: `mock-${idempotencyKey}`, status: "ready" as const }
}

// TODO: [SPARK-243] Connect to live BE endpoint when available
export async function listPendingClaims(accountKey: string): Promise<RegistrationClaim[]> {
  return [...(mockClaims.get(accountKey) ?? [])]
}

type ApiResponse<T> = { success: boolean; data: T }

export function canonicalVietnamesePlate(plate: string) {
  return plate.trim().toUpperCase().replace(/[\s.-]/g, "")
}

export function isSupportedVietnamesePlate(
  plate: string,
  vehicleType: RegisteredVehicle["vehicleType"],
) {
  const canonical = canonicalVietnamesePlate(plate)
  return vehicleType === "CAR"
    ? /^[1-9][0-9][A-Z][0-9]{5}$/.test(canonical)
    : /^[1-9][0-9][A-Z][A-Z0-9][0-9]{5}$/.test(canonical)
}

export function formatVietnamesePlateValue(
  rawPlate: string,
  vehicleType: RegisteredVehicle["vehicleType"],
) {
  const plate = canonicalVietnamesePlate(rawPlate)
  if (vehicleType === "CAR" && /^[1-9][0-9][A-Z][0-9]{5}$/.test(plate))
    return `${plate.slice(0, 3)}-${plate.slice(3, 6)}.${plate.slice(6)}`
  if (
    vehicleType === "MOTORCYCLE" &&
    /^[1-9][0-9][A-Z][A-Z0-9][0-9]{5}$/.test(plate)
  )
    return `${plate.slice(0, 2)}-${plate.slice(2, 4)} ${plate.slice(4, 7)}.${plate.slice(7)}`
  return rawPlate.trim().toUpperCase()
}

export function formatVietnamesePlate(vehicle: RegisteredVehicle) {
  return formatVietnamesePlateValue(
    vehicle.canonicalPlate || vehicle.rawPlate,
    vehicle.vehicleType,
  )
}

export const vehiclesApi = {
  async list(): Promise<RegisteredVehicle[]> {
    const response = await request<ApiResponse<RegisteredVehicle[]>>(
      "",
      "GET",
      undefined,
      "/api/vehicles",
    )
    if (!response.success || !Array.isArray(response.data))
      throw new Error("Invalid vehicle list response.")
    return response.data.map(vehicle => ({ ...vehicle, status: vehicle.status ?? "ACTIVE" }))
  },

  // TODO: [SPARK-243] Connect to live BE endpoint when available
  async listForRegistration(accountKey: string): Promise<RegisteredVehicle[]> {
    const live = await vehiclesApi.list()
    const local = mockVehicles.get(accountKey) ?? []
    return [...live.filter(vehicle => !local.some(item => item.id === vehicle.id)), ...local]
  },

  // TODO: [SPARK-243] Connect to live BE endpoint when available
  save(accountKey: string, input: VehicleInput, active: RegisteredVehicle[], existing?: RegisteredVehicle): RegisteredVehicle | RegistrationClaim {
    if (!accountKey || accountKey !== vehicleAccountKey()) throw new Error("Your account has changed. Sign in again.")
    const canonicalPlate = canonicalVietnamesePlate(input.plate)
    const local = mockVehicles.get(accountKey) ?? []
    if (active.some(vehicle => vehicle.id !== existing?.id && vehicle.canonicalPlate === canonicalPlate))
      throw new Error("This plate is already registered on your account.")
    if (!existing && !input.imageAssetId) throw new Error("Upload a vehicle image first.")

    const claims = mockClaims.get(accountKey) ?? []
    const pending = claims.find(claim => canonicalVietnamesePlate(claim.plate) === canonicalPlate)
    if (pending) {
      if (existing) throw new Error("This plate is already pending dispute review.")
      return pending
    }

    const conflict = [...mockVehicles].some(([owner, items]) =>
      owner !== accountKey && items.some(vehicle => vehicle.canonicalPlate === canonicalPlate))
    if (conflict) {
      if (existing) throw new Error("This plate cannot replace your current registration.")
      const claim: RegistrationClaim = {
        id: crypto.randomUUID(), plate: input.plate, vehicleType: input.vehicleType,
        status: "PENDING_DISPUTE", submittedAt: new Date().toISOString(),
      }
      mockClaims.set(accountKey, [...claims, claim])
      return claim
    }

    const vehicle: RegisteredVehicle = {
      id: existing?.id ?? crypto.randomUUID(),
      rawPlate: input.plate,
      canonicalPlate,
      vehicleType: input.vehicleType,
      imageAssetId: input.imageAssetId ?? existing?.imageAssetId,
      status: "ACTIVE",
    }
    mockVehicles.set(accountKey, [...local.filter(item => item.id !== vehicle.id), vehicle])
    return vehicle
  },
}
