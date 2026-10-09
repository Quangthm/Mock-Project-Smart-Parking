import { useState, type FormEvent } from 'react';
import { parkingApi, type Site } from '../../../lib/parkingApi';

export function SiteForm({ site, onSaved, onCancel }: { site?: Site; onSaved: (site: Site) => void; onCancel: () => void }) {
  const [form, setForm] = useState({ code: site?.code ?? '', name: site?.name ?? '', address: site?.address ?? '' });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    const body = { code: form.code.trim().toUpperCase(), name: form.name.trim(), address: form.address.trim() };
    if (!body.code || !body.name || !body.address) { setError('Enter a site code, name and address.'); return; }
    setError(''); setBusy(true);
    try {
      if (site) {
        await parkingApi.edit(site.id, { action: 'profile', ...body, latitude: site.latitude, longitude: site.longitude });
        onSaved({ ...site, ...body });
      } else {
        onSaved(await parkingApi.create(body));
      }
    } catch (err) { setError(err instanceof Error ? err.message : 'Cannot save parking site.'); }
    finally { setBusy(false); }
  }
  return <form className="card space-y-4" onSubmit={submit}>
    <h3>{site ? 'Edit parking site' : 'Create parking site'}</h3>
    <p className="text-sm text-[var(--muted)]">{site ? 'Update the site name and address.' : 'Create an active site for your operators. Parking capacity starts at zero; configure its layout separately.'}</p>
    <fieldset disabled={busy} className="space-y-4" style={{ border: 0, padding: 0, margin: 0 }}>
      <div><label className="label" htmlFor="site-code">Site code</label><input id="site-code" className="input" maxLength={50} value={form.code} onChange={e => setForm(current => ({ ...current, code: e.target.value }))} required /></div>
      <div><label className="label" htmlFor="site-name">Site name</label><input id="site-name" className="input" maxLength={255} value={form.name} onChange={e => setForm(current => ({ ...current, name: e.target.value }))} required /></div>
      <div><label className="label" htmlFor="site-address">Address</label><input id="site-address" className="input" maxLength={2000} value={form.address} onChange={e => setForm(current => ({ ...current, address: e.target.value }))} required /></div>
      {error && <p role="alert" className="text-sm text-red-600">{error}</p>}
      <div className="flex justify-end gap-2"><button type="button" className="btn-outline" onClick={onCancel}>Cancel</button><button type="submit" className="btn-primary" disabled={busy}>{busy ? 'Saving…' : site ? 'Save Changes' : 'Create Site'}</button></div>
    </fieldset>
  </form>;
}
