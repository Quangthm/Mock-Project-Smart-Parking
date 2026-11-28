#requires -Version 7.2
<#
.SYNOPSIS
    Seed the MVP-SEED-V1 foundation, commercial fixtures, and lifecycle history in an authorized local Compose project.
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

function ConvertTo-SeedJson($Value) {
    [System.Text.Json.JsonSerializer]::Serialize[object]($Value, [System.Text.Json.JsonSerializerOptions]::Default)
}

function Get-SeedFingerprint($Value) {
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes((ConvertTo-SeedJson $Value))))
}

function Import-LedgerAssemblies {
    if ([Environment]::Version.Major -lt 10) { throw 'Checkpoint C requires PowerShell running on .NET 10 or newer.' }
    foreach ($service in 'OperationService','PaymentService') {
        foreach ($component in 'Domain','Application') {
            $name = "SmartParking.$service.$component"
            $bin = Join-Path $root "src/Services/$service/$name/bin"
            $assembly = @('Debug','Release') | ForEach-Object {Join-Path $bin "$_/net10.0/$name.dll"} |
                Where-Object {Test-Path -LiteralPath $_} | Select-Object -First 1
            if (!$assembly) { throw "Build $name for net10.0 before generating the C ledger." }
            $null = [Reflection.Assembly]::LoadFrom($assembly)
        }
    }
}

function New-LedgerSchedule($Data, [DateTimeOffset]$T, [DateTimeOffset]$HistoryStart, [int]$ProtectionLead) {
    $earliest = $HistoryStart.AddMinutes(20)
    $latest = $T.AddHours(-9)
    if (($latest-$earliest).TotalHours -lt 42) {
        throw "Strategy A cannot fit 75 reservations and 100 completed visits. This schedule needs 51h20m for admission, fifteen blocks and the final visits. Available=$($T-$HistoryStart), earliest=$HistoryStart, T=$T. Use a target with longer existing compatible history."
    }
    $step = [Math]::Floor(($latest-$earliest).TotalMinutes / 14)
    $siteSlots = @{}
    foreach ($site in $Data.sites) {
        $siteSlots[$site.n] = @($site.floors.slots | Where-Object {
            $_.kind -eq 'STANDARD' -and !$_.backup -and $_.id -ne $manualSlot
        } | Sort-Object { $_.code })
        if ($siteSlots[$site.n].Count -lt 12) { throw "Insufficient ordinary bays at $($site.code)." }
    }
    $bookings = [Collections.Generic.List[hashtable]]::new()
    $walkIns = [Collections.Generic.List[hashtable]]::new()
    foreach ($d in 1..5) {
        $n = '{0:00}' -f $d
        foreach ($i in 0..14) {
            $start = $earliest.AddMinutes($i*$step)
            $site = 1 + (($d-1+$i)%5)
            $status = if ($i -lt 9) {'COMPLETED'} elseif ($i -lt 11) {'CANCELLED'} elseif ($i -lt 13) {'EXPIRED'} else {'NO_SHOW'}
            $monthly = $i -eq 8 -and $d -in 1,3,4
            if ($monthly) { $site=$d; $start=$T.AddHours(-7) }
            if ($status -eq 'NO_SHOW') {
                $site=@(1,3,4)[($d+$i)%3]
                if ($d -gt 3) { $start=$start.AddMinutes(80) }
            }
            # A private bay index per driver keeps concurrent historical rotations disjoint.
            $slot = $siteSlots[('{0:00}' -f $site)][$d+4].id
            $replacement = $d -eq 2 -and $i -eq 0
            $overstay = $d -eq 5 -and $i -eq 0
            $label = if ($replacement) {'historical-replacement'} elseif ($overstay) {'historical-overstay'} else {"historical-$status"}
            $bookings.Add(@{key="c/$n/history/$i"; d=$d; v=1; site=$site; slot=$slot; start=$start; end=$start.AddHours(1)
                admit=$start.AddMinutes(-18); status=$status; monthly=$monthly; replacement=$replacement
                overstay=$overstay; label=$label})
        }
        foreach ($i in 0..10) {
            $monthly = $i -eq 10 -and $d -in 1,3,4
            $site = 1 + (($d-1+$i)%5)
            $entry = $earliest.AddMinutes($i*$step+90)
            if ($monthly) { $site=$d; $entry=$T.AddHours(-5) }
            $walkIns.Add(@{key="c/$n/walk/$i"; d=$d; v=1; site=$site; entry=$entry; exit=$entry.AddMinutes(60)
                scan=$siteSlots[('{0:00}' -f $site)][$d+4].id; monthly=$monthly; label='historical-walk-in'})
        }
        foreach ($r in 0..9) {
            $site = 1+(($d+$r)%5)
            $protect = $d -eq 1 -and $r -eq 0
            $zero = $d -eq 1 -and $r -eq 9
            $hours = if ($protect) { $ProtectionLead / 60 } else {5+2*$r}
            $start = $T.AddHours($hours)
            $minutes = if ($zero) {15} else {60}
            $admitOffset = $r*6+$d
            if ($zero) { $admitOffset += 35 }
            $label = if ($protect) {'protection-window'} elseif ($zero) {'zero-deposit'} else {'future-confirmed'}
            $bookings.Add(@{key="c/$n/future/$r"; d=$d; v=1+(($r+$d)%3); site=$site
                slot=$siteSlots[('{0:00}' -f $site)][4].id; start=$start; end=$start.AddMinutes($minutes)
                admit=$T.AddMinutes(-4).AddSeconds($admitOffset); status='CONFIRMED'; label=$label})
        }
        $start = $T.AddMinutes(-35)
        $stop = if ($d -eq 4) { $T.AddMinutes(-5) } else { $start.AddHours(1) }
        $label = switch ($d) {1 {'early-arrival'} 4 {'active-overstay'} 5 {'incorrect-slot'} default {'active-linked'}}
        $bookings.Add(@{key="c/$n/active"; d=$d; v=1; site=$d; slot=$siteSlots[$n][0].id
            start=$start; end=$stop; admit=$start.AddMinutes(-45); status='PARKING'; label=$label})
    }
    foreach ($placement in @(@(1,2,2,1),@(1,3,2,2),@(2,2,3,1),@(2,3,3,2),@(3,2,1,1),@(3,3,1,2),@(4,2,4,1),@(5,2,4,0))) {
        $d,$v,$site,$bay = $placement
        $scan = if ($bay) { $siteSlots[('{0:00}' -f $site)][$bay].id }
        $walkIns.Add(@{key="c/$d/open-walk/$v"; d=$d; v=$v; site=$site; entry=$T.AddMinutes(-24+$v)
            exit=$null; scan=$scan; label=if ($scan) {'active-walk-in'} else {'unlocated-walk-in'}})
    }
    $bookings.Add(@{key='c/04/pending'; d=4; v=3; site=1; slot=$siteSlots['01'][3].id; start=$T.AddHours(3)
        end=$T.AddHours(4); admit=$T.AddMinutes(-1); status='PENDING_PAYMENT'; label='pending-payment'})
    @{ bookings=$bookings; walkIns=$walkIns; slots=$siteSlots }
}

function Get-LedgerPolicy($Commercial, $Site, [DateTimeOffset]$At, $PlatformHistory) {
    $rows = @($PlatformHistory | Where-Object { $_.begins -le $At -and (!$_.ends -or $At -lt $_.ends) })
    if ($rows.Count -ne 1) { throw "Expected exactly one compatible platform revision at $At." }
    $row = $rows[0]
    $values = [Collections.Generic.Dictionary[string,int]]::new()
    foreach ($pair in ($row.values.GetEnumerator())) { $values.Add($pair.Key,[int]$pair.Value) }
    $bounds = [Collections.Generic.Dictionary[string,SmartParking.OperationService.Domain.Reservations.PolicyBound]]::new()
    $bounds.Add('ARRIVAL_LATE_TOLERANCE',[SmartParking.OperationService.Domain.Reservations.PolicyBound]::new(15,30))
    $overrides = [Collections.Generic.Dictionary[string,int]]::new()
    if ($Commercial.late) { $overrides.Add('ARRIVAL_LATE_TOLERANCE',$Commercial.late) }
    $revisions = [SmartParking.OperationService.Domain.Reservations.ReservationPolicyRevision[]]@(
        [SmartParking.OperationService.Domain.Reservations.ReservationPolicyRevision]::new(
            $row.id,$row.version,$null,$null,$values,$bounds,$null,'Asia/Ho_Chi_Minh')
        [SmartParking.OperationService.Domain.Reservations.ReservationPolicyRevision]::new(
            $Commercial.sitepolicy,1,[guid]$Site.tenant,[guid]$Site.id,$overrides,$null,[bool]$Commercial.late,[NullString]::Value)
    )
    [SmartParking.OperationService.Domain.Reservations.ReservationPolicyResolver]::ResolveForSite($revisions,$Site.tenant,$Site.id)
}

