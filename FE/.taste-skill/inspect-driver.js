async (page) => {
  await page.route('**/api/auth/me', async (route) => route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({success:true,data:{userId:'visual-driver',fullName:'Visual driver',email:'visual@example.com',role:'driver',onboardingComplete:true}})}));
  await page.setViewportSize({width:375,height:900});
  await page.goto('http://127.0.0.1:5174/');
  await page.evaluate(() => {sessionStorage.setItem('sp_access_token','visual-only-token');sessionStorage.setItem('sp_access_expires_at',String(Date.now()+3600000));localStorage.setItem('sp_theme','light');});
  await page.reload();
  await page.waitForTimeout(500);
  await page.screenshot({path:'C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/new-ui/driver-light-375.png'});
  await page.getByRole('button',{name:/parking lots/}).click();
  await page.screenshot({path:'C:/FPT/Mock-Project-Smart-Parking/FE/.taste-skill/new-ui/driver-results-375.png'});
  return await page.evaluate(() => ['.find-parking','.find-parking-panel','.find-parking-search','.find-parking-results','.find-parking-map','.find-parking-map-style','.find-parking-map-status','.find-parking-map-canvas'].map(s => {const e=document.querySelector(s);return {s,rect:e?.getBoundingClientRect().toJSON(),position:e&&getComputedStyle(e).position,background:e&&getComputedStyle(e).backgroundColor}}));
}
