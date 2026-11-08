# SmartPark Assistant — S01 end-to-end API Design

**Knowledge amendment — 2026-11-07, user selected existing Markdown:** normal FAQ and Demo use `docs/03-design/ai/p1/knowledge/smartpark-knowledge.md` as the single Vietnamese content source. No `public-faq.json` fallback or separate English policy editing. DTO shapes stay unchanged; source version follows Markdown metadata, `sources[].locale` is the actual source language (`vi`), not the requested answer language. Each source excerpt is the complete validated section up to6000 characters, preserving conditions; existing answer limit8000 remains. English generation receives `answerLanguage=en`, `sourceLanguage=vi`; document fallback explicitly labels original Vietnamese content. Only section content/title enters providers/citations; metadata/aliases/editorial references are excluded. Exact-byte Markdown hash/version and public/published/approved metadata plus Admin publication manifest are required. Invalid/draft/edited/withdrawn content remains unavailable; existing revision/publication recheck, history/source annotations and no-private-egress rules remain. Source selection is approved; production publication is not implied.

| Metadata | Value |
| --- | --- |
| Status | IN REVIEW; sản phẩm đã xác nhận, wire/security/retention mới PROPOSED; chưa được duyệt triển khai toàn bộ |
| Contract version / date | 0.4.1 / 2026-11-05 — post-develop re-survey |
| Owning service / author | IoT Assistant; Operations/User/Facility/Payment giữ authority / Codex |
| Jira design / implementation / verification | OPEN — chưa có Jira hoặc người phụ trách cụ thể |
| Requirements baseline | [SRS v1.1](../../01-requirements/srs/smartpark-srs-v1.1.md) |
| Implementation | NOT IMPLEMENTED target; [source matrix](../ai/assistant-s01-api-matrix.md) tại HEAD 3d3def763870f66f8da71386357a66f776fa61e5 + staged Assistant restoration; initial survey 78336604 superseded for current coverage |
| Approval evidence | Product intent: chat 01a1a774-7912-7db2-8d85-d1e29936c351, user xác nhận cách hoạt động và yêu cầu kế hoạch; S01 không phê duyệt các lựa chọn D01–D05 thay user |

## Overview

Guest hỏi FAQ; Driver đọc dữ liệu được phép và chuẩn bị/sửa bản nháp đặt xe. UI thường xác nhận chính xác dữ liệu; domain backend kiểm tra lại, tạo reservation và chuyển checkout. Không có Assistant tool tạo đơn, giữ chỗ, charge/refund, sửa xe hoặc thao tác gate.

| Traceability | Reference |
| --- | --- |
| FAQ/intent/search/read/draft/confirmation/error | [FR-AI-01–11, FR-BAS-10/11](../../01-requirements/srs/modules/10-ai-assistance.md), [BR-AI-01–05](../../01-requirements/business-rules/10-ai-assistance.md) |
| Use cases | [UC-AI-01](../../01-requirements/use-cases/uc-ai-01.md), [UC-AI-02](../../01-requirements/use-cases/uc-ai-02.md), [UC-AI-03](../../01-requirements/use-cases/uc-ai-03.md), [UC-SEARCH-01](../../01-requirements/use-cases/uc-search-01.md) |
| Permissions/ownership | [Permission matrix](../../01-requirements/srs/permissions.md), [service boundaries](../../02-architecture/service-boundaries/ownership-and-events.md) |
| Existing contract / approved boundaries | [P0 v0.3.0](iot-assistant-p0-api-design.md), [guardrails](../../../ai/context/architecture-guardrails.md), [delivery plan](../ai/design-and-development-plan.md) |

S01 là **overlay** cho target, không sửa ngầm demo v0.3.0 hoặc xác nhận endpoint đã deploy. P0 conversation routes, ownership, envelope, paging, request ledger và error taxonomy được kế thừa trừ delta nêu rõ dưới đây. D02 retention thay các con số retention đề xuất cũ nếu được duyệt. Không đổi schema DB ở S01.

**Đối soát develop 2026-11-05:** [ADR authorization đã duyệt](../../02-architecture/architecture-decisions/authorization-freshness-and-current-access.md) và [operation matrix](../../01-requirements/srs/authorization-operation-classification.md) quyết định freshness, thay yêu cầu current-session/current-access chung của tài liệu cũ. `TOKEN` là mặc định cho authenticated business reads/quotes, kèm local ownership/scope/domain checks; UserService giữ kiểm tra authoritative local. Create reservation là `REQUEST`: durable pending admission, Account-current authorization bất đồng bộ qua Saga, không phải synchronous freshness lookup hoặc exact browser-session continuity. Đây là hướng đã duyệt, không chứng minh runtime đã triển khai; delegation và chi tiết wire/Saga thuộc D01/D05 còn OPEN.

**S04 scoped approval — 2026-11-05:** In the S04 chat, user approved the explicitly presented §§2–3 routes/payloads, body/input/output/abuse/time limits and D02/D03 **guest only** (“Ổn nhất thì áp dụng vào thôi”). No Account/history, delegation or domain API approval is implied. Corpus publication requires separate Admin approval. Implementation uses a conservative D03 subset: only a server-owned FAQ topic and approved public excerpt, never raw guest text. Guest retention is stateless/no durable transcript or ledger. Setup/publication and current limitations: [S04 handoff](../ai/knowledge-and-faq.md). §2 status is limited to 60 reads/min/socket IP; §3 limits are per process, not distributed guarantees. No candidate uses approved document guidance; publication/readiness failures remain 503. D02/D03 rows below remain PROPOSED for their other scopes.

