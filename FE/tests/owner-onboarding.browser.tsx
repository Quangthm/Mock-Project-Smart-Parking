import React from "react"
import { createRoot, type Root } from "react-dom/client"
import { MemoryRouter } from "react-router-dom"
import { AppProvider, useApp } from "../src/context/AppContext"
import { SignIn } from "../src/roles/authentications/Sign In/SignIn"
import { SignUp } from "../src/roles/authentications/Sign Up/SignUp"
import { BusinessLotSetupPage } from "../src/roles/owner/parking-lots/CreateParkingLotForOwner"

// Deliberately isolated dev-only harness: real components/events, deterministic API fixtures.
const host = document.getElementById("test-app")!
const results = document.getElementById("test-results")!
const runButton = document.getElementById("run-tests") as HTMLButtonElement
const ownerKey = "sp_pending_owner_registration"
const driverKey = "sp_pending_driver_registration"
const email = "browser-fixture@example.com"
const base = { id: "fixture-owner", email, lotType: "outdoor", lotSetupComplete: false }
const challenge = () => ({ challengeId: "fixture-recovery", expiresAt: new Date(Date.now() + 300000).toISOString(), resendAt: new Date(Date.now() - 1000).toISOString() })
let root: Root | undefined
let saved = false
let verified = false
let freshToken = false
const requests: string[] = []

function Screen() {
  const { view } = useApp()
  if (view === "parking-lot-setup") return <BusinessLotSetupPage />
  if (view === "sign-up") return <SignUp />
  if (view === "pending-approval") return <h2>Fixture pending Admin approval</h2>
  return <SignIn />
}
const wait = (ms = 15) => new Promise(resolve => setTimeout(resolve, ms))
function assert(value: unknown, message: string): asserts value { if (!value) throw new Error(message) }
async function until(test: () => unknown, message: string) {
  for (let i = 0; i < 200; i++) { if (test()) return; await wait() }
  throw new Error(message)
}
async function click(text: string) {
  const button = [...host.querySelectorAll("button")].find(button => button.textContent?.trim() === text)
  assert(button && !button.disabled, `Missing/enabled button: ${text}`)
  button.click()
  await wait()
}
async function fill(selector: string, value: string) {
  const input = host.querySelector<HTMLInputElement>(selector)
  assert(input, `Missing input ${selector}`)
  Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!.call(input, value)
  input.dispatchEvent(new Event("input", { bubbles: true }))
  await wait()
}
function has(text: string) { return host.textContent?.includes(text) }
async function mount(owner?: object, driver = false) {
  root?.unmount()
  host.innerHTML = ""
  sessionStorage.removeItem(ownerKey)
  sessionStorage.removeItem(driverKey)
  localStorage.removeItem(ownerKey)
  if (owner) sessionStorage.setItem(ownerKey, JSON.stringify(owner))
  if (driver) sessionStorage.setItem(driverKey, JSON.stringify({ registrationId: "fixture-driver", contact: "driver-fixture@example.com", channel: "email", expiresAt: challenge().expiresAt, resendAvailableAt: challenge().resendAt }))
  saved = false; verified = false; freshToken = false; requests.length = 0
  root = createRoot(host)
  root.render(<MemoryRouter><AppProvider><Screen /></AppProvider></MemoryRouter>)
  await until(() => host.querySelector("button"), "App did not mount")
}

