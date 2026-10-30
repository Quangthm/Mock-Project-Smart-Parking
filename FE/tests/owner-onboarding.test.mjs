import assert from "node:assert/strict"
import { fileURLToPath } from "node:url"
import React from "react"
import { renderToStaticMarkup } from "react-dom/server"
import { createServer } from "vite"

const memory = () => {
  const data = new Map()
  return { getItem: (key) => data.get(key) ?? null, setItem: (key, value) => data.set(key, String(value)), removeItem: (key) => data.delete(key), clear: () => data.clear() }
}
globalThis.sessionStorage = memory()
globalThis.localStorage = memory()
const originalFetch = globalThis.fetch
const server = await createServer({ root: fileURLToPath(new URL("..", import.meta.url)), server: { middlewareMode: true }, appType: "custom" })
let passed = 0
function check(name, action) {
  sessionStorage.clear()
  localStorage.clear()
  action()
  console.log(`PASS ${name}`)
  passed++
}
try {
  const { restoreOwnerRegistration, ownerRegistrationStage, ensureOwnerRegistrationStorage, pendingOwnerKey } = await server.ssrLoadModule("/src/lib/ownerRegistration.ts")
  const { ownerApi } = await server.ssrLoadModule("/src/lib/ownerApi.ts")
  const { BusinessLotSetupPage } = await server.ssrLoadModule("/src/roles/owner/parking-lots/CreateParkingLotForOwner.tsx")
  const { SignUp } = await server.ssrLoadModule("/src/roles/authentications/Sign Up/SignUp.tsx")
  const base = { id: "test-registration", email: "owner@example.com", lotType: "outdoor", lotSetupComplete: false }
  const valid = { ...base, setupToken: "test-credential" }
  const challenge = { challengeId: "test-challenge", expiresAt: new Date(Date.now() + 300000).toISOString(), resendAt: new Date().toISOString() }
  check("valid setup remains accessible", () => {
    sessionStorage.setItem(pendingOwnerKey, JSON.stringify(valid))
    assert.equal(ownerRegistrationStage(restoreOwnerRegistration()), "setup")
    assert.match(renderToStaticMarkup(React.createElement(BusinessLotSetupPage)), /Set Up Your Parking Lot/)
    assert.match(renderToStaticMarkup(React.createElement(SignUp)), /Continue to Parking Lot Setup/)
  })
  for (const storage of [sessionStorage, localStorage]) check("legacy record opens recovery instead of wizard", () => {
    storage.setItem(pendingOwnerKey, JSON.stringify({ ...base, verification: challenge }))
    assert.equal(ownerRegistrationStage(restoreOwnerRegistration()), "recovery")
    const markup = renderToStaticMarkup(React.createElement(BusinessLotSetupPage))
    assert.match(markup, /Check registration status/)
    assert.doesNotMatch(markup, /Set Up Your Parking Lot|server has not enabled/)
    assert.match(renderToStaticMarkup(React.createElement(SignUp)), /Check registration status/)
  })
  check("corrupt session record does not hide legacy local record", () => {
    sessionStorage.setItem(pendingOwnerKey, "{")
    localStorage.setItem(pendingOwnerKey, JSON.stringify(base))
    assert.equal(restoreOwnerRegistration().email, base.email)
  })
  check("empty token and invalid lot type require recovery", () => {
    assert.equal(ownerRegistrationStage({ ...valid, setupToken: " " }), "recovery")
    assert.equal(ownerRegistrationStage({ ...valid, lotType: "unknown" }), "recovery")
  })
  check("saved lot requires a valid challenge", () => {
    assert.equal(ownerRegistrationStage({ ...base, lotSetupComplete: true, verification: challenge }), "verification")
    assert.equal(ownerRegistrationStage({ ...base, lotSetupComplete: true }), "recovery")
  })
  check("setup recovery challenge survives reload and displays its own OTP form", () => {
    sessionStorage.setItem(pendingOwnerKey, JSON.stringify({ email: base.email, lotSetupComplete: false, setupRecovery: challenge }))
    assert.equal(ownerRegistrationStage(restoreOwnerRegistration()), "recovery")
    const markup = renderToStaticMarkup(React.createElement(SignUp))
    assert.match(markup, /Verify email to resume setup/)
    assert.match(markup, /Verify and continue setup/)
    assert.doesNotMatch(markup, /Continue to Parking Lot Setup/)
  })
  check("blocked session storage is detected before registration", () => {
    const storage = sessionStorage
    try {
      globalThis.sessionStorage = { setItem() { throw new Error("blocked") } }
      assert.throws(ensureOwnerRegistrationStorage, /Browser storage is unavailable/)
    } finally { globalThis.sessionStorage = storage }
    ensureOwnerRegistrationStorage()
    assert.equal(sessionStorage.getItem(`${pendingOwnerKey}_probe`), null)
  })
  for (const owner of [base, valid, { ...base, lotSetupComplete: true, verification: challenge }]) check("pending Driver OTP remains reachable alongside Owner registration", () => {
    sessionStorage.setItem(pendingOwnerKey, JSON.stringify(owner))
    sessionStorage.setItem("sp_pending_driver_registration", JSON.stringify({ registrationId: "driver-id", contact: "driver@example.com", channel: "email", expiresAt: challenge.expiresAt, resendAvailableAt: challenge.resendAt }))
    const markup = renderToStaticMarkup(React.createElement(SignUp))
    assert.match(markup, /driver@example.com/)
    assert.doesNotMatch(markup, /Continue to Parking Lot Setup|Check registration status/)
  })
  sessionStorage.clear()
  let calls = 0
  globalThis.fetch = async () => { calls++; return Response.json({ success: true, data: { ...base } }, { status: 201 }) }
  await assert.rejects(ownerApi.register({ email: base.email }), /did not return a valid setup credential/)
  assert.equal(calls, 1)
  await assert.rejects(ownerApi.submitLotSetup(base.id, "", {}), /setup credential is missing/)
  assert.equal(calls, 1)
  console.log("PASS incompatible backend response rejected and missing token never sent")
  passed++
  globalThis.fetch = async () => Response.json({ success: true, data: { ...base, verification: challenge } })
  await assert.rejects(ownerApi.recover(base.email), /did not return a saved parking lot/)
  globalThis.fetch = async () => Response.json({ success: true, data: { ...base, lotSetupComplete: true, verification: challenge } })
  assert.equal(ownerRegistrationStage(await ownerApi.recover(base.email)), "verification")
  console.log("PASS recovery accepts only saved setups with verification")
  passed++
  const paths = []
  globalThis.fetch = async (url) => {
    paths.push(new URL(url).pathname)
    return String(url).endsWith("/setup-recovery/request")
      ? Response.json({ success: true, data: challenge })
      : Response.json({ success: false, code: "SETUP_RECOVERY_REQUIRED", message: "Email proof required" }, { status: 409 })
  }
  const pendingRecovery = await ownerApi.recover(base.email)
  assert.equal(pendingRecovery.setupRecovery.challengeId, challenge.challengeId)
  assert.equal(pendingRecovery.setupToken, undefined)
  assert.equal(paths.length, 2)
  globalThis.fetch = async () => Response.json({ success: true, data: valid })
  assert.equal(ownerRegistrationStage(await ownerApi.completeSetupRecovery(base.email, challenge.challengeId, "123456")), "setup")
  console.log("PASS incomplete registration requests email proof before returning setup access")
  passed++
  const { resumePendingSignIn } = await server.ssrLoadModule("/src/roles/authentications/pendingRegistrationSignIn.ts")
  const { AuthApiError } = await server.ssrLoadModule("/src/lib/authApi.ts")
  const pendingError = new AuthApiError("Incomplete registration", 403, "ACCOUNT_PENDING_VERIFICATION")
  const approvalError = new AuthApiError("Awaiting approval", 403, "ACCOUNT_PENDING_APPROVAL")
  const response = (data) => Response.json({ success: true, data })
  const missingDriver = () => Response.json({ success: false, code: "REGISTRATION_NOT_FOUND" }, { status: 404 })
  sessionStorage.clear(); localStorage.clear()
  let resumeCalls = []
  globalThis.fetch = async (url) => {
    resumeCalls.push(String(url))
    return String(url).endsWith("/driver/recover") ? missingDriver()
      : String(url).endsWith("/setup-recovery/request") ? response(challenge)
      : Response.json({ success: false, code: "SETUP_RECOVERY_REQUIRED" }, { status: 409 })
  }
  assert.equal(await resumePendingSignIn(new AuthApiError("Wrong password", 401, "AUTH_FAILED"), base.email), null)
  assert.equal(resumeCalls.length, 0)
  assert.equal(await resumePendingSignIn(pendingError, base.email), "sign-up")
  assert.ok(JSON.parse(sessionStorage.getItem(pendingOwnerKey)).setupRecovery)
  resumeCalls = []
  assert.equal(await resumePendingSignIn(pendingError, base.email), "sign-up")
  assert.equal(resumeCalls.length, 1, "Restored recovery challenge must not send a duplicate recovery email")
  console.log("PASS ordinary sign-in resumes missing-token setup and preserves existing recovery challenge")
  passed++
  sessionStorage.setItem(pendingOwnerKey, JSON.stringify(valid))
  resumeCalls = []
  assert.equal(await resumePendingSignIn(pendingError, base.email), "parking-lot-setup")
  assert.equal(resumeCalls.length, 1, "Existing setup credential should avoid recovery OTP")
  console.log("PASS sign-in with existing setup token continues directly to wizard")
  passed++
  sessionStorage.clear()
  globalThis.fetch = async (url) => String(url).endsWith("/driver/recover") ? missingDriver()
    : response({ ...base, lotSetupComplete: true, emailVerified: false, verification: challenge })
  assert.equal(await resumePendingSignIn(pendingError, base.email), "sign-up")
  assert.equal(ownerRegistrationStage(JSON.parse(sessionStorage.getItem(pendingOwnerKey))), "verification")
  globalThis.fetch = async () => response({ ...base, lotSetupComplete: true, emailVerified: true, verification: challenge })
  assert.equal(await resumePendingSignIn(approvalError, base.email), "pending-approval")
  assert.equal(sessionStorage.getItem(pendingOwnerKey), null)
  console.log("PASS sign-in routes saved setups to email verification or pending approval")
  passed++
  globalThis.fetch = async () => response({ registrationId: "driver-fixture", channel: "email", expiresAt: challenge.expiresAt, resendAvailableAt: challenge.resendAt })
  assert.equal(await resumePendingSignIn(pendingError, "driver@example.com"), "sign-up")
  assert.equal(JSON.parse(sessionStorage.getItem("sp_pending_driver_registration")).contact, "driver@example.com")
  console.log("PASS pending Driver sign-in still resumes its own verification")
  passed++
  sessionStorage.clear()
  globalThis.fetch = async () => missingDriver()
  assert.equal(await resumePendingSignIn(approvalError, "operator@example.com"), null)
  assert.equal(await resumePendingSignIn(pendingError, "operator@example.com"), null)
  console.log("PASS other pending roles retain their original sign-in status")
  passed++
  console.log(`${passed} onboarding regression checks passed`)
} finally {
  globalThis.fetch = originalFetch
  await server.close()
}