function New-LedgerInvoice($Order, $Id, $Price, $Start, $End, $Paid, $Transaction) {
    $snapshot = [SmartParking.PaymentService.Domain.Entities.InvoicePricingSnapshot]::new()
    $snapshot.OrderCode = $Order.order_code
    $snapshot.AccountId = $Order.account_id
    $snapshot.CustomerType = if ($Order.account_id) {'REGISTERED_DRIVER'} else {'GUEST_DRIVER'}
    $snapshot.PlateNumber = $Order.plate_number
    $snapshot.EntryTime = $Start
    $snapshot.PricingCutoffAt = $End
    $snapshot.TariffId = $Price.AppliedTariffId
    $snapshot.TariffVersion = $Price.AppliedPolicyVersion
    $snapshot.PolicyName = $Price.AppliedTariffName
    $snapshot.SubtotalParkingFee = $Price.FinalAmountVnd
    $snapshot.FinalTotalAmount = $Price.FinalAmountVnd
    $snapshot.IsSettled = $true
    $snapshot.Status = 'PAID'
    $invoiceMinutes = 0
    if ($Transaction) {
        $invoiceMinutes = $Price.InvoiceRoundedMinutes
        $snapshot.TotalDurationMinutes = $invoiceMinutes
        $snapshot.BillableDurationMinutes = [Math]::Ceiling($Price.BilledMinutes)
        $snapshot.FreeParkingGraceMinutes = $Price.FreeParkingGraceMinutes
        $snapshot.PaidAt = $Paid
        $snapshot.PaymentMethod = 'CASH'
        $snapshot.TransactionReference = $Transaction
        foreach ($segment in $Price.Segments) {
            $item = [SmartParking.PaymentService.Domain.Entities.InvoiceLineItem]::new()
            $item.ItemType = 'PARKING_TIME_BLOCK'
            $item.Description = "Parking time block $($segment.BlockCode)"
            $item.StartTime = $segment.SegmentStart
            $item.EndTime = $segment.SegmentEnd
            $item.DurationMinutes = [Math]::Round($segment.OverlapMinutes)
            $item.ChargeableMinutes = $item.DurationMinutes
            $item.UnitRate = $segment.RatePerHourVnd
            $item.Amount = $segment.RawChargeVnd
            $snapshot.LineItems.Add($item)
        }
    }
    @{id=$Id; tenant_id=$Order.tenant_id; site_id=$Order.site_id; payment_order_id=$Order.id
        reference_type=$Order.reference_type; reference_id=$Order.reference_id
        session_id=if ($Transaction) {$Order.reference_id}; invoice_number="MVP-C-$Id"
        total_parked_minutes=$invoiceMinutes; base_parking_fee=$Price.FinalAmountVnd; final_amount=$Price.FinalAmountVnd
        pricing_snapshot=(ConvertTo-SeedJson $snapshot); created_at=$Paid}
}

function New-BookingRows($Ledger,$Data,$Schedule,$Booking,$PlatformHistory) {
    $b = $Booking
    foreach ($optional in 'replacement','monthly','overstay') {
        if (!$b.ContainsKey($optional)) { $b[$optional]=$false }
    }
    $site = $Data.sites[$b.site-1]
    $commercial = $Data.commercial[$b.site-1]
    $driver = '{0:00}' -f $b.d
    $person = $Data.accounts.Where({$_.role -eq 'driver' -and $_.n -eq $driver})[0]
    $plate = $Data.vehicles.Where({$_.id -eq (Get-FixtureId "vehicle/$driver/$($b.v)")})[0]
    $id = Get-FixtureId "$($b.key)/request"
    $orderId = Get-FixtureId "$($b.key)/order"
    $quoteId = Get-FixtureId "$($b.key)/quote"
    $decisionId = Get-FixtureId "$($b.key)/decision"
    $issued = $b.admit.AddSeconds(-30)
    $admitted = $b.admit.AddSeconds(-5)
    $decided = $b.admit.AddSeconds(-2)
    $deadline = $admitted.AddSeconds(15)
    $valid = $issued.AddMinutes(10)
    $policy = Get-LedgerPolicy $commercial $site $issued $PlatformHistory
    $window = [SmartParking.OperationService.Domain.Reservations.ReservationInterval]::new($b.start,$b.end)
    if (!$policy.AllowsWindow($window,$b.admit)) { throw "Booking window rejected: $($b.key)" }
    $price = $Ledger.calculator.Calculate([SmartParking.PaymentService.Domain.Pricing.PricingCalculationRequest]::new(
        $site.tenant,$site.id,'CAR',$b.start,$b.end,$Ledger.tariffs[$site.id],$null,$null))
    $amount = [decimal]$price.FinalAmountVnd
    $zero = $amount -eq 0
    if ($zero -ne ($b.label -eq 'zero-deposit')) { throw "Unexpected booking amount: $($b.key)" }
    $hold = if (!$zero) {$policy.DeadlinesFor($window,$b.admit).PaymentDueAt}
    $paid = $b.status -in 'COMPLETED','CONFIRMED','PARKING','NO_SHOW'
    $orderAt = $b.admit.AddSeconds(1)
    $transfer = if ($paid -and !$zero) {$b.admit.AddSeconds(30)}
    $verified = if ($transfer) {$transfer.AddSeconds(1)}
    $confirmed = if ($zero) {$b.admit} elseif ($verified) {$verified.AddSeconds(1)}
    $cancelled = if ($b.status -eq 'CANCELLED') {$b.admit.AddMinutes(2)}
    $terminalAt = switch ($b.status) {
        'CANCELLED' {$cancelled}
        'EXPIRED' {$hold}
        'NO_SHOW' {
            if (!$policy.ArrivalLateToleranceEnabled) { throw 'No-show requires enabled late tolerance.' }
            $b.start.AddMinutes($policy.Values['ARRIVAL_LATE_TOLERANCE']).AddSeconds(1)
        }
    }
    foreach ($at in @($b.admit,$confirmed,$terminalAt) | Where-Object { $_ }) {
        if ((Get-LedgerPolicy $commercial $site $at $PlatformHistory).Platform -ne $policy.Platform) {
            throw "Policy boundary crosses represented workflow $($b.key). Choose a compatible historical window."
        }
    }
    if ($confirmed -and ($confirmed -ge $b.start -or ($hold -and $confirmed -ge $hold))) { throw 'Invalid confirmation timing.' }
    $intent = [SmartParking.OperationService.Application.Reservations.QuoteReservationIntent]::new(
        $plate.canonical,$b.slot,$b.start,$b.end,'PAYOS',$quoteId)
    $binding = [SmartParking.PaymentService.Application.Reservations.ReviewedQuoteBinding]::new(
        $person.id,$plate.canonical,$site.tenant,$site.id,$b.slot,$b.start,$b.end,'PAYOS',
        [SmartParking.PaymentService.Application.Reservations.QuoteCoverage]::new($false,'none-v1'),
        $policy.Platform.Id,$policy.Platform.Version,$policy.Site.Id,$policy.Site.Version)
    $proof = [SmartParking.PaymentService.Application.Reservations.ReviewedQuoteProof]::new(
        $quoteId,'ISSUED',$valid,$binding,$commercial.tariffrev,$amount,0,$amount,'VND','FULL',
        'Non-refundable deposit applies if cancelled within 15 minutes of reservation start time.',$price)
    $quoteEntity = [SmartParking.PaymentService.Domain.Entities.ReservationQuote]@{
        Id=$quoteId; AccountId=$person.id; TenantId=$site.tenant; SiteId=$site.id; RequestedSlotId=$b.slot
        StartTime=$b.start; EndTime=$b.end; ValidUntil=$valid; BookedAmount=$amount; DepositAmount=$amount
        Currency='VND'; TariffRevisionId=$commercial.tariffrev; CoverageVersion='none-v1'
        Status='ACCEPTED'; ReviewedEvidence=(ConvertTo-SeedJson $proof)
    }
    $null = [SmartParking.PaymentService.Application.Reservations.ReviewedQuoteProof]::From($quoteEntity)
    $fingerprintInput = [ordered]@{WorkflowId=[guid]$id; UserId=[guid]$person.uid; AccountId=[guid]$person.id
        Role='driver'; Operation='CREATE_RESERVATION'; AdmittedAt=$admitted; OverallDeadline=$deadline}
    $Ledger.account_access_decisions.Add(@{id=$decisionId; workflow_id=$id; user_id=$person.uid; account_id=$person.id
        role='driver'; operation='CREATE_RESERVATION'; request_fingerprint=(Get-SeedFingerprint $fingerprintInput); allowed=$true; decided_at=$decided})
    $Ledger.reservation_requests.Add(@{id=$id; user_id=$person.uid; account_id=$person.id; idempotency_key=(Get-FixtureId "$($b.key)/idempotency")
        operation='CREATE_RESERVATION'; intent_json=(ConvertTo-SeedJson $intent); fingerprint=(Get-SeedFingerprint $intent)
        admitted_at=$admitted; overall_deadline=$deadline; attempt_id=(Get-FixtureId "$($b.key)/attempt")
        decision_id=$decisionId; decided_at=$decided; reservation_id=$id; state=if ($zero) {'CONFIRMED'} else {'HOLD_CREATED'}
        account_check='SUCCEEDED'; plate_check='SUCCEEDED'; slot_check='SUCCEEDED'; resolved_site_id=$site.id
        resolved_tenant_id=$site.tenant; vehicle_binding_revision=1; vehicle_binding_checked_at=$decided; vehicle_plate=$plate.canonical})
    $Ledger.reservation_quotes.Add(@{id=$quoteId; tenant_id=$site.tenant; site_id=$site.id; account_id=$person.id
        requested_slot_id=$b.slot; start_time=$b.start; end_time=$b.end; booked_amount=$amount; deposit_amount=$amount
        prepayment_mode='FULL'; currency='VND'; tariff_id=$commercial.tariff; tariff_revision_id=$commercial.tariffrev
        coverage_version='none-v1'; valid_until=$valid; status='ACCEPTED'; reviewed_evidence=$quoteEntity.ReviewedEvidence
        created_at=$issued; updated_at=$orderAt})
    $code = (810000000000000 + $Ledger.reservation_quotes.Count).ToString($invariant)
    $order = @{id=$orderId; tenant_id=$site.tenant; site_id=$site.id; account_id=$person.id; order_code=$code
        idempotency_key=$id; order_type='RESERVATION_FEE'; reference_type='RESERVATION'; reference_id=$id; quote_id=$quoteId
        reviewed_method='PAYOS'; reviewed_currency='VND'; plate_number=$plate.canonical; amount=$amount; final_amount=$amount
        status=if ($paid) {'PAID'} else {'PENDING'}; expires_at=$hold; created_at=$orderAt
        updated_at=if ($verified) {$verified} else {$orderAt}}
    $Ledger.payment_orders.Add($order)
    $receipt = if ($zero) {Get-FixtureId "$($b.key)/invoice"}
    $collection = if ($transfer) {Get-FixtureId "$($b.key)/collection"}
    $effect = if ($confirmed) {Get-FixtureId "$($b.key)/confirmation"}
    $collectionProof = if ($collection) {
        $stableAmount = $amount.ToString('G29',$invariant)
        "$(([guid]$collection).ToString('N'))/$(([guid]$orderId).ToString('N'))/$($transfer.UtcTicks)/$stableAmount/VND/PAYOS"
    }
    if ($collection -or $b.status -eq 'PENDING_PAYMENT') {
        $tx = if ($collection) {$collection} else {Get-FixtureId "$($b.key)/checkout-attempt"}
        $Ledger.payment_transactions.Add(@{id=$tx; tenant_id=$site.tenant; payment_order_id=$orderId; transaction_type='PAYMENT'
            payment_method='PAYOS'; idempotency_key=(Get-FixtureId "$($b.key)/payment-attempt-key")
            gateway_transaction_id=if ($collection) {"mvp-c-synthetic-link-$tx"}
            amount=$amount; created_at=$orderAt.AddSeconds(1)
            status=if ($collection) {'SUCCESS'} else {'PENDING'}; paid_at=$transfer; provider_verified_at=$verified
            provider_collection_reference=if ($collection) {"mvp-c-synthetic-collection-$tx"}})
    }
    if ($receipt) { $Ledger.invoices.Add((New-LedgerInvoice $order $receipt $price $b.start $b.end $orderAt $null)) }
    $effectiveSlot = if ($b.replacement) {$Schedule.slots[$site.n][11].id} else {$b.slot}
    $Ledger.reservations.Add(@{id=$id; request_id=$id; quote_id=$quoteId; quote_valid_until=$valid; tariff_version=$commercial.tariffrev
        coverage_version='none-v1'; is_covered=$false; covered_amount=0; zero_receipt_id=$receipt; selected_payment_method='PAYOS'
        booked_amount=$amount; deposit_amount=$amount; currency='VND'; payment_order_id=if ($paid) {$orderId}
        confirmation_state=if ($confirmed) {'CONFIRMED'} else {'AWAITING_COLLECTION'}; collection_id=$collection
        collection_fingerprint=$collectionProof; confirmation_effect_id=$effect; confirmed_at=$confirmed; confirmation_evaluated_at=$confirmed
        tenant_id=$site.tenant; site_id=$site.id; account_id=$person.id; target_slot_id=$b.slot
        active_claim_slot_id=if ($b.status -notin @('CANCELLED','EXPIRED','NO_SHOW')) {$effectiveSlot}
        original_plate=$plate.canonical; normalized_plate=$plate.canonical; vehicle_type='CAR'; expected_start_time=$b.start
        expected_end_time=$b.end; claim_end_time=$b.end.AddMinutes($policy.Values['INTER_BOOKING_BUFFER'])
        hold_expires_at=$hold; cancelled_at=$cancelled; accepted_operational_policy_snapshot=(ConvertTo-SeedJson $policy)
        status=$b.status; created_at=$b.admit})
    if ($cancelled) {
        $Ledger.reservation_cancellation_evidence.Add(@{id=(Get-FixtureId "$($b.key)/cancel"); reservation_id=$id; account_id=$person.id
            cancelled_at=$cancelled; reason='VOLUNTARY'
            policy_snapshot=(ConvertTo-SeedJson (Get-LedgerPolicy $commercial $site $cancelled $PlatformHistory))})
    }
    $Ledger.scenarios.Add(@{label=$b.label; driver=$driver; reservation=$id; quote=$quoteId; order=$orderId; receipt=$receipt
        slot=$b.slot; effective=if ($b.status -in @('COMPLETED','PARKING')) {$effectiveSlot}
        site=$site.code; plate=$plate.canonical; start=$b.start; end=$b.end; terminal=$terminalAt; deadline=$hold
        protection=if ($b.label -eq 'protection-window') {$b.start.AddMinutes(-$policy.Values['RESERVATION_PROTECTION_WINDOW'])}})
    if ($b.status -in 'COMPLETED','PARKING') {
        $entry = if ($b.label -eq 'early-arrival') {$b.start.AddMinutes(-4)} else {$b.start.AddMinutes(2)}
        $scanAt = $b.start.AddMinutes(4)
        $exit = if ($b.overstay) {$b.end.AddMinutes(20)} elseif ($b.status -eq 'COMPLETED') {$b.end.AddMinutes(-1)}
        $scan = if ($b.label -eq 'incorrect-slot') {$Schedule.slots[$site.n][3].id} else {$effectiveSlot}
        $session = New-VisitRows $Ledger $Data @{key=$b.key; d=$b.d; v=$b.v; site=$b.site; entry=$entry; exit=$exit
            scan=$scan; scanAt=$scanAt; monthly=$b.monthly; label=$b.label} $id $person.id
        if ($b.replacement) {
            $Ledger.slot_allocations.Add(@{id=(Get-FixtureId "$($b.key)/original-allocation"); reservation_id=$id
                tenant_id=$site.tenant; site_id=$site.id; slot_id=$b.slot; allocated_start_time=$b.start
                allocated_end_time=$b.start.AddMinutes(1); allocation_status='RELEASED'; created_at=$b.start.AddMinutes(-5)})
        }
        $mismatch = $scan -ne $effectiveSlot
        $Ledger.slot_allocations.Add(@{id=(Get-FixtureId "$($b.key)/allocation"); reservation_id=$id
            parking_session_id=if (!$mismatch) {$session}; site_id=$site.id; tenant_id=$site.tenant; slot_id=$effectiveSlot
            allocated_start_time=if ($b.replacement) {$b.start.AddMinutes(1)} else {$b.start}
            allocated_end_time=if ($exit) {$exit} else {$b.end}; allocation_status=if ($exit) {'RELEASED'} elseif ($mismatch) {'RESERVED'} else {'OCCUPIED'}
            created_at=if ($b.replacement) {$b.start.AddMinutes(1)} else {$b.start.AddMinutes(-5)}})
    }
}