**S05 scoped approval — 2026-11-05:** After explicit decision questions the user delegated selection of the recommended S05 options. Approved §§4–6/P0 conversation and ledger wire, B-03 expiry-before-delivery and B-05 bounds; D02 Account absolute30days from creation with immediate hiding/daily purge/internal holds; D03 Account conservative subset only server FAQ topic+published excerpt, local context≤6 completed turns/12000 characters, no raw text/history egress. D01 delegation and D05 domain work remain OPEN. B-02 runtime/key rotation, backup deletion SLA, broader D03 text egress and corpus publication remain unapproved. Implementation/evidence and wire discrepancy (`errors:[]` inherited from P0/shared IoT versus object/null in §1) are recorded in [S05 handoff](../ai/conversation-and-recovery.md). Existing PROPOSED labels below remain applicable outside this explicitly approved S05 subset.

## API Specification

**S06 scoped approval — 2026-11-05:** User delegated selection after explicit D01-S06/D05-S06-G03 questions. Selected bounded request-memory Bearer delegation and minimal Operations own-read projection, without new schema/dependencies or owner writes. Vehicles preserve User membership; reservations omit unavailable vehicleId/siteName/termsVersion/paymentOrderId rather than fabricate them. Exact reduced DTO, paging, read grammar, disabled defaults and evidence: [S06 contract](assistant-s06-read-integration.md), [handoff](../ai/business-data-integration.md). This supersedes §§13–14 rich DTO only for the approved S06 slice. G01 remains missing/search false; other D01/D05 scopes, G04–G07, runtime trust and PostgreSQL remain OPEN/UNVERIFIED.

### 0. Revalidation v0.4.1 / freshness mapping

v0.4.1 cập nhật evidence/acceptance sau develop, không triển khai route mới hoặc đổi runtime. Các sửa TOKEN/REQUEST từ session merge được giữ; không nhận là phát hiện mới của lần này. G07 nay có source order/checkout nhưng route/DTO/auth chưa tương thích; G08 generic realtime còn thiếu counterpart. Chi tiết và source links tại matrix §6.

| Operation sections | Freshness / invocation origin | Scope and implementation boundary |
| --- | --- | --- |
| §2 status, §3 guest, §11 search | PUBLIC | Chỉ dữ liệu public; search target chưa có |
| §4–7 capabilities/conversations/message/cancel/draft, §13–15 own reads/quote, §17 lookup | TOKEN | Driver + Account/resource scope, chấp nhận stale claims tới expiry; không tự gọi Saga cho read |
| §8–10 Admin settings/discovery/test | TOKEN | Admin, platform scope; local JWT như hiện tại, role không bypass secret handling |
| §12 own vehicles | TOKEN + User authoritative local checks | Giữ membership/current-access logic User đang có, không đổi ownership sang body AccountId |
| §16 normal create reservation | REQUEST | Durable pending + Account-current Saga authorization; không exact initiating-session guarantee |
| §18 customer checkout/status | TOKEN initial default | Chỉ khi API có auth thật và ownership; hiện implementation chưa đáp ứng |
| §18 service-origin order/payment/callback | SERVICE | Trusted service/provider identity + business reference; không giả user actor, trust contract còn OPEN |

Numeric retry defaults/CQRS authorization model đã duyệt trong ADR §§6/9; D05 chỉ chốt mapping/override có lý do, không hỏi lại quyết định toàn cục. Runtime Payment dev mock identity không đáp ứng TOKEN này. Classify side effects theo use case chứ không HTTP verb: status GET có reconciliation nên không đưa vào read tool allowlist.

### 1. Quy ước chung và schema

IoT JSON UTF-8, `Cache-Control: no-store`; thành công `{success:true,data,traceId}`, lỗi `{success:false,code,message,errors,traceId}`. HTTP status nằm ở transport. `errors` là object field→string[] hoặc null. Ví dụ lỗi:

```json
{"success":false,"code":"REVISION_CONFLICT","message":"Hội thoại đã thay đổi. Hãy tải lại.","errors":null,"traceId":"s01-example"}
```

UUID không rỗng; timestamps ISO-8601 có offset, response UTC; số revision 0..9007199254740991. `message` trim 1–4000 UTF-16 units; text trả tối đa 8000; unknown JSON ignored, tuyệt đối không mass-assign. Field required/null quy định ở từng schema; omitted optional giữ nguyên, null chỉ được phép khi nói rõ. Client không chọn actor, owner, tool, giá, trạng thái thật hoặc lịch sử model. Server không trả SYSTEM/TOOL/raw prompt/secret/provider/model/debug cho khách.

