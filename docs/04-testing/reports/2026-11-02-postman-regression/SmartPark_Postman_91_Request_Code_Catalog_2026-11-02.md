# SmartPark Postman — complete 91-request code catalog

Source: `SmartPark - API Test Suite.postman_collection.json`. Scripts are **test definitions, not evidence of execution**. Manual response evidence belongs to the QA report. Sensitive literal values are masked if present.

## 01. 00 Auth smoke - Runner / Anonymous session is rejected

**GET** `{{userUrl}}/api/auth/me`

**Before request script:**
```javascript
const requiredVariables = ["userUrl"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test("HTTP 401 without token", () => pm.response.to.have.status(401));
```

## 02. 00 Auth smoke - Runner / Login successful

**POST** `{{userUrl}}/api/auth/login`

**Body (template):**
```json
{
  "email": "{{smokeEmail}}",
  "password": "{{smokePassword}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["smokeEmail","smokePassword","userUrl"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.environment.unset('smokeAccessToken');
pm.environment.unset('smokeRefreshToken');
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Valid session for the expected role", () => {
  const result = pm.response.json();
  const d = result.data;
  pm.expect(result.mfaRequired === true, 'Complete /api/auth/mfa/login before expecting a session').to.eql(false);
  pm.expect(d.accessToken).to.be.a('string').and.not.empty;
  pm.expect(d.accessToken.split('.')).to.have.lengthOf(3);
  pm.expect(d.refreshToken).to.be.a('string').and.not.empty;
  pm.expect(d.tokenType).to.eql('Bearer');
  pm.expect(d.expiresIn).to.be.within(895, 900);
  pm.expect(d.user.role).to.eql('driver');
  pm.expect(d.user.userId).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('smokeAccessToken', d.accessToken);
  pm.environment.set('smokeRefreshToken', d.refreshToken);
});
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.user.email).to.eql(pm.variables.get('smokeEmail').trim().toLowerCase());
  pm.expect(d.user).not.to.have.property('passwordHash');
});
```

## 03. 00 Auth smoke - Runner / Wrong password

**POST** `{{userUrl}}/api/auth/login`

**Body (template):**
```json
{
  "email": "{{smokeEmail}}",
  "password": "[REDACTED]"
}
```

**Before request script:**
```javascript
const requiredVariables = ["smokeEmail","userUrl"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test("HTTP 401", function () { pm.response.to.have.status(401); });
const body = pm.response.json();
pm.test("Success flag", function () { pm.expect(body.success).to.eql(false); });
pm.test("Generic credential error", function () { pm.expect(body.message).to.eql("Invalid email or password."); });
pm.test("AUTH_FAILED", () => pm.expect(body.code).to.eql("AUTH_FAILED"));
```

## 04. 00 Auth smoke - Runner / Unknown email

**POST** `{{userUrl}}/api/auth/login`

**Body (template):**
```json
{
  "email": "unknown@gmail.com",
  "password": "{{smokePassword}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["smokePassword","userUrl"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test("HTTP 401", function () { pm.response.to.have.status(401); });
const body = pm.response.json();
pm.test("Success flag", function () { pm.expect(body.success).to.eql(false); });
pm.test("Generic credential error", function () { pm.expect(body.message).to.eql("Invalid email or password."); });
pm.test("AUTH_FAILED", () => pm.expect(body.code).to.eql("AUTH_FAILED"));
```

## 05. 00 Auth smoke - Runner / Missing email

**POST** `{{userUrl}}/api/auth/login`

**Body (template):**
```json
{
  "password": "{{smokePassword}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["smokePassword","userUrl"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test("HTTP 400", function () { pm.response.to.have.status(400); });
const body = pm.response.json();
pm.test("Success flag", function () { pm.expect(body.success).to.eql(false); });
pm.test("Validation errors", function () { pm.expect(body.errors).to.be.an("array").that.is.not.empty; });
```

## 06. 00 Auth smoke - Runner / Invalid email

**POST** `{{userUrl}}/api/auth/login`

**Body (template):**
```json
{
  "email": "invalid",
  "password": "{{smokePassword}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["smokePassword","userUrl"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test("HTTP 400", function () { pm.response.to.have.status(400); });
const body = pm.response.json();
pm.test("Success flag", function () { pm.expect(body.success).to.eql(false); });
pm.test("Validation errors", function () { pm.expect(body.errors).to.be.an("array").that.is.not.empty; });
```

## 07. 00 Auth smoke - Runner / Missing password

**POST** `{{userUrl}}/api/auth/login`

**Body (template):**
```json
{
  "email": "{{smokeEmail}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["smokeEmail","userUrl"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test("HTTP 400", function () { pm.response.to.have.status(400); });
const body = pm.response.json();
pm.test("Success flag", function () { pm.expect(body.success).to.eql(false); });
pm.test("Validation errors", function () { pm.expect(body.errors).to.be.an("array").that.is.not.empty; });
```

## 08. 00 Auth smoke - Runner / Current authenticated user

**GET** `{{userUrl}}/api/auth/me`

**Before request script:**
```javascript
const requiredVariables = ["smokeAccessToken","userUrl"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test("HTTP 200", () => pm.response.to.have.status(200));
pm.test("Server identity and role", () => { const user = pm.response.json().data; pm.expect(user.userId).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i); pm.expect(user.role).to.eql("driver"); });
```

## 09. 00 Auth smoke - Runner / Logout requires refresh token

**POST** `{{userUrl}}/api/auth/logout`

**Body (template):**
```json
{}
```

**Before request script:**
```javascript
const requiredVariables = ["smokeAccessToken","userUrl"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test("HTTP 400", () => pm.response.to.have.status(400));
pm.test("MISSING_REFRESH_TOKEN", () => pm.expect(pm.response.json().code).to.eql("MISSING_REFRESH_TOKEN"));
```

## 10. 00 Auth smoke - Runner / Logout current session

**POST** `{{userUrl}}/api/auth/logout`

**Body (template):**
```json
{
  "refreshToken": "{{smokeRefreshToken}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["smokeAccessToken","userUrl","smokeRefreshToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test("HTTP 200", () => pm.response.to.have.status(200));
pm.test("Logout success", () => pm.expect(pm.response.json().success).to.eql(true));
```

## 11. 00 Auth smoke - Runner / Revoked token is rejected

**GET** `{{userUrl}}/api/auth/me`

**Before request script:**
```javascript
const requiredVariables = ["smokeAccessToken","userUrl"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test("HTTP 401 after logout", () => pm.response.to.have.status(401));
pm.environment.unset("smokeAccessToken");
pm.environment.unset("smokeRefreshToken");
```

## 12. 01 Manual workflows - OTP and invitations / 01 Driver / Register Driver

**POST** `{{userUrl}}/api/auth/register/driver`

**Body (template):**
```json
{
    "fullName": "QA Driver",
    "email": "{{driverEmail}}",
    "password": "{{driverPassword}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","driverEmail","driverPassword"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 201', () => pm.response.to.have.status(201));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.registrationId).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('registrationId', d.registrationId);
});
```

## 13. 01 Manual workflows - OTP and invitations / 01 Driver / Recover pending registration

**POST** `{{userUrl}}/api/auth/register/driver/recover`

**Body (template):**
```json
{
  "contact": "{{driverEmail}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","driverEmail"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.registrationId).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('registrationId', d.registrationId);
});
```

## 14. 01 Manual workflows - OTP and invitations / 01 Driver / Resend after cooldown

**POST** `{{userUrl}}/api/auth/register/driver/resend`

**Body (template):**
```json
{
  "registrationId": "{{registrationId}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","registrationId"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.registrationId).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('registrationId', d.registrationId);
});
```

## 15. 01 Manual workflows - OTP and invitations / 01 Driver / Verify delivered registration OTP

