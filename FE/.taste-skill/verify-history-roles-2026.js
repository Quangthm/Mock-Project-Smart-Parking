async (page) => {
  const base = 'C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/current';
  const results = [];
  await page.goto('http://127.0.0.1:5176/');
  await page.getByRole('button', { name: 'Sign Up' }).click();
  await page.setViewportSize({ width: 375, height: 900 });
  await page.screenshot({ path: `${base}/sign-up-2026-light-375.png` });
  results.push({ screen: 'sign-up', overflow: await page.evaluate(() => document.documentElement.scrollWidth - innerWidth), panel: await page.locator('.auth-panel').boundingBox() });
  for (const role of ['driver', 'owner', 'operator', 'admin']) {
    await page.unroute('**/api/auth/me');
    await page.route('**/api/auth/me', async (route) => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, data: { userId: `visual-${role}`, fullName: `Visual ${role}`, email: 'visual@example.com', role, onboardingComplete: true } }) }));
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('http://127.0.0.1:5176/');
    await page.evaluate(() => { sessionStorage.setItem('sp_access_token', 'visual-only-token'); sessionStorage.setItem('sp_access_expires_at', String(Date.now() + 3600000)); });
    await page.reload();
    await page.waitForTimeout(300);
    const sizes = await page.evaluate(() => ({ notification: document.querySelector('.dashboard-topbar-actions > div[style] > button')?.getBoundingClientRect().width, icon: document.querySelector('.dashboard-topbar .icon-button')?.getBoundingClientRect().width, overflow: document.documentElement.scrollWidth - innerWidth }));
    results.push({ role, ...sizes });
    if (role === 'driver') {
      await page.evaluate(() => window.dispatchEvent(new CustomEvent('sp:dashboard-nav', { detail: 'history' })));
      await page.waitForTimeout(250);
      await page.screenshot({ path: `${base}/booking-history-2026-light-1440.png` });
      results.push({ screen: 'history', gap: await page.evaluate(() => { const header = document.querySelector('.dashboard-content h1'); const tabs = document.querySelector('.booking-history-tabs'); return header && tabs ? tabs.getBoundingClientRect().top - header.getBoundingClientRect().bottom : null; }) });
    }
  }
  await page.unroute('**/api/auth/me');
  return results;
}
