async (page) => {
  await page.route('**/api/auth/me', async route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, data: { userId: 'visual-driver', fullName: 'Visual driver', email: 'driver@example.com', role: 'driver', onboardingComplete: true } }) }))
  await page.setViewportSize({ width: 1440, height: 900 })
  await page.goto('http://127.0.0.1:5176/')
  await page.evaluate(() => { sessionStorage.setItem('sp_access_token', 'visual-only-token'); sessionStorage.setItem('sp_access_expires_at', String(Date.now() + 3600000)) })
  await page.reload()
  await page.locator('.parking-map-marker').first().waitFor()
  const before = await page.locator('.parking-map-marker').evaluateAll(elements => elements.map(el => ({ transform: el.style.transform, classes: el.className })))
  await page.getByRole('button', { name: 'Map controls' }).click()
  await page.getByRole('button', { name: 'Zoom in' }).click()
  await page.waitForTimeout(800)
  const after = await page.locator('.parking-map-marker').evaluateAll(elements => elements.map(el => ({ transform: el.style.transform, classes: el.className })))
  await page.screenshot({ path: 'C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/current/map-after-zoom-1440.png' })
  await page.unroute('**/api/auth/me')
  return { count: before.length, movedWithGeography: before.map((item, index) => item.transform !== after[index]?.transform), anchoredClassesUnchanged: before.map((item, index) => item.classes === after[index]?.classes) }
}
