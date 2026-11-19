const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');
const ts = require('../../FE/node_modules/typescript');

const deferred = () => {
  let resolve, reject;
  const promise = new Promise((yes, no) => { resolve = yes; reject = no; });
  return { promise, resolve, reject };
};
const settle = () => new Promise(resolve => setImmediate(resolve));

test('live guest widget renders a sign-in handoff even with Demo disabled', async () => {
  const f = fixture({ enabled: false });
  const signIn = [];
  const props = { role: 'driver', guest: {
    send: async () => ({ reply: 'Sign in to view reservations.', mode: 'system', sources: [], loginRequired: true }),
    onSignIn: question => signIn.push(question), locale: "en", onLocaleChange: () => {},
  } };
  f.render(props);
  f.button('Open SmartParking AI').props.onClick();
  f.submit('Show my reservations');
  await settle();
  f.button('Sign in').props.onClick();
  assert.deepEqual(signIn, ['Show my reservations']);
});
const reply = (text = 'Parking guidance') => ({
  isDemo: true, mode: 'provider', reply: text, notice: 'Public FAQ only', provider: 'gemini', model: 'configured-model',
  sources: [{ articleId: 'parking-guide', version: '1', title: '<b>Guide</b>', excerpt: 'Public guidance' }],
});

test('guest FAQ displays the answer without a references section', async () => {
  const f = fixture({ enabled: false });
  f.render({ role: 'admin', guest: { send: async () => ({
    reply: 'SmartPark giúp bạn tìm bãi đỗ.', mode: 'knowledge', loginRequired: false,
    sources: [{ articleId: 'overview', version: '1', title: 'SmartPark', excerpt: 'SmartPark giúp bạn tìm bãi đỗ.' }],
  }) } });
  f.button('Open SmartParking AI').props.onClick();
  f.button('SmartPark là gì?').props.onClick();
  await settle();
  assert.doesNotMatch(f.text(), /Tham khảo|Sources|Read guidance|SmartPark document guidance|Version 1/);
  assert.equal(f.nodes().some(node => node.type === 'details'), false);
  assert.equal(f.text().match(/SmartPark giúp bạn tìm bãi đỗ\./g).length, 1);
});

function compile(file, env) {
  const source = fs.readFileSync(path.join(__dirname, '../../FE/src', file), 'utf8')
    .replace(/import\.meta\.env/g, JSON.stringify(env));
  return ts.transpileModule(source, { compilerOptions: {
    module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX,
  } }).outputText;
}

// Execute the component and real HTTP adapter; hooks model state/effect lifetimes,
// not browser layout. No auth/session mocks: accidental access would fail.
function fixture({ fetch, send, enabled = true, dev = true, base = 'http://localhost:5095' } = {}) {
  const env = { DEV: dev, VITE_ASSISTANT_DEMO_ENABLED: String(enabled), VITE_ASSISTANT_DEMO_API_BASE_URL: base };
  const api = { exports: {}, fetch, URL, AbortController, Error, TypeError, SyntaxError, setTimeout, clearTimeout };
  vm.runInNewContext(compile('lib/assistantDemoApi.ts', env), api);
  const slots = [], effects = [], listeners = new Map(), events = [];
  let cursor = 0, props = { role: 'admin' }, writes = 0;
  const react = {
    useState: initial => {
      const index = cursor++;
      if (!(index in slots)) slots[index] = typeof initial === 'function' ? initial() : initial;
      return [slots[index], value => { writes++; slots[index] = typeof value === 'function' ? value(slots[index]) : value; }];
    },
    useRef: initial => { const index = cursor++; return slots[index] ??= { current: initial }; },
    useEffect: (effect, deps) => {
      const index = cursor++, previous = slots[index];
      if (!previous || deps.some((value, i) => !Object.is(value, previous.deps[i]))) {
        effects.push(() => { previous?.cleanup?.(); slots[index] = { deps, cleanup: effect() }; });
      }
    },
  };
  const jsx = (type, props) => ({ type, props });
  const context = {
    exports: {}, AbortController, Error, requestAnimationFrame: callback => callback(),
    CustomEvent: class { constructor(type, init) { this.type = type; this.detail = init.detail; } },
    document: { querySelector: () => null },
    window: { innerWidth: 1280, innerHeight: 900, setTimeout,
      matchMedia: () => ({ matches: false }),
      addEventListener: (name, handler) => listeners.set(name, handler), removeEventListener: name => listeners.delete(name),
      dispatchEvent: event => { events.push({ type: event.type, detail: event.detail }); },
    },
    require: name => {
      if (name === 'react') return react;
      if (name === 'react/jsx-runtime') return { jsx, jsxs: jsx };
      if (name === '../../lib/assistantDemoApi') return { sendDemoMessage: send ?? api.exports.sendDemoMessage };
      if (name === '../icon/UntitledIcon') return { UntitledIcon: 'icon' };
      throw new Error(`Unexpected import: ${name}`);
    },
  };
  vm.runInNewContext(compile('components/chat/FloatingChatbot.tsx', env), context);
  function render(next = props) {
    props = next; cursor = 0;
    const tree = context.exports.FloatingChatbot(props);
    effects.splice(0).forEach(effect => effect());
    return tree;
  }
  function nodes(node, result = []) {
    if (Array.isArray(node)) node.forEach(child => nodes(child, result));
    else if (node && typeof node === 'object') { result.push(node); nodes(node.props?.children, result); }
    return result;
  }
  function text(node) {
    if (Array.isArray(node)) return node.map(text).join(' ');
    if (node && typeof node === 'object') return text(node.props?.children);
    return typeof node === 'string' || typeof node === 'number' ? String(node) : '';
  }
  const button = label => nodes(render()).find(node => node.type === 'button' &&
    (node.props['aria-label'] === label || node.props.children === label));
  function submit(question) {
    nodes(render()).find(node => node.props.id === 'smartparking-ai-input').props.onChange({ target: { value: question } });
    nodes(render()).find(node => node.type === 'form').props.onSubmit({ preventDefault() {} });
  }
  render();
  return { render, nodes: () => nodes(render()), text: () => text(render()), button, submit, events, get writes() { return writes; },
    unmount: () => slots.forEach(slot => slot?.cleanup?.()),
  };
}

