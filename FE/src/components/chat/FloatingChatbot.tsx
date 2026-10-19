import { useEffect, useRef, useState, type FormEvent } from "react"
import type { Role } from "../../lib/types"
import { getMockChatReply, suggestedQuestions } from "../../lib/chatbotMockService"
import { UntitledIcon } from "../icon/UntitledIcon"

type Message = { id: number; sender: "user" | "assistant"; text: string }

const welcome = "Hello! I'm SmartParking AI. How can I help you today?"

export function FloatingChatbot({ role }: { role: Role }) {
  const [open, setOpen] = useState(false)
  const [messages, setMessages] = useState<Message[]>([])
  const [input, setInput] = useState("")
  const [pending, setPending] = useState(false)
  const busyRef = useRef(false)
  const revisionRef = useRef(0)
  const nextId = useRef(0)
  const scrollRef = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    if (open) inputRef.current?.focus()
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
    <>
      {open && (
        <section
          role="region"
          aria-label="SmartParking AI conversation"
          className="fixed bottom-[92px] right-4 z-40 flex flex-col overflow-hidden rounded-2xl border border-[var(--border)] bg-[var(--bg)] text-[var(--fg)] shadow-[0_16px_50px_rgba(15,23,42,0.18)] dark:shadow-[0_16px_50px_rgba(0,0,0,0.45)] sm:right-6"
          style={{ width: "min(380px, calc(100vw - 32px))", height: "min(520px, calc(100dvh - 168px))" }}
        >
          <header className="flex shrink-0 items-center gap-2.5 border-b border-[var(--border)] px-4 py-3">
            <span className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-[var(--primary)] text-[var(--primary-fg)]"><UntitledIcon name="sparkles" size={18} /></span>
            <div className="min-w-0 flex-1"><h2 className="truncate text-sm font-semibold">SmartParking AI</h2><p className="text-[11px] text-[var(--muted)]">How can I help?</p></div>
            <button type="button" onClick={newConversation} title="New conversation" aria-label="New conversation" className="flex items-center gap-1 rounded-lg px-2 py-1.5 text-xs font-medium text-[var(--muted)] transition-colors hover:bg-[var(--card)] hover:text-[var(--fg)] focus-visible:outline-2 focus-visible:outline-[var(--primary)]"><UntitledIcon name="plus" size={15} /><span>New</span></button>
            <button type="button" onClick={() => setOpen(false)} title="Minimize" aria-label="Minimize conversation" className="rounded-lg p-1.5 text-[var(--muted)] transition-colors hover:bg-[var(--card)] hover:text-[var(--fg)] focus-visible:outline-2 focus-visible:outline-[var(--primary)]"><UntitledIcon name="chevron-down" size={17} /></button>
            <button type="button" onClick={() => setOpen(false)} title="Close" aria-label="Close conversation" className="rounded-lg p-1.5 text-[var(--muted)] transition-colors hover:bg-[var(--card)] hover:text-[var(--fg)] focus-visible:outline-2 focus-visible:outline-[var(--primary)]"><UntitledIcon name="x" size={17} /></button>
          </header>

          <div ref={scrollRef} className="min-h-0 flex-1 space-y-4 overflow-y-auto px-4 py-4" aria-live="polite" aria-relevant="additions text">
            <div className="flex items-start gap-2.5"><span className="flex size-7 shrink-0 items-center justify-center rounded-lg bg-[var(--card)] text-[var(--primary)]"><UntitledIcon name="sparkles" size={15} /></span><p className="max-w-[86%] rounded-2xl rounded-tl-sm bg-[var(--card)] px-3.5 py-2.5 text-sm leading-relaxed">{welcome}</p></div>
            {messages.length === 0 && <div className="pl-9"><p className="mb-2 text-[11px] font-medium text-[var(--muted)]">You can ask</p><div className="flex flex-wrap gap-2">{suggestedQuestions(role).map((question) => <button key={question} type="button" disabled={pending} onClick={() => void send(question)} className="rounded-full border border-[var(--border)] bg-[var(--bg)] px-3 py-1.5 text-left text-xs text-[var(--fg)] transition-colors hover:border-[var(--primary)] hover:text-[var(--primary)] focus-visible:outline-2 focus-visible:outline-[var(--primary)]">{question}</button>)}</div></div>}
            {messages.map((message) => message.sender === "user" ? <div key={message.id} className="flex justify-end"><p className="max-w-[86%] whitespace-pre-wrap break-words rounded-2xl rounded-br-sm bg-[var(--primary)] px-3.5 py-2.5 text-sm leading-relaxed text-[var(--primary-fg)]">{message.text}</p></div> : <div key={message.id} className="flex items-start gap-2.5"><span className="flex size-7 shrink-0 items-center justify-center rounded-lg bg-[var(--card)] text-[var(--primary)]"><UntitledIcon name="sparkles" size={15} /></span><p className="max-w-[86%] whitespace-pre-wrap break-words rounded-2xl rounded-tl-sm bg-[var(--card)] px-3.5 py-2.5 text-sm leading-relaxed">{message.text}</p></div>)}
            {pending && <div className="flex items-start gap-2.5" role="status" aria-label="SmartParking AI is typing"><span className="flex size-7 shrink-0 items-center justify-center rounded-lg bg-[var(--card)] text-[var(--primary)]"><UntitledIcon name="sparkles" size={15} /></span><span className="flex items-center gap-1 rounded-2xl rounded-tl-sm bg-[var(--card)] px-3.5 py-3"><span className="size-1.5 rounded-full bg-[var(--muted)] motion-safe:animate-pulse" /><span className="size-1.5 rounded-full bg-[var(--muted)] motion-safe:animate-pulse" /><span className="size-1.5 rounded-full bg-[var(--muted)] motion-safe:animate-pulse" /></span></div>}
          </div>

          <form onSubmit={submit} className="flex shrink-0 items-center gap-2 border-t border-[var(--border)] bg-[var(--bg)] p-3">
            <label htmlFor="smartparking-ai-input" className="sr-only">Message SmartParking AI</label>
            <input id="smartparking-ai-input" ref={inputRef} value={input} onChange={(event) => setInput(event.target.value)} maxLength={500} placeholder="Ask a question…" className="min-w-0 flex-1 rounded-xl border border-[var(--border)] bg-[var(--card)] px-3 py-2.5 text-sm text-[var(--fg)] outline-none placeholder:text-[var(--muted)] focus:border-[var(--primary)]" />
            <button type="submit" disabled={!input.trim() || pending} aria-label="Send message" className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-[var(--primary)] text-[var(--primary-fg)] transition-opacity hover:opacity-90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--primary)] disabled:cursor-not-allowed disabled:opacity-40"><UntitledIcon name="arrow-right" size={18} /></button>
          </form>
        </section>
      )}
      <button
        type="button"
        onClick={() => setOpen((current) => !current)}
        aria-label={open ? "Collapse SmartParking AI" : "Open SmartParking AI"}
        aria-expanded={open}
        title="SmartParking AI"
        className="fixed bottom-6 right-4 z-40 flex size-14 items-center justify-center rounded-full bg-[var(--primary)] text-[var(--primary-fg)] shadow-[0_8px_24px_rgba(15,23,42,0.24)] transition-[transform,box-shadow] duration-200 hover:-translate-y-0.5 hover:shadow-[0_12px_28px_rgba(15,23,42,0.28)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--primary)] dark:shadow-[0_8px_24px_rgba(0,0,0,0.48)] sm:right-6"
      >
        <UntitledIcon name="sparkles" size={23} />
      </button>
    </>
  )
}
