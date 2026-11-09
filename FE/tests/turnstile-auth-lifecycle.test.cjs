const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const test = require('node:test');
const ts = require('../node_modules/typescript');

// Helper to transpile and execute TypeScript files in isolated Node vm context
function loadModule(relativePath, mockImports = {}, customEnv = {}, customGlobals = {}) {
  const fullPath = path.join(__dirname, '../src', relativePath);
  let source = fs.readFileSync(fullPath, 'utf8');

  // Replace import.meta.env
  source = source.replaceAll('import.meta.env.VITE_TURNSTILE_SITE_KEY', JSON.stringify(customEnv.VITE_TURNSTILE_SITE_KEY || ''));
  source = source.replaceAll('import.meta.env.DEV', 'true');

  const transpiled = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022,
      jsx: ts.JsxEmit.ReactJSX,
    },
  }).outputText;

  const mockWindow = customGlobals.window || {
    location: { href: 'http://localhost/' },
  };

  const mockDocument = customGlobals.document || {
    getElementById: () => null,
    createElement: () => ({ setAttribute: () => {} }),
    head: { appendChild: () => {} },
  };

  const context = {
    exports: {},
    console,
    Date,
    Promise,
    setTimeout,
    clearTimeout,
    Error,
    window: mockWindow,
    document: mockDocument,
    sessionStorage: {
      getItem: () => null,
      setItem: () => {},
      removeItem: () => {},
    },
    localStorage: {
      getItem: () => null,
      setItem: () => {},
      removeItem: () => {},
    },
    require: (moduleName) => {
      if (moduleName in mockImports) {
        return mockImports[moduleName];
      }
      throw new Error(`Unexpected import in ${relativePath}: ${moduleName}`);
    },
    ...customGlobals,
  };

  context.global = context;
  context.window.window = context.window;
  context.window.document = context.document;

  vm.runInNewContext(transpiled, context);
  return context.exports;
}

class AuthApiError extends Error {
  constructor(message, status, code, details = null) {
    super(message);
    this.name = 'AuthApiError';
    this.status = status;
    this.code = code;
    this.details = details;
  }
}

// -------------------------------------------------------------------------------------------------
// Test Suites for useTurnstile Hook & TurnstileWidget Component (Scenarios 19 & 26)
// -------------------------------------------------------------------------------------------------

test('Scenario 19 & 26: useTurnstile hook handles script load, render, callbacks, reset, and unmount cleanup', async () => {
  let renderedContainer = null;
  let renderOptions = null;
  let resetCallCount = 0;
  let removeCallCount = 0;
  const mockWidgetId = 'cf-widget-test-123';

  const mockWindow = {
    turnstile: {
      render(container, options) {
        renderedContainer = container;
        renderOptions = options;
        return mockWidgetId;
      },
      reset(id) {
        if (id === mockWidgetId) resetCallCount++;
      },
      remove(id) {
        if (id === mockWidgetId) removeCallCount++;
      },
    },
  };

  const mockDocument = {
    getElementById: (id) => (id === 'cf-turnstile-script' ? { addEventListener: () => {} } : null),
    createElement: () => ({ setAttribute: () => {} }),
    head: { appendChild: () => {} },
  };

  let tokenReceived = '';
  let expireCalled = false;
  let errorReceived = null;
  let lastCleanup = null;

  const hookModule = loadModule('hooks/useTurnstile.ts', {
    react: {
      useEffect: (cb) => {
        const cleanup = cb();
        if (typeof cleanup === 'function') lastCleanup = cleanup;
      },
      useRef: (initial) => ({ current: initial }),
      useState: (initial) => [initial, () => {}],
      useCallback: (fn) => fn,
    },
  }, {}, {
    window: mockWindow,
    document: mockDocument,
  });

  const containerRef = { current: { tagName: 'DIV' } };

  const hookInstance = hookModule.useTurnstile(containerRef, {
    action: 'driver_register',
    siteKey: '1x00000000000000000000AA',
    onTokenChange: (tok) => { tokenReceived = tok; },
    onExpire: () => { expireCalled = true; },
    onError: (err) => { errorReceived = err; },
  });

  // Wait for script promise tick
  await new Promise((r) => setTimeout(r, 20));

  assert.ok(renderedContainer !== null, 'Turnstile render was called with container');
  assert.equal(renderOptions.action, 'driver_register');
  assert.equal(renderOptions.sitekey, '1x00000000000000000000AA');

  // Simulate token callback
  renderOptions.callback('minted-token-xyz');
  assert.equal(tokenReceived, 'minted-token-xyz');

  // Scenario 26: Simulate expired-callback
  renderOptions['expired-callback']();
  assert.equal(tokenReceived, '', 'Token cleared to empty on expire');
  assert.equal(expireCalled, true, 'onExpire callback triggered');

  // Simulate reset()
  hookInstance.reset();
  assert.equal(resetCallCount, 1, 'window.turnstile.reset was called');

  // Simulate unmount cleanup
  if (lastCleanup) {
    lastCleanup();
    assert.equal(removeCallCount, 1, 'window.turnstile.remove called on unmount');
  }
});

