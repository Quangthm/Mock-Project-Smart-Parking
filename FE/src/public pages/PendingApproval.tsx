import { useApp } from '../context/AppContext';
import { UntitledIcon } from '../components/icon/UntitledIcon';

export function PendingApproval() {
  const { setView } = useApp();
  return (
    <div style={{ minHeight: '80vh', display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '2rem' }}>
      <div style={{ maxWidth: '520px', textAlign: 'center' }}>
        <div style={{ width: 80, height: 80, background: '#fef9c3', border: '2px solid #fde68a', borderRadius: '50%', display: 'flex', alignItems: 'center', justifyContent: 'center', margin: '0 auto 1.5rem', fontSize: '2rem' }}>
          <UntitledIcon name="clock" size={22} />
        </div>
        <h1 style={{ fontFamily: 'Outfit', fontWeight: 800, fontSize: '1.75rem', color: 'var(--fg)', marginBottom: '0.75rem' }}>
          Application Submitted!
        </h1>
        <p style={{ color: 'var(--muted)', fontSize: '1rem', lineHeight: 1.7, marginBottom: '1.5rem' }}>
          Thank you for applying to become a SmartParking partner. Your application is now under review by our team.
        </p>

        <div className="card" style={{ textAlign: 'left', marginBottom: '1.5rem' }}>
          <h3 style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '0.95rem', color: 'var(--fg)', marginBottom: '1rem' }}>What happens next?</h3>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.875rem' }}>
            {[
              { step: '01', label: 'Application Review', desc: 'An Admin will review your company and contact details.', color: '#2563eb', done: true },
              { step: '02', label: 'Admin Decision', desc: 'The Admin records an approval or rejection. Contact support for your application status.', color: '#f59e0b', done: false },
              { step: '03', label: 'Account Activation', desc: 'Once approved, sign in to access your Owner dashboard and set up your lot.', color: '#22c55e', done: false },
            ].map(s => (
              <div key={s.step} style={{ display: 'flex', gap: '0.875rem', alignItems: 'flex-start' }}>
                <div style={{ width: 32, height: 32, borderRadius: '50%', background: s.done ? s.color : 'var(--muted)18', border: `2px solid ${s.done ? s.color : 'var(--border)'}`, display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0, fontSize: '0.7rem', fontWeight: 700, color: s.done ? '#fff' : 'var(--muted)' }}>
                  {s.done ? <UntitledIcon name="check" size={16} /> : s.step}
                </div>
                <div>
                  <div style={{ fontWeight: 600, fontSize: '0.875rem', color: 'var(--fg)', marginBottom: '0.15rem' }}>{s.label}</div>
                  <div style={{ fontSize: '0.8rem', color: 'var(--muted)' }}>{s.desc}</div>
                </div>
              </div>
            ))}
          </div>
        </div>

        <div style={{ background: 'var(--primary)10', border: '1px solid var(--primary)30', borderRadius: 'var(--radius)', padding: '0.875rem 1rem', marginBottom: '1.5rem', fontSize: '0.85rem', color: 'var(--muted)', textAlign: 'left' }}>
          <strong style={{ color: 'var(--primary)' }}>Note:</strong> Your account is created but <strong>inactive</strong> until admin approval. You can sign in once your application has been approved.
        </div>

        <div style={{ display: 'flex', gap: '0.875rem', justifyContent: 'center' }}>
          <button className="btn-primary" onClick={() => setView('landing')}>
            Back to Home
          </button>
          <button className="btn-outline" onClick={() => setView('support')}>
            Contact Support
          </button>
        </div>
      </div>
    </div>
  );
}
