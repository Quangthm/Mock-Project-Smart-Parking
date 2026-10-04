import { useApp, type View } from '../../context/AppContext';
import { BrandLogo } from '../brand/BrandLogo';
import { ThemeIcon } from '../icon/ThemeIcon';

export function Navbar() {
  const { user, view, theme, setView, toggleTheme, signOut } = useApp();

  return (
    <nav style={{ position: 'sticky', top: 0, zIndex: 100, background: 'var(--bg)', borderBottom: '1px solid var(--border)', backdropFilter: 'blur(8px)' }}>
      <div style={{ maxWidth: '1200px', margin: '0 auto', padding: '0 1.5rem', display: 'flex', alignItems: 'center', height: '60px', gap: '1.5rem' }}>
        {/* Logo */}
        <button onClick={() => setView('landing')} style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', background: 'none', border: 'none', cursor: 'pointer', padding: 0 }}>
          <BrandLogo height={60} />
          <span className="brand-wordmark" style={{ fontSize: '1.1rem' }}><span className="brand-smart">Smart</span><span className="brand-parking">Parking</span></span>
        </button>

        {/* Nav links */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.25rem', flex: user ? '0 0 auto' : 1 }}>
          {!user && (
            <>
              <NavLink label="Home" active={view === 'landing'} onClick={() => setView('landing')} />
              <NavLink label="Pricing" active={view === 'pricing'} onClick={() => setView('pricing')} />
              <NavLink label="Support" active={view === 'support'} onClick={() => setView('support')} />
            </>
          )}
          {user && (
            <span style={{ fontSize: '0.85rem', color: 'var(--muted)' }}>
              Welcome, <strong style={{ color: 'var(--fg)' }}>{user.name}</strong>
            </span>
          )}
        </div>

        {/* Right side */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginLeft: user ? 'auto' : 0 }}>
          <button
            onClick={toggleTheme}
            style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: 'var(--radius)', width: 36, height: 36, cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'var(--fg)' }}
            title={theme === 'dark' ? 'Switch to Light Mode' : 'Switch to Dark Mode'}
          >
            <span style={{ display: 'block', width: 20, height: 20 }}><ThemeIcon mode={theme === 'dark' ? 'light' : 'dark'} /></span>
          </button>

          {user ? (
            <button className="btn-outline" style={{ fontSize: '0.85rem', padding: '0.4rem 0.875rem' }} onClick={signOut}>
              Sign Out
            </button>
          ) : (
            <>
              <button className="btn-outline" style={{ fontSize: '0.85rem', padding: '0.4rem 0.875rem' }} onClick={() => setView('sign-in')}>
                Sign In
              </button>
              <button className="btn-primary" style={{ fontSize: '0.85rem', padding: '0.4rem 0.875rem' }} onClick={() => setView('sign-up')}>
                Sign Up
              </button>
            </>
          )}
        </div>
      </div>
    </nav>
  );
}

function NavLink({ label, active, onClick }: { label: string; active: boolean; onClick: () => void }) {
  return (
    <button
      onClick={onClick}
      style={{
        background: 'none', border: 'none', cursor: 'pointer',
        padding: '0.375rem 0.75rem',
        borderRadius: 'var(--radius)',
        fontSize: '0.9rem',
        fontWeight: active ? 600 : 400,
        color: active ? 'var(--primary)' : 'var(--muted)',
        transition: 'color 0.15s',
      }}
    >
      {label}
    </button>
  );
}
