import { useEffect, useState } from 'react';
import { useApp } from '../../../context/AppContext';
import { parkingApi, operatorsApi, type Site, type Operator } from '../../../lib/parkingApi';
import { OnboardingWizard } from '../parking-lots/CreateParkingLotForOwner';
import { OwnerOverview } from './OwnerOverview';
import { OperatorManagement } from '../operators/OperatorManagement';
import { PolicySettings } from '../policies/PolicySettings';
import { SiteManagement } from '../parking-lots/SiteManagement';
import { LotManagement } from '../parking-lots/LotManagement';
import { RevenueDashboard } from '../revenue/RevenueDashboard';
import { ParkingStructure } from '../structure/ParkingStructure';
import { DashboardSidebar } from '../../../components/layout/DashboardSidebar';

type Tab = 'dashboard' | 'sites' | 'lots' | 'structure' | 'operators' | 'finance' | 'policy';

export function OwnerDashboard() {
  const { user } = useApp();
  const [tab, setTab] = useState<Tab>('dashboard');
  const [selectedSiteId, setSelectedSiteId] = useState('all');
  const [setupSiteId, setSetupSiteId] = useState<string | null>(null);
  const [siteRefresh, setSiteRefresh] = useState(0);
  const [sites, setSites] = useState<Site[]>([]);
  const [sitesLoaded, setSitesLoaded] = useState(false);
  const [operators, setOperators] = useState<Operator[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  useEffect(() => {
    let current = true;
    setLoading(true); setSitesLoaded(false); setError('');
    Promise.allSettled([parkingApi.list(), operatorsApi.list()])
      .then(([siteResult, operatorResult]) => {
        if (!current) return;
        if (siteResult.status === 'fulfilled') {
          setSites(siteResult.value);
          setSitesLoaded(true);
          setSelectedSiteId(current => current === setupSiteId || current === 'all' || siteResult.value.some(site => site.id === current) ? current : 'all');
        } else {
          setSites([]);
          setError(siteResult.reason instanceof Error ? siteResult.reason.message : 'Cannot load owner sites.');
        }
        if (operatorResult.status === 'fulfilled') setOperators(operatorResult.value);
        else {
          setOperators([]);
          if (siteResult.status === 'fulfilled') setError(operatorResult.reason instanceof Error ? operatorResult.reason.message : 'Cannot load operators.');
        }
      })
      .finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [user?.id, siteRefresh, setupSiteId]);

  useEffect(() => {
    const onNavigate = (event: Event) => setTab((event as CustomEvent<Tab>).detail);
    window.addEventListener('sp:dashboard-nav', onNavigate);
    return () => window.removeEventListener('sp:dashboard-nav', onNavigate);
  }, []);

  const showOnboarding = !loading && sitesLoaded && !user?.onboardingComplete && sites.length === 0 && tab === 'dashboard';

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
        { id: 'finance', label: 'Financial', icon: 'banknote', active: tab === 'finance', onClick: () => setTab('finance') },
        { id: 'policy', label: 'Policies', icon: '⚖', active: tab === 'policy', onClick: () => setTab('policy') },
      ] },
    ]}>
      <main className={`min-w-0 overflow-y-auto${showOnboarding ? '' : ' p-4 sm:p-7'}`}>
        {showOnboarding ? (
          <div className="min-h-[calc(100vh-60px)] bg-[var(--bg)]">
            <OnboardingWizard onComplete={(lot) => { setSetupSiteId(lot.id); setSelectedSiteId(lot.id); setTab('structure'); setSiteRefresh(value => value + 1); }} />
          </div>
        ) : (
          <>
        {loading && <p role="status">Loading owner sites…</p>}
        {error && <div role="alert" className="text-red-600">{error} <button className="btn-outline" onClick={() => setSiteRefresh(value => value + 1)}>Retry</button></div>}
        {(tab === 'dashboard' || tab === 'finance') && (
          <div className="mb-6 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-[var(--border)] bg-[var(--card)] p-4">
            <div><p className="text-sm font-medium text-[var(--muted)]">Multiple sites</p><p className="mt-1 font-semibold text-[var(--fg)]">Filter Owner Dashboard</p></div>
            <label className="flex items-center gap-3 text-sm font-medium text-[var(--fg)]"><span>Site</span><select className="input min-w-[210px]" value={selectedSiteId} onChange={event => setSelectedSiteId(event.target.value)}><option value="all">All Sites</option>{sites.map(site => <option key={site.id} value={site.id}>{site.name}</option>)}</select></label>
          </div>
        )}
        <div className="animate-in">
          {tab === 'dashboard' && <OwnerOverview selectedSiteId={selectedSiteId} sites={sites} operators={operators} />}
          {tab === 'sites' && <SiteManagement onSiteCreated={(site, created) => { if (created) { setSetupSiteId(site.id); setSelectedSiteId(site.id); setTab('structure'); } setSiteRefresh(value => value + 1); }} />}
          {tab === 'lots' && <LotManagement siteFilterId={selectedSiteId} />}
          {tab === 'structure' && (setupSiteId && !sites.some(site => site.id === setupSiteId)
            ? <p role="status" className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5 text-sm text-[var(--muted)]">Waiting for the new site to appear in the server list. Use Retry above if it does not load.</p>
            : <ParkingStructure sites={sites} selectedSiteId={selectedSiteId} onChanged={() => setSiteRefresh(value => value + 1)} />)}
          {tab === 'operators' && <OperatorManagement selectedSiteId={selectedSiteId} onOperatorsChanged={() => setSiteRefresh(value => value + 1)} />}
          {tab === 'finance' && <RevenueDashboard selectedSiteId={selectedSiteId} sites={sites} />}
          {tab === 'policy' && <PolicySettings />}
        </div>
          </>
        )}
      </main>
    </DashboardSidebar>
  );
}
