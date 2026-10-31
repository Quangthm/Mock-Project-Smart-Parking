import { useCallback, useEffect, useRef, useState } from "react"

import { useApp } from "../../context/AppContext"
import { store } from "../../lib/store"
import { uiToastEvent, type UiToastDetail } from "../../lib/uiToast"

interface Toast extends UiToastDetail { id: number }

export function AppToastHost() {
  const { user, authError } = useApp()
  const [queue, setQueue] = useState<Toast[]>([])
  const nextId = useRef(0)
  const knownIds = useRef(new Set<string>())
  const active = queue[0]

  const enqueue = useCallback((detail: UiToastDetail) => {
    const toast = { ...detail, id: ++nextId.current }
    setQueue(current => {
      if (detail.title === "Signed out" || detail.title === "Session expired") {
        return [toast]
      }
      if (current.some(item => item.title === detail.title && item.message === detail.message)) return current
      const kept = current.length < 4 ? current : [current[0], ...current.slice(-2)]
      return [...kept, toast]
    })
  }, [])

  useEffect(() => {
    const handleToast = (event: Event) => enqueue((event as CustomEvent<UiToastDetail>).detail)
    window.addEventListener(uiToastEvent, handleToast)
    return () => window.removeEventListener(uiToastEvent, handleToast)
  }, [enqueue])

  useEffect(() => {
    if (!authError) return
    enqueue({
      title: authError.includes("session has expired") ? "Session expired" : "Action could not be completed",
      message: authError,
      tone: "error",
    })
  }, [authError, enqueue])

  useEffect(() => {
    if (!user?.id) { knownIds.current.clear(); return }
    knownIds.current = new Set(store.getNotifications(user.id).map(item => item.id))
    const refresh = () => {
      const latest = store.getNotifications(user.id)
      for (const item of latest) {
        if (!knownIds.current.has(item.id)) {
          knownIds.current.add(item.id)
          enqueue({ title: item.title, message: item.message, tone: "info" })
        }
      }
    }
    window.addEventListener("sp-data-change", refresh)
    window.addEventListener("storage", refresh)
    return () => {
      window.removeEventListener("sp-data-change", refresh)
      window.removeEventListener("storage", refresh)
    }
  }, [user?.id, enqueue])

  useEffect(() => {
    if (!active) return
    const timer = window.setTimeout(() => setQueue(current => current.filter(item => item.id !== active.id)), 5_200)
    return () => window.clearTimeout(timer)
  }, [active?.id])

  return <div className="app-toast-viewport">
    {active && <div key={active.id} className="app-toast" data-tone={active.tone} role={active.tone === "error" ? "alert" : "status"}>
      <span className="app-toast-mark" aria-hidden="true">{active.tone === "success" ? "✓" : active.tone === "error" ? "!" : "i"}</span>
      <span className="app-toast-copy"><strong>{active.title}</strong><span>{active.message}</span></span>
    </div>}
  </div>
}
