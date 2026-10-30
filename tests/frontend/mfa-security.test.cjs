const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');
const ts = require('../../FE/node_modules/typescript');

function nodes(tree) {
  if (!tree || typeof tree !== 'object') return [];
  return [tree, ...[tree.props?.children].flat(Infinity).flatMap(nodes)];
}

async function fixture(enabled = false, rejected = false) {
  const states = [], effects = [], calls = [], storage = new Map(), events = [], dismissal = [];
  let cursor = 0, effectCursor = 0;
  const jsx = (type, props) => ({ type, props });
  const api = {
    async mfaStatus() { return { enabled, recoveryCodesRemaining: enabled ? 8 : 0 }; },
    async setupMfa(body) {
      calls.push(['setup', body]);
      if (rejected) throw new Error('Invalid verification code.');
      return { secret: 'private-secret', uri: 'otpauth://private-uri', expiresAt: 'future' };
    },
    async enableMfa(code) {
      calls.push(['enable', code]);
      return { recoveryCodes: Array.from({ length: 8 }, (_, index) => `private-code-${index}`), signInRequired: enabled };
    },
    async disableMfa(body) { calls.push(['disable', body]); },
    async regenerateRecoveryCodes(body) {
      calls.push(['regenerate', body]);
      return { recoveryCodes: Array.from({ length: 8 }, (_, index) => `new-private-code-${index}`), signInRequired: false };
    },
    clearSession() { storage.clear(); },
  };
  const source = fs.readFileSync(path.join(__dirname, '../../FE/src/components/forms/AccountSecurityPanel.tsx'), 'utf8');
  const javascript = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX },
  }).outputText;
  const imports = {
    react: {
      useState(initial) {
        const index = cursor++;
        if (!(index in states)) states[index] = typeof initial === 'function' ? initial() : initial;
        return [states[index], value => { states[index] = typeof value === 'function' ? value(states[index]) : value; }];
      },
      useEffect(effect, dependencies) {
        const index = effectCursor++;
        const previous = effects[index];
        if (!previous || dependencies.some((value, item) => value !== previous.dependencies[item]))
          effects[index] = { effect, dependencies, pending: true, cleanup: previous?.cleanup };
      },
    },
    'react/jsx-runtime': { jsx, jsxs: jsx },
    'qrcode.react': { QRCodeSVG: 'qr-code' },
    'react-router-dom': { useNavigate: () => path => events.push(path) },
    '../../lib/authApi': { authApi: api, authExpiredEvent: 'expired' },
    './MfaCodeInput': { MfaCodeInput: 'mfa-input' },
  };
  const context = {
    exports: {}, Error, Event,
    require: name => {
      if (!(name in imports)) throw new Error(`Unexpected import: ${name}`);
      return imports[name];
    },
    sessionStorage: { removeItem: key => storage.delete(key) },
    window: {
      dispatchEvent: event => events.push(event.type), location: { assign: path => events.push(path) },
      addEventListener() {}, removeEventListener() {},
    },
  };
  vm.runInNewContext(javascript, context);
  const onDismissBlockedChange = blocked => dismissal.push(blocked);
  const render = () => {
    cursor = 0; effectCursor = 0;
    const tree = context.exports.AccountSecurityPanel({ onDismissBlockedChange });
    for (const effect of effects) {
      if (effect.pending) {
        effect.cleanup?.();
        effect.cleanup = effect.effect();
        effect.pending = false;
      }
    }
    return tree;
  };
  render();
  await new Promise(resolve => setImmediate(resolve));
  const button = text => nodes(render()).find(node => node.type === 'button' && node.props.children === text);
  const password = value => nodes(render()).find(node => node.props?.autoComplete === 'current-password').props.onChange({ target: { value } });
  const submit = () => nodes(render()).find(node => node.type === 'form').props.onSubmit({ preventDefault() {} });
  return { render, button, password, submit, calls, storage, events, dismissal };
}

test('setup verifies password, renders a local QR and shows recovery codes only after activation', async () => {
  const f = await fixture();
  assert.equal(f.button('Turn off 2FA'), undefined);
  f.button('Set up 2FA').props.onClick();
  f.password('Password@123');
  await f.submit();
  assert.equal(f.calls[0][1].password, 'Password@123');
  assert.equal(nodes(f.render()).find(node => node.type === 'qr-code').props.value, 'otpauth://private-uri');
  assert.equal(f.button('Download codes'), undefined);
  nodes(f.render()).find(node => node.props?.autoComplete === 'one-time-code').props.onChange({ target: { value: '123456' } });
  await f.submit();
  assert.equal(f.calls[1][1], '123456');
  assert.equal(f.button('Done').props.disabled, true);
  assert.equal(f.dismissal.at(-1), true);
  assert.ok(nodes(f.render()).find(node => node.type === 'pre').props.children.includes('private-code-7'));
  assert.equal(f.storage.size, 0);
  nodes(f.render()).find(node => node.props?.type === 'checkbox').props.onChange({ target: { checked: true } });
  f.button('Done').props.onClick();
  assert.equal(nodes(f.render()).find(node => node.type === 'pre'), undefined);
  assert.equal(f.dismissal.at(-1), false);
});

test('failed replacement verification retains the form and never exposes a new secret', async () => {
  const f = await fixture(true, true);
  f.button('Replace authenticator').props.onClick();
  f.password('Password@123');
  nodes(f.render()).find(node => node.type === 'mfa-input').props.onChange('000000');
  await f.submit();
  assert.equal(f.calls[0][1].totp, '000000');
  assert.equal(nodes(f.render()).find(node => node.type === 'qr-code'), undefined);
  assert.equal(nodes(f.render()).find(node => node.props?.role === 'alert').props.children, 'Invalid verification code.');
  assert.equal(f.dismissal.at(-1), false);
});

test('disable submits password plus recovery code and returns to sign-in after success', async () => {
  const f = await fixture(true);
  f.button('Turn off 2FA').props.onClick();
  f.password('Password@123');
  nodes(f.render()).find(node => node.type === 'mfa-input').props.onMethodChange(true);
  nodes(f.render()).find(node => node.type === 'mfa-input').props.onChange('saved-code');
  await f.submit();
  assert.equal(f.calls[0][0], 'disable');
  assert.equal(f.calls[0][1].recoveryCode, 'saved-code');
  assert.equal(f.calls[0][1].totp, undefined);
  assert.deepEqual(f.events, ['expired', '/sign-in']);
});

test('replacement keeps its new recovery codes visible until acknowledged, then returns to sign-in', async () => {
  const f = await fixture(true);
  f.button('Replace authenticator').props.onClick();
  f.password('Password@123');
  nodes(f.render()).find(node => node.type === 'mfa-input').props.onChange('123456');
  await f.submit();
  nodes(f.render()).find(node => node.props?.autoComplete === 'one-time-code').props.onChange({ target: { value: '654321' } });
  await f.submit();
  assert.deepEqual(f.events, []);
  assert.equal(f.button('Continue to sign-in').props.disabled, true);
  nodes(f.render()).find(node => node.props?.type === 'checkbox').props.onChange({ target: { checked: true } });
  f.button('Continue to sign-in').props.onClick();
  assert.deepEqual(f.events, ['expired', '/sign-in']);
});
