async (page) => {
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
    await page.setViewportSize({ width: 375, height: 900 });
    await page.goto('http://127.0.0.1:5174/');
    await page.evaluate(() => {
      sessionStorage.setItem('sp_access_token', 'visual-only-token');
      sessionStorage.setItem('sp_access_expires_at', String(Date.now() + 3600000));
      localStorage.setItem('sp_theme', 'light');
    });
    await page.reload();
    await page.getByRole('button', { name: 'Open navigation' }).click();
    const navItems = page.locator('.dashboard-nav-item');
    const second = await navItems.nth(1).innerText();
    await navItems.nth(1).click();
    await page.waitForTimeout(100);
    const state = await page.evaluate(() => {
      const trigger = document.querySelector('.menu-toggle');
      const style = getComputedStyle(trigger);
      return {
        expanded: trigger?.getAttribute('aria-expanded'),
        focused: document.activeElement?.getAttribute('aria-label'),
        outline: style.outlineStyle,
        outlineWidth: style.outlineWidth,
        overflow: document.documentElement.scrollWidth - innerWidth,
      };
    });
    results.push({ role, second, state });
  }
  await page.unroute('**/api/auth/me');
  return results;
}
