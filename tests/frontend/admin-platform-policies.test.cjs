const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');
const ts = require('../../FE/node_modules/typescript');

/**
 * Same harness as auth-api.test.cjs / operator-provisioning.test.cjs: transpile the real TS
 * module to CommonJS and execute it in a fresh vm context with every import injected by name.
 *
 * The two admin modules each emit `const authApi_1 = require("./authApi")`, so they cannot run
 * in a single shared context (duplicate lexical declaration). We therefore load the real
 * `authApi.ts` in its own context and inject its exports into each admin module's context.
 */
function load(file, imports = {}, globals = {}) {
  const source = fs.readFileSync(path.join(__dirname, '../../FE/src/lib', file), 'utf8')
    .replace(/import\.meta\.env\.\w+/g, 'undefined');
  const javascript = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  }).outputText;
  const context = {
    exports: {},
    URLSearchParams,
    ...globals,
    require(name) {
      if (!(name in imports)) throw new Error(`Unexpected import: ${name}`);
      return imports[name];
    },
  };
  vm.runInNewContext(javascript, context);
  return context.exports;
}

const response = (status, payload) => ({
  status,
  ok: status >= 200 && status < 300,
  json: async () => payload,
});

function fixture(respond, storageSeed) {
  const storage = new Map(storageSeed ?? [
    ['sp_access_token', 'access-a'],
    ['sp_refresh_token', 'refresh-a'],
    ['sp_access_expires_at', '9999999999999'],
  ]);
  const calls = [], events = [];
  const authApi = load('authApi.ts', { './store': { store: { findUserById: () => undefined } } }, {
    AbortSignal, Event, FormData,
    sessionStorage: {
      getItem: key => storage.get(key) ?? null,
      removeItem: key => storage.delete(key),
      setItem: (key, value) => storage.set(key, value),
    },
    window: { dispatchEvent: event => events.push(event.type) },
    fetch: async (url, options) => {
      calls.push({ url, options });
      return respond(url, options, storage);
    },
  });
  const policyApi = load('adminPolicyApi.ts', { './authApi': authApi });
  const integrationApi = load('adminIntegrationApi.ts', { './authApi': authApi });
  const validation = load('adminPolicyValidation.ts');
  return { authApi, policyApi, integrationApi, validation, calls, events, storage };
}

const POLICY_KEYS = [
  'PAYMENT_HOLD_DURATION',
  'RESERVATION_MIN_DURATION',
  'RESERVATION_MAX_DURATION',
  'MAX_ADVANCE_BOOKING',
  'INTER_BOOKING_BUFFER',
  'RESERVATION_PROTECTION_WINDOW',
  'ALLOCATION_LEAD_TIME',
  'FACILITY_FAILURE_REFUND_LEAD_TIME',
  'OCCUPANCY_VALIDITY_TIMESPAN',
  'ARRIVAL_LATE_TOLERANCE',
  'RAPID_CANCELLATION_COUNT',
  'RAPID_CANCELLATION_WINDOW',
  'RAPID_CANCELLATION_RESTRICTION_DURATION',
  'DAILY_CANCELLATION_LIMIT',
];

const cataloguePayload = {
  revisionId: 'rev-1',
  version: 3,
  effectiveFrom: '2026-01-01T00:00:00Z',
  cancellationTimezone: 'Asia/Ho_Chi_Minh',
  policies: POLICY_KEYS.map((key, index) => ({
    key,
    name: `Name ${key}`,
    description: `Description ${key}`,
    dataType: index === 0 ? 'duration' : 'count',
    unit: index === 0 ? 'minutes' : 'submissions',
    defaultValue: index === 0 ? 30 : index,
    isOverridableByOwner: index !== POLICY_KEYS.length - 1,
    bound: index === 1 ? null : { minimum: index, maximum: index + 5 },
  })),
};

// A fully valid draft baseline reused across validation tests.
const validDraft = {
  paymentHoldDurationMinutes: 15,
  minReservationDurationMinutes: 30,
  maxReservationDurationHours: 24,
  maxAdvanceBookingDays: 7,
  interBookingBufferMinutes: 0,
  reservationProtectionWindowHours: 2,
  allocationLeadTimeMinutes: 10,
  facilityFailureRefundLeadMinutes: 5,
  occupancyValidityTimespanHours: 1,
  arrivalLateToleranceMinutes: 20,
  rapidCancellationCount: 3,
  rapidCancellationWindowMinutes: 60,
  rapidCancellationRestrictionMinutes: 1440,
  dailyCancellationLimit: 5,
  cancellationTimezone: 'Asia/Ho_Chi_Minh',
};

