import { useEffect, useRef } from "react"
import { authApi } from "./authApi"

// Refresh on returning to the app. Timed polling is opt-in for existing checkout flows.
type Refresh = (isCurrent: () => boolean) => void | Promise<void>

export function watchServerRefresh(refresh: Refresh, intervalMs?: number) {
  let disposed = false
  let pending = false
  let lastStarted = Date.now()

  const run = async () => {
    if (disposed || pending || document.visibilityState === "hidden" || navigator.onLine === false) return
    if (Date.now() - lastStarted < Math.min(5_000, intervalMs ?? 5_000)) return
    lastStarted = Date.now()
    pending = true
    try {
      await refresh(() => !disposed)
    } finally {
      pending = false
    }
  }
  const schedule = () => { void run().catch(() => { /* The resource loader owns its error state. */ }) }
  const timer = intervalMs === undefined ? undefined : window.setInterval(schedule, intervalMs)
  window.addEventListener("focus", schedule)
  window.addEventListener("online", schedule)
  document.addEventListener("visibilitychange", schedule)
  return () => {
    disposed = true
    window.clearInterval(timer)
    window.removeEventListener("focus", schedule)
    window.removeEventListener("online", schedule)
    document.removeEventListener("visibilitychange", schedule)
  }
}

export function useServerRefresh(scope: string | undefined, refresh: Refresh, intervalMs?: number) {
  const currentRefresh = useRef(refresh)
  currentRefresh.current = refresh
  useEffect(() => {
    if (!scope) return
    return watchServerRefresh(isCurrent => {
      // A background read must not renew an unattended session.
      if (authApi.expiresAt() > Date.now() + 30_000) return currentRefresh.current(isCurrent)
    }, intervalMs)
  }, [scope, intervalMs])
}