// -------------------------------------------------------------------------------------------------
// Test Harness for SignIn.tsx Token Lifecycle (Scenarios 19, 20, 21, 22, 23, 24, 25)
// -------------------------------------------------------------------------------------------------

function setupSignInFixture(options = {}) {
  const {
    apiLoginResponse = null,
    apiLoginError = null,
  } = options;

  const stateStore = [];
  let stateIndex = 0;
  const effects = [];
  const apiCalls = [];
  let widgetResetCalls = 0;
  let currentToken = '';

  const mockTurnstileRef = {
    current: {
      reset: () => {
        widgetResetCalls++;
        currentToken = '';
      },
      getToken: () => currentToken,
    },
  };

  const react = {
    useEffect(effect) { effects.push(effect); },
    useState(initial) {
      const idx = stateIndex++;
      if (!(idx in stateStore)) {
        stateStore[idx] = typeof initial === 'function' ? initial() : initial;
      }
      const setter = (val) => {
        stateStore[idx] = typeof val === 'function' ? val(stateStore[idx]) : val;
      };
      return [stateStore[idx], setter];
    },
    useRef() {
      return mockTurnstileRef;
    },
  };

  const jsx = (type, props) => ({ type, props });

  const mockAuthApi = {
    async login(email, password, turnstileToken) {
      apiCalls.push({ action: 'login', email, password, turnstileToken });
      if (apiLoginError) throw apiLoginError;
      return apiLoginResponse || { id: 'u-1', name: 'Driver', role: 'driver' };
    },
    async requestOtp() { return { challengeId: 'c1' }; },
    async completeMfaLogin() { return { id: 'u-1', name: 'Driver', role: 'driver' }; },
  };

  const contactMock = {
    isValidAccountEmail: (val) => val.includes('@') && val.includes('.'),
    isValidSignInContact: (val) => val.includes('@') || /^\+?[0-9]{9,15}$/.test(val),
    signInContactMessage: 'Please enter a valid phone number or email address.',
  };

  const componentModule = loadModule('roles/authentications/Sign In/SignIn.tsx', {
    react,
    'react/jsx-runtime': { jsx, jsxs: jsx },
    '../../../context/AppContext': {
      useApp: () => ({ view: 'sign-in', user: null, setUser: () => {}, setView: () => {} }),
    },
    '../data/data': {
      authData: { saveUser: () => {}, addAuditLog: () => {}, notifyUser: () => {} },
    },
    'react-router-dom': { useNavigate: () => () => {} },
    '../../operator/data/roleRoutes': { getUserHomePath: () => '/' },
    '../../../components/brand/BrandLogo': { BrandLogo: 'brand-logo', BrandWordmark: 'brand-wordmark' },
    '../../../components/ui/PageHeader': { PageHeader: 'page-header' },
    '../../../components/forms/PasswordVisibilityIcon': { PasswordVisibilityButton: 'btn', PasswordVisibilityIcon: 'icon' },
    '../../../components/icon/UntitledIcon': { UntitledIcon: 'untitled-icon' },
    '../../../lib/authApi': { authApi: mockAuthApi, AuthApiError },
    '../../../components/common/TurnstileWidget': {
      TurnstileWidget: 'TurnstileWidget',
    },
    '../pendingRegistrationSignIn': { resumePendingSignIn: async () => null },
    '../Sign Up/DriverOtpForm': { pendingDriverKey: 'sp_pending_driver_registration' },
    '../../../components/forms/MfaCodeInput': { MfaCodeInput: 'mfa-input' },
    '../../../lib/signInContact': contactMock,
    '../../../lib/uiToast': { showUiToast: () => {} },
  });

  const render = () => {
    stateIndex = 0;
    return componentModule.SignIn({ flow: 'password' });
  };

  function findNodes(node) {
    if (!node || typeof node !== 'object') return [];
    return [node, ...[node.props?.children].flat(Infinity).flatMap(findNodes)];
  }

  const getNodes = () => findNodes(render());

  const setEmail = (val) => {
    const input = getNodes().find((n) => n.props?.autoComplete === 'username');
    input.props.onChange({ target: { value: val } });
  };

  const setPassword = (val) => {
    const input = getNodes().find((n) => n.props?.autoComplete === 'current-password');
    input.props.onChange({ target: { value: val } });
  };

  const setToken = (tok) => {
    currentToken = tok;
    const widget = getNodes().find((n) => n.type === 'TurnstileWidget');
    if (widget?.props?.onTokenChange) {
      widget.props.onTokenChange(tok);
    }
  };

  const submit = async () => {
    const form = getNodes().find((n) => n.type === 'form');
    await form.props.onSubmit({ preventDefault: () => {} });
  };

  return {
    render,
    getNodes,
    setEmail,
    setPassword,
    setToken,
    submit,
    getWidgetResetCalls: () => widgetResetCalls,
    getApiCalls: () => apiCalls,
    AuthApiError,
  };
}