function New-VisitRows($Ledger,$Data,$Visit,$ReservationId,$AccountId) {
    $v = $Visit
    $site = $Data.sites[$v.site-1]
    $driver = '{0:00}' -f $v.d
    $plate = $Data.vehicles.Where({$_.id -eq (Get-FixtureId "vehicle/$driver/$($v.v)")})[0]
    $id = Get-FixtureId "$($v.key)/session"
    $scannedAt = if ($v.scan) {
        if ($v.ContainsKey('scanAt')) {$v.scanAt} else {$v.entry.AddMinutes(1)}
    }
    $monthly = $v.ContainsKey('monthly') -and $v.monthly
    if ($monthly -and (!$v.exit -or $v.entry -lt $Ledger.t.AddDays(-1) -or
        !$Data.purchases.Where({$_.vehicle -eq $plate.id -and $_.site -eq $site.id}))) {
        throw "Monthly coverage mismatch: $($v.key)"
    }
    $checkout = $null
    $paidAt = $null
    if ($v.exit -and !$ReservationId -and !$monthly) {
        $cutoff = $v.exit.AddMinutes(-2)
        $paidAt = $v.exit.AddMinutes(-1)
        $price = $Ledger.calculator.Calculate([SmartParking.PaymentService.Domain.Pricing.PricingCalculationRequest]::new(
            $site.tenant,$site.id,'CAR',$v.entry,$cutoff,$Ledger.tariffs[$site.id],$id,$null))
        $amount = $price.FinalAmountVnd
        if ($amount -le 0) { throw "Expected a paid walk-in: $($v.key)" }
        $checkout = Get-FixtureId "$($v.key)/checkout"
        $transaction = Get-FixtureId "$($v.key)/cash"
        $code = (820000000000000+$Ledger.invoices.Count).ToString($invariant)
        $order = @{id=$checkout; tenant_id=$site.tenant; site_id=$site.id; order_code=$code; idempotency_key="checkout:parking_session:$(([guid]$id).ToString('N'))"
            account_id=$null; order_type='PARKING_FEE'; reference_type='PARKING_SESSION'; reference_id=$id
            plate_number=$plate.canonical; amount=$amount; final_amount=$amount; status='PAID'
            expires_at=$cutoff.AddMinutes(15); created_at=$cutoff; updated_at=$paidAt}
        $Ledger.payment_orders.Add($order)
        $Ledger.payment_transactions.Add(@{id=$transaction; tenant_id=$site.tenant; payment_order_id=$checkout
            transaction_type='PAYMENT'; payment_method='CASH'; amount=$amount; status='SUCCESS'; paid_at=$paidAt; created_at=$paidAt})
        $Ledger.invoices.Add((New-LedgerInvoice $order (Get-FixtureId "$($v.key)/invoice") $price $v.entry $cutoff $paidAt $transaction))
    }
    $Ledger.parking_sessions.Add(@{id=$id; tenant_id=$site.tenant; site_id=$site.id; reservation_id=$ReservationId; account_id=$AccountId
        current_slot_id=$v.scan; location_scanned_at=$scannedAt; normalized_plate=$plate.canonical; original_plate=$plate.canonical
        vehicle_type='CAR'; ticket_reference="GATE-$(([guid]$id).ToString('N'))"; entry_time=$v.entry; exit_time=$v.exit
        checkout_payment_order_id=$checkout; checkout_paid_at=$paidAt; session_status=if ($v.exit) {'COMPLETED'} else {'ACTIVE'}
        version=1+[int][bool]$scannedAt+[int][bool]$v.exit+2*[int][bool]$checkout; created_at=$v.entry})
    $Ledger.visits.Add(@{id=$id; driver=$driver; plate=$plate.canonical; site=$site.code; slot=$v.scan; entry=$v.entry; scan=$scannedAt
        exit=$v.exit; monthly=$monthly; label=$v.label; checkout=$checkout})
    if (!$v.exit -and $v.scan) {
        $Ledger.occupied.Add(@{id=$v.scan; tenant_id=$site.tenant; site_id=$site.id; updated_at=$scannedAt})
    }
    $id
}

