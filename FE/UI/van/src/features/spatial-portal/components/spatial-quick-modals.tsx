import * as React from "react"
import { Bot, Sparkles, Send, QrCode, AlertTriangle, PhoneCall, CheckCircle2, ShieldAlert } from "lucide-react"
import { Button } from "@/components/ui/button"

/* --------------------------------------------------------------------------
   1. AI Assistant Chat Modal
   -------------------------------------------------------------------------- */
interface AiAssistantModalProps {
  isOpen: boolean
  onClose: () => void
}

export function SpatialAiAssistantModal({ isOpen, onClose }: AiAssistantModalProps) {
  const [messages, setMessages] = React.useState<{ role: "ai" | "user"; text: string }[]>([
    {
      role: "ai",
      text: "Xin chào tài xế! Tôi là SmartPark AI. Bạn muốn tôi tìm bãi đỗ còn nhiều chỗ, tra cứu giá cước hay dẫn đường vào ô sạc xe điện EV?",
    },
  ])
  const [input, setInput] = React.useState("")

  if (!isOpen) return null

  const handleSend = (e: React.FormEvent) => {
    e.preventDefault()
    if (!input.trim()) return
    const userMsg = input.trim()
    setMessages((prev) => [...prev, { role: "user", text: userMsg }])
    setInput("")

    setTimeout(() => {
      let reply = "Dạ bãi xe Vincom Center Đồng Khởi hiện còn 65 ô trống ở tầng B1, đơn giá 25.000 VND/giờ. Bạn có thể bấm trực tiếp vào ô A-08 trên sơ đồ 3D để giữ chỗ ngay ạ!"
      if (userMsg.toLowerCase().includes("sạc") || userMsg.toLowerCase().includes("ev")) {
        reply = "Khu B tầng B1 có sẵn 12 trụ sạc nhanh DC 60kW cho xe điện (ô C-01 tới C-12). Hệ thống đã tự động định vị cho bạn trên bản đồ rồi ạ!"
      }
      setMessages((prev) => [...prev, { role: "ai", text: reply }])
    }, 400)
  }

  return (
    <div className="fixed inset-0 z-50 bg-black/60 backdrop-blur-md flex items-center justify-center p-4">
      <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-white/20 rounded-2xl max-w-lg w-full h-[540px] flex flex-col justify-between shadow-2xl text-slate-900 dark:text-white overflow-hidden animate-in fade-in zoom-in-95 duration-200">
        {/* Header */}
        <div className="p-4 border-b border-slate-200 dark:border-white/10 flex items-center justify-between bg-slate-50 dark:bg-white/5">
          <div className="flex items-center gap-2.5">
            <div className="size-9 rounded-lg bg-gradient-to-r from-emerald-500 to-lime-400 text-slate-950 flex items-center justify-center font-bold">
              <Bot size={18} />
            </div>
            <div>
              <h3 className="font-bold text-base flex items-center gap-1.5 text-slate-900 dark:text-white">
                <span>SmartPark AI Copilot</span>
                <span className="size-2 rounded-full bg-emerald-500 animate-pulse" />
              </h3>
              <p className="text-sm text-slate-500 dark:text-slate-400">Trợ lý không gian đỗ xe thông minh</p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="size-8 rounded-full bg-slate-100 hover:bg-slate-200 text-slate-600 dark:bg-white/10 dark:hover:bg-white/20 dark:text-slate-400 flex items-center justify-center cursor-pointer transition-colors"
          >
            ✕
          </button>
        </div>

        {/* Message Thread */}
        <div className="p-4 flex-1 overflow-y-auto space-y-3 text-sm">
          {messages.map((m, idx) => (
            <div
              key={idx}
              className={`flex ${m.role === "user" ? "justify-end" : "justify-start"}`}
            >
              <div
                className={`max-w-[85%] p-3.5 rounded-2xl leading-relaxed text-sm ${
                  m.role === "user"
                    ? "bg-[#B4E33F] text-[#1E3B27] font-semibold"
                    : "bg-slate-100 text-slate-800 border border-slate-200 dark:bg-white/10 dark:text-slate-200 dark:border-white/10"
                }`}
              >
                {m.text}
              </div>
            </div>
          ))}
        </div>

        {/* Quick Suggestion Chips & Form Input */}
        <div className="p-3.5 border-t border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/5 space-y-2.5">
          <div className="flex items-center gap-2 overflow-x-auto text-sm text-slate-600 dark:text-slate-300">
            <button
              type="button"
              onClick={() => setInput("Tìm ô có sạc xe điện EV")}
              className="px-3 py-1.5 rounded-full bg-slate-200/70 hover:bg-slate-200 text-slate-800 dark:bg-white/10 dark:hover:bg-white/20 dark:text-slate-200 whitespace-nowrap cursor-pointer transition-colors text-sm font-medium"
            >
              ⚡ Ô sạc EV
            </button>
            <button
              type="button"
              onClick={() => setInput("Giá vé gửi qua đêm bao nhiêu?")}
              className="px-3 py-1.5 rounded-full bg-slate-200/70 hover:bg-slate-200 text-slate-800 dark:bg-white/10 dark:hover:bg-white/20 dark:text-slate-200 whitespace-nowrap cursor-pointer transition-colors text-sm font-medium"
            >
              🌙 Gửi qua đêm
            </button>
            <button
              type="button"
              onClick={() => setInput("Có ô nào gần lối thang máy không?")}
              className="px-3 py-1.5 rounded-full bg-slate-200/70 hover:bg-slate-200 text-slate-800 dark:bg-white/10 dark:hover:bg-white/20 dark:text-slate-200 whitespace-nowrap cursor-pointer transition-colors text-sm font-medium"
            >
              🛗 Gần thang máy
            </button>
          </div>

          <form onSubmit={handleSend} className="flex items-center gap-2">
            <input
              type="text"
              placeholder="Nhập câu hỏi cho trợ lý AI..."
              value={input}
              onChange={(e) => setInput(e.target.value)}
              className="flex-1 px-3.5 py-2.5 rounded-xl bg-white dark:bg-white/10 border border-slate-200 dark:border-white/20 text-slate-900 dark:text-white text-sm focus:outline-none focus:border-[#B4E33F]"
            />
            <Button intent="primary" size="md" type="submit" className="h-10 px-4">
              <Send size={16} />
            </Button>
          </form>
        </div>
      </div>
    </div>
  )
}

