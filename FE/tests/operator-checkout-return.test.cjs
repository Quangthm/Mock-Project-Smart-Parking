const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');
const ts = require('../node_modules/typescript');
const source = fs.readFileSync(path.join(__dirname, '../src/lib/operatorCheckoutReturn.ts'), 'utf8');
const code = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText;
const context = { exports: {}, URL };
vm.runInNewContext(code, context);
const { operatorCheckoutReturnUrls } = context.exports;

test('PayOS returns to the current Operator page and site on local or deployed origins', () => {
  for (const href of ['http://localhost:5173/dashboard/operation?existing=1', 'https://parking.example/pos?existing=1', 'http://localhost:5173/?existing=1']) {
    const urls = operatorCheckoutReturnUrls('assigned-site-id', href);
    for (const value of [urls.returnUrl, urls.cancelUrl]) {
      const url = new URL(value);
      assert.equal(url.origin, new URL(href).origin);
      assert.equal(url.pathname, new URL(href).pathname);
      assert.equal(url.searchParams.get('existing'), '1');
      assert.equal(url.searchParams.get('checkoutReturn'), 'payos');
      assert.equal(url.searchParams.get('checkoutSiteId'), 'assigned-site-id');
      assert.equal(url.searchParams.has('status'), false);
    }
  }
});

test('restoring Operator from root keeps checkout site and PayOS query parameters', () => {
  const search = '?checkoutReturn=payos&checkoutSiteId=assigned-site&status=PAID&orderCode=123';
  const effects = [], navigations = [];
  const source = fs.readFileSync(path.join(__dirname, '../src/App.tsx'), 'utf8') + '\nexport { AppContent };';
  const roleSource = fs.readFileSync(path.join(__dirname, '../src/roles/operator/data/roleRoutes.ts'), 'utf8');
  const roles = { exports: {} };
  vm.runInNewContext(ts.transpileModule(roleSource, { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText, roles);
  const jsx = () => null;
  const sandbox = {
    exports: {}, window: { location: { href: `http://localhost:5173/${search}` }, history: { state: {}, replaceState() {} } },
    require(name) {
      if (name === 'react') return { useEffect: effect => effects.push(effect), useLayoutEffect() {} };
      if (name === 'react/jsx-runtime') return { jsx, jsxs: jsx };
      if (name === 'motion/react') return { useReducedMotion: () => false, motion: { div: 'div' } };
      if (name === 'react-router-dom') return {
        useLocation: () => ({ pathname: '/', search, hash: '#checkout' }),
        useNavigate: () => (...args) => navigations.push(args),
      };
      if (name === './context/AppContext') return { useApp: () => ({
        view: 'operator', user: { role: 'operator', operatorRole: 'operation' }, authReady: true, authError: '',
      }) };
      if (name === './roles/operator/data/roleRoutes') return roles.exports;
      return {};
    },
  };
  vm.runInNewContext(ts.transpileModule(source, { compilerOptions: {
    module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.ReactJSX,
  } }).outputText, sandbox);
  sandbox.exports.AppContent();
  effects.forEach(effect => effect());
  assert.equal(navigations.length, 1);
  assert.equal(navigations[0][0].pathname, '/dashboard/operation');
  assert.equal(navigations[0][0].search, search);
  assert.equal(navigations[0][0].hash, '#checkout');
  assert.equal(navigations[0][1].replace, true);
});
