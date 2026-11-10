import { useEffect, useId, useLayoutEffect, useRef, useState, useSyncExternalStore } from "react"
import { assistantApi, assistantConfigured, type ChatMessage, type Conversation } from "../../lib/assistantApi"
import { AuthApiError, getAuthSessionGeneration, subscribeAuthSession } from "../../lib/authApi"
import { UntitledIcon } from "../icon/UntitledIcon"

const suggestions = ["SmartPark là gì?", "Cách tìm bãi?", "Cách đặt chỗ?"]

type PendingRequest = {
  conversationId: string
  requestId: string
  original?: { clientMessageId: string; expectedRevision: number; message: string }
}

function record(value: unknown): Record<string, unknown> {
  return value && typeof value === "object" ? value as Record<string, unknown> : {}
}
function label(value: unknown) { return typeof value === "string" ? value : "" }
function date(value: unknown) {
  const time = Date.parse(label(value))
  return Number.isFinite(time) ? new Date(time).toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" }) : "Chưa xác định"
}
function MessageCards({ message, detail, disabled }: { message: ChatMessage; detail: (id: string) => void; disabled: boolean }) {
  const allowed = new Set(message.actions.map(record).filter(action => action.type === "view_reservation")
    .map(action => label(action.reservationId)).filter(id => /^[a-f0-9]{8}(?:-[a-f0-9]{4}){3}-[a-f0-9]{12}$/i.test(id)))
  return message.cards.slice(0, 20).map((value, index) => {
    const card = record(value)
    const parking = record(card.parking)
    const reservation = record(card.reservation)
    const availability = record(parking.availability)
    const price = record(parking.price)
    if (card.type === "vehicles" && Array.isArray(card.items)) return <div key={index} className="border-t border-[var(--border)] pt-2">
      {card.items.slice(0, 20).map((vehicle, row) => <p key={row}>{label(record(vehicle).displayLabel)} · {label(record(vehicle).vehicleType)}</p>)}
      {card.truncated === true && <p>Danh sách đã được rút gọn.</p>}
    </div>
    if (card.type === "reservation" && label(reservation.id)) return <div key={index} className="border-t border-[var(--border)] pt-2">
      <strong>{label(record(reservation.vehicle).displayLabel)}</strong>
      <p>{date(reservation.startAt)} – {date(reservation.endAt)}</p>
      <p>{label(reservation.status)} · Dữ liệu lúc {date(reservation.asOf)}</p>
      {allowed.has(label(reservation.id)) && <button type="button" disabled={disabled} className="min-h-11 underline disabled:opacity-50" onClick={() => detail(label(reservation.id))}>Xem chi tiết mới nhất</button>}
    </div>
    if (card.type === "parking" && label(parking.name)) return <div key={index} className="border-t border-[var(--border)] pt-2">
      <strong>{label(parking.name)}</strong><p>{label(parking.address)}</p>
      <p>Chỗ trống: {typeof availability.availableCount === "number" && Number.isSafeInteger(availability.availableCount) && availability.availableCount >= 0 ? availability.availableCount : "Chưa xác định"}</p>
      <p>Giá: {typeof price.amountVnd === "number" && Number.isFinite(price.amountVnd) ? `${price.amountVnd.toLocaleString("vi-VN")} ₫ · ${label(price.billingUnit)}` : "Chưa xác định"}</p>
      <p>Dữ liệu lúc {date(card.asOf)}</p>
    </div>
    return null
  })
}

export function DriverChatbot(props: { pendingQuestion?: string; onUseQuestion?: () => void }) {
  const generation = useSyncExternalStore(subscribeAuthSession, getAuthSessionGeneration)
  if (!assistantConfigured) return null
  return <DriverChatSession key={generation} generation={generation} {...props} />
}

