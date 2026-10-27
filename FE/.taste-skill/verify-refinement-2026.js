async (page) => {
  const base = 'C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/current'
  const reports = []
  let role = 'driver'
  await page.route('**/api/auth/me', async route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ success: true, data: { userId: `visual-${role}`, fullName: `Visual ${role}`, email: `${role}@example.com`, role, onboardingComplete: true } })
  }))
  for (role of ['driver', 'owner', 'operator', 'admin']) {
    for (const width of [375, 768, 1440]) {
      await page.setViewportSize({ width, height: 900 })
      await page.goto('http://127.0.0.1:5176/')
      await page.evaluate(() => {
        sessionStorage.setItem('sp_access_token', 'visual-only-token')
        sessionStorage.setItem('sp_access_expires_at', String(Date.now() + 3600000))
        localStorage.setItem('sp_theme', 'light')
      })
      await page.reload()
      await page.waitForTimeout(1100)
      await page.screenshot({ path: `${base}/refined-${role}-${width}.png` })
      reports.push({ role, width, overflow: await page.evaluate(() => document.documentElement.scrollWidth - innerWidth), title: await page.title(), mapLabels: await page.locator('.parking-map-label').count() })
    }
  }
  await page.unroute('**/api/auth/me')
  return reports
}
