# Chatbot integration scaffold

`src/Services/IoTService/SmartParking.IoTService.Application/Assistant/ChatbotIntegration.cs` contains the original disabled scaffold. No real model provider, credentials or business data are bundled.

Adapters belong to Infrastructure and must call the owning backend API with current authorization. The initial allowlist contains FAQ and parking search only; writes hand off to the normal user-confirmed workflow. A model intent never grants a permission or determines pricing/payment/capacity. Provider configuration is deployment-specific. This task delivers the integration structure, not a functioning production chatbot.

## Demo API — hướng dẫn chạy và phạm vi

Được người dùng duyệt ngày 03/11/2026: làm host test/demo độc lập, dùng interface và dữ liệu mẫu; chưa làm thêm phần lấy tài khoản hiện tại hoặc doanh thu. Tiến độ/test/review nằm trong [session hiện có](../../../ai/tasks/archive/chatbot-implementation-history.md#nhat-ky-1).

Host có ba chế độ cấu hình: `mock` dùng câu mẫu để kiểm tra transport, `provider` dùng một cấu hình Gemini/OpenAI, và `managed` dùng Settings cùng chuỗi provider/key dự phòng. Phản hồi được ghi nhãn riêng `mock`, `provider` hoặc `knowledge` (hướng dẫn gốc khi AI gián đoạn). Đã có một lượt FAQ dùng provider thật thành công; failover được kiểm thử bằng lỗi giả lập, không coi đó là nghiệm thu SLA hoặc mọi key thật. Không có đăng nhập giả, dữ liệu nghiệp vụ trực tiếp hoặc lịch sử chat ở server. JWT Admin chỉ mở quản trị khi bật managed; FAQ local vẫn anonymous, không mở controller hội thoại production.

### Chạy từ thư mục gốc repo

```powershell
dotnet run --project src/Services/IoTService/SmartParking.IoTService.API --no-launch-profile -- --environment Demo --Assistant:Demo:Enabled true --urls http://127.0.0.1:5095 --Assistant:Demo:AllowedOrigins:0 http://127.0.0.1:5173
```

Port và origin ở lệnh trên chỉ là ví dụ, thay theo môi trường frontend. Không cần JWT key, database, Docker hay provider credential. Chạy ở terminal riêng và dừng bằng Ctrl+C.

Host yêu cầu **cả môi trường `Demo` và `Assistant:Demo:Enabled=true`**. Thiếu một trong hai thì không khởi động. Chạy mặc định/Production không tự chuyển sang mock. Giới hạn này dành cho executable demo; không phải quyết định về hành vi lỗi cấu hình JWT của host production sau này.

ASP.NET nhận các giá trị trực tiếp từ tham số lệnh trên hoặc biến môi trường của tiến trình. `.env` ở root **không tự được đọc** bởi lệnh này; `setup.ps1` và Compose app gốc không nạp cấu hình demo; Docker dùng overlay tùy chọn ở mục bên dưới. Template `.env.example` chỉ ghi tên biến để tham khảo. Nếu đổi biến môi trường, cần khởi động lại tiến trình demo. Dùng command line như trên là đủ, không cần sửa `.env` cá nhân hay cấu hình các service khác.

### Kiểm tra phản hồi

```powershell
Invoke-WebRequest http://127.0.0.1:5095/health/live
Invoke-RestMethod http://127.0.0.1:5095/health/ready
Invoke-RestMethod http://127.0.0.1:5095/api/demo/assistant/capabilities
```

| Đường dẫn | Kết quả |
| --- | --- |
| `/health/live` | 204, không body; tiến trình đang phản hồi |
| `/health/ready` | 200, `data.isDemo=true`, `readyFor=demo-api-only`, `assistantEnabled=false`; chỉ xác nhận API demo chạy, không phải chatbot thật sẵn sàng |
| `/api/demo/assistant/capabilities` | 200, envelope có `success/data/traceId`; `data.isDemo=true`, thông báo dữ liệu demo và `data.capabilities` chứa toàn bộ chức năng tắt |
| `/api/demo/assistant/capabilities?role=owner` | 400 `VALIDATION_FAILED`; demo không nhận tham số query hoặc quyền do client chọn |
| POST `/api/demo/assistant/messages` | Nhận JSON `{ "message": "Tôi muốn đặt chỗ" }`, trả `data.isDemo`, `data.notice`, `data.reply`, `data.mode`, `data.sources`, `data.provider/model`; mock hoặc FAQ provider theo cấu hình |
| POST `/api/demo/assistant/capabilities` | 405 `METHOD_NOT_ALLOWED`, giữ header Allow |
| `/api/assistant/*` | 404; các API thật không được expose bởi host demo |

Mọi phản hồi có `X-Assistant-Mode: demo`, `Cache-Control: no-store` và correlation do server sinh. Demo không yêu cầu đăng nhập vì chỉ có dữ liệu mẫu công khai; không dùng kết quả này để chứng minh quyền Driver/Owner. Không gửi token thật vào host demo.

### Frontend kết nối như thế nào?

Mở terminal PowerShell thứ hai từ root repo và chạy:

```powershell
cd FE
$env:VITE_ASSISTANT_DEMO_ENABLED="true"
$env:VITE_ASSISTANT_DEMO_API_BASE_URL="http://127.0.0.1:5095"
npm run dev -- --host 127.0.0.1 --port 5173 --strictPort
```

Mở **http://127.0.0.1:5173/demo/assistant**. Hai terminal phải tiếp tục chạy; lệnh `dotnet test` chỉ chạy test rồi kết thúc, không mở chatbot để sử dụng. Dừng hai tiến trình bằng Ctrl+C khi xong. Các biến ở trên chỉ áp dụng cho terminal hiện tại, không sửa `.env` cá nhân. Nếu dùng `FE/.env`, thêm biến từ template rồi restart Vite; setup không tự bổ sung biến vào file `.env` đã tồn tại.

Trang chỉ bật khi Vite ở development và cờ demo true. Production build loại bỏ nhánh demo. Đường dẫn này nạp riêng màn hình demo, không nạp AppProvider/luồng đăng nhập. Giao diện gọi POST demo trực tiếp, không gửi cookie/token và không tự retry hoặc trả lời giả khi mất kết nối.

**Ô chat nổi trên dashboard:** cùng điều kiện development và `VITE_ASSISTANT_DEMO_ENABLED=true`, widget gọi cùng `sendDemoMessage` và URL ở trên. Chế độ managed dùng provider/model/key đã lưu trong Settings; không nhập key ở frontend. Docker overlay hiện tại đã cấp các biến này và bind mount mã FE, nên thay đổi widget được Vite cập nhật, không cần rebuild backend. Widget hiển thị chế độ AI/mô phỏng, thông báo giới hạn và nguồn tham khảo; lỗi có nút Try again, không tự gửi lại hoặc thay bằng câu trả lời mock. New, đổi vai trò và unmount hủy yêu cầu cũ, ngăn kết quả muộn quay lại. Khi chưa bật preview hoặc trong production, widget báo chưa khả dụng và không gọi endpoint demo. Đây là tích hợp FAQ local, chưa mở API demo anonymous cho production.

Khi lỗi AI hoặc nhận hướng dẫn knowledge dự phòng, khách Driver có nút **Find parking & book directly** để mở luồng tìm bãi/đặt chỗ sẵn có mà không cần thêm lượt gọi model. Nếu đang điền BookingFlow, nút đổi thành **Continue booking** và chỉ đóng chat, giữ nguyên form cùng dữ liệu đã nhập. Nút không tạo đơn, giữ slot, chọn xe/ngày giờ tự động hoặc thay thế bước xác nhận. Các role khác không có điều hướng đặt chỗ Driver. Lời nhắc lỗi công khai không yêu cầu khách cấu hình key/provider trong Settings; mã tra cứu vẫn dùng để hỗ trợ. Luồng đặt chỗ này độc lập với cơ chế dự phòng managed ở dưới.

Thử hỏi về tìm bãi, đặt chỗ hoặc thanh toán. Phản hồi ghi rõ chế độ; provider hiển thị nguồn được backend xác minh, mock ghi mô phỏng. Câu hỏi doanh thu không có số liệu. “Hội thoại mới” hủy yêu cầu đang chờ và xóa nội dung trong tab; tải lại trang cũng mất nội dung. Không có memory hội thoại phía server: mỗi câu hỏi được xử lý riêng, backend chưa hiểu câu hỏi tiếp nối theo lịch sử.

Lỗi kết nối có nút “Thử gửi lại”, chỉ gửi khi người dùng bấm; timeout frontend 30 giây. Giới hạn tin nhắn 1–4000 UTF-16 code units sau trim (emoji có thể tính 2), body JSON tối đa 32 KiB cho demo. Không nhập thông tin cá nhân. Test pending/reset/lỗi thực tế nằm trong session.

Đặt `Assistant:Demo:AllowedOrigins:0` đúng origin frontend (scheme + host + port, không path hoặc dấu `/` cuối). Có thể thêm chỉ mục `:1` cho origin thứ hai. Mặc định không cấp quyền cross-origin; không wildcard, không cookie credentials. CORS không phải cơ chế xác thực; chỉ chạy demo trên máy/môi trường thử nghiệm riêng, ví dụ bind loopback như lệnh trên.

### Phần nào đang dùng mock?

| Interface / thành phần | Implementation demo | Chuyển sang thật sau này |
| --- | --- | --- |
| `IDemoAssistantStatusProvider` | `DemoAssistantStatusProvider` trả trạng thái cố định, toàn bộ tính năng false | Không dùng fixture làm readiness thật. Production phải lấy trạng thái từ các phụ thuộc thực tế theo contract đã duyệt |
| `IDemoReplyGenerator` | `DemoReplyGenerator` dùng câu trả lời mẫu cho tìm bãi, đặt chỗ, thanh toán và fallback; doanh thu không trả số liệu | Tích hợp model/corpus được duyệt ở bước sau, không coi fixture là FAQ chính thức |
| HTTP + MediatR + formatter lỗi | Code thật chạy trong host demo | Tái sử dụng khi xây composition root production |
| JWT và actor | Không đăng ký trong demo; test RSA thật được giữ riêng | Nối cấu hình tin cậy và actor đã xác minh vào host production; không thêm mock fallback |

Interface có tên Demo cố ý để không nhầm dữ liệu mẫu với trạng thái thật. Bản demo không tạo mock doanh thu hoặc role; hội thoại chỉ là danh sách tin nhắn trong bộ nhớ React, không được lưu lâu dài. Việc chuyển sang thật cần cấu hình và kiểm thử tích hợp, không chỉ tắt nhãn demo.

### Chạy bằng Docker của nhóm (tùy chọn)

Chỉ thêm `compose.assistant-demo.yml` sau file app gốc, cùng project `smartpark-app`. Không sửa setup, Dockerfile, `.env` hoặc Compose chung. Đây là lệnh riêng cho demo đã được duyệt; runner chung vẫn dùng như cũ cho các service khác.

Điều kiện: nhóm đã setup Docker app, root `.env` đáp ứng các biến bắt buộc của Compose gốc, network `smartpark-schema_default` và image frontend hiện có. Chạy từ root repo; dừng bản demo chạy trực tiếp nếu đang chiếm cổng 5095. Dùng đúng `localhost:5173` để khớp CORS.

```powershell
# Kiểm tra cấu hình, không in secret.
docker compose -p smartpark-app --env-file .env -f compose.app.yml -f compose.assistant-demo.yml config --quiet
# Chỉ build image demo, không rebuild service của nhóm.
docker compose -p smartpark-app --env-file .env -f compose.app.yml -f compose.assistant-demo.yml build assistant-demo
# Chỉ thêm demo và cập nhật frontend; không khởi động lại backend khác.
docker compose -p smartpark-app --env-file .env -f compose.app.yml -f compose.assistant-demo.yml up -d --no-deps --no-build assistant-demo frontend
docker compose -p smartpark-app --env-file .env -f compose.app.yml -f compose.assistant-demo.yml ps assistant-demo frontend
Invoke-RestMethod http://localhost:5095/health/ready
```

Nếu `config --quiet` báo thiếu biến của app gốc (ví dụ `SMARTPARK_RABBITMQ_PASSWORD`), dừng tại đó và đối chiếu cấu hình setup của nhóm; không điền mật khẩu giả vào `.env` hoặc đổi mật khẩu database để chạy demo.

Mở **http://localhost:5173/demo/assistant**. Frontend được recreate để nhận cấu hình demo và URL quản trị nên sẽ gián đoạn ngắn trên máy bạn; các backend/database không đổi. Không dùng project name khác hoặc chạy overlay một mình. Không chạy runner frontend/All trong lúc demo vì runner chỉ biết cấu hình gốc và có thể tắt cờ demo.

Demo bind mount knowledge chỉ đọc và dành volume `assistant-management-keys` cho khóa mã hóa của chế độ quản trị. Không tự thêm database hoặc network riêng; container dùng bridge có sẵn và chỉ publish cổng trên loopback. Image riêng `smartpark/assistant-demo:local` tái sử dụng Dockerfile backend chung. Ba biến frontend chỉ có hiệu lực khi ghép overlay. Không đặt chúng trong `FE/.env` nếu muốn việc tắt demo dưới đây khôi phục hoàn toàn trạng thái ban đầu. Giữ volume khóa khi dọn container để tránh mất khả năng đọc key đã mã hóa.

**Tắt và dọn demo** (chạy trước khi bỏ file overlay):

```powershell
# Chỉ dừng/xóa container demo, không xóa volume hoặc container khác.
docker compose -p smartpark-app --env-file .env -f compose.app.yml -f compose.assistant-demo.yml rm --stop --force assistant-demo
# Tạo lại riêng frontend theo cấu hình ban đầu, bỏ biến do overlay thêm.
docker compose -p smartpark-app --env-file .env -f compose.app.yml up -d --no-deps --no-build frontend
# Tùy chọn khi không dùng demo nữa: xóa đúng image demo, không force/prune.
docker image rm smartpark/assistant-demo:local
```

Không dùng `down`, `down -v`, `--remove-orphans` hoặc `prune` để dọn demo trong project dùng chung. Build cache có thể được Docker giữ lại như các lần build bình thường; không tự dọn cache chung. Nếu chỉ tạm ngừng demo, giữ image để lần sau bật nhanh. Muốn gỡ cấu hình khỏi repo về sau thì bỏ file overlay và mục hướng dẫn này; không có migration hay dữ liệu demo cần xử lý.

### Bật FAQ dùng knowledge và provider thật

Knowledge hiện chỉ có **một file tiếng Việt**: [smartpark-knowledge.md](p1/knowledge/smartpark-knowledge.md), gồm 10 mục được gộp từ bản Việt trước đó. Không duy trì bản chính sách tiếng Anh riêng. Mỗi mục giữ mã nguồn ổn định; backend tách mục rồi tìm bằng từ khóa/cụm từ Việt–Anh, chọn tối đa 3 mục liên quan. AI dùng nguồn tiếng Việt và diễn đạt theo ngôn ngữ câu hỏi, mặc định tiếng Việt khi chưa rõ. Không có bước gọi API dịch riêng. Tiêu đề/trích đoạn nguồn vẫn là nguyên văn tiếng Việt để đối chiếu. Chưa dùng embedding/vector database và không bảo đảm nhận ra mọi cách diễn đạt; không có lịch sử server.

Chỉ phần `### Nội dung` được gửi đến model. Từ khóa, hướng dẫn sửa file, metadata và `### Đối chiếu — không gửi đến AI` không được gửi. Giữ nguyên căn cứ biên tập và các điều kiện/con số chính sách khi gộp. Corpus vẫn ở trạng thái draft, không phải publication production. Model trả ID nguồn; backend chỉ chấp nhận ID đã truy xuất và tạo title/version/excerpt từ phần nội dung thật. Việc kiểm tra ID không tự chứng minh bản dịch hoặc mọi câu model nói đều đúng; cần nghiệm thu model thật riêng.

Câu ngoài phạm vi FAQ nhận lời nhắc thân thiện về các chủ đề SmartPark hỗ trợ và sources rỗng. Từ đệm như “bạn”, “mình”, “đang”, “quá” không tự tạo căn cứ chọn tài liệu. Nếu vẫn có nguồn được chọn nhưng model trả JSON hợp lệ với `sourceIds: []` (không có nguồn hỗ trợ câu hỏi), backend bỏ hẳn nội dung reply của model và trả cùng lời nhắc giới hạn cố định. Đây không phải câu trả lời nghiệp vụ không cần nguồn hoặc mock fallback. Thiếu field sourceIds, sai kiểu, ID lạ/trùng hay JSON không hợp lệ vẫn báo lỗi kỹ thuật. Không mở trò chuyện/tư vấn ngoài SmartPark. Prompt yêu cầu plain text, không Markdown emphasis/code fence để phù hợp bộ hiển thị hiện tại.

**Khi sửa chính sách:** tìm đúng mục trong file, sửa nội dung tiếng Việt, tăng `version` ở đầu file rồi lưu. Không đổi mã trong dấu `[ ]`. Chỉ sửa dòng `Từ khóa:` khi cần hỗ trợ chủ đề/cách hỏi mới; từ khóa ngăn bằng dấu chấm phẩy và không lặp lại chính sách. Các đề mục con bên trong nội dung dùng cấp 4 (`####`) trở xuống. Backend đọc lại file cho mỗi câu hỏi mới, kể cả khi giữ cùng generator, nên không cần restart vì chỉnh nội dung. Lượt đã nhận dùng snapshot trước khi sửa; file thiếu/sai định dạng báo lỗi an toàn, không dùng cache chính sách cũ. Giới hạn: file UTF-8 tối đa 256 KiB, 1–100 mục, mỗi nội dung tối đa 16000 ký tự.

**Chuyển từ bộ đọc cũ:** cần build và recreate riêng `assistant-demo` **một lần** bằng cùng cấu hình JWT/DB/certificate/provider hiện tại (lệnh Docker phía trên). Image cũ không đọc được định dạng gộp; chỉ tải lại frontend không cập nhật backend. Mount thư mục knowledge trong overlay không đổi. Sau khi bộ đọc mới đã chạy, các lần sửa nội dung tiếp theo chỉ cần lưu file rồi hỏi lại, không rebuild. Không reset database hoặc cấp lại API key.

**Docker:** trong PowerShell chạy Compose, đặt cấu hình không phải secret:

```powershell
$env:ASSISTANT_DEMO_MODE='provider'
$env:ASSISTANT_DEMO_PROVIDER='gemini'
$env:ASSISTANT_DEMO_MODEL='gemini-3.8-flash'
# Nhập key bằng prompt ẩn, không đưa key vào câu lệnh/history/chat.
$demoKey = Read-Host 'API key của provider đã chọn' -AsSecureString
$env:ASSISTANT_DEMO_API_KEY = [System.Net.NetworkCredential]::new('', $demoKey).Password
```

Sau đó chạy lệnh build/up của mục Docker phía trên. Key chỉ truyền vào container backend, không phải VITE variable. Có thể lưu các biến trong root `.env` riêng của máy theo chính sách nhóm; file example đã có tên biến, setup không tự cấp key provider. Không in `docker compose config` đầy đủ hoặc `docker inspect` vì có thể lộ key; dùng `config --quiet`.

Với chế độ `provider` trực tiếp, để dùng OpenAI thay Gemini, đặt `ASSISTANT_DEMO_PROVIDER='openai'`, `ASSISTANT_DEMO_MODEL='gpt-5.4-mini-2026-03-17'` và nhập key OpenAI. Chế độ này vẫn chỉ dùng một cấu hình, không tự failover. Đổi provider/model/key thì recreate riêng `assistant-demo` bằng cùng Compose overlay. Muốn quản lý nhiều key/provider dự phòng, dùng `managed` và Settings như mục dưới.

**Chạy trực tiếp bằng dotnet:** đặt các biến `Assistant__Demo__Mode=provider`, `Assistant__Demo__Provider`, `Assistant__Demo__Model`, `Assistant__Demo__ApiKey` trong terminal backend; dùng prompt ẩn tương tự. Đặt `Assistant__Demo__KnowledgeDirectory` thành đường dẫn tuyệt đối đến `docs/03-design/ai/p1/knowledge`, rồi dùng lệnh dotnet ở đầu trang. Root `.env` không tự nạp vào dotnet.

Khi dùng xong, dừng/xóa container demo theo mục cleanup và xóa biến key khỏi terminal:

```powershell
Remove-Item Env:ASSISTANT_DEMO_API_KEY -ErrorAction SilentlyContinue
Remove-Item Env:Assistant__Demo__ApiKey -ErrorAction SilentlyContinue
Remove-Variable demoKey -ErrorAction SilentlyContinue
```

Provider chỉ nhận câu hỏi hiện tại và nội dung được truy xuất, không token đăng nhập hay lịch sử. Không nhập dữ liệu cá nhân/secret trong demo. Bộ lọc nhận diện một số mẫu credential trước khi gửi, không phải bảo đảm loại được mọi thông tin nhạy cảm. Key backend không nằm trong prompt. OpenAI request đặt `store=false`; đây không phải cam kết zero retention của nhà cung cấp.

Giới hạn thử nghiệm: tối đa 10 POST/phút trên cả tiến trình, 2 lượt gọi provider đồng thời, không xếp hàng, deadline backend 20 giây, output tối đa 2048 token và body provider 128 KiB. Chế độ `provider` trực tiếp giữ một lượt gọi; `managed` có chuỗi dự phòng tối đa3cấu hình khác nhau, không thử lặp cùng key trong một câu hỏi. Vì vậy giới hạn10tin nhắn/phút không đồng nghĩa chỉ10lượt tính phí/phút. Timeout/ngắt kết nối cũng không bảo đảm provider chưa xử lý/tính phí. Các giới hạn này không thay ngân sách tiền ở tài khoản provider; không mở demo anonymous ra Internet.

Khi không có dự phòng hợp lệ, lỗi generation vẫn dùng HTTP503 nhưng phân loại rõ bằng code: thiếu cấu hình `ASSISTANT_NOT_CONFIGURED`, không đọc được knowledge `RETRIEVAL_UNAVAILABLE`, hết deadline tổng `MODEL_TIMEOUT`, JSON/citation/body đầu ra không dùng được `INVALID_MODEL_OUTPUT`, upstream/mạng/busy `MODEL_UNAVAILABLE`. Managed có thể phục hồi các lỗi kết nối/provider đủ điều kiện bằng chuỗi dưới đây; không che lỗi knowledge/config/citation bằng câu trả lời giả. Thông báo backend chỉ dùng câu tĩnh an toàn; không trả body provider, key hoặc exception thô. Frontend không xóa phiên đăng nhập hoặc tự resubmit. Vượt limiter local trả429 với lời nhắc chờ. Provider trả401/403 không trở thành401 phiên ứng dụng. Không tìm được nguồn thì thông báo giới hạn, sources rỗng và không gọi model. `health/ready` chỉ xác nhận demo API, không probe provider/key/quota.

Log backend ghi loại lỗi đã giới hạn, mã HTTP provider nếu có và thông tin provider/model; lỗi cuối có trace ID trong envelope. Managed ghi từng candidate thất bại ngay cả khi trả knowledge200. Không ghi key, câu hỏi, body upstream hoặc exception thô. `ProviderRejected; ProviderStatus: 503` cho biết upstream tạm không phục vụ; timeout chưa đủ để kết luận key sai. Phân biệt với `ASSISTANT_MAINTENANCE` và `RATE_LIMITED`. Kiểm tra theo [hướng dẫn lỗi Gemini](https://ai.google.dev/gemini-api/docs/generate-content/api-errors); frontend không gửi lại liên tục.

Request đã được rà lại ngày04/11: JSON dùng làm văn bản prompt giữ nguyên ký tự tiếng Việt sau khi giải mã envelope HTTP, thay vì để model đọc hàng loạt literal Unicode escape. Bộ mã hóa vẫn escape ký tự JSON/HTML nhạy cảm; response frontend vẫn hiển thị plain text. Bỏ `candidateCount` thừa và để mặc định một kết quả, giữ `responseFormat.text`/schema và mức thinking hiện tại. Căn cứ [hướng dẫn Gemini](https://aistudio.google.com/docs/troubleshooting) và [GenerateContent](https://aistudio.google.com/api-reference/generate-content). Đây là sửa tương thích/chất lượng request; không chứng minh hoặc bảo đảm chữa được503 do năng lực phục vụ của Google. Không thêm retry/failover tự động hoặc đổi model/key/ưu tiên.

Model IDs đã đối chiếu tài liệu chính thức ngày 03/11/2026; quyền tài khoản và chất lượng trả lời live chưa được kiểm chứng. Luồng này không thay JWT production, storage hoặc quyền nghiệp vụ.

### Cài đặt Chatbot trong tài khoản Admin — bổ sung 03/11/2026

Trong Settings của tài khoản Admin đã có tab **AI Chatbot**. Tab dùng access token của phiên đăng nhập để đọc/lưu qua `/api/assistant/admin`; không dùng mock hoặc localStorage để lưu key. Các tab Account, Interface và Security giữ luồng hiện tại.

Có thể bật/tắt chatbot, nhập thông báo bảo trì một dòng, chỉnh model/thứ tự ưu tiên Gemini và OpenAI, thêm/thay key, bật/tắt hoặc thu hồi key. Key mới mặc định tắt; bật key và provider trước khi bật chatbot. Key đã lưu không được trả lại trình duyệt.

**Dự phòng managed — người dùng xác nhận ngày04/11:**

1. Provider có số ưu tiên nhỏ hơn được xét trước; bên trong provider, key đang bật/chưa thu hồi được xếp theo ưu tiên rồi ID. Tối đa3cấu hình được gọi mỗi câu hỏi, mỗi cấu hình tối đa một lần. Backend không tự bật key/provider đang tắt và không đổi model đã lưu.
2. Lỗi401/403/429 từ provider làm key đó nghỉ60giây và cho phép thử key tiếp theo. Lỗi500/502/503/504,408, mạng/timeout làm provider+model nghỉ60giây, bỏ qua các key khác cùng model để thử provider còn lại. Nhiều key cùng project có thể chung quota; thêm key không tự tăng năng lực provider.
3. Mỗi attempt tối đa7giây; chuỗi AI tối đa18giây; toàn lượt tối đa20giây, chừa thời gian kiểm tra cấu hình và đọc tài liệu. Các lượt gọi ngoài không có bảo đảm chỉ tính phí một lần khi timeout/kết quả chưa rõ. Admission busy chuyển thẳng sang tài liệu, không thử lặp.
4. Kiểm tra enabled/revision trước mỗi key dự phòng và trước tài liệu; có thay đổi/revoke/rotation thì dừng thay vì gửi key theo snapshot cũ. Lượt đã gửi có thể hoàn tất. Cooldown chỉ ở bộ nhớ instance, reset khi lưu Settings/revision mới hoặc restart; không cache chính sách knowledge.
5. Hết lựa chọn AI hợp lệ thì trả `mode=knowledge`, provider/model rỗng, nhãn **Hướng dẫn từ tài liệu**, nguồn mở sẵn với nguyên mục tiếng Việt, không cắt mất điều kiện cuối. Câu tiếng Anh được báo rõ đang đọc tài liệu Việt gốc; không giả vờ đã dịch bằng AI. Không tìm được mục thì chỉ nhắc phạm vi FAQ. Lỗi corpus/citation/output/config, mất ciphertext/storage và maintenance vẫn được báo an toàn, không trở thành mock hoặc hướng dẫn nghiệp vụ bịa.

**Điều kiện chạy:** phần này cần backend ở chế độ `managed`, PostgreSQL riêng của IoT và JWT Admin thật. Chế độ `mock`/`provider` trước đó vẫn chạy độc lập nhưng không mở API quản trị. Tab xuất hiện không có nghĩa database hoặc provider đã được cấu hình xong.

1. Người phụ trách DB review rồi áp dụng `scripts/database/migrations/20261103_iot_assistant_management.sql` vào đúng database/schema IoT, bằng tài khoản có quyền trên các bảng Assistant. Migration chỉ thêm ba bảng cấu hình, credential mã hóa và biên nhận thay đổi; không đụng bảng User/Facility/Operation. Code không tự chạy migration. Connection phải có cùng search path với nơi áp dụng SQL.
2. Cấu hình JWT issuer, audience và public PEM khớp UserService đang cấp token. Backend kiểm tra chữ ký và role `admin`; không chấp nhận tự chọn role từ UI. Theo phương án B đã chọn, quyền trong token còn hiệu lực đến hết hạn tối đa 15 phút. Chống xóa Admin cuối không làm token bị thu hồi ngay.
3. Chuẩn bị kho Data Protection bền vững. Chạy dotnet trên Windows dùng thư mục tuyệt đối và DPAPI của tài khoản chạy app. Docker/Linux cần PFX có private key, password và quyền đọc phù hợp. Overlay dành volume `assistant-management-keys`, backend đọc certificate tại `/app/management-keys/admin.pfx`; người vận hành provision certificate vào đó trước khi bật chế độ managed. Không commit PFX/password. Sao lưu DB cùng key ring/certificate theo quy trình nhóm; không xóa volume khi dọn demo. Mất khóa bảo vệ thì ciphertext trong DB không giải mã lại được.
4. Với Docker overlay, đặt `ASSISTANT_DEMO_MODE=managed`, `ASSISTANT_MANAGEMENT_ENABLED=true`, `ASSISTANT_MANAGEMENT_CONNECTION`, `ASSISTANT_JWT_ISSUER`, `ASSISTANT_JWT_AUDIENCE`, `ASSISTANT_JWT_PUBLIC_KEY_PEM`, `ASSISTANT_MANAGEMENT_CERT_PASSWORD`. Nhập secret qua prompt ẩn hoặc cấu hình local được nhóm quản lý. Database qua cổng host phải dùng host mà container truy cập được, ví dụ `host.docker.internal` trên Docker Desktop; không dùng `localhost` bên trong container để chỉ DB của host. Không sửa mạng/cấu hình các service khác.
5. Chạy trực tiếp dotnet thì dùng tên cấu hình `Assistant__Demo__Mode=managed`, `Assistant__Management__Enabled=true`, `ConnectionStrings__IoT`, `Assistant__Jwt__Issuer`, `Assistant__Jwt__Audience`, `Assistant__Jwt__PublicKeyPem`, `Assistant__Management__KeyDirectory`; giữ opt-in Demo, AllowedOrigins và KnowledgeDirectory như hướng dẫn trên. Docker map các biến này qua overlay. Frontend cần `VITE_ASSISTANT_API_BASE_URL=http://localhost:5095` và khởi động lại Vite sau khi đổi env; overlay đã có giá trị này.
6. Đăng nhập Admin, mở Settings → AI Chatbot, thêm key, bật key/provider rồi lưu bật chatbot. Thử tại `/demo/assistant` hoặc ô chat nổi trên dashboard khi bật preview local, sau đó tắt chatbot và kiểm tra thông báo bảo trì. Cấu hình được đọc lại cho mỗi tin nhắn mới; lượt đã được nhận xử lý có thể hoàn tất sau khi tắt/thu hồi.

**Phiên bản UserService khi dùng quản trị:** image phải khớp contract JWT của checkout hiện tại, gồm `sub`, `account_id`, `jti`, `role`, issuer/audience và thời hạn. Đúng cặp khóa RSA chưa đủ nếu image cũ cấp token thiếu claim. `up --no-build` hoặc `docker start` chỉ dùng image đã có. **DB healthy chỉ xác nhận PostgreSQL chạy, không xác nhận schema tương thích.** Trước khi cập nhật UserService, dùng verifier chỉ đọc bên dưới; nếu có lỗi như thiếu `accounts.role_code`, dừng để chuẩn bị migration đã review hoặc reset riêng UserDB khi được xác nhận mất dữ liệu. Không chạy lại base fresh-install trên DB đang có bảng và không dùng `setup.ps1 -RecreateDb` để sửa riêng UserDB vì lệnh đó reset cả sáu DB.

```powershell
Get-Content -Raw -Encoding UTF8 scripts/database/test/verify-user-access-schema.sql | docker compose --env-file .env -f compose.schema-integration.yml exec -T user-db psql -U smartpark_user -d smartpark_user -v ON_ERROR_STOP=1
```

Khi schema đã tương thích, trong terminal đã nạp cùng cấu hình JWT với Assistant, build lại UserService, bảo đảm DB User đang healthy rồi cập nhật riêng container đó:

```powershell
docker compose -p smartpark-app --env-file .env -f compose.app.yml -f compose.assistant-demo.yml build user-service
docker compose --env-file .env -f compose.schema-integration.yml up -d --wait user-db
docker compose -p smartpark-app --env-file .env -f compose.app.yml -f compose.assistant-demo.yml up -d --no-deps --no-build user-service
```

Sau đó đăng xuất/đăng nhập để lấy token mới; refresh token phát hành bởi image cũ cũng có thể không còn tương thích. Không reset DB để xử lý lỗi khác phiên bản. Nếu Assistant trả 401, tab quản trị hiển thị lỗi và giữ phiên ứng dụng; UserService vẫn là nơi quyết định phiên hết hiệu lực qua refresh hoặc API của chính nó.

Thiếu cấu hình bắt buộc khi bật quản trị làm host demo không khởi động; DB không sẵn sàng trả lỗi 503 an toàn. Đây là hành vi của chế độ quản trị local opt-in, không xác nhận quyết định vận hành production còn mở. `health/ready` vẫn chỉ xác nhận demo API, không probe DB/provider. Lỗi 409 hoặc kết quả ghi chưa rõ cần tải lại cấu hình trước khi sửa tiếp; không tự gửi lại với mã thao tác mới.

Đã có kiểm thử tự động cho API/quyền/validation và giao diện transport. **Chưa kiểm chứng** migration trên PostgreSQL thật, hai phiên ghi đồng thời, khôi phục key sau restart, trình duyệt với backend managed thật hoặc gọi provider bằng key thật. Không coi hướng dẫn này là xác nhận hệ thống đã được provision.

### Kiểm thử

```powershell
dotnet test tests/Services/SmartParking.IoTService.Tests/SmartParking.IoTService.Tests.csproj --nologo
```

Test dùng TestServer trong tiến trình, không gọi DB/UserService/provider. Bao gồm chặn sai môi trường, opt-in, health, dữ liệu mẫu, CORS, API thật không bị mở, lỗi HTTP và bộ kiểm tra JWT đã có. Kết quả từng lần chạy và review chỉ cập nhật trong session.