**POST** `{{userUrl}}/api/auth/register/driver/verify`

**Body (template):**
```json
{
  "registrationId": "{{registrationId}}",
  "code": "{{otp}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","registrationId","otp"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
```

## 16. 01 Manual workflows - OTP and invitations / 02 Driver session / Request driver login OTP

**POST** `{{userUrl}}/api/auth/otp/request`

**Body (template):**
```json
{
  "contact": "{{driverEmail}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","driverEmail"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.challengeId).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('challengeId', d.challengeId);
});
```

## 17. 01 Manual workflows - OTP and invitations / 02 Driver session / Complete driver OTP login

**POST** `{{userUrl}}/api/auth/otp/login`

**Body (template):**
```json
{
  "challengeId": "{{challengeId}}",
  "code": "{{otp}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","challengeId","otp"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.environment.unset('driverToken');
pm.environment.unset('driverRefresh');
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Valid session for the expected role", () => {
  const result = pm.response.json();
  const d = result.data;
  pm.expect(result.mfaRequired === true, 'Complete /api/auth/mfa/login before expecting a session').to.eql(false);
  pm.expect(d.accessToken).to.be.a('string').and.not.empty;
  pm.expect(d.accessToken.split('.')).to.have.lengthOf(3);
  pm.expect(d.refreshToken).to.be.a('string').and.not.empty;
  pm.expect(d.tokenType).to.eql('Bearer');
  pm.expect(d.expiresIn).to.be.within(895, 900);
  pm.expect(d.user.role).to.eql('driver');
  pm.expect(d.user.userId).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('driverToken', d.accessToken);
  pm.environment.set('driverRefresh', d.refreshToken);
});
```

## 18. 01 Manual workflows - OTP and invitations / 02 Driver session / Password login driver

**POST** `{{userUrl}}/api/auth/login`

**Body (template):**
```json
{
  "email": "{{driverEmail}}",
  "password": "{{driverPassword}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","driverEmail","driverPassword"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.environment.unset('driverToken');
pm.environment.unset('driverRefresh');
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Valid session for the expected role", () => {
  const result = pm.response.json();
  const d = result.data;
  pm.expect(result.mfaRequired === true, 'Complete /api/auth/mfa/login before expecting a session').to.eql(false);
  pm.expect(d.accessToken).to.be.a('string').and.not.empty;
  pm.expect(d.accessToken.split('.')).to.have.lengthOf(3);
  pm.expect(d.refreshToken).to.be.a('string').and.not.empty;
  pm.expect(d.tokenType).to.eql('Bearer');
  pm.expect(d.expiresIn).to.be.within(895, 900);
  pm.expect(d.user.role).to.eql('driver');
  pm.expect(d.user.userId).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('driverToken', d.accessToken);
  pm.environment.set('driverRefresh', d.refreshToken);
});
```

## 19. 01 Manual workflows - OTP and invitations / 02 Driver session / Me

**GET** `{{userUrl}}/api/auth/me`

**Before request script:**
```javascript
const requiredVariables = ["userUrl","driverToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.userId).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.expect(d.role).to.eql('driver');
});
```

## 20. 01 Manual workflows - OTP and invitations / 02 Driver session / Refresh and save rotated pair

**POST** `{{userUrl}}/api/auth/refresh`

**Body (template):**
```json
{
  "refreshToken": "{{driverRefresh}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","driverRefresh"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
const previousRefresh = pm.variables.get('driverRefresh');
pm.environment.unset('driverToken');
pm.environment.unset('driverRefresh');
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Valid session for the expected role", () => {
  const result = pm.response.json();
  const d = result.data;
  pm.expect(result.mfaRequired === true, 'Complete /api/auth/mfa/login before expecting a session').to.eql(false);
  pm.expect(d.accessToken).to.be.a('string').and.not.empty;
  pm.expect(d.accessToken.split('.')).to.have.lengthOf(3);
  pm.expect(d.refreshToken).to.be.a('string').and.not.empty;
  pm.expect(d.tokenType).to.eql('Bearer');
  pm.expect(d.expiresIn).to.be.within(895, 900);
  pm.expect(d.user.role).to.eql('driver');
  pm.expect(d.user.userId).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.expect(d.refreshToken).not.to.eql(previousRefresh);
  pm.environment.set('driverToken', d.accessToken);
  pm.environment.set('driverRefresh', d.refreshToken);
});
```

## 21. 01 Manual workflows - OTP and invitations / 03 Owner registration / Register Owner

**POST** `{{userUrl}}/api/auth/register/owner`

**Body (template):**
```json
{
  "fullName": "QA Owner",
  "businessName": "QA Parking",
  "email": "{{ownerEmail}}",
  "phone": "{{ownerPhone}}",
  "lotType": "outdoor",
  "agreedToPolicy": true,
  "password": "{{ownerPassword}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","ownerEmail","ownerPhone","ownerPassword"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.environment.unset('ownerApplicationId');
pm.environment.unset('ownerEmailChallenge');
pm.environment.unset('ownerSetupToken');
pm.test('Expected HTTP 201', () => pm.response.to.have.status(201));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.status).to.eql('pending');
  pm.expect(d.lotSetupComplete).to.eql(false);
  pm.expect(d.setupToken).to.be.a('string').and.not.empty;
  pm.environment.set('ownerSetupToken', d.setupToken);
  pm.expect(d.id).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('ownerApplicationId', d.id);
});
```

## 22. 01 Manual workflows - OTP and invitations / 03 Owner registration / Save Owner lot setup and request email OTP

**POST** `{{userUrl}}/api/auth/register/owner/lot-setup`

**Body (template):**
```json
{
  "registrationId": "{{ownerApplicationId}}",
  "setupToken": "{{ownerSetupToken}}",
  "lot": {
    "name": "QA Owner Lot",
    "address": "QA address",
    "lotType": "outdoor",
    "totalSlots": 10,
    "floors": 1,
    "devices": [],
    "modelFileName": "outdoor_parking_lot.glb",
    "hourlyRate": 20000,
    "dailyRate": 100000,
    "nightRate": 30000,
    "gracePeriodMinutes": 15,
    "subscriptionMonthly": 500000,
    "subscriptionYearly": 5000000,
    "policyAccepted": true
  }
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","ownerApplicationId","ownerSetupToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.id).to.eql(pm.variables.get('ownerApplicationId'));
  pm.expect(d.lotSetupComplete).to.eql(true);
  pm.expect(d.verification.challengeId).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('ownerEmailChallenge', d.verification.challengeId);
});
```

## 23. 01 Manual workflows - OTP and invitations / 03 Owner registration / Recover Owner

**POST** `{{userUrl}}/api/auth/register/owner/recover`

**Body (template):**
```json
{
  "contact": "{{ownerEmail}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","ownerEmail"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.id).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.expect(d.lotSetupComplete).to.eql(true);
  pm.expect(d.verification.challengeId).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('ownerApplicationId', d.id);
  pm.environment.set('ownerEmailChallenge', d.verification.challengeId);
});
```

## 24. 01 Manual workflows - OTP and invitations / 03 Owner registration / Resend Email after cooldown

**POST** `{{userUrl}}/api/auth/register/owner/resend`

**Body (template):**
```json
{
  "registrationId": "{{ownerEmailChallenge}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","ownerEmailChallenge"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.challengeId).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('ownerEmailChallenge', d.challengeId);
});
```

## 25. 01 Manual workflows - OTP and invitations / 03 Owner registration / Verify Email OTP

**POST** `{{userUrl}}/api/auth/register/owner/verify`

**Body (template):**
```json
{
  "challengeId": "{{ownerEmailChallenge}}",
  "code": "{{otp}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","ownerEmailChallenge","otp"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
```

