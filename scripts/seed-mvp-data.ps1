#requires -Version 7.2
<#
.SYNOPSIS
    Seed Checkpoint A of MVP-SEED-V1 in an explicitly authorized local Compose project.
.DESCRIPTION
    -Preview is offline: generate and validate the fixture without Docker or credentials.
    -IsolatedProject identifies the operator-authorized disposable Compose project.
    Reset requires exclusive maintenance of that target, including host-based clients.
    An ordinary replay preserves password hashes, tokens and publication revisions.
#>
[CmdletBinding()]
param(
    [string]$ReferenceTime,
    [switch]$Reset,
    [switch]$Preview,
    [ValidatePattern('^[a-z0-9][a-z0-9_-]*$')][string]$IsolatedProject,
    [securestring]$FixturePassword
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$OutputEncoding = [Text.UTF8Encoding]::new($false)
$root = Split-Path $PSScriptRoot
$seedNamespace = '9e05403d-71f1-5889-9f06-55434e65b6ee'
$started = [DateTimeOffset]::UtcNow
$invariant = [Globalization.CultureInfo]::InvariantCulture

function Get-FixtureId([string]$Key) {
    # RFC 4122 hashes namespace bytes in network order (Guid.ToByteArray uses mixed endian).
    $inputBytes = [Convert]::FromHexString($seedNamespace.Replace('-', '')) + [Text.Encoding]::UTF8.GetBytes($Key)
    $bytes = [Security.Cryptography.SHA1]::HashData([byte[]]$inputBytes)
    $bytes[6] = ($bytes[6] -band 15) -bor 80
    $bytes[8] = ($bytes[8] -band 63) -bor 128
    return [guid]::ParseExact([Convert]::ToHexString($bytes, 0, 16), 'N').ToString()
}
function Format-Instant([DateTimeOffset]$Instant) {
    return $Instant.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", $invariant)
}

function Read-Bays([string]$File) {
    $reader = [IO.BinaryReader]::new([IO.File]::OpenRead((Join-Path $root "FE/public/models/parking/$File")))
    try {
        if ($reader.ReadUInt32() -ne 0x46546c67 -or $reader.ReadUInt32() -ne 2) { throw "Invalid GLB: $File" }
        [void]$reader.ReadUInt32()
        $length = $reader.ReadInt32()
        if ($reader.ReadUInt32() -ne 0x4e4f534a) { throw "Missing GLB JSON: $File" }
        $nodes = ([Text.Encoding]::UTF8.GetString($reader.ReadBytes($length)) | ConvertFrom-Json -AsHashtable).nodes
    } finally { $reader.Dispose() }
    $parents = @{}
    foreach ($i in 0..($nodes.Count - 1)) {
        foreach ($child in $nodes[$i]['children']) { $parents[[int]$child] = $i }
    }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($i in 0..($nodes.Count - 1)) {
        $code = [string]$nodes[$i]['name']
        if ($code -notmatch '^(CarParkingSpace_\d+|PARKING_EV_CAR_\d+|B\d+_(EV_)?CAR_\d+|PARKING_(G|L\d+)_(CAR|EV_CAR|DISABLED)_\d+|(SLOT|PARKING_SLOT|CAR_BAY)[_-]?[A-Z0-9_. -]+)([. ]\d+)*$') { continue }
        if (!$seen.Add($code)) { throw "Duplicate raw slot code in $File ($code)" }
        $ancestor = $i
        $floor = ''
        while ($parents.ContainsKey($ancestor)) {
            $ancestor = $parents[$ancestor]
            $floor = [string]$nodes[$ancestor]['name']
            if ($floor -match '^(OutdoorParking|Floor_(G|L1|L2|B1|B2))$') { break }
        }
        if ($floor -notmatch '^(OutdoorParking|Floor_(G|L1|L2|B1|B2))$') { throw "Unmapped floor: $code" }
        $kind = if ($code -match 'DISABLED') { 'DISABLED' } elseif ($code -match 'EV_CAR') { 'EV' } else { 'STANDARD' }
        [pscustomobject]@{ code = $code; floor = $floor; kind = $kind }
    }
}

function New-Inventory {
    $definitions = @(
        @('Bến Đỗ Ven Kênh','outdoor',10.785,106.705,48,2,10000),
        @('Nhà Xe Nhiều Tầng','multi-storey',10.748,106.690,64,4,15000),
        @('Hầm Đỗ Phía Bắc','basement',10.838,106.715,48,6,20000),
        @('Sân Đỗ Phía Nam','outdoor',10.660,106.750,48,3,15000),
        @('Hầm Đỗ Đông Bắc','basement',10.995,106.860,48,5,25000)
    )
    $models = @{outdoor='outdoor'; 'multi-storey'='indoor'; basement='underground'}
    $floorCounts = @{ outdoor=@{OutdoorParking=48}; 'multi-storey'=@{Floor_G=20;Floor_L1=18;Floor_L2=26}; basement=@{Floor_B1=24;Floor_B2=24} }
    $sites = foreach ($i in 1..5) {
        $name,$type,$lat,$lon,$capacity,$backup,$rate = $definitions[$i-1]
        $n = '{0:00}' -f $i
        $owner = if ($i -le 3) { '01' } else { '02' }
        $tenant = Get-FixtureId $(if ($i -le 3) { 'site/01' } else { 'site/04' })
        $file = $models[$type] + '_parking_lot.glb'
        $bays = @(Read-Bays $file)
        $groups = @($bays | Group-Object floor)
        if ($bays.Count -ne $capacity -or $groups.Count -ne $floorCounts[$type].Count) { throw "Model inventory changed: $file" }
        [string[]]$eligible = @($bays | Where-Object { $_.kind -eq 'STANDARD' -and $_.code -cne 'CarParkingSpace_013' } | ForEach-Object code)
        [Array]::Sort($eligible, [StringComparer]::Ordinal)
        $selected = @($eligible | Select-Object -Last $backup)
        $floors = foreach ($group in $groups) {
            $f = $group.Name
            if ($group.Count -ne $floorCounts[$type][$f]) { throw "Floor inventory changed: $file/$f" }
            $slots = foreach ($b in $group.Group) {
                @{id=Get-FixtureId "slot/$n/$($b.code)"; code=$b.code; kind=$b.kind; backup=$selected -ccontains $b.code
                  compatibility=Get-FixtureId "compatibility/$n/$($b.code)/CAR"}
            }
            @{name=$f; id=Get-FixtureId "floor/$n/$f"; zone=Get-FixtureId "zone/$n/$f"; slots=@($slots)
              floorCapacity=Get-FixtureId "capacity/$n/$f/floor"; zoneCapacity=Get-FixtureId "capacity/$n/$f/zone"}
        }
        $r = [Math]::PI/180
        $a = [Math]::Pow([Math]::Sin(($lat-10.775)*$r/2),2) + [Math]::Cos(10.775*$r)*[Math]::Cos($lat*$r)*[Math]::Pow([Math]::Sin(($lon-106.700)*$r/2),2)
        @{n=$n; id=Get-FixtureId "site/$n"; owner=$owner; tenant=$tenant; code="MVP-$n"; name="MVPSEED $name"
          address="Cổng mô phỏng $n, khu thử nghiệm MVPSEED, TP.HCM"; lat=$lat; lon=$lon; metres=[Math]::Round(12742000*[Math]::Asin([Math]::Sqrt($a)))
          type=$type; model=$file; rate=$rate; capacity=$capacity; backup=$backup; policy=Get-FixtureId "backup/$n/CAR"
          publication=Get-FixtureId "publication/$n"; initial=$i -in 1,4; floors=@($floors)}
    }
    $accounts = foreach ($role in 'owner','operator','driver') {
        $count = @{owner=2; operator=10; driver=6}[$role]
        foreach ($i in 1..$count) {
            $n = '{0:00}' -f $i
            $site = switch ($role) {
                owner { $sites[@(0,3)[$i-1]] }
                operator { $sites[[int][Math]::Floor(($i-1)/2)] }
            }
            $tenant = if ($site) { $site.tenant }
            $siteId = if ($role -eq 'operator') { $site.id }
            $prefix = @{owner='81'; operator='82'; driver='83'}[$role]
            @{role=$role; n=$n; id=Get-FixtureId "account/$role/$n"; uid=Get-FixtureId "user/$role/$n"
              email="$role$n@mvpseed.example.test"; phone="+8491$($prefix)000$n"; tenant=$tenant; site=$siteId}
        }
    }
    $vehicles = foreach ($d in 1..6) {
        $n = '{0:00}' -f $d
        $count = if ($d -eq 6) { 1 } else { 3 }
        foreach ($v in 1..$count) {
            $digits = (91000 + 10*$d + $v).ToString($invariant)
            @{id=Get-FixtureId "vehicle/$n/$v"; account=Get-FixtureId "account/driver/$n"
              plate="51M-$($digits.Substring(0,3)).$($digits.Substring(3))"; canonical="51M$digits"}
        }
    }
    return @{ sites=@($sites); accounts=@($accounts); vehicles=@($vehicles) }

}

function Show-Inventory($Data) {
    Write-Host "MVP-SEED-V1 / $seedNamespace | Checkpoint A | origin 10.775, 106.700 | keyword MVPSEED"
    foreach ($s in $Data.sites) {
        $source = if ($s.initial) { 'initial application evidence' } else { 'synthetic additional-site grant' }
        Write-Host "$($s.code) $($s.name) [$($s.id)] tenant $($s.tenant), owner$($s.owner), $($s.lat),$($s.lon) ($($s.metres)m)"
        Write-Host "  $($s.model), $($s.floors.Count) floors / zones, $($s.capacity) CAR bays, $source"
        foreach ($floor in $s.floors) {
            foreach ($slot in $floor.slots.Where({ $_.backup })) { Write-Host "  BACKUP $($slot.code) $($slot.id)" }
        }
    }
    foreach ($a in $Data.accounts) { Write-Host "$($a.email) user=$($a.uid) account=$($a.id) tenant=$($a.tenant) site=$($a.site)" }
    foreach ($v in $Data.vehicles) { Write-Host "$($v.plate) $($v.id) account=$($v.account)" }
    Write-Host 'Owner model setup is browser-local. Verify the existing lot type/model and publication after saving; test Operator layout in the assigned site.'
}

function Invoke-Docker([string[]]$Arguments) {
    $result = & docker @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Docker $($Arguments[0]) failed ($LASTEXITCODE)." }
    return $result
}

function Resolve-Target {
    if ($env:DOCKER_HOST -or $env:PGHOST -or $env:PGSERVICE) { throw 'Remove connection overrides before identifying the local target.' }
    $context = Invoke-Docker @('context','show')
    $endpoint = Invoke-Docker @('context','inspect',$context,'--format','{{.Endpoints.docker.Host}}')
    if ($endpoint -notin 'npipe:////./pipe/docker_engine','npipe:////./pipe/dockerDesktopLinuxEngine','unix:///var/run/docker.sock') {
        throw 'A recognized local Docker endpoint is required.'
    }
    foreach ($service in $services) {
        $id = @(Invoke-Docker ($compose + @('ps','-q',$service)))
        if ($id.Count -ne 1) { throw "Expected one running $service in $IsolatedProject." }
        # Request only nonsecret fields. Container Config.Env includes the database password.
        $format = '{{json .Config.Labels}}|{{json .Mounts}}|{{json .NetworkSettings.Ports}}|{{.Config.Image}}'
        $info = (Invoke-Docker @('inspect','--format',$format,$id[0])) -split '\|'
        $labels = $info[0] | ConvertFrom-Json -AsHashtable
        $mounts = $info[1] | ConvertFrom-Json -AsHashtable
        $bindings = ($info[2] | ConvertFrom-Json -AsHashtable)['5432/tcp']
        $volume = @($mounts | Where-Object Destination -eq '/var/lib/postgresql/data')
        $short = $service.Replace('-db','')
        if ($labels['com.docker.compose.project'] -cne $IsolatedProject -or $labels['com.docker.compose.service'] -cne $service -or
            $info[3] -cne 'postgres:16-alpine' -or $volume.Count -ne 1 -or $volume[0].Type -ne 'volume' -or
            $volume[0].Name -cne "$($IsolatedProject)_$short-schema-data" -or !$bindings -or
            @($bindings | Where-Object HostIp -notin '127.0.0.1','::1').Count) { throw "Unexpected Compose identity, image, volume or port for $service." }
        $vlabels = (Invoke-Docker @('volume','inspect','--format','{{json .Labels}}',$volume[0].Name)) | ConvertFrom-Json -AsHashtable
        if ($vlabels['com.docker.compose.project'] -cne $IsolatedProject -or $vlabels['com.docker.compose.volume'] -cne "$short-schema-data") {
            throw "Unverified volume ownership for $service."
        }
        if (@(Invoke-Docker @('ps','-q','--filter',"volume=$($volume[0].Name)")).Count -ne 1) { throw "Shared volume for $service." }
        $containers[$service] = Invoke-Docker @('inspect','--format','{{.Id}}',$id[0])
    }
    if ($Reset) {
        $networks = @{}
        foreach ($id in $containers.Values) {
            $attached = (Invoke-Docker @('inspect','--format','{{json .NetworkSettings.Networks}}',$id)) | ConvertFrom-Json -AsHashtable
            foreach ($net in $attached.Keys) { $networks[$net] = $true }
        }
        foreach ($net in $networks.Keys) {
            foreach ($id in @(Invoke-Docker @('ps','-q','--no-trunc','--filter',"network=$net"))) {
                if ($id -in $containers.Values) { continue }
                $tag = Invoke-Docker @('inspect','--format','{{index .Config.Labels "com.docker.compose.project"}}/{{index .Config.Labels "com.docker.compose.service"}}',$id)
                if ($tag -notin "$IsolatedProject/redis","$IsolatedProject/rabbitmq") { throw "Reset requires exclusive maintenance: another service is attached to $net." }
            }
        }
    }
}

function Invoke-Database([string]$Service, [string]$Sql) {
    $db = 'smartpark_' + $Service.Replace('-db','')
    # Pin the verified container ID for the whole run. A Compose recreate cannot redirect a write.
    $result = $Sql | & docker exec -i $containers[$Service] psql -X -qAt -v ON_ERROR_STOP=1 -v VERBOSITY=terse -U $db -d $db
    if ($LASTEXITCODE -ne 0) { throw "$Service SQL failed ($LASTEXITCODE). Its current transaction did not confirm completion." }
    return $result
}

function New-PasswordHashes {
    $project = Join-Path $root 'src/Services/UserService/SmartParking.UserService.Infrastructure'
    $assets = Get-Content -Raw -LiteralPath "$project/obj/project.assets.json" | ConvertFrom-Json -AsHashtable
    $package = @($assets.libraries.Keys | Where-Object { $_ -like 'BCrypt.Net-Next/*' })
    $version = ([xml](Get-Content -Raw "$project/SmartParking.UserService.Infrastructure.csproj")).Project.ItemGroup.PackageReference |
        Where-Object Include -eq 'BCrypt.Net-Next' | Select-Object -ExpandProperty Version
    if ($package.Count -ne 1 -or $package[0] -ne "BCrypt.Net-Next/$version") { throw 'Restore the current UserService BCrypt dependency before seeding.' }
    $dll = foreach ($dir in $assets.packageFolders.Keys) {
        $path = Join-Path $dir ($assets.libraries[$package[0]].path + '/lib/netstandard2.1/BCrypt-Net-Next.dll')
        if (Test-Path -LiteralPath $path) { $path; break }
    }
    if (!$dll) { throw 'Restored UserService BCrypt assembly unavailable.' }
    Add-Type -Path $dll
    if (!$FixturePassword) { $FixturePassword = Read-Host 'Local fixture password (8-15 characters, upper/lowercase, digit and special)' -AsSecureString }
    $plain = [Net.NetworkCredential]::new('', $FixturePassword).Password
    try {
        if ($plain.Length -notin (8..15) -or $plain -cnotmatch '^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[^A-Za-z0-9]).+$') { throw 'Invalid fixture password format.' }
        foreach ($a in $data.accounts) {
            $hash = [BCrypt.Net.BCrypt]::HashPassword($plain,12)
            if (![BCrypt.Net.BCrypt]::Verify($plain,$hash)) { throw 'BCrypt verification failed.' }
            $a.hash = $hash
        }
        if ($userAnchor) {
            $ids = ($data.accounts | ForEach-Object { "('$($_.uid)'::uuid,'$($_.email)')" }) -join ','
            $existing = @(Invoke-Database 'user-db' "SELECT u.password_hash FROM public.users u JOIN (VALUES $ids) f(id,email) ON f.id=u.id AND f.email=u.email;")
            foreach ($stored in $existing) {
                if (![BCrypt.Net.BCrypt]::Verify($plain,$stored)) { throw 'The supplied password does not match an established fixture account. Existing credentials were preserved.' }
            }
        }
    } finally { $plain = $null }
}

# Each SQL file declares its expected rows in typed temporary tables. This helper only
# inserts by explicit primary key; a natural-key conflict aborts the service transaction.
$commonSql = @'
CREATE TEMP TABLE run_context AS SELECT
    current_setting('mvp.mode') AS mode, current_setting('mvp.reset')::bool AS reset,
    current_setting('mvp.reference')::timestamptz AS t, false AS established;
CREATE TEMP TABLE fixture_keys(table_name text, key_name text, key_value text,
                               PRIMARY KEY(table_name,key_value));
CREATE FUNCTION pg_temp.require(ok boolean, message text) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    IF ok IS DISTINCT FROM true THEN RAISE EXCEPTION '%', message; END IF;
END $$;
CREATE FUNCTION pg_temp.sync(rel text, pk text, restore_cols text[] DEFAULT '{}',
                            ignore_cols text[] DEFAULT '{}', repair_missing boolean DEFAULT false)
RETURNS void LANGUAGE plpgsql AS $$
DECLARE
    cond text := format('p.%1$I = e.%1$I', pk);
    expected_table text := format('pg_temp.%I', 'e_' || rel);
    target_table text := format('public.%I', rel);
    cols text; values_e text; assignments text; changed text;
    present boolean; permitted text[]; ctx record;
BEGIN
    SELECT * INTO STRICT ctx FROM run_context;
    SELECT string_agg(format('%I',attname),', ' ORDER BY attnum),
           string_agg(format('e.%I',attname),', ' ORDER BY attnum)
    INTO cols, values_e FROM pg_attribute WHERE attrelid=expected_table::regclass AND attnum>0 AND NOT attisdropped;
    -- Check required column existence/type compatibility even on an empty target.
    EXECUTE format('SELECT %s FROM %s LIMIT 0',cols,target_table);
    EXECUTE format('INSERT INTO fixture_keys SELECT %L,%L,%I::text FROM %s',rel,pk,pk,expected_table);
    IF NOT ctx.established THEN
        EXECUTE format('SELECT EXISTS(SELECT FROM %s e JOIN %s p ON %s)',expected_table,target_table,cond) INTO present;
        PERFORM pg_temp.require(NOT present,rel || ': fixture key exists without its service anchor');
    END IF;
    permitted := ignore_cols || CASE WHEN ctx.reset AND ctx.mode <> 'verify' THEN restore_cols ELSE ARRAY[]::text[] END;
    EXECUTE format('SELECT EXISTS(SELECT FROM %s e JOIN %s p ON %s,
        LATERAL jsonb_each(to_jsonb(e) - $1) field WHERE to_jsonb(p)->field.key IS DISTINCT FROM field.value)',
        expected_table,target_table,cond) INTO present USING permitted;
    PERFORM pg_temp.require(NOT present,rel || ': incompatible fixture identity, binding, timeline or progressed state');
    IF ctx.mode='verify' OR (ctx.established AND NOT (ctx.reset AND repair_missing)) THEN
        EXECUTE format('SELECT EXISTS(SELECT FROM %s e WHERE NOT EXISTS(SELECT FROM %s p WHERE %s))',
            expected_table,target_table,cond) INTO present;
        PERFORM pg_temp.require(NOT present,rel || ': missing after atomic initialization (withdrawn, edited, or removed)');
    END IF;
    IF ctx.mode='apply' THEN
        IF ctx.reset AND cardinality(restore_cols)>0 THEN
            SELECT string_agg(format('%1$I=e.%1$I',col),', '),
                   string_agg(format('p.%1$I IS DISTINCT FROM e.%1$I',col),' OR ')
              INTO assignments,changed FROM unnest(restore_cols) col;
            EXECUTE format('UPDATE %s p SET %s FROM %s e WHERE %s AND (%s)',
                target_table,assignments,expected_table,cond,changed);
        END IF;
        EXECUTE format('INSERT INTO %s (%s) SELECT %s FROM %s e WHERE NOT EXISTS(SELECT FROM %s p WHERE %s)',
            target_table,cols,values_e,expected_table,target_table,cond);
    END IF;
