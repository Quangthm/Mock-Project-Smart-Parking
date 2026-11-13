import { request } from "./authApi"

export interface AssistantStatus { enabled: boolean; guestFaq: boolean; locales: string[]; contractVersion: string }
export interface Conversation { id: string; title: string; locale: string; revision: number; createdAt: string; lastMessageAt: string | null; expiresAt: string | null }
export interface ChatMessage { id: string; sequence: number; role: string; content: string; createdAt: string; clientMessageId: string; outcome: string | null; sources: unknown[]; actions: unknown[]; mode: string; cards: unknown[] }
export interface RequestResult { requestId: string; clientMessageId: string; conversationId: string; state: "running" | "completed" | "failed" | "cancelled"; revision: number; answer: ChatMessage | null; requestError: { code: string; message: string; retryable: boolean } | null }
export interface Page<T> { items: T[]; nextCursor: string | null }
export interface History extends Page<ChatMessage> { conversation: Conversation; activeRequestClientMessageId: string | null }
const configured = (import.meta.env.VITE_ASSISTANT_API_BASE_URL ?? "").trim()
let server = ""
try {
  const url = new URL(configured)
  if (!["https:", "http:"].includes(url.protocol) || url.username || url.password || url.pathname !== "/" || url.search || url.hash) throw new Error()
  server = url.origin
} catch { /* Explicit configuration only; no Demo or UserService fallback. */ }
export const assistantConfigured = Boolean(server)
function requireServer() { if (!server) throw new Error("ASSISTANT_NOT_CONFIGURED") }
function id(value: string) {
  if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value) || /^0{8}-0{4}-0{4}-0{4}-0{12}$/.test(value)) throw new Error("INVALID_ASSISTANT_ID")
  return value
}
function data<T>(envelope: { success: boolean; data: T }): T {
  if (envelope?.success !== true || envelope.data === null || typeof envelope.data !== "object") throw new Error("INVALID_ASSISTANT_RESPONSE")
  return envelope.data
}
async function privateCall<T>(path: string, body?: unknown, signal?: AbortSignal): Promise<T> {
  requireServer()
  return data(await request<{ success: boolean; data: T }>(path, body === undefined ? "GET" : "POST", body,
    "/api/assistant", server, undefined,
    { expireSessionOnUnauthorized: false, timeoutMs: 30_000, signal, redirect: "error", credentials: "omit" }))
}
async function publicCall<T>(path: string, body?: unknown, signal?: AbortSignal): Promise<T> {
  requireServer()
  const response = await fetch(server + "/api/assistant" + path, {
    method: body === undefined ? "GET" : "POST", cache: "no-store", credentials: "omit", redirect: "error",
    signal: signal ? AbortSignal.any([signal, AbortSignal.timeout(30_000)]) : AbortSignal.timeout(30_000),
    ...(body === undefined ? {} : { headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) }),
  })
  if (!response.ok) throw Object.assign(new Error("ASSISTANT_REQUEST_FAILED"), { status: response.status })
  return data(await response.json())
}
function paging(cursor?: string | null) { return cursor ? "?cursor=" + encodeURIComponent(cursor) : "" }
export const assistantApi = {
  status: (signal?: AbortSignal) => publicCall<AssistantStatus>("/status", undefined, signal),
  guest: (message: string, locale: "vi" | "en" | "auto", signal?: AbortSignal) => {
    const text = message.trim()
    if (!text || text.length > 4000) throw new Error("INVALID_ASSISTANT_MESSAGE")
    return publicCall<Record<string, unknown>>("/guest/messages", { requestId: crypto.randomUUID(), message: text, locale }, signal)
  },
  capabilities: (signal?: AbortSignal) => privateCall<Record<string, unknown>>("/capabilities", undefined, signal),
  create: (clientConversationId: string, locale: "vi" | "en", signal?: AbortSignal) => privateCall<Conversation>("/conversations", { clientConversationId: id(clientConversationId), locale }, signal),
  conversations: (cursor?: string | null, signal?: AbortSignal) => privateCall<Page<Conversation>>("/conversations" + paging(cursor), undefined, signal),
  reset: async (conversationId: string, signal?: AbortSignal) => {
    const result = await privateCall<{ reset: boolean }>(`/conversations/${id(conversationId)}/reset`, {}, signal)
    if (result.reset !== true) throw new Error("INVALID_ASSISTANT_RESPONSE")
  },
  history: (conversationId: string, cursor?: string | null, signal?: AbortSignal) => privateCall<History>(`/conversations/${id(conversationId)}/messages` + paging(cursor), undefined, signal),
  send: (conversationId: string, body: { clientMessageId: string; expectedRevision: number; message: string }, signal?: AbortSignal) => {
    id(body.clientMessageId)
    if (!Number.isSafeInteger(body.expectedRevision) || body.expectedRevision < 0 || !body.message.trim() || body.message.trim().length > 4000) throw new Error("INVALID_ASSISTANT_MESSAGE")
    return privateCall<RequestResult>(`/conversations/${id(conversationId)}/messages`, body, signal)
  },
  result: (conversationId: string, clientMessageId: string, signal?: AbortSignal) => privateCall<RequestResult>(`/conversations/${id(conversationId)}/requests/${id(clientMessageId)}`, undefined, signal),
  cancel: (conversationId: string, clientMessageId: string, signal?: AbortSignal) => privateCall<RequestResult>(`/conversations/${id(conversationId)}/requests/${id(clientMessageId)}/cancel`, {}, signal),
}