## 26. 01 Manual workflows - OTP and invitations / 03b Optional Owner setup recovery - alternative path / Request lost Owner setup credential recovery

**POST** `{{userUrl}}/api/auth/register/owner/setup-recovery/request`

**Body (template):**
```json
{
  "contact": "{{ownerEmail}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","ownerEmail"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.challengeId).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('ownerRecoveryChallenge', d.challengeId);
});
```

## 27. 01 Manual workflows - OTP and invitations / 03b Optional Owner setup recovery - alternative path / Complete Owner setup credential recovery

**POST** `{{userUrl}}/api/auth/register/owner/setup-recovery/complete`

**Body (template):**
```json
{
  "challengeId": "{{ownerRecoveryChallenge}}",
  "code": "{{otp}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","ownerRecoveryChallenge","otp"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.environment.unset('ownerEmailChallenge');
pm.environment.unset('ownerSetupToken');
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.status).to.eql('pending');
  pm.expect(d.lotSetupComplete).to.eql(false);
  pm.expect(d.setupToken).to.be.a('string').and.not.empty;
  pm.environment.set('ownerSetupToken', d.setupToken);
  pm.expect(d.id).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('ownerApplicationId', d.id);
});
```

## 28. 01 Manual workflows - OTP and invitations / 04 Admin approval / Admin password login

**POST** `{{userUrl}}/api/auth/login`

**Body (template):**
```json
{
  "email": "{{adminEmail}}",
  "password": "{{adminPassword}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","adminEmail","adminPassword"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.environment.unset('adminToken');
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Valid session for the expected role", () => {
  const result = pm.response.json();
  const d = result.data;
  pm.expect(result.mfaRequired === true, 'Complete /api/auth/mfa/login before expecting a session').to.eql(false);
  pm.expect(d.accessToken).to.be.a('string').and.not.empty;
  pm.expect(d.accessToken.split('.')).to.have.lengthOf(3);
  pm.expect(d.refreshToken).to.be.a('string').and.not.empty;
  pm.expect(d.tokenType).to.eql('Bearer');
  pm.expect(d.expiresIn).to.be.within(895, 900);
  pm.expect(d.user.role).to.eql('admin');
  pm.expect(d.user.userId).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('adminToken', d.accessToken);
});
```

## 29. 01 Manual workflows - OTP and invitations / 04 Admin approval / List applications

**GET** `{{userUrl}}/api/owner-applications`

**Before request script:**
```javascript
const requiredVariables = ["userUrl","adminToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d).to.be.an('array');
  pm.expect(d.some(a => a.id === pm.variables.get('ownerApplicationId'))).to.eql(true);
});
```

## 30. 01 Manual workflows - OTP and invitations / 04 Admin approval / Application detail

**GET** `{{userUrl}}/api/owner-applications/{{ownerApplicationId}}`

**Before request script:**
```javascript
const requiredVariables = ["userUrl","ownerApplicationId","adminToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.id).to.eql(pm.variables.get('ownerApplicationId'));
  pm.expect(d.lotSetupComplete).to.eql(true);
  pm.expect(d.contactVerified).to.eql(true);
});
```

## 31. 01 Manual workflows - OTP and invitations / 04 Admin approval / Approve after email verification

**PATCH** `{{userUrl}}/api/owner-applications/{{ownerApplicationId}}/review`

**Body (template):**
```json
{
  "status": "approved",
  "reviewNote": "QA confirmed email verification and completed lot setup"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","ownerApplicationId","adminToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.id).to.eql(pm.variables.get('ownerApplicationId'));
  pm.expect(d.status).to.eql('approved');
  pm.expect(d.lotSetupComplete).to.eql(true);
  pm.expect(d.deliveryId).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('deliveryId', d.deliveryId);
});
```

## 32. 01 Manual workflows - OTP and invitations / 05 Owner session / Request owner login OTP

**POST** `{{userUrl}}/api/auth/otp/request`

**Body (template):**
```json
{
  "contact": "{{ownerEmail}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","ownerEmail"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.challengeId).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('challengeId', d.challengeId);
});
```

## 33. 01 Manual workflows - OTP and invitations / 05 Owner session / Complete owner OTP login

**POST** `{{userUrl}}/api/auth/otp/login`

**Body (template):**
```json
{
  "challengeId": "{{challengeId}}",
  "code": "{{otp}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","challengeId","otp"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.environment.unset('ownerToken');
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Valid session for the expected role", () => {
  const result = pm.response.json();
  const d = result.data;
  pm.expect(result.mfaRequired === true, 'Complete /api/auth/mfa/login before expecting a session').to.eql(false);
  pm.expect(d.accessToken).to.be.a('string').and.not.empty;
  pm.expect(d.accessToken.split('.')).to.have.lengthOf(3);
  pm.expect(d.refreshToken).to.be.a('string').and.not.empty;
  pm.expect(d.tokenType).to.eql('Bearer');
  pm.expect(d.expiresIn).to.be.within(895, 900);
  pm.expect(d.user.role).to.eql('owner');
  pm.expect(d.user.userId).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('ownerToken', d.accessToken);
});
```

## 34. 01 Manual workflows - OTP and invitations / 05 Owner session / Password login owner

**POST** `{{userUrl}}/api/auth/login`

**Body (template):**
```json
{
  "email": "{{ownerEmail}}",
  "password": "{{ownerPassword}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","ownerEmail","ownerPassword"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.environment.unset('ownerToken');
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Valid session for the expected role", () => {
  const result = pm.response.json();
  const d = result.data;
  pm.expect(result.mfaRequired === true, 'Complete /api/auth/mfa/login before expecting a session').to.eql(false);
  pm.expect(d.accessToken).to.be.a('string').and.not.empty;
  pm.expect(d.accessToken.split('.')).to.have.lengthOf(3);
  pm.expect(d.refreshToken).to.be.a('string').and.not.empty;
  pm.expect(d.tokenType).to.eql('Bearer');
  pm.expect(d.expiresIn).to.be.within(895, 900);
  pm.expect(d.user.role).to.eql('owner');
  pm.expect(d.user.userId).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('ownerToken', d.accessToken);
});
```

## 35. 01 Manual workflows - OTP and invitations / 06 Parking Owner / List own lots

**GET** `{{facilityUrl}}/api/parking-lots`

**Before request script:**
```javascript
const requiredVariables = ["facilityUrl","ownerToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d).to.be.an('array');
});
```

## 36. 01 Manual workflows - OTP and invitations / 06 Parking Owner / Create lot (unique code per run)

**POST** `{{facilityUrl}}/api/parking-lots`

**Body (template):**
```json
{
  "code": "{{lotCode}}",
  "name": "QA Lot",
  "address": "QA address"
}
```

**Before request script:**
```javascript
const code = ('QA-' + pm.variables.replaceIn('{{$guid}}')).toUpperCase();
pm.environment.set('lotCode', code);
pm.variables.set('lotCode', code);
pm.environment.unset('siteId');
const requiredVariables = ["facilityUrl","ownerToken","lotCode"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 201', () => pm.response.to.have.status(201));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.code).to.eql(pm.variables.get('lotCode'));
  pm.expect(d.isActive).to.eql(true);
  pm.expect(d.id).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('siteId', d.id);
});
```

## 37. 01 Manual workflows - OTP and invitations / 06 Parking Owner / Read structure and capacity

**GET** `{{facilityUrl}}/api/parking-lots/{{siteId}}/structure`

**Before request script:**
```javascript
const requiredVariables = ["facilityUrl","siteId","ownerToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.site.id).to.eql(pm.variables.get('siteId'));
  pm.expect(d.units).to.be.an('array');
  pm.expect(d.slots).to.be.an('array');
  pm.expect(d.capacityViews).to.be.an('array');
});
```