runButton.onclick = async () => {
  runButton.disabled = true
  const originalFetch = window.fetch
  const keys = [ownerKey, driverKey, "sp_access_token", "sp_refresh_token", "sp_access_expires_at"]
  const backup = keys.map(key => ({ key, session: sessionStorage.getItem(key), local: localStorage.getItem(key) }))
  keys.forEach(key => sessionStorage.removeItem(key))
  results.textContent = "Running…\n"
  window.fetch = async (input, options) => {
    const path = new URL(String(input), location.origin).pathname
    requests.push(path)
    const body = JSON.parse(String(options?.body ?? "{}"))
    const ok = (data: unknown) => Response.json({ success: true, data })
    const fail = (code: string, status = 400) => Response.json({ success: false, code, message: code }, { status })
    if (path.endsWith("/api/auth/login") || path.endsWith("/api/auth/otp/request")) return fail("ACCOUNT_PENDING_VERIFICATION", 403)
    if (path.endsWith("/register/driver/recover")) return fail("REGISTRATION_NOT_FOUND", 404)
    if (path.endsWith("/setup-recovery/request")) return ok(challenge())
    if (path.endsWith("/setup-recovery/complete")) {
      if (body.code !== "123456") return fail("OTP_INVALID")
      freshToken = true
      return ok({ ...base, setupToken: "fresh-token" })
    }
    if (path.endsWith("/register/owner/recover")) return saved
      ? ok({ ...base, lotSetupComplete: true, verification: challenge(), emailVerified: verified })
      : fail("SETUP_RECOVERY_REQUIRED", 409)
    if (path.endsWith("/register/owner/lot-setup")) {
      if (!freshToken || body.setupToken !== "fresh-token") return fail("SETUP_TOKEN_INVALID", 401)
      saved = true
      return ok({ ...base, lotSetupComplete: true, verification: challenge() })
    }
    if (path.endsWith("/register/owner/verify")) {
      if (body.code !== "654321") return fail("OTP_INVALID")
      verified = true
      return ok({})
    }
    return fail(`UNEXPECTED_FIXTURE_REQUEST ${path}`, 500)
  }
  const pass = (name: string) => { results.textContent += `PASS ${name}\n` }
  try {
    await mount(base)
    assert(has("Resume your Business registration") && !has("Set Up Your Parking Lot"), "Missing token entered wizard")
    await click("Check registration status")
    await until(() => host.querySelector("#owner-recovery-code"), "Recovery OTP form missing")
    await fill("#owner-recovery-code", "000000")
    await click("Verify and continue setup")
    await until(() => has("OTP_INVALID"), "Wrong OTP error missing")
    await fill("#owner-recovery-code", "123456")
    await click("Verify and continue setup")
    await until(() => has("Set Up Your Parking Lot"), "Recovery did not enter setup")
    assert(JSON.parse(sessionStorage.getItem(ownerKey)!).setupToken === "fresh-token", "New token not persisted")
    pass("legacy missing token -> recovery OTP -> wrong OTP -> valid OTP -> setup")

    await mount({ ...base, setupToken: "expired-token" })
    await click("Continue to Parking Lot Setup")
    await click("Continue")
    await fill('input[placeholder="e.g. Vincom Center Parking"]', "Preserved fixture lot")
    await fill('input[placeholder="Full address"]', "Preserved fixture address")
    await fill('input[placeholder="e.g. 50"]', "445")
    await click("Continue")
    await click("Continue")
    const policy = host.querySelector<HTMLInputElement>('input[type="checkbox"]')!
    assert(policy, "Policy checkbox missing"); policy.click(); await wait()
    await click("Continue to Review")
    await click("Save Lot and Continue to Email Verification")
    await until(() => has("Resume your Business registration"), "Expired token did not open recovery")
    await click("Check registration status")
    await until(() => host.querySelector("#owner-recovery-code"), "Recovery OTP missing")
    await fill("#owner-recovery-code", "123456")
    await click("Verify and continue setup")
    await until(() => has("Review & Confirm"), "Review step not preserved")
    assert(has("Preserved fixture lot") && has("Preserved fixture address") && has("445"), "Entered lot data lost")
    await click("Save Lot and Continue to Email Verification")
    await until(() => host.querySelector("#owner-code"), "Saved lot did not open email verification")
    assert(!has("Fixture pending Admin approval"), "Email OTP skipped prematurely")
    await fill("#owner-code", "654321")
    const verifyForm = host.querySelector<HTMLFormElement>("#owner-code")!.closest("form")!
    verifyForm.requestSubmit()
    await until(() => has("Fixture pending Admin approval"), "Verified owner did not reach pending approval")
    assert(verified && !sessionStorage.getItem(ownerKey), "Verification did not complete/clear state")
    pass("expired token -> OTP recovery -> form preserved -> save -> email verification -> pending approval")

    await mount({ ...base, lotSetupComplete: true, verification: challenge() }, true)
    assert(has("driver-fixture@example.com"), "Owner state hid Driver OTP")
    await click("Register with a different contact")
    await click("Business")
    await until(() => host.querySelector("#owner-code"), "Owner verification unreachable after abandoning Driver OTP")
    pass("mixed Driver/Owner registrations preserve tab transitions")

    await mount()
    await fill("#sign-in-email", email)
    await fill("#sign-in-password", "FixturePass@123")
    await click("Sign-In")
    await until(() => host.querySelector("#owner-recovery-code"), "Password login did not resume pending registration")
    pass("normal password login automatically resumes incomplete Business registration")
    await mount()
    await fill("#sign-in-email", email)
    await click("Sign in with email / SMS code")
    await until(() => host.querySelector("#owner-recovery-code"), "Email/SMS login did not resume pending registration")
    pass("existing email/SMS sign-in automatically resumes incomplete registration without password")
    results.textContent += "ALL 5 BROWSER SCENARIOS PASSED"
  } catch (error) {
    results.textContent += `FAIL ${error instanceof Error ? error.message : String(error)}`
  } finally {
    window.fetch = originalFetch
    root?.unmount(); root = undefined
    for (const item of backup) {
      if (item.session === null) sessionStorage.removeItem(item.key); else sessionStorage.setItem(item.key, item.session)
      if (item.local === null) localStorage.removeItem(item.key); else localStorage.setItem(item.key, item.local)
    }
    runButton.disabled = false
  }
}
