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
    for (const width of [375, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      await page.goto('http://127.0.0.1:5174/');
      await page.evaluate(() => {
        sessionStorage.setItem('sp_access_token', 'visual-only-token');
        sessionStorage.setItem('sp_access_expires_at', String(Date.now() + 3600000));
        localStorage.setItem('sp_theme', 'light');
      });
      await page.reload();
      await page.waitForTimeout(450);
      await page.screenshot({ path: `${out}/${role}-before-${width}.png` });
      results.push({ role, width, overflow: await page.evaluate(() => document.documentElement.scrollWidth - innerWidth) });
    }
  }
  await page.unroute('**/api/auth/me');
  return results;
}
