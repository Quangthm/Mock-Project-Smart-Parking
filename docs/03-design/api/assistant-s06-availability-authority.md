# S06 — Operations availability authority decision packet

| Metadata | Value |
| --- | --- |
| Status | Evaluator source/offline implemented after user continuation; authoritative count producer absent; S06 PARTIAL |
| Contract version / date | 0.1 / 2026-11-06 |
| Owning service / author | Operations / Codex |
| Jira | OPEN |
| Requirements baseline | SRS v1.1 discovery and reservations/allocation |
| Implementation | Domain fixed-resource evaluator and Application source port wired into existing search; production source explicitly unsupported; no new route, producer, SQL or enablement |

## Overview

Implementation continuation: user requested proceeding with the evaluator direction. `Domain/DiscoveryCapacity.cs` evaluates fully resolved fixed unit resources and blocked half-open intervals, unions overlapping blocks, requires coherent complete configuration/policy/coverage and retains original freshness timestamps. `Application/Discovery/CapacitySource.cs` supplies the inward port; the only production registration is `UnsupportedDiscoveryCapacity`, which returns no evidence. Search invokes it under the existing deadline/cancellation, maps contradictory evidence to503, preserves Facility closure/stale restrictions, and filters/ranks the resulting count before paging. Positive evidence is exercised only by synthetic test DI. No physical producer, tenant mapping, trust path or SQL source has been invented. A01–A06 actual source/policy evidence remains unresolved. Future source adapters must validate trusted site–tenant attribution before constructing evidence; Domain validates site/category and is not tenant authorization.

Supported count model is exact fixed resources (one compatible vehicle each), bounded to10000 resources/blocks, with each accepted block effective for its stated interval. Count is resources free for the entire query window, not minimum instantaneous aggregate vacancy. Capacity-only/shared pools, unlocated occupancy, effective buffer/protection calculations and physical evidence ordering/provenance require a real owner resolver before `Complete` may be asserted. Domain does not infer release, departure, buffer defaults or coverage from time. The completeness flag is an internal adapter precondition, never a client field or proof supplied by a model.

Close the remaining G01 evidence gap without treating counters, checkout or an empty claims table as proof of free capacity. This packet specifies a bounded extension of the existing Operations search, not a new Search service. Implementation authorization for P01–P04 does not supply the missing physical evidence or approve a new producer trust contract.

| Traceability | Reference |
| --- | --- |
| FR-CAP-01 / UC-SEARCH-01 | [Discovery](../../01-requirements/srs/modules/03-discovery-visualization.md) |
| Claim/occupancy policy | [Reservations/allocation](../../01-requirements/srs/modules/04-reservations-allocation.md), including effective protection and occupancy validity settings |
| Service ownership | [Ownership and events](../../02-architecture/service-boundaries/ownership-and-events.md) |
| Approved source direction | [P01–P04 packet](assistant-s06-search-owner-proposal.md), [current handoff](../ai/business-data-integration.md) |
| Auth classification | [Operation matrix](../../01-requirements/srs/authorization-operation-classification.md) |

## Source evidence before evaluator integration

All paths in this table are relative to `src/Services/OperationService/`. Audit is of checkout HEAD `3d3def763870f66f8da71386357a66f776fa61e5` plus preserved S01–S06 working changes. Source is not runtime verification.

| Path / actual writer or reader | Established fact | Missing authority |
| --- | --- | --- |
| `...Application/Discovery/ParkingSearch.cs`, `ParkingSearchHandler.Project` | Public search evaluates fresh Facility closure and otherwise emits null count. No Operations capacity store is injected. | Positive current/interval producer is absent from this call chain. |
| `...Persistence/ReservationGateStore.cs`, `CreateWalkInAsync` | Writes an active category-specific session without assigning a slot. | Slot-only subtraction misses legitimate occupants; category totals cannot establish which physical resource is occupied. |
| Same file, `TransitionAsync` check-in | Links reservation, allocation and session; allocation becomes OCCUPIED. | Summing all three double-counts one claim. Reservations without allocation and sessions without slot must remain accounted for. |
| Same file, checkout methods | Writes `ExitTime`, COMPLETED and RELEASED from the checkout path. | No independently established physical departure provenance is present in these rows; do not interpret an exit image URL or elapsed time as verified passage. |
| `...Domain/Entities/ReservationEntities.cs`, `SiteCapacityPoolRow` | Holds category, optional unit, totals and counters; `UpdatedAt` exists. | No interval coverage, watermark across every writer, unique correlation to allocations, effective policy revision or physical-observation provenance. |
| `...Persistence/StructureSafetyStore.cs`, `BuildSnapshotAsync` | Unions known allocation/session slot IDs and reads pool claims for structure-edit safety. | Safety snapshot is not a public availability evaluator; site/unit pools can overlap; held claims/occupants can be unlocated. |
| `...Persistence/Repositories/ParkingSessionRepository.cs`, `SlotAllocationRepository.cs` | Tenant/site-scoped row lookup. | Repository presence is not an observation producer or complete resource universe. |

