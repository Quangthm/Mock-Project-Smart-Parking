const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');
const ts = require('../../FE/node_modules/typescript');

async function fixture(enabled) {
  const source = fs.readFileSync(path.join(__dirname, '../../FE/src/hooks/useTurnstile.ts'), 'utf8')
    .replace('import.meta.env.VITE_TURNSTILE_ENABLED', enabled === undefined ? 'undefined' : JSON.stringify(enabled))
    .replace('import.meta.env.VITE_TURNSTILE_SITE_KEY', 'undefined');
  const js = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
  let renders = 0;
  const tokens = [];
  const context = { exports: {}, console,
    window: { turnstile: { render() { renders++; return 'widget'; }, remove() {} } },
    require(name) {
      assert.equal(name, 'react');
      return { useState: value => [value, () => {}], useRef: value => ({ current: value }), useCallback: fn => fn,
        useEffect: fn => { fn(); } };
    },
  };
  vm.runInNewContext(js, context);
  context.exports.useTurnstile({ current: {} }, { action: 'password_login', onTokenChange: value => tokens.push(value) });
  await new Promise(resolve => setImmediate(resolve));
  return { enabled: context.exports.turnstileEnabled, renders, tokens };
}

test('explicitly disabled Turnstile never renders a widget or produces a fake token', async () => {
  const f = await fixture('false');
  assert.equal(f.enabled, false);
  assert.equal(f.renders, 0);
  assert.deepEqual(f.tokens, ['']);
});

test('Turnstile remains enabled by default and when explicitly enabled', async () => {
  for (const setting of [undefined, 'true']) {
    const f = await fixture(setting);
    assert.equal(f.enabled, true);
    assert.equal(f.renders, 1);
  }
});