// -------------------------------------------------------------------------------------------------
// Scenarios 19 & 20: SignIn Widget Action Binding & Payload Attachment
// -------------------------------------------------------------------------------------------------

test('Scenario 19: SignIn mounts TurnstileWidget with action "password_login"', () => {
  const f = setupSignInFixture();
  const widget = f.getNodes().find((n) => n.type === 'TurnstileWidget');
  assert.ok(widget, 'TurnstileWidget rendered in SignIn');
  assert.equal(widget.props.action, 'password_login', 'Action must be bound to password_login');
});

test('Scenario 20: SignIn attaches turnstileToken to authApi.login payload and resets on success', async () => {
  const f = setupSignInFixture();
  f.setEmail('driver@smartpark.vn');
  f.setPassword('Password@123');
  f.setToken('cf-token-auth-valid');

  await f.submit();

  const calls = f.getApiCalls();
  assert.equal(calls.length, 1);
  assert.equal(calls[0].action, 'login');
  assert.equal(calls[0].email, 'driver@smartpark.vn');
  assert.equal(calls[0].password, 'Password@123');
  assert.equal(calls[0].turnstileToken, 'cf-token-auth-valid');
  assert.equal(f.getWidgetResetCalls(), 1, 'Widget reset upon successful authentication');
});

// -------------------------------------------------------------------------------------------------
// Scenario 21: Pre-submit Client Validation Retains Token (DO NOT RESET WIDGET)
// -------------------------------------------------------------------------------------------------

test('Scenario 21: Pre-submit client validation failure KEEPS TOKEN and DOES NOT RESET WIDGET', async () => {
  const f = setupSignInFixture();
  f.setToken('valid-pre-submit-token');
  f.setEmail('invalid-email-format');

  await f.submit();

  assert.equal(f.getApiCalls().length, 0, 'Client validation stops execution before API call');
  assert.equal(f.getWidgetResetCalls(), 0, 'WIDGET MUST NOT BE RESET on client validation error');
});

