const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const test = require('node:test')
const vm = require('node:vm')
const ts = require('../node_modules/typescript')

const fixtureDir = path.join(__dirname, '../src/fixtures')
const current = JSON.parse(fs.readFileSync(path.join(fixtureDir, 'slots-operational-fixture.json'), 'utf8')).data
const stale = JSON.parse(fs.readFileSync(path.join(fixtureDir, 'slots-operational-stale-fixture.json'), 'utf8')).data
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/
const physical = new Set(['AVAILABLE', 'OCCUPIED', 'UNKNOWN', 'MAINTENANCE', 'UNAVAILABLE'])
const reservation = new Set(['UNRESERVED', 'RESERVED', 'PROTECTED', 'BACKUP'])
const vehicle = new Set(['CAR', 'MOTORCYCLE', 'OVERSIZED'])
const slotType = new Set(['STANDARD', 'EV', 'DISABLED', 'VIP'])

function loadLayout(dev, enabled) {
  const filename = path.join(__dirname, '../src/lib/operationalLayout.ts')
  let source = fs.readFileSync(filename, 'utf8')
  source = source.replaceAll('import.meta.env.VITE_OPERATIONAL_LAYOUT_FIXTURE', JSON.stringify(enabled ? 'true' : 'false'))
    .replaceAll('import.meta.env.DEV', String(dev))
  const code = ts.transpileModule(source, { compilerOptions: {
    module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022,
  } }).outputText
  const context = {
    exports: {}, Date, Promise,
    require: name => {
      if (name === '../fixtures/slots-operational-fixture.json') return { success: true, data: current }
      throw new Error(`Unexpected import: ${name}`)
    },
  }
  vm.runInNewContext(code, context)
  return context.exports
}

function checkSchema(data) {
  assert.equal(data.contractVersion, '1.2.0')
  assert.match(data.siteId, uuid)
  assert.equal(data.stalenessThresholdSeconds, 30)
  assert.equal(data.metrics.totalPhysicalCapacity, data.slots.filter(slot => slot.supportedVehicleType === 'CAR').length)
  assert.equal(data.metrics.occupiedCount, data.slots.filter(slot => slot.physicalState === 'OCCUPIED').length)
  assert.equal(data.metrics.reservedCount, data.slots.filter(slot => slot.reservationState === 'RESERVED').length)
  assert.equal(data.metrics.availableCarSlots, data.slots.filter(slot =>
    slot.supportedVehicleType === 'CAR' && slot.physicalState === 'AVAILABLE' && slot.reservationState === 'UNRESERVED',
  ).length)
  assert.equal(data.metrics.motorcycleCapacity.availableCount,
    data.metrics.motorcycleCapacity.maxCapacity - data.metrics.motorcycleCapacity.occupiedCount)
  assert.equal(data.metrics.motorcycleCapacity.occupiedCount, data.slots.filter(slot =>
    slot.supportedVehicleType === 'MOTORCYCLE' && slot.physicalState === 'OCCUPIED',
  ).length)
  assert.equal(new Set(data.slots.map(slot => slot.slotId)).size, data.slots.length)
  assert.equal(new Set(data.slots.map(slot => slot.slotCode)).size, data.slots.length)
  const floors = new Map(data.floors.map(floor => [floor.floorId, floor]))
  for (const floor of data.floors) {
    assert.match(floor.floorId, uuid)
    assert.equal(floor.maxCapacity, data.slots.filter(slot => slot.floorId === floor.floorId).length)
  }
  for (const slot of data.slots) {
    assert.match(slot.slotId, uuid)
    assert.ok(floors.has(slot.floorId))
    assert.equal(slot.floorCode, floors.get(slot.floorId).floorCode)
    assert.ok(physical.has(slot.physicalState))
    assert.ok(reservation.has(slot.reservationState))
    assert.ok(vehicle.has(slot.supportedVehicleType))
    assert.ok(slotType.has(slot.slotType))
    assert.equal(slot.isBookable, slot.physicalState === 'AVAILABLE' && slot.reservationState === 'UNRESERVED')
    assert.equal(slot.sessionType, null)
    assert.equal(slot.operatorContext, null)
  }
  for (const zone of data.metrics.zoneCapacities) {
    assert.match(zone.zoneId, uuid)
    const slots = data.slots.filter(slot => slot.zoneCode === zone.zoneCode && slot.floorCode === zone.floorCode)
    const reserved = slots.filter(slot => slot.reservationState === 'RESERVED').length
    assert.equal(zone.maxCapacity, slots.length)
    assert.equal(zone.occupiedCount, slots.filter(slot => slot.physicalState === 'OCCUPIED').length)
    assert.equal(zone.allocatedSlotCount, reserved)
    assert.equal(zone.zoneOnlyReservedCount, reserved)
  }
}

