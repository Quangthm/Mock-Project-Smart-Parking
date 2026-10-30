import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr"
import { authApi, authExpiredEvent, authRefreshedEvent, ensureAccessToken } from "./authApi"

const serverUrl = (import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5035").replace(/\/$/, "")

export function watchOperators(onChanged: () => void) {
  let stopped = false
  let retry: ReturnType<typeof setTimeout> | undefined
  let starting: Promise<void> | undefined
  const connection = new HubConnectionBuilder()
    .withUrl(serverUrl + "/hubs/operators", { accessTokenFactory: ensureAccessToken, withCredentials: false })
    .configureLogging(LogLevel.None)
    .build()
  connection.on("OperatorsChanged", onChanged)
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
    if (stopped || starting || connection.state !== "Disconnected" || document.visibilityState === "hidden") return
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
  const expire = () => { clearRetry(); void connection.stop() }
  window.addEventListener(authRefreshedEvent, resume)
  window.addEventListener(authExpiredEvent, expire)
  window.addEventListener("focus", resume)
  document.addEventListener("visibilitychange", resume)
  void connect()
  return () => {
    stopped = true
    clearRetry()
    window.removeEventListener(authRefreshedEvent, resume)
    window.removeEventListener(authExpiredEvent, expire)
    window.removeEventListener("focus", resume)
    document.removeEventListener("visibilitychange", resume)
    void connection.stop()
  }
}