test('adminPolicyApi.getCatalogue parses the 14-key platform catalogue with bound null vs present', async () => {
  const f = fixture(() => response(200, { success: true, data: cataloguePayload }));
  const result = await f.policyApi.getCatalogue();
  assert.equal(result.policies.length, 14);
  assert.deepEqual(result.policies.map(policy => policy.key), POLICY_KEYS);
  assert.deepEqual(result.policies[0].bound, { minimum: 0, maximum: 5 });
  assert.equal(result.policies[1].bound, null);
  assert.equal(result.policies[0].unit, 'minutes');
  assert.equal(result.policies[0].defaultValue, 30);
  assert.equal(result.policies[0].dataType, 'duration');
  assert.equal(result.policies[1].unit, 'submissions');
  assert.equal(result.policies[1].dataType, 'count');
  assert.equal(result.policies[13].isOverridableByOwner, false);
  assert.equal(result.revisionId, 'rev-1');
  assert.equal(result.version, 3);
  assert.equal(result.cancellationTimezone, 'Asia/Ho_Chi_Minh');
  assert.equal(f.calls.length, 1);
  assert.equal(f.calls[0].options.method, 'GET');
  assert.equal(f.calls[0].url, 'http://localhost:5055/api/v1/admin/policies/operations/catalogue');
  assert.equal(f.calls[0].options.headers.Authorization, 'Bearer access-a');
});

test('adminPolicyApi.getCatalogue rejects an envelope without a policies array', async () => {
  const f = fixture(() => response(200, { success: true, data: { ...cataloguePayload, policies: null } }));
  await assert.rejects(f.policyApi.getCatalogue(), /Invalid catalogue response/);
});

test('adminPolicyApi.getEffective resolves values, inheritance, source and passes scope filters', async () => {
  const effective = {
    scope: 'SITE',
    tenantId: 't-1',
    siteId: 's-1',
    evaluatedAt: '2026-01-01T00:00:00Z',
    platformRevision: { id: 'p-rev', version: 1 },
    siteRevision: { id: 's-rev', version: 2 },
    values: {
      RESERVATION_MIN_DURATION: { value: 30, unit: 'minutes', inherited: false, source: 'SITE_OVERRIDE', isZeroNotDisabled: true },
      INTER_BOOKING_BUFFER: { value: 0, unit: 'minutes', inherited: true, source: 'PLATFORM_DEFAULT', isZeroNotDisabled: true },
    },
    arrivalLateTolerance: { enabled: true, minutes: 20, inherited: false },
    cancellationTimezone: 'Asia/Ho_Chi_Minh',
  };
  const f = fixture(() => response(200, { success: true, data: effective }));
  const result = await f.policyApi.getEffective({ tenantId: 't-1', siteId: 's-1', at: '2026-01-01' });
  assert.equal(result.scope, 'SITE');
  assert.equal(result.values.RESERVATION_MIN_DURATION.value, 30);
  assert.equal(result.values.RESERVATION_MIN_DURATION.source, 'SITE_OVERRIDE');
  assert.equal(result.values.RESERVATION_MIN_DURATION.inherited, false);
  assert.equal(result.values.INTER_BOOKING_BUFFER.value, 0);
  assert.equal(result.values.INTER_BOOKING_BUFFER.isZeroNotDisabled, true);
  assert.equal(result.values.INTER_BOOKING_BUFFER.inherited, true);
  assert.equal(result.values.INTER_BOOKING_BUFFER.source, 'PLATFORM_DEFAULT');
  assert.equal(result.arrivalLateTolerance.minutes, 20);
  const url = f.calls[0].url;
  assert.ok(url.startsWith('http://localhost:5055/api/v1/admin/policies/operations/effective?'));
  assert.ok(url.includes('tenantId=t-1'));
  assert.ok(url.includes('siteId=s-1'));
  assert.ok(url.includes('at=2026-01-01'));
});

test('adminPolicyApi.getEffective omits the query string when no scope filters are given', async () => {
  const f = fixture(() => response(200, { success: true, data: { scope: 'PLATFORM' } }));
  await f.policyApi.getEffective();
  assert.equal(f.calls[0].url, 'http://localhost:5055/api/v1/admin/policies/operations/effective');
});

