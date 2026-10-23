async (page) => {
  const out = 'C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/new-ui';
  const checks = [];
  for (const role of ['driver', 'owner', 'operator', 'admin']) {
    await page.unroute('**/api/auth/me');
    await page.route('**/api/auth/me', async (route) => route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({success:true,data:{userId:`visual-${role}`,fullName:`Visual ${role}`,email:'visual@example.com',role,onboardingComplete:true}})}));
    await page.setViewportSize({width:1440,height:900});
    await page.goto('http://127.0.0.1:5174/');
    await page.evaluate(() => {sessionStorage.setItem('sp_access_token','visual-only-token');sessionStorage.setItem('sp_access_expires_at',String(Date.now()+3600000));localStorage.setItem('sp_theme','light');});
    await page.reload();
    await page.getByRole('button',{name:'Open navigation'}).click();
    await page.waitForTimeout(250);
    await page.screenshot({path:`${out}/${role}-menu-1440.png`});
    const open = await page.evaluate(() => ({dialog:!!document.querySelector('#dashboard-navigation[role="dialog"]'),contentX:document.querySelector('.dashboard-content')?.getBoundingClientRect().x,focused:document.activeElement?.getAttribute('aria-label')}));
    await page.keyboard.press('Escape');
    const closed = await page.evaluate(() => document.querySelector('.menu-toggle')?.getAttribute('aria-expanded'));
    checks.push({role,open,closed});
    if (role === 'driver') {
      await page.getByRole('button',{name:/parking lots/}).click();
      await page.waitForTimeout(250);
      await page.screenshot({path:`${out}/driver-results-1440.png`});
      await page.locator('.find-parking-result').first().click();
      checks.push({detailVisible:await page.locator('.find-parking-details-panel').isVisible()});
      const book = page.locator('.find-parking-details-panel').getByRole('button',{name:/book/i}).first();
      if (await book.isEnabled()) {
        await book.click();
        checks.push({bookingFlowVisible:await page.locator('.driver-booking-flow').isVisible()});
      }
    }
  }
  await page.unroute('**/api/auth/me');
  return checks;
}
