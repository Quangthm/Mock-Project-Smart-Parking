# AI Chatbot — Hướng dẫn setup và test case cho tester

Cập nhật 08/11/2026. Dành cho QA, người dựng môi trường test và developer hỗ trợ lỗi. Đọc mục 1–3 để chuẩn bị, chạy smoke và test cốt lõi ở mục 4–5, chạy mục 6 khi owner đã sẵn sàng. Ghi kết quả và chốt theo mục 8–9. [Tổng quan cho leader](../03-design/ai/system-overview-and-review.md).

## 1. Phạm vi bàn giao

Bản này có Settings Admin, FAQ Guest, chat Driver, lịch sử theo Account, tra lại kết quả, hủy và phục hồi yêu cầu. Docker Assistant có đường setup riêng. Xe, lịch đặt và tìm bãi là nhóm test tích hợp có điều kiện.

Luồng người dùng nói “đặt ô tô từ giờ A đến giờ B” rồi mở form điền sẵn, xác nhận đặt chỗ và tạo thanh toán còn chưa triển khai. TC-BND-01 bên dưới kiểm khả năng từ chối an toàn của bản hiện tại. Nghiệm thu luồng đặt chỗ tương lai theo tài liệu leader.

| Nhóm chạy | Khi nào chạy | Ai chuẩn bị |
| --- | --- | --- |
| Cốt lõi | Settings + FAQ + Driver history khi UserService, IoT DB và corpus đã sẵn sàng | IoT developer + QA + Admin test |
| Provider thật | Có key test, model hợp lệ và quota được phép | Admin test + QA |
| Owner reads | Owner đã cung cấp HTTPS, JWT trust, dữ liệu fixture và cấu hình mở tính năng | User/Operations/Facility/Payment owner + IoT + QA |
| Sự cố DB, retention, rollback | Môi trường cách ly có dữ liệu giả lập và người vận hành | IoT developer/DevOps + QA |

Lưu cho mỗi đợt test tên nhánh, commit bản được bàn giao, ngày, hệ điều hành, trình duyệt, topology Docker/host và version corpus. Bằng chứng bên dưới được ghi nhận trên working tree nhánh `AI-ChatBot`, HEAD nền `3d3def76`. Tester phải ghi commit mới sau khi bản này được đóng gói.

## 2. Chuẩn bị môi trường

### 2.1. Công cụ và dữ liệu đầu vào

Cần Git, .NET SDK 10, Docker Desktop Linux containers và Docker Compose có `up --wait`. Runner hỗ trợ Windows PowerShell 5.1. PowerShell 7 cần cho công thức tạo khóa local và harness Docker tự động. Có kết nối tới registry Docker/NuGet ở lần build đầu. Node trên host cần khi chạy FE trực tiếp, Docker FE đã có Node.

Người phụ trách môi trường cung cấp những thứ sau qua kênh riêng của nhóm.

| Đầu vào | Điều kiện đạt trước test |
| --- | --- |
| Admin test | Đăng nhập được UserService, có role Admin và được phép sửa Settings |
| Driver A, Driver B | Hai Account khác nhau, đăng nhập được bằng hai profile trình duyệt riêng |
| JWT trust | UserService dùng khóa ký ổn định, Assistant có public key tương ứng, issuer và audience khớp |
| PFX + keyring | PFX RSA có private key và password, thư mục keyring ghi được. Bộ đang dùng với DB cũ được giữ cùng nhau |
| Knowledge | `smartpark-knowledge.md` bản triển khai đã duyệt cùng `publication.json`, hash/version khớp, thời hạn còn hiệu lực |
| Key provider | Gemini/OpenAI test key của từng provider cần nghiệm thu, có quyền dùng model và quota |
| Owner fixture tùy chọn | XE-A/B, LỊCH-A/B, BÃI-TEST có ID và dữ liệu đối chiếu từ service sở hữu |

Không ghi token, key, mật khẩu, connection string hoặc dữ liệu cá nhân thật vào phiếu test/HAR. Dùng chức năng che Authorization và request chứa key khi chia sẻ bằng chứng.

### 2.2. Máy mới dùng Docker

Chạy lệnh ở thư mục gốc repo. Nhận đúng nhánh/commit được giao, rồi tạo `.env` từ mẫu khi file chưa tồn tại.

```powershell
if (!(Test-Path .env)) { Copy-Item .env.example .env }
```

