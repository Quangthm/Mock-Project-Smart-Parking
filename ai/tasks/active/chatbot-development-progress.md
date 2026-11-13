# AI Chatbot — Quá trình phát triển backend

## Review bàn giao tester — 13/11/2026 (hoàn tất review/fix và tài liệu; nghiệm thu đích còn mở)

- Yêu cầu: review cấu trúc, toàn bộ script/Markdown và AIChatbot; sửa setup Docker/env; gom hướng dẫn QA đầy đủ, dữ liệu và test case từng role; xóa tài liệu dư có căn cứ.
- Baseline: branch `AI-ChatBot`, HEAD `ac59552d799f41bcce574c53ba8e6795bb761c3b`. Working tree đã có thay đổi env, FE, Docker/setup/seed, Operations/Payment/User tests cùng discovery compose/scripts chưa tracked. Giữ toàn bộ thay đổi trước phiên, không commit/reset.
- Áp dụng coding skill qua router; env/session theo `ai/rule.md`. Ba reviewer độc lập GPT-6.1 Sol: backend assistant; scripts/compose/env/seed; frontend chatbot. Parent phụ trách tài liệu và kiểm chứng tổng hợp.
- Preflight dọn tài liệu: chỉ bỏ hướng dẫn trùng sau khi nhập đủ nội dung hữu ích vào `docs/04-testing/assistant-handoff-testing.md` và sửa liên kết. Giữ requirements, contracts, source knowledge, archive quyết định và bằng chứng lịch sử. Người dùng đã yêu cầu xóa Markdown dư; không xóa DB/volume hoặc file cấu hình cá nhân.
- Kiểm chứng hiện tại: [UNVERIFIED], chưa dùng số liệu các phiên trước làm bằng chứng phiên này. Không tự xuất bản corpus draft hoặc tạo phê duyệt nghiệp vụ.
- Bước tiếp: đối chiếu source/cấu hình, chạy test phù hợp và ghi readiness theo từng phạm vi; không hứa booking/payment chatbot khi chưa có luồng thực thi.

### Kết quả cuối của đợt review 13/11

- Ba subagent GPT-6.1 Sol review độc lập backend, FE, scripts/setup; reviewer FE kiểm lại fix race backend và tài liệu QA. Đã xử lý các finding, gồm field capabilities, role, test provider cần cô lập candidate, admission prerequisite và seed không có approval publication.
- Sửa backend management race: tắt global trong generation → cancel, đổi revision → RETRIEVAL_UNAVAILABLE, không lưu late answer. Hai HTTP regression test. Guest widget hiển thị lỗi an toàn/dễ hiểu.
- Setup: thêm initializer local fresh và `-UseExistingSigningKey` cho add-on giữ nguyên JWT FILE/PEM/Base64; đồng bộ env-relative path ở setup/host/Docker; bảo vệ key cũ; tạo TLS/keyring riêng; sửa host listeners, fresh Facility discovery init, capacity seed, duplicate env keys và PS5.1 UTF-8 fixtures. `.env` cá nhân không sửa.
- Handoff QA đã viết lại: máy trắng/add-on, nhóm env, provider/corpus, seed và manual onboarding, 76 case ID theo Guest/Driver/Admin/Owner/Operator, negative authorization và ranh giới draft/read/write. LOCAL-SETUP/QUICK_START/maintenance/env skill cập nhật cùng workflow.
- Dọn docs: đã backup SHA256 `71c7a11412c42af42148516cfd5354585e5ab891f6d6bd3f7486c5e317e870c9` rồi gộp/xóa checklist leader trùng, sửa incoming links. Inventory 412 → 411 Markdown; 197 → 193 missing file links (còn phần lớn archive ngoài scope), không có link file hỏng trong docs thay đổi; anchor guide và 76 ID duy nhất đạt. Không xóa archive quyết định/requirements/source knowledge.
- Validation: solution build 0 error; full tests 1607 pass / 0 fail / 109 skip (IoT 518/0/7). PostgreSQL harness riêng 6/6 pass, migration replay + populated upgrade + backup/restore đạt; worker con là skip thiết kế. FE build đạt; targeted chatbot 67/67, full Node frontend 298/298. Docker Assistant fixture thực đạt PS7 và Windows PS5.1; fresh Facility discovery fixture đạt; Compose app/schema/discovery config quiet đạt. Setup/check/runner/listener/discovery/migration-plan/initializer fixtures đạt; final initializer add-on FILE/PEM/Base64 preserve + mismatch/missing/placeholder reject đạt.
- Giới hạn: build còn NU1510/NU1900/NU1902/NU1903, không tự nâng packages. Chưa test live browser với UserService/provider thật hoặc full clean-machine onboarding. Capacity source còn unknown, reservation reads cần admission/JWKS của owner; chatbot không tạo booking/payment. Seed DEMO-HQ thiếu finalized onboarding evidence, không là positive search fixture.
- Git: giữ nguyên baseline dirty work; không stage/commit/push. Script fixture dùng tài nguyên cách ly; không reset shared DB/volume. Một volume fixture Facility được giữ lại theo báo cáo reviewer, container đã dừng; không có thao tác xóa volume ngoài harness.
- Bước tiếp cho QA: mở `docs/04-testing/assistant-handoff-testing.md`, chuẩn bị corpus/provider/account môi trường đích và ghi PASS/FAIL/BLOCKED từng ca. Review/fix/docs của yêu cầu đã bàn giao; nhật ký chatbot vẫn active vì nghiệm thu toàn bộ sản phẩm và booking scope còn mở.

## 1. Thông tin
- Session gốc07/11, cập nhật08/11/2026; branch `AI-ChatBot`, mốc trước đợt chia commit là `3d3def76`.
- **In Progress**; một nhật ký active cho chatbot backend, không tạo file mỗi prompt.
- Review S02–S05/S09 và tổ chức tài liệu; frontend có owner riêng.
- Trạng thái Git và kiểm chứng mới nhất nằm ở mục **Đồng bộ develop** cuối file. Các checkpoint trước giữ bối cảnh tại thời điểm ghi.

## 2. Mục tiêu
- Settings/FAQ/history/recovery/reads chạy thật ổn định; chỉ mở draft khi đủ owner evidence/approval.
- Một kế hoạch, một nhật ký, lịch sử tìm lại được.

## 3. Việc đã làm
- User xác nhận chạy thật; Gemini FAQ thực có evidence08/11 trên corpus runtime riêng. Source Markdown còn draft.
- SQL IoT S02/S05, User compatibility, backup/restore/Admin auth từng được kiểm chứng có phạm vi. Không áp lại vì docs đổi chỗ.
- S02–S05 implemented; S06/S08/S09/S10 partial; S07 chưa draft.
- [Lịch sử đầy đủ](../archive/chatbot-implementation-history.md) giữ checkpoints, approvals và test. Không dùng trạng thái host cũ như live state.

## 4. Quyết định
- User yêu cầu review backend và gộp/đổi tên/bỏ Markdown dư. Lượt này không thay runtime code, FE, API contracts, egress hoặc SQL.
- Giữ independent review sau fix; user test trước bàn giao; Git write/SQL/provider/runtime theo quyền riêng.
- Knowledge path, contract và test evidence được bảo toàn.

## 5. Tài liệu thay đổi
- Sáu thiết kế/roadmap cũ gộp vào lịch sử; kế hoạch hiện hành và phiếu owner được viết gọn lại.
- Mười nhật ký assistant gộp vào lịch sử; file này tiếp quản việc chưa xong, không đánh dấu sản phẩm complete.
- Bốn README session đổi tên theo knowledge/FAQ, hội thoại/phục hồi, tích hợp tra cứu, điều kiện đặt chỗ.
- README là mục lục; cập nhật links/anchors. Backup byte-for-byte trước thao tác ở ignored `.cache/assistant-docs-before-20261108`.

## 6. Findings trước đợt hoàn thiện backend
- S02/S03 reviewer chưa thấy P1/P2 chắc chắn; cải tiến diagnostics và FAQ smoke test là đề xuất.
- S05 P2: Driver thiếu mapping special FAQ intents của Guest; fallback phủ định search dù có thể bật.
- S09: HttpClient mới mỗi answer; poll100ms lặp DB/file parsing. Chưa đo lợi ích tối ưu, không tự đổi freshness.
- S04: lexical chặn trước model; một article/title hạn chế hiểu ý. Semantic/egress expansion PROPOSED.
- Owner A01–A06/G04–G08: [phiếu quyết định](../../../docs/03-design/ai/service-dependencies-and-decisions.md).

## 7. Kiểm chứng
- Lượt review trước cùng chat: IoT465/465, FE53/53, FE build pass; sandbox VSTest timeout, ngoài sandbox pass. Không gọi là rerun của dọn docs.
- Hai subagent hiện tại review read-only management và chat/performance. Findings backend chưa sửa.
- Kiểm chứng dọn docs: không tạo thêm link nội bộ thiếu file; tất cả link tới anchor archive hợp lệ; diff whitespace check đạt. Repo có188 link thiếu từ trước, không mở rộng sang sửa tài liệu ngoài phạm vi.
- Independent doc reviewer xác nhận SHA256 backup khớp và toàn bộ nội dung16 file gốc được giữ trong archive sau chuẩn hóa links/anchors. Reviewer chỉ ra trạng thái cũ trong hướng dẫn S04/S05 và đường dẫn kế hoạch cũ; đã sửa và kiểm tra lại trực tiếp. Rereview độc lập cuối chưa thực hiện do agent hết hạn mức; không ghi CLOSED cho lần đó.
- Session merge05/11 đã hoàn tất chuyển sang completed và cập nhật link; chỉ còn một session chatbot active. Không chạy lại build/test vì thay đổi lượt này chỉ là tài liệu.
- Không chạy provider/DB/browser mới, không start/stop app.