function New-Ledger($Data,[DateTimeOffset]$T,[DateTimeOffset]$HistoryStart,$PlatformHistory) {
    $current = Get-LedgerPolicy $Data.commercial[1] $Data.sites[1] $T $PlatformHistory
    $lead = $current.Values['ALLOCATION_LEAD_TIME']
    $protection = $current.Values['RESERVATION_PROTECTION_WINDOW']
    if ($protection -le $lead+1) { throw 'No protection-only fixture interval under the effective policy.' }
    $offset = [Math]::Min([Math]::Max(120,$lead+1),$protection-1)
    $schedule = New-LedgerSchedule $Data $T $HistoryStart $offset
    $ledger = @{t=$T; begins=$HistoryStart; calculator=[SmartParking.PaymentService.Domain.Pricing.ParkingPricingCalculator]::new(); tariffs=@{}}
    foreach ($name in 'account_access_decisions','reservation_requests','reservation_quotes','payment_orders','payment_transactions',
        'invoices','reservations','reservation_cancellation_evidence','parking_sessions','slot_allocations','scenarios','visits','occupied') {
        $ledger[$name] = [Collections.Generic.List[hashtable]]::new()
    }
    foreach ($c in $Data.commercial) {
        $blocks = [SmartParking.PaymentService.Domain.Pricing.TimeBlockRule[]]@(
            [SmartParking.PaymentService.Domain.Pricing.TimeBlockRule]::new('ALL_DAY',[TimeOnly]::MinValue,[TimeOnly]::MinValue,$c.rules.timeBlocks[0].ratePerHourVnd))
        $site = $Data.sites.Where({$_.id -eq $c.site})[0]
        $ledger.tariffs[$c.site] = [SmartParking.PaymentService.Domain.Pricing.PricingPolicySnapshot]::new(
            $c.tariff,"$($site.code) CAR",1,'CAR',$c.grace,$blocks,'Asia/Ho_Chi_Minh','HALF_UP_THOUSAND')
    }
    foreach ($booking in $schedule.bookings) { New-BookingRows $ledger $Data $schedule $booking $PlatformHistory }
    foreach ($visit in $schedule.walkIns) { $null = New-VisitRows $ledger $Data $visit $null $null }
    $ledger.Remove('tariffs')
    $ledger.Remove('calculator')
    $ledger.ownedIds = @(
        foreach ($name in 'account_access_decisions','reservation_requests','reservation_quotes','payment_orders',
            'payment_transactions','invoices','reservations','reservation_cancellation_evidence','parking_sessions','slot_allocations') {
            foreach ($row in $ledger[$name]) {
                $row.id
                foreach ($key in 'idempotency_key','attempt_id','confirmation_effect_id') {
                    if ($row.ContainsKey($key) -and [string]$row[$key] -match '^[0-9a-f-]{36}$') { $row[$key] }
                }
            }
        }
    ) | Sort-Object -Unique
    Assert-Ledger $ledger $Data $current
    $ledger
}

function Show-Ledger($Ledger, [string]$Evidence) {
    Write-Host "Checkpoint C | T=$(Format-Instant $Ledger.t) execution=$(Format-Instant $started) history=$(Format-Instant $Ledger.begins) duration=$($Ledger.t-$Ledger.begins) ($Evidence)"
    foreach ($driver in '01','02','03','04','05') {
        $states = $Ledger.scenarios.Where({$_.driver -eq $driver}) | Group-Object {$_.label} | ForEach-Object {"$($_.Name)=$($_.Count)"}
        $visits = $Ledger.visits.Where({$_.driver -eq $driver})
        $driverId = Get-FixtureId "account/driver/$driver"
        $last = $Ledger.reservations | Where-Object {$_.account_id -eq $driverId -and $_.status -in @('CONFIRMED','PARKING','COMPLETED')} |
            Sort-Object {$_.confirmed_at},{$_.id} -Descending | Select-Object -First 1
        $lastSite = $data.sites.Where({$_.id -eq $last.site_id})[0].code
        Write-Host "Driver $driver | $($states -join ', ') | completed visits=$($visits.Where({$_.exit}).Count) active=$($visits.Where({!$_.exit}).Count) | latest confirmed=$lastSite $($last.id)"
    }
    foreach ($row in $Ledger.scenarios) {
        Write-Host "$($row.label) $($row.site) $($row.plate) request/reservation=$($row.reservation) quote=$($row.quote) order=$($row.order) requested=$($row.slot) allocation=$($row.effective) start=$($row.start) deadline=$($row.deadline) protection=$($row.protection) receipt=$($row.receipt)"
    }
    foreach ($visit in $Ledger.visits | Where-Object {!$_.exit -or $_.monthly -or $_.label -in @('historical-overstay','historical-replacement')}) {
        Write-Host "$($visit.label) $($visit.site) $($visit.plate) session=$($visit.id) scan=$($visit.slot) monthly=$($visit.monthly)"
    }
    foreach ($site in $data.sites) {
        $orders = $Ledger.payment_orders.Where({$_.site_id -eq $site.id})
        $siteBookings = $Ledger.reservations.Where({$_.site_id -eq $site.id}).Count
        $siteVisits = $Ledger.parking_sessions.Where({$_.site_id -eq $site.id})
        $totals = $orders | Group-Object {$_.order_type},{$_.status} | ForEach-Object {
            "$($_.Name)=$($_.Count)/$(($_.Group.final_amount | Measure-Object -Sum).Sum)VND"
        }
        Write-Host "$($site.code) | bookings=$siteBookings visits=$($siteVisits.Where({$_.exit_time}).Count) active=$($siteVisits.Where({!$_.exit_time}).Count) | $($totals -join ', ') | $($Ledger.availability[$site.id])"
    }
    $success = $Ledger.payment_transactions.Where({$_.status -eq 'SUCCESS'}).Count
    $pending = $Ledger.payment_transactions.Where({$_.status -eq 'PENDING'}).Count
    Write-Host "Synthetic C totals: bookings=$($Ledger.reservations.Count) visits=$($Ledger.parking_sessions.Count) allocations=$($Ledger.slot_allocations.Count) orders=$($Ledger.payment_orders.Count) transactions=$($Ledger.payment_transactions.Count) (success=$success pending=$pending) invoices=$($Ledger.invoices.Count)."
    Write-Host "Driver 06 history is empty. Handoff bay $manualSlot remains free. The pending fixture does not schedule expiry."
    Write-Host 'Provider-shaped references are synthetic. Use only local Payment reads for these references.'
}

