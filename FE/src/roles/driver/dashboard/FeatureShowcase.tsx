import { UntitledIcon } from "../../../components/icon/UntitledIcon"

const features = [
  { icon: "map", title: "Find Nearby Lots", description: "See nearby parking sites and current availability." },
  { icon: "parking", title: "Choose a Space", description: "Compare available spaces and reserve one." },
  { icon: "credit-card", title: "Pay Securely", description: "Review the amount and choose a payment method." },
  { icon: "ticket", title: "Enter the Lot", description: "Use your booking QR code or registered license plate." },
  { icon: "clipboard", title: "Review History", description: "Find current and past bookings in one place." },
  { icon: "life-buoy", title: "Get Support", description: "Report an issue or request help with a booking." },
]

export function FeatureShowcase() {
  return (
    <section className="feature-guide" aria-labelledby="feature-guide-title">
      <div className="feature-guide-heading">
        <h2 id="feature-guide-title">Your parking journey</h2>
        <p>From search to entry, each step stays in your account.</p>
      </div>
      <ol className="feature-guide-list">
        {features.map((feature, index) => (
          <li key={feature.title}>
            <span className="feature-guide-number">{index + 1}</span>
            <UntitledIcon name={feature.icon} size={19} />
            <div>
              <strong>{feature.title}</strong>
              <p>{feature.description}</p>
            </div>
          </li>
        ))}
      </ol>
    </section>
  )
}
