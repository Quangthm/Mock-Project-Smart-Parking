import { useState } from 'react';
import { useApp } from '../../../context/AppContext';
import { authData as store } from '../data/data';
import { useNavigate } from 'react-router-dom';
import { getUserHomePath } from '../../operator/data/roleRoutes';
import { BrandLogo } from '../../../components/brand/BrandLogo';
import { PasswordVisibilityIcon } from '../../../components/forms/PasswordVisibilityIcon';


export function SignIn() {
  const { setUser, setView } = useApp();
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState('');

  function submit(e: React.FormEvent) {
    e.preventDefault();
    setError('');

    const user = store.findUserByEmail(email);
    if (!user || user.password !== password) {
      setError('Invalid email or password.');
      return;
    }

    if (user.accountStatus === 'suspended') {
      setError('This account is suspended. Please contact support.');
      return;
    }
    if (user.accountStatus === 'locked') {
      setError('This account is locked. Please contact an administrator.');
      return;
    }

    if (user.lockedUntil && new Date(user.lockedUntil) > new Date()) {
      const remaining = Math.ceil((new Date(user.lockedUntil).getTime() - Date.now()) / 60000);
      setError(`Account locked. Try again in ${remaining} minute(s).`);
      return;
    }

    if (user.role === 'owner' && !user.policyAccepted) {
      setError('Your business account is pending admin approval. You will receive an email once approved.');
      return;
    }

    const signedInUser = { ...user, lastLoginAt: new Date().toISOString(), accountStatus: 'active' as const };
    store.saveUser(signedInUser);
    store.addAuditLog({ userId: user.id, userName: user.name, userRole: user.role, action: 'SIGN_IN', details: `User signed in as ${user.role}` });
    store.notifyUser(user.id, 'LOGIN_SUCCESS', 'Successful login', 'You have successfully logged in.');
    setUser(signedInUser);
    setView(user.role as any);
    if (user.role === 'operator') navigate(getUserHomePath(user), { replace: true });
  }

  return (
    <div className="auth-page-background" style={{ minHeight: '80vh', display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '2rem 1rem' }}>
      <div style={{ width: '100%', maxWidth: '420px' }}>
        <div style={{ textAlign: 'center', marginBottom: '2rem' }}>
          <BrandLogo height={100} style={{ margin: '0 auto 1rem' }} />
          <h1 style={{ fontFamily: 'Outfit', fontWeight: 800, fontSize: '1.75rem', color: 'var(--fg)', margin: '0 0 0.375rem' }}>
            Sign In
          </h1>
          <p style={{ color: 'var(--muted)', fontSize: '0.925rem' }}>
            Welcome back to SmartParking
          </p>
        </div>

        <form onSubmit={submit} className="card auth-form-background" style={{ display: 'flex', flexDirection: 'column', gap: '1.125rem' }}>
          <div>
            <label className="label" style={{ fontSize: '0.925rem' }}>Email Address</label>
            <input className="input" type="email" value={email} onChange={e => setEmail(e.target.value)} placeholder="you@example.com" required autoFocus style={{ fontSize: '1rem' }} />
          </div>

          <div>
            <label className="label" style={{ fontSize: '0.925rem' }}>Password</label>
            <div style={{ position: 'relative' }}>
              <input className="input" type={showPassword ? 'text' : 'password'} value={password} onChange={e => setPassword(e.target.value)} placeholder="Enter your password" required style={{ paddingRight: '2.5rem', fontSize: '1rem' }} />
              <button type="button" aria-label={showPassword ? 'Hide password' : 'Show password'} title={showPassword ? 'Hide password' : 'Show password'} onClick={() => setShowPassword(!showPassword)} style={{ position: 'absolute', right: '0.75rem', top: '50%', transform: 'translateY(-50%)', display: 'flex', width: 20, height: 20, padding: 0, background: 'none', border: 'none', cursor: 'pointer', color: 'var(--muted)' }}>
                <PasswordVisibilityIcon visible={showPassword} />
              </button>
            </div>
          </div>

          {error && (
            <div style={{ background: '#fee2e2', border: '1px solid #fca5a5', borderRadius: 'var(--radius)', padding: '0.625rem 0.875rem', color: '#dc2626', fontSize: '0.875rem' }}>
              {error}
            </div>
          )}

          <button type="submit" className="btn-primary" style={{ width: '100%', justifyContent: 'center', fontSize: '1rem', padding: '0.75rem' }}>
            Sign In
          </button>

          <p style={{ textAlign: 'center', fontSize: '0.9rem', color: 'var(--muted)', margin: 0 }}>
            Don't have an account?{' '}
            <button type="button" onClick={() => setView('sign-up')} style={{ background: 'none', border: 'none', color: 'var(--primary)', cursor: 'pointer', fontWeight: 600, fontSize: '0.9rem', padding: 0 }}>
              Sign Up
            </button>
          </p>
        </form>
      </div>
    </div>
  );
}
