const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const test = require('node:test');
const ts = require('../node_modules/typescript');
const settle = () => new Promise(resolve => setImmediate(resolve));
function load(file, mocks) {
  const source = fs.readFileSync(path.join(__dirname, '../src', file), 'utf8').replace(/import\.meta\.env\.\w+/g, 'undefined');
  const js = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX } }).outputText;
  const context = { exports: {}, URLSearchParams, Error, require(name) {
    if (name.endsWith('.css')) return {};
    assert.ok(name in mocks, `Unexpected import ${name}`); return mocks[name];
  } };
  vm.runInNewContext(js, context); return context.exports;
}
const id = n => `b0000000-0000-4000-8000-${String(n).padStart(12, '0')}`;
const row = n => ({ reservationId: id(n), siteId: id(99), state: 'COMPLETED', start: '2026-11-15T01:00:00Z', end: '2026-11-15T03:00:00Z', createdAt: '2026-11-15T00:00:00.1234567+00:00', bookedAmount: 0, depositAmount: 0, currency: 'VND', paymentStatus: null, licensePlate: '51A-123.45', allocatedSlotId: id(100), allocationStatus: 'RELEASED' });

test('history API sends paired cursor without losing timestamp precision and uses authenticated Operations request', async () => {
  const calls = [];
  const { reservationsApi } = load('lib/reservationsApi.ts', { './authApi': { request: async (...args) => { calls.push(args); return { success: true, data: [row(1)] }; } } });
  await reservationsApi.historyPage(9);
  await reservationsApi.historyPage(9, { before: row(1).createdAt, beforeId: id(1) });
  assert.equal(calls[0][0], '/me/reservations?pageSize=9');
  const query = new URLSearchParams(calls[1][0].split('?')[1]);
  assert.equal(query.get('before'), row(1).createdAt); assert.equal(query.get('beforeId'), id(1));
  assert.equal(calls[1][3], '/api'); assert.equal(calls[1][4], 'http://localhost:5055');
  await assert.rejects(reservationsApi.historyPage(101), /page size/);
  await assert.rejects(reservationsApi.historyPage(9, { before: row(1).createdAt }), /cursor/);
});

function fixture(options = {}) {
  const slots = [], queued = [];
  let index = 0, scope = 'driver-A', generation = 1, result;
  const calls = { pages: [], sites: [], details: [] };
  const react = {
    useState(initial) { const i = index++; slots[i] ??= { value: typeof initial === 'function' ? initial() : initial }; return [slots[i].value, value => { slots[i].value = typeof value === 'function' ? value(slots[i].value) : value; }]; },
    useRef(initial) { const i = index++; return slots[i] ??= { current: initial }; },
    useEffect(fn, deps) {
      const i = index++, previous = slots[i];
      if (!previous || deps.some((value, j) => !Object.is(value, previous.deps[j]))) queued.push(() => {
        previous?.cleanup?.(); slots[i] = { deps, fn, cleanup: fn() };
      });
    },
  };
  const { useReservationHistory } = load('roles/driver/history/useReservationHistory.ts', {
    react,
    '../../../lib/authApi': { getSessionGeneration: () => generation, request: async (...args) => {
      calls.sites.push(args); if (options.siteError) throw new Error('Site unavailable');
      return { success: true, data: { siteId: id(99), siteName: 'Central Parking', slots: [{ slotId: id(100), slotCode: 'B1_Car_001' }] } };
    } },
    '../../../lib/reservationsApi': { reservationsApi: {
      historyPage: async (size, cursor) => { calls.pages.push({ size, cursor }); return options.page ? options.page(cursor) : Array.from({ length: 9 }, (_, i) => row(i + 1)); },
      booking: async value => { calls.details.push(value); return options.detail ? options.detail(value) : { ...row(1), reservationId: value, state: 'NO_SHOW' }; },
    } },
  });
  const render = () => { index = 0; result = useReservationHistory(scope); return result; };
  const effects = () => queued.splice(0).forEach(fn => fn());
  return { calls, render,
    async flush() { render(); effects(); await settle(); render(); effects(); await settle(); return render(); },
    switchAccount(next) { scope = next; generation++; return render(); },
    replayEffects() { for (const entry of slots) if (entry?.fn) { entry.cleanup?.(); entry.cleanup = entry.fn(); } },
    unmount() { for (const entry of slots) entry?.cleanup?.(); },
    get result() { return result; },
  };
}

test('cursor pages use an extra row for lookahead and the eighth row for continuation; site reads are deduplicated', async () => {
  const f = fixture({ page: cursor => cursor ? [row(10)] : Array.from({ length: 9 }, (_, i) => row(i + 1)) });
  let h = await f.flush(); assert.equal(h.items.length, 8); assert.equal(h.hasNext, true);
  assert.equal(f.calls.sites.length, 1); h.next(); h = await f.flush();
  assert.equal(h.pageNumber, 2); assert.equal(h.items.length, 1); assert.equal(h.hasNext, false);
  assert.equal(f.calls.pages[1].cursor.beforeId, id(8)); assert.equal(f.calls.pages[1].cursor.before, row(8).createdAt);
  assert.equal(f.calls.sites.length, 1); h.previous(); h = await f.flush(); assert.equal(h.pageNumber, 1);
  h.refresh(); h = await f.flush(); assert.equal(f.calls.sites.length, 2); f.unmount();
});

