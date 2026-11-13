# SmartPark Assistant — P0 API theo phương án B

**Amendment 10/11/2026.** The current-chat contract supersedes historical Driver-only and conversation expiry rules in this document. Driver, Admin and Business/Owner each have one current chat per Account and role. Confirmed reset hides the old chat and makes it due for deletion after seven days. See [current chat and recovery](../ai/conversation-and-recovery.md#approval-and-scope).

| Metadata | Giá trị |
| --- | --- |
| Status | IN REVIEW — hướng B được người dùng xác nhận; wire/policy gaps bên dưới chưa final-approved |
| Contract version / date | 0.3.0 / 2026-11-02 |
| Owning service / author | IoTService / Codex hỗ trợ nhóm SmartPark |
| Jira design / implementation / verification | OPEN — chưa được cung cấp ticket/owner cụ thể |
| Requirements baseline | [SRS v1.1](../../01-requirements/srs/smartpark-srs-v1.1.md); không thay status của requirements |
| Implementation | R1 PARTIAL trên working tree AI-ChatBot, base 78336604fbe1adc3c3a685e0165a5253c3deca6c: JWT/actor/errors/capabilities kiểm chứng qua TestServer; chưa có production host/config lifecycle; R2–R4 chưa triển khai |
| Provenance | Chọn lọc schema/route/retry từ recovery a9b8c8ef3abe353044bfeafabc097c65639193b8; bỏ evidence cũ và auth A |
| Approval evidence | Người dùng xác nhận nhóm chọn B và đồng ý chuyển đổi trong chat 02/11/2026; [session](../../../ai/tasks/archive/chatbot-implementation-history.md#nhat-ky-1) ghi scope |
| Plan | [Kế hoạch chuyển đổi](../ai/luu-tru/design-history.md#tai-lieu-2) |

## Overview

S01 ngày 05/11/2026 bổ sung [target end-to-end v0.4.1](assistant-s01-end-to-end-api-design.md) cho guest, Driver tools/draft và configuration model riêng. Đây là overlay IN REVIEW: giữ P0 làm nguồn token/ledger/history và behavior demo hiện tại; không suy ra target đã được duyệt hay triển khai. Retention và các delta mới phải theo quyết định D01–D05 của S01 khi được duyệt.

Ngày 03/11/2026, người dùng duyệt host demo riêng để kiểm thử trước, chưa mở rộng actor hoặc doanh thu. Demo không thay contract Driver bên dưới: xem phụ lục demo cuối tài liệu và [hướng dẫn chạy](../ai/chatbot-scaffold.md).

Driver FAQ có nguồn, hội thoại thuộc Account và lookup sau timeout. IoT tự validate JWT do UserService cấp; không gọi current-actor mỗi request. Bảy endpoint dưới đây là target để review và triển khai từng phần; không phải cam kết đã chạy. Chi tiết kế thừa từ recovery vẫn PROPOSED nếu chưa có quyết định rõ; không dùng việc phục hồi tài liệu để phê duyệt policy.

| Traceability | Reference |
| --- | --- |
| FAQ / conversation / lỗi / quyền | [FR-AI-01/02/09/10](../../01-requirements/srs/modules/10-ai-assistance.md), [UC-AI-01](../../01-requirements/use-cases/uc-ai-01.md), [BR-AI](../../01-requirements/business-rules/10-ai-assistance.md) |
| Permission / ownership | [Permissions](../../01-requirements/srs/permissions.md), [Service boundaries](../../02-architecture/service-boundaries/ownership-and-events.md) |
| Retention | [File/media](../../01-requirements/srs/modules/09-file-media.md) |
| Implementation convention | [Shared coding skill](../../07-skills/SmartPark-Coding-Skill-v1.0.md), [guardrails](../../../ai/context/architecture-guardrails.md) |

## 1. Scope và quyết định

- Đợt này: Driver FAQ; HTTP JSON, Account-owned conversation, ledger/revision và sources.
- Không mở live booking/payment/search tools, Admin mutations hoặc thay Python recognition.
- UserService vẫn cấp/refresh token. IoT không truy vấn User DB, không tự refresh token.
- Quyết định B thay yêu cầu current-actor HTTP của recovery cho Assistant. Không đổi chính sách quyền của domain services hoặc sửa shared skill thành JWT-only cho toàn hệ thống.
- Request hợp lệ về chữ ký không chứng minh Account/session/role còn hiện hành. Việc chấp nhận token cũ đến hết hạn phải được ghi nhận ở B-01 trước nghiệm thu auth.

## 2. Token contract đã kiểm chứng trong source

Evidence ở baseline trên: AccessTokenService.cs, JwtKeyProvider.cs, ApiAuthenticationExtensions.cs, AccountRolePolicy.cs và SessionLifetime.cs thuộc UserService. Đây là mô tả source, không phải kiểm chứng deployment.

| Thuộc tính | Mapping/validation cho IoT |
| --- | --- |
| alg | Chỉ RS256; require signature + trusted RSA public key; không HS256/none/algorithm theo client |
| iss / aud | Đúng cấu hình do User owner cung cấp; không đoán giá trị hoặc lấy theo token |
| exp / nbf | Require exp; từ chối khi now >= exp hoặc now < nbf; clock skew 0 theo validator hiện tại |
| sub | UserId, Guid khác rỗng; không phải AccountId |
| account_id | AccountId, Guid khác rỗng; owner hội thoại |
| jti | AccessTokenId, GUID (source phát dạng N); không là owner vĩnh viễn |
| role | Source map role thành lowercase; Driver FAQ yêu cầu driver, role khác 403 |
| Lifetime | Source access lifetime 900 giây; không tự sửa TTL UserService |
| Key | Public key tin cậy RSA >=2048 bit; User giữ private key; không tham chiếu User Infrastructure để lấy key |

R1 đề xuất chuẩn JwtBearer tại API với MapInboundClaims=false, inward current-actor port do API triển khai từ principal đã xác minh. Application không đọc HttpContext/raw token. Fail closed với claim bắt buộc thiếu, invalid/empty GUID, ambiguous duplicate identity/role. Không lấy identity từ body/query/header tùy ý, không token logs/model context.

B-02: đề xuất public PEM do vận hành cấp qua config IoT, issuer/audience tương ứng; không tự tạo JWKS endpoint hoặc fetch key từ URL trong token. Cách phân phối/rotation/key overlap vẫn OPEN; test dùng RSA test keys, không đọc private key thật. Cấu hình thiếu/sai không có fallback key cho production và không được báo ready. Cụ thể startup fail-fast hay liveness-only unhealthy host cần chốt khi R1 thiết kế config.

Theo yêu cầu người dùng, các lựa chọn B-02 và tác động vận hành được trình bày trong [phiếu quyết định cho leader](../ai/luu-tru/design-history.md#tai-lieu-3). Phiếu đang PROPOSED, không tự cấp phép triển khai runtime. B-01 được người dùng xác nhận tiếp tục B sau khi giải thích độ trễ tối đa 15 phút; không hứa thu hồi tức thời.

## API Specification

## 4. Quy ước dùng chung

Tất cả route base /api/assistant yêu cầu Authorization: Bearer; role driver từ token đã xác minh. Scope own AccountId. POST JSON UTF-8, response JSON UTF-8. Unknown JSON properties ignored; known fields sai kiểu/null bị 400, không mass assign. Numeric string không thay integer. ID UUID khác rỗng, timestamps UTC ISO-8601; revision/sequence integer không âm trong miền an toàn JavaScript. GET không body; query chỉ nhận field đã liệt kê, không duplicate/empty values.

Đề xuất giới hạn recovery giữ để review: body 16 KiB, message 4000 UTF-16 code units sau trim; không silent truncate. Không gửi full history/system prompt/tool URL/provider config từ FE. Owner/role trong body không có authority và không tham gia fingerprint.

Private responses Cache-Control: no-store, server-generated X-Correlation-ID + traceId; 401 có WWW-Authenticate: Bearer. CORS theo origin cấu hình, expose Location/Retry-After/X-Correlation-ID. Success/error envelopes bên dưới; HTTP status chỉ ở response, không trong JSON.

### 4.1 Auth, ownership và thay đổi so với phương án A

1. Mỗi protected request xác minh JWT cục bộ. Không User HTTP lookup, không positive current-actor cache, không User DB access.
2. Sau validation, kiểm role driver. Missing/malformed identity claims -> 401 INVALID_TOKEN; valid identity nhưng role không được phép -> 403 ASSISTANT_FORBIDDEN.
3. Owner là AccountId. Cùng User khác Account không đọc chéo; đăng nhập lại cùng Account đọc retained history được. Missing/foreign/expired conversation đều 404, không metadata; Admin không có bypass tại bảy route này.
4. Token bị revoke/Account bị khóa/role đổi sau phát hành: local validation chưa biết, có thể chấp nhận đến exp. Không mô tả như đã deny ngay. UserService refresh vẫn theo policy owner; IoT không được làm yếu refresh.
5. B-03 đề xuất giữ terminal persistence rồi kiểm lại exp/identity/role claims của cùng credential trước delivery. Nếu hết hạn thì 401 không private data; kết quả đã lưu vẫn lookup bằng token mới cùng Account. Không recheck live state, không tự hủy provider khi Account thay đổi. Ordering/retention trong tác vụ dài chưa final-approved.
6. 503 AUTH_CONFIGURATION_UNAVAILABLE chỉ là trusted validation configuration không sẵn sàng nếu host vẫn phục vụ; không còn lỗi User HTTP dependency. Token signature sai/key không khớp trusted key -> 401. Provider 401/403 là MODEL_UNAVAILABLE của generation, không logout FE.
7. Exception integration theo UseExceptionHandler + IExceptionHandler; riêng auth challenge/forbid, binding và status-code producers dùng cùng formatter. Giữ response-started/cancellation/bodyless HEAD/204 đúng HTTP; không copy custom catch-all recovery.

#### 4.2 Schema response tái sử dụng

| Schema / field | Type / nullability | Ý nghĩa |
| --- | --- | --- |
| SuccessEnvelope | `{success:true,data:T,traceId:string}`; tất cả required | HTTP đọc/ghi resource thành công; không đồng nghĩa model đã trả lời được |
| ErrorEnvelope | `{success:false,code:string,message:string,errors:FieldError[],traceId:string}` | `errors` luôn array; không có field error dùng `[]`; message an toàn cho UI |
| FieldError | `{field:string,code:string,message:string}` | Tên field theo JSON path, không echo dữ liệu nhạy cảm |
| Conversation | `id:UUID, title:string, locale:enum, revision:int, createdAt:UTC, lastMessageAt:UTC|null, expiresAt:UTC` | Tất cả required; title server mặc định theo locale, tối đa 255; không dùng model tạo title phạm vi Driver FAQ |
| Page<T> | `items:T[], nextCursor:string|null` | Cursor signed/protected hoặc server-side state, bind AccountId/resource/query/snapshot; base64 đơn thuần không đủ; invalid/mismatch → 400; vẫn reauthorize mỗi GET |
| Message | `id:UUID, sequence:int, role:user|assistant, content:string, createdAt:UTC, clientMessageId:UUID, outcome:enum|null, sources:Source[], actions:[]` | User: outcome null, sources/actions rỗng; assistant dùng outcome phía dưới. Không trả SYSTEM/TOOL messages |
| Source | `articleId:string, version:string, locale:vi|en, title:string, excerpt:string, status:active|withdrawn|outdated` | Tất cả required; server tạo từ corpus snapshot, excerpt tối đa 1000 ký tự; status là annotation hiện hành khi đọc, không đổi answer/source gốc; không URL/HTML tùy ý |
| RequestResult | `clientMessageId:UUID, conversationId:UUID, state:running|completed|failed, revision:int, userMessageId:UUID, answer:Message|null, requestError:RequestError|null, createdAt:UTC, finishedAt:UTC|null` | Tất cả required; running: answer/error/finishedAt null; completed: answer và finishedAt có giá trị, error null; failed: answer null, error/finishedAt có giá trị |
| RequestError | `code:string, message:string, retryable:boolean` | Lỗi generation đã được ghi nhận, không chứa stack/raw provider body |

`Message.outcome` của assistant: `answered`, `clarification`, `handoff`, `unavailable`. Đây là **kết quả trả lời**, tách khỏi request state. `unavailable` là không có nguồn/capability phù hợp nên thông báo giới hạn; sự cố provider/retrieval kỹ thuật làm request `failed`. `answered` cho FAQ phải có ít nhất một nguồn đã kiểm chứng. Text plain, tối đa 8000 ký tự; tối đa 5 sources; phạm vi Driver FAQ `actions=[]`. Handoff chỉ hướng dẫn đã được duyệt, không báo đã tạo support ticket. Locale answer theo conversation; không có nguồn ngôn ngữ phù hợp thì hỏi rõ/thông báo giới hạn theo corpus được duyệt.

### 5. GET `/api/assistant/capabilities`

| Item | Contract |
| --- | --- |
| Availability / auth / scope | PROPOSED trên baseline mới; Bearer được IoT xác minh, role driver; capability dành cho actor; theo quy ước mục 4 |
| Content type / headers | Response JSON; Authorization; no-store |
| Request sample | `GET /api/assistant/capabilities` — No body; không có query |

```json
{"success":true,"data":{"enabled":true,"contractVersion":"0.3.0","locales":["vi","en"],"maxMessageLength":4000,"features":{"faq":true,"getMyVehicles":false,"searchParking":false,"readMyReservations":false,"readMySession":false,"readMyMoney":false,"prepareReservation":false,"createSupportTicket":false}},"traceId":"trace-demo-01"}
```

HTTP 200. Tất cả field required/non-null: enabled và feature values là boolean; contractVersion string; locales enum array; maxMessageLength integer. `enabled=false` vẫn trả 200 để FE biết trạng thái tắt, faq khi đó false. Không expose secrets/model/account quota. Config bật nhưng chưa đủ dependency/corpus cũng không báo capability sẵn sàng. Mỗi request thực thi vẫn kiểm tra quyền/readiness, capability không phải authorization grant.

Readiness chỉ bật FAQ khi storage, corpus đã công bố và provider được cấu hình/cho phép. Thiếu dependency không advertise FAQ; không tự publish draft. Đây là mục tiêu, chưa có evidence trên baseline mới.

Validation/errors: 400 query không hỗ trợ; 401/403/503 auth theo mục 10, 429 đọc quá giới hạn, 500 lỗi nội bộ. GET retry được theo backoff; không đổi state.

### 6. POST `/api/assistant/conversations`

| Item | Contract |
| --- | --- |
| Availability / auth / scope | PROPOSED trên baseline mới, readiness-gated; Bearer, Driver; tạo cho chính actor; theo mục 4 |
| Content type / headers | Request/response JSON; Authorization; response Location |

Request: `POST /api/assistant/conversations`

```json
{"clientConversationId":"11111111-1111-4111-8111-111111111111","locale":"vi"}
```

| Field | Location | Type | Required / nullable | Validation |
| --- | --- | --- | --- | --- |
| clientConversationId | body | UUID | Có / không null | Client sinh một lần cho mỗi New, giữ nguyên khi retry |
| locale | body | vi hoặc en | Có / không null | Không suy ra từ role/header |

Response: **201 Created**, `Location: /api/assistant/conversations/22222222-2222-4222-8222-222222222222`.

```json
{"success":true,"data":{"id":"22222222-2222-4222-8222-222222222222","title":"Hội thoại mới","locale":"vi","revision":0,"createdAt":"2026-10-27T03:00:00Z","lastMessageAt":null,"expiresAt":"2027-01-25T03:00:00Z"},"traceId":"trace-demo-02"}
```

Response schema: SuccessEnvelope<Conversation>, mục 4.2. Unique key `(ownerAccountId, clientConversationId)`, canonical input là locale hợp lệ; field ignored không tham gia fingerprint. Retry cùng key/cùng locale trả **200** cùng conversation hiện hành và Location như trên, không tạo thêm; cùng key/khác locale trả 409 `IDEMPOTENCY_CONFLICT`. Không hồi sinh record hết hạn; key cũ không được chủ động dùng lại, key gặp record đang bị ẩn/hết hạn trả 404. Sau purge không còn bảo đảm dedupe; FE dùng key mới cho New. Owner/auth/dedupe trước feature-off guard cho create mới; replay vẫn hợp lệ khi generation tắt.

Validation/errors: 400 malformed/locale/known-field null sai; 401/403 auth; 409 xung đột; 413 body; 429 rate limit; 503 cấu hình xác minh/assistant disabled/not configured/storage; 500 unexpected. Unknown JSON ignored. Timeout/5xx: gửi lại **cùng key** để xác định outcome. New không xóa hay hủy request của hội thoại trước.

### 7. GET `/api/assistant/conversations?limit=20&cursor=...`

| Item | Contract |
| --- | --- |
| Availability / auth / scope | PROPOSED trên baseline mới; Bearer, Driver; chỉ own-account còn được hiển thị |
| Content type / headers | Response JSON; Authorization; no-store |
| Request sample | `GET /api/assistant/conversations?limit=20` — No body |

| Field | Location | Type | Required / nullable | Validation/default |
| --- | --- | --- | --- | --- |
| limit | query | integer | Không / không null | 1–50; mặc định 20 |
| cursor | query | opaque string | Không / không null | Tối đa 2048 ký tự; bind actor và limit; không tự sửa |

HTTP 200, SuccessEnvelope<Page<Conversation>>; không có dữ liệu là thành công với array rỗng:

```json
{"success":true,"data":{"items":[],"nextCursor":null},"traceId":"trace-demo-03"}
```

Sắp xếp `activityAt DESC, id DESC`, với activityAt = lastMessageAt hoặc createdAt nếu chưa có tin nhắn. Trang đầu chốt snapshot thời gian, cursor giữ snapshot và mốc sort; filter event sau snapshot, không hứa snapshot isolation toàn phiên duyệt khi hội thoại tiếp tục đổi activity. FE dedupe theo ID và refresh trang đầu để thấy cập nhật mới. Không hỗ trợ search/filter/sort tự do.

Validation/errors: 400 query/cursor; 401/403; 429; 503 cấu hình xác minh/storage; 500. GET retry được; không reset retention. Khi assistant tắt vẫn cho đọc hội thoại hợp lệ để phục hồi lịch sử.

### 7.1 GET `/api/assistant/conversations/{conversationId}`

| Item | Contract |
| --- | --- |
| Availability / auth / scope | PROPOSED trên baseline mới; Bearer, Driver theo JWT hợp lệ; owner AccountId |
| Content type / headers | Response JSON UTF-8; Authorization; Cache-Control: no-store |
| Request sample | `GET /api/assistant/conversations/22222222-2222-4222-8222-222222222222` — No body/query |

| Field | Location | Type / required / nullable | Validation |
| --- | --- | --- | --- |
| conversationId | path | UUID / required / non-null | Existing, own AccountId, not expired; otherwise uniform 404 |

Response **200 OK**, SuccessEnvelope<Conversation> theo mục 4.2; ID/timestamp đều synthetic:

```json
{"success":true,"data":{"id":"22222222-2222-4222-8222-222222222222","title":"Hội thoại mới","locale":"vi","revision":0,"createdAt":"2026-10-27T03:00:00Z","lastMessageAt":null,"expiresAt":"2027-01-25T03:00:00Z"},"traceId":"trace-demo-detail"}
```

Validation/errors theo 10.1: 400 `VALIDATION_FAILED` (path/query); 401 `INVALID_TOKEN`; 403 `ASSISTANT_FORBIDDEN`; 404 `RESOURCE_NOT_FOUND` cho missing/not-owned/expired với cùng safe envelope; 429 `RATE_LIMITED`; 503 cấu hình xác minh/storage; 500 `INTERNAL_ERROR`. Client sửa input, reauthenticate, backoff hoặc retry GET theo loại lỗi. GET idempotent, không đổi revision/retention/lastMessageAt, không gọi provider; history/detail vẫn đọc được khi generation tắt hoặc provider chưa cấu hình, subject to JWT validation/ownership/retention/storage.

### 8. GET `/api/assistant/conversations/{conversationId}/messages?limit=50&cursor=...`

| Item | Contract |
| --- | --- |
| Availability / auth / scope | PROPOSED trên baseline mới; Bearer, Driver; own conversation; theo mục 4 |
| Content type / headers | Response JSON; Authorization; no-store |
| Request sample | `GET /api/assistant/conversations/22222222-2222-4222-8222-222222222222/messages?limit=50` — No body |

| Field | Location | Type | Required / nullable | Validation/default |
| --- | --- | --- | --- | --- |
| conversationId | path | UUID | Có / không null | Không có quyền/đã hết hạn → 404 |
| limit | query | integer | Không / không null | 1–100; mặc định 50 |
| cursor | query | opaque string | Không / không null | Tối đa 2048; bind actor, conversation, limit và upper sequence |

HTTP 200; object data có `conversation:Conversation`, `items:Message[]`, `nextCursor:string|null`, `activeRequestClientMessageId:UUID|null`, tất cả required.

```json
{"success":true,"data":{"conversation":{"id":"22222222-2222-4222-8222-222222222222","title":"Hội thoại mới","locale":"vi","revision":0,"createdAt":"2026-10-27T03:00:00Z","lastMessageAt":null,"expiresAt":"2027-01-25T03:00:00Z"},"items":[],"nextCursor":null,"activeRequestClientMessageId":null},"traceId":"trace-demo-04"}
```

Trang đầu lấy 50 message mới nhất, trả trong trang theo `sequence ASC`; nextCursor lấy tiếp các message cũ hơn, giữ upper sequence trang đầu. FE prepend và dedupe theo ID. Revision là hiện hành lúc đọc; transcript có thể cũ hơn khi phân trang. User message của request failed/running vẫn có trong history; FE lookup clientMessageId để render trạng thái. Không trả SYSTEM/TOOL/prompt/raw arguments. History/replay giữ nguyên answer, outcome và source/version lúc tạo; source nay withdrawn/outdated chỉ thêm `Source.status`, FE đánh dấu hướng dẫn lịch sử không còn hiện hành. Không viết lại answer thành unavailable, không gọi model lại. Security/legal redaction là ngoại lệ riêng có policy/audit do data owner quyết định; ordinary withdrawal không phải redaction. Câu hỏi mới chỉ retrieve nguồn ACTIVE tại snapshot admission.

Validation/errors: 400 path/query/cursor; 401/403; 404; 429; 503 dependency; 500. GET không thay đổi lastMessageAt; vẫn đọc được khi feature tắt, trừ tài nguyên đã hết hạn.

### 9. POST `/api/assistant/conversations/{conversationId}/messages`

| Item | Contract |
| --- | --- |
| Availability / auth / scope | PROPOSED trên baseline mới; generation readiness-gated; Bearer, Driver; own conversation; FAQ sau R3 |
| Content type / headers | Request/response JSON; Authorization; 202 có Location và Retry-After |

Request:

```json
{"clientMessageId":"33333333-3333-4333-8333-333333333333","expectedRevision":0,"message":"SmartPark hỗ trợ những gì?"}
```

| Field | Location | Type | Required / nullable | Validation |
| --- | --- | --- | --- | --- |
| conversationId | path | UUID | Có / không null | Own-account, chưa hết hạn |
| clientMessageId | body | UUID | Có / không null | Giữ nguyên khi retry cùng một lượt |
| expectedRevision | body | integer | Có / không null | >=0, bằng revision hiện tại khi nhận lượt mới |
| message | body | string | Có / không null | Sau trim còn 1–4000 UTF-16 code units; FE/BE thống nhất cách đếm; không silently truncate |

DTO không bind locale mới, history, arbitrary context, toolName hay action; unknown JSON ignored. Fingerprint từ DTO chuẩn hóa `(message đã trim, expectedRevision)`; đổi nội dung sau gửi phải tạo clientMessageId mới. Ignored fields không vào fingerprint/quyền. Conversation locale là server state.

Luồng lần đầu: validate/auth/owner/retention → dedupe → readiness/quota cho lượt mới → atomic revision/busy guard + commit user message/ledger `running` và effective config/corpus snapshot → bounded retrieval/model → conditional terminal commit → kiểm lại hạn token trước delivery theo đề xuất B-03 → **200** SuccessEnvelope<RequestResult>. Fixture sau giả định source/version đã Admin công bố, không xác nhận corpus P1 từ recovery còn draft đang active:

```json
{"success":true,"data":{"clientMessageId":"33333333-3333-4333-8333-333333333333","conversationId":"22222222-2222-4222-8222-222222222222","state":"completed","revision":2,"userMessageId":"44444444-4444-4444-8444-444444444444","answer":{"id":"55555555-5555-4555-8555-555555555555","sequence":2,"role":"assistant","content":"SmartPark hỗ trợ tìm hiểu và sử dụng các chức năng đỗ xe. Tôi có thể giải thích hướng dẫn đã được công bố.","createdAt":"2026-10-27T03:01:05Z","clientMessageId":"33333333-3333-4333-8333-333333333333","outcome":"answered","sources":[{"articleId":"smartpark-overview","version":"1.0","locale":"vi","title":"Giới thiệu SmartPark","excerpt":"Hướng dẫn sử dụng SmartPark.","status":"active"}],"actions":[]},"requestError":null,"createdAt":"2026-10-27T03:01:00Z","finishedAt":"2026-10-27T03:01:05Z"},"traceId":"trace-demo-05"}
```

Response dùng RequestResult và Message ở mục 4.2. `success=true` nghĩa đọc được trạng thái request; UI phải xét `data.state`, không hiển thị thành công chỉ từ envelope. Sự cố generation sau admission vẫn trả **200**, `state=failed`, `requestError` có giá trị, không chèn câu trả lời giả vào transcript. Ví dụ outcome lỗi được minh họa trong endpoint lookup dưới đây.

#### Validation/error và retry

| Case | HTTP / code | Client recovery / state |
| --- | --- | --- |
| Cùng key/cùng fingerprint, đã terminal | 200, RequestResult hiện hành | Không thêm user/assistant message, không gọi model lại; vẫn JWT validation/ownership/retention và source-status annotation |
| Cùng key/cùng fingerprint, đang chạy | 202, RequestResult running; `Retry-After: 2`; Location là endpoint lookup | Poll; không bắt đầu generation thứ hai |
| Cùng key/khác fingerprint | 409 `IDEMPOTENCY_CONFLICT` | Giữ request cũ; sửa ý định bằng lượt mới |
| Lượt mới khi có request running | 409 `CONVERSATION_BUSY` | Đọc history/lookup; không lưu lượt bị từ chối |
| Lượt mới, revision cũ | 409 `REVISION_CONFLICT` | Reload history; người dùng chọn gửi lại với key mới và revision mới |
| Input/path/body sai | 400 `VALIDATION_FAILED` | Field errors; không admission |
| Body quá lớn | 413 `PAYLOAD_TOO_LARGE` | Rút ngắn, không truncate tự động |
| Không auth/không đúng role/không own resource | 401/403/404 theo mục 10 | Không cung cấp dữ liệu hoặc tạo lượt |
| Giới hạn lượt mới/cost cap | 429 `RATE_LIMITED` hoặc `ASSISTANT_QUOTA_EXCEEDED` | Có Retry-After; không admission |
| Feature tắt/chưa cấu hình/auth hoặc storage unavailable | 503 theo mục 10 | Lookup cùng key nếu outcome mạng chưa rõ; không chuyển sang mock |
| Sự cố không dự kiến | 500 `INTERNAL_ERROR` | Lookup trước retry, không kết luận chưa lưu |

Với lượt mới sau dedupe/readiness/quota, nếu vừa running vừa stale revision tại admission decision thì ưu tiên 409 `CONVERSATION_BUSY`; khi không running mới xét `REVISION_CONFLICT`. Quyết định phải atomic theo PostgreSQL, không dựa trên snapshot RAM trước đó.

Unique scope `(ownerAccountId, conversationId, clientMessageId)`; unique running guard theo conversation; clientConversationId scope ở mục 6. Dedupe trước stale revision, busy guard, readiness và quota lượt mới; auth/owner/retention/read limiter luôn áp dụng. Cùng key/request replay kết quả đã lưu, giữ answer gốc với source-status annotation; feature-off chỉ chặn admission mới. ID không dùng lại cho intent mới; không có yêu cầu header Idempotency-Key chung toàn project.

### 10. GET `/api/assistant/conversations/{conversationId}/requests/{clientMessageId}`

| Item | Contract |
| --- | --- |
| Availability / auth / scope | PROPOSED trên baseline mới; Bearer, Driver; own conversation và own request |
| Content type / headers | Response JSON; Authorization; running có Retry-After: 2 |
| Request sample | `GET /api/assistant/conversations/22222222-2222-4222-8222-222222222222/requests/33333333-3333-4333-8333-333333333333` — No body/query |

Path fields bắt buộc UUID, không nullable. HTTP **200** SuccessEnvelope<RequestResult> cho cả running/completed/failed. Nếu generation đã timeout:

```json
{"success":true,"data":{"clientMessageId":"33333333-3333-4333-8333-333333333333","conversationId":"22222222-2222-4222-8222-222222222222","state":"failed","revision":2,"userMessageId":"44444444-4444-4444-8444-444444444444","answer":null,"requestError":{"code":"MODEL_TIMEOUT","message":"Chưa thể tạo câu trả lời trong thời gian cho phép.","retryable":true},"createdAt":"2026-10-27T03:01:00Z","finishedAt":"2026-10-27T03:01:25Z"},"traceId":"trace-demo-06"}
```

`retryable=true` nghĩa người dùng có thể yêu cầu **lượt mới sau terminal**; gửi lại key cũ chỉ nhận lại failure. Không tự động phát sinh lượt tốn phí từ GET. `revision` là revision hội thoại hiện hành tại lúc đọc, không phải số revision lịch sử cố định của request.

Validation/errors: 400 path/query; 401/403; 404 `RESOURCE_NOT_FOUND`; 429 giới hạn đọc; 503 cấu hình xác minh/storage; 500. Không có request/không có quyền cùng 404. Sau timeout POST, lookup 404 chưa chứng minh POST không thể đang tới server: client chỉ resend cùng key/payload để an toàn. FE poll 2s, backoff đến 10s khi lỗi/429 theo Retry-After, dừng sau 60s chờ chủ động; giữ ID cho lần mở lại, không tạo request khác vì chưa thấy kết quả.

#### 10.1 ErrorEnvelope theo hướng chuẩn SmartPark cho cả bảy endpoint

```json
{"success":false,"code":"VALIDATION_FAILED","message":"Dữ liệu gửi lên chưa hợp lệ.","errors":[{"field":"message","code":"MAX_LENGTH","message":"Tin nhắn tối đa 4000 ký tự theo quy ước contract."}],"traceId":"trace-demo-error"}
```

| HTTP | Machine code | Khi xảy ra / recovery |
| --- | --- | --- |
| 400 | VALIDATION_FAILED / INVALID_CURSOR | Binding, query không hỗ trợ, known field null sai schema; sửa request; unknown JSON ignored |
| 401 | INVALID_TOKEN | Thiếu/sai/hết hạn token hoặc thiếu claim bắt buộc; local JWT không phát hiện revocation; FE refresh bằng luồng hiện có hoặc yêu cầu đăng nhập; không refresh vô hạn |
| 403 | ASSISTANT_FORBIDDEN | Phiên hợp lệ nhưng role/quyền không được dùng; không retry như lỗi mạng |
| 404 | RESOURCE_NOT_FOUND | Không tồn tại/khác actor/hết hạn; không leak owner hoặc metadata |
| 405 | METHOD_NOT_ALLOWED (PROPOSED) | Method không được route hỗ trợ; giữ Allow header, sửa method; không đổi thành 400 |
| 409 | IDEMPOTENCY_CONFLICT / REVISION_CONFLICT / CONVERSATION_BUSY | Theo bảng endpoint POST |
| 413 | PAYLOAD_TOO_LARGE | Body >16 KiB |
| 415 | UNSUPPORTED_MEDIA_TYPE (PROPOSED) | POST không có content type JSON được hỗ trợ; sửa Content-Type, không đổi thành 400 |
| 429 | RATE_LIMITED / ASSISTANT_QUOTA_EXCEEDED | Retry-After số giây; lượt mới chưa admission |
| 503 | AUTH_CONFIGURATION_UNAVAILABLE / STORAGE_UNAVAILABLE / ASSISTANT_DISABLED / ASSISTANT_NOT_CONFIGURED | Không logout vì provider/storage outage; lookup/retry theo loại lỗi |
| 500 | INTERNAL_ERROR | Có traceId, không stack/raw exception; mutation lookup trước retry |

`RequestError.code` sau admission có allowlist riêng: `MODEL_TIMEOUT`, `MODEL_UNAVAILABLE`, `RETRIEVAL_UNAVAILABLE`, `INVALID_MODEL_OUTPUT`, `REQUEST_INTERRUPTED`, `INTERNAL_ERROR`. Invalid output chỉ retryable nếu lỗi kỹ thuật có thể phục hồi; lỗi guardrail không cho tự động lặp prompt. Kiểm tra token/quyền thất bại khi trả kết quả dùng HTTP 401/403/503, không nhét dữ liệu riêng vào error body.

## Retry and state outcomes

### 11. Lifecycle và concurrency

```text
Chưa nhận -> validate/auth/owner/dedupe/revision -> running -> completed
                                                     \-> failed
```

- Admission atomically ghi user message, ledger và tăng revision một lần. Terminal completed ghi một assistant message và tăng revision một lần; terminal failed không ghi assistant message nhưng vẫn tăng revision một lần để báo state thay đổi. User message có sequence 1, assistant thành công sequence 2; failed có thể không có assistant sequence. Sequence đơn điệu, không yêu cầu liên tục.
- `revision` là version trạng thái conversation, không phải message count/sequence. PostgreSQL unique constraints/indexes bảo vệ client keys, message sequence và một running request/conversation; expectedRevision bằng conditional update/transaction, terminal state compare-and-set. Không dùng in-memory check làm bảo đảm concurrency. R2 phải test concurrent same-key, hai tab, provider-timeout và hai recovery workers bằng PostgreSQL thật.
- Chỉ một running request mỗi conversation; nhiều conversation của cùng actor vẫn chịu rate/cost limit chung. Không giữ DB transaction khi chờ model. Trước terminal dùng điều kiện state/version; late response không thể đổi failed thành completed hoặc chèn thêm answer.
- Budget đề xuất: JWT validation không có User HTTP dependency; generation gồm retrieval/model tối đa 20s; tổng xử lý HTTP mục tiêu hard deadline 25s kể từ lúc nhận, tính cả persistence và kiểm hạn token trước delivery. Khi admission lưu deadline, không cộng lại budget khi retry. Đây là **giá trị cấu hình chờ duyệt/đo thực tế**, không là SLA latency đã đạt.
- Hierarchy bắt buộc kiểm chứng theo deployment: `FE timeout > proxy allowance > Assistant hard deadline > từng auth/retrieval/provider budget`. Đề xuất FE 30s, proxy 28s, Assistant 25s; generation tối đa 20s phải dùng remaining overall deadline để còn thời gian persistence/recheck, không cộng budget vượt 25s. Khi mất socket sau admission, công việc do host quản lý theo service deadline, không theo RequestAborted; không unmanaged fire-and-forget. Không queue/broker/streaming trong FAQ; 202 chỉ replay running. Con số là PROPOSED, chưa SLA đo được.
- Host crash/restart: request running quá deadline chuyển failed `REQUEST_INTERRUPTED` bằng recovery có điều kiện; không tự gọi provider lại. Startup sweep và periodic recovery/lookup phải giải phóng request mồ côi chậm nhất 60s sau deadline. Nếu storage hỏng không thể ghi terminal, API trả dependency error; không cam kết recovery hoàn tất trước khi storage phục hồi.
- Khi host đang sống nhưng provider về sau deadline: bỏ late result. Retry cùng ID không gọi lại provider. DB dedupe không bảo đảm provider chỉ tính phí một lần trong mọi crash/network scenario; đối soát usage thực tế, không quảng bá exactly-once billing.
- Lượt failed không đưa vào model context như cặp hội thoại đã thành công. User có thể thấy lịch sử lỗi và gửi lại có chủ đích với key mới/revision hiện hành. Không tự retry generation trong phạm vi Driver FAQ; chỉ retry GET có giới hạn trong cùng budget.

### 12. Persistence mapping và retention

Recovery có migration tại scripts/database/migrations/20261028_iot_assistant_p2b.sql; chưa phục hồi/apply tại baseline mới. R2 phải đối chiếu [schema IoT base](../../../scripts/database/microservices/06-iot-recognition-service-db.sql), review bảng/index/owner mapping và SQL rollback trước áp dụng. Legacy conversations thiếu Account mapping phải dừng migration; không suy owner từ UserId. Hold là internal state, không Driver mutation.

| Thành phần | Thông tin cần bổ sung/giữ |
| --- | --- |
| assistant_conversations | owner_account_id bắt buộc, user_id chỉ correlation, client_conversation_id unique theo owner_account_id, locale, revision, last_message_at, expires_at; không dùng access token/session làm owner, không lưu credential vào session_token |
| assistant_messages | sequence unique theo conversation, client_message_id/request reference, answer outcome, source IDs/version và metadata cần replay; content và role hiện có |
| assistant_requests (đề xuất mới) | Account/conversation/client key, fingerprint, state, deadline, timestamps, message references, safe failure code; unique scope mục 9; pin provider/model/configurationVersion/promptVersion/corpus/indexVersion, retrieved article IDs+versions và correlation; không secret/hidden reasoning |
| assistant_tool_runs | Giữ cho tool nghiệp vụ tương lai (ngoài R0–R5); payload tối thiểu/redacted, không tự lưu full HTTP headers/provider credentials |
| Retention/holds | Ánh xạ authorized holds của data owner vào migration/lifecycle; internal mapping cần review cùng R2 |

Conversation có message hết hạn tại `lastMessageAt + 90 ngày`; thời điểm bằng expiresAt đã hết hạn. Hội thoại rỗng đề xuất `createdAt + 90 ngày` để tránh lưu vô hạn; đây là policy bổ sung cần BA/leader duyệt. lastMessageAt chỉ đổi khi ghi message thật, không đổi do GET/replay/recovery lỗi. Failed lượt có user message đã admission vẫn tính mốc message đó.

Đến hạn, ordinary APIs không phục vụ conversation/request result kể cả cleanup chưa xong. Hold giữ evidence theo quyền, không tự cấp quyền đọc trong các endpoint Driver. Hold review mỗi 30 ngày và khi đóng case, không auto-release khi đến lịch review. Ledger, messages, sources và tool payload phải đi cùng lifecycle, không giữ nội dung vô hạn trong logs/backup mà không có chính sách. Trước release phải chốt purge/backup/provider-data handling và test retention bằng clock, không đợi giai đoạn tool nghiệp vụ tương lai.


## Sequence Diagram

```mermaid
sequenceDiagram
    participant FE
    participant API as IoT API + JWT
    participant APP as Application
    participant DB as IoT Persistence
    participant MODEL as Retrieval/model
    FE->>API: Bearer + message + stable ID/revision
    API->>API: RS256/issuer/audience/time/claims
    Note over API,APP: Token invalid -> 401; role denied -> 403; terminate
    API->>APP: IMediator + trusted actor
    APP->>DB: Owner/retention/dedupe/admission
    Note over APP,DB: Foreign resource -> 404; replay -> saved result; no model call
    APP->>MODEL: Allowed corpus + bounded history
    MODEL-->>APP: Typed answer or safe failure
    APP->>DB: Conditional terminal persistence
    Note over APP,API: B-03 proposed: recheck token expiry before delivery; no User lookup
    APP-->>API: RequestResult
    API-->>FE: Result or auth error without private data
```

## Open questions and deferred items

| ID | Gap / proposal | Affected part / owner | Approval |
| --- | --- | --- | --- |
| B-01 | Chấp nhận token cũ đến exp khi revoke/lock/role change; không immediate invalidation | R1 auth acceptance / lead + User owner | Người dùng xác nhận giữ B sau giải thích độ trễ tối đa 15 phút trong chat R1; deployment chưa verified |
| B-02 | Public PEM config, trusted issuer/audience, key rotation/overlap và restart behavior | R1 config/integration / User + IoT + ops | Chờ leader duyệt theo [phiếu quyết định](../ai/luu-tru/design-history.md#tai-lieu-3); chưa có config bridge/production host |
| B-03 | Terminal persist rồi expiry check trước delivery; lookup bằng phiên mới cùng Account; không cancel theo revocation live | R2 lifecycle / lead + security | PROPOSED |
| B-04 | Empty conversation expiry; purge/backup/holds/provider retention | R2 schema và release / data owner | 90 ngày từ last message có SRS; empty từ create còn PROPOSED |
| B-05 | Recovery wire details, 16 KiB/4000 input, budgets 25s/30s/60s, rate/cost limits | R1–R4 / FE + BE + QA + lead | IN REVIEW; không claim SLA; không invent rate defaults |
| B-06 | Provider/model/data sharing/budget/quality và Admin publication | R3/live acceptance / AI + BA/Admin | OPEN; P1 content approval không bằng publication |
| B-07 | Admin management scope/auth/secret/spend lifecycle | R5 riêng / lead + security | DEFERRED khỏi Driver contract; không coi local JWT Driver tự áp dụng cho Admin |
| B-08 | Remote freshness, Jira owners, hosting/origins/schema mapping | Trước integration tương ứng / team | OPEN; local baseline verified |

Không còn blocker “cần endpoint User current-actor” cho B. Những gap trên không cấm làm source/test độc lập theo contract đã có; không tự thông qua phần security/data policy chưa chốt. Release readiness cần evidence các gap có liên quan, không chỉ unit tests.

## Review checks và acceptance cần thực hiện

- [x] Pin baseline/recovery, traceability, phân biệt source/proposal/runtime.
- [x] Bỏ dependency introspection và lời hứa current-role/revocation tức thời khỏi Driver contract.
- [ ] Lead/User/FE xác nhận các gap tương ứng; Jira và implementation SHA/PR khi có.
- [ ] R1: HTTP JWT thật ký bằng key test, sai alg/key/issuer/audience, missing/duplicate/empty claims, exp equality/nbf, 401 challenge/403, safe envelope, DI.
- [ ] R2: cross-Account cùng User, phiên mới cùng Account, owner trên list/detail/history/lookup/replay, PostgreSQL concurrency/dedupe/recovery/retention.
- [ ] B tests chứng minh token stale có thể còn được chấp nhận; không dùng mock “revoke tức thời” làm acceptance.
- [ ] R3: sources/publication/injection/provider errors/cancel và policy/data/budget; không paid call tự động.
- [ ] R4: refresh/sessionGeneration, logout/account switch/New, timeout lookup, no HTML/tool execution, responsive/browser.
- [ ] Build/test trên baseline tích hợp; review medium độc lập theo session. Không kế thừa pass cũ.


## Phụ lục — API demo độc lập, được phép triển khai ngày 03/11/2026

Phạm vi: kiểm chứng HTTP, lỗi, trạng thái mẫu và gửi tin nhắn nhận câu trả lời mô phỏng trên giao diện demo. Không xác thực tài khoản giả, không mở quyền mới cho role, không thay các endpoint Driver đã đặc tả. Host chỉ chạy khi environment Demo và Assistant:Demo:Enabled=true. Hướng dẫn cấu hình/chạy ở [scaffold](../ai/chatbot-scaffold.md); evidence tại session.

| Method / route | Auth / quyền / phạm vi | Request | Response |
| --- | --- | --- | --- |
| GET /health/live | Anonymous, tiến trình demo | Không body/query | 204, không body |
| GET /health/ready | Anonymous, trạng thái host demo | Không body/query | 200 SuccessEnvelope, data={isDemo:true,readyFor:"demo-api-only",assistantEnabled:false} |
| GET /api/demo/assistant/capabilities | Anonymous, dữ liệu mẫu công khai; không dữ liệu Account | Không body; không hỗ trợ query | 200 SuccessEnvelope<DemoAssistantStatus> |

DemoAssistantStatus: isDemo boolean luôn true, notice string có nhãn dữ liệu demo, capabilities theo schema mục 5 với enabled/FAQ và mọi feature đều false. Không null; contractVersion 0.3.0, locales vi/en, maxMessageLength 4000. Các feature false nói về chức năng thật; endpoint trả lời mô phỏng tách riêng bên dưới.

```json
{"success":true,"data":{"isDemo":true,"notice":"Dữ liệu demo. Chưa kết nối tài khoản, hội thoại hoặc dịch vụ AI.","capabilities":{"enabled":false,"contractVersion":"0.3.0","locales":["vi","en"],"maxMessageLength":4000,"features":{"faq":false,"getMyVehicles":false,"searchParking":false,"readMyReservations":false,"readMySession":false,"readMyMoney":false,"prepareReservation":false,"createSupportTicket":false}}},"traceId":"server-generated"}
```

Headers: X-Assistant-Mode: demo, Cache-Control: no-store, X-Correlation-ID. Errors dùng ErrorEnvelope của mục 10.1: query capabilities không hỗ trợ => 400 VALIDATION_FAILED; route không map (bao gồm /api/assistant/*) => 404 RESOURCE_NOT_FOUND; method không hỗ trợ => 405 METHOD_NOT_ALLOWED và Allow; lỗi bất ngờ => 500 INTERNAL_ERROR. HEAD lỗi không body. GET không ghi state; retry được, không có provider spend. Không tuyên bố rate limit hoặc POST/body validation production qua demo này.

CORS lấy danh sách origin rõ ràng từ Assistant:Demo:AllowedOrigins, GET/POST, Content-Type, expose X-Correlation-ID/X-Assistant-Mode; mặc định không cho cross-origin và không credentials. Health chỉ cho biết tiến trình/fixture chạy, không xác nhận JWT, storage, corpus hay provider thật. Chạy demo độc lập không đồng nghĩa leader đã duyệt policy cấu hình/key rotation production.

### POST `/api/demo/assistant/messages` — gửi tin nhắn mô phỏng

| Item | Contract |
| --- | --- |
| Availability / auth / scope | Implemented in opt-in demo host only; anonymous, public fixtures; no Account, revenue, provider or storage |
| Request | Content-Type application/json (or +json), UTF-8; JSON object; query unsupported |
| Input | message: string required/non-null, trim then 1–4000 UTF-16 code units; unknown JSON ignored; body max 32 KiB including chunked requests |
| Response | 200 SuccessEnvelope, data={isDemo:true,notice:string,reply:string}; all required/non-null; plain text |
| Headers | no-store, X-Correlation-ID, X-Assistant-Mode: demo |

Request example:

```json
{"message":"Tôi muốn đặt chỗ đỗ xe"}
```

Response example:

```json
{"success":true,"data":{"isDemo":true,"notice":"Câu trả lời mô phỏng. Không phải kết quả từ AI hoặc dữ liệu thực tế.","reply":"Ví dụ mô phỏng: bạn có thể bắt đầu bằng việc chọn bãi và thời gian cần đỗ xe. Bản demo chưa kiểm tra chỗ trống hoặc tạo đặt chỗ; chưa có thao tác nào được thực hiện."},"traceId":"server-generated"}
```

Errors: 400 VALIDATION_FAILED for malformed JSON, invalid message/query; 413 PAYLOAD_TOO_LARGE above 32 KiB; 415 UNSUPPORTED_MEDIA_TYPE for non-JSON/non-UTF8; 500 INTERNAL_ERROR for unexpected generator failure. No fake success on exceptions. No persistence, idempotency ledger, provider charges or business mutation; manual retry resubmits the same question safely, but this is not a production write/replay guarantee. Request cancellation propagates; late UI results after New/unmount are discarded. No history or credentials are sent; local transcript is not model context. This demo does not grant any new role permission.

### Demo provider extension — 03/11/2026 (not production Driver authorization)

POST `/api/demo/assistant/messages` keeps `isDemo=true`, `notice`, `reply` and requires `mode` (`mock`, `provider` or `knowledge`), `sources` (array of `{articleId,version,title,excerpt}`); optional `provider`/`model` are null in mock and knowledge. Only server configuration selects mode/provider/model, not request data. Since04/11 the corpus is one Vietnamese smartpark-knowledge.md document split into stable-ID sections, with bilingual keyword/phrase retrieval (up to3). Only Nội dung is sent to OpenAI Responses or Gemini generateContent; editorial references/search aliases are excluded. A successful model answers in the question's language, Vietnamese by default; source excerpts remain original Vietnamese. Each new question reloads the bounded document. No retrieval match is a successful limitation response with empty sources and no upstream call. No error becomes mock success. Managed degraded knowledge mode is specified below and explicitly identifies original document guidance rather than AI generation.

Known generation errors keep HTTP503 and the ErrorEnvelope from10.1, updated04/11:

| Code | Cause / recovery |
| --- | --- |
| ASSISTANT_NOT_CONFIGURED | Missing/invalid local model configuration; administrator checks configuration, no automatic retry |
| RETRIEVAL_UNAVAILABLE | Knowledge file cannot be read/parsed; administrator checks canonical file and deployed reader version |
| MODEL_TIMEOUT | Backend20s deadline expired; user may manually try later |
| INVALID_MODEL_OUTPUT | Oversize/malformed/unsupported output or invalid source citations; no partial answer or mock fallback |
| MODEL_UNAVAILABLE | Network/busy/upstream rejection including provider401/403/429/5xx; sanitized static message, user checks provider configuration or waits as appropriate |

The public envelope does not include raw provider body, internal failure enum/status or exception details. TraceId remains an opaque correlation value. Frontend branches on codes, shows a bounded safe traceId for support and never clears the application session for these errors. Unknown codes stay generic. Local10/min admission rejection remains HTTP429 RATE_LIMITED. Direct provider mode still has one attempt; user-approved managed mode can switch enabled candidates and return document guidance as specified below. No exact-once provider-charge guarantee is implied by failure/timeout.

For out-of-scope questions or insufficient evidence,200 may carry a fixed FAQ-boundary reply with `sources: []`. This occurs before dispatch if retrieval finds nothing, or after one dispatch if the model returns a valid nonblank reply and explicit `sourceIds: []`. In the latter case the server discards the model's uncited reply, including any alleged business actions, and returns only the fixed localized scope message. Missing/null/wrong-type/unknown/duplicate citations and malformed JSON remain INVALID_MODEL_OUTPUT; accepting a no-answer does not accept unsupported facts. No additional model call/retry is made.

Example known error:

```json
{"success":false,"code":"RETRIEVAL_UNAVAILABLE","message":"The assistant knowledge could not be loaded. Contact an administrator.","errors":[],"traceId":"server-generated"}
```

Direct provider-mode bounds:10message POST/min globally,2active upstream requests, no queue or repeated attempt,20s backend deadline,30s FE deadline,2048output tokens,128KiB upstream response. Managed bounds below allow up to3external attempts per admitted message, not per-minute request equivalence. Demo body budget remains32KiB; production limits above remain separate proposals. Sources are server-owned excerpts from retrieved articles, not proof of semantic answer correctness. No conversation history, business tools, authenticated Driver storage or production publication is added. Readiness remains demo-api-only and does not test provider credentials. Full live-provider/corpus evaluation remains separate from offline tests.

### Admin settings extension — 03/11/2026

Traceability: explicit user approval to integrate Admin Settings, PostgreSQL IoT with encrypted credentials and local JWT option B for Admin. Applies only when Demo host opts into `Assistant:Management:Enabled=true` and `Assistant:Demo:Mode=managed`. Earlier anonymous demo behavior remains unchanged when disabled. This does not approve production startup/key-rotation policy or grant Driver/revenue permissions.

| Method / route | Authorization | Input / result |
| --- | --- | --- |
| GET `/api/assistant/admin/settings` | Signed JWT, exact `admin` role, valid Account actor/expiry | No query; 200 success envelope with ManagementView |
| POST `/api/assistant/admin/mutations` | Same; authorization also checked in Application | JSON UTF-8, no query, max32KiB; 200 success envelope with current ManagementView |

ManagementView contains `settings` (`revision`, `enabled`, `maintenanceMessage`, `providers`, `keys`), `secretsConfigured`, `runtimeReleaseApproved`. The latter describes configured managed runtime only, not production release approval or a successful provider probe. Providers contain id/model/enabled/priority. Keys contain id/provider/label/enabled/priority/createdAt/revokedAt; secret, ciphertext and credential version are never returned.

All mutations require nonempty UUID `mutationId`, integer `expectedRevision` between0 and9007199254740990, and `action`. Unknown properties ignored. Actions:

- `save-config`: enabled boolean, nonblank single-line maintenanceMessage max500, exactly two providers with ids gemini/openai, distinct integer priorities1–1000, model identifiers max128. Enabling requires an enabled usable key/provider.
- `save-key`: provider, label nonblank single-line max80, enabled boolean, priority1–1000; optional keyId updates existing nonrevoked slot. New key requires secret8–1024 with no whitespace/control characters. Null secret on update preserves credential; supplied secret rotates it. Maximum64 key metadata entries, oldest revoked metadata pruned when necessary.
- `revoke-key`: existing nonrevoked keyId; disables slot and removes its stored ciphertext transactionally.

Example metadata-only update (UUID/revision must come from the current settings):

```json
{"mutationId":"11111111-1111-4111-8111-111111111111","expectedRevision":2,"action":"save-key","keyId":"22222222-2222-4222-8222-222222222222","provider":"gemini","label":"Primary","enabled":true,"priority":1}
```

Writes lock the singleton state and atomically commit revision, protected credential and safe receipt. Same actor/mutationId and same input replays without another write, returning current settings; different input gives409 IDEMPOTENCY_CONFLICT. Stale revision gives409 REVISION_CONFLICT. UI reloads after conflicts or uncertain outcomes before another edit. No automatic retry with a new mutationId. Audit receipt contains actor/action/time/revision and fingerprint, not raw mutation or key.

Errors:400 VALIDATION_FAILED,401 UNAUTHORIZED,403 FORBIDDEN,409 as above,413 PAYLOAD_TOO_LARGE,415 UNSUPPORTED_MEDIA_TYPE,503 MANAGEMENT_UNAVAILABLE; unexpected errors use existing safe500 envelope. No secret in logging/errors. CORS explicitly allows Authorization for configured origins; browser sends bearer token, not credential cookies.

Managed mode uses saved config for `/api/demo/assistant/messages`. Disabled returns503 ASSISTANT_MAINTENANCE with the configured plain-text message. User explicitly approved backup-key/provider behavior on04/11. Enabled/unrevoked candidates are ordered by provider priority/ID then key priority/ID. Maximum3distinct candidates per message,7s per attempt,18s AI-chain budget and20s total including state/document I/O. The same key is not retried. Provider401/403/429 cool the selected key60s; network/timeout/408/500/502/503/504 cool that configured provider/model60s and skip other keys of that same model. Both cooldown tables are instance-local, revision-scoped, bounded to configured slots/providers and reset by settings refresh/restart. HTTP400/404/redirect, invalid local configuration/knowledge/output/citations and storage/decryption failures remain errors, not guard bypass/fallback. Global admission busy stops the chain and permits local document guidance. Caller cancellation aborts without failover or guidance.

Each new attempt re-reads enabled/revision and resolves the selected credential version; changed revision during credential resolution or between backups fails closed. Already admitted successful calls may finish after disable/revoke. Before document fallback enabled/revision is checked again under total deadline. If eligible candidates all fail/cool down or AI-chain budget ends,200 returns `mode=knowledge`, null provider/model, a static localized explanation and up to3 full current source section contents in `sources[].excerpt`, so policy conditions are not silently cut. Knowledge parsing retains file256KiB/section16000chars limits. Source panels open automatically and are explicitly labeled as document guidance, not an AI answer. English requests are told that original Vietnamese guidance is being shown; no fake translation. Empty retrieval gives scope guidance only. Missing/invalid document still errors. State/credential store failure and disabled/no eligible configured candidates are not masked. Each dispatch may be billable even if cancelled/timeout; maximum3attempts is a bound on dispatches, not an exactly-once cost guarantee. This remains anonymous local FAQ, not authority to create reservations or authenticated production conversation storage.

Review: two independent subagent reviews completed; JSON numeric strictness, UTF-8 binding and maintenance input mismatch fixed. Automated tests cover authorization/expiry, key metadata masking, selection/rotation/revocation and HTTP binding. PostgreSQL transaction races, restart decryption, browser managed runtime and live provider calls remain UNVERIFIED.
