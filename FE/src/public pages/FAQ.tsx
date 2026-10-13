import { useState } from "react"

const FAQS = [
  {
    q: "How do I book a parking slot?",
    a: "Sign in or create an account, use the map to find a nearby lot, select an available slot, enter your license plate, and complete payment. You'll receive OTP confirmation via SMS or email.",
  },

  {
    q: "What is the cancellation and refund policy?",
    a: "Cancel 30+ minutes before: 100% refund. Cancel 15–30 minutes before: 50% refund. Cancel less than 15 minutes or no-show: no refund. Refunds are processed within 3–5 business days.",
  },

  {
    q: "How does OTP verification work?",
    a: "After selecting your payment method, a 6-digit OTP is sent to your registered email or phone. You have 3 attempts to enter the correct code. After 3 failed attempts, the booking is cancelled and your account is locked for 5 minutes.",
  },

  {
    q: "What license plate formats are accepted?",
    a: "Vietnamese license plates follow the format: XX[A-Z]-XXXXX (e.g., 51A-12345, 51AB-67890). Foreign plates are also accepted and flagged separately for operator verification.",
  },

  {
    q: "How do I become a parking lot owner/partner?",
    a: 'Click "Become a Partner" and complete the business registration form. Our team will review your application and contact you within 2–3 business days to set up your account and onboarding.',
  },

  {
    q: "How are operators created?",
    a: "Operator accounts are created and managed exclusively by the lot Owner through their dashboard. Operators receive their credentials directly from the Owner and cannot self-register.",
  },

  {
    q: "What payment methods are supported?",
    a: "We support Visa/Mastercard, domestic ATM cards, MoMo, ZaloPay, VNPAY, and QR code payments. All transactions are encrypted and secure.",
  },

  {
    q: "Can I get a monthly subscription?",
    a: "Yes. Monthly passes start from 800,000₫/month and annual subscriptions offer up to 2 months free. Available for select partner lots.",
  },
]

export function FAQ() {
  const [open, setOpen] = useState<number | null>(null)

  return (
    <div
      style={{ maxWidth: "900px", margin: "0 auto", padding: "3rem 1.5rem" }}
    >
      <h1 className="section-title" style={{ marginBottom: "0.5rem" }}>
        Frequently Asked Questions
      </h1>
      <p className="section-sub" style={{ marginBottom: "2rem" }}>
        Find answers to common questions about SmartParking.
      </p>
      <section
        style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}
      >
        {FAQS.map((faq, i) => (
          <div
            key={faq.q}
            style={{
              border: "1px solid var(--border)",
              borderRadius: "var(--radius)",
              overflow: "hidden",
            }}
          >
            <button
              onClick={() => setOpen(open === i ? null : i)}
              aria-expanded={open === i}
              style={{
                width: "100%",
                background: "var(--card)",
                border: "none",
                padding: "1rem 1.25rem",
                textAlign: "left",
                cursor: "pointer",
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                gap: "1rem",
              }}
            >
              <span
                style={{
                  fontWeight: 600,
                  fontSize: "0.925rem",
                  color: "var(--fg)",
                }}
              >
                {faq.q}
              </span>
              <span
                style={{
                  color: "var(--primary)",
                  fontSize: "1.1rem",
                  flexShrink: 0,
                }}
              >
                {open === i ? "−" : "+"}
              </span>
            </button>
            {open === i && (
              <div
                style={{
                  padding: "0.875rem 1.25rem",
                  background: "var(--bg)",
                  borderTop: "1px solid var(--border)",
                  fontSize: "0.875rem",
                  color: "var(--muted)",
                  lineHeight: 1.7,
                }}
              >
                {faq.a}
              </div>
            )}
          </div>
        ))}
      </section>
    </div>
  )
}
