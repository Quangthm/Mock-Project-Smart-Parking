import { useState, type FormEvent } from 'react';
import { useApp } from '../../../context/AppContext';
import { ownerData as store } from '../data/data';
import type { OperatorAccessRole } from '../../../lib/types';
import type { ParkingLot } from '../../../lib/types';

interface CreateOperatorFormProps {
  sites: ParkingLot[];
  onCreated: () => void;
  onCancel: () => void;
}

export function CreateOperatorForm({ sites, onCreated, onCancel }: CreateOperatorFormProps) {
  const { user } = useApp();
  const [form, setForm] = useState({ name: '', email: '', password: '', operatorRole: 'operation' as OperatorAccessRole, operatorSiteId: sites[0]?.id ?? '' });
  const [error, setError] = useState('');

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError('');
    const name = form.name.trim();
    const email = form.email.trim();
    if (!name || !email || form.password.length < 8 || (!form.operatorSiteId && form.operatorSiteId !== 'all')) {
      setError('Enter a name, valid email, password with at least 8 characters, and select a site.');
      return;
    }
    if (store.findUserByEmail(email)) {
      setError('Email already in use.');
      return;
    }

    const payload = { name, email, password: form.password, role: 'operator' as const, operatorRole: form.operatorRole, operatorSiteId: form.operatorSiteId, ownerId: user?.id };
    store.createUser(payload);
    if (user) store.notifyUser(user.id, 'EMPLOYEE_CREATED', 'Employee account created', `The employee account has been created successfully for the selected position: ${form.operatorRole}.`);
    store.addAuditLog({ userId: user?.id ?? '', userName: user?.name ?? '', userRole: 'owner', action: 'OPERATOR_CREATED', details: `Created ${form.operatorRole} operator at ${form.operatorSiteId}: ${name}` });
    onCreated();
  }

  return (
    <form onSubmit={handleSubmit} className="card animate-in" style={{ marginBottom: '1.25rem', display: 'flex', gap: '0.875rem', flexWrap: 'wrap', alignItems: 'flex-end' }}>
      <div style={{ flex: 1, minWidth: '150px' }}><label className="label" htmlFor="operator-name">Full Name</label><input id="operator-name" className="input" value={form.name} onChange={event => setForm(current => ({ ...current, name: event.target.value }))} required /></div>
      <div style={{ flex: 1, minWidth: '200px' }}><label className="label" htmlFor="operator-email">Email</label><input id="operator-email" className="input" type="email" value={form.email} onChange={event => setForm(current => ({ ...current, email: event.target.value }))} required /></div>
      <div style={{ flex: 1, minWidth: '150px' }}><label className="label" htmlFor="operator-password">Password</label><input id="operator-password" className="input" type="password" minLength={8} value={form.password} onChange={event => setForm(current => ({ ...current, password: event.target.value }))} required /></div>
      <div style={{ flex: 1, minWidth: '150px' }}><label className="label" htmlFor="operator-access-role">Role</label><select id="operator-access-role" className="input" value={form.operatorRole} onChange={event => setForm(current => ({ ...current, operatorRole: event.target.value as OperatorAccessRole }))}><option value="financial">Financial</option><option value="operation">Operation</option><option value="cashier">Cashier</option></select></div>
      <div style={{ flex: 1, minWidth: '180px' }}><label className="label" htmlFor="operator-site">Site</label><select id="operator-site" className="input" value={form.operatorSiteId} onChange={event => setForm(current => ({ ...current, operatorSiteId: event.target.value }))} required><option value="" disabled>Select a site</option>{form.operatorRole === 'financial' && <option value="all">All Sites</option>}{sites.map(site => <option key={site.id} value={site.id}>{site.name}</option>)}</select></div>
      {error && <div role="alert" style={{ width: '100%', color: '#dc2626', fontSize: '0.82rem' }}>{error}</div>}
      <div style={{ width: '100%', display: 'flex', justifyContent: 'flex-end', gap: '0.5rem' }}>
        <button type="button" className="btn-outline" style={{ fontSize: '0.875rem' }} onClick={onCancel}>Cancel</button>
        <button type="submit" className="btn-primary" style={{ fontSize: '0.875rem' }}>Create</button>
      </div>
    </form>
  );
}
