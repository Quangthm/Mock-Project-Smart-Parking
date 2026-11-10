// Workflow checks with synthetic responses. These do not prove live API/security behavior.
const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const root = path.resolve(__dirname, '../../docs/04-testing/postman');
const collection = JSON.parse(fs.readFileSync(path.join(root, 'SPARK-327-JWT-JWKS.postman_collection.json'), 'utf8'));
const template = JSON.parse(fs.readFileSync(path.join(root, 'SPARK-327-Local.postman_environment.example.json'), 'utf8'));
const pre = collection.event.find(e => e.listen === 'prerequest').script.exec.join('\n');
const post = collection.event.find(e => e.listen === 'test').script.exec.join('\n');
const names = JSON.parse(collection.variable.find(v => v.key === 'sp_request_names').value);
const userId = 'aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa';
const accountId = 'bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb';
const okProfile = {success:true, data:{userId, role:'driver'}};
const rejected = {success:false, code:'INVALID_TOKEN'};
const key = {kty:'RSA', kid:'smartpark-local-01', use:'sig', alg:'RS256', n:Buffer.alloc(384, 193).toString('base64url'), e:'AQAB'};

function harness() {
    const env = new Map(template.values.map(v => [v.key, v.value]));
    env.set('sp_email', 'fixture@example.invalid'); env.set('sp_password', 'fixture-only-password');
    const local = new Map();
    let now = Date.parse('2026-11-05T08:00:00Z');
    class Clock extends Date {
        constructor(...args) { super(...(args.length ? args : [now])); }
        static now() { return now; }
    }
    const scope = map => ({get:k=>map.get(k), set:(k,v)=>map.set(k,v), unset:k=>map.delete(k)});
    function run(step, body = {status:'ok'}, code = 200) {
        const result = {next:undefined, skipped:false, failures:[], body:undefined, bearer:undefined};
        const headers = new Map([['authorization', 'Bearer stale-request-token']]);
        const pm = {
            environment:scope(env), variables:scope(local), collectionVariables:{get:k=>collection.variable.find(v=>v.key===k)?.value},
            info:{requestName:names[step]},
            request:{headers:{get:key=>headers.get(key.toLowerCase()), remove:key=>headers.delete(key.toLowerCase()),
                upsert:({key,value})=>headers.set(key.toLowerCase(),value)}},
            execution:{setNextRequest:value=>{result.next=value;}, skipRequest:()=>{result.skipped=true; throw new Error('SKIPPED');}},
            response:{code, json:()=>body, headers:{get:name=>({'content-type':'application/jwk-set+json','cache-control':'public, max-age=300'})[name.toLowerCase()]}},
            test:(name,fn)=>{try { fn(); } catch { result.failures.push(name); }},
            visualizer:{set:()=>{}}
        };
        const context = () => ({pm, require:name=>{assert.equal(name,'buffer'); return {Buffer};}, Date:Clock});
        try { vm.runInNewContext(pre, context(), {timeout:1000}); }
        catch (e) { if (!result.skipped) throw e; }
        result.body = local.get('sp_request_body'); result.bearer = headers.get('authorization')?.replace(/^Bearer /,'');
        if (!result.skipped) vm.runInNewContext(post, context(), {timeout:1000});
        return result;
    }
    function session(serial) {
        const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');
        const token = [encode({alg:'RS256',kid:key.kid}), encode({iss:'SmartParking.UserService',aud:'SmartParking',
            sub:userId,account_id:accountId,role:'driver',jti:serial.toString(16).padStart(32,'0'),
            nbf:Math.floor(now/1000),exp:Math.floor(now/1000)+900}), Buffer.alloc(384,serial).toString('base64url')].join('.');
        return {success:true,mfaRequired:false,data:{accessToken:token,refreshToken:'fixture-refresh-'+serial,
            expiresIn:900,tokenType:'Bearer',user:{userId,role:'driver'}}};
    }
    function expect(step, body, next, code=200) {
        const result = run(step,body,code);
        assert.equal(result.skipped,false, 'unexpected skipped request '+step);
        assert.deepEqual(result.failures,[], 'failed assertions at '+step);
        assert.equal(result.next,next===null?null:names[next], 'routing at '+step);
        return result;
    }
    function baseline(mfa=false) {
        expect('00',{status:'ok'},'01'); expect('01',{keys:[key]},'02');
        if (mfa) {
            expect('02',{success:true,mfaRequired:true,data:{challengeId:'fixture-challenge',challengeToken:'fixture-challenge-secret',
                expiresAt:new Date(now+300000).toISOString()}},null);
        } else { expect('02',session(1),'04'); expect('04',okProfile,null); }
    }
    return {env,local,run,expect,session,baseline, advance:ms=>{now+=ms;}};
}

