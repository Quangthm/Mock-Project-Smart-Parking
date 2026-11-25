import { ensureAccessToken, request } from "./authApi"

const POLICY_PREFIX = "/api/v1/admin/policies/operations"

const reservationServer = () =>
  (import.meta.env.VITE_RESERVATION_API_BASE_URL ?? "http://localhost:5055").replace(/\/$/, "")

const facilityServer = () =>
  (import.meta.env.VITE_PARKING_API_BASE_URL ?? "http://localhost:5045").replace(/\/$/, "")

export interface AdminTenantOption {
  id: string
  code: string
  name: string
  status: string
}

export interface AdminSiteOption {
  id: string
  tenantId: string
  code: string
  name: string
  status: string
  isActive: boolean
}

export interface AdminFacilityDirectory {
  tenants: AdminTenantOption[]
  sites: AdminSiteOption[]
}

interface Response<T> {
  success: boolean
  data: T
  message?: string
}

export type PolicyDataType = "duration" | "count"

export type PolicyUnit = "minutes" | "hours" | "days" | "submissions"

export interface PolicyBoundDto {
  minimum: number
  maximum: number
}

export interface PlatformPolicyKeyMetadata {
  key: string
  name: string
  description: string
  dataType: PolicyDataType
  unit: PolicyUnit
  defaultValue: number
  isOverridableByOwner: boolean
  bound: PolicyBoundDto | null
}

export interface PlatformPolicyCatalogue {
  revisionId: string
  version: number
  effectiveFrom: string
  cancellationTimezone: string
  policies: PlatformPolicyKeyMetadata[]
}

export type PolicyValueSource = "PLATFORM_DEFAULT" | "SITE_OVERRIDE"

export interface EffectivePolicyValue {
  value: number
  unit: PolicyUnit
  inherited: boolean
  source: PolicyValueSource
  isZeroNotDisabled: boolean
}

export interface ArrivalLateToleranceEffective {
  enabled: boolean | null
  minutes: number
  inherited: boolean
}

export interface EffectiveReservationPolicy {
  scope: "PLATFORM" | "SITE"
  tenantId: string | null
  siteId: string | null
  evaluatedAt: string
  platformRevision: { id: string; version: number }
  siteRevision: { id: string; version: number } | null
  values: Record<string, EffectivePolicyValue>
  arrivalLateTolerance: ArrivalLateToleranceEffective
  cancellationTimezone: string
}

export type PolicyRevisionStatus = "DRAFT" | "SCHEDULED" | "CURRENT" | "SUPERSEDED"

export interface PolicyRevisionListItem {
  id: string
  scope: "PLATFORM" | "SITE"
  tenantId: string | null
  siteId: string | null
  version: number
  status: PolicyRevisionStatus
  isActive: boolean
  effectiveFrom: string
  effectiveUntil: string | null
  createdAt: string
  updatedAt: string
  paymentHoldDurationMinutes: number | null
  minReservationDurationMinutes: number | null
  maxReservationDurationHours: number | null
  maxAdvanceBookingDays: number | null
  interBookingBufferMinutes: number | null
  reservationProtectionWindowHours: number | null
  allocationLeadTimeMinutes: number | null
  facilityFailureRefundLeadMinutes: number | null
  occupancyValidityTimespanHours: number | null
  arrivalLateToleranceMinutes: number | null
  arrivalLateToleranceEnabled: boolean | null
  rapidCancellationCount: number | null
  rapidCancellationWindowMinutes: number | null
  rapidCancellationRestrictionMinutes: number | null
  dailyCancellationLimit: number | null
  cancellationTimezone: string | null
  overrideBounds: Record<string, PolicyBoundDto>
}

