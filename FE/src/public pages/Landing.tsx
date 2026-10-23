import { lazy, Suspense, useLayoutEffect, useRef } from "react"
import { motion, useReducedMotion } from "motion/react"
import { Button } from "../components/ui/Button"
import { UntitledIcon } from "../components/icon/UntitledIcon"
import { useApp } from "../context/AppContext"
import visaLogo from "../assets/payment-visa.webp"
import applePayLogo from "../assets/Apple Pay.png"
import momoLogo from "../assets/Momo.jpg"
import vnpayLogo from "../assets/payment-vnpay.png"
import zalopayLogo from "../assets/payment-zalopay.jpg"
import qrLogo from "../assets/payment-qr.png"

const ParkingHero3D = lazy(() => import("../components/landing/ParkingHero3D").then(module => ({ default: module.ParkingHero3D })))

const STEPS = [
  { num: "01", label: "Find a lot", icon: "search", desc: "Search nearby parking lots on the map by your current location." },
  { num: "02", label: "Choose a slot", icon: "parking", desc: "View real-time slot availability and pick the best spot." },
  { num: "03", label: "Book", icon: "clipboard", desc: "Enter your license plate and confirm your booking details." },
  { num: "04", label: "Pay", icon: "credit-card", desc: "Secure payment with OTP verification via SMS or email." },
  { num: "05", label: "Check in", icon: "check-circle", desc: "Show your booking QR code at the barrier to enter." },
  { num: "06", label: "Check out", icon: "car", desc: "Drive out when you're ready. Your invoice is sent by email and SMS." },
]

const BENEFITS = [
  { icon: "zap", title: "More time for the road", desc: "See nearby options and reserve a space before you arrive." },
  { icon: "lock", title: "Clear payments", desc: "Review your booking and payment details in one place." },
  { icon: "chart", title: "One view of your lots", desc: "Owners can follow occupancy and revenue from their dashboard." },
  { icon: "settings", title: "Smoother operations", desc: "Operators can manage arrivals, spaces, and incidents as they happen." },
]

const PAYMENT_METHODS = [
  { name: "Visa / Mastercard", logo: visaLogo },
  { name: "Apple Pay", logo: applePayLogo },
  { name: "MoMo", logo: momoLogo },
  { name: "VNPAY", logo: vnpayLogo },
  { name: "ZaloPay", logo: zalopayLogo },
  { name: "QR Code", logo: qrLogo },
]

export function Landing() {
  const { setView } = useApp()
  const landingRef = useRef<HTMLDivElement>(null)
  const reducedMotion = useReducedMotion()

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
    }, { threshold: .12, rootMargin: "0px 0px -7% 0px" })
    root.classList.add("landing-motion-ready")
    elements.forEach((element) => observer.observe(element))
    return () => observer.disconnect()
  }, [])

  return (
    <div ref={landingRef} className="landing-page">
      <section className="smartparking-hero">
        <motion.div
          className="strata-hero-copy"
          initial={reducedMotion ? false : { opacity: 0, y: 22 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: .7, ease: [.2, .7, .2, 1] }}
        >
          <h1>Park Smart <em> Book Easy</em> Manage Better</h1>
          <p>Find a space, book it, and arrive with confidence. SmartParking brings drivers and parking teams together in one clear flow.</p>
          <div className="hero-actions">
            <Button variant="primary" trailingIcon={<UntitledIcon name="arrow-right" size={18} />} onClick={() => setView("sign-in")}>Find parking</Button>
            <Button variant="outline" onClick={() => setView("business")}>For parking partners</Button>
          </div>
          <div className="hero-facts" aria-label="How SmartParking works">
            <div><strong>01</strong><span>Discover nearby</span></div>
            <div><strong>02</strong><span>Reserve a space</span></div>
            <div><strong>03</strong><span>Arrive and park</span></div>
          </div>
        </motion.div>
        <Suspense fallback={<div className="parking-hero-3d" role="img" aria-label="SmartParking parking building and car illustration"><div className="parking-hero-3d-fallback" aria-hidden="true"><span /><span /><span /></div></div>}><ParkingHero3D /></Suspense>
      </section>

      <section className="landing-section landing-benefits">
        <div className="landing-section-heading landing-reveal">
          <span className="landing-kicker">ONE PLATFORM, EVERY JOURNEY</span>
          <h2>Parking that works<br />for everyone.</h2>
          <p>A calmer way to park, with the tools to run each lot well.</p>
        </div>
        <div className="landing-benefit-grid">
          {BENEFITS.map((benefit, index) => (
            <article key={benefit.title} className={`landing-benefit landing-reveal landing-reveal-delay-${index % 4}`}>
              <span className="landing-benefit-icon"><UntitledIcon name={benefit.icon} size={24} /></span>
              <span className="landing-benefit-index">0{index + 1}</span>
              <h3>{benefit.title}</h3>
              <p>{benefit.desc}</p>
            </article>
          ))}
        </div>
      </section>

      <section className="landing-section landing-journey">
        <div className="landing-section-heading landing-reveal">
          <span className="landing-kicker">FROM ARRIVAL TO EXIT</span>
          <h2>One easy route through parking.</h2>
          <p>Every step is connected, so you always know what comes next.</p>
        </div>
        <div className="landing-journey-grid">
          {STEPS.map((step, index) => (
            <article key={step.num} className={`landing-step landing-reveal landing-reveal-delay-${index % 3}`}>
              <div className="landing-step-top"><span>{step.num}</span><UntitledIcon name={step.icon} size={22} /></div>
              <h3>{step.label}</h3><p>{step.desc}</p>
            </article>
          ))}
        </div>
      </section>

      <section className="landing-section landing-payment">
        <div className="landing-section-heading landing-reveal">
          <span className="landing-kicker">PAY YOUR WAY</span>
          <h2>Familiar ways to pay.</h2>
          <p>Review payment and refund details before you confirm a booking.</p>
        </div>
        <div className="landing-payment-grid">
          {PAYMENT_METHODS.map((method, index) => (
            <div key={method.name} className={`landing-payment-method landing-reveal landing-reveal-delay-${index % 3}`}>
              <img src={method.logo} alt="" loading="lazy" /><span>{method.name}</span>
            </div>
          ))}
        </div>
      </section>

      <section className="landing-final landing-reveal">
        <span className="landing-kicker">YOUR SPACE IS OUT THERE</span>
        <h2>Ready when you are.</h2>
        <p>Start your next trip with one less thing to think about.</p>
        <div className="landing-final-actions">
          <Button variant="primary" trailingIcon={<UntitledIcon name="arrow-right" size={18} />} onClick={() => setView("sign-up")}>Get started</Button>
          <Button variant="outline" onClick={() => setView("pricing")}>View pricing</Button>
        </div>
      </section>
    </div>
  )
}
