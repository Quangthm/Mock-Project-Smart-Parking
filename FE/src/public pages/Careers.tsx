import { UntitledIcon } from "../components/icon/UntitledIcon"

const JOBS = [
  {
    title: "Senior Full-Stack Engineer",
    dept: "Engineering",
    location: "HCMC / Remote",
    type: "Full-time",
    desc: "Build and scale SmartParking's core booking and real-time systems. React, Node.js, PostgreSQL, Redis.",
  },

  {
    title: "IoT Systems Engineer",
    dept: "Engineering",
    location: "HCMC",
    type: "Full-time",
    desc: "Design and deploy sensor networks, camera integrations and barrier systems across partner lots.",
  },

  {
    title: "Product Manager — Mobility",
    dept: "Product",
    location: "HCMC",
    type: "Full-time",
    desc: "Define the roadmap for SmartParking's driver and owner products. Work closely with engineering and design.",
  },

  {
    title: "Business Development Manager",
    dept: "Sales",
    location: "HCMC / Hanoi",
    type: "Full-time",
    desc: "Grow our network of parking lot partners across Vietnam. Own the full partnership cycle from outreach to onboarding.",
  },

  {
    title: "UX Designer",
    dept: "Design",
    location: "HCMC / Remote",
    type: "Full-time",
    desc: "Design intuitive experiences for drivers, operators, and lot owners. Strong prototyping skills required.",
  },

  {
    title: "Customer Success Specialist",
    dept: "Support",
    location: "HCMC",
    type: "Full-time",
    desc: "Support driver and business customers. Build processes that scale our support operations.",
  },
]

const PERKS = [
  { icon: "🏥", label: "Comprehensive Health Insurance" },

  { icon: "🎓", label: "Learning & Development Budget" },

  { icon: "🏠", label: "Flexible Remote Work" },

  { icon: "🚗", label: "Free SmartParking Credits" },

  { icon: "📈", label: "Performance Bonuses" },

  { icon: "🌏", label: "Annual Company Retreat" },
]

export function Careers() {
  return (
    <div
      style={{ maxWidth: "900px", margin: "0 auto", padding: "3rem 1.5rem" }}
    >
      <div style={{ textAlign: "center", marginBottom: "3rem" }}>
        <h1
          style={{
            fontFamily: "Outfit",
            fontWeight: 800,
            fontSize: "2rem",
            color: "var(--fg)",
            marginBottom: "0.75rem",
          }}
        >
          Careers at SmartParking
        </h1>
        <p
          style={{
            color: "var(--muted)",
            fontSize: "1rem",
            maxWidth: "500px",
            margin: "0 auto",
            lineHeight: 1.7,
          }}
        >
          Join our mission to make urban parking smarter. We're a fast-growing
          team building technology that impacts millions of daily journeys.
        </p>
      </div>

      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))",
          gap: "1rem",
          marginBottom: "3rem",
        }}
      >
        {PERKS.map((p) => (
          <div
            key={p.label}
            className="card"
            style={{ display: "flex", gap: "0.625rem", alignItems: "center" }}
          >
            <span style={{ color: "var(--primary)", display: "inline-flex" }}>
              <UntitledIcon name={p.icon} size={20} />
            </span>
            <span
              style={{
                fontSize: "0.85rem",
                fontWeight: 500,
                color: "var(--fg)",
              }}
            >
              {p.label}
            </span>
          </div>
        ))}
      </div>

      <h2
        style={{
          fontFamily: "Outfit",
          fontWeight: 700,
          fontSize: "1.3rem",
          color: "var(--fg)",
          marginBottom: "1.25rem",
          borderLeft: "3px solid var(--primary)",
          paddingLeft: "0.75rem",
        }}
      >
        Open Positions
      </h2>
      <div
        style={{
          display: "flex",
          flexDirection: "column",
          gap: "0.875rem",
          marginBottom: "2.5rem",
        }}
      >
        {JOBS.map((job) => (
          <div
            key={job.title}
            className="card"
            style={{
              display: "flex",
              justifyContent: "space-between",
              gap: "1rem",
              flexWrap: "wrap",
            }}
          >
            <div style={{ flex: 1 }}>
              <div
                style={{
                  fontFamily: "Outfit",
                  fontWeight: 700,
                  fontSize: "0.95rem",
                  color: "var(--fg)",
                  marginBottom: "0.25rem",
                }}
              >
                {job.title}
              </div>
              <div
                style={{
                  display: "flex",
                  gap: "0.625rem",
                  marginBottom: "0.5rem",
                  flexWrap: "wrap",
                }}
              >
                {[job.dept, job.location, job.type].map((tag) => (
                  <span
                    key={tag}
                    style={{
                      fontSize: "0.72rem",
                      padding: "0.15rem 0.5rem",
                      background: "var(--muted)15",
                      borderRadius: "4px",
                      color: "var(--muted)",
                      fontWeight: 500,
                    }}
                  >
                    {tag}
                  </span>
                ))}
              </div>
              <div
                style={{
                  fontSize: "0.82rem",
                  color: "var(--muted)",
                  lineHeight: 1.6,
                }}
              >
                {job.desc}
              </div>
            </div>
            <button
              className="btn-outline"
              style={{
                fontSize: "0.8rem",
                padding: "0.4rem 0.875rem",
                alignSelf: "flex-start",
                flexShrink: 0,
              }}
              onClick={() =>
                alert(
                  `Apply for ${job.title}: Send your CV to careers@smartparking.vn`,
                )
              }
            >
              Apply
            </button>
          </div>
        ))}
      </div>

      <div className="card" style={{ textAlign: "center", padding: "2rem" }}>
        <h3
          style={{
            fontFamily: "Outfit",
            fontWeight: 700,
            fontSize: "1rem",
            color: "var(--fg)",
            marginBottom: "0.5rem",
          }}
        >
          Don't see the right role?
        </h3>
        <p
          style={{
            color: "var(--muted)",
            fontSize: "0.875rem",
            marginBottom: "1rem",
          }}
        >
          We're always looking for exceptional talent. Send us your CV and tell
          us how you'd contribute.
        </p>
        <a
          href="mailto:careers@smartparking.vn"
          style={{
            color: "var(--primary)",
            fontWeight: 600,
            fontSize: "0.875rem",
          }}
        >
          careers@smartparking.vn
        </a>
      </div>
    </div>
  )
}
