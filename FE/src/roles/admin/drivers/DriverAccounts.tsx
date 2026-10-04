import { useEffect, useState } from 'react';
import { useApp } from '../../../context/AppContext';
import type { User } from '../../../lib/types';
import { adminData as store } from '../data/data';

function accountStatus(driver: User) {
  if (driver.accountStatus === 'suspended') return 'Suspended';
  if (driver.accountStatus === 'locked' || (driver.lockedUntil && new Date(driver.lockedUntil) > new Date())) return 'Locked';
  return 'Active';
}

export function DriverAccounts() {
  const { user: admin } = useApp();
  const [drivers, setDrivers] = useState(() => store.getUsers().filter(user => user.role === 'driver'));
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [edit, setEdit] = useState<{ name: string; email: string; phone: string }>({ name: '', email: '', phone: '' });
  const [notice, setNotice] = useState('');
  const [temporaryPassword, setTemporaryPassword] = useState('');
  const selected = drivers.find(driver => driver.id === selectedId) ?? null;

  useEffect(() => {
    const refresh = () => setDrivers(store.getUsers().filter(user => user.role === 'driver'));
    window.addEventListener('sp-data-change', refresh);
    window.addEventListener('storage', refresh);
    return () => { window.removeEventListener('sp-data-change', refresh); window.removeEventListener('storage', refresh); };
  }, []);

  function choose(driver: User) {
    setSelectedId(driver.id);
    setEdit({ name: driver.name, email: driver.email, phone: driver.phone ?? '' });
    setNotice('');
    setTemporaryPassword('');
  }

  function saveProfile(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selected || !edit.name.trim() || !edit.email.trim()) return;
    const existing = store.getUsers().find(user => user.email.toLowerCase() === edit.email.trim().toLowerCase() && user.id !== selected.id);
    if (existing) { setNotice('That email address is already in use.'); return; }
    store.saveUser({ ...selected, name: edit.name.trim(), email: edit.email.trim(), phone: edit.phone.trim() || undefined });
    store.addAuditLog({ userId: admin?.id ?? '', userName: admin?.name ?? '', userRole: 'admin', action: 'DRIVER_PROFILE_UPDATED', details: `Updated Driver account ${selected.id}` });
    setNotice('Driver information updated.');
  }

  function setStatus(status: 'active' | 'suspended' | 'locked') {
    if (!selected || !admin) return;
    store.saveUser({ ...selected, accountStatus: status, lockedUntil: status === 'locked' ? new Date(Date.now() + 30 * 60_000).toISOString() : undefined });
    store.addAuditLog({ userId: admin.id, userName: admin.name, userRole: 'admin', action: `DRIVER_${status.toUpperCase()}`, details: `${status} Driver account ${selected.id}` });
  }

  function resetPassword() {
    if (!selected || !admin) return;
    const password = `Reset-${Math.random().toString(36).slice(2, 8)}A1!`;
    store.saveUser({ ...selected, password });
    store.addAuditLog({ userId: admin.id, userName: admin.name, userRole: 'admin', action: 'DRIVER_CREDENTIALS_RESET', details: `Reset credentials for Driver ${selected.id}` });
    setTemporaryPassword(password);
  }

  if (admin?.role !== 'admin') return <p role="alert">Administrator access required.</p>;

  return <section style={{ maxWidth: 1200, margin: '0 auto' }}>
    <header style={{ marginBottom: '1rem' }}><p style={{ color: 'var(--primary)', fontSize: '0.85rem', marginBottom: 4 }}>Account administration</p><h1 style={{ fontFamily: 'Outfit', margin: 0, color: 'var(--fg)' }}>Driver Account Management</h1><p style={{ color: 'var(--muted)', fontSize: '0.85rem' }}>Review account status, activity, bookings, payments, and reported issues.</p></header>
    <div className="driver-accounts-grid" style={{ display: 'grid', gap: '1rem', alignItems: 'start' }}>
      <div className="card" style={{ padding: '0.75rem', maxHeight: 680, overflow: 'auto' }}>
        <h2 style={{ fontSize: '0.95rem', margin: '0.25rem 0 0.75rem' }}>Driver accounts ({drivers.length})</h2>
        {drivers.map(driver => {
          const status = accountStatus(driver);
          const color = status === 'Active' ? '#22c55e' : status === 'Locked' ? '#f59e0b' : '#ef4444';
          return <button type="button" key={driver.id} onClick={() => choose(driver)} style={{ display: 'block', width: '100%', padding: '0.75rem', marginBottom: '0.4rem', textAlign: 'left', borderRadius: '0.65rem', border: `1px solid ${selectedId === driver.id ? 'var(--primary)' : 'var(--border)'}`, background: selectedId === driver.id ? 'var(--primary)10' : 'var(--bg)', color: 'var(--fg)', cursor: 'pointer' }}>
            <strong style={{ display: 'block' }}>{driver.name}</strong><span style={{ display: 'block', fontSize: '0.76rem', color: 'var(--muted)' }}>{driver.email}</span><span style={{ fontSize: '0.72rem', fontWeight: 700, color }}>{status}</span>
          </button>;
        })}
        {!drivers.length && <p style={{ color: 'var(--muted)', fontSize: '0.85rem' }}>No Driver accounts found.</p>}
      </div>
      {selected ? <div style={{ display: 'grid', gap: '1rem' }}>
        <form className="card" onSubmit={saveProfile} style={{ display: 'grid', gap: '0.75rem' }}>
          <h2 style={{ fontSize: '1rem', margin: 0 }}>Driver details <span style={{ fontSize: '0.75rem', color: accountStatus(selected) === 'Suspended' ? '#ef4444' : accountStatus(selected) === 'Locked' ? '#f59e0b' : '#22c55e' }}>· {accountStatus(selected).toUpperCase()}</span></h2>
          <p style={{ margin: 0, fontSize: '0.78rem', color: 'var(--muted)' }}>Driver ID: {selected.id} · Registered {new Date(selected.createdAt).toLocaleString()} · Last login {selected.lastLoginAt ? new Date(selected.lastLoginAt).toLocaleString() : 'Never'}</p>
          <label className="label">Full name<input className="input" value={edit.name} onChange={event => setEdit(current => ({ ...current, name: event.target.value }))} /></label>
          <label className="label">Email<input className="input" type="email" value={edit.email} onChange={event => setEdit(current => ({ ...current, email: event.target.value }))} /></label>
          <label className="label">Phone<input className="input" value={edit.phone} onChange={event => setEdit(current => ({ ...current, phone: event.target.value }))} /></label>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem' }}><button className="btn-primary" type="submit">Save Driver</button><button className="btn-outline" type="button" onClick={() => setStatus('active')}>Activate</button><button className="btn-outline" type="button" onClick={() => setStatus('active')}>Unlock</button><button className="btn-outline" type="button" onClick={() => setStatus('suspended')}>Suspend</button><button className="btn-outline" type="button" onClick={() => setStatus('locked')}>Lock</button><button className="btn-outline" type="button" onClick={resetPassword}>Reset Password</button></div>
          {notice && <p role="status" style={{ margin: 0, fontSize: '0.82rem', color: notice.startsWith('That') ? '#ef4444' : '#22c55e' }}>{notice}</p>}
          {temporaryPassword && <p role="status" style={{ margin: 0, fontSize: '0.82rem' }}>Temporary password: <strong>{temporaryPassword}</strong></p>}
        </form>
        <div className="card"><h2 style={{ fontSize: '1rem', marginTop: 0 }}>Vehicles</h2>{store.getVehiclesByDriver(selected.id).map(vehicle => <p key={vehicle.id} style={{ margin: '0.35rem 0', fontSize: '0.85rem' }}>{vehicle.type} · {vehicle.licensePlate} · {vehicle.plateType}</p>)}{!store.getVehiclesByDriver(selected.id).length && <p style={{ color: 'var(--muted)', fontSize: '0.82rem' }}>No saved vehicles.</p>}</div>
        <div className="card"><h2 style={{ fontSize: '1rem', marginTop: 0 }}>Booking & payment history</h2>{store.getBookingsByDriver(selected.id).map(booking => <p key={booking.id} style={{ margin: '0.4rem 0', fontSize: '0.82rem' }}>{booking.id} · {booking.lotName} · {booking.status} / {booking.paymentStatus ?? 'pending'} · {booking.amount.toLocaleString('vi-VN')}₫</p>)}{!store.getBookingsByDriver(selected.id).length && <p style={{ color: 'var(--muted)', fontSize: '0.82rem' }}>No bookings.</p>}</div>
        <div className="card"><h2 style={{ fontSize: '1rem', marginTop: 0 }}>Reported issues & activity</h2>{store.getTickets().filter(ticket => ticket.userId === selected.id).map(ticket => <p key={ticket.id} style={{ margin: '0.4rem 0', fontSize: '0.82rem' }}>Ticket {ticket.id} · {ticket.subject} · {ticket.status}</p>)}{store.getAuditLogs().filter(log => log.userId === selected.id).slice(0, 8).map(log => <p key={log.id} style={{ margin: '0.4rem 0', fontSize: '0.78rem', color: 'var(--muted)' }}>{new Date(log.timestamp).toLocaleString()} · {log.action} · {log.details}</p>)}</div>
      </div> : <div className="card" style={{ color: 'var(--muted)' }}>Select a Driver account to view its details and activity.</div>}
    </div>
  </section>;
}
