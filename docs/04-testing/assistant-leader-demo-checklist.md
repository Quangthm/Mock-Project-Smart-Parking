# Nghiệm thu AI Chatbot trên máy leader

> Cập nhật 12/11/2026, chuẩn bị demo 13/11/2026. Hướng dẫn này dành cho tester thao tác; phần kiến trúc/vấn đáp ở [tổng quan hệ thống](../03-design/ai/system-overview-and-review.md). Nhãn nút dưới đây được đối chiếu source FE; lượt hiện tại chưa chạy lại toàn bộ UI vì công cụ trình duyệt bị chặn.

## 0. Kết luận sẵn sàng và chọn kịch bản

**Đủ để giao tester kiểm thử phạm vi cốt lõi, chưa đủ để tuyên bố toàn bộ demo end-to-end đã PASS.** Trên máy hiện tại đã chạy Docker thật: User/Assistant healthy, frontend HTTP 200, status `enabled=true`, `guestFaq=true`; hỏi `SmartPark là gì?` trả `answered`, nguồn `smartpark-overview` phiên bản `0.4.0`, mode `knowledge`. Đây là fallback từ tài liệu, không phải bằng chứng lượt đó do model sinh.

| Phần | Bằng chứng hiện có | Quyết định demo |
| --- | --- | --- |
| Docker + FAQ công khai có nguồn | Runtime API PASS ngày 12/11 | Kịch bản chính sau khi tester mở UI xác nhận |
| Settings provider | Source + regression, chưa nghiệm thu lại UI/provider thật trong lượt này | Chỉ demo cấu hình đã được tester kiểm; không rotate/revoke key đang dùng |
| Chat Driver/history/reset/recovery | Backend/FE regression; UI thật lượt này chưa xong | Chỉ đưa vào kịch bản chính khi TC-D1/D2 bên dưới PASS |
| Parser và bản nháp FE | 14/14 parser tests; toàn FE 282/282 | Demo đúng câu mẫu, nói rõ chưa đặt chỗ |
| Vehicle card dương | HTTPS/empty từng kiểm trước; card có xe chưa nghiệm thu | Tuỳ chọn, chỉ demo sau TC-V1 PASS |
| A/B isolation | Có automated tests; chưa nghiệm thu 2 Driver thật | Ghi UNVERIFIED nếu thiếu Account B |
| Lịch đặt/search/availability/quote/booking/payment | Chưa nghiệm thu tích hợp; ngoài phạm vi đợt này | Không demo |

Backend lượt 12/11: 502 PASS, 7 SKIP, 0 FAIL. FE build PASS (có cảnh báo bundle lớn), 282/282 tests PASS. Các test skip không được tính thành pass. Không gọi bộ test toàn solution xanh từ các con số này.

### Checklist trước khi tester nhận branch

1. Người bàn giao commit đủ tracked và untracked cần thiết; ghi SHA thực tế vào biên bản. Hiện working tree chưa phải candidate đã commit.
2. Đặc biệt kiểm đủ hai Compose overlay, migration `20261111_user_vehicle_binding_revision.sql`, test `test-user-binding-preflight.ps1` và tài liệu này. Không chỉ push HEAD cũ.
3. Không đưa `.env`, private PEM, PFX, keyring, provider key, token hoặc `.cache` vào commit.
4. Người setup bàn giao cấu hình riêng qua kênh nội bộ được phép, hoặc chuẩn bị lại bộ key trên máy mới theo LOCAL-SETUP. Clone source không tự tạo đủ key/certificate/publication/provider credential.
5. Tester ghi máy/OS, PowerShell, SHA, thời điểm và kết quả từng ca. Phần không có điều kiện kiểm ghi BLOCKED/UNVERIFIED, không đánh PASS.

Phạm vi: FAQ, Settings, chat/history/recovery, đọc xe của Driver và bản nháp nhập liệu FE. Booking thật, quote/terms, availability, tạo reservation và payment thuộc thành viên phụ trách; không bật các capability này để vượt qua demo.

## 1. Chuẩn bị máy