test('site lookup failure does not erase reservation history; no payment status is fabricated', async () => {
  const f = fixture({ siteError: true, page: () => [row(1)] }); const h = await f.flush();
  assert.equal(h.items.length, 1); assert.equal(h.items[0].paymentStatus, null);
  assert.equal(h.sites[id(99)], null); assert.equal(h.error, ''); assert.equal(h.hasNext, false); f.unmount();
});

test('a late page cannot restore data after an account switch; history and detail are immediately hidden', async () => {
  let release;
  const f = fixture({ page: () => new Promise(resolve => { release = resolve; }) });
  await f.flush(); const hidden = f.switchAccount('driver-B');
  assert.equal(hidden.items.length, 0); assert.equal(hidden.selectedId, null);
  release([row(1)]); await settle(); assert.equal(f.render().items.length, 0); f.unmount();
});

test('details are fetched on demand, preserve server outcome, and ignore late replies after closing', async () => {
  let release;
  const f = fixture({ page: () => [row(1)], detail: () => new Promise(resolve => { release = resolve; }) });
  let h = await f.flush(); assert.equal(f.calls.details.length, 0);
  h.openDetails(id(1)); h = await f.flush(); assert.equal(h.detail.loading, true);
  h.closeDetails(); await f.flush(); release({ ...row(1), state: 'NO_SHOW' }); await settle();
  h = f.render(); assert.equal(h.selectedId, null); assert.equal(h.detail.data, null); f.unmount();
});

test('failed history retries explicitly and empty history performs no site enrichment', async () => {
  let fail = true;
  const f = fixture({ page: () => { if (fail) throw new Error('History unavailable'); return []; } });
  let h = await f.flush(); assert.equal(h.error, 'History unavailable');
  fail = false; h.refresh(); h = await f.flush(); assert.equal(h.error, ''); assert.equal(h.items.length, 0);
  assert.equal(f.calls.sites.length, 0); f.unmount();
});

test('StrictMode effect replay shares an in-flight history page', async () => {
  let release;
  const f = fixture({ page: () => new Promise(resolve => { release = resolve; }) });
  await f.flush(); f.replayEffects(); await settle(); assert.equal(f.calls.pages.length, 1);
  release([row(1)]); await settle(); await f.flush(); assert.equal(f.result.items.length, 1); f.unmount();
});

test('zero prices remain zero and unknown amounts do not become zero', () => {
  const { historyMoney } = load('roles/driver/history/BookingHistory.tsx', {
    'react': {},
    'react/jsx-runtime': {}, '../../../context/AppContext': {}, '../../../lib/authApi': {}, '../../../lib/operationalLayout': {},
    '../../../components/history/ParkingHistory': {}, '../../../components/ui/PageHeader': {}, '../../../components/ui/Modal': {},
    '../../../components/ui/Badge': {}, '../../../components/icon/UntitledIcon': {}, './useReservationHistory': {},
  });
  assert.equal(historyMoney(0, 'VND'), '0 VND'); assert.equal(historyMoney(null, 'VND'), 'Unavailable');
  assert.equal(historyMoney(25000, 'VND'), '25,000 VND');
});

test('parking scans show provided images and explain absent or broken evidence', () => {
  let failed = false;
  const jsx = (type, props) => ({ type, props });
  const { ReservationScan } = load('roles/driver/history/BookingHistory.tsx', {
    'react': { useState: () => [failed, value => { failed = value; }] },
    'react/jsx-runtime': { jsx, jsxs: jsx }, '../../../context/AppContext': {}, '../../../lib/authApi': {}, '../../../lib/operationalLayout': {},
    '../../../components/history/ParkingHistory': {}, '../../../components/ui/PageHeader': {}, '../../../components/ui/Modal': {},
    '../../../components/ui/Badge': {}, '../../../components/icon/UntitledIcon': {}, './useReservationHistory': {},
  });
  const src = 'https://example.test/check-in.jpg';
  let scan = ReservationScan({ label: 'Check-in', src });
  const link = scan.props.children[1];
  assert.equal(link.type, 'a'); assert.equal(link.props.href, src);
  assert.equal(link.props.children[0].props.src, src);
  link.props.children[0].props.onError();
  scan = ReservationScan({ label: 'Check-in', src });
  assert.match(scan.props.children[1].props.children, /could not be loaded/);
  failed = false;
  scan = ReservationScan({ label: 'Check-out', src: null });
  assert.match(scan.props.children[1].props.children, /No scan image was provided/);
  assert.equal(scan.props.children[1].type, 'p');
});
