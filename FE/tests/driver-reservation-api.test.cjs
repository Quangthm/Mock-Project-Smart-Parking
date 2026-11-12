const assert = require("node:assert/strict")
const fs = require("node:fs")
const path = require("node:path")
const test = require("node:test")
const vm = require("node:vm")
const ts = require("typescript")

const source = fs.readFileSync(path.join(__dirname, "../src/lib/driverReservationApi.ts"), "utf8")
  .replace("import.meta.env.VITE_RESERVATION_API_BASE_URL", '"https://operations.example/"')
const code = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText

test("Reservation uses the reviewed terms and reuses the idempotency key on retry", async () => {
  const calls = []
  const context = { exports: {}, require: () => ({
    AuthApiError: class extends Error {},
    request: async (...args) => {
      calls.push(args)
      return { success: true, data: { requestId: "request-1", state: "PENDING_AUTHORIZATION" } }
    },
  }) }
  vm.runInNewContext(code, context)
  const api = context.exports.driverReservationApi
  const intent = { vehicleId: "vehicle", siteId: "site", requestedSlotId: "slot", start: "2026-11-12T00:00:00Z",
    end: "2026-11-12T01:00:00Z", paymentMethod: "PAYOS", mode: "SPECIFIC_SLOT", termsId: "terms", termsVersion: "v1" }
  const first = await api.create(intent, "same-key")
  await api.create(intent, "same-key")
  await api.receipt(first.requestId)
  await api.read("reservation-1")
  assert.equal(calls[0][0], "/reservation-requests")
  assert.equal(calls[0][1], "POST")
  assert.equal(calls[0][2], intent)
  assert.equal(calls[0][3], "/api")
  assert.equal(calls[0][4], "https://operations.example")
  assert.equal(calls[0][5]["Idempotency-Key"], "same-key")
  assert.equal(calls[1][5]["Idempotency-Key"], "same-key")
  assert.equal(calls[2][0], "/reservation-requests/request-1")
  assert.equal(calls[3][0], "/me/reservations/reservation-1")
})
