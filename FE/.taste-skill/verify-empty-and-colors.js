async (page) => {
  await page.route('**/api/auth/me', (route) => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, data: { userId: 'visual-driver', fullName: 'Visual driver', email: 'visual@example.com', role: 'driver', onboardingComplete: true } }) }));
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('http://127.0.0.1:5176/');
  await page.evaluate(() => {
    sessionStorage.setItem('sp_access_token', 'visual-only-token');
    sessionStorage.setItem('sp_access_expires_at', String(Date.now() + 3600000));
    localStorage.setItem('sp_theme', 'light');
    localStorage.setItem('sp_bookings', '[]');
    const lots = JSON.parse(localStorage.getItem('sp_lots') || '[]');
    if (lots[0]?.slots?.length) lots[0].slots = lots[0].slots.map((slot) => ({ ...slot, status:'occupied' }));
    if (lots[1]?.slots?.length) lots[1].slots = lots[1].slots.map((slot, index) => ({ ...slot, status:index === 0 ? 'available' : 'occupied' }));
    localStorage.setItem('sp_lots', JSON.stringify(lots));
  });
  await page.reload();
  await page.locator('.parking-map-label').first().waitFor({ timeout: 15000 });
  const levels = await page.locator('.parking-map-label').evaluateAll((elements) => elements.map((element) => ({ className:element.className, count:element.querySelector('.parking-map-label-count')?.textContent })));
  await page.screenshot({ path:'C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/new-ui-234/map-status-colors-light-1440.png' });
  await page.getByRole('button', { name:'Current booking', exact:true }).click();
  await page.waitForTimeout(400);
  const parkingEmpty = await page.getByText('No parking session in progress').isVisible();
  await page.getByRole('button', { name:/Upcoming/ }).click();
  const upcomingEmpty = await page.getByText('No upcoming bookings').isVisible();
  return { levels, parkingEmpty, upcomingEmpty };
}