Searching current Operations/IoT C# source found the counter fields and their structure-safety consumers, but no producer with a complete observation/claim watermark and verified-departure contract. Historical SQL/projection names and seeds do not establish an implemented authoritative read path. Do not read Facility tables from Operations to fill these gaps.

## Direction requiring approval

| Choice | Benefit | Cost / limitation |
| --- | --- | --- |
| Keep existing unknown (current behavior) | Honest search and published closure work now; no new trust/storage contract | Available-only cannot prove positive matches; G01 remains partial |
| Recommended: Operations-owned coherent read of resource, claim and physical evidence | Reuses service ownership; current/interval share one evaluation policy; no public private records | Needs actual producers and writer coverage before known counts are enabled; cross-owner configuration contract may need extension |
| New materialized count projection | Can bound read latency once complete and maintained | Adds synchronization, replay, lag/watermark and operational obligations; not justified as an S06 shortcut |

Recommendation is not approval. Do not add a synthetic snapshot endpoint, privileged manual count writer, telemetry trust path, background worker or table merely to make tests return positive values. Retain unknown while any required source is absent. A partial resource subset must not be presented as the site's exact available count.

### Concrete approval list

| ID / owner | Required decision and evidence | Safe behavior until supplied |
| --- | --- | --- |
| A01 Operations + product | Count unit: additional category-compatible vehicles admissible now or throughout the requested interval; exact mapping for fixed slots versus capacity-only pools, quota, backup and unlocated occupants. Identify nonoverlapping resources and authoritative complete configuration version. | Unknown for uncovered category/site; never sum parent and child pools. |
| A02 Operations | Lifecycle mapping for pending/confirmed/allocated/parked/cancelled/expired claims; correlation reservation→allocation→session and independent claim identity. Specify committed release/expiry writer and protection/buffer policy revision. | No release by chat clock, reservation end or nominal hold expiry. |
| A03 Operations + physical-evidence owner | Name existing trusted departure/occupancy producer, ingress auth, provenance/revision, event ordering/deduplication, coverage and unresolved/disconnected behavior. If none exists, submit its own reviewed owner proposal; no route/auth is approved by this packet. | Checkout, payment and gate-open alone cannot increase availability. |
| A04 Facility + Operations | Complete dated opening/closure/resource restrictions and compatibility coverage; how version changes invalidate Operations evidence. Existing optional P01 serviceWindow proves only its stated interval. | Open/no-window is insufficient for a known count; fresh closure can prove zero. |
| A05 Operations + product | Effective occupancy projection horizon and protection window from policy authority; per-source maximum age/skew and equality behavior; no implicit default policy values from documentation. | Outside proven coverage or missing effective policy: unknown. |
| A06 Operations | Coherent observation under concurrent entry/departure/claim/restriction/configuration changes; each writer participates or fences the revision. Declare read consistency and live-page semantics. | A new read timestamp does not freshen old facts; known counts blocked without coverage. |

The user authorized preparation of this packet. These unresolved semantic/source decisions are OPEN. They do not reopen TOKEN/REQUEST/SERVICE, D01 or D05. Deployment credentials, SQL application, containers and release activation remain separately unrequested.

## API specification — existing route, proposed known-count behavior

| Item | Contract |
| --- | --- |
| Method / URL | `GET /api/parking-search` (existing Operations route) |
| Availability | Route implemented; positive count evaluation PROPOSED and gated by A01–A06 |
| Authentication / origin | PUBLIC; no human actor fabricated, no delegated Bearer to public owners |
| Resource scope | Only explicitly published Facility candidates; category/site binding validated by owner |
| Content type | No request body; JSON response |
| Headers | `Cache-Control: no-store` on success/errors; no private query/header logging |