## 38. 01 Manual workflows - OTP and invitations / 06 Parking Owner / Create outdoor zone

**POST** `{{facilityUrl}}/api/parking-lots/{{siteId}}/units`

**Body (template):**
```json
{
  "type": "ZONE",
  "name": "Zone A",
  "capacity": 10
}
```

**Before request script:**
```javascript
const requiredVariables = ["facilityUrl","siteId","ownerToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('unitId', d);
});
```

## 39. 01 Manual workflows - OTP and invitations / 06 Parking Owner / Create CAR slot

**POST** `{{facilityUrl}}/api/parking-lots/{{siteId}}/slots`

**Body (template):**
```json
{
  "unitId": "{{unitId}}",
  "code": "A-01",
  "vehicleType": "CAR",
  "type": "STANDARD"
}
```

**Before request script:**
```javascript
const requiredVariables = ["facilityUrl","siteId","ownerToken","unitId"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('slotId', d);
});
```

## 40. 01 Manual workflows - OTP and invitations / 06 Parking Owner / Create path

**POST** `{{facilityUrl}}/api/parking-lots/{{siteId}}/paths`

**Body (template):**
```json
{
  "code": "ENTRY-A",
  "to": "{{unitId}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["facilityUrl","siteId","ownerToken","unitId"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('pathId', d);
});
```

## 41. 01 Manual workflows - OTP and invitations / 06 Parking Owner / Rename unit

**PATCH** `{{facilityUrl}}/api/parking-lots/{{siteId}}/structure`

**Body (template):**
```json
{
  "action": "renameUnit",
  "resourceId": "{{unitId}}",
  "name": "Zone B"
}
```

**Before request script:**
```javascript
const requiredVariables = ["facilityUrl","siteId","ownerToken","unitId"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
```

## 42. 01 Manual workflows - OTP and invitations / 06 Parking Owner / Configure category backup

**PATCH** `{{facilityUrl}}/api/parking-lots/{{siteId}}/structure`

**Body (template):**
```json
{
  "action": "backup",
  "unitId": "{{unitId}}",
  "vehicleType": "CAR",
  "capacity": 1
}
```

**Before request script:**
```javascript
const requiredVariables = ["facilityUrl","siteId","ownerToken","unitId"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
```

## 43. 01 Manual workflows - OTP and invitations / 06 Parking Owner / Verify persisted structure changes

**GET** `{{facilityUrl}}/api/parking-lots/{{siteId}}/structure`

**Before request script:**
```javascript
const requiredVariables = ["facilityUrl","siteId","ownerToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.site.id).to.eql(pm.variables.get('siteId'));
  pm.expect(d.units.some(x => x.id === pm.variables.get('unitId') && x.name === 'Zone B')).to.eql(true);
  pm.expect(d.slots.some(x => x.id === pm.variables.get('slotId') && x.code === 'A-01')).to.eql(true);
  pm.expect(d.paths.some(x => x.id === pm.variables.get('pathId'))).to.eql(true);
  pm.expect(d.backupPolicies.some(x => x.unitId === pm.variables.get('unitId') && x.vehicleType === 'CAR' && x.count === 1)).to.eql(true);
});
```

## 44. 01 Manual workflows - OTP and invitations / 07 Operator provisioning / Owner creates Operator

**POST** `{{userUrl}}/api/users`

**Body (template):**
```json
{
  "fullName": "QA Operator",
  "email": "{{operatorEmail}}",
  "siteIds": [
    "{{siteId}}"
  ],
  "permissions": [
    "SLOT_OVERRIDE"
  ]
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","ownerToken","operatorEmail","siteId"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 201', () => pm.response.to.have.status(201));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.status).to.eql('pending_verification');
  pm.expect(d.siteIds).to.include(pm.variables.get('siteId'));
  pm.expect(d.permissions).to.include('SLOT_OVERRIDE');
  pm.expect(d.id).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.expect(d.deliveryId).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('operatorId', d.id);
  pm.environment.set('deliveryId', d.deliveryId);
});
```

## 45. 01 Manual workflows - OTP and invitations / 07 Operator provisioning / Owner delivery status

**GET** `{{userUrl}}/api/auth/deliveries/{{deliveryId}}`

**Before request script:**
```javascript
const requiredVariables = ["userUrl","deliveryId","ownerToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.id).to.eql(pm.variables.get('deliveryId'));
  pm.expect(d.status).to.be.oneOf(['pending', 'sent', 'failed', 'cancelled']);
});
```

## 46. 01 Manual workflows - OTP and invitations / 07 Operator provisioning / Retry failed delivery

**POST** `{{userUrl}}/api/auth/deliveries/{{deliveryId}}/retry`

**Before request script:**
```javascript
const requiredVariables = ["userUrl","deliveryId","ownerToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.id).to.eql(pm.variables.get('deliveryId'));
  pm.expect(d.status).to.be.oneOf(['pending', 'sent', 'failed', 'cancelled']);
});
```

## 47. 01 Manual workflows - OTP and invitations / 07 Operator provisioning / Reissue pending Operator invitation - optional

**POST** `{{userUrl}}/api/operators/{{operatorId}}/invitation`

**Before request script:**
```javascript
const requiredVariables = ["userUrl","operatorId","ownerToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.challengeId).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('bootstrapChallenge', d.challengeId);
});
```

## 48. 01 Manual workflows - OTP and invitations / 07 Operator provisioning / Activate Operator from emailed invitation - required before login

**POST** `{{userUrl}}/api/auth/bootstrap-password`

**Body (template):**
```json
{
  "challengeId": "{{bootstrapChallenge}}",
  "token": "{{bootstrapToken}}",
  "password": "{{operatorPassword}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","bootstrapChallenge","bootstrapToken","operatorPassword"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
```

## 49. 01 Manual workflows - OTP and invitations / 08 Operator session / Request operator login OTP

**POST** `{{userUrl}}/api/auth/otp/request`

**Body (template):**
```json
{
  "contact": "{{operatorEmail}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","operatorEmail"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.challengeId).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('challengeId', d.challengeId);
});
```

## 50. 01 Manual workflows - OTP and invitations / 08 Operator session / Complete operator OTP login

**POST** `{{userUrl}}/api/auth/otp/login`

**Body (template):**
```json
{
  "challengeId": "{{challengeId}}",
  "code": "{{otp}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","challengeId","otp"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.environment.unset('operatorToken');
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Valid session for the expected role", () => {
  const result = pm.response.json();
  const d = result.data;
  pm.expect(result.mfaRequired === true, 'Complete /api/auth/mfa/login before expecting a session').to.eql(false);
  pm.expect(d.accessToken).to.be.a('string').and.not.empty;
  pm.expect(d.accessToken.split('.')).to.have.lengthOf(3);
  pm.expect(d.refreshToken).to.be.a('string').and.not.empty;
  pm.expect(d.tokenType).to.eql('Bearer');
  pm.expect(d.expiresIn).to.be.within(895, 900);
  pm.expect(d.user.role).to.eql('operator');
  pm.expect(d.user.userId).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('operatorToken', d.accessToken);
});
```

## 51. 01 Manual workflows - OTP and invitations / 08 Operator session / Password login operator

**POST** `{{userUrl}}/api/auth/login`

**Body (template):**
```json
{
  "email": "{{operatorEmail}}",
  "password": "{{operatorPassword}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","operatorEmail","operatorPassword"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.environment.unset('operatorToken');
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Valid session for the expected role", () => {
  const result = pm.response.json();
  const d = result.data;
  pm.expect(result.mfaRequired === true, 'Complete /api/auth/mfa/login before expecting a session').to.eql(false);
  pm.expect(d.accessToken).to.be.a('string').and.not.empty;
  pm.expect(d.accessToken.split('.')).to.have.lengthOf(3);
  pm.expect(d.refreshToken).to.be.a('string').and.not.empty;
  pm.expect(d.tokenType).to.eql('Bearer');
  pm.expect(d.expiresIn).to.be.within(895, 900);
  pm.expect(d.user.role).to.eql('operator');
  pm.expect(d.user.userId).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('operatorToken', d.accessToken);
});
```

