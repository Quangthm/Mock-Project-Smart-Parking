import { request } from './authApi';

const server = (import.meta.env.VITE_PARKING_API_BASE_URL ?? 'http://localhost:5045').replace(/\/$/, '');
type Response<T> = { success: boolean; data: T };
export interface Site { id: string; tenantId: string; code: string; name: string; address: string; latitude: number | null; longitude: number | null; status: string; isActive: boolean; totalPhysicalCapacity: number }
export interface Unit { id: string; parentId: string | null; name: string; type: 'FLOOR' | 'ZONE' | 'BLOCK'; maxCapacity: number }
export interface Slot { id: string; unitId: string; code: string; vehicleType: string; type: string; operationalStatus: string; isPhysicallyOccupied: boolean; reservationState: string | null }
export interface Structure { site: Site; units: Unit[]; slots: Slot[]; paths: Array<{id: string; code: string}> }
async function call<T>(path: string, method = 'GET', body?: unknown): Promise<T> {
  const result = await request<Response<T>>(path, method, body, '/api/parking-lots', server);
  if (!result.success) throw new Error('Parking request failed.');
  return result.data;
}
export const parkingApi = {
  list: () => call<Site[]>(''),
  create: (body: { code: string; name: string; address: string }) => call<Site>('', 'POST', body),
  structure: (id: string) => call<Structure>(`/${id}/structure`),
  unit: (id: string, body: { type: string; name: string; parentId: string | null; capacity: number }) => call<string>(`/${id}/units`, 'POST', body),
  slot: (id: string, body: { unitId: string; code: string; vehicleType: string; type: string }) => call<string>(`/${id}/slots`, 'POST', body),
  edit: (id: string, body: Record<string, unknown>) => call<void>(`/${id}/structure`, 'PATCH', body),
};
export interface Operator { id: string; fullName: string; email: string; status: string; siteIds: string[]; permissions: string[] }
export interface OperatorAssignment { siteId: string; tenantId: string; permissions: string[] }
export const operatorsApi = {
  async assignments() { return (await request<Response<OperatorAssignment[]>>('/me', 'GET', undefined, '/api/operators')).data; },
  async list() { return (await request<Response<Operator[]>>('', 'GET', undefined, '/api/operators')).data; },
  async create(body: { fullName: string; email: string; password: string; siteIds: string[]; permissions: string[] }) {
    const result = await request<Response<Operator>>('', 'POST', body, '/api/users');
    if (!result.success) throw new Error('Operator creation failed.');
    return result.data;
  },
};