## 8. Git
- Trước đợt chia commit: bảo toàn staged/unstaged/untracked cũ, index và stash; không commit/push/reset/fetch. Đợt chia commit cuối file có quyền Git mới do người dùng cung cấp.
- File bỏ đã được gộp và backup; không xóa knowledge/contracts/code người khác.

## 9. Bước tiếp theo
- Nghiệm thu UserService và provider được phép, đối chiếu citation/locale, đo tải trên host đích.
- Chạy checklist SET-01/02 trên môi trường đích. Mất ACK và deadline dưới lock còn là ca kiểm thử độ bền mở.
- UI do FE owner. Các quyết định liên service vẫn theo phiếu owner.

## 10. Điểm vào
- [README](../../../docs/03-design/ai/README.md).
- [Kế hoạch](../../../docs/03-design/ai/design-and-development-plan.md).
- [Nghiệm thu](../../../docs/04-testing/assistant-handoff-testing.md).
- [Lịch sử thiết kế](../../../docs/03-design/ai/luu-tru/design-history.md).

### Cập nhật tên tài liệu — 08/11/2026

Theo yêu cầu người dùng, bỏ SESSION/ngày khỏi tên file chatbot; dùng tên tiếng Anh mô tả mục đích. Cập nhật liên kết, giữ nội dung và anchors. Không đổi code, knowledge runtime path, index hoặc stash.

### Đợt hoàn thiện backend độc lập — 08/11/2026

- **S01:** Đối chiếu ma trận hiện trạng, cập nhật mối liên hệ giữa capabilities và bằng chứng nghiệm thu. Positive availability, draft/quote, tạo đơn và payment giữ trạng thái chờ owner.
- **S05:** `GuestIntents`, `GuestHandlers` và `ChatHandlers` dùng chung nhánh FAQ về thời gian. Câu đỗ quá giờ dùng câu hỏi backend cố định và mục thanh toán đã duyệt. Fallback Driver chỉ hướng về FAQ. Bộ regression Guest/Driver chạy Release đạt 150/150 ở checkpoint này.
- **S02/S05:** Harness `scripts/test-assistant-postgres.ps1` tạo PostgreSQL 16 dùng một lần và chạy SQL IoT hiện có. `AssistantPostgresTests` dùng hai datasource để kiểm idempotency, revision, rotation/revocation và ledger cancel/finish/recovery. Kiểm tra revision sau worker trước mọi call có khả năng tự phục hồi. Lần chạy S02/S05 đạt 2/2.
- **S04:** 21 ca Việt/Anh trên Markdown repo. Baseline trước thay đổi ghi nhận ít nhất năm câu kỳ vọng đúng nguồn bị từ chối. Sau sửa, bộ 21 ca đạt toàn bộ. Phục hồi một lỗi gõ trong alias ngắn và duy nhất, dùng rule hạn chế trên alias chuẩn. Thêm ca sai nguồn, `auto` locale và xác minh Gemini giả lập nhận tiêu đề backend khi câu gốc không qua sàng lọc.
- **S09:** Named HTTP client tái sử dụng handler, credential riêng theo request. Cache parse theo SHA-256, vẫn đọc và kiểm byte/manifest/thời hạn mỗi lần. Thêm histogram knowledge/provider và năm stage tại Application. Fixture 80 cặp đọc có p50 0,975 → 0,431 ms, p95 1,683 → 0,960 ms. Lượt test FAQ/knowledge/transport đạt 27/27.
- **S10:** Bổ sung smoke test dùng normal-host TestServer và PostgreSQL thật với JWT, certificate, keyring, corpus và provider giả lập. Chạy Settings, Test connection, FAQ và Driver chat, dựng lại host và kiểm replay/history theo Account. Lượt đầu 3/3 sau khi sửa cách fixture truyền password của DB dùng một lần. Năm harness PowerShell setup/check/runner/listeners/migration-plan đạt.
- **Review:** Mỗi đợt có hai subagent độc lập review đọc mã. Đã xử lý fallback tìm bãi khi tắt, recovery test có thể tự sửa dữ liệu sau worker, typo chọn sai nguồn/sai locale, registration HTTP của `AddManagement`, fixture cache thiếu từ khóa và thiếu password khi tạo lại datasource. Review lại S04/S05/S09/S10 xác nhận không thấy lỗi chặn ở các điểm đã sửa.
- **Giới hạn:** Restart là dựng lại ASP.NET host trong tiến trình test. [UNVERIFIED] kill tiến trình khi provider đang chạy, deadline dưới lock, mất ACK, retention, full setup SET-01/02 và tích hợp UserService/provider thật. Không đổi API wire, migration, frontend hay mã service khác. Xem [hướng dẫn chạy và số đo](../../../docs/04-testing/assistant-handoff-testing.md).
- **Checkpoint bàn giao:** Bản sao source working tree không có `bin`, `obj` hoặc cấu hình cá nhân. Restore từ cache NuGet cục bộ, build solution Release đạt (một warning NU1510 có sẵn). Full suite 962 đạt, 70 skip và tái hiện đúng bốn lỗi Facility operational-layout đã ghi trong lịch sử. IoT 473 đạt, ba ca opt-in PostgreSQL chạy riêng đạt 3/3. Branch `AI-ChatBot`, giữ index và trạng thái working tree đã có. Đợt backend độc lập có thể bàn giao cho local test, release và S07/S08 write flow vẫn mở.

### Kiểm chứng crash, retention và khôi phục — 08/11/2026

- **S05:** `AssistantPostgresRecoveryTests.cs` chạy host trong tiến trình test riêng, gửi request qua HTTP TestServer và chờ adapter giả lập bắt đầu xử lý. Sau kill, host mới tự chuyển ledger thành `REQUEST_INTERRUPTED` sau deadline. Tra và gửi lại cùng ID giữ kết quả, không tạo thêm tin nhắn hay dispatch. Ca retention kiểm hạn 30 ngày, 404 cho hội thoại hết hạn kể cả có hold, purge cascade và bảo toàn hold.
- **S10:** `scripts/test-assistant-postgres.ps1` chạy lại ba migration trên DB fixture có dữ liệu. Tám bảng giữ cùng dữ liệu. `pg_dump` được khôi phục sang DB test thứ hai và đối chiếu. Ca smoke chạy host với bản sao keyring và certificate fixture, dùng một Test connection mới để xác nhận decrypt credential.
- **Review:** Hai reviewer độc lập xác định cần dùng thời gian PostgreSQL cho fixture retention, tránh worker purge từ host test khác, giới hạn chờ tiến trình con thoát và kiểm decrypt bằng diagnostic mới. Đã xử lý các điểm này.
- **Kết quả cuối:** Build solution Release đạt, một warning NU1510 có sẵn. Harness PostgreSQL đạt 5/5 và xác nhận migration replay/restore. IoT mặc định 473 đạt, 6 skip là năm ca opt-in và worker con chỉ chạy trong harness. Checker fixture và `git diff --check` đạt. Không gọi provider thật trong lượt này.

### Setup Docker cho thành viên — 08/11/2026