## 52. 01 Manual workflows - OTP and invitations / 08 Operator session / Current assignments

**GET** `{{userUrl}}/api/operators/me`

**Before request script:**
```javascript
const requiredVariables = ["userUrl","operatorToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d).to.be.an('array');
  pm.expect(d.some(x => x.siteId === pm.variables.get('siteId'))).to.eql(true);
});
```

## 53. 01 Manual workflows - OTP and invitations / 08 Operator session / Current slot scope

**GET** `{{userUrl}}/api/operators/slot-scope?siteId={{siteId}}`

**Before request script:**
```javascript
const requiredVariables = ["userUrl","siteId","operatorToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test("Unwrapped authoritative slot scope", () => {
  const d = pm.response.json();
  pm.expect(d.userId).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.expect(d.tenantIds).to.be.an('array').that.is.not.empty;
});
```

## 54. 01 Manual workflows - OTP and invitations / 08 Operator session / Assigned operational layout

**GET** `{{facilityUrl}}/api/parking-lots/{{siteId}}/operational-layout`

**Before request script:**
```javascript
const requiredVariables = ["facilityUrl","siteId","operatorToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.site.id).to.eql(pm.variables.get('siteId'));
  pm.expect(d.slots.some(x => x.id === pm.variables.get('slotId'))).to.eql(true);
});
```

## 55. 01 Manual workflows - OTP and invitations / 08 Operator session / Mark eligible slot BACKUP

**POST** `{{facilityUrl}}/api/parking-lots/{{siteId}}/slots/{{slotId}}/backup`

**Body (template):**
```json
{
  "reason": "QA operational backup"
}
```

**Before request script:**
```javascript
const requiredVariables = ["facilityUrl","siteId","slotId","operatorToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
```

## 56. 01 Manual workflows - OTP and invitations / 09 Vehicles / Own list

**GET** `{{userUrl}}/api/vehicles`

**Before request script:**
```javascript
const requiredVariables = ["userUrl","driverToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
```

## 57. 01 Manual workflows - OTP and invitations / 09 Vehicles / Create vehicle

**POST** `{{userUrl}}/api/vehicles`

**Body (template):**
```json
{
  "plate": "{{vehiclePlate}}",
  "vehicleType": "CAR",
  "imageReference": "https://qa-image-store.example.test/vehicle.jpg"
}
```

**Before request script:**
```javascript
const digits = String(Math.floor(Math.random() * 100000)).padStart(5, '0');
const plate = '51A-' + digits;
pm.environment.set('vehiclePlate', plate);
pm.variables.set('vehiclePlate', plate);
pm.environment.unset('vehicleId');
const requiredVariables = ["userUrl","driverToken","vehiclePlate"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 201', () => pm.response.to.have.status(201));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.canonicalPlate).to.eql(pm.variables.get('vehiclePlate').toUpperCase().replace(/[^A-Z0-9]/g, ''));
  pm.expect(d.vehicleType).to.eql('CAR');
  pm.expect(d.id).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('vehicleId', d.id);
});
```

## 58. 01 Manual workflows - OTP and invitations / 09 Vehicles / Update vehicle

**PATCH** `{{userUrl}}/api/vehicles/{{vehicleId}}`

**Body (template):**
```json
{
  "plate": "{{vehiclePlate}}",
  "vehicleType": "CAR",
  "imageReference": "https://qa-image-store.example.test/updated.jpg"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","vehicleId","driverToken","vehiclePlate"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.id).to.eql(pm.variables.get('vehicleId'));
  pm.expect(d.canonicalPlate).to.eql(pm.variables.get('vehiclePlate').toUpperCase().replace(/[^A-Z0-9]/g, ''));
  pm.expect(d.vehicleType).to.eql('CAR');
  pm.expect(d.imageReference).to.eql('https://qa-image-store.example.test/updated.jpg');
});
```

## 59. 01 Manual workflows - OTP and invitations / 09 Vehicles / Verify vehicle persisted after update

**GET** `{{userUrl}}/api/vehicles`

**Before request script:**
```javascript
const requiredVariables = ["userUrl","driverToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d).to.be.an('array');
  pm.expect(d.some(x => x.id === pm.variables.get('vehicleId') && x.imageReference === 'https://qa-image-store.example.test/updated.jpg')).to.eql(true);
});
```

## 60. 01 Manual workflows - OTP and invitations / 10 Optional MFA / Setup Driver MFA

**POST** `{{userUrl}}/api/auth/mfa/setup`

**Body (template):**
```json
{
  "password": "{{driverPassword}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","driverToken","driverPassword"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.secret).to.be.a('string').and.not.empty;
  pm.expect(d.uri).to.be.a('string').and.include('otpauth://');
});
```

## 61. 01 Manual workflows - OTP and invitations / 10 Optional MFA / Enable Driver MFA

**POST** `{{userUrl}}/api/auth/mfa/enable`

**Body (template):**
```json
{
  "code": "{{totp}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","driverToken","totp"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.recoveryCodes).to.be.an('array').that.is.not.empty;
  pm.expect(d.signInRequired).to.eql(false);
});
```

## 62. 01 Manual workflows - OTP and invitations / 10 Optional MFA / Request MFA login OTP

**POST** `{{userUrl}}/api/auth/otp/request`

**Body (template):**
```json
{
  "contact": "{{driverEmail}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","driverEmail"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.challengeId).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('challengeId', d.challengeId);
});
```

## 63. 01 Manual workflows - OTP and invitations / 10 Optional MFA / OTP login with fresh TOTP

**POST** `{{userUrl}}/api/auth/otp/login`

**Body (template):**
```json
{
  "challengeId": "{{challengeId}}",
  "code": "{{otp}}",
  "totp": "{{totp}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","challengeId","otp","totp"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.environment.unset('driverToken');
pm.environment.unset('driverRefresh');
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Valid session for the expected role", () => {
  const result = pm.response.json();
  const d = result.data;
  pm.expect(result.mfaRequired === true, 'Complete /api/auth/mfa/login before expecting a session').to.eql(false);
  pm.expect(d.accessToken).to.be.a('string').and.not.empty;
  pm.expect(d.accessToken.split('.')).to.have.lengthOf(3);
  pm.expect(d.refreshToken).to.be.a('string').and.not.empty;
  pm.expect(d.tokenType).to.eql('Bearer');
  pm.expect(d.expiresIn).to.be.within(895, 900);
  pm.expect(d.user.role).to.eql('driver');
  pm.expect(d.user.userId).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('driverToken', d.accessToken);
  pm.environment.set('driverRefresh', d.refreshToken);
});
```

## 64. 01 Manual workflows - OTP and invitations / 11 Destructive operation - isolated fixture / Create disposable unmarked slot for removal

**POST** `{{facilityUrl}}/api/parking-lots/{{siteId}}/slots`

**Body (template):**
```json
{
  "unitId": "{{unitId}}",
  "code": "{{destructiveSlotCode}}",
  "vehicleType": "CAR",
  "type": "STANDARD"
}
```

**Before request script:**
```javascript
const code = 'REMOVE-' + pm.variables.replaceIn('{{$guid}}').slice(0, 8);
pm.environment.set('destructiveSlotCode', code);
pm.variables.set('destructiveSlotCode', code);
pm.environment.set('operationKey', pm.variables.replaceIn('{{$guid}}'));
pm.environment.unset('destructiveSlotId');
pm.environment.unset('operationId');
const requiredVariables = ["facilityUrl","siteId","ownerToken","unitId","destructiveSlotCode"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.environment.set('destructiveSlotId', d);
});
```

