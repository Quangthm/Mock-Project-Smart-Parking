const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const test = require('node:test')
const vm = require('node:vm')
const ts = require('../../FE/node_modules/typescript')

const source = fs.readFileSync(path.join(__dirname, '../../FE/src/lib/ownerApi.ts'), 'utf8')
const code = ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText

function fixture(response) {
  const calls = []
  const module = { exports: {} }
  vm.runInNewContext(code, { module, exports: module.exports, require(name) {
    if (name === './authApi') return { request: async (...args) => { calls.push(args); return response } }
    if (name === './ownerRegistration') return { ownerRegistrationStage: () => 'setup', validOwnerChallenge: () => true }
    throw new Error(`Unexpected import: ${name}`)
  } })
  return { api: module.exports.ownerApi, calls }
}

test('Admin contract loads one Owner application through the implemented detail endpoint', async () => {
  const id = '29da4918-d0a6-4f21-94f7-3854780d3dc9'
  const f = fixture({ success: true, data: { id, businessName: 'Test Parking' } })
  assert.equal((await f.api.get(id)).businessName, 'Test Parking')
  assert.equal(f.calls.length, 1)
  assert.deepEqual(Array.from(f.calls[0]), [`/${id}`, 'GET', undefined, '/api/owner-applications'])
})

test('Owner application detail rejects mismatched response IDs', async () => {
  const f = fixture({ success: true, data: { id: 'another-application' } })
  await assert.rejects(f.api.get('expected-application'), /Invalid application response/)
})