export interface PolicyRevisionPage {
  items: PolicyRevisionListItem[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface PolicyDraftValues {
  paymentHoldDurationMinutes: number
  minReservationDurationMinutes: number
  maxReservationDurationHours: number
  maxAdvanceBookingDays: number
  interBookingBufferMinutes: number
  reservationProtectionWindowHours: number
  allocationLeadTimeMinutes: number
  facilityFailureRefundLeadMinutes: number
  occupancyValidityTimespanHours: number
  arrivalLateToleranceMinutes: number
  rapidCancellationCount: number
  rapidCancellationWindowMinutes: number
  rapidCancellationRestrictionMinutes: number
  dailyCancellationLimit: number
  arrivalLateToleranceEnabled?: boolean | null
  cancellationTimezone: string
  overrideBounds?: Record<string, PolicyBoundDto> | null
}

export type CreatePolicyDraftRequest = PolicyDraftValues & {
  tenantId?: string | null
  siteId?: string | null
}

export type UpdatePolicyDraftRequest = PolicyDraftValues & { expectedUpdatedAt?: string }

export interface PolicyDraftSummary {
  id: string
  version: number
  scope: "PLATFORM"
  status: "DRAFT"
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface PolicyDraftUpdateResult {
  id: string
  version: number
  scope: "PLATFORM"
  status: "DRAFT"
  isActive: boolean
  updatedAt: string
}

export interface PolicyActivationResult {
  id: string
  version: number
  scope: "PLATFORM"
  status: "SCHEDULED" | "CURRENT"
  isActive: boolean
  effectiveFrom: string
  effectiveUntil: string | null
  supersededRevision: { id: string; version: number; effectiveUntil: string } | null
}

export async function getAdminFacilityDirectory(): Promise<AdminFacilityDirectory> {
  await ensureAccessToken()
  const response = await request<Response<AdminFacilityDirectory>>(
    "/facility-directory",
    "GET",
    undefined,
    "/api/v1/admin",
    facilityServer(),
  )
  if (!response.success || !Array.isArray(response.data?.tenants) || !Array.isArray(response.data?.sites))
    throw new Error("Invalid facility directory response.")
  return response.data
}

export async function getCatalogue(): Promise<PlatformPolicyCatalogue> {
  await ensureAccessToken()
  const response = await request<Response<PlatformPolicyCatalogue>>(
    "/catalogue",
    "GET",
    undefined,
    POLICY_PREFIX,
    reservationServer(),
  )
  if (!response.success || !response.data || !Array.isArray(response.data.policies))
    throw new Error("Invalid catalogue response.")
  return response.data
}

export async function getEffective(params?: {
  tenantId?: string
  siteId?: string
  at?: string
}): Promise<EffectiveReservationPolicy> {
  await ensureAccessToken()
  const search = new URLSearchParams()
  if (params?.tenantId) search.set("tenantId", params.tenantId)
  if (params?.siteId) search.set("siteId", params.siteId)
  if (params?.at) search.set("at", params.at)
  const suffix = search.toString() ? `?${search.toString()}` : ""
  const response = await request<Response<EffectiveReservationPolicy>>(
    `/effective${suffix}`,
    "GET",
    undefined,
    POLICY_PREFIX,
    reservationServer(),
  )
  if (!response.success || !response.data || typeof response.data.scope !== "string")
    throw new Error("Invalid effective policy response.")
  return response.data
}

export async function getRevisions(params?: {
  status?: string
  scope?: string
  tenantId?: string
  siteId?: string
  page?: number
  pageSize?: number
}): Promise<PolicyRevisionPage> {
  await ensureAccessToken()
  const search = new URLSearchParams()
  if (params?.status) search.set("status", params.status)
  if (params?.scope) search.set("scope", params.scope)
  if (params?.tenantId) search.set("tenantId", params.tenantId)
  if (params?.siteId) search.set("siteId", params.siteId)
  if (params?.page !== undefined) search.set("page", String(params.page))
  if (params?.pageSize !== undefined) search.set("pageSize", String(params.pageSize))
  const suffix = search.toString() ? `?${search.toString()}` : ""
  const response = await request<Response<PolicyRevisionPage>>(
    `/revisions${suffix}`,
    "GET",
    undefined,
    POLICY_PREFIX,
    reservationServer(),
  )
  if (!response.success || !response.data || !Array.isArray(response.data.items))
    throw new Error("Invalid revisions response.")
  return response.data
}

export async function getRevisionById(id: string): Promise<PolicyRevisionListItem> {
  await ensureAccessToken()
  const response = await request<Response<PolicyRevisionListItem>>(
    `/revisions/${id}`,
    "GET",
    undefined,
    POLICY_PREFIX,
    reservationServer(),
  )
  if (!response.success || !response.data?.id)
    throw new Error("Invalid revision response.")
  return response.data
}

export async function createDraft(body: CreatePolicyDraftRequest): Promise<PolicyDraftSummary> {
  await ensureAccessToken()
  const response = await request<Response<PolicyDraftSummary>>(
    "/draft",
    "POST",
    body,
    POLICY_PREFIX,
    reservationServer(),
  )
  if (!response.success || !response.data?.id) throw new Error("Invalid create draft response.")
  return response.data
}

export async function updateDraft(
  id: string,
  body: UpdatePolicyDraftRequest,
  expectedUpdatedAt: string,
): Promise<PolicyDraftUpdateResult> {
  await ensureAccessToken()
  const payload: UpdatePolicyDraftRequest = { ...body }
  delete payload.expectedUpdatedAt
  const response = await request<Response<PolicyDraftUpdateResult>>(
    `/draft/${id}`,
    "PUT",
    payload,
    POLICY_PREFIX,
    reservationServer(),
    { "If-Match": expectedUpdatedAt },
  )
  if (!response.success || !response.data?.id) throw new Error("Invalid draft update response.")
  return response.data
}

export async function activateRevision(
  id: string,
  options?: { effectiveFrom?: string },
): Promise<PolicyActivationResult> {
  await ensureAccessToken()
  const body = options?.effectiveFrom === undefined ? {} : { effectiveFrom: options.effectiveFrom }
  const response = await request<Response<PolicyActivationResult>>(
    `/revisions/${id}/activate`,
    "POST",
    body,
    POLICY_PREFIX,
    reservationServer(),
  )
  if (!response.success || !response.data?.id) throw new Error("Invalid activation response.")
  return response.data
}