test('artifact contains only local variable references and blank secrets', () => {
    assert.equal(collection.item.length,24);
    assert.equal(new Set(collection.item.map(i=>i.name)).size,24);
    for (const item of collection.item) {
        assert.equal(names[item.name.slice(0,2)],item.name);
        assert.ok(item.request.url.startsWith('{{sp_base_url}}'));
        assert.equal(item.request.auth.type,'noauth');
        assert.equal(item.protocolProfileBehavior.followRedirects,false);
        assert.equal(item.request.header.some(h=>h.key.toLowerCase()==='authorization'),false);
        if(item.request.method==='POST') assert.equal(item.request.body.raw,'{{sp_request_body}}');
    }
    for (const variable of template.values.filter(v=>v.type==='secret')) assert.equal(variable.value,'');
    for (const field of ['sp_email','sp_config_snapshot']) assert.equal(template.values.find(v=>v.key===field).value,'');
    assert.doesNotMatch(JSON.stringify(collection),/BEGIN (?:RSA )?PRIVATE KEY|console\.log|pm\.sendRequest|Bearer \{\{sp_bearer\}\}/);
});

test('all stages route correctly, preserve original tokens, pause, and clear credentials only at completion', () => {
    const h=harness(); h.baseline();
    assert.equal(h.env.get('sp_status'),'PAUSED_RESTART');
    const original=h.env.get('sp_original_access');
    assert.equal(h.expect('00',{status:'ok'},null).bearer,undefined); // anonymous control clears stale auth
    assert.equal(h.env.get('sp_original_access'),original);
    h.env.set('sp_action','continue_restart'); h.expect('00',{status:'ok'},'05');
    h.expect('05',{keys:[key]},'06');
    assert.equal(h.expect('06',okProfile,'07').bearer,original);
    const tampered=h.expect('07',rejected,'08',401).bearer;
    assert.deepEqual(tampered.split('.').slice(0,2),original.split('.').slice(0,2));
    assert.notEqual(tampered.split('.')[2],original.split('.')[2]);
    h.expect('08',okProfile,'09');
    assert.equal(JSON.parse(h.expect('09',h.session(2),'10').body).refreshToken,'fixture-refresh-1');
    h.local.set('sp_bearer',original); // A stale run-local alias must never override the selected new token.
    assert.equal(h.expect('10',okProfile,'11').bearer,h.env.get('sp_access_token'));
    assert.equal(h.expect('11',rejected,'12',401).bearer,original);
    assert.equal(JSON.parse(h.expect('12',rejected,'13',401).body).refreshToken,'fixture-refresh-1');
    h.expect('13',okProfile,'14');
    assert.equal(JSON.parse(h.expect('14',{success:true},'15').body).refreshToken,'fixture-refresh-2');
    h.expect('15',rejected,'16',401); h.expect('16',rejected,'17',401);
    h.expect('17',h.session(3),'18'); h.expect('18',okProfile,null);
    assert.equal(h.env.get('sp_status'),'PAUSED_EXPIRY'); assert.equal(h.env.get('sp_core_result'),'PASS');
    const expiry=h.env.get('sp_expiry_token');
    h.env.set('sp_action','continue_expiry'); h.expect('00',{status:'ok'},null);
    assert.equal(h.env.get('sp_status'),'PAUSED_EXPIRY'); assert.equal(h.env.get('sp_expiry_token'),expiry);
    h.advance(902000); h.env.set('sp_action','continue_expiry'); h.expect('00',{status:'ok'},'19');
    h.expect('19',{keys:[key]},'20');
    assert.equal(h.expect('20',rejected,'21',401).bearer,expiry);
    assert.equal(JSON.parse(h.expect('21',h.session(4),'22').body).refreshToken,'fixture-refresh-3');
    h.expect('22',okProfile,'23'); h.expect('23',{success:true},null);
    assert.equal(h.env.get('sp_status'),'COMPLETE');
    for (const field of template.values.filter(v=>v.type==='secret')) assert.equal(h.env.get(field.key),'');
    assert.equal(h.local.get('sp_request_body'),undefined);
    assert.equal(h.local.get('sp_bearer'),undefined);
    const report=h.env.get('sp_results');
    assert.ok(JSON.parse(report).length>=10);
    assert.doesNotMatch(report,/fixture|eyJ|aaaaaaaa|bbbbbbbb/);
    assert.equal(h.env.get('sp_mfa_result'),'NOT_EXERCISED');
});

