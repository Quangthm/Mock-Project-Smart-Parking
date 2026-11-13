export type DemoSource = { articleId: string; version: string; title: string; excerpt: string }
export type DemoReply = {
  isDemo: true; notice: string; reply: string; mode: "mock" | "provider" | "knowledge";
  sources: DemoSource[]; provider?: string | null; model?: string | null
}

function isSource(value: unknown): value is DemoSource {
  if (!value || typeof value !== "object") return false
  const source = value as Record<string, unknown>
  return ["articleId", "version", "title", "excerpt"].every(key =>
    typeof source[key] === "string" && (source[key] as string).trim().length > 0)
}

export async function sendDemoMessage(message: string, signal: AbortSignal): Promise<DemoReply> {
  const base = import.meta.env.VITE_ASSISTANT_DEMO_API_BASE_URL?.trim()
  if (!base) throw new Error("Chưa cấu hình địa chỉ API demo.")
  let url: URL
  try {
    url = new URL(base)
    if (!["http:", "https:"].includes(url.protocol) || url.username || url.password || url.search || url.hash)
      throw new Error()
  } catch { throw new Error("Địa chỉ API demo không hợp lệ.") }
  const text = message.trim()
  if (!text || text.length > 4000) throw new Error("Nhập từ 1 đến 4000 ký tự.")
  const controller = new AbortController()
  const cancel = () => controller.abort()
  signal.addEventListener("abort", cancel, { once: true })
  if (signal.aborted) controller.abort()
  let timedOut = false
  const timer = setTimeout(() => { timedOut = true; controller.abort() }, 30000)
  try {
    const response = await fetch(`${url.href.replace(/\/$/, "")}/api/demo/assistant/messages`, {
      method: "POST", credentials: "omit", redirect: "error", cache: "no-store",
      headers: { "Content-Type": "application/json" }, body: JSON.stringify({ message: text }),
      signal: controller.signal,
    })
    if (!response.ok) {
      if (response.status === 503) {
        const error = await response.json().catch(() => null)
        if (error?.code === "ASSISTANT_MAINTENANCE" && typeof error.message === "string" && error.message.length <= 500)
          throw new Error(error.message)
        const modelErrors: Record<string, string> = {
          RETRIEVAL_UNAVAILABLE: "Chưa tải được hướng dẫn SmartPark. Bạn có thể tiếp tục thao tác trên ứng dụng hoặc liên hệ hỗ trợ.",
          ASSISTANT_NOT_CONFIGURED: "Trợ lý hiện chưa sẵn sàng. Bạn có thể tiếp tục thao tác trên ứng dụng hoặc liên hệ hỗ trợ.",
          MODEL_TIMEOUT: "Dịch vụ AI phản hồi quá lâu. Bạn có thể chờ rồi thử lại.",
          INVALID_MODEL_OUTPUT: "Dịch vụ AI trả về phản hồi chưa hợp lệ. Bạn có thể thử lại sau.",
          MODEL_UNAVAILABLE: "Trợ lý đang tạm gián đoạn. Bạn vẫn có thể tìm bãi và đặt chỗ trực tiếp trên ứng dụng.",
          MANAGEMENT_UNAVAILABLE: "Trợ lý hiện chưa sẵn sàng. Bạn có thể tiếp tục thao tác trên ứng dụng hoặc liên hệ hỗ trợ.",
        }
        if (typeof error?.code === "string" && Object.hasOwn(modelErrors, error.code)) {
          const trace = typeof error.traceId === "string" && /^[a-zA-Z0-9._-]{1,128}$/.test(error.traceId)
            ? ` Mã tra cứu: ${error.traceId}.` : ""
          throw new Error(modelErrors[error.code] + trace)
        }
      }
      const errors: Record<number, string> = {
        400: "Tin nhắn chưa hợp lệ. Hãy kiểm tra nội dung và gửi lại.",
        413: "Tin nhắn quá lớn. Hãy rút ngắn nội dung.",
        415: "API chưa hỗ trợ định dạng tin nhắn này.",
        429: "Bạn đang gửi quá nhanh. Chờ một lúc rồi gửi lại một lần.",
      }
      throw new Error(errors[response.status] ?? "API demo chưa thể trả lời. Bạn có thể thử gửi lại.")
    }
    const body = await response.json()
    if (body?.success !== true || body.data?.isDemo !== true ||
      typeof body.data.reply !== "string" || !body.data.reply.trim() ||
      typeof body.data.notice !== "string" || !body.data.notice.trim() ||
      !["mock", "provider", "knowledge"].includes(body.data.mode) ||
      !Array.isArray(body.data.sources) || !body.data.sources.every(isSource) ||
      (body.data.provider != null && (typeof body.data.provider !== "string" || !body.data.provider.trim())) ||
      (body.data.model != null && (typeof body.data.model !== "string" || !body.data.model.trim())) ||
      (body.data.mode === "knowledge" && (body.data.provider != null || body.data.model != null)))
      throw new Error("Phản hồi API demo không hợp lệ.")
    return body.data
  } catch (error) {
    if (timedOut) throw new Error("API demo phản hồi quá lâu. Bạn có thể thử gửi lại.")
    if (signal.aborted) throw error
    if (error instanceof TypeError) throw new Error("Không kết nối được API demo. Kiểm tra kết nối rồi thử lại.")
    if (error instanceof SyntaxError) throw new Error("Phản hồi API demo không hợp lệ.")
    throw error
  } finally {
    clearTimeout(timer)
    signal.removeEventListener("abort", cancel)
  }
}
