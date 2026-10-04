import { useEffect, useState } from 'react';
import { useApp } from '../../../context/AppContext';
import { ownerData as store } from '../data/data';
import { CreateOperatorForm } from './CreateOperatorForm';
import type { OperatorAccessRole, User } from '../../../lib/types';
import type { ParkingLot } from '../../../lib/types';
import { UntitledIcon } from '../../../components/icon/UntitledIcon';

// Operator management
export function OperatorManagement({ sites, selectedSiteId, onOperatorsChanged }: { sites: ParkingLot[]; selectedSiteId: string; onOperatorsChanged: () => void }) {
  const { user } = useApp();
  const [operators, setOperators] = useState<User[]>(() =>
    getVisibleOperators()
  );
  const [showCreate, setShowCreate] = useState(false);
  useEffect(() => setOperators(getVisibleOperators()), [selectedSiteId, user?.id, sites]);
  function getVisibleOperators() {
    return store.getUsers().filter(operator => {
      if (operator.role !== 'operator' || operator.ownerId !== user?.id) return false;
      return selectedSiteId === 'all' || operator.operatorSiteId === selectedSiteId || (operator.operatorRole === 'financial' && operator.operatorSiteId === 'all');
    });
  }
  function refreshOperators() {
    setOperators(getVisibleOperators());
    setShowCreate(false);
    onOperatorsChanged();
  }

  function toggleLock(op: User) {
    const updated = { ...op, lockedUntil: op.lockedUntil ? undefined : new Date(Date.now() + 99999 * 60000).toISOString() };
    store.saveUser(updated);
    setOperators(getVisibleOperators());
    onOperatorsChanged();
  }

  function updateOperatorRole(op: User, operatorRole: OperatorAccessRole) {
    store.saveUser({ ...op, operatorRole, operatorSiteId: op.operatorSiteId === 'all' && operatorRole !== 'financial' ? sites[0]?.id : op.operatorSiteId });
    setOperators(getVisibleOperators());
    onOperatorsChanged();
  }

  function updateOperatorSite(op: User, operatorSiteId: string) {
    store.saveUser({ ...op, operatorSiteId });
    setOperators(getVisibleOperators());
    onOperatorsChanged();
  }

  function deleteOperator(op: User) {
    store.deleteUser(op.id);
    setOperators(getVisibleOperators());
    onOperatorsChanged();
  }

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.25rem' }}>
        <h3 style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '1.05rem', color: 'var(--fg)', margin: 0 }}>Operator Accounts</h3>
        <button className="btn-primary" style={{ fontSize: '0.85rem' }} onClick={() => setShowCreate(!showCreate)}>
          {showCreate ? <><UntitledIcon name="x" size={16} /> Cancel</> : <><UntitledIcon name="plus" size={16} /> New Operator</>}
        </button>
      </div>
      {showCreate && <CreateOperatorForm sites={sites} onCreated={refreshOperators} onCancel={() => setShowCreate(false)} />}
      <div style={{ display: 'flex', flexDirection: 'column', gap: '0.625rem' }}>
        {operators.map(op => (
          <div key={op.id} className="card" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '1rem', padding: '0.875rem 1rem' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
              <div style={{ width: 32, height: 32, background: '#a855f720', borderRadius: '50%', display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 700, color: '#a855f7', fontSize: '0.9rem' }}>{op.name.charAt(0)}</div>
              <div>
                <div style={{ fontWeight: 600, fontSize: '0.875rem', color: 'var(--fg)' }}>{op.name}</div>
                <div style={{ fontSize: '0.77rem', color: 'var(--muted)' }}>{op.email} · {(op.operatorRole ?? 'operation').replace(/^./, role => role.toUpperCase())}</div>
              </div>
            </div>
            <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
              <label className="sr-only" htmlFor={`operator-site-${op.id}`}>Site for {op.name}</label>
              <select
                id={`operator-site-${op.id}`}
                className="input"
                value={op.operatorSiteId ?? ''}
                onChange={event => updateOperatorSite(op, event.target.value)}
                style={{ width: 'auto', minWidth: '140px', maxWidth: '180px', padding: '0.35rem 0.5rem', fontSize: '0.78rem' }}
              >
                {op.operatorRole === 'financial' && <option value="all">All Sites</option>}
                {!op.operatorSiteId && <option value="">Unassigned</option>}
                {sites.map(site => <option key={site.id} value={site.id}>{site.name}</option>)}
              </select>
              <label className="sr-only" htmlFor={`operator-role-${op.id}`}>Role for {op.name}</label>
              <select
                id={`operator-role-${op.id}`}
                className="input"
                value={op.operatorRole ?? 'operation'}
                onChange={event => updateOperatorRole(op, event.target.value as OperatorAccessRole)}
                style={{ width: 'auto', minWidth: '125px', padding: '0.35rem 0.5rem', fontSize: '0.78rem' }}
              >
                <option value="financial">Financial</option>
                <option value="operation">Operation</option>
                <option value="cashier">Cashier</option>
              </select>
              {op.lockedUntil && <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4, fontSize: '0.72rem', color: '#ef4444', fontWeight: 600 }}><UntitledIcon name="lock" size={13} /> LOCKED</span>}
              <button style={{ fontSize: '0.78rem', padding: '0.3rem 0.625rem', background: 'var(--card)', border: '1px solid var(--border)', borderRadius: 'var(--radius)', cursor: 'pointer', color: 'var(--muted)' }} onClick={() => toggleLock(op)}>
                {op.lockedUntil ? 'Unlock' : 'Lock'}
              </button>
              <button style={{ fontSize: '0.78rem', padding: '0.3rem 0.625rem', background: '#fee2e2', border: '1px solid #fca5a5', borderRadius: 'var(--radius)', cursor: 'pointer', color: '#dc2626' }} onClick={() => { if (confirm(`Remove operator ${op.name}?`)) deleteOperator(op); }}>
                Remove
              </button>
            </div>
          </div>
        ))}
        {!operators.length && (
          <div style={{ textAlign: 'center', padding: '2rem', color: 'var(--muted)', fontSize: '0.875rem' }}>
            No operators yet. Create an account for your staff.
          </div>
        )}
      </div>
    </div>
  );
}
