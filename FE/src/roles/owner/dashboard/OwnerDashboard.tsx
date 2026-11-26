import { useEffect, useRef, useState } from 'react';
import { useApp } from '../../../context/AppContext';
import { parkingApi, operatorsApi, type Site, type Operator } from '../../../lib/parkingApi';
import { OnboardingWizard } from '../parking-lots/CreateParkingLotForOwner';
import { OwnerOverview } from './OwnerOverview';
import { OperatorManagement } from '../operators/OperatorManagement';
import { PolicySettings } from '../policies/PolicySettings';
import { OwnerPassPlans } from '../passes/OwnerPassPlans';
import { SiteManagement } from '../parking-lots/SiteManagement';
import { LotManagement } from '../parking-lots/LotManagement';
import { RevenueDashboard } from '../revenue/RevenueDashboard';
import { ParkingStructure } from '../structure/ParkingStructure';
import { ErrorBoundary } from '../../../components/feedback/ErrorBoundary';
import { DashboardSidebar } from '../../../components/layout/DashboardSidebar';
import { reconcileOwnerLotSetups } from '../../../lib/ownerLotSetup';
import { watchOperators } from '../../../lib/operatorLive';
import { AuthApiError, getSessionGeneration } from '../../../lib/authApi';

type Tab = 'dashboard' | 'sites' | 'lots' | 'structure' | 'operators' | 'finance' | 'policy' | 'passes';

