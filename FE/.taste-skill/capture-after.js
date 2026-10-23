async (page) => {
  const out = 'C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/after';
  const screens = [
    ['landing', null],
    ['pricing', 'Pricing'],
    ['support', 'Support'],
    ['sign-in', 'Sign In'],
    ['sign-up', 'Sign Up'],
  ];
  const findings = [];
  for (const theme of ['light', 'dark']) {
    for (const width of [375, 768, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      for (const [screen, nav] of screens) {
        await page.goto('http://127.0.0.1:5174/');
        await page.evaluate((value) => localStorage.setItem('sp_theme', value), theme);
        await page.reload();
        if (nav) await page.getByRole('button', { name: nav, exact: true }).first().click();
        await page.waitForTimeout(250);
        const info = await page.evaluate(() => ({
          width: window.innerWidth,
          scrollWidth: document.documentElement.scrollWidth,
          dark: document.documentElement.classList.contains('dark'),
          font: getComputedStyle(document.body).fontFamily,
          buttonsUnder44: Array.from(document.querySelectorAll('button')).filter((el) => {
            const rect = el.getBoundingClientRect();
            return rect.width > 0 && rect.height > 0 && (rect.width < 44 || rect.height < 44);
          }).length,
        }));
        await page.screenshot({ path: `${out}/${screen}-${theme}-${width}.png`, fullPage: true });
        findings.push({ screen, theme, ...info });
      }
    }
  }
  return findings;
}
