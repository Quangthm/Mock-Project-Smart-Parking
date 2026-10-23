async (page) => {
  const base = 'C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/current'
  await page.emulateMedia({ reducedMotion: 'no-preference' })
  await page.setViewportSize({ width: 1440, height: 900 })
  await page.goto('http://127.0.0.1:5176/')
  await page.waitForSelector('.parking-hero-3d-canvas canvas')
  await page.screenshot({ path: `${base}/hero-building-1440.png` })
  await page.waitForTimeout(4500)
  await page.screenshot({ path: `${base}/hero-separating-1440.png` })
  await page.waitForTimeout(4500)
  await page.screenshot({ path: `${base}/hero-outdoor-1440.png` })
  await page.emulateMedia({ reducedMotion: 'reduce' })
  await page.reload()
  await page.waitForSelector('.parking-hero-3d-canvas canvas')
  const canvas = await page.locator('.parking-hero-3d-canvas canvas').count()
  await page.screenshot({ path: `${base}/hero-reduced-motion-1440.png` })
  return { canvas, reducedMotion: await page.evaluate(() => matchMedia('(prefers-reduced-motion: reduce)').matches), overflow: await page.evaluate(() => document.documentElement.scrollWidth - innerWidth) }
}
