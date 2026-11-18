# SPARK-340 — Report tổng hợp, evidence và bàn giao kiểm thử

Ngày hợp nhất: **18/11/2026 — Asia/Saigon**. Phạm vi: SPARK-340, Backend SPARK-368, Frontend SPARK-369, QA SPARK-370. Branch `feat/SPARK-340-admin-platform-policies`.

Đây là report chung thay thế các report/review/evidence/handoff rời của task. API design chính, quyết định storage đã duyệt, collection/environment Postman, guide dựng môi trường và session tiếp tục giữ vai trò riêng. Không có source/runtime change hay test run mới trong lần hợp nhất tài liệu này.

## Mục lục

- [Trạng thái và quyết định hiện hành](#current-status)
- [Phạm vi, thiết kế và các sửa đổi](#implementation)
- [Lịch sử review/verification](#history)
- [Findings và việc còn mở](#findings)
- [Bằng chứng mới nhất và HTTP lịch sử](#evidence)
- [Checklist UAT U01–U20](#uat-checklist)
- [DDL, repair và rollout](#rollout)
- [Prompt giao tester kiểm thử đầy đủ trước PR](#tester-prompt)
- [PR draft nội bộ — chưa submit](#pr-draft)
- [Danh mục nguồn gốc và khôi phục lịch sử](#source-archive)
- [Quy tắc cập nhật report này](#future-updates)

<a id="current-status"></a>
## 1. Trạng thái và quyết định hiện hành

| Phần | Trạng thái được chốt |
|---|---|
| P1 — Contracts | API design v1.6 approved; approved design không tự chứng minh runtime đã hoạt động. |
| P2/P3 — BE/FE | Implementation đã triển khai và qua các lượt sửa/review trong phạm vi task. |
| P4 | **Tạm đóng có điều kiện theo user**. FAIL/SKIP/NOT RUN/BLOCKED giữ nguyên, không đổi thành PASS. |
| P5 | **CLOSED — scoped implementation review accepted** ngày18/11 sau independent round2 đóng F-A/F-B/F-C, không phát hiện P1/P2 mới trong phần sửa. |
| P6 / trước PR | Branch đã công bố cho tester; **chờ kiểm thử đầy đủ và sign-off trước khi mở PR**. |
| Remote PR / merge | Chưa tạo PR, chưa merge. PR draft chỉ là nội dung tham khảo trong report này. |
| Live rollout | **NOT READY**: target DB DDL/inventory/import, live HTTP mới và durability trên target chưa hoàn tất. |
| Parent SPARK-340 | Vẫn active; chưa Done hoặc ready-to-merge. Session không chuyển completed. |

Source được independent reviewer xác minh tại `fb4d2ce41683ff727efd8b48bd6ebaff8ee9d098`. Commit tài liệu trước hợp nhất: `1bedfdae1b25218d0c7b728fa57087cec1ce8dab`. Các số kiểm thử bên dưới là evidence lịch sử được xác minh, **không phải run mới do việc gộp file**.

Scope closure không miễn các finding baseline/security ngoài phạm vi cho production. Nếu tester phát hiện blocking regression trong phạm vi, mở lại finding/P5 phù hợp. Nhận định trong report cũ rằng P4 chưa đủ điều kiện nghiệm thu đầy đủ vẫn là sự thật về evidence; quyết định tạm đóng phase của user không bị đổi hoặc diễn giải thành full acceptance.

<a id="implementation"></a>
## 2. Phạm vi, thiết kế và implementation

<a id="api-design-review"></a>
### 2.1 Contracts và ranh giới service

Các vòng review API được hợp nhất về [API design chính](../../03-design/api/admin-platform-policies-and-integrations-spark-340-api-design.md), v1.6 approved12/11/2026. Bảy review design gốc có snapshot bất biến trong danh mục nguồn ở cuối report. Approval thiết kế và status thực thi được tách rõ; đặc biệt không suy ra rằng SPARK-273 commercial/confirmation producer đã được giao.

- Operation sở hữu catalogue14 keys, effective PLATFORM/SITE, provenance/inheritance, draft/history/concurrency và activation.
- Payment sở hữu mode/credential/provider adapter, integration history, checkout binding và money verification.
- Notification sở hữu registered delivery fixtures; ACTIVE ở đây không chứng minh provider production gửi tin thành công.
- FE Admin System Config sử dụng API clients/auth helper hiện có, có console policy/effective/draft/integration; FE không tự làm nguồn giá hoặc quyết định tài chính.
- Admin settings dùng TOKEN freshness và local role/scope checks. Không thêm universal REQUEST Saga, role management, lot/gate/device operations hoặc broad Admin payment privilege.

Quyết định riêng F6/F7 đã được user duyệt: [dedicated Payment binding table và offline repair](../../03-design/api/SPARK-340-F6-F7-binding-repair-proposal.md), có additive Schema Freeze exception. Approval chuẩn bị DDL/code/test DB tạm **không** là quyền tự áp lên DB user.

### 2.2 Các sửa đổi quan trọng đã thực hiện

| Nhóm | Hành vi và giới hạn |
|---|---|
| Admin authorization | Role lowercase `admin` theo token thật; Admin Payment/Notification dùng strict UserServiceJwt RS256/JWKS. Default auth surface cũ của Payment là boundary khác, không được gộp thành claim mọi endpoint đã được harden. |
| Policy lifecycle | Typed catalogue, explicit units/null/zero/disabled, paginated/filterable revisions và by-id, create/update, If-Match/expectedUpdatedAt, immediate/scheduled activation. |
| Owner/Admin compatibility | Shared transaction-scoped advisory gate, validate composed effective policies, interval conflicts và bounded retries; PG interleavings được kiểm chứng. |
| Numeric/legacy policy | Safe hour/day-to-minute conversion và ceil division; compatibility của giá trị reallocation cũ được giới hạn, không tự cấp override mới khi Admin chưa có bounds. |
| Settings persistence | Save giữ gate qua persist/rollback, reader không thấy candidate chưa commit; hydration lỗi fail closed/retry. Atomic history allocation có PG evidence. |
| Secret hygiene | Masked fields/booleans, mode-isolated credentials, URL allowlist, request-local immutable config snapshot/redaction, protected persistence; không dùng raw provider error để lộ credential. |
| Key durability | Payment named DataProtection volume `/app/.keys/payment`, pinned application name. FIXTURE không lưu credential theo thiết kế; test durability credential phải phân biệt mode. |
| F6 — legacy recovery | Offline inventory/import, default dry-run, explicit apply, trusted order/PAYOS/PAYMENT/tenant/provider-link evidence, bounded private manifest, preflight, immutable/idempotent replay, no automatic key generation, generic safe errors. Không tự chứng minh operator-entered key là key lịch sử đúng; owner evidence và callback verification vẫn bắt buộc. |
| F7 — correct storage | Dedicated `checkout_signing_bindings`, FK RESTRICT/unique order identity, no new binding writes vào pricing history; exact protected legacy bridge và explicit migration. Không prune/TTL/delete legacy rows hoặc key ring tự động. |
| F8 — platform input | Create/update reject cả true và false của site-owned ArrivalLateToleranceEnabled trước persistence; omitted/null hợp lệ; Owner/site enablement không bị đổi. |
| F-A — authority | Webhook chỉ đọc exact per-order binding, không fallback options checksum; real checkout cần cả settings authority và binding, thiếu thì zero outbound. Options-only zero-HTTP emulation vẫn giữ. |
| F-B — association | Shared PayOsOrderCodes.Resolve giữa issuance và persistence, authoritative order read và conditional INSERT pin id/local code. Supported writers được bảo vệ; không thêm trigger chống mọi DML đặc quyền ngoài quy trình. |
| F-C — inventory | Unsupported/nonnumeric/noncanonical rows có original identity, nullable numeric code và state riêng; không chặn các valid row phía sau. Exit0 chỉ nói report hoàn tất, không nói rollout ready. |
| Environment/signing repair | Trước đây đã đồng bộ operational draft DDL có sẵn, sửa lifetime RSA signer, cấu hình dedicated Facility integration và percent-encoded broker password. Đây không phải bằng chứng dedicated binding DDL mới đã được apply trên target. |
| Flaky regression | Audit replay test dùng bounded observed-consumption wait thay fixed delay. Những quan sát flaky lịch sử được ghi riêng, không dùng để giấu failure mới. |

<a id="history"></a>
## 3. Lịch sử review và verification

| Mốc | Nội dung và cách đọc evidence |
|---|---|
|12/11 — P1 | Các vòng review design đến v1.6 và approval; không phải chứng nhận runtime/production. |
|14/11 — P4 | BE/FE và collection được bổ sung; các lỗi auth casing, concurrency, persistence/key binding và assertion được phát hiện qua review. Báo cáo kỳ vọng hoặc NOT_EXECUTED không được dùng như run thật. |
|16/11 — hardening / môi trường | Save/hydration rollback, key persistence và binding hardening; Owner/SITE fixture và Facility signer được repair. Terms vẫn503 do commercial producer chưa tích hợp. |
|17/11 — live54 | Full collection54 defined,48 executed,6 downstream NOT RUN;76 assertions pass/1fail/1skip. Failure terms503; skip optional SANDBOX credentials. SITE_OVERRIDE thật, stale-token/overlap/RBAC và durability có evidence. |
|17–18/11 — F6–F8 | Dedicated table/offline repair/F8 validators; build và BE/PG source verification. Collection tăng58, thêm4 F8 HTTP negatives; revision58 chưa chạy live. |
|18/11 — independent round2 | F-A/F-B/F-C CLOSED; implementation acceptable, no new P1/P2 in scope. Full BE1190pass/114skip/0fail; PG Payment9/9; scoped Node33/33. |
|18/11 — phase handoff | User yêu cầu đóng P5, push branch nhưng chưa PR; tester phải kiểm thử lại đầy đủ trước PR. |
|18/11 — consolidation | Hợp nhất47 file lịch sử/report/evidence thành report này, giữ full snapshots gốc trong Git; không thay source hoặc chạy lại baseline chỉ vì đổi tài liệu. |

Lịch sử count tăng theo implementation và scope command. Build có restore0errors/60warnings và --no-restore0errors/29warnings là hai lệnh khác. Whole-solution skip không được cộng thành pass; PG được bật và chạy riêng. Các nhận định quá rộng trước đó (SCHEDULED listing tương đương boundary values, overlap409 chứng minh U04, fresh-context chứng minh connection độc lập) đã được sửa; không lấy chúng làm nghiệm thu hiện tại.

<a id="findings"></a>
## 4. Findings, disposition và phần còn mở

| Finding / case | Disposition hiện hành |
|---|---|
| F6/F7/F8 implementation | Accepted qua independent review; actual target legacy data chưa được repair/rollout bởi các lượt implementation. |
| F-A/F-B/F-C | CLOSED; F-B giữ giới hạn supported application writers. |
| G-A | Low follow-up: issuance NumberStyles.Integer khác inventory canonical rule với input như whitespace/+prefix. Current writers tạo canonical numeric codes; unsupported lịch sử vẫn cần operator disposition. |
| G-B | Test HTTP-isolation incident đã công khai, sửa bằng post-configure và re-proven. Lần đầu không được khẳng định cô lập HTTP; chỉ synthetic credentials, không user DB. Không phải defect sản phẩm còn mở. |
| G-C | Low cleanup: keyring method cũ không có production caller. |
| F-D | Low: không có immutable DB trigger; không hứa bảo vệ mọi DML đặc quyền. |
| F-E | Low: một số CLI usage errors exit1 thay vì2; guide đã ghi đúng. |
| F-F | Low: outside-repo check có giới hạn khi không tìm thấy `.git`; operator phải dùng checkout/paths/ACL đúng quy trình. |
| F-G | Low: fallback parser `.env` ở PG helper chỉ strip double quotes; runner phải cấp env đúng, không in password. |
| F-H | Pre-existing/low: local nonnumeric order code không resolve được ở webhook lookup; inventory reporting không tự sửa protocol đó. |
| F-I | Low: handler ghi null enablement, nên bypass validator ngoài supported MediatR flow không được xem là API-supported path. |
| Baseline F1/default auth; deposit/S2S/EnsureOrder F2–F5 | Ngoài phạm vi sửa SPARK-340 theo user, chuyển Payment/security/SPARK-273 owner; không phải waiver cho production exposure. ID finding trong review design cũ có scope khác, không đồng nhất theo số F1/F2. |
| U06 | Deferred: cần version/value/provenance trước/tại/sau effectiveFrom, không chỉ list SCHEDULED. |
| U11 | Deferred: exact inherited platform minimum/site maximum composed-invariant case cần tester evidence. |
| U20 / group UAT | Chưa có per-case responsive/browser sign-off. Nhận xét “localhost phần lớn ổn” không đủ điền PASS. |
| U19 | BLOCKED dependency SPARK-273, còn là AC snapshot của SPARK-340/370. Không triển khai fake producer/acceptance hoặc suy từ policy revision để nhận booking PASS. |

Các correction quan trọng: U04 là RESOURCE_VERSION_CONFLICT từ stale token; U07 là overlap409. SITE_OVERRIDE đã được chứng minh qua Owner publish, không qua Admin site write. Clock skew claim áp dụng từng scheme; không suy tất cả default auth đều30s. FIXTURE mode không chứng minh persisted credential decryption.

SPARK-273 còn liên quan contract/auth/coverage, hold-first order provisioning ngoài Operations locks/transaction, combined acceptance+EnsureOrder và event-driven confirmation/recovery. Đóng review của task này không xác nhận những luồng đó đã tồn tại.

<a id="evidence"></a>
## 5. Bằng chứng và gate trước PR

### 5.1 Baseline mới nhất đã được reviewer xác minh

| Gate | Kết quả thật / giới hạn |
|---|---|
| `dotnet build SmartParking.slnx --nologo -m:1` |0errors/60warnings ở independent run có restore. Dependency/advisory warnings vẫn được ghi nhận. |
| `dotnet test SmartParking.slnx --no-build --nologo -m:1` |1190passed/0failed/114skipped/1304total; Payment335/12skip, Operation71/3skip, User352/70skip. |
| PG Payment concurrency/repair suite |9passed/0failed/0skipped trên generated temp DB. |
| Admin FE + Postman script tests |33passed/0failed/0skipped. |
| Full FE build/full frontend suite | Có run lịch sử thành công, cần tester chạy mới trước PR; không nhận count cũ thành kết quả hiện tại. |
| Newman58 +4F8 HTTP negatives | NOT RUN live ở revision hiện tại. |
| Target DDL/inventory/import/restart | NOT RUN trên target của user. |

Full collection hiện6 folders/58requests: RBAC12, Lifecycle18, Payment6, Notification1, Snapshot10, Scheduling/Write RBAC11. Request defined/executed/skip/NOT RUN và assertions phải báo riêng.

Hai full JSON snapshots chính được giữ trong các block bên dưới: independent source review mới nhất và HTTP54 lịch sử. Chúng là dữ liệu evidence, không phải hướng dẫn thực thi và không phải run mới. Toàn bộ JSON/report cũ khác vẫn có SHA256 + immutable Git link trong danh mục nguồn.

### 5.2 Điều kiện trước PR và rollout

- Tester chạy mới BE/FE/PG, prepared-stack HTTP58, Owner/SITE, F8 negatives, temporal/composed cases và browser UAT; lưu per-case kết quả.
- P1/P2 regression mới trong phạm vi thì mở lại review; low findings tiếp tục có disposition. Ngoài phạm vi được chuyển owner, không giấu failure.
- U19 giữ dependency/disposition của owner trong PR notes. Không xóa AC hoặc nhận PASS khi producer chưa có.
- Target cần DDL trước binary mới, đúng key ring và owner-verified legacy mappings; inventory report hoàn tất không đồng nghĩa mọi row ready. MISSING_BINDING/UNSUPPORTED_ORDER_CODE còn là action items.
- Chưa tự tạo PR/merge/deploy. Không tác động stack user khi chưa có target/window được chỉ định.

<a id="uat-checklist"></a>
## 6. Checklist UAT U01–U20

Mở http://localhost:5173. Dùng tài khoản seed Admin/Owner của môi trường local và mật khẩu trong `.env`; không chụp hoặc xuất mật khẩu/token vào evidence. Admin Dashboard → **System Config** là màn hình kiểm thử chính, không có URL riêng cho từng tab.

Ghi PASS / FAIL / NOT RUN, thời điểm, dữ liệu fixture và ảnh màn hình đã che thông tin nhạy cảm cho từng ca. Browser DevTools → Network có thể kiểm tra status/code và response; che Authorization khi lưu bằng chứng.

| Ca | Thao tác | Kết quả cần quan sát |
| --- | --- | --- |
| U01 Admin | Đăng nhập Admin, mở System Config → Operational Policies | Catalogue tải từ OperationService :5055, có 14 key, giá trị/version hiện hành và bounds thực tế; không 403 |
| U02 Draft | Create draft, thay một giá trị rồi lưu; đóng và mở lại draft | Các giá trị/bounds khác được giữ; chưa activate thì effective hiện tại không đổi |
| U03 Validation | Nhập minimum ≤0, minimum lớn hơn maximum, arrival tolerance ngoài 15–30, bound min > max | Có lỗi field hoặc lỗi có mã từ BE, không tạo revision hoạt động |
| U04 Conflict | Mở cùng draft ở hai cửa sổ; cửa sổ A lưu rồi B lưu với token cũ | B nhận 409 và hướng dẫn reload, không ghi đè A |
| U05 Immediate | Activate một draft hợp lệ ngay | Lịch sử và catalogue tải lại đúng version; revision trước còn đọc được |
| U06 Scheduled | Schedule một draft vài phút trong tương lai; đọc Effective Policy trước/sau mốc đó | Trước mốc dùng version cũ, tại/sau mốc dùng version mới; lịch sử SCHEDULED/CURRENT nhất quán |
| U07 Overlap | Hai draft cùng mốc kích hoạt | Draft thứ hai bị từ chối 409; không có hai revision hiệu lực chồng lấn |
| U08 Effective | Effective Policy → PLATFORM, rồi SITE với tenant/site đúng | PLATFORM_DEFAULT và SITE_OVERRIDE đúng dữ liệu; 0 hiển thị là giá trị thật; bật/tắt arrival tolerance độc lập giá trị phút |
| U09 Owner | Owner Dashboard → Policies, chọn bãi thuộc Owner; thử giá trị trong/ngoài Admin bounds | Giá trị hợp lệ publish được; ngoài bounds bị chặn; khi không cấp override thì không tự có khoảng 0..5 cho driver reallocation |
| U10 Legacy | Với bãi đã có DRIVER_REALLOCATION_REQUEST_LIMIT=2, republish giữ nguyên rồi thử đổi sang 5 khi chưa có bounds | Giữ nguyên dữ liệu cũ được phép; đổi mới không được tự cấp quyền |
| U11 Cross-policy | Site có override maximum=60 phút; Admin thử tăng minimum kế thừa thành90 phút | Admin activation bị từ chối vì effective minimum > maximum; cấu hình cũ còn hoạt động |
| U12 Payment | Integrations → PayOS; GET và lưu FIXTURE | Response chỉ có masked client id/configured flags; không có raw API/checksum key; FIXTURE không gọi provider |
| U13 Payment validation | Chọn URL không được phép hoặc gửi credential rỗng qua DevTools/API client | 400 có code cụ thể; lỗi hiện đúng trên form; không lưu cấu hình sai |
| U14 Mode isolation | Với credential SANDBOX/LIVE giả lập chỉ dùng fixture HTTP trong test tự động, đổi mode nhiều lần | Credential của mode này không sao chép sang mode khác; input chưa lưu được xóa khi đổi mode. Không gửi credential giả tới provider thật |
| U15 Restart | Ghi lại mode/status/masked id, restart rồi force-recreate Payment theo lệnh dưới | Cấu hình vẫn giải mã/khôi phục được; không tự mất credential; không cần nhập lại khóa |
| U16 Storage failure | Agent kiểm thử mô phỏng DB unavailable; user thử mở/lưu Integrations | 503 PAYMENT_INTEGRATION_PERSISTENCE_FAILED, không success giả; sau phục hồi giữ nguyên cấu hình đã commit |
| U17 Notification | Integrations → Notifications | EMAIL/SMS/PUSH/IN_APP hiển thị fixture provider tương ứng và trạng thái do BE cung cấp |
| U18 RBAC | Owner/Driver đăng nhập rồi mở màn Admin hoặc gọi các API Admin | UI chặn quyền; API trả 403. Anonymous API 401; Admin 200 |
| U19 Confirmed booking | Driver dùng vehicle/slot thuộc fixture hợp lệ, review terms rồi xác nhận booking; lưu receipt/booking id; Admin đổi policy | Booking cũ giữ giá trị/điều khoản đã chấp nhận; booking mới theo policy mới. Nếu thiếu fixture đánh NOT RUN, không đánh PASS bằng revision id đơn thuần |
| U20 Responsive | Kiểm tra các tab ở desktop, tablet và mobile; dùng bàn phím cho tab/modal | Bảng/card đọc được, nút chính còn truy cập được, focus và thông báo lỗi hợp lý |

### Restart test

Chỉ chạy khi không có người đang lưu cấu hình. Không xóa volume.

```powershell
docker compose -f compose.app.yml restart payment-service
docker compose -f compose.app.yml up -d --no-deps --force-recreate payment-service
```

Payment dùng named volume `payment-data-protection-keys` tại `/app/.keys/payment`. Lần chuyển môi trường hiện tại đã giữ bộ khóa cũ trước khi tạo lại container. Khi triển khai sang máy khác cần chuyển/backup key store an toàn cùng dữ liệu đã mã hóa.

### Các ca cần test tự động có kiểm soát

UI không chứng minh được hai giao dịch chạy đúng interleaving hoặc webhook HMAC. Agent độc lập phải chạy PostgreSQL concurrency; Save A thất bại trong khi Save B chờ; hydration đọc lỗi rồi Save; callback từ checkout đã bind sau mode switch/rotation/restart; binding thiếu hoặc thay thế bị từ chối; provider phản chiếu secret trên initiate/cancel được redact.

Checkout PayOS thật được tạo trước khi cơ chế binding bền vững tồn tại không có đủ dữ liệu để suy ra khóa phát hành. Cần đối soát và khôi phục bằng thông tin đáng tin cậy trước khi xử lý callback; không tạo binding bằng cách đoán timestamp. Ghi riêng trường hợp legacy này khi rollout có giao dịch thật đang chờ.

<a id="rollout"></a>
## 7. DDL, repair và rollout

Thiết kế bảng riêng và công cụ offline đã được user duyệt, gồm ngoại lệ Schema Freeze. Việc duyệt chỉ cho phép chuẩn bị code/DDL và test trên DB tạm. Chưa tự chạy DDL/import trên stack đang UAT. Hướng dẫn này dành cho Payment owner/operator có quyền DB và nguồn evidence đáng tin cậy; không thêm quyền xử lý thanh toán vào Admin settings API.

### Thay đổi hành vi

- F7: binding mới được lưu tại `checkout_signing_bindings`, liên kết tới Payment order; payload mode/version/checksum được DataProtection mã hóa. Không append binding mới vào `policy_revisions`. FK RESTRICT và unique order identity bảo vệ liên kết; chưa có automatic TTL/prune. Giữ binding và key ring cùng financial history cho đến khi có chính sách archive được duyệt.
- Binding chính xác, đã mã hóa trong reserved policy namespace của bản trước vẫn đọc được. Có thể chuyển sang bảng mới bằng source LEGACY_POLICY. Không suy checksum key từ thời điểm tạo order và không dùng key hiện hành làm fallback.
- F6: checkout chưa từng có binding chỉ được khôi phục bằng mapping có provider link khớp transaction PAYOS/PAYMENT của đúng order/tenant, với key/mode/version do owner xác nhận. Dry-run là mặc định. Mapping sai/thiếu evidence/conflict không được ghi. Đây không phải chứng minh tự động rằng một key do operator nhập là key lịch sử đúng; nguồn dữ liệu và callback verification vẫn phải được owner xác nhận.
- F8: platform create/update chỉ nhận omitted/null cho arrivalLateToleranceEnabled. true/false trả validation error trước khi persistence; Owner site flow giữ quyền enablement.

#### Follow-up F-A/F-B/F-C

Webhook verification luôn đọc exact per-order binding, kể cả khi adapter được tạo trực tiếp không có settings store; key trong options không còn là nguồn fallback. Checkout thật bắt buộc có cả settings authority và signing binding. Options-only chỉ còn hỗ trợ các nhánh emulation vốn không gửi HTTP.

Checkout issuance và repository dùng chung PayOsOrderCodes.Resolve để đối chiếu provider code với order đọc từ DB, gồm cả mapping Guid fallback đã có. INSERT cũng so sánh local code đã quan sát để không nhận thay đổi chen giữa bước kiểm tra/ghi. Đây là bảo đảm của các supported application writers, không phải trigger chặn mọi DML thủ công ngoài quy trình; không thêm schema trong follow-up này.

### Thứ tự rollout do owner thực hiện

1. Xác định Payment DB đích và sao lưu theo quy trình nhóm; giữ nguyên Payment DataProtection volume/key ring. Không dùng `.env` hoặc key XML trong Git/evidence.
2. Review/apply `scripts/database/upgrades/20261117-payment-checkout-signing-bindings.sql` bằng tài khoản owner. Script additive, transaction-scoped, không sửa money/status, không xóa policy history. Fresh DB init đã có bảng trong `04-payment-pricing-service-db.sql`. Chưa có startup migration tự động.
3. Build offline tool từ cùng branch. Cấp `ConnectionStrings__Payment` qua secret/environment của operator, không qua argv; cấp `DataProtection__KeysDirectory` là đường dẫn tuyệt đối tới đúng key ring Payment, ngoài repo. Tool không tự sinh/rotate key. Bảo vệ ACL và không đưa key directory vào repo.
4. Chạy inventory và dry-run. Thu thập mapping từ provider/audit record được Payment owner xác nhận. Không dùng thứ tự tạo order/thời gian để đoán key.
5. Khi dry-run READY và owner kiểm tra bằng chứng, chạy apply. Nếu lỗi giữa batch, một số row trước đó có thể đã commit; rerun cùng mapping là idempotent. Công cụ preflight toàn manifest trước khi bắt đầu apply; không có all-or-nothing transaction xuyên nhiều order.
6. Kiểm tra binding count/source, callback có chữ ký đúng và sai, replay, decrypt sau restart bằng cùng key ring. Kiểm tra order/transaction/invoice amounts/status không bị sửa bởi import. Lưu kết quả đã sanitize.
7. Deploy binary mới sau khi DDL sẵn sàng và mọi checkout cũ còn cần xử lý đã có disposition. **Inventory có MISSING_BINDING chưa được xử lý là rollout gate**, không coi code tool đã pass test là legacy production data đã được repair.

Nếu binary mới chạy trước DDL, binding repository sẽ fail closed, outbound checkout bị chặn. Không sửa bằng cách tắt persistence hoặc nhận callback không xác minh. Không drop bảng/key ring khi rollback; binary cũ không đọc bảng mới nên rollback trực tiếp có thể lại mất callback của checkout mới. Dùng bản tương thích đọc cả hai nơi hoặc hoàn tất reconciliation trước rollback theo owner-approved plan.

### Lệnh operator — chưa thực thi trên DB user

Sau khi operator cấu hình psql bằng secret/env riêng và chọn đúng Payment DB:

```powershell
psql -v ON_ERROR_STOP=1 -f scripts/database/upgrades/20261117-payment-checkout-signing-bindings.sql
dotnet build scripts/payment/SmartParking.CheckoutBindingRepair --nologo
dotnet run --project scripts/payment/SmartParking.CheckoutBindingRepair --no-build -- --inventory --offset 0 --limit 100
dotnet run --project scripts/payment/SmartParking.CheckoutBindingRepair --no-build -- --manifest C:\Secure\private-bindings.json
dotnet run --project scripts/payment/SmartParking.CheckoutBindingRepair --no-build -- --manifest C:\Secure\private-bindings.json --apply
```

Inventory paginates over PAYOS checkout transactions without a dedicated binding. Each row reports PaymentOrderId, OriginalOrderCode, nullable numeric OrderCode, ProviderLinkId and State. Nonnumeric, nonpositive or noncanonical historical codes return UNSUPPORTED_ORDER_CODE with OrderCode=null; the scan continues through following rows. These rows require owner investigation/reconciliation rather than guessed lookup. Exit0 means the inventory report completed, **not** that all rows are ready: MISSING_BINDING and UNSUPPORTED_ORDER_CODE remain rollout actions.

Capture all pages on a quiescent snapshot **before** applying imports; applying changes between offset pages changes membership. After apply, rescan from offset0. BOUND_LEGACY_POLICY means an authentic protected policy row exists and can be migrated, not that tariff cleanup is already safe. New checkout writes validate provider code against the stored order using the shared issuance mapping; a caller-supplied PaymentOrderId alone is insufficient. The existing nonnumeric webhook local-order lookup limitation is not repaired by reporting the inventory row.

Private manifest is an array, absolute path outside repository, max1MiB/1000 unique order codes. Template for manual legacy import (invalid until owner supplies actual evidence and key):

```json
[
  {
    "orderCode": 123456789,
    "providerLinkId": "owner-must-supply-exact-stored-provider-link-id",
    "source": "LEGACY_IMPORT",
    "evidenceReference": "owner-export/verified-entry-id",
    "mode": null,
    "version": 0,
    "checksumKey": null
  }
]
```

Use LIVE or SANDBOX, positive configuration version and actual checksum from the issuing authority. FIXTURE is not an authoritative provider mode. Unknown/missing historical key or missing stored provider-link evidence stays a manual reconciliation case. Protect manifest ACL, keep it outside Git, and follow the team's secret-file disposal process. Do not echo it to terminal or include it in raw Newman exports.

For an existing reserved-policy binding, use `source: LEGACY_POLICY`, same real orderCode/providerLinkId/evidenceReference, omit mode/version/checksumKey. The tool decrypts the exact old protected row and writes the dedicated row; no new key is guessed. Old policy row is retained. No automatic deletion is implemented; owner may only archive the legacy namespace after migration verification, backup and retention approval. Ordinary pricing rows are not migrated or deleted.

Exit0 = READY dry-run, APPLIED apply or completed inventory report. Exit2 = invalid/conflicting per-entry result and the no-arguments usage path. Other malformed usage currently follows the generic exit1 path (known low finding). Exit1 also covers schema/key access/invalid JSON. Output never includes checksum or protected payload. Exit codes and individual states must be checked; process completion alone is not proof of application.

### Reviewer checklist

- Verify real PG suites, especially backend PID inequality and read-before-outbound flag, source migration with unchanged pricing count, concurrent first-writer winner, orphan rejection and FK retention.
- Run offline CLI integration test: missing pre-feature binding fails signature; dry-run writes0; apply/replay; wrong provider/tenant/method/secret conflict; batch preflight; malformed JSON never echoes secrets; fresh key provider verifies the restored signature, tampered signature rejected.
- Verify missing/corrupt binding never falls back to current key. Normal initiate must persist a binding before any provider HTTP. Import never initiates checkout or changes financial status.
- For F8 exercise both create and update, true and false, and unchanged row after validation failure. Null/omitted remain accepted; existing Owner enablement behavior still passes.
- No live U19/SPARK-273 conclusion follows from these repairs. Existing user stack has not been migrated/restarted by this implementation turn.

<a id="tester-prompt"></a>
## 8. Prompt giao tester kiểm thử đầy đủ trước PR

**Output rule:** cập nhật kết quả và JSON sanitized ngay trong report chung này, không tạo thêm dated report/evidence files. Raw exports và private inputs giữ ngoài Git.

Bạn là tester/QA độc lập cho SPARK-340 và các task con368/369/370. P5 implementation review đã đóng theo quyết định user ngày18/11/2026. Nhiệm vụ hiện tại là kiểm thử lại toàn bộ chức năng trong phạm vi, thu evidence và đưa verdict **đủ/chưa đủ chuẩn bị PR**. Không tự tạo PR, merge, commit/push hoặc đóng parent Jira. Không sửa source để làm test xanh; phát hiện implementation defect thì báo đúng owner và tái hiện.

### 1. Baseline và hướng dẫn

Repo `Quangthm/Mock-Project-Smart-Parking`, branch `feat/SPARK-340-admin-platform-policies`. Fetch/pull --ff-only trong checkout tester đã giữ an toàn local work, ghi actual HEAD và working tree. Source implementation được review tại fb4d2ce4; các commit docs sau đó không thay đổi source. Không hardcode các số test cũ làm kết quả run mới.

Đọc AGENTS.md → ai/AGENTS.md → coding/FE guide áp dụng → session mới nhất → P5 closure ngày18/11 → independent FA–FC review → tester handoff/U01–U20 → F6–F8 rollout guide. Collection hiện **58 request**, gồm4 F8 HTTP negatives. Các report54 request chỉ là lịch sử.

### 2. Môi trường và giới hạn quyền

- Ưu tiên máy/stack tester riêng với secrets riêng và DB có schema đúng. Không dùng credentials thật của PayOS, không gửi HTTP checkout/charge đến provider thật. FIXTURE dùng cho functional test; optional SANDBOX provider credentials thiếu thì ghi SKIP.
- Fresh test DB có init DDL hiện tại. Với DB tester cũ, owner review và áp additive script trước binary mới theo runbook. Công cụ repair chỉ xử lý fixture/legacy evidence do owner xác nhận. Không suy key từ timestamp, không coi signature options hiện tại là fallback.
- Không xóa DB/volume, thay keypair, chạy DDL/import hoặc restart stack user đang UAT khi chưa có target/window được owner xác nhận. Approval schema exception không phải quyền thực thi trên mọi DB. Nếu thiếu target, tiếp tục read-only/unit/temp-DB work và ghi live gates NOT RUN, rồi yêu cầu owner chỉ định môi trường/window.
- Nếu dùng chung máy đang có stack, không dựng thêm stack trên cùng external network/DB aliases hoặc reuse user volumes rồi gọi đó là isolated. Cần cấu hình tách network/volume/port được kiểm tra hoặc dùng máy riêng.
- Full Newman tạo/activate platform revisions và có thể tác động fixture site; restart/recreate Payment ngắt checkout. Chạy trong môi trường/window test riêng, ghi side effects và fixture đã dùng.

### 3. Gate tự động — chạy thật và báo từng kết quả

```powershell
dotnet build SmartParking.slnx --nologo -m:1
dotnet test SmartParking.slnx --no-build --nologo -m:1
npm --prefix FE run build
node --test tests/frontend/*.test.cjs tests/postman/spark340-collection.test.cjs
dotnet test tests/Services/SmartParking.OperationService.Tests --no-build --filter FullyQualifiedName~PlatformPolicyPostgresConcurrencyTests
dotnet test tests/Services/SmartParking.PaymentService.Tests --no-build --filter FullyQualifiedName~PaymentIntegrationPostgresConcurrencyTests
```

PG opt-in vars: SMARTPARK_OPERATION_POLICY_TEST_CONNECTION và SMARTPARK_PAYMENT_TEST_CONNECTION, password cung cấp riêng đúng quoting; chỉ fixture-generated temporary DB được CREATE/DROP. Không in connection string. Ghi passed/failed/skipped/total và warning theo lệnh thực tế (build có restore khác --no-restore). Nếu full baseline có lỗi ngoài task, ghi rõ nguồn/ảnh hưởng và chuyển owner, không mở rộng sửa service khác hoặc giấu lỗi.

Các ca Payment quan trọng: callback chỉ tin exact binding; thiếu settings/binding không outbound; DI fake-handler thực sự cô lập; provider code khác order bị chặn trước insert; distinct backend PID tại biên outbound; import dry-run0write, apply/replay/conflict/partial retry, tamper rejection, key-provider/context restart, inventory có unsupported vẫn trả các row hợp lệ. Không dùng current-key/timestamp inference hoặc fake success để vượt gate.

### 4. HTTP/Postman/Newman — full58 trên prepared stack

Import collection và environment example của SPARK-340. Token UserService thật cho Admin/Owner/Driver, resource IDs lấy từ API, fixtures hợp lệ theo đúng ownership. Giữ populated environment/keys ngoài Git. Không chỉnh example trong repo thành file chứa token.

Chứng minh:

1. Catalogue14 keys, type/unit/bounds, null/unset so với0/disabled, missing/invalid policy values.
2. Admin allowed, Owner/Driver forbidden và anonymous401 trên GET lẫn writes; Admin không có quyền site/platform ngoài contract, không được cấp thêm lot/gate/device/payment-operation privilege.
3. Draft create/update, stale If-Match/expectedUpdatedAt409 RESOURCE_VERSION_CONFLICT, by-id/history/filter/pagination, invalid ranges và bounds. Overlap409 là ca riêng.
4. Immediate/scheduled activation. Với U06 chụp actual timestamp/version/value **trước/tại/sau effectiveFrom**, dùng supported as-of query để chứng minh đúng boundary và phân biệt với việc chỉ list SCHEDULED.
5. Owner publish trong/ngoài Admin bounds, inheritance, SITE_OVERRIDE thật từ Owner flow; GET effective PLATFORM và SITE có provenance đúng. U11 platform minimum/site inherited maximum khiến activation không hợp lệ phải bị chặn, không làm đổi active policy.
6. Bốn F8 HTTP negatives: create/update × true/false đều400 VALIDATION_FAILED, đúng ArrivalLateToleranceEnabled field. Null/omitted vẫn hợp lệ; so draft/updatedAt trước/sau để bảo đảm invalid request không mutate. Owner enablement vẫn hoạt động.
7. Payment GET/PUT masking và mode isolation, URL allowlist/blank credentials/error contract, fixture zero outbound. Notification registered fixture channels không được diễn giải thành provider live đã gửi thành công.
8. Restart/force-recreate Payment trên test stack được phép: giữ DB/keys volume, cấu hình giải mã được. Nếu chứng minh credential durability, dùng marker synthetic trên fixture settings và chỉ GET/PUT/restart; **không gọi provider**. FIXTURE không lưu credential theo thiết kế nên không dùng FIXTURE để khẳng định raw credential decryption.
9. F6/F7 DDL/inventory/import trên **DB tester được phép**: exact old protected rows migrate, missing historical mapping giữ blocked/reconciliation, không đổi money/status, inventory unsupported là action item. Giữ key ring và dữ liệu tài chính, không prune bằng chứng.

Ghi request-defined/executed/skipped/NOT RUN riêng và assertions passed/failed/skipped, script/prerequest/request failures. Không tính pm.execution.skipRequest() thành PASS. Không giảm expected status để nhận503/400 ngoài contract. Nếu terms vẫn503 OWNER_UNAVAILABLE, ghi đúng failure và các request downstream NOT RUN; không suy booking snapshot từ revision policy.

### 5. Browser UAT và nghiệm thu nhóm

Tester chạy checklist U01–U20 trên FE+BE từ đúng commit. Cần per-case evidence, đặc biệt U06 temporal values, U11 composed invariant và U20 desktop/tablet/mobile (ví dụ360/768/1440px). Kiểm tra loading/error/409 reload, form validation, tab/modal/history, secret clearing/masking, units/zero/disabled và Owner UI. Backend tests không chứng minh responsive UI.

User đã báo phần lớn localhost ổn nhưng chưa có sign-off từng case; không tự điền PASS từ nhận xét đó. Nếu agent không thao tác browser/không có fixture, ghi NOT RUN để tester nhóm tiếp tục, không thay bằng source scan.

U19 hiện BLOCKED bởi SPARK-273. Không yêu cầu task này triển khai commercial producer/event-driven reservation workflow. Snapshot immutability vẫn là AC của SPARK-340/370: giữ dependency và disposition của owner trong report/PR notes, không đánh dấu PASS hoặc tự xóa tiêu chí. Nếu producer chưa có, report rõ phần còn chưa chứng minh end-to-end.

### 6. Deliverables / gate trước PR

- Cập nhật report chung với section kết quả ngày chạy, actual commit/configuration, fixtures, commands và số thật; UAT matrix PASS/FAIL/SKIP/NOT RUN/BLOCKED có lý do.
- Chèn evidence JSON hợp lệ trong fenced block của section run mới trong report chung, response/code/assertions đã sanitize. Không Authorization/Bearer/token/password/connection string/key XML/private manifest, không raw request chứa credential trong repo. Scan output trước khi lưu; chỉ mask prose không phải secret value.
- Findings có severity, file/line, expected/actual và bước tái hiện. P1/P2 mới trong phạm vi thì đề nghị reopen P5/finding; low observations ghi disposition riêng.
- Verdict phân biệt implementation, test-environment validation, owner live rollout và parent acceptance. P5 đã đóng source review không có nghĩa tất cả UAT/live gates PASS.
- Chỉ đề nghị chuẩn bị PR sau khi các gate trong phạm vi có bằng chứng, testcase còn hoãn có owner/disposition rõ và tester nhóm sign-off. Không tự mở PR hoặc đổi Jira. Gắn link section kết quả của report chung vào session, giữ lịch sử và collection SPARK-238.

<a id="pr-draft"></a>
## 9. PR draft nội bộ — chưa submit

**Proposed title:** `feat(admin): configure platform policies and integration settings [SPARK-340]`

Target: `develop`. Local preparation only — **do not submit a PR yet**. P5 implementation review closed18/11/2026 after independent acceptance of F-A/F-B/F-C. User requires full tester verification/sign-off before opening a PR. The feature branch is published; no remote PR has been created. See [closure record](#current-status) and [test prompt](#tester-prompt).

> Publication follow-up: user authorized committing/pushing the feature branch for tester verification. This draft is not an instruction to merge or submit a PR. Consult [tester handoff](#tester-prompt) and the latest session section for actual Git delivery status; earlier baseline descriptions below are historical preparation context.

### Summary

Admin can manage versioned platform policy defaults and Owner override bounds, inspect effective policy provenance, and configure permitted Payment integration settings through service-owned APIs. The Admin console exposes catalogue, draft/history/activation, effective values and masked integration status while preserving service ownership and role boundaries.

### Related Issue

Refs SPARK-340, SPARK-368, SPARK-369, SPARK-370.

SPARK-273 supplies the reservation commercial integration needed for the linked booking-snapshot UAT case. This draft does not claim that dependency is implemented or that the parent story is fully accepted.

### Changes

- [x] Add typed14-key policy catalogue, effective PLATFORM/SITE resolution and inheritance provenance.
- [x] Add platform draft create/update, optimistic version checks, revision listing/by-ID and immediate/scheduled activation.
- [x] Coordinate Owner publication and Admin activation through the shared compatibility lock.
- [x] Add authenticated Admin Payment settings with mode-isolated credentials, masked responses, URL allowlist, persisted history and protected checksum bindings.
- [x] Add read-only registered Notification fixture status and Admin FE views using existing auth clients.
- [x] Add BE/FE regression coverage, reusable Postman collection, sanitized evidence and delivery documentation.
- [x] Implement approved dedicated signing-binding storage and offline legacy repair (F6/F7), and reject ignored platform site-enablement input (F8).
- [ ] Obtain independent re-review, prepare the target DB with reviewed additive DDL and validate actual legacy checkout mappings before rollout; assign remaining P5 findings.

### Implementation Details

HTTP controllers dispatch through IMediator and application-owned ports. Operation owns platform policy resolution/lifecycle; Payment retains secrets, gateway mode, history and checkout signing; Notification exposes its registered local fixture adapters. Admin configuration uses TOKEN freshness and local role/scope checks. Payment/Notification Admin routes explicitly use strict UserServiceJwt; their pre-existing non-Admin authentication surfaces are separate dependencies.

Policy updates use an explicit conditional write with expectedUpdatedAt/If-Match. Activation and Owner publication share transaction-scoped serialization and reject incompatible intervals/composed settings. Accepted reservation JSON is guarded against mutation in the existing Operations UnitOfWork; full booking-chain HTTP proof remains blocked by SPARK-273.

Payment DataProtection keys use a persistent volume. Durable signing bindings are written before outbound creation and remain bound to the issuing configuration. Approved F6/F7 repair moves new writes to checkout_signing_bindings with financial-history retention and FK RESTRICT, retains exact protected legacy-row reads, and provides an offline dry-run/apply repair tool. Missing bindings fail closed; target DB upgrade and owner-verified legacy mappings must be completed before rollout. See the F6–F8 rollout guide.

### Testing

- [x] Build succeeds — latest independent restored BE build0errors/60warnings; the earlier29-warning figure used --no-restore. Prior P6 FE build succeeded; a fresh full FE build is requested in the next tester run.
- [x] Unit tests pass — latest independent whole-solution run1190passed/0failed/114skipped. Skips are not executed passes.
- [x] Scoped FE/Postman regression —independent33passed (30 Admin FE +3 collection tests); earlier P6 full FE261 remains historical pending fresh full tester run.
- [x] Integration tests where enabled — latest independent PG Payment9/9, including offline CLI, mixed inventory, provider-code identity and distinct backend-PID proof. Earlier independent PG Operation3/3 remains historical pending rerun.
- [ ] Manual testing completed — user reports most localhost checks stable, tester-group sign-off pending. U06 boundary values, U11 composed invariant and U20 responsive UAT remain deferred.
- [ ] Full linked booking snapshot — U19 BLOCKED by unavailable commercial integration. Historical Newman76pass/1fail/1skip belongs to54-request revision. Current58-request collection adds4 F8 negatives; live run NOT RUN on this revision. No all-green UAT claim.

**Test results:** [F6–F8 verification](#source-008) and earlier [P6 handoff](#source-017). An expanded generic Payment Postman probe exposed an unrelated baseline environment-example failure; the P6 report records it separately from scoped SPARK-340 regression.

### Screenshots / Evidence

- [Independent review and live verification](#source-028)
- [Sanitized Newman evidence](#evidence-http-history)
- [P5 source/security audit](#source-036)
- [P5 review and finding dispositions](#findings)
- [Localhost UAT checklist](#uat-checklist)

### Breaking Changes

- [ ] No breaking changes — cannot claim this while legacy checkout callback compatibility remains unresolved.
- [x] Rollout/configuration requirements documented — preserve Payment DataProtection volume/key material and JWT/JWKS configuration. User approved one additive Schema Freeze exception: checkout_signing_bindings. Review/apply the provided upgrade script manually before the new binary; fresh init declaration is also updated. No shared-DB migration was executed by the implementation agent.
- [ ] Legacy checkout migration/drain/reconciliation plan approved and verified.

### Reviewer Notes

P4 was temporarily closed by user instruction; no failed/blocked test was promoted to PASS. P5 implementation review is now closed after independent round2 acceptance; low findings retain the closure record's follow-up disposition. Full tester verification and owner rollout/inventory validation remain pending. Baseline/default-auth and reservation order/confirmation findings remain delegated to Payment/SPARK-273. Do not submit this draft before the user-requested testing gate.

The initial tester snapshot is9a84aeaf, containing five task-scoped commits on top of baseline055da459. The unrelated SPARK-238 collection change remains outside those commits. Current remote develop freshness has not been checked; compare/reconcile it safely before a future merge. Do not auto-close Jira from this draft. Session remains active until actual delivery/acceptance is complete.

<a id="evidence-snapshots"></a>
## 10. Full evidence snapshots được giữ trong report

<a id="evidence-latest-independent"></a>
<details>
<summary>Latest independent source review — 1190 BE / PG9 / Node33</summary>

Nguồn: [source-034](#source-034), original SHA256 `0a9d5398b9eacf4fc0fb8c9a8c12d1eb5fb41627297c6286f2311511cd95da9e`. Data JSON giữ nguyên giá trị, chỉ chuẩn hóa cách trình bày.

```json
{
  "ticket": "SPARK-340",
  "scope": "Independent review of the F-A/F-B/F-C follow-up (signing authority, provider-code validation, mixed inventory)",
  "reviewerRole": "independent reviewer",
  "reviewRound": 2,
  "executedAtUtc": "2026-11-18T01:59:17Z",
  "executedAtLocal": "2026-11-18T08:59:17+07:00",
  "branch": "feat/SPARK-340-admin-platform-policies",
  "head": "fb4d2ce41683ff727efd8b48bd6ebaff8ee9d098",
  "roundBaseline": "8ca61a609f07fb1acec8a9654d9944fbd50309db",
  "wholeStoryBaseline": "c38d6f8a644e778e99c4d6d7077653aa471c5614",
  "roundDelta": {
    "files": 18,
    "insertions": 1067,
    "deletions": 43,
    "commits": [
      "3306bb14 fix(payment): require signing authority for checkout callbacks [SPARK-340]",
      "596ac375 fix(payment): validate provider order bindings and inventory rows [SPARK-340]",
      "fb4d2ce4 docs(policy): record independent review and binding hardening [SPARK-340]"
    ],
    "schemaFilesChanged": 0
  },
  "workingTree": {
    "porcelainEntriesBefore": 1,
    "porcelainEntriesAfter": 1,
    "untrackedFiles": 0,
    "entry": "M docs/04-testing/postman/SmartParking-FacilityStructure-SPARK-238.postman_collection.json",
    "note": "Unrelated SPARK-238 collection preserved untouched."
  },
  "secretsHandling": {
    "tokensRedacted": true,
    "authorizationHeadersIncluded": false,
    "connectionStringsIncluded": false,
    "credentialsIncluded": false,
    "keyXmlIncluded": false
  },
  "commands": [
    {
      "order": 1,
      "command": "node --test tests/frontend/admin-platform-policies.test.cjs tests/postman/spark340-collection.test.cjs",
      "exitCode": 0,
      "result": {
        "tests": 33,
        "pass": 33,
        "fail": 0,
        "cancelled": 0,
        "skipped": 0,
        "todo": 0,
        "durationMs": 3412.5
      }
    },
    {
      "order": 2,
      "command": "dotnet build SmartParking.slnx --nologo -m:1",
      "exitCode": 0,
      "result": {
        "errors": 0,
        "warnings": 60,
        "elapsed": "00:01:37.12"
      },
      "note": "Implementation report's 29 warnings is a --no-restore measurement; restored build adds NU1900 for the offline tool. Advisory only."
    },
    {
      "order": 3,
      "command": "dotnet test SmartParking.slnx --no-build --nologo -m:1",
      "exitCode": 0,
      "result": {
        "passed": 1190,
        "failed": 0,
        "skipped": 114,
        "total": 1304
      },
      "perProject": [
        {
          "project": "SmartParking.Architecture.Tests",
          "failed": 0,
          "passed": 12,
          "skipped": 0,
          "total": 12
        },
        {
          "project": "SmartParking.FacilityService.Tests",
          "failed": 0,
          "passed": 199,
          "skipped": 29,
          "total": 228
        },
        {
          "project": "SmartParking.IoTService.Tests",
          "failed": 0,
          "passed": 1,
          "skipped": 0,
          "total": 1
        },
        {
          "project": "SmartParking.NotificationAuditService.Tests",
          "failed": 0,
          "passed": 220,
          "skipped": 0,
          "total": 220
        },
        {
          "project": "SmartParking.OperationService.Tests",
          "failed": 0,
          "passed": 71,
          "skipped": 3,
          "total": 74
        },
        {
          "project": "SmartParking.PaymentService.Tests",
          "failed": 0,
          "passed": 335,
          "skipped": 12,
          "total": 347
        },
        {
          "project": "SmartParking.UserService.Tests",
          "failed": 0,
          "passed": 352,
          "skipped": 70,
          "total": 422
        }
      ],
      "skipReasons": [
        {
          "count": 29,
          "fact": "StructurePostgresFact",
          "requires": "SMARTPARK_STRUCTURE_TEST_CONNECTION"
        },
        {
          "count": 3,
          "fact": "OperationPolicyPostgresFact",
          "requires": "SMARTPARK_OPERATION_POLICY_TEST_CONNECTION"
        },
        {
          "count": 4,
          "fact": "PaymentPostgresFact",
          "requires": "SMARTPARK_PAYMENT_TEST_CONNECTION (executed in command 4)"
        },
        {
          "count": 3,
          "fact": "RabbitMqLiveFact",
          "requires": "SMARTPARK_RABBIT_TEST_PORT"
        },
        {
          "count": 68,
          "fact": "PostgresFact (UserService)",
          "requires": "SMARTPARK_AUTH_TEST_CONNECTION"
        },
        {
          "count": 1,
          "fact": "ProfileBrokerFact",
          "requires": "isolated PostgreSQL + SMARTPARK_PROFILE_RABBITMQ_PORT"
        },
        {
          "count": 1,
          "fact": "RabbitPostgresFact",
          "requires": "isolated PostgreSQL and RabbitMQ fixtures"
        }
      ]
    },
    {
      "order": 4,
      "command": "dotnet test tests/Services/SmartParking.PaymentService.Tests --no-build --filter FullyQualifiedName~PaymentIntegrationPostgresConcurrencyTests",
      "exitCode": 0,
      "result": {
        "passed": 9,
        "failed": 0,
        "skipped": 0,
        "total": 9,
        "durationSeconds": 86
      },
      "environment": "SMARTPARK_PAYMENT_TEST_CONNECTION supplied in process env; local PostgreSQL service account with CREATE DATABASE",
      "databaseScope": "generated temporary databases only (smartpark_payment_test_<32 hex>), dropped WITH (FORCE)"
    }
  ],
  "criteria": {
    "F-A": {
      "verdict": "CLOSED",
      "proven": [
        "webhook trusted key comes only from the per-order binding; options ChecksumKey is not a verification fallback with or without a settings store",
        "real checkout requires settings authority AND binding persistence before any outbound HTTP (CHECKOUT_BINDING_UNAVAILABLE, zero calls)",
        "exact previously issued bound key verifies true while the current options key verifies false",
        "production Infrastructure DI resolves a working binding authority; removing ICheckoutSigningBindings yields zero outbound",
        "FIXTURE and credential-less SANDBOX emulation still return before the authority check with zero HTTP"
      ],
      "residual": [
        "GetTrustedWebhookChecksumKey still exists with no production caller",
        "PayOsOptions.ChecksumKey still used for outbound checkout signature and cancel/status configuration (not claimed removed)"
      ]
    },
    "F-B": {
      "verdict": "CLOSED_FOR_SUPPORTED_WRITERS",
      "proven": [
        "PayOsOrderCodes.Resolve is the single issuance mapping used by checkout and binding persistence",
        "existing mapping preserved for numeric, non-numeric and Int64.MaxValue-shaped inputs; Int64.MinValue handled without overflow",
        "non-positive numeric codes rejected with PaymentDomainException INVALID_PAYMENT_ORDER_CODE (mapped to 400)",
        "repository loads the authoritative order and rejects a provider code mismatch before inserting",
        "conditional INSERT pins payment_orders.id and the observed local order_code; read-back verifies parent identity and code",
        "same bound code with a different trusted PaymentOrderId leaves zero binding rows"
      ],
      "limitation": "application-level guarantee for supported writers only; no DB trigger/constraint added, so privileged out-of-band DML is not prevented"
    },
    "F-C": {
      "verdict": "CLOSED",
      "proven": [
        "inventory rows now carry PaymentOrderId, OriginalOrderCode, nullable OrderCode, ProviderLinkId, State",
        "non-numeric, non-positive and non-canonical codes become UNSUPPORTED_ORDER_CODE with OrderCode=null and scanning continues",
        "unit test proves a valid row after unsupported rows is still classified",
        "real CLI JSON proves one unsupported row with preserved local identity plus a later numeric row",
        "exit 0 documented as report completion, not readiness"
      ]
    },
    "F6": {
      "verdict": "PASS",
      "proven": [
        "import resolves the order and requires an exact PAYOS/PAYMENT transaction matching payment_order_id, tenant, provider link and non-blank checkout url",
        "CASH / foreign / unknown / invalid input and conflicting key cannot create or replace a binding",
        "trusted operator mapping is not treated as provenance proof; signed callback verification remains authoritative",
        "callback invalid before import, valid after apply, tampered rejected, valid after fresh key provider and context",
        "CLI dry-run default, whole-manifest preflight with zero writes, immutable idempotent replay, safe partial retry, bounded pagination and manifest size, private paths outside repo, no automatic key generation, generic error output, malformed input with synthetic secret not echoed",
        "no amount/status mutation and no provider call in the import path"
      ]
    },
    "F7": {
      "verdict": "PASS",
      "proven": [
        "dedicated checkout_signing_bindings with FK RESTRICT, unique payment_order_id, PK order_code, source CHECK and retention comment",
        "no new checkout binding writes into policy_revisions",
        "exact legacy reserved-namespace read bridge and idempotent LEGACY_POLICY migration with unchanged policy row count",
        "different existing legacy key cannot be overwritten through the new table",
        "trusted PaymentOrderId flow with orphan rejection and pre-insert provider-code validation",
        "old policy rows and key material retained; no automatic cleanup",
        "two distinct backend PIDs proven at the outbound boundary with the visibility flag set after verification"
      ]
    },
    "F8": {
      "verdict": "PASS_SOURCE_LEVEL_UNCHANGED",
      "proven": [
        "create and update reject ArrivalLateToleranceEnabled true and false before persistence with the exact field error",
        "row values and UpdatedAt unchanged after rejection; null/omitted valid",
        "MediatR production-like DI with ValidationBehavior exercised",
        "exception handler maps to 400 VALIDATION_FAILED with property-level errors",
        "owner/site enablement flow unchanged"
      ],
      "notRun": "the 4 live HTTP negatives remain NOT RUN"
    }
  },
  "collection": {
    "path": "docs/04-testing/postman/SPARK-340-Admin-Platform-Policies.postman_collection.json",
    "totalRequests": 58,
    "f8Negatives": 4,
    "liveRunStatus": "NOT RUN",
    "liveRunReason": "policy-mutating collection; no agreed test window on a prepared stack while the user runs localhost UAT",
    "historicalNote": "54-request results remain historical only"
  },
  "approvedSchemaChange": {
    "approval": "dedicated table and offline tool approved 17 November 2026",
    "scope": "additive Schema Freeze exception for checkout_signing_bindings + unique index + table comment",
    "script": "scripts/database/upgrades/20261117-payment-checkout-signing-bindings.sql",
    "changedInThisFollowUp": false,
    "appliedToUserDatabase": false,
    "appliedByThisReview": false,
    "automaticStartupMigration": false,
    "evidenceSource": "generated temporary databases only"
  },
  "findings": [
    {
      "id": "G-A",
      "severity": "P4",
      "title": "issuance and inventory disagree on whitespace/signed numeric local codes",
      "files": [
        "src/Services/PaymentService/SmartParking.PaymentService.Application/Integrations/PayOsOrderCodes.cs",
        "src/Services/PaymentService/SmartParking.PaymentService.Application/Integrations/ImportCheckoutBinding.cs"
      ],
      "trigger": "stored local order code such as \" 123\" or \"+123\"",
      "expected": "one canonical rule shared by issuance and inventory",
      "actual": "Resolve parses with NumberStyles.Integer; inventory requires NumberStyles.None plus exact round-trip and labels the same string UNSUPPORTED_ORDER_CODE",
      "impact": "operator sees a row classified unsupported although issuance would accept it; fail-closed both ways",
      "acceptanceCriterion": "F-B single mapping contract / F-C inventory accuracy"
    },
    {
      "id": "G-B",
      "severity": "P4",
      "title": "first DI regression attempt was not HTTP-isolated (process finding)",
      "files": [
        "tests/Services/SmartParking.PaymentService.Tests/PayOsAuthorityHardeningTests.cs"
      ],
      "trigger": "named-client configuration overriding the defaults-level fake handler",
      "expected": "zero-outbound tests must intercept every client",
      "actual": "the failed attempt may have attempted the configured origin with synthetic credentials; corrected with PostConfigureAll<HttpClientFactoryOptions> over all clients, then proved by Calls==1 positive and Calls==0 negative",
      "impact": "no real credentials or user data; residual risk that a similar mis-wiring could emit unnoticed traffic",
      "acceptanceCriterion": "verification hygiene for zero-outbound claims"
    },
    {
      "id": "G-C",
      "severity": "P4",
      "title": "dead timestamp/current-mode key fallback remains in the settings store",
      "files": [
        "src/Services/PaymentService/SmartParking.PaymentService.Infrastructure/Services/PaymentIntegrationSettingsStore.cs:263",
        "src/Services/PaymentService/SmartParking.PaymentService.Application/Common/Interfaces/Ports/IPaymentIntegrationSettings.cs:65"
      ],
      "trigger": "future re-wire of GetTrustedWebhookChecksumKey",
      "expected": "removed or unreachable",
      "actual": "still implemented and interface-exposed, no production caller",
      "impact": "latent reintroduction of a fallback path",
      "acceptanceCriterion": "F7 no current-key/timestamp fallback"
    }
  ],
  "openEarlierFindings": [
    "F-D no immutability trigger",
    "F-E CLI usage errors exit 1 not 2 (now documented)",
    "F-F RequireOutsideRepository passes without a .git ancestor",
    "F-G PG helper .env fallback strips double quotes only",
    "F-H pre-existing non-numeric stored order_code unresolvable at webhook time",
    "F-I platform handlers hardcode null enablement"
  ],
  "notRun": [
    {
      "item": "live Newman 58-request collection",
      "reason": "policy-mutating; no agreed test window"
    },
    {
      "item": "4 F8 HTTP negatives",
      "reason": "require live draft id and admin token from the same window"
    },
    {
      "item": "live owner publish / SITE_OVERRIDE",
      "reason": "would mutate the UAT site policy"
    },
    {
      "item": "DDL / inventory / import against the target Payment database",
      "reason": "outside the approval; temporary databases only"
    },
    {
      "item": "payment restart / force-recreate durability re-check",
      "reason": "would interrupt user UAT"
    },
    {
      "item": "U19 booking snapshot, U06, U11, U20, tester-group sign-off",
      "reason": "unchanged blocked/unrun status"
    }
  ],
  "claimReconciliation": [
    {
      "claim": "1190 passed / 114 skipped",
      "status": "CONFIRMED"
    },
    {
      "claim": "PG 9/9",
      "status": "CONFIRMED"
    },
    {
      "claim": "scoped Node 33/33 historical (no FE/collection change)",
      "status": "CORRECT_REASONING_AND_RERUN_PASSED"
    },
    {
      "claim": "build 0 errors / 29 warnings",
      "status": "ACCURATE_FOR_NO_RESTORE",
      "note": "restored build yields 60 advisory warnings"
    },
    {
      "claim": "options ChecksumKey is never a verification fallback",
      "status": "CONFIRMED"
    },
    {
      "claim": "real checkout requires settings authority and binding",
      "status": "CONFIRMED"
    },
    {
      "claim": "provider code validated from persisted order before conditional INSERT",
      "status": "CONFIRMED"
    },
    {
      "claim": "mixed inventory continues to valid rows",
      "status": "CONFIRMED"
    },
    {
      "claim": "no additional schema change or shared DB execution",
      "status": "CONFIRMED"
    },
    {
      "claim": "first DI regression attempt may have contacted the configured origin with synthetic credentials",
      "status": "DISCLOSED_AND_CORRECTED",
      "seeFinding": "G-B"
    }
  ],
  "uatStatus": {
    "u19": {
      "status": "BLOCKED",
      "owner": "SPARK-273",
      "reason": "commercial producer absent plus guarded-transaction acceptance, in-transaction EnsureOrder, non-terminal creation receipt and unimplemented collection-event confirmation"
    },
    "u06": {
      "status": "NOT_PROVEN"
    },
    "u11": {
      "status": "NOT_EXERCISED"
    },
    "u20": {
      "status": "NOT_EXERCISED"
    },
    "testerGroupSignoff": "OUTSTANDING",
    "note": "User's general positive statement is context only; no per-case UAT PASS inferred."
  },
  "verdict": {
    "faFbFcClosed": true,
    "implementationAcceptable": true,
    "implementationCaveats": [
      "G-A",
      "G-B",
      "G-C",
      "F-D",
      "F-E",
      "F-F",
      "F-G",
      "F-H",
      "F-I"
    ],
    "liveRolloutReady": false,
    "liveRolloutBlockers": [
      "approved upgrade script not applied to the target database",
      "inventory not run against real data; historical checkout disposition unresolved",
      "live 58-request Newman run and 4 F8 HTTP negatives NOT RUN",
      "new binary requires checkout_signing_bindings before serving real checkouts"
    ],
    "p4Closeable": false,
    "p4Blockers": [
      "U19 blocked under SPARK-273",
      "U06/U11/U20 unproven",
      "tester-group sign-off outstanding"
    ],
    "proposalStatus": {
      "dedicatedTableAndOfflineTool": "APPROVED",
      "remainingOrchestrationProposal": "PROPOSED_NOT_DELIVERED"
    }
  },
  "hygiene": {
    "sourceEdited": false,
    "commits": false,
    "pushes": false,
    "resetOrClean": false,
    "dockerRestart": false,
    "ddlAgainstUserDatabase": false,
    "importAgainstUserDatabase": false,
    "appDatabaseOrVolumeDeleted": false,
    "keypairChanged": false,
    "spark238CollectionPreserved": true,
    "historicalReportsPreserved": true
  },
  "artefacts": {
    "report": "docs/04-testing/reports/SPARK-340-FA-FC-independent-review-20261117.md",
    "evidence": "docs/04-testing/reports/evidence/SPARK-340-FA-FC-independent-review-20261117.json",
    "session": "ai/tasks/active/TASK-SPARK-340-SESSION-20261112.md"
  }
}
```

</details>

<a id="evidence-http-history"></a>
<details>
<summary>Historical HTTP collection54 — NOT a run of current58</summary>

Nguồn: [source-045](#source-045), original SHA256 `7fef3283ac31bff5b88b16217b4b5b780f1a49f1f92ef531ac054ffbd9945bce`. Data JSON giữ nguyên giá trị, chỉ chuẩn hóa cách trình bày.

```json
{
  "ticket": "SPARK-340 / SPARK-368 / SPARK-370",
  "documentType": "Independent review + verification evidence (sanitized)",
  "executedAtUtc": "2026-11-17T03:16:33.9483636Z",
  "executedAtLocal": "2026-11-17T10:16:33.9538797+07:00",
  "repoCommit": "055da4591ee9a1822eb9870dc261c9685ca3258b",
  "branch": "feat/SPARK-340-admin-platform-policies",
  "workingTreePorcelainEntries": 115,
  "workingTreeNote": "All SPARK-340/370 artefacts are uncommitted (31 modified + 84 untracked). Reviewed the whole working tree, not only tracked diff.",
  "secretsRedacted": true,
  "note": "No Authorization header, token, credential, connection string or DataProtection key XML is included in this file.",
  "collection": {
    "path": "docs/04-testing/postman/SPARK-340-Admin-Platform-Policies.postman_collection.json",
    "environmentSource": "docs/04-testing/postman/SPARK-340-Admin-Platform-Policies.postman_environment.example.json (populated in a temp file outside the repo)",
    "requestsDefined": 54,
    "requestsExecuted": 48,
    "requestsNotRun": [
      "Submit exactly the reviewed terms for admission",
      "Read admission receipt until this booking is confirmed",
      "Capture raw historical platform settings before activation",
      "Read accepted booking before activation",
      "Read accepted booking after activation",
      "Compare historical raw platform settings after activation"
    ],
    "notRunReason": "Blocked by OperationService IReservationCommercialOwner = UnavailableReservationCommercialOwner (InfrastructureDependencyInjection.cs:49). POST /api/reservation-terms returns HTTP 503 OWNER_UNAVAILABLE \"Reservation commercial producer is not integrated.\" The collection skipRequests all folder-04 dependents.",
    "assertionsPassed": 76,
    "assertionsFailed": 1,
    "assertionsSkipped": 1,
    "testScriptFailures": 0,
    "prerequestScripts": 9,
    "prerequestScriptFailures": 0,
    "newmanExitCode": 1,
    "newmanExitCodeNote": "Non-zero because of the single expected failed assertion on the 503 terms review, not a harness error."
  },
  "perRequest": [
    {
      "name": "Operations policies - Anonymous -> 401",
      "method": "GET",
      "path": "api/v1/admin/policies/operations/catalogue",
      "httpStatus": 401,
      "responseTimeMs": 47,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Operations policies - Driver -> 403",
      "method": "GET",
      "path": "api/v1/admin/policies/operations/catalogue",
      "httpStatus": 403,
      "responseTimeMs": 48,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Operations policies - Owner -> 403",
      "method": "GET",
      "path": "api/v1/admin/policies/operations/catalogue",
      "httpStatus": 403,
      "responseTimeMs": 8,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Operations policies - Admin -> 200",
      "method": "GET",
      "path": "api/v1/admin/policies/operations/catalogue",
      "httpStatus": 200,
      "responseTimeMs": 23,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Payment integration - Anonymous -> 401",
      "method": "GET",
      "path": "api/v1/admin/integrations/payment",
      "httpStatus": 401,
      "responseTimeMs": 75,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Payment integration - Driver -> 403",
      "method": "GET",
      "path": "api/v1/admin/integrations/payment",
      "httpStatus": 403,
      "responseTimeMs": 66,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Payment integration - Owner -> 403",
      "method": "GET",
      "path": "api/v1/admin/integrations/payment",
      "httpStatus": 403,
      "responseTimeMs": 13,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Payment integration - Admin -> 200",
      "method": "GET",
      "path": "api/v1/admin/integrations/payment",
      "httpStatus": 200,
      "responseTimeMs": 20,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Notification integration - Anonymous -> 401",
      "method": "GET",
      "path": "api/v1/admin/integrations/notifications",
      "httpStatus": 401,
      "responseTimeMs": 37,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Notification integration - Driver -> 403",
      "method": "GET",
      "path": "api/v1/admin/integrations/notifications",
      "httpStatus": 403,
      "responseTimeMs": 49,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Notification integration - Owner -> 403",
      "method": "GET",
      "path": "api/v1/admin/integrations/notifications",
      "httpStatus": 403,
      "responseTimeMs": 6,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Notification integration - Admin -> 200",
      "method": "GET",
      "path": "api/v1/admin/integrations/notifications",
      "httpStatus": 200,
      "responseTimeMs": 21,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Catalogue - 14 canonical keys with null vs configured bounds",
      "method": "GET",
      "path": "api/v1/admin/policies/operations/catalogue",
      "httpStatus": 200,
      "responseTimeMs": 7,
      "assertionsPassed": 6,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Create platform draft",
      "method": "POST",
      "path": "api/v1/admin/policies/operations/draft",
      "httpStatus": 201,
      "responseTimeMs": 64,
      "assertionsPassed": 2,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "List revisions - DRAFT contains created draft",
      "method": "GET",
      "path": "api/v1/admin/policies/operations/revisions",
      "httpStatus": 200,
      "responseTimeMs": 19,
      "assertionsPassed": 3,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "List revisions - SQL pagination",
      "method": "GET",
      "path": "api/v1/admin/policies/operations/revisions",
      "httpStatus": 200,
      "responseTimeMs": 8,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Read revision by id - 200",
      "method": "GET",
      "path": "api/v1/admin/policies/operations/revisions/0ab1468a-00f7-4619-bcb9-01ce65c0d01d",
      "httpStatus": 200,
      "responseTimeMs": 14,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Read revision by id - 404",
      "method": "GET",
      "path": "api/v1/admin/policies/operations/revisions/11111111-1111-1111-1111-111111111111",
      "httpStatus": 404,
      "responseTimeMs": 8,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Update draft with If-Match - success",
      "method": "PUT",
      "path": "api/v1/admin/policies/operations/draft/0ab1468a-00f7-4619-bcb9-01ce65c0d01d",
      "httpStatus": 200,
      "responseTimeMs": 27,
      "assertionsPassed": 3,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Update draft with stale If-Match - 409",
      "method": "PUT",
      "path": "api/v1/admin/policies/operations/draft/0ab1468a-00f7-4619-bcb9-01ce65c0d01d",
      "httpStatus": 409,
      "responseTimeMs": 15,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Activate revision - immediate",
      "method": "POST",
      "path": "api/v1/admin/policies/operations/revisions/0ab1468a-00f7-4619-bcb9-01ce65c0d01d/activate",
      "httpStatus": 200,
      "responseTimeMs": 39,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "List revisions - status CURRENT",
      "method": "GET",
      "path": "api/v1/admin/policies/operations/revisions",
      "httpStatus": 200,
      "responseTimeMs": 17,
      "assertionsPassed": 2,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "List revisions - status SCHEDULED",
      "method": "GET",
      "path": "api/v1/admin/policies/operations/revisions",
      "httpStatus": 200,
      "responseTimeMs": 13,
      "assertionsPassed": 2,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "List revisions - status SUPERSEDED",
      "method": "GET",
      "path": "api/v1/admin/policies/operations/revisions",
      "httpStatus": 200,
      "responseTimeMs": 10,
      "assertionsPassed": 2,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Effective policies - PLATFORM provenance",
      "method": "GET",
      "path": "api/v1/admin/policies/operations/effective",
      "httpStatus": 200,
      "responseTimeMs": 19,
      "assertionsPassed": 4,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Effective policies - SITE provenance",
      "method": "GET",
      "path": "api/v1/admin/policies/operations/effective",
      "httpStatus": 200,
      "responseTimeMs": 10,
      "assertionsPassed": 4,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Get payment integration - secrets masked",
      "method": "GET",
      "path": "api/v1/admin/integrations/payment",
      "httpStatus": 200,
      "responseTimeMs": 7,
      "assertionsPassed": 5,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Switch to FIXTURE mode - success",
      "method": "PUT",
      "path": "api/v1/admin/integrations/payment",
      "httpStatus": 200,
      "responseTimeMs": 58,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Disallowed BaseUrl - 400 INVALID_PAYMENT_BASE_URL",
      "method": "PUT",
      "path": "api/v1/admin/integrations/payment",
      "httpStatus": 400,
      "responseTimeMs": 42,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Invalid mode - 400 VALIDATION_FAILED",
      "method": "PUT",
      "path": "api/v1/admin/integrations/payment",
      "httpStatus": 400,
      "responseTimeMs": 21,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Blank credential - 400 VALIDATION_FAILED",
      "method": "PUT",
      "path": "api/v1/admin/integrations/payment",
      "httpStatus": 400,
      "responseTimeMs": 5,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Configure SANDBOX credentials - masked (optional)",
      "method": "PUT",
      "path": "api/v1/admin/integrations/payment",
      "httpStatus": 400,
      "responseTimeMs": 6,
      "assertionsPassed": 0,
      "assertionsFailed": 0,
      "assertionsSkipped": 1,
      "failedAssertions": [],
      "skippedAssertions": [
        "SKIPPED: payment_test_client_id/payment_test_api_key/payment_test_checksum_key are blank; no SANDBOX credentials supplied"
      ]
    },
    {
      "name": "Get notification integrations - 4 fixed channels ACTIVE",
      "method": "GET",
      "path": "api/v1/admin/integrations/notifications",
      "httpStatus": 200,
      "responseTimeMs": 6,
      "assertionsPassed": 4,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Accept reservation terms - capture accepted snapshot",
      "method": "POST",
      "path": "api/reservation-terms",
      "httpStatus": 503,
      "responseTimeMs": 88,
      "assertionsPassed": 0,
      "assertionsFailed": 1,
      "assertionsSkipped": 0,
      "failedAssertions": [
        {
          "assertion": "terms review succeeds",
          "message": "expected response to have status code 200 but got 503"
        }
      ],
      "skippedAssertions": []
    },
    {
      "name": "Create replacement platform draft - change payment hold",
      "method": "POST",
      "path": "api/v1/admin/policies/operations/draft",
      "httpStatus": 201,
      "responseTimeMs": 13,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Activate replacement platform revision",
      "method": "POST",
      "path": "api/v1/admin/policies/operations/revisions/8ec7d40b-231c-4796-82d3-1221acce7e5d/activate",
      "httpStatus": 200,
      "responseTimeMs": 13,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Effective platform reflects the changed revision",
      "method": "GET",
      "path": "api/v1/admin/policies/operations/effective",
      "httpStatus": 200,
      "responseTimeMs": 5,
      "assertionsPassed": 3,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Create scheduled draft - future effectiveFrom fixture",
      "method": "POST",
      "path": "api/v1/admin/policies/operations/draft",
      "httpStatus": 201,
      "responseTimeMs": 11,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Activate scheduled revision - future effectiveFrom",
      "method": "POST",
      "path": "api/v1/admin/policies/operations/revisions/f31f58a2-be93-46a6-9426-9de451b24021/activate",
      "httpStatus": 200,
      "responseTimeMs": 26,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "List revisions - SCHEDULED contains scheduled revision",
      "method": "GET",
      "path": "api/v1/admin/policies/operations/revisions",
      "httpStatus": 200,
      "responseTimeMs": 14,
      "assertionsPassed": 3,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Create overlapping draft - same future effectiveFrom",
      "method": "POST",
      "path": "api/v1/admin/policies/operations/draft",
      "httpStatus": 201,
      "responseTimeMs": 11,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Activate overlapping revision - 409 OVERLAPPING_POLICY_INTERVAL",
      "method": "POST",
      "path": "api/v1/admin/policies/operations/revisions/8ba3eb29-6b54-4740-8f1d-b7805049679b/activate",
      "httpStatus": 409,
      "responseTimeMs": 15,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Owner cannot create platform draft - 403",
      "method": "POST",
      "path": "api/v1/admin/policies/operations/draft",
      "httpStatus": 403,
      "responseTimeMs": 4,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Driver cannot create platform draft - 403",
      "method": "POST",
      "path": "api/v1/admin/policies/operations/draft",
      "httpStatus": 403,
      "responseTimeMs": 9,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Owner cannot update platform draft - 403",
      "method": "PUT",
      "path": "api/v1/admin/policies/operations/draft/0ab1468a-00f7-4619-bcb9-01ce65c0d01d",
      "httpStatus": 403,
      "responseTimeMs": 5,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Driver cannot update platform draft - 403",
      "method": "PUT",
      "path": "api/v1/admin/policies/operations/draft/0ab1468a-00f7-4619-bcb9-01ce65c0d01d",
      "httpStatus": 403,
      "responseTimeMs": 6,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Owner cannot activate platform revision - 403",
      "method": "POST",
      "path": "api/v1/admin/policies/operations/revisions/0ab1468a-00f7-4619-bcb9-01ce65c0d01d/activate",
      "httpStatus": 403,
      "responseTimeMs": 4,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    },
    {
      "name": "Driver cannot activate platform revision - 403",
      "method": "POST",
      "path": "api/v1/admin/policies/operations/revisions/0ab1468a-00f7-4619-bcb9-01ce65c0d01d/activate",
      "httpStatus": 403,
      "responseTimeMs": 6,
      "assertionsPassed": 1,
      "assertionsFailed": 0,
      "assertionsSkipped": 0,
      "failedAssertions": [],
      "skippedAssertions": []
    }
  ],
  "siteOverrideProof": {
    "verified": true,
    "surface": "GET /api/v1/admin/policies/operations/effective?tenantId=&siteId=",
    "scope": "SITE",
    "tenantId": "a0000000-0000-4000-8000-000000000001",
    "siteId": "b0000000-0000-4000-8000-000000000001",
    "overriddenKey": "ARRIVAL_LATE_TOLERANCE",
    "overriddenValue": 20,
    "overriddenSource": "SITE_OVERRIDE",
    "inherited": false,
    "otherKeysSource": "PLATFORM_DEFAULT",
    "otherKeysCount": 13,
    "producedBy": "Owner lot-policy draft + publish through the supported /api/v1/lots/{siteId}/policies endpoint (environment repair 16/11), not by admin platform draft (admin site-scope writes are rejected 403 FORBIDDEN_SCOPE_WRITE by design).",
    "collectionAssertions": "Effective policies - SITE provenance: 4 passed / 0 failed / 0 skipped"
  },
  "platformMutationFromThisRun": {
    "note": "The collection creates and activates PLATFORM revisions; this changed live platform policy state during the agreed test window.",
    "platformRevisionIdAfterRun": "8ec7d40b-231c-4796-82d3-1221acce7e5d",
    "platformVersionAfterRun": 45,
    "effectiveFromAfterRun": "2026-11-17T03:13:37.905235+00:00",
    "catalogueKeys": 14
  },
  "regressionGates": [
    {
      "command": "dotnet build SmartParking.slnx --nologo -m:1",
      "exitCode": 0,
      "errors": 0,
      "warnings": 56,
      "result": "PASS"
    },
    {
      "command": "dotnet test SmartParking.slnx --no-build --nologo -m:1",
      "exitCode": 0,
      "passed": 1161,
      "failed": 0,
      "skipped": 109,
      "total": 1270,
      "result": "PASS"
    },
    {
      "command": "dotnet test tests/Services/SmartParking.PaymentService.Tests --no-build --filter FullyQualifiedName~PaymentIntegrationPostgresConcurrencyTests",
      "exitCode": 0,
      "passed": 4,
      "failed": 0,
      "skipped": 0,
      "total": 4,
      "database": "real PostgreSQL, fixture-created temp database",
      "result": "PASS"
    },
    {
      "command": "dotnet test tests/Services/SmartParking.OperationService.Tests --no-build --filter FullyQualifiedName~PlatformPolicyPostgresConcurrencyTests",
      "exitCode": 0,
      "passed": 3,
      "failed": 0,
      "skipped": 0,
      "total": 3,
      "database": "real PostgreSQL, fixture-created temp database",
      "result": "PASS"
    },
    {
      "command": "node --test tests/postman/spark340-collection.test.cjs",
      "exitCode": 0,
      "passed": 2,
      "failed": 0,
      "skipped": 0,
      "result": "PASS"
    },
    {
      "command": "npm --prefix FE run build",
      "exitCode": 0,
      "result": "PASS",
      "note": "built in 10.01s"
    },
    {
      "command": "node --test tests/frontend/*.test.cjs",
      "exitCode": 0,
      "passed": 261,
      "failed": 0,
      "skipped": 0,
      "result": "PASS"
    }
  ],
  "paymentPersistenceAcrossRestart": {
    "performedInThisReview": true,
    "restartCommand": "docker compose -f compose.app.yml restart payment-service",
    "forceRecreateCommand": "docker compose -f compose.app.yml up -d --no-deps --force-recreate payment-service",
    "healthAfterRestart": "running/healthy in 6s",
    "healthAfterForceRecreate": "running/healthy in 6s",
    "modeBefore": "FIXTURE",
    "modeAfterRestart": "FIXTURE",
    "modeAfterForceRecreate": "FIXTURE",
    "isConfiguredAfterForceRecreate": true,
    "statusAfterForceRecreate": "HEALTHY",
    "decryptionEvidence": "The integration state returned after both restarts is decrypted from the persisted payment history revision using the volume-backed DataProtection key ring; a lost or wrong key would have failed hydration (fail-closed) instead of returning the stored mode/baseUrl.",
    "volumeMount": "smartpark-app_payment-data-protection-keys -> /app/.keys/payment (unchanged across force-recreate)",
    "keyFileCount": 1,
    "keyXmlPrinted": false,
    "appDatabaseVolumeDeleted": false,
    "logErrorsAfterRecreate": "none matched (docker logs --tail 200)"
  },
  "codeReviewFindings": [
    {
      "severity": "P1",
      "preExisting": true,
      "title": "Production Payment API trusts a repo-committed symmetric JWT signing key",
      "file": "src/Services/PaymentService/SmartParking.PaymentService.API/appsettings.json:23",
      "trigger": "Non-Development start with Jwt:SecretKey from appsettings (compose overrides Issuer only)",
      "expected": "RS256/JWKS-only trust for UserService tokens",
      "actual": "SymmetricSecurityKey configured unconditionally in Program.cs:56-72; any holder of the public repo key can mint HS256 tokens for iss=SmartParking.UserService",
      "impact": "Impersonation of any role including money-bearing internal endpoints",
      "criterion": "coding skill security: no insecure symmetric/default trust. Not introduced by this branch (file is unmodified; literal key is pre-existing tracked content)."
    },
    {
      "severity": "P2",
      "title": "Deposit-order endpoints remain on the loose default scheme",
      "file": "InternalReservationOrdersController.cs:11 + Payment Program.cs:96-107",
      "trigger": "Any caller presenting a SERVICE/OPERATIONS_WORKER/INTERNAL_SERVICE role or scope=s2s claim; in Development any nonblank X-Internal-Service-Key",
      "expected": "Dedicated Operations identity with Payment-specific audience and narrow reservation permission",
      "actual": "Broad policy over a symmetric-key-capable default scheme; DevAuthenticationHandler accepts any Bearer string and spoofable X-Actor-* headers",
      "impact": "Over-privileged machine access to order creation/replay and collection lookup",
      "criterion": "11/11 decision §3/§5, producer handoff §28"
    },
    {
      "severity": "P2",
      "title": "EnsureOrder does not bind the full immutable quote",
      "file": "EnsureReservationDepositOrderCommandHandler.cs:94-127",
      "trigger": "PUT /internal/reservation-deposit-orders/{operationId} with a quote whose site/tenant/currency/terms differ but amount matches",
      "expected": "Compare all price inputs and require H <= quote.ValidUntil",
      "actual": "Only Status, AccountId, DepositAmount (and expiry) checked; replay compares ReferenceId+AccountId+Amount only",
      "impact": "Order can be bound to wrong site/tenant/currency; same operationId with different scope silently returns the original order",
      "criterion": "decision §3 quote-binding requirement"
    },
    {
      "severity": "P2",
      "title": "Concurrent same-idempotency-key EnsureOrder returns 500",
      "file": "EnsureReservationDepositOrderCommandHandler.cs:40,149-150 + PaymentExceptionHandler default",
      "trigger": "Two concurrent PUTs with the same operationId",
      "expected": "Replay the same order (or 409 on payload mismatch)",
      "actual": "Read-then-insert without transaction/advisory lock; unique violation surfaces as 500 INTERNAL_SERVER_ERROR",
      "impact": "Caller cannot distinguish created from transient failure; may strand a paid hold",
      "criterion": "idempotency contract"
    },
    {
      "severity": "P2",
      "title": "Quote acceptance is not serialized",
      "file": "EnsureReservationDepositOrderCommandHandler.cs:94,107,126,150",
      "trigger": "Two different operationIds for one quoteId",
      "expected": "One order per accepted quote",
      "actual": "No FOR UPDATE, no unique constraint on payment_orders.quoteId -> two orders; impact double-charge risk",
      "criterion": "followup proposal §42 / decision §3"
    },
    {
      "severity": "P2",
      "title": "Deploying this branch rejects webhooks for pre-existing checkouts",
      "file": "PayOsAdapter.cs:388-399 (and dead fallback PaymentIntegrationSettingsStore.cs:263-307)",
      "trigger": "PayOS callback for an order created before the binding feature deployed",
      "expected": "Record the collection (or migrate/backfill bindings)",
      "actual": "Missing binding -> InvalidCallback 401 INVALID_WEBHOOK_SIGNATURE on every retry",
      "impact": "Paid orders never confirmed without manual reconciliation",
      "criterion": "each verified collection must be recorded"
    },
    {
      "severity": "P3",
      "title": "Checkout signing bindings stored in the pricing policy_revisions table",
      "file": "CheckoutBindingRepository.cs:10-21",
      "trigger": "Every fixed-price checkout",
      "expected": "Dedicated owner for signing bindings",
      "actual": "One permanent TARIFF/GLOBAL row per order code in the pricing table",
      "impact": "Unbounded growth in a pricing-owned table; a future prune deletes live signing bindings; contradicts the followup proposal not to overload policy history",
      "criterion": "coding skill §9 / followup §44"
    },
    {
      "severity": "P3",
      "title": "arrivalLateToleranceEnabled accepted then silently discarded",
      "file": "AdminPolicyContracts.cs:75,96; CreatePolicyDraft.cs:50; UpdatePolicyDraft.cs:54",
      "trigger": "POST/PUT platform draft including the field",
      "expected": "Reject non-null for platform scope or remove from contract",
      "actual": "201/200 returned while the value is hardcoded null",
      "impact": "Client, Postman collection and FE modal all send it; false success",
      "criterion": "no unsupported write should report success"
    },
    {
      "severity": "P4",
      "title": "Platform draft version allocation is check-then-insert",
      "file": "ReservationPolicyRepository.cs:137-149",
      "trigger": "Two concurrent admins creating platform drafts",
      "expected": "Retryable conflict",
      "actual": "Unique violation surfaces as generic 409 RESOURCE_CONFLICT with no retry hint",
      "impact": "Misleading error, no corruption"
    },
    {
      "severity": "P4",
      "title": "Mixed clocks at policy time boundaries",
      "file": "ReservationPolicyRepository.cs:41,94,134,177,247",
      "trigger": "effectiveFrom close to now",
      "expected": "Single authoritative clock",
      "actual": "App clock for reads vs DB CURRENT_TIMESTAMP for writes",
      "impact": "Boundary value can differ by skew"
    },
    {
      "severity": "P4",
      "title": "Admin endpoints return 500 when authentication is unconfigured",
      "file": "Operation Program.cs:32,103 + PlatformPoliciesController.cs:12",
      "trigger": "Admission/JWT validation disabled",
      "expected": "401/503 uniform envelope",
      "actual": "No authentication scheme -> 500"
    },
    {
      "severity": "P4",
      "title": "Fixture notification providers reported as ready/ACTIVE",
      "file": "GetAdminNotificationIntegrationsQuery.cs:30-68",
      "trigger": "LOCAL_*_FIXTURE providers",
      "expected": "Clearly non-production label",
      "actual": "IsReady=true, Status=ACTIVE",
      "impact": "Reads as integration-ready during UAT"
    }
  ],
  "testSensitivityVerdicts": {
    "checkoutBindingOutboundOrderingTests": "PROVEN for all three tests. Binding moved after SendAsync fails each test (blocked-store timeout / missing read / Calls==0 assert). Un-awaited store fails deterministically. Storage failure with outbound traffic is caught by Assert.Equal(0, handler.Calls). A swallowed assertion cannot become a false PASS because XunitException is neither HttpRequestException nor OperationCanceledException (PayOsAdapter.cs:306-307) and the external flags are set after the assertion and then asserted.",
    "pgIndependentConnectionTest": "PARTIALLY PROVEN. Ordering and decryption at the outbound boundary are proven (fresh PaymentDbContext + fresh CheckoutSigningBindings inside SendAsync). Commit isolation and connection independence are NOT discriminated: the reader rents from the same Npgsql pool, there is no pg_backend_pid inequality, no Pooling=false and no pre-opened independent connection, so a never-committed transaction would still pass. Rename to ...ReadableByFreshContext... or force a distinct connection.",
    "postmanTermsGuardTest": "PROVEN. With snapshot_fixture_ready=true and a 503 response, the script produces exactly one outcome named \"terms review succeeds\" with passed=false, and environment.set is never called, so accepted terms cannot be captured from an error response.",
    "liveTermsRecheck": "Executed through the real collection against the running services: HTTP 503, 1 failed assertion, 0 test-script failures, 0 prerequest failures."
  },
  "uatReconciliation": [
    {
      "case": "U04",
      "oldClaim": "two-window save -> 409",
      "correctedEvidence": "The applicable 409 is the stale If-Match / expectedUpdatedAt path: RESOURCE_VERSION_CONFLICT. Verified live: \"Update draft with stale If-Match - 409\" passed (1 assertion). overlap 409 (OVERLAPPING_POLICY_INTERVAL) proves U07, not U04.",
      "status": "PASS (HTTP, If-Match path)"
    },
    {
      "case": "U06",
      "oldClaim": "scheduled activation before/at/after effectiveFrom",
      "correctedEvidence": "No automated test asserts effective VALUES before/at/after effectiveFrom. Existing tests cover revision classification from a fixed clock (PlatformPoliciesTests.cs:266) and owner publish scheduling (:441,:475). The live collection proves create+activate+listing of a SCHEDULED revision only.",
      "status": "PARTIAL / NOT PROVEN for boundary values"
    },
    {
      "case": "U07",
      "oldClaim": "overlap 409",
      "correctedEvidence": "\"Activate overlapping revision - 409 OVERLAPPING_POLICY_INTERVAL\" passed live.",
      "status": "PASS (HTTP)"
    },
    {
      "case": "U08",
      "oldClaim": "effective PLATFORM then SITE",
      "correctedEvidence": "Both requests passed. SITE scope shows ARRIVAL_LATE_TOLERANCE=20 as SITE_OVERRIDE and 13 keys as PLATFORM_DEFAULT.",
      "status": "PASS (HTTP, real SITE_OVERRIDE)"
    },
    {
      "case": "U11",
      "oldClaim": "site max 60 + admin min 90 -> activation rejected",
      "correctedEvidence": "Not executed in this review; no such fixture was built and no test covers it.",
      "status": "NOT EXERCISED"
    },
    {
      "case": "U19",
      "oldClaim": "confirmed booking snapshot",
      "correctedEvidence": "Blocked. Terms review returns 503 because IReservationCommercialOwner is the UnavailableReservationCommercialOwner stub. Policy-revision immutability and checkout-binding durability are separate proofs and must not be presented as booking-snapshot proof.",
      "status": "BLOCKED"
    },
    {
      "case": "U20",
      "oldClaim": "responsive",
      "correctedEvidence": "No automated backend evidence; requires the tester group on a real UI.",
      "status": "NOT EXERCISED"
    }
  ],
  "u19Assessment": {
    "verdict": "BLOCKED",
    "owner": "SPARK-273 (reservation producer integration)",
    "notMerelyMissingAdapter": true,
    "decisionViolations": [
      "ProgressReservationHandler.PrepareAsync calls commercial.AcceptAsync (BookingProgressHandler.cs:117) inside the guarded transaction and before the hold commit (commit at :49) — violates D11-A/D11-F external-call-outside-transaction.",
      "ProgressBookingAsync calls EnsureOrderAsync (BookingProgressHandler.cs:154) inside the same transaction and under site/vehicle locks (:91-93).",
      "The creation receipt is not terminal at the hold: AttachBooking sets state BOOKING (ReservationRequest.cs:43-48) and only completion sets COMPLETED (:241); no HOLD_CREATED outcome exists."
    ],
    "missingCapabilities": [
      "Event-driven confirmation: no ReservationDepositCollectedV1; Payment emits generic PaymentSucceededV1 and Operations has no reservation-collection consumer.",
      "Immutable coverage evidence: CoverageVersion is validated only as non-blank (BookingReviewHandlers.cs:137); Payment never receives coverage.",
      "Durable Operations-side order intent before the financial call (no operation_id column; UNIQUE order/collection columns exist at DDL:296-298).",
      "Recovery: per-second CollectionsAsync polling, no inbox/dedup, no durable operationId -> outcome record."
    ],
    "forbiddenShortcuts": [
      "fake quote/accept producer",
      "separate acceptance HTTP endpoint",
      "dropping the Facility confirmation dependency without a writer-invariant proof"
    ],
    "proposalsStatus": "All five orchestration/contract proposals in SPARK-340-review-followup-20261117.md remain PROPOSED, not implemented. They must not be cited as delivered."
  },
  "p4Conclusion": "P4 is NOT closeable. U19 is BLOCKED, the four working-tree regression gates pass, and the only failing Newman assertion is the known 503 commercial-producer block. Tester-group UAT sign-off is still pending.",
  "historicalReportsPreserved": true
}
```

</details>

<a id="source-archive"></a>
## 11. Danh mục nguồn và khôi phục lịch sử

47 nguồn cũ đã được kiểm tra khớp blob tại commit `1bedfdae1b25218d0c7b728fa57087cec1ce8dab` trước khi xóa file rời. SHA256 trong bảng tính trên bytes của Git snapshot, không phải whitespace mới của report tổng hợp. Full nội dung từng nguồn vẫn truy xuất được qua immutable Git links; không rewrite Git history. Local backup của working bytes và manifest nằm trong `.cache/` ignored. Các tên file cũ trong session lịch sử là source identifiers, không phải yêu cầu tạo lại file.

<details>
<summary>Mở danh mục47 nguồn và immutable Git snapshots</summary>

| ID | Nguồn gốc trong Git | SHA256 original snapshot |
|---|---|---|
| <a id="source-001"></a>source-001 | [docs/03-design/api/admin-platform-policies-and-integrations-spark-340-review-v1.1.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/03-design/api/admin-platform-policies-and-integrations-spark-340-review-v1.1.md) | `ea6d181653220fa2ec0ffc00d289af315974a92691db68511785cee63a1b5759` |
| <a id="source-002"></a>source-002 | [docs/03-design/api/admin-platform-policies-and-integrations-spark-340-review-v1.2.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/03-design/api/admin-platform-policies-and-integrations-spark-340-review-v1.2.md) | `7c5c6389426559025927d6275ee77c154b0c76cc072100f281c662b0c9b94fa4` |
| <a id="source-003"></a>source-003 | [docs/03-design/api/admin-platform-policies-and-integrations-spark-340-review-v1.3.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/03-design/api/admin-platform-policies-and-integrations-spark-340-review-v1.3.md) | `fe284b516299617ead41b11749361234aaf98cb221ebbb5ed9edf3278381f399` |
| <a id="source-004"></a>source-004 | [docs/03-design/api/admin-platform-policies-and-integrations-spark-340-review-v1.4.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/03-design/api/admin-platform-policies-and-integrations-spark-340-review-v1.4.md) | `59955a12fc48e0318343c30b7600c698f5a1b59386f82266d7ffb33c3b831b2d` |
| <a id="source-005"></a>source-005 | [docs/03-design/api/admin-platform-policies-and-integrations-spark-340-review-v1.5.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/03-design/api/admin-platform-policies-and-integrations-spark-340-review-v1.5.md) | `1183d89b6215917a1d2b59962412c0b03148858fceae0a4137fc0202c5ef2645` |
| <a id="source-006"></a>source-006 | [docs/03-design/api/admin-platform-policies-and-integrations-spark-340-review-v1.6-final.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/03-design/api/admin-platform-policies-and-integrations-spark-340-review-v1.6-final.md) | `51ee39c112ad9ace0a46075b8b23bf288fd0ca942e9989167ac482980e880fe7` |
| <a id="source-007"></a>source-007 | [docs/03-design/api/admin-platform-policies-and-integrations-spark-340-review.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/03-design/api/admin-platform-policies-and-integrations-spark-340-review.md) | `18118734ca40d215afcd566b13176bf4379ac628fdfa735f371434bcbb401ebf` |
| <a id="source-008"></a>source-008 | [docs/04-testing/reports/SPARK-340-F6-F8-implementation-20261117.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/SPARK-340-F6-F8-implementation-20261117.md) | `68ad663e0638bcd2faa30b20f67be70a93a24b22b8d0179c2b9cdb9c49c2b3fe` |
| <a id="source-009"></a>source-009 | [docs/04-testing/reports/SPARK-340-F6-F8-independent-review-20261117.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/SPARK-340-F6-F8-independent-review-20261117.md) | `6061d62f5fc6706958caa3da7fe880fdb04ce82e455f9d84c7865111696c5754` |
| <a id="source-010"></a>source-010 | [docs/04-testing/reports/SPARK-340-F6-F8-reviewer-prompt.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/SPARK-340-F6-F8-reviewer-prompt.md) | `9efc3a1283c6e89b68645ec64a73f950d83971f8deedd0ea17397dc8bc0abbdc` |
| <a id="source-011"></a>source-011 | [docs/04-testing/reports/SPARK-340-F6-F8-rollout-guide.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/SPARK-340-F6-F8-rollout-guide.md) | `512cbba763d8b237155b790e41a86e1d21824cb3e02f4e24b6c1fc4eefef72ef` |
| <a id="source-012"></a>source-012 | [docs/04-testing/reports/SPARK-340-FA-FC-implementation-20261117.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/SPARK-340-FA-FC-implementation-20261117.md) | `388c7f2004a3b27296cbe2a07f81434839e20899f73b83dac628f3e0ea2ee8cc` |
| <a id="source-013"></a>source-013 | [docs/04-testing/reports/SPARK-340-FA-FC-independent-review-20261117.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/SPARK-340-FA-FC-independent-review-20261117.md) | `d9f3baaf2f73276d6e0cfd46ffef70f84f851672e48176e671e3a00a98239c70` |
| <a id="source-014"></a>source-014 | [docs/04-testing/reports/SPARK-340-P4-fix-verification-20261114.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/SPARK-340-P4-fix-verification-20261114.md) | `994aaf9a8a396037df7421ab1dd3ed1085ef78d7cf1615ff427bb2ddb41300d3` |
| <a id="source-015"></a>source-015 | [docs/04-testing/reports/SPARK-340-P5-closure-20261118.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/SPARK-340-P5-closure-20261118.md) | `020587024440a1b1a1cf68174f6a0a99ab51e79879840cd2764772b8eba80bc1` |
| <a id="source-016"></a>source-016 | [docs/04-testing/reports/SPARK-340-P5-review-20261117.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/SPARK-340-P5-review-20261117.md) | `05ab2f1ddea793d46bcad8217b7b1425f10f8683f9bf4591a974c864f402dbda` |
| <a id="source-017"></a>source-017 | [docs/04-testing/reports/SPARK-340-P6-delivery-20261117.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/SPARK-340-P6-delivery-20261117.md) | `fa0376e1620d62d333bddd1c033daa1725acad69c9b7030f632578a3f9172dde` |
| <a id="source-018"></a>source-018 | [docs/04-testing/reports/SPARK-340-PR-draft.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/SPARK-340-PR-draft.md) | `f05e5e72550ba2d3f96c5371881d463194604b3bd30208335dfa35a82515fdb5` |
| <a id="source-019"></a>source-019 | [docs/04-testing/reports/SPARK-340-UAT-environment-repair-20261116.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/SPARK-340-UAT-environment-repair-20261116.md) | `5061093f66b318c5095fe9d4b5aa3352105547f1a41bc3a2ced2d3f89485547c` |
| <a id="source-020"></a>source-020 | [docs/04-testing/reports/SPARK-340-fixes-and-handoff-20261116.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/SPARK-340-fixes-and-handoff-20261116.md) | `bdf0b913289a9226a4410e4c1d97f2eeccf3f8e801657e921903d2d11b9c9d98` |
| <a id="source-021"></a>source-021 | [docs/04-testing/reports/SPARK-340-localhost-UAT-checklist.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/SPARK-340-localhost-UAT-checklist.md) | `65c24363077b9112df44d6e4b95a7c45d9077b3d62025a52d7399d214a50c0f6` |
| <a id="source-022"></a>source-022 | [docs/04-testing/reports/SPARK-340-pre-PR-full-test-prompt-20261118.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/SPARK-340-pre-PR-full-test-prompt-20261118.md) | `833dbf5a3e370afddc594316e9084e3df4fd944d528c75253b2c5e6af346f077` |
| <a id="source-023"></a>source-023 | [docs/04-testing/reports/SPARK-340-review-followup-20261117.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/SPARK-340-review-followup-20261117.md) | `1f2f4e193d38b966ab17d81caaee4e616657b34b4fc98586a0f417f6b4a775fa` |
| <a id="source-024"></a>source-024 | [docs/04-testing/reports/SPARK-340-tester-handoff.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/SPARK-340-tester-handoff.md) | `82ae695d0b9595c5b5cfdfd3dac1d67642591ac7e0a1d2c3486dd52ff4f4ce75` |
| <a id="source-025"></a>source-025 | [docs/04-testing/reports/SPARK-370-P4-independent-verification-20261114.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/SPARK-370-P4-independent-verification-20261114.md) | `ca5cf6c8fb61b62c2f6e73bf21945cfa8fd49e8bb44a0b7324028a0d6523980c` |
| <a id="source-026"></a>source-026 | [docs/04-testing/reports/SPARK-370-independent-reverification-2026-11-17.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/SPARK-370-independent-reverification-2026-11-17.md) | `7794567c55d29f5ea520d80301e800a3ba58576f89e0805aef87bc06a0f5028a` |
| <a id="source-027"></a>source-027 | [docs/04-testing/reports/SPARK-370-independent-review-20261116.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/SPARK-370-independent-review-20261116.md) | `27448e17fb30691b8124bebd6ddf066b97bc185eaece1e4926f685314686087f` |
| <a id="source-028"></a>source-028 | [docs/04-testing/reports/SPARK-370-independent-review-20261117-round2.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/SPARK-370-independent-review-20261117-round2.md) | `1f6da4c0ef2e110276d5fe9bb6852a9083ccf69edee2bf6d842c0e139462d485` |
| <a id="source-029"></a>source-029 | [docs/04-testing/reports/SPARK-370-independent-test-agent-prompt.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/SPARK-370-independent-test-agent-prompt.md) | `3077152965b12f48fe974e1cf8f053a53e9e0eece40f0e5abfa94dd7cbecb835` |
| <a id="source-030"></a>source-030 | [docs/04-testing/reports/SPARK-370-independent-verification-20261116b.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/SPARK-370-independent-verification-20261116b.md) | `a2bf3821aae5d8317d9161d26651e7e06692b15127cbd225ea5e2eb785abe9c8` |
| <a id="source-031"></a>source-031 | [docs/04-testing/reports/SPARK-370-reviewer-after-followup-20261117.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/SPARK-370-reviewer-after-followup-20261117.md) | `0e96451c4c6d37e1511b925337ac834d2dc0081dce9a5676e36f55f07a455cf8` |
| <a id="source-032"></a>source-032 | [docs/04-testing/reports/evidence/SPARK-340-F6-F8-independent-review-20261117.json](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/evidence/SPARK-340-F6-F8-independent-review-20261117.json) | `e7807ecea243573d3d496e2b01a0e60872e48fa92e0cf62b6b4436ab2d73613a` |
| <a id="source-033"></a>source-033 | [docs/04-testing/reports/evidence/SPARK-340-F6-F8-verification-20261117.json](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/evidence/SPARK-340-F6-F8-verification-20261117.json) | `78906621c7844ba8b3bdafdb679046860a2511045c8ebd326313339b2ac54ff1` |
| <a id="source-034"></a>source-034 | [docs/04-testing/reports/evidence/SPARK-340-FA-FC-independent-review-20261117.json](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/evidence/SPARK-340-FA-FC-independent-review-20261117.json) | `0a9d5398b9eacf4fc0fb8c9a8c12d1eb5fb41627297c6286f2311511cd95da9e` |
| <a id="source-035"></a>source-035 | [docs/04-testing/reports/evidence/SPARK-340-FA-FC-verification-20261117.json](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/evidence/SPARK-340-FA-FC-verification-20261117.json) | `723b18763d9a7cd1fb2b72690b640343d220ea6725a8a9935576afbe832ee841` |
| <a id="source-036"></a>source-036 | [docs/04-testing/reports/evidence/SPARK-340-P5-source-audit-20261117.json](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/evidence/SPARK-340-P5-source-audit-20261117.json) | `e8de94e57bcbb80650e8419f0915c791243d7ce8bd5c993507952af36865956c` |
| <a id="source-037"></a>source-037 | [docs/04-testing/reports/evidence/SPARK-340-P6-verification-20261117.json](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/evidence/SPARK-340-P6-verification-20261117.json) | `85f8c0b58b01837ce627ac61db67f0fb883ecede3235b929e2497eb906ee1a45` |
| <a id="source-038"></a>source-038 | [docs/04-testing/reports/evidence/SPARK-340-tester-publication-20261117.json](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/evidence/SPARK-340-tester-publication-20261117.json) | `16fc98e911ab587757db479674706464c6bb27c280508fd1931fb005cf7e8fec` |
| <a id="source-039"></a>source-039 | [docs/04-testing/reports/evidence/SPARK-370-Newman-UAT-Result.json](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/evidence/SPARK-370-Newman-UAT-Result.json) | `c8a3672c353140d4836d18d4797b114c362a88a67c9d3f896be743e98858d885` |
| <a id="source-040"></a>source-040 | [docs/04-testing/reports/evidence/SPARK-370-environment-repair-20261116.json](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/evidence/SPARK-370-environment-repair-20261116.json) | `62677b8f23d23723550366f17367ac2e47643d20853a258501a0ad4abbdfec96` |
| <a id="source-041"></a>source-041 | [docs/04-testing/reports/evidence/SPARK-370-fix-Newman-20261116.json](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/evidence/SPARK-370-fix-Newman-20261116.json) | `4c37919f0b16d31365629c47976ac1439ffc85a798095d4cf619aaf85146c147` |
| <a id="source-042"></a>source-042 | [docs/04-testing/reports/evidence/SPARK-370-independent-Newman-20261116-tokenfresh.json](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/evidence/SPARK-370-independent-Newman-20261116-tokenfresh.json) | `2e551f93ed36380e5ead3aa3bfb02de503f29795f51767170495df26e992d90b` |
| <a id="source-043"></a>source-043 | [docs/04-testing/reports/evidence/SPARK-370-independent-Newman-20261116.json](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/evidence/SPARK-370-independent-Newman-20261116.json) | `faf2ffde223369e92e8227a8e3871137dc0f22c673c6c0d94d12ed0d6265e150` |
| <a id="source-044"></a>source-044 | [docs/04-testing/reports/evidence/SPARK-370-independent-reverification-2026-11-17.json](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/evidence/SPARK-370-independent-reverification-2026-11-17.json) | `505bf08213b869afd1d5afcd57a34bccb9b14a4b6ff28085cb7b63767a0ea0e5` |
| <a id="source-045"></a>source-045 | [docs/04-testing/reports/evidence/SPARK-370-independent-review-20261117-round2.json](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/evidence/SPARK-370-independent-review-20261117-round2.json) | `7fef3283ac31bff5b88b16217b4b5b780f1a49f1f92ef531ac054ffbd9945bce` |
| <a id="source-046"></a>source-046 | [docs/04-testing/reports/evidence/SPARK-370-repaired-environment-Newman-20261116.json](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/evidence/SPARK-370-repaired-environment-Newman-20261116.json) | `fc0c1648bebeedbedfbc39de7f307f1aad6b6d3364bfa9cbc197fe8081b84536` |
| <a id="source-047"></a>source-047 | [docs/04-testing/reports/evidence/SPARK-370-terms-guard-recheck-20261117.json](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/1bedfdae1b25218d0c7b728fa57087cec1ce8dab/docs/04-testing/reports/evidence/SPARK-370-terms-guard-recheck-20261117.json) | `ade3181b0fd83a30a3e5c1678935cd9c9dca85581a1ad40dbe2f4c85eb35ffe8` |

</details>

Có thể đọc source bất kỳ mà không đổi working tree bằng `git show 1bedfdae1b25218d0c7b728fa57087cec1ce8dab:<path-trong-bang>`. Không reset/checkout cả branch để xem lịch sử. Những bản cũ có claim đã bị correction phải được đọc trong bối cảnh snapshot; section1–5 ở đầu report mới là trạng thái tổng hợp hiện hành.

<a id="future-updates"></a>
## 12. Quy tắc cập nhật từ đây

Chỉ cập nhật report này cho SPARK-340/370: thêm mốc ngày/commit/commands, actual counts, per-case UAT và verdict. Chèn JSON sanitized trong fenced block hoặc details của chính mốc run, phân biệt history/current và PASS/FAIL/SKIP/NOT RUN/BLOCKED. Không tạo lại các dated report/evidence files đã gom. Raw Newman export/private environment/key files giữ ngoài Git; không trỏ CLI export vào file Markdown này. Session chỉ ghi checkpoint và link tới section tương ứng. API contracts/approved decisions và executable collection/tests tiếp tục ở các vị trí gốc.
