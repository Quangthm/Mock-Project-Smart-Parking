import { useState, type FormEvent } from 'react';
import { useApp } from '../../../context/AppContext';
import { UntitledIcon } from '../../../components/icon/UntitledIcon';
import { ownerData as store } from '../data/data';
import type { LotType, ParkingLot, ParkingSlot, User } from '../../../lib/types';
import { ALLOWED_PARKING_MODELS } from '../../../lib/3d/parking3DConfig';

interface SiteFormState {
  name: string;
  address: string;
  type: LotType;
  totalSlots: string;
  floors: string;
}

const emptyForm: SiteFormState = { name: '', address: '', type: 'outdoor', totalSlots: '', floors: '1' };
const fieldClass = 'input w-full';

export function SiteManagement({ sites, operators, onSiteCreated }: { sites: ParkingLot[]; operators: User[]; onSiteCreated: () => void }) {
  const { user } = useApp();
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingSite, setEditingSite] = useState<ParkingLot | null>(null);
  const [form, setForm] = useState<SiteFormState>(emptyForm);
  const [error, setError] = useState('');

  function openCreateModal() {
    setEditingSite(null);
    setForm(emptyForm);
    setError('');
    setIsModalOpen(true);
  }

  function openEditModal(site: ParkingLot) {
    setEditingSite(site);
    setForm({ name: site.name, address: site.address, type: site.type, totalSlots: String(site.totalSlots), floors: String(site.floors) });
    setError('');
    setIsModalOpen(true);
  }

  function closeModal() {
    setEditingSite(null);
    setForm(emptyForm);
    setError('');
    setIsModalOpen(false);
  }

  function buildSlots(existing: ParkingSlot[], count: number, floors: number) {
    const protectedSlots = existing.filter(slot => slot.status !== 'available');
    if (protectedSlots.length > count) return null;

    const keptSlots = [...protectedSlots, ...existing.filter(slot => slot.status === 'available')].slice(0, count);
    const newCount = count - keptSlots.length;
    const generated = newCount > 0 ? store.generateSlots(newCount, floors) : [];
    const usedNumbers = new Set(keptSlots.map(slot => slot.number));
    let nextNumber = Math.max(0, ...keptSlots.map(slot => Number(slot.number.replace(/\D/g, '')) || 0)) + 1;
    const newSlots = generated.map((slot, index) => {
      while (usedNumbers.has(String(nextNumber))) nextNumber += 1;
      const number = String(nextNumber++);
      usedNumbers.add(number);
      return { ...slot, number, status: 'available' as const, reservedBy: undefined, reservedUntil: undefined, floor: (keptSlots.length + index) % floors + 1 };
    });

    const slotsPerFloor = Math.ceil(count / floors);
    return [...keptSlots, ...newSlots].map((slot, index) => ({ ...slot, floor: Math.min(floors, Math.floor(index / slotsPerFloor) + 1) }));
  }

  function saveSite(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const totalSlots = Number(form.totalSlots);
    const floors = form.type === 'outdoor' ? 1 : Number(form.floors);
    if (!user || !form.name.trim() || !form.address.trim() || !Number.isInteger(totalSlots) || totalSlots < 1 || totalSlots > 2000 || !Number.isInteger(floors) || floors < 1 || floors > 20) {
      setError('Enter a site name, address, 1–2,000 slots, and a valid floor count.');
      return;
    }

    const slots = buildSlots(editingSite?.slots ?? [], totalSlots, floors);
    if (!slots) {
      setError('The slot count cannot be lower than the number of occupied or reserved slots.');
      return;
    }

    if (editingSite) {
      const modelDef = ALLOWED_PARKING_MODELS[form.type];
      const updatedSite = {
        ...editingSite,
        name: form.name.trim(),
        address: form.address.trim(),
        type: form.type,
        totalSlots,
        floors,
        slots,
        modelFileName: editingSite.modelFileName || modelDef.fileName,
        modelUrl: editingSite.modelUrl || modelDef.path,
      };
      console.log('Mock update site payload', updatedSite);
      store.saveLot(updatedSite);
    } else {
      const modelDef = ALLOWED_PARKING_MODELS[form.type];
      const newSite = {
        ownerId: user.id,
        name: form.name.trim(),
        type: form.type,
        address: form.address.trim(),
        slots,
        totalSlots,
        floors,
        devices: [],
        hourlyRate: 15000,
        dailyRate: 100000,
        nightRate: 50000,
        gracePeriodMinutes: 15,
        subscriptionMonthly: 800000,
        subscriptionYearly: 8000000,
        modelFileName: modelDef.fileName,
        modelUrl: modelDef.path,
        status: 'active' as const,
        lat: 10.77 + Math.random() * 0.1,
        lng: 106.69 + Math.random() * 0.1,
      };
      console.log('Mock create site payload', newSite);
      store.createLot(newSite);
    }

    closeModal();
    onSiteCreated();
  }

  return (
    <section className="space-y-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div><h1 className="text-2xl font-bold text-[var(--fg)]">Site Management</h1><p className="mt-2 text-sm text-[var(--muted)]">Manage parking branches, parking capacity, and assigned employees.</p></div>
        <button type="button" className="btn-primary" onClick={openCreateModal}>Add New Site</button>
      </header>

      <div className="overflow-x-auto rounded-xl border border-[var(--border)] bg-[var(--card)]">
        <table className="w-full min-w-[720px] text-left text-sm">
          <thead className="border-b border-[var(--border)] text-xs uppercase tracking-wide text-[var(--muted)]"><tr><th className="px-4 py-3">Site</th><th className="px-4 py-3">Address</th><th className="px-4 py-3">Capacity</th><th className="px-4 py-3 text-right">Employees</th><th className="px-4 py-3">Actions</th></tr></thead>
          <tbody className="divide-y divide-[var(--border)]">
            {sites.map(site => {
              const employeeCount = operators.filter(operator => operator.operatorSiteId === site.id || operator.operatorSiteId === 'all').length;
              return <tr key={site.id}><td className="px-4 py-4 font-semibold text-[var(--fg)]">{site.name}</td><td className="px-4 py-4 text-[var(--muted)]">{site.address}</td><td className="px-4 py-4 text-[var(--fg)]">{site.totalSlots} slots · {site.floors} {site.floors === 1 ? 'level' : 'levels'}</td><td className="px-4 py-4 text-right font-medium text-[var(--fg)]">{employeeCount}</td><td className="px-4 py-4"><button type="button" className="btn-outline" style={{ fontSize: '0.8rem', padding: '0.35rem 0.75rem' }} onClick={() => openEditModal(site)}>Edit</button></td></tr>;
            })}
            {!sites.length && <tr><td colSpan={5} className="px-4 py-10 text-center text-[var(--muted)]">No sites found. Add your first parking site.</td></tr>}
          </tbody>
        </table>
      </div>

      {isModalOpen && <div className="fixed inset-0 z-[200] flex items-center justify-center bg-slate-950/60 p-4" role="presentation" onMouseDown={event => { if (event.target === event.currentTarget) closeModal(); }}>
        <section role="dialog" aria-modal="true" aria-labelledby="site-modal-title" className="max-h-[90vh] w-full max-w-2xl overflow-y-auto rounded-2xl border border-[var(--border)] bg-[var(--bg)] p-5 shadow-2xl sm:p-7">
          <div className="mb-6 flex items-start justify-between gap-4">
            <div><p className="text-sm font-medium text-[var(--primary)]">SmartParking · Site setup</p><h2 id="site-modal-title" className="mt-1 text-2xl font-bold text-[var(--fg)]">{editingSite ? 'Edit Parking Site' : 'Add a Parking Site'}</h2><p className="mt-2 text-sm text-[var(--muted)]">Configure the site structure and parking capacity, similar to the Owner sign-up setup.</p></div>
            <button type="button" className="btn-outline" onClick={closeModal} aria-label="Close site form"><UntitledIcon name="x" size={18} /></button>
          </div>

          <form onSubmit={saveSite} className="space-y-5">
            <div className="grid gap-4 sm:grid-cols-2">
              <label className="space-y-2 text-sm font-medium text-[var(--fg)]">Site name<input className={fieldClass} value={form.name} onChange={event => setForm(current => ({ ...current, name: event.target.value }))} placeholder="e.g. Site 2 - Cho Ray" required /></label>
              <label className="space-y-2 text-sm font-medium text-[var(--fg)]">Parking lot type<select className={fieldClass} value={form.type} onChange={event => setForm(current => ({ ...current, type: event.target.value as LotType, floors: event.target.value === 'outdoor' ? '1' : current.floors === '1' ? '2' : current.floors }))}><option value="outdoor">Outdoor Lot</option><option value="basement">Basement Parking</option><option value="multi-storey">Multi-Storey</option></select></label>
              <label className="space-y-2 text-sm font-medium text-[var(--fg)] sm:col-span-2">Address<input className={fieldClass} value={form.address} onChange={event => setForm(current => ({ ...current, address: event.target.value }))} placeholder="Street, district, city" required /></label>
              <label className="space-y-2 text-sm font-medium text-[var(--fg)]">Total slots<input className={fieldClass} type="number" min="1" max="2000" value={form.totalSlots} onChange={event => setForm(current => ({ ...current, totalSlots: event.target.value }))} placeholder="e.g. 50" required /></label>
              <label className="space-y-2 text-sm font-medium text-[var(--fg)]">Levels<input className={fieldClass} type="number" min="1" max="20" value={form.floors} onChange={event => setForm(current => ({ ...current, floors: event.target.value }))} disabled={form.type === 'outdoor'} required /></label>
            </div>
            {form.totalSlots && Number(form.totalSlots) > 0 && <p className="text-xs text-[var(--muted)]">About {Math.ceil(Number(form.totalSlots) / (form.type === 'outdoor' ? 1 : Math.max(1, Number(form.floors))))} slots per level.</p>}
            {error && <p role="alert" className="rounded-lg bg-red-500/10 px-4 py-3 text-sm text-red-600">{error}</p>}
            <div className="flex justify-end gap-2 border-t border-[var(--border)] pt-4"><button type="button" className="btn-outline" onClick={closeModal}>Cancel</button><button type="submit" className="btn-primary">{editingSite ? 'Save Changes' : 'Create Site'}</button></div>
          </form>
        </section>
      </div>}
    </section>
  );
}
