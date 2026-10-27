import { useEffect, useRef, useState, type FormEvent, type PointerEvent } from "react"
import type { Role } from "../../lib/types"
import { getMockChatReply, suggestedQuestions } from "../../lib/chatbotMockService"
import { UntitledIcon } from "../icon/UntitledIcon"

type Message = { id: number; sender: "user" | "assistant"; text: string }

const welcome = "Hello! I'm SmartParking AI. How can I help you today?"
const launcherSize = 56
const edgeGap = 16
type Position = { x: number; y: number }
function clamp(value: number, min: number, max: number) { return Math.min(Math.max(value, min), max) }

export function FloatingChatbot({ role, parkingMode = false, parkingSheetOpen = false, parkingDetailsOpen = false, onOpenChange }: {
  role: Role
  parkingMode?: boolean
  parkingSheetOpen?: boolean
  parkingDetailsOpen?: boolean
  onOpenChange?: (open: boolean) => void
}) {
  const [open, setOpen] = useState(false)
  const [messages, setMessages] = useState<Message[]>([])
  const [input, setInput] = useState("")
  const [pending, setPending] = useState(false)
  const busyRef = useRef(false)
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
  }, [messages, pending, open])

  useEffect(() => () => { revisionRef.current += 1 }, [])

  function newConversation() {
    revisionRef.current += 1
    busyRef.current = false
    setPending(false)
    setMessages([])
    setInput("")
    inputRef.current?.focus()
  }

  async function send(question: string) {
    const text = question.trim()
    if (!text || busyRef.current) return
    busyRef.current = true
    const revision = revisionRef.current
    setMessages((current) => [...current, { id: ++nextId.current, sender: "user", text }])
    setInput("")
    setPending(true)
    try {
      const reply = await getMockChatReply(role, text)
      if (revision === revisionRef.current) {
        setMessages((current) => [...current, { id: ++nextId.current, sender: "assistant", text: reply }])
      }
    } catch {
      if (revision === revisionRef.current) {
        setMessages((current) => [...current, { id: ++nextId.current, sender: "assistant", text: "I couldn't respond just now. Please try again." }])
      }
    } finally {
      if (revision === revisionRef.current) {
        busyRef.current = false
        setPending(false)
      }
    }
  }

  function submit(event: FormEvent) {
    event.preventDefault()
    void send(input)
  }

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
            {messages.length === 0 && <div className="pl-9"><p className="mb-2 text-[11px] font-medium text-[var(--muted)]">You can ask</p><div className="flex flex-wrap gap-2">{suggestedQuestions(role).map((question) => <button key={question} type="button" disabled={pending} onClick={() => void send(question)} className="rounded-full border border-[var(--border)] bg-[var(--bg)] px-3 py-1.5 text-left text-xs text-[var(--fg)] transition-colors hover:border-[var(--primary)] hover:text-[var(--primary)] focus-visible:outline-2 focus-visible:outline-[var(--primary)]">{question}</button>)}</div></div>}
            {messages.map((message) => message.sender === "user" ? <div key={message.id} className="smartparking-chat-message flex justify-end"><p className="max-w-[86%] whitespace-pre-wrap break-words rounded-2xl rounded-br-sm bg-[var(--primary)] px-3.5 py-2.5 text-sm leading-relaxed text-[var(--primary-fg)]">{message.text}</p></div> : <div key={message.id} className="smartparking-chat-message flex items-start gap-2.5"><span className="flex size-7 shrink-0 items-center justify-center rounded-lg bg-[var(--card)] text-[var(--primary)]"><UntitledIcon name="sparkles" size={15} /></span><p className="max-w-[86%] whitespace-pre-wrap break-words rounded-2xl rounded-tl-sm bg-[var(--card)] px-3.5 py-2.5 text-sm leading-relaxed">{message.text}</p></div>)}
            {pending && <div className="flex items-start gap-2.5" role="status" aria-label="SmartParking AI is typing"><span className="flex size-7 shrink-0 items-center justify-center rounded-lg bg-[var(--card)] text-[var(--primary)]"><UntitledIcon name="sparkles" size={15} /></span><span className="flex items-center gap-1 rounded-2xl rounded-tl-sm bg-[var(--card)] px-3.5 py-3"><span className="size-1.5 rounded-full bg-[var(--muted)] motion-safe:animate-pulse" /><span className="size-1.5 rounded-full bg-[var(--muted)] motion-safe:animate-pulse" /><span className="size-1.5 rounded-full bg-[var(--muted)] motion-safe:animate-pulse" /></span></div>}
          </div>

          <form onSubmit={submit} className="flex shrink-0 items-center gap-2 border-t border-[var(--border)] bg-[var(--bg)] p-3">
            <label htmlFor="smartparking-ai-input" className="sr-only">Message SmartParking AI</label>
            <input id="smartparking-ai-input" ref={inputRef} value={input} onChange={(event) => setInput(event.target.value)} maxLength={500} placeholder="Ask a question…" className="min-w-0 flex-1 rounded-xl border border-[var(--border)] bg-[var(--card)] px-3 py-2.5 text-sm text-[var(--fg)] outline-none placeholder:text-[var(--muted)] focus:border-[var(--primary)]" />
            <button type="submit" disabled={!input.trim() || pending} aria-label="Send message" className="flex size-11 shrink-0 items-center justify-center rounded-full bg-[var(--primary)] text-[var(--primary-fg)] transition-opacity hover:opacity-90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--primary)] disabled:cursor-not-allowed disabled:opacity-40"><UntitledIcon name="arrow-right" size={18} /></button>
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