test('both fixtures match the verified projection schema and capacity semantics', () => {
  checkSchema(current)
  checkSchema(stale)
  assert.equal(current.siteId, stale.siteId)
  assert.deepEqual(current.slots.map(slot => slot.slotId), stale.slots.map(slot => slot.slotId))
  assert.deepEqual(current.slots.map(slot => slot.slotCode), stale.slots.map(slot => slot.slotCode))
})

test('unknown and reserved overlays never appear available; stale data fails closed', () => {
  const layout = loadLayout(false, false)
  for (const slot of current.slots) {
    const visual = layout.mapSlotVisualState(slot)
    if (slot.physicalState === 'UNKNOWN') assert.equal(visual, 'unknown')
    if (slot.physicalState === 'OCCUPIED') assert.equal(visual, 'occupied')
    if (!slot.isBookable) assert.notEqual(visual, 'available')
  }
  assert.equal(layout.mapSlotVisualState({}), 'unknown')
  assert.equal(layout.isLayoutStale({ ...current, isFresh: undefined }, Date.parse(current.generatedAt)), true)
  assert.equal(layout.isLayoutStale(current, Date.parse(current.generatedAt) + 1000), false)
  assert.equal(layout.isLayoutStale(stale, Date.parse(stale.generatedAt) + 1000), true)
  assert.ok(stale.slots.every(slot => Date.parse(slot.updatedAt) <= Date.parse(stale.generatedAt)))
})

test('only explicitly enabled dev fixture mode loads a simulated current snapshot', async () => {
  assert.equal(await loadLayout(false, true).readOperationalLayoutFixture(), null)
  assert.equal(await loadLayout(true, false).readOperationalLayoutFixture(), null)
  const live = await loadLayout(true, true).readOperationalLayoutFixture()
  assert.equal(live.siteId, current.siteId)
  assert.deepEqual(live.slots.map(slot => slot.slotId), current.slots.map(slot => slot.slotId))
  assert.ok(Math.abs(Date.now() - Date.parse(live.generatedAt)) < 5000)
})

test('fixture mode cannot substitute another parking lot or fall through to HTTP', async () => {
  let source = fs.readFileSync(path.join(__dirname, '../src/lib/parkingApi.ts'), 'utf8')
  source = source.replaceAll('import.meta.env.VITE_PARKING_API_BASE_URL', 'undefined')
  const code = ts.transpileModule(source, { compilerOptions: {
    module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022,
  } }).outputText
  let requests = 0
  const context = {
    exports: {}, Promise,
    require: name => {
      if (name === './authApi') return { request: async () => { requests += 1; throw new Error('unexpected HTTP') } }
      if (name === './operationalLayout') return { readOperationalLayoutFixture: async () => current }
      throw new Error(`Unexpected import: ${name}`)
    },
  }
  vm.runInNewContext(code, context)
  await assert.rejects(context.exports.parkingApi.operationalLayout('00000000-0000-0000-0000-000000000000'),
    /Fixture site ID does not match/)
  assert.equal(requests, 0)
})
