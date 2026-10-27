async (page) => {
  await page.route('**/api/auth/me', (route) => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, data: { userId: 'visual-driver', fullName: 'Visual driver', email: 'visual@example.com', role: 'driver', onboardingComplete: true } }) }));
  const results = [];
  for (const width of [375, 768, 1440]) {
    for (const theme of ['light', 'dark']) {
      await page.setViewportSize({ width, height: 900 });
      await page.goto('http://127.0.0.1:5176/');
      await page.evaluate((value) => {
        sessionStorage.setItem('sp_access_token', 'visual-only-token');
        sessionStorage.setItem('sp_access_expires_at', String(Date.now() + 3600000));
        localStorage.setItem('sp_theme', value);
      }, theme);
      await page.reload();
      await page.locator('.parking-map-label').first().waitFor({ timeout: 15000 });
      await page.waitForTimeout(450);
      const labels = await page.locator('.parking-map-label').evaluateAll((nodes) => nodes.map((node) => {
        const rect = node.getBoundingClientRect();
        return { x: Math.round(rect.x), y: Math.round(rect.y), right: Math.round(rect.right), bottom: Math.round(rect.bottom), width: Math.round(rect.width), height: Math.round(rect.height) };
      }));
      const searchButtonClickable = await page.locator('.find-parking-reopen').evaluate((node) => {
        const rect = node.getBoundingClientRect();
        const hit = document.elementFromPoint(rect.x + rect.width / 2, rect.y + rect.height / 2);
        return hit === node || node.contains(hit);
      });
      await page.screenshot({ path: `C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/new-ui-234/map-final-${theme}-${width}.png` });
      await page.getByRole('button', { name: 'Open parking search' }).click();
      const searchOpens = await page.locator('.find-parking-panel').isVisible();
      await page.getByRole('button', { name: 'Collapse parking search' }).click();
      results.push({ width, theme, labels, searchButtonClickable, searchOpens, overflow: await page.evaluate(() => document.documentElement.scrollWidth - innerWidth) });
    }
  }
  return results;
}
