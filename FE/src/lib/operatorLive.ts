import { HubConnectionBuilder, HttpTransportType, LogLevel } from "@microsoft/signalr"
import { authApi, authExpiredEvent, authRefreshedEvent, ensureAccessToken } from "./authApi"

const serverUrl = (import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5035").replace(/\/$/, "")
const retryDelays = [2_000, 10_000, 30_000]

let stopping: Promise<void> | undefined

function authorizationFailure(error: unknown): 401 | 403 | undefined {
  if (!error || typeof error !== "object") return undefined
  const failure = error as { status?: number; statusCode?: number; message?: string; innerErrors?: unknown[] }
  const status = failure.statusCode ?? failure.status
  if (status === 401 || status === 403) return status
  if (/\b(forbidden|403)\b/i.test(failure.message ?? "")) return 403
  if (/\b(unauthorized|unauthenticated|401)\b/i.test(failure.message ?? "")) return 401
  if (Array.isArray(failure.innerErrors)) {
    return failure.innerErrors.map(authorizationFailure).find(status => status !== undefined)
  }
}

export function watchOperators(onChanged: () => void, onDenied: () => void, onUnavailable?: () => void) {
  let stopped = false
  let blocked = false
  let connectedAt: number | undefined
  let attempts = 0
  let retry: ReturnType<typeof setTimeout> | undefined
  let invalidation: ReturnType<typeof setTimeout> | undefined
  let starting: Promise<void> | undefined
  const connection = new HubConnectionBuilder()
    .withUrl(serverUrl + "/hubs/operators", {
      accessTokenFactory: ensureAccessToken, withCredentials: false, transport: HttpTransportType.WebSockets,
    })
    .configureLogging(LogLevel.None)
    .build()
  const invalidate = () => {
    if (stopped || blocked || invalidation !== undefined) return
    invalidation = setTimeout(() => {
      invalidation = undefined
      if (!stopped && !blocked) onChanged()
    }, 250)
  }
  connection.on("OperatorsChanged", invalidate)
  function clearRetry() {
    clearTimeout(retry)
    retry = undefined
  }
  function block() {
    blocked = true
    clearRetry()
    clearTimeout(invalidation)
  }
  function handleAuthorizationFailure(error: unknown) {
    const status = authorizationFailure(error)
    if (!status) return false
    block()
    if (status === 403) onDenied()
    else onUnavailable?.()
    return true
  }
  function scheduleRetry() {
    if (stopped || blocked || retry !== undefined || authApi.expiresAt() <= Date.now() + 30_000) return
    if (attempts >= retryDelays.length) { onUnavailable?.(); return }
    const delay = retryDelays[attempts++]
    const timer = setTimeout(() => {
      if (retry !== timer) return
      retry = undefined
      if (authApi.expiresAt() > Date.now() + 30_000) void connect()
    }, delay)
    retry = timer
  }
  async function connect() {
    if (stopped || blocked || starting || connection.state !== "Disconnected" || document.visibilityState === "hidden" ||
      authApi.expiresAt() <= Date.now() + 30_000) return
    if (stopping) {
      await stopping
      if (stopped || blocked || starting || connection.state !== "Disconnected" ||
        authApi.expiresAt() <= Date.now() + 30_000) return
    }
    clearRetry()
    starting = connection.start().then(() => {
      clearRetry()
      if (stopped || blocked) return
      const reconnecting = connectedAt !== undefined || attempts > 0
      connectedAt = Date.now()
      if (reconnecting) invalidate()
    }).catch((error: unknown) => {
      if (stopped || blocked) return
      if (!handleAuthorizationFailure(error)) scheduleRetry()
    }).finally(() => { starting = undefined })
    await starting
  }
  // Reconnect after the next authenticated activity/refresh; never refresh solely
  // to keep an idle subscription alive. Server closes connections at token expiry.
  connection.onclose((error?: Error) => {
    if (stopped || blocked) return
    if (handleAuthorizationFailure(error)) return
    // A websocket can upgrade successfully and then be rejected by the hub.
    if (connectedAt !== undefined && Date.now() - connectedAt >= 60_000) attempts = 0
    scheduleRetry()
  })
  const resume = () => {
    if (attempts < retryDelays.length) void connect()
  }
  const expire = () => {
    block()
    void connection.stop().catch(() => {})
  }
  window.addEventListener(authRefreshedEvent, resume)
  window.addEventListener(authExpiredEvent, expire)
  window.addEventListener("focus", resume)
  window.addEventListener("online", resume)
  document.addEventListener("visibilitychange", resume)
  void connect()
  return () => {
    stopped = true
    clearRetry()
    clearTimeout(invalidation)
    connection.off?.("OperatorsChanged", invalidate)
    window.removeEventListener(authRefreshedEvent, resume)
    window.removeEventListener(authExpiredEvent, expire)
    window.removeEventListener("focus", resume)
    window.removeEventListener("online", resume)
    document.removeEventListener("visibilitychange", resume)
    const shutdown = connection.stop().catch(() => {})
    stopping = shutdown
    void shutdown.then(() => { if (stopping === shutdown) stopping = undefined })
  }
}