## 65. 01 Manual workflows - OTP and invitations / 11 Destructive operation - isolated fixture / Remove fixture slot idempotently

**POST** `{{facilityUrl}}/api/parking-lots/{{siteId}}/operations`

**Body (template):**
```json
{
  "idempotencyKey": "{{operationKey}}",
  "edit": {
    "action": "removeSlot",
    "resourceId": "{{destructiveSlotId}}"
  }
}
```

**Before request script:**
```javascript
const requiredVariables = ["facilityUrl","siteId","ownerToken","operationKey","destructiveSlotId"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.environment.unset('operationId');
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.id).to.match(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  pm.expect(d.siteId).to.eql(pm.variables.get('siteId'));
  pm.environment.set('operationId', d.id);
});
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.status, 'Operation must complete; pending/failed is not a passing happy path').to.eql('completed');
});
```

## 66. 01 Manual workflows - OTP and invitations / 11 Destructive operation - isolated fixture / Read authoritative operation status

**GET** `{{facilityUrl}}/api/parking-lots/{{siteId}}/operations/{{operationId}}`

**Before request script:**
```javascript
const requiredVariables = ["facilityUrl","siteId","operationId","ownerToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.id).to.eql(pm.variables.get('operationId'));
  pm.expect(d.status).to.eql('completed');
});
```

## 67. 01 Manual workflows - OTP and invitations / 11 Destructive operation - isolated fixture / Replay completed removal with the same idempotency key

**POST** `{{facilityUrl}}/api/parking-lots/{{siteId}}/operations`

**Body (template):**
```json
{
  "idempotencyKey": "{{operationKey}}",
  "edit": {
    "action": "removeSlot",
    "resourceId": "{{destructiveSlotId}}"
  }
}
```

**Before request script:**
```javascript
const requiredVariables = ["facilityUrl","siteId","ownerToken","operationKey","destructiveSlotId"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.id).to.eql(pm.variables.get('operationId'));
  pm.expect(d.status).to.eql('completed');
});
```

## 68. 01 Manual workflows - OTP and invitations / 11 Destructive operation - isolated fixture / Verify disposable slot was removed

**GET** `{{facilityUrl}}/api/parking-lots/{{siteId}}/structure`

**Before request script:**
```javascript
const requiredVariables = ["facilityUrl","siteId","ownerToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
pm.test("Response data matches the request", () => {
  const d = pm.response.json().data;
  pm.expect(d.site.id).to.eql(pm.variables.get('siteId'));
  pm.expect(d.slots).to.be.an('array');
  pm.expect(d.slots.some(x => x.id === pm.variables.get('destructiveSlotId'))).to.eql(false);
  pm.expect(d.slots.some(x => x.id === pm.variables.get('slotId'))).to.eql(true);
});
```

## 69. 01 Manual workflows - OTP and invitations / 12 Logout last / Logout current Driver session

**POST** `{{userUrl}}/api/auth/logout`

**Body (template):**
```json
{
  "refreshToken": "{{driverRefresh}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","driverToken","driverRefresh"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', () => pm.response.to.have.status(200));
pm.test('Successful response envelope', () => pm.expect(pm.response.json().success).to.eql(true));
```

## 70. 01 Manual workflows - OTP and invitations / 12 Logout last / Old access token denied

**GET** `{{userUrl}}/api/auth/me`

**Before request script:**
```javascript
const requiredVariables = ["userUrl","driverToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 401', () => pm.response.to.have.status(401));
```

## 71. 01 Manual workflows - OTP and invitations / 12 Logout last / Old refresh denied

**POST** `{{userUrl}}/api/auth/refresh`

**Body (template):**
```json
{
  "refreshToken": "{{driverRefresh}}"
}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","driverRefresh"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 401', () => pm.response.to.have.status(401));
```

## 72. 02 Payment pricing - Development / 01 - Pricing Calculator / Calculate pricing - Policy override with Admin token (Happy Path)

**POST** `{{paymentUrl}}/api/v1/pricing/calculate`

**Body (template):**
```json
{
  "tenantId": "{{$guid}}",
  "siteId": "{{$guid}}",
  "vehicleType": "CAR",
  "entryTime": "{{calcEntryTime}}",
  "exitTime": "{{calcExitTime}}",
  "policyOverride": {
    "tariffId": "{{$guid}}",
    "tariffName": "Biểu giá Ô tô Tiêu chuẩn 2026",
    "policyVersion": 1,
    "vehicleType": "CAR",
    "freeParkingGraceMinutes": 15,
    "timeBlocks": [
      {
        "blockCode": "DAY_SHIFT",
        "startTime": "06:00:00",
        "endTime": "18:00:00",
        "ratePerHourVnd": 20000
      },
      {
        "blockCode": "NIGHT_SHIFT",
        "startTime": "18:00:00",
        "endTime": "06:00:00",
        "ratePerHourVnd": 30000
      }
    ],
    "timeZoneId": "Asia/Ho_Chi_Minh",
    "roundingMode": "HALF_UP_WHOLE_VND"
  }
}
```

**Before request script:**
```javascript
const now = new Date();
const entry = new Date(now.getTime() - 2 * 60 * 60 * 1000); // 2 giờ trước
pm.environment.set('calcEntryTime', entry.toISOString());
pm.environment.set('calcExitTime', now.toISOString());
const requiredVariables = ["paymentAdminToken","calcEntryTime","calcExitTime","paymentUrl"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Status code is 200 OK', () => pm.response.to.have.status(200));
pm.test('Content-Type is application/json', () => pm.expect(pm.response.headers.get('Content-Type')).to.include('application/json'));
const json = pm.response.json();
pm.test('Response envelope has success=true and data', () => {
    pm.expect(json).to.have.property('success', true);
    pm.expect(json).to.have.property('data');
});
pm.test('Financial calculation fields are valid', () => {
    const data = json.data;
    pm.expect(data.currency).to.eql('VND');
    pm.expect(data.finalAmountVnd).to.be.a('number');
    pm.expect(data.finalAmountVnd).to.be.at.least(0);
    pm.expect(data.totalParkedMinutes).to.be.a('number');
    pm.expect(data.invoiceRoundedMinutes).to.be.a('number');
    pm.expect(data.roundingMode).to.be.a('string');
    pm.expect(data.segments).to.be.an('array');
    pm.expect(data.segments.length).to.be.at.least(1);
});
```

## 73. 02 Payment pricing - Development / 01 - Pricing Calculator / Calculate pricing - Free parking stay within grace period (Happy Path)

**POST** `{{paymentUrl}}/api/v1/pricing/calculate`

**Body (template):**
```json
{
  "tenantId": "{{$guid}}",
  "siteId": "{{$guid}}",
  "vehicleType": "CAR",
  "entryTime": "{{freeEntryTime}}",
  "exitTime": "{{freeExitTime}}",
  "policyOverride": {
    "tariffId": "{{$guid}}",
    "tariffName": "Biểu giá Ô tô Tiêu chuẩn 2026",
    "policyVersion": 1,
    "vehicleType": "CAR",
    "freeParkingGraceMinutes": 15,
    "timeBlocks": [
      {
        "blockCode": "DAY_SHIFT",
        "startTime": "06:00:00",
        "endTime": "18:00:00",
        "ratePerHourVnd": 20000
      },
      {
        "blockCode": "NIGHT_SHIFT",
        "startTime": "18:00:00",
        "endTime": "06:00:00",
        "ratePerHourVnd": 30000
      }
    ],
    "timeZoneId": "Asia/Ho_Chi_Minh",
    "roundingMode": "HALF_UP_WHOLE_VND"
  }
}
```

