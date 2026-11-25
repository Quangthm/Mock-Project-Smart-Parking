const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const crypto = require('node:crypto');
const { test } = require('node:test');

const collection = JSON.parse(fs.readFileSync(path.join(__dirname,
  '../../docs/04-testing/postman/SPARK-340-Admin-Platform-Policies.postman_collection.json'), 'utf8'));
const requests = collection.item.flatMap(folder => folder.item);

test('P1-R1 keeps all 48 non-Payment requests unchanged and adds exactly two Admin lock negatives', () => {
  assert.equal(collection.item.length, 6);
  assert.equal(requests.length, 50);
  assert.equal(collection.item[0].item.length, 8);
  const retained = requests.filter(item => !item.request.url.raw.endsWith('/admin/integrations/payment'));
  assert.equal(retained.length, 48);
  // Hash of the 48 original request objects at combined baseline f8df2c89.
  assert.equal(crypto.createHash('sha256').update(JSON.stringify(retained)).digest('hex'),
    '30e5467c9a984a7c813ba4ada9873942c8054d83e9553680445416ffffc25d18');
  assert.equal(requests.filter(item => item.request.url.raw.endsWith('/admin/integrations/notifications')).length, 5);
  assert.match(collection.info.description, /DEFERRED/);
});

test('Payment GET and PUT require Admin and empty 404; success, leaked bodies and JSON Content-Type fail', () => {
  const locks = requests.filter(item => item.request.url.raw.endsWith('/admin/integrations/payment'));
  assert.deepEqual(locks.map(item => item.request.method), ['GET', 'PUT']);
  for (const item of locks) {
    assert.equal(item.request.header.find(header => header.key === 'Authorization').value, 'Bearer {{admin_token}}');
    const script = item.event.find(event => event.listen === 'test').script.exec.join('\n');
    function run(status, body, contentType) {
      const outcomes = [];
      const pm = {
        response: { to: { have: { status: expected => assert.equal(status, expected) } },
          text: () => body, headers: { get: () => contentType } },
        expect: value => ({ to: { eql: expected => assert.equal(value, expected) } }),
        test: (_name, fn) => { try { fn(); outcomes.push(true); } catch { outcomes.push(false); } },
      };
      vm.runInNewContext(`(function () {\n${script}\n})();`, { pm }, { timeout: 1000 });
      return outcomes;
    }
    assert.deepEqual(run(404, '', undefined), [true]);
    assert.deepEqual(run(200, '', undefined), [false]);
    assert.deepEqual(run(404, '{"success":true}', undefined), [false]);
    assert.deepEqual(run(404, '', 'application/json'), [false]);
  }
  // Deliberately invalid update payload cannot mutate settings if the lock regresses.
  assert.deepEqual(JSON.parse(locks[1].request.body.raw), {});
});

test('terms 503 remains a failed assertion without a second data dereference failure', () => {
  const request = requests.find(item => item.name === 'Accept reservation terms - capture accepted snapshot');
  assert.ok(request);
  const script = request.event.find(event => event.listen === 'test').script.exec.join('\n');
  const outcomes = [];
  const pm = {
    environment: {
      get: key => key === 'snapshot_fixture_ready' ? 'true' : undefined,
      set: () => assert.fail('A failed review must not capture accepted terms')
    },
    response: {
      code: 503,
      json: () => ({ success: false, code: 'OWNER_UNAVAILABLE' }),
      to: { have: { status: expected => assert.equal(503, expected) } }
    },
    test: (name, body) => {
      try { body(); outcomes.push({ name, passed: true }); }
      catch (error) { outcomes.push({ name, passed: false, message: error.message }); }
    }
  };
  vm.runInNewContext(`(function () {\n${script}\n})();`, { pm }, { timeout: 1000 });
  assert.equal(outcomes.length, 1);
  assert.equal(outcomes[0].name, 'terms review succeeds');
  assert.equal(outcomes[0].passed, false);
});

test('all collection event scripts compile in the Postman function scope', () => {
  let scripts = 0;
  for (const item of [collection, ...collection.item, ...requests]) {
    for (const event of item.event || []) {
      new vm.Script(`(function () {\n${event.script.exec.join('\n')}\n})();`);
      scripts++;
    }
  }
  assert.ok(scripts > 0);
});

test('all four F8 cases require 400, canonical code and the site-only field error', () => {
  const cases = requests.filter(item => item.name.startsWith('F8 -'));
  assert.equal(cases.length, 4);
  assert.deepEqual(cases.map(item => [item.request.method, /"arrivalLateToleranceEnabled": true/.test(item.request.body.raw)]),
    [['POST', true], ['POST', false], ['PUT', true], ['PUT', false]]);
  for (const item of cases) {
    function run(status, body) {
      const outcomes = [];
      const pm = {
        response: { to: { have: { status: expected => assert.equal(status, expected) } }, json: () => body },
        expect: value => ({ to: { eql: expected => assert.deepEqual(value, expected), match: pattern => assert.match(value, pattern) } }),
        test: (name, fn) => { try { fn(); outcomes.push(true); } catch { outcomes.push(false); } }
      };
      vm.runInNewContext(`(function () {\n${item.event[0].script.exec.join('\n')}\n})();`, { pm }, { timeout: 1000 });
      return outcomes;
    }
    const expected = { code: 'VALIDATION_FAILED', errors: [{ property: 'ArrivalLateToleranceEnabled' }] };
    assert.deepEqual(run(400, expected), [true]);
    assert.deepEqual(run(200, { success: true }), [false]);
    assert.deepEqual(run(400, { code: 'VALIDATION_FAILED', errors: [{ property: 'OtherField' }] }), [false]);
  }
});