- Dùng checkout có đủ thay đổi Assistant, gồm `compose.user-tls.yml`, `compose.assistant-trust.yml` và migration User vehicle binding. Các file chưa commit không tự đi theo khi clone/pull branch.
- Chuẩn bị cấu hình theo [LOCAL-SETUP](../../LOCAL-SETUP.md): JWT public key khớp UserService, keyring/PFX gốc nếu dùng lại DB, knowledge publication, TLS PFX SAN `user-service` và CA public. Mọi path local phải tồn tại trên máy leader; path Assistant có thể ghi tương đối so với `.env`. Không đưa khóa bí mật vào Git hoặc báo cáo test.
- Khởi động Docker Desktop; `docker info` phải thành công trước khi chạy setup. Giữ password của volume hiện có. Không reset volume để xử lý lỗi cấu hình.
- Đặt `Assistant__Reads__User__Enabled=true` khi TLS/trust đã sẵn sàng; giữ Operations/Search tắt nếu chưa được owner nghiệm thu.

## 2. Kiểm tra và chạy

Chạy fixture setup bằng **Windows PowerShell 5.1+ hoặc PowerShell 7+**. Fixture dùng dữ liệu giả và không chứng minh app thật healthy.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-setup.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-assistant-docker-setup.ps1 -PreflightOnly -NoBuild
.\setup.ps1 -AssistantCheck
```

Sau khi đã chuẩn bị đủ cấu hình, dùng quy trình setup trong LOCAL-SETUP để provision/migrate DB. Build app bằng runner; không dùng `-NoBuild` trên máy mới hoặc sau thay đổi source:

```powershell
.\setup.ps1 -SkipRestore
.\scripts\run-app-service.ps1 -Service User -NoLogWindow
.\scripts\run-app-service.ps1 -Service Assistant -NoLogWindow
.\scripts\run-app-service.ps1 -Service Frontend -NoLogWindow
docker ps --format "{{.Names}} {{.Status}}"
```

DB mới mặc định chưa bật chatbot trong Settings. Admin vào Settings, nhập provider key local, chọn model, bật provider/cấu hình và bật chatbot theo LOCAL-SETUP. Kiểm `/api/assistant/status` trên cổng Assistant thực tế và xác nhận `data.guestFaq=true` trước khi nghiệm thu FAQ; container healthy chỉ chứng minh tiến trình đang sống. Không ghi provider key vào bằng chứng.

Đăng nhập Driver demo đã được UserService tạo. Thêm xe qua My Vehicles và đối chiếu xe thuộc đúng Account. Nếu dùng dữ liệu QA đã được chấp thuận trên DB demo, `scripts/seed-demo-site.ps1` thêm xe `51A-123.45` cho `driver@gmail.com`; script này còn seed tenant/site/Operations và yêu cầu các DB tương ứng. Không coi đây là script chỉ thêm xe, không chạy trên dữ liệu dùng chung chưa thống nhất.

## 3. Ca nghiệm thu

| Ca | Kết quả cần quan sát | Bằng chứng |
| --- | --- | --- |
| Driver hỏi `Xe của tôi` | Card khớp xe được phép xem trong My Vehicles | Ảnh card và My Vehicles; không chụp token |
| Reload, mở lại chat | Lượt hỏi và card đã lưu vẫn xuất hiện | Ảnh sau reload |
| Driver không có xe | Thông báo rõ không tìm thấy xe được phép xem | Ảnh trạng thái empty |
| Đổi Driver A sang B | Không hiện chat/card riêng của A; B chỉ nhận xe được phép xem | Ghi hai Account QA và kết quả, không ghi mật khẩu |
| `Đặt chỗ cho ô tô tại Bitexco ngày mai từ 14h đến 16h` | Bản nháp CAR, Bitexco, ngày mai, 14:00–16:00 | Ảnh bản nháp; chưa tạo booking |
| `Đặt chỗ ô tô ở Bitexco ngày mai từ 14h đến 16h` | Cùng kết quả tách trường | Ảnh bản nháp |
| `Tôi muốn đặt chỗ tại bãi Bitexco ngày mai từ 14h đến 16h` | Đúng bãi/ngày/giờ, hỏi loại xe còn thiếu | Không tự suy đoán CAR |
| FAQ và mở lại hội thoại | Câu trả lời/lịch sử bình thường | Kết quả ngắn và thời điểm test |

Các ca JWT lỗi và account isolation đầy đủ theo [hướng dẫn QA](assistant-handoff-testing.md). Nếu chỉ có một Driver thì ghi A/B là **UNVERIFIED**. Nếu Docker daemon chưa truy cập được thì runtime cũng **UNVERIFIED**, dù fixture pass.

## 4. Mẫu ghi kết quả bàn giao

Ghi: commit/working-tree đang chạy, máy test, thời điểm, phiên bản PowerShell, trạng thái container, từng ca PASS/FAIL/UNVERIFIED và vị trí bằng chứng. Chỉ kết luận sẵn sàng demo vehicle card dương khi đã thấy card thật và giữ được sau reload. Nếu chưa đạt, demo trạng thái empty hoặc báo owner-read pending đúng thực tế.

## 5. Setup từng bước cho tester trên máy mới

### Bước 1 — Chuẩn bị và mở đúng thư mục

1. Mở Docker Desktop, đợi engine Running. Dùng Linux containers.
2. Nhận checkout đúng SHA từ người bàn giao, mở PowerShell ở thư mục chứa `setup.ps1`.
3. Chạy `docker info`; nếu lỗi pipe/daemon thì dừng, mở lại Docker Desktop. Chưa chạy setup khi Docker chưa sẵn sàng.
4. Máy cần các prerequisite trong LOCAL-SETUP; cần .NET 10 SDK cho restore/host tests, Node theo project cho FE tests/build, Windows PowerShell 5.1+ cho setup fixture và Docker fixture, PowerShell 7 vẫn có thể dùng cho đoạn tạo key mẫu. Build container cần truy cập registry/package feed.
5. Nếu chưa có `.env`, sao chép `.env.example` thành `.env`. Nếu đã có, giữ file hiện tại; bổ sung từng key thiếu. Đừng ghi đè password gắn với volume cũ.

### Bước 2 — Chuẩn bị cấu hình riêng (người setup thực hiện một lần)

| Nhóm | Tester/người setup phải có |
| --- | --- |
| JWT | User private key; Assistant public PEM tương ứng; issuer/audience khớp. Dùng hướng dẫn tạo key ở LOCAL-SETUP nếu DB/môi trường mới hoàn toàn |
| Mã hoá quản trị | Thư mục keyring bền vững, PFX có private key và password. DB cũ phải dùng bộ gốc; không tự thay keyring mới |
| Knowledge | Thư mục triển khai có `smartpark-knowledge.md` đã được duyệt và `publication.json` khớp hash/version. Source DRAFT trong repo chưa phải publication |
| Release flags | `SMARTPARK_ASSISTANT_DOCKER_ENABLED=true`, `Assistant__Guest__ReleaseEnabled=true`, `Assistant__Chat__ReleaseEnabled=true` |
| Owner read | Nếu chỉ FAQ/history/parser, giữ User/Operations/Search reads false. Nếu demo xe: bật User read và đủ TLS/CA theo bảng mục 1–2; Operations/Search vẫn false |
| Provider | Key/quota và model được Admin phép dùng; nhập trong Settings, không nhập vào FE hoặc Git |
| Đăng nhập | Account Admin và Driver QA; nếu bật Turnstile phải hoàn tất xác minh trong UI. Không tắt bảo vệ để giả lập test PASS |

Kiểm đường dẫn theo máy mới; không copy nguyên đường dẫn `D:\...` của người phát triển. Đọc bước tạo JWT/PFX trong [LOCAL-SETUP](../../LOCAL-SETUP.md#ai-chatbot-bằng-docker), và quy trình publication trong [knowledge-and-faq](../03-design/ai/knowledge-and-faq.md#publication-manual-admin-controlled-deployment). Nếu thiếu vật liệu này, ghi BLOCKED và yêu cầu người setup chuẩn bị; chưa thể chạy chỉ bằng một lệnh Docker.

### Bước 3 — Preflight và build lần đầu

Chạy lần lượt từ root; lệnh nào báo lỗi thì xử lý lỗi đó trước, không tiếp tục coi stack đã xong:

```powershell
.\setup.ps1 -AssistantCheck
.\setup.ps1 -SkipSonarQube
.\scripts\run-app-service.ps1 -Service User -NoLogWindow
.\scripts\run-app-service.ps1 -Service Assistant -NoLogWindow
.\scripts\run-app-service.ps1 -Service Frontend -NoLogWindow
docker ps --format "{{.Names}} {{.Status}}"
```

Setup khởi tạo hạ tầng/schema; các runner trên chỉ chọn app cần cho chatbot. Không cần build/chạy app Operations/Payment để demo FAQ/history/parser. Setup có thể khởi động các DB chung theo thiết kế repo; đó không phải triển khai booking/payment.

Máy mới dùng build mặc định; không thêm `-NoBuild`. Nếu Windows chặn chạy `.ps1`, dùng `powershell -NoProfile -ExecutionPolicy Bypass -File <script> <tham-số>` cho lần chạy, không cần thay execution policy toàn máy.

Với DB cũ, nếu báo thiếu `vehicles.binding_revision`, dừng và theo mục migration trong LOCAL-SETUP: sao lưu, người quản lý DB áp migration được chỉ định rồi chạy lại setup. Không dùng `-RecreateDb`/`down --volumes` để né lỗi. DB Assistant đã có dữ liệu còn cần đúng keyring/PFX và opt-in dùng lại theo LOCAL-SETUP.

### Bước 4 — Xác nhận kết nối

1. Mở `http://localhost:5173` (hoặc cổng FE đã cấu hình): phải tải được trang, không trắng trang.
2. Mở `http://localhost:5076/health/live`: phản hồi thành công; trang có thể trống do status 204.
3. Mở `http://localhost:5076/api/assistant/status`: cần `success=true`. Sau bật Settings ở bước 5 phải có `data.enabled=true`, `data.guestFaq=true`.
4. Container healthy chỉ kiểm liveness; status/FAQ mới kiểm ứng dụng sẵn sàng. Nếu FE dùng host runner thay vì Docker, kiểm `VITE_ASSISTANT_API_BASE_URL` trong `FE/.env`; runner giữ giá trị đã có, không tự ghi đè.
5. URL localhost là máy đang mở trình duyệt. Các bước này dành cho tester chạy cùng máy Docker, không phải cấu hình truy cập LAN từ máy khác.

