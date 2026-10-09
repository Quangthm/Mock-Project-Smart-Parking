import { useState, type FormEvent } from 'react';
import { operatorsApi, type Operator, type Site } from '../../../lib/parkingApi';
import { isValidAccountEmail } from '../../../lib/signInContact';

interface CreateOperatorFormProps {
  sites: Site[];
  onCreated: (operator: Operator) => void;
  onCancel: () => void;
}

const permissionOptions = [
  ['DEVICE_STATUS_VIEW', 'View device status'],
  ['DEVICE_MANAGE', 'Manage devices'],
  ['CASH_COLLECT', 'Collect cash'],
  ['APPEAL_REVIEW', 'Review appeals'],
  ['SLOT_OVERRIDE', 'Override slots'],
] as const;

export function CreateOperatorForm({ sites, onCreated, onCancel }: CreateOperatorFormProps) {
  const [form, setForm] = useState({ name: '', email: '', password: '', siteId: sites[0]?.id ?? '' });
  const [permissions, setPermissions] = useState<string[]>(['DEVICE_STATUS_VIEW']);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    setError('');
    const siteIds = form.siteId === 'all' ? sites.map(site => site.id) : sites.filter(site => site.id === form.siteId).map(site => site.id);
    if (!isValidAccountEmail(form.email)) { setError('Enter a valid email address.'); return; }
    if (!form.name.trim() || !form.email.trim() || !siteIds.length || siteIds.length > 100 || !permissions.length) {
      setError('Enter a name, email, select 1–100 active sites and at least one permission.');
      return;
    }
    if (form.password.length < 8 || form.password.length > 15 || !/[a-z]/.test(form.password) || !/[A-Z]/.test(form.password) || !/[0-9]/.test(form.password) || !/[^A-Za-z0-9]/.test(form.password)) {
      setError('Password must be 8–15 characters with uppercase, lowercase, a digit and a special character.');
      return;
    }
    setBusy(true);
    try {
      const operator = await operatorsApi.create({ fullName: form.name.trim(), email: form.email.trim(), password: form.password, siteIds, permissions });
      setForm(current => ({ ...current, password: '' }));
      onCreated(operator);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Operator creation failed.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="card animate-in" style={{ marginBottom: '1.25rem' }}>
      <fieldset disabled={busy} style={{ border: 0, padding: 0, margin: 0, display: 'flex', gap: '0.875rem', flexWrap: 'wrap', alignItems: 'flex-end' }}>
        <div style={{ flex: 1, minWidth: '150px' }}><label className="label" htmlFor="operator-name">Full Name</label><input id="operator-name" className="input" maxLength={255} value={form.name} onChange={event => setForm(current => ({ ...current, name: event.target.value }))} required /></div>
        <div style={{ flex: 1, minWidth: '200px' }}><label className="label" htmlFor="operator-email">Email</label><input id="operator-email" className="input" type="email" maxLength={255} value={form.email} onChange={event => setForm(current => ({ ...current, email: event.target.value }))} required /></div>
        <div style={{ flex: 1, minWidth: '150px' }}><label className="label" htmlFor="operator-password">Password</label><input id="operator-password" className="input" type="password" autoComplete="new-password" minLength={8} maxLength={15} value={form.password} onChange={event => setForm(current => ({ ...current, password: event.target.value }))} required /></div>
        <div style={{ flex: 1, minWidth: '150px' }}><label className="label" htmlFor="operator-access-role">Role</label><input id="operator-access-role" className="input" value="Operation" disabled readOnly /></div>
        <div style={{ flex: 1, minWidth: '180px' }}>
          <label className="label" htmlFor="operator-site">Site</label>
          <select id="operator-site" className="input" value={form.siteId} onChange={event => setForm(current => ({ ...current, siteId: event.target.value }))} required>
            <option value="" disabled>Select a site</option>
            <option value="all">All active sites</option>
            {sites.map(site => <option key={site.id} value={site.id}>{site.name}</option>)}
          </select>
        </div>
        <fieldset style={{ width: '100%', border: 0, padding: 0, margin: 0 }}>
          <legend className="label">Permissions</legend>
          <div style={{ display: 'flex', gap: '1rem', flexWrap: 'wrap' }}>
            {permissionOptions.map(([code, label]) => <label key={code} style={{ fontSize: '0.82rem' }}><input type="checkbox" checked={permissions.includes(code)} onChange={event => setPermissions(current => event.target.checked ? [...current, code] : current.filter(value => value !== code))} /> {label}</label>)}
          </div>
        </fieldset>
        {error && <div role="alert" style={{ width: '100%', color: '#dc2626', fontSize: '0.82rem' }}>{error}</div>}
        <div style={{ width: '100%', display: 'flex', justifyContent: 'flex-end', gap: '0.5rem' }}>
          <button type="button" className="btn-outline" onClick={onCancel}>Cancel</button>
          <button type="submit" className="btn-primary" disabled={busy || !sites.length}>{busy ? 'Creating…' : 'Create'}</button>
        </div>
      </fieldset>
    </form>
  );
}
