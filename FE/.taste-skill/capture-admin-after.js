async (page) => {
  const out = 'C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/after';
  await page.route('**/api/auth/me', async (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    headers: { 'access-control-allow-origin': '*' },
    body: JSON.stringify({ success: true, data: {
      userId: 'visual-admin', fullName: 'Visual admin',
      email: 'visual@example.com', role: 'admin', onboardingComplete: true,
    } }),
  }));
  const results = [];
  for (const theme of ['light', 'dark']) {
    for (const width of [375, 768, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      await page.goto('http://127.0.0.1:5174/');
      await page.evaluate((value) => {
        localStorage.setItem('sp_theme', value);
        sessionStorage.setItem('sp_access_token', 'visual-only-token');
        sessionStorage.setItem('sp_access_expires_at', String(Date.now() + 3600000));
      }, theme);
      await page.reload();
      await page.waitForTimeout(300);
      results.push(await page.evaluate(() => ({
        width: innerWidth,
        scrollWidth: document.documentElement.scrollWidth,
        columns: getComputedStyle(document.querySelector('.admin-overview-charts')).gridTemplateColumns,
      })));
      await page.screenshot({ path: `${out}/admin-fixture-${theme}-${width}.png`, fullPage: true });
    }
  }
  await page.unroute('**/api/auth/me');
  return results;
}
