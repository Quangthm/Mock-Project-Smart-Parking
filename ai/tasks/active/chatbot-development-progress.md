# AI Chatbot — Quá trình phát triển backend

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
