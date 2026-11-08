import { useEffect, useState } from "react"
import { assistantApi, assistantConfigured } from "../../lib/assistantApi"
import type { Role } from "../../lib/types"
import { FloatingChatbot, type GuestChatTransport } from "./FloatingChatbot"

async function sendGuest(message: string, signal: AbortSignal, signedIn: boolean): ReturnType<GuestChatTransport["send"]> {
  const data = await assistantApi.guest(message, "auto", signal)
  if (typeof data.content !== "string" || !data.content || data.content.length > 8000) {
    throw new Error("SmartParking couldn't return guidance right now.")
  }
  const mode = data.mode === "ai" ? "provider" : data.mode
  if (mode !== "provider" && mode !== "knowledge" && mode !== "system") throw new Error("SmartParking couldn't return guidance right now.")
  const loginRequired = data.outcome === "handoff" && Array.isArray(data.actions) &&
    data.actions.some(action => action?.type === "login")
  let reply = data.content
  if (loginRequired && signedIn) {
    reply = data.replyLocale === "en"
      ? "Use the assistant on the Driver dashboard to view a Driver account’s vehicles and reservations."
      : "Dùng trợ lý trong dashboard Driver để xem xe và lịch đặt của tài khoản Driver."
  }
  return { reply, mode, loginRequired: loginRequired && !signedIn }
}

export function GuestChatbot({ role = "driver", onSignIn }: { role?: Role; onSignIn?: (question: string) => void }) {
  const [ready, setReady] = useState(false)
  const [open, setOpen] = useState(false)
  useEffect(() => {
    if (!assistantConfigured) return
    let active: AbortController | undefined
    let disposed = false
    async function refresh() {
      if (document.visibilityState === "hidden" || active) return
      const controller = new AbortController()
      active = controller
      try {
        const status = await assistantApi.status(controller.signal)
        if (!disposed) setReady(status.enabled === true && status.guestFaq === true)
      } catch {
        if (!disposed) setReady(false)
      } finally {
        active = undefined
      }
    }
    void refresh()
    const interval = window.setInterval(refresh, 30_000)
    window.addEventListener("focus", refresh)
    document.addEventListener("visibilitychange", refresh)
    return () => {
      disposed = true
      active?.abort()
      window.clearInterval(interval)
      window.removeEventListener("focus", refresh)
      document.removeEventListener("visibilitychange", refresh)
    }
  }, [open])
  return ready ? <FloatingChatbot role={role} onOpenChange={setOpen} guest={{
    send: (message, signal) => sendGuest(message, signal, !onSignIn),
    onSignIn,
  }} /> : null
}
