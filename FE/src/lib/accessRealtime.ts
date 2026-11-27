import { HubConnectionBuilder, HttpTransportType, LogLevel } from "@microsoft/signalr"
import { ensureAccessToken } from "./authApi"

export type AccessRevokedReason = "USER_LOCKED" | "ACCOUNT_DISABLED" | "SESSION_REVOKED"

const serverUrl = (import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5035").replace(/\/$/, "")
const retryDelays = [2_000, 10_000, 30_000]

function isTerminal(error: unknown): boolean {
  if (!error || typeof error !== "object") return false
  const failure = error as { status?: number; statusCode?: number; message?: string; innerErrors?: unknown[] }
  return [401, 403, 404].includes(failure.statusCode ?? failure.status ?? 0) ||
    /\b(forbidden|unauthorized|401|403|404)\b/i.test(failure.message ?? "") ||
    (Array.isArray(failure.innerErrors) && failure.innerErrors.some(isTerminal))
}

export function watchAccessRevocation(onRevoked: (reason: AccessRevokedReason) => void) {
  let stopped = false
  let retry: ReturnType<typeof setTimeout> | undefined
  let starting: Promise<void> | undefined
  let connectedAt: number | undefined
  let attempts = 0
  const connection = new HubConnectionBuilder()
    .withUrl(`${serverUrl}/hubs/access`, {
      accessTokenFactory: ensureAccessToken, withCredentials: false, transport: HttpTransportType.WebSockets,
    })
    .configureLogging(LogLevel.None)
    .build()

  const stop = () => {
    if (stopped) return
    stopped = true
    clearTimeout(retry)
    connection.off("AccessRevoked", revoked)
    window.removeEventListener("focus", resume)
    document.removeEventListener("visibilitychange", resume)
    void connection.stop().catch(() => {})
  }
  const revoked = (message: { reasonCode?: string } | undefined) => {
    if (stopped) return
    onRevoked(message?.reasonCode === "USER_LOCKED" || message?.reasonCode === "ACCOUNT_DISABLED"
      ? message.reasonCode : "SESSION_REVOKED")
    stop()
  }
  const connect = () => {
    if (stopped || starting || connection.state !== "Disconnected" || document.visibilityState === "hidden") return
    starting = connection.start().then(() => { connectedAt = Date.now() }).catch((error: unknown) => {
      if (isTerminal(error)) stop()
      else scheduleRetry()
    }).finally(() => {
      starting = undefined
    })
  }
  function scheduleRetry() {
    if (stopped || retry !== undefined || attempts >= retryDelays.length) return
    retry = setTimeout(() => {
      retry = undefined
      connect()
    }, retryDelays[attempts++])
  }
  const resume = () => {
    if (retry === undefined && attempts < retryDelays.length) connect()
  }
  connection.on("AccessRevoked", revoked)
  connection.onclose((error?: Error) => {
    if (stopped) return
    if (isTerminal(error)) { stop(); return }
    if (connectedAt !== undefined && Date.now() - connectedAt >= 60_000) attempts = 0
    scheduleRetry()
  })
  window.addEventListener("focus", resume)
  document.addEventListener("visibilitychange", resume)
  connect()
  return stop
}