function Assert-Ledger($Ledger,$Data,$Policy) {
    foreach ($expected in @{reservations=131;reservation_requests=131;account_access_decisions=131;reservation_quotes=131
        parking_sessions=113;occupied=12;slot_allocations=51;reservation_cancellation_evidence=10;payment_orders=183
        payment_transactions=162;invoices=53}.GetEnumerator()) {
        if ($Ledger[$expected.Key].Count -ne $expected.Value) { throw "Wrong C row count for $($expected.Key)." }
    }
    $open = $Ledger.parking_sessions.Where({!$_.exit_time})
    if (($open.normalized_plate | Sort-Object -Unique).Count -ne 13 -or
        ($Ledger.occupied.id | Sort-Object -Unique).Count -ne 12 -or
        $manualSlot -in $Ledger.reservations.target_slot_id -or $manualSlot -in $Ledger.parking_sessions.current_slot_id -or
        $Ledger.visits.Where({$_.monthly}).Count -ne 6) { throw 'Invalid occupied, monthly, or handoff fixtures.' }
    foreach ($group in ($Ledger.parking_sessions | Group-Object {$_.normalized_plate})) {
        $previous = $null
        foreach ($visit in $group.Group | Sort-Object {$_.entry_time}) {
            if ($previous -and (!$previous.exit_time -or $previous.exit_time -gt $visit.entry_time)) {
                throw "Overlapping actual visits: $($group.Name)"
            }
            $previous = $visit
        }
    }
    foreach ($site in $Data.sites) {
        $accepted = $Ledger.reservations.Where({$_.site_id -eq $site.id -and $_.confirmed_at})
        $quota = $Data.commercial[[int]$site.n-1].quota
        foreach ($booking in $accepted) {
            $overlaps = $accepted.Where({$_.id -ne $booking.id -and
                $_.expected_start_time -lt $booking.claim_end_time -and $booking.expected_start_time -lt $_.claim_end_time})
            if ($overlaps.Where({$_.target_slot_id -eq $booking.target_slot_id -or $_.normalized_plate -eq $booking.normalized_plate}).Count) {
                throw "Overlapping reservation commitment at $($booking.id)."
            }
            if ($quota -and ($accepted.Where({
                $_.expected_start_time -le $booking.expected_start_time -and $booking.expected_start_time -lt $_.claim_end_time
            }).Count -gt $quota)) { throw "Fixture quota exceeded at $($booking.id)." }
        }
    }
    foreach ($driver in '01','02','03','04','05') {
        $scenarios = $Ledger.scenarios.Where({$_.driver -eq $driver})
        $visits = $Ledger.visits.Where({$_.driver -eq $driver})
        if ($scenarios.Where({$_.start -gt $Ledger.t -and $_.label -ne 'pending-payment'}).Count -ne 10 -or
            $scenarios.Where({$_.label -like 'historical-*'}).Count -ne 15 -or $visits.Where({$_.exit}).Count -ne 20) {
            throw "Incorrect Driver $driver history quantities."
        }
        $driverId = Get-FixtureId "account/driver/$driver"
        $last = $Ledger.reservations | Where-Object {$_.account_id -eq $driverId -and $_.status -in 'CONFIRMED','COMPLETED','PARKING'} |
            Sort-Object {$_.confirmed_at},{$_.id} -Descending | Select-Object -First 1
        if ($last.site_id -ne $Data.sites[[int]$driver-1].id) { throw "Unexpected latest confirmation for Driver $driver." }
    }
    $ledger.availability = @{}
    $allocationType = [SmartParking.OperationService.Domain.Reservations.CapacityAllocation]
    $projection = [TimeSpan]::FromMinutes($Policy.Values['OCCUPANCY_VALIDITY_TIMESPAN'])
    $at = $Ledger.t
    foreach ($site in $Data.sites) {
        $commercial = $Data.commercial[[int]$site.n-1]
        $claims = [Collections.Generic.List[SmartParking.OperationService.Domain.Reservations.CapacityDemand]]::new()
        $visits = [Collections.Generic.List[SmartParking.OperationService.Domain.Reservations.CapacityVisit]]::new()
        foreach ($row in $Ledger.reservations.Where({$_.site_id -eq $site.id -and
            ($_.status -in @('CONFIRMED','PARKING') -or ($_.status -eq 'PENDING_PAYMENT' -and $_.hold_expires_at -gt $at))})) {
            $allocations = [SmartParking.OperationService.Domain.Reservations.CapacityAllocation[]]@(
                foreach ($allocation in $Ledger.slot_allocations.Where({$_.reservation_id -eq $row.id -and $_.allocation_status -ne 'RELEASED'})) {
                    $match = $open.Where({$_.id -eq $allocation.parking_session_id}) | Select-Object -First 1
                    $scanAt = if ($match) {$match.location_scanned_at}
                    $allocationType::new($allocation.slot_id,$allocation.allocated_start_time,$allocation.allocated_end_time,
                        $allocation.allocation_status,$row.id,$allocation.parking_session_id,$scanAt)
                }
            )
            $claims.Add([SmartParking.OperationService.Domain.Reservations.CapacityDemand]::new(
                $row.id,$row.active_claim_slot_id,$row.expected_start_time,$row.claim_end_time,$true,'CAR',$allocations,$row.target_slot_id))
        }
        foreach ($row in $open.Where({$_.site_id -eq $site.id})) {
            $visits.Add([SmartParking.OperationService.Domain.Reservations.CapacityVisit]::new(
                $row.id,$row.reservation_id,$row.current_slot_id,$row.entry_time,'CAR',$null))
        }
        $candidates = [guid[]]@($site.floors.slots.Where({!$_.backup}).id)
        $interval = [SmartParking.OperationService.Domain.Reservations.ReservationInterval]::new($at.AddHours(5),$at.AddHours(6))
        $evaluator = [SmartParking.OperationService.Domain.Reservations.ReservationAvailability]
        $ct = [Threading.CancellationToken]::None
        $nowCount = $evaluator::CountAvailable($candidates,$null,$at,[TimeSpan]::Zero,$claims,$visits,$null,$null,$ct)
        $laterCount = $evaluator::CountAvailable($candidates,$interval,$at,$projection,$claims,$visits,$null,$commercial.quota,$ct)
        $protection = $Ledger.scenarios.Where({$_.site -eq $site.code -and $_.label -eq 'protection-window'})
        $gap = ''
        if ($protection.Count) {
            $p = $protection[0]
            $decision = $evaluator::EvaluatePhysicalOccupancy($p.slot,$at,$claims,$visits,$null)
            if ($decision.Allowed) { $gap = " | protection bay allowed by current evaluator; policy count=$($nowCount-1)" }
            $both = [SmartParking.OperationService.Domain.Reservations.ReservationInterval]::new($p.start.AddMinutes(-15),$p.start.AddMinutes(15))
            $clear = [SmartParking.OperationService.Domain.Reservations.ReservationInterval]::new($p.start.AddMinutes(-30),$p.start)
            if ($evaluator::CountAvailable([guid[]]@($p.slot),$both,$at,$projection,$claims,$visits,$null,$null,$ct) -ne 0 -or
                $evaluator::CountAvailable([guid[]]@($p.slot),$clear,$at,$projection,$claims,$visits,$null,$null,$ct) -ne 1) {
                throw 'Expected the protection fixture bay to reject a stay that crosses into its booking.'
            }
        }
        if ($site.n -eq '05' -and ($null -ne $nowCount -or $null -eq $laterCount)) {
            throw 'Incorrect-slot evidence should make current availability unknown while the mismatch is relevant.'
        }
        if ($site.n -eq '04' -and $laterCount -ne 0) { throw 'Expected a quota-limited first round at MVP-04.' }
        $Ledger.availability[$site.id] = "current=$($nowCount ?? 'unknown') T+5h=$($laterCount ?? 'unknown')$gap"
        foreach ($row in $claims | Where-Object {$_.Start -gt $at}) {
            $decision = $evaluator::Evaluate($row,$claims,$visits,$commercial.quota,$at,$projection,$null,$ct)
            if (!$decision.Allowed) { throw "Offline availability rejected booking $($row.Id): $($decision.Rejection)" }
        }
        if ($site.n -eq '01') {
            $hold = $Ledger.reservations.Where({$_.status -eq 'PENDING_PAYMENT'})[0]
            $stay = [SmartParking.OperationService.Domain.Reservations.ReservationInterval]::new($hold.expected_start_time,$hold.claim_end_time)
            $onlyBay = [guid[]]@($hold.target_slot_id)
            $after = [SmartParking.OperationService.Domain.Reservations.CapacityDemand[]]@($claims | Where-Object {$_.Id -ne [guid]$hold.id})
            $beforeCount = $evaluator::CountAvailable($onlyBay,$stay,$hold.hold_expires_at.AddSeconds(-1),$projection,$claims,$visits,$null,$null,$ct)
            $afterCount = $evaluator::CountAvailable($onlyBay,$stay,$hold.hold_expires_at,$projection,$after,$visits,$null,$null,$ct)
            if ($beforeCount -ne 0 -or $afterCount -ne 1) { throw 'Pending hold must release its availability claim at expiry.' }
        }
    }
}

function New-PreviewPolicy([DateTimeOffset]$T) {
    @{id='27300000-0000-4000-8000-000000000001';version=1;begins=$T.AddDays(-90);ends=$null
        values=@{RESERVATION_MIN_DURATION=15;RESERVATION_MAX_DURATION=1440;MAX_ADVANCE_BOOKING=10080;INTER_BOOKING_BUFFER=0
            PAYMENT_HOLD_DURATION=5;RESERVATION_PROTECTION_WINDOW=240;ALLOCATION_LEAD_TIME=60;FACILITY_FAILURE_REFUND_LEAD_TIME=15
            OCCUPANCY_VALIDITY_TIMESPAN=240;ARRIVAL_LATE_TOLERANCE=15;RAPID_CANCELLATION_COUNT=2
            RAPID_CANCELLATION_WINDOW=15;RAPID_CANCELLATION_RESTRICTION_DURATION=180;DAILY_CANCELLATION_LIMIT=5}}
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
    $commercial = foreach ($site in $sites) {
        $index = [int]$site.n - 1
        $late = @(15,$null,20,30,$null)[$index]
        $grace = @(15,0,0,0,0)[$index]
        $row = @{
            site=$site.id; monthly=@(600000,750000,900000,750000,1000000)[$index]
            quota=@($null,4,3,1,5)[$index]; late=$late; grace=$grace
            rules=@{timeZoneId='Asia/Ho_Chi_Minh'; roundingMode='HALF_UP_THOUSAND'
                timeBlocks=@(@{blockCode='ALL_DAY'; startTime='00:00'; endTime='00:00'; ratePerHourVnd=$site.rate})
                discoveryEstimate=@{graceMinutes=$grace; minimumVnd=0}}
        }
        foreach ($key in 'tariff','tariffrev','sitepolicy','plan','planrev','term','termrev','auditrev') {
            $row[$key] = Get-FixtureId "commercial/$($site.n)/$key/1"
        }
        if ($null -ne $row.quota) { $row.quotaid = Get-FixtureId "commercial/$($site.n)/quota/CAR" }
        $row
    }
    $purchases = foreach ($n in '01','03','04') {
        $row = @{site=Get-FixtureId "site/$n"; vehicle=Get-FixtureId "vehicle/$n/1"; account=Get-FixtureId "account/driver/$n"}
        foreach ($key in 'subscription','purchase','cash','invoice') {
            $row[$key] = Get-FixtureId "monthly/$n/vehicle/1/site/$n/$key"
        }
        $row
    }
    return @{ sites=@($sites); accounts=@($accounts); vehicles=@($vehicles); commercial=@($commercial); purchases=@($purchases) }

}

