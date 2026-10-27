async (page) => {
  const warnings = [];
  page.on('console', (message) => { if (message.type() === 'warning' || message.type() === 'error') warnings.push(message.text()); });
  await page.route('**/api/auth/me', (route) => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, data: { userId: 'visual-driver', fullName: 'Visual driver', email: 'visual@example.com', role: 'driver', onboardingComplete: true } }) }));
  await page.goto('http://127.0.0.1:5176/');
  await page.evaluate(() => { sessionStorage.setItem('sp_access_token', 'visual-only-token'); sessionStorage.setItem('sp_access_expires_at', String(Date.now() + 3600000)); });
  await page.reload();
  await page.locator('.parking-map-label').first().waitFor({ timeout: 15000 });
  const initialWarnings = warnings.filter((warning) => warning.includes('Invalid style') || warning.includes('deprecated'));
  warnings.length = 0;
  const styleSelect = page.getByRole('combobox', { name: 'Map type' });
  await styleSelect.selectOption('satellite');
  await page.waitForTimeout(700);
  await styleSelect.selectOption('hybrid');
  await page.waitForTimeout(700);
  await styleSelect.selectOption('streets');
  await page.waitForTimeout(700);
  return { selected: await styleSelect.inputValue(), initialWarnings, changeWarnings: warnings.filter((warning) => warning.includes('Invalid style') || warning.includes('deprecated')), errors: warnings.filter((warning) => warning.includes('Error')) };
}
