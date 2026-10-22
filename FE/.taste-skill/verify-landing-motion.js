async (page) => {
  await page.unroute('**/api/auth/me');
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('http://127.0.0.1:5174/');
  await page.evaluate(() => {
    sessionStorage.removeItem('sp_access_token');
    sessionStorage.removeItem('sp_access_expires_at');
  });
  await page.reload();
  await page.locator('.landing-page').waitFor({ state: 'visible' });
  await page.waitForTimeout(700);
  const initial = await page.evaluate(() => ({
    total: document.querySelectorAll('.landing-reveal').length,
    visible: document.querySelectorAll('.landing-reveal.is-visible').length,
    overflow: document.documentElement.scrollWidth > innerWidth,
  }));
  for (const y of [600, 1200, 1800, 2400, 3000]) {
    await page.evaluate((top) => window.scrollTo({ top, behavior: 'instant' }), y);
    await page.waitForTimeout(180);
  }
  await page.waitForTimeout(750);
  const afterScroll = await page.evaluate(() => ({
    total: document.querySelectorAll('.landing-reveal').length,
    visible: document.querySelectorAll('.landing-reveal.is-visible').length,
    overflow: document.documentElement.scrollWidth > innerWidth,
  }));
  await page.evaluate(() => window.scrollTo({ top: 0, behavior: 'instant' }));
  await page.waitForTimeout(700);
  await page.screenshot({ path: 'C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/current/landing-motion-1440.png', fullPage: true });
  return { initial, afterScroll };
}
