import { request } from "./authApi"

export interface AiProvider { id: "gemini" | "openai"; model: string; enabled: boolean; priority: number }
export interface AiKey {
  id: string; provider: "gemini" | "openai"; label: string; enabled: boolean; priority: number
  createdAt: string; revokedAt: string | null
}
export interface AiConfiguration extends AiKey { model: string; credentialVersion: string }
export interface ModelPage { items: { id: string; displayName: string; supportsChat: boolean }[]; nextCursor: string | null; asOf: string }
export interface TestResult {
  testRequestId: string; configurationId: string; testedRevision: number; model: string
  outcome: "passed" | "failed" | "unknown"; latencyMs: number; checkedAt: string; current: boolean; code: string | null
}
export interface AiSettings {
  revision: number; enabled: boolean; maintenanceMessage: string; providers: AiProvider[]; configurations: AiConfiguration[]; contractVersion: 2
}
export interface AiManagement { settings: AiSettings; secretsConfigured: boolean; runtimeReleaseApproved: boolean }
const configuredServer = (import.meta.env.VITE_ASSISTANT_API_BASE_URL ?? "").trim()
function trustedServer(): string {
  try {
    const url = new URL(configuredServer)
    if (!["http:", "https:"].includes(url.protocol) || url.username || url.password || url.search || url.hash) return ""
    return url.href.replace(/\/$/, "")
  } catch { return "" }
}
const server = trustedServer()
export const assistantAdminConfigured = server.length > 0
const id = (value: unknown) => typeof value === "string" && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value)
const integer = (value: unknown) => Number.isSafeInteger(value) && Number(value) >= 0
const providerId = (value: unknown) => value === "gemini" || value === "openai"
function invalid(): never { throw new Error("INVALID_ASSISTANT_ADMIN_RESPONSE") }
function management(value: unknown): AiManagement {
  const view = value as AiManagement, settings = view?.settings
  if (!settings || !integer(settings.revision) || typeof settings.enabled !== "boolean" ||
    typeof settings.maintenanceMessage !== "string" || typeof view.secretsConfigured !== "boolean" ||
    typeof view.runtimeReleaseApproved !== "boolean" || !Array.isArray(settings.providers) ||
    settings.providers.length !== 2 || settings.contractVersion !== 2 || !Array.isArray(settings.configurations) || settings.configurations.length > 64) invalid()
  for (const p of settings.providers) {
    if (!p || !providerId(p.id) || typeof p.model !== "string" || typeof p.enabled !== "boolean" || !integer(p.priority)) invalid()
  }
  if (new Set(settings.providers.map(p => p.id)).size !== 2) invalid()
  for (const key of settings.configurations) {
    if (!key || !id(key.id) || !id(key.credentialVersion) || typeof key.model !== "string" || !providerId(key.provider) || typeof key.label !== "string" ||
      typeof key.enabled !== "boolean" || !integer(key.priority) || typeof key.createdAt !== "string" ||
      !(key.revokedAt === null || typeof key.revokedAt === "string")) invalid()
  }
  // Retain public metadata only; never hold unknown credential fields in UI state.
  return {
    secretsConfigured: view.secretsConfigured, runtimeReleaseApproved: view.runtimeReleaseApproved,
    settings: {
      revision: settings.revision, enabled: settings.enabled, maintenanceMessage: settings.maintenanceMessage,
      providers: settings.providers.map(({ id, model, enabled, priority }) => ({ id, model, enabled, priority })),
      contractVersion: 2,
      configurations: settings.configurations.map(({ id, provider, label, model, enabled, priority, credentialVersion, createdAt, revokedAt }) =>
        ({ id, provider, label, model, enabled, priority, credentialVersion, createdAt, revokedAt })),
    },
  }
}
async function call<T>(path: string, body?: Record<string, unknown>, timeoutMs?: number): Promise<T> {
  if (!assistantAdminConfigured) throw new Error("ASSISTANT_NOT_CONFIGURED")
  // Refresh/session-generation checks still belong to UserService. An Assistant
  // rejection cannot establish that the application's sign-in has expired.
  const envelope = await request<{ success: boolean; data: T }>(
    path, body === undefined ? "GET" : "POST", body, "/api/assistant/admin", server, undefined,
    { expireSessionOnUnauthorized: false, ...(timeoutMs === undefined ? {} : { timeoutMs }) })
  if (envelope?.success !== true) invalid()
  return envelope.data
}
export const assistantAdminApi = {
  read: async () => management(await call("/settings")),
  change: async (body: Record<string, unknown>) => management(await call("/mutations", body)),
  async discover(body: { provider: AiProvider["id"]; secret: string; cursor: string | null } |
    { configurationId: string; expectedRevision: number; cursor: string | null }): Promise<ModelPage> {
    const page = await call<ModelPage>("/models/discover", body)
    if (!Array.isArray(page?.items) || page.items.length > 100 ||
      !page.items.every(item => item && typeof item.id === "string" && typeof item.displayName === "string" && item.supportsChat === true) ||
      !(page.nextCursor === null || typeof page.nextCursor === "string") || typeof page.asOf !== "string") invalid()
    return { items: page.items.map(({ id, displayName, supportsChat }) => ({ id, displayName, supportsChat })),
      nextCursor: page.nextCursor, asOf: page.asOf }
  },
  async test(configurationId: string, body: { expectedRevision: number; testRequestId: string }) {
    if (!id(configurationId)) invalid()
    const result = await call<TestResult | { outcome: "running"; configurationId: string; testRequestId: string }>(
      `/configurations/${configurationId}/test`, body, 35_000)
    if (result?.testRequestId !== body.testRequestId || result.configurationId !== configurationId) invalid()
    if (result.outcome === "running") return { outcome: "running" } as const
    const { outcome, checkedAt, model, current, testedRevision, latencyMs, code, testRequestId } = result
    if (!["passed", "failed", "unknown"].includes(outcome) || testedRevision !== body.expectedRevision ||
      !integer(latencyMs) || typeof current !== "boolean" || typeof model !== "string" ||
      !Number.isFinite(Date.parse(checkedAt)) || !(code === null || typeof code === "string")) invalid()
    return { outcome, checkedAt, model, current, testedRevision, latencyMs, code, testRequestId, configurationId }
  },
}
