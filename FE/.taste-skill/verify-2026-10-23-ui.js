async (page) => {
  const base = 'C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/current';
  const report = [];
  for (const width of [375, 768, 1440]) {
    for (const theme of ['light', 'dark']) {
      await page.setViewportSize({ width, height: 900 });
      await page.goto('http://127.0.0.1:5176/');
      await page.evaluate((selected) => localStorage.setItem('sp_theme', selected), theme);
      await page.reload();
      await page.waitForTimeout(1000);
      await page.screenshot({ path: `${base}/landing-2026-${theme}-${width}.png` });
      report.push({ screen: 'landing', width, theme, overflow: await page.evaluate(() => document.documentElement.scrollWidth - innerWidth) });
      await page.getByRole('button', { name: 'Sign In' }).click();
      await page.waitForTimeout(300);
      await page.screenshot({ path: `${base}/sign-in-2026-${theme}-${width}.png` });
      report.push({ screen: 'sign-in', width, theme, overflow: await page.evaluate(() => document.documentElement.scrollWidth - innerWidth), panel: await page.locator('.auth-panel').boundingBox() });
    }
  }
  await page.route('**/api/auth/me', async (route) => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, data: { userId: 'visual-driver', fullName: 'Visual driver', email: 'visual@example.com', role: 'driver', onboardingComplete: true } }) }));
  for (const width of [375, 768, 1440]) {
    for (const theme of ['light', 'dark']) {
      await page.setViewportSize({ width, height: 900 });
      await page.goto('http://127.0.0.1:5176/');
      await page.evaluate((selected) => { sessionStorage.setItem('sp_access_token', 'visual-only-token'); sessionStorage.setItem('sp_access_expires_at', String(Date.now() + 3600000)); localStorage.setItem('sp_theme', selected); }, theme);
      await page.reload();
      await page.waitForTimeout(1200);
      const open = page.getByRole('button', { name: 'Open parking search' });
      if (await open.count()) {
        await open.click();
        await page.waitForTimeout(200);
        await page.screenshot({ path: `${base}/parking-search-2026-${theme}-${width}.png` });
        report.push({ screen: 'parking-search', width, theme, overflow: await page.evaluate(() => document.documentElement.scrollWidth - innerWidth), history: await page.locator('.find-parking-history').count(), filter: await page.locator('.find-parking-filter-toggle').count() });
      }
    }
  }
  await page.unroute('**/api/auth/me');
  return report;
}