test('adminPolicyApi.createDraft POSTs the draft and unwraps the 201 envelope', async () => {
  const summary = { id: 'draft-1', version: 1, scope: 'PLATFORM', status: 'DRAFT', isActive: false, createdAt: '2026-01-01T00:00:00Z', updatedAt: '2026-01-01T00:00:00Z' };
  const f = fixture(() => response(201, { success: true, data: summary }));
  const body = { ...validDraft, tenantId: 't-1' };
  const result = await f.policyApi.createDraft(body);
  assert.equal(f.calls[0].options.method, 'POST');
  assert.equal(f.calls[0].url, 'http://localhost:5055/api/v1/admin/policies/operations/draft');
  assert.deepEqual(JSON.parse(f.calls[0].options.body), body);
  assert.deepEqual(result, summary);
});

test('adminPolicyApi.createDraft rejects a failure envelope', async () => {
  const f = fixture(() => response(200, { success: false, message: 'Rejected.' }));
  await assert.rejects(f.policyApi.createDraft(validDraft), /Invalid create draft response/);
});

test('adminPolicyApi.updateDraft sends If-Match and strips expectedUpdatedAt from the body', async () => {
  const committed = { id: 'draft-1', version: 2, scope: 'PLATFORM', status: 'DRAFT', isActive: false, updatedAt: '2026-01-02T00:00:00Z' };
  const f = fixture(() => response(200, { success: true, data: committed }));
  const body = { ...validDraft, expectedUpdatedAt: 'ignored-body-token' };
  const result = await f.policyApi.updateDraft('draft-1', body, '2026-01-01T00:00:00Z');
  const call = f.calls[0];
  assert.equal(call.options.method, 'PUT');
  assert.equal(call.url, 'http://localhost:5055/api/v1/admin/policies/operations/draft/draft-1');
  assert.equal(call.options.headers['If-Match'], '2026-01-01T00:00:00Z');
  assert.equal('If-Match' in JSON.parse(call.options.body), false);
  const sentBody = JSON.parse(call.options.body);
  assert.equal('expectedUpdatedAt' in sentBody, false);
  assert.equal(sentBody.minReservationDurationMinutes, 30);
  assert.equal(result.updatedAt, '2026-01-02T00:00:00Z');
});

for (const [label, options, expectedBody] of [
  ['without effectiveFrom', undefined, {}],
  ['with effectiveFrom', { effectiveFrom: '2026-02-01T00:00:00Z' }, { effectiveFrom: '2026-02-01T00:00:00Z' }],
]) {
  test(`adminPolicyApi.activateRevision ${label} POSTs to the revision activate endpoint`, async () => {
    const activation = { id: 'rev-2', version: 2, scope: 'PLATFORM', status: 'SCHEDULED', isActive: true, effectiveFrom: '2026-02-01T00:00:00Z', effectiveUntil: null, supersededRevision: null };
    const f = fixture(() => response(200, { success: true, data: activation }));
    const result = await f.policyApi.activateRevision('rev-2', options);
    assert.equal(f.calls[0].options.method, 'POST');
    assert.equal(f.calls[0].url, 'http://localhost:5055/api/v1/admin/policies/operations/revisions/rev-2/activate');
    assert.deepEqual(JSON.parse(f.calls[0].options.body), expectedBody);
    assert.equal(result.status, 'SCHEDULED');
  });
}

test('adminPolicyApi.getRevisions forwards paging through the operations prefix', async () => {
  const page = { items: [{ id: 'rev-1' }], totalCount: 1, page: 2, pageSize: 5, totalPages: 1 };
  const f = fixture(() => response(200, { success: true, data: page }));
  const result = await f.policyApi.getRevisions({ status: 'DRAFT', page: 2, pageSize: 5 });
  assert.equal(f.calls[0].url, 'http://localhost:5055/api/v1/admin/policies/operations/revisions?status=DRAFT&page=2&pageSize=5');
  assert.equal(result.totalCount, 1);
});

