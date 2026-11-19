/* Offline browser acceptance. Build FE with VITE_{ASSISTANT_API,API}_BASE_URL=https://s03.test.
   node tests/frontend/assistant-settings.browser.cjs <dist> <evidence-dir>
   Uses a separately available Playwright installation and CHROME_PATH. */
const assert = require('node:assert/strict');
const path = require('node:path');
const fs = require('node:fs/promises');
const { randomUUID } = require('node:crypto');
const { chromium } = require('playwright');

async function run() {
  const dist = path.resolve(process.argv[2]), evidence = path.resolve(process.argv[3]);
  await fs.mkdir(evidence, { recursive: true });
  const browser = await chromium.launch({ executablePath: process.env.CHROME_PATH, headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 }, serviceWorkers: 'block' });
    page.setDefaultTimeout(8000);
    const settings = { contractVersion: 2, revision: 1, enabled: true, maintenanceMessage: 'Service paused',
      providers: ['gemini', 'openai'].map((id, i) => ({ id, enabled: true, model: 'sample', priority: i + 1 })),
      configurations: ['Primary', 'Reserve'].map((label, i) => ({ id: randomUUID(), label, provider: 'gemini', enabled: true,
        model: 'sample', priority: i + 1, credentialVersion: randomUUID(), createdAt: new Date().toISOString(), revokedAt: null })) };
    const view = { settings, secretsConfigured: true, runtimeReleaseApproved: false };
    const testRequests = [], mutations = [];
    let conflict = false, discovery = 'first', testOutcome = 'passed';
    let releaseTest, releaseMutation, releaseRead, holdTest = false, holdMutation = false, holdRead = false;
    await page.addInitScript(() => {
      sessionStorage.setItem('sp_access_token', 'synthetic-admin');
      sessionStorage.setItem('sp_access_expires_at', '9999999999999');
      sessionStorage.setItem('sp_refresh_token', 'synthetic-refresh');
    });
    await page.route('**/*', async route => {
      const url = new URL(route.request().url()), pathname = url.pathname;
      const success = (data, status = 200) => route.fulfill({ status, json: { success: true, data } });
      const fail = (code, status) => route.fulfill({ status, json: { success: false, code } });
      if (url.origin !== 'https://s03.test') return route.abort();
      if (pathname === '/api/auth/me') return success({ userId: 'qa-admin', role: 'admin', email: 'qa@example.test', fullName: 'QA Admin' });
      if (pathname === '/api/assistant/admin/settings') {
        const snapshot = structuredClone(view);
        if (holdRead) { holdRead = false; await new Promise(resolve => { releaseRead = resolve; }); }
        return success(snapshot);
      }
      if (pathname.startsWith('/api/assistant/admin/')) {
        const body = route.request().postDataJSON();
        assert.equal(route.request().headers().authorization, 'Bearer synthetic-admin');
        if (pathname.endsWith('/discover')) {
          if (discovery === 'expired') return fail('CURSOR_CONFLICT', 409);
          if (discovery === 'error') return fail('PROVIDER_REJECTED', 502);
          const model = body.cursor ? 'sample-two' : 'sample';
          return success({ asOf: new Date().toISOString(), items: discovery === 'empty' ? [] :
            [{ id: model, displayName: model, supportsChat: true }], nextCursor: body.cursor || discovery === 'empty' ? null : 'opaque' });
        }
        if (pathname.endsWith('/test')) {
          testRequests.push(body);
          if (holdTest) {
            holdTest = false;
            await new Promise(resolve => { releaseTest = resolve; });
            return fail('REVISION_CONFLICT', 409);
          }
          const base = { testRequestId: body.testRequestId, configurationId: pathname.split('/').at(-2) };
          return testRequests.length === 1 || testOutcome === 'running' ? success({ ...base, outcome: 'running' }, 202) :
            success({ ...base, outcome: testOutcome, testedRevision: body.expectedRevision, latencyMs: 27, current: false,
              checkedAt: '2026-11-05T05:00:00Z', code: null, model: 'sample-two' });
        }
        assert.equal(pathname, '/api/assistant/admin/mutations');
        assert.equal(body.expectedRevision, settings.revision);
        assert.match(body.mutationId, /^[\da-f-]{36}$/i);
        assert(!mutations.includes(body.mutationId)); mutations.push(body.mutationId);
        if (holdMutation) {
          holdMutation = false;
          await new Promise(resolve => { releaseMutation = resolve; });
        }
        settings.revision++;
        if (conflict) { conflict = false; return fail('REVISION_CONFLICT', 409); }
        const row = settings.configurations.find(r => r.id === body.keyId);
        switch (body.action) {
          case 'reorder-providers': settings.providers.forEach(p => p.priority = body.providerIds.indexOf(p.id) + 1); break;
          case 'reorder-configurations': settings.configurations.forEach(r => r.priority = body.configurationIds.indexOf(r.id) + 1); break;
          case 'set-enabled': settings.enabled = body.enabled; break;
          case 'save-config': settings.providers = body.providers; break;
          case 'add-configuration':
            assert.equal(body.enabled, false); assert.equal(body.secret, 'synthetic-only');
            settings.configurations.push({ id: randomUUID(), credentialVersion: randomUUID(), createdAt: new Date().toISOString(),
              revokedAt: null, enabled: false, model: body.model, label: body.label, provider: body.provider, priority: body.priority }); break;
          case 'set-configuration': Object.assign(row, { label: body.label, model: body.model, enabled: body.enabled }); break;
          case 'rotate-key': assert.equal(body.secret, 'replacement-only'); row.credentialVersion = randomUUID(); break;
          case 'revoke-key': row.revokedAt = new Date().toISOString(); row.enabled = false; break;
          default: throw new Error('Unexpected mutation ' + body.action);
        }
        return success(view);
      }
      if (pathname.startsWith('/api/')) return fail('OFFLINE', 503);
      const file = path.join(dist, pathname === '/' ? 'index.html' : pathname);
      if (!file.startsWith(dist + path.sep)) return route.abort();
      try { return await route.fulfill({ path: file }); } catch { return route.abort(); }
    });
    await page.goto('https://s03.test/');
    const button = (label, area = page) => area.getByRole('button', { name: label, exact: true });
    await button('Open profile menu').click(); await button('Settings').click(); await button('AI Chatbot').click();
    const group = label => page.getByRole('region', { name: label, exact: true });
    const row = label => page.getByRole('article', { name: label, exact: true });
    const reload = async () => { await button('Reload settings').click(); await button('Reload settings').waitFor();
      await page.waitForFunction(() => !document.querySelector('.assistant-settings-fields').disabled); };
    await group('Gemini').waitFor();
    for (const [width, height] of [[1440,900], [768,1024], [390,844]]) {
      await page.setViewportSize({ width, height });
      assert(await page.locator('.settings-dialog-content').evaluate(el => el.scrollWidth <= el.clientWidth + 1));
      await page.screenshot({ path: path.join(evidence, 'settings-' + width + '.png') });
    }
    await button('Move OpenAI up').focus(); await page.keyboard.press('Enter'); await reload();
    assert.equal(await page.locator('.assistant-settings-provider h4').first().innerText(), 'OpenAI');
    await page.setViewportSize({ width: 1440, height: 900 });
    await row('Reserve').locator('[draggable]').dragTo(row('Primary'));
    await reload(); assert.equal(await group('Gemini').getByRole('article').first().getAttribute('aria-label'), 'Reserve');
    conflict = true; const before = mutations.length;
    await page.getByRole('switch', { name: 'System chatbot' }).click();
    await page.getByText(/Review the reloaded values/).waitFor(); assert.equal(mutations.length, before + 1);
    await button('Add configuration', group('OpenAI')).click();
    const editor = page.locator('dialog[open]');
    await editor.getByLabel('New API key').fill('discard-on-close');
    await button('Cancel', editor).focus();
    await page.keyboard.press('Tab');
    assert(await editor.evaluate(el => el.contains(document.activeElement)));
    await page.evaluate(() => document.documentElement.classList.add('dark'));
    await page.waitForTimeout(400);
    const colors = await editor.locator('input').first().evaluate(el => ({
      foreground: getComputedStyle(el).color, background: getComputedStyle(el).backgroundColor
    }));
    assert.equal(colors.foreground, 'rgb(241, 246, 243)');
    assert.equal(colors.background, 'rgb(21, 28, 27)');
    await page.screenshot({ path: path.join(evidence, 'editor-dark.png') });
    await page.evaluate(() => document.documentElement.classList.remove('dark'));
    await page.keyboard.press('Escape');
    await editor.waitFor({ state: 'hidden' }); await group('OpenAI').waitFor();
    await button('Add configuration', group('OpenAI')).click();
    assert.equal(await editor.getByLabel('New API key').inputValue(), '');
    await editor.getByLabel('Configuration name').fill('Trial');
    await editor.getByLabel('New API key').fill('synthetic-only');
    discovery = 'empty'; await button('Load models', editor).click();
    await editor.getByText('No compatible models found.').waitFor();
    discovery = 'error'; await button('Refresh models', editor).click(); await editor.getByText(/Could not load/).waitFor();
    discovery = 'first'; await button('Refresh models', editor).click();
    discovery = 'expired'; await button('Load more models', editor).click(); await editor.getByText(/Model list expired/).waitFor();
    discovery = 'first'; await button('Refresh models', editor).click(); await button('Load more models', editor).click();
    await editor.getByLabel('Model', { exact: true }).selectOption('sample-two');
    await button('Save disabled configuration', editor).click(); await row('Trial').waitFor();
    assert.equal(testRequests.length, 0);
    page.once('dialog', d => { assert.match(d.message(), /may charge/); return d.dismiss(); });
    await button('Test connection', row('Trial')).click(); assert.equal(testRequests.length, 0);
    page.on('dialog', d => d.accept());
    await button('Test connection', row('Trial')).click(); await row('Trial').getByText(/Test in progress/).waitFor();
    await row('Trial').getByText('passed', { exact: true }).waitFor(); assert.deepEqual(testRequests[0], testRequests[1]);
    assert.match(await row('Trial').innerText(), /27 ms.*Previous configuration/s);
    for (const outcome of ['failed', 'unknown']) {
      testOutcome = outcome; await button('Test connection', row('Trial')).click(); await row('Trial').getByText(outcome, { exact: true }).waitFor();
    }
    assert.equal(settings.configurations.at(-1).enabled, false);
    // A late diagnostic conflict must not release a mutation's lock or discard its response.
    holdTest = true;
    await button('Test connection', row('Trial')).click();
    await page.waitForTimeout(50);
    holdMutation = true;
    await button('Enable', row('Trial')).click();
    await page.waitForTimeout(50);
    assert.equal(typeof releaseTest, 'function');
    assert.equal(typeof releaseMutation, 'function');
    releaseTest();
    await page.waitForTimeout(100);
    assert(await page.locator('.assistant-settings-fields').evaluate(el => el.disabled));
    releaseMutation();
    await page.getByText(/Review the reloaded values/).waitFor();
    await button('Disable', row('Trial')).click();
    await button('Enable', row('Trial')).waitFor();
    // An old GET cannot suppress the diagnostic conflict that arrived during it.
    holdTest = true;
    await button('Test connection', row('Trial')).click();
    await page.waitForTimeout(50);
    holdRead = true;
    await button('Reload settings').click();
    await page.waitForTimeout(50);
    settings.revision++;
    releaseTest();
    await page.waitForTimeout(100);
    releaseRead();
    await page.getByText(/Review the reloaded values/).waitFor();
    await page.getByText(new RegExp('Revision ' + settings.revision + ' ·')).waitFor();
    // Simulate a suspended tab: automatic polling cannot reuse an expired receipt.
    testOutcome = 'running';
    await button('Test connection', row('Trial')).click();
    await row('Trial').getByText(/Test in progress/).waitFor();
    const dispatched = testRequests.length;
    await page.evaluate(() => { window.originalDateNow = Date.now; Date.now = () => window.originalDateNow() + 61 * 60_000; });
    await row('Trial').getByText(/Result lookup expired/).waitFor();
    assert.equal(testRequests.length, dispatched);
    page.removeAllListeners('dialog');
    page.once('dialog', d => { assert.match(d.message(), /may charge/); return d.dismiss(); });
    await button('Test connection', row('Trial')).click();
    assert.equal(testRequests.length, dispatched);
    await page.evaluate(() => { Date.now = window.originalDateNow; });
    page.on('dialog', d => d.accept());
    testOutcome = 'passed';
    await button('Enable', row('Trial')).click(); await button('Disable', row('Trial')).waitFor();
    await button('Edit', row('Trial')).click(); await editor.getByLabel('Configuration name').fill('Renamed');
    await button('Save changes', editor).click(); await row('Renamed').waitFor();
    await button('Replace key', row('Renamed')).click(); assert.equal(await editor.getByLabel('New API key').inputValue(), '');
    await editor.getByLabel('New API key').fill('replacement-only'); await button('Replace key', editor).click();
    await button('Revoke', row('Renamed')).click(); await row('Renamed').getByText(/Revoked/).waitFor();
    await button('Add configuration', group('OpenAI')).click();
    await editor.getByLabel('Configuration name').fill('Outage draft');
    await editor.getByLabel('New API key').fill('synthetic-only');
    discovery = 'error';
    await button('Load models', editor).click();
    await editor.getByLabel('Known model ID (saved disabled)').fill('sample');
    await button('Save disabled configuration', editor).click();
    await row('Outage draft').waitFor();
    assert.equal(settings.configurations.at(-1).enabled, false);
    console.log('PASS offline Settings entry, three viewport widths, keyboard/drag order and reload, conflict, editor lifecycle, discovery states, saved tests and credential actions.');
  } finally { await browser.close(); }
}
run().catch(error => { console.error(error); process.exitCode = 1; });
