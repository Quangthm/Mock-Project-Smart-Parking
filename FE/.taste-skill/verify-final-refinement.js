async (page) => {
  const base = 'C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/current'
  const report = []
  for (const width of [375, 768, 1440]) {
    for (const theme of ['light', 'dark']) {
      await page.setViewportSize({ width, height: 900 })
      await page.goto('http://127.0.0.1:5176/')
      await page.evaluate(theme => localStorage.setItem('sp_theme', theme), theme)
      await page.reload()
      await page.waitForTimeout(450)
      await page.screenshot({ path: `${base}/refined-landing-${theme}-${width}.png` })
      const heading = await page.locator('.strata-hero-copy h1').innerText()
      report.push({ screen: 'landing', width, theme, heading, overflow: await page.evaluate(() => document.documentElement.scrollWidth - innerWidth) })
      await page.getByRole('button', { name: 'Sign Up' }).click()
      await page.waitForTimeout(100)
      await page.screenshot({ path: `${base}/refined-sign-up-${theme}-${width}.png` })
      report.push({ screen: 'sign-up', width, theme, overflow: await page.evaluate(() => document.documentElement.scrollWidth - innerWidth) })
    }
  }
  await page.route('**/api/auth/me', async route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, data: { userId: 'visual-driver', fullName: 'Visual driver', email: 'driver@example.com', role: 'driver', onboardingComplete: true } }) }))
  for (const width of [375, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 })
    await page.goto('http://127.0.0.1:5176/')
    await page.evaluate(() => { sessionStorage.setItem('sp_access_token', 'visual-only-token'); sessionStorage.setItem('sp_access_expires_at', String(Date.now() + 3600000)); localStorage.setItem('sp_theme', 'light') })
    await page.reload()
    await page.waitForTimeout(1900)
    await page.screenshot({ path: `${base}/refined-map-${width}.png` })
    const controls = await page.evaluate(() => {
      const search = document.querySelector('.find-parking-reopen')?.getBoundingClientRect()
      const style = document.querySelector('.find-parking-map-style')?.getBoundingClientRect()
      const frame = document.querySelector('.find-parking-map')?.getBoundingClientRect()
      const markers = [...document.querySelectorAll('.parking-map-marker')].map(el => {
        const b = el.getBoundingClientRect()
        return { x: b.x, y: b.y, width: b.width, height: b.height, inside: !!frame && b.left >= frame.left && b.right <= frame.right && b.top >= frame.top && b.bottom <= frame.bottom }
      })
      return { search: search && { width: search.width, height: search.height }, style: style && { width: style.width, height: style.height }, markers }
    })
    report.push({ screen: 'map', width, overflow: await page.evaluate(() => document.documentElement.scrollWidth - innerWidth), controls })
  }
  await page.unroute('**/api/auth/me')
  return report
}
