import { useEffect, useState } from 'react';
import { useApp } from '../../../context/AppContext';
import { Overview } from './Overview';
import { Applications } from '../applications/Applications';
import { AuditLogView } from '../audit-log/AuditLogView';
import { SystemConfig } from '../system/SystemConfig';
import { AdminSettings } from '../setting/AdminSettings';
import { DriverAccounts } from '../drivers/DriverAccounts';
import { DashboardSidebar } from '../../../components/layout/DashboardSidebar';

type Tab = 'overview' | 'applications' | 'drivers' | 'audit' | 'system' | 'settings';

export function AdminDashboard() {
  const { user } = useApp();
  const [tab, setTab] = useState<Tab>('overview');
  useEffect(() => {
    const onNavigate = (event: Event) => setTab((event as CustomEvent<Tab>).detail);
    window.addEventListener('sp:dashboard-nav', onNavigate);
    return () => window.removeEventListener('sp:dashboard-nav', onNavigate);
  }, []);
  if (user?.role !== 'admin') return <div role="alert" style={{ padding: '2rem', color: 'var(--fg)' }}>Administrator access required.</div>;

  const NAV = [
    { id: 'overview' as Tab, label: 'Overview', icon: '📊' },
    { id: 'applications' as Tab, label: 'Applications', icon: '📝' },
    { id: 'drivers' as Tab, label: 'Driver Accounts', icon: '🚘' },
    { id: 'audit' as Tab, label: 'Audit Log', icon: '🔍' },
    { id: 'system' as Tab, label: 'System Config', icon: '⚙️' },
    { id: 'settings' as Tab, label: 'Settings', icon: '🔧' },
  ];

  return (
    <DashboardSidebar groups={[
      { items: [{ id: 'overview', label: 'Dashboard', icon: '⌂', active: tab === 'overview', onClick: () => setTab('overview') }] },
      { label: 'Accounts', items: [
        { id: 'applications', label: 'Applications', icon: '▤', active: tab === 'applications', onClick: () => setTab('applications') },
        { id: 'drivers', label: 'Driver accounts', icon: '♙', active: tab === 'drivers', onClick: () => setTab('drivers') },
      ] },
      { label: 'System', items: [
        { id: 'audit', label: 'Audit log', icon: '◷', active: tab === 'audit', onClick: () => setTab('audit') },
        { id: 'system', label: 'System configuration', icon: '⚙', active: tab === 'system', onClick: () => setTab('system') },
      ] },
    ]}>
      <div style={{ padding: '1.75rem', overflowY: 'auto' }}>
        <div className="animate-in">
          {tab === 'overview' && <Overview />}
          {tab === 'applications' && <Applications />}
          {tab === 'drivers' && <DriverAccounts />}
          {tab === 'audit' && <AuditLogView />}
          {tab === 'system' && <SystemConfig />}
          {tab === 'settings' && <AdminSettings />}
        </div>
      </div>
    </DashboardSidebar>
  );
}
