# Frontend demo readiness review — 15 October 2026

Scope: read-only source review of the requested 16 October demo, plus existing build/tests. No application source edits. The supplied Downloads/AGENTS.md is intended direction; the supplied review package is historical evidence pinned to 6165024, not proof of the current checkout. Read FE/AGENTS.md, FE/.agent/lead-frontend.md, the supplied package README and relevant call paths, and relevant visualization/gate requirements.

## Result

The current frontend builds, but the complete requested demo is not an integrated backend journey. Authentication, registration, approval and Operator creation have real HTTP clients. Owner onboarding/structure, Driver bookings and Operator gates use different local stores. Owner 3D viewing has no mounted viewer. Payment is explicitly unconfigured. Successful UI messages cannot establish persisted acceptance.

| Demo step | Current classification | Evidence / limitation |
|---|---|---|
| Login/logout | Implemented HTTP; runtime not verified | `FE/src/lib/authApi.ts:286` login; `:346` logout. Backend identity is used; tokens in sessionStorage. |
| Driver registration/contact proof | Implemented HTTP; runtime not verified | `SignUp.tsx:406`, `authApi.ts:228` onwards; OTP delivery/service/database must run. |
| Owner registration/contact proof | Implemented HTTP; runtime not verified | `SignUp.tsx:461`, `ownerApi.ts:38`; verification precedes Pending approval. |
| Admin approves Owner | Implemented HTTP; runtime not verified | `Applications.tsx:60`, `ownerApi.ts:77`; approval depends on Parking tenant provisioning. |
| Owner initial facility onboarding | Fixture-backed | `CreateParkingLotForOwner.tsx:213` creates local lot; `:246` local onboarding flag. |
| Owner Sites create/profile | Mixed real/fixture; misleading fallback | `SiteForm.tsx:207–262`: real create followed by local fallback/local lot generation. |
| Owner creates structure | Fixture-backed | Mounted `ParkingStructure.tsx:695,712` writes structureStore; does not call backend unit/slot API. |
| Owner views 3D | Missing from mounted Owner journey | `Parking3DViewer` callers are Driver and Operator only; onboarding selects an asset but has no viewer. |
| Owner creates Operator | Implemented HTTP; runtime not verified | `CreateOperatorForm.tsx:155`, `parkingApi.ts:188`; requires active real site. |
| Operator checks in/out | Fixture-backed | `CheckInOut.tsx:106–181` mutates local booking and lot only. |
| Slot immediately changes | Local Operator lot can change; backend/Owner structure disconnected | Different stores and IDs; no live projection subscription or authoritative reload. |
| Payment | Unconfigured frontend | `BookingFlow.tsx:365–376,1141`: QR explanatory placeholder; other methods show unavailable. |

Paths abbreviated in table are beneath `FE/src/roles/` or `FE/src/lib/` as named below.

## Prioritized concrete findings

### P1 FE-01 — Failed site creation is reported as success

`FE/src/roles/owner/parking-lots/SiteForm.tsx:207–234` catches every failed create (including network, permission, validation and duplicate failures), manufactures an ACTIVE `site-${Date.now()}` record and calls `onSaved(createdSite)` at line 262. `SiteManagement.tsx:81–90` then displays a saved message. A retry/reload loses the site because it never existed in PostgreSQL. Operator provisioning subsequently cannot use that ID. Remove the fallback from the real workflow and preserve the server error; explicit fixture mode, if retained, must identify itself.

### P1 FE-02 — Real Site IDs, local Lot IDs and structure data do not describe the same facility

`SiteForm.tsx:233` calls `store.createLot` without the backend `createdSite.id`; `FE/src/lib/store.ts:599–609` generates another ID. The mounted `OwnerDashboard.tsx:76` passes backend sites into `ParkingStructure`, which loads `structureStore.getStructure(siteId)` (`ParkingStructure.tsx:118–119`). `structureStore.ts:481–501` fabricates a default when the local structure is missing. Its saves only write localStorage (`:457–470,504–507`). Operator and Driver instead use `sp_lots`/`sp_bookings`. Creating floors/zones/slots under the Owner's real site will not change authoritative capacity or the operational lot shown to the Operator. The API-backed units/slots editor exists in `OwnerWorkspace.tsx:431,543`, but no file imports/mounts OwnerWorkspace. Reconnect the actual mounted editor to `parkingApi.structure/unit/slot/edit` and share backend IDs across readers.

### P1 FE-03 — Gate actions bypass the backend and current Operator scope

