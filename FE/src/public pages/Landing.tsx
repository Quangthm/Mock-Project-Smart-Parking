import { PageHeader } from "../components/ui/PageHeader"
import { Button } from "../components/ui/Button"
import { useLayoutEffect, useRef, useState } from "react"

import { useApp } from "../context/AppContext"

import { UntitledIcon } from "../components/icon/UntitledIcon"

import visaLogo from "../assets/payment-visa.webp"

import applePayLogo from "../assets/Apple Pay.png"

import momoLogo from "../assets/Momo.jpg"

import vnpayLogo from "../assets/payment-vnpay.png"

import zalopayLogo from "../assets/payment-zalopay.jpg"

import qrLogo from "../assets/payment-qr.png"

const STEPS = [
  {
    num: "01",

    label: "Find a Lot",

    icon: "search",

    desc: "Search nearby parking lots on the map by your current location.",
  },

  {
    num: "02",

    label: "Choose a Slot",

    icon: "parking",

    desc: "View real-time slot availability and pick the best spot.",
  },

  {
    num: "03",

    label: "Book",

    icon: "clipboard",

    desc: "Enter your license plate and confirm your booking details.",
  },

  {
    num: "04",

    label: "Pay",

    icon: "credit-card",

    desc: "Secure payment with OTP verification via SMS or email.",
  },

  {
    num: "05",

    label: "Check-In",

    icon: "check-circle",

    desc: "Show your booking QR code at the barrier to enter.",
  },

  {
    num: "06",

    label: "Check-Out",

    icon: "car",

    desc: "Drive out seamlessly. Invoice sent to your email and SMS.",
  },
]

const BENEFITS = [
  {
    icon: "zap",

    title: "Save Time",

    desc: "Find and book a spot in under 60 seconds. No more circling the block.",
  },

  {
    icon: "lock",

    title: "Transparent Payments",

    desc: "Clear refund policy shown before booking. Multiple payment methods supported.",
  },

  {
    icon: "chart",

    title: "Maximize Revenue",

    desc: "Automated cash flow management. Track your lot performance anytime, anywhere.",
  },

  {
    icon: "settings",

    title: "Streamlined Ops",

    desc: "100% digital in/out process. Automatic alerts for violations via Red/Amber system.",
  },
]

const PAYMENT_METHODS = [
  { name: "Visa / Mastercard", logo: visaLogo },

  { name: "Apple Pay", logo: applePayLogo },

  { name: "MoMo", logo: momoLogo },

  { name: "VNPAY", logo: vnpayLogo },

  { name: "ZaloPay", logo: zalopayLogo },

  { name: "QR Code", logo: qrLogo },
]

const CAROUSEL_SLIDES = [
  {
    img: "https://images.unsplash.com/photo-1506521781263-d8422e82f27a?w=900&h=500&fit=crop&auto=format",

    title: "Smart Parking for Modern Cities",

    sub: "Real-time slot booking across multi-storey, basement & outdoor lots.",
  },

  {
    img: "https://images.unsplash.com/photo-1732194516739-9325055c35de?w=900&h=500&fit=crop&auto=format",

    title: "Instant Reservation. Zero Hassle.",

    sub: "Find, book, and pay in seconds — from any device, anywhere.",
  },

  {
    img: "https://images.unsplash.com/photo-1486325212027-8081e485255e?w=900&h=500&fit=crop&auto=format",

    title: "Revenue Insights for Lot Owners",

    sub: "Live dashboards, operator management and automated billing in one platform.",
  },

  {
    img: "https://images.unsplash.com/photo-1590674899484-d5640e854abe?w=900&h=500&fit=crop&auto=format",

    title: "AI-Powered Vehicle Recognition",

    sub: "Automatic license plate detection for seamless check-in and check-out.",
  },
]

function HeroCarousel() {
  const [current, setCurrent] = useState(0)

  function goTo(idx: number) {
    setCurrent(idx)
  }

  const slide = CAROUSEL_SLIDES[current]

  return (
    <div
      className="hero-carousel"
      style={{
        position: "relative",

        borderRadius: "14px",

        overflow: "hidden",

        border: "1px solid var(--border)",

        background: "#0b1628",
      }}
    >
      <img
        key={current}
        src={slide.img}
        alt={slide.title}
        width={900}
        height={500}
        style={{
          width: "100%",

          height: "340px",

          objectFit: "cover",

          opacity: 0.7,
        }}
      />
      <div
        style={{
          position: "absolute",

          inset: 0,

          background:
            "linear-gradient(to top, rgba(11,22,40,0.92) 0%, rgba(11,22,40,0.3) 60%, transparent 100%)",

          display: "flex",

          flexDirection: "column",

          justifyContent: "flex-end",

          padding: "1.5rem",
        }}
      >
        <h3
          style={{
            fontFamily: "Be Vietnam Pro",

            fontWeight: 700,

            fontSize: "1.15rem",

            color: "#fff",

            margin: "0 0 0.375rem",

          }}
        >
          {slide.title}
        </h3>
        <p
          style={{
            color: "rgba(255,255,255,0.78)",

            fontSize: "0.85rem",

            margin: "0 0 1rem",

            lineHeight: 1.5,
          }}
        >
          {slide.sub}
        </p>
        <div className="carousel-controls">
          {CAROUSEL_SLIDES.map((_, i) => (
            <button
              key={i}
              type="button"
              className="carousel-control"
              aria-label={`Show slide ${i + 1}: ${CAROUSEL_SLIDES[i].title}`}
              aria-current={i === current ? "true" : undefined}
              onClick={() => goTo(i)}
            />
          ))}
        </div>
        <a className="hero-photo-credit" href={slide.img} target="_blank" rel="noreferrer">Image source: Unsplash</a>
      </div>
    </div>
  )
}