Existing origin, category, radius, interval, bounds, price filters and protected live cursor contract remain unchanged. No new public count endpoint. No detail/booking/payment action is added. Proposed internal evaluation stays in Operations: Application resolves bounded local/owner facts; Domain evaluates resolved facts; Persistence establishes coherent local reads; Infrastructure implements approved owner adapters only. Port/type names and physical evidence transport await A01–A06; none is registered in production by this document.

### Request sample

Synthetic request, no body:

```http
GET /api/parking-search?lat=10.77&lon=106.70&vehicleType=CAR&radiusKm=5&limit=20
```

Use the current route's existing query binding for interval/filters, not a new grammar. Current uses authoritative assembly instant; interval uses paired offset timestamps normalized to UTC, half-open `[startAt,endAt)`, at most 24 hours and start at most seven days ahead. The proposed evaluator receives the exact validated interval, category, publication/configuration version and trusted site attribution. Unknown fields retain existing API handling; caller-supplied counts, source times and policy versions are never authority.

### Response fields and sample

Existing `success/data` envelope and `ParkingSummary` remain. Below is the availability-related fragment of a synthetic item, not a complete response:

```json
{
  "availability": { "kind": "current", "availableCount": null, "asOf": null, "reason": "unknown" },
  "fieldStatus": { "availability": { "freshness": "unknown", "reason": "unknown" } }
}
```

| Field | Rule |
| --- | --- |
| `availability.kind` | `current` or `interval`, matching input mode |
| `availableCount` | Nullable nonnegative integer; exact site/category count only after complete evidence. Known zero needs the same evidence or a fresh authoritative closure. No lower bound presented as an exact count. |
| `availability.asOf` | Null when no evaluation evidence exists. Proposed known count uses oldest required observation watermark, not HTTP read time. If previously-known evidence becomes stale, null the count but retain its original timestamp, matching existing `AtDelivery`. Closure keeps its original published timestamp. |
| `sourceAsOf.operations` | Same watermark as known availability, retained for stale previously-known evidence; null when no evaluation evidence exists. Existing field type retained. |
| `fieldStatus.availability` | Existing fresh/unknown/stale contract; unknown/stale count null. Reason vocabulary remains compatible (`unknown`, `stale`, or known closure `closed`); new public reasons require contract review. |

Private claim, Account, plate, session/reservation IDs, physical evidence, credentials and exact origin never enter provider input or public availability fields. Operations performs filtering/ranking before paging; model never reranks. `availableOnly=true` admits only proven positive results. Successful empty means no proven matches, not that all lots are full.

### Validation, error and retry outcomes

Existing envelope: `{"success":false,"code":"DEPENDENCY_UNAVAILABLE","message":"...","traceId":"..."}` with the actual HTTP status; no raw upstream URL/body/header. Public 401/403 must not be introduced for missing machine trust: owner auth failure is a dependency failure here.

| HTTP / outcome | Condition | Recovery |
| --- | --- | --- |
| 200 / unknown | Successful evaluation proves required source/policy/coverage absent or unsupported | Show unknown; do not auto-expand scope or manufacture a count |
| 200 / stale | Successfully read observations exceed approved age/skew/coverage | Null count; keep freshness honest |
| 200 / known | A01–A06 satisfied for full requested category/interval, or fresh closure proves zero | Historical observation only, no booking guarantee |
| 400 | Invalid bounds/filter/cursor | Correct request; no owner dispatch on invalid input |
| 409 | Catalogue/cursor expired or changed, including owner404/409 under existing adapter mapping | Start fresh search with explicit current origin/context |
| 429 | Existing admission/rate limit or bounded owner throttling | Preserve bounded Retry-After; no automatic retry |
| 503 | Owner/DB outage, malformed contradictory evidence, missing batch row, owner401/403/5xx, work bound exceeded | Safe unavailable; never unknown/empty fallback |
| 504 / cancellation | Existing total deadline or cancellation | Preserve timeout/cancel and ledger semantics; no success/empty stored for cancelled work |
| 500 / INTERNAL_ERROR | Unexpected route failure under existing hosting handler | Safe envelope; no raw URL/body/header/private evidence; subsequent read only, no automatic retry |

