async (page) => {
  const out = 'C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/after';
  const findings = [];
  for (const role of ['driver', 'owner', 'operator', 'admin']) {
    await page.unroute('**/api/auth/me');
    await page.route('**/api/auth/me', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: { 'access-control-allow-origin': '*' },
        body: JSON.stringify({ success: true, data: {
          userId: `visual-${role}`,
          fullName: `Visual ${role}`,
          email: 'visual@example.com',
          role,
          onboardingComplete: true,
        } }),
      });
    });
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
        const info = await page.evaluate(() => ({
          width: innerWidth,
          scrollWidth: document.documentElement.scrollWidth,
          title: document.querySelector('main h1')?.textContent?.trim() || document.querySelector('h1')?.textContent?.trim(),
        }));
        await page.screenshot({ path: `${out}/${role}-fixture-${theme}-${width}.png`, fullPage: true });
        findings.push({ role, theme, ...info });
      }
    }
  }
  await page.unroute('**/api/auth/me');
  await page.evaluate(() => {
    sessionStorage.removeItem('sp_access_token');
    sessionStorage.removeItem('sp_access_expires_at');
  });
  return findings;
}
