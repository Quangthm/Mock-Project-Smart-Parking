async (page) => {
  await page.unroute('**/api/auth/me');
  await page.route('**/api/auth/me', async (route) => route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({success:true,data:{userId:'visual-driver',fullName:'Visual driver',email:'visual@example.com',role:'driver',onboardingComplete:true}})}));
  const results = [];
  for (const width of [375,1440]) {
    await page.setViewportSize({width,height:900});
    await page.goto('http://127.0.0.1:5174/');
    await page.evaluate(() => {sessionStorage.setItem('sp_access_token','visual-only-token');sessionStorage.setItem('sp_access_expires_at',String(Date.now()+3600000));localStorage.setItem('sp_theme','light');});
    await page.reload();
    await page.getByRole('button',{name:'Open navigation'}).click();
    await page.waitForTimeout(250);
    await page.screenshot({path:`C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/new-ui/driver-menu-${width}.png`});
    const menu = await page.evaluate(() => ({rect:document.querySelector('#dashboard-navigation')?.getBoundingClientRect().toJSON(),overflow:document.documentElement.scrollWidth-innerWidth}));
    await page.keyboard.press('Escape');
    await page.getByRole('button',{name:'Open profile menu'}).click();
    const profileVisible = await page.locator('.profile-popover').isVisible();
    await page.getByRole('button',{name:'Close menu'}).click();
    await page.getByRole('button',{name:'Search pages'}).click();
    const searchVisible = await page.locator('.dashboard-search-popover').isVisible();
    await page.getByRole('button',{name:'Close menu'}).click();
    results.push({width,menu,profileVisible,searchVisible});
  }
  await page.unroute('**/api/auth/me');
  return results;
}