`FE/src/roles/operator/dashboard/OperatorDashboard.tsx:44–45` loads all lots from local storage. Its filter at `:90–100` falls back to all lots if no match. `authApi.ts:15–20,89–108` maps only backend ID/name/email/role and preserves cached fixture-only fields; new operators therefore have no authoritative site fields. `RoleRoute.tsx:34–37` defaults them to operation. `OperatorAssignments` does fetch real assignments for display, but those results never control the parent operational selector/actions.

`CheckInOut.tsx:106–181` uses a 400 ms timer, writes a client-selected booking status, and rewrites slot occupancy in localStorage. No admission/session/payment/current grant decision reaches a service. Thus another browser/database does not observe the action and an assignment listing does not prove scoped enforcement. Bind selector/action requests to current authorized backend assignments and committed operation results, with duplicate protection; treat any retained local gate as a clearly labeled fixture.

### P1 FE-04 — Owner cannot open the requested 3D viewer

`FE/src/roles/owner/dashboard/OwnerDashboard.tsx:48–80` offers sites/lots/structure/operators/finance/policies, with no mounted `Parking3DViewer`. Repository-wide caller search found only `roles/driver/booking/BookingFlow.tsx:560`, `BookingCard.tsx:359`, and `roles/operator/parking-lot/LotStatus.tsx:376`. The Owner onboarding model picker merely stores asset references. Add an Owner entry into an existing viewer backed by the selected site's persisted layout/status, checking actual fixed model assets. Requirements: module 03 FR-MAP-02 and its domain contract require backend state and distinguish requested/allocated positions.

### P1 FE-05 — Checkout releases occupancy without a departure or settlement result

`CheckInOut.tsx:110–136` immediately sets completed/available, without recording checkout cutoff, charge clearance or actual departure. This is more than a missing live update: it contradicts `docs/01-requirements/srs/modules/05-sessions-operations.md:27,65–71` (FR-GATE-05). For a simple demo, the explicit check-out action may represent a manual confirmed actual-departure workflow, but that meaning and durable backend operation must be implemented. Merely setting green on checkout is not proof of authoritative release; retain financial state separately.

### P2 FE-06 — Site profile edit resets local slot state and invents saved settings

`SiteForm.tsx:166–176` replaces the whole local lot with newly generated slots on every profile edit, dropping current occupancy/reservations and replacing slot IDs. Lines 194–204 report capacity/active values locally although the backend request at `:153–161` only patches profile fields. Newly created site requests likewise send only code/name/address; visible slot count/floors/pricing/devices are local. Preserve identity/state, persist supported settings through their owning contracts, and do not display local selections as committed server configuration.

### P2 FE-07 — Owner onboarding completion is browser-local

`OwnerDashboard.tsx:44–46` gates the whole workspace on `user.onboardingComplete`. That property comes from the cached local User spread in `authApi.ts:89–108`; onboarding at `CreateParkingLotForOwner.tsx:213–252` creates a local lot and saves local completion. The same approved Owner on a fresh browser repeats onboarding even when real sites exist. Derive facility onboarding from persisted server state or an explicit account contract.

### P2 FE-08 — Optional payment is not wired and cannot confirm a new booking

`FE/src/roles/driver/booking/BookingFlow.tsx:365–376` has no provider/order request. QR mode at `:1141–1143` expressly says no gateway is connected and the booking has not been charged. Holds/bookings remain local, so an ordinary new Driver cannot obtain a real confirmed paid booking for the gate through this UI. A fixture booking is not a verified payOS flow. Use Payment-owned order/attempt status and authenticated provider verification if this is included; do not add a manual success button and describe it as payment.

### P2 FE-09 — Refresh token is saved but never exchanged

`authApi.ts:126` saves a refresh token, but there is no refresh request in this client. `AppContext.tsx:150–156` expires the whole session at access-token expiry. Once the approved short access lifetime is enforced, a longer rehearsal/demo can unexpectedly return to sign-in. This is not a current exploit claim; implement supported refresh/rotation/replay handling or rehearse re-login. Do not lengthen access lifetime to conceal the gap.

### P2 FE-10 — Existing provisioning tests fail before exercising components

See verification below. The VM harness has stale mocked import lists, so all 22 failures occur at module load, not assertions. Updating mocks alone must not be presented as fixing FE-01/02; several test names explicitly expect real structure persistence, whereas the current mounted component writes localStorage.

## Demo click path and acceptance evidence

