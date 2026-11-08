# AI Chatbot — Hội thoại đăng nhập và phục hồi yêu cầu

Cập nhật08/11/2026: S05 đã có implementation; S06 owner reads và S08 read UI đã được bổ sung. IoT S02/S05 SQL đã có rollout local, Admin auth và Gemini FAQ có bằng chứng thật trong [lịch sử triển khai](../../../ai/tasks/archive/chatbot-implementation-history.md). Không đồng nghĩa PostgreSQL concurrency/restart/retention và toàn luồng Driver đã nghiệm thu. S07 draft vẫn chưa triển khai. [Ma trận kiểm chứng hiện hành](../../04-testing/assistant-handoff-testing.md).

Mốc S05 ban đầu05/11: IoT355/355 và build pass; full solution còn4 Facility baseline failures. Đây là [test lịch sử](../../../ai/tasks/archive/chatbot-implementation-history.md#nhat-ky-9), không phải lần chạy mới.

## Approval and scope

On 2026-11-05 the user delegated selection after explicit D02/D03/wire questions. Selected: absolute **30 days from conversation creation**, immediate expiry hiding, daily cleanup; local context at most **six completed turns and 12,000 UTF-16 characters**; provider receives only a server-owned FAQ topic and approved public excerpt. Raw text, transcript, Account/User IDs, tokens and private records never become provider context. This deliberately supports narrow FAQ continuation (“tell me more”, “explain more”, “nói rõ hơn”, “giải thích thêm”), not general conversational extraction. Guest transcript is never imported.

S01 §§4–6 and inherited P0 conversation/ledger wire, B-03 delivery expiry and B-05 limits were approved for code/offline tests. D01 delegation and D05 domain APIs are not consumed. B-02 runtime/key rotation, backup deletion SLA, provider deployment/retention terms and Admin publication remain OPEN. TOKEN stale claims may remain accepted until JWT expiry; there is no live revocation guarantee. G08 `/hubs/access` / `AccessRevoked` still lacks a server counterpart.

## Implemented HTTP contract

Driver và Guest dùng chung cách xử lý câu hỏi về đến trễ, đỗ quá giờ và thời điểm chưa rõ. Câu đỗ quá giờ dùng câu hỏi cố định của backend và mục hướng dẫn thanh toán đã duyệt. Hai trường hợp còn lại trả hướng dẫn tại chỗ. Tin nhắn ngoài phạm vi hướng người dùng về FAQ. Trạng thái tìm bãi thuộc `capabilities.features.searchParking` và phản hồi khi người dùng gửi yêu cầu tìm bãi.

All routes below require locally validated RS256 JWT with trusted issuer/audience/public key, required identity claims and exact Driver role. Owner is `account_id`, never `sub`, body or model. Same User with another Account is isolated; a new valid token for the same Account can recover retained results. JSON, no-store, correlation ID; unknown JSON fields ignored without affecting ownership or fingerprints.

| Method / route | Payload / result |
|---|---|
| GET `/api/assistant/capabilities` | No query/body; version0.4.1; FAQ only when release/global/storage/publication ready; all business flags false |
| POST `/api/assistant/conversations` | `{clientConversationId,locale}`;201 new/200 replay; Location; changed locale409 |
| GET `/api/assistant/conversations` | `limit` default20/max50, protected cursor; current Account only |
| GET `/api/assistant/conversations/{id}` | Conversation; missing/foreign/expired404 |
| GET `/api/assistant/conversations/{id}/messages` | limit50/max100, protected cursor; newest page returned ascending sequence; active request ID; no provider call |
| POST `/api/assistant/conversations/{id}/messages` | `{clientMessageId,expectedRevision,message}`;200 terminal/202 running replay with Location/Retry-After2; fingerprint trimmed message+expectedRevision |
| GET `/api/assistant/conversations/{id}/requests/{clientMessageId}` |200 saved/current ledger, including failed/cancelled/running; never restarts generation |
| POST `/api/assistant/conversations/{id}/requests/{clientMessageId}/cancel` | JSON object `{}`;200 first terminal wins; completed/failed/cancelled unchanged |

Wire inherits P0 `Conversation`, `Page`, `Message`, `RequestResult` plus S01 `requestId`, `mode`, `cards`, `cancelled`. Request ID is server generated, distinct from the client message key. Draft routes are absent. Sources retain original excerpt/version; lookup/history annotate withdrawn/outdated. Annotation has a 500ms bound; unavailable publication is conservatively shown withdrawn so corpus failure cannot indefinitely block recovery.

**Known contract discrepancy:** S01 §1 says `errors` object/null, while P0 and existing shared IoT formatter use `errors:[]`. S05 deliberately preserves the implemented shared P0 envelope `{success:false,code,message,errors:[],traceId}`; it does not silently migrate S02/S04 or claim object/null compatibility. HTTP status is not duplicated in JSON. Consumers should use `code`; coordinated envelope normalization is OPEN.

400 invalid JSON/known fields/path/query/cursor;401 invalid/expired JWT plus Bearer challenge;403 valid wrong role;404 uniform invisible resource;409 `IDEMPOTENCY_CONFLICT`/`CONVERSATION_BUSY`/`REVISION_CONFLICT`;413 body;415 media;429 limits plus Retry-After;503 disabled/storage/dependency/publication;504 overall timeout. After admission provider failure is stored `failed` with safe requestError (HTTP200 when delivered). Upstream auth failure does not invalidate the user token. `REQUEST_INTERRUPTED` means local processing has no deliverable answer, **not proof the provider did not execute or bill**. Unknown POST outcome means lookup the same key;404 during a racing POST is not proof it never arrived.

## Lifecycle and bounds

Admission commits user message, request ledger, snapshot metadata and revision together. Every competing request writer locks the owning conversation first. One running request per conversation, unique client keys/sequences and conditional terminal writes protect invariants. No transaction survives into provider I/O. Completion claims a still-running request before its deadline, then appends its answer in the same transaction; failed advances revision without an answer, cancelled changes no transcript/revision. A fresh message after cancel uses the retained user-message revision.

New messages are bounded per process:10/min/Account, two active generations, no queue. A60/min/Account endpoint limiter also covers recovery reads and replay; limits are not distributed quotas.16KiB JSON, trimmed message1–4000, output≤8000. One25s overall budget starts at endpoint execution including custom body binding, admission, generation and final delivery; generation uses at most20s and remaining budget reserves up to5s for persistence. Admission subtracts connection/lock elapsed time from the durable deadline. Bounds are implementation timeouts, not latency SLA or proof of proxy/client deployment configuration.

Host owns and awaits admitted work independently of socket disconnect. Each actual model attempt checks the durable request, current settings and publication snapshot immediately before invoking the existing S02 generator adapter. Cancel/off is cooperative; a provider call already past its dispatch boundary may still execute/bill. Polling detects cancel/off while awaiting provider; terminal CAS prevents late answer insertion. S02 routing/failover remains bounded to its approved candidates and budgets. No exactly-once billing promise.

Global off stops new conversation/message admission and new reply delivery. Existing authenticated history/result lookup and identical replay remain usable, including create replay. Terminal persistence precedes a final token-expiry and snapshot check; after401 the same Account refreshes via UserService and looks up the existing key. A publication/settings change suppresses fresh delivery but does not destroy stored recovery evidence.

Startup/periodic recovery performs set-based, conversation-locked, skip-locked transitions for overdue running requests, without provider redispatch. Poll interval1s, batch≤1000, operation timeout5s. The intended recovery target is within60s after deadline; **real PostgreSQL contention/load/restart and outage behavior are UNVERIFIED**, and an unavailable store prevents that guarantee. Lookup also recovers an overdue request transactionally. Expired conversations are immediately invisible; daily purge cascades ledger/messages/tool records except internal retention holds. Holds do not grant Driver access or auto-release.

## SQL and separately authorized setup

Review [20261105_iot_assistant_s05.sql](../../../scripts/database/migrations/20261105_iot_assistant_s05.sql) against the actual IoT schema and existing S02 migration. It extends existing conversation/message tables and adds request ledger; does not create a second business store, cross-service FK, signing key or plaintext credential field. Existing `session_token` must remain null. Nonempty legacy data without explicit Account mapping/message conversion causes the transaction to abort; never infer Account from UserId. No automatic startup migration.

Before any apply, the operator must obtain separate authorization and:

1. Identify the exact IoT database/schema and pause Assistant writers/recovery workers. Verify database/schema identity without exposing connection secrets. Do not use User/Operations/Payment databases.
2. Take and test a restorable backup of IoT Assistant tables and separately retain the existing S02 encryption/data-protection keyring. Record backup ownership, access controls, expiry and deletion process; backup deletion SLA is still OPEN.
3. Inspect legacy rows, holds and table/column/index definitions. If the SQL guard fails, prepare a reviewed mapping/conversion or quarantine plan; do not weaken the guard or delete records to make it pass.
4. Apply the reviewed transaction with the owning migration principal. Runtime principal needs only intended select/insert/update/delete permissions on Assistant tables, not general schema DDL or cross-service access. Verify indexes and schema before enabling release.
5. Retain existing JWT/managed datasource/protection configuration. Proposed new deployment key `Assistant:Chat:ReleaseEnabled` defaults false; enable only after schema, publication, auth and operational gates are accepted. Guest release is separate. Existing guest origins allow Authorization and now expose Location; do not use wildcard origins. No current environment file was read or changed.
6. Run the PostgreSQL scenarios below using synthetic data in a separately approved isolated database; then perform explicitly authorized runtime acceptance. Never test real paid providers without separate permission.

Rollback: stop S05 writers and workers, disable Chat release, take an incident backup. Keep added columns/tables while rolling application code back; dropping them destroys history/recovery evidence. If full restoration is required, restore the pre-migration backup into a separate verified database first and reconcile any post-migration data/holds before replacement. Restoring old DB data cannot undo provider charges. No destructive down migration is supplied or executed.

## PostgreSQL acceptance scenarios

Use two independent connections/hosts and a fake provider with controllable barriers. Capture SQL-visible rows and provider-call counters; memory locks are not acceptance evidence.

The [isolated PostgreSQL harness](../../04-testing/assistant-handoff-testing.md#nghiệm-thu-backend-trên-postgresql-cách-ly) verifies duplicate creation/admission, conflict and busy outcomes, terminal serialization and overdue recovery at the store boundary. The crash test kills a TestServer process inside synthetic provider generation, then checks recovery after the durable deadline and replay without another dispatch. The retention test checks the 30-day lifetime, immediate invisibility at expiry and purge with or without an internal hold. Deadline under a held lock, backlog, lost commit acknowledgement and legacy recovery remain [UNVERIFIED].

| Scenario | Required observation |
|---|---|
| Concurrent same Account/create key | one conversation; same locale replay200; changed locale409; different Account key independent |
| Concurrent same message key/payload | one user row and ledger; at most one owner of dispatch; replay202/200; changed payload409 |
| Two different keys, same revision | one admitted; other busy409 (takes priority over stale); no rejected user row |
| Lock admission for several seconds | durable deadline loses lock/connection wait time, never receives a fresh25s after wait |
| Cancel before dispatch, during provider, after terminal | zero dispatch before boundary; cooperative abort during; late answer cannot append; completed cancel unchanged |
| Block terminal until deadline crossing | conditional completion loses; no assistant row; interrupted outcome remains terminal |
| Kill host after durable admission / after dispatch / before commit | restart sweep marks overdue interrupted; no provider redispatch; unknown billing preserved |
| Two recovery workers plus completion/cancel | same conversation lock protocol; one terminal transition, correct revision, no duplicated message |
| Large overdue backlog/locked conversations | set-based batches commit progress; measure60s target; skip-locked contention does not claim guaranteed SLA |
| Lost commit acknowledgement/storage outage | caller retains key and queries; no automatic new ID/provider retry; safe dependency error |
| Absolute expiry/hold/purge | equality hidden in all APIs/replay;30days from creation unchanged by activity; hold retains internal data only; purge cascades |
| Restart history/cursors | stored transcript/ledger retained; persistent protection keyring decodes cursors; wrong Account/resource/limit rejected |
| Migration legacy/malformed schema | transaction aborts safely; no guessed owner, no credential copy, tested backup restoration |

## S06 / S08 handoff

S06 uses Account-owned history/ledger and conservative local context. Vehicles/reservations/search capabilities are conditional on read configuration and readiness; they are no longer universally false. Owner list/detail/search endpoints exist; positive availability authority and runtime integration remain incomplete. Draft stays false; G04/G05/G06 and G07 owner contracts remain open. No booking, hold or payment action exists in chat. See [owner decisions](service-dependencies-and-decisions.md).

S08 must use a dedicated abort/deadline-aware transport (normal shared10s timeout is shorter than25s work), or treat timeout as unknown and look up the same key. Do not globally increase auth API timeout. Preserve `authApi` argument6 extraHeaders / argument7 options.

On logout or Account switch: increment session/account generation, abort local requests, clear private transcript/history/draft/pending intent; reject every late response unless Account generation + conversation ID + client request ID still match. Aborting the socket alone does not cancel durable work; use the cancel endpoint while authorized when the UI explicitly cancels. Do not rely on G08 realtime revocation. On401, use UserService's existing refresh/reauth policy; only the same Account may look up the old request. Provider503 is not logout. Guest pending intent is memory-only, requires explicit continuation after login, never transcript import or mutation replay. Global off hides/closes launcher but must retain an authenticated recovery path. No S08 UI was changed.