test('MFA stops before issuing a session and resumes the correct branch with a one-time local code', () => {
    const h=harness(); h.baseline(true);
    assert.equal(h.env.get('sp_status'),'PAUSED_MFA'); assert.equal(h.env.get('sp_access_token'),'');
    h.env.set('sp_mfa_code','123456'); h.env.set('sp_action','continue_mfa');
    h.expect('00',{status:'ok'},'03');
    const response=h.expect('03',h.session(1),'04');
    assert.deepEqual(JSON.parse(response.body),{challengeId:'fixture-challenge',challengeToken:'fixture-challenge-secret',totp:'123456'});
    assert.equal(h.env.get('sp_mfa_code'),''); assert.equal(h.env.get('sp_mfa_challenge_token'),'');
    assert.equal(h.env.get('sp_mfa_result'),'PASS');
    // The second login must resume expiry, not rerun restart/session revocation.
    h.env.set('sp_flow','expiry'); h.env.set('sp_status','RUNNING'); h.env.set('sp_step','17');
    h.expect('17',{success:true,mfaRequired:true,data:{challengeId:'next',challengeToken:'next-secret',expiresAt:'2026-11-05T08:05:00Z'}},null);
    h.env.set('sp_mfa_code','654321'); h.env.set('sp_action','continue_mfa');
    h.expect('00',{status:'ok'},'03'); h.expect('03',h.session(2),'18');
});

test('changed key, assertion failure and changed environment stop before subsequent writes', () => {
    const h=harness(); h.baseline();
    h.env.set('sp_action','continue_restart'); h.expect('00',{status:'ok'},'05');
    const result=h.run('05',{keys:[{...key,n:Buffer.alloc(384,195).toString('base64url')}]});
    assert.equal(result.next,null); assert.ok(result.failures.includes('Public key unchanged'));
    assert.equal(h.env.get('sp_status'),'FAILED');
    const changed=harness(); changed.baseline(); changed.env.set('sp_email','other@example.invalid');
    changed.env.set('sp_action','continue_restart');
    assert.ok(changed.run('00').failures.includes('Same environment and identity across checkpoints'));
    assert.equal(changed.env.get('sp_status'),'FAILED');
});

test('wrong step, missing password and remote target are skipped before sending', () => {
    for(const scenario of ['wrong-step','missing-password','remote-target']) {
        const h=harness(); let step='14';
        if(scenario==='missing-password') {
            h.expect('00',{status:'ok'},'01'); h.expect('01',{keys:[key]},'02');
            h.env.set('sp_password',''); step='02';
        }
        if(scenario==='remote-target') {h.env.set('sp_base_url','https://example.invalid'); step='00';}
        const result=h.run(step);
        assert.equal(result.skipped,true,scenario); assert.equal(result.next,null,scenario);
        assert.equal(h.env.get('sp_status'),'FAILED');
    }
});

test('expired restart and expired MFA cannot be mistaken for passing checks', () => {
    const h=harness(); h.baseline(); h.advance(901000); h.env.set('sp_action','continue_restart');
    assert.ok(h.run('00').failures.includes('Original token still has at least 60 seconds remaining'));
    assert.equal(h.env.get('sp_status'),'FAILED');
    const mfa=harness(); mfa.baseline(true); mfa.advance(301000);
    mfa.env.set('sp_mfa_code','123456'); mfa.env.set('sp_action','continue_mfa');
    mfa.expect('00',{status:'ok'},'03'); assert.equal(mfa.run('03').skipped,true);
    assert.equal(mfa.env.get('sp_status'),'FAILED');
});

test('plain start preserves a checkpoint; only explicit start_over resets it', () => {
    const h=harness(); h.baseline(); const original=h.env.get('sp_original_access');
    h.env.set('sp_action','start'); h.expect('00',{status:'ok'},null);
    assert.equal(h.env.get('sp_original_access'),original);
    h.env.set('sp_restart_attestation','old confirmation'); h.env.set('sp_action','start_over');
    h.expect('00',{status:'ok'},'01');
    assert.equal(h.env.get('sp_original_access'),''); assert.equal(h.env.get('sp_restart_attestation'),'');
    assert.equal(h.env.get('sp_results'),'[]'); assert.equal(h.env.get('sp_core_result'),'PENDING');
});