### Bước 5 — Admin bật AI Chatbot

1. Trên FE bấm nút đăng nhập, nhập Account Admin QA, hoàn tất Turnstile/MFA nếu có. Không dùng tài khoản Driver cho Settings.
2. Vào dashboard Admin → mục **Settings** → kéo tới khối **AI Chatbot**. Nếu UI không vào được, ghi lỗi đăng nhập/route, chưa đánh test chatbot fail.
3. Bấm **Reload settings**; phải tải được revision và các nhóm Gemini/OpenAI. `Management service is not connected` nghĩa là endpoint FE chưa cấu hình; `Credential store unavailable` nghĩa là keyring/certificate chưa sẵn sàng.
4. Nếu đã có cấu hình QA hợp lệ, dùng lại cấu hình đó. Nếu chưa có: tại provider đã được phép dùng bấm **Add configuration**.
5. Điền **Configuration name** (ví dụ `QA demo`), **New API key** (key riêng), bấm **Load models**, chọn **Model**, bấm **Save disabled configuration**. Không chụp key trong ảnh bằng chứng.
6. Bấm **Test connection** tại cấu hình vừa lưu; ghi kết quả. Test connection thành công chưa chứng minh generation FAQ thành công.
7. Bấm **Enable** trên cấu hình, bật switch provider tương ứng, bật switch **System chatbot**. Đợi trạng thái **Settings saved.**
8. Bấm **Reload settings**, xác nhận trạng thái vẫn Enabled; mở lại status và xác nhận readiness. Nếu muốn chứng minh model chạy, cần response FAQ `mode=ai`; mode `knowledge` chỉ xác minh fallback.
9. Không thử **Revoke**/**Replace key** trên key phục vụ demo chính. Những ca đó chỉ chạy trên cấu hình QA riêng.

## 6. Tester click theo từng ca

### TC-G1 — Guest FAQ có nguồn (P0)

1. Mở cửa sổ ẩn danh hoặc đăng xuất; vào trang chủ FE.
2. Bấm biểu tượng chat ở góc dưới; khung chat phải mở.
3. Bấm ô nhập câu hỏi, nhập `SmartPark là gì?`, bấm nút gửi.
4. Đợi trả lời. Phải mô tả SmartPark theo tài liệu, không tự tạo booking/payment hoặc số chỗ còn trống.
5. Ghi nội dung, lỗi nếu có và mode/source trong DevTools Network → request `/guest/messages` → Response. Không chụp headers/token.
6. Gửi `Xe của tôi`: phải hướng dẫn đăng nhập, không hiện dữ liệu riêng.
7. Gửi một câu ngoài phạm vi như `Dự báo giá bitcoin ngày mai`: phải từ chối/hướng dẫn phạm vi; không bịa câu trả lời nghiệp vụ.
8. Chụp UI và ghi PASS/FAIL từng câu. Guest không có lịch sử bền vững sau reload là giới hạn thiết kế.

### TC-D1 — Driver và lịch sử bền vững (P0)

1. Đăng nhập Driver QA đã có. Tài khoản dev mặc định là `driver@gmail.com`; mật khẩu demo được ghi trong LOCAL-SETUP, dùng credential được người setup xác nhận nếu đã đổi.
2. Vào dashboard Driver; bấm nút chat góc dưới có nhãn truy cập **Trợ lý · Lịch sử**.
3. Trong ô **Câu hỏi**, nhập `SmartPark là gì?`, bấm nút **Gửi**.
4. Đợi phản hồi hoàn tất; ghi một đoạn nội dung để đối chiếu.
5. Bấm **Đóng**, mở lại chat: câu hỏi và phản hồi vẫn còn.
6. Bấm F5, chờ dashboard tải và mở chat lại: cùng lượt hỏi/đáp phải còn; không tự tạo bản sao lượt gửi.
7. Nếu phản hồi có tiêu đề nguồn dạng mở rộng, bấm tiêu đề để xem excerpt. Đối chiếu nguồn với response.
8. Ghi PASS chỉ khi cả gửi, đóng/mở và reload đều thành công với Account thật.

### TC-D2 — Tạo chat mới (P0, dùng hội thoại QA)

1. Trong chat đã có tin QA, bấm **Mới**.
2. Hộp **Tạo đoạn chat mới?** xuất hiện. Bấm **Giữ đoạn chat**: lịch sử phải còn.
3. Bấm **Mới** lần nữa, đọc thông báo rồi bấm **Tạo chat mới** trên hội thoại QA được phép reset.
4. Chat hiện tại phải trống. Gửi FAQ mới rồi F5: chỉ hội thoại hiện tại được mở lại.
5. Giải thích: reset ẩn hội thoại ngay, purge theo retention sau 7 ngày nếu không có hold; không nói dữ liệu bị xoá vật lý ngay.

### TC-P1 — Bản nháp đặt chỗ, không tạo booking (P0)

1. Mở chat trên dashboard Driver. Nhập `Đặt chỗ cho ô tô tại Bitexco ngày mai từ 14h đến 16h` rồi **Gửi**.
2. Phải thấy **Bản nháp đặt chỗ**, loại xe **Ô tô**, bãi **Bitexco**, ngày mai theo giờ Việt Nam, 14:00–16:00; xe cụ thể còn thiếu.
3. Bấm **Bỏ bản nháp**. Thử `Đặt chỗ ô tô ở Bitexco ngày mai từ 14h đến 16h`: kiểm cùng các trường.
4. Bấm **Bỏ bản nháp**. Thử `Tôi muốn đặt chỗ tại bãi Bitexco ngày mai từ 14h đến 16h`: bãi/ngày/giờ đúng, loại xe **Chưa nhập**, phải hỏi thêm thay vì tự gán CAR.
5. Kiểm dòng **Thông tin nhập trong phiên này. Xe, bãi và chỗ trống đang chờ xác minh.**
6. Bản nháp chỉ trong bộ nhớ widget; reload có thể mất. Đây không phải lỗi history. Không bấm/đòi tạo booking hoặc thanh toán; chưa có wire sang form xác nhận.

### TC-V1 — Xe của tôi (tuỳ chọn, P0 nếu chọn demo card)

1. Người setup xác nhận User reads bật, TLS/CA đúng, User container healthy.
2. Trong Driver mở **My Vehicles**, đối chiếu tài khoản và xe QA được phép xem. Nếu trống, thêm xe theo form My Vehicles hoặc nhờ người setup chuẩn bị dữ liệu QA đã duyệt; không giả response.
3. Quay lại dashboard → mở chat → gửi `Xe của tôi`.
4. Nếu có xe: card phải khớp biển số/loại xe trong My Vehicles; không lẫn xe Account khác.
5. F5 → mở lại chat: card của lượt đã hoàn tất phải được phục hồi. History là kết quả đã lưu, không phải một lần tra cứu mới; gửi câu mới nếu muốn refresh dữ liệu owner.
6. Nếu Account không có xe: chấp nhận thông báo **Không tìm thấy xe được phép xem cho tài khoản hiện tại.**; ghi EMPTY PASS, không ghi POSITIVE CARD PASS.
7. Nếu báo chưa kết nối/lỗi nguồn: ghi BLOCKED/FAIL tương ứng; bỏ phần card khỏi demo cho đến khi sửa và kiểm lại.

### TC-A1 — Chuyển Account (P1)

1. Ghi trạng thái chat/xe Driver A, đăng xuất bằng UI.
2. Đăng nhập Driver B riêng đã được cấp, mở chat.
3. B không thấy hội thoại/card của A; gửi `Xe của tôi` phải lấy dữ liệu B hoặc empty đúng.
4. Đăng nhập lại A để đối chiếu hội thoại A. Không có B thì ghi UNVERIFIED, không lấy một Account đổi tên làm bằng chứng isolation.

### TC-R1 — Hủy/phục hồi (P1, không ép tình huống khi demo)

1. Gửi FAQ trên hội thoại QA; nếu UI hiện pending, dùng **Kiểm tra kết quả** để lấy trạng thái request cũ.
2. Nếu còn running, có thể bấm **Hủy yêu cầu** rồi kiểm lại. Server có thể đã hoàn tất trước hủy; phải hiển thị kết quả cuối thực tế.
3. Chỉ khi UI cho phép mới dùng **Gửi lại cùng mã**. Không tự tạo ID mới để gửi trùng.
4. Câu trả lời quá nhanh không đủ điều kiện kiểm pending/cancel thì ghi NOT EXERCISED. Không tự tắt DB/mạng trong demo chính.

## 7. Kịch bản trình bày 8–10 phút

1. **1 phút:** nêu phạm vi: FAQ có nguồn, quản trị provider, hội thoại, parser; backend owner quyết định dữ liệu nghiệp vụ.
2. **2 phút:** Guest FAQ và câu đọc riêng dẫn đăng nhập. Nếu mode knowledge, nói rõ fallback, không gọi đó là generation thành công.
3. **2 phút:** Driver FAQ → đóng/mở → reload lịch sử (chỉ dùng nếu TC-D1 đã PASS).
4. **1–2 phút:** ba mẫu parser, nhấn mạnh đây là bản nháp tạm chưa tạo giao dịch.
5. **1 phút:** Admin Settings có cấu hình/mô hình/thứ tự, key không trả về trình duyệt. Chỉ trình diễn thao tác đã rehearsal.
6. **Tuỳ chọn 1 phút:** card xe nếu TC-V1 positive PASS. Nếu không, trình bày empty đúng trạng thái.
7. **Kết:** mở sơ đồ trong tài liệu tổng quan, giải thích RAG và ranh giới Operations/Payment. Không demo ngẫu nhiên `Lịch đặt ngày mai`, search, quote, create hay payment khi capability đang tắt/chưa nghiệm thu.

## 8. Lỗi hay gặp và việc tester cần ghi

| Hiện tượng | Kiểm tra / xử lý |
| --- | --- |
| Docker pipe missing/permission denied | Docker Desktop Running, đúng Linux context, quyền truy cập Docker; không suy ra source lỗi |
| Không thấy widget | Kiểm FE API URL, status enabled/guestFaq, release flags và Settings; mở Network tìm request lỗi |
| 401/403 | Đăng nhập đúng role, token hết hạn/issuer/audience/public key; không sửa role hoặc JWT để qua test |
| BOT_VERIFICATION_REQUIRED | Đăng nhập qua UI và hoàn tất Turnstile; không dùng API login thiếu challenge để kết luận chatbot hỏng |
| TLS SAN/CA lỗi | PFX còn hạn, private key/password đúng, DNS SAN đúng `user-service`, CA ký khớp và mount tồn tại trên máy nhận |
| Thiếu binding_revision | Dừng ở preflight; backup và áp migration theo hướng dẫn; không reset dữ liệu |
| Knowledge unavailable | Kiểm file runtime/manifest/hash/version/approval/time; không sửa DRAFT thành approved mà chưa được duyệt |
| Trả knowledge thay vì ai | Ghi đúng mode; kiểm key/model/provider enabled, quota/timeout. FAQ fallback vẫn có thể dùng được |
| 409 khi Settings thay đổi | Reload settings, xem revision mới rồi thao tác lại; không lặp POST mù |
| 429 | Đợi rồi thử thủ công; không spam để vượt giới hạn |

Mẫu biên bản: `SHA | máy | thời điểm | TC | PASS/FAIL/BLOCKED/UNVERIFIED | expected | actual | ảnh/traceId | người kiểm`. Chỉ lưu response đã loại token/secret. Kết luận GO cho từng phân đoạn, không đánh GO toàn hệ thống khi chỉ FAQ pass.