**Before request script:**
```javascript
const now = new Date();
const entry = new Date(now.getTime() - 10 * 60 * 1000); // 10 phút trước (<= 15 phút grace)
pm.environment.set('freeEntryTime', entry.toISOString());
pm.environment.set('freeExitTime', now.toISOString());
const requiredVariables = ["paymentAdminToken","freeEntryTime","freeExitTime","paymentUrl"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Status code is 200 OK', () => pm.response.to.have.status(200));
const json = pm.response.json();
pm.test('Stay within grace period should be free (0 VND)', () => {
    pm.expect(json.success).to.eql(true);
    pm.expect(json.data.isFreeStay).to.eql(true);
    pm.expect(json.data.finalAmountVnd).to.eql(0);
    pm.expect(json.data.billedMinutes).to.eql(0);
    pm.expect(json.data.segments.length).to.eql(0);
});
```

## 74. 02 Payment pricing - Development / 01 - Pricing Calculator / Calculate pricing - Motorcycle vehicle type (Happy Path)

**POST** `{{paymentUrl}}/api/v1/pricing/calculate`

**Body (template):**
```json
{
  "tenantId": "{{$guid}}",
  "siteId": "{{$guid}}",
  "vehicleType": "MOTORCYCLE",
  "entryTime": "{{calcEntryTime}}",
  "exitTime": "{{calcExitTime}}",
  "policyOverride": {
    "tariffId": "{{$guid}}",
    "tariffName": "Biểu giá Xe máy Tiêu chuẩn",
    "policyVersion": 1,
    "vehicleType": "MOTORCYCLE",
    "freeParkingGraceMinutes": 15,
    "timeBlocks": [
      {
        "blockCode": "DAY_SHIFT",
        "startTime": "06:00:00",
        "endTime": "18:00:00",
        "ratePerHourVnd": 5000
      },
      {
        "blockCode": "NIGHT_SHIFT",
        "startTime": "18:00:00",
        "endTime": "06:00:00",
        "ratePerHourVnd": 10000
      }
    ],
    "timeZoneId": "Asia/Ho_Chi_Minh",
    "roundingMode": "HALF_UP_WHOLE_VND"
  }
}
```

**Before request script:**
```javascript
const now = new Date();
const entry = new Date(now.getTime() - 90 * 60 * 1000); // 1.5 giờ trước
pm.environment.set('calcEntryTime', entry.toISOString());
pm.environment.set('calcExitTime', now.toISOString());
const requiredVariables = ["paymentAdminToken","calcEntryTime","calcExitTime","paymentUrl"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Status code is 200 OK', () => pm.response.to.have.status(200));
const json = pm.response.json();
pm.test('Motorcycle calculation succeeds', () => {
    pm.expect(json.success).to.eql(true);
    pm.expect(json.data.finalAmountVnd).to.be.a('number');
});
```

## 75. 02 Payment pricing - Development / 01 - Pricing Calculator / Calculate pricing - Missing required fields (Expected 400)

**POST** `{{paymentUrl}}/api/v1/pricing/calculate`

**Body (template):**
```json
{
  "tenantId": "00000000-0000-0000-0000-000000000000",
  "siteId": "00000000-0000-0000-0000-000000000000",
  "vehicleType": "",
  "entryTime": "2026-10-15T08:00:00+07:00",
  "exitTime": "2026-10-15T10:00:00+07:00"
}
```

**Before request script:**
```javascript
const requiredVariables = ["paymentUserToken","paymentUrl"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Status code is 400 Bad Request', () => pm.response.to.have.status(400));
const json = pm.response.json();
pm.test('Validation error envelope structure is correct', () => {
    pm.expect(json.success).to.eql(false);
    pm.expect(json.errorCode).to.eql('VALIDATION_ERROR');
    pm.expect(json.errors).to.be.an('array');
    pm.expect(json.errors.length).to.be.at.least(1);
});
```

## 76. 02 Payment pricing - Development / 01 - Pricing Calculator / Calculate pricing - Unsupported vehicle type (Expected 400)

**POST** `{{paymentUrl}}/api/v1/pricing/calculate`

**Body (template):**
```json
{
  "tenantId": "{{$guid}}",
  "siteId": "{{$guid}}",
  "vehicleType": "TRUCK",
  "entryTime": "2026-10-15T08:00:00+07:00",
  "exitTime": "2026-10-15T10:00:00+07:00"
}
```

**Before request script:**
```javascript
const requiredVariables = ["paymentUserToken","paymentUrl"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Status code is 400 Bad Request', () => pm.response.to.have.status(400));
const json = pm.response.json();
pm.test('Returns UNSUPPORTED_VEHICLE_TYPE error', () => {
    pm.expect(json.success).to.eql(false);
    pm.expect(json.errorCode).to.eql('VALIDATION_ERROR');
    const hasVehicleTypeError = json.errors.some(e => e.field === 'VehicleType' || e.code === 'UNSUPPORTED_VEHICLE_TYPE');
    pm.expect(hasVehicleTypeError).to.eql(true);
});
```

## 77. 02 Payment pricing - Development / 01 - Pricing Calculator / Calculate pricing - ExitTime before EntryTime (Expected 400)

**POST** `{{paymentUrl}}/api/v1/pricing/calculate`

**Body (template):**
```json
{
  "tenantId": "{{$guid}}",
  "siteId": "{{$guid}}",
  "vehicleType": "CAR",
  "entryTime": "2026-10-15T12:00:00+07:00",
  "exitTime": "2026-10-15T08:00:00+07:00"
}
```

**Before request script:**
```javascript
const requiredVariables = ["paymentUserToken","paymentUrl"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Status code is 400 Bad Request', () => pm.response.to.have.status(400));
const json = pm.response.json();
pm.test('Returns INVALID_TIME_RANGE error', () => {
    pm.expect(json.success).to.eql(false);
    pm.expect(json.errorCode).to.eql('VALIDATION_ERROR');
    const hasTimeError = json.errors.some(e => e.field === 'ExitTime' || e.code === 'INVALID_TIME_RANGE');
    pm.expect(hasTimeError).to.eql(true);
});
```

## 78. 02 Payment pricing - Development / 01 - Pricing Calculator / Calculate pricing - Policy override by unauthorized regular user (Expected 400)

**POST** `{{paymentUrl}}/api/v1/pricing/calculate`

**Body (template):**
```json
{
  "tenantId": "{{$guid}}",
  "siteId": "{{$guid}}",
  "vehicleType": "CAR",
  "entryTime": "2026-10-15T08:00:00+07:00",
  "exitTime": "2026-10-15T10:00:00+07:00",
  "policyOverride": {
    "tariffId": "{{$guid}}",
    "tariffName": "Unauthorized Override",
    "policyVersion": 1,
    "vehicleType": "CAR",
    "freeParkingGraceMinutes": 15,
    "timeBlocks": [
      {
        "blockCode": "DAY_SHIFT",
        "startTime": "00:00:00",
        "endTime": "00:00:00",
        "ratePerHourVnd": 0
      }
    ],
    "timeZoneId": "Asia/Ho_Chi_Minh",
    "roundingMode": "HALF_UP_WHOLE_VND"
  }
}
```

**Before request script:**
```javascript
const requiredVariables = ["paymentUserToken","paymentUrl"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Status code is 400 Bad Request', () => pm.response.to.have.status(400));
const json = pm.response.json();
pm.test('Returns FORBIDDEN validation error for regular user override', () => {
    pm.expect(json.success).to.eql(false);
    pm.expect(json.errorCode).to.eql('VALIDATION_ERROR');
    const hasForbiddenError = json.errors.some(e => e.field === 'PolicyOverride' || e.code === 'FORBIDDEN');
    pm.expect(hasForbiddenError).to.eql(true);
});
```

