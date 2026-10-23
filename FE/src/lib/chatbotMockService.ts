import type { Role } from "./types"

type Guidance = { question: string; answer: string }

const guidance: Record<Role, Guidance[]> = {
  driver: [
    { question: "How do I find parking?", answer: "Open Find parking from your Driver dashboard to browse the parking options available to you." },
    { question: "Where can I manage my vehicles?", answer: "Open your Driver dashboard and go to the vehicle section to review or update your registered vehicles." },
    { question: "How do I view my bookings?", answer: "Use Current bookings for active bookings and Booking history for earlier bookings on your Driver dashboard." },
  ],
  owner: [
    { question: "Where can I edit parking structure?", answer: "Open Parking Structure in your Owner dashboard to view sites, units, slots, and capacity settings." },
    { question: "How do I manage operators?", answer: "Open Operators in your Owner dashboard to review operator access and assignments." },
    { question: "Where are policy settings?", answer: "Open Policies in your Owner dashboard to review the settings available to your account." },
  ],
  operator: [
    { question: "Where can I view my assignments?", answer: "If your account has access, open My assignments in your Operator dashboard to see your assigned sites." },
    { question: "How do I handle check-in?", answer: "If your account has access, open Check-in and check-out in your Operator dashboard. The actions available there depend on your permissions." },
    { question: "Where can I view driver tickets?", answer: "Open Driver tickets in your Operator dashboard if your account has access to that section." },
  ],
  admin: [
    { question: "Where can I manage users?", answer: "Open User Management in your Admin dashboard to find the account controls available to you." },
    { question: "How do I review audit logs?", answer: "Open Audit log in your Admin dashboard to review the records available to your account." },
    { question: "Where are system settings?", answer: "Open System configuration in your Admin dashboard to see the settings available to you." },
  ],
}

const topics: Record<Role, RegExp[]> = {
  driver: [/find|search|parking|đỗ xe|bãi xe/, /vehicle|car|motorcycle|xe của tôi|phương tiện/, /booking|reservation|đặt chỗ/],
  owner: [/structure|floor|zone|slot|cấu trúc|tầng|khu vực/, /operator|nhân viên/, /policy|policies|chính sách/],
  operator: [/assignment|assigned|phân công/, /check.in|check.out|vào bãi|ra bãi/, /ticket|support|hỗ trợ/],
  admin: [/user|account|tài khoản|người dùng/, /audit|nhật ký/, /system|configuration|cấu hình/],
}

export function suggestedQuestions(role: Role): string[] {
  return guidance[role].map((item) => item.question)
}

export async function getMockChatReply(role: Role, question: string): Promise<string> {
  await new Promise((resolve) => setTimeout(resolve, 450))

  const normalized = question.trim().toLowerCase().replace(/[?.!]+$/, "")
  const match = guidance[role].find((item) => item.question.toLowerCase().replace(/[?.!]+$/, "") === normalized)
  if (match) return match.answer

  if (/\b(hello|hi|hey)\b|xin chào|chào bạn/.test(normalized)) {
    return "Hello! I can help you find your way around SmartParking. Choose a suggested question or tell me what you need help with."
  }

  const topic = topics[role].findIndex((pattern) => pattern.test(normalized))
  if (topic >= 0) return guidance[role][topic].answer

  return "I can help with general SmartParking navigation, but I can't check live account, booking, payment, or parking data in this chat. Please use the relevant dashboard section for current details, or contact Support if you need help."
}
