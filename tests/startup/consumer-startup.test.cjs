// Run after dotnet build SmartParking.slnx. Starts disposable loopback API processes only.
const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const http=require('node:http');
const {spawn}=require('node:child_process');
const {once}=require('node:events');
const root=path.resolve(__dirname,'../..');
const delay=ms=>new Promise(resolve=>setTimeout(resolve,ms));

for(const [service,connection] of [['Facility','Parking'],['Operation','Reservation'],['Payment','Payment']]) {
    test(service+' starts and stays healthy while UserService is unavailable', {timeout:45000},async()=>{
        let upstreamCalls=0;
        const unavailable=http.createServer((req,res)=>{upstreamCalls++;res.writeHead(503);res.end();});
        unavailable.listen(0,'127.0.0.1');await once(unavailable,'listening');
        const upstream='http://127.0.0.1:'+unavailable.address().port;
        // Reserve a free loopback port, releasing it immediately before the child binds it.
        const reservation=http.createServer();reservation.listen(0,'127.0.0.1');await once(reservation,'listening');
        const port=reservation.address().port;
        await new Promise(resolve=>reservation.close(resolve));
        let child;
        try {
            const dll=path.join(root,'src/Services',service+'Service','SmartParking.'+service+'Service.API','bin/Debug/net10.0','SmartParking.'+service+'Service.API.dll');
            assert.ok(fs.existsSync(dll),'Build the solution first: missing '+service+' API assembly');
            // Do not inherit signing keys, credentials or service configuration from the developer shell.
            const env={};
            for(const [key,value] of Object.entries(process.env))if(/^(path|systemroot|windir|temp|tmp|programfiles|programfiles\(x86\)|dotnet_root)$/i.test(key))env[key]=value;
            const args=[dll,'--environment','Testing','--urls','http://127.0.0.1:'+port,
                '--Services:Key','startup-fixture-service-key-not-a-secret-327',
                '--Services:User',upstream+'/', '--Services:Parking',upstream+'/', '--Services:Reservation',upstream+'/',
                '--Services:ReservationService:BaseUrl',upstream+'/',
                '--ConnectionStrings:'+connection,'Host=127.0.0.1;Port=1;Database=unused_startup_fixture;Username=unused;Password=unused',
                '--JwtValidation:Issuer','startup-fixture','--JwtValidation:Audience','startup-fixture',
                '--JwtValidation:JwksUri',upstream+'/.well-known/jwks.json','--Messaging:Enabled','false'];
            child=spawn('dotnet',args,{cwd:path.dirname(dll),env,windowsHide:true,stdio:['ignore','pipe','pipe']});
            // Drain logs without copying configuration/exception contents into test output.
            child.stdout.resume();child.stderr.resume();
            let launchError;child.on('error',e=>{launchError=e.code;});
            const deadline=Date.now()+30000;
            let ready=false;
            while(Date.now()<deadline){
                assert.equal(launchError,undefined,'Process launch failed');
                assert.equal(child.exitCode,null,'Consumer exited before health was available');
                try {const response=await fetch('http://127.0.0.1:'+port+'/health',{signal:AbortSignal.timeout(750)});if(response.ok){ready=true;break;}}catch{}
                await delay(200);
            }
            assert.ok(ready,'Health did not become available within 30 seconds');
            await delay(2000);
            assert.equal(child.exitCode,null,'Consumer exited after startup');
            const response=await fetch('http://127.0.0.1:'+port+'/health',{signal:AbortSignal.timeout(1000)});
            assert.equal(response.status,200);
            assert.equal((await response.json()).status,'ok');
            assert.equal(upstreamCalls,0,'Current configuration-only consumers must not fetch UserService during startup');
        } finally {
            if(child&&child.exitCode===null){const exited=once(child,'exit');child.kill();await exited;}
            await new Promise(resolve=>unavailable.close(resolve));
        }
    });
}