## 79. 02 Payment pricing - Development / 01 - Pricing Calculator / Calculate pricing - No token provided (Expected 401)

**POST** `{{paymentUrl}}/api/v1/pricing/calculate`

**Body (template):**
```json
{
  "tenantId": "{{$guid}}",
  "siteId": "{{$guid}}",
  "vehicleType": "CAR",
  "entryTime": "2026-10-15T08:00:00+07:00",
  "exitTime": "2026-10-15T10:00:00+07:00"
}
```

**Before request script:**
```javascript
const requiredVariables = ["paymentUrl"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Status code is 401 Unauthorized', () => pm.response.to.have.status(401));
```

## 80. 02 Payment pricing - Development / 01 - Pricing Calculator / Calculate pricing - Non-existent walk-in tariff (Expected 404)

**POST** `{{paymentUrl}}/api/v1/pricing/calculate`

**Body (template):**
```json
{
  "tenantId": "{{$guid}}",
  "siteId": "{{$guid}}",
  "vehicleType": "CAR",
  "entryTime": "2026-10-15T08:00:00+07:00",
  "exitTime": "2026-10-15T10:00:00+07:00"
}
```

**Before request script:**
```javascript
const requiredVariables = ["paymentUserToken","paymentUrl"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 404; unavailable DB is not a passing case', () => pm.response.to.have.status(404));
pm.test("Missing tariff error", () => {
  const json = pm.response.json();
  pm.expect(json.success).to.eql(false);
  pm.expect(json.errorCode).to.eql('TARIFF_NOT_FOUND');
});
```

## 81. 02 Payment pricing - Development / 01 - Pricing Calculator / Calculate pricing - Missing reservation snapshot (Expected 422)

**POST** `{{paymentUrl}}/api/v1/pricing/calculate`

**Body (template):**
```json
{
  "tenantId": "11111111-1111-1111-1111-111111111111",
  "siteId": "22222222-2222-2222-2222-222222222222",
  "vehicleType": "CAR",
  "reservationId": "00000000-0000-0000-0000-000000000000",
  "entryTime": "2026-10-15T08:00:00+07:00",
  "exitTime": "2026-10-15T10:00:00+07:00"
}
```

**Before request script:**
```javascript
const requiredVariables = ["paymentUserToken","paymentUrl"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 422; unavailable dependency is not a passing case', () => pm.response.to.have.status(422));
pm.test("Missing snapshot error", () => {
  const json = pm.response.json();
  pm.expect(json.success).to.eql(false);
  pm.expect(json.errorCode).to.eql('CORRUPTED_POLICY_SNAPSHOT');
});
```

## 82. 03 Authorization audit - prepared fixtures / Anonymous list

**GET** `{{userUrl}}/api/owner-applications`

**Before request script:**
```javascript
const requiredVariables = ["userUrl"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 401', function () { pm.response.to.have.status(401); });
```

## 83. 03 Authorization audit - prepared fixtures / Anonymous detail

**GET** `{{userUrl}}/api/owner-applications/00000000-0000-0000-0000-000000000000`

**Before request script:**
```javascript
const requiredVariables = ["userUrl"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 401', function () { pm.response.to.have.status(401); });
```

## 84. 03 Authorization audit - prepared fixtures / Anonymous review denied

**PATCH** `{{userUrl}}/api/owner-applications/00000000-0000-0000-0000-000000000000/review`

**Body (template):**
```json
{"status":"approved","reviewNote":"SPARK-197 read-only authorization check"}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 401', function () { pm.response.to.have.status(401); });
```

## 85. 03 Authorization audit - prepared fixtures / Owner list denied

**GET** `{{userUrl}}/api/owner-applications`

**Before request script:**
```javascript
const requiredVariables = ["userUrl","ownerToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 403', function () { pm.response.to.have.status(403); });
```

## 86. 03 Authorization audit - prepared fixtures / Owner detail denied

**GET** `{{userUrl}}/api/owner-applications/00000000-0000-0000-0000-000000000000`

**Before request script:**
```javascript
const requiredVariables = ["userUrl","ownerToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 403', function () { pm.response.to.have.status(403); });
```

## 87. 03 Authorization audit - prepared fixtures / Owner review denied

**PATCH** `{{userUrl}}/api/owner-applications/00000000-0000-0000-0000-000000000000/review`

**Body (template):**
```json
{"status":"approved","reviewNote":"SPARK-197 read-only authorization check"}
```

**Before request script:**
```javascript
const requiredVariables = ["userUrl","ownerToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 403', function () { pm.response.to.have.status(403); });
```

## 88. 03 Authorization audit - prepared fixtures / Admin list

**GET** `{{userUrl}}/api/owner-applications`

**Before request script:**
```javascript
const requiredVariables = ["userUrl","adminToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', function () { pm.response.to.have.status(200); });
const body = pm.response.json();
const apps = Array.isArray(body.data) ? body.data : [];
const a = apps.find(x => x.businessName === "SPARK197 QA Parking A");
const b = apps.find(x => x.businessName === "SPARK197 QA Parking B");
pm.test("Approved Owner A and Rejected Owner B retained", function () {
    pm.expect(a, "Owner A exists").to.exist;
    pm.expect(b, "Owner B exists").to.exist;
    pm.expect(a.status).to.eql("approved");
    pm.expect(b.status).to.eql("rejected");
});
if (a) pm.environment.set("auditOwnerAId", a.id);
if (b) pm.environment.set("auditOwnerBId", b.id);
```

## 89. 03 Authorization audit - prepared fixtures / Admin detail A

**GET** `{{userUrl}}/api/owner-applications/{{auditOwnerAId}}`

**Before request script:**
```javascript
const requiredVariables = ["userUrl","auditOwnerAId","adminToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', function () { pm.response.to.have.status(200); });
pm.test('Status is approved', function () { pm.expect(pm.response.json().data.status).to.eql('approved'); });
pm.test("Review metadata recorded", function () { const d=pm.response.json().data; pm.expect(d.reviewedBy).to.exist; pm.expect(d.reviewedAt).to.exist; });
pm.test('Notification delivery sent (prepare delivered fixtures first)', function () { pm.expect(pm.response.json().data.deliveryStatus).to.eql('sent'); });
```

## 90. 03 Authorization audit - prepared fixtures / Admin detail B

**GET** `{{userUrl}}/api/owner-applications/{{auditOwnerBId}}`

**Before request script:**
```javascript
const requiredVariables = ["userUrl","auditOwnerBId","adminToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 200', function () { pm.response.to.have.status(200); });
pm.test('Status is rejected', function () { pm.expect(pm.response.json().data.status).to.eql('rejected'); });
pm.test("Review metadata recorded", function () { const d=pm.response.json().data; pm.expect(d.reviewedBy).to.exist; pm.expect(d.reviewedAt).to.exist; });
pm.test('Notification delivery sent (prepare delivered fixtures first)', function () { pm.expect(pm.response.json().data.deliveryStatus).to.eql('sent'); });
```

## 91. 03 Authorization audit - prepared fixtures / Admin nonexistent ID

**GET** `{{userUrl}}/api/owner-applications/00000000-0000-0000-0000-000000000000`

**Before request script:**
```javascript
const requiredVariables = ["userUrl","adminToken"];
const missingVariables = requiredVariables.filter(key => { const value = pm.variables.get(key); return value === undefined || value === null || String(value).trim() === '' || /{{/.test(String(value)); });
if (missingVariables.length) { if (pm.execution && typeof pm.execution.skipRequest === 'function') pm.execution.skipRequest(); throw new Error('Set variables or run prerequisite requests first: ' + missingVariables.join(', ')); }
```

**After response script:**
```javascript
pm.test('Expected HTTP 404', function () { pm.response.to.have.status(404); });
```
