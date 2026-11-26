import { useEffect, useRef, useState } from "react"
import { sendDemoMessage, type DemoReply } from "../../../lib/assistantDemoApi"
import "./assistant-demo.css"

type Turn = { id: number; message: string; result?: DemoReply; error?: string }
const suggestions = ["Làm sao để tìm bãi đỗ xe?", "Tôi muốn đặt chỗ đỗ xe", "Thanh toán phí đỗ xe như thế nào?"]

export default function AssistantDemoPage() {
  const [turns, setTurns] = useState<Turn[]>([])
  const [draft, setDraft] = useState("")
  const [pending, setPending] = useState(false)
  const request = useRef<AbortController | null>(null)
  const sequence = useRef(0)
  const input = useRef<HTMLTextAreaElement>(null)
  const end = useRef<HTMLDivElement>(null)

  useEffect(() => () => { sequence.current++; request.current?.abort() }, [])
  useEffect(() => { end.current?.scrollIntoView({ block: "nearest" }) }, [turns, pending])

  function reset() {
    sequence.current++
    request.current?.abort()
    request.current = null
    setPending(false)
    setTurns([])
    setDraft("")
    input.current?.focus()
  }

  async function send(text: string, retryId?: number) {
    const message = text.trim()
    if (request.current || !message || message.length > 4000) return
    const id = ++sequence.current
    const turnId = retryId ?? id
    const controller = new AbortController()
    request.current = controller
    setPending(true)
    if (retryId === undefined) {
      setTurns(previous => [...previous, { id: turnId, message }])
      setDraft("")
    } else setTurns(previous => previous.map(turn => turn.id === turnId ? { id: turnId, message } : turn))
    try {
      const result = await sendDemoMessage(message, controller.signal)
      if (id !== sequence.current) return
      setTurns(previous => previous.map(turn => turn.id === turnId ? { ...turn, result } : turn))
    } catch (error) {
      if (id !== sequence.current || controller.signal.aborted) return
      setTurns(previous => previous.map(turn => turn.id === turnId ? {
        ...turn, error: error instanceof Error ? error.message : "Chưa thể nhận phản hồi demo. Hãy thử lại.",
      } : turn))
    } finally {
      if (id === sequence.current) {
        request.current = null
        setPending(false)
        input.current?.focus()
      }
    }
  }

  return <main className="assistant-demo">
    <header className="demo-header">
      <div><span className="demo-brand">SmartPark</span><h1>Trò chuyện thử nghiệm</h1></div>
      <button type="button" onClick={reset}>Hội thoại mới</button>
    </header>
    <aside className="demo-notice"><strong>Bản demo · Hỏi đáp SmartPark</strong><p>Chế độ trả lời phụ thuộc cấu hình máy chủ và được ghi rõ dưới mỗi phản hồi. Câu hỏi có thể được gửi đến dịch vụ AI. Không nhập thông tin cá nhân. Chưa kết nối tài khoản hoặc giao dịch; hội thoại chỉ giữ trong tab này và mất khi tải lại.</p></aside>
    <section className="demo-conversation" aria-label="Hội thoại demo">
      {turns.length === 0 && <div className="demo-empty">
        <h2>Bạn muốn thử hỏi điều gì?</h2>
        <p>Thử hỏi về SmartPark. Khi có nguồn tham khảo, bạn có thể đọc ngay dưới câu trả lời.</p>
        <div className="demo-suggestions">{suggestions.map(text => <button key={text} type="button" onClick={() => { setDraft(text); input.current?.focus() }}>{text}<span aria-hidden="true">↗</span></button>)}</div>
      </div>}
      <div role="log" aria-live="polite" aria-relevant="additions text">
        {turns.map(turn => <article key={turn.id} className="demo-turn">
          <div className="demo-user"><span>Bạn</span><p>{turn.message}</p></div>
          {turn.result && <div className="demo-reply">
            <span>SmartPark · {turn.result.mode === "knowledge" ? "Hướng dẫn từ tài liệu" : turn.result.mode === "mock" ? "Mô phỏng" : "Chế độ AI"}</span>
            <p>{turn.result.reply}</p><small>{turn.result.notice}</small>
            {turn.result.provider && <small>Dịch vụ: {turn.result.provider}{turn.result.model ? ` · ${turn.result.model}` : ""}</small>}
            {turn.result.sources.length > 0 && <details open={turn.result.mode === "knowledge"} className="demo-sources">
              <summary>Nguồn tham khảo ({turn.result.sources.length})</summary>
              <ol>{turn.result.sources.map((source, index) => <li key={`${source.articleId}-${source.version}-${index}`}>
                <strong>{source.title}</strong><small>{source.articleId} · Phiên bản {source.version}</small><p>{source.excerpt}</p>
              </li>)}</ol>
            </details>}
          </div>}
          {turn.error && <div className="demo-error" role="alert"><p>{turn.error}</p><button type="button" disabled={pending} onClick={() => void send(turn.message, turn.id)}>Thử gửi lại</button></div>}
        </article>)}
      </div>
      {pending && <p className="demo-pending" role="status">Đang chờ phản hồi…</p>}
      <div ref={end} />
    </section>
    <form className="demo-composer" onSubmit={event => { event.preventDefault(); void send(draft) }}>
      <label htmlFor="demo-message">Tin nhắn của bạn</label>
      <textarea id="demo-message" ref={input} value={draft} rows={3} maxLength={4000}
        placeholder="Hỏi về bãi đỗ, đặt chỗ hoặc thanh toán…" onChange={event => setDraft(event.target.value)}
        aria-describedby="demo-input-help" onKeyDown={event => {
          if (event.key === "Enter" && !event.shiftKey && !event.nativeEvent.isComposing) { event.preventDefault(); void send(draft) }
        }} />
      <div className="demo-composer-footer"><span id="demo-input-help">{draft.length}/4000 · Shift + Enter để xuống dòng</span><button className="demo-send" type="submit" disabled={pending || !draft.trim()}>{pending ? "Đang gửi…" : "Gửi tin nhắn"}</button></div>
    </form>
  </main>
}