- Luồng mặc định vẫn dựng IoT base. Thêm `setup.ps1 -WithAssistant` cho máy mới và `scripts/setup-assistant-docker.ps1` để bổ sung riêng Assistant vào stack DB local đang chạy. Compose và Dockerfile riêng, endpoint loopback 5076 mặc định. Chỉ điền URL Assistant trống trong FE/.env.
- Cần public trust khớp UserService, PFX RSA bảo vệ keyring và corpus đã được duyệt. Chỉ bootstrap ba migration trong một transaction trên schema Assistant trống. Xác thực PostgreSQL qua TCP trước SQL, từ chối schema dở dang hoặc legacy có dữ liệu. DB đã lưu lịch sử/credential cần chọn dùng lại keyring certificate gốc.
- Hai reviewer độc lập rà Docker, migration và trust. Đã sửa regex đường dẫn Windows, kiểm Docker local trước setup nền, probe DB password, kiểm PFX RSA, chặn DPAPI và bảo vệ mount khỏi .env/khóa User. Sửa thêm tương thích Windows PowerShell 5.1 vì `Select-Object -First 1` ngắt lệnh native khi đọc context.
- **Kiểm chứng:** Docker image build đạt. `test-assistant-docker-setup.ps1 -NoBuild` đạt với Windows PowerShell 5.1 ở runner, PostgreSQL 16 mới, sai mật khẩu không ghi SQL, Admin JWT/Driver 403, Guest FAQ theo corpus và lịch sử Driver giữ sau rerun. Harness setup cũ, AssistantCheck, migration plan, host runner và `git diff --check` đạt. [UNVERIFIED] chạy top-level toàn stack trên máy mới và smoke với UserService/provider thật. [Hướng dẫn onboarding Docker](../../../LOCAL-SETUP.md#ai-chatbot-bằng-docker).
- **Review cuối:** Hai reviewer độc lập đọc lại xác nhận đã xử lý các phát hiện và chưa thấy lỗi chặn. Bổ sung harness `test-setup.ps1` cho thứ tự preflight → setup nền → Assistant, preflight lỗi dừng trước Docker, bảo toàn .env và chặn tổ hợp `SkipDatabase/RecreateDb`. Windows PowerShell 5.1 chạy harness này đạt. Quick start đã dẫn tới hướng dẫn tạo khóa local và cấu hình Docker.

### Tài liệu bàn giao tester và leader — 08/11/2026

- Theo yêu cầu người dùng, viết lại `docs/04-testing/assistant-handoff-testing.md` thành hướng dẫn QA có setup, điều kiện kích hoạt, fixture, smoke, case ID/priority/steps/expected, báo lỗi và cổng nghiệm thu. Giữ các anchor lịch sử đang có liên kết.
- Tạo `docs/03-design/ai/system-overview-and-review.md` cho leader, gồm mục tiêu, kiến trúc bốn assembly chatbot, ownership, trạng thái S01–S10, việc độc lập còn mở, dependency A01–A06/G04–G08, What/Why/When/Who và phiếu phân công.
- Đối chiếu source Guest/Driver/Settings, capability, owner adapters và Docker. Guest citation kiểm bằng Network vì UI chưa hiển thị nguồn chi tiết. Docker hiện không export owner-read flags. Bằng chứng cũ giữ ngày/phạm vi, không nhận là suite chạy mới.
- Đã giao reviewer độc lập cho từng tài liệu và một reviewer source đối chiếu chéo. Kết quả xử lý và kiểm links/ID được cập nhật trước bàn giao. Lượt này chỉ sửa tài liệu và mục lục.
- Review leader/source không phát hiện lỗi chặn. Reviewer QA chỉ ra thiếu nhãn trước Load models, cần pacing vì rate limit, cần yêu cầu mode=ai để nghiệm thu provider và điều kiện image postgres:16-alpine của harness. Đã sửa cả bốn và bổ sung ví dụ model sai có thể tái hiện.
- Kiểm tự động tài liệu đạt, gồm 55 case ID duy nhất, link/anchor đích, code fence, whitespace và ba anchor tester có link từ tài liệu khác. `git diff --check` đạt. Không rerun runtime suite trong đợt tài liệu này. Mọi số liệu test giữ provenance và giới hạn của các lượt trước.
- Reviewer QA đã rà bản cuối và xác nhận bốn phát hiện được xử lý, không còn phát hiện chặn bàn giao tài liệu. Hai tài liệu sẵn sàng gửi theo đúng mục đích tester/leader. Tên owner cá nhân và ngày cam kết vẫn để leader điền.
- **Môi trường local:** `setup.ps1 -AssistantCheck` chạy chỉ đọc, cấu hình cốt lõi đã có và owner reads đang tắt. Liveness 204, guest status 200 với `enabled=true`, `guestFaq=true`. Đây là kết quả của host local đang chạy. [Checklist bàn giao](../../../docs/04-testing/assistant-handoff-testing.md#chốt-bàn-giao-trên-môi-trường-test-của-nhóm) có các ca cần tài khoản và provider thật. [UNVERIFIED] deploy/rollback của môi trường đích, tải đại diện, lost commit ACK và deadline dưới lock. Phần booking tiếp tục theo owner.

### Chia commit để leader review — 08/11/2026

- Người dùng yêu cầu tách theo từng phần, mỗi commit có ít nhất vài file; đã xác nhận dùng Conventional Commits và **không cần Jira** cho đợt này. Giữ branch `AI-ChatBot`, không push. Index cũ đã được sao lưu trong cache trước khi sắp xếp lại; không thay đổi nội dung công việc để tạo commit trung gian.
- Tách backend nền từ snapshot đã staged, sau đó bổ sung Settings/diagnostics và Guest/Driver chat theo thứ tự phụ thuộc. Riêng error mapping Settings chưa đưa hai loại lỗi Guest/Chat vào cho tới commit chat. Knowledge corpus đi cùng backend vì regression đọc trực tiếp tài liệu này.
- Hai reviewer độc lập đã đối chiếu phạm vi và phụ thuộc. Các tài liệu cũ đã được gộp nhưng còn trong index trước đó không được đưa trở lại. Giữ nguyên Docker backend ignore cũ; loại file scratch `.tmp-assistant-handoff-status.txt`, `.env`, keyring, certificate, cache và build output khỏi commit. Kiểm tra mẫu secret có độ tin cậy cao trên 192 file ứng viên không tìm thấy kết quả.
- Chỉ sửa thêm comment đường dẫn rollback S05 sang tài liệu hiện hành và bỏ dòng trắng thừa ở EOF của `GuestFaqTests.cs` để đạt kiểm tra whitespace; không đổi hành vi.

| Nhóm | Commit | Nội dung | Số file |
|---|---|---|---:|
| 1 | `6c93e477` | Backend nền, JWT, management và grounded demo | 45 |
| 2 | `dd821ccb` | Settings, diagnostics, credential và provider transport | 26 |
| 3 | `dd6ff401` | FAQ, hội thoại Account, history và read adapters | 34 |
| 4 | `881e6e13` | PostgreSQL recovery và request baseline | 5 |
| 5 | `9f08bd7f` | User publication evidence và auth compatibility | 6 |
| 6 | `1f9e773d` | Facility discovery catalogue và publication | 8 |
| 7 | `a61beeab` | Payment discovery pricing | 8 |
| 8 | `26dd3b63` | Operation search, capacity và own reservations | 12 |
| 9 | `95ba9f0a` | Frontend demo cách ly | 6 |
| 10 | `33b26335` | Frontend Admin Settings | 9 |
| 11 | `614cd1f3` | Frontend Guest và Driver chat | 9 |
| 12 | `b5a8cbb9` | Docker opt-in, setup và readiness | 19 |
| 13 | `49f8f482` | Hợp đồng API, thiết kế và dependency | 15 |
| 14 | Commit chứa cập nhật này | Tài liệu tester/leader, mục lục và tiến độ | 7 |

- **Kiểm chứng mới của đợt chia commit:** Bản sao source cô lập không có `.env` cá nhân. Backend nền đạt 230/230 test; mốc Settings đạt 270/270. Ứng viên cuối build toàn solution Release đạt, chỉ warning NU1510 đã có. IoT đạt 473, skip 6 ca opt-in/worker. Regression mới của User/Operation đạt 55, Facility discovery/publication đạt 13, Payment discovery pricing đạt 8. Frontend chatbot đạt 53/53 và `npm --prefix FE run build` đạt, còn cảnh báo bundle/dynamic import đã có. Harness Windows PowerShell 5.1 `test-setup.ps1` và `test-assistant-check.ps1` đạt.
- Không chạy lại full solution test, Docker smoke, PostgreSQL opt-in hoặc provider thật trong đợt chia commit; bằng chứng các lượt đó giữ nguyên phạm vi ở trên. Bốn lỗi Facility operational-layout của lượt full suite trước vẫn cần owner xử lý, không gộp vào đợt này.
- **Bước bàn giao:** Người dùng push branch khi sẵn sàng, gửi tester tài liệu nghiệm thu và gửi leader tài liệu tổng quan. Việc chia commit không đóng các dependency booking, capacity authority hoặc các cổng nghiệm thu môi trường đích còn mở.

## Đồng bộ develop — 10/11/2026

- Tích hợp `develop` tại `d5a10f0c` vào `AI-ChatBot` tại `2ff96f6b`. Ba reviewer độc lập rà trước merge và kiểm tra lại phần xử lý 7 conflict.
- Giữ nghiệp vụ mới của `develop` cho Turnstile, media, phiên đăng nhập và Facility CQRS. Giữ cấu hình, route Discovery và tùy chọn HTTP riêng của Assistant. Sửa khai báo state trùng có sẵn trong form bãi xe và cập nhật fixture kiểm thử theo import hiện hành.
- Build .NET và frontend thành công. Bộ .NET offline đạt 1.221 test, gồm 12 test kiến trúc. Frontend đạt 268 test. Năm script PowerShell offline kiểm tra setup, runner, listener, migration plan và readiness đạt.
- Docker, PostgreSQL tích hợp, broker và provider thật vẫn chờ nghiệm thu môi trường. Các gate về đặt chỗ và availability giữ theo tài liệu owner.


## Bản đề xuất knowledge mở rộng — 10/11/2026

- Tạo [bản nháp 0.4.0](../../../docs/03-design/ai/p1/knowledge/smartpark-knowledge.md) bên cạnh nguồn hiện hành. Kế thừa 12 mã mục, bổ sung 9 mục hướng dẫn và mở rộng các quy tắc đã có căn cứ. Bản nháp dành cho FAQ công khai và người lái, chưa được công bố.
- Hai subagent kiểm tra độc lập về nghiệp vụ và chọn nguồn. Vòng đầu tái hiện một hồi quy chọn loại xe và chỉ ra các điểm thiếu về no-show, hủy gói, điều kiện vào bãi và thời hạn thanh toán. Đã sửa và kiểm tra lại.
- Harness .NET 10 ngoại tuyến dùng parser, loader công bố và bộ chọn nguồn trong repo. Loader từ chối bản draft và chấp nhận bản sao fixture có công bố mô phỏng. Bản cuối giữ 43/43 kỳ vọng phân loại cũ và 58/58 đường dẫn tệp tham chiếu tồn tại. Các câu mới thử được chọn đúng mục cho tới trễ, khách đến trực tiếp, hủy gói, hóa đơn và sự cố tại cổng. Chưa gọi provider hoặc nghiệm thu runtime.
- Handoff: review/phê duyệt nội dung và kiểm thử trên môi trường đích nếu chọn làm bản thay thế. Bộ phân loại hiện tại vẫn chuyển một số câu có số hoặc cụm “bao nhiêu” sang nhánh hạn chế trước khi đọc knowledge. Cải thiện nhánh này là công việc backend riêng.

- Theo yêu cầu tiếp theo, chọn 0.4.0 làm nguồn biên tập smartpark-knowledge.md và lưu nguyên byte của 0.3.1 dưới tên smartpark-knowledge-old.md. Giữ metadata draft/pending cho nguồn repo.


## Công bố knowledge 0.4.0 cho Docker local — 10/11/2026

- Admin xác nhận duyệt 0.4.0 cho Docker local trong phản hồi Codex ngày 10/11/2026. Bản sao Markdown đã duyệt và manifest khớp SHA-256 được đặt vào thư mục knowledge đang mount của Assistant local. Giữ bản 0.3.1 và manifest tương ứng trong thư mục rollback của mount. Nguồn biên tập trong repo giữ trạng thái draft.
- Trước công bố, smoke test FAQ thật trả HTTP 200 và `vehicle-guide@0.3.1`. Sau công bố, status trả HTTP 200 với Guest FAQ khả dụng. Câu “Can I park without a reservation?” trả HTTP 200, `mode=ai`, `outcome=answered`, nguồn `walk-in-and-entry-guide@0.4.0`, trace `95d5cf0bb38f43a487c3a9ed082473f6`. Không cần khởi động lại Assistant.
- Kiểm chứng ngoại tuyến trước rollout: 43/43 ca phân loại, 244 test backend đạt và 3 ca skip, 53 test frontend đạt. Phạm vi nghiệm thu thật là Guest API local và một câu công khai.

## Smoke test lại AI Chatbot — 10/11/2026

- Kiểm working tree của `AI-ChatBot` tại `d2ee96e0`, gồm các thay đổi chưa commit. Tách kết quả từ source hiện tại, Docker đang chạy và fixture cách ly.
- `dotnet test` cho IoTService từ source hiện tại đạt 479, lỗi 4 và skip 6. Cả 4 ca `SingleVietnameseCorpusAnswersBothLanguages` dừng tại `Assert.Single(Directory.GetFiles(corpus, "*.md"))` do có thêm `smartpark-knowledge-old.md`. Năm ca opt-in chạy riêng bằng `scripts/test-assistant-postgres.ps1` đều đạt, gồm crash/recovery, retention và lưu lịch sử sau restart. Migration replay và backup/restore cũng đạt. Ca skip còn lại là tiến trình con của harness crash.
- Frontend đạt 53/53, `npm --prefix FE run build` đạt. Hai browser harness dùng dữ liệu fixture đạt cho Driver recovery/history và Admin Settings. Năm script fixture setup, check, runner, owner listeners và migration plan đạt. `scripts/test-assistant-docker-setup.ps1 -NoBuild` đạt trên image local với JWT và DB fixture, kiểm Admin đọc Settings, Driver bị chặn 403 ở Settings, Guest fallback và Driver history sau khi chạy lại setup.
- Docker live đạt liveness 204, status 200 với `guestFaq=true`, CORS preflight, từ chối JWT thiếu/sai 401, JSON sai 400, content type sai 415, rate limit 429 và cách ly route Demo. Widget Guest thực tế ở 1440×900 và 390×844 hiển thị FAQ từ `smartpark-overview@0.4.0`, sau đó chuyển sang đăng nhập khi hỏi lịch đặt. Guest không xác nhận giá hiện tại và không lặp token giả lập.
- **FAIL provider generation.** Các lượt Guest và Demo thử trên runtime trả `mode=knowledge` với nguồn 0.4.0. Log Guest có upstream HTTP 503 và timeout. Demo cũng ghi timeout. Trace Guest `b49a413d38804928ab1af780e778192e`, Demo `d4d177abd3d2456ab0e729e30c5de013`. Chưa chẩn đoán được nguyên nhân phía provider.
- **FAIL phân loại policy trên Docker.** `Đặt chỗ trước tối đa bao nhiêu ngày?` bị đưa sang giá/chỗ trống (`58ad49f71d474796b04a7c46e8caaf3e`), `Hủy đặt chỗ trước 24 giờ có được hoàn tiền không?` bị báo dữ liệu nhạy cảm (`3981e852aec04c3e9a4363779e3c3c29`). DLL đang chạy thiếu `PublicFaqIntents`. DLL build từ working tree chọn đúng route cho hai câu này, nhưng `Tôi có thể đặt chỗ trước tối đa bao nhiêu ngày?` vẫn vào `dynamic` và `Nếu hủy đặt chỗ trước 24 giờ thì có được hoàn tiền không?` vẫn vào `sensitive`.
- [UNVERIFIED] Phiên Admin/Driver do UserService thật cấp, diagnostics và mutation trên Settings live, đọc dữ liệu owner live. Các cờ owner reads chưa được chuyển vào container đang kiểm. Chuẩn bị đặt chỗ và positive availability tiếp tục theo phạm vi đã chốt với các owner.
- Bàn giao các lỗi để xử lý và kiểm lại. Lượt này chỉ ghi kết quả vào nhật ký. Runtime hiện có được kiểm tra trực tiếp, các thao tác ghi/restart chỉ diễn ra trong fixture.


## Cải thiện nhận diện câu hỏi FAQ — 10/11/2026

- Guest và Driver FAQ dùng thêm cụm từ theo ngữ cảnh cho walk-in, đến trễ, lấy xe quá giờ, hủy gói, sự cố cổng và thiếu thông báo. Một số câu quy tắc công khai có số qua mẫu toàn câu và dùng câu hỏi cố định do backend cung cấp. Tách hủy gói tự nguyện với bãi đóng cửa. Giữ kiểm tra dữ liệu cá nhân và trạng thái hiện tại của bãi trước truy xuất thông thường.
- Hai subagent review độc lập sau mỗi vòng sửa về định tuyến và dữ liệu gửi provider. Đã xử lý các lỗi review về ký tự nhạy cảm, chủ đề ghép, điều kiện về loại xe, từ “vẫn”, hoàn gói khi bãi đóng cửa và câu hỏi bãi còn nhận xe lúc này. Review cuối không phát hiện blocker.
- IoTService.Tests bản cuối đạt 489, lỗi 0, skip 6. Frontend Assistant đạt 53/53. Các kiểm tra bao gồm 43/43 kỳ vọng phân loại gốc và 38/38 ca smoke qua Guest handler fixture. Đã sửa ca Demo để kiểm tra tên file knowledge chính khi có file lưu 0.3.1. Probe riêng xác nhận câu hỏi khả năng tiếp nhận ngay lúc này đi nhánh dynamic ở cả Việt và Anh.
- Các cách hỏi ngoài mẫu như “How long is a booking at minimum?” và “Đặt trước lâu nhất được mấy hôm?” vẫn có thể chưa tìm được nguồn. [UNVERIFIED] Câu trả lời từ model thật trên build mới. Chưa cập nhật image hay restart Docker Assistant trong lượt sửa code.


## Smoke test Docker với bộ nhận diện mới — 10/11/2026

- Build lại và recreate riêng service Assistant trong project smartpark-app, dùng cấu hình của container đang chạy. Image mới f8669910540a thay b8525c7a69dd. Container healthy, status HTTP 200, Guest FAQ khả dụng, corpus mount 0.4.0.
- Guest API trả HTTP 200 cho 10/10 câu và đi đúng nhánh. Tám câu trích đúng nguồn 0.4.0 ở mode knowledge. Hai câu đối chứng hỏi lại đúng cho “Gửi xe muộn thì sao?” và từ chối xác nhận bãi còn nhận xe lúc này. Các cách hỏi chạy thẳng đến bãi, đến sau giờ hẹn, đặt tối thiểu 15 phút và hủy trước 24 giờ đã chọn đúng mục.
- Provider generation chưa đạt trong lượt này. Log ghi Timeout, không có mã HTTP upstream. Nhánh dự phòng trả trích đoạn tiếng Việt có nguồn, câu tiếng Anh có thông báo tài liệu chỉ có bản tiếng Việt. Chưa xác nhận câu trả lời của model trên image mới. Không sửa code, dữ liệu hoặc cấu hình provider trong lượt smoke.


## Smoke test 27 câu với provider thật — 10/11/2026

- Guest API Docker local dùng image f8669910540a và corpus 0.4.0. Thử 3 đợt tuần tự lúc 10:29–10:33 giờ Việt Nam, cách ít nhất 65 giây sau mỗi đợt. 21 câu FAQ và 6 ca đối chứng. HTTP 200 cho 27/27, đúng nhánh 25/27, đúng nguồn FAQ 19/21, đúng giới hạn 6/6. Model 9/21 và tài liệu dự phòng 10/21. Hai câu FAQ đặt trước lâu nhất được mấy hôm và thời lượng đặt tối thiểu bằng tiếng Anh chưa tìm được nguồn.
- Theo đợt, tỷ lệ model trên 7 câu FAQ là 2/7, 5/7 và 2/7. Log trong cửa sổ test có một Gemini 503 và hai timeout. Độ trễ đầu cuối p50 32 ms, p95 7027 ms. Chín câu mode ai có trung vị 5005 ms và khoảng 1484–6724 ms. Mẫu nhỏ chạy tuần tự, chưa đo tải đồng thời hoặc SLA.
- Kiểm nội dung: luồng AI phân biệt bãi tạm đóng và vĩnh viễn, hỗ trợ xe máy tùy bãi và chưa xác nhận xe đạp. Câu hoàn trước 45 phút nêu đúng các bậc và tiền đủ điều kiện đã thu, nhưng cần người đọc tự áp mốc. Tài liệu dự phòng chọn đúng bài nhưng trả cả mục, và ba câu tiếng Anh ở mode knowledge chỉ có nội dung gốc tiếng Việt. Không đổi cấu hình provider hoặc code.

## Một đoạn chat đang dùng cho mỗi Account và role — 10/11/2026

- Người dùng duyệt một đoạn chat đang dùng cho Driver, Admin và Business/Owner. Xác nhận `+ Mới` làm ẩn đoạn cũ ngay và đặt hạn dọn sau 7 ngày. Quyền tra cứu dữ liệu nghiệp vụ vẫn thuộc Driver. Hold đã duyệt giữ dữ liệu trong phạm vi tương ứng.
- Đã đồng bộ SRS, thông báo trong chat và trang quyền riêng tư. Migration S05 giữ đoạn Driver còn hiệu lực gần nhất, các đoạn cũ hơn giữ nguyên hạn lưu và hold. Đã thử nâng cấp, chạy lại migration và khôi phục backup trong PostgreSQL cách ly. Chưa áp migration lên DB đang chạy.
- Kiểm chứng đạt 490 test .NET offline, 6 test PostgreSQL cách ly, 20 test frontend liên quan, build Vite và Chrome desktop/mobile với API giả. Ba subagent đã review và kiểm tra lại các sửa đổi.

## Review trước PR — 10/11/2026

- Trang lịch sử cũ chỉ bổ sung tin nhắn và cursor, giữ revision của trang mới nhất đang hiển thị. Backend đọc revision, tin nhắn và request đang chạy dưới cùng khóa hội thoại. Cursor lịch sử dùng sequence để giữ giới hạn trang khi có tin mới. Đã cập nhật ca QA cho lượt gửi đầu tiên, hai tab, phân trang và purge sau reset, cùng mô tả retention trong tài liệu tự học.
- Subagent review độc lập hai vòng, phát hiện race ở trang đầu và xác nhận bản sửa sau vòng hai. Build Vite, 268 test frontend, Chrome 1440×900 và 390×844 với API giả, harness migration plan và `git diff --check` đạt. Toàn solution .NET đạt 1.238, lỗi 0, skip 104, trong đó IoT đạt 490, skip 7.
- Đưa migration current-chat, source FAQ và bản lưu corpus được tham chiếu vào Git index. File tạm được chuyển vào `.cache`. Index chỉ chứa nhóm file mới đã chọn, các thay đổi tracked khác vẫn cần được chọn vào commit theo phạm vi PR.
- [UNVERIFIED] Chưa chạy lại migration PostgreSQL thật hoặc smoke provider thật trong lượt này. Bước tiếp theo là đồng bộ với `develop`, kiểm checkout của commit sẽ mở PR và xác minh cả volume mới lẫn nâng cấp từ S05.

## Chuẩn bị PR cho QA — 10/11/2026

- Giữ hướng dẫn QA, tổng quan cho leader và hợp đồng ở các tài liệu có mục đích riêng. Chuyển ghi chú tự học cá nhân sang `.cache`. Chuyển corpus 0.3.1 nguyên nội dung sang kho lưu trữ và cập nhật liên kết. Thư mục knowledge dùng cho triển khai chỉ chứa nguồn 0.4.0 ở trạng thái draft.

## S06 tìm bãi và đọc lịch Driver — 10/11/2026

- Đối soát nhánh `AI-ChatBot`, nền `04c6def2`, working tree sạch khi bắt đầu. Giữ adapter HTTPS hiện có cho Operations. Search là GET công khai. Danh sách và chi tiết lịch dùng JWT Driver của yêu cầu hiện tại và Operations lọc theo Account.
- Giữ mẫu tọa độ. Câu tự nhiên ngoài mẫu được xử lý cục bộ, hỏi phần thiếu hoặc mơ hồ rồi yêu cầu gửi lại đầy đủ trong một tin nhắn. Tên địa điểm cần tọa độ. “Chiều mai” cần giờ bắt đầu và kết thúc. Lời báo rỗng và lỗi owner riêng biệt. Thẻ bãi dùng số `0` hoặc “Chưa xác định” theo dữ liệu nguồn.
- Docker Assistant nhận cờ read, HTTPS origin và ngưỡng freshness từ cấu hình local. Kiểm tra trước khi provision và giữ mặc định tắt. [Hướng dẫn bật và chạy từng ca](../../../docs/04-testing/assistant-handoff-testing.md#6-test-tích-hợp-owner-sau-khi-môi-trường-sẵn-sàng).
- Chrome 1440×900 và 390×844 với API giả lập đạt các ca rỗng, lỗi, null/0, bấm tra chi tiết lịch và đổi Account. Frontend 268 test đạt. Windows PowerShell 5.1 qua kiểm tra setup và checker. Reviewer xác nhận cấu hình Compose fixture và preflight chấp nhận HTTPS hợp lệ, từ chối HTTP và freshness ngoài ngưỡng.
- Build solution đạt. Toàn solution .NET đạt 1.248, lỗi 0, skip 104, gồm IoTService.Tests đạt 500 và skip 7. JWT ký cho test và owner HTTP giả lập bao phủ tìm theo tọa độ, khung giờ ISO, hỏi lại cục bộ, rỗng/lỗi và danh sách/chi tiết lịch. Ba subagent review chéo. Bản cuối xử lý các ca nhận nhầm loại xe trong tên địa điểm, giờ hiện tại bị phủ định, thứ tự tùy chọn ISO, lựa chọn ngôn ngữ tìm bãi và tra chi tiết bằng mã ở thẻ không có nút.
- [UNVERIFIED] Nghiệm thu với JWT UserService và dữ liệu thật. Docker local chỉ có Sonar DB đang chạy khi kiểm tra, các cổng Assistant, Operations và User không phản hồi, và các cờ owner reads trong `.env` local đều tắt. Bước tiếp theo là QA dùng HTTPS/fixture đã bàn giao và chạy TC-OWN-02 đến TC-OWN-07. Số chỗ dương cần nguồn capacity đáng tin cậy từ Operations và các owner liên quan.
- Sửa lỗi Windows PowerShell 5.1 ở `-AssistantCheck` khi cờ đọc đã bật và khóa origin chưa có. Checker báo `INVALID` trước khi phân tích origin rỗng. Fixture thiếu origin và lệnh với `.env` local đều chạy xong trên 5.1. Subagent tái hiện lỗi cũ và xác nhận bản sửa. Lượt kiểm tra local mới cho thấy Search đang bật và thiếu origin, max age, clock skew.
- Docker theo cấu trúc của nhóm `smartpark-schema` và `smartpark-app`. Preflight Docker Assistant từ chối HTTPS loopback cho owner reads trước bước provision. Harness `scripts/test-assistant-docker-setup.ps1 -PreflightOnly` xác nhận Windows PowerShell 5.1 từ chối `https://localhost:5056` và chấp nhận HTTPS origin khác loopback về mặt cú pháp. `.env` local đang bật Search với loopback. [UNVERIFIED] Docker tìm bãi thật cần HTTPS owner được container tin cậy và cấu hình Discovery của Operations, Facility và Payment.
- `-AssistantCheck` cũng báo `INVALID` cho owner origin loopback khi `.env` bật Docker Assistant. Test checker PowerShell 5.1 và Docker preflight đều đạt. Chạy checker trực tiếp với `.env` hiện tại cho kết quả Search origin `INVALID`, hai ngưỡng thời gian `CONFIGURED`. [UNVERIFIED] Docker Desktop Linux engine không phản hồi và Assistant status cổng 5076 hết thời gian trong lượt kiểm tra này.

## S07 hỏi đáp và xem lại thông tin đặt chỗ — 10/11/2026

- Bắt đầu trên `AI-ChatBot` tại `04c6def2`. Working tree có các thay đổi S06 đã có sẵn, gồm widget Driver, browser harness và nhật ký này. Giữ nguyên phần S06.
- Phạm vi lượt này là nhận diện và chỉnh thông tin do Driver nhập, xử lý tại widget theo phiên xác thực hiện tại. G04/G05, hợp đồng lưu/TTL, xác minh xe/bãi và wire chuyển form còn OPEN. Bản xem lại chỉ phục vụ nhập liệu.
- Đã nối bộ xử lý `assistantBookingPreview.ts` vào widget trên Driver Dashboard. Lượt đặt chỗ lấy loại xe và ngày giờ Việt Nam, hỏi thông tin còn thiếu và hiển thị dữ liệu sau từng lần sửa. Bản xem lại nằm trong bộ nhớ phiên widget, gắn với auth generation. Đổi Account hoặc tạo chat mới xóa bản xem lại. Tin nhắn đã nhận diện là nhập liệu được xử lý tại trình duyệt.
- [Bàn giao FE](../../../docs/03-design/ai/booking-prerequisites.md#4-bàn-giao-fe--xem-lại-thông-tin-nhập-từ-chat) mô tả `BookingPreview`, cách dùng nhãn, ISO offset và các mục còn thiếu. FE/Product chốt wire và vòng đời. User/Operations cung cấp bằng chứng xe, bãi và interval. Operations/Payment chốt G04/G05 trước bước báo giá.
- `node --test tests/frontend/*.test.cjs` đạt 277/277, gồm 9 ca cho phần nhập liệu. Vite build đạt. `tests/frontend/assistant-read.browser.cjs` với API fixture trên Chrome 1440×900 và 390×844 đạt, gồm hỏi lại, sửa xe/bãi/giờ, đổi Account khi có phản hồi lịch sử đang chờ và tạo chat mới. Harness xác nhận các lượt bản nháp không gửi POST.
- `dotnet build SmartParking.slnx --nologo --no-restore -m:1` đạt, có cảnh báo NU1510. [UNVERIFIED] Toàn bộ suite .NET trong lượt này do VSTest hết thời gian kết nối testhost trong sandbox. [UNVERIFIED] Xác minh owner, báo giá và form thật chờ hợp đồng. Thay đổi chưa commit.

## S07/S08 đối chiếu bàn giao đặt chỗ · 10/11/2026

- Tiếp tục trên `AI-ChatBot` tại `04c6def2` với thay đổi S06/S07 có sẵn. Đọc kết quả Chat 1 và Chat 2, rồi đối chiếu owner handoff, route, DTO, auth, hạn terms và receipt ở ref SPARK-273 `5daa4cc6`.
- Hoàn thiện các lỗi đã tái hiện trong bản nháp. Câu tra xe và tra lịch ngày mai đi lại luồng S06. Các mẫu biển số trong test được xử lý cục bộ. Giờ mơ hồ và thứ trong tuần chưa đủ để điền được hỏi lại. Câu sửa nhiều trường cùng lượt yêu cầu gửi từng thay đổi.
- `node --test tests/frontend/*.test.cjs` đạt 280/280. Vite build thường và build cho fixture đạt. Browser harness `assistant-read.browser.cjs` với API fixture đạt 1440×900 và 390×844. Assertion ghi nhận POST kể cả khác origin, bỏ riêng telemetry MapTiler đã xác định trong fixture, và xác nhận các lượt nhập liệu không gửi POST nghiệp vụ hoặc chat.
- Ba subagent review độc lập về nhập liệu, hợp đồng owner và nghiệm thu. Đã sửa các mẫu biển số có đuôi lịch sự, câu ghép thiếu động từ vế sau và cách nhận diện khung giờ chỉ có một cách hiểu hợp lệ trong cùng ngày. Review lại 12 ca unit và sáu probe trực tiếp đạt. Ghi chênh lệch `TERMS_STALE` với `TERMS_CHANGED_OR_EXPIRED` và bổ sung ca recovery/cùng key/đổi Account trong ma trận.
- **BLOCKED theo owner.** Ref SPARK-273 có preview, terms, admission và receipt, nhưng các route đó chưa có trên `AI-ChatBot`. Producer giá/capacity/Facility chưa sẵn sàng. S06 own reads và SPARK-273 trùng route với DTO khác. Form chưa có giao diện nhận `BookingPreview`, submit thật và phục hồi receipt. [Ma trận owner, mapping và ca nghiệm thu](../../../docs/03-design/ai/booking-prerequisites.md#5-đối-chiếu-bàn-giao-spark-273--10112026).
- [UNVERIFIED] Hết hạn/đổi giá, mất chỗ, timeout/retry/lookup của booking và thanh toán thật. Phục hồi Assistant chat đã được chạy bằng fixture, độc lập với receipt booking. Bước tiếp theo là nhận wire prefill và bản tích hợp Operations/Facility/Payment, thống nhất own reads và chạy lại theo ma trận trên.
- [UNVERIFIED] Lượt chạy chọn lọc test .NET cho S06 build được project IoT, nhưng VSTest không kết nối testhost sau 90 giây. Không tính lượt này là backend test pass.

## Smoke test Docker và hướng dẫn bàn giao · 10/11/2026

- Chạy `pwsh -NoProfile -File scripts/test-assistant-docker-setup.ps1` từ source hiện tại. Docker build và stack PostgreSQL cách ly đạt các ca schema, chạy lại setup, JWT Admin/Driver, FAQ mẫu, lịch sử và reset. Container và volume fixture đã được dọn.
- Xác nhận `setup.ps1` và runner `run-app-service.ps1` của nhóm đã gọi setup Assistant. Bổ sung đường chạy smoke cách ly và preflight stack nhóm trong `LOCAL-SETUP.md`. [UNVERIFIED] Tìm bãi và tra lịch với API thật cần HTTPS origin và dữ liệu QA của owner.
- Rà trước commit trên working tree. Suite IoT đạt 500, skip 7 ca opt-in và worker, frontend đạt 280/280. Đồng bộ trạng thái S06/S07 và Docker trong tài liệu tổng quan hiện có. Bàn giao cần kèm hai file mới `assistantBookingPreview.ts` và `assistant-booking-preview.test.cjs`.

## Đối chiếu tích hợp UserService theo SPARK-273 · 11/11/2026

- Kiểm tra trên `AI-ChatBot` tại `f40bfc97`, working tree ban đầu sạch. Ref SPARK-273 cục bộ tại `5daa4cc6`, chưa fetch remote trong lượt này.
- Assistant đã có xác thực RS256 bằng public PEM, actor từ claims, adapter `GET /api/vehicles` và isolation theo Account/role. Git ghi nhận các phần này ở commit của Dang Tuan Kiet (`6c93e477`, `dd6ff401`, `5941c72a`), cùng wiring owner reads `20e1be11` và test `a54873bc`. Các commit UserService của Quangthm về JWT/JWKS (`aa341d7f`) và xe (`62a87917`) đã nằm trong lịch sử nhánh hiện tại.
- Commit admission/Account-current `0a2383bd` của SPARK-273 chưa là ancestor của HEAD. Ref SPARK-273 có Account-current và bổ sung `CanonicalPlate` vào vehicle binding. Không áp riêng các thay đổi này lên nhánh Assistant vì cần tích hợp workflow và đối chiếu contract cùng Operations/UserService. Assistant dùng public PEM theo contract hiện tại, không tự chuyển sang JWKS hoặc thêm Saga vào luồng đọc.
- Kiểm tra local bằng checker hiện có với Windows PowerShell 5.1 và `-ExecutionPolicy Bypass`. Cả User/Operations/Search reads đang tắt. Issuer/audience Assistant trong `.env` khác effective defaults của User runner/Compose khi các biến User không được cấu hình. Chưa đối chiếu cấu hình tiến trình thực tế hoặc khóa đang phát token, nên giữ nguyên cấu hình local.
- Docker Desktop Linux pipe không tồn tại. Các probe User health/JWKS cổng 5035, Assistant Docker status cổng 5076 và Assistant host theo URL local đều không truy cập được. [UNVERIFIED] JWT UserService thật qua Assistant, đọc xe thật, hai Account/xe test và Account-current xuyên service. Cần runtime sẵn sàng, HTTPS owner origin và hai Driver test trước khi nghiệm thu.
- Test chọn lọc IoT cho `AssistantHttpContractTests|AuthenticatedChatTests` đạt 135, skip 3 sau khi chạy ngoài sandbox. Lượt UserService build được project test, VSTest timeout kết nối testhost sau 90 giây, không có kết quả pass. Người dùng sau đó yêu cầu lấy Docker đang dùng làm môi trường nghiệm thu chính.
- Phạm vi thay đổi hiện tại là nhật ký đối chiếu. Không sửa source UserService/Operations/Assistant, cấu hình local hoặc lịch sử Git trong lượt này.

### Kiểm trực tiếp stack Docker đang dùng · 11/11/2026

- Theo yêu cầu người dùng, khởi động Docker Desktop và kiểm chính các container `smartpark-app-*` cùng DB hiện có. Docker Engine 29.9.3 hoạt động. Các lệnh inspect/HTTP local cần chạy ngoài sandbox vì named pipe và localhost bị sandbox chặn.
- UserService thật cổng 5035 phát token có issuer `SmartParking.UserService.Demo`, audience `SmartParking.Postman.Demo`, trùng cấu hình Assistant cổng 5076. Nguồn hai giá trị phía User là `appsettings.Development.json` trong image đang chạy. Kết luận so với runner defaults ở bước static phía trên không phải lỗi trust giữa hai container thực tế.
- Đăng nhập bằng Admin và Owner được cấu hình sẵn trong container theo lựa chọn người dùng. Cả hai login, `/api/auth/me`, Assistant `/capabilities` và GET `/conversations` đều HTTP 200. API `/api/vehicles` trả 403 `FORBIDDEN` cho hai role này. Token thật bị sửa chữ ký trả 401 `INVALID_TOKEN` ở Assistant. Đã logout các phiên smoke sau kiểm tra, HTTP 200. Không ghi token/password hoặc dữ liệu cá nhân vào kết quả.
- GET `/.well-known/jwks.json` của User Docker trả 404 `NOT_FOUND`. Image User hiện tại là `7fb7046fbdb6d406bc10973f5150cd834f5f2efd6d245609316dc201a3295d60`, tạo 08/11/2026. Image chứa cấu hình Development khác source checkout hiện tại. Các kết quả này chứng minh hành vi deployment đang chạy, chưa xác nhận image được build từ HEAD `f40bfc97`.
- Admin inventory qua API xác nhận có một Driver, nhưng không có tài khoản demo `driver@gmail.com`. Login demo nhận 401 `AUTH_FAILED`. `.env` và cấu hình container có tài khoản test Admin/Owner, không có thông tin đăng nhập Driver test. Browser inventory không có tab hoặc phiên Driver sẵn để tiếp tục.
- History Admin/Owner đang rỗng nên ca đọc hội thoại chéo Account chưa chạy. [UNVERIFIED] Driver thật qua Assistant, dữ liệu xe positive/empty, hai Driver A/B, TOKEN hết hạn và REQUEST Account-current xuyên service. Cần phiên Driver test hợp lệ, HTTPS owner origin được container tin cậy và đối chiếu/rebuild image từ source được giao trước nghiệm thu các ca này. Các cờ reads trong `.env` vẫn ở trạng thái tắt khi kiểm tra.
- Script chẩn đoán local nằm trong `.cache/assistant-user-docker-diagnose.ps1` bị Git ignore. Script giữ credential/token trong bộ nhớ và chỉ xuất mã HTTP, code cùng metadata công khai.

### Rebuild và kiểm lại Docker hiện có · 11/11/2026

- Người dùng yêu cầu build lại rồi test trực tiếp trên stack Docker đang dùng. Build User bằng `docker compose --project-name smartpark-app --env-file .env -f compose.app.yml build user-service` đạt. Chạy lại User bằng runner repo với `-Service User -NoBuild -NoLogWindow`. Image mới `cf1a73fc5ef77c0339175b408181421078b7c48af8a003e02405bd249bc674bb` healthy.
- Chạy `scripts/run-app-service.ps1 -Service Assistant -NoLogWindow` để build/cập nhật Assistant. Image mới `d05e553105d180cf03d8974a33c569fc4e63e209f011bf52c47a48f582b8f592` healthy. Runner dùng DB, public key, PFX và host keyring hiện có. Đối chiếu public key từ khóa ký User với public PEM local và PEM Assistant đang chạy đều khớp. Tên keyring host/container khớp. Metadata User đã có `last_refreshed_at` và `legacy_onboarding_complete`.
- Cấu hình local bổ sung `SMARTPARK_JWT_ISSUER` và `SMARTPARK_JWT_AUDIENCE` theo bộ trust Demo đang chạy. Đồng bộ public `VITE_TURNSTILE_SITE_KEY` đã có từ `FE/.env` sang root `.env`, rồi cập nhật frontend bằng runner `-Service Frontend -NoBuild -NoLogWindow`. Docker frontend trước đó nhận site key rỗng trong khi User bật Turnstile. Giao diện sau cập nhật hiện Cloudflare Success. UserService mới từ chối login thiếu token bằng 400 `BOT_VERIFICATION_REQUIRED`; người dùng đã đăng nhập Driver qua giao diện thật để tiếp tục test.
- User JWKS sau rebuild trả 200, đúng một key, modulus/exponent khớp public PEM Assistant. Assistant status `enabled=true`, `guestFaq=true`. Gửi một FAQ công khai thật trả 200, `mode=knowledge`, `outcome=answered`, một source. Đây là pass cho knowledge fallback, chưa chứng minh provider generation. Assistant thiếu JWT trả 401 `INVALID_TOKEN`.
- Driver thật mở được chat và lịch sử. Gửi `Xe của tôi` trả `Công cụ tra cứu này chưa được kết nối` do User reads đang tắt. Reload trang/mở lại widget vẫn thấy đúng câu hỏi và câu trả lời đã lưu. [UNVERIFIED] Hai Driver A/B, positive owner-read data và REQUEST Account-current.
- Màn hình My Vehicles ban đầu lỗi. Log Docker đã lọc xác định PostgreSQL 42703 `column v.binding_revision does not exist`. Metadata xác nhận bảng vehicles cũ thiếu đúng cột đã có trong schema gốc SPARK-243. Soạn migration `scripts/database/migrations/20261111_user_vehicle_binding_revision.sql`. Người dùng duyệt rõ `Duyệt sao lưu và áp migration` trước khi thay DB hiện có.
- Backup User DB custom-format tại `.cache/user-docker-backups/user-before-binding-9654c35a12c843638f73db46ccc4802b.dump` (85.655 byte), kiểm được catalogue archive. Áp migration và chạy lại đều đạt. Cột mới `bigint NOT NULL DEFAULT 1`, có check dương. Đối chiếu count users/accounts/vehicles/vehicle_registration_claims trước và sau khớp. Chỉ kiểm catalogue backup, chưa thực hiện restore rehearsal trong lượt này.
- Refresh My Vehicles trên phiên Driver đang đăng nhập đã hết lỗi và hiển thị `No vehicles yet`. Không có xe mẫu để nghiệm thu positive vehicle card. Kết nối Assistant→User vẫn cần HTTPS origin được container tin cậy rồi bật `Assistant__Reads__User__Enabled`; source read contract hiện yêu cầu HTTPS trong khi app stack đang cung cấp HTTP nội bộ.
- Thay đổi tracked gồm migration nâng cấp User và nhật ký này. Các điều chỉnh `.env` cùng script chẩn đoán/backup nằm local bị Git ignore. UserService/Assistant được build từ source HEAD `f40bfc97`; migration xử lý tương thích DB hiện hữu phát hiện khi chạy thực tế.

### Tiếp tục HTTPS User reads và review độc lập · 11/11/2026

- Người dùng yêu cầu tiếp tục và tạo subagent review độc lập sau mỗi phần. Working tree đầu lượt giữ migration và nhật ký từ lượt trước. Review migration trước đó không thấy blocker với schema đã quan sát, ghi giới hạn kiểm schema drift khi gặp default/constraint khác chuẩn.
- Thêm overlay `compose.user-tls.yml` cho HTTPS `user-service:8443` trong mạng Docker, và `compose.assistant-trust.yml` để cài thêm public CA mỗi lần Assistant khởi động. Runner User/All kiểm PFX TLS và dùng overlay khi opt-in. Setup Assistant kiểm public PEM còn hạn và dùng overlay trust khi được cấu hình. Khai báo mẫu `.env.example` và hướng dẫn trong LOCAL-SETUP.
- Review độc lập wiring phát hiện TLS preflight chặn service ngoài User/All và process environment có thể ghi đè PFX/password đã kiểm từ `.env`. Đã scope validation/overlay vào User/All, bridge hai giá trị TLS trong từng `Invoke-Docker` và restore trong finally. Reviewer đọc lại không thấy blocker mới.
- Tạo CA local và server TLS certificate riêng, SAN `user-service`, thời hạn server 90 ngày. CA public và PFX nằm trong `.cache` bị ignore, password chỉ lưu `.env` local. Khóa CA private chỉ tồn tại trong bộ nhớ lúc ký. Bật User TLS, CA trust và `Assistant__Reads__User__Enabled=true`, origin `https://user-service:8443`. Các key signing JWT và keyring tiếp tục dùng bộ đã đối chiếu trước đó.
- Chạy hai runner bằng Windows PowerShell 5.1 với image hiện có. User và Assistant healthy sau recreate. Curl từ Assistant trước trust bị từ chối certificate (exit 60). Sau trust, `/health` trả 200, `/api/vehicles` thiếu JWT trả 401, hostname sai vẫn exit 60. Trust và read flag được cấp qua Compose, không chỉ thay đổi bằng docker exec trong container.
- Qua widget trên phiên Driver thật, câu `Xe của tôi` đã đi qua HTTPS và trả kết quả owner. Danh sách hiện rỗng, nhưng nội dung cũ chỉ nói câu giới thiệu chung. Sửa ReadTools để nói rõ không tìm thấy xe được phép xem theo locale vi/en và giữ outcome empty/cards như contract.
- Subagent review phần sửa empty response phát hiện test đọc sai `history.messages` thay vì `history.items`. Lượt test đầu tái hiện đúng lỗi đó ở 2 case. Đã sửa test và reviewer kiểm lại không thấy finding mới. Test chọn lọc JWT/actor/owner/history đang chạy lại trước khi build image chứa phản hồi mới.
- Chờ dữ liệu xe positive và Driver B để nghiệm thu runtime hai Account. Các test fixture của own-read không thay thế bằng chứng Docker thực tế này.
- Người dùng xác nhận `Hiện chỉ kiểm được Driver này`, nên phạm vi runtime hiện tại là Driver đang đăng nhập và tập xe rỗng. Giữ positive vehicle cards và hai Driver A/B là UNVERIFIED.
- Chạy lại bộ `AssistantHttpContractTests|AuthenticatedChatTests` đạt 137 pass, 3 skip (các ca PG opt-in/worker), 0 fail. Hai ca empty vi/en kiểm cả replay và `history.items`. Build toàn solution `--no-restore --nologo -m:1` đạt 0 error, một warning NU1510 đã có.
- Kiểm hồi quy trực tiếp Docker/Windows PowerShell 5.1 với process env cố tình chỉ PFX/password khác: runner dùng giá trị đã kiểm trong `.env`, User tiếp tục healthy, process overrides được khôi phục sau lệnh. Đây là kiểm lại finding về precedence đã sửa.
- Rebuild Assistant bằng runner repo, image mới `764e537fce9be243b88f7f04e2ea590884e121f4ba1246563b57fd6a815593c4`. Container recreate và healthy, overlay CA được cài lại qua entrypoint. Qua widget Driver thật, lượt `Xe của tôi` mới trả `Không tìm thấy xe được phép xem cho tài khoản hiện tại.`. Reload/mở lại widget vẫn giữ phản hồi đó. Các câu cũ trong history giữ nội dung lúc xử lý ban đầu.
- Reviewer cuối độc lập kiểm toàn delta, tự kiểm Docker read-only và xác nhận hai container healthy, PFX chỉ ở User, Assistant nhận public CA, HTTPS health 200, xe thiếu JWT 401 và hostname sai exit 60. Không phát hiện blocker mới. Reviewer phân biệt đúng kết quả tự kiểm với suite/build/phiên Driver/recreate do main thực hiện. Các review trước bao gồm migration, wiring HTTPS và empty response; findings đã được sửa và review lại.

### Chuẩn bị Driver vehicle card và khóa Docker preflight · 12/11/2026

- Bổ sung seed demo idempotent cho Driver `driver@gmail.com`: vehicle `51A-123.45`, UUID cố định `e0000000-0000-4000-8000-000000000001`, không yêu cầu media storage. Seed không sửa quyền sở hữu khi UUID đã tồn tại; kiểm tra sau seed yêu cầu đúng Driver Account, UUID và biển số.
- `run-app-service.ps1` hiện kiểm tra PFX User Docker có private key, còn hạn và DNS SAN `user-service`. `setup-assistant-docker.ps1` yêu cầu CA public khi bật User read và chặn origin `https://user-service:8443` nếu TLS overlay chưa bật. LOCAL-SETUP ghi lại thứ tự chạy trên máy leader.
- Review chéo độc lập vòng 1 phát hiện hai lỗi seed ownership/verification; đã sửa. Review vòng 2 đang chờ kết quả.
- Validation: PowerShell parse ba script pass; `git diff --check` pass; `dotnet build SmartParking.slnx --nologo --no-restore -m:1` pass 0 error (warning NU1510); `node --test tests/frontend/*.test.cjs` pass 280/280. `dotnet test` filtered trong sandbox trả exit 1 không có output, chưa tính là pass. Docker Compose runtime config chưa xác nhận vì lệnh bị auto-review timeout.
- Review chéo độc lập vòng 1 đã tìm và mình đã sửa hai P2: seed không còn `ON CONFLICT DO UPDATE` đổi ownership; verification yêu cầu đúng UUID/biển số/Driver Account. Vòng 2 đã được tạo nhưng agent không trả kết quả trong thời gian chờ; giữ kết quả vòng 1 làm bằng chứng và tự chạy lại static checks.
- Chạy lại PowerShell parse, `git diff --check`, và `scripts/test-assistant-docker-setup.ps1 -PreflightOnly -NoBuild`: pass. `scripts/test-setup.ps1` không pass trong process hiện tại vì fixture phát hiện signing variables kế thừa vào child consumer; không liên quan thay đổi task và chưa coi là pass.

### Bổ sung parser booking tự nhiên trước demo · 12/11/2026

- Tái hiện ba mẫu P0 trong source hiện tại: hai mẫu `tại/ở Bitexco ... ngày mai ...` bị gộp ngày/giờ vào tên bãi; mẫu `Tôi muốn đặt chỗ tại bãi Bitexco ...` không có loại xe nên không được suy ra CAR.
- Sửa `assistantBookingPreview.ts` để tách timing nằm sau site label. Bổ sung hai test hồi quy; kết quả đúng cho hai mẫu CAR và giữ `vehicleType=null`/missing fields cho mẫu không nêu loại xe.
- Review chéo độc lập sau sửa không phát hiện lỗi. `node --test tests/frontend/*.test.cjs` đạt 282/282; bộ parser đạt 14/14. Runtime Docker positive vehicle card vẫn [UNVERIFIED] vì chưa chạy được Docker API trong sandbox; seed script đã sẵn sàng cho máy leader.

### Xác minh setup và checklist máy leader · 12/11/2026
- Chạy lại test-setup.ps1 bằng Windows PowerShell 5.1.26100.9818: exit 0, toàn bộ fixture PASS. Lỗi trước ở PowerShell 7 do khác biệt xóa biến môi trường null thành empty; chưa có bằng chứng lộ signing value. Review độc lập xác nhận; đã bỏ hai sửa đổi suy đoán trong runner/JWT helper.
- Docker Assistant PreflightOnly: PASS. Docker runtime hiện UNVERIFIED: pipe dockerDesktopLinuxEngine không tồn tại, không có bằng chứng container healthy ở lượt này.
- Thêm docs/04-testing/assistant-leader-demo-checklist.md: setup/build đúng máy mới, kiểm card/history/A-B/parser, giới hạn seed-demo-site và giữ booking/payment thuộc owner khác.

### Chỉnh hướng dẫn theo phản hồi tester — 13/11/2026

- Viết lại mục setup thành các bước copy/chạy, thêm khối dotenv giá trị local đầy đủ cho máy mới và khối riêng cho máy đã có JWT.
- Tách rõ mật khẩu tự sinh phải giữ nguyên, provider key nhập UI, Turnstile local smoke và cặp key QA cần tự điền. Không sửa `.env` thật.
- Chuyển bảng cấu trúc kỹ thuật xuống cuối tài liệu. Giữ test cases và giới hạn tính năng; kiểm link/fence/case ID và `git diff --check`, không chạy lại runtime vì chỉ đổi tài liệu.

### Review chéo bộ câu hỏi từng role và API — 13/11/2026

- Yêu cầu mới: giữ đầy đủ 80 câu hỏi user đưa, có expected rõ theo role và hướng dẫn setup/API copy được. Ba subagent độc lập `questions_audit`, `api_setup_audit`, `role_faq_audit` rà code/corpus/contracts và review chéo bản Markdown.
- Thêm 4.6: 80 câu nguyên văn, chia chung 11 / Driver 18 / Admin 14 / Owner 18 / Operator 19; giữ 76 TC trước đó. Phân biệt mong đợi FAQ, đọc thật, draft FE, thiếu nội dung và kiểm giới hạn; không coi safe refusal là hoàn thành nghiệp vụ.
- Thêm 2.6: Postman environment, status/Guest JSON, Bearer đúng role, GET current trước create, history/revision/send/lookup/replay, Admin settings read và lỗi phổ biến. Provider keys vẫn nhập qua UI, không chép secrets vào docs.
- Reviewer chạy probe .NET offline với classifier/retrieval source hiện tại và 31 câu FAQ trên corpus repo; parent chạy lại binary để đối chiếu. Kết quả tại 4.7 ghi unknown và wrong-source (đăng ký tài khoản → partner, đổi giá → allocation, thiếu mã booking → pass cancellation). Đây là phát hiện GAP chất lượng, không provider/API/runtime evidence.
- Không sửa code, quyền nghiệp vụ, corpus publication hoặc cấu hình local trong lượt này. Full suite trước đó không nhận là rerun lượt docs. Cross-review sửa token environment/Vault syntax ambiguity và hướng dẫn sinh UUID bằng PowerShell.
- Handoff còn cần QA môi trường thật và phát triển retrieval/knowledge/tool theo phạm vi được duyệt; tài liệu không hứa tất cả câu hiện đã hoạt động.

### Pre-commit readiness review — 13/11/2026

- Rechecked current dirty candidate without staging/committing. Initializer (fresh and existing FILE/PEM/Base64) and discovery fixture rerun PASS; full Node frontend 298/298 PASS; diff whitespace and guide links/fences/JSON PASS. Changed/untracked text scan found no private-key payload/provider-token patterns; local `.env`, `.env.bak` and `.cache` are not tracked. Pattern scan is not a comprehensive secret audit.
- Prior current-code solution build/test and Docker/PG evidence remains as recorded; no backend edits since those runs, no unnecessary broad rerun. This review does not replace target-machine UI/provider acceptance.
- Verdict: GO for QA candidate with known gaps, NOT full 80-question functional acceptance or production release. FAQ unknown/wrong-source issues remain in guide 4.7; tester must receive approved corpus, provider access and QA account setup. All new compose/helper/initializer/test scripts must be included in the eventual user commit; tracked-file-only commit would omit required dependencies. No Git write action performed.
