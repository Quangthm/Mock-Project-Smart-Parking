import type { ReactNode } from 'react';
import { useApp } from '../../context/AppContext';
import { UntitledIcon } from '../icon/UntitledIcon';

export type DashboardNavItem = { id: string; label: string; icon?: string; active: boolean; onClick: () => void };
export type DashboardNavGroup = { label?: string; items: DashboardNavItem[] };

export function DashboardSidebar({ groups, children }: { groups: DashboardNavGroup[]; children: ReactNode }) {
  const { dashboardMenuOpen, setDashboardMenuOpen } = useApp();
  return <div className={`dashboard-workspace${dashboardMenuOpen ? ' is-menu-open' : ''}`}>
    <aside className={`dashboard-sidebar${dashboardMenuOpen ? ' is-open' : ''}`} aria-label="Dashboard navigation" aria-hidden={!dashboardMenuOpen}>
      {groups.map((group, index) => <div className="dashboard-nav-group" key={group.label ?? index}>
        {group.label && <div className="dashboard-nav-label">{group.label}</div>}
        {group.items.map(item => <button key={item.id} type="button" className={`dashboard-nav-item${item.active ? ' active' : ''}`} onClick={() => { item.onClick(); setDashboardMenuOpen(false); }} tabIndex={dashboardMenuOpen ? 0 : -1}>
          <span className="dashboard-nav-icon"><UntitledIcon name={item.icon ?? item.label} size={18} /></span><span>{item.label}</span>
        </button>)}
      </div>)}
    </aside>
    {dashboardMenuOpen && <button className="dashboard-scrim" aria-label="Close navigation" onClick={() => setDashboardMenuOpen(false)} />}
    <div className="dashboard-content">{children}</div>
  </div>;
}
