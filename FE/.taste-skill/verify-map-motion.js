async (page) => {
  await page.route('**/api/auth/me', (route) => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, data: { userId: 'visual-driver', fullName: 'Visual driver', email: 'visual@example.com', role: 'driver', onboardingComplete: true } }) }));
  const outcomes = [];
  for (const width of [375, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    await page.goto('http://127.0.0.1:5176/');
    await page.evaluate(() => { sessionStorage.setItem('sp_access_token', 'visual-only-token'); sessionStorage.setItem('sp_access_expires_at', String(Date.now() + 3600000)); localStorage.setItem('sp_theme', 'light'); });
    await page.reload();
    await page.locator('.parking-map-label').first().waitFor({ timeout: 15000 });
    await page.waitForTimeout(550);
    const getMarker = () => page.locator('.parking-map-label').first().evaluate((node) => { const r = node.getBoundingClientRect(); return { x: Math.round(r.x), y: Math.round(r.y), width: Math.round(r.width), height: Math.round(r.height) }; });
    const before = await getMarker();
    const searchInitiallyClosed = await page.locator('.find-parking-reopen').isVisible();
    await page.getByRole('button', { name: 'Map controls' }).click();
    await page.getByRole('button', { name: 'Zoom in' }).click();
    await page.waitForTimeout(700);
    const afterZoom = await getMarker();
    const map = await page.locator('.find-parking-map-canvas').boundingBox();
    if (map) {
      await page.mouse.move(map.x + map.width * .52, map.y + map.height * .67);
      await page.mouse.down();
      await page.mouse.move(map.x + map.width * .62, map.y + map.height * .67, { steps: 12 });
      await page.mouse.up();
      await page.waitForTimeout(350);
    }
    const afterPan = await getMarker();
    await page.getByRole('button', { name: 'Open parking search' }).click();
    const searchOpens = await page.locator('.find-parking-panel').isVisible();
    await page.getByRole('button', { name: 'Collapse parking search' }).click();
    await page.screenshot({ path: `C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/new-ui-234/map-refined-light-${width}.png` });
    outcomes.push({ width, before, afterZoom, afterPan, searchInitiallyClosed, searchOpens, overflow: await page.evaluate(() => document.documentElement.scrollWidth - innerWidth) });
  }
  return outcomes;
}