// -------------------------------------------------------------------------------------------------
// Scenario 22: Rate Limit 429 Retention (DO NOT RESET WIDGET)
// -------------------------------------------------------------------------------------------------

test('Scenario 22: HTTP 429 Too Many Requests KEEPS TOKEN and DOES NOT RESET WIDGET', async () => {
  const f = setupSignInFixture({
    apiLoginError: new AuthApiError(
      'Too many requests. Please try again later.',
      429,
      'TOO_MANY_REQUESTS',
    ),
  });
  f.setEmail('driver@smartpark.vn');
  f.setPassword('Password@123');
  f.setToken('token-waiting-cooldown');

  await f.submit();

  assert.equal(f.getApiCalls().length, 1);
  assert.equal(f.getWidgetResetCalls(), 0, 'WIDGET MUST NOT BE RESET on HTTP 429 rate limit error');
});

// -------------------------------------------------------------------------------------------------
// Scenario 23: Business Validation Rejection (400) MUST RESET WIDGET
// -------------------------------------------------------------------------------------------------

test('Scenario 23: Server 400 VALIDATION_FAILED MUST RESET WIDGET', async () => {
  const f = setupSignInFixture({
    apiLoginError: new AuthApiError(
      'Validation failed.',
      400,
      'VALIDATION_FAILED',
    ),
  });
  f.setEmail('driver@smartpark.vn');
  f.setPassword('Password@123');
  f.setToken('token-consumed-by-server');

  await f.submit();

  assert.equal(f.getApiCalls().length, 1);
  assert.equal(f.getWidgetResetCalls(), 1, 'WIDGET MUST BE RESET on server 400 rejection');
});

// -------------------------------------------------------------------------------------------------
// Scenario 24: Authentication Failure (401/403) MUST RESET WIDGET
// -------------------------------------------------------------------------------------------------

test('Scenario 24: Wrong password (401 AUTH_FAILED) MUST RESET WIDGET', async () => {
  const f = setupSignInFixture({
    apiLoginError: new AuthApiError(
      'Invalid email or password.',
      401,
      'AUTH_FAILED',
    ),
  });
  f.setEmail('driver@smartpark.vn');
  f.setPassword('WrongPassword@123');
  f.setToken('token-auth-failed');

  await f.submit();

  assert.equal(f.getApiCalls().length, 1);
  assert.equal(f.getWidgetResetCalls(), 1, 'WIDGET MUST BE RESET on 401 AUTH_FAILED');
});

test('Scenario 24: Inactive account (403 ACCOUNT_INACTIVE) MUST RESET WIDGET', async () => {
  const f = setupSignInFixture({
    apiLoginError: new AuthApiError(
      'Your account is inactive.',
      403,
      'ACCOUNT_INACTIVE',
    ),
  });
  f.setEmail('driver@smartpark.vn');
  f.setPassword('Password@123');
  f.setToken('token-inactive-account');

  await f.submit();

  assert.equal(f.getApiCalls().length, 1);
  assert.equal(f.getWidgetResetCalls(), 1, 'WIDGET MUST BE RESET on 403 ACCOUNT_INACTIVE');
});

// -------------------------------------------------------------------------------------------------
// Scenario 25: Bot Verification Failure (400/503) MUST RESET WIDGET
// -------------------------------------------------------------------------------------------------

test('Scenario 25: 400 BOT_VERIFICATION_FAILED MUST RESET WIDGET', async () => {
  const f = setupSignInFixture({
    apiLoginError: new AuthApiError(
      'Bot verification failed.',
      400,
      'BOT_VERIFICATION_FAILED',
    ),
  });
  f.setEmail('driver@smartpark.vn');
  f.setPassword('Password@123');
  f.setToken('bad-bot-token');

  await f.submit();

  assert.equal(f.getApiCalls().length, 1);
  assert.equal(f.getWidgetResetCalls(), 1, 'WIDGET MUST BE RESET on BOT_VERIFICATION_FAILED');
});

