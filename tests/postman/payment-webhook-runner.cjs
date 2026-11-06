// Run the real Postman CLI against a disposable loopback receiver, never Payment/payOS.
// Usage: node tests/postman/payment-webhook-runner.cjs [path-to-postman.exe]
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const fs = require('node:fs');
const http = require('node:http');
const os = require('node:os');
const path = require('node:path');
const {spawn} = require('node:child_process');
const directory = path.resolve(__dirname, '../../docs/04-testing/postman');
const collection = JSON.parse(fs.readFileSync(path.join(directory,'PaymentService.postman_collection.json')));
const environment = JSON.parse(fs.readFileSync(path.join(directory,'PaymentService-Local.postman_environment.example.json')));
const checksum = 'synthetic-receiver-checksum-not-a-provider-secret';
const received = [];
const errors = [];
const server = http.createServer(async(req,res)=>{
    try {
        let raw='';for await(const part of req)raw+=part;
        assert.equal(req.url,'/api/v1/payments/callbacks/payos');
        assert.equal(req.method,'POST');
        const body=JSON.parse(raw);
        const canonical=Object.entries(body.data).filter(([,v])=>v!==null&&String(v)!=='').sort(([a],[b])=>a<b?-1:1).map(([k,v])=>`${k}=${v}`).join('&');
        const valid=body.signature===crypto.createHmac('sha256',checksum).update(canonical).digest('hex');
        received.push({raw,valid});
        res.writeHead(valid?200:400,{'Content-Type':'application/json'});
        res.end(JSON.stringify(valid?{success:true}:{success:false,errorCode:'INVALID_WEBHOOK_SIGNATURE'}));
    } catch(error) {errors.push(error.message);res.writeHead(500);res.end('{}');}
});
server.listen(0,'127.0.0.1',async()=>{
    const temp=fs.mkdtempSync(path.join(os.tmpdir(),'smartpark-payment-postman-'));
    const collectionFile=path.join(temp,'collection.json'), environmentFile=path.join(temp,'environment.json');
    try {
        collection.item=collection.item.flatMap(f=>f.item).filter(r=>/^3\.(10|11|12) /.test(r.name));
        const state={onlineOrderCode:'202611060001',payosOrderCode:'202611060001',onlineAmount:'25000',payosChecksumKey:'wrong-collection-default'};
        collection.variable.forEach(v=>{if(v.key in state)v.value=state[v.key];});
        environment.values.find(v=>v.key==='baseUrl').value='http://127.0.0.1:'+server.address().port;
        environment.values.find(v=>v.key==='payosChecksumKey').value=checksum;
        fs.writeFileSync(collectionFile,JSON.stringify(collection));
        fs.writeFileSync(environmentFile,JSON.stringify(environment));
        const exitCode=await new Promise((resolve,reject)=>{
            const child=spawn(process.argv[2]||'postman',['collection','run',collectionFile,'-e',environmentFile,'--no-report-events','--ignore-redirects','--timeout','30000','--bail'],{stdio:'inherit'});
            child.on('error',reject);child.on('exit',resolve);
        });
        assert.equal(exitCode,0,'Postman assertions must pass');
        assert.deepEqual(errors,[]);
        assert.equal(received.length,3);
        assert.deepEqual(received.map(r=>r.valid),[true,true,false]);
        assert.equal(received[0].raw,received[1].raw,'Replay must be byte-identical');
        console.log('PASS: real runner sent valid environment-key HMAC, exact replay, and invalid-signature control. Backend/provider NOT tested.');
    } catch(error) {console.error(error.message);process.exitCode=1;}
    finally {
        server.close();
        for(const file of [collectionFile,environmentFile])if(fs.existsSync(file))fs.unlinkSync(file);
        fs.rmdirSync(temp);
    }
});
