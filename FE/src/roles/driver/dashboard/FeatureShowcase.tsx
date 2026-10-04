import { useState, useEffect } from "react"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"

// Animated feature showcase for home tab
const FEATURES = [
  {
    icon: "🗺️",
    title: "Find Nearby Lots",
    desc: "Real-time map showing available lots near your location.",
    color: "#2563eb",
  },
  {
    icon: "🅿️",
    title: "Choose & Book a Slot",
    desc: "See live availability and reserve in seconds.",
    color: "#22c55e",
  },
  {
    icon: "💳",
    title: "Secure Payment",
    desc: "OTP-verified payments with 6 popular methods.",
    color: "#f59e0b",
  },
  {
    icon: "📋",
    title: "Booking History",
    desc: "Track all your past and current bookings.",
    color: "#a855f7",
  },
  {
    icon: "🎫",
    title: "Monthly Subscriptions",
    desc: "Save more with pass packages at partner lots.",
    color: "#ef4444",
  },
  {
    icon: "🛟",
    title: "Support & Appeals",
    desc: "Get help, report issues, or appeal a charge.",
    color: "#06b6d4",
  },
]

export function FeatureShowcase() {
  const [active, setActive] = useState(0)
  useEffect(() => {
    const id = setInterval(
      () => setActive((a) => (a + 1) % FEATURES.length),
      2500,
    )
    return () => clearInterval(id)
  }, [])
  const f = FEATURES[active]
  return (
    <div className="card" style={{ textAlign: "center", padding: "2rem" }}>
      <div
        style={{
          display: "flex",
          justifyContent: "center",
          gap: "0.5rem",
          marginBottom: "1.5rem",
          flexWrap: "wrap",
        }}
      >
        {FEATURES.map((feat, i) => (
          <button
            key={i}
            onClick={() => setActive(i)}
            style={{
              width: 10,
              height: 10,
              borderRadius: "50%",
              border: "none",
              cursor: "pointer",
              background: i === active ? f.color : "var(--border)",
              transition: "background 0.3s",
            }}
          />
        ))}
      </div>
      <div key={active} className="animate-in" style={{ minHeight: "120px" }}>
        <div style={{ color: f.color, marginBottom: "0.75rem" }}>
          <UntitledIcon name={f.icon} size={36} />
        </div>
        <h3
          style={{
            fontFamily: "Outfit",
            fontWeight: 700,
            fontSize: "1.2rem",
            color: f.color,
            marginBottom: "0.5rem",
          }}
        >
          {f.title}
        </h3>
        <p style={{ color: "var(--muted)", fontSize: "0.9rem" }}>{f.desc}</p>
      </div>
      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(3, 1fr)",
          gap: "0.5rem",
          marginTop: "1.5rem",
        }}
      >
        {FEATURES.map((feat, i) => (
          <button
            key={i}
            onClick={() => setActive(i)}
            style={{
              background: i === active ? feat.color + "18" : "var(--card)",
              border: `1.5px solid ${
                i === active ? feat.color : "var(--border)"
              }`,
              borderRadius: "var(--radius)",
              padding: "0.5rem",
              cursor: "pointer",
              fontSize: "1.1rem",
              transition: "all 0.2s",
            }}
          >
            <UntitledIcon name={feat.icon} size={20} />
          </button>
        ))}
      </div>
    </div>
  )
}