1. Open frontend at an origin allowed by backend CORS (current UserService allows localhost/127.0.0.1 port 5173). Use the top navigation for views; most screens live at `/` and stateful tabs, not dedicated URLs.
2. Sign up → Driver → submit a unique contact → receive/verify actual OTP → sign in → sign out. Capture HTTP outcomes and persisted account; do not assume queued delivery means received.
3. Sign up → Business → email/contact verification → pending approval. Sign in as a pre-provisioned Admin → Applications → Refresh applications → Approve the verified application. Verify the remote tenant and approved account before Owner login.
4. Sign in Owner → currently forced local onboarding for a fresh browser. Then Sites → create. Confirm an actual API 201 and a reload retaining the site; a success banner alone is insufficient because FE-01 exists.
5. Parking Structure → floor/zone/space creation. Currently local only: backend GET remains unchanged. This is a blocking acceptance item, not a successful persistence demo.
6. Operators → Create Operator → active persisted site → submit email/password. Verify returned ID/site grants and sign in as that Operator. My Assignments can show real backend grants; gate/lot selector is still independent.
7. Gate Check-In / Out → select a lot → lookup a confirmed fixture booking plate/ID → confirm check-in → inspect Lot Status & 3D Map → lookup again → confirm check-out. This can demonstrate browser-local transitions only and must be labeled accordingly until FE-02/03/05 are fixed. New unpaid Driver bookings will not automatically provide a confirmed candidate.
8. Owner 3D entry and Payment are currently unavailable through the requested integrated path. After fixes, verify two separately authenticated browsers observe the same persisted slot update and that reloading preserves it; check wrong-site Operator denial, duplicate check-in/out, and rejected/unknown payment behavior.

## Verification actually run

- `npm run build` in `FE`: PASS, Vite 8.3.1, 732 modules. Warning: main JavaScript chunk approximately 3.2 MB / 839 KB gzip. This script does not run a TypeScript type-check or browser acceptance.
- `node --test --test-reporter=tap tests/frontend/*.test.cjs`: FAIL, **88 tests, 66 passed, 22 failed, 0 skipped**. All 22 are in `operator-provisioning.test.cjs` and fail while loading VM imports: `react-dom`, `../data/data`, `../parking-lots/LotManagement`, or `../data/structureStore`. They do not establish 22 reproduced user-facing defects.
- No formatting, dependency installation, live payments, messaging, server startup, source edits, database mutation or browser runtime acceptance performed by this reviewer. Parent agent reports backend dependencies/DB runtime are not available for a full integration rehearsal; this review makes no runtime success claim.

## Recommended repair order

Remove false site success and align persisted IDs first; restore the mounted backend structure editor and server-derived Owner onboarding next; connect current Operator assignment/session/departure commands and reloadable occupancy projection; expose the existing 3D viewer to Owner; repair meaningful provisioning tests and rehearse real API steps. Add optional payOS only after the core facility/session path is verifiable. Keep every fixture visibly labeled during rehearsal.

## Recheck after Docker database startup

The user reported Docker/database availability after the initial review. Backend/database acceptance is being rechecked separately by the coordinating reviewer; the earlier dependency limitation above records the initial review environment only.

- `http://localhost:5173/` now responds HTTP 200 with the Vite development client and title `Smart Parking`. Ports 5174 and 8443 did not respond. No new development server was started.
- HTTP GET of the served Vite modules `/src/roles/owner/dashboard/OwnerDashboard.tsx`, `/src/roles/owner/structure/ParkingStructure.tsx`, `/src/roles/operator/check-in-out/CheckInOut.tsx` and `/src/roles/driver/booking/BookingFlow.tsx` all returned 200. Their served contents retain the same mounted Owner structure, local `structureStore.createFloor`, local `store.saveBooking`, and unconnected-payment message inspected above. This confirms the active frontend is serving those implementations; it does not establish browser interaction or backend mutation success.
- Reinspection confirms FE-01 through FE-09 remain applicable. Docker can enable the existing real authentication/account/site endpoints, but it cannot turn local Owner structure or local gate actions into HTTP operations, add the missing Owner viewer, or connect payment. The fake-site catch remains at `SiteForm.tsx:215`; gate local writes remain at `CheckInOut.tsx:118,134`; Owner floor/zone writes remain at `ParkingStructure.tsx:695,712`.
- No source changes were found in the reviewed workflow files since the earlier review. Build/tests were not repeated because no relevant implementation change justified a duplicate run. No browser clicks, credentials, payments or database writes were performed by this frontend reviewer.
