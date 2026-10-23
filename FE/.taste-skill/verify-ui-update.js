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
        localStorage.setItem('sp_theme', 'light');
        sessionStorage.setItem('sp_access_token', 'visual-only-token');
        sessionStorage.setItem('sp_access_expires_at', String(Date.now() + 3600000));
      });
      await page.reload();
      if (width < 1101) await page.getByRole('button', { name: 'Open navigation' }).click();
      results.push(await page.evaluate((currentRole) => ({ role: currentRole, width: innerWidth,
        overflow: document.documentElement.scrollWidth > innerWidth,
        menuVisible: getComputedStyle(document.querySelector('.dashboard-sidebar')).visibility,
      }), role));
      await page.screenshot({ path: `${out}/${role}-menu-${width}.png` });
      if (width < 1101) await page.locator('.dashboard-sidebar-close').click();
      await page.getByRole('button', { name: 'Open profile menu' }).click();
      await page.getByRole('button', { name: 'Settings', exact: true }).click();
      await page.waitForTimeout(400);
      await page.screenshot({ path: `${out}/${role}-settings-${width}.png` });
      const title = await page.locator('#account-dialog-title').textContent();
      results.push({ role, width, settingsTitle: title?.trim() });
      await page.getByRole('button', { name: 'Interface', exact: true }).click();
      if (role === 'driver') await page.screenshot({ path: `${out}/${role}-interface-${width}.png` });
      await page.getByRole('button', { name: 'Close settings' }).click();
    }
  }
  await page.unroute('**/api/auth/me');
  return results;
}
