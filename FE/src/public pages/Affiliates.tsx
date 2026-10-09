import { UntitledIcon } from "../components/icon/UntitledIcon"

export function Affiliates() {
  return (
    <div
      style={{ maxWidth: "900px", margin: "0 auto", padding: "3rem 1.5rem" }}
    >
      <h1
        style={{
          fontFamily: "Outfit",
          fontWeight: 800,
          fontSize: "2rem",
          color: "var(--fg)",
          marginBottom: "0.5rem",
        }}
      >
        Affiliate Program
      </h1>
      <p
        style={{
          color: "var(--muted)",
          fontSize: "1rem",
          marginBottom: "3rem",
        }}
      >
        Earn by referring businesses and drivers to SmartParking.
      </p>

      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
          gap: "1.25rem",
          marginBottom: "3rem",
        }}
      >
        {[
          {
            icon: "🔗",
            title: "Share Your Link",
            desc: "Get a unique referral link after joining the program.",
          },

          {
            icon: "👥",
            title: "Refer Businesses",
            desc: "Earn 2% of revenue from every lot you refer for 12 months.",
          },

          {
            icon: "🚗",
            title: "Refer Drivers",
            desc: "Earn 20,000₫ credit for every driver who books their first spot.",
          },

          {
            icon: "💰",
            title: "Get Paid",
            desc: "Monthly payouts via bank transfer or in-app credit.",
          },
        ].map((s) => (
          <div key={s.title} className="card" style={{ textAlign: "center" }}>
            <div style={{ color: "var(--primary)", marginBottom: "0.625rem" }}>
              <UntitledIcon name={s.icon} size={26} />
            </div>
            <div
              style={{
                fontFamily: "Outfit",
                fontWeight: 600,
                fontSize: "0.95rem",
                color: "var(--fg)",
                marginBottom: "0.375rem",
              }}
            >
              {s.title}
            </div>
            <div
              style={{
                fontSize: "0.82rem",
                color: "var(--muted)",
                lineHeight: 1.6,
              }}
            >
              {s.desc}
            </div>
          </div>
        ))}
      </div>

      <div className="card" style={{ marginBottom: "2.5rem" }}>
        <h2
          style={{
            fontFamily: "Outfit",
            fontWeight: 700,
            fontSize: "1.15rem",
            color: "var(--fg)",
            marginBottom: "1rem",
          }}
        >
          Commission Structure
        </h2>
        <div
          style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}
        >
          {[
            {
              type: "Business Referral (Lot Owner)",
              rate: "2% of monthly revenue",
              period: "12 months",
              color: "#2563eb",
            },

            {
              type: "Driver Referral",
              rate: "20,000₫ credit",
              period: "Per first booking",
              color: "#22c55e",
            },

            {
              type: "Premium Business Partner",
              rate: "3% + bonuses",
              period: "Annual program",
              color: "#f59e0b",
            },
          ].map((row) => (
            <div
              key={row.type}
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                padding: "0.75rem 1rem",
                background: `${row.color}10`,
                borderRadius: "var(--radius)",
                border: `1px solid ${row.color}30`,
              }}
            >
              <span
                style={{
                  fontSize: "0.875rem",
                  fontWeight: 500,
                  color: "var(--fg)",
                }}
              >
                {row.type}
              </span>
              <div style={{ textAlign: "right" }}>
                <div
                  style={{
                    fontWeight: 700,
                    color: row.color,
                    fontSize: "0.875rem",
                  }}
                >
                  {row.rate}
                </div>
                <div style={{ fontSize: "0.72rem", color: "var(--muted)" }}>
                  {row.period}
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>

      <div className="card" style={{ maxWidth: "480px" }}>
        <h3
          style={{
            fontFamily: "Outfit",
            fontWeight: 700,
            fontSize: "1rem",
            color: "var(--fg)",
            marginBottom: "1rem",
          }}
        >
          Apply to Join
        </h3>
        <div
          style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}
        >
          <input className="input" placeholder="Your full name" />
          <input className="input" type="email" placeholder="Email address" />
          <input
            className="input"
            placeholder="Website or social profile (optional)"
          />
          <button
            className="btn-primary"
            style={{ alignSelf: "flex-start" }}
            onClick={() =>
              alert(
                "Application submitted! We will contact you within 3 business days.",
              )
            }
          >
            Apply Now
          </button>
        </div>
      </div>
    </div>
  )
}
