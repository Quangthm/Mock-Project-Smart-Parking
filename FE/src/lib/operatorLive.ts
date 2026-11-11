import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr"
import { authApi, authExpiredEvent, authRefreshedEvent, ensureAccessToken } from "./authApi"

const serverUrl = (import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5035").replace(/\/$/, "")

const listeners = new Set<() => void>()
let stopShared: (() => void) | undefined
let stopping: Promise<void> | undefined

export function watchOperators(onChanged: () => void) {
  // Each registration owns its cleanup, even if two consumers use the same callback.
  const listener = () => onChanged()
  listeners.add(listener)
  stopShared ??= startOperators(() => listeners.forEach(notify => notify()))
  return () => {
    if (!listeners.delete(listener) || listeners.size) return
    stopShared?.()
    stopShared = undefined
  }
}

function startOperators(onChanged: () => void) {
  let stopped = false
  let retry: ReturnType<typeof setTimeout> | undefined
  let invalidation: ReturnType<typeof setTimeout> | undefined
  let starting: Promise<void> | undefined
  const connection = new HubConnectionBuilder()
    .withUrl(serverUrl + "/hubs/operators", { accessTokenFactory: ensureAccessToken, withCredentials: false })
    .configureLogging(LogLevel.None)
    .build()
  const invalidate = () => {
    if (stopped || invalidation !== undefined) return
    invalidation = setTimeout(() => {
      invalidation = undefined
      if (!stopped) onChanged()
    }, 250)
  }
  connection.on("OperatorsChanged", invalidate)
  function clearRetry() {
    clearTimeout(retry)
    retry = undefined
  }
  function scheduleRetry(delay: number) {
    clearRetry()
    if (stopped || authApi.expiresAt() <= Date.now() + 30_000) return
    retry = setTimeout(() => {
      retry = undefined
      if (authApi.expiresAt() > Date.now() + 30_000) void connect()
    }, delay)
  }
  async function connect() {
    if (stopped || starting || connection.state !== "Disconnected" || document.visibilityState === "hidden" ||
      authApi.expiresAt() <= Date.now() + 30_000) return
    if (stopping) {
      await stopping
      if (stopped || starting || connection.state !== "Disconnected") return
    }
    clearRetry()
    starting = connection.start().then(() => {
      clearRetry()
      if (!stopped) onChanged()
    }).catch(() => {
      scheduleRetry(10_000)
    }).finally(() => { starting = undefined })
    await starting
  }
  // Reconnect after the next authenticated activity/refresh; never refresh solely
  // to keep an idle subscription alive. Server closes connections at token expiry.
  connection.onclose(() => {
    scheduleRetry(2_000)
  })
  const resume = () => {
    if (connection.state === "Disconnected") void connect()
  }
  const expire = () => {
    clearRetry()
    clearTimeout(invalidation)
    invalidation = undefined
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
