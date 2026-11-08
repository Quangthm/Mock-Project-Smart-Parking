import { useEffect, useRef, useState, useSyncExternalStore } from "react"
import { assistantApi, assistantConfigured, type ChatMessage, type Conversation } from "../../lib/assistantApi"
import { AuthApiError, getAuthSessionGeneration, subscribeAuthSession } from "../../lib/authApi"

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
  const [busy, setBusy] = useState(false)
  const [notice, setNotice] = useState("")
  const [input, setInput] = useState("")
  const [conversations, setConversations] = useState<Conversation[]>([])
  const [listCursor, setListCursor] = useState<string | null>(null)
  const [conversation, setConversation] = useState<Conversation | null>(null)
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [historyCursor, setHistoryCursor] = useState<string | null>(null)
  const [pending, setPending] = useState<PendingRequest | null>(null)
  const [canResend, setCanResend] = useState(false)
  const active = useRef<AbortController | null>(null)
  const creation = useRef<{ id: string; locale: "vi" | "en" } | null>(null)
  useEffect(() => () => active.current?.abort(), [])
  useEffect(() => {
    const controller = new AbortController()
    let checking = false
    async function refresh() {
      if (document.hidden || checking || controller.signal.aborted) return
      checking = true
      try {
        const result = await assistantApi.capabilities(controller.signal)
        if (!controller.signal.aborted) setEnabled(result.enabled === true)
      } catch {
        if (!controller.signal.aborted) setEnabled(false)
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

  async function run(work: (signal: AbortSignal) => Promise<void>) {
    if (active.current || generation !== getAuthSessionGeneration()) return
    const controller = new AbortController()
    active.current = controller
    setBusy(true)
    setNotice("")
    try { await work(controller.signal) }
    catch {
      if (!controller.signal.aborted && generation === getAuthSessionGeneration())
        setNotice("Không tải được dữ liệu trợ lý. Nếu vừa gửi câu hỏi, hãy kiểm tra kết quả trước khi gửi câu khác.")
    } finally {
      if (active.current === controller) active.current = null
      if (!controller.signal.aborted) setBusy(false)
    }
  }
  async function loadHistory(id: string, signal: AbortSignal, cursor?: string) {
    const history = await assistantApi.history(id, cursor, signal)
    if (signal.aborted || generation !== getAuthSessionGeneration()) return
    setConversation(history.conversation)
    setMessages(previous => cursor ? [...history.items, ...previous] : history.items)
    setHistoryCursor(history.nextCursor)
    setPending(history.activeRequestClientMessageId
      ? { conversationId: id, requestId: history.activeRequestClientMessageId } : null)
    setCanResend(false)
  }
  function openHistory() {
    setEnabled(false)
    setOpen(true)
    void run(async signal => {
      const page = await assistantApi.conversations(null, signal)
      if (signal.aborted || generation !== getAuthSessionGeneration()) return
      setConversations(page.items)
      setListCursor(page.nextCursor)
      if (conversation) {
        if (!pending) await loadHistory(conversation.id, signal)
      } else if (page.items[0]) await loadHistory(page.items[0].id, signal)
    })
  }
  function send(text: string) {
    if (!enabled || pending || !text.trim()) return
    void run(async signal => {
      const attempt = conversation ? null : (creation.current ??= { id: crypto.randomUUID(), locale: "vi" })
      const current = attempt ? await assistantApi.create(attempt.id, attempt.locale, signal) : conversation!
      if (signal.aborted || generation !== getAuthSessionGeneration()) return
      setConversation(current)
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
    setMessages([])
    setHistoryCursor(null)
    creation.current = null
  }

  if (!open && !enabled) return null
  return <div className="fixed bottom-24 right-4 z-40 sm:bottom-6">
    {!open ? <button type="button" className="btn-outline min-h-11 bg-[var(--bg)]" onClick={openHistory}>Trợ lý · Lịch sử</button>
      : <section aria-label="Trợ lý SmartParking" className="flex max-h-[min(36rem,calc(100dvh-8rem))] w-[min(24rem,calc(100vw-2rem))] flex-col rounded-xl border border-[var(--border)] bg-[var(--bg)] text-sm text-[var(--fg)] shadow-lg">
        <header className="flex items-center justify-between border-b border-[var(--border)] px-3">
          <h2 className="font-semibold">Trợ lý SmartParking</h2>
          <button className="min-h-11 px-2" type="button" onClick={() => setOpen(false)}>Đóng</button>
        </header>
        <div className="flex flex-wrap items-center gap-2 border-b border-[var(--border)] px-3 py-1">
          <select aria-label="Hội thoại" className="min-h-11 max-w-40 bg-[var(--bg)]" value={conversation?.id ?? ""} disabled={busy || Boolean(pending)}
            onChange={event => { const id = event.target.value; if (id) void run(signal => loadHistory(id, signal)) }}>
            <option value="" disabled>Chọn hội thoại</option>
            {conversation && !conversations.some(item => item.id === conversation.id) && <option value={conversation.id}>{conversation.title}</option>}
            {conversations.map(item => <option key={item.id} value={item.id}>{item.title}</option>)}
          </select>
          <button type="button" disabled={busy || Boolean(pending)} onClick={resetConversation} className="min-h-11 underline">Mới</button>
          {listCursor && <button type="button" disabled={busy} className="min-h-11 underline" onClick={() => void run(async signal => {
            const page = await assistantApi.conversations(listCursor, signal)
            if (signal.aborted || generation !== getAuthSessionGeneration()) return
            setConversations(previous => [...previous, ...page.items.filter(item => !previous.some(old => old.id === item.id))])
            setListCursor(page.nextCursor)
          })}>Thêm hội thoại</button>}
        </div>
        <div className="min-h-24 flex-1 space-y-3 overflow-auto p-3">
          {!enabled && <p>Hiện chỉ có thể xem lịch sử. Gửi câu hỏi chưa khả dụng.</p>}
          {pendingQuestion && <div className="space-y-1 border-b border-[var(--border)] pb-2">
            <p className="break-words">Câu hỏi trước khi đăng nhập: {pendingQuestion}</p>
            <button type="button" disabled={!enabled || busy || Boolean(pending)} className="min-h-11 underline" onClick={() => {
              setInput(pendingQuestion)
              onUseQuestion?.()
            }}>Đưa vào ô soạn</button>
          </div>}
          {historyCursor && conversation && <button type="button" disabled={busy || Boolean(pending)} className="min-h-11 underline"
            onClick={() => void run(signal => loadHistory(conversation.id, signal, historyCursor))}>Tin nhắn cũ hơn</button>}
          {messages.map(message => <article key={message.id} className="space-y-2 rounded-md border border-[var(--border)] p-2">
            <p className="text-xs text-[var(--muted)]">{message.role.toLowerCase() === "user" ? "Bạn" : "Trợ lý"}</p>
            <p className="whitespace-pre-wrap break-words">{message.content}</p>
            <MessageCards message={message} disabled={!enabled || busy || Boolean(pending)} detail={id => send(`reservation details ${id}`)} />
            {message.sources.map(record).filter(source => source.status !== "withdrawn" && typeof source.excerpt === "string").map((source, index) =>
              <details key={index}><summary>{label(source.title)}</summary><p className="whitespace-pre-wrap break-words">{label(source.excerpt)}</p></details>)}
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
        <form className="flex gap-2 border-t border-[var(--border)] p-2" onSubmit={event => { event.preventDefault(); send(input) }}>
          <input aria-label="Câu hỏi" className="min-h-11 min-w-0 flex-1 rounded border border-[var(--border)] bg-[var(--bg)] px-2" maxLength={4000}
            disabled={!enabled || Boolean(pending)} value={input} onChange={event => setInput(event.target.value)} />
          <button type="submit" className="min-h-11 px-2 font-semibold" disabled={!enabled || busy || Boolean(pending) || !input.trim()}>Gửi</button>
        </form>
      </section>}
  </div>
}