END $$;
'@

# Full reference scan only for explicit reset in a quiescent, isolated dataset.
# Include logical/JSON references (FK introspection alone misses cross-database consumers).
$dependencySql = @'
DO $$
DECLARE tab record; hit boolean; owned_key text;
BEGIN
    PERFORM pg_temp.require(NOT EXISTS(SELECT FROM pg_stat_activity WHERE datname=current_database()
        AND pid<>pg_backend_pid() AND backend_type='client backend'), 'Reset blocked: other database clients are connected');
    FOR tab IN SELECT c.oid, n.nspname, c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
        WHERE c.relkind IN ('r','p','f','m') AND n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema'
        ORDER BY n.nspname,c.relname
    LOOP
        SELECT key_name INTO owned_key FROM fixture_keys WHERE table_name=tab.relname AND tab.nspname='public' LIMIT 1;
        EXECUTE format('SELECT EXISTS(SELECT FROM %I.%I r WHERE
            NOT EXISTS(SELECT FROM fixture_keys k WHERE k.table_name=$1 AND k.key_value=to_jsonb(r)->>$2)
            AND EXISTS(SELECT FROM regexp_matches(lower(to_jsonb(r)::text),
                ''[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}|[0-9a-f]{32}|51m-?91[0-9][.]?[0-9]{2}'',''g'') m
                JOIN seed_identifiers i ON i.value=replace(replace(m[1],''-'',''''),''.'','''')))',tab.nspname,tab.relname)
            INTO hit USING tab.relname,owned_key;
        PERFORM pg_temp.require(NOT hit,format('Reset blocked by a dependent or unrecognized row in %I.%I',tab.nspname,tab.relname));
    END LOOP;
END $$;
'@

function Get-DataSql([string]$Service, [string]$Mode) {
    $scope = @{sites=$data.sites; vehicles=$data.vehicles; accounts=@($data.accounts | ForEach-Object {
        $item = $_.Clone()
        if ($Service -ne 'user-db') { $item.Remove('hash') }
        $item
    })}
    $json = ConvertTo-Json -InputObject $scope -Depth 12 -Compress
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($json))
    $body = if ($files.ContainsKey($Service)) { Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path "$PSScriptRoot/database/seeds/mvp" $files[$Service]) } else { '' }
    $guard = if ($Reset) { $dependencySql } else { '' }
    $resetValue = ([bool]$Reset).ToString().ToLowerInvariant()
    # JSON is sent over stdin, never through process arguments or the environment.
    return @"
BEGIN;
SET LOCAL search_path TO pg_catalog, public, pg_temp;
SET LOCAL timezone TO 'UTC';
SET LOCAL lock_timeout TO '5s';
SET LOCAL mvp.mode TO '$Mode';
SET LOCAL mvp.reset TO '$resetValue';
SET LOCAL mvp.reference TO '$appliedTime';
SELECT pg_advisory_xact_lock(hashtextextended('MVP-SEED-V1',0));
$commonSql
SELECT pg_temp.require(EXISTS(SELECT FROM pg_extension WHERE extname='uuid-ossp'), 'Missing uuid-ossp base schema extension')
    WHERE current_database() IN ('smartpark_user','smartpark_facility','smartpark_operations');
CREATE TEMP TABLE manifest AS SELECT convert_from(decode('$encoded','base64'),'UTF8')::jsonb AS j;
CREATE TEMP VIEW site_input AS SELECT s.* FROM manifest, jsonb_to_recordset(j->'sites') s(
    n text,id uuid, owner text,tenant uuid,code text,name text,address text,lat numeric,lon numeric, type text,model text,rate integer,
    capacity integer,backup integer,policy uuid,publication uuid,initial boolean,floors jsonb);
CREATE TEMP VIEW person_input AS SELECT a.* FROM manifest,jsonb_to_recordset(j->'accounts') a(
    n text,id uuid,uid uuid,role text,email text,phone text,tenant uuid,site uuid,hash text);
CREATE TEMP VIEW vehicle_input AS SELECT v.* FROM manifest,jsonb_to_recordset(j->'vehicles') v(id uuid,account uuid,plate text,canonical text);
CREATE TEMP VIEW owner_input AS SELECT s.*, a.uid,a.email,a.phone,'MVPSEED Owner '||a.n AS business,
    'T-'||replace(s.id::text,'-','') AS tenant_code,
    jsonb_build_object('name',s.name,'address',s.address,'lotType',s.type,'totalSlots',s.capacity,
      'floors',jsonb_array_length(s.floors),'devices','[]'::jsonb,'modelFileName',s.model,'hourlyRate',s.rate,
      'dailyRate',0,'nightRate',s.rate,'gracePeriodMinutes',0,'subscriptionMonthly',0,'subscriptionYearly',0,'policyAccepted',true) AS lot
    FROM site_input s JOIN person_input a ON a.role='owner' AND a.tenant=s.id WHERE s.initial;
CREATE TEMP TABLE seed_identifiers(value text PRIMARY KEY);
INSERT INTO seed_identifiers SELECT DISTINCT replace(m[1],'-','') FROM manifest,
    regexp_matches(lower(j::text),'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}|51m91[0-9]{3}','g') m;
$body
$guard
SELECT 'validated $Service ($Mode), expected owned rows='||count(*) FROM fixture_keys;
COMMIT;
"@
}

function Read-Anchor([string]$Service) {
    $first = $data.sites[0].id
    $row = Invoke-Database $Service @"
BEGIN READ ONLY;
SET LOCAL timezone TO 'UTC';
SELECT CASE WHEN code='T-'||replace(id::text,'-','') AND name='MVPSEED Owner 01'
    AND contact_email='owner01@mvpseed.example.test' AND deleted_at IS NULL AND status='ACTIVE'
    THEN to_char(created_at+interval '120 days','YYYY-MM-DD"T"HH24:MI:SS.US"Z"')
    ELSE 'COLLISION' END FROM public.tenants WHERE id='$first';
COMMIT;
"@
    if ($row -eq 'COLLISION') { throw "$Service anchor has incompatible ownership." }
    return $row
}

$data = New-Inventory
$expectedDistances = @(1239,3195,7194,13905,30061)
if (($data.sites.metres -join ',') -ne ($expectedDistances -join ',')) { throw 'Unexpected geographic membership.' }
$manualSlot = Get-FixtureId 'slot/01/CarParkingSpace_013'
if ($manualSlot -ne '1e3b8e2e-a9ac-527f-b58b-bf6fcf26d498' -or
    @($data.sites[0].floors[0].slots.Where({$_.id -eq $manualSlot -and !$_.backup})).Count -ne 1) { throw 'Fresh-booking bay mismatch.' }
$requestedTime = if ($PSBoundParameters.ContainsKey('ReferenceTime')) {
    if ($ReferenceTime -notmatch '^\d{4}-\d{2}-\d{2}T.+(Z|[+-]\d{2}:\d{2})$') { throw 'ReferenceTime requires ISO date/time with explicit offset.' }
    Format-Instant ([DateTimeOffset]::Parse($ReferenceTime,$invariant,[Globalization.DateTimeStyles]::None))
}
if ($Preview) {
    Show-Inventory $data
    Write-Host 'Offline inventory only. No databases inspected or changed.'
    return
}
if (!$IsolatedProject) {
    $IsolatedProject = Read-Host 'Name of the explicitly authorized isolated local Compose project (empty cancels)'
    if ($IsolatedProject -cnotmatch '^[a-z0-9][a-z0-9_-]*$') { throw 'An authorized isolated project is required.' }
}
$files = @{'user-db'='01-user.sql'; 'facility-db'='02-facility.sql'; 'operations-db'='03-operations.sql'}
$services = @('user-db','facility-db','operations-db')
if ($Reset) { $services += @('payment-db','notification-db','iot-db') }
$containers = @{}
$compose = @('compose','--project-name',$IsolatedProject,'-f',(Join-Path $root 'compose.schema-integration.yml'))
$mutex = [Threading.Mutex]::new($false,"SmartPark-MVP-SEED-V1-$IsolatedProject")
$locked = $false
$committed = @()
$phase = 'local target inspection'
try {
    try { $locked = $mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $locked = $true }
    if (!$locked) { throw 'Another seed invocation is active for this project.' }
    if ($Reset) { Write-Host 'Reset requires the operator to keep all host clients and workers quiescent through final verification.' }
    Resolve-Target
    foreach ($s in $services) {
        $db = 'smartpark_' + $s.Replace('-db','')
        $ok = Invoke-Database $s "SELECT current_database()='$db' AND current_user='$db' AND current_setting('server_version_num')::int/10000=16;"
        if ($ok -ne 't') { throw "Database/role/version mismatch: $s" }
    }
    $userAnchor = Read-Anchor 'user-db'
    $facilityAnchor = Read-Anchor 'facility-db'
    if (!$userAnchor -and $facilityAnchor) { throw 'Facility phase exists without User anchor; reconcile before seeding.' }
    if (!$facilityAnchor) {
        $siteList = ($data.sites | ForEach-Object { "'$($_.id)'::uuid" }) -join ','
        if ((Invoke-Database 'operations-db' "SELECT EXISTS(SELECT FROM public.structure_site_state WHERE site_id IN ($siteList));") -ne 'f') {
            throw 'Operations site state exists without Facility initialization; reconcile before seeding.'
        }
    }
    if ($facilityAnchor -and $facilityAnchor -ne $userAnchor -and !($Reset -and $requestedTime -eq $userAnchor)) {
        throw "Mixed timelines: User=$userAnchor Facility=$facilityAnchor. Inspect and resume the interrupted reset using its explicit User reference time."
    }
    if ($requestedTime -and $userAnchor -and $requestedTime -ne $userAnchor -and !$Reset) {
        throw "Dataset already uses $userAnchor. A different reference time requires explicit reset."
    }
    $appliedTime = if ($requestedTime) { $requestedTime } elseif ($userAnchor) { $userAnchor } else { Format-Instant $started }
    $modeName = if ($Reset) { 'reset' } else { 'normal' }
    Write-Host "Checkpoint A | project=$IsolatedProject | mode=$modeName | reference=$appliedTime | execution=$(Format-Instant $started)"
    $phase = 'credential preparation'
    New-PasswordHashes
    foreach ($s in $services) {
        $phase = "$s preflight"
        Write-Host $phase
        Invoke-Database $s (Get-DataSql $s 'check') | Where-Object { $_ }
    }
    foreach ($s in 'user-db','facility-db','operations-db') {
        $phase = "$s apply"
        Write-Host $phase
        Invoke-Database $s (Get-DataSql $s 'apply') | Where-Object { $_ }
        $committed += $s
    }
    foreach ($s in 'user-db','facility-db','operations-db') {
        $phase = "$s persisted-state verification"
        Invoke-Database $s (Get-DataSql $s 'verify') | Where-Object { $_ }
    }
    if ((Read-Anchor 'user-db') -ne $appliedTime -or (Read-Anchor 'facility-db') -ne $appliedTime) { throw 'Reference-time anchors disagree.' }
    Show-Inventory $data
    Write-Host "Verified Checkpoint A foundation in $($committed -join ', '). No commercial or visit fixtures implemented."
} catch {
    Write-Warning "Stopped at $phase. Acknowledged commits: [$($committed -join ', ')]. Inspect before retrying an ambiguous commit."
    throw
} finally {
    if ($locked) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
