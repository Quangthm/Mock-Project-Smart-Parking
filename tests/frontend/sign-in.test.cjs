const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const test = require('node:test');
const ts = require('../../FE/node_modules/typescript');
function load(file, imports = {}) {
  const js = ts.transpileModule(fs.readFileSync(path.join(__dirname, '../../FE/src', file), 'utf8'), {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX },
  }).outputText;
  const context = { exports: {}, Error, require: name => {
    if (!(name in imports)) throw new Error(`Unexpected import: ${name}`);
    return imports[name];
  }, sessionStorage: { removeItem() {} } };
  vm.runInNewContext(js, context);
  return context.exports;
}
const contact = load('lib/signInContact.ts');
for (const value of ['', ' ', 'invalid', 'a@example', 'a@@example.com', 'a @example.com', 'a@example..com', '.a@example.com', 'a..b@example.com', 'a@-example.com', 'Name <a@example.com>', 'a\n@example.com', 'a'.repeat(256)]) {
  test(`invalid sign-in contact is rejected: ${JSON.stringify(value).slice(0, 45)}`, () => assert.equal(contact.isValidSignInContact(value), false));
}
for (const value of [' driver+test@example.com ', 'ADMIN@smartpark.local', '0912345678', '+84912345678']) {
  test(`valid sign-in contact is accepted: ${value}`, () => assert.equal(contact.isValidSignInContact(value), true));
}
function nodes(tree) {
  if (!tree || typeof tree !== 'object') return [];
  return [tree, ...[tree.props?.children].flat(Infinity).flatMap(nodes)];
}
function fixture(mode, fail = false, mfaRequired = false, recoveryView = null) {
  const state = [], calls = [], identities = [], navigations = [];
  let cursor = 0, failRequests = fail, currentView = "sign-in", routeActive = true;
  const effects = [], views = [];
  const react = { useEffect(effect) { effects.push(effect); }, useState(initial) {
    const i = cursor++;
    if (!(i in state)) state[i] = typeof initial === 'function' ? initial() : initial;
    return [state[i], value => { state[i] = typeof value === 'function' ? value(state[i]) : value; }];
  } };
  const jsx = (type, props) => ({ type, props });
  const failure = 'Your account has not been approved yet. Please wait for administrator approval before signing in.';
  const api = {
    async requestOtp(email) { calls.push(['otp', email]); if (failRequests) throw new Error(failure); return { challengeId: 'challenge' }; },
    async login(email, password) {
      calls.push(['password', email, password]);
      if (failRequests) throw new Error(failure);
      if (mfaRequired) return { mfaRequired: true, challenge: { challengeId: 'mfa-challenge', challengeToken: 'proof', expiresAt: 'future' } };
      return { id: 'owner', role: 'owner', name: 'Owner' };
    },
    async completeMfaLogin(challenge, totp, recoveryCode) {
      calls.push(['mfa', challenge, totp, recoveryCode]);
      return { id: 'owner', role: 'owner', name: 'Owner' };
    },
  };
  const component = load('roles/authentications/Sign In/SignIn.tsx', {
    react, 'react/jsx-runtime': { jsx, jsxs: jsx },
    '../../../context/AppContext': { useApp: () => ({ view: currentView, setUser: user => identities.push(user), setView(value) { currentView = value; views.push(value); } }) },
    '../data/data': { authData: { saveUser() {}, addAuditLog() {}, notifyUser() {} } },
    'react-router-dom': { useNavigate: () => (...args) => { navigations.push(args); if (args[0] === '/') routeActive = false; } },
    '../../operator/data/roleRoutes': { getUserHomePath: () => '/' },
    '../../../components/brand/BrandLogo': { BrandLogo: 'brand', BrandWordmark: 'wordmark' },
    '../../../components/forms/PasswordVisibilityIcon': { PasswordVisibilityIcon: 'password-icon' },
    '../../../components/icon/UntitledIcon': { UntitledIcon: 'icon' },
    '../../../components/ui/PageHeader': { PageHeader: 'page-header' },
    '../pendingRegistrationSignIn': { resumePendingSignIn: async () => recoveryView },
    '../Sign Up/DriverOtpForm': { pendingDriverKey: 'sp_pending_driver_registration' },
    '../../../components/forms/MfaCodeInput': { MfaCodeInput: 'mfa-input' },
    '../../../lib/authApi': { authApi: api, AuthApiError: class extends Error {} }, '../../../lib/signInContact': contact,
  }).SignIn;
  const render = () => { cursor = 0; return component({ flow: mode === "otp" ? "email-otp" : "password" }); };
  const input = () => nodes(render()).find(node => node.props?.autoComplete === 'username');
  const submit = () => nodes(render()).find(node => node.type === 'form').props.onSubmit({ preventDefault() {} });
  if (mode === 'password') nodes(render()).find(node => node.props?.placeholder === 'Enter your password').props.onChange({ target: { value: 'Password@123' } });
  return { render, input, submit, calls, identities, navigations, failure, views,
    setFailure(value) { failRequests = value; },
    flushEffects() { const pending = effects.splice(0); if (routeActive) pending.forEach(effect => effect()); },
    get view() { return currentView; }, get routeActive() { return routeActive; } };
}
for (const mode of ['password', 'otp']) {
  test(`${mode}: malformed email shows validation error without sending a request, and valid retry works`, async () => {
    const f = fixture(mode);
    f.input().props.onChange({ target: { value: 'invalid@@mail' } });
    await f.submit();
    assert.equal(f.calls.length, 0);
    assert.equal(nodes(f.render()).find(node => node.props?.role === 'alert').props.children, mode === 'password' ? 'Please enter a valid email address.' : contact.signInContactMessage);
    f.input().props.onChange({ target: { value: 'owner@example.com' } });
    await f.submit();
    assert.equal(f.calls.length, 1);
    assert.equal(nodes(f.render()).some(node => node.props?.role === 'alert'), false);
    assert.equal(f.identities.length, mode === 'password' ? 1 : 0);
  });
  test(`${mode}: pending approval message stays visible with no login or navigation`, async () => {
    const f = fixture(mode, true);
    f.input().props.onChange({ target: { value: 'owner@example.com' } });
    await f.submit();
    assert.equal(nodes(f.render()).find(node => node.props?.role === 'alert').props.children, f.failure);
    assert.equal(f.identities.length, 0);
    assert.equal(f.navigations.length, 0);
    assert.equal(nodes(f.render()).find(node => node.type === 'button' && node.props?.type === 'submit').props.disabled, false);
  });
}

