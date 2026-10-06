import { useEffect, useState } from 'react';
import { useApp } from '../../../context/AppContext';
import { usersApi, type UserDetail, type UsersPage } from '../../../lib/usersApi';

export function Users() {
  const { user: admin } = useApp();
  const [search, setSearch] = useState('');
  const [query, setQuery] = useState({ search: '', role: '', status: '', page: 1 });
  const [page, setPage] = useState<UsersPage | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [detail, setDetail] = useState<UserDetail | null>(null);
  const [loading, setLoading] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [detailError, setDetailError] = useState('');
  const [notice, setNotice] = useState('');
  const [reload, setReload] = useState(0);
  const [detailReload, setDetailReload] = useState(0);

  useEffect(() => {
    if (admin?.role !== 'admin') return;
    let current = true;
    setLoading(true); setError(''); setPage(null);
    usersApi.list(query).then(result => { if (current) setPage(result); })
      .catch(err => { if (current) setError(err instanceof Error ? err.message : 'Cannot load users.'); })
      .finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [query, reload, admin?.id, admin?.role]);

  useEffect(() => {
    setDetail(null); setDetailError(''); setNotice(''); setDetailLoading(false);
    if (!selectedId || admin?.role !== 'admin') return;
    let current = true;
    setDetailLoading(true);
    usersApi.get(selectedId).then(result => { if (current) setDetail(result); })
      .catch(err => { if (current) setDetailError(err instanceof Error ? err.message : 'Cannot load user details.'); })
      .finally(() => { if (current) setDetailLoading(false); });
    return () => { current = false; };
  }, [selectedId, detailReload, admin?.id, admin?.role]);

  async function changeStatus() {
    if (!detail || saving) return;
    const status = detail.user.status === 'locked' ? 'active' : 'locked';
    setSaving(true); setDetailError(''); setNotice('');
    try {
      const updated = await usersApi.setStatus(detail.user.id, status);
      setDetail(current => current?.user.id === updated.id ? {
        user: updated, loginActivity: current.loginActivity.map(activity => status === 'locked' ? { ...activity, isRevoked: true } : activity),
      } : current);
      setNotice(status === 'locked' ? 'Account locked. Existing sessions have been revoked.' : 'Account unlocked. The user can sign in again.');
      setReload(value => value + 1);
    } catch (err) { setDetailError(err instanceof Error ? err.message : 'Cannot update account status.'); }
    finally { setSaving(false); }
  }

  if (admin?.role !== 'admin') return <p role="alert">Administrator access required.</p>;
  const managed = detail?.user;
  const canChange = managed && managed.id !== admin.id && !managed.roles.includes('admin') && ['active', 'locked'].includes(managed.status);
  return <section style={{ maxWidth: 1200, margin: '0 auto' }}>
    <header style={{ marginBottom: '1rem' }}>
      <h1 style={{ fontFamily: 'Outfit', color: 'var(--fg)' }}>User Management</h1>
      <p style={{ color: 'var(--muted)' }}>Search accounts, manage access, and review login activity.</p>
    </header>
    <form className="card" style={{ display: 'flex', flexWrap: 'wrap', gap: '0.75rem', marginBottom: '1rem' }} onSubmit={event => {
      event.preventDefault(); if (saving) return;
      setSelectedId(null); setQuery(current => ({ ...current, search, page: 1 }));
    }}>
      <label className="label">Name, email or phone<input className="input" disabled={saving} maxLength={200} value={search} onChange={event => setSearch(event.target.value)} /></label>
      <label className="label">Role<select className="input" disabled={saving} value={query.role} onChange={event => {
        setSelectedId(null); setQuery(current => ({ ...current, role: event.target.value, page: 1 }));
      }}><option value="">All roles</option>{['driver', 'owner', 'operator', 'admin'].map(role => <option key={role} value={role}>{role}</option>)}</select></label>
      <label className="label">Status<select className="input" disabled={saving} value={query.status} onChange={event => {
        setSelectedId(null); setQuery(current => ({ ...current, status: event.target.value, page: 1 }));
      }}><option value="">All statuses</option>{['active', 'locked', 'pendingVerification', 'pendingApproval', 'rejected'].map(status => <option key={status} value={status}>{status}</option>)}</select></label>
      <button className="btn-primary" type="submit" disabled={saving}>Search</button>
    </form>
    {error && <p role="alert">{error} <button className="btn-outline" onClick={() => setReload(value => value + 1)}>Retry</button></p>}
    <div className="driver-accounts-grid" style={{ display: 'grid', gap: '1rem', alignItems: 'start' }}>
      <div className="card" aria-busy={loading}>
        {loading && <p role="status">Loading users…</p>}
        {page && <>
          <h2 style={{ fontSize: '1rem' }}>Accounts ({page.total})</h2>
          {page.items.map(account => <button className="btn-outline" type="button" key={account.id} disabled={saving} onClick={() => setSelectedId(account.id)} aria-pressed={selectedId === account.id} style={{ display: 'block', width: '100%', textAlign: 'left', marginBottom: '0.5rem' }}>
            <strong>{account.fullName}</strong><br />{account.email || account.phone}<br /><small>{account.roles.join(', ')} · {account.status}</small>
          </button>)}
          {!page.items.length && <p>No users found.</p>}
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem' }}>
            <button className="btn-outline" disabled={query.page <= 1 || saving} onClick={() => { setSelectedId(null); setQuery(current => ({ ...current, page: current.page - 1 })); }}>Previous</button>
            <span>Page {page.page}</span>
            <button className="btn-outline" disabled={page.page * page.pageSize >= page.total || saving} onClick={() => { setSelectedId(null); setQuery(current => ({ ...current, page: current.page + 1 })); }}>Next</button>
          </div>
        </>}
      </div>
      <div className="card" aria-busy={detailLoading || saving}>
        {detailLoading && <p role="status">Loading account details…</p>}
        {detailError && <p role="alert">{detailError} {!managed && selectedId && <button className="btn-outline" onClick={() => setDetailReload(value => value + 1)}>Retry</button>}</p>}
        {!selectedId && <p>Select an account to view details.</p>}
        {managed && <>
          <h2 style={{ fontSize: '1rem' }}>{managed.fullName}</h2>
          <p>{managed.email || 'No email'} · {managed.phone || 'No phone'}</p>
          <p>Roles: {managed.roles.join(', ') || 'Unassigned'} · Status: {managed.status}</p>
          {managed.createdAt && <p>Registered: {new Date(managed.createdAt).toLocaleString()}</p>}
          {managed.lockedUntil && <p>Temporary lock until: {new Date(managed.lockedUntil).toLocaleString()}</p>}
          {canChange && <button className="btn-outline" disabled={saving || detailLoading} onClick={changeStatus}>{saving ? 'Saving…' : managed.status === 'locked' ? 'Unlock account' : 'Lock account'}</button>}
          {notice && <p role="status">{notice}</p>}
          <h3 style={{ fontSize: '0.95rem' }}>Recent login sessions</h3>
          {detail!.loginActivity.map((activity, index) => <p key={index} style={{ fontSize: '0.82rem' }}>{new Date(activity.createdAt).toLocaleString()} · {activity.isRevoked ? 'Revoked' : new Date(activity.expiresAt).getTime() <= Date.now() ? 'Expired' : 'Open'}</p>)}
          {!detail!.loginActivity.length && <p>No login sessions recorded.</p>}
        </>}
      </div>
    </div>
  </section>;
}
