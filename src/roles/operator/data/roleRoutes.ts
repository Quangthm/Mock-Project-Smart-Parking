import type { OperatorAccessRole, User } from '../../../lib/types';

export const operatorHomePath: Record<OperatorAccessRole, string> = {
  financial: '/dashboard/finance',
  operation: '/dashboard/operation',
  cashier: '/pos',
};

export function getUserHomePath(user: User | null) {
  if (user?.role === 'operator') return operatorHomePath[user.operatorRole ?? 'operation'];
  return '/';
}
