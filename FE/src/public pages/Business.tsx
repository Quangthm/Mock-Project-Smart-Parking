import { useRef } from 'react';
import { useApp } from '../context/AppContext';

import { UntitledIcon } from '../components/icon/UntitledIcon';

const SOLUTIONS = [
  { icon: '🤖', title: 'AI-Powered Operations', desc: 'Automatic license plate recognition, real-time slot sensors and barrier control — reduce staffing costs and eliminate revenue leakage.' },
  { icon: '📊', title: 'Revenue Analytics Portal', desc: 'Live dashboards showing occupancy, peak hours, and monthly revenue. Compare performance across multiple lots from one screen.' },
  { icon: '📱', title: 'Booking Platform Integration', desc: 'Your lots appear on SmartParking\'s map and mobile app — reaching thousands of active drivers instantly.' },
  { icon: '👷', title: 'Operator Management', desc: 'Create and manage operator accounts for your staff. Track check-ins, incidents and performance from your Owner dashboard.' },
  { icon: '💳', title: 'Automated Billing', desc: 'All transactions processed digitally. Monthly statements and invoices delivered automatically — no manual reconciliation.' },
  { icon: '🔒', title: 'Compliance & Security', desc: 'All data processing complies with Nghị định 13/2023/NĐ-CP. 24/7 system monitoring and 98% uptime SLA guarantee.' },
];

const BENEFITS = [
  { stat: '15%', label: 'Platform Fee Only', desc: 'You keep 85% of every booking — no hidden charges, no setup cost.' },
  { stat: '3×', label: 'Revenue Uplift', desc: 'Partners report 3× revenue improvement in the first 6 months.' },
  { stat: '98%', label: 'Uptime SLA', desc: 'Enterprise-grade infrastructure with guaranteed availability.' },
  { stat: '2–3', label: 'Days to Launch', desc: 'From application approved to live bookings in under 72 hours.' },
];

function Calculator() {
  const slots = useRef<HTMLInputElement>(null);
  const rate = useRef<HTMLInputElement>(null);
  const hours = useRef<HTMLInputElement>(null);
  const occupancy = useRef<HTMLInputElement>(null);
  const result = useRef<HTMLDivElement>(null);

  function calculate() {
    const s = parseInt(slots.current?.value || '0');
    const r = parseInt(rate.current?.value || '0');
    const h = parseFloat(hours.current?.value || '0');
    const occ = parseFloat(occupancy.current?.value || '0') / 100;
    const yearly = Math.round(s * r * h * occ * 365);
    const yourShare = Math.round(yearly * 0.85);
    const platform = Math.round(yearly * 0.15);
    if (result.current) {
      result.current.innerHTML = `
        <div style="display:grid;grid-template-columns:1fr 1fr 1fr;gap:1rem;margin-top:1rem">
          <div style="text-align:center;padding:1rem;background:var(--primary)10;border-radius:8px;border:1px solid var(--primary)30">
            <div style="font-family:Outfit;font-weight:800;font-size:1.3rem;color:var(--primary)">${yearly.toLocaleString('vi-VN')}₫</div>
            <div style="font-size:0.78rem;color:var(--muted)">Gross Revenue/Year</div>
          </div>
          <div style="text-align:center;padding:1rem;background:#22c55e10;border-radius:8px;border:1px solid #22c55e30">
            <div style="font-family:Outfit;font-weight:800;font-size:1.3rem;color:#22c55e">${yourShare.toLocaleString('vi-VN')}₫</div>
            <div style="font-size:0.78rem;color:var(--muted)">Your Share (85%)</div>
          </div>
          <div style="text-align:center;padding:1rem;background:var(--card);border-radius:8px;border:1px solid var(--border)">
            <div style="font-family:Outfit;font-weight:800;font-size:1.3rem;color:var(--muted)">${platform.toLocaleString('vi-VN')}₫</div>
            <div style="font-size:0.78rem;color:var(--muted)">Platform Fee (15%)</div>
          </div>
        </div>
      `;
    }
  }

  return (
    <div className="card" style={{ maxWidth: '700px', margin: '0 auto' }}>
      <h3 style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '1.15rem', color: 'var(--fg)', marginBottom: '0.375rem' }}>
        Calculate Your Fleet's Yearly Savings
      </h3>
      <p style={{ color: 'var(--muted)', fontSize: '0.875rem', marginBottom: '1.5rem' }}>
        Enter your lot details to estimate annual revenue when partnering with SmartParking.
      </p>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem', marginBottom: '1rem' }}>
        {[
          { ref: slots, label: 'Number of Slots', placeholder: '50', unit: 'slots' },
          { ref: rate, label: 'Hourly Rate (₫)', placeholder: '15000', unit: '₫/hr' },
          { ref: hours, label: 'Avg Hours Open/Day', placeholder: '12', unit: 'hrs' },
          { ref: occupancy, label: 'Expected Occupancy', placeholder: '60', unit: '%' },
        ].map(f => (
          <div key={f.label}>
            <label className="label">{f.label}</label>
            <div style={{ position: 'relative' }}>
              <input ref={f.ref} className="input" type="number" placeholder={f.placeholder} style={{ paddingRight: '2.5rem' }} />
              <span style={{ position: 'absolute', right: '0.75rem', top: '50%', transform: 'translateY(-50%)', fontSize: '0.75rem', color: 'var(--muted)' }}>{f.unit}</span>
            </div>
          </div>
        ))}
      </div>
      <button className="btn-primary" onClick={calculate} style={{ width: '100%', justifyContent: 'center' }}>
        Calculate Yearly Revenue →
      </button>
      <div ref={result} />
    </div>
  );
}

