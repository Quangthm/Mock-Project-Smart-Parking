const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');
const collection=JSON.parse(fs.readFileSync(path.resolve(__dirname,'../../docs/04-testing/postman/SPARK-327-Refresh-Probe.postman_collection.json')));
const source=listen=>collection.event.find(e=>e.listen===listen).script.exec.join('\n');
function fixture(){
 const env=new Map([['sp_base_url','http://localhost:5035'],['sp_refresh_token','synthetic-old-refresh']]),local=new Map();
 const scope=map=>({get:key=>map.get(key),set:(key,value)=>map.set(key,value),unset:key=>map.delete(key)});
 const token='eyJhbGciOiJSUzI1NiJ9.'+Buffer.from(JSON.stringify({exp:Math.floor(Date.now()/1000)+900})).toString('base64url')+'.c3ludGhldGlj';
 function run(index,status=200){
  const headers=new Map([['Authorization','Bearer stale']]),result={next:null,skipped:false,failed:[],wait:0};
  const body=index===0?{success:true,data:{accessToken:token,refreshToken:'synthetic-new-refresh'}}:{success:status===200,code:status===200?undefined:'INVALID_TOKEN',data:{email:'must-not-enter-report@example.invalid'}};
  const pm={environment:scope(env),variables:scope(local),collectionVariables:{get:key=>collection.variable.find(v=>v.key===key).value},info:{requestName:collection.item[index].name},
   request:{headers:{remove:key=>headers.delete(key),upsert:({key,value})=>headers.set(key,value),get:key=>headers.get(key)}},
   response:{code:status,json:()=>body,headers:{get:()=>new Date().toUTCString()}},
   execution:{setNextRequest:value=>{result.next=value;},skipRequest:()=>{result.skipped=true;}},
   test:(name,fn)=>{try{fn();}catch{result.failed.push(name);}}};
  const context=()=>({pm,require:name=>{assert.equal(name,'buffer');return {Buffer};},setTimeout:(_,ms)=>{result.wait=ms;}});
  vm.runInNewContext(source('prerequest'),context());
  if(!result.skipped)vm.runInNewContext(source('test'),context());
  result.header=headers.get('Authorization');return result;
 }
 return {env,local,token,run};
}
test('401 remains visible while same-token comparisons continue and report excludes secrets',()=>{
 const h=fixture();assert.equal(h.run(0).next,collection.item[1].name);
 assert.equal(h.env.get('sp_status'),'DIAGNOSTIC_ONLY');
 const first=h.run(1,401);assert.equal(first.header,'Bearer '+h.token);assert.equal(first.failed.length,1);
 assert.equal(first.next,collection.item[2].name);
 assert.equal(collection.item[2].request.auth.type,'bearer');
 assert.equal(h.run(2).next,collection.item[3].name);
 const last=h.run(3);assert.equal(last.wait,2000);assert.equal(last.header,'Bearer '+h.token);assert.equal(last.next,null);
 const report=h.env.get('sp_refresh_probe_report');assert.deepEqual(JSON.parse(report).map(r=>r.status),[401,200,200]);
 assert.doesNotMatch(report,/eyJ|synthetic|example.invalid/);assert.equal(h.local.get('probe_access'),undefined);
 assert.equal(h.env.get('sp_refresh_token'),'synthetic-new-refresh');
});
test('missing refresh, remote targets and running comparisons alone are blocked',()=>{
 for(const mode of ['missing','remote','wrong-step']){
  const h=fixture();if(mode==='missing')h.env.delete('sp_refresh_token');if(mode==='remote')h.env.set('sp_base_url','https://example.invalid');
  const r=h.run(mode==='wrong-step'?1:0);assert.equal(r.skipped,true);assert.equal(r.next,null);
 }
});
test('failed refresh does not continue or replace saved session tokens',()=>{
 const h=fixture(),r=h.run(0,401);assert.equal(r.next,null);assert.equal(r.failed.length,1);
 assert.equal(h.env.get('sp_refresh_token'),'synthetic-old-refresh');assert.equal(h.env.get('sp_access_token'),undefined);
 assert.equal(JSON.parse(h.env.get('sp_refresh_probe_report')).status,401);
});
