const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const test = require('node:test')
const vm = require('node:vm')
const ts = require('../../FE/node_modules/typescript')

const source = fs.readFileSync(path.join(__dirname, '../../FE/src/lib/dbResetCleanup.ts'), 'utf8')
const code = ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText

const resetId = 'a'.repeat(32)
const sampleLot = { id: 'lot-001' }
const oldLot = { id: 'old-lot' }
const newLot = { id: 'new-lot' }

function fixture(fetch) {
  const storage = new Map([['sp_theme', 'dark']])
  const module = { exports: {} }
  const localStorage = {
    getItem: (key) => storage.get(key) ?? null,
    setItem: (key, value) => storage.set(key, value),
  }
  vm.runInNewContext(code, { module, exports: module.exports, fetch, localStorage, AbortSignal })
  return {
    storage,
    clean: module.exports.cleanLotsAfterDbReset,
    saveLots(lots) {
      storage.set('sp_lots', JSON.stringify(lots))
      storage.set('sp_parking_structures', JSON.stringify(
        Object.fromEntries(lots.map(({ id }) => [id, { lotId: id }])),
      ))
    },
  }
}

test('cleans lots and their structures once per reset while keeping the sample lot and theme', async () => {
  let marker = resetId
  const { storage, clean, saveLots } = fixture(async () => new Response(marker))
  saveLots([sampleLot, oldLot])

  await clean()
  assert.deepEqual(JSON.parse(storage.get('sp_lots')), [sampleLot])
  assert.deepEqual(JSON.parse(storage.get('sp_parking_structures')), {
    'lot-001': { lotId: sampleLot.id },
  })
  assert.equal(storage.get('sp_theme'), 'dark')

  saveLots([sampleLot, newLot])
  await clean()
  assert.deepEqual(JSON.parse(storage.get('sp_lots')), [sampleLot, newLot])

  marker = 'b'.repeat(32)
  await clean()
  assert.deepEqual(JSON.parse(storage.get('sp_lots')), [sampleLot])
})

test('keeps lots when the reset file is missing or invalid', async () => {
  for (const response of [new Response(null, { status: 404 }), new Response('invalid')]) {
    const { storage, clean, saveLots } = fixture(async () => response)
    saveLots([oldLot])

    await clean()
    assert.deepEqual(JSON.parse(storage.get('sp_lots')), [oldLot])
    assert.equal(storage.has('sp_last_db_reset_id'), false)
  }
})
