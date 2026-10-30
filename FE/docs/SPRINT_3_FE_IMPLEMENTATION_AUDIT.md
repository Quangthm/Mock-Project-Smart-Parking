# SmartParking — Sprint 3 Front-End Implementation Audit & QA Handoff

## 1. Sprint 3 Overview

| Item | Audit record |
|---|---|
| Audit date | 2026-10-20 (Asia/Saigon) |
| Repository | `C:\FPT\Mock-Project-Smart-Parking\FE` |
| Branch / commit | `fe-v2.2` / `f797797a2389d445a8a65eb23dda7c833dfc0eb8` |
| Scope | SPARK-276, SPARK-280, SPARK-251, SPARK-283, SPARK-294 only |
| Priority | SPARK-280 reservation and SPARK-251 payment |
| Environment | Windows PowerShell; installed Node modules; Vite 8.3.1 build; TypeScript CLI; FE and service listener ports observed locally |
| Source changes | None. This document is the sole requested deliverable. |

This audit inspected the actual FE source, relevant Backend source, available tests, and Jira issue descriptions. Jira issues [SPARK-276](https://mangamanagement.atlassian.net/browse/SPARK-276), [SPARK-280](https://mangamanagement.atlassian.net/browse/SPARK-280), [SPARK-251](https://mangamanagement.atlassian.net/browse/SPARK-251), [SPARK-283](https://mangamanagement.atlassian.net/browse/SPARK-283), and [SPARK-294](https://mangamanagement.atlassian.net/browse/SPARK-294) were accessible and cross-checked. All five were in Jira's **To Do** status at audit time; that workflow status alone is not implementation evidence. Related Backend Jira work SPARK-274/275, SPARK-278/279, SPARK-250, SPARK-282, and SPARK-293 was also To Do. SPARK-249 could not be confirmed because its Jira request timed out.

**Verification limit:** The production build and TypeScript command were executed. No authenticated browser walkthrough, API contract test with valid tokens, provider transaction, or Sprint 3 automated test was executed. Local listening ports do not prove endpoint behavior. Findings labeled *source inspection* are not runtime PASS results. No test credentials were used. The code has seeded localStorage parking data, but authentication still calls the User Service; therefore an independent offline mock mode was not demonstrated.

## 2. Executive Summary

The five Jira features are mutually exclusively classified at **issue level** below. Subrequirements can have different states; the detailed tables explain those differences. Backend dependencies listed later do not add to these counts.

| Classification | Issue count | Jira IDs |
|---|---:|---|
| FE Complete — BE Integrated | 0 | — |
| FE Complete — BE Pending | 0 | — |
| FE Mock Working — BE Pending | 0 | —; source contains mock paths, but no complete independent mock flow was executed |
| FE Partial | 5 | SPARK-276, 280, 251, 283, 294 |
| FE Not Implemented | 0 | — at issue level; numerous individual acceptance criteria are absent |
| Blocked — BE Dependency | 0 | — as a primary issue classification; all five have specific BE dependencies |
| Not Verified | 0 | — at issue level; live behavior remains unverified |
| **Total** | **5** | |

**Main handoff conclusion:** Vite builds, but `npx tsc --noEmit` fails with three existing cross-project errors. The Driver reservation submit path is intentionally stopped before a reservation API call. There is no payOS checkout integration in the inspected FE/BE source. Discovery uses localStorage parking fixtures, the Driver 3D view uses local slot state, and chat uses mock FAQ replies. None of these should be recorded as a live Backend-integrated Sprint 3 success.

## 3. Sprint 3 Front-End Status Matrix

| Jira ID | Feature | FE Status | Backend Status | Mock Working | Tested | Evidence |
|---|---|---|---|---|---|---|
| SPARK-276 | Discovery & Availability | FE Partial: searchable local list/map, details and directions; no vehicle/time availability query | Discovery contract/implementation pending; current Parking Service lacks required temporal availability/pricing/freshness | Local fixture path exists; full offline flow **not verified** | Source inspection; build only; browser NOT TESTED | `src/roles/driver/parking/FindParking.tsx:35-85,120-175,289-370`; `src/lib/store.ts:23,96`; Parking Service `Program.cs:134-164` |
| SPARK-280 | Reservation | FE Partial: slot/time/vehicle UI and local estimate; no review/commit | Driver review/create/allocation API absent from inspected Reservation Service; SPARK-278/279 To Do | No completed mock reservation; submit blocks | Source inspection; build only; browser NOT TESTED | `src/roles/driver/booking/BookingFlow.tsx:146-156,240-261`; Reservation Service `Program.cs:82-193` |
| SPARK-251 | Payment | FE Partial: payment-method and informational QR screens; no payOS checkout/status | Payment & Pricing service source not present in inspected services; SPARK-250 To Do | Legacy local payment fixture exists, but no working checkout mock demonstrated | Source inspection; build only; provider NOT TESTED | `src/roles/driver/booking/BookingFlow.tsx:250-261,1025-1070`; `src/roles/driver/data/paymentStore.ts:203-302` |
| SPARK-283 | 3D Operations View | FE Partial: 3D and 2D renderers exist; no authoritative live lifecycle/freshness/reconnect | SPARK-282 projection/contract To Do; operator layout snapshot route exists | 3D can consume fixture slots by code; browser NOT TESTED | Source inspection; build only; browser NOT TESTED | `src/components/3d/Parking3DViewer.tsx:59,395`; `src/roles/operator/parking-lot/LotStatus.tsx:297-323`; `src/lib/3d/slotMatcher.ts:205-220` |
| SPARK-294 | AI Assistant | FE Partial: morphing chat UI, messages/loading/errors; FAQ mock only, no structured result/handoff | Assistant backend route and draft preparation not found; SPARK-293 To Do | Mock reply code exists; runtime NOT TESTED | Source inspection; build only; browser NOT TESTED | `src/components/chat/FloatingChatbot.tsx:3,130-220`; `src/lib/chatbotMockService.ts:1-60` |

## 4. Detailed Feature Audit

Legend: **Yes** = meaningful implementation found in source; **Partial** = limited implementation; **No** = not found or submit path prevents it; **Unverified** = code exists but behavior was not run. “Mock Working” is **Unverified** unless an actual FE-only flow was executed. Evidence uses repository-relative paths with line references. Jira acceptance criteria take precedence over UI refinements carried over from earlier requests.

### SPARK-276 — Discovery & Availability

| Requirement | FE Implemented | BE Required | Mock Working | Verification Result | Evidence |
|---|---|---|---|---|---|
| Find Parking screen reachable from Driver | Yes | Authentication to enter Driver UI | Unverified | Source inspection | `src/roles/driver/dashboard/DriverDashboard.tsx:122-164,450-466` |
| Search name/address and list all active fixture lots | Yes | Real discovery feed for production | Unverified | Source inspection; no browser run | `src/roles/driver/parking/FindParking.tsx:35-36,68-85,289-370` |
| GPS/current location | Yes | No BE for browser geolocation | Unverified | Browser permission not exercised | `src/roles/driver/parking/FindParking.tsx:279-288` |
| Manual location selection | No | Discovery may need location input support | No | No manual origin/location UI found | `src/roles/driver/parking/FindParking.tsx:50-85,279-288` |
| Vehicle type filter | No | Vehicle-specific capacity contract | No | No filter in search predicate | `src/roles/driver/parking/FindParking.tsx:68-85` |
| Parking time filter | No | Time-bounded availability API | No | No time interval query/filter | `src/roles/driver/parking/FindParking.tsx:68-85` |
| Outdoor/indoor/underground filter | Yes, on local parking type | Real lot type field | Unverified | Source inspection only | `src/roles/driver/parking/FindParking.tsx:68-85` |
| Map markers with valid coordinates | Yes | Real accessible coordinates | Unverified | MapTiler browser load not run | `src/roles/driver/parking/FindParking.tsx:120-175` |
| Parking list, View selection and details | Yes | Real selected-lot data | Unverified | Source inspection only | `src/roles/driver/parking/FindParking.tsx:289-370`; `src/roles/driver/parking/ParkingDetailsPanel.tsx:36-82` |
| Car and motorcycle available counts | Partial: local slot counts; type-specific values show “Not provided” | Vehicle-specific available counts/freshness | No reliable mock count by type | Source inspection | `src/roles/driver/parking/ParkingDetailsPanel.tsx:14-27,36-64` |
| Prices and units by vehicle type | Partial: generic local hourly/daily rate; per-type rate unavailable | Authoritative car/motorcycle rates and billing units | No | Source inspection | `src/roles/driver/parking/ParkingDetailsPanel.tsx:36-64`; `src/lib/parkingApi.ts:10-61` |
| Parking address/name | Yes, from local lot | Real discovery response | Unverified | Source inspection | `src/roles/driver/parking/ParkingDetailsPanel.tsx:36-64`; `src/lib/store.ts:96` |
| Full/unavailable state | Partial: local slot status and active filter | BE-confirmed status at requested time | Unverified | No live state tested | `src/roles/driver/parking/FindParking.tsx:35-36,68-85`; `src/roles/driver/parking/ParkingDetailsPanel.tsx:14-27` |
| Freshness/stale/disconnected behavior | No | Timestamp/projection health and refresh semantics | No | `sp-data-change` is same-tab local-store event, not BE freshness | `src/roles/driver/parking/FindParking.tsx:91-104` |
| Distance/travel estimate with source/freshness | Partial: distance after GPS; directions via Valhalla | Travel estimate or routing data if acceptance requires it | Unverified | External route and geolocation not exercised | `src/roles/driver/parking/FindParking.tsx:205-288`; `src/lib/routingApi.ts:11` |
| Selection context into reservation review | Partial: selected lot passed to BookingFlow | Review endpoint | No completed review | BookingFlow receives lot; no review step/API | `src/roles/driver/dashboard/DriverDashboard.tsx:450-466`; `src/roles/driver/booking/BookingFlow.tsx:35-104,240-242` |
| Real Backend discovery data | No | SPARK-274/275 API/implementation | No | Driver imports local store, not parkingApi | `src/roles/driver/parking/FindParking.tsx:35-36`; `src/lib/parkingApi.ts:82-88` |
| Independent mock discovery | Partial | Auth still required for Driver screen | Not verified | Local fixtures exist; offline authentication not demonstrated | `src/lib/store.ts:23,48-55,96`; `src/context/AppContext.tsx:100,141-153` |

**UI refinements separate from Jira acceptance:** left searchable list and View action, right details, Search collapse, and Direction button are present by source inspection (`FindParking.tsx:289-370`, `ParkingDetailsPanel.tsx:70-82`). Their browser behavior and responsive layout were not executed during this audit. Direction uses browser GPS plus Valhalla; neither is proof of Backend-confirmed availability or a Sprint 3 discovery API.

### SPARK-280 — Reservation Integration (priority)

| Requirement | FE Implemented | BE Required | Mock Working | Verification Result | Evidence |
|---|---|---|---|---|---|
| BookingFlow reachable with selected lot | Yes | Auth to enter Driver UI | Unverified | Source inspection | `src/roles/driver/dashboard/DriverDashboard.tsx:450-466`; `src/roles/driver/booking/BookingFlow.tsx:35-104` |
| Selected lot and slot displayed | Yes, from local store/model | Real lot/slot projection | Unverified | Source inspection | `src/roles/driver/booking/BookingFlow.tsx:35-104,339-450` |
| Selected vehicle | Yes; vehiclesApi called | Vehicle API operational with Driver auth | Unverified | Backend vehicle route exists; no authenticated call tested | `src/roles/driver/booking/BookingFlow.tsx:105-137,667-840`; `src/lib/vehiclesApi.ts:69` |
| Booking date/time | Yes, local half-hour picker | Reservation rules/review | Unverified | Source inspection | `src/roles/driver/booking/BookingDateTimePicker.tsx:1-125`; `src/roles/driver/booking/BookingFlow.tsx:667-840` |
| Booking Review with exact Backend terms | No | Review/quote API, SPARK-278/279 | No | No review request or authoritative review state | `src/roles/driver/booking/BookingFlow.tsx:146-156,240-261` |
| Backend-calculated amount | No; FE computes estimate from local rate | Review/price response | No | `Math.ceil(hours) * lot.hourlyRate` | `src/roles/driver/booking/BookingFlow.tsx:146-156` |
| Prepayment/refund terms | Partial: generic policy modal, some hard-coded text | Backend-confirmed terms on review | No authoritative mock | Source inspection | `src/components/modals/LotPolicyModal.tsx:41-52,381,592` |
| Reservation confirm/create API | No | Driver create endpoint and idempotency contract | No | Handler explicitly stops with unavailable message | `src/roles/driver/booking/BookingFlow.tsx:240-242`; Reservation Service `Program.cs:82-193` |
| Duplicate submit prevention | No meaningful server commit to protect; UI state only | Idempotency key/response | No | Cannot verify without command | `src/roles/driver/booking/BookingFlow.tsx:240-261` |
| Changed terms force new review | No | Review version/expiry | No | No review state | `src/roles/driver/booking/BookingFlow.tsx:146-156,240-261` |
| PENDING_PAYMENT, CONFIRMED, ALLOCATED | No live lifecycle | Reservation status/query API | No | Type names alone do not render lifecycle | `src/lib/structureTypes.ts:7`; `src/roles/driver/booking/BookingFlow.tsx:240-261`; `src/roles/driver/booking/ActiveBookings.tsx:5-16` |
| Requested versus allocated slot | No | Allocation result/projection | No | No allocated slot response or display | `src/roles/driver/booking/BookingFlow.tsx:35-104,240-261` |
| Transition to actual payment | No | Reservation order + checkout API | No | Non-QR methods say not configured; QR informational | `src/roles/driver/booking/BookingFlow.tsx:250-261,1025-1070` |
| Loading/error states | Partial: vehicle/API and UI states; no reservation request states | Real service errors | Unverified | No reservation API call | `src/roles/driver/booking/BookingFlow.tsx:105-137,240-261` |
| Mock end-to-end reservation | No | — | No | Booking submit intentionally stops | `src/roles/driver/booking/BookingFlow.tsx:240-242` |
| 3D left, details right, Back to Parking | Yes by component/layout source | No new BE for layout | Unverified | Responsive/browser behavior untested | `src/roles/driver/booking/BookingFlow.tsx:296-305,339-450`; `src/roles/driver/dashboard/DriverDashboard.tsx:450-466` |

### SPARK-251 — Payment Gateway Integration (priority)

| Requirement | FE Implemented | BE Required | Mock Working | Verification Result | Evidence |
|---|---|---|---|---|---|
| Checkout surface | Partial: method and QR information screens | Valid reservation/order | Unverified | No reachable real checkout after blocked reservation submit | `src/roles/driver/booking/BookingFlow.tsx:250-261,850-1070` |
| Backend amount/order/attempt | No | Payment & Pricing API | No | FE uses local estimate; no provider attempt | `src/roles/driver/booking/BookingFlow.tsx:146-156,250-261` |
| Payment API client | No Sprint 3 client found | Payment service contract | No | Relevant FE search found only legacy local payment store/options | `src/roles/driver/data/paymentStore.ts:6,29-302`; `src/roles/driver/data/paymentOptions.ts:32-47` |
| payOS checkout/QR | No | payOS Backend order/checkout URL/QR | No | QR page explicitly says no gateway connected and no charge | `src/roles/driver/booking/BookingFlow.tsx:1025-1070` |
| Payment attempts and duplicate prevention | No | Attempt API/idempotency | No | No provider request path | `src/roles/driver/booking/BookingFlow.tsx:250-261,1025-1070` |
| Refresh/poll authoritative status | No | Payment status endpoint/webhook-backed query | No | No status polling/query | `src/roles/driver/booking/BookingFlow.tsx:850-1070` |
| Pending/failed/UNKNOWN/success states | No real lifecycle | Provider-specific verified status | No | Legacy fixture has pending/paid/failed, not live or UNKNOWN | `src/roles/driver/data/paymentStore.ts:29-196,203-302`; `src/lib/types.ts:11-13` |
| Reservation state after payment | No | Reservation/payment linkage | No | ActiveBookings/PaymentHistory show static empties | `src/roles/driver/booking/ActiveBookings.tsx:5-16`; `src/roles/driver/payments/PaymentHistory.tsx:3-18` |
| Checkout versus check-out settlement | No | Separate Backend flows/contracts | No | No implemented payment commands found | `src/roles/driver/booking/BookingFlow.tsx:250-261`; `src/roles/driver/data/paymentStore.ts:29-302` |
| Mock payment flow | Partial legacy fixture only | — | **Not verified** | `retryPayment` can set local “paid”; not a real payOS result and not connected to active checkout | `src/roles/driver/data/paymentStore.ts:203-302` |

No simulated QR, localStorage `paid` flag, or redirect is evidence of a successful payOS transaction. The checked Backend service tree contains IoTService, FacilityService, OperationService, and UserService; no Payment & Pricing service source was available in this repository snapshot.

### SPARK-283 — 3D Operations View

| Requirement | FE Implemented | BE Required | Mock Working | Verification Result | Evidence |
|---|---|---|---|---|---|
| 2D/3D visualizers | Yes | Layout feed for production | Unverified | Source inspection; no browser GPU run | `src/components/3d/Parking3DViewer.tsx:59`; `src/roles/operator/parking-lot/LotStatus.tsx:297-323` |
| Load lot layout | Partial: Operator calls operator-layout; Driver uses local store | Authenticated structure/layout API | Unverified | API route exists, payload not exercised | `src/lib/operatorApi.ts:164-178`; Parking Service `Program.cs:164`; `src/roles/driver/booking/BookingFlow.tsx:440-450` |
| Map real slot IDs to model | Partial; matching exists, but auto-generated fallback slots possible | Authoritative slot inventory/geometry | Unverified | Source inspection; correctness not tested against real model | `src/lib/3d/slotMatcher.ts:205-220` |
| Slot states displayed | Partial: local/structure snapshot statuses | Live reservation/session projection | Unverified | No live refresh test | `src/lib/operatorApi.ts:105-160`; `src/components/3d/Parking3DViewer.tsx:172-203` |
| Requested versus allocated | No | Allocation result/projection | No | UI transform collapses non-empty reservationState into reserved | `src/lib/operatorApi.ts:105-160` |
| ALLOCATED/OCCUPIED/RELEASED transitions | No authoritative lifecycle UI | SPARK-282 projection contract/stream | No | Static slot categories are not lifecycle evidence | `src/lib/operatorApi.ts:105-160`; `src/roles/operator/dashboard/OperatorDashboard.tsx:71-113` |
| Backend synchronization/freshness | No live freshness state; Operator loads snapshot | Timestamp/health and refresh contract | No | `refreshKey` event is not reconnection logic | `src/roles/operator/dashboard/OperatorDashboard.tsx:71-113` |
| Stale/disconnected/reconnect | No | Projection health/reconnect semantics | No | No polling/subscription/reconnect found | `src/roles/operator/dashboard/OperatorDashboard.tsx:71-113`; `src/roles/operator/parking-lot/LotStatus.tsx:14-87` |
| Mock 3D model with valid slots | Partial source support | — | Unverified | Fixture slots can be passed; rendering not executed | `src/lib/store.ts:64,96`; `src/roles/driver/booking/BookingFlow.tsx:440-450` |
| Never invent operational state | **No** | Authoritative state/unknown fallback | No | Extra mesh fallback becomes `slot-auto-*` with `available`; missing status defaults to `available` | `src/lib/3d/slotMatcher.ts:205-220`; `src/lib/3d/slotMaterialManager.ts:29` |

Operator's Backend-derived layout is a point-in-time API projection, not proof of a real-time Sprint 3 operations view. Driver's booking model is fed by local store slots. These are distinct integration paths.

### SPARK-294 — AI Assistant

| Requirement | FE Implemented | BE Required | Mock Working | Verification Result | Evidence |
|---|---|---|---|---|---|
| Chat bubble opens morphing window | Yes by UI/CSS source | No | Unverified | Animation not browser-tested | `src/components/chat/FloatingChatbot.tsx:168-220` |
| User and assistant messages | Yes | Assistant response for production | Unverified | Component state/code inspected | `src/components/chat/FloatingChatbot.tsx:14,130-160` |
| Loading and error states | Yes, local mock invocation handling | Network/API errors for production | Unverified | No browser interaction | `src/components/chat/FloatingChatbot.tsx:130-160` |
| Assistant API connected | No | SPARK-293 chat endpoint/model | No | Component imports mock service | `src/components/chat/FloatingChatbot.tsx:3`; `src/lib/chatbotMockService.ts:1-60` |
| Structured parking search results | No | Structured result contract/search | No | Replies are strings/FAQ | `src/lib/chatbotMockService.ts:1-60` |
| Select suggested lot | No | Result IDs/context | No | No result-card selection UI | `src/components/chat/FloatingChatbot.tsx:14-220` |
| Reservation draft handoff; no direct create | No handoff; no direct create | Draft preparation + review | No | Chat stays conversational only | `src/components/chat/FloatingChatbot.tsx:14-220`; `src/lib/chatbotMockService.ts:1-60` |
| Mock FAQ responses | Yes by source; no backend dependency | — | Unverified at runtime | Artificial delay and canned response exist, but no browser run | `src/lib/chatbotMockService.ts:1-60` |
| Available across roles | Yes by route/component placement | Role auth to access | Unverified | Driver own mount; non-Driver App mount | `src/roles/driver/dashboard/DriverDashboard.tsx:522`; `src/App.tsx:155-156` |
| Conversation persists after refresh | No evidence | Optional persistence contract | No | State lives in component memory | `src/components/chat/FloatingChatbot.tsx:14` |

## 5. Completed Front-End Work

“Completed” here means **implementation found by source inspection**, not a runtime QA PASS or completion of an entire Jira issue.

| Category | Verified implementation evidence | Limit |
|---|---|---|
| UI | Driver Find Parking list, map container, markers, details panel, View and Direction affordances (`FindParking.tsx:120-175,289-370`; `ParkingDetailsPanel.tsx:70-82`) | Actual browser layout/MapTiler load untested |
| UI | BookingFlow slot/model and Time & Vehicle screens; Back to Parking (`BookingFlow.tsx:296-305,339-450,667-840`) | Reservation review/commit absent |
| UI | 3D viewer and Operator 2D fallback (`Parking3DViewer.tsx:59,395`; `LotStatus.tsx:297-323`) | State mapping and live sync incomplete |
| UI | Chat bubble/window morph and message composer (`FloatingChatbot.tsx:130-220`) | Animation and keyboard behavior untested |
| Interaction code | Local name/address search, lot selection, GPS request, map fly/direction invocation (`FindParking.tsx:68-85,120-175,205-288`) | No authoritative discovery availability |
| Interaction code | Local slot selection/time selection and estimated price (`BookingFlow.tsx:146-204`; `BookingDateTimePicker.tsx:1-125`) | Estimate is FE-calculated; no booking command |
| API client | Vehicle API and Operator parking layout client are implemented (`vehiclesApi.ts:69`; `operatorApi.ts:164-178`) | Endpoint existence is not successful authenticated integration |
| Mock data | Seeded local parking lots/slots and chatbot FAQ replies (`store.ts:23,64,96`; `chatbotMockService.ts:1-60`) | Neither proves a complete standalone FE-only Sprint 3 flow |

**Completed Sprint 3 BE-integrated flows verified:** none. **Completed simulated end-to-end mock flows executed:** none.

## 6. Incomplete Front-End Work

| Jira ID | FE gap (not to be mislabeled as a BE-only blocker) | Evidence / proposed change for a later approved implementation |
|---|---|---|
| SPARK-276 | No manual location, vehicle/time filter, production discovery request, freshness display, source-labeled travel estimate, or vehicle-specific prices/counts | `FindParking.tsx:35-85,279-288`; `ParkingDetailsPanel.tsx:36-64`. Add UI/query state and integrate the agreed discovery contract when available. |
| SPARK-280 | No reservation review, authoritative quote/terms, re-review on changes, create/idempotency command, allocation/status UI, or functional mock submit | `BookingFlow.tsx:146-156,240-261`; `LotPolicyModal.tsx:41-52`. Implement FE state machine against approved contract; remove hard-coded term claims. |
| SPARK-251 | No payOS client/order/attempt/QR, status refresh/UNKNOWN, verified success, duplicate prevention, or distinction between settlement types | `BookingFlow.tsx:250-261,1025-1070`; `paymentStore.ts:203-302`. Implement provider-backed checkout only after API contract; do not promote local paid flags. |
| SPARK-283 | No requested/allocated/released lifecycle distinction, freshness/reconnect, and safe unknown state; generated mesh defaults to available | `operatorApi.ts:105-160`; `slotMatcher.ts:205-220`; `slotMaterialManager.ts:29`. Preserve authoritative IDs and render unknown for unmatched meshes. |
| SPARK-294 | No Assistant API, structured cards, selectable parking result, reservation draft handoff, or refresh persistence | `FloatingChatbot.tsx:3,14-220`; `chatbotMockService.ts:1-60`. Integrate structured API and hand off to review, never direct reservation. |
| Cross-project gate | TypeScript check fails in three files, two of which are outside Sprint 3; this is not a Backend dependency | See Section 9. Fix only with separate approval. |

## 7. Backend Dependencies & Blockers

| Jira ID | Feature | Missing Backend Capability / Existing Route | Impact | FE Workaround | Required BE Action |
|---|---|---|---|---|---|
| SPARK-276 | Availability | SPARK-274/275 discovery contract/implementation To Do. Parking Service has site/structure list (`src/Services/FacilityService/SmartParking.FacilityService.API/Program.cs:134-164`), but inspected FE `Site`/`Structure` fields lack temporal availability, price, freshness, and travel estimates (`FE/src/lib/parkingApi.ts:10-61`). | Cannot claim request-time, vehicle-specific availability or authoritative price. | LocalStorage lots/slots; **not** a production fallback. | Publish authenticated Driver discovery API, eligible sites, vehicle/time availability, price/unit, coordinates, freshness/source, full/stale semantics. |
| SPARK-280 | Reservation review/create | SPARK-278/279 To Do. Reservation Service routes inspected at `src/Services/OperationService/SmartParking.OperationService.API/Program.cs:82-193` cover Operator lookup/walk-in/check-in/out; no Driver review/create/status/allocation route found. Service also requires service key/DB and can return 503 on missing schema (`Program.cs:17-20,42-55`). | BookingFlow cannot commit or display authoritative terms/status/allocation. | No complete mock booking; handler stops. | Supply review/quote and idempotent create APIs, review version/expiry, lifecycle query, requested/allocated IDs, errors and auth contract. |
| SPARK-251 | payOS checkout | SPARK-250 To Do. No Payment & Pricing service source in available `src/Services` tree; no confirmed provider endpoint or response contract. SPARK-249 Jira fetch timed out. | No real checkout, QR, attempts, status/UNKNOWN, or verified payment success. | Informational QR page and dormant local payment fixture; neither is a paid transaction. | Supply payOS order/attempt, amount/provider/mode, QR/checkout URL, idempotency, webhook-verified status query and checkout/departure distinction. |
| SPARK-283 | Live 3D projection | SPARK-282 To Do. Parking Service operator-layout snapshot exists (`Program.cs:164`); authoritative reservation/session state projection and freshness contract absent from inspected integration. | Cannot show requested/allocated/occupied/released live sequence or stale/reconnect truthfully. | Local slots for Driver; Operator point-in-time layout. | Publish slot lifecycle projection keyed by real slot ID, timestamps/health, refresh or event stream, reconnection semantics. |
| SPARK-294 | Assistant | SPARK-293 To Do. User Service registers unconfigured chatbot components (`src/Services/UserService/SmartParking.UserService.API/Program.cs:25-30`); no chat route was found. | No real assistant, structured parking results, or draft handoff. | Explicit FAQ mock (`FE/src/lib/chatbotMockService.ts:1-60`). | Provide authenticated chat/structured result and reservation draft preparation contract; keep reservation commit in review flow. |

**Endpoint existence versus operational readiness:** The inspected route declarations and local listening ports do not verify successful authenticated requests, complete DB schemas, response fields, or payOS webhooks. Runtime integration remains unverified until BE and QA execute contract and end-to-end tests.

## 8. Front-End-Only Test Results

The table separates **source-predicted capability** from **executed behavior**. No authenticated browser session or end-to-end mock run was executed, so no row is marked PASS. “Runtime errors” means observed in an executed UI session, not inferred from static code.

| Feature | Screen Opens | Interaction Works | Mock Flow Works | Runtime Errors | Result |
|---|---|---|---|---|---|
| Discovery & Availability | Driver route and component exist; not opened in browser | Search/selection/GPS/direction code exists; not exercised | Seeded local lots exist, but auth depends on BE and no offline walkthrough occurred | Not observed; browser not run | **NOT TESTED** for FE-only runtime. Source shows partial discovery only. |
| Reservation | Component is routed; not opened in browser | Local selection/time/vehicle UI exists; `holdSlotAndContinue` deterministically stops before commit | **No completed mock reservation path in source** | Not observed; browser not run | **NOT TESTED** for runtime; FE-only end-to-end reservation is source-blocked. |
| Payment | Payment/QR JSX exists, but ordinary booking submit cannot reach real checkout | Method UI only; no provider transaction/status refresh | Dormant local payment fixture, no connected mock checkout | Not observed; browser not run | **NOT TESTED** for runtime; live or simulated checkout is source-blocked. |
| 3D Operations | Viewer and 2D fallback code exist; not opened in browser | Model selection/rendering code exists; no GPU test | Local slot feed exists; rendered output unverified | Not observed; browser not run | **NOT TESTED** for runtime; lifecycle sync incomplete. |
| AI Assistant | Component mounted in role shells; not opened in browser | Text/mock reply/morph code exists; not exercised | FAQ mock exists, but no independent authenticated browser run | Not observed; browser not run | **NOT TESTED** for runtime; structured handoff absent. |

Screen access requires an authenticated role. `src/context/AppContext.tsx:141-153` asks `authApi.currentUser()` to restore a session, and `src/lib/authApi.ts:6,9-11,90-150,282-292` uses backend tokens. Local fixture persistence in `src/lib/store.ts:23,48-55,96` does not replace that dependency. Refresh persistence of a booking or payment was not demonstrated; chat messages are in component state and will reset on remount/refresh (`FloatingChatbot.tsx:14`). Tester can inspect UI portions with a valid Driver/Operator session and local lot fixtures, but must not mark live availability, reservation, payment, or allocation PASS without a working Backend.

## 9. Build & Automated Test Results

| Command / check | Actual result | Relevant output / reason | Sprint 3 QA impact |
|---|---|---|---|
| `npm run build` | **PASS** | Vite 8.3.1 transformed 733 modules and completed in 4.27 s. Warning: output JS chunk approximately 3.17 MB exceeds Vite's 500 kB advisory size. | Packaging works; no user flow or API behavior proved. Bundle size is a performance risk, not a build failure. |
| `npx tsc --noEmit` | **FAIL** | `src/components/forms/GroupedNumberInput.tsx:27` TS18047 `inputRef.current` possibly null; `src/roles/driver/support/DriverSupport.tsx:65` TS2304 missing `setBookingId`; `src/roles/owner/parking-lots/SiteManagement.tsx:105` TS2339 `reassignSite` missing on API type. | Global FE type gate fails. Errors are outside this audit's Sprint 3 scope; they were not fixed. |
| `package.json` script inspection | **DONE** | Scripts available: `dev`, `build`, `preview`, `format`. No `lint`, `test`, `typecheck`, or E2E script. | No supported lint or Sprint 3 test command to execute. |
| Existing test inspection | **DONE; no Sprint 3 suite found** | Sibling `tests/frontend` contains auth/sign-in/operator-provisioning tests; no tests for the five Sprint 3 issues were found. | Do not count previous-sprint tests as Sprint 3 verification. |
| Browser walkthrough | **NOT RUN** | No authenticated browser session or test credentials used; code inspection cannot establish map, 3D, animation, or provider behavior. | All UI-only and FE-only flow rows remain NOT TESTED. |
| Authenticated API/provider test | **NOT RUN** | Local listener ports 5035, 5045, 5055 were observed, but no authorized requests, response payloads, DB readiness, or payOS transaction were verified. | No issue qualifies as “BE Integrated.” |

No ESLint/test command was invented, and no existing application source was changed to make a check pass. A successful Vite build does not override the TypeScript failure or missing Sprint 3 behavior.

## 10. Bugs & Risks

Reproduction steps below are **for Tester** unless explicitly labeled executed. Actual behavior and root cause are supported by source inspection; no browser reproduction was performed in this audit.

| ID / Severity | Steps to reproduce | Expected result | Actual result / verified cause | Class / evidence |
|---|---|---|---|---|
| S3-FE-01 / **Critical** | In an authenticated Driver session, select a lot/slot, enter Time & Vehicle, continue. | Review authoritative terms, then create an idempotent reservation. | Source handler only shows “Booking is unavailable until the Driver booking service is connected”; no create call. | FE + BE dependency; `src/roles/driver/booking/BookingFlow.tsx:240-242`; Reservation Service `Program.cs:82-193`. |
| S3-FE-02 / **Critical** | Attempt checkout after reservation or inspect QR step. | Backend-priced payOS order, genuine QR/URL, verified status. | No payment client/provider command; QR copy explicitly says no gateway/charge. | FE + BE dependency; `BookingFlow.tsx:250-261,1025-1070`; `paymentStore.ts:203-302`. |
| S3-FE-03 / **High** | Open Find Parking with seeded lots; compare displayed availability with live Backend. | Authorized request-time availability with freshness. | Driver list comes from `store.getLots()` localStorage; no production discovery request. | FE + BE dependency; `FindParking.tsx:35-36,91-104`; `store.ts:23,96`. |
| S3-FE-04 / **High** | Load a 3D model with more matched meshes than authoritative slots. | Unmapped mesh is unknown/unselectable. | Matcher generates `slot-auto-*` with `available`; material manager also defaults missing status to available. Source-proven risk of invented operational availability. | FE; `src/lib/3d/slotMatcher.ts:205-220`; `src/lib/3d/slotMaterialManager.ts:29`. |
| S3-FE-05 / **High** | Read policy/prepayment terms in a Driver flow. | Backend-confirmed, current reservation terms. | Generic fallback and hard-coded refund text may be shown; no reviewed terms response. | FE + BE dependency; `src/components/modals/LotPolicyModal.tsx:41-52,381,592`; `BookingFlow.tsx:146-156`. |
| S3-FE-06 / **High** | Change a slot allocation/session state on BE while Operator 3D view remains open. | Allocated/occupied/released state updates with freshness and disconnect handling. | Operator layout is loaded as a snapshot; no Sprint 3 live projection/reconnect path found. Backend runtime not tested. | Integration; `src/lib/operatorApi.ts:105-178`; `src/roles/operator/dashboard/OperatorDashboard.tsx:71-113`. |
| S3-FE-07 / **Medium** | Ask chatbot for parking and try to select a result. | Structured parking result and handoff to reservation review. | Mock FAQ returns text; no selectable structured result or handoff. | FE + BE dependency; `FloatingChatbot.tsx:3,14-220`; `chatbotMockService.ts:1-60`. |
| S3-FE-08 / **Medium** | Run `npx tsc --noEmit`. | No TypeScript errors. | Command failed with the three errors in Section 9; no Sprint 3 source fix authorized. | FE build gate; `GroupedNumberInput.tsx:27`; `DriverSupport.tsx:65`; `SiteManagement.tsx:105`. |
| S3-FE-09 / **Medium** | Inspect Driver Active Bookings, Booking History and Payment History. | Current reservation/payment lifecycle and receipts. | ActiveBookings and PaymentHistory render static empty states; BookingHistory has local tickets, not authoritative reservation history. | FE + BE dependency; `ActiveBookings.tsx:5-16`; `BookingHistory.tsx:9-43`; `PaymentHistory.tsx:3-18`. |
| S3-FE-10 / **Medium** | Select a lot and inspect car/motorcycle availability and rates. | Correct per-type values, currency/unit or explicit unknown. | Panel shows “Not provided” for per-type data and generic local rate; API types lack needed fields. | FE + BE dependency; `ParkingDetailsPanel.tsx:36-64`; `parkingApi.ts:10-61`. |
| S3-FE-11 / **Medium** | Try full FE-only workflow with the APIs unavailable. | Clearly labeled independent mock mode or graceful blocked state. | Seeded local lots do not bypass backend auth, booking stops, and QR cannot pay. This is a source-based limitation, not a browser result. | FE; `AppContext.tsx:141-153`; `BookingFlow.tsx:240-261`. |

## 11. Tester Regression Checklist

These cases are **unexecuted handoff cases**. Tester should independently fill **Actual Result** and **PASS/FAIL** after each run. A code-inspection observation in earlier sections is not a prefilled result. Use separate Backend-integrated and fixture/mock sessions where available; record API request/response IDs and screenshots without secrets. “Expected Result” is the acceptance target, so a currently missing feature is expected to FAIL until implemented.

| Test ID | Jira ID | Test Scenario | Preconditions | Test Steps | Expected Result | Actual Result | PASS/FAIL | Notes |
|---|---|---|---|---|---|---|---|---|
| D-01 | SPARK-276 | Open discovery | Driver signed in; MapTiler key | Driver Dashboard → Find Parking | Search, map, and permitted lot list load with no runtime error | | | Mark data source local/BE. |
| D-02 | SPARK-276 | Search match | At least two permitted lots | Enter lot name and address fragments | Correct matching list/markers, no unauthorized lot | | | Capture query and IDs. |
| D-03 | SPARK-276 | Empty search | Driver signed in | Enter impossible name | Clear empty state, map remains usable | | | |
| D-04 | SPARK-276 | Discovery loading | Throttled discovery API, if integrated | Open Find Parking | Loading UI until real response; no stale value presented as fresh | | | API contract pending. |
| D-05 | SPARK-276 | Discovery API unavailable | API intentionally unavailable | Reload Find Parking | Error and retry/recovery; no silent mock substitution | | | Current local path must be identified. |
| D-06 | SPARK-276 | GPS allowed/denied | Browser geolocation controllable | Use current location; repeat with denial | Correct origin or clear permission error; no hard-coded origin | | | |
| D-07 | SPARK-276 | Manual location | Driver signed in | Set origin without GPS | Discovery uses selected manual location | | | Expected currently missing by source. |
| D-08 | SPARK-276 | Vehicle/time filters | API with differing car/moto/time availability | Change vehicle and requested interval | Results reflect backend-confirmed request-time availability | | | Expected currently missing by source. |
| D-09 | SPARK-276 | Lot type/price/distance filters | Lots with known types/rates/coordinates | Change each filter | Visible list and map agree; missing data not treated as zero | | | Distinguish fixture from BE. |
| D-10 | SPARK-276 | Select list/marker/View | Several valid lots | Select list, marker, then View on another lot | Map flies, selected marker/details update, no stale lot | | | |
| D-11 | SPARK-276 | Availability and full lot | Known full/unknown/available records | Open each detail | Per-vehicle counts and booking eligibility match backend and freshness | | | No invented counts. |
| D-12 | SPARK-276 | Prices/address/source | Known backend record | Open details | Correct address, car/moto price, currency, unit, source/freshness | | | |
| D-13 | SPARK-276 | Direction route | GPS permitted; routing available | Select lot → Direction; close route | Road route, distance/time if supplied, selectable lot retained | | | Valhalla is external service. |
| D-14 | SPARK-276 | Refresh and panel state | Selected lot/search active | Reload, collapse/reopen search | No broken navigation; persistence matches product contract | | | Record what is retained. |
| D-15 | SPARK-276 | Selection to booking | Driver with eligible lot | Select lot → Book Now | Correct lot ID/context reaches review, no reservation guarantee | | | |
| R-01 | SPARK-280 | Open selected booking | Driver; eligible lot, slot and vehicle | Book Now from Find Parking | Correct lot/slot/vehicle UI loads | | | |
| R-02 | SPARK-280 | 3D slot choice | Model with valid slot IDs | Pick available slot, then occupied slot | Correct real ID/status; occupied slot rejected | | | Check generated IDs. |
| R-03 | SPARK-280 | Time/vehicle valid | Driver has verified vehicle | Choose valid arrival/end/vehicle | Review request uses exact selected data | | | |
| R-04 | SPARK-280 | Invalid time/input | Driver signed in | Select past/reversed/overlap/unsupported interval | Validation blocks commit with clear reason | | | Include backend rules. |
| R-05 | SPARK-280 | Review exact terms | Review API ready | Continue after valid selection | Backend-calculated price, deposit, refund, expiry, allocation terms shown | | | Compare raw response. |
| R-06 | SPARK-280 | Changed terms | Existing review | Change slot, time, vehicle, or receive changed quote | Prior review invalidated; new review required before confirm | | | |
| R-07 | SPARK-280 | Confirm once/duplicate | Create API ready | Double-click confirm; retry after timeout | One idempotent reservation, no duplicate charge/order | | | Capture idempotency ID. |
| R-08 | SPARK-280 | Lifecycle states | BE fixtures for states | Query PENDING_PAYMENT, CONFIRMED, ALLOCATED | Correct labels/actions; requested vs allocated slot distinct | | | |
| R-09 | SPARK-280 | API unavailable/error | Review/create API fails | Continue and retry | Loading ends, error visible, no fake reservation; safe retry | | | |
| R-10 | SPARK-280 | Back and refresh | Selection in progress | Back to Parking, reopen; refresh booking page | Expected selection persistence and unsaved-change handling | | | |
| R-11 | SPARK-280 | FE-only mock path | Backend disabled, supported mock mode if configured | Attempt selection → review → confirm | Controlled simulated result or explicit block; never labeled real booking | | | Current source blocks. |
| P-01 | SPARK-251 | Checkout order | Confirmed pending-payment reservation; payOS test env | Open checkout | Exact BE amount, order/attempt, mode/provider and payment method | | | Never use real card in QA. |
| P-02 | SPARK-251 | payOS QR | Valid provider response | Request QR/check-out link | Provider-generated QR/URL belongs to correct order and amount | | | Informational local QR is FAIL. |
| P-03 | SPARK-251 | Pending and refresh | Pending attempt | Refresh status/page | Pending remains pending until verified provider/BE update | | | |
| P-04 | SPARK-251 | Verified success | payOS sandbox confirms payment | Complete sandbox transaction; refresh | BE-verified success, linked reservation updates once | | | Capture provider/order IDs. |
| P-05 | SPARK-251 | Failed/cancelled/UNKNOWN | Provider fixtures for each state | Trigger each result | Distinct, accurate state and retry guidance; UNKNOWN not shown as success | | | |
| P-06 | SPARK-251 | Duplicate attempt | Pending order | Double-click pay/reload/retry | Idempotent attempt or explicit safe retry; no duplicate order | | | |
| P-07 | SPARK-251 | API unavailable | Payment service offline | Request checkout/refresh | Error or unknown, never simulated paid | | | |
| P-08 | SPARK-251 | Checkout vs departure | Both charge types available | Open each payment from respective workflow | Order purpose, amount and status remain separate | | | |
| P-09 | SPARK-251 | Mock versus real | Mock and sandbox environments | Repeat QR/payment flow in both | Mock clearly labeled; only sandbox provider confirmation counts as integrated | | | Current mock flow unverified. |
| P-10 | SPARK-251 | Payment history refresh | Completed sandbox order | Navigate History and reload | Current authoritative payment/reservation appears once | | | |
| V-01 | SPARK-283 | 2D/3D open | Operator assigned to site; valid layout | Open lot view; switch 2D/3D | Same real slots/statuses; model loads; fallback clear | | | |
| V-02 | SPARK-283 | Model mapping | Model/layout with known IDs | Select meshes and compare API slot list | Every selectable mesh maps to real authoritative slot ID | | | Check `slot-auto-*`. |
| V-03 | SPARK-283 | Lifecycle | Backend sends allocated/occupied/released | Advance one slot through states | Exact state and requested/allocated distinction visible | | | |
| V-04 | SPARK-283 | Missing/unknown state | Layout omits status or extra mesh exists | Load model | Unknown/unmapped slot cannot be offered as available | | | Source indicates risk. |
| V-05 | SPARK-283 | Stale/disconnect/reconnect | Interrupt live feed/API | Disconnect, restore, observe | Stale warning, no fresh claims, full resync on reconnect | | | |
| V-06 | SPARK-283 | Layout API unavailable/empty | BE returns error/empty | Open Operator lot view | Clear error/empty state; no invented slots | | | |
| V-07 | SPARK-283 | FE-only model fixture | Controlled local lot/model | Load model without layout API | Fixture visualization works if supported and is labeled mock | | | Runtime not audited. |
| A-01 | SPARK-294 | Bubble animation | Any role signed in | Open/close by pointer and keyboard | One bubble morphs to chat and back, no jump/overlap | | | Check reduced motion. |
| A-02 | SPARK-294 | Mock FAQ message | Assistant Backend disabled; explicit mock setting if any | Send known prompt | User/assistant messages, loading/error handling; clearly mock | | | No independent mode proven. |
| A-03 | SPARK-294 | Assistant API success | Chat API ready | Ask for parking with vehicle/time | Structured authorized results with lot IDs, no invented availability | | | |
| A-04 | SPARK-294 | Result selection/handoff | Structured result returned | Select suggested lot, proceed | Correct context reaches Booking Review; no direct reservation | | | |
| A-05 | SPARK-294 | API failure/empty/invalid | Control API responses | Send prompt in each condition | Error/empty states and retry; no fabricated success | | | |
| A-06 | SPARK-294 | Refresh/navigation/roles | Driver/Owner/Operator/Admin accounts | Open chat, navigate, refresh, reopen | Access and conversation persistence match product contract | | | Current state appears in memory only. |

## 12. Tester Handoff Instructions

1. **Run FE:** From `FE/`, use the already running Vite preview when available. Otherwise inspect `package.json` and run `npm run dev`; do not assume a fixed port. Run `npm run build` and `npx tsc --noEmit` separately. `format` changes files and is not a QA check. There is no declared lint/test/E2E script in this snapshot.
2. **Environment:** `src/lib/authApi.ts:6` reads `VITE_API_BASE_URL` (fallback `http://localhost:5035`). `src/lib/parkingApi.ts:4` targets Parking Service (`http://localhost:5045` by default). `src/lib/routingApi.ts:11` uses an external Valhalla route service. The map requires the existing MapTiler key/configuration in `FindParking.tsx:120-175`. Confirm environment values from the local deployment configuration without printing keys/tokens in bug reports. No `.env` contents or credentials were copied into this document.
3. **Mock mode:** There is **no verified standalone Sprint 3 mock mode switch**. `src/lib/store.ts:23,48-55,96` seeds browser localStorage lots/slots, and `src/lib/chatbotMockService.ts:1-60` returns FAQ replies. Authentication still requires a valid Driver session. The legacy `paymentStore.ts` local paid flag is **not** a payOS result. Do not create or claim fake operational responses.
4. **Without Backend:** Once a valid session is available, Tester may examine local discovery/filter/map/details, booking layout and local slot/time interactions, 3D rendering, and FAQ chat. This audit did not execute these browser interactions; Tester should record real results. Reservation confirm and real payment are source-blocked. If auth service is fully offline with no valid session, the Driver route cannot be assumed accessible.
5. **With Backend:** Discovery availability/price/freshness, reservation review/create/allocation, payment provider order/status, 3D live lifecycle, and structured AI handoff require their agreed Backend services, test data and contracts. Confirm DB schema, auth and provider sandbox readiness; a listening port or controller declaration is insufficient.
6. **Accounts/permissions:** Request non-production Driver, assigned Operator, Owner and Admin QA accounts through the team. Driver needs eligible vehicle/site access; Operator needs an assigned site for operations 3D. Do not put passwords, bearer tokens, provider secrets or personal data in the checklist or bug report.
7. **Do not mark PASS yet:** No live integrated Jira feature was verified in this audit. Do not pass reservation creation, payOS settlement, live availability, live slot lifecycle, or assistant handoff on the basis of screens, localStorage, mock QR, or source inspection. Also keep the TypeScript gate marked FAIL until independently re-run successfully.
8. **Report bugs:** Include Jira ID and checklist Test ID; role/account type without credentials; environment/branch/commit; API mode (fixture, local BE, sandbox); reproducible steps; expected/actual behavior; screenshot/video; browser console and sanitized request/response or correlation IDs; whether a retry or reload changes the result. Flag any fake “paid” or “available” operational claim as high priority.

## 13. Final Readiness Assessment

| Dimension | Status | Rationale |
|---|---|---|
| FE UI Readiness | **PARTIALLY READY** | Discovery, booking, payment information, 3D, and chat surfaces exist by source inspection; authenticated browser/responsive/animation verification was not run. |
| FE Functional Readiness | **NOT READY** | Reservation submit stops before review/create, payment has no real checkout, discovery lacks request-time/vehicle availability, 3D live state and AI handoff are missing. |
| Mock Testing Readiness | **PARTIALLY READY** | Local lot/slot and FAQ fixtures exist for limited UI exploration with a valid session. No independent end-to-end mock reservation/payment flow exists or was executed. |
| Backend Integration Readiness | **NOT READY** | Required discovery, reservation, payment, live projection and assistant contracts/implementations are absent or unverified; FE integration work also remains. |
| End-to-End Testing Readiness | **NOT READY** | No authenticated Sprint 3 E2E run, real reservation, payOS sandbox confirmation, or verified lifecycle synchronization. |
| Sprint 3 QA Handoff Readiness | **PARTIALLY READY** | This evidence matrix and regression checklist are ready for Tester; pass/fail execution and BE readiness are outstanding. |

**Requested independent retest:** Tester should re-execute every case in Section 11, fill Actual Result and PASS/FAIL, and attach sanitized evidence. Do not declare Sprint 3 Front-End complete until implementation and independently verified QA results support that conclusion.
