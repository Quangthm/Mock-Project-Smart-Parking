import { useEffect, useState } from 'react';
import { useApp } from '../../../context/AppContext';
import { parkingApi, operatorsApi, type Site, type Operator } from '../../../lib/parkingApi';
import { OnboardingWizard } from '../parking-lots/CreateParkingLotForOwner';
import { OwnerOverview } from './OwnerOverview';
import { OperatorManagement } from '../operators/OperatorManagement';
import { PolicySettings } from '../policies/PolicySettings';
import { SiteManagement } from '../parking-lots/SiteManagement';
import { RevenueDashboard } from '../revenue/RevenueDashboard';
import { ParkingStructure } from '../structure/ParkingStructure';
import { DashboardSidebar } from '../../../components/layout/DashboardSidebar';

type Tab = 'dashboard' | 'sites' | 'lots' | 'structure' | 'operators' | 'finance' | 'policy';

export function OwnerDashboard() {
  const { user } = useApp();
  const [tab, setTab] = useState<Tab>('dashboard');
  const [selectedSiteId, setSelectedSiteId] = useState('all');
  const [siteRefresh, setSiteRefresh] = useState(0);
  const [sites, setSites] = useState<Site[]>([]);
  const [operators, setOperators] = useState<Operator[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  useEffect(() => {
    let current = true;
    setLoading(true); setError('');
    Promise.all([parkingApi.list(), operatorsApi.list()])
      .then(([sites, operators]) => {
        if (!current) return;
        setSites(sites); setOperators(operators);
        setSelectedSiteId(current => current === 'all' || sites.some(site => site.id === current) ? current : 'all');
      })
      .catch(err => { if (current) { setSites([]); setOperators([]); setError(err instanceof Error ? err.message : 'Cannot load owner data.'); } })
      .finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [user?.id, siteRefresh]);

  useEffect(() => {
    const onNavigate = (event: Event) => setTab((event as CustomEvent<Tab>).detail);
    window.addEventListener('sp:dashboard-nav', onNavigate);
    return () => window.removeEventListener('sp:dashboard-nav', onNavigate);
  }, []);

  if (!user?.onboardingComplete) {
    return <div style={{ minHeight: 'calc(100vh - 60px)', background: 'var(--bg)' }}><OnboardingWizard onComplete={() => setTab('dashboard')} /></div>;
  }

  return (
    <DashboardSidebar groups={[
      { items: [{ id: 'dashboard', label: 'Dashboard', icon: '⌂', active: tab === 'dashboard', onClick: () => setTab('dashboard') }] },
      { label: 'Parking management', items: [
        { id: 'sites', label: 'Sites', icon: '⌖', active: tab === 'sites', onClick: () => setTab('sites') },
        { id: 'lots', label: 'My parking lots', icon: '▦', active: tab === 'lots', onClick: () => setTab('lots') },
        { id: 'structure', label: 'Parking Structure', icon: '🏢', active: tab === 'structure', onClick: () => setTab('structure') },
      ] },
      { label: 'Business', items: [
        { id: 'operators', label: 'Operators', icon: '♙', active: tab === 'operators', onClick: () => setTab('operators') },
        { id: 'finance', label: 'Financial', icon: '₫', active: tab === 'finance', onClick: () => setTab('finance') },
        { id: 'policy', label: 'Policies', icon: '⚖', active: tab === 'policy', onClick: () => setTab('policy') },
      ] },
    ]}>
      <main className="min-w-0 overflow-y-auto p-4 sm:p-7">
        {loading && <p role="status">Loading owner sites…</p>}
        {error && <div role="alert" className="text-red-600">{error} <button className="btn-outline" onClick={() => setSiteRefresh(value => value + 1)}>Retry</button></div>}
        {(tab === 'dashboard' || tab === 'finance') && (
          <div className="mb-6 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-[var(--border)] bg-[var(--card)] p-4">
            <div><p className="text-xs font-medium uppercase tracking-wide text-[var(--muted)]">Multi-site</p><p className="mt-1 font-semibold text-[var(--fg)]">Filter Owner Dashboard</p></div>
            <label className="flex items-center gap-3 text-sm font-medium text-[var(--fg)]"><span>Site</span><select className="input min-w-[210px]" value={selectedSiteId} onChange={event => setSelectedSiteId(event.target.value)}><option value="all">All Sites</option>{sites.map(site => <option key={site.id} value={site.id}>{site.name}</option>)}</select></label>
          </div>
        )}
        <div className="animate-in">
          {tab === 'dashboard' && <OwnerOverview selectedSiteId={selectedSiteId} sites={sites} operators={operators} />}
          {tab === 'sites' && <SiteManagement onSiteCreated={() => setSiteRefresh(value => value + 1)} />}
          {tab === 'lots' && <SiteManagement onSiteCreated={() => setSiteRefresh(value => value + 1)} />}
          {tab === 'structure' && <ParkingStructure sites={sites} selectedSiteId={selectedSiteId} onChanged={() => setSiteRefresh(value => value + 1)} />}
          {tab === 'operators' && <OperatorManagement selectedSiteId={selectedSiteId} onOperatorsChanged={() => setSiteRefresh(value => value + 1)} />}
          {tab === 'finance' && <RevenueDashboard selectedSiteId={selectedSiteId} sites={sites} />}
          {tab === 'policy' && <PolicySettings />}
        </div>
      </main>
    </DashboardSidebar>
  );
}
