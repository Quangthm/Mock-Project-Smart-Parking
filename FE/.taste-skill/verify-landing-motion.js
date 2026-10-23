async (page) => {
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await page.evaluate(() => { sessionStorage.removeItem('sp_access_token'); sessionStorage.removeItem('sp_access_expires_at'); });
  await page.reload();
  await page.locator('.hero-carousel-image').waitFor({ timeout: 15000 });
  const image = page.locator('.hero-carousel-image');
  const start = await image.getAttribute('src');
  await page.waitForTimeout(5700);
  const afterAuto = await image.getAttribute('src');
  await page.locator('.hero-carousel').hover();
  const beforePause = await image.getAttribute('src');
  await page.waitForTimeout(5800);
  const afterPause = await image.getAttribute('src');
  await page.getByRole('button', { name: 'Pause slideshow' }).click();
  await page.mouse.move(0, 0);
  await page.locator('.hero-carousel').evaluate((node) => { const active = document.activeElement; if (active && node.contains(active)) active.blur(); });
  const beforeManualPause = await image.getAttribute('src');
  await page.waitForTimeout(5800);
  const afterManualPause = await image.getAttribute('src');
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.reload();
  await page.locator('.hero-carousel-image').waitFor({ timeout: 15000 });
  const reducedAnimation = await page.locator('.hero-carousel-image').evaluate((node) => getComputedStyle(node).animationName);
  const reducedStart = await page.locator('.hero-carousel-image').getAttribute('src');
  await page.waitForTimeout(5800);
  const reducedEnd = await page.locator('.hero-carousel-image').getAttribute('src');
  return { autoAdvances: start !== afterAuto, pausesOnHover: beforePause === afterPause, manualPauseWorks: beforeManualPause === afterManualPause, reducedAnimation, reducedAutoStops: reducedStart === reducedEnd };
}
