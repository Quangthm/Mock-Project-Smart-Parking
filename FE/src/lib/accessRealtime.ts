import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr"
import { ensureAccessToken } from "./authApi"

export type AccessRevokedReason = "USER_LOCKED" | "ACCOUNT_DISABLED" | "SESSION_REVOKED"

const serverUrl = (import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5035").replace(/\/$/, "")

export function watchAccessRevocation(onRevoked: (reason: AccessRevokedReason) => void) {
  let stopped = false
  let retry: ReturnType<typeof setTimeout> | undefined
  let starting: Promise<void> | undefined
  const connection = new HubConnectionBuilder()
    .withUrl(`${serverUrl}/hubs/operators`, { accessTokenFactory: ensureAccessToken, withCredentials: false })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.None)
    .build()

  const stop = () => {
    if (stopped) return
    stopped = true
    clearTimeout(retry)
    connection.off("AccessRevoked", revoked)
    window.removeEventListener("focus", resume)
    document.removeEventListener("visibilitychange", resume)
    void connection.stop()
  }
  const revoked = (message: { reasonCode?: string } | undefined) => {
    if (stopped) return
    onRevoked(message?.reasonCode === "USER_LOCKED" || message?.reasonCode === "ACCOUNT_DISABLED"
      ? message.reasonCode : "SESSION_REVOKED")
    stop()
  }
  const connect = () => {
    if (stopped || starting || connection.state !== "Disconnected" || document.visibilityState === "hidden") return
    clearTimeout(retry)
    retry = undefined
    starting = connection.start().catch((err: any) => {
      if (!stopped) {
        const status = err?.statusCode || (err && (err as any).status)
        const msg = String(err?.message || err || "")
        if (status === 403 || msg.includes("403") || msg.includes("Forbidden")) {
          stop()
          return
        }
        // Realtime is optional; protected HTTP requests still enforce access.
        console.warn("Access updates are temporarily unavailable; retrying.")
        retry = setTimeout(connect, 10_000)
      }
    }).finally(() => {
      starting = undefined
      if (stopped) void connection.stop()
    })
  }
  const resume = () => {
    if (document.visibilityState !== "hidden") connect()
  }
  connection.on("AccessRevoked", revoked)
  connection.onclose(() => {
    if (!stopped) {
      clearTimeout(retry)
      retry = setTimeout(connect, 2_000)
    }
  })
  window.addEventListener("focus", resume)
  document.addEventListener("visibilitychange", resume)
  connect()
  return stop
}
