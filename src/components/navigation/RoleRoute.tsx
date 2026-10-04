import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { useApp } from '../../context/AppContext';
import type { OperatorAccessRole, Role } from '../../lib/types';
import { getUserHomePath } from '../../roles/operator/data/roleRoutes';

export function RoleRoute({ allowedRoles, children }: { allowedRoles: Array<Role | OperatorAccessRole>; children: ReactNode }) {
  const { user, authReady } = useApp();

  if (!authReady) return <div className="p-8 text-center text-sm text-[var(--muted)]">Loading account…</div>;
  if (!user) return <Navigate to="/" replace />;

  const hasAccess = allowedRoles.includes(user.role)
    || (user.role === 'operator' && allowedRoles.includes(user.operatorRole ?? 'operation'));

  return hasAccess ? children : <Navigate to={getUserHomePath(user)} replace />;
}
