import { request } from './authApi';
import type { LotType } from './types';

export interface OwnerApplicationRecord {
  id: string; ownerId: string; ownerName: string; businessName: string;
  email: string; phone: string; lotType: LotType;
  status: 'pending' | 'approved' | 'rejected'; submittedAt: string;
  reviewedAt?: string; reviewedBy?: string; reviewNote?: string;
}
interface Response<T> { success: boolean; data: T }

export const ownerApi = {
  async register(body: { fullName: string; businessName: string; email: string; phone: string;
    password: string; lotType: LotType; agreedToPolicy: boolean }): Promise<OwnerApplicationRecord> {
    const r = await request<Response<OwnerApplicationRecord>>('/register/owner', 'POST', body);
    if (!r.success || !r.data?.id) throw new Error('Invalid registration response.');
    return r.data;
  },
  async list(): Promise<OwnerApplicationRecord[]> {
    const r = await request<Response<OwnerApplicationRecord[]>>('', 'GET', undefined, '/api/owner-applications');
    if (!r.success || !Array.isArray(r.data)) throw new Error('Invalid application response.');
    return r.data;
  },
  async review(id: string, status: 'approved' | 'rejected', reviewNote: string): Promise<OwnerApplicationRecord> {
    const r = await request<Response<OwnerApplicationRecord>>(`/${encodeURIComponent(id)}/review`, 'PATCH',
      { status, reviewNote }, '/api/owner-applications');
    if (!r.success || !r.data?.id) throw new Error('Invalid review response.');
    return r.data;
  },
};
