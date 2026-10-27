async (page) => {
  await page.route('**/api/auth/me', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, data: { userId: 'visual-driver', fullName: 'Visual driver', email: 'driver@example.com', role: 'driver', onboardingComplete: true } }) }))
  const booking = { id: 'BOOK-2026-0084', driverId: 'visual-driver', lotId: 'lot-1', lotName: 'Central Parking', slotId: 'slot-1', slotNumber: 'A-12', licensePlate: '51A-12345', startTime: new Date().toISOString(), endTime: new Date(Date.now() + 2 * 3600000).toISOString(), status: 'checked-in', amount: 40000 }
  await page.setViewportSize({ width: 375, height: 900 })
  await page.goto('http://127.0.0.1:5176/')
  await page.evaluate(value => { sessionStorage.setItem('sp_access_token', 'visual-only-token'); sessionStorage.setItem('sp_access_expires_at', String(Date.now() + 3600000)); localStorage.setItem('sp_theme', 'light'); localStorage.setItem('sp_bookings', JSON.stringify([value])) }, booking)
  await page.reload()
  await page.evaluate(() => window.dispatchEvent(new CustomEvent('sp:dashboard-nav', { detail: 'bookings' })))
  await page.locator('.current-booking-card').waitFor({ timeout: 10000 })
  await page.screenshot({ path: 'C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/current/requested-booking-card-375.png' })
  return { overflow: await page.evaluate(() => document.documentElement.scrollWidth - innerWidth), tabs: await page.locator('.current-bookings-tabs button').evaluateAll(elements => elements.map(element => { const rect = element.getBoundingClientRect(); return { x: rect.x, y: rect.y, width: rect.width } })) }
}
