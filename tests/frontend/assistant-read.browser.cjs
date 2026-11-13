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
const reservationId = '90000000-0000-4000-8000-000000000003';
const reservationWithoutAction = '90000000-0000-4000-8000-000000000004';
const conversation = { id: conversationId, title: 'Hội thoại kiểm thử A', locale: 'vi', revision: 2,
  createdAt: now, lastMessageAt: now, expiresAt: '2026-12-08T02:00:00Z' };
const answer = { id: requestId, sequence: 2, role: 'assistant', content: 'Phản hồi riêng A',
  createdAt: now, clientMessageId: requestId, outcome: 'answered', sources: [],
  actions: [{ type: 'view_reservation', reservationId }], mode: 'system', cards: [
    { type: 'parking', asOf: now, parking: { name: 'Bãi chưa rõ chỗ', availability: { availableCount: null } } },
    { type: 'parking', asOf: now, parking: { name: 'Bãi đã đủ xe', availability: { availableCount: 0 } } },
    { type: 'reservation', reservation: { id: reservationId, vehicle: { displayLabel: 'Xe kiểm thử A' },
      startAt: now, endAt: '2026-11-08T04:00:00Z', status: 'CONFIRMED', asOf: now } },
    { type: 'reservation', reservation: { id: reservationWithoutAction, vehicle: { displayLabel: 'Xe kiểm thử khác' },
      startAt: now, endAt: '2026-11-08T04:00:00Z', status: 'CONFIRMED', asOf: now } },
  ] };
const older = { ...answer, id: 'older-message', sequence: 1, role: 'user', content: 'Tin nhắn từ trang cũ' };
const latest = { ...answer, id: 'latest-message', sequence: 4, content: 'Phản hồi từ tab khác' };
const empty = { ...latest, id: 'empty', sequence: 6, cards: [], actions: [], outcome: 'empty',
  content: 'Không tìm thấy bãi nào được xác nhận phù hợp với bộ lọc đã gửi.' };
const failure = { ...empty, id: 'failure', sequence: 8, outcome: 'dependency_unavailable',
  content: 'API tìm bãi gặp lỗi khi trả kết quả. Bạn có thể thử lại.' };
const detail = { ...empty, id: 'detail', sequence: 10, outcome: 'answered',
  content: 'Chi tiết lịch đặt vừa được đọc lại.', cards: answer.cards.filter(card => card.reservation?.id === reservationId) };

