import { createContext, useContext, useState, useEffect, type ReactNode } from 'react';
import type { User } from '../lib/types';
import { store } from '../lib/store';

export type View =
  | 'landing' | 'pricing' | 'support' | 'faq'
  | 'sign-in' | 'sign-up' | 'pending-approval'
  | 'business' | 'about' | 'affiliates' | 'careers'
  | 'driver' | 'owner' | 'operator' | 'admin'
  | 'terms' | 'privacy' | 'payment-refund';

interface AppCtx {
  user: User | null;
  view: View;
  theme: 'light' | 'dark';
  dashboardMenuOpen: boolean;
  accentColor: string;
  authReady: boolean;
  setUser: (u: User | null) => void;
  setView: (v: View) => void;
  toggleTheme: () => void;
  setDashboardMenuOpen: (open: boolean) => void;
  setAccentColor: (color: string) => void;
  signOut: () => void;
}

const Ctx = createContext<AppCtx>({} as AppCtx);
export const useApp = () => useContext(Ctx);

export function AppProvider({ children }: { children: ReactNode }) {
  const [user, setUserState] = useState<User | null>(null);
  const [view, setView] = useState<View>('landing');
  const [theme, setTheme] = useState<'light' | 'dark'>('light');
  const [dashboardMenuOpen, setDashboardMenuOpen] = useState(false);
  const [accentColor, setAccentColorState] = useState('blue');
  const [authReady, setAuthReady] = useState(false);

  useEffect(() => {
    store.init();
    const savedTheme = (localStorage.getItem('sp_theme') as 'light' | 'dark') || 'light';
    setTheme(savedTheme);
    if (savedTheme === 'dark') document.documentElement.classList.add('dark');
    const savedAccent = localStorage.getItem('sp_accent') || 'blue';
    setAccentColorState(savedAccent);
    delete document.documentElement.dataset.accent;

    const uid = store.getCurrentUserId();
    if (uid) {
      const u = store.findUserById(uid);
      if (u) {
        setUserState(u);
        setView(u.role as View);
      }
    }
    setAuthReady(true);
  }, []);

  useEffect(() => {
    store.remindExpiringBookings();
    const timer = window.setInterval(() => store.remindExpiringBookings(), 30_000);
    return () => window.clearInterval(timer);
  }, []);

  const setUser = (u: User | null) => {
    setUserState(u);
    store.setCurrentUserId(u?.id ?? null);
  };

  const toggleTheme = () => {
    const next = theme === 'light' ? 'dark' : 'light';
    setTheme(next);
    localStorage.setItem('sp_theme', next);
    if (next === 'dark') document.documentElement.classList.add('dark');
    else document.documentElement.classList.remove('dark');
  };

  const setAccentColor = (color: string) => {
    setAccentColorState(color);
    localStorage.setItem('sp_accent', color);
  };

  const signOut = () => {
    setUser(null);
    setView('landing');
    setDashboardMenuOpen(false);
    store.addAuditLog({ userId: user?.id ?? '', userName: user?.name ?? '', userRole: user?.role ?? 'driver', action: 'SIGN_OUT', details: 'User signed out' });
  };

  return (
    <Ctx.Provider value={{ user, view, theme, accentColor, dashboardMenuOpen, authReady, setUser, setView, toggleTheme, setDashboardMenuOpen, setAccentColor, signOut }}>
      {children}
    </Ctx.Provider>
  );
}
