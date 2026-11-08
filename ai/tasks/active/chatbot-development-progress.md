# AI Chatbot — Quá trình phát triển backend

## 1. Thông tin
- Session gốc07/11, cập nhật08/11/2026; branch `AI-ChatBot`, mốc trước đợt chia commit là `3d3def76`.
- **In Progress**; một nhật ký active cho chatbot backend, không tạo file mỗi prompt.
- Review S02–S05/S09 và tổ chức tài liệu; frontend có owner riêng.
- Trạng thái Git và kiểm chứng mới nhất: xem mục **Chia commit để leader review** ở cuối file. Các checkpoint phía trên giữ bối cảnh tại thời điểm ghi.

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