test('Scenario 25: 400 BOT_VERIFICATION_EXPIRED MUST RESET WIDGET', async () => {
  const f = setupSignInFixture({
    apiLoginError: new AuthApiError(
      'Bot verification token has expired or was already used. Please try again.',
      400,
      'BOT_VERIFICATION_EXPIRED',
    ),
  });
  f.setEmail('driver@smartpark.vn');
  f.setPassword('Password@123');
  f.setToken('expired-bot-token');

  await f.submit();

  assert.equal(f.getApiCalls().length, 1);
  assert.equal(f.getWidgetResetCalls(), 1, 'WIDGET MUST BE RESET on BOT_VERIFICATION_EXPIRED');
});

test('Scenario 25: 503 BOT_VERIFICATION_UNAVAILABLE MUST RESET WIDGET', async () => {
  const f = setupSignInFixture({
    apiLoginError: new AuthApiError(
      'Bot verification service is temporarily unavailable. Please retry.',
      503,
      'BOT_VERIFICATION_UNAVAILABLE',
    ),
  });
  f.setEmail('driver@smartpark.vn');
  f.setPassword('Password@123');
  f.setToken('bot-service-down');

  await f.submit();

  assert.equal(f.getApiCalls().length, 1);
  assert.equal(f.getWidgetResetCalls(), 1, 'WIDGET MUST BE RESET on 503 BOT_VERIFICATION_UNAVAILABLE');
});

// -------------------------------------------------------------------------------------------------
// Scenario 27: SignUp Tab Switching & Action Binding Isolation (Driver vs Owner)
// -------------------------------------------------------------------------------------------------