P0 `Conversation`, `Message`, `RequestResult`, `Source`, `Page` và pagination giữ nguyên: [schema và endpoints](iot-assistant-p0-api-design.md#api-specification). S01 bổ sung:

| Schema / field | Type / nullability / meaning |
| --- | --- |
| Message.mode | required enum `ai`, `knowledge`, `system`; knowledge là trích tài liệu có nhãn, system là thông báo xác định/typed domain result; không gọi là AI generated |
| Message.cards | required array, tối đa 20; discriminated union bên dưới; không chứa arbitrary HTML/URL |
| Message.actions | required array, tối đa 5, enum action bên dưới; thay `actions=[]` FAQ-only của P0 |
| Message.sources | giữ P0 source/version/status; FAQ answered cần source; domain cards không giả thành FAQ citation; system/login/clarification có thể [] |
| RequestResult.state | P0 `running/completed/failed` + `cancelled`; terminal không tự chạy lại. `success=true` nghĩa lấy được ledger, không đồng nghĩa booking thành công |
| RequestResult.requestId | UUID server cấp, required; correlation với clientMessageId, không thay idempotency key |
| Draft | `id:UUID`, `revision:int`, `siteId:UUID|null`, `vehicleId:UUID|null`, `vehicleType:CAR|MOTORCYCLE|null`, `startAt/endAt:ISO|null`, `timeZone:string`, `missingFields:string[]`, `quote:Quote|null`, `expiresAt:ISO`, `state:collecting|ready|stale|handed_off`; tất cả required |
| Quote | `id:UUID`, `version:string`, `expiresAt:ISO`, `amountVnd:integer>=0`, `currency:"VND"`, `termsVersion:string`, `availabilityAsOf:ISO`; required, không null; nguồn Operations phối hợp Payment, **không hold** |
| Action | `{type:"login"}` hoặc `{type:"view_reservation",reservationId:UUID}` hoặc `{type:"review_draft",draftId:UUID,draftRevision:int}` hoặc `{type:"open_booking"}`; backend allowlist, frontend route resolver |
| Card | `{type:"reservation",reservation:ReservationSummary}` / `{type:"parking",parking:ParkingSummary}` / `{type:"draft",draft:Draft}` / `{type:"vehicles",items:VehicleSummary[]}`; fields theo phần domain bên dưới |

Card dữ liệu nghiệp vụ được render trực tiếp từ kết quả API đã kiểm quyền. Text AI không được khẳng định giá/chỗ/trạng thái trái card. Không gửi toàn bộ kết quả riêng tư cho model chỉ để model viết lại. History card là snapshot cũ có `asOf`, mở detail phải đọc mới và kiểm quyền; không coi history replay là trạng thái hiện tại.

### 2. GET `/api/assistant/status` — public minimal status (S04 APPROVED)

| Item | Contract |
| --- | --- |
| Auth / scope | Anonymous, platform-public only; không nhận account ID |
| Content / headers | JSON response; no-store; không có body/query |
| Response | 200, các field dưới đây required/non-null |

```json
{"success":true,"data":{"enabled":true,"guestFaq":true,"locales":["vi","en"],"contractVersion":"0.4.1"},"traceId":"s01-status"}
```

`enabled` là global UI switch AND release gate; `guestFaq` thêm readiness/published corpus. Không công khai key/model/quota/health detail/maintenance text do Admin nhập tự do. Switch off → cả hai false, FE đóng chat/hide launcher, ngừng request mới; status failure → FE không đoán enabled. Kiểm lại khi mở chat/focus; PROPOSED poll tối đa mỗi 30 giây khi trang visible. Server rechecks từng admission và trước delivery nên stale status không cấp quyền. GET 400 query, 429 rate limit, 503 storage/status unavailable, 500 unexpected; retry đọc backoff, không thay state.

### 3. POST `/api/assistant/guest/messages` — guest FAQ (S04 APPROVED)

**2026-11-08:** User approved the screened public FAQ question as additional Gemini input. This updates the S04 provider payload described above. OpenAI and Driver FAQ use the server-owned topic title. For parking overstay, both use a fixed server-owned question with the approved payment article. The API request/response shape stays the same. See the S04 handoff for screening and fallback.

Web gửi `locale=auto`. Backend nhận diện Việt/Anh theo từng câu và trả `replyLocale` ở chế độ này. Giá trị `vi` và `en` vẫn được hỗ trợ. Driver cũng nhận diện ngôn ngữ theo từng tin nhắn; locale hội thoại là mặc định khi câu quá ngắn hoặc không rõ. Ngôn ngữ nguồn Markdown giữ theo nội dung gốc.

| Item | Contract |
| --- | --- |
| Auth / scope | Anonymous, approved public knowledge only; không history account/tool domain |
| Headers / content | JSON; no-store; max body 32 KiB; không cần Bearer |
| Fields | `requestId:UUID`, `message:string`, `locale:vi|en|auto` required/non-null; không nhận transcript/draft/account |

```json
{"requestId":"11111111-1111-4111-8111-111111111111","message":"Ngày mai tôi có lịch đặt không?","locale":"vi"}
```

200 sample không có nguồn tương ứng, do đó là system clarification chứ không giả FAQ grounded:

```json
{"success":true,"data":{"requestId":"11111111-1111-4111-8111-111111111111","mode":"system","outcome":"handoff","content":"Đăng nhập để xem lịch đặt của bạn.","sources":[],"cards":[],"actions":[{"type":"login"}]},"traceId":"s01-guest"}
```

FAQ grounded dùng `mode=ai|knowledge`, `outcome=answered`, content có nguồn P0; no-match dùng clarification. UI giữ pending intent trong memory, không token/PII trong redirect query. Sau login hỏi người dùng tiếp tục; không tự submit command, không chuyển guest transcript vào account history.

Guest không có server durable ledger/idempotency: requestId chỉ correlation, timeout là unknown inference result; **không tự retry POST**. Người dùng thử lại có thể tốn một inference khác. D02 đề xuất transcript chỉ memory của tab, mất khi reload; không đặt guest cookie định danh dài hạn. Abort dừng hợp tác HTTP/provider, không đảm bảo nhà cung cấp dừng tính phí.

Errors: 400 validation/JSON; 413/415 transport; 429 `RATE_LIMITED` + Retry-After; 503 `ASSISTANT_DISABLED`/`DEPENDENCY_UNAVAILABLE`; 502 `INVALID_MODEL_OUTPUT`; 504 `REQUEST_TIMEOUT`; 500. Không trả “không có lịch” khi chưa login. Tối đa 3 candidate, 7s/attempt, 18s chain trong 20s total, reuse failover đã có; không retry invalid output/knowledge/guardrails. Rate đề xuất 10 requests/min/IP + concurrency 2 global như baseline admission; operational tuning cần S09, không tuyên bố SLA.

### 4. GET `/api/assistant/capabilities` — Driver (existing stub; target PROPOSED)

Auth: P0 RS256/Driver, no body/query, JSON/no-store. Kế thừa fields/errors mục 5 P0, contractVersion đổi `0.4.1`; mỗi boolean chỉ true khi dependency và authorization tương ứng đã có. Không cho `prepareReservation=true` khi chỉ có localStorage. Không quảng cáo readMyMoney/readMySession/support nếu chưa triển khai. 401 token hết hạn, 403 sai role, 503 cấu hình/store; GET retry an toàn. Quyền domain vẫn kiểm mỗi call, capability không là grant. Source controller là stub, demo host không MapControllers: S05 phải expose authenticated production host/routes, không coi file controller là URL đang chạy.

### 5. Account conversations, messages, lookup và context (PROPOSED)

| Method / URL | Request / response / auth / validation / retry |
| --- | --- |
| POST `/api/assistant/conversations` | P0 §6 nguyên vẹn: `{clientConversationId,locale}`, 201 new/200 replay, own Account, 409 đổi payload; retention theo D02 |
| GET `/api/assistant/conversations` | P0 §7: limit default20/max50, bound opaque cursor, activityAt DESC/id DESC, 200 Page<Conversation> |
| GET `/api/assistant/conversations/{id}` | P0 §7.1: 200 Conversation; expired/missing/not-owned uniform404 |
| GET `/api/assistant/conversations/{id}/messages` | P0 §8: limit default50/max100, cursor, sequence ASC within page; 200 transcript + active request; không gọi provider |
| POST `/api/assistant/conversations/{id}/messages` | P0 §9 + schema delta §1; `{clientMessageId,expectedRevision,message}`. 200 terminal/202 running, Location tới lookup + Retry-After:2; unknown outcome lookup cùng key |
| GET `/api/assistant/conversations/{id}/requests/{clientMessageId}` | P0 §10 + state cancelled/requestId; 200 RequestResult, uniform404; không restart inference |

Tất cả Bearer Driver, Account-owned, JSON/no-store, dùng toàn bộ trường/null/errors cụ thể của P0; không tạo bộ route history thứ hai. Các ví dụ P0 là synthetic, expiresAt phải tính từ D02 thay vì chép ngày cũ. RequestId server và clientMessageId phân biệt rõ trong S05.

Context server giữ last selected site/vehicle, draft revision và pending clarification; chỉ cập nhật từ input đã validate/kết quả tool authorized. “Bãi thứ hai” gắn với result set đã lưu của hội thoại, không global list; hết hạn thì refresh/clarify. “Ngày mai” dùng clock server + timezone đã xác nhận (default đề xuất Asia/Ho_Chi_Minh); trả date/time rõ ràng trước review. Không đoán điểm đi từ IP, xin vị trí hoặc nhập địa điểm; geocoding/provider location riêng cần approved adapter, chưa có thì hỏi cụ thể.

Một request running/conversation; admission tăng conversation revision cho user message, terminal tăng khi append assistant outcome; draft revision chỉ tăng khi draft thay đổi. Dedupe check trước expectedRevision như P0: cùng clientMessageId + canonical payload replay, khác payload409; stale revision409; request mới khi busy409. Refresh/re-auth dùng **cùng Account** lookup, không lặp lời gửi mới. Logout/switch Account xóa UI/history/pending intent/draft cache và hủy request tại client; late response bị loại bằng account generation + conversation/request ID. Server lookup vẫn auth+ownership.

Token hết hạn trước delivery: 401 không gửi private result; ledger kết thúc vẫn được lưu nếu đã xử lý, refresh qua UserService rồi lookup cùng key. Domain access mất hiệu lực phải fail closed ở domain; không dựa B để vượt quyền. Tắt global sau admission: cancel cooperatively, không deliver reply mới; giữ request outcome/history để recovery khi authorized; không xóa dữ liệu đã lưu.

### 6. POST `/api/assistant/conversations/{id}/requests/{clientMessageId}/cancel` (PROPOSED)

**Transport/revocation integration sau merge:** reuse authApi generation guard nhưng giữ `extraHeaders` ở argument6, `options` ở argument7 như Assistant Admin hiện tại. Shared request timeout10s ngắn hơn FAQ budget20s: S05 chọn transport hỗ trợ abort/deadline phù hợp hoặc 202+lookup, không tăng timeout mọi API auth. Assistant401 không tự chứng minh UserService session đã bị thu hồi; refresh/reauth qua UserService rồi lookup cùng key. `/hubs/access`/AccessRevoked chưa có server counterpart trong source, nên S08 không dựa realtime để bảo đảm clear private state. Reuse AppContext sign-out/account change guard; hub/event alignment là G08, không phải chuyển Driver sang owner-only `/hubs/operators`.

| Item | Contract |
| --- | --- |
| Auth / scope | Driver, own Account/conversation/request |
| Content / fields | JSON `{}`; path UUID required; no query; unknown fields ignored; no-store |
| Response | 200 same RequestResult schema: running→cancelled, terminal→unchanged; cancel repeated idempotent |
| Errors | 400 invalid path/body, 401/403, uniform404, 429, 503 storage, 500 |

Không bắt buộc expectedRevision vì cancel nhắm chính xác request đã admission; cancellation và completion cạnh tranh, terminal đầu tiên thắng. cancelled không append late answer/draft, requestError null/answer null, finishedAt có giá trị; revision chỉ tăng nếu có transcript change, không tạo business write. Provider có thể vẫn tính phí. Timeout cancel → GET lookup; không suy ra rollback reservation (Assistant không tạo reservation).

### 7. GET `/api/assistant/conversations/{id}/drafts/{draftId}` (PROPOSED)

Driver own Account; UUID path, no body/query; 200 SuccessEnvelope<Draft> theo §1. 400/401/403/404/429/503/500 chung. Read retry an toàn; không tự quote lại, không hold. Hết hạn hoặc khác owner404. Đổi site/vehicle/time qua message mới cập nhật draft revision và xóa quote cũ; bất kỳ giá/terms/availability thay đổi buộc review lại. `review_draft` action chứa đúng draftRevision; FE đọc latest, mismatch hiển thị bản mới để xác nhận, không âm thầm submit.

Draft handoff là dữ liệu nhập sẵn cho normal form, không proof giá/quyền/hold. Normal form lấy quote từ Operations và gọi create bằng token người dùng sau nút xác nhận. “Ừ/đồng ý/đặt luôn” trong chat chỉ mở review. S08 thêm navigation resolver typed actions vào dashboard; chưa có route detail thật thì action không được quảng cáo hoạt động.

### 8. Admin configuration v2 (D04 approved; IoT)

S02 đã triển khai §§8–10 theo D04, kiểm thử offline. Xem [bàn giao S02](../../../ai/tasks/archive/chatbot-implementation-history.md#nhat-ky-6) cho migration và giới hạn kiểm chứng.

Giữ `/api/assistant/admin/settings` GET và `/mutations` POST đã có, không làm breaking replacement ngay. S02 thêm `contractVersion`, `configurations` trong ManagementSettings; migrate từng key cũ → configuration cùng ID/provider/label/enabled/priority, model copy từ provider hiện tại, giữ encrypted credential/version/revokedAt; provider.model cũ chỉ compatibility, không còn quyết định routing mới. Không reset global toggle hoặc đổi credentials. Client cũ mutation thiếu model chỉ dùng giá trị hiện hữu; không overwrite model mỗi configuration từ provider.model. Version cũ không biểu đạt được edit mới → 409 `CLIENT_UPGRADE_REQUIRED` thay mất dữ liệu. SQL review trước, chưa apply DB.

| Field | Type / rule |
| --- | --- |
| providers | required array đúng `gemini`,`openai`; mỗi item `{id,enabled:boolean,priority:int}`; priority 1..2 unique |
| configurations | array `{id:UUID,provider,label,model,enabled,priority,credentialVersion:UUID,createdAt,revokedAt:ISO|null}`; không secret; label1..80, model1..128 opaque adapter-supported ID; priority1..N unique trong provider, revoked không candidate |
| model | per configuration; không hardcode tên model ví dụ thành model đang có. Discovery ID canonical theo adapter, không cho URL/path tùy ý |
| revision | existing monotonically increasing safe integer; reorder atomic entire affected list; stale revision409 |

POST mutations request ví dụ extension (secret chỉ nhập mới/rotate, không echo):

```json
{"mutationId":"11111111-1111-4111-8111-111111111111","expectedRevision":7,"action":"set-configuration","keyId":"22222222-2222-4222-8222-222222222222","model":"synthetic-chat-model","label":"Primary","enabled":false,"priority":1}
```

`synthetic-chat-model` minh họa schema, không tên model có thể gọi. Fields mutation required: mutationId, expectedRevision, action. Các field khác required theo action: add-configuration(provider,label,model,secret,priority; mặc định disabled); set-configuration(keyId,label,model,enabled,priority); rotate-key(keyId,secret); revoke-key(keyId); reorder-providers(providerIds[]); reorder-configurations(provider,configurationIds[]); set-enabled(enabled). Không cho null để xóa secret/model. Reorder đủ các ID active thuộc scope, không duplicate/foreign ID; không tự bật. Existing actions giữ contract cũ và migration compatibility ở trên. Unknown action400.

GET no body/query →200 ManagementView; POST→200 ManagementView mới, không chứa secret; `settings.revision` tăng một lần. Cùng mutationId/canonical payload replay không mutation thêm; khác payload409 `IDEMPOTENCY_CONFLICT`, stale409 `REVISION_CONFLICT`. Auth Admin verified JWT B hiện tại, platform scope, không tự nới role. Errors400/401/403/413/415/429/503/500 theo P0 management; trước đáp ứng/check credential re-read revision/revoked status. Fresh Admin revocation vượt B là D01 riêng, không âm thầm đổi auth.

Routing: provider.priority ASC rồi configuration.priority ASC, không LLM chọn key; disabled/revoked bỏ qua. Giữ tối đa3 candidate và budgets guest §3. Cooldown key/credentialVersion cho key lỗi; model failure theo provider+model (không khóa model khác); state revision/config change invalidate health thích hợp, late failure không poison config mới. Admin test không auto-enable/reorder/reset runtime circuit. Knowledge fallback chỉ approved static content, không thay live business API thất bại.

### 9. POST `/api/assistant/admin/models/discover` (D04 approved)

Gemini lấy danh mục theo credential và giữ các model Gemini 3 Flash, Flash-Lite và Pro dạng text hỗ trợ `generateContent`. Admin có thể chọn trong danh sách hoặc nhập ID. Khi thêm cấu hình Gemini hoặc đổi model, server kiểm tra ID trong danh mục của đúng credential trước khi ghi. ID không hợp lệ hoặc không có trong danh mục trả `MODEL_NOT_AVAILABLE`; lỗi tải danh mục trong bước xác minh trả `MODEL_CATALOG_UNAVAILABLE`. Cả hai từ chối thay đổi. Sửa metadata khi giữ nguyên model không cần gọi Gemini. Danh mục không xác nhận giá hoặc khả năng sinh phản hồi thực tế.

Admin verified JWT, platform; JSON/no-store, body≤32KiB. Hai union input:

```json
{"provider":"gemini","secret":"SYNTHETIC-NEW-KEY","cursor":null}
```

Hoặc `{configurationId:UUID,expectedRevision:int,cursor:string|null}`. Không trộn secret/provider với saved ID. Secret mới transient chỉ gọi provider adapter, không lưu/log/cache secret; không đi query string. Cursor opaque≤2048, bind credential/config version/provider/filter; TTL5 phút, không cho model/provider URL tự chọn. Admin frontend giữ secret memory trong form; close/unmount xóa.

200 `{success:true,data:{items:[{id:"synthetic-chat-model",displayName:"Synthetic chat model",supportsChat:true}],nextCursor:null,asOf:"2026-11-05T03:00:00Z"},traceId:"s01-models"}`. items≤100/page, sort id ASC, lọc compatibility adapter; nextCursor nullable, mọi field khác required. Với OpenAI model list không chứng minh generation/tool/schema compatibility; adapter allowlist/capability mapping cần S02 test và official docs tại lúc triển khai. Cache saved credentials tối đa5 phút keyed credential version; unsaved không persist/cache nội dung credential.

400 input, 401/403 local auth,404 missing/revoked config,409 revision/cursor mismatch,429 local/upstream rate limit,502 `PROVIDER_REJECTED` hoặc invalid response,503 config unavailable,504 timeout. Không gửi raw upstream body. Timeout10s, không tự retry; reload rõ ràng, không tự chọn model hoặc save. Loading/empty/failure UI khác nhau; discovery success không nghĩa generation success.

### 10. POST `/api/assistant/admin/configurations/{id}/test` (D04 approved)

Admin JWT/platform, saved config only; JSON `{expectedRevision:7,testRequestId:UUID}` required/non-null. Mẫu dùng UUID như mutationId ở trên. Cho test disabled config để kiểm trước enable; revoked từ chối404. Đúng model/key/version tại revision đó; nếu thay trong lúc gọi, result ghi tested revision và `current:false`, không gắn nhãn config mới đã pass.

200 SuccessEnvelope<{testRequestId,configurationId,testedRevision,model,outcome,latencyMs,checkedAt,current,code}>; `outcome=passed|failed|unknown`, code nullable safe enum, latencyMs>=0, timestamps UTC, fields required. Model chỉ được trả Admin. Fixed synthetic prompt “Reply OK”, tối đa16 output tokens nếu adapter hỗ trợ, không gửi knowledge/customer data. passed chỉ khi response hợp lệ; unknown cho network/timeout không biết provider xử lý chưa.

Errors trước dispatch:400/401/403/404/409/429/503. Provider rejection sau dispatch →200 outcome failed/code, timeout→200 unknown, không biến thành passed. Repeated same testRequestId replay result/202 running, payload khác409; lưu dedupe1h đề xuất trong D04. Deadline25s cho test, tối đa5s để ghi kết quả, client chờ35s riêng cho endpoint này. Receipt chưa có kết quả giữ trạng thái running trong 35s, sau đó trả unknown với thời lượng ước tính từ deadline. Mỗi Admin/config≤3 tests/min, không failover/retry; test có thể phát sinh phí và UI nói rõ trước thao tác. Không tự sửa config/health. S02 test fake HTTP; live test phải do người dùng thao tác hoặc cho phép cụ thể.

## Domain integration contracts — PROPOSED owner backlog, không phải API đã có

Mọi route ở phần này cần D05 owner review. S06/S07 chỉ dùng khi implement, quyền và real integration đã kiểm chứng. Authenticated reads/writes dùng actor từ JWT đã validate và freshness theo operation matrix: `TOKEN` mặc định, create reservation `REQUEST`; owner vẫn kiểm resource/scope/domain, UserService giữ authoritative local checks. Không nhận actor từ body. D01 quyết định service delegation; không tin `X-User-Id`. Content JSON, private no-store, unknown fields ignored, error convention đề xuất như §1; adapter map `errorCode` của Payment hiện hữu, không sửa Payment envelope toàn cục trong S01.

### 11. GET `/api/parking-search` — Operations

Public discovery chỉ dữ liệu công khai, no body; query required lat[-90,90],lon[-180,180],vehicleType CAR|MOTORCYCLE; optional radiusKm default5/max50, startAt/endAt phải cùng có và end>start, timeZone default Asia/Ho_Chi_Minh, availableOnly defaultfalse, lotType enum outdoor|basement|multi_storey, limit default20/max50,cursor≤2048. Các bound mới là PROPOSED API protection, không business policy. Không tự xin GPS qua backend.

Sample `GET /api/parking-search?lat=10.77&lon=106.70&vehicleType=CAR&limit=20` →200 `{success:true,data:{items:[],nextCursor:null,asOf:"2026-11-05T03:00:00Z"},traceId:"s01-search"}`. ParkingSummary required `{siteId:UUID,name,address,latitude,longitude,distanceMeters:number>=0,availability:{kind:current|interval,availableCount:int|null,asOf:ISO},price:{kind:tariff|estimate,amountVnd:int,billingUnit:string}|null}`. Null availability/price là unknown, không zero/free; không interval thì không estimated total. Sort Haversine distance ASC/siteId ASC, cursor bind filters/snapshot, all options pageable; no LLM rerank. Radius expansion phải người dùng chọn hoặc UI nêu rõ, không âm thầm đổi.

400 validation,429,503 dependency unavailable,504 timeout,500. Empty200 khác outage; partial fields null có nhãn thiếu dữ liệu, không cho filter “available” coi unknown là true. GET retry tối đa1 trong deadline, cancel propagated; không hold.

### 12. GET `/api/vehicles` — User (existing; adapter reuse)

Bearer Driver/current access, no body/query; existing200 `{success:true,data:[{id,rawPlate,canonicalPlate,vehicleType,imageReference}]}`; field shape tại `FE/src/lib/vehiclesApi.ts`, source scope tại matrix. Assistant map sang VehicleSummary `{id,vehicleType,displayLabel}` required; displayLabel chỉ gửi authorized UI, không model nếu chứa plate. Không tạo endpoint Assistant sửa xe; action thêm xe đi màn thường. 401/403/5xx không thành empty. Owner phải chốt Account/User semantics G02, không filter từ client userId.

### 13. GET `/api/me/reservations` — Operations

Bearer Driver, `TOKEN` freshness + local Account ownership/scope, own authorized records; no body; query from/to ISO offset optional pair, to>from, limit20/max50,cursor≤2048,status optional owner enum. Interval filter **overlap** `startAt < to && endAt > from`; “ngày mai” convert local midnight boundaries sang UTC, không equals start date. Sort startAt ASC/id ASC, cursor bind account+filter. Sample `GET /api/me/reservations?from=2026-11-05T17:00:00Z&to=2026-11-06T17:00:00Z&limit=20` →200 Page<ReservationSummary>, empty items[] hợp lệ.

ReservationSummary required `{id:UUID,siteId:UUID,siteName:string,vehicle:VehicleSummary,startAt:ISO,endAt:ISO,status:string,asOf:ISO}`; status lấy owner state mapping, lạ→UI “chưa hỗ trợ trạng thái”, không đoán confirmed. Enum mapping cụ thể **OPEN G03** trước implementation; chỉ booking owner có quyền chốt. 400/401/403/429/503/504/500; GET retry bounded, không cache private across account.

### 14. GET `/api/me/reservations/{id}` — Operations

Auth/scope như list; UUID path, no body/query; 200 `{success:true,data:{reservation:ReservationSummary,paymentOrderId:UUID|null,termsVersion:string,asOf:ISO},traceId}`; paymentOrderId chỉ khi actual linked order và caller được phép. Missing/not-owned404 giống nhau; ngoài ra400/401/403/429/503/504/500. GET không mutation; UI action gọi resolver rồi reload detail, không tự mở provider URL từ model.

### 15. POST `/api/reservation-quotes` — Operations orchestrates Payment

Bearer Driver, `TOKEN` freshness + local ownership/scope/domain checks; JSON required/non-null `{siteId:UUID,vehicleId:UUID,startAt:ISO,endAt:ISO,timeZone:string}`, optional requestedSlotId UUID (không có nghĩa giữ slot). Sample:

```json
{"siteId":"11111111-1111-4111-8111-111111111111","vehicleId":"22222222-2222-4222-8222-222222222222","startAt":"2026-11-06T05:00:00Z","endAt":"2026-11-06T06:00:00Z","timeZone":"Asia/Ho_Chi_Minh"}
```

200 SuccessEnvelope<Quote>. Quote id/version opaque server-issued bound to current actor+canonical inputs; server knows inputs even if response projection small. expiresAt do owner policy quyết định, `now>=expiresAt` expired; amountVnd từ Payment, no invented validity minutes/booking horizon. Check vehicle ownership/category/site/interval/effective policy; changed params produce new quote, no hold. Errors400 validation,401/403,404 invisible vehicle/site,409 `UNAVAILABLE`/`POLICY_CONFLICT`,503/504 dependency,500. Retry read-like estimate may yield new quote; UI replace only matching draft revision. Never auto create on quote success.

### 16. POST `/api/me/reservations` — normal UI only, Operations

Bearer Driver được validate tại ingress; `REQUEST` Account-current theo ADR, không yêu cầu exact browser-session continuity sau admission. JSON `{quoteId:UUID,quoteVersion:string,termsVersion:string}`, required `Idempotency-Key:UUID`. UI exact review and confirm; server resolves authoritative quote payload, no model-provided price/account/state. New admission trả202 sau khi atomically lưu durable pending intent + outbox, với operationId và Location result lookup; không trả final success trước authorization. Saga lấy Account-current authorization bất đồng bộ, chỉ finalizes khi accepted `Allowed` và các prerequisite bắt buộc đều đạt. Chỉ preparatory/speculative effects được owner duyệt riêng mới được chạy song song; chưa suy ra capacity hold được phép. Pending replay trả202 cùng operationId; completed replay có thể trả200 với actual result `{success:true,data:{reservationId:UUID,status:string,paymentOrderId:UUID|null,operationId:UUID},traceId}`. Pending response wire, correlation/attempt fencing, retry/deadline, reconciliation/compensation và accepted post-ALLOW race phải được owner chốt ở D05/G06 theo ADR trước implementation. Reservation accepted is not payment successful.

400/401/403;404 inaccessible quote;409 `QUOTE_EXPIRED`/`TERMS_CHANGED`/`CAPACITY_CHANGED`/`IDEMPOTENCY_CONFLICT`;429;503/504 unknown outcome possible;500. Changed quote/terms needs new displayed review then new confirmation. Same Account+key+payload →same accepted result; changed payload409. Concurrent claims cannot double allocate. Lookup even when current feature off, subject auth; retention/dedupe duration for business writes **OPEN G06**, cannot infer from chat retention. Until owner implements durable semantics, no write retry and feature stays false.

### 17. GET `/api/me/reservation-requests/{idempotencyKey}` — Operations

Bearer Driver own scope; UUID path/no body/query;200 `{success:true,data:{state:"pending",operationId:UUID,reservationId:null,paymentOrderId:null,error:null},traceId}`. state pending|completed|failed; completed has reservationId, failed error safe code/message, paymentOrderId nullable during provisioning.404 not seen/not owned;401/403/429/503/504/500. GET retry bounded. Timeout POST first query same key;404 alone during racing request is not proof no commit—replay **same key** only under approved G06 contract. Never new key automatically.

### 18. Checkout and result — reuse Payment only after G07

**Source mới, chưa phải contract tích hợp đạt:** Operations đã có `/api/checkout/pricing`, prepare reservation/session và proxy payos/cash/demo/status; đây là luồng Operator/gate, không thay quote/create reservation tương lai của Driver. Payment có `POST /api/v1/payments/orders`, cash/demo-pay; adapter Operations lại gọi `/api/v1/payments/orders/prepare`. Request fields và response amount cũng khác (matrix G07a). Không sửa URL đơn lẻ hoặc lấy giá FE/model để lấp gap. Tách Driver booking payment và checkout chuyến đã gửi xe.

Payment `Program` hiện DevBearer mô phỏng, ngoài Development chưa concrete authentication scheme; direct user API cần TOKEN đúng issuer/audience/signature và owner binding, service-origin cần trusted SERVICE contract. Operations prepare có operator/site guard nhưng order proxies chỉ forward HTTP; `/internal` key không bảo vệ `/api/checkout`. Create order đang nhận Amount/AccountId từ body và cash đang ghi paid: giữ ngoài Assistant, G07b phải chốt authority trước dùng.

Acceptance mới G07a–d: integration test đúng cả route/DTO và authoritative price; fake token/foreign order bị từ chối; same key+same payload replay stored result, changed payload409; source/status failures giữ distinction401/403/409/429 thay map hết503. Checkout replay phải trả gateway result đã lưu, không QR mẫu. Không coi status GET là pure read; unknown mutation query same operation, không tạo key mới.

Normal UI `POST /api/v1/payments/orders/{orderId}/checkout`, `Idempotency-Key`, body `{paymentMethod:"PAYOS",returnUrl,cancelUrl}`; GET `/api/v1/payments/transactions/{transactionId}/status`. Existing DTOs are `CheckoutResponse`/`PaymentStatusResponse` in `FE/src/lib/paymentApi.ts`; response success/data; current errors use errorCode. Preserve existing contract until Payment owner separately fixes scope/replay/order integration. No Assistant mutation/read tool calls these handlers: status GET can reconcile/write and needs owner review.

Checkout requires owned actual payment order from create/detail, exact terms displayed; never ask model to invent order ID. Pending/unknown never paid, provider redirect success never authoritative settlement, payment success never means Operations fulfillment already updated. Existing mock replay or missing owner authorization blocks live handoff, not masked by Assistant. Unknown checkout outcome query same order/attempt, no new charge; missing outcome-by-order route is G07 owner work. Cancellation/refund/security requests navigate ordinary screens only.

## Retry and state outcomes

Chat cancel, logout, global Assistant off chỉ dừng chat/delivery; **không** tự hủy reservation Saga đã admission qua form thường. Expiry hoặc revoke riêng browser session sau admission không tự invalidate Account-current REQUEST. Lookup của UI vẫn cần token hợp lệ của owner. Workflow tiếp tục/deny/compensate theo state và accepted authorization outcome, không theo sự tồn tại của cửa sổ chat. Numeric authorization rechecks kế thừa ADR (tối đa3 evaluations/90s, attempt fencing); business mutation retry giữ operation ID, không dùng authorization attempt ID làm key tạo đơn.

| Case | Caller-visible outcome |
| --- | --- |
| Empty search/bookings/vehicles |200 items[] only from successful owner API; dependency error never empty |
| Late reply / cancel / logout / new account |No UI append into other context; server terminal CAS/ownership and UI generation guard; no financial rollback claim |
| Edit draft after quote/review |Increment draft revision, invalidate quote/confirmation; fresh owner checks |
| Expired JWT while generating |401 delivery; same-account refresh + request lookup; no duplicate POST under new client ID |
| Duplicate click/create timeout |Disable repeated UI submit; owner idempotency/result lookup; never new key automatically |
| Global off |No new admissions, launcher hidden; authorized history/lookup retained, no new answer delivery |
| API/model outage |Static FAQ may source-backed fallback; business result shows unavailable + normal navigation, no fake status/price |

## Open questions and deferred items

| ID | Concrete recommended direction / alternatives / tradeoff | Affected owner/session / approval |
| --- | --- | --- |
| D01 PROPOSED | Reuse delegated user Bearer to fixed allowlisted owner APIs via Infrastructure; owner validates JWT and local resource/scope on every call, with `TOKEN`/`REQUEST` freshness selected by the approved matrix and UserService authoritative local checks retained. IoT never stores token in transcript/model or durable workflow. Verify audience acceptance before wiring; if not supported, choose scoped token exchange/service identity with actor delegation (more setup, better service isolation), never expand audience blindly or use shared admin token. FAQ/history retains approved local JWT behavior; business reads/default operations use the approved `TOKEN` stale-claim window, while create reservation inherits asynchronous Account-current `REQUEST`. Delegation does not override either classification. | User/IoT/Operations/Payment; S05/S06; pending lead decision and owner auth evidence |
| D02 PROPOSED | Guest tab memory only/no durable history; Account conversations 30 days from creation (absolute), draft30 minutes since last edit; request ledger retained with conversation, no implicit business-write dedupe expiry. Purge expired content daily, history/lookup hidden immediately at expiry; metadata-only diagnostics30days, backup deletion SLA OPEN before production. Alternative P0 proposed90days offers longer recovery but more private storage. | IoT persistence/S04/S05/S07; pending retention policy approval; no schema implemented |
| D03 PROPOSED | Provider receives approved FAQ excerpts + bounded sanitized intent text (max4000), at most6 recent sanitized conversational turns for extraction. Redact plates/contact/account/order IDs/tokens; resolve selections via ephemeral local references. Private records/prices/status render deterministic cards, not full provider context. Free text may contain PII: redact before dispatch and disclose provider processing; if cannot sanitize, ask structured UI input. Alternative full chat/history egress offers richer context but requires broader privacy/retention review; not selected. | IoT/S04–S07; pending data-sharing approval, provider retention/deployment terms OPEN |
| D04 APPROVED 2026-11-05 | One model per encrypted credential configuration, keep existing key identity/version, atomic ordered revision, staged migration preserving old data; transient unsaved discovery, saved-only synthetic test. Limits/cache/dedupe as §§8–10. Alternative model shared per provider simpler but fails desired per-key flexibility. | IoT/S02/S03; implemented offline; manual migration review pending, no SQL applied |
| D05 PROPOSED / OPEN | Domain routes §§11–17 are target acceptance contracts. Owners approve wire/state mapping, quote validity and reservation→order workflow/idempotency retention. Extend existing owner services rather than new AI business DB or new Search service. Adds owner implementation effort but avoids duplicate financial/capacity authority. | Operations/Payment/User/Facility; G01–G07; includes create-reservation Saga wire/state/join/retry/reconciliation details under the approved ADR; S06–S08 blocked only for unavailable integrations |
| DEFERRED | Streaming, Owner revenue, Operator tools, support-ticket creation, payment/refund/session tools, personalized ranking, real hardware. Nonstream JSON first; S09 measure before adding transport complexity. | Separate approved scope required |

No approval requested to finish these documents. These concrete decisions are reviewable; implementation sessions require approval of only the D-items they consume, not reapproval of product scope.

## Review checks

- [x] FR/BR/UC/ownership linked; implementation vs target separated.
- [x] Source matrix traces UI/API/use case/data, includes mock and missing dependencies.
- [x] Guest/account/admin scope, nullability/errors and safe recovery described.
- [x] No autonomous business write; normal confirmation and owner authority preserved.
- [x] P0 inheritance and S01 deltas explicit, no silent claim of approved migration.
- [ ] D01–D05 approved by lead/owners; Jira and exact implemented commits supplied.
- [ ] G01–G07 implemented and integrated; resource authorization/concurrency/unknown outcome tested.
- [ ] Live performance/provider/DB/payment acceptance. S01 makes no such claim.
