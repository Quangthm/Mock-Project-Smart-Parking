const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const test = require('node:test')
const vm = require('node:vm')
const ts = require('../../FE/node_modules/typescript')

const compile = (file, jsx = false) => ts.transpileModule(
  fs.readFileSync(path.join(__dirname, '../../FE/src', file), 'utf8'),
  { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: jsx ? ts.JsxEmit.ReactJSX : undefined } },
).outputText

test('Owner agreement opens from shared browser storage, signs the current setup, and closes its tab', () => {
  const values = new Map()
  const localStorage = {
    getItem: key => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value),
  }
  const contractModule = { exports: {} }
  vm.runInNewContext(compile('lib/partnerContract.ts'), { module: contractModule, exports: contractModule.exports, localStorage, JSON, require: () => { throw new Error('Unexpected import') } })
  const contract = contractModule.exports
  const applicationId = 'owner-application-1'
  const handoff = { applicationId, ownerName: 'Minh Pham', ownerEmail: 'owner@example.com', businessName: 'Parking Co', lotName: 'Central Lot', setupFingerprint: 'setup-v1' }
  contract.saveOwnerContractHandoff(handoff)
  assert.equal(contract.loadOwnerContractHandoff('another-application'), null)

  let dialogProps, closeCount = 0
  const pageModule = { exports: {} }
  vm.runInNewContext(compile('roles/owner/parking-lots/OwnerContractPage.tsx', true), {
    module: pageModule, exports: pageModule.exports,
    window: { close: () => { closeCount++ }, closed: true, setTimeout: () => {} },
    require(name) {
      if (name === 'react') return { useState: initial => [typeof initial === 'function' ? initial() : initial, () => {}] }
      if (name === 'react-router-dom') return { useParams: () => ({ applicationId }) }
      if (name.endsWith('/PartnerContractDialog')) return { PartnerContractDialog: 'dialog' }
      if (name.endsWith('/partnerContract')) return contract
      if (name === 'react/jsx-runtime') return { Fragment: 'fragment', jsx: (type, props) => { if (type === 'dialog') dialogProps = props; return { type, props } }, jsxs: (type, props) => { if (type === 'dialog') dialogProps = props; return { type, props } } }
      throw new Error(`Unexpected import: ${name}`)
    },
  })
  pageModule.exports.OwnerContractPage()
  assert.equal(dialogProps.page, true)
  assert.equal(dialogProps.ownerName, 'Minh Pham')
  dialogProps.onSign('Minh Pham')
  assert.equal(closeCount, 1)
  assert.equal(contract.loadContractDraft(applicationId).setupFingerprint, 'setup-v1')
  assert.equal(contract.loadContractDraft(applicationId).ownerAcknowledgedAt.length > 0, true)
})
