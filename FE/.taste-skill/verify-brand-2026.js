async (page) => {
  const base = 'C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/current';
  const results = [];
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('http://127.0.0.1:5177/');
  await page.evaluate(() => sessionStorage.clear());
  await page.reload();
  await page.screenshot({ path: `${base}/smartparking-public-brand-1440.png` });
  results.push({ view: 'public', title: await page.title(), name: await page.locator('nav .brand-wordmark-text').first().innerText() });
  let role = 'driver';
  await page.route('**/api/auth/me', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, data: { userId: `brand-${role}`, fullName: `Visual ${role}`, email: `${role}@example.com`, role, onboardingComplete: true } }) }));
  for (role of ['driver', 'owner', 'operator', 'admin']) {
    for (const width of [375, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      await page.goto('http://127.0.0.1:5177/');
      await page.evaluate(() => { sessionStorage.setItem('sp_access_token', 'visual-only-token'); sessionStorage.setItem('sp_access_expires_at', String(Date.now() + 3600000)); });
      await page.reload();
      await page.waitForTimeout(350);
      const brand = page.locator('.dashboard-brand');
      results.push({ role, width, logo: await brand.locator('.brand-logo').count(), name: await brand.innerText(), aria: await brand.getAttribute('aria-label'), overflow: await page.evaluate(() => Math.max(0, document.documentElement.scrollWidth - innerWidth)) });
      await page.screenshot({ path: `${base}/smartparking-brand-${role}-${width}.png` });
    }
  }
  return results;
}
