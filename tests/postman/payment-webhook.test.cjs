// Synthetic collection tests; no Payment database, provider or real credentials.
const {test} = require('node:test');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const directory = path.resolve(__dirname, '../../docs/04-testing/postman');
const collection = JSON.parse(fs.readFileSync(path.join(directory, 'PaymentService.postman_collection.json')));
const environment = JSON.parse(fs.readFileSync(path.join(directory, 'PaymentService-Local.postman_environment.example.json')));
const requests = collection.item.flatMap(folder => folder.item);
const request = step => requests.find(item => item.name.startsWith(step + ' '));
const code = (step, kind) => request(step).event.find(event => event.listen === kind).script.exec.join('\n');
const checksum = 'synthetic-checksum-for-tests-only';

function harness() {
    const values = new Map(collection.variable.map(v => [v.key, v.value]));
    Object.entries({onlineOrderCode:202611060001, payosOrderCode:'202611060001', onlineAmount:25000}).forEach(([k,v]) => values.set(k,v));
    const env = new Map(environment.values.map(v => [v.key,v.value]));
    env.set('payosChecksumKey', checksum);
    values.set('payosChecksumKey', 'wrong-collection-default');
    const local = new Map();
    const scope = map => ({get:k=>map.get(k), set:(k,v)=>map.set(k,v), unset:k=>map.delete(k)});
    const pm = {
        collectionVariables:scope(values),
        variables:{...scope(local), get:k=>local.has(k)?local.get(k):env.has(k)?env.get(k):values.get(k), replaceIn:s=>s.replace('{{$guid}}',crypto.randomUUID())},
        execution:{skipRequest(){pm.skipped=true;},setNextRequest(value){pm.next=value;}},
        vault:{async get(name){assert.equal(name,'payment-checksum');return checksum;}},
        test(name,fn){fn();},
        expect(actual){return {to:{be:{true:assert.equal(actual,true)},eql:expected=>assert.equal(actual,expected)}};}
    };
    async function run(step, kind='prerequest', status=200, response={success:true}) {
        pm.skipped=false;
        pm.request={body:Object.freeze({...request(step).request.body})};
        pm.response={code:status,json:()=>response,to:{have:{status:expected=>assert.equal(status,expected)}}};
        // CryptoJS is provided by Postman; Node's crypto independently supplies the harness equivalent.
        const requireStub = name => {
            assert.equal(name,'crypto-js');
            return {enc:{Hex:'hex'},HmacSHA256:(data,key)=>({toString:()=>crypto.createHmac('sha256',key).update(data).digest('hex')})};
        };
        await vm.runInNewContext('(async()=>{\n'+code(step,kind)+'\n})()', {pm,require:requireStub});
        return request(step).request.body?.raw.replace(/\{\{(\w+)\}\}/g,(_,key)=>pm.variables.get(key));
    }
    return {pm,values,env,local,run};
}

test('serialized callback has the environment-key HMAC over the data actually sent', async()=>{
    const h=harness(); const body=JSON.parse(await h.run('3.10'));
    const canonical=Object.entries(body.data).sort(([a],[b])=>a<b?-1:1).map(([k,v])=>`${k}=${v}`).join('&');
    assert.equal(body.signature,crypto.createHmac('sha256',checksum).update(canonical).digest('hex'));
    assert.equal(body.data.orderCode,202611060001);
    assert.equal(body.data.amount,25000);
    assert.ok(!JSON.stringify(body).includes(checksum));
    assert.equal(h.pm.skipped,false);
});

test('replay preserves exact accepted bytes across separate Send executions',async()=>{
    const h=harness(); const first=await h.run('3.10'); await h.run('3.10','test');
    h.local.clear();
    assert.equal(await h.run('3.11'),first);
});

test('blank, unresolved and unavailable Vault secrets stop before sending',async()=>{
    for(const value of ['', '{{missing}}', '{{vault:payment-checksum}}']) {
        const h=harness(); h.env.set('payosChecksumKey',value);
        h.pm.vault.get=async()=>{throw new Error('Vault unavailable');};
        await assert.rejects(h.run('3.10'));
        assert.equal(h.pm.skipped,true); assert.equal(h.pm.next,null);
        assert.equal(h.local.has('paymentWebhookBody'),false);
    }
});

test('Desktop Vault reference resolves before signing without persisting its key',async()=>{
    const h=harness();h.env.set('payosChecksumKey','{{vault:payment-checksum}}');
    const body=JSON.parse(await h.run('3.10'));
    assert.match(body.signature,/^[a-f0-9]{64}$/);
    assert.equal(h.env.get('payosChecksumKey'),'{{vault:payment-checksum}}');
    assert.ok(![...h.local.values()].join('').includes(checksum));
});

test('missing, unsafe or mismatched order data cannot fall back to a hard-coded order',async()=>{
    for(const [key,value] of [['payosOrderCode',''],['onlineOrderCode','123'],['payosOrderCode',Number.MAX_SAFE_INTEGER+1],['onlineAmount',0],['onlineAmount',2.5]]) {
        const h=harness();h.values.set(key,value);
        await assert.rejects(h.run('3.10'),/Run 3.1 and 3.4/);
        assert.equal(h.pm.skipped,true);
    }
});

test('replay requires successful callback and rejects changed target/order',async()=>{
    const empty=harness(); await assert.rejects(empty.run('3.11'),/Run 3.10 successfully/);
    for(const change of [h=>h.env.set('baseUrl','http://localhost:9999'),h=>h.values.set('onlineOrderCode','123'),h=>h.values.set('onlineAmount',999)]) {
        const h=harness();await h.run('3.10');await h.run('3.10','test');change(h);
        await assert.rejects(h.run('3.11'),/different target or order/);
        assert.equal(h.pm.skipped,true);
    }
});

test('new order or failed callback cannot reuse an old accepted replay',async()=>{
    const h=harness();await h.run('3.10');await h.run('3.10','test');
    await h.run('3.1');assert.equal(h.values.has('paymentWebhookReplay'),false);
    assert.equal(h.values.has('payosOrderCode'),false);
    const failed=harness();failed.values.set('paymentWebhookReplay','stale');await failed.run('3.10');
    // pm.test records failures rather than throwing in a real runner.
    failed.pm.test=()=>{};
    await failed.run('3.10','test',400,{success:false,errorCode:'INVALID_WEBHOOK_SIGNATURE'});
    assert.equal(failed.values.has('paymentWebhookReplay'),false);
    assert.equal(failed.pm.next,null);
});

test('environment contains only configuration, no generated state or checksum secret',()=>{
    assert.equal(environment.values.find(v=>v.key==='payosChecksumKey').value,'');
    assert.deepEqual(environment.values.map(v=>v.key).sort(),['baseUrl','accessToken','adminToken','operatorToken','internalServiceKey','tenantId','siteId','payosChecksumKey'].sort());
});
