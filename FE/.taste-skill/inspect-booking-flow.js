async (page) => {
  await page.unroute('**/api/auth/me');
  await page.route('**/api/auth/me', async (route) => route.fulfill({
    status: 200, contentType: 'application/json',
    headers: { 'access-control-allow-origin': '*' },
    body: JSON.stringify({ success: true, data: {
      userId: 'visual-driver', fullName: 'Visual Driver',
      email: 'visual@example.com', role: 'driver', onboardingComplete: true,
    } }),
  }));
  const results = [];
  for (const width of [320, 900]) {
    await page.setViewportSize({ width, height: 900 });
    await page.goto('http://127.0.0.1:5174/');
    await page.evaluate(() => {
      sessionStorage.setItem('sp_access_token', 'visual-only-token');
      sessionStorage.setItem('sp_access_expires_at', String(Date.now() + 3600000));
      localStorage.setItem('sp_theme', 'light');
    });
    await page.reload();
    await page.locator('.find-parking-result').first().dispatchEvent('click');
    await page.getByRole('button', { name: 'Book Now' }).click();
    await page.locator('.driver-booking-flow').waitFor();
    await page.screenshot({ path: `C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/current/booking-after-${width}.png`, fullPage: true });
    results.push(await page.evaluate(() => {
      const rect = (selector) => document.querySelector(selector)?.getBoundingClientRect().toJSON();
      return { width: innerWidth, overflow: document.documentElement.scrollWidth - innerWidth,
        flow: rect('.driver-booking-flow'), stage: rect('.booking-flow-stage-grid'),
        model: rect('.booking-model-panel'), summary: rect('.booking-summary-panel'),
        content: rect('.dashboard-content'), header: rect('.booking-flow-header'), steps: rect('.booking-flow-steps'),
        modelBadge: rect('.booking-model-panel .pointer-events-none'), modelControls: rect('.booking-model-panel .top-3.right-3'),
      };
    }));
    await page.getByRole('button', { name: 'Grid View' }).click();
    const availableLabel = await page.locator('button[aria-label^="Slot "]').evaluateAll((buttons) =>
      buttons.find((button) => button.style.opacity === '1')?.getAttribute('aria-label'));
    if (availableLabel) {
      await page.getByRole('button', { name: availableLabel, exact: true }).click();
      await page.getByRole('button', { name: /Continue with Slot/ }).click();
      await page.screenshot({ path: `C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/current/booking-details-after-${width}.png`, fullPage: true });
      results.push(await page.evaluate(() => ({ width: innerWidth, step: 'details', overflow: document.documentElement.scrollWidth - innerWidth })));
    }
  }
  return results;
}