// A background site-list reload can return an equal list with a brand-new array
// identity. Keeping the previous array in that case stops every child that depends
// on `sites` (the structure screen's site effect in particular) from re-running for
// no reason. Only a genuine content change replaces the list.
function sameSiteList(current: Site[], next: Site[]): boolean {
  if (current.length !== next.length) return false;
  for (let index = 0; index < current.length; index += 1) {
    if (JSON.stringify(current[index]) !== JSON.stringify(next[index])) return false;
  }
  return true;
}

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
  const [rosterError, setRosterError] = useState('');
  const [rosterDenied, setRosterDenied] = useState(false);
  const [rosterLoading, setRosterLoading] = useState(true);
  const refreshRoster = useRef<(manual?: boolean) => void>(() => {});
  const session = getSessionGeneration();
  useEffect(() => {
    if (user?.role !== 'owner') return;
    let active = true, reading = false, dirty = false, denied = false;
    let restartNeeded = false;
    let stop: (() => void) | undefined;
    const current = () => active && session === getSessionGeneration();
    setOperators([]);
    setRosterError('');
    setRosterDenied(false);
    setRosterLoading(true);
    const suspend = () => {
      denied = true;
      dirty = false;
      stop?.();
      stop = undefined;
      if (!current()) return;
      setOperators([]);
      setRosterDenied(true);
      setRosterError('You do not have permission to manage operators.');
    };
    const refresh = async (manual = false) => {
      if (!current() || (denied && !manual)) return;
      if (manual && (denied || restartNeeded)) {
        denied = false;
        restartNeeded = false;
        stop?.();
        stop = undefined;
      }
      dirty = true;
      if (reading) return;
      reading = true;
      if (manual) setRosterLoading(true);
      try {
        do {
          dirty = false;
          const accounts = await operatorsApi.list();
          if (!current() || denied) return;
          if (!dirty) setOperators(accounts);
          setRosterDenied(false);
          setRosterError('');
          stop ??= watchOperators(() => { void refresh(); }, suspend, () => {
            restartNeeded = true;
            if (current()) setRosterError('Live operator updates are unavailable. Showing the last loaded roster.');
          });
        } while (current() && dirty);
      } catch (cause) {
        if (!current()) return;
        if (cause instanceof AuthApiError && cause.status === 403) suspend();
        else setRosterError(cause instanceof Error ? cause.message : 'Cannot load operators.');
      } finally {
        reading = false;
        if (current()) setRosterLoading(false);
      }
    };
    refreshRoster.current = manual => { void refresh(manual); };
    void refresh();
    return () => { active = false; stop?.(); refreshRoster.current = () => {}; };
  }, [user?.id, user?.role, session]);
  useEffect(() => {
    let current = true;
    setLoading(true); setSitesLoaded(false); setError('');
    parkingApi.list()
      .then(async sites => {
        if (!current || session !== getSessionGeneration()) return;
        await reconcileOwnerLotSetups(sites, user?.id ?? '').catch(() => {});
        if (!current || session !== getSessionGeneration()) return;
        setSites(previous => sameSiteList(previous, sites) ? previous : sites);
        setSitesLoaded(true);
        setSelectedSiteId(previous => previous === setupSiteId || previous === 'all' || sites.some(site => site.id === previous) ? previous : 'all');
      })
      .catch(cause => {
        if (!current || session !== getSessionGeneration()) return;
        setSites([]);
        setError(cause instanceof Error ? cause.message : 'Cannot load owner sites.');
      })
      .finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [user?.id, session, siteRefresh, setupSiteId]);

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
        { id: 'passes', label: 'Parking passes', icon: 'ticket', active: tab === 'passes', onClick: () => setTab('passes') },
      ] },
    ]}>
      <main className={`owner-workspace-main min-w-0 overflow-y-auto${showOnboarding ? '' : ' p-4 sm:p-7'}`}>
        {showOnboarding ? (
          <div className="min-h-[calc(100vh-60px)] bg-[var(--bg)]">
            <OnboardingWizard onComplete={(lot) => { setSetupSiteId(lot.id); setSelectedSiteId(lot.id); setTab('structure'); setSiteRefresh(value => value + 1); }} />
          </div>
        ) : (
          <>
        {loading && <p role="status">Loading owner sites…</p>}
        {error && <div role="alert" className="owner-workspace-error">{error} <button className="btn-outline" onClick={() => setSiteRefresh(value => value + 1)}>Retry</button></div>}
        {(tab === 'dashboard' || tab === 'sites' || tab === 'operators') && rosterError && <div role="alert" className="owner-workspace-error">
          {rosterError} <button type="button" className="btn-outline" disabled={rosterLoading} onClick={() => refreshRoster.current(true)}>Retry operators</button>
        </div>}
        <div key={tab} className="animate-in">
          {tab === 'dashboard' && <OwnerOverview selectedSiteId={selectedSiteId} onSiteChange={setSelectedSiteId} sites={sites}
            operators={operators} rosterUnavailable={rosterDenied || rosterLoading} />}
          {tab === 'sites' && <SiteManagement operators={operators} rosterDenied={rosterDenied} onSiteCreated={(site, created) => { if (created) { setSetupSiteId(site.id); setSelectedSiteId(site.id); setTab('structure'); } setSiteRefresh(value => value + 1); }} />}
          {tab === 'lots' && <LotManagement siteFilterId={selectedSiteId} />}
          {tab === 'structure' && (setupSiteId && !sites.some(site => site.id === setupSiteId)
            ? <p role="status" className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5 text-sm text-[var(--muted)]">Waiting for the new site to appear in the server list. Use Retry above if it does not load.</p>
            : (
                <ErrorBoundary name="Parking Structure">
                  <ParkingStructure sites={sites} selectedSiteId={selectedSiteId} onSiteChange={setSelectedSiteId} />
                </ErrorBoundary>
              ))}
          {tab === 'operators' && <OperatorManagement selectedSiteId={selectedSiteId} liveOperators={operators}
            rosterDenied={rosterDenied} rosterLoading={rosterLoading} onRefreshRoster={() => refreshRoster.current(true)}
            onOperatorsChanged={() => refreshRoster.current()} />}
          {tab === 'finance' && <RevenueDashboard selectedSiteId={selectedSiteId} sites={sites} />}
          {tab === 'policy' && <PolicySettings />}
          {tab === 'passes' && !loading && <OwnerPassPlans sites={sites} />}
        </div>
          </>
        )}
      </main>
    </DashboardSidebar>
  );
}
