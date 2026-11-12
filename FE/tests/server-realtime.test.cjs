const assert = require("node:assert/strict")
const { readFileSync } = require("node:fs")
const { join } = require("node:path")
const { test } = require("node:test")
const vm = require("node:vm")
const ts = require("typescript")

function load(file, imports, globals) {
  const source = readFileSync(join(__dirname, "../src/lib", file), "utf8")
    .replace("import.meta.env.VITE_API_BASE_URL", "undefined")
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  })
  const exports = {}
  vm.runInNewContext(outputText, { exports, require: name => imports[name], ...globals })
  return exports
}

test("foreground reconciliation is throttled, skips hidden/offline tabs and ignores late results after disposal", async () => {
  const window = new EventTarget()
  const document = Object.assign(new EventTarget(), { visibilityState: "visible" })
  const navigator = { onLine: true }
  let clock = 0
  let tick
  window.setInterval = run => { tick = run; return 1 }
  window.clearInterval = () => {}
  const { watchServerRefresh } = load("serverRefresh.ts", { react: {}, "./authApi": {} },
    { window, document, navigator, Date: { now: () => clock } })
  let calls = 0
  let finish
  let current
  const stop = watchServerRefresh(isCurrent => {
    calls++
    current = isCurrent
    return new Promise(resolve => { finish = resolve })
  }, 30_000)
  clock = 30_000
  document.visibilityState = "hidden"
  tick()
  document.visibilityState = "visible"
  navigator.onLine = false
  tick()
  assert.equal(calls, 0)
  navigator.onLine = true
  window.dispatchEvent(new Event("online"))
  window.dispatchEvent(new Event("focus"))
  clock += 30_000
  tick()
  assert.equal(calls, 1)
  assert.equal(current(), true)
  stop()
  assert.equal(current(), false)
  finish()
  await Promise.resolve()
  tick()
  assert.equal(calls, 1)
})

test("Owner hub shares one connection, batches event bursts and reconciles after reconnect", async () => {
  const window = new EventTarget()
  const document = Object.assign(new EventTarget(), { visibilityState: "visible" })
  const timers = new Map()
  const flush = async () => { await new Promise(resolve => setImmediate(resolve)) }
  let builds = 0
  let starts = 0
  let sequence = 0
  let changed
  let closed
  const connection = {
    state: "Disconnected",
    on(event, callback) { assert.equal(event, "OperatorsChanged"); changed = callback },
    off(event, callback) { assert.equal(callback, changed); changed = undefined },
    onclose(callback) { closed = callback },
    async start() { starts++; this.state = "Connected" },
    async stop() { this.state = "Disconnected"; closed() },
  }
  class Builder {
    withUrl(url) { assert.match(url, /\/hubs\/operators$/); return this }
    configureLogging() { return this }
    build() { builds++; return connection }
  }
  const { watchOperators } = load("operatorLive.ts", {
    "@microsoft/signalr": { HubConnectionBuilder: Builder, LogLevel: { None: 0 } },
    "./authApi": { authApi: { expiresAt: () => Date.now() + 600_000 },
      authExpiredEvent: "expired", authRefreshedEvent: "refreshed", ensureAccessToken: async () => "stub" },
  }, {
    window, document, Date,
    setTimeout(callback) { timers.set(++sequence, callback); return sequence },
    clearTimeout(id) { timers.delete(id) },
  })
  let a = 0
  let b = 0
  const stopA = watchOperators(() => a++)
  const stopB = watchOperators(() => b++)
  await flush()
  assert.equal(builds, 1)
  assert.equal(starts, 1)
  assert.deepEqual([a, b], [1, 1])
  for (let index = 0; index < 20; index++) changed()
  assert.equal(timers.size, 1)
  const runTimer = () => {
    const [id, callback] = timers.entries().next().value
    timers.delete(id)
    callback()
  }
  runTimer()
  assert.deepEqual([a, b], [2, 2])
  stopA()
  connection.state = "Disconnected"
  closed()
  runTimer()
  await flush()
  assert.equal(starts, 2)
  assert.deepEqual([a, b], [2, 3])
  const lateEvent = changed
  stopB()
  lateEvent()
  assert.equal(timers.size, 0)
  assert.equal(changed, undefined)
})
