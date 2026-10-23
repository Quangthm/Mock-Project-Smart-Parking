async (page) => {
  await page.route('**/api/auth/me', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, data: { userId: 'visual-operator', fullName: 'Visual operator', email: 'operator@example.com', role: 'operator', onboardingComplete: true } }) }))
  await page.route('**/api/operators/me', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, data: [{ siteId: 'visual-lot', tenantId: 'tenant-1', permissions: [] }] }) }))
  const slots = ['AVAILABLE', 'OCCUPIED', 'RESERVED', 'MAINTENANCE', 'BACKUP'].map((kind, index) => ({ id: `slot-${index}`, unitId: 'floor-1', code: `A${index + 1}`, vehicleType: 'CAR', type: 'STANDARD', operationalStatus: kind === 'MAINTENANCE' ? 'MAINTENANCE' : 'OPERATIONAL', isPhysicallyOccupied: kind === 'OCCUPIED', reservationState: kind === 'BACKUP' ? 'BACKUP' : kind === 'RESERVED' ? 'RESERVED' : null }))
  await page.route('**/api/parking-lots/visual-lot/operator-layout', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, data: { site: { id: 'visual-lot', tenantId: 'tenant-1', code: 'LOT-1', name: 'Central Parking', address: '17 Le Duan Street', latitude: null, longitude: null, status: 'ACTIVE', isActive: true, totalPhysicalCapacity: 5 }, units: [{ id: 'floor-1', parentId: null, name: 'Ground', type: 'FLOOR', maxCapacity: 5 }], slots, paths: [], capacityViews: [{ unitId: null, vehicleType: 'CAR', total: 5, occupied: 1, protected: 0, pendingPayment: 0, configuredBackup: 1, markedBackup: 0, effectiveBackup: 1, unavailable: 1, available: 1, inheritedBackup: false }] } }) }))
  await page.setViewportSize({ width: 1440, height: 900 })
  await page.goto('http://127.0.0.1:5176/')
  await page.evaluate(() => { sessionStorage.setItem('sp_access_token', 'visual-only-token'); sessionStorage.setItem('sp_access_expires_at', String(Date.now() + 3600000)); localStorage.setItem('sp_theme', 'light') })
  await page.reload()
  await page.locator('.operator-kpi').first().waitFor({ timeout: 12000 })
  await page.screenshot({ path: 'C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/current/requested-operator-kpis-1440.png' })
  const colors = await page.locator('.operator-kpi[data-status]').evaluateAll(elements => elements.map(element => ({ status: element.getAttribute('data-status'), color: getComputedStyle(element.firstElementChild).color, size: getComputedStyle(element.firstElementChild).fontSize })))
  return { kpis: await page.locator('.operator-kpi').count(), colors, overflow: await page.evaluate(() => document.documentElement.scrollWidth - innerWidth) }
}