test('adminIntegrationApi.getPaymentIntegration exposes masked clientId and configured booleans only', async () => {
  const payment = {
    provider: 'PayOS',
    mode: 'SANDBOX',
    baseUrl: 'https://api-merchant.payos.vn',
    isConfigured: true,
    clientIdMasked: 'client-****',
    apiKeyConfigured: true,
    checksumKeyConfigured: false,
    status: 'HEALTHY',
    lastTestedAt: null,
    capabilities: { supportsVietQr: true, supportsWebhook: true, supportsDirectRefund: false },
  };
  const f = fixture(() => response(200, { success: true, data: payment }));
  const result = await f.integrationApi.getPaymentIntegration();
  assert.equal(result.clientIdMasked, 'client-****');
  assert.equal(result.apiKeyConfigured, true);
  assert.equal(result.checksumKeyConfigured, false);
  assert.equal('apiKey' in result, false);
  assert.equal('checksumKey' in result, false);
  assert.equal('clientId' in result, false);
  assert.equal(f.calls[0].options.method, 'GET');
  assert.equal(f.calls[0].url, 'http://localhost:5065/api/v1/admin/integrations/payment');
  assert.equal(f.calls[0].options.headers.Authorization, 'Bearer access-a');
});

test('adminIntegrationApi.updatePaymentIntegration PUTs settings and returns the message envelope', async () => {
  const updated = {
    provider: 'PayOS',
    mode: 'LIVE',
    baseUrl: 'https://api-merchant.payos.vn',
    isConfigured: true,
    clientIdMasked: 'client-****',
    apiKeyConfigured: true,
    checksumKeyConfigured: true,
    status: 'UNTESTED',
    lastTestedAt: null,
  };
  const f = fixture(() => response(200, { success: true, message: 'Saved.', data: updated }));
  const body = { mode: 'LIVE', baseUrl: 'https://api-merchant.payos.vn', clientId: 'client', apiKey: 'secret', checksumKey: 'checksum' };
  const result = await f.integrationApi.updatePaymentIntegration(body);
  assert.equal(f.calls[0].options.method, 'PUT');
  assert.equal(f.calls[0].url, 'http://localhost:5065/api/v1/admin/integrations/payment');
  assert.deepEqual(JSON.parse(f.calls[0].options.body), body);
  assert.equal(result.message, 'Saved.');
  assert.equal(result.data.status, 'UNTESTED');
});

test('PAYMENT_BASE_URL_ALLOWLIST gates client-side base URLs per mode', () => {
  const f = fixture(() => response(200, { success: true, data: {} }));
  const allowlist = f.integrationApi.PAYMENT_BASE_URL_ALLOWLIST;
  // The allowlist constants are created in the vm realm; copy into host arrays before comparing.
  assert.deepEqual([...allowlist.LIVE], ['https://api-merchant.payos.vn']);
  assert.deepEqual([...allowlist.SANDBOX], ['https://api-merchant.payos.vn']);
  assert.deepEqual([...allowlist.FIXTURE], ['http://localhost:5173', 'https://fixture.internal']);
  const isAllowed = (mode, url) => allowlist[mode].includes(url);
  assert.equal(isAllowed('LIVE', 'https://api-merchant.payos.vn'), true);
  assert.equal(isAllowed('SANDBOX', 'https://api-merchant.payos.vn'), true);
  assert.equal(isAllowed('FIXTURE', 'https://fixture.internal'), true);
  assert.equal(isAllowed('LIVE', 'https://evil.example.com'), false);
  assert.equal(isAllowed('FIXTURE', 'https://api-merchant.payos.vn'), false);
});

test('adminIntegrationApi.getNotificationIntegrations preserves EMAIL, SMS, PUSH, IN_APP order', async () => {
  const channels = ['EMAIL', 'SMS', 'PUSH', 'IN_APP'].map(channel => ({
    channel,
    provider: `provider-${channel}`,
    isReady: true,
    status: 'HEALTHY',
    description: '',
    lastTestedAt: null,
  }));
  const f = fixture(() => response(200, { success: true, data: { channels } }));
  const result = await f.integrationApi.getNotificationIntegrations();
  assert.deepEqual(result.channels.map(item => item.channel), ['EMAIL', 'SMS', 'PUSH', 'IN_APP']);
  assert.equal(f.calls[0].url, 'http://localhost:5068/api/v1/admin/integrations/notifications');
  assert.equal(f.calls[0].options.method, 'GET');
});

test('adminIntegrationApi.getNotificationIntegrations rejects an envelope without channels', async () => {
  const f = fixture(() => response(200, { success: true, data: {} }));
  await assert.rejects(f.integrationApi.getNotificationIntegrations(), /Invalid notification integrations response/);
});

