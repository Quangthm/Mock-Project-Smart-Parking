import { useMemo, useState } from 'react';
import { useApp } from '../../context/AppContext';
import { NotificationBell } from '../navigation/NotificationBell';
import { BrandLogo } from '../brand/BrandLogo';
import { ThemeIcon } from '../icon/ThemeIcon';
import { UntitledIcon } from '../icon/UntitledIcon';

const rolePages: Record<string, { id: string; label: string }[]> = {
  driver: [{ id: 'find', label: 'Find parking' }, { id: 'bookings', label: 'Current bookings' }, { id: 'history', label: 'Booking history' }, { id: 'subscriptions', label: 'Passes' }, { id: 'support', label: 'Support' }],
  owner: [{ id: 'dashboard', label: 'Overview' }, { id: 'sites', label: 'Parking sites' }, { id: 'lots', label: 'Parking lots' }, { id: 'operators', label: 'Operators' }, { id: 'policy', label: 'Policies' }],
  admin: [{ id: 'overview', label: 'Overview' }, { id: 'applications', label: 'Applications' }, { id: 'drivers', label: 'Driver accounts' }, { id: 'audit', label: 'Audit log' }, { id: 'system', label: 'System configuration' }],
  operator: [{ id: 'checkin', label: 'Check-in and check-out' }, { id: 'status', label: 'Lot status' }, { id: 'slots', label: 'Manage slots' }, { id: 'tickets', label: 'Driver tickets' }, { id: 'emergency', label: 'Emergency' }, { id: 'finance', label: 'Financial reports' }],
};

const accents = [
  { id: 'blue', label: 'Default', color: '#2563eb' }, { id: 'pink', label: 'Pink', color: '#db2777' },
  { id: 'sky', label: 'Blue', color: '#0284c7' }, { id: 'green', label: 'Green', color: '#16a34a' },
  { id: 'red', label: 'Red', color: '#dc2626' }, { id: 'black', label: 'Black', color: '#111827' },
];

