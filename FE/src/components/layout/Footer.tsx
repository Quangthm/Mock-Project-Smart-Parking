import { useApp } from "../../context/AppContext"

import { BrandLogo, BrandWordmark } from "../brand/BrandLogo"

import { UntitledIcon } from "../icon/UntitledIcon"

export function Footer() {
  const { setView } = useApp()

  return (
    <footer className="site-footer"
      style={{
        background: "var(--card)",
        borderTop: "1px solid var(--border)",
        marginTop: "5rem",
      }}
    >
      <div className="site-footer-inner"
        style={{
          maxWidth: "1200px",
          margin: "0 auto",
          padding: "3rem 1.5rem 2rem",
        }}
      >
        <div className="site-footer-columns"
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))",
            gap: "2rem",
            marginBottom: "2.5rem",
          }}
        >
          <div className="site-footer-brand">
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: "0.5rem",
                marginBottom: "1rem",
              }}
            >
              <BrandLogo height={48} />
              <BrandWordmark height={21} />
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
          </div>

          <div className="site-footer-group">
            <h4
              style={{
                fontFamily: "Be Vietnam Pro",
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

          <div className="site-footer-group">
            <h4
              style={{
                fontFamily: "Be Vietnam Pro",
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

          <div className="site-footer-group">
            <h4
              style={{
                fontFamily: "Be Vietnam Pro",
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

          <div className="site-footer-group site-footer-contact">
            <h4
              style={{
                fontFamily: "Be Vietnam Pro",
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
                <UntitledIcon name="map-pin" size={15} /> Smart Hub Building, 17
                Le Duan Street, District 1, Ho Chi Minh City
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

        <div className="site-footer-bottom"
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
            This product is for educational purposes.
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

function FooterLinks({ links }: { links: { label: string; view: string }[] }) {
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