test('validateDraftValues accepts a valid baseline and a zero inter-booking buffer', () => {
  const f = fixture(() => response(200, { success: true, data: {} }));
  // validateDraftValues returns a vm-realm array, so assert on its length rather than deep-equality.
  assert.equal(f.validation.validateDraftValues(validDraft).length, 0);
  assert.equal(f.validation.validateDraftValues({ ...validDraft, interBookingBufferMinutes: 0 }).length, 0);
  assert.equal(f.validation.hasBlockingIssues([]), false);
  assert.equal(f.validation.hasBlockingIssues([{ property: 'x', message: 'y' }]), true);
});

test('validateDraftValues flags a non-positive minimum reservation duration with the contract message', () => {
  const f = fixture(() => response(200, { success: true, data: {} }));
  const issues = f.validation.validateDraftValues({ ...validDraft, minReservationDurationMinutes: 0 });
  assert.ok(issues.some(issue =>
    issue.property === 'minReservationDurationMinutes'
    && issue.message === 'Minimum reservation duration must be greater than 0.',
  ));
});

test('validateDraftValues flags arrival late tolerance outside 15..30 minutes', () => {
  const f = fixture(() => response(200, { success: true, data: {} }));
  for (const value of [14, 31]) {
    const issues = f.validation.validateDraftValues({ ...validDraft, arrivalLateToleranceMinutes: value });
    assert.ok(issues.some(issue => issue.property === 'arrivalLateToleranceMinutes'));
  }
});

test('validateOverrideBound rejects bounds on a non-overridable or unknown key', () => {
  const f = fixture(() => response(200, { success: true, data: {} }));
  for (const key of ['UNKNOWN_KEY', 'RAPID_CANCELLATION_COUNT']) {
    const issues = f.validation.validateOverrideBound(key, { minimum: 1, maximum: 5 });
    assert.ok(issues.some(issue => issue.property === `overrideBounds.${key}`));
  }
  assert.equal(f.validation.isOverridablePolicyKey('RESERVATION_MIN_DURATION'), true);
  assert.equal(f.validation.isOverridablePolicyKey('UNKNOWN_KEY'), false);
});

test('validateOverrideBound flags a maximum below the minimum', () => {
  const f = fixture(() => response(200, { success: true, data: {} }));
  const issues = f.validation.validateOverrideBound('RESERVATION_MIN_DURATION', { minimum: 10, maximum: 5 });
  assert.ok(issues.some(issue => issue.property === 'overrideBounds.RESERVATION_MIN_DURATION'));
});

test('validateDraftValues surfaces an override bound issue supplied inline', () => {
  const f = fixture(() => response(200, { success: true, data: {} }));
  const issues = f.validation.validateDraftValues({
    ...validDraft,
    overrideBounds: { UNKNOWN_KEY: { minimum: 1, maximum: 5 } },
  });
  assert.ok(issues.some(issue => issue.property === 'overrideBounds.UNKNOWN_KEY'));
});

test('validateDraftValues flags a maximum duration below the minimum with the contract message', () => {
  const f = fixture(() => response(200, { success: true, data: {} }));
  const issues = f.validation.validateDraftValues({
    ...validDraft,
    minReservationDurationMinutes: 120,
    maxReservationDurationHours: 1,
  });
  assert.ok(issues.some(issue =>
    issue.property === 'maxReservationDurationHours'
    && issue.message === 'Maximum reservation duration must be at least the minimum duration.',
  ));
});

test('validateDraftValues only accepts the Asia/Ho_Chi_Minh timezone', () => {
  const f = fixture(() => response(200, { success: true, data: {} }));
  const issues = f.validation.validateDraftValues({ ...validDraft, cancellationTimezone: 'UTC' });
  assert.ok(issues.some(issue =>
    issue.property === 'cancellationTimezone'
    && issue.message === 'Only the Asia/Ho_Chi_Minh timezone is supported.',
  ));
});

test('validateDraftValues flags a non-integer value with the integer contract message', () => {
  const f = fixture(() => response(200, { success: true, data: {} }));
  const issues = f.validation.validateDraftValues({ ...validDraft, arrivalLateToleranceMinutes: 20.5 });
  assert.ok(issues.some(issue =>
    issue.property === 'arrivalLateToleranceMinutes'
    && issue.message === 'Value must be an integer.',
  ));
});

