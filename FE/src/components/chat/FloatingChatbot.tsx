import { useEffect, useRef, useState, type FormEvent, type PointerEvent } from "react"
import type { Role } from "../../lib/types"
import { sendDemoMessage, type DemoReply } from "../../lib/assistantDemoApi"
import { UntitledIcon } from "../icon/UntitledIcon"

type Turn = { id: number; question: string; result?: DemoReply | Awaited<ReturnType<GuestChatTransport["send"]>>; error?: string }

const welcome = "Hello! I can help with general SmartParking questions."
const suggestions = ["How do I find parking?", "How do I book a parking space?", "How do parking payments work?"]
const demoEnabled = import.meta.env.DEV && import.meta.env.VITE_ASSISTANT_DEMO_ENABLED === "true"
export type GuestChatTransport = {
  send: (message: string, signal: AbortSignal) => Promise<Pick<DemoReply, "reply"> & { mode: "knowledge" | "provider" | "mock" | "system"; loginRequired: boolean }>
  onSignIn?: (question: string) => void
}
const launcherSize = 56
const edgeGap = 16
type Position = { x: number; y: number }
function clamp(value: number, min: number, max: number) { return Math.min(Math.max(value, min), max) }

export function FloatingChatbot({ role, parkingMode = false, parkingSheetOpen = false, parkingDetailsOpen = false, bookingInProgress = false, onOpenChange, guest }: {
  role: Role
  parkingMode?: boolean
  parkingSheetOpen?: boolean
  parkingDetailsOpen?: boolean
  bookingInProgress?: boolean
  onOpenChange?: (open: boolean) => void
  guest?: GuestChatTransport
}) {
  const assistantEnabled = Boolean(guest) || demoEnabled
  const prompts = guest ? ["SmartPark là gì?", "Cách tìm bãi?", "Cách đặt chỗ?"] : suggestions
  const [open, setOpen] = useState(false)
  const [turns, setTurns] = useState<Turn[]>([])
  const [input, setInput] = useState("")
  const [pending, setPending] = useState(false)
  const requestRef = useRef<AbortController | null>(null)
  const revisionRef = useRef(0)
  const nextId = useRef(0)
  const scrollRef = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLInputElement>(null)
  const launcherRef = useRef<HTMLButtonElement>(null)
  const dragRef = useRef<{ pointerId: number; x: number; y: number; start: Position; moved: boolean } | null>(null)
  const suppressClickRef = useRef(false)
  const wasDraggedRef = useRef(false)
  const [viewport, setViewport] = useState(() => ({ width: window.innerWidth, height: window.innerHeight }))
  const [position, setPosition] = useState<Position>(() => ({ x: window.innerWidth - launcherSize - 24, y: window.innerHeight - launcherSize - 24 }))

  useEffect(() => {
    const resize = () => {
      setViewport({ width: window.innerWidth, height: window.innerHeight })
      const driverMobile = role === "driver" && window.matchMedia("(max-width: 900px)").matches
      const bottomGap = driverMobile ? parkingMode ? 88 : 96 : edgeGap
      if (!open) setPosition((current) => ({ x: clamp(current.x, edgeGap, Math.max(edgeGap, window.innerWidth - launcherSize - edgeGap)), y: clamp(current.y, parkingMode ? edgeGap : 76, Math.max(parkingMode ? edgeGap : 76, window.innerHeight - launcherSize - bottomGap)) }))
    }
    window.addEventListener("resize", resize)
    return () => window.removeEventListener("resize", resize)
  }, [open, parkingMode, role])
  useEffect(() => {
    if (open) return
    if (wasDraggedRef.current && !parkingDetailsOpen) return
    const mobile = window.matchMedia("(max-width: 900px)").matches
    const details = parkingMode && parkingDetailsOpen ? document.querySelector(".find-parking-details-wrap")?.getBoundingClientRect() : null
    const x = details && !mobile ? details.left - launcherSize - 16 : viewport.width - launcherSize - (mobile ? 18 : 24)
    const y = details && mobile ? details.top - launcherSize - 12 : viewport.height - launcherSize - (parkingMode && mobile && parkingSheetOpen ? Math.min(viewport.height * .46, 370) + 36 : parkingMode && mobile ? 88 : role === "driver" && mobile ? 96 : 24)
    setPosition({ x: clamp(x, edgeGap, viewport.width - launcherSize - edgeGap), y: clamp(y, edgeGap, viewport.height - launcherSize - edgeGap) })
  }, [open, parkingMode, parkingSheetOpen, parkingDetailsOpen, role, viewport])

  const mobileParking = parkingMode && window.matchMedia("(max-width: 900px)").matches
  const detailsRect = parkingMode && parkingDetailsOpen ? document.querySelector(".find-parking-details-wrap")?.getBoundingClientRect() : null
  const searchRect = parkingMode ? document.querySelector(".find-parking-panel:not([hidden])")?.getBoundingClientRect() : null
  const safeLeft = parkingMode && !mobileParking && searchRect ? searchRect.right + 12 : edgeGap
  const safeRight = parkingMode && !mobileParking && detailsRect ? detailsRect.left - 12 : viewport.width - edgeGap
  const safeTop = mobileParking ? searchRect ? searchRect.bottom + 12 : 118 : parkingMode ? 205 : 76
  const driverMobile = role === "driver" && window.matchMedia("(max-width: 900px)").matches
  const safeBottom = mobileParking && detailsRect ? detailsRect.top - 12 : driverMobile ? viewport.height - 96 : viewport.height - edgeGap
  const chatWidth = Math.min(380, Math.max(180, safeRight - safeLeft))
  const chatHeight = Math.min(520, Math.max(160, safeBottom - safeTop))
  const chatLeft = clamp(position.x + launcherSize - chatWidth, safeLeft, Math.max(safeLeft, safeRight - chatWidth))
  const chatTop = clamp(position.y - chatHeight - 12, safeTop, Math.max(safeTop, safeBottom - chatHeight))

  function pointerDown(event: PointerEvent<HTMLButtonElement>) {
    if (event.pointerType === "mouse" && event.button !== 0) return
    dragRef.current = { pointerId: event.pointerId, x: event.clientX, y: event.clientY, start: position, moved: false }
    event.currentTarget.setPointerCapture(event.pointerId)
  }
  function pointerMove(event: PointerEvent<HTMLButtonElement>) {
    const drag = dragRef.current
    if (!drag || drag.pointerId !== event.pointerId) return
    const dx = event.clientX - drag.x
    const dy = event.clientY - drag.y
    if (Math.hypot(dx, dy) > 5) drag.moved = true
    if (!drag.moved) return
    wasDraggedRef.current = true
    const mobile = parkingMode && window.matchMedia("(max-width: 900px)").matches
    const search = parkingMode ? document.querySelector(".find-parking-panel:not([hidden])")?.getBoundingClientRect() : null
    const details = parkingMode ? document.querySelector(".find-parking-details-wrap")?.getBoundingClientRect() : null
    const minX = parkingMode && !mobile && search ? search.right + 12 : edgeGap
    const maxX = parkingMode && !mobile && details ? details.left - launcherSize - 12 : window.innerWidth - launcherSize - edgeGap
    const minY = mobile ? search ? search.bottom + 12 : 118 : parkingMode ? 205 : 76
    const maxY = mobile && details ? details.top - launcherSize - 12 : window.innerHeight - launcherSize - (role === "driver" && window.matchMedia("(max-width: 900px)").matches ? parkingMode ? 88 : 96 : edgeGap)
    setPosition({
      x: clamp(drag.start.x + dx, minX, Math.max(minX, maxX)),
      y: clamp(drag.start.y + dy, minY, Math.max(minY, maxY)),
    })
  }
  function pointerUp(event: PointerEvent<HTMLButtonElement>) {
    if (dragRef.current?.pointerId !== event.pointerId) return
    suppressClickRef.current = dragRef.current.moved
    dragRef.current = null
    if (event.currentTarget.hasPointerCapture(event.pointerId)) event.currentTarget.releasePointerCapture(event.pointerId)
    window.setTimeout(() => { suppressClickRef.current = false }, 80)
  }

  useEffect(() => { onOpenChange?.(open) }, [open, onOpenChange])

  function closeChat() {
    setOpen(false)
    requestAnimationFrame(() => launcherRef.current?.focus())
  }

  function findParkingDirectly() {
    if (role !== "driver") return
    // Reuse the dashboard's existing navigation. This opens the normal flow only;
    // no model intent, booking API, availability claim or transaction is executed.
    // Keep an already-open BookingFlow mounted so its selected vehicle/slot/times survive.
    if (!bookingInProgress) window.dispatchEvent(new CustomEvent("sp:dashboard-nav", { detail: "find" }))
    closeChat()
  }

  useEffect(() => {
    if (!open) return
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") closeChat()
    }
    window.addEventListener("keydown", onKeyDown)
    return () => window.removeEventListener("keydown", onKeyDown)
  }, [open])

  useEffect(() => {
    if (open && window.matchMedia("(pointer: fine)").matches) inputRef.current?.focus({ preventScroll: true })
  }, [open])

  useEffect(() => {
    if (open) scrollRef.current?.scrollTo({
      top: scrollRef.current.scrollHeight,
      behavior: window.matchMedia("(prefers-reduced-motion: reduce)").matches ? "auto" : "smooth",
    })
  }, [turns, pending, open])

  useEffect(() => {
    newConversation()
    return () => {
      revisionRef.current += 1
      requestRef.current?.abort()
      requestRef.current = null
    }
  }, [role])

  function newConversation() {
    revisionRef.current += 1
    requestRef.current?.abort()
    requestRef.current = null
    setPending(false)
    setTurns([])
    setInput("")
    if (open) inputRef.current?.focus()
  }

  async function send(question: string, retryId?: number) {
    const text = question.trim()
    if (!assistantEnabled || !text || text.length > 4000 || requestRef.current) return
    const controller = new AbortController()
    requestRef.current = controller
    const revision = revisionRef.current
    const id = retryId ?? ++nextId.current
    if (retryId === undefined) {
      setTurns((current) => [...current, { id, question: text }])
      setInput("")
    } else setTurns((current) => current.map(turn => turn.id === id ? { id, question: text } : turn))
    setPending(true)
    try {
      const result = guest ? await guest.send(text, controller.signal) : await sendDemoMessage(text, controller.signal)
      if (revision === revisionRef.current) {
        setTurns((current) => current.map(turn => turn.id === id ? { ...turn, result } : turn))
      }
    } catch (error) {
      if (revision === revisionRef.current && !controller.signal.aborted) {
        setTurns((current) => current.map(turn => turn.id === id ? {
          ...turn, error: error instanceof Error ? error.message : "I couldn't respond just now. Please try again.",
        } : turn))
      }
    } finally {
      if (revision === revisionRef.current) {
        requestRef.current = null
        setPending(false)
      }
    }
  }

  function submit(event: FormEvent) {
    event.preventDefault()
    void send(input)
  }

  function bookingShortcut() {
    if (guest) return null
    return role === "driver" ? <button type="button" onClick={findParkingDirectly} className="min-h-11 rounded-full bg-[var(--primary)] px-3 text-xs text-[var(--primary-fg)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--primary)]">{bookingInProgress ? "Continue booking" : "Find parking & book directly"}</button> : null
  }

  if (!assistantEnabled) return null

  return (
    <div
      className={`smartparking-chat-shell${open ? " is-open" : ""}${parkingMode ? " is-parking" : ""}`}
      style={{ left: open ? chatLeft : position.x, top: open ? chatTop : position.y,
        width: open ? chatWidth : launcherSize, height: open ? chatHeight : launcherSize }}
    >
        <section
          role="region"
          aria-label="SmartParking AI conversation"
          aria-hidden={!open}
          inert={!open}
          className="smartparking-chat-window"
        >
          <header className="flex shrink-0 items-center gap-2.5 border-b border-[var(--border)] px-4 py-3">
            <span className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-[var(--primary)] text-[var(--primary-fg)]"><UntitledIcon name="sparkles" size={18} /></span>
            <div className="min-w-0 flex-1"><h2 className="truncate text-sm font-semibold">SmartParking AI</h2><p className="text-[11px] text-[var(--muted)]">How can I help?</p></div>
            <button type="button" onClick={newConversation} title="New conversation" aria-label="New conversation" className="flex min-h-11 items-center gap-1 rounded-full px-3 text-xs font-medium text-[var(--muted)] transition-colors hover:bg-[var(--card)] hover:text-[var(--fg)] focus-visible:outline-2 focus-visible:outline-[var(--primary)]"><UntitledIcon name="plus" size={15} /><span>New</span></button>
            <button type="button" onClick={closeChat} title="Close" aria-label="Close conversation" className="grid size-11 shrink-0 place-items-center rounded-full text-[var(--muted)] transition-colors hover:bg-[var(--card)] hover:text-[var(--fg)] focus-visible:outline-2 focus-visible:outline-[var(--primary)]"><UntitledIcon name="x" size={17} /></button>
          </header>

          <div ref={scrollRef} className="min-h-0 flex-1 space-y-4 overflow-y-auto px-4 py-4" aria-live="polite" aria-relevant="additions text">
            <div className="flex items-start gap-2.5"><span className="flex size-7 shrink-0 items-center justify-center rounded-lg bg-[var(--card)] text-[var(--primary)]"><UntitledIcon name="sparkles" size={15} /></span><p className="max-w-[86%] rounded-2xl rounded-tl-sm bg-[var(--card)] px-3.5 py-2.5 text-sm leading-relaxed">{welcome}</p></div>
            <p id="smartparking-ai-notice" className="text-xs leading-relaxed text-[var(--muted)]">
              {assistantEnabled
                ? guest ? guest.onSignIn ? "Ask about SmartParking. Sign in to see your vehicles or reservations."
                  : "Ask about SmartParking. Personal vehicles and reservations are available in the Driver dashboard."
                  : "FAQ preview. No live account or booking data. Each question is handled separately. Messages may be sent to an AI provider; don't share personal information."
                : "FAQ preview is unavailable in this environment."}
            </p>
            {turns.length === 0 && assistantEnabled && <div className="pl-9"><p className="mb-2 text-[11px] font-medium text-[var(--muted)]">You can ask</p><div className="flex flex-wrap gap-2">{prompts.map((question) => <button key={question} type="button" disabled={pending} onClick={() => void send(question)} className="rounded-full border border-[var(--border)] bg-[var(--bg)] px-3 py-1.5 text-left text-xs text-[var(--fg)] transition-colors hover:border-[var(--primary)] hover:text-[var(--primary)] focus-visible:outline-2 focus-visible:outline-[var(--primary)]">{question}</button>)}</div></div>}
            {turns.map((turn) => <div key={turn.id} className="space-y-4">
              <div className="smartparking-chat-message flex justify-end"><p className="max-w-[86%] whitespace-pre-wrap break-words rounded-2xl rounded-br-sm bg-[var(--primary)] px-3.5 py-2.5 text-sm leading-relaxed text-[var(--primary-fg)]">{turn.question}</p></div>
              {turn.result && <div className="smartparking-chat-message flex items-start gap-2.5">
                <span className="flex size-7 shrink-0 items-center justify-center rounded-lg bg-[var(--card)] text-[var(--primary)]"><UntitledIcon name="sparkles" size={15} /></span>
                <div className="min-w-0 max-w-[86%] space-y-2 rounded-2xl rounded-tl-sm bg-[var(--card)] px-3.5 py-2.5">
                  <p className="whitespace-pre-wrap break-words text-sm leading-relaxed">{turn.result.reply}</p>
                  {!guest && <p className="break-words text-[11px] text-[var(--muted)]">{turn.result.mode === "knowledge" ? "SmartPark document guidance" : turn.result.mode === "mock" ? "Simulated reply" : "AI reply"}{"provider" in turn.result && turn.result.provider ? ` · ${turn.result.provider}` : ""}{"model" in turn.result && turn.result.model ? ` · ${turn.result.model}` : ""}</p>}
                  {"notice" in turn.result && <p className="break-words text-[11px] text-[var(--muted)]">{turn.result.notice}</p>}
                  {guest?.onSignIn && "loginRequired" in turn.result && turn.result.loginRequired && <button type="button" onClick={() => guest.onSignIn?.(turn.question)} className="min-h-11 underline">Sign in</button>}
                  {turn.result.mode === "knowledge" && bookingShortcut()}
                  {!guest && "sources" in turn.result && turn.result.sources.length > 0 && <details open={turn.result.mode === "knowledge"} className="break-words text-xs leading-relaxed text-[var(--muted)]">
                    <summary className="cursor-pointer">Sources ({turn.result.sources.length})</summary>
                    <ul className="mt-2 space-y-3">{turn.result.sources.map((source, index) => <li key={`${source.articleId}-${source.version}-${index}`}>
                      <strong>{source.title}</strong><p className="text-[11px] text-[var(--muted)]">Version {source.version}</p><p>{source.excerpt}</p>
                    </li>)}</ul>
                  </details>}
                </div>
              </div>}
              {turn.error && <div role="alert" className="space-y-2 pl-9 text-sm">
                <p className="break-words">{turn.error}</p>
                {bookingShortcut()}
                {!guest && <button type="button" disabled={pending} onClick={() => void send(turn.question, turn.id)} className="min-h-11 rounded-full border border-[var(--border)] px-3 text-xs focus-visible:outline-2 focus-visible:outline-[var(--primary)] disabled:opacity-40">Try again</button>}
              </div>}
            </div>)}
            {pending && <div className="flex items-start gap-2.5" role="status" aria-label="SmartParking AI is typing"><span className="flex size-7 shrink-0 items-center justify-center rounded-lg bg-[var(--card)] text-[var(--primary)]"><UntitledIcon name="sparkles" size={15} /></span><span className="flex items-center gap-1 rounded-2xl rounded-tl-sm bg-[var(--card)] px-3.5 py-3"><span className="size-1.5 rounded-full bg-[var(--muted)] motion-safe:animate-pulse" /><span className="size-1.5 rounded-full bg-[var(--muted)] motion-safe:animate-pulse" /><span className="size-1.5 rounded-full bg-[var(--muted)] motion-safe:animate-pulse" /></span></div>}
          </div>

          <form onSubmit={submit} className="flex shrink-0 items-center gap-2 border-t border-[var(--border)] bg-[var(--bg)] p-3">
            <label htmlFor="smartparking-ai-input" className="sr-only">Message SmartParking AI</label>
            <input id="smartparking-ai-input" ref={inputRef} value={input} onChange={(event) => setInput(event.target.value)} maxLength={4000} disabled={!assistantEnabled} aria-describedby="smartparking-ai-notice" placeholder="Ask a question…" className="min-w-0 flex-1 rounded-xl border border-[var(--border)] bg-[var(--card)] px-3 py-2.5 text-sm text-[var(--fg)] outline-none placeholder:text-[var(--muted)] focus:border-[var(--primary)]" />
            <button type="submit" disabled={!assistantEnabled || !input.trim() || pending} aria-label="Send message" className="flex size-11 shrink-0 items-center justify-center rounded-full bg-[var(--primary)] text-[var(--primary-fg)] transition-opacity hover:opacity-90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--primary)] disabled:cursor-not-allowed disabled:opacity-40"><UntitledIcon name="arrow-right" size={18} /></button>
          </form>
        </section>
      <button
        ref={launcherRef}
        type="button"
        onClick={() => { if (suppressClickRef.current) { suppressClickRef.current = false; return } open ? closeChat() : setOpen(true) }}
        onPointerDown={pointerDown}
        onPointerMove={pointerMove}
        onPointerUp={pointerUp}
        onPointerCancel={pointerUp}
        aria-label={open ? "Collapse SmartParking AI" : "Open SmartParking AI"}
        aria-expanded={open}
        title="SmartParking AI"
        className="smartparking-chat-launcher"
        style={{ touchAction: "none" }}
      >
        <UntitledIcon name="sparkles" size={23} />
      </button>
    </div>
  )
}