function Show-Inventory($Data) {
    Write-Host "MVP-SEED-V1 / $seedNamespace | Checkpoints A+B | origin 10.775, 106.700 | keyword MVPSEED"
    foreach ($s in $Data.sites) {
        $source = if ($s.initial) { 'initial application evidence' } else { 'synthetic additional-site grant' }
        Write-Host "$($s.code) $($s.name) [$($s.id)] tenant $($s.tenant), owner$($s.owner), $($s.lat),$($s.lon) ($($s.metres)m)"
        Write-Host "  $($s.model), $($s.floors.Count) floors / zones, $($s.capacity) CAR bays, $source"
        $c = $Data.commercial.Where({ $_.site -eq $s.id })[0]
        Write-Host "  CAR hourly=$($s.rate) grace=$($c.grace) monthly=$($c.monthly) VND quota=$($c.quota ?? 'absent') late=$($c.late ?? 'disabled')"
        Write-Host "  DiscoveryPricing:SiteTenants:$($s.id)=$($s.tenant) | DiscoveryPricing__SiteTenants__$($s.id)=$($s.tenant)"
        foreach ($floor in $s.floors) {
            foreach ($slot in $floor.slots.Where({ $_.backup })) { Write-Host "  BACKUP $($slot.code) $($slot.id)" }
        }
    }
    foreach ($a in $Data.accounts) { Write-Host "$($a.email) user=$($a.uid) account=$($a.id) tenant=$($a.tenant) site=$($a.site)" }
    foreach ($v in $Data.vehicles) { Write-Host "$($v.plate) $($v.id) account=$($v.account)" }
    foreach ($p in $Data.purchases) {
        Write-Host "Monthly subscription=$($p.subscription) vehicle=$($p.vehicle) site=$($p.site) order=$($p.purchase) transaction=$($p.cash) invoice=$($p.invoice)"
    }
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
    # No application can observe the gap between C's Operations and Facility commits.
    if ($containers.Count) {
        $networks = @{}
        foreach ($id in $containers.Values) {
            $attached = (Invoke-Docker @('inspect','--format','{{json .NetworkSettings.Networks}}',$id)) | ConvertFrom-Json -AsHashtable
            foreach ($net in $attached.Keys) { $networks[$net] = $true }
        }
        foreach ($net in $networks.Keys) {
            foreach ($id in @(Invoke-Docker @('ps','-q','--no-trunc','--filter',"network=$net"))) {
                if ($id -in $containers.Values) { continue }
                $tag = Invoke-Docker @('inspect','--format','{{index .Config.Labels "com.docker.compose.project"}}/{{index .Config.Labels "com.docker.compose.service"}}',$id)
                if ($tag -notin "$IsolatedProject/redis","$IsolatedProject/rabbitmq","$IsolatedProject/notification-db","$IsolatedProject/iot-db") {
                    throw "C requires a quiescent target: an application or unknown service is attached to $net."
                }
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

function Get-DataSql([string]$Service, [string]$Mode, [string]$Timeline=$appliedTime,
    [string]$Stage='foundation', $CommercialLedger=$ledger, $PriorOccupancy=$null, [bool]$OperationsEstablished=$false,
    [bool]$PriorTimeline=$false) {
    $scope = @{sites=$data.sites; vehicles=$data.vehicles; commercial=$data.commercial; purchases=$data.purchases; accounts=@($data.accounts | ForEach-Object {
        $item = $_.Clone()
        if ($Service -ne 'user-db') { $item.Remove('hash') }
        $item
    })}
    $json = ConvertTo-Json -InputObject $scope -Depth 12 -Compress
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($json))
    $columnSets = @{
        'user-db'=@('account_access_decisions')
        'payment-db'=@('reservation_quotes','payment_orders','payment_transactions','invoices')
        'operations-db'=@('reservation_requests','reservations','reservation_cancellation_evidence','parking_sessions','slot_allocations')
    }
    $localLedger = @{occupied=$CommercialLedger.occupied; prior_occupancy=$PriorOccupancy; begins=$CommercialLedger.begins
        user_anchor=(Get-FixtureId 'c/04/pending/decision'); workflow_anchor=(Get-FixtureId 'c/04/pending/request')
        quote_anchor=(Get-FixtureId 'c/04/pending/quote'); owned_ids=$CommercialLedger.ownedIds}
    foreach ($table in $columnSets[$Service]) { $localLedger[$table]=$CommercialLedger[$table] }
    $ledgerJson = ConvertTo-Json -InputObject $localLedger -Depth 8 -Compress
    $ledgerEncoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($ledgerJson))
    $keys = ConvertTo-SeedJson $columnSets[$Service]
    $parts = if ($files.ContainsKey($Service)) {
        (Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path "$PSScriptRoot/database/seeds/mvp" $files[$Service])) -split '(?m)^-- CHECKPOINT C\r?\n',2
    }
    $body = if (!$parts) {''} elseif ($Stage -eq 'lifecycle') {$parts[1]} else {$parts[0]}
    $guard = if ($Reset -and $Stage -eq 'foundation') { $dependencySql } else { '' }
    $resetValue = ([bool]$Reset).ToString().ToLowerInvariant()
    $opsValue = $OperationsEstablished.ToString().ToLowerInvariant()
    $priorValue = $PriorTimeline.ToString().ToLowerInvariant()
    # JSON is sent over stdin, never through process arguments or the environment.
    return @"
BEGIN;
SET LOCAL search_path TO pg_catalog, public, pg_temp;
SET LOCAL timezone TO 'UTC';
SET LOCAL lock_timeout TO '5s';
SET LOCAL mvp.mode TO '$Mode';
SET LOCAL mvp.reset TO '$resetValue';
SET LOCAL mvp.reference TO '$Timeline';
SET LOCAL mvp.operations_established TO '$opsValue';
SET LOCAL mvp.prior_timeline TO '$priorValue';
SELECT pg_advisory_xact_lock(hashtextextended('MVP-SEED-V1',0));
$commonSql
SELECT pg_temp.require(EXISTS(SELECT FROM pg_extension WHERE extname='uuid-ossp'), 'Missing uuid-ossp base schema extension')
    WHERE current_database() IN ('smartpark_user','smartpark_facility','smartpark_operations');
CREATE TEMP TABLE manifest AS SELECT convert_from(decode('$encoded','base64'),'UTF8')::jsonb AS j;
CREATE TEMP TABLE lifecycle_manifest AS SELECT convert_from(decode('$ledgerEncoded','base64'),'UTF8')::jsonb AS j;
CREATE TEMP VIEW site_input AS SELECT s.* FROM manifest, jsonb_to_recordset(j->'sites') s(
    n text,id uuid, owner text,tenant uuid,code text,name text,address text,lat numeric,lon numeric, type text,model text,rate integer,
    capacity integer,backup integer,policy uuid,publication uuid,initial boolean,floors jsonb);
CREATE TEMP VIEW person_input AS SELECT a.* FROM manifest,jsonb_to_recordset(j->'accounts') a(
    n text,id uuid,uid uuid,role text,email text,phone text,tenant uuid,site uuid,hash text);
CREATE TEMP VIEW vehicle_input AS SELECT v.* FROM manifest,jsonb_to_recordset(j->'vehicles') v(id uuid,account uuid,plate text,canonical text);
CREATE TEMP VIEW commercial_input AS
    SELECT c.*,s.n,s.tenant,s.code,s.name,s.rate,a.uid AS actor,t-interval '90 days' AS inception
    FROM manifest CROSS JOIN LATERAL jsonb_to_recordset(j->'commercial') c(site uuid,monthly integer,quota integer,
        quotaid uuid,late integer,grace integer,rules jsonb,tariff uuid,tariffrev uuid,sitepolicy uuid,plan uuid,
        planrev uuid,term uuid,termrev uuid,auditrev uuid)
    JOIN site_input s ON s.id=c.site JOIN person_input a ON a.role='owner' AND a.n=s.owner CROSS JOIN run_context;
CREATE TEMP VIEW purchase_input AS
    SELECT c.*,p.account,p.vehicle,p.subscription,p.purchase,p.cash,p.invoice,v.plate,v.canonical,
        t-interval '1 day' AS starts,
        (((t-interval '1 day') AT TIME ZONE 'Asia/Ho_Chi_Minh') + interval '1 month') AT TIME ZONE 'Asia/Ho_Chi_Minh' AS ends
    FROM manifest CROSS JOIN LATERAL jsonb_to_recordset(j->'purchases') p(site uuid,vehicle uuid,account uuid,
        subscription uuid,purchase uuid,cash uuid,invoice uuid)
    JOIN commercial_input c USING(site) JOIN vehicle_input v ON v.id=p.vehicle AND v.account=p.account CROSS JOIN run_context;
SELECT pg_temp.require((SELECT count(*)=5 FROM commercial_input) AND (SELECT count(*)=3 AND sum(monthly)=2250000
    AND bool_and(starts<=t AND t<ends) FROM purchase_input CROSS JOIN run_context),'Incomplete commercial manifest');
CREATE TEMP VIEW owner_input AS SELECT s.*, a.uid,a.email,a.phone,'MVPSEED Owner '||a.n AS business,
    'T-'||replace(s.id::text,'-','') AS tenant_code,
    jsonb_build_object('name',s.name,'address',s.address,'lotType',s.type,'totalSlots',s.capacity,
      'floors',jsonb_array_length(s.floors),'devices','[]'::jsonb,'modelFileName',s.model,'hourlyRate',s.rate,
      'dailyRate',0,'nightRate',s.rate,'gracePeriodMinutes',0,'subscriptionMonthly',0,'subscriptionYearly',0,'policyAccepted',true) AS lot
    FROM site_input s JOIN person_input a ON a.role='owner' AND a.tenant=s.id WHERE s.initial;
CREATE TEMP TABLE seed_identifiers(value text PRIMARY KEY);
INSERT INTO seed_identifiers SELECT DISTINCT replace(m[1],'-','') FROM manifest,
    regexp_matches(lower(j::text),'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}|51m91[0-9]{3}','g') m;
INSERT INTO seed_identifiers SELECT DISTINCT replace(id,'-','') FROM lifecycle_manifest,
    jsonb_array_elements_text(j->'owned_ids') id
    ON CONFLICT DO NOTHING;
CREATE TEMP VIEW lifecycle_occupancy AS
    SELECT o.* FROM lifecycle_manifest,jsonb_to_recordset(j->'occupied')
        o(id uuid,site_id uuid,tenant_id uuid,updated_at timestamptz);
CREATE TEMP VIEW prior_occupancy AS
    SELECT o.* FROM lifecycle_manifest,jsonb_to_recordset(nullif(j->'prior_occupancy','null'::jsonb))
        o(id uuid,site_id uuid,tenant_id uuid,updated_at timestamptz);
$lifecycleSql
$body
INSERT INTO fixture_keys
    SELECT tables.name,'id',records.row->>'id' FROM lifecycle_manifest,
        jsonb_array_elements_text(coalesce(nullif('$keys'::jsonb,'null'::jsonb),'[]'::jsonb)) tables(name),
        jsonb_array_elements(j->tables.name) records(row) ON CONFLICT DO NOTHING;
$guard
SELECT 'validated $Service ($Mode), expected owned rows='||count(*) FROM fixture_keys;
COMMIT;
"@
}

$lifecycleSql = @'
CREATE FUNCTION pg_temp.lifecycle_rows(rel text) RETURNS jsonb LANGUAGE sql AS $$
    SELECT jsonb_agg(row || coalesce((
        SELECT jsonb_object_agg(field.key,(field.value #>> '{}')::jsonb)
        FROM jsonb_each(row) field WHERE field.key IN ('intent_json','reviewed_evidence',
            'accepted_operational_policy_snapshot','policy_snapshot','pricing_snapshot')
    ),'{}'::jsonb))
    FROM lifecycle_manifest,jsonb_array_elements(j->rel) row;
$$;
CREATE FUNCTION pg_temp.sync_lifecycle(rel text) RETURNS void LANGUAGE plpgsql AS $$
DECLARE cols text; restore text[]; body jsonb;
BEGIN
    body := pg_temp.lifecycle_rows(rel);
    PERFORM pg_temp.require(jsonb_typeof(body)='array' AND jsonb_array_length(body)>0,rel||': missing C fixture input');
    SELECT string_agg(format('%I',key),', ' ORDER BY key),array_agg(key) FILTER (WHERE key<>'id')
    INTO cols,restore FROM (SELECT DISTINCT jsonb_object_keys(row) AS key FROM jsonb_array_elements(body) row) keys;
    EXECUTE format('CREATE TEMP TABLE %I AS SELECT %s FROM jsonb_populate_recordset(NULL::public.%I,$1)',
        'e_'||rel,cols,rel) USING body;
    PERFORM pg_temp.sync(rel,'id',restore);
END $$;
'@

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

function Read-LifecycleTime($Service,$Table,$Column,$Key,[int]$Seconds,[string]$Condition='TRUE') {
    Invoke-Database $Service "SELECT to_char(($Column AT TIME ZONE 'UTC') + make_interval(secs=>$Seconds),'YYYY-MM-DD`"T`"HH24:MI:SS.US`"Z`"') FROM public.$Table WHERE id='$Key' AND ($Condition);"
}

function Read-LedgerPolicyHistory {
    $json = Invoke-Database 'operations-db' @'
SELECT coalesce(jsonb_agg(to_jsonb(p) ORDER BY effective_from),'[]'::jsonb)
FROM operational_policies p WHERE tenant_id IS NULL AND site_id IS NULL AND is_active
AND cancellation_timezone='Asia/Ho_Chi_Minh'
AND override_bounds->'ARRIVAL_LATE_TOLERANCE'='{"minimum":15,"maximum":30}'::jsonb
AND (payment_hold_duration_minutes,min_reservation_duration_minutes,max_reservation_duration_hours,
    max_advance_booking_days,inter_booking_buffer_minutes,reservation_protection_window_hours,allocation_lead_time_minutes,
    facility_failure_refund_lead_minutes,occupancy_validity_timespan_hours,rapid_cancellation_count,
    rapid_cancellation_window_minutes,rapid_cancellation_restriction_minutes,daily_cancellation_limit)=(5,15,24,7,0,4,60,15,4,2,15,180,5)
AND arrival_late_tolerance_minutes BETWEEN 15 AND 30;
'@
    foreach ($p in (ConvertFrom-Json -AsHashtable -InputObject $json)) {
        @{id=$p.id; version=$p.version; begins=([DateTimeOffset]$p.effective_from).ToUniversalTime()
            ends=if ($p.effective_until) {([DateTimeOffset]$p.effective_until).ToUniversalTime()}
            values=@{RESERVATION_MIN_DURATION=$p.min_reservation_duration_minutes;RESERVATION_MAX_DURATION=(60*$p.max_reservation_duration_hours)
                MAX_ADVANCE_BOOKING=(1440*$p.max_advance_booking_days);INTER_BOOKING_BUFFER=$p.inter_booking_buffer_minutes
                PAYMENT_HOLD_DURATION=$p.payment_hold_duration_minutes;RESERVATION_PROTECTION_WINDOW=(60*$p.reservation_protection_window_hours)
                ALLOCATION_LEAD_TIME=$p.allocation_lead_time_minutes;FACILITY_FAILURE_REFUND_LEAD_TIME=$p.facility_failure_refund_lead_minutes
                OCCUPANCY_VALIDITY_TIMESPAN=(60*$p.occupancy_validity_timespan_hours)
                ARRIVAL_LATE_TOLERANCE=$p.arrival_late_tolerance_minutes;RAPID_CANCELLATION_COUNT=$p.rapid_cancellation_count
                RAPID_CANCELLATION_WINDOW=$p.rapid_cancellation_window_minutes
                RAPID_CANCELLATION_RESTRICTION_DURATION=$p.rapid_cancellation_restriction_minutes;DAILY_CANCELLATION_LIMIT=$p.daily_cancellation_limit}}
    }
}

function New-InstalledLedger($Data,[string]$Timeline,$History) {
    $t = ([DateTimeOffset]$Timeline).ToUniversalTime()
    $active = @($History | Where-Object {$_.begins -le $started -and (!$_.ends -or $_.ends -gt $started)})
    if ($active.Count -ne 1) { throw 'Strategy A requires an installed compatible platform revision at execution time.' }
    $begin = $active[0].begins
    # B permits adjacent compatible platform revisions. Keep the actual revision
    # at each lifecycle boundary when projecting the fixture-owned site policies.
    foreach ($prior in $History | Sort-Object {$_.begins} -Descending) {
        if ($prior.ends -and $prior.ends -eq $begin) { $begin = $prior.begins }
    }
    if ($begin -lt $t.AddDays(-90)) { $begin = $t.AddDays(-90) }
    foreach ($revision in $History) {
        Write-Host "Compatible platform revision $($revision.id) v$($revision.version): $($revision.begins) through $($revision.ends ?? 'open')"
    }
    Write-Host "Installed compatible platform history starts at $(Format-Instant $begin), T=$(Format-Instant $t), duration=$($t-$begin)"
    New-Ledger $Data $t $begin $History
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
    Import-LedgerAssemblies
    $previewTime = [DateTimeOffset]$(if ($requestedTime) {$requestedTime} else {Format-Instant $started})
    $previewPolicy = New-PreviewPolicy $previewTime
    $ledger = New-Ledger $data $previewTime $previewPolicy.begins @($previewPolicy)
    Show-Inventory $data
    Show-Ledger $ledger 'assumed compatible policy history, no database inspected'
    Write-Host 'Offline inventory only. No databases inspected or changed.'
    return
}
if (!$IsolatedProject) {
    $IsolatedProject = Read-Host 'Name of the explicitly authorized isolated local Compose project (empty cancels)'
    if ($IsolatedProject -cnotmatch '^[a-z0-9][a-z0-9_-]*$') { throw 'An authorized isolated project is required.' }
}
$files = @{'user-db'='01-user.sql'; 'facility-db'='02-facility.sql'; 'operations-db'='03-operations.sql'; 'payment-db'='04-payment.sql'}
$applyOrder = @('user-db','facility-db','payment-db','operations-db')
$services = $applyOrder
if ($Reset) { $services += @('notification-db','iot-db') }
$containers = @{}
$compose = @('compose','--project-name',$IsolatedProject,'-f',(Join-Path $root 'compose.schema-integration.yml'))
$mutex = [Threading.Mutex]::new($false,"SmartPark-MVP-SEED-V1-$IsolatedProject")
$locked = $false
$committed = @()
$phase = 'local target inspection'
try {
    try { $locked = $mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $locked = $true }
    if (!$locked) { throw 'Another seed invocation is active for this project.' }
    Write-Host 'Keep host applications and workers disconnected from the isolated target through final verification.'
    Resolve-Target
    foreach ($s in $services) {
        $db = 'smartpark_' + $s.Replace('-db','')
        $ok = Invoke-Database $s "SELECT current_database()='$db' AND current_user='$db' AND current_setting('server_version_num')::int/10000=16 AND NOT EXISTS(SELECT FROM pg_stat_activity WHERE datname=current_database() AND pid<>pg_backend_pid() AND backend_type='client backend');"
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
    Write-Host "Checkpoint B | project=$IsolatedProject | mode=$modeName | reference=$appliedTime | execution=$(Format-Instant $started)"
    # The immutable revision date identifies a local B commit, independently of A.
    $firstCommercial = $data.commercial[0]
    $revisionSources = [ordered]@{
        'payment-db'=@('policy_revisions',$firstCommercial.tariffrev)
        'operations-db'=@('parking_pass_plan_revisions',$firstCommercial.planrev)
    }
    $commercialTimes = @{}
    foreach ($s in $revisionSources.Keys) {
        $table,$key = $revisionSources[$s]
        $commercialTimes[$s] = Invoke-Database $s "SELECT to_char((effective_from AT TIME ZONE 'UTC')+interval '90 days','YYYY-MM-DD`"T`"HH24:MI:SS.US`"Z`"') FROM public.$table WHERE id='$key';"
    }
    $payTime = $commercialTimes['payment-db']
    $opsTime = $commercialTimes['operations-db']
    if (($payTime -and !$facilityAnchor) -or ($opsTime -and !$payTime)) { throw 'Commercial phases exist out of order. Inspect before recovery.' }
    $oldTimes = @(@($payTime,$opsTime,$facilityAnchor) | Where-Object { $_ -and $_ -ne $userAnchor } | Select-Object -Unique)
    $outOfOrder = ($payTime -eq $userAnchor -and $payTime -and $facilityAnchor -ne $userAnchor) -or
        ($opsTime -eq $userAnchor -and $opsTime -and $payTime -ne $userAnchor)
    if ($outOfOrder -or $oldTimes.Count -gt 1 -or ($oldTimes.Count -and !($Reset -and $requestedTime -eq $userAnchor))) {
        throw "Mixed commercial timelines: User=$userAnchor Facility=$facilityAnchor Payment=$payTime Operations=$opsTime. Inspect and explicitly resume at User T."
    }
    $phase = 'C installed-history and timeline preflight'
    Import-LedgerAssemblies
    $history = @(Read-LedgerPolicyHistory)
    $cSources = [ordered]@{
        'user-db'=@('account_access_decisions','decided_at',(Get-FixtureId 'c/04/pending/decision'),62)
        'payment-db'=@('reservation_quotes','created_at',(Get-FixtureId 'c/04/pending/quote'),90)
        'operations-db'=@('reservation_requests','admitted_at',(Get-FixtureId 'c/04/pending/request'),65)
    }
    $cTimes = @{}
    foreach ($s in $cSources.Keys) {
        $table,$column,$key,$seconds = $cSources[$s]
        $cTimes[$s] = Read-LifecycleTime $s $table $column $key $seconds
    }
    $cUser,$cPay,$cOps = $cTimes['user-db'],$cTimes['payment-db'],$cTimes['operations-db']
    $cStale = @(@($cUser,$cPay,$cOps) | Where-Object {$_ -and $_ -ne $userAnchor} | Select-Object -Unique)
    if (($cUser -and !$opsTime) -or ($cPay -and !$cUser) -or ($cOps -and !$cPay) -or
        ($cPay -eq $userAnchor -and $cPay -and $cUser -ne $userAnchor) -or
        ($cOps -eq $userAnchor -and $cOps -and $cPay -ne $userAnchor) -or
        ($cStale.Count -gt 1) -or ($cStale.Count -and !($Reset -and $requestedTime -eq $userAnchor))) {
        throw "Unsafe C phase prefix or stale timeline: User=$cUser Payment=$cPay Operations=$cOps; A/B reference=$userAnchor."
    }
    $ledger = New-InstalledLedger $data $appliedTime $history
    $priorLedgers = @{$appliedTime=$ledger}
    foreach ($time in @($cUser,$cPay,$cOps) | Where-Object {$_} | Select-Object -Unique) {
        if (!$priorLedgers.ContainsKey($time)) { $priorLedgers[$time] = New-InstalledLedger $data $time $history }
    }
    $firstOccupied = $ledger.occupied[0]
    $scanOffset = [int]($ledger.t-$firstOccupied.updated_at).TotalSeconds
    $cFacility = Read-LifecycleTime 'facility-db' 'parking_slots' 'updated_at' $firstOccupied.id $scanOffset "physical_state='OCCUPIED'"
    $staleTimeline = @(@($facilityAnchor,$payTime,$opsTime,$cUser,$cPay,$cOps,$cFacility) |
        Where-Object {$_ -and $_ -ne $userAnchor} | Select-Object -Unique)
    if (($cFacility -and !$cOps) -or ($cFacility -and $cFacility -ne $cOps -and
        !($Reset -and $requestedTime -eq $userAnchor -and $cOps -eq $userAnchor)) -or $staleTimeline.Count -gt 1 -or
        ($cUser -eq $userAnchor -and $cUser -and $opsTime -ne $userAnchor)) {
        throw "C Facility scan time does not match a recoverable phase prefix: Facility=$cFacility Operations=$cOps."
    }
    $priorOccupancy = if ($cFacility) {
        if (!$priorLedgers.ContainsKey($cFacility)) { $priorLedgers[$cFacility] = New-InstalledLedger $data $cFacility $history }
        $priorLedgers[$cFacility].occupied
    }
    $cOrder = @('user-db','payment-db','operations-db','facility-db')
    $phase = 'credential preparation'
    New-PasswordHashes
    if ($Reset) {
        foreach ($s in $revisionSources.Keys) {
            if ($commercialTimes[$s]) {
                $phase = "$s original commercial timeline verification"
                Invoke-Database $s (Get-DataSql $s 'verify' $commercialTimes[$s]) | Where-Object { $_ }
            }
        }
    }
    foreach ($s in $services) {
        $phase = "$s preflight"
        Write-Host $phase
        Invoke-Database $s (Get-DataSql $s 'check') | Where-Object { $_ }
    }
    foreach ($s in $cOrder) {
        $phase = "$s lifecycle preflight"
        if ($Reset -and $cTimes[$s]) {
            Invoke-Database $s (Get-DataSql $s 'verify' $cTimes[$s] 'lifecycle' $priorLedgers[$cTimes[$s]] $null $false $true) | Where-Object { $_ }
        }
        Invoke-Database $s (Get-DataSql $s 'check' $appliedTime 'lifecycle' $ledger $priorOccupancy ([bool]$cOps)) | Where-Object { $_ }
    }
    foreach ($s in $applyOrder) {
        if ($s -eq 'operations-db') {
            $phase = 'Payment verification before exposing Operations coverage'
            Invoke-Database 'payment-db' (Get-DataSql 'payment-db' 'verify') | Where-Object { $_ }
        }
        $phase = "$s apply"
        Write-Host $phase
        Invoke-Database $s (Get-DataSql $s 'apply') | Where-Object { $_ }
        $committed += $s
    }
    foreach ($s in $applyOrder) {
        $phase = "$s persisted-state verification"
        Invoke-Database $s (Get-DataSql $s 'verify') | Where-Object { $_ }
    }
    foreach ($s in $cOrder) {
        $phase = "$s lifecycle apply"
        Invoke-Database $s (Get-DataSql $s 'apply' $appliedTime 'lifecycle' $ledger $priorOccupancy ([bool]$cOps)) | Where-Object { $_ }
        $committed += "$s/C"
        $phase = "$s lifecycle persisted verification"
        Invoke-Database $s (Get-DataSql $s 'verify' $appliedTime 'lifecycle') | Where-Object { $_ }
    }
    if ((Read-Anchor 'user-db') -ne $appliedTime -or (Read-Anchor 'facility-db') -ne $appliedTime) { throw 'Reference-time anchors disagree.' }
    Show-Inventory $data
    Show-Ledger $ledger 'installed policy history'
    Write-Host "Verified deterministic seed rows in $($committed -join ', '). Application acceptance requires configured services."
} catch {
    Write-Warning "Stopped at $phase. Acknowledged commits: [$($committed -join ', ')]. Inspect before retrying an ambiguous commit."
    throw
} finally {
    if ($locked) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