test('validation module exposes the overridable keys and unit divisors', () => {
  const f = fixture(() => response(200, { success: true, data: {} }));
  assert.equal(f.validation.OVERRIDABLE_POLICY_KEYS.length, 10);
  assert.ok(f.validation.OVERRIDABLE_POLICY_KEYS.includes('RESERVATION_MIN_DURATION'));
  assert.equal(f.validation.OVERRIDABLE_POLICY_KEYS.includes('RAPID_CANCELLATION_COUNT'), false);
  assert.equal(f.validation.POLICY_UNIT_DIVISOR.RESERVATION_MAX_DURATION, 60);
  assert.equal(f.validation.POLICY_UNIT_DIVISOR.RESERVATION_MIN_DURATION, 1);
});

test('adminPolicyApi.updateDraft surfaces a 409 RESOURCE_VERSION_CONFLICT as AuthApiError', async () => {
  const f = fixture(() => response(409, {
    success: false,
    code: 'RESOURCE_VERSION_CONFLICT',
    message: 'The policy revision changed. Reload and retry.',
  }));
  let caught;
  try {
    await f.policyApi.updateDraft('draft-1', validDraft, 'stale-token');
  } catch (error) {
    caught = error;
  }
  assert.ok(caught instanceof f.authApi.AuthApiError);
  assert.equal(caught.status, 409);
  assert.equal(caught.code, 'RESOURCE_VERSION_CONFLICT');
  assert.equal(caught.message, 'The policy revision changed. Reload and retry.');
});

test('adminPolicyApi.getRevisionById calls GET /api/v1/admin/policies/operations/revisions/:id', async () => {
  const rev = {
    id: 'rev-uuid-1',
    version: 4,
    status: 'DRAFT',
    scope: 'PLATFORM',
    values: {},
  };
  const f = fixture((url, options) => {
    assert.equal(url, 'http://localhost:5055/api/v1/admin/policies/operations/revisions/rev-uuid-1');
    assert.equal(options.method, 'GET');
    return response(200, { success: true, data: rev });
  });
  const result = await f.policyApi.getRevisionById('rev-uuid-1');
  assert.deepEqual(result, rev);
});

test('validateOverrideBound rejects non-positive minimum for positive-minimum duration keys', () => {
  const f = fixture(() => response(200, { success: true, data: {} }));

  const minIssues = f.validation.validateOverrideBound('RESERVATION_MIN_DURATION', { minimum: 0, maximum: 5 });
  assert.ok(minIssues.some(i => i.message === 'Bound minimum must be at least 1.'));

  const maxIssues = f.validation.validateOverrideBound('RESERVATION_MAX_DURATION', { minimum: 0, maximum: 59 });
  assert.ok(maxIssues.some(i => i.message === "Bound range cannot be represented with the policy's integer storage unit."));

  const holdIssues = f.validation.validateOverrideBound('PAYMENT_HOLD_DURATION', { minimum: 0, maximum: 10 });
  assert.ok(holdIssues.some(i => i.message === 'Bound minimum must be at least 1.'));

  const validIssues = f.validation.validateOverrideBound('RESERVATION_MIN_DURATION', { minimum: 1, maximum: 5 });
  assert.equal(validIssues.length, 0);
});

test('policyDisplay exports 14 canonical keys across all groups and labels', () => {
  const display = load('policyDisplay.ts');
  const allGroupKeys = display.CANONICAL_POLICY_GROUPS.flatMap(g => g.keys);
  assert.equal(allGroupKeys.length, 14);
  assert.equal(Object.keys(display.CANONICAL_POLICY_LABELS).length, 14);
  assert.ok(display.CANONICAL_POLICY_LABELS.OCCUPANCY_VALIDITY_TIMESPAN);
  assert.equal(display.formatPolicyDateTime(null), '—');
});

test('updatePaymentIntegration surfaces INVALID_PAYMENT_BASE_URL error code from backend', async () => {
  const f = fixture(() => response(400, {
    success: false,
    code: 'INVALID_PAYMENT_BASE_URL',
    errorCode: 'INVALID_PAYMENT_BASE_URL',
    message: 'The base URL is not permitted for the selected mode.',
  }));
  let caught;
  try {
    await f.integrationApi.updatePaymentIntegration({
      mode: 'LIVE',
      baseUrl: 'http://malicious.domain.com',
    });
  } catch (err) {
    caught = err;
  }
  assert.ok(caught instanceof f.authApi.AuthApiError);
  assert.equal(caught.status, 400);
  assert.equal(caught.code, 'INVALID_PAYMENT_BASE_URL');
  assert.equal(caught.message, 'The base URL is not permitted for the selected mode.');
});
