async (page) => {
  const out = 'C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/current';
  const results = [];
  for (const role of ['driver', 'owner', 'operator', 'admin']) {
    await page.unroute('**/api/auth/me');
    await page.route('**/api/auth/me', async (route) => route.fulfill({
      status: 200, contentType: 'application/json',
      headers: { 'access-control-allow-origin': '*' },
      body: JSON.stringify({ success: true, data: {
        userId: `visual-${role}`, fullName: `Visual ${role}`,
        email: 'visual@example.com', role, onboardingComplete: true,
      } }),
    }));
    for (const width of [375, 768, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      await page.goto('http://127.0.0.1:5174/');
      await page.evaluate(() => {
        sessionStorage.setItem('sp_access_token', 'visual-only-token');
        sessionStorage.setItem('sp_access_expires_at', String(Date.now() + 3600000));
      });
      await page.reload();
      const toggle = page.getByRole('button', { name: 'Open navigation' });
      await toggle.waitFor();
      const closed = await page.evaluate(() => ({
        expanded: document.querySelector('.menu-toggle')?.getAttribute('aria-expanded'),
        sidebar: getComputedStyle(document.querySelector('.dashboard-sidebar')).visibility,
        overflow: document.documentElement.scrollWidth > innerWidth,
      }));
      if (closed.expanded !== 'false' || closed.sidebar !== 'hidden' || closed.overflow) {
        throw new Error(`${role} ${width}px closed state: ${JSON.stringify(closed)}`);
      }
      if (width !== 768) await page.screenshot({ path: `${out}/${role}-menu-closed-${width}.png` });
      await toggle.click();
      await page.waitForTimeout(260);
      const open = await page.evaluate(() => ({
        expanded: document.querySelector('.menu-toggle')?.getAttribute('aria-expanded'),
        sidebar: getComputedStyle(document.querySelector('.dashboard-sidebar')).visibility,
        top: Math.round(document.querySelector('.dashboard-sidebar').getBoundingClientRect().top),
        overflow: document.documentElement.scrollWidth > innerWidth,
      }));
      if (open.expanded !== 'true' || open.sidebar !== 'visible' || open.top !== 60 || open.overflow) {
        throw new Error(`${role} ${width}px open state: ${JSON.stringify(open)}`);
      }
      if (width !== 768) await page.screenshot({ path: `${out}/${role}-menu-open-${width}.png` });
      await page.keyboard.press('Escape');
      await page.waitForFunction(() => getComputedStyle(document.querySelector('.dashboard-sidebar')).visibility === 'hidden');
      const afterEscape = await page.evaluate(() => ({
        expanded: document.querySelector('.menu-toggle')?.getAttribute('aria-expanded'),
        sidebar: getComputedStyle(document.querySelector('.dashboard-sidebar')).visibility,
      }));
      if (afterEscape.expanded !== 'false' || afterEscape.sidebar !== 'hidden') {
        throw new Error(`${role} ${width}px Escape state: ${JSON.stringify(afterEscape)}`);
      }
      results.push({ role, width, closed, open, afterEscape });
    }
  }
  await page.unroute('**/api/auth/me');
  return results;
}
