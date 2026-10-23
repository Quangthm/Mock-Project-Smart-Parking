async (page) => {
  await page.route('**/api/auth/me', (route) => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, data: { userId: 'visual-driver', fullName: 'Visual driver', email: 'visual@example.com', role: 'driver', onboardingComplete: true } }) }));
  const results = [];
  for (const width of [375, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    await page.goto('http://127.0.0.1:5176/');
    await page.evaluate(() => { sessionStorage.setItem('sp_access_token', 'visual-only-token'); sessionStorage.setItem('sp_access_expires_at', String(Date.now() + 3600000)); localStorage.setItem('sp_theme', 'light'); });
    await page.reload();
    await page.locator('.parking-map-label').first().waitFor({ timeout: 15000 });
    await page.waitForTimeout(800);
    const markerRects = await page.locator('.parking-map-label').evaluateAll((elements) => elements.map((element) => { const rect = element.getBoundingClientRect(); return { left: Math.round(rect.left), right: Math.round(rect.right), top: Math.round(rect.top), bottom: Math.round(rect.bottom) }; }));
    await page.screenshot({ path: `C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/new-ui-234/map-light-${width}.png` });
    await page.getByRole('button', { name: 'Map controls' }).click();
    await page.getByRole('button', { name: 'Zoom in' }).click();
    const menuClosed = !(await page.locator('.find-parking-map-menu-list').count());
    const visibleMarker = markerRects.findIndex((rect) => rect.left >= 0 && rect.right <= width && rect.top >= 0 && rect.bottom <= 900);
    if (visibleMarker >= 0) await page.locator('.parking-map-label').nth(visibleMarker).click({ force: true });
    const detailsOpen = await page.locator('.find-parking-details-panel').isVisible();
    if (await page.getByRole('button', { name: 'Collapse parking search' }).count()) await page.getByRole('button', { name: 'Collapse parking search' }).click();
    const searchButtonRadius = await page.locator('.find-parking-reopen').evaluate((element) => getComputedStyle(element).borderRadius);
    const mapTypeRadius = await page.locator('.find-parking-map-style').evaluate((element) => getComputedStyle(element).borderRadius);
    results.push({ width, markerRects, visibleMarker, menuClosed, detailsOpen, searchButtonRadius, mapTypeRadius, overflow: await page.evaluate(() => document.documentElement.scrollWidth - innerWidth) });
  }
  return results;
}