Counts and price remain independently sourced. Live continuation re-evaluates availability and can observe changes; it is not a frozen count snapshot. Retain existing filter/catalogue/anchor-bound cursor and 5-minute TTL. Do not claim repeatable counts across pages. No automatic retries, holds, mutations or new recovery scheduler.

## Acceptance and runtime verification plan

Automated cases use synthetic data and fake provider/HTTP through actual hosting/DI. Fixed-resource union, whole-interval and half-open boundaries, age/skew equality, missing/configuration/coverage/invalid evidence, resource bounds and cancellation now have Domain tests. Actual search HTTP/DI tests cover known/partial/stale/missing coverage, invalid site/duplicate resources, dependency outage, interval filtering, source timeout and ingress cancellation. Final User suite271 pass/54 skip includes these tests; see task record for commands. Producer-specific lifecycle, physical evidence ordering, buffer/protection policy resolution, tenant attribution and PostgreSQL/concurrency cases below remain PLANNED/UNVERIFIED. Synthetic complete evidence does not prove a producer exists.

| Case | Required assertion |
| --- | --- |
| Two compatible resources, one correlated reservation/allocation/session | One occupied resource, exact free count one; no triple subtraction |
| Occupant plus another Driver's protection on same resource | Union unavailable resources without removing independent future claim when occupant departs |
| Unlocated walk-in, shared/parent pool, incompatible category | Apply approved capacity model or unknown, never ignore occupants or double total |
| Invalid evidence shape or arithmetic | Negative/impossible counts, integer overflow, duplicate/conflicting resource IDs and tenant/site/category mismatch fail503; never clamp to zero, leak another scope or silently downgrade contradictory evidence to unknown |
| Pending hold / nominal expiry / concurrent expiry-payment | Only committed owner lifecycle releases claim; deadline alone never frees capacity |
| Checkout/payment/gate-open without verified passage | Count cannot increase |
| Verified passage with newer contradictory occupancy, duplicate/out-of-order evidence | Accepted evidence ordering/deduplication; contradiction fails safely |
| Effective buffer, protection, occupancy horizon | Before/exactly/after boundary; horizon expiry never proves physical departure; outside coverage unknown |
| Interval changes occupants/claims mid-window | Whole-interval admissibility, not endpoint sampling or unapproved arithmetic on independent slots |
| Closed interval overlap, open window gap, stale/future restriction | Correct half-open boundaries; zero only where authoritative closure suffices; otherwise unknown/stale |
| Source asOf age/skew equality and one tick beyond | Known count retains oldest required observation; newer DB read does not relabel freshness |
| Empty vs outage; 400/401/403/404/409/429/5xx | Unknown only after successful incomplete-source evaluation; outages remain dependency errors |
| Unexpected exception | Existing safe500 envelope; assert no private URL, body, header, token or evidence in response/log/provider input |
| Paging, count changes, global off, timeout/cancel | Backend filter/rank before paging, live semantics, zero dispatch/delivery when forbidden by existing S05 guards |
| Privacy and regression | No origin/private records/token in provider or logs; vehicles/reservations/context/ledger/recovery unchanged |

Separate isolated PostgreSQL/runtime scenarios remain UNVERIFIED: real coherent reads during concurrent writers, invalidation of structure/category/policy revisions, missed/late physical evidence, crash/replay, cursor-key rotation and missing-schema/outage fail-closed. Prepare synthetic resources and provenance with owner-approved producers, prove writer fencing/coverage and compare known-count assertions across transactions. Mock results cannot establish these guarantees. No SQL/runtime operations are authorized or executed by this packet.

## Review checks

- Source evidence and proposed producer behavior are distinguished; route presence is not capacity authority.
- A01–A06 require explicit owner/lead resolution before implementing the new count direction.
- No new service, transport trust, table, dependency, runtime, token scope or mutation has been introduced.
- S06 remains PARTIAL; S02/S05/P01 SQL unapplied, S04 DRAFT, search release default false, G04–G07 unresolved.
- Independent contract/service, privacy/context and test/recovery reviews are recorded in the S06 task record after completion.
