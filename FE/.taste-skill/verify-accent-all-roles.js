async (page) => {
  const results = []
  for (const role of ["driver", "owner", "operator", "admin"]) {
    await page.unroute("**/api/auth/me")
    await page.route("**/api/auth/me", async (route) => route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ success: true, data: {
        userId: `visual-${role}`, fullName: `Visual ${role}`,
        email: "visual@example.com", role, onboardingComplete: true,
      } }),
    }))
    await page.goto("http://127.0.0.1:5176/")
    await page.evaluate(() => {
      sessionStorage.setItem("sp_access_token", "visual-only-token")
      sessionStorage.setItem("sp_access_expires_at", String(Date.now() + 3600000))
    })
    for (const theme of ["light", "dark"]) {
      for (const accent of [role === "driver" ? "pink" : role === "owner" ? "sky" : role === "operator" ? "green" : "red"]) {
        await page.evaluate(({ theme, accent }) => {
          localStorage.setItem("sp_theme", theme)
          localStorage.setItem("sp_accent", accent)
        }, { theme, accent })
        await page.reload()
        await page.waitForTimeout(50)
        const state = await page.evaluate(() => {
          const root = document.querySelector(".dashboard-theme")
          const logo = root?.querySelector(".brand-logo")
          const css = root ? getComputedStyle(root) : null
          return {
            role: root ? "dashboard" : "public",
            accent: root?.getAttribute("data-accent"),
            logo: logo ? getComputedStyle(logo).color : null,
            primary: css?.getPropertyValue("--primary").trim(),
            overflow: document.documentElement.scrollWidth - innerWidth,
          }
        })
        results.push({ role, theme, accent, ...state })
      }
    }
  }
  await page.unroute("**/api/auth/me")
  return results
}