export function Business() {
  const { setView } = useApp();
  const contactRef = useRef<HTMLDivElement>(null);

  function scrollToContact() {
    contactRef.current?.scrollIntoView({ behavior: 'smooth' });
  }

  return (
    <div>
      {/* Hero */}
      <section style={{ background: 'var(--primary)', padding: '5rem 1.5rem', textAlign: 'center', position: 'relative', overflow: 'hidden' }}>
        <div style={{ position: 'absolute', inset: 0, backgroundImage: 'url(https://images.unsplash.com/photo-1590674899484-d5640e854abe?w=1400&h=600&fit=crop&auto=format)', backgroundSize: 'cover', backgroundPosition: 'center', opacity: 0.15 }} />
        <div style={{ position: 'relative', maxWidth: '800px', margin: '0 auto' }}>
          <div style={{ display: 'inline-flex', alignItems: 'center', gap: '0.5rem', background: 'rgba(255,255,255,0.15)', borderRadius: '999px', padding: '0.3rem 1rem', marginBottom: '1.5rem', border: '1px solid rgba(255,255,255,0.25)' }}>
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: '0.8rem', color: '#fff', fontWeight: 600 }}><UntitledIcon name="building" size={15} /> SmartParking for Business</span>
          </div>
          <h1 style={{ fontFamily: 'Outfit', fontWeight: 800, fontSize: 'clamp(2rem, 4vw, 3rem)', color: '#fff', lineHeight: 1.15, marginBottom: '1.25rem' }}>
            The Smart Way to Run<br />Your Parking Business
          </h1>
          <p style={{ color: 'rgba(255,255,255,0.82)', fontSize: '1.1rem', lineHeight: 1.7, marginBottom: '2rem', maxWidth: '560px', margin: '0 auto 2rem' }}>
            Join 50+ parking operators who've increased revenue, cut costs, and simplified operations with SmartParking's business platform.
          </p>
          <div style={{ display: 'flex', gap: '1rem', justifyContent: 'center', flexWrap: 'wrap' }}>
            <button onClick={() => { setView('sign-up'); }} className="btn-accent" style={{ fontSize: '1rem', padding: '0.875rem 2rem' }}>
              Register Your Business
            </button>
            <button onClick={scrollToContact} style={{ fontSize: '1rem', padding: '0.875rem 2rem', background: 'rgba(255,255,255,0.15)', border: '1.5px solid rgba(255,255,255,0.5)', color: '#fff', borderRadius: 'var(--radius)', cursor: 'pointer', fontWeight: 600 }}>
              Get in Touch
            </button>
          </div>
        </div>
      </section>

      {/* Social proof images */}
      <section style={{ maxWidth: '1200px', margin: '0 auto', padding: '3rem 1.5rem' }}>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '1rem' }}>
          {[
            { img: 'https://images.unsplash.com/photo-1506521781263-d8422e82f27a?w=400&h=240&fit=crop&auto=format', label: 'Multi-Storey Parking' },
            { img: 'https://images.unsplash.com/photo-1548345680-f5475ea5df84?w=400&h=240&fit=crop&auto=format', label: 'Basement Parking' },
            { img: 'https://images.unsplash.com/photo-1486325212027-8081e485255e?w=400&h=240&fit=crop&auto=format', label: 'Outdoor Lots' },
          ].map(item => (
            <div key={item.label} style={{ borderRadius: '10px', overflow: 'hidden', position: 'relative', border: '1px solid var(--border)' }}>
              <img src={item.img} alt={item.label} style={{ width: '100%', height: '180px', objectFit: 'cover', display: 'block' }} />
              <div style={{ position: 'absolute', bottom: 0, left: 0, right: 0, background: 'linear-gradient(to top, rgba(0,0,0,0.75), transparent)', padding: '1rem 0.875rem 0.625rem' }}>
                <span style={{ color: '#fff', fontSize: '0.85rem', fontWeight: 600 }}>{item.label}</span>
              </div>
            </div>
          ))}
        </div>
      </section>

      {/* Stats */}
      <section style={{ background: 'var(--card)', borderTop: '1px solid var(--border)', borderBottom: '1px solid var(--border)', padding: '3rem 1.5rem' }}>
        <div style={{ maxWidth: '900px', margin: '0 auto', display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '1.5rem', textAlign: 'center' }}>
          {BENEFITS.map(b => (
            <div key={b.label}>
              <div style={{ fontFamily: 'Outfit', fontWeight: 800, fontSize: '2.25rem', color: 'var(--primary)' }}>{b.stat}</div>
              <div style={{ fontFamily: 'Outfit', fontWeight: 600, fontSize: '0.95rem', color: 'var(--fg)', marginBottom: '0.25rem' }}>{b.label}</div>
              <div style={{ fontSize: '0.8rem', color: 'var(--muted)' }}>{b.desc}</div>
            </div>
          ))}
        </div>
      </section>

      {/* Solutions */}
      <section style={{ maxWidth: '1200px', margin: '0 auto', padding: '4rem 1.5rem' }}>
        <div style={{ textAlign: 'center', marginBottom: '3rem' }}>
          <h2 style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '1.85rem', color: 'var(--fg)', marginBottom: '0.75rem' }}>Products & Solutions</h2>
          <p style={{ color: 'var(--muted)', fontSize: '1rem', maxWidth: '520px', margin: '0 auto' }}>
            Everything you need to run a modern, profitable parking operation.
          </p>
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: '1.25rem' }}>
          {SOLUTIONS.map(s => (
            <div key={s.title} className="card" style={{ display: 'flex', gap: '1rem' }}>
              <div style={{ color: 'var(--primary)', flexShrink: 0, marginTop: '0.1rem' }}><UntitledIcon name={s.icon} size={24} /></div>
              <div>
                <div style={{ fontFamily: 'Outfit', fontWeight: 600, fontSize: '0.95rem', color: 'var(--fg)', marginBottom: '0.375rem' }}>{s.title}</div>
                <div style={{ fontSize: '0.85rem', color: 'var(--muted)', lineHeight: 1.6 }}>{s.desc}</div>
              </div>
            </div>
          ))}
        </div>
      </section>

      {/* Portal preview */}
      <section style={{ background: 'var(--card)', borderTop: '1px solid var(--border)', borderBottom: '1px solid var(--border)', padding: '4rem 1.5rem' }}>
        <div style={{ maxWidth: '1100px', margin: '0 auto', display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '3rem', alignItems: 'center' }}>
          <div>
            <h2 style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '1.75rem', color: 'var(--fg)', marginBottom: '1rem' }}>
              The Owner Portal
            </h2>
            <p style={{ color: 'var(--muted)', fontSize: '0.95rem', lineHeight: 1.7, marginBottom: '1.5rem' }}>
              Once approved, you get access to a full-featured dashboard for managing your lots, operators, pricing policies, and revenue reporting — all in one place.
            </p>
            <ul style={{ listStyle: 'none', padding: 0, display: 'flex', flexDirection: 'column', gap: '0.625rem' }}>
              {['Live occupancy monitor', 'Operator account creation & management', 'Flexible hourly/daily/monthly pricing', 'Automated revenue reports', 'Policy & document management', 'Priority support channel'].map(item => (
                <li key={item} style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontSize: '0.875rem', color: 'var(--fg)' }}>
                  <span style={{ color: '#22c55e', display: 'inline-flex', flexShrink: 0 }}><UntitledIcon name="check" size={16} /></span> {item}
                </li>
              ))}
            </ul>
          </div>
          <div style={{ borderRadius: '12px', overflow: 'hidden', border: '1px solid var(--border)' }}>
            <img src="https://images.unsplash.com/photo-1551288049-bebda4e38f71?w=600&h=400&fit=crop&auto=format" alt="Business analytics dashboard" style={{ width: '100%', height: '300px', objectFit: 'cover' }} />
          </div>
        </div>
      </section>

      {/* Calculator */}
      <section style={{ maxWidth: '1200px', margin: '0 auto', padding: '4rem 1.5rem' }}>
        <div style={{ textAlign: 'center', marginBottom: '2.5rem' }}>
          <h2 style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '1.75rem', color: 'var(--fg)', marginBottom: '0.75rem' }}>
            Calculate Your Fleet's Yearly Savings
          </h2>
          <p style={{ color: 'var(--muted)', fontSize: '0.95rem' }}>Estimate how much your parking lot can earn on the SmartParking platform.</p>
        </div>
        <Calculator />
      </section>

      {/* Contact / Signup — 2 ways */}
      <div ref={contactRef} />
      <section style={{ background: 'var(--card)', borderTop: '1px solid var(--border)', padding: '4rem 1.5rem' }}>
        <div style={{ maxWidth: '900px', margin: '0 auto' }}>
          <div style={{ textAlign: 'center', marginBottom: '2.5rem' }}>
            <h2 style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '1.75rem', color: 'var(--fg)', marginBottom: '0.75rem' }}>
              Get Started — Two Ways to Join
            </h2>
            <p style={{ color: 'var(--muted)', fontSize: '0.95rem' }}>Choose the path that works best for your business.</p>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1.5rem' }}>
            {/* Option 1 — Self-register */}
            <div className="card" style={{ border: '2px solid var(--primary)', textAlign: 'center', padding: '2rem 1.5rem' }}>
              <div style={{ color: 'var(--primary)', marginBottom: '0.75rem', display: 'flex', justifyContent: 'center' }}><UntitledIcon name="sparkles" size={30} /></div>
              <h3 style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '1.05rem', color: 'var(--fg)', marginBottom: '0.5rem' }}>Self-Register Online</h3>
              <p style={{ color: 'var(--muted)', fontSize: '0.85rem', marginBottom: '1.25rem', lineHeight: 1.6 }}>
                Create your business account directly. Fill in your lot details and our team will review your application within 2–3 business days.
              </p>
              <button className="btn-primary" style={{ width: '100%', justifyContent: 'center' }} onClick={() => setView('sign-up')}>
                Register Now
              </button>
            </div>
            {/* Option 2 — Contact form */}
            <ContactForm />
          </div>
        </div>
      </section>
    </div>
  );
}