export function Landing() {
  const { setView } = useApp()
  const landingRef = useRef<HTMLDivElement>(null)

  useLayoutEffect(() => {
    const root = landingRef.current
    if (!root) return
    const elements = Array.from(root.querySelectorAll<HTMLElement>(".landing-reveal"))
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches || !("IntersectionObserver" in window)) {
      elements.forEach((element) => element.classList.add("is-visible"))
      return
    }
    const observer = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          entry.target.classList.add("is-visible")
          observer.unobserve(entry.target)
        }
      })
    }, { threshold: 0.12, rootMargin: "0px 0px -8% 0px" })
    root.classList.add("landing-motion-ready")
    elements.forEach((element) => observer.observe(element))
    return () => observer.disconnect()
  }, [])

  return (
    <div ref={landingRef} className="landing-page">
      {/* Hero */}
      <section className="smartparking-hero">
        <div className="landing-hero-enter">
          <PageHeader variant="hero" align="start" title={<>Park Smart.
            <br />
            <span style={{ color: "var(--primary)" }}>Book Easy.</span>
            <br />
            Manage Better.</>} description={<>Find, book, and pay for parking in seconds. For lot owners —
            automate operations and maximize revenue with real-time analytics.</>} />
          <div className="hero-actions">
            <Button
              variant="primary"
              trailingIcon={<UntitledIcon name="arrow-right" size={18} />}
              style={{ fontSize: "1rem", padding: "0.75rem 1.75rem" }}
              onClick={() => setView("sign-in")}
            >
              Book a Spot
            </Button>
            <Button
              variant="outline"
              style={{ fontSize: "1rem", padding: "0.75rem 1.75rem" }}
              onClick={() => setView("business")}
            >
              Become a Partner
            </Button>
          </div>
          <div className="hero-facts">
            {[
              ["Find", "Nearby spaces"],
              ["Book", "Your spot"],
              ["Go", "QR or plate entry"],
            ].map(([v, l]) => (
              <div key={l}>
                <div
                  style={{
                    fontFamily: "Be Vietnam Pro",

                    fontSize: "1.4rem",

                    fontWeight: 700,

                    color: "var(--primary)",
                  }}
                >
                  {v}
                </div>
                <div style={{ fontSize: "0.8rem", color: "var(--muted)" }}>
                  {l}
                </div>
              </div>
            ))}
          </div>
        </div>
        <div className="landing-hero-enter landing-hero-enter-image">
          <HeroCarousel />
        </div>
      </section>

      {/* Benefits */}
      <section
        style={{
          background: "var(--card)",

          borderTop: "1px solid var(--border)",

          borderBottom: "1px solid var(--border)",

          padding: "4rem 1.5rem",
        }}
      >
        <div style={{ maxWidth: "1200px", margin: "0 auto" }}>
          <div className="landing-reveal" data-motion="rise" style={{ textAlign: "center", marginBottom: "3rem" }}>
            <h2 className="section-title">Why SmartParking?</h2>
            <p
              className="section-sub"
              style={{ maxWidth: "520px", margin: "0.75rem auto 0" }}
            >
              Built for drivers who value their time and owners who want
              results.
            </p>
          </div>
          <div
            style={{
              display: "grid",

              gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))",

              gap: "1.5rem",
            }}
          >
            {BENEFITS.map((b, index) => (
              <div
                key={b.title}
                className={`card landing-reveal landing-reveal-delay-${index % 4}`}
                data-motion="rise"
                style={{ border: "1px solid var(--border)" }}
              >
                <div
                  style={{ color: "var(--primary)", marginBottom: "0.875rem" }}
                >
                  <UntitledIcon name={b.icon} size={24} />
                </div>
                <h3
                  style={{
                    fontFamily: "Be Vietnam Pro",

                    fontWeight: 600,

                    fontSize: "1.05rem",

                    marginBottom: "0.5rem",

                    color: "var(--fg)",
                  }}
                >
                  {b.title}
                </h3>
                <p
                  style={{
                    fontSize: "0.875rem",

                    color: "var(--muted)",

                    lineHeight: 1.6,
                  }}
                >
                  {b.desc}
                </p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* How it works */}
      <section
        style={{ maxWidth: "1200px", margin: "0 auto", padding: "4rem 1.5rem" }}
      >
        <div className="landing-reveal" data-motion="rise" style={{ textAlign: "center", marginBottom: "3rem" }}>
          <h2 className="section-title">How It Works</h2>
          <p className="section-sub">
            Six simple steps from search to checkout.
          </p>
        </div>
        <div
          style={{
            display: "grid",

            gridTemplateColumns: "repeat(auto-fit, minmax(160px, 1fr))",

            gap: "1rem",
          }}
        >
          {STEPS.map((step, index) => (
            <div
              key={step.num}
              className={`card landing-reveal landing-reveal-delay-${index % 3}`}
              data-motion="rise"
              style={{ textAlign: "center", padding: "1.5rem 1rem" }}
            >
              <div
                style={{
                  fontFamily: "Be Vietnam Pro",

                  fontSize: "0.875rem",

                  fontWeight: 700,

                  color: "var(--primary)",


                  marginBottom: "0.75rem",
                }}
              >
                Step {Number(step.num)}
              </div>
              <div style={{ color: "var(--primary)", marginBottom: "0.5rem" }}>
                <UntitledIcon name={step.icon} size={24} />
              </div>
              <div
                style={{
                  fontFamily: "Be Vietnam Pro",

                  fontWeight: 600,

                  fontSize: "0.95rem",

                  color: "var(--fg)",

                  marginBottom: "0.5rem",
                }}
              >
                {step.label}
              </div>
              <div
                style={{
                  fontSize: "0.78rem",

                  color: "var(--muted)",

                  lineHeight: 1.5,
                }}
              >
                {step.desc}
              </div>
            </div>
          ))}
        </div>
      </section>

      {/* Payment Methods */}
      <section
        style={{
          background: "var(--card)",

          borderTop: "1px solid var(--border)",

          borderBottom: "1px solid var(--border)",

          padding: "4rem 1.5rem",
        }}
      >
        <div
          className="landing-reveal"
          data-motion="rise"
          style={{ maxWidth: "900px", margin: "0 auto", textAlign: "center" }}
        >
          <h2 className="section-title" style={{ marginBottom: "0.75rem" }}>
            Supported Payment Methods
          </h2>
          <p className="section-sub" style={{ marginBottom: "2.5rem" }}>
            Secure payments — transparent deposit &amp; refund policy.
          </p>
          <div
            style={{
              display: "grid",

              gridTemplateColumns: "repeat(3, 1fr)",

              gap: "1rem",

              maxWidth: "600px",

              margin: "0 auto 1.5rem",
            }}
          >
            {PAYMENT_METHODS.map((pm, index) => (
              <div
                key={pm.name}
                className={`card landing-reveal landing-reveal-delay-${index % 3}`}
                data-motion="scale"
                style={{
                  display: "flex",

                  flexDirection: "column",

                  alignItems: "center",

                  gap: "0.5rem",

                  padding: "1.25rem",

                  border: "1px solid var(--border)",
                }}
              >
                <img
                  src={pm.logo}
                  alt={pm.name}
                  style={{
                    height: "36px",

                    objectFit: "contain",

                    maxWidth: "90px",
                  }}
                />
                <span
                  style={{
                    fontSize: "0.82rem",

                    color: "var(--fg)",

                    fontWeight: 600,
                  }}
                >
                  {pm.name}
                </span>
              </div>
            ))}
          </div>
          <p style={{ fontSize: "0.875rem", color: "var(--muted)" }}>
            <UntitledIcon name="shield" size={16} /> All transactions are
            encrypted. Deposits are refundable per our clear refund policy.
          </p>
        </div>
      </section>

      {/* CTA */}
      <section
        className="landing-reveal"
        data-motion="rise"
        style={{
          maxWidth: "800px",

          margin: "0 auto",

          padding: "5rem 1.5rem",

          textAlign: "center",
        }}
      >
        <h2 className="section-title" style={{ marginBottom: "1rem" }}>
          Ready to Park Smarter?
        </h2>
        <p className="section-sub" style={{ marginBottom: "2rem" }}>
          Join thousands of drivers and lot owners on the SmartParking platform.
        </p>
        <div
          style={{
            display: "flex",

            gap: "1rem",

            justifyContent: "center",

            flexWrap: "wrap",
          }}
        >
          <Button
            variant="primary"
            trailingIcon={<UntitledIcon name="arrow-right" size={18} />}
            style={{ fontSize: "1rem", padding: "0.875rem 2rem" }}
            onClick={() => setView("sign-up")}
          >
            Get Started
          </Button>
          <Button
            variant="outline"
            style={{ fontSize: "1rem", padding: "0.875rem 2rem" }}
            onClick={() => setView("pricing")}
          >
            View Pricing
          </Button>
        </div>
      </section>
    </div>
  )
}