/* --------------------------------------------------------------------------
   2. Emergency SOS / Incident Report Modal
   -------------------------------------------------------------------------- */
interface SosModalProps {
  isOpen: boolean
  onClose: () => void
}

export function SpatialSosModal({ isOpen, onClose }: SosModalProps) {
  const [sent, setSent] = React.useState(false)
  const [reason, setReason] = React.useState("Kẹt barrier cổng ra")

  if (!isOpen) return null

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setSent(true)
    setTimeout(() => {
      setSent(false)
      onClose()
    }, 2000)
  }

  return (
    <div className="fixed inset-0 z-50 bg-black/60 backdrop-blur-md flex items-center justify-center p-4">
      <div className="bg-white dark:bg-slate-900 border border-red-200 dark:border-red-500/30 rounded-2xl max-w-md w-full p-6 shadow-2xl text-slate-900 dark:text-white space-y-4 animate-in fade-in zoom-in-95 duration-200">
        <div className="flex items-center justify-between pb-3 border-b border-slate-200 dark:border-white/10">
          <div className="flex items-center gap-2.5 text-red-500">
            <div className="size-9 rounded-xl bg-red-500/15 flex items-center justify-center shrink-0">
              <ShieldAlert size={20} />
            </div>
            <div>
              <h3 className="font-bold text-base text-slate-900 dark:text-white">Báo Cáo Sự Cố Khẩn Cấp</h3>
              <p className="text-sm text-slate-500 dark:text-slate-400">Kênh kết nối trực tiếp Operator trực ban</p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="size-8 rounded-full bg-slate-100 hover:bg-slate-200 text-slate-600 dark:bg-white/10 dark:hover:bg-white/20 dark:text-slate-400 flex items-center justify-center cursor-pointer transition-colors"
          >
            ✕
          </button>
        </div>

        {sent ? (
          <div className="py-6 text-center space-y-2.5">
            <div className="size-12 rounded-full bg-emerald-500/20 text-emerald-600 dark:text-emerald-400 mx-auto flex items-center justify-center">
              <CheckCircle2 size={24} />
            </div>
            <h4 className="font-bold text-base text-slate-900 dark:text-white">Đã gửi tín hiệu SOS thành công!</h4>
            <p className="text-sm text-slate-500 dark:text-slate-400">
              Nhân viên kỹ thuật tại Vincom Center Đồng Khởi đang di chuyển đến vị trí của bạn trong 2 phút.
            </p>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-4 text-sm">
            <div>
              <label className="font-semibold text-slate-700 dark:text-slate-300 block mb-1.5 text-sm">Vấn đề bạn gặp phải:</label>
              <select
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                className="w-full px-3.5 py-2.5 rounded-xl bg-slate-50 dark:bg-white/10 border border-slate-200 dark:border-white/20 text-slate-900 dark:text-white text-sm focus:outline-none focus:border-red-400"
              >
                <option value="Kẹt barrier cổng ra" className="bg-white dark:bg-slate-900 text-slate-900 dark:text-white">Kẹt barrier cổng ra</option>
                <option value="Ô đỗ đã có xe khác chiếm" className="bg-white dark:bg-slate-900 text-slate-900 dark:text-white">Ô đỗ đã có xe khác chiếm</option>
                <option value="Trụ sạc EV không nhận điện" className="bg-white dark:bg-slate-900 text-slate-900 dark:text-white">Trụ sạc EV không nhận điện</option>
                <option value="Sự cố va quẹt trong bãi" className="bg-white dark:bg-slate-900 text-slate-900 dark:text-white">Sự cố va quẹt trong bãi</option>
                <option value="Mất thẻ / Không nhận diện biển số" className="bg-white dark:bg-slate-900 text-slate-900 dark:text-white">Mất thẻ / Không nhận diện biển số</option>
              </select>
            </div>

            <div className="p-3.5 rounded-xl bg-red-500/10 border border-red-500/20 flex items-center gap-2.5 text-red-600 dark:text-red-300 text-sm">
              <PhoneCall size={18} className="shrink-0" />
              <span>Hotline kỹ thuật 24/7: <strong>1900 6868</strong> (Miễn cước)</span>
            </div>

            <div className="flex items-center justify-end gap-2.5 pt-2 border-t border-slate-200 dark:border-white/10">
              <Button intent="ghost" size="md" type="button" onClick={onClose} className="h-10 text-sm">
                Hủy
              </Button>
              <Button intent="destructive" size="md" type="submit" className="font-semibold h-10 text-sm border-slate-300 hover:border-slate-400 dark:border-white/20">
                Gửi cảnh báo ngay
              </Button>
            </div>
          </form>
        )}
      </div>
    </div>
  )
}
