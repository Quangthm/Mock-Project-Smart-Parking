async (page) => {
  const base = 'C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/current'
  const reports = []
  let role = 'driver'
  await page.route('**/api/auth/me', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, data: { userId: `visual-${role}`, fullName: `Visual ${role}`, email: `${role}@example.com`, role, onboardingComplete: true } }) }))
  await page.route('**/api/owner-applications', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, data: [{ id: 'visual-app', ownerId: 'owner-1', ownerName: 'Jamie Lee', businessName: 'Central Parking', email: 'owner@example.com', phone: '0900000000', lotType: 'outdoor', status: 'pending', submittedAt: new Date().toISOString(), contactVerified: true, lotSetupComplete: true, lotSetup: { name: 'Central Parking', address: '17 Le Duan Street', lotType: 'outdoor', totalSlots: 80, floors: 1, devices: [], modelFileName: 'lot.glb', hourlyRate: 20000, dailyRate: 90000, nightRate: 80000, gracePeriodMinutes: 10, subscriptionMonthly: 1000000, subscriptionYearly: 10000000, policyAccepted: true } }] }) }))
  for (const width of [375, 1440]) {
    await page.setViewportSize({ width, height: 900 })
    await page.goto('http://127.0.0.1:5176/')
    await page.evaluate(() => { sessionStorage.removeItem('sp_access_token'); localStorage.setItem('sp_theme', 'light') })
    await page.reload()
    await page.getByRole('button', { name: 'Sign In', exact: true }).first().click()
    await page.screenshot({ path: `${base}/requested-sign-in-${width}.png` })
    reports.push({ screen: 'sign-in', width, overflow: await page.evaluate(() => document.documentElement.scrollWidth - innerWidth), otp: await page.getByRole('button', { name: 'Sign in with email OTP' }).count(), google: await page.getByRole('button', { name: /Sign in with Google/ }).count() })
  }
  for (role of ['driver', 'operator', 'admin']) {
    for (const width of [375, 1440]) {
      await page.setViewportSize({ width, height: 900 })
      await page.goto('http://127.0.0.1:5176/')
      await page.evaluate(() => { sessionStorage.setItem('sp_access_token', 'visual-only-token'); sessionStorage.setItem('sp_access_expires_at', String(Date.now() + 3600000)); localStorage.setItem('sp_theme', 'light') })
      await page.reload()
      await page.waitForTimeout(600)
      if (role === 'driver') await page.evaluate(() => window.dispatchEvent(new CustomEvent('sp:dashboard-nav', { detail: 'bookings' })))
      if (role === 'admin') await page.evaluate(() => window.dispatchEvent(new CustomEvent('sp:dashboard-nav', { detail: 'applications' })))
      await page.waitForTimeout(450)
      await page.screenshot({ path: `${base}/requested-${role}-${width}.png` })
      reports.push({ screen: role, width, overflow: await page.evaluate(() => document.documentElement.scrollWidth - innerWidth), reviewForm: await page.locator('.application-review-form').count(), bookings: await page.locator('.current-bookings').count(), kpis: await page.locator('.operator-kpi').count() })
    }
  }
  await page.unroute('**/api/auth/me')
  await page.unroute('**/api/owner-applications')
  return reports
}
