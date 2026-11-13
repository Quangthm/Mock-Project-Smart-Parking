# AI Chatbot — Knowledge, FAQ và xuất bản nội dung

Từ 10/11/2026, [bản 0.4.0](p1/knowledge/smartpark-knowledge.md) là nguồn biên tập trong repo. [Bản 0.3.1](luu-tru/knowledge/smartpark-knowledge-old.md) được lưu để đối chiếu. Nguồn biên tập vẫn là **DRAFT** và cần manifest riêng khi công bố. Bản runtime local riêng đã được duyệt và dùng gọi Gemini thật theo [nhật ký triển khai](../../../ai/tasks/archive/chatbot-implementation-history.md). Thay đổi source không tự phát hành. Môi trường đích và từng phiên bản cần bằng chứng kiểm thử riêng. [Ma trận nghiệm thu](../../04-testing/assistant-handoff-testing.md).

## Sources and editorial decisions

**User-selected source, 07/11/2026:** `p1/knowledge/smartpark-knowledge.md` is now the single Vietnamese editing source for Demo and normal guest/authenticated FAQ. Normal hosting reads this Markdown directly with the shared parser; `public-faq.json` is a superseded draft retained for review, not a runtime fallback. Do not maintain a second English policy copy. English model output is generated from the Vietnamese approved section with `sourceLanguage=vi`; source citations retain locale `vi`. Without a model, an English request gets explicitly labelled original Vietnamese guidance.

The reader hashes the exact Markdown bytes and validates published/approved/public frontmatter plus the deployment publication manifest. Only `### Nội dung` enters generation/citations; aliases, metadata and `### Đối chiếu — không gửi đến AI` do not. Full sections up to6000 characters are retained rather than truncating conditions to the old1000-character S04 excerpt limit. Missing/malformed/oversized Markdown fails closed; JSON is never substituted.

| Topic | Canonical basis | Actual source reconciliation |
| --- | --- | --- |
| Overview / needs / roles | SRS AI and discovery | Intended product capability is distinguished from current FAQ-only guest capability |
| Vehicle categories | Discovery category-specific capacity | BookingFlow uses CAR/MOTORCYCLE. No truck, bus or EV charging claim |
| Search | Discovery FR-CAP-01, BR-AI-01 | FindParking → driverData → store.getLots uses local data; G01 remains open |
| Booking | BR-AI-03 / SRS AI | BookingFlow uses local lots, own vehicles API; no authoritative Driver create workflow. G03/G04/G06 remain open |
| Payment | BR-AI-04/05 | Matrix G07a–d: prepare route/DTO mismatch and auth/replay gaps. No payment success claim |
| Support | FR-BAS-10/11 | Asynchronous direction only; guest has no ticket tool, no invented contact or staffed-chat claim |
| Limits | BR-AI-01–05 | No personal data adapters, writes, draft/history or dynamic lookup in S04 |

OPEN cho bản nội dung mới/deployment đích: approval và disclosure wording; legal/business identity, address, hotline, partners, coverage areas, support channel/hours/SLA chưa được xác nhận thì không bổ sung. Local publication/provider evidence đã có không thay approval cho phạm vi mới. Source paths establish traceability, not automatic semantic correctness or Admin authority.

## Publication (manual Admin-controlled deployment)

The SRS permits a simple Admin/dev managed versioned corpus. No CMS, publication HTTP endpoint or new role is added. A deployment operator acts only on recorded Admin approval, with the approval record kept in the project's normal review process. Restrict filesystem write access to authorized deployment administrators; guest/Owner processes must never write these files. The manifest's role string is metadata, **not authentication**. Hash binding detects content changes after approval, not a malicious filesystem administrator.

After independent Admin review, prepare a deployment copy of `smartpark-knowledge.md` with frontmatter `publication_state: published`, `review_status: approved`, and `audience: [public, driver]`; increment version for subsequent content changes. Create `publication.json` in the same directory (metadata only, no duplicated FAQ content):

```json
{"sha256":"REPLACE_WITH_SHA256_OF_EXACT_MARKDOWN_BYTES","version":"0.3.0","approvedBy":"APPROVED_ADMIN_RECORD_REFERENCE","role":"admin","publishedAt":"REPLACE_WITH_ACTUAL_APPROVED_UTC_TIME","effectiveTo":null}
```

