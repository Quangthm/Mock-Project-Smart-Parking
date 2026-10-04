import { UntitledIcon } from '../components/icon/UntitledIcon';

export function About() {
  return (
    <div
      style={{ maxWidth: "900px", margin: "0 auto", padding: "3rem 1.5rem" }}
    >
      {/* Hero */}
      <div
        style={{
          borderRadius: "14px",
          overflow: "hidden",
          marginBottom: "3rem",
          position: "relative",
        }}
      >
        <img
          src="https://images.unsplash.com/photo-1494976388531-d1058494cdd8?w=900&h=320&fit=crop&auto=format"
          alt="City parking"
          style={{
            width: "100%",
            height: "240px",
            objectFit: "cover",
            opacity: 0.75,
          }}
        />
        <div
          style={{
            position: "absolute",
            inset: 0,
            background:
              "linear-gradient(to right, rgba(11,22,40,0.85) 0%, rgba(11,22,40,0.3) 100%)",
            display: "flex",
            alignItems: "center",
            paddingLeft: "2.5rem",
          }}
        >
          <div>
            <h1
              style={{
                fontFamily: "Outfit",
                fontWeight: 800,
                fontSize: "2rem",
                color: "#fff",
                margin: "0 0 0.5rem",
              }}
            >
              About SmartParking
            </h1>
            <p
              style={{
                color: "rgba(255,255,255,0.78)",
                fontSize: "0.95rem",
                margin: 0,
              }}
            >
              Redefining urban parking in Vietnam since 2024.
            </p>
          </div>
        </div>
      </div>

      <section style={{ marginBottom: "2.5rem" }}>
        <h2
          style={{
            fontFamily: "Outfit",
            fontWeight: 700,
            fontSize: "1.3rem",
            color: "var(--fg)",
            marginBottom: "1rem",
            borderLeft: "3px solid var(--primary)",
            paddingLeft: "0.75rem",
          }}
        >
          Our Mission
        </h2>
        <p
          style={{
            color: "var(--muted)",
            fontSize: "0.95rem",
            lineHeight: 1.8,
          }}
        >
          SmartParking was founded with a single goal: make urban parking
          effortless. We connect drivers with available parking spots instantly,
          while giving lot owners the tools to run profitable, data-driven
          operations. We believe that smarter parking means less congestion,
          less pollution, and less wasted time for everyone.
        </p>
      </section>

      <section style={{ marginBottom: "2.5rem" }}>
        <h2
          style={{
            fontFamily: "Outfit",
            fontWeight: 700,
            fontSize: "1.3rem",
            color: "var(--fg)",
            marginBottom: "1rem",
            borderLeft: "3px solid var(--primary)",
            paddingLeft: "0.75rem",
          }}
        >
          Our Story
        </h2>
        <p
          style={{
            color: "var(--muted)",
            fontSize: "0.95rem",
            lineHeight: 1.8,
          }}
        >
          Founded in Ho Chi Minh City in 2024, SmartParking emerged from a
          simple frustration: spending 20 minutes circling blocks looking for a
          parking spot. Our founding team of engineers and urban mobility
          experts built a platform that combines real-time IoT sensors,
          AI-powered camera systems, and a seamless booking experience to solve
          this problem at scale.
        </p>
        <p
          style={{
            color: "var(--muted)",
            fontSize: "0.95rem",
            lineHeight: 1.8,
            marginTop: "0.875rem",
          }}
        >
          Today, we partner with over 50 parking operators across Vietnam,
          managing more than 1,200 parking slots and processing thousands of
          bookings daily.
        </p>
      </section>

      <section style={{ marginBottom: "2.5rem" }}>
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
          Our Values
        </h2>
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
            gap: "1rem",
          }}
        >
          {[
            {
              icon: "🎯",
              title: "Transparency",
              desc: "Clear pricing, honest refund policies, and no hidden fees.",
            },
            {
              icon: "⚡",
              title: "Efficiency",
              desc: "Every feature is designed to save time — for drivers and operators.",
            },
            {
              icon: "🔒",
              title: "Security",
              desc: "Enterprise-grade data protection compliant with Vietnamese law.",
            },
            {
              icon: "🌱",
              title: "Sustainability",
              desc: "Reducing idle driving reduces emissions. Smart parking is green parking.",
            },
          ].map((v) => (
            <div key={v.title} className="card">
              <div style={{ color: 'var(--primary)', marginBottom: "0.5rem" }}>
                <UntitledIcon name={v.icon} size={24} />
              </div>
              <div
                style={{
                  fontFamily: "Outfit",
                  fontWeight: 600,
                  fontSize: "0.9rem",
                  color: "var(--fg)",
                  marginBottom: "0.375rem",
                }}
              >
                {v.title}
              </div>
              <div
                style={{
                  fontSize: "0.82rem",
                  color: "var(--muted)",
                  lineHeight: 1.6,
                }}
              >
                {v.desc}
              </div>
            </div>
          ))}
        </div>
      </section>

      <section>
        <h2
          style={{
            fontFamily: "Outfit",
            fontWeight: 700,
            fontSize: "1.3rem",
            color: "var(--fg)",
            marginBottom: "1rem",
            borderLeft: "3px solid var(--primary)",
            paddingLeft: "0.75rem",
          }}
        >
          Headquarters
        </h2>
        <div className="card">
          <p style={{ color: "var(--muted)", fontSize: "0.9rem", margin: 0 }}>
            <UntitledIcon name="map-pin" size={16} /> Tòa nhà Smart Hub, 17 Lê Duẩn, Phường Bến Nghé, Quận 1, TP. Hồ
            Chí Minh, Việt Nam
            <br />
            <UntitledIcon name="mail" size={16} /> support.smartparkingvn@gmail.com &nbsp;|&nbsp; <UntitledIcon name="phone" size={16} /> 1900-xxxx
          </p>
        </div>
      </section>
    </div>
  )
}
