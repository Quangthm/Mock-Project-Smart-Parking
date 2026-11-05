import legacyDistricts from "./legacyDistricts.json"

// Province list: Decision 19/2025/QĐ-TTg (effective 2025-07-01). Existing site-code prefixes are retained.
// https://xaydungchinhsach.chinhphu.vn/bang-danh-muc-va-ma-so-cua-34-tinh-thanh-moi-cac-don-vi-hanh-chinh-cap-xa-moi-11925070418263625.htm
// District labels are familiar pre-2025 areas, grouped under today's provinces; they are not current administrative units.
// https://provinces.open-api.vn/api/v1/?depth=2
// https://xaydungchinhsach.chinhphu.vn/chi-tiet-34-don-vi-hanh-chinh-cap-tinh-tu-12-6-2025-119250612141845533.htm
export const CITIES = [
  { code: "HCM", name: "TP. Hồ Chí Minh", defaultLat: 10.7769, defaultLng: 106.7009 },
  { code: "HN", name: "Hà Nội", defaultLat: 21.0285, defaultLng: 105.8542 },
  { code: "CB", name: "Cao Bằng", defaultLat: 22.6666, defaultLng: 106.2529 },
  { code: "TQ", name: "Tuyên Quang", defaultLat: 21.8236, defaultLng: 105.2181 },
  { code: "DB", name: "Điện Biên", defaultLat: 21.386, defaultLng: 103.023 },
  { code: "LC", name: "Lai Châu", defaultLat: 22.3964, defaultLng: 103.458 },
  { code: "SL", name: "Sơn La", defaultLat: 21.327, defaultLng: 103.914 },
  { code: "LCA", name: "Lào Cai", defaultLat: 22.4856, defaultLng: 103.9707 },
  { code: "TN", name: "Thái Nguyên", defaultLat: 21.5942, defaultLng: 105.8482 },
  { code: "LS", name: "Lạng Sơn", defaultLat: 21.8537, defaultLng: 106.761 },
  { code: "QN", name: "Quảng Ninh", defaultLat: 20.9504, defaultLng: 107.0734 },
  { code: "BN", name: "Bắc Ninh", defaultLat: 21.1214, defaultLng: 106.111 },
  { code: "PT", name: "Phú Thọ", defaultLat: 21.3227, defaultLng: 105.4019 },
  { code: "HP", name: "Hải Phòng", defaultLat: 20.8449, defaultLng: 106.6881 },
  { code: "HY", name: "Hưng Yên", defaultLat: 20.6464, defaultLng: 106.0511 },
  { code: "NB", name: "Ninh Bình", defaultLat: 20.2506, defaultLng: 105.9745 },
  { code: "TH", name: "Thanh Hóa", defaultLat: 19.8075, defaultLng: 105.7765 },
  { code: "NA", name: "Nghệ An", defaultLat: 18.6796, defaultLng: 105.6813 },
  { code: "HT", name: "Hà Tĩnh", defaultLat: 18.3559, defaultLng: 105.8877 },
  { code: "QT", name: "Quảng Trị", defaultLat: 16.8163, defaultLng: 107.1 },
  { code: "HUE", name: "Huế", defaultLat: 16.4637, defaultLng: 107.5909 },
  { code: "DN", name: "Đà Nẵng", defaultLat: 16.0544, defaultLng: 108.2022 },
  { code: "QNG", name: "Quảng Ngãi", defaultLat: 15.1214, defaultLng: 108.8044 },
  { code: "GL", name: "Gia Lai", defaultLat: 13.9833, defaultLng: 108 },
  { code: "KH", name: "Khánh Hòa", defaultLat: 12.2388, defaultLng: 109.1967 },
  { code: "DLAK", name: "Đắk Lắk", defaultLat: 12.6662, defaultLng: 108.0382 },
  { code: "LD", name: "Lâm Đồng", defaultLat: 11.9404, defaultLng: 108.4583 },
  { code: "DNAI", name: "Đồng Nai", defaultLat: 10.9574, defaultLng: 106.8427 },
  { code: "TNI", name: "Tây Ninh", defaultLat: 11.3103, defaultLng: 106.0983 },
  { code: "DTH", name: "Đồng Tháp", defaultLat: 10.4602, defaultLng: 105.6329 },
  { code: "VL", name: "Vĩnh Long", defaultLat: 10.2537, defaultLng: 105.9722 },
  { code: "AG", name: "An Giang", defaultLat: 10.3864, defaultLng: 105.4352 },
  { code: "CT", name: "Cần Thơ", defaultLat: 10.0452, defaultLng: 105.7469 },
  { code: "CM", name: "Cà Mau", defaultLat: 9.1768, defaultLng: 105.1524 },
] as const

export const DISTRICTS_BY_PROVINCE: Record<string, string[]> = legacyDistricts