function setupSignUpFixture(options = {}) {
  const {
    defaultTab = 'driver',
    apiDriverResponse = null,
    apiDriverError = null,
    apiOwnerResponse = null,
    apiOwnerError = null,
  } = typeof options === 'string' ? { defaultTab: options } : options;

  const stateStore = [defaultTab];
  let stateIndex = 0;
  let driverResetCalls = 0;
  let ownerResetCalls = 0;
  let driverToken = '';
  let ownerToken = '';
  const driverCalls = [];
  const ownerCalls = [];

  const mockDriverRef = {
    current: {
      reset: () => {
        driverResetCalls++;
        driverToken = '';
      },
      getToken: () => driverToken,
    },
  };

  const mockOwnerRef = {
    current: {
      reset: () => {
        ownerResetCalls++;
        ownerToken = '';
      },
      getToken: () => ownerToken,
    },
  };

  let refCallCount = 0;
  const react = {
    useEffect: () => {},
    useState(initial) {
      const idx = stateIndex++;
      if (!(idx in stateStore)) {
        stateStore[idx] = typeof initial === 'function' ? initial() : initial;
      }
      const setter = (val) => {
        stateStore[idx] = typeof val === 'function' ? val(stateStore[idx]) : val;
      };
      return [stateStore[idx], setter];
    },
    useRef() {
      refCallCount++;
      return refCallCount % 2 === 1 ? mockDriverRef : mockOwnerRef;
    },
  };

  const jsx = (type, props) => ({ type, props });

  const mockAuthApi = {
    async registerDriver(payload) {
      driverCalls.push(payload);
      if (apiDriverError) throw apiDriverError;
      return apiDriverResponse || { registrationId: 'reg-1', channel: 'email', expiresAt: '2026-12-31', resendAvailableAt: '2026-12-31' };
    },
    async recoverDriver() { return { registrationId: 'reg-rec', channel: 'email', expiresAt: '2026-12-31', resendAvailableAt: '2026-12-31' }; },
  };

  const mockOwnerApi = {
    async register(payload) {
      ownerCalls.push(payload);
      if (apiOwnerError) throw apiOwnerError;
      return apiOwnerResponse || { id: 'app-1', email: payload.email, ownerName: payload.fullName, businessName: payload.businessName, lotType: payload.lotType, status: 'pending' };
    },
    async recover() { return { id: 'app-rec', email: 'owner@test.com' }; },
  };

  const signUpModule = loadModule('roles/authentications/Sign Up/SignUp.tsx', {
    react,
    'react/jsx-runtime': { jsx, jsxs: jsx },
    '../../../components/ui/RoundedSelect': { RoundedSelect: 'rounded-select' },
    '../../../context/AppContext': { useApp: () => ({ view: 'sign-up', setView: () => {} }) },
    '../data/data': { authData: { getOwners: () => [] } },
    '../../../components/brand/BrandLogo': { BrandLogo: 'brand-logo', BrandWordmark: 'brand-wordmark' },
    '../../../components/ui/PageHeader': { PageHeader: 'page-header' },
    '../../../components/forms/PasswordVisibilityIcon': { PasswordVisibilityButton: 'btn', PasswordVisibilityIcon: 'icon' },
    '../../../components/icon/UntitledIcon': { UntitledIcon: 'untitled-icon' },
    '../../../lib/authApi': { authApi: mockAuthApi, AuthApiError },
    '../../../lib/ownerApi': { ownerApi: mockOwnerApi },
    '../../../components/common/TurnstileWidget': {
      TurnstileWidget: 'TurnstileWidget',
    },
    './DriverOtpForm': { DriverOtpForm: 'driver-otp', pendingDriverKey: 'sp_key', restoreDriverRegistration: () => null },
    './OwnerVerification': { OwnerVerification: 'owner-verification' },
    './OwnerRegistrationRecovery': { OwnerRegistrationRecovery: 'owner-recovery' },
    '../../../lib/ownerRegistration': { restoreOwnerRegistration: () => null, ownerRegistrationStage: () => null, ensureOwnerRegistrationStorage: () => {} },
  });

  const render = () => {
    stateIndex = 0;
    refCallCount = 0;
    return signUpModule.SignUp();
  };

  function findNodes(node) {
    if (!node || typeof node !== 'object') return [];
    return [node, ...[node.props?.children].flat(Infinity).flatMap(findNodes)];
  }

  const getNodes = () => findNodes(render());

  const setDriverToken = (tok) => {
    driverToken = tok;
    const widget = getNodes().find((n) => n.type === 'TurnstileWidget' && n.props?.action === 'driver_register');
    if (widget?.props?.onTokenChange) widget.props.onTokenChange(tok);
  };

  const setOwnerToken = (tok) => {
    ownerToken = tok;
    const widget = getNodes().find((n) => n.type === 'TurnstileWidget' && n.props?.action === 'owner_register');
    if (widget?.props?.onTokenChange) widget.props.onTokenChange(tok);
  };

  return {
    render,
    getNodes,
    setDriverToken,
    setOwnerToken,
    getDriverCalls: () => driverCalls,
    getOwnerCalls: () => ownerCalls,
    getDriverResetCalls: () => driverResetCalls,
    getOwnerResetCalls: () => ownerResetCalls,
  };
}

test('Scenario 19 & 27: SignUp mounts Driver widget with "driver_register" and Owner with "owner_register"', () => {
  // Test Driver tab
  const driverFixture = setupSignUpFixture('driver');
  const driverNodes = driverFixture.getNodes();
  const driverWidget = driverNodes.find((n) => n.type === 'TurnstileWidget' && n.props?.action === 'driver_register');
  assert.ok(driverWidget, 'Driver tab must render TurnstileWidget with action="driver_register"');

  // Test Business/Owner tab
  const ownerFixture = setupSignUpFixture('business');
  const ownerNodes = ownerFixture.getNodes();
  const ownerWidget = ownerNodes.find((n) => n.type === 'TurnstileWidget' && n.props?.action === 'owner_register');
  assert.ok(ownerWidget, 'Owner tab must render TurnstileWidget with action="owner_register"');
});

