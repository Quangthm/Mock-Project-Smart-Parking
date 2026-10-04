import { useState } from 'react';
import { useApp } from '../../../context/AppContext';
import { adminData as store } from '../data/data';

export function AdminSettings() {
  const { user, setUser } = useApp();
  const [form, setForm] = useState({ name: user?.name ?? '', currentPassword: '', newPassword: '', confirmPassword: '' });
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  function saveProfile(e: React.FormEvent) {
    e.preventDefault();
    setError(''); setSuccess('');
    if (!user) return;
    if (form.newPassword) {
      if (form.currentPassword !== user.password) { setError('Current password is incorrect.'); return; }
      if (form.newPassword !== form.confirmPassword) { setError('New passwords do not match.'); return; }
      if (form.newPassword.length < 8) { setError('New password must be at least 8 characters.'); return; }
    }
    const updated = { ...user, name: form.name, password: form.newPassword || user.password };
    store.saveUser(updated);
    setUser(updated);
    store.addAuditLog({ userId: user.id, userName: user.name, userRole: 'admin', action: 'PROFILE_UPDATED', details: 'Admin profile updated' });
    setSuccess('Profile updated successfully.');
    setForm(f => ({ ...f, currentPassword: '', newPassword: '', confirmPassword: '' }));
  }

  return (
    <div style={{ maxWidth: '500px' }}>
      <h2 style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '1.3rem', marginBottom: '1.5rem', color: 'var(--fg)' }}>Admin Settings</h2>
      <form onSubmit={saveProfile} className="card" style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
        <div><label className="label">Display Name</label><input className="input" value={form.name} onChange={e => setForm(f => ({ ...f, name: e.target.value }))} /></div>
        <div style={{ borderTop: '1px solid var(--border)', paddingTop: '1rem' }}>
          <div style={{ fontWeight: 600, fontSize: '0.875rem', color: 'var(--fg)', marginBottom: '0.875rem' }}>Change Password</div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.875rem' }}>
            <div><label className="label">Current Password</label><input className="input" type="password" value={form.currentPassword} onChange={e => setForm(f => ({ ...f, currentPassword: e.target.value }))} /></div>
            <div><label className="label">New Password</label><input className="input" type="password" value={form.newPassword} onChange={e => setForm(f => ({ ...f, newPassword: e.target.value }))} /></div>
            <div><label className="label">Confirm New Password</label><input className="input" type="password" value={form.confirmPassword} onChange={e => setForm(f => ({ ...f, confirmPassword: e.target.value }))} /></div>
          </div>
        </div>
        {error && <div style={{ background: '#fee2e2', border: '1px solid #fca5a5', borderRadius: 'var(--radius)', padding: '0.5rem 0.75rem', color: '#dc2626', fontSize: '0.85rem' }}>{error}</div>}
        {success && <div style={{ background: '#f0fdf4', border: '1px solid #86efac', borderRadius: 'var(--radius)', padding: '0.5rem 0.75rem', color: '#166534', fontSize: '0.85rem' }}>{success}</div>}
        <button type="submit" className="btn-primary" style={{ alignSelf: 'flex-start' }}>Save Changes</button>
      </form>
    </div>
  );
}
