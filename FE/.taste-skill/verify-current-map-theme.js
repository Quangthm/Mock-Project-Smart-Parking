async (page) => {
  const stylePaths = []
  page.on("request", (request) => {
    try {
      const url = new URL(request.url())
      if (url.hostname === "api.maptiler.com" && url.pathname.endsWith("/style.json")) stylePaths.push(url.pathname)
    } catch { /* Ignore non-URL requests. */ }
  })
  await page.route("**/api/auth/me", async (route) => route.fulfill({
    status: 200, contentType: "application/json",
    body: JSON.stringify({ success: true, data: {
      userId: "visual-driver", fullName: "Visual driver",
      email: "visual@example.com", role: "driver", onboardingComplete: true,
    } }),
  }))
  await page.goto("http://127.0.0.1:5176/")
  await page.evaluate(() => {
    sessionStorage.setItem("sp_access_token", "visual-only-token")
    sessionStorage.setItem("sp_access_expires_at", String(Date.now() + 3600000))
    localStorage.setItem("sp_theme", "dark")
  })
  await page.reload()
  await page.waitForTimeout(2500)
  const dark = {
    darkClass: await page.evaluate(() => document.documentElement.classList.contains("dark")),
    mapType: await page.locator('select[aria-label="Map type"]').inputValue().catch(() => null),
    stylePaths: [...stylePaths],
    status: await page.locator(".find-parking-map-status").innerText().catch(() => "ready"),
  }
  stylePaths.length = 0
  await page.evaluate(() => localStorage.setItem("sp_theme", "light"))
  await page.reload()
  await page.waitForTimeout(2500)
  const light = {
    darkClass: await page.evaluate(() => document.documentElement.classList.contains("dark")),
    mapType: await page.locator('select[aria-label="Map type"]').inputValue().catch(() => null),
    stylePaths: [...stylePaths],
    status: await page.locator(".find-parking-map-status").innerText().catch(() => "ready"),
  }
  await page.unroute("**/api/auth/me")
  return { dark, light }
}
