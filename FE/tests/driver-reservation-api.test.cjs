const assert = require("node:assert/strict")
const fs = require("node:fs")
const path = require("node:path")
const test = require("node:test")
const vm = require("node:vm")
const ts = require("typescript")

const source = fs.readFileSync(path.join(__dirname, "../src/lib/driverReservationApi.ts"), "utf8")
  .replace("import.meta.env.VITE_RESERVATION_API_BASE_URL", '"https://operations.example/"')
const code = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText

test("Driver reservation API preserves preview and owning reads after retiring terms submission", async () => {
  const calls = []
  const context = { exports: {}, require: () => ({
    AuthApiError: class extends Error {},
    getSessionGeneration: () => 1,
    request: async (...args) => {
      calls.push(args)
      return { success: true, data: { bookedAmount: 20_000, currency: "VND" } }
    },
  }) }
  vm.runInNewContext(code, context)
  const api = context.exports.driverReservationApi
  const selection = { vehicleId: "vehicle", siteId: "site", requestedSlotId: "slot",
    start: "2026-11-12T00:00:00Z", end: "2026-11-12T01:00:00Z" }
  const preview = await api.preview(selection)
  await api.receipt("request-1")
  await api.read("reservation-1")
  await api.list()
  assert.deepEqual(preview, { bookedAmount: 20_000, currency: "VND" })
  assert.deepEqual(calls[0], ["/reservation-price-previews", "POST", selection, "/api", "https://operations.example"])
  assert.deepEqual(calls.slice(1).map(call => call.slice(0, 2)), [
    ["/reservation-requests/request-1", "GET"], ["/me/reservations/reservation-1", "GET"], ["/me/reservations", "GET"],
  ])
  assert.equal(api.review, undefined)
  assert.equal(api.create, undefined)
})
