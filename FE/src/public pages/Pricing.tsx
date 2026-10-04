import { useApp } from '../context/AppContext';
import { UntitledIcon } from '../components/icon/UntitledIcon';

const PLANS = [
  {
    name: 'Driver — Pay as You Go',
    price: 'From 15,000₫/hr',
    period: 'per session',
    color: '#22c55e',
    features: [
      'Real-time slot availability',
      'Book up to 7 days in advance',
      'All payment methods',
      'Digital invoice via SMS & email',
      'Free cancellation (30 min before)',
    ],
    cta: 'Start Booking',
    role: 'sign-up',
  },
  {
    name: 'Driver — Monthly Pass',
    price: '800,000₫',
    period: 'per month',
    color: '#2563eb',
    badge: 'Popular',
    features: [
      'Unlimited bookings at partner lots',
      'Reserved priority slot',
      'No per-hour charges',
      'Digital monthly invoice',
      'Early access to new lots',
    ],
    cta: 'Subscribe Now',
    role: 'sign-up',
  },
  {
    name: 'Owner — Partnership',
    price: 'Revenue Share',
    period: 'per booking',
    color: '#f59e0b',
    features: [
      'List unlimited parking lots',
      'AI camera & sensor integration',
      'Automated revenue reports',
      'Operator account management',
      'Priority customer support',
      'Custom pricing policies',
    ],
    cta: 'Become a Partner',
    role: 'sign-up',
  },
];

export function Pricing() {
  const { setView } = useApp();
  return (
    <div style={{ maxWidth: '1100px', margin: '0 auto', padding: '3rem 1.5rem' }}>
      <div style={{ textAlign: 'center', marginBottom: '3rem' }}>
        <h1 className="section-title">Transparent Pricing</h1>
        <p className="section-sub" style={{ maxWidth: '480px', margin: '0.75rem auto 0' }}>
          No hidden fees. Clear refund policies. Pay only for what you use.
        </p>
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: '1.5rem', marginBottom: '3rem' }}>
        {PLANS.map(plan => (
          <div key={plan.name} className="card" style={{ border: `2px solid ${plan.color}`, position: 'relative' }}>
            {plan.badge && (
              <div style={{ position: 'absolute', top: '-12px', left: '50%', transform: 'translateX(-50%)', background: plan.color, color: '#fff', fontSize: '0.75rem', fontWeight: 700, padding: '0.2rem 0.75rem', borderRadius: '999px' }}>
                {plan.badge}
              </div>
            )}
            <div style={{ marginBottom: '1rem' }}>
              <div style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '1rem', color: plan.color, marginBottom: '0.375rem' }}>{plan.name}</div>
              <div style={{ fontFamily: 'Outfit', fontWeight: 800, fontSize: '1.75rem', color: 'var(--fg)' }}>{plan.price}</div>
              <div style={{ fontSize: '0.82rem', color: 'var(--muted)' }}>{plan.period}</div>
            </div>
            <ul style={{ listStyle: 'none', padding: 0, margin: '0 0 1.5rem', display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
              {plan.features.map(f => (
                <li key={f} style={{ display: 'flex', alignItems: 'flex-start', gap: '0.5rem', fontSize: '0.875rem', color: 'var(--fg)' }}>
                  <span style={{ color: plan.color, display: 'inline-flex', flexShrink: 0 }}><UntitledIcon name="check" size={16} /></span> {f}
                </li>
              ))}
            </ul>
            <button
              className="btn-primary"
              style={{ width: '100%', background: plan.color, border: 'none' }}
              onClick={() => setView(plan.role as any)}
            >
              {plan.cta}
            </button>
          </div>
        ))}
      </div>

      <div className="card" style={{ maxWidth: '700px', margin: '0 auto' }}>
        <h3 style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '1.1rem', marginBottom: '1rem', color: 'var(--fg)' }}>Refund Policy Summary</h3>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.625rem' }}>
          {[
            { label: 'Cancel 30+ min before booking', value: '100% deposit refund', color: '#22c55e' },
            { label: 'Cancel 15–30 min before booking', value: '50% deposit refund', color: '#f59e0b' },
            { label: 'Cancel < 15 min before or no-show', value: 'No refund', color: '#ef4444' },
          ].map(row => (
            <div key={row.label} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '0.625rem 0.875rem', background: `${row.color}12`, borderRadius: 'var(--radius)', border: `1px solid ${row.color}30` }}>
              <span style={{ fontSize: '0.875rem', color: 'var(--fg)' }}>{row.label}</span>
              <span style={{ fontWeight: 700, color: row.color, fontSize: '0.875rem' }}>{row.value}</span>
            </div>
          ))}
        </div>
        <p style={{ fontSize: '0.8rem', color: 'var(--muted)', marginTop: '1rem' }}>
          Full details available in our{' '}
          <button onClick={() => setView('payment-refund')} style={{ background: 'none', border: 'none', color: 'var(--primary)', cursor: 'pointer', padding: 0, fontSize: '0.8rem', textDecoration: 'underline' }}>
            Payment & Refund Policy
          </button>.
        </p>
      </div>
    </div>
  );
}
