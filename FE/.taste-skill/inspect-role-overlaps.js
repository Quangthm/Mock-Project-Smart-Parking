async (page) => {
  const report = [];
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
        localStorage.setItem('sp_theme', 'light');
      });
      await page.reload();
      await page.waitForTimeout(350);
      report.push(await page.evaluate((role) => {
        const bar = document.querySelector('.dashboard-topbar')?.getBoundingClientRect();
        const content = document.querySelector('.dashboard-content')?.getBoundingClientRect();
        return {role, width: innerWidth, overflow: document.documentElement.scrollWidth - innerWidth,
          topbarBottom: Math.round(bar?.bottom ?? 0), contentTop: Math.round(content?.top ?? 0),
          menuHidden: getComputedStyle(document.querySelector('.dashboard-sidebar')).visibility === 'hidden'};
      }, role));
      if (role === 'driver') {
        try {
          await page.locator('.find-parking-result').first().click({ timeout: 4000 });
          report.push({ role, width, searchClickable: await page.getByRole('button', {name: 'Book Now'}).isVisible() });
        } catch (error) { report.push({ role, width, searchClickable: false, reason: String(error).slice(0, 180) }); }
      }
    }
  }
  await page.unroute('**/api/auth/me');
  return report;
}
