const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { test } = require('node:test');

const collection = JSON.parse(fs.readFileSync(path.join(__dirname,
  '../../docs/04-testing/postman/SPARK-340-Admin-Platform-Policies.postman_collection.json'), 'utf8'));
const requests = collection.item.flatMap(folder => folder.item);

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
