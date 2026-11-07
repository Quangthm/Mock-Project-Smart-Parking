const assert = require('node:assert/strict');
const path = require('node:path');

// Ensure local connections bypass any proxy
delete process.env.http_proxy;
delete process.env.https_proxy;
delete process.env.all_proxy;
process.env.no_proxy = '*';

const signalR = require(path.join(__dirname, '../FE/node_modules/@microsoft/signalr'));

const USER_API = 'http://localhost:5035';
const OPS_API = 'http://localhost:5055';
const SEEDED_SITE = 'b0000000-0000-4000-8000-000000000001';
const UNASSIGNED_SITE = 'b0000000-0000-4000-8000-000000000099';

async function login(email, password) {
  const res = await fetch(`${USER_API}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password })
  });
  const data = await res.json();
  if (!data.success) throw new Error(`Login failed for ${email}: ${JSON.stringify(data)}`);
  return data.data.accessToken;
}

function buildConnection(siteId, token) {
  const builder = new signalR.HubConnectionBuilder()
    .withUrl(`${OPS_API}/ws/parking-lots/${siteId}/layout`, {
      accessTokenFactory: token ? () => token : undefined,
      skipNegotiation: false,
      transport: signalR.HttpTransportType.WebSockets
    })
    .configureLogging(signalR.LogLevel.None);
  return builder.build();
}

// Wait for a condition or until timeout
async function waitFor(predicate, timeoutMs = 5000) {
  const start = Date.now();
  while (!predicate() && Date.now() - start < timeoutMs) {
    await new Promise(r => setTimeout(r, 50));
  }
  return predicate();
}

async function runTests() {
  console.log('=================================================================');
  console.log(' Starting SPARK-244 SignalR Realtime & Edge Case Test Suite');
  console.log('=================================================================\n');

  console.log('[1/8] Logging in test accounts...');
  const operatorToken = await login('operator@smartpark.local', 'Aa1!d4r0V2e3');
  const driverToken = await login('driver@gmail.com', 'Password@123');
  console.log('  Operator and Driver tokens acquired successfully.\n');

  // Test 1: Connect without token -> Server aborts in OnConnectedAsync (WebSocket closed with 1011)
  console.log('[2/8] Testing Edge Case 401: Connect without token');
  {
    const conn = buildConnection(SEEDED_SITE, null);
    let closedWithError = null;
    conn.onclose(err => { closedWithError = err; });

    try {
      await conn.start();
    } catch (err) {
      closedWithError = err;
    }
    // Give time for OnConnectedAsync exception to close the WebSocket
    await waitFor(() => closedWithError !== null, 2000);
    assert.ok(closedWithError, 'Connection without token must be closed with error');
    assert.equal(conn.state, signalR.HubConnectionState.Disconnected, 'Connection must be Disconnected');
    console.log(`  PASS: Connection without token rejected: ${closedWithError.message || closedWithError}\n`);
  }

  // Test 2: Connect with invalid/expired token -> Server aborts in OnConnectedAsync
  console.log('[3/8] Testing Edge Case 401: Connect with invalid / expired token');
  {
    const conn = buildConnection(SEEDED_SITE, 'invalid.expired.jwt.token');
    let closedWithError = null;
    conn.onclose(err => { closedWithError = err; });

    try {
      await conn.start();
    } catch (err) {
      closedWithError = err;
    }
    await waitFor(() => closedWithError !== null, 2000);
    assert.ok(closedWithError, 'Connection with invalid token must be closed with error');
    assert.equal(conn.state, signalR.HubConnectionState.Disconnected, 'Connection must be Disconnected');
    console.log(`  PASS: Connection with invalid token rejected: ${closedWithError.message || closedWithError}\n`);
  }

  // Test 3: Connect with driver token (non-operator role) -> Server rejects with 403 OPERATOR_LOT_UNASSIGNED
  console.log('[4/8] Testing Edge Case 403: Driver connecting to operator hub');
  {
    const conn = buildConnection(SEEDED_SITE, driverToken);
    let closedWithError = null;
    conn.onclose(err => { closedWithError = err; });

    try {
      await conn.start();
    } catch (err) {
      closedWithError = err;
    }
    await waitFor(() => closedWithError !== null, 2000);
    assert.ok(closedWithError, 'Driver connection must be closed with error');
    assert.equal(conn.state, signalR.HubConnectionState.Disconnected, 'Connection must be Disconnected');
    console.log(`  PASS: Driver connection rejected: ${closedWithError.message || closedWithError}\n`);
  }

  // Test 4: Connect with operator token to unassigned site -> 403 OPERATOR_LOT_UNASSIGNED
  console.log('[5/8] Testing Edge Case 403: Operator connecting to unassigned site');
  {
    const conn = buildConnection(UNASSIGNED_SITE, operatorToken);
    let closedWithError = null;
    conn.onclose(err => { closedWithError = err; });

    try {
      await conn.start();
    } catch (err) {
      closedWithError = err;
    }
    await waitFor(() => closedWithError !== null, 2000);
    assert.ok(closedWithError, 'Unassigned site connection must be closed with error');
    assert.equal(conn.state, signalR.HubConnectionState.Disconnected, 'Connection must be Disconnected');
    console.log(`  PASS: Unassigned site connection rejected: ${closedWithError.message || closedWithError}\n`);
  }

  // Test 5: Connect assigned operator -> Success & SnapshotRequired event received (AC-1, AC-8)
  console.log('[6/8] Testing AC-1 & AC-8: Assigned operator connects & receives SnapshotRequired');
  let activeConn;
  let receivedSnapshotSiteId = null;
  {
    activeConn = buildConnection(SEEDED_SITE, operatorToken);
    activeConn.on('SnapshotRequired', (siteId) => {
      receivedSnapshotSiteId = siteId;
    });

    await activeConn.start();
    assert.equal(activeConn.state, signalR.HubConnectionState.Connected, 'Connection should be Connected');

    const received = await waitFor(() => receivedSnapshotSiteId !== null, 5000);
    assert.ok(received, 'SnapshotRequired must be received');
    assert.equal(receivedSnapshotSiteId, SEEDED_SITE, `SnapshotRequired siteId must match ${SEEDED_SITE}`);
    console.log(`  PASS: Connected successfully and received SnapshotRequired for site: ${receivedSnapshotSiteId}\n`);
  }

  // Test 6: Trigger walk-in check-in mutation -> Receive broadcast OccupancyChanged
  console.log('[7/8] Testing Realtime Delivery: Mutation triggers broadcast to hub');
  {
    let receivedEvent = null;
    activeConn.on('OccupancyChanged', (envelope) => {
      receivedEvent = envelope;
    });

    const plateNumber = `WS-TEST-${Date.now().toString().slice(-6)}`;
    const checkInRes = await fetch(`${OPS_API}/api/parking-sessions/walk-in/check-in?siteId=${SEEDED_SITE}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${operatorToken}`
      },
      body: JSON.stringify({
        plateNumber: plateNumber,
        vehicleType: 'CAR'
      })
    });
    const checkInData = await checkInRes.json();
    assert.equal(checkInData.success, true, 'Check-in must succeed');
    const sessionId = checkInData.data.id;

    // Outbox worker polls periodically and publishes to broadcast
    const received = await waitFor(() => receivedEvent !== null, 5000);
    assert.ok(received, 'Must receive OccupancyChanged broadcast envelope');
    assert.ok(receivedEvent.eventId, 'Envelope must have eventId');
    assert.ok(typeof receivedEvent.sequenceNumber === 'number', 'Envelope must have sequenceNumber');
    assert.equal(receivedEvent.siteId, SEEDED_SITE, 'Envelope siteId must match');
    assert.equal(receivedEvent.eventType, 'OccupancyChanged', 'eventType must match');
    console.log(`  PASS: Broadcast received: eventId=${receivedEvent.eventId}, seq=${receivedEvent.sequenceNumber}, type=${receivedEvent.eventType}`);

    // Cleanup session
    const checkOutRes = await fetch(`${OPS_API}/api/parking-sessions/${sessionId}/walk-in/check-out?siteId=${SEEDED_SITE}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${operatorToken}`
      },
      body: JSON.stringify({ plateNumber })
    });
    const checkOutData = await checkOutRes.json();
    assert.equal(checkOutData.success, true, 'Check-out cleanup must succeed');
    console.log('  Cleanup: Session checked out successfully.\n');
  }

  // Test 7: Disconnect & Reconnect -> receives SnapshotRequired again (AC-8)
  console.log('[8/8] Testing AC-8: Reconnect triggers fresh SnapshotRequired');
  {
    await activeConn.stop();
    assert.equal(activeConn.state, signalR.HubConnectionState.Disconnected, 'Should be disconnected');

    let reconnectedSnapshotSiteId = null;
    activeConn.on('SnapshotRequired', (siteId) => {
      reconnectedSnapshotSiteId = siteId;
    });

    await activeConn.start();
    assert.equal(activeConn.state, signalR.HubConnectionState.Connected, 'Should be reconnected');

    const received = await waitFor(() => reconnectedSnapshotSiteId !== null, 5000);
    assert.ok(received, 'Must receive SnapshotRequired on reconnect');
    assert.equal(reconnectedSnapshotSiteId, SEEDED_SITE, 'Must receive SnapshotRequired on reconnect');
    console.log(`  PASS: Reconnect successful, received SnapshotRequired for site: ${reconnectedSnapshotSiteId}\n`);

    await activeConn.stop();
  }

  // Test 8: AC-9 - Replay duplicate event & sequence watermark idempotency
  console.log('[9/9] Testing AC-9: RealtimeIdempotencyGuard sequence watermark & duplicate event filtering');
  {
    // Load RealtimeIdempotencyGuard using TypeScript compiler from FE
    const ts = require(path.join(__dirname, '../FE/node_modules/typescript'));
    const fs = require('node:fs');
    const source = fs.readFileSync(path.join(__dirname, '../FE/src/lib/realtimeIdempotency.ts'), 'utf8');
    const js = ts.transpileModule(source, {
      compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 }
    }).outputText;
    const mod = { exports: {} };
    const vm = require('node:vm');
    vm.runInNewContext(js, { exports: mod.exports, module: mod, Set, Map });
    const { RealtimeIdempotencyGuard } = mod.exports;

    const guard = new RealtimeIdempotencyGuard();
    const slotId = 'slot-alpha-001';

    // 1. Initial event seq 10
    const ev1 = { eventId: 'evt-001', sequenceNumber: 10, siteId: SEEDED_SITE };
    let res = guard.decide(ev1, slotId);
    assert.equal(res.apply, true);
    assert.equal(res.reason, 'new');
    guard.commit(ev1, slotId);
    assert.equal(guard.processedCount, 1);

    // 2. Duplicate eventId
    res = guard.decide(ev1, slotId);
    assert.equal(res.apply, false);
    assert.equal(res.reason, 'duplicate_event');

    // 3. Stale sequence number (seq 8 < 10)
    const evStale = { eventId: 'evt-002', sequenceNumber: 8, siteId: SEEDED_SITE };
    res = guard.decide(evStale, slotId);
    assert.equal(res.apply, false);
    assert.equal(res.reason, 'stale_sequence');

    // 4. Duplicate sequence number (seq 10 == 10)
    const evDupSeq = { eventId: 'evt-003', sequenceNumber: 10, siteId: SEEDED_SITE };
    res = guard.decide(evDupSeq, slotId);
    assert.equal(res.apply, false);
    assert.equal(res.reason, 'duplicate_sequence');

    // 5. Newer sequence number (seq 15 > 10)
    const evNew = { eventId: 'evt-004', sequenceNumber: 15, siteId: SEEDED_SITE };
    res = guard.decide(evNew, slotId);
    assert.equal(res.apply, true);
    assert.equal(res.reason, 'new');
    guard.commit(evNew, slotId);
    assert.equal(guard.processedCount, 2);

    // 6. Reset on reconnect
    guard.reset();
    assert.equal(guard.processedCount, 0);
    // After reset, ev1 can be applied as new again
    res = guard.decide(ev1, slotId);
    assert.equal(res.apply, true);
    assert.equal(res.reason, 'new');

    console.log('  PASS: RealtimeIdempotencyGuard drops duplicate eventId, stale seq, duplicate seq; advances watermark on newer seq.\n');
  }

  console.log('=================================================================');
  console.log(' ALL 9 REALTIME & SIGNALR EDGE CASE TESTS PASSED 100%!');
  console.log('=================================================================');
}

runTests().catch(err => {
  console.error('\nTEST SUITE FAILED:', err);
  process.exit(1);
});
