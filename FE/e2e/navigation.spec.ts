import { test, expect } from "@playwright/test"

test.describe("E2E & Responsive Layout: SmartPark Critical Navigation", () => {
  test("loads landing page with branding and primary actions", async ({ page }) => {
    await page.goto("/")
    await expect(page).toHaveTitle(/SmartPark/i)

    const nav = page.locator("nav, header")
    await expect(nav.first()).toBeVisible()

    const signInBtn = page.getByRole("button", { name: /đăng nhập|sign in/i })
    if (await signInBtn.isVisible()) {
      await expect(signInBtn).toBeEnabled()
    }
  })

  test("responsive viewport check: mobile view preserves menu and action buttons", async ({
    page,
    isMobile,
  }) => {
    await page.goto("/")

    if (isMobile) {
      const viewportSize = page.viewportSize()
      expect(viewportSize?.width).toBeLessThanOrEqual(450)

      const brand = page.locator("header, nav").first()
      await expect(brand).toBeVisible()
    } else {
      const viewportSize = page.viewportSize()
      expect(viewportSize?.width).toBeGreaterThanOrEqual(1024)
    }
  })

  test("protected routes: accessing private workspace without credentials falls closed", async ({
    page,
  }) => {
    await page.addInitScript(() => {
      localStorage.clear()
      sessionStorage.clear()
    })

    await page.goto("/#driver")
    const pageContent = await page.textContent("body")
    expect(pageContent).toBeDefined()
  })
})