This example is not approval. The loader requires matching digest/version, public audience, approved review state, valid effective_from/effective_to bounds, consistent published_at when specified, Admin publication metadata, nonfuture publication time, unexpired effectiveTo and valid bounded Markdown sections with unique IDs. Missing/malformed/draft/withdrawn/changed content is unavailable. Publish complete files atomically; intermediate mismatches fail closed. Withdrawal: remove manifest or use a nonpublished state; admitted requests recheck before delivery. There is no database migration for S04.

## Setup for separately authorized runtime acceptance

Normal executable uses AssistantGuestHost outside Demo; Demo remains explicitly gated. Existing managed S02 configuration supplies IoT database, encryption and Admin JWT settings. Do not copy credentials to this document. New configuration keys (deployment configuration, not changes to current runtime):

- `Assistant:Guest:ReleaseEnabled`: false by default. True authorizes public admission, still AND global settings and publication readiness.
- `Assistant:Guest:KnowledgeDirectory`: absolute deployment directory containing smartpark-knowledge.md and the separately approved publication.json.
- `Assistant:Guest:AllowedOrigins`: explicit HTTP(S) origins, no wildcard/credentials.

Map environment variables with double underscores using the existing configuration convention. Existing S02 SQL/setup is still separately reviewed, not applied here. No container or actual settings have been changed.

HTTP path: GuestEndpoints → IMediator → GuestHandlers → public snapshot + managed store → ManagedDemoGenerator (S02 routing/cooldown/budgets) → per-request public snapshot adapter → existing bounded provider HTTP adapter. No eligible configured model gives labelled public document guidance. Invalid model output is not retried. Provider network/recoverable failure falls back through S02, only to the same approved snapshot. Global off/revision/publication changes suppress delivery.

Free text is classified locally. A small set of complete public questions about booking lead time, duration, cancellation timing and general overstay can use fixed questions before the broad number, live-data and personal-context checks. The match requires an approved article and a strict input shape. Other input passes the existing restrictions before topic and paraphrase matching, then ranks approved Markdown sections by normalized titles, aliases and content. When lexical ranking finds no candidate, a two- to four-word alias may recover one insertion, deletion or adjacent transposition in a word of at least five characters. The other words must match, the source must be unique, and the alias must pass the same restriction checks. Ambiguous matches can return clarification.

On 2026-11-08 the user approved sending screened public FAQ questions to Gemini alongside approved article content. Selected public intents now use a fixed backend question in Guest and Driver FAQ, including distinct questions for voluntary pass cancellation and lot closure. No user-provided number or phrase enters these fixed questions. For other intents, Guest Gemini can still use the original question after checking length, syntax, vocabulary and personal-context cues. Otherwise the provider receives the server-owned topic title. The provider payload excludes transcripts, identifiers and account records. Guest requests are independent and have no durable history or automatic retry.

The corpus evaluation in `PublicKnowledgeTests` checks 21 fixed Vietnamese/English examples. It covers typos, synonyms, ambiguity, private/live requests and near matches that must abstain. Separate handler cases verify `locale=auto`, citations and the source-language fallback. A synthetic Gemini test verifies that `vehcile types` selects the vehicle guide while dispatching the server title. These fixtures measure retrieval and request construction. Provider answer quality requires a separate evaluation.

Rate limits are per **process**: 10 POST/min/socket IP, two concurrent POST total; status 60/min/IP, Retry-After 60. Caller X-Forwarded-For cannot select a partition. Before deployment behind a proxy or multiple replicas, review trusted proxy configuration and edge/global limits; current code does not claim distributed limits. Body 32 KiB, input 1–4000 trimmed UTF-16 units, output 8000, complete source section ≤6000, one source per topical answer; request timeout20s, S02 attempts ≤3,7s each,18s AI chain. Status/message failures never expose maintenance text, provider/model, credential or draft source.

S05: no guest durable data to migrate; Account retention/egress/delegation keep their approved boundaries. S06 reads use owner contracts, never local store fallback; positive availability is still incomplete. S08 uses S01 §§2–3 plus latest UI amendments: Guest sends locale=auto, explicit vi/en remains supported by API; no automatic POST retry; login only hands intent to composition, not mutation. Global off blocks admission but protected Driver history/recovery must remain accessible. FE owner handles presentation; source display follows the latest accepted UI, not the old S04 mockup.
