async (page) => {
  const out = 'C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/new-ui-234';
  await page.route('**/api/auth/me', (route) => route.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify({ success: true, data: { userId: 'visual-driver', fullName: 'Visual driver', email: 'visual@example.com', role: 'driver', onboardingComplete: true } }),
  }));
  const results = [];
  for (const theme of ['light', 'dark']) {
    for (const width of [375, 768, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      await page.goto('http://127.0.0.1:5176/');
      await page.evaluate((selectedTheme) => {
        sessionStorage.setItem('sp_access_token', 'visual-only-token');
        sessionStorage.setItem('sp_access_expires_at', String(Date.now() + 3600000));
        localStorage.setItem('sp_theme', selectedTheme);
        const lots = JSON.parse(localStorage.getItem('sp_lots') || '[]');
        const lot = lots[0];
        if (lot) {
          const now = Date.now();
          localStorage.setItem('sp_bookings', JSON.stringify([
            { id:'VISUAL-01', driverId:'visual-driver', lotId:lot.id, lotName:lot.name, slotId:'visual-a08', slotNumber:'A-08', licensePlate:'59-B1 678.90', startTime:new Date(now - 3600000).toISOString(), endTime:new Date(now + 7200000).toISOString(), status:'checked-in', amount:100000, createdAt:new Date(now - 7200000).toISOString() },
            { id:'VISUAL-02', driverId:'visual-driver', lotId:lot.id, lotName:lot.name, slotId:'visual-b12', slotNumber:'B-12', licensePlate:'59-B1 678.90', startTime:new Date(now + 86400000).toISOString(), endTime:new Date(now + 93600000).toISOString(), status:'confirmed', amount:85000, createdAt:new Date(now).toISOString() },
          ]));
        }
      }, theme);
      await page.reload();
      await page.locator('.dashboard-nav-item').first().waitFor();
      await page.locator('.parking-map-label').first().waitFor({ timeout: 12000 }).catch(() => {});
      await page.waitForTimeout(650);
      const mapLabels = await page.locator('.parking-map-label').count();
      await page.screenshot({ path: `${out}/map-${theme}-${width}.png` });
      await page.getByRole('button', { name: 'Map controls' }).click();
      const menuVisible = await page.locator('.find-parking-map-menu-list').isVisible();
      await page.screenshot({ path: `${out}/map-menu-${theme}-${width}.png` });
      await page.getByRole('button', { name: 'Current booking', exact: true }).click();
      await page.waitForTimeout(450);
      await page.screenshot({ path: `${out}/parking-now-${theme}-${width}.png` });
      await page.getByRole('button', { name: /Upcoming/ }).click();
      await page.screenshot({ path: `${out}/upcoming-${theme}-${width}.png` });
      results.push({ theme, width, menuVisible, mapLabels, overflow: await page.evaluate(() => document.documentElement.scrollWidth - innerWidth) });
    }
  }
  return results;
}
