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
        await page.waitForTimeout(550);
        const before = await page.evaluate(() => ({
          overflow: document.documentElement.scrollWidth - innerWidth,
          contentWidth: document.querySelector('.dashboard-content')?.getBoundingClientRect().width,
          palette: getComputedStyle(document.querySelector('.dashboard-theme')).getPropertyValue('--primary').trim(),
        }));
        await page.screenshot({ path: `${out}/${role}-after-${theme}-${width}.png` });
        const menu = page.getByRole('button', { name: 'Open navigation' });
        await menu.click();
        await page.waitForTimeout(230);
        const open = await page.evaluate(() => ({
          contentWidth: document.querySelector('.dashboard-content')?.getBoundingClientRect().width,
          sidebarVisible: getComputedStyle(document.querySelector('.dashboard-sidebar')).visibility,
          focused: document.activeElement?.getAttribute('aria-label'),
        }));
        if (width === 375) await page.screenshot({ path: `${out}/${role}-menu-${theme}-${width}.png` });
        await page.keyboard.press('Escape');
        const closed = await page.evaluate(() => ({
          expanded: document.querySelector('.menu-toggle')?.getAttribute('aria-expanded'),
          focused: document.activeElement?.getAttribute('aria-label'),
        }));
        results.push({ role, theme, width, before, open, closed });
      }
    }
  }
  await page.unroute('**/api/auth/me');
  return results;
}