test('lifetime allows one-second issuance rounding but rejects an old one-hour contract', () => {
    for(const seconds of [899,3600]) {
        const h=harness(); h.expect('00',{status:'ok'},'01'); h.expect('01',{keys:[key]},'02');
        const body=h.session(1), parts=body.data.accessToken.split('.');
        const payload=JSON.parse(Buffer.from(parts[1],'base64url').toString());
        payload.exp=payload.nbf+seconds;
        parts[1]=Buffer.from(JSON.stringify(payload)).toString('base64url');
        body.data.accessToken=parts.join('.'); body.data.expiresIn=seconds;
        const result=h.run('02',body);
        if(seconds===899) { assert.deepEqual(result.failures,[]); assert.equal(result.next,names['04']); }
        else { assert.equal(h.env.get('sp_status'),'FAILED'); assert.equal(result.next,null); }
    }
});

test('fresh response tokens survive stale or protected environment values within a run', () => {
    for (const saved of ['stale', 'vault-reference']) {
        const h=harness(); h.baseline();
        const original=h.env.get('sp_original_access');
        h.local.clear(); // A new Runner invocation starts with environment checkpoints only.
        h.env.set('sp_action','continue_restart'); h.expect('00',{status:'ok'},'05');
        h.expect('05',{keys:[key]},'06'); h.expect('06',okProfile,'07');
        h.expect('07',rejected,'08',401); h.expect('08',okProfile,'09');
        const fresh=h.session(2); h.expect('09',fresh,'10');
        // Model a stale environment read/protected reference; not a claim Postman does this.
        h.env.set('sp_access_token',saved==='stale'?original:'{{vault:access}}');
        h.env.set('sp_refresh_token',saved==='stale'?'fixture-refresh-1':'{{vault:refresh}}');
        assert.equal(h.expect('10',okProfile,'11').bearer,fresh.data.accessToken);
        const report=JSON.parse(h.env.get('sp_token_transport'));
        assert.deepEqual(report,{version:2,source:'run-local-response',environmentMatchesSelected:false,
            preparedHeaderMatchesSelected:true,httpStatus:200});
        assert.equal(h.expect('11',rejected,'12',401).bearer,original);
        assert.equal(JSON.parse(h.expect('12',rejected,'13',401).body).refreshToken,'fixture-refresh-1');
        assert.equal(h.expect('13',okProfile,'14').bearer,fresh.data.accessToken);
        assert.equal(JSON.parse(h.expect('14',{success:true},'15').body).refreshToken,fresh.data.refreshToken);
    }
});

test('verify_refresh exercises the real 09/10 path without claiming restart or overall completion', () => {
    const h=harness(); h.baseline(); h.local.clear();
    h.local.set('sp_run_access','stale-from-another-run');
    h.env.set('sp_action','verify_refresh'); h.advance(901000); // Refresh can follow access expiry.
    h.expect('00',{status:'ok'},'09');
    const fresh=h.session(2); h.expect('09',fresh,'10');
    assert.equal(h.expect('10',okProfile,null).bearer,fresh.data.accessToken);
    assert.equal(h.env.get('sp_status'),'DIAGNOSTIC_ONLY');
    assert.equal(h.env.get('sp_core_result'),'PENDING');
    assert.equal(h.env.get('sp_restart_attestation'),'');
    assert.equal(h.env.get('sp_refresh_token'),fresh.data.refreshToken);
    assert.doesNotMatch(h.env.get('sp_token_transport'),/fixture|eyJ|aaaaaaaa|bbbbbbbb/);
});

test('verify_refresh stops on missing checkpoints and reports a failed profile without bypassing it', () => {
    const missing=harness(); missing.baseline(); missing.env.set('sp_refresh_token','{{vault:refresh}}');
    missing.env.set('sp_action','verify_refresh');
    assert.ok(missing.run('00').failures.includes('Saved refresh token is a literal value'));
    assert.equal(missing.env.get('sp_status'),'FAILED');
    const h=harness(); h.baseline(); h.env.set('sp_action','verify_refresh');
    h.expect('00',{status:'ok'},'09'); h.expect('09',h.session(2),'10');
    const result=h.run('10',rejected,401);
    assert.equal(result.next,null); assert.ok(result.failures.includes('HTTP 200'));
    assert.equal(h.env.get('sp_status'),'FAILED');
    assert.equal(JSON.parse(h.env.get('sp_token_transport')).httpStatus,401);
});
