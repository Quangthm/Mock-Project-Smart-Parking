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
  } };
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
function fixture(mode, fail = false) {
  const state = [], calls = [], identities = [], navigations = [];
  let cursor = 0;
  const react = { useState(initial) {
    const i = cursor++;
    if (!(i in state)) state[i] = typeof initial === 'function' ? initial() : initial;
    return [state[i], value => { state[i] = typeof value === 'function' ? value(state[i]) : value; }];
  } };
  const jsx = (type, props) => ({ type, props });
  const failure = 'Your account has not been approved yet. Please wait for administrator approval before signing in.';
  const api = {
    async requestOtp(email) { calls.push(['otp', email]); if (fail) throw new Error(failure); return { challengeId: 'challenge' }; },
    async login(email, password) { calls.push(['password', email, password]); if (fail) throw new Error(failure); return { id: 'owner', role: 'owner', name: 'Owner' }; },
  };
  const component = load('roles/authentications/Sign In/SignIn.tsx', {
    react, 'react/jsx-runtime': { jsx, jsxs: jsx },
    '../../../context/AppContext': { useApp: () => ({ setUser: user => identities.push(user), setView() {} }) },
    '../data/data': { authData: { saveUser() {}, addAuditLog() {}, notifyUser() {} } },
    'react-router-dom': { useNavigate: () => (...args) => navigations.push(args) },
    '../../operator/data/roleRoutes': { getUserHomePath: () => '/' },
    '../../../components/brand/BrandLogo': { BrandLogo: 'brand' },
    '../../../components/forms/PasswordVisibilityIcon': { PasswordVisibilityIcon: 'password-icon' },
    '../../../lib/authApi': { authApi: api }, '../../../lib/signInContact': contact,
  }).SignIn;
  const render = () => { cursor = 0; return component(); };
  const input = () => nodes(render()).find(node => node.props?.autoComplete === 'username');
  const submit = () => nodes(render()).find(node => node.type === 'form').props.onSubmit({ preventDefault() {} });
  nodes(render()).find(node => node.type === 'select').props.onChange({ target: { value: mode } });
  if (mode === 'password') nodes(render()).find(node => node.props?.placeholder === 'Enter your password').props.onChange({ target: { value: 'Password@123' } });
  return { render, input, submit, calls, identities, navigations, failure };
}
for (const mode of ['password', 'otp']) {
  test(`${mode}: malformed email shows validation error without sending a request, and valid retry works`, async () => {
    const f = fixture(mode);
    f.input().props.onChange({ target: { value: 'invalid@@mail' } });
    await f.submit();
    assert.equal(f.calls.length, 0);
    assert.equal(nodes(f.render()).find(node => node.props?.role === 'alert').props.children, contact.signInContactMessage);
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
