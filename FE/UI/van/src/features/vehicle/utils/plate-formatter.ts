import type { VehicleType } from "@/types/domain"

/**
 * ============================================================================
 * Vietnamese License Plate Formatter & Regex Validator
 * Document Ref: tempfe/specs/driver-features-spec.md Section 2.2
 * ============================================================================
 */

/**
 * Normalizes a license plate by stripping spaces, hyphens, and dots, uppercase.
 * Example: "51a - 123.45" -> "51A12345"
 */
export function canonicalVietnamesePlate(plate: string): string {
  return plate.trim().toUpperCase().replace(/[\s.-]/g, "")
}

/**
 * Validates if the given plate strictly adheres to Vietnamese 5-digit format:
 * - CAR: 2 digits + 1 series letter + 5 digits (e.g. 51A-123.45)
 * - MOTORCYCLE: 2 digits + 1-2 letters/digits + 5 digits (e.g. 59-B1 123.45)
 */
export function isSupportedVietnamesePlate(
  plate: string,
  vehicleType: VehicleType
): boolean {
  const canonical = canonicalVietnamesePlate(plate)
  return vehicleType === "CAR"
    ? /^[1-9][0-9][A-Z][0-9]{5}$/.test(canonical)
    : /^[1-9][0-9][A-Z][A-Z0-9][0-9]{5}$/.test(canonical)
}

/**
 * Formats a raw or canonical plate into standard human-readable display string:
 * - CAR: 51A-123.45
 * - MOTORCYCLE: 59-B1 123.45
 */
export function formatVietnamesePlateValue(
  rawPlate: string,
  vehicleType: VehicleType
): string {
  const plate = canonicalVietnamesePlate(rawPlate)
  if (vehicleType === "CAR" && /^[1-9][0-9][A-Z][0-9]{5}$/.test(plate)) {
    return `${plate.slice(0, 3)}-${plate.slice(3, 6)}.${plate.slice(6)}`
  }
  if (
    vehicleType === "MOTORCYCLE" &&
    /^[1-9][0-9][A-Z][A-Z0-9][0-9]{5}$/.test(plate)
  ) {
    return `${plate.slice(0, 2)}-${plate.slice(2, 4)} ${plate.slice(4, 7)}.${plate.slice(7)}`
  }
  return rawPlate.trim().toUpperCase()
}
