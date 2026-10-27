async (page) => {
  const out = 'C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/current';
  await page.unroute('**/api/auth/me');
  await page.route('**/api/auth/me', async (route) => route.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify({ success: true, data: {
      userId: 'visual-owner', fullName: 'Visual owner', email: 'visual@example.com',
      role: 'owner', onboardingComplete: true,
    } }),
  }));
  const checks = [];
  for (const theme of ['light', 'dark']) {
    for (const width of [375, 768, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      await page.goto('http://127.0.0.1:5174/');
      await page.evaluate((selectedTheme) => {
        sessionStorage.setItem('sp_access_token', 'visual-only-token');
        sessionStorage.setItem('sp_access_expires_at', String(Date.now() + 3600000));
        localStorage.setItem('sp_theme', selectedTheme);
      }, theme);
      await page.reload();
      await page.waitForTimeout(500);
      await page.screenshot({ path: `${out}/owner-after-${theme}-${width}.png` });
      const menu = page.getByRole('button', { name: 'Open navigation' });
      await menu.click();
      const last = page.locator('.dashboard-nav-item').last();
      await last.focus();
      await page.keyboard.press('Tab');
      const wrapped = await page.evaluate(() => document.activeElement?.getAttribute('aria-label'));
      await page.keyboard.press('Escape');
      checks.push({ theme, width, wrapped, overflow: await page.evaluate(() => document.documentElement.scrollWidth - innerWidth) });
    }
  }
  await page.unroute('**/api/auth/me');
  return checks;
}