export function DashboardNavbar() {
  const { user, theme, toggleTheme, signOut, dashboardMenuOpen, setDashboardMenuOpen, accentColor, setAccentColor } = useApp();
  const [panel, setPanel] = useState<'profile' | 'my-profile' | 'settings' | 'search' | null>(null);
  const [query, setQuery] = useState('');
  const pages = user?.role === 'operator'
    ? user.operatorRole === 'financial' ? rolePages.operator.filter(page => page.id === 'finance')
      : user.operatorRole === 'cashier' ? [] : rolePages.operator.filter(page => page.id !== 'finance')
    : rolePages[user?.role ?? ''] ?? [];
  const results = useMemo(() => pages.filter(page => page.label.toLowerCase().includes(query.trim().toLowerCase())), [pages, query]);

  function navigate(id: string) {
    window.dispatchEvent(new CustomEvent('sp:dashboard-nav', { detail: id }));
    setDashboardMenuOpen(false);
    setPanel(null); setQuery('');
  }

  function openProfile(section: string) {
    if (section === 'vehicles' || (section === 'account' && user?.role === 'driver')) {
      window.dispatchEvent(new CustomEvent('sp:driver-profile', { detail: section }));
      setDashboardMenuOpen(false);
      setPanel(null);
    } else {
      setDashboardMenuOpen(false);
      setPanel('my-profile');
    }
  }

  function toggleNavigation() {
    setPanel(null);
    setDashboardMenuOpen(!dashboardMenuOpen);
  }

  function toggleSearch() {
    const opening = panel !== 'search';
    if (opening) setDashboardMenuOpen(false);
    setPanel(opening ? 'search' : null);
  }

  function toggleProfile() {
    const opening = panel !== 'profile';
    if (opening) setDashboardMenuOpen(false);
    setPanel(opening ? 'profile' : null);
  }

  return <nav className="dashboard-topbar">
    <div className="dashboard-topbar-left">
      {user?.operatorRole !== 'cashier' && !(user?.role === 'owner' && !user.onboardingComplete) && <button type="button" className="icon-button menu-toggle" aria-label={dashboardMenuOpen ? 'Close navigation' : 'Open navigation'} aria-expanded={dashboardMenuOpen} onClick={toggleNavigation}><UntitledIcon name="menu" /></button>}
      <button type="button" className="dashboard-brand" onClick={() => navigate(user?.role === 'driver' ? 'find' : user?.role === 'operator' ? 'checkin' : user?.role === 'owner' ? 'dashboard' : 'overview')} aria-label="SmartParking dashboard">
        <BrandLogo height={60} /><span className="brand-wordmark" style={{ fontSize: '1.1rem' }}><span className="brand-smart">Smart</span><span className="brand-parking">Parking</span></span>
      </button>
    </div>
    <div className="dashboard-topbar-actions">
      {pages.length > 0 && <div className="dashboard-popover-anchor">
        <button type="button" className="icon-button" aria-label="Search pages" aria-expanded={panel === 'search'} onClick={toggleSearch}><UntitledIcon name="search" /></button>
        {panel === 'search' && <div className="dashboard-popover dashboard-search-popover">
          <label className="sr-only" htmlFor="dashboard-search">Search pages and features</label><input id="dashboard-search" autoFocus className="input" placeholder="Search pages and features" value={query} onChange={event => setQuery(event.target.value)} />
          <div className="dashboard-search-results">{results.map(result => <button key={result.id} type="button" onClick={() => navigate(result.id)}>{result.label}<UntitledIcon name="arrow-right" size={16} /></button>)}{!results.length && <p>No matching pages</p>}</div>
        </div>}
      </div>}
      {user && <NotificationBell userId={user.id} />}
      <div className="dashboard-popover-anchor">
        <button type="button" className="profile-trigger" aria-label="Open profile menu" aria-expanded={panel === 'profile' || panel === 'my-profile'} onClick={toggleProfile}><span className="profile-avatar">{user?.avatar ? <img src={user.avatar} alt="" /> : user?.name?.charAt(0).toUpperCase() ?? '?'}</span><span className="profile-trigger-name">{user?.name}</span><UntitledIcon name="chevron-down" size={16} /></button>
        {(panel === 'profile' || panel === 'my-profile') && <div className="dashboard-popover profile-popover">
          {panel === 'my-profile' && <div className="popover-title-row"><strong>My Profile</strong><button type="button" className="text-button" onClick={() => setPanel('profile')}>Back</button></div>}
          <div className="profile-summary"><span className="profile-avatar profile-avatar-large">{user?.name?.charAt(0).toUpperCase() ?? '?'}</span><div><strong>{user?.name}</strong><span>{user?.email}</span><small>{user?.role === 'operator' ? `${user.operatorRole ?? 'Operator'} operator` : user?.role}</small></div></div>
          {panel === 'my-profile' && <div className="profile-details">
            <div><span>Full name</span><strong>{user?.name}</strong></div><div><span>Email</span><strong>{user?.email}</strong></div>
            <div><span>Phone</span><strong>{user?.phone || 'Not provided'}</strong></div><div><span>Role</span><strong>{user?.role === 'operator' ? `${user.operatorRole ?? 'Operator'} operator` : user?.role}</strong></div>
            {user?.createdAt && <div><span>Member since</span><strong>{new Date(user.createdAt).toLocaleDateString()}</strong></div>}
          </div>}
          {panel === 'my-profile' && user?.role === 'admin' && <button type="button" className="popover-item" onClick={() => navigate('settings')}><UntitledIcon name="settings" size={16} /> <span>Edit profile and security</span></button>}
          {panel === 'my-profile' && user?.role !== 'admin' && <button type="button" className="popover-item" onClick={() => setPanel('profile')}><UntitledIcon name="arrow-left" size={16} /> <span>Profile menu</span></button>}
          {panel === 'profile' && <>
          <div className="popover-divider" />
          <button type="button" className="popover-item" onClick={() => openProfile('account')}><UntitledIcon name="users" size={16} /> <span>My Profile</span></button>
          {user?.role === 'driver' && <button type="button" className="popover-item" onClick={() => openProfile('vehicles')}><UntitledIcon name="car" size={16} /> <span>My Vehicles</span></button>}
          <button type="button" className="popover-item" onClick={() => setPanel('settings')}><UntitledIcon name="settings" size={16} /> <span>Settings</span></button>
          <div className="popover-divider" />
          <button type="button" className="popover-item signout-item" onClick={signOut}><UntitledIcon name="log-out" size={16} /> <span>Sign Out</span></button>
          </>}
        </div>}
        {panel === 'settings' && <div className="dashboard-popover settings-popover">
          <div className="popover-title-row"><strong>Settings</strong><button type="button" className="text-button" onClick={() => setPanel('profile')}>Back</button></div>
          <div className="appearance-label">Appearance</div>
          <div className="theme-options">{(['light', 'dark'] as const).map(option => <button type="button" key={option} className={`theme-option${theme === option ? ' selected' : ''}`} onClick={() => { if (theme !== option) toggleTheme(); }} aria-pressed={theme === option}><span className="theme-option-icon"><ThemeIcon mode={option} /></span><span>{option === 'light' ? 'Light' : 'Dark'}</span></button>)}</div>
          <div className="appearance-label accent-label">Accent color</div>
          <div className="accent-options">{accents.map(accent => <button key={accent.id} type="button" className={`accent-option${accentColor === accent.id ? ' selected' : ''}`} onClick={() => setAccentColor(accent.id)} aria-label={accent.label} aria-pressed={accentColor === accent.id}><span style={{ background: accent.color }} />{accent.label}</button>)}</div>
        </div>}
      </div>
    </div>
    {(panel === 'profile' || panel === 'my-profile') && <button type="button" className="popover-dismiss" aria-label="Close profile menu" onClick={() => setPanel(null)} />}
    {panel === 'search' && <button type="button" className="popover-dismiss" aria-label="Close search" onClick={() => setPanel(null)} />}
    {panel === 'settings' && <button type="button" className="popover-dismiss" aria-label="Close settings" onClick={() => setPanel(null)} />}
  </nav>;
}
