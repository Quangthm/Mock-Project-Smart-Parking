import { useApp } from "../../context/AppContext"

import { BrandLogo } from "../brand/BrandLogo"

import { UntitledIcon } from "../icon/UntitledIcon"

export function Footer() {
  const { setView } = useApp()

  return (
    <footer
      style={{
        background: "var(--card)",
        borderTop: "1px solid var(--border)",
        marginTop: "5rem",
      }}
    >
      <div
        style={{
          maxWidth: "1200px",
          margin: "0 auto",
          padding: "3rem 1.5rem 2rem",
        }}
      >
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))",
            gap: "2rem",
            marginBottom: "2.5rem",
          }}
        >
          <div>
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: "0.5rem",
                marginBottom: "1rem",
              }}
            >
              <BrandLogo height={60} />
              <span className="brand-wordmark" style={{ fontSize: "1.05rem" }}>
                <span className="brand-smart">Smart</span>
                <span className="brand-parking">Parking</span>
              </span>
            </div>
            <p
              style={{
                fontSize: "0.82rem",
                color: "var(--muted)",
                lineHeight: 1.6,
              }}
            >
              Smart parking solutions for drivers and businesses. Find, book,
              and manage parking with ease.
            </p>
            <div style={{ display: "flex", gap: "0.75rem", marginTop: "1rem" }}>
              {[
                { icon: "f", label: "Facebook" },
                { icon: "in", label: "LinkedIn" },
                { icon: "▶", label: "YouTube" },
              ].map((s) => (
                <button
                  key={s.label}
                  title={s.label}
                  style={{
                    width: 30,
                    height: 30,
                    border: "1px solid var(--border)",
                    borderRadius: "50%",
                    background: "none",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    fontSize: "0.7rem",
                    fontWeight: 700,
                    color: "var(--muted)",
                    cursor: "pointer",
                    transition: "border-color 0.15s, color 0.15s",
                  }}
                >
                  {s.icon}
                </button>
              ))}
            </div>
          </div>

          <div>
            <h4
              style={{
                fontFamily: "Outfit",
                fontWeight: 600,
                fontSize: "0.9rem",
                marginBottom: "1rem",
                color: "var(--fg)",
              }}
            >
              Information
            </h4>
            <FooterLinks
              links={[
                { label: "About Us", view: "about" },

                { label: "Affiliates", view: "affiliates" },

                { label: "Careers", view: "careers" },
              ]}
            />
          </div>

          <div>
            <h4
              style={{
                fontFamily: "Outfit",
                fontWeight: 600,
                fontSize: "0.9rem",
                marginBottom: "1rem",
                color: "var(--fg)",
              }}
            >
              For Business
            </h4>
            <FooterLinks
              links={[
                { label: "Become a Partner", view: "business" },

                { label: "Business Accounts", view: "business" },
              ]}
            />
          </div>

          <div>
            <h4
              style={{
                fontFamily: "Outfit",
                fontWeight: 600,
                fontSize: "0.9rem",
                marginBottom: "1rem",
                color: "var(--fg)",
              }}
            >
              Support
            </h4>
            <FooterLinks
              links={[
                { label: "FAQ", view: "faq" },

                { label: "Contact", view: "support" },

                { label: "Report an Issue", view: "support" },
              ]}
            />
          </div>

          <div>
            <h4
              style={{
                fontFamily: "Outfit",
                fontWeight: 600,
                fontSize: "0.9rem",
                marginBottom: "1rem",
                color: "var(--fg)",
              }}
            >
              Contact
            </h4>
            <div
              style={{
                fontSize: "0.82rem",
                color: "var(--muted)",
                display: "flex",
                flexDirection: "column",
                gap: "0.5rem",
              }}
            >
              <span>
                <UntitledIcon name="map-pin" size={15} /> Tòa nhà Smart Hub, 17
                Lê Duẩn, Q.1, TP.HCM
              </span>
              <span>
                <UntitledIcon name="phone" size={15} /> Driver: 1900-xxxx
              </span>
              <span>
                <UntitledIcon name="phone" size={15} /> Business: 090xxxxxxx
              </span>
              <span>
                <UntitledIcon name="mail" size={15} />{" "}
                support.smartparkingvn@gmail.com
              </span>
            </div>
          </div>
        </div>

        <div
          style={{
            borderTop: "1px solid var(--border)",
            paddingTop: "1.5rem",
            display: "flex",
            flexWrap: "wrap",
            justifyContent: "space-between",
            gap: "0.75rem",
            alignItems: "center",
          }}
        >
          <p style={{ fontSize: "0.8rem", color: "var(--muted)", margin: 0 }}>
            © 2026 Smart Parking. All rights reserved.
          </p>
          <div style={{ display: "flex", gap: "1.25rem" }}>
            {[
              { label: "Terms of Service", view: "terms" as const },

              { label: "Privacy Policy", view: "privacy" as const },

              { label: "Payment & Refund", view: "payment-refund" as const },
            ].map((p) => (
              <button
                key={p.label}
                onClick={() => setView(p.view)}
                style={{
                  background: "none",
                  border: "none",
                  cursor: "pointer",
                  fontSize: "0.8rem",
                  color: "var(--muted)",
                  padding: 0,
                  textDecoration: "underline",
                }}
              >
                {p.label}
              </button>
            ))}
          </div>
        </div>
      </div>
    </footer>
  )
}

function FooterLinks({ links }: { links: { label: string view: string }[] }) {
  const { setView } = useApp()

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
      {links.map((l) => (
        <button
          key={l.label}
          onClick={() => setView(l.view as any)}
          style={{
            background: "none",
            border: "none",
            cursor: "pointer",
            fontSize: "0.85rem",
            color: "var(--muted)",
            padding: 0,
            textAlign: "left",
            transition: "color 0.15s",
          }}
          onMouseEnter={(e) => (e.currentTarget.style.color = "var(--primary)")}
          onMouseLeave={(e) => (e.currentTarget.style.color = "var(--muted)")}
        >
          {l.label}
        </button>
      ))}
    </div>
  )
}
