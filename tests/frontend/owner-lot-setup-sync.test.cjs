const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const test = require('node:test')
const vm = require('node:vm')
const ts = require('../../FE/node_modules/typescript')

const source = fs.readFileSync(path.join(__dirname, '../../FE/src/roles/owner/parking-lots/useLotManagement.ts'), 'utf8')
const code = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText
const site = { id: 'site-1', name: 'Registered lot', code: 'LOT-1', address: 'Main Street', isActive: true }
const setup = { id: site.id, ownerId: 'owner-1', type: 'basement', modelFileName: 'underground_parking_lot.glb', hourlyRate: 10000, dailyRate: 200000, nightRate: 70000 }

function renderWithCache(lots) {
  const state = [], effects = []
  let cursor = 0
  const react = {
    useState(initial) { const index = cursor++; if (!(index in state)) state[index] = initial; return [state[index], value => { state[index] = typeof value === 'function' ? value(state[index]) : value }] },
    useEffect(effect) { effects.push(effect) },
    useMemo(compute) { return compute() },
  }
  const module = { exports: {} }
  vm.runInNewContext(code, { module, exports: module.exports, Error, Map, Promise, require(name) {
    if (name === 'react') return react
    if (name.endsWith('/AppContext')) return { useApp: () => ({ user: { id: 'owner-1' } }) }
    if (name.endsWith('/parkingApi')) return { parkingApi: { list: async () => [site], structure: async () => ({ capacityViews: [{ unitId: null, total: 8, available: 8, occupied: 0, protected: 0, pendingPayment: 0, effectiveBackup: 0, unavailable: 0 }] }) } }
    if (name.endsWith('/data/data')) return { ownerData: { getLotsByOwner: id => id === 'owner-1' ? lots : [] } }
    if (name.endsWith('/ownerLotSetup')) return { reconcileOwnerLotSetups: async () => {} }
    throw new Error(`Unexpected import: ${name}`)
  } })
  const render = () => { cursor = 0; return module.exports.useLotManagement() }
  render(); effects.splice(0).forEach(effect => effect())
  return render
}

test('Owner lot list joins a confirmed site to its locally saved setup without replacing live capacity', async () => {
  const render = renderWithCache([setup])
  await new Promise(resolve => setImmediate(resolve))
  const lot = render().filteredData[0]
  assert.equal(lot.setup.type, 'basement')
  assert.equal(lot.setup.modelFileName, 'underground_parking_lot.glb')
  assert.equal(lot.setup.hourlyRate, 10000)
  assert.equal(lot.metrics.available, 8)
})

test('Owner lot list does not invent setup metadata when this browser has none', async () => {
  const render = renderWithCache([])
  await new Promise(resolve => setImmediate(resolve))
  assert.equal(render().filteredData[0].setup, null)
})

test('approved site reuses the registered setup and copies its Owner model to the site ID', async () => {
  const source = fs.readFileSync(path.join(__dirname, '../../FE/src/lib/ownerLotSetup.ts'), 'utf8')
  const compiled = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText
  const draft = { ...setup, id: 'application-1', name: 'Registered lot', address: 'Main Street', modelUrl: 'blob:old' }
  const saved = [], copies = []
  const module = { exports: {} }
  vm.runInNewContext(compiled, { module, exports: module.exports, require(name) {
    if (name === './store') return { store: { getLots: () => [draft], saveLot: lot => saved.push(lot) } }
    if (name === './3d/ownerModelStorage') return { copyOwnerModel: async (from, to) => { copies.push([from, to]); return 'blob:new' } }
    throw new Error(`Unexpected import: ${name}`)
  } })
  await module.exports.reconcileOwnerLotSetups([site], 'owner-1')
  assert.deepEqual(copies, [['application-1', 'site-1']])
  assert.equal(saved[0].id, site.id)
  assert.equal(saved[0].modelUrl, 'blob:new')
  assert.equal(saved[0].hourlyRate, draft.hourlyRate)
})