async function scenario(browser, viewport, dist) {
  const page = await browser.newPage({ viewport, serviceWorkers: 'block' });
  page.setDefaultTimeout(8000);
  let enabled = false, recovered = false, hold = false, reset = false;
  let paginate = false, latestAvailable = false, detailAvailable = false;
  let sentRevision;
  let releaseHistory;
  const historyReleased = new Promise(resolve => { releaseHistory = resolve; });
  const account = id => ({ userId: id, role: 'driver', email: `${id}@example.test`,
    fullName: `Fixture ${id}`, onboardingComplete: true });
  const requests = [];
  const posts = [];
  await page.route('**/*', async route => {
    const request = route.request();
    const url = new URL(request.url());
    // The map SDK can emit background telemetry while the Driver dashboard is open.
    const endpoint = `${url.origin}${url.pathname}`;
    if (request.method() === 'POST' && endpoint !== 'https://api.maptiler.com/metrics') posts.push(endpoint);
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
    if (routePath === conversations) return ok({ items: isA && !reset ? [conversation] : [], nextCursor: null });
    if (routePath === `${conversations}/${conversationId}/reset` && isA) {
      assert.equal(request.method(), 'POST');
      reset = true;
      return ok({ reset: true });
    }
    if (routePath === messages && isA) {
      if (request.method() === 'POST') {
        const { expectedRevision, message, clientMessageId } = request.postDataJSON();
        if (message === `reservation details ${reservationId}`) {
          assert.equal(expectedRevision, 8);
          detailAvailable = true;
          return ok({ requestId: clientMessageId, conversationId, clientMessageId, state: 'completed', answer: detail, revision: 10 });
        }
        sentRevision = expectedRevision;
        latestAvailable = true;
        return route.fulfill({ status: 409, json: { success: false, code: 'REVISION_CONFLICT' } });
      }
      if (url.searchParams.has('cursor')) {
        return ok({ conversation: { ...conversation, revision: 4 }, items: [older], nextCursor: null,
          activeRequestClientMessageId: null });
      }
      if (latestAvailable) {
        const items = [answer, latest, empty, failure, ...(detailAvailable ? [detail] : [])];
        return ok({ conversation: { ...conversation, revision: items.at(-1).sequence }, items, nextCursor: null,
          activeRequestClientMessageId: null });
      }
      if (hold) await historyReleased;
      return ok({ conversation, items: recovered ? [answer] : [], nextCursor: paginate ? 'older-page' : null,
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
  const booking = panel.getByRole('article', { name: 'Bản nháp đặt chỗ' });
  const send = async text => {
    await panel.getByRole('textbox').fill(text);
    await button('Gửi', panel).click();
  };
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
    await panel.getByText('Chỗ trống: 0', { exact: true }).waitFor();
    await panel.getByText(`Mã lịch đặt ${reservationWithoutAction}`, { exact: true }).waitFor();
    await button('Đưa vào ô soạn', panel).click();
    assert.equal(await panel.getByRole('textbox').inputValue(), question);
    assert.equal(requests.filter(([method]) => method === 'POST').length, 1);
    await button('Đóng', panel).click();
    paginate = true;
    await driver.click();
    await panel.getByText(answer.content, { exact: true }).waitFor();
    // Dispatch without Playwright scrolling the button into view and triggering auto-pagination.
    await button('Tin nhắn cũ hơn', panel).dispatchEvent('click');
    await panel.getByText(older.content, { exact: true }).waitFor();
    await button('Gửi', panel).click();
    await panel.getByText(latest.content, { exact: true }).waitFor();
    await panel.getByText(empty.content, { exact: true }).waitFor();
    await panel.getByText(failure.content, { exact: true }).waitFor();
    assert.equal(sentRevision, conversation.revision);
    assert.equal(await panel.getByRole('textbox').inputValue(), question);
    await button('Xem chi tiết mới nhất', panel).first().click();
    await panel.getByText(detail.content, { exact: true }).waitFor();
    const postsBeforeDraft = posts.length;
    await send('đặt cho tôi chỗ ô tô từ 14h đến 16h mai');
    await booking.getByText(/xe nào.*bãi nào/).waitFor();
    await send('51A-123.45');
    await booking.getByText(/bãi nào/).waitFor();
    await send('bãi FPT');
    await booking.getByText('FPT', { exact: true }).waitFor();
    await booking.getByText(/14:00.*16:00/).waitFor();
    await send('đổi xe sang xe máy');
    await booking.getByText('Xe máy', { exact: true }).waitFor();
    assert.equal(await booking.getByText('51A-123.45', { exact: true }).count(), 0);
    await send('xe 59A1-123.45 nhé?');
    await booking.getByText('59A1-123.45', { exact: true }).waitFor();
    await send('đổi bãi sang bãi Vincom và đổi giờ sang 15h đến 17h');
    await booking.getByText(/từng thay đổi/).waitFor();
    await booking.getByText('FPT', { exact: true }).waitFor();
    await send('đổi bãi sang bãi Trung Tâm');
    await booking.getByText('Trung Tâm', { exact: true }).waitFor();
    await send('đổi giờ sang chiều mai');
    await booking.getByText(/giờ bắt đầu và giờ kết thúc/).waitFor();
    assert.equal(await booking.getByText(/14:00.*16:00/).count(), 0);
    await send('đổi giờ sang 15h đến 17h');
    await booking.getByText(/15:00.*17:00/).waitFor();
    await send('đồng ý');
    await booking.getByText(/xem lại thông tin bên dưới/).waitFor();
    assert.deepEqual(posts.slice(postsBeforeDraft), []);
    assert.equal(await booking.evaluate(element => element.scrollWidth > element.clientWidth), false);
    latestAvailable = false;
    paginate = false;
    await button('Mới', panel).click();
    const confirm = page.getByRole('dialog', { name: 'Tạo đoạn chat mới?' });
    await button('Giữ đoạn chat', confirm).click();
    assert.equal(reset, false);
    await panel.getByText(answer.content, { exact: true }).waitFor();
    await button('Đóng', panel).click();
    enabled = false;
    await afterResponse('/api/assistant/capabilities', focus);
    await driver.click();
    await panel.getByText('Hiện chỉ có thể xem lịch sử. Gửi câu hỏi chưa khả dụng.').waitFor();
    await panel.getByText(answer.content, { exact: true }).waitFor();
    await booking.waitFor({ state: 'hidden' });
    enabled = true;
    await afterResponse('/api/assistant/capabilities', focus);
    await send('đặt chỗ ô tô từ 14h đến 16h mai tại bãi Riêng Của A');
    await booking.getByText('Riêng Của A', { exact: true }).waitFor();
    await button('Đóng', panel).click();
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
    await panel.getByRole('textbox').waitFor();
    assert.equal(await panel.getByText(answer.content, { exact: true }).count(), 0);
    assert.equal(await booking.count(), 0);
    await send('đặt chỗ ô tô từ 14h đến 16h mai');
    await booking.getByText(/xe nào.*bãi nào/).waitFor();
    assert.equal(await booking.getByText('Riêng Của A', { exact: true }).count(), 0);
    assert.equal(await panel.getByRole('combobox').count(), 0);
    await button('Đóng', panel).click();
    await page.locator('.profile-trigger').filter({ hasText: 'Fixture b' }).click();
    await button('Sign Out').click();
    if (!(await page.getByPlaceholder('you@example.com or phone number').isVisible())) {
      await button('Sign In').first().click();
    }
    hold = false;
    await login('a');
    await driver.click();
    await panel.getByText(answer.content, { exact: true }).waitFor();
    assert.equal(await booking.count(), 0);
    await send('đặt chỗ ô tô từ 14h đến 16h mai tại bãi Mới');
    await booking.getByText('Mới', { exact: true }).waitFor();
    await button('Mới', panel).click();
    await afterResponse(`${conversations}/${conversationId}/reset`, () => button('Tạo chat mới', confirm).click());
    await panel.getByText(answer.content, { exact: true }).waitFor({ state: 'hidden' });
    await booking.waitFor({ state: 'hidden' });
    assert.equal(reset, true);
    console.log(`${viewport.width}x${viewport.height}: FAQ, history/recovery, S06 reads, S07 booking preview and edits, logout/relogin PASS`);
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