function DriverChatSession({ generation, pendingQuestion, onUseQuestion }: {
  generation: number; pendingQuestion?: string; onUseQuestion?: () => void
}) {
  const [open, setOpen] = useState(false)
  const [enabled, setEnabled] = useState(false)
  const [hasSavedChat, setHasSavedChat] = useState(false)
  const [busy, setBusy] = useState(false)
  const [notice, setNotice] = useState("")
  const [input, setInput] = useState("")
  const [conversation, setConversation] = useState<Conversation | null>(null)
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [historyCursor, setHistoryCursor] = useState<string | null>(null)
  const [pending, setPending] = useState<PendingRequest | null>(null)
  const [canResend, setCanResend] = useState(false)
  const active = useRef<AbortController | null>(null)
  const creation = useRef<{ id: string; locale: "vi" | "en" } | null>(null)
  const resetDialog = useRef<HTMLDialogElement>(null)
  const resetTitle = useId()
  const scroll = useRef<HTMLDivElement>(null)
  const scrollHeightBeforePrepend = useRef<number | null>(null)
  useLayoutEffect(() => {
    if (!open || !scroll.current) return
    const area = scroll.current
    const before = scrollHeightBeforePrepend.current
    area.scrollTop = before === null ? area.scrollHeight : area.scrollTop + area.scrollHeight - before
    scrollHeightBeforePrepend.current = null
  }, [messages, open])
  useEffect(() => () => active.current?.abort(), [])
  useEffect(() => {
    const controller = new AbortController()
    let checking = false
    async function refresh() {
      if (document.hidden || checking || controller.signal.aborted) return
      checking = true
      let available = false
      try {
        try {
          available = (await assistantApi.capabilities(controller.signal)).enabled === true
        } catch { /* History can still be available when generation readiness fails. */ }
        if (controller.signal.aborted) return
        setEnabled(available)
        if (!available) {
          const saved = await assistantApi.conversations(null, controller.signal)
          if (!controller.signal.aborted) setHasSavedChat(saved.items.length > 0)
        }
      } catch {
        // Keep the last known history state during a temporary failure.
      } finally { checking = false }
    }
    void refresh()
    const interval = window.setInterval(refresh, 30_000)
    window.addEventListener("focus", refresh)
    document.addEventListener("visibilitychange", refresh)
    return () => {
      controller.abort()
      window.clearInterval(interval)
      window.removeEventListener("focus", refresh)
      document.removeEventListener("visibilitychange", refresh)
    }
  }, [open])

  async function run(work: (signal: AbortSignal) => Promise<void>,
    failureNotice = "Không tải được dữ liệu trợ lý. Nếu vừa gửi câu hỏi, hãy kiểm tra kết quả trước khi gửi câu khác.") {
    if (active.current || generation !== getAuthSessionGeneration()) return
    const controller = new AbortController()
    active.current = controller
    setBusy(true)
    setNotice("")
    try { await work(controller.signal) }
    catch {
      if (!controller.signal.aborted && generation === getAuthSessionGeneration())
        setNotice(failureNotice)
    } finally {
      if (active.current === controller) active.current = null
      if (!controller.signal.aborted) setBusy(false)
    }
  }
  async function loadHistory(id: string, signal: AbortSignal, cursor?: string) {
    const history = await assistantApi.history(id, cursor, signal)
    if (signal.aborted || generation !== getAuthSessionGeneration()) return
    setHasSavedChat(true)
    scrollHeightBeforePrepend.current = cursor ? scroll.current?.scrollHeight ?? null : null
    setMessages(previous => cursor ? [...history.items, ...previous] : history.items)
    setHistoryCursor(history.nextCursor)
    // Older pages use a fixed message snapshot even when their metadata is newer.
    if (cursor) return
    setConversation(history.conversation)
    setPending(history.activeRequestClientMessageId
      ? { conversationId: id, requestId: history.activeRequestClientMessageId } : null)
    setCanResend(false)
  }
  function loadOlderMessages() {
    if (!conversation || !historyCursor || busy || pending) return
    void run(signal => loadHistory(conversation.id, signal, historyCursor))
  }
  function openHistory() {
    setEnabled(false)
    setOpen(true)
    void run(async signal => {
      const page = await assistantApi.conversations(null, signal)
      if (signal.aborted || generation !== getAuthSessionGeneration()) return
      const current = page.items[0]
      if (!current) resetConversation()
      else if (!pending || pending.conversationId !== current.id) await loadHistory(current.id, signal)
    })
  }
  function send(text: string) {
    if (!enabled || pending || !text.trim()) return
    void run(async signal => {
      const attempt = conversation ? null : (creation.current ??= { id: crypto.randomUUID(), locale: "vi" })
      let current = conversation
      if (attempt) {
        try { current = await assistantApi.create(attempt.id, attempt.locale, signal) }
        catch (error) {
          if (!(error instanceof AuthApiError) || !["CONVERSATION_EXISTS", "RESOURCE_NOT_FOUND"].includes(error.code ?? "")) throw error
          const page = await assistantApi.conversations(null, signal)
          if (signal.aborted || generation !== getAuthSessionGeneration()) return
          resetConversation()
          if (page.items[0]) await loadHistory(page.items[0].id, signal)
          if (!signal.aborted) {
            setInput(text)
            setNotice("Đoạn chat đã thay đổi. Kiểm tra đoạn chat hiện tại trước khi gửi lại.")
          }
          return
        }
      }
      if (signal.aborted || generation !== getAuthSessionGeneration()) return
      if (!current) return
      setConversation(current)
      setHasSavedChat(true)
      creation.current = null
      const requestId = crypto.randomUUID()
      const original = { clientMessageId: requestId, expectedRevision: current.revision, message: text }
      setPending({ conversationId: current.id, requestId, original })
      setCanResend(false)
      setInput("")
      const result = await dispatch(current.id, original, signal)
      if (!result) return
      if (signal.aborted || generation !== getAuthSessionGeneration()) return
      if (result.state !== "running") {
        await loadHistory(current.id, signal)
        if (result.state !== "completed") setNotice("Yêu cầu đã kết thúc mà không có câu trả lời.")
      }
    })
  }
  async function dispatch(id: string, original: NonNullable<PendingRequest["original"]>, signal: AbortSignal) {
    try { return await assistantApi.send(id, original, signal) }
    catch (error) {
      if (!(error instanceof AuthApiError) || error.status !== 409 ||
          !["REVISION_CONFLICT", "CONVERSATION_BUSY"].includes(error.code ?? "")) throw error
      if (signal.aborted || generation !== getAuthSessionGeneration()) return
      setPending(null)
      setCanResend(false)
      setInput(original.message)
      await loadHistory(id, signal)
      if (!signal.aborted) setNotice("Hội thoại vừa thay đổi. Kiểm tra lịch sử trước khi gửi lại.")
    }
  }
  function recover(action: "check" | "cancel" | "resend" = "check") {
    if (!pending) return
    void run(async signal => {
      let result
      try {
        result = action === "resend" && pending.original
          ? await dispatch(pending.conversationId, pending.original, signal)
          : await (action === "cancel" ? assistantApi.cancel : assistantApi.result)(pending.conversationId, pending.requestId, signal)
      } catch (error) {
        if (!(error instanceof AuthApiError) || error.status !== 404 || action !== "check") throw error
        if (signal.aborted || generation !== getAuthSessionGeneration()) return
        try { await assistantApi.history(pending.conversationId, null, signal) }
        catch (historyError) {
          if (!(historyError instanceof AuthApiError) || historyError.status !== 404) throw historyError
          if (signal.aborted || generation !== getAuthSessionGeneration()) return
          resetConversation()
          setPending(null)
          setCanResend(false)
          setInput(pending.original?.message ?? "")
          setNotice("Hội thoại không còn khả dụng. Bạn có thể mở hội thoại khác.")
          return
        }
        if (signal.aborted || generation !== getAuthSessionGeneration()) return
        if (!pending.original) {
          await loadHistory(pending.conversationId, signal)
          return
        }
        setCanResend(true)
        setNotice("Chưa tìm thấy yêu cầu. Bạn có thể tra lại hoặc gửi lại với cùng mã yêu cầu.")
        return
      }
      if (!result || signal.aborted || generation !== getAuthSessionGeneration()) return
      setCanResend(false)
      if (result.state === "running") setNotice("Yêu cầu vẫn đang xử lý.")
      else {
        await loadHistory(pending.conversationId, signal)
        if (result.state !== "completed") setNotice("Yêu cầu đã kết thúc mà không có câu trả lời.")
      }
    })
  }
  function resetConversation() {
    setConversation(null)
    setHasSavedChat(false)
    setMessages([])
    setHistoryCursor(null)
    setPending(null)
    setCanResend(false)
    creation.current = null
  }
  function confirmReset() {
    if (!conversation) return
    const id = conversation.id
    void run(async signal => {
      try { await assistantApi.reset(id, signal) }
      catch (error) {
        if (!(error instanceof AuthApiError) || error.status !== 404 || error.code !== "RESOURCE_NOT_FOUND") throw error
        const page = await assistantApi.conversations(null, signal)
        if (signal.aborted || generation !== getAuthSessionGeneration()) return
        const current = page.items[0]
        if (current?.id === id) throw error
        if (current) {
          await loadHistory(current.id, signal)
          if (!signal.aborted) setNotice("Đoạn chat đã thay đổi ở phiên khác.")
          return
        }
      }
      if (signal.aborted || generation !== getAuthSessionGeneration()) return
      resetConversation()
      setInput("")
    }, "Chưa xác nhận được việc tạo chat mới. Bạn có thể thử lại hoặc mở lại khung chat để kiểm tra.")
  }

  if (!open && !enabled && !hasSavedChat) return null
  return <div className={`smartparking-chat-shell bottom-[calc(6rem+env(safe-area-inset-bottom))] right-4 sm:bottom-[calc(1rem+env(safe-area-inset-bottom))] ${open
    ? "is-open h-[min(520px,calc(100dvh-11rem-env(safe-area-inset-bottom)))] w-[min(380px,calc(100vw-2rem))] sm:h-[min(520px,calc(100dvh-6rem-env(safe-area-inset-bottom)))]"
    : "size-14"}`}>
    {!open ? <button type="button" className="smartparking-chat-launcher" aria-label="Trợ lý · Lịch sử" aria-expanded={false} title="SmartParking AI" onClick={openHistory}>
      <UntitledIcon name="sparkles" size={23} />
    </button>
      : <section aria-label="Trợ lý SmartParking" className="smartparking-chat-window">
        <header className="flex shrink-0 items-center gap-2.5 border-b border-[var(--border)] px-4 py-3">
          <span className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-[var(--primary)] text-[var(--primary-fg)]"><UntitledIcon name="sparkles" size={18} /></span>
          <div className="min-w-0 flex-1">
            <h2 className="truncate text-sm font-semibold">SmartParking AI</h2>
            <p className="text-[11px] text-[var(--muted)]">Mình có thể giúp gì?</p>
          </div>
          <button type="button" disabled={busy || !conversation} onClick={() => {
            if (!resetDialog.current) return
            resetDialog.current.returnValue = ""
            resetDialog.current.showModal()
          }}
            className="flex min-h-11 items-center gap-1 rounded-full px-3 text-xs font-medium text-[var(--muted)] hover:bg-[var(--card)] focus-visible:outline-2 focus-visible:outline-[var(--primary)] disabled:opacity-40">
            <UntitledIcon name="plus" size={15} /> Mới
          </button>
          <button type="button" aria-label="Đóng" onClick={() => setOpen(false)}
            className="grid size-11 shrink-0 place-items-center rounded-full text-[var(--muted)] hover:bg-[var(--card)] focus-visible:outline-2 focus-visible:outline-[var(--primary)]">
            <UntitledIcon name="x" size={17} />
          </button>
        </header>
        <dialog ref={resetDialog} aria-labelledby={resetTitle} aria-describedby={`${resetTitle}-description`} className="fixed inset-0 m-auto w-[min(24rem,calc(100vw-2rem))] rounded-2xl border border-[var(--border)] bg-[var(--bg)] p-5 text-[var(--fg)] backdrop:bg-black/45"
          onClose={event => { if (event.currentTarget.returnValue === "reset") confirmReset() }}>
          <h3 id={resetTitle} className="text-base font-semibold">Tạo đoạn chat mới?</h3>
          <p id={`${resetTitle}-description`} className="mt-2 text-sm leading-relaxed">Đoạn chat hiện tại sẽ biến mất khỏi lịch sử ngay và đến hạn xóa vĩnh viễn sau 7 ngày, trừ khi có yêu cầu lưu giữ đã được phê duyệt.</p>
          <form method="dialog" className="mt-5 flex flex-wrap justify-end gap-2">
            <button type="submit" value="keep" autoFocus className="btn-outline min-h-11">Giữ đoạn chat</button>
            <button type="submit" value="reset" className="btn-primary min-h-11">Tạo chat mới</button>
          </form>
        </dialog>
        <div ref={scroll} className="min-h-0 flex-1 space-y-4 overflow-y-auto px-4 py-4 text-sm" aria-live="polite" aria-relevant="additions text"
          onScroll={event => {
            const area = event.currentTarget
            if (area.scrollTop <= 16 && area.scrollHeight > area.clientHeight) loadOlderMessages()
          }}>
          {!enabled && <p>Hiện chỉ có thể xem lịch sử. Gửi câu hỏi chưa khả dụng.</p>}
          {enabled && !busy && !pending && !messages.length && <div className="space-y-4">
            <div className="flex items-start gap-2.5">
              <span className="flex size-7 shrink-0 items-center justify-center rounded-lg bg-[var(--card)] text-[var(--primary)]"><UntitledIcon name="sparkles" size={15} /></span>
              <p className="max-w-[86%] rounded-2xl rounded-tl-sm bg-[var(--card)] px-3.5 py-2.5 leading-relaxed">Xin chào! Mình có thể giải đáp các câu hỏi về SmartParking.</p>
            </div>
            <div className="space-y-2 pl-9">
              <p className="text-xs font-medium text-[var(--muted)]">Bạn có thể hỏi</p>
              <div className="flex flex-wrap gap-2">{suggestions.map(question => (
                <button key={question} type="button" onClick={() => send(question)} className="min-h-11 rounded-full border border-[var(--border)] px-3 text-left text-xs hover:border-[var(--primary)] focus-visible:outline-2 focus-visible:outline-[var(--primary)]">{question}</button>
              ))}</div>
            </div>
          </div>}
          {pendingQuestion && <div className="space-y-1 border-b border-[var(--border)] pb-2">
            <p className="break-words">Câu hỏi trước khi đăng nhập: {pendingQuestion}</p>
            <button type="button" disabled={!enabled || busy || Boolean(pending)} className="min-h-11 underline" onClick={() => {
              setInput(pendingQuestion)
              onUseQuestion?.()
            }}>Đưa vào ô soạn</button>
          </div>}
          {historyCursor && conversation && <button type="button" disabled={busy || Boolean(pending)} className="min-h-11 underline"
            onClick={loadOlderMessages}>Tin nhắn cũ hơn</button>}
          {messages.map(message => message.role.toLowerCase() === "user"
            ? <article key={message.id} aria-label="Bạn" className="smartparking-chat-message flex justify-end">
              <p className="max-w-[86%] whitespace-pre-wrap break-words rounded-2xl rounded-br-sm bg-[var(--primary)] px-3.5 py-2.5 leading-relaxed text-[var(--primary-fg)]">{message.content}</p>
            </article>
            : <article key={message.id} aria-label="Trợ lý" className="smartparking-chat-message flex items-start gap-2.5">
              <span className="flex size-7 shrink-0 items-center justify-center rounded-lg bg-[var(--card)] text-[var(--primary)]"><UntitledIcon name="sparkles" size={15} /></span>
              <div className="min-w-0 max-w-[86%] space-y-2 break-words rounded-2xl rounded-tl-sm bg-[var(--card)] px-3.5 py-2.5 leading-relaxed">
                <p className="whitespace-pre-wrap break-words">{message.content}</p>
                <MessageCards message={message} disabled={!enabled || busy || Boolean(pending)} detail={id => send(`reservation details ${id}`)} />
                {message.sources.map(record).filter(source => source.status !== "withdrawn" && typeof source.excerpt === "string").map((source, index) =>
                  <details key={index}><summary>{label(source.title)}</summary><p className="whitespace-pre-wrap break-words">{label(source.excerpt)}</p></details>)}
              </div>
            </article>)}
          {busy && <p role="status">Đang tải…</p>}
          {notice && <p role="alert">{notice}</p>}
          {pending && <div className="flex flex-wrap gap-3">
            <button type="button" className="min-h-11 underline" disabled={busy} onClick={() => recover()}>Kiểm tra kết quả</button>
            <button type="button" className="min-h-11 underline" disabled={busy} onClick={() => recover("cancel")}>Hủy yêu cầu</button>
            {canResend && enabled && <button type="button" className="min-h-11 underline" disabled={busy}
              onClick={() => recover("resend")}>Gửi lại cùng mã</button>}
          </div>}
        </div>
        <form className="flex shrink-0 items-center gap-2 border-t border-[var(--border)] bg-[var(--bg)] p-3" onSubmit={event => { event.preventDefault(); send(input) }}>
          <input aria-label="Câu hỏi" placeholder="Nhập câu hỏi…" className="min-h-11 min-w-0 flex-1 rounded-xl border border-[var(--border)] bg-[var(--card)] px-3 text-sm placeholder:text-[var(--muted)] focus-visible:outline-2 focus-visible:outline-[var(--primary)]" maxLength={4000}
            disabled={!enabled || Boolean(pending)} value={input} onChange={event => setInput(event.target.value)} />
          <button type="submit" aria-label="Gửi" className="grid size-11 shrink-0 place-items-center rounded-full bg-[var(--primary)] text-[var(--primary-fg)] hover:opacity-90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--primary)] disabled:opacity-40" disabled={!enabled || busy || Boolean(pending) || !input.trim()}>
            <UntitledIcon name="arrow-right" size={18} />
          </button>
        </form>
      </section>}
  </div>
}