test('widget sends one public FAQ through the real adapter without credentials, role or history', async () => {
  const response = deferred(); let calls = 0;
  const f = fixture({ fetch: (url, options) => {
    calls++;
    assert.equal(url, 'http://localhost:5095/api/demo/assistant/messages');
    assert.equal(options.credentials, 'omit');
    assert.equal(options.headers.Authorization, undefined);
    assert.equal(options.body, JSON.stringify({ message: 'How do I find parking?' }));
    return response.promise;
  } });
  f.submit('  How do I find parking?  ');
  f.submit('Duplicate click');
  assert.equal(calls, 1);
  assert.equal(f.button('Send message').props.disabled, true);
  response.resolve({ ok: true, json: async () => ({ success: true, data: reply('<script>plain text</script>') }) });
  await settle();
  assert.match(f.text(), /<script>plain text<\/script>/);
  assert.match(f.text(), /Sources/);
  assert.match(f.text(), /<b>Guide<\/b>/);
  assert.match(f.text(), /AI reply/);
  assert.ok(f.nodes().every(node => !node.props?.dangerouslySetInnerHTML));
});

for (const status of [401, 429, 503]) {
  test(`HTTP ${status} is an explicit error; only a manual retry resends the failed turn`, async () => {
    let calls = 0;
    const f = fixture({ fetch: async () => {
      calls++;
      return calls === 1
        ? { ok: false, status, json: async () => ({ code: status === 503 ? 'ASSISTANT_MAINTENANCE' : 'REJECTED', message: 'Temporarily paused' }) }
        : { ok: true, json: async () => ({ success: true, data: reply() }) };
    } });
    f.submit('Where can I park?'); await settle();
    assert.equal(calls, 1);
    assert.equal(f.nodes().filter(node => node.props.role === 'alert').length, 1);
    assert.doesNotMatch(f.text(), /Parking guidance|Simulated reply/);
    f.button('Try again').props.onClick(); await settle();
    assert.equal(calls, 2);
    assert.equal(f.text().split('Where can I park?').length - 1, 1);
    assert.match(f.text(), /Parking guidance/);
    assert.equal(f.nodes().filter(node => node.props.role === 'alert').length, 0);
  });
}

test('New aborts the old request; its late success cannot alter the new pending conversation', async () => {
  const requests = [];
  const f = fixture({ send: (question, signal) => {
    const request = { question, signal, ...deferred() }; requests.push(request); return request.promise;
  } });
  f.submit('Old question');
  f.button('New conversation').props.onClick();
  assert.equal(requests[0].signal.aborted, true);
  f.submit('New question');
  requests[0].resolve(reply('Stale reply')); await settle();
  assert.doesNotMatch(f.text(), /Old question|Stale reply/);
  assert.equal(f.button('Send message').props.disabled, true);
  assert.equal(f.nodes().filter(node => node.props.role === 'status').length, 1);
  requests[1].resolve(reply('Current reply')); await settle();
  assert.match(f.text(), /New question/);
  assert.match(f.text(), /Current reply/);
});

