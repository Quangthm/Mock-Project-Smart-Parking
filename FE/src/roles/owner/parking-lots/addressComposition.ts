// Pure address helpers for the Owner site form. This module deliberately has no
// runtime imports so the address composition rule can be unit-tested directly.

export type CityOption = {
  code: string
  name: string
  defaultLat: number
  defaultLng: number
  districts: string[]
}

export const CITIES: CityOption[] = [
  { code: "HCM", name: "TP. Hồ Chí Minh", defaultLat: 10.7769, defaultLng: 106.7009, districts: ["Quận 1", "Quận 3", "TP. Thủ Đức", "Quận Bình Thạnh", "Quận 7", "Quận Tân Bình"] },
  { code: "HN", name: "Hà Nội", defaultLat: 21.0285, defaultLng: 105.8542, districts: ["Quận Hoàn Kiếm", "Quận Ba Đình", "Quận Cầu Giấy", "Quận Đống Đa", "Quận Nam Từ Liêm"] },
  { code: "DN", name: "Đà Nẵng", defaultLat: 16.0544, defaultLng: 108.2022, districts: ["Quận Hải Châu", "Quận Sơn Trà", "Quận Thanh Khê", "Quận Ngũ Hành Sơn"] },
  { code: "BD", name: "Bình Dương", defaultLat: 10.9804, defaultLng: 106.6519, districts: ["TP. Thủ Dầu Một", "TP. Thuận An", "TP. Dĩ An"] },
  { code: "HP", name: "Hải Phòng", defaultLat: 20.8449, defaultLng: 106.6881, districts: ["Quận Hồng Bàng", "Quận Ngô Quyền", "Quận Lê Chân"] },
  { code: "CT", name: "Cần Thơ", defaultLat: 10.0452, defaultLng: 105.7469, districts: ["Quận Ninh Kiều", "Quận Bình Thủy", "Quận Cái Răng"] },
]

export type ParsedSiteAddress = {
  cityCode: string | null
  district: string | null
  street: string
}

function normalize(value: string) {
  return value.normalize("NFC").trim().toLowerCase()
}

/** Canonical display order: `street, district, city name`. */
export function composeAddress(street: string, district: string, cityCode: string, cities: CityOption[] = CITIES) {
  const city = cities.find(candidate => candidate.code === cityCode)
  return [street.trim(), district.trim(), city?.name ?? ""].filter(Boolean).join(", ")
}

/**
 * Effective address for a save. An existing site whose location fields were not
 * edited keeps its stored address verbatim; only a real city/district/street
 * change is recomposed.
 */
export function resolveSiteAddress(input: {
  storedAddress?: string | null
  addressTouched: boolean
  street: string
  district: string
  cityCode: string
}, cities: CityOption[] = CITIES) {
  const stored = (input.storedAddress ?? "").trim()
  if (stored && !input.addressTouched) return stored
  return composeAddress(input.street, input.district, input.cityCode, cities)
}

/**
 * Parse a stored site address back into its structured parts. Returns null
 * parts when the address cannot be mapped onto the known city list; callers
 * must then preserve the original address instead of rewriting it.
 */
export function parseSiteAddress(address: string | null | undefined, cities: CityOption[] = CITIES): ParsedSiteAddress {
  const raw = (address ?? "").trim()
  const parts = raw.split(",").map(part => part.trim()).filter(Boolean)
  if (parts.length === 0) return { cityCode: null, district: null, street: "" }

  const findCity = (value: string) =>
    cities.find(candidate => normalize(candidate.name) === normalize(value) || normalize(candidate.code) === normalize(value))

  let city: CityOption | undefined
  let cityIndex = -1
  for (let index = parts.length - 1; index >= 0; index--) {
    const match = findCity(parts[index])
    if (match) {
      city = match
      cityIndex = index
      break
    }
  }

  // Some stored addresses omit the city name but still carry a known district.
  if (!city) {
    for (let index = parts.length - 1; index >= 0; index--) {
      const match = cities.find(candidate => candidate.districts.some(district => normalize(district) === normalize(parts[index])))
      if (match) {
        city = match
        cityIndex = index + 1
        break
      }
    }
  }

  if (!city) return { cityCode: null, district: null, street: raw }

  let district: string | null = null
  let districtIndex = -1
  for (let index = cityIndex - 1; index >= 0; index--) {
    const match = city.districts.find(candidate => normalize(candidate) === normalize(parts[index]))
    if (match) {
      district = match
      districtIndex = index
      break
    }
  }

  const streetParts = parts.slice(0, districtIndex >= 0 ? districtIndex : cityIndex)
  return { cityCode: city.code, district, street: streetParts.join(", ") }
}