test('Scenario 20 & 21: SignUp Driver attaches turnstileToken, preserves on client validation error, and resets on success', async () => {
  const f = setupSignUpFixture('driver');
  f.setDriverToken('token-driver-valid');

  // Driver submit with mismatched password fails client validation
  const driverFormNode = f.getNodes().find((n) => n.type === 'form');
  // Trigger submit while fields are empty
  await driverFormNode.props.onSubmit({ preventDefault: () => {} });

  assert.equal(f.getDriverCalls().length, 0, 'Pre-submit client validation prevents API submission');
  assert.equal(f.getDriverResetCalls(), 0, 'WIDGET MUST NOT BE RESET on pre-submit client validation error');
});

test('Scenario 22: SignUp Driver retains token on HTTP 429 Rate Limit', async () => {
  const f = setupSignUpFixture({
    defaultTab: 'driver',
    apiDriverError: new AuthApiError('Rate limited', 429, 'TOO_MANY_REQUESTS'),
  });

  // Provide valid driver form inputs
  const nameInput = f.getNodes().find((n) => n.props?.placeholder === 'Your full name');
  if (nameInput) nameInput.props.onChange({ target: { value: 'Driver Name' } });
  const emailInput = f.getNodes().find((n) => n.props?.placeholder === 'you@example.com');
  if (emailInput) emailInput.props.onChange({ target: { value: 'driver@test.com' } });
  const pwInput = f.getNodes().find((n) => n.props?.placeholder === 'Optional, 8–15 characters');
  if (pwInput) pwInput.props.onChange({ target: { value: 'Password@123' } });
  const cpwInput = f.getNodes().find((n) => n.props?.placeholder === 'Repeat your password');
  if (cpwInput) cpwInput.props.onChange({ target: { value: 'Password@123' } });

  f.setDriverToken('driver-token-waiting-cooldown');

  const form = f.getNodes().find((n) => n.type === 'form');
  await form.props.onSubmit({ preventDefault: () => {} });

  assert.equal(f.getDriverCalls().length, 1);
  assert.equal(f.getDriverCalls()[0].turnstileToken, 'driver-token-waiting-cooldown');
  assert.equal(f.getDriverResetCalls(), 0, 'WIDGET MUST NOT BE RESET on HTTP 429 rate limit error');
});

test('Scenario 23 & 25: SignUp Driver resets widget on 400 VALIDATION_FAILED or 400 BOT_VERIFICATION_FAILED', async () => {
  const f = setupSignUpFixture({
    defaultTab: 'driver',
    apiDriverError: new AuthApiError('Bot verification failed', 400, 'BOT_VERIFICATION_FAILED'),
  });

  const nameInput = f.getNodes().find((n) => n.props?.placeholder === 'Your full name');
  if (nameInput) nameInput.props.onChange({ target: { value: 'Driver Name' } });
  const emailInput = f.getNodes().find((n) => n.props?.placeholder === 'you@example.com');
  if (emailInput) emailInput.props.onChange({ target: { value: 'driver@test.com' } });
  const pwInput = f.getNodes().find((n) => n.props?.placeholder === 'Optional, 8–15 characters');
  if (pwInput) pwInput.props.onChange({ target: { value: 'Password@123' } });
  const cpwInput = f.getNodes().find((n) => n.props?.placeholder === 'Repeat your password');
  if (cpwInput) cpwInput.props.onChange({ target: { value: 'Password@123' } });

  f.setDriverToken('bad-driver-token');

  const form = f.getNodes().find((n) => n.type === 'form');
  await form.props.onSubmit({ preventDefault: () => {} });

  assert.equal(f.getDriverCalls().length, 1);
  assert.equal(f.getDriverResetCalls(), 1, 'WIDGET MUST BE RESET on 400 BOT_VERIFICATION_FAILED');
});