test('role switch clears the conversation and unmount aborts without late state writes', async () => {
  const requests = [];
  const f = fixture({ send: (_question, signal) => {
    const request = { signal, ...deferred() }; requests.push(request); return request.promise;
  } });
  f.submit('Admin question');
  f.render({ role: 'driver' });
  assert.equal(requests[0].signal.aborted, true);
  requests[0].reject(new Error('Old failure')); await settle();
  assert.doesNotMatch(f.text(), /Admin question|Old failure/);
  f.submit('Driver question'); f.unmount();
  const writes = f.writes;
  assert.equal(requests[1].signal.aborted, true);
  requests[1].resolve(reply('Late reply')); await settle();
  assert.equal(f.writes, writes);
});

test('production and disabled preview never send to the anonymous demo endpoint', () => {
  for (const config of [{ dev: false }, { enabled: false }]) {
    const f = fixture({ ...config, fetch: () => assert.fail('must not fetch') });
    assert.equal(f.render(), null);
  }
});

test('missing API configuration is visible and never falls back to mock', async () => {
  const f = fixture({ base: '', fetch: () => assert.fail('must not fetch') });
  f.submit('Question'); await settle();
  assert.match(f.text(), /Chưa cấu hình/);
  assert.equal(f.nodes().filter(node => node.props.role === 'alert').length, 1);
  assert.doesNotMatch(f.text(), /Simulated reply/);
});

test('Driver can open normal parking/booking navigation after AI failure without another API call', async () => {
  let calls = 0;
  const f = fixture({ fetch: async () => { calls++; return { ok: false, status: 503,
    json: async () => ({ code: 'MODEL_UNAVAILABLE', message: 'unavailable', traceId: 'trace-123' }) }; } });
  f.render({ role: 'driver' });
  f.button('Open SmartParking AI').props.onClick();
  f.submit('Book a parking space for tomorrow'); await settle();
  f.button('Find parking & book directly').props.onClick();
  assert.deepEqual(f.events, [{ type: 'sp:dashboard-nav', detail: 'find' }]);
  assert.equal(calls, 1);
  assert.equal(f.nodes().find(node => node.props['aria-label'] === 'SmartParking AI conversation').props['aria-hidden'], true);
  assert.doesNotMatch(f.text(), /Settings|API key|provider\/model/);
});

test('non-Driver errors do not offer Driver-only booking navigation', async () => {
  const f = fixture({ send: async () => { throw new Error('unavailable'); } });
  for (const role of ['admin', 'owner', 'operator']) {
    f.render({ role });
    f.submit('Question'); await settle();
    assert.equal(f.button('Find parking & book directly'), undefined);
    assert.deepEqual(f.events, []);
  }
});

test('continuing an existing booking closes chat without navigation that would discard its draft', async () => {
  let calls = 0;
  const f = fixture({ send: async () => { calls++; throw new Error('unavailable'); } });
  f.render({ role: 'driver', bookingInProgress: true });
  f.button('Open SmartParking AI').props.onClick();
  f.submit('Help with my selected slot'); await settle();
  assert.equal(f.button('Find parking & book directly'), undefined);
  f.button('Continue booking').props.onClick();
  assert.deepEqual(f.events, []);
  assert.equal(calls, 1);
  assert.equal(f.nodes().find(node => node.props['aria-label'] === 'SmartParking AI conversation').props['aria-hidden'], true);
});

test('document fallback opens real source text and keeps normal booking available without another AI call', async () => {
  let calls = 0;
  const f = fixture({ send: async () => { calls++; return { ...reply('Read the original guidance'),
    mode: 'knowledge', provider: null, model: null, notice: 'Not generated by AI' }; } });
  f.render({ role: 'driver', bookingInProgress: true });
  f.submit('Help me book'); await settle();
  assert.match(f.text(), /SmartPark document guidance/);
  assert.doesNotMatch(f.text(), /AI reply|configured-model|gemini|Simulated reply/);
  assert.equal(f.nodes().find(node => node.type === 'details').props.open, true);
  f.button('Continue booking').props.onClick();
  assert.equal(calls, 1);
  assert.deepEqual(f.events, []);
});
