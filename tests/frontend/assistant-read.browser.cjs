/* Offline Assistant read UI. Build with both VITE_ASSISTANT_API_BASE_URL and
   VITE_API_BASE_URL set to https://s03.test, then pass the dist directory. */
const assert = require('node:assert/strict');
const path = require('node:path');
const { chromium } = require('playwright');

const origin = 'https://s03.test';
const conversationId = '90000000-0000-4000-8000-000000000001';
const requestId = '90000000-0000-4000-8000-000000000002';
const conversations = '/api/assistant/conversations';
const messages = `${conversations}/${conversationId}/messages`;
const result = `${conversations}/${conversationId}/requests/${requestId}`;
const question = 'Lịch đặt của tôi';
const now = '2026-11-08T02:00:00Z';
const conversation = { id: conversationId, title: 'Hội thoại kiểm thử A', locale: 'vi', revision: 2,
  createdAt: now, lastMessageAt: now, expiresAt: '2026-12-08T02:00:00Z' };
const answer = { id: requestId, sequence: 2, role: 'assistant', content: 'Phản hồi riêng A',
  createdAt: now, clientMessageId: requestId, outcome: 'answered', sources: [], actions: [],
  mode: 'system', cards: [{ type: 'parking', asOf: now, parking: { name: 'Bãi fixture', availability: {} } }] };

async function scenario(browser, viewport, dist) {
  const page = await browser.newPage({ viewport, serviceWorkers: 'block' });
  page.setDefaultTimeout(8000);
  let enabled = false, recovered = false, hold = false;
  let releaseHistory;
  const historyReleased = new Promise(resolve => { releaseHistory = resolve; });
  const account = id => ({ userId: id, role: 'driver', email: `${id}@example.test`,
    fullName: `Fixture ${id}`, onboardingComplete: true });
  const requests = [];
  await page.route('**/*', async route => {
    const request = route.request();
    const url = new URL(request.url());
    const routePath = url.pathname;
    const ok = data => route.fulfill({ json: { success: true, data } });
    if (url.origin !== origin) return route.abort();
    if (routePath.startsWith('/api/assistant/')) requests.push([request.method(), routePath]);
    const isA = request.headers().authorization === 'Bearer token-a';
    if (routePath === '/api/auth/login') {
      const id = request.postDataJSON().email.startsWith('a@') ? 'a' : 'b';
      return ok({ accessToken: `token-${id}`, refreshToken: `refresh-${id}`, expiresIn: 3600, user: account(id) });
    }
    if (routePath === '/api/auth/me') return ok(account(isA ? 'a' : 'b'));
    if (routePath === '/api/auth/logout') return ok({});
    if (routePath === '/api/assistant/status') return ok({ enabled, guestFaq: enabled });
    if (routePath === '/api/assistant/capabilities') return ok({ enabled });
    if (routePath === '/api/assistant/guest/messages') {
      assert.equal(request.postDataJSON().message, question);
      return ok({ content: 'Sign in to view your reservations.', mode: 'system',
        outcome: 'handoff', actions: [{ type: 'login' }] });
    }
    if (routePath === conversations) return ok({ items: isA ? [conversation] : [], nextCursor: null });
    if (routePath === messages && isA) {
      if (hold) await historyReleased;
      return ok({ conversation, items: recovered ? [answer] : [], nextCursor: null,
        activeRequestClientMessageId: recovered ? null : requestId }).catch(error => {
        if (!hold) throw error;
      });
    }
    if (routePath === result && isA) {
      recovered = true;
      return ok({ requestId, conversationId, clientMessageId: requestId, state: 'completed', answer, revision: 2 });
    }
    if (routePath.startsWith('/api/')) return route.fulfill({ status: 404, json: { success: false } });
    return route.fulfill({ path: path.join(dist, path.extname(routePath) ? routePath : 'index.html') }).catch(() => route.abort());
  });
  const button = (name, scope = page) => scope.getByRole('button', { name, exact: true });
  const guest = button('Open SmartParking AI');
  const driver = button('Trợ lý · Lịch sử');
  const panel = page.getByRole('region', { name: 'Trợ lý SmartParking' });
  const login = async id => {
    await page.getByPlaceholder('you@example.com or phone number').fill(`${id}@example.test`);
    await page.getByPlaceholder('Enter your password').fill('fixture-password');
    await page.locator('form button[type=submit]').first().click();
  };
  const focus = () => page.evaluate(() => window.dispatchEvent(new Event('focus')));
  const afterResponse = async (routePath, action) => {
    const response = page.waitForResponse(res => new URL(res.url()).pathname === routePath);
    await action();
    await (await response).finished();
  };
  try {
    await afterResponse('/api/assistant/status', () => page.goto(origin));
    assert.equal(await guest.count(), 0);
    enabled = true;
    await focus();
    await guest.click();
    await page.locator('#smartparking-ai-input').fill(question);
    await button('Send message').click();
    await button('Sign in', page.getByRole('region', { name: 'SmartParking AI conversation' })).click();
    await login('a');
    await driver.click();
    await button('Kiểm tra kết quả', panel).click();
    await panel.getByText('Chỗ trống: Chưa xác định', { exact: true }).waitFor();
    await button('Đưa vào ô soạn', panel).click();
    assert.equal(await panel.getByRole('textbox').inputValue(), question);
    assert.equal(requests.filter(([method]) => method === 'POST').length, 1);
    await button('Đóng', panel).click();
    await driver.click();
    await panel.getByText(answer.content, { exact: true }).waitFor();
    await button('Đóng', panel).click();
    enabled = false;
    await afterResponse('/api/assistant/capabilities', focus);
    await driver.waitFor({ state: 'hidden' });
    enabled = true;
    await focus();
    hold = true;
    const historyHeld = page.waitForRequest(req =>
      new URL(req.url()).pathname === messages && req.method() === 'GET' &&
      req.headers().authorization === 'Bearer token-a');
    await driver.click();
    await historyHeld;
    const aborted = page.waitForEvent('requestfailed', req => new URL(req.url()).pathname === messages);
    await page.locator('.profile-trigger').filter({ hasText: 'Fixture a' }).click();
    await button('Sign Out').click();
    await aborted;
    releaseHistory();
    if (!(await page.getByPlaceholder('you@example.com or phone number').isVisible())) {
      await button('Sign In').first().click();
    }
    await login('b');
    await afterResponse(conversations, () => driver.click());
    await panel.getByRole('combobox').waitFor();
    await panel.locator('select:not([disabled])').waitFor();
    assert.equal(await panel.getByText(answer.content, { exact: true }).count(), 0);
    assert.equal(await panel.getByRole('option', { name: conversation.title }).count(), 0);
    console.log(`${viewport.width}x${viewport.height}: FAQ gate, login handoff, recovery, history, availability, Driver gate, logout/relogin PASS`);
  } finally {
    releaseHistory();
    await page.close();
  }
}

(async () => {
  const dist = path.resolve(process.argv[2]);
  const browser = await chromium.launch({ executablePath: process.env.CHROME_PATH, headless: true });
  try {
    for (const viewport of [{ width: 1440, height: 900 }, { width: 390, height: 844 }]) {
      await scenario(browser, viewport, dist);
    }
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
