async (page) => {
  const base = 'C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/current'
  await page.setViewportSize({ width: 1440, height: 900 })
  await page.goto('http://127.0.0.1:5176/')
  await page.evaluate(() => { sessionStorage.clear(); localStorage.setItem('sp_theme', 'light') })
  await page.reload()
  await page.waitForTimeout(800)
  await page.screenshot({ path: `${base}/smartpark-building-1440.png` })
  await page.waitForTimeout(3500)
  await page.screenshot({ path: `${base}/smartpark-split-1440.png` })
  await page.waitForTimeout(5000)
  await page.screenshot({ path: `${base}/smartpark-outdoor-1440.png` })
  await page.waitForTimeout(9300)
  await page.screenshot({ path: `${base}/smartpark-return-1440.png` })
  const brand = await page.locator('.brand-wordmark-svg').first().evaluate(element => ({ width: element.getBoundingClientRect().width, height: element.getBoundingClientRect().height }))
  await page.setViewportSize({ width: 375, height: 900 })
  await page.screenshot({ path: `${base}/smartpark-landing-375.png` })
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - innerWidth)
  return { brand, overflow }
}
