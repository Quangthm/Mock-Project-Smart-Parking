async (page) => {
  const out = 'C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/new-ui';
  const results = [];
  for (const role of ['driver', 'owner', 'operator', 'admin']) {
    await page.unroute('**/api/auth/me');
    await page.route('**/api/auth/me', async (route) => route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ success: true, data: {
        userId: `visual-${role}`, fullName: `Visual ${role}`,
        email: 'visual@example.com', role, onboardingComplete: true,
      } }),
    }));
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
        await page.screenshot({ path: `${out}/${role}-${theme}-${width}.png` });
        results.push({ role, theme, width, measures: await page.evaluate(() => ({
          overflow: document.documentElement.scrollWidth - innerWidth,
          rail: document.querySelector('.dashboard-topbar')?.getBoundingClientRect().toJSON(),
          content: document.querySelector('.dashboard-content')?.getBoundingClientRect().toJSON(),
        })) });
      }
    }
  }
  await page.unroute('**/api/auth/me');
  return results;
}
