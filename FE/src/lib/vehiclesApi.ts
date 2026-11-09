import { request } from "./authApi"

export type RegisteredVehicle = {
  id: string
  rawPlate: string
  canonicalPlate: string
  vehicleType: "CAR" | "MOTORCYCLE"
  imageReference: string | null
  imageAssetId: string | null
  imageStatus: "managed" | "reupload-required"
}

export type VehicleInput = {
  plate: string
  vehicleType: RegisteredVehicle["vehicleType"]
  imageAssetId: string
  expectedImageAssetId?: string | null
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

export type RegistrationClaim = {
  id: string
  rawPlate: string
  canonicalPlate: string
  vehicleType: RegisteredVehicle["vehicleType"]
  status: "PENDING_DISPUTE"
  createdAt: string
}

export const pendingRegistrationMessage = "Registration pending dispute review. This vehicle can’t be used for booking."

export const vehiclesApi = {
  async claims(): Promise<RegistrationClaim[]> {
    const response = await request<ApiResponse<RegistrationClaim[]>>("/registration-claims", "GET", undefined, "/api/vehicles")
    if (!response.success || !Array.isArray(response.data)) throw new Error("Could not confirm registration status. Please refresh.")
    return response.data
  },
  async list(): Promise<RegisteredVehicle[]> {
    const response = await request<ApiResponse<RegisteredVehicle[]>>(
      "",
      "GET",
      undefined,
      "/api/vehicles",
    )
    if (!response.success || !Array.isArray(response.data))
      throw new Error("Invalid vehicle list response.")
    return response.data
  },

  async save(input: VehicleInput, id?: string): Promise<RegisteredVehicle> {
    if (!input.imageAssetId) throw new Error("Upload a vehicle image first.")
    const response = await request<ApiResponse<RegisteredVehicle>>(
      id ? `/${encodeURIComponent(id)}` : "",
      id ? "PATCH" : "POST",
      input,
      "/api/vehicles",
    )
    if (!response.success || !response.data?.id)
      throw new Error("Invalid vehicle response.")
    return response.data
  },
}