Điền các trường sau trong `.env`. Runner Docker chấp nhận đường dẫn tuyệt đối hoặc tương đối so với thư mục chứa `.env`. Xem [công thức tạo khóa dev và hướng dẫn chi tiết](../../LOCAL-SETUP.md#ai-chatbot-bằng-docker).

| Trường | Giá trị cần điền |
| --- | --- |
| `SMARTPARK_ASSISTANT_DOCKER_ENABLED` | `true` để setup và runner `All` chạy Assistant |
| `SMARTPARK_JWT_PRIVATE_KEY_PEM_BASE64` | Khóa riêng của UserService local theo mục 5.5 trong LOCAL-SETUP. Giữ khóa đang dùng nếu đã có |
| `Assistant__Jwt__PublicKeyFile` | PEM công khai cùng cặp khóa JWT đó |
| `Assistant__Jwt__Issuer` | Mặc định của UserService Docker là `SmartParking.UserService`, đối chiếu cấu hình thực |
| `Assistant__Jwt__Audience` | Mặc định tương ứng là `SmartParking`, đối chiếu cấu hình thực |
| `Assistant__Management__KeyDirectory` | Thư mục riêng lưu keyring bền vững |
| `Assistant__Management__KeyEncryptionCertificatePath` | Đường dẫn PFX RSA bảo vệ keyring |
| `Assistant__Management__KeyEncryptionCertificatePassword` | Password PFX local |
| `Assistant__Guest__KnowledgeDirectory` | Thư mục riêng chứa Markdown và manifest đã duyệt |
| `Assistant__Guest__AllowedOrigins__0` | `http://localhost:5173` nếu dùng FE Docker mặc định |
| `Assistant__Guest__ReleaseEnabled`, `Assistant__Chat__ReleaseEnabled` | `true` cho hai luồng cần nghiệm thu |
| `SMARTPARK_ASSISTANT_DOCKER_PORT` | `5076` mặc định hoặc port local được giao |

Các password DB/Admin còn trống theo mẫu được setup sinh. Lấy thông tin đăng nhập từ `.env` local sau setup, không gửi nội dung file vào báo cáo. Không tạo lại khóa JWT trên máy đang có bộ khóa của nhóm. Keyring DPAPI Windows cần quy trình chuyển đổi trước khi dùng Linux container.

```powershell
.\setup.ps1 -SkipSonarQube
.\scripts\run-app-service.ps1 -Service All
```

Kỳ vọng setup dựng DB nền, cài các migration Assistant trên phần chatbot trống, build và chạy Assistant. Luồng app stack tiếp tục dựng User/Facility/Operation/Payment/FE. Frontend mở tại `http://localhost:5173`, Assistant tại `http://localhost:5076`. Port có thể đổi theo cấu hình của đợt test.

Runner Docker tự đặt `VITE_ASSISTANT_API_BASE_URL` cho container frontend theo cổng Assistant healthy trong project `smartpark-app`. Khi dựng riêng bằng `-Service Assistant`, frontend Docker đang chạy được cập nhật tự động. Frontend chạy bằng host vẫn đọc `FE/.env`.

### 2.3. Máy đã có DB hoặc đang chạy Assistant bằng host

DB local đã chạy, lần đầu thêm Assistant Docker dùng lệnh sau sau khi đã chuẩn bị đầu vào.

```powershell
.\scripts\setup-assistant-docker.ps1
```

Nếu đã có image và chỉ đổi cấu hình, dùng `-NoBuild`. Với DB có lịch sử/credential, người vận hành xác nhận đúng PFX và keyring gốc rồi đặt `SMARTPARK_ASSISTANT_DOCKER_USE_EXISTING_KEYRING=true`. Script dừng ở schema dở dang hoặc dữ liệu legacy cần chuyển đổi. Giữ lỗi này để developer xử lý, bảo toàn dữ liệu test.

Môi trường host đã được dựng sẵn dùng URL được giao, thường `https://localhost:5075`. Runner host là `scripts/run-schema-service.ps1 -Service Assistant`, cần IoT connection, public JWT trust và các path tương ứng. Chỉ người phụ trách môi trường chạy một topology Assistant cho đợt test. Owner reads cần hướng dẫn riêng ở mục 6.

### 2.4. Kiểm sẵn sàng và kích hoạt

1. Mở `<ASSISTANT_URL>/health/live`. Docker mới kỳ vọng HTTP 204. Đây là kiểm tiến trình sống.
2. Mở `<ASSISTANT_URL>/api/assistant/status`. Trước khi bật công tắc chung, `data.guestFaq=false` là trạng thái có thể gặp.
3. Đăng nhập Admin → Settings → AI Chatbot. Chọn provider → Add configuration → nhập Configuration name, ví dụ `QA Gemini 01`, và key test → Load models → chọn model → Save disabled configuration.
4. Chạy Test connection. Sau khi kết quả passed, bật cấu hình, bật provider và bật công tắc chatbot. Với bài test một provider, chỉ để cấu hình đó đủ điều kiện hoạt động.
5. Kiểm status lại có `data.enabled=true`, `data.guestFaq=true`, rồi gửi FAQ. Sau test, khôi phục công tắc/thứ tự ban đầu theo biên bản.

Test connection và FAQ được nghiệm thu riêng. Trong Chrome DevTools → Network, phản hồi `POST /api/assistant/guest/messages` có `data.mode=ai` khi model trả lời. `mode=knowledge` là nhánh đọc tài liệu dự phòng. Guest UI hiện không có vùng mở citation, nên kiểm `data.sources` trong Network. Driver UI có phần mở nguồn bên dưới tin trả lời.

## 3. Dữ liệu test và cách ghi kết quả

| Ký hiệu | Dữ liệu dùng xuyên suốt |
| --- | --- |
| ADMIN | Admin test được phép đổi Settings |
| A, B | Driver A, B khác Account, mỗi người dùng một profile trình duyệt |
| KEY-G, KEY-O | Key test riêng của Gemini/OpenAI, ghi nhãn và model, không chép key vào báo cáo |
| CONV-A, REQ-A | UUID hội thoại và `clientMessageId` do A tạo. Ghi từ Network |
| XE-A/B | Xe thuộc từng Account trong UserService, dùng khi bật đọc xe |
| LỊCH-A/B | Lịch đặt thuộc từng Account trong Operations, có UUID và khoảng giờ để đối chiếu |
| BÃI-TEST | Bãi đã publish, có tọa độ, loại xe và dữ liệu do owner xác nhận |

Dùng PASS, FAIL, BLOCKED và NOT RUN. BLOCKED phải ghi điều kiện thiếu và người cần cung cấp. Không có key/provider/owner fixture thì đánh BLOCKED cho ca tương ứng. Khi request hoàn tất quá nhanh để ngắt/hủy, ghi NOT RUN và dùng harness cách ly để tái hiện.

```text
Test ID       TC-DRV-02
Bản test      <branch, commit, corpus version>
Môi trường    <Docker hoặc host, browser, thời gian>
Tiền điều kiện đã kiểm    <ghi rõ>
Kết quả       PASS / FAIL / BLOCKED / NOT RUN
Thực tế       <HTTP status, code/outcome, dữ liệu đã che>
Bằng chứng    <ảnh, video hoặc traceId>
Lỗi liên quan <ticket nếu có>
Người test / người xử lý <QA / owner>
```

P0 là chặn bàn giao phạm vi cốt lõi nếu FAIL. P1 cần chạy trước khi đóng nhóm tính năng. P2 là ca vận hành hoặc UX bổ sung. Mỗi dòng bảng là một test case, thực hiện đủ bước theo thứ tự trong ô thao tác.

## 4. Chạy smoke trước

Chạy TC-SET-01, TC-ADM-01, TC-ADM-03, TC-FAQ-01, TC-FAQ-02, TC-DRV-01 và TC-DRV-02. Smoke thành công thì chuyển sang các ca còn lại. Ghi riêng PASS của provider thật và PASS của knowledge fallback.

Giữ nhịp tối đa tám Guest messages mỗi đợt rồi nghỉ ít nhất 61 giây trước đợt tiếp, tối đa hai request cùng lúc. Guest giới hạn theo socket IP nên profile ẩn danh khác vẫn có thể dùng chung hạn mức. Driver cũng có 10 messages/phút/Account, chia đợt tương tự. Tách TC-API-06 kiểm 429 khỏi các ca khác và đợi hạn mức hồi trước tiếp tục.

### 4.1. Setup và cấu hình

| ID / mức | Tiền điều kiện | Thao tác | Kết quả mong đợi |
| --- | --- | --- | --- |
| TC-SET-01 / P0 | Máy mới, đầu vào mục 2 đã chuẩn bị | Chạy hai lệnh mục 2.2, mở FE, kiểm liveness/status, đăng nhập Admin | Assistant healthy, liveness 204, Admin đọc Settings. Status chuyển guestFaq=true sau kích hoạt |
| TC-SET-02 / P1 | Đã có lịch sử và một cấu hình test, bộ PFX/keyring gốc | Ghi ID CONV-A và thông tin cấu hình. Chọn dùng lại keyring theo mục 2.3, chạy setup Assistant `-NoBuild`, mở lại | Settings và lịch sử còn nguyên. Không tạo lại dữ liệu đã có |
| TC-SET-03 / P1 | File env fixture riêng thiếu issuer | Chạy `setup.ps1 -AssistantCheck -AssistantEnvPath <file-fixture>` | Báo MISSING issuer, readiness UNVERIFIED, không ghi cấu hình hoặc secret |
| TC-SET-04 / P1 | DB và file env test cách ly | Dùng sai DB password trong bản env fixture, chạy runner Assistant | Lỗi xác thực, dừng trước migration. Developer kiểm bảng/dữ liệu không bị thay |
| TC-SET-05 / P1 | DB Assistant đã có credential/history | Bỏ opt-in dùng lại keyring, chạy runner. Sau đó cung cấp bộ gốc và opt-in đúng | Lần đầu dừng yêu cầu kiểm keyring. Lần sau dùng được DB khi bộ khóa tương thích |
| TC-SET-06 / P2 | FE/.env có URL khác | Chạy setup Assistant | URL cũ được giữ và có cảnh báo. QA chọn đúng URL rồi khởi động lại FE |

TC-SET-04/05 chỉ chạy trên DB test cách ly. Người vận hành phải có quyền quản lý tài nguyên được dùng trong ca.

### 4.2. Admin Settings

Cần ADMIN và key test của provider đang kiểm. Trước ca thay đổi, ghi nhãn, model, thứ tự và trạng thái để khôi phục. Test connection giới hạn ba lần/phút cho mỗi Admin và cấu hình, nên chờ qua cửa sổ hạn mức khi thử lặp. Giữ cùng Test request ID nếu đang kiểm tra lại kết quả của một lần chạy.

| ID / mức | Thao tác | Kết quả mong đợi |
| --- | --- | --- |
| TC-ADM-01 / P0 | Admin mở Settings. Driver gọi `GET /api/assistant/admin/settings` bằng chính phiên Driver | Admin 200, Driver 403. Người chưa đăng nhập gọi route này nhận 401 |
| TC-ADM-02 / P1 | Add configuration cho mỗi provider có key, Load models, chọn model và Save disabled configuration, reload | Giữ provider/nhãn/model, cấu hình mới disabled. Key không được trả plaintext trong Settings |
| TC-ADM-03 / P0 | Với từng cấu hình hợp lệ bấm Test connection. Sau đó gửi một FAQ bằng Guest | Test connection passed và FAQ có nguồn đúng với `data.mode=ai` thì provider generation PASS. Nếu `knowledge`, ghi fallback riêng và FAIL/BLOCKED cho provider theo nguyên nhân thực tế |
| TC-ADM-04 / P1 | Edit đổi nhãn/model hợp lệ, bật/tắt, thêm cấu hình thứ hai và đổi thứ tự, reload | Giá trị và thứ tự giữ đúng, các cấu hình khác không bị đổi key |
| TC-ADM-05 / P1 | Hai tab Admin tải cùng revision. Tab 1 lưu nhãn, tab 2 lưu từ dữ liệu cũ | Tab 2 báo conflict/yêu cầu tải lại, không ghi đè bản mới |
| TC-ADM-06 / P1 | Replace key trên cấu hình test, reload, Test connection với key mới | Key được thay, cấu hình giữ đúng provider. Không thấy plaintext key cũ/mới |
| TC-ADM-07 / P1 | Revoke cấu hình test, xác nhận, reload | Cấu hình revoked và không còn được chọn cho lần gọi mới |
| TC-ADM-08 / P1 | Dùng `gemini-3-does-not-exist` qua Enter model ID của Gemini rồi lưu | Backend từ chối model không có trong catalogue, bản cấu hình trước giữ nguyên |
| TC-ADM-09 / P1 | Dùng key sai ở cấu hình riêng và Test connection | Lỗi an toàn hoặc lưu bị từ chối. Không hiện passed, không lộ raw upstream body/key |
| TC-ADM-10 / P0 | Khi Guest/Driver đang hoạt động, tắt công tắc chung. Reload trang Guest, thử gửi Driver, mở lịch sử | Guest launcher ẩn sau refresh trạng thái, Driver composer bị khóa. History/tra kết quả cũ vẫn dùng được khi phiên hợp lệ |
| TC-ADM-11 / P2 | Provider chính giả lập lỗi 429/503, có dự phòng được cấu hình trong môi trường kiểm soát | Theo failover/cooldown có giới hạn, kết quả có nguồn. Không nhận biết provider từ lời trả cho khách. Đối chiếu bằng test/telemetry của developer |

### 4.3. FAQ Guest

Cần release Guest, công tắc chung và publication hợp lệ. FAQ-01/02 dùng một provider thật đủ điều kiện. Các ca về nguồn đối chiếu bản Markdown được duyệt của đợt test.

| ID / mức | Nhập gì và thao tác | Kết quả mong đợi |
| --- | --- | --- |
| TC-FAQ-01 / P0 | Cửa sổ ẩn danh hỏi `SmartPark là gì?`, kiểm Network | Trả thông tin có trong mục smartpark-overview. `sources` đúng articleId/version. Không lộ provider/model trong nội dung UI |
| TC-FAQ-02 / P0 | Hỏi `Which vehicle types does SmartPark cover?` | Nếu `mode=ai`, trả tiếng Anh đúng nguồn về ô tô/xe máy. Fallback phải ghi rõ tài liệu gốc tiếng Việt. Không tự khẳng định hỗ trợ xe đạp |
| TC-FAQ-03 / P1 | Lần lượt hỏi `SmartPark có loại xe nào?`, `SmartPark ho tro loai xe nao?`, `vehcile types` | Chọn vehicle-guide, giữ ngôn ngữ phù hợp. Kiểm source qua Network |
| TC-FAQ-04 / P1 | Hỏi `Gửi xe muộn thì sao?`, rồi `đỗ xe quá giờ`, rồi `đến bãi trễ` | Câu đầu hỏi rõ đến trễ hay đỗ quá giờ. Quá giờ dẫn payment-guide, đến trễ hướng kiểm điều khoản/trạng thái. Không tự bịa số phút hay mức phạt |
| TC-FAQ-05 / P1 | Khi công tắc chung còn bật, tắt mọi cấu hình model. Hỏi `Cách đặt chỗ?`, rồi khôi phục | `mode=knowledge`, có nguồn và nhãn phù hợp, vẫn là hướng dẫn công khai |
| TC-FAQ-06 / P0 | Hỏi `Lịch đặt của tôi`, rồi `Xe của tôi` khi chưa đăng nhập | Handoff đăng nhập, không có thẻ dữ liệu riêng của Account |
| TC-FAQ-07 / P1 | Hỏi `Bãi nào còn chỗ lúc 14h?`, `Giá hiện tại bao nhiêu?` | Không xác nhận chỗ trống/giá giao dịch từ FAQ |
| TC-FAQ-08 / P1 | Hỏi `SmartPark có dự báo thời tiết không?`, `vehicle tyres`, `lost wicket` | Hỏi lại/báo thiếu nguồn, không chọn nguồn xe/vé sai để trả câu ngoài phạm vi |
| TC-FAQ-09 / P1 | Gửi `How to pay? token=TEST_ONLY` bằng dữ liệu giả lập | Xử lý an toàn, không lặp bí mật trong câu trả lời. Egress kiểm bằng unit/adapter test ở mục 7 |
| TC-FAQ-10 / P1 | Người vận hành sửa một byte hoặc rút manifest trên corpus fixture riêng, reload status và gửi FAQ | FAQ không sẵn sàng, không phục vụ bản bị đổi. Khôi phục bộ file hợp lệ sau ca |
| TC-FAQ-11 / P1 | Admin/Owner đăng nhập, hỏi FAQ công khai rồi `Lịch đặt của tôi` | FAQ hoạt động. Câu riêng tư hướng dùng Driver, không đọc lịch của tài khoản khác |

### 4.4. Driver chat, history và recovery

Cần A/B, release Driver, corpus và Settings sẵn sàng. Dùng câu FAQ `SmartPark là gì?` để các ca cốt lõi chạy được khi owner reads tắt.

| ID / mức | Thao tác | Kết quả mong đợi |
| --- | --- | --- |
| TC-DRV-01 / P0 | Dùng tài khoản A chưa có chat. Mở Trợ lý · Lịch sử, gửi FAQ đầu tiên, đóng/mở và reload | Lượt gửi đầu tiên tạo chat. Một câu người dùng và một câu trả lời, đúng thứ tự, nguồn Driver mở được |
| TC-DRV-02 / P0 | Ghi CONV-A. B mở danh sách và gọi GET history/requests của CONV-A bằng JWT của B | B không thấy CONV-A. API cho resource của A trả 404 |
| TC-DRV-03 / P1 | Guest hỏi lịch đặt, Sign in bằng A, chọn Đưa vào ô soạn, quan sát Network trước Gửi | Chỉ đưa câu vào ô soạn, không tự POST message. Bấm Gửi mới tạo yêu cầu |
| TC-DRV-04 / P1 | A gửi `What is SmartPark?` rồi `tell me more` trong cùng hội thoại | Tiếp tục FAQ theo nguồn công khai đã chọn trong context hợp lệ. Không tự đọc dữ liệu tài khoản |
| TC-DRV-05 / P0 | Slow network, gửi FAQ, Offline khi request còn pending. Online lại và chọn Kiểm tra kết quả | Tra đúng clientMessageId. Nếu cho gửi lại, cùng ID và payload. Không tạo câu hỏi trùng |
| TC-DRV-06 / P1 | Với request đủ lâu, chọn Hủy yêu cầu, sau đó tra lại cùng ID | Terminal đầu tiên giữ nguyên. Nếu cancel thắng thì cancelled, kết quả về muộn không chèn thêm. Completed trước cancel được giữ |
| TC-DRV-07 / P1 | Hai tab A mở cùng đoạn chat có đủ tin để phân trang. Tab 1 gửi câu và chờ hoàn tất. Tab 2 chọn Tin nhắn cũ hơn rồi gửi câu | Tab 2 vẫn gửi revision của trang mới nhất đã hiển thị và nhận `REVISION_CONFLICT`. UI tải lại trang mới nhất, giữ câu vừa nhập để người dùng gửi lại và không tạo tin nhắn trùng |
| TC-DRV-08 / P0 | A đang tải history chậm, logout rồi login B, chờ response cũ về | UI của B không nhận tin/thẻ hay trạng thái request của A |
| TC-DRV-09 / P1 | Ghi CONV-A/REQ-A, restart Assistant test cùng DB/keyring, mở lại hội thoại | History hoàn tất còn nguyên. Tra request cũ không kích hoạt generation mới |
| TC-DRV-10 / P1 | Dùng harness crash hoặc developer tạo provider giả lập chờ, kill tiến trình sau admission và restart | Sau deadline, worker ghi REQUEST_INTERRUPTED, cùng ID tra lại được, không redispatch. Xem mục 7 |
| TC-DRV-11 / P2 | Dùng một đoạn chat có trên 50 tin, chọn Tin nhắn cũ hơn đến hết lịch sử | Tin nhắn cũ được ghép đúng thứ tự, không lặp ID và không làm mất vị trí đang đọc |
| TC-DRV-12 / P1 | Reset đoạn chat, mở lại lịch sử và tra ID cũ. Dùng harness PostgreSQL cách ly để kiểm tra purge trước và sau mốc 7 ngày | Chat cũ ẩn ngay, history và replay trả 404. Purge giữ chat trước hạn và giữ chat có hold; chat đến hạn không có hold được xóa |
| TC-DRV-13 / P1 | Driver gửi ba câu muộn/quá giờ như TC-FAQ-04 | Cùng quyết định nguồn và hướng dẫn với Guest ở ba intent tương ứng |

## 5. API và ranh giới chức năng

Các ca API dùng phiên hợp lệ từ chính UserService test. Trong Network ghi route, payload và response. Developer hỗ trợ resend bằng API client nếu UI chặn nhập trước khi gửi.

| ID / mức | Tiền điều kiện và thao tác | Kết quả mong đợi |
| --- | --- | --- |
| TC-API-01 / P0 | JWT thiếu/hết hạn gọi `/api/assistant/conversations`; Admin gọi `/api/assistant/capabilities` | JWT thiếu/hết hạn 401. Admin có JWT hợp lệ nhưng không phải Driver nhận 403 ở capabilities |
| TC-API-02 / P1 | Gửi message trống, trên 4000 ký tự hoặc clientMessageId rỗng qua API client | 400 validation, không lưu request mới. UI có thể chặn sớm trước API |
| TC-API-03 / P1 | Gửi body sai JSON hoặc sai content type | 400 cho JSON lỗi, 415 cho content type. Có envelope lỗi và traceId, không stack/SQL |
| TC-API-04 / P0 | Replay một POST message với cùng ID và payload gốc. Sau đó đổi message nhưng giữ ID | Replay cùng payload dùng ledger cũ, không thêm message. Đổi payload nhận 409 IDEMPOTENCY_CONFLICT |
| TC-API-05 / P1 | Thêm accountId/userId của B vào body tạo conversation hoặc message dưới token A | Trường lạ không đổi owner. B vẫn không đọc được conversation của A |
| TC-API-06 / P2 | Trong môi trường fixture, gửi trên 10 Guest messages/phút từ cùng socket IP bằng câu ngoài phạm vi | 429 có Retry-After khi vượt hạn mức. Chạy tách khỏi các ca FAQ khác để tránh nhiễu |
| TC-BND-01 / P0 | Driver nói `Đặt cho tôi chỗ ô tô từ 14h đến 16h ngày mai` | Bản hiện tại không mở form tự điền và không tạo booking/payment. Capabilities prepareReservation=false. Không tuyên bố đã đặt thành công |

Với TC-API-04, giữ nguyên `expectedRevision` của POST gốc khi replay. Khi muốn gửi một câu mới, dùng ID mới và revision vừa đọc. Mã requestError trong ledger có thể nằm trong HTTP 200 của kết quả đã lưu, nên kiểm cả `state` và `requestError`, không chỉ HTTP status.

## 6. Test tích hợp owner sau khi môi trường sẵn sàng

Docker Assistant mặc định chỉ provision phạm vi Settings/FAQ/history. Overlay hiện không chuyển các cờ owner reads vào container. Muốn chạy nhóm này, IoT/DevOps phải cấu hình triển khai bổ sung hoặc dùng host runner đã cấu hình và có các owner HTTPS thực.

Kiểm `GET /api/assistant/capabilities` bằng Driver trước mỗi nhóm. `getMyVehicles`, `readMyReservations`, `searchParking` là cờ cấu hình/readiness của Assistant, vẫn cần xác nhận owner có thể trả dữ liệu. Ghi BLOCKED nếu thiếu fixture/trust/source.

| ID / mức | Điều kiện riêng | Thao tác | Kết quả mong đợi |
| --- | --- | --- | --- |
| TC-OWN-01 / P1 | User read bật, HTTPS và XE-A/B tồn tại | A hỏi `Xe của tôi`, B hỏi cùng câu | Mỗi Account chỉ thấy xe được UserService cho phép |
| TC-OWN-02 / P1 | Operations read bật và có LỊCH-A/B | A hỏi `Lịch đặt của tôi`, bấm Xem chi tiết mới nhất | Dữ liệu khớp owner, không có LỊCH-B |
| TC-OWN-03 / P1 | LỊCH-A giao ngày mai, B không có lịch ngày mai | Hỏi `Ngày mai tôi có lịch đặt không?` bằng A và B | A có lịch giao khoảng ngày Việt Nam, B nhận rỗng rõ ràng |
| TC-OWN-04 / P0 | UUID lịch A, token B | B gửi `chi tiết lịch đặt <UUID lịch A>` | Không có thẻ/chi tiết của A, backend owner từ chối resource ngoài quyền |
| TC-OWN-05 / P1 | Search bật, HTTPS, age 1–3600 giây và skew 0–60 giây, bãi test đã publish | Gửi `Tìm bãi ô tô gần 10.77,106.70 radius=5`, thay tọa độ theo fixture | Kết quả khớp catalogue. Dữ liệu giá/chỗ trống thiếu hiển thị chưa xác định, không suy từ FAQ |
| TC-OWN-06 / P1 | Như OWN-05 | Gửi `Tìm bãi ô tô`, rồi tìm tại vùng fixture chắc chắn không có bãi | Thiếu gốc tìm phải hỏi lại. Vùng rỗng chỉ trả empty nếu owner xác nhận không có kết quả |
| TC-OWN-07 / P1 | Owner read có trước, vận hành tạo lỗi ở owner test | Gửi lại câu tra cứu | Lỗi nguồn khác empty. Không thay bằng bãi/xe/lịch giả lập |

Thiếu nguồn positive availability hiện là hạn chế đã biết. Owner phải cung cấp dữ kiện nhất quán trước khi nghiệm thu “còn chỗ trong toàn bộ khung giờ”.

## 7. Bộ kiểm tra tự động và bằng chứng đã có

### Nghiệm thu backend trên PostgreSQL cách ly

Các harness dùng dữ liệu và tài nguyên test riêng. Lệnh Docker dùng một lần cần quyền quản lý Docker local. Chạy restore trước. PostgreSQL harness dùng image local `postgres:16-alpine` với `--pull never`, nên chạy `docker pull postgres:16-alpine` một lần nếu setup chưa tải image. Docker setup harness có thể build image Assistant local và kéo base image. Không dùng harness để kiểm DB shared bằng cách thay chuỗi kết nối.

```powershell
dotnet restore SmartParking.slnx
dotnet build SmartParking.slnx -c Release --no-restore --nologo -m:1
dotnet test tests/Services/SmartParking.IoTService.Tests/SmartParking.IoTService.Tests.csproj -c Release --no-build --nologo
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-assistant-postgres.ps1
pwsh -NoProfile -File scripts/test-assistant-docker-setup.ps1
```

| Bộ test | Kiểm gì | Kết quả đã ghi nhận 08/11/2026 |
| --- | --- | --- |
| IoT suite mặc định | HTTP contract, auth, quản trị, retrieval, failover, history/recovery | 473 pass, 6 skip. Năm ca PG opt-in và một worker con chạy trong harness riêng |
| PostgreSQL harness | Trùng ID, revision conflict, rotate/revoke, keyring, cancel/finish, crash, retention, migration replay, backup/restore DB fixture | 5/5 pass và đối chiếu dữ liệu sau restore đạt |
| Docker setup harness | Image/container thật, PG16 mới, sai password, keyring opt-in, Admin JWT/Driver403, FAQ và lịch sử sau rerun | PASS với runner Windows PowerShell 5.1, JWT/corpus do fixture tạo, FAQ dùng knowledge |
| Setup fixtures | Setup cũ, thứ tự preflight mới, env preservation, checker, migration plan, runner/listeners | PASS trong các lượt ghi tại nhật ký |
| FAQ corpus | 21 câu Việt/Anh có lỗi gõ, đồng nghĩa, câu ngoài nguồn/private/live | Tất cả expectation đạt trên corpus fixture. Chất lượng model thật vẫn cần QA |
| Full solution | Build/test các service | Build Release đạt. Lượt full suite 962 pass, 4 fail, 70 skip trước khi bổ sung hai ca PG/worker. Bốn fail là baseline fixture operational-layout Facility |

Đây là bằng chứng của các lượt trước được lưu trong [nhật ký backend](../../ai/tasks/active/chatbot-development-progress.md). Chỉnh tài liệu lần này không chạy lại runtime suite. Tester chạy trên commit bàn giao và ghi kết quả riêng.

Chạy thêm khi thay UI hoặc setup tương ứng.

```powershell
node --test tests/frontend/assistant-admin.test.cjs tests/frontend/assistant-api.test.cjs tests/frontend/assistant-demo.test.cjs tests/frontend/floating-chatbot.test.cjs
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-setup.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-assistant-check.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-assistant-runner.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-owner-listeners.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-assistant-migration-plan.ps1
```

### Baseline S09 · fixture offline

Giữ anchor này để tra bằng chứng trước. TestServer Windows/.NET 10, store in-memory, corpus/provider/owner theo fixture. Mỗi luồng có 80 mẫu, warm-up 20, concurrency tối đa 1 hoặc 2. p95 đã ghi lần baseline đầu cho FAQ lần lượt 0,661/0,742 ms, Driver gửi tin 1,145/1,320 ms và lookup 0,900/1,034 ms. Số này chỉ dùng so fixture, không đại diện latency mạng/provider/DB thật.

Trong phép so 80 cặp đọc corpus local, parse mỗi lần có p50/p95 0,975/1,683 ms, tái sử dụng parse có 0,431/0,960 ms. Mỗi nhánh vẫn đọc byte, SHA-256 và manifest. Histogram stage gồm faq_retrieval, chat_readiness, chat_admission, chat_finish, provider_failover. Các bước có lồng nhau, không cộng tổng. Tải đại diện/SLO cần leader chốt.

## 8. Phân loại lỗi và xử lý môi trường

| Hiện tượng | Kiểm tiếp | Người xử lý |
| --- | --- | --- |
| Setup dừng trước Docker | Path tuyệt đối, PEM public, PFX RSA, password, corpus hash và context Docker local | Người dựng môi trường |
| DB password sai | Dùng đúng password đã init volume, giữ dữ liệu, không tự reset | DevOps/DB owner |
| Container healthy nhưng FAQ ẩn | Global toggle, guest release, publication approved/hash/version/effective dates | Admin + IoT |
| 401 ở Admin/Driver | Token hết hạn, issuer/audience, User dùng khóa ổn định và Assistant dùng đúng public key | User + IoT |
| 403 ở Driver khi vào Admin | Đúng kỳ vọng. Nếu Admin cũng bị 403, đối chiếu role trong phiên User | User + QA |
| Test connection passed nhưng FAQ lỗi | Payload generation/citation, eligibility, timeout, quota và corpus | IoT + provider Admin |
| FAQ trả knowledge | Hết candidate/lỗi recoverable/fallback, chưa đủ bằng chứng provider generation pass | IoT + Admin |
| Unknown outcome sau mất mạng | Tra cùng ID, giữ payload replay gốc | QA + IoT |
| Owner đọc lỗi | HTTPS/trust, token, schema/data fixture và cờ read, kiểm API owner trước | Owner service + IoT |
| History mất sau restart | Đúng IoT DB/Account và keyring/certificate gốc, kiểm ledger | IoT + DevOps |

Lỗi rò dữ liệu Account, trả credential hoặc tạo đơn ngoài xác nhận là lỗi chặn P0. Khi gặp, dừng nhóm test liên quan và gửi bằng chứng đã che cho leader.

## 9. Điều kiện chốt đợt test

Đóng nhóm cốt lõi khi các ca P0/P1 áp dụng đều PASS, provider đã nghiệm thu riêng, không còn lỗi rò dữ liệu/mất lịch sử và có xác nhận người test. Ghi rõ BLOCKED cho owner reads chưa mở. Các ca vận hành chưa tái hiện cần NOT RUN và chỉ ra harness hoặc môi trường cần bổ sung.

### Chốt bàn giao trên môi trường test của nhóm

Người QA ghi biên bản smoke Settings/FAQ/Driver trên UserService và provider test thật. Leader chốt chấp nhận kết quả theo phạm vi. Toàn bộ S01–S10 và luồng đặt chỗ chỉ đóng khi các điều kiện ở [bản tổng quan leader](../03-design/ai/system-overview-and-review.md) đạt. Trước khi gửi bản test, developer đóng gói source, test, script và tài liệu cùng một commit/nhánh review, rà secret và kiểm candidate đó.