function ContactForm() {
  const { setView } = useApp();
  return (
    <div className="card" style={{ border: '2px solid var(--border)', padding: '2rem 1.5rem', display: 'flex', flexDirection: 'column' }}>
      <div style={{ color: 'var(--primary)', marginBottom: '0.75rem', display: 'flex', justifyContent: 'center' }}><UntitledIcon name="mail" size={30} /></div>
      <h3 style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '1.05rem', color: 'var(--fg)', marginBottom: '0.5rem', textAlign: 'center' }}>Talk to Our Sales Team</h3>
      <p style={{ color: 'var(--muted)', fontSize: '0.85rem', marginBottom: '1.25rem', lineHeight: 1.6, textAlign: 'center' }}>
        Have questions? Our business team will contact you within 24 hours to discuss your needs.
      </p>
      <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem', flex: 1 }}>
        <input className="input" placeholder="Business name" />
        <input className="input" placeholder="Contact email" type="email" />
        <input className="input" placeholder="Phone number" />
        <textarea className="input" rows={3} placeholder="Brief description of your lot (type, location, # slots)..." style={{ resize: 'none' }} />
        <button className="btn-outline" style={{ width: '100%', justifyContent: 'center' }}
          onClick={() => alert('Message sent! Our team will contact you within 24 hours.')}>
          Send Message →
        </button>
      </div>
    </div>
  );
}
