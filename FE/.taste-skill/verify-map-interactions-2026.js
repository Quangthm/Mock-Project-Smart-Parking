async (page) => {
  await page.route('**/api/auth/me', async (route) => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, data: { userId: 'visual-driver', fullName: 'Visual driver', email: 'visual@example.com', role: 'driver', onboardingComplete: true } }) }));
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('http://127.0.0.1:5176/');
  await page.evaluate(() => { sessionStorage.setItem('sp_access_token', 'visual-only-token'); sessionStorage.setItem('sp_access_expires_at', String(Date.now() + 3600000)); localStorage.setItem('sp_theme', 'dark'); });
  await page.reload();
  await page.waitForTimeout(1800);
  await page.getByRole('button', { name: 'Open parking search' }).click();
  await page.getByRole('button', { name: /Filters/ }).click();
  const filters = await page.locator('#find-parking-filter-options').innerText();
  await page.getByLabel('Search by parking name or address').fill('Nguyen');
  await page.getByLabel('Search by parking name or address').press('Enter');
  await page.getByRole('button', { name: 'Collapse parking search' }).click();
  await page.getByRole('button', { name: 'Open parking search' }).click();
  await page.getByLabel('Search by parking name or address').fill('');
  const history = await page.locator('.find-parking-history').innerText();
  await page.locator('.parking-map-label').first().hover();
  await page.waitForTimeout(250);
  const hover = await page.locator('.parking-map-marker').first().evaluate((element) => ({ classes: element.className, text: element.querySelector('.parking-map-hover')?.textContent, visible: getComputedStyle(element.querySelector('.parking-map-hover')).visibility }));
  await page.screenshot({ path: 'C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/current/map-hover-2026-dark-1440.png' });
  await page.locator('.parking-map-label').first().click();
  const details = await page.locator('.find-parking-details-panel').count();
  await page.unroute('**/api/auth/me');
  return { filters, history, hover, details };
}
