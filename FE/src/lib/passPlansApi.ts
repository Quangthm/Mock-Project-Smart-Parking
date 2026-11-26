import { request } from "./authApi"

const operationsUrl = import.meta.env.VITE_RESERVATION_API_BASE_URL ?? "http://localhost:5055"
const paymentUrl = import.meta.env.VITE_PAYMENT_API_BASE_URL ?? "http://localhost:5065"

export const passVehicleTypes = ["CAR", "MOTORCYCLE", "OVERSIZED"] as const
export type PassVehicleType = typeof passVehicleTypes[number]
export type PassProductType = "MONTHLY" | "YEARLY"
export const passVehicleLabels: Record<PassVehicleType, string> = {
  CAR: "Car", MOTORCYCLE: "Motorcycle", OVERSIZED: "Oversized vehicle",
}

interface Coverage {
  dayCovered: boolean
  nightCovered: boolean
  excludedZoneIds: string[]
}

interface PassDraft {
  zoneId: string | null
  vehicleTypes: PassVehicleType[]
  ordinaryCoverageRules: Coverage
  isAvailableOnly: true
}

export interface PassPlan extends Omit<PassDraft, "ordinaryCoverageRules" | "isAvailableOnly"> {
  planId: string | null
  productType: PassProductType
  status: string
  isEnabled: boolean
  hasDraft: boolean
  activeRevision: number
  ordinaryCoverageRules: Coverage | null
  financialReadiness: {
    hasCompleteFinancialTerms: boolean
    financialTermRevision: number | null
    blockedReason: string | null
  }
}

export interface PassOffer {
  planId: string
  planRevision: number
  financialTermRevision: number
  productType: PassProductType
  vehicleType: PassVehicleType
  fullPeriodPrice: number
  currency: string
  zoneId: string | null
  ordinaryCoverageRules: Coverage
}

async function call<T>(server: string, path: string, method = "GET", body?: unknown, key?: string) {
  const result = await request<{ success: boolean; data: T }>(
    path, method, body, "/api/v1", server.replace(/\/$/, ""), key ? { "Idempotency-Key": key } : undefined,
  )
  if (!result?.success || result.data == null) throw new Error("Pass service returned no result.")
  return result.data
}

const plansPath = (siteId: string) => `/lots/${encodeURIComponent(siteId)}/pass-plans`

export const passPlansApi = {
  list: (siteId: string) => call<{ plans: PassPlan[] }>(operationsUrl, plansPath(siteId)),
  saveDraft: (siteId: string, product: PassProductType, draft: PassDraft) =>
    call(operationsUrl, `${plansPath(siteId)}/${product}/draft`, "PUT", draft),
  publish: (siteId: string, product: PassProductType, key: string) =>
    call(operationsUrl, `${plansPath(siteId)}/${product}/publish`, "POST", {}, key),
  setEnabled: (siteId: string, product: PassProductType, isEnabled: boolean, revision: number) =>
    call(operationsUrl, `${plansPath(siteId)}/${product}/enablement`, "PATCH", { isEnabled, expectedPlanRevision: revision }),
  saveMonthlyPrices: (siteId: string, planId: string, revision: number, prices: { vehicleType: PassVehicleType; fullPeriodPrice: number }[], key: string) =>
    call(paymentUrl, `/pricing${plansPath(siteId)}/${encodeURIComponent(planId)}/financial-terms`, "PUT", {
      productType: "MONTHLY", planRevision: revision, currency: "VND", vehiclePrices: prices,
      refundPolicy: { voluntaryBeforeStartPercent: 100, voluntaryInPeriodPercent: 50, facilityClosurePercent: 100, roundingMode: "HALF_UP_THOUSAND" },
    }, key),
  offers: (siteId: string) =>
    call<{ offers: PassOffer[]; yearlyBlockedByQ05: boolean }>(operationsUrl, `/sites/${encodeURIComponent(siteId)}/pass-offers`),
}