test('MFA challenge holds navigation until a recovery code is verified and clears the password', async () => {
  const f = fixture('password', false, true);
  f.input().props.onChange({ target: { value: 'owner@example.com' } });
  await f.submit();
  assert.equal(f.identities.length, 0);
  assert.equal(f.navigations.length, 0);
  assert.ok(!nodes(f.render()).some(node => node.props?.autoComplete === 'current-password'));
  let mfa = nodes(f.render()).find(node => node.type === 'mfa-input');
  mfa.props.onMethodChange(true);
  mfa = nodes(f.render()).find(node => node.type === 'mfa-input');
  mfa.props.onChange('saved-code');
  await f.submit();
  assert.equal(f.calls[1][0], 'mfa');
  assert.equal(f.calls[1][2], undefined);
  assert.equal(f.calls[1][3], 'saved-code');
  assert.equal(f.identities.length, 1);
  assert.equal(f.navigations.length, 1);
});


for (const nextView of ['sign-up', 'parking-lot-setup', 'pending-approval']) {
  for (const branch of ['submit', 'request-another-code']) {
    test(`email OTP ${branch} recovery exits the route and preserves ${nextView}`, async () => {
      const f = fixture('otp', branch === 'submit', false, nextView);
      f.input().props.onChange({ target: { value: 'pending@example.com' } });
      f.flushEffects();
      await f.submit();
      if (branch === 'request-another-code') {
        f.setFailure(true);
        const retry = nodes(f.render()).find(node => node.type === 'button' && node.props.children === 'Request another code');
        await retry.props.onClick();
      }
      assert.equal(f.view, nextView);
      assert.equal(f.navigations.length, 1);
      assert.equal(f.navigations[0][0], '/');
      assert.equal(f.routeActive, false);
      // Leaving the explicit OTP route unmounts SignIn before its real view-sync effect can reset recovery.
      f.render(); f.flushEffects();
      assert.equal(f.view, nextView);
      assert.equal(f.identities.length, 0);
    });
  }
}
