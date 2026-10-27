async (page) => {
  const out = 'C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/current';
  const results = [];
  for (const width of [375, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    await page.goto('http://127.0.0.1:5175/');
    await page.waitForTimeout(1200);
    await page.screenshot({ path: `${out}/prototype-map-${width}.png` });
    const toggle = page.getByRole('button', { name: 'Mở rộng thanh điều hướng' });
    await toggle.click();
    await page.screenshot({ path: `${out}/prototype-menu-${width}.png` });
    results.push({ width, title: await page.title() });
  }
  return results;
}
