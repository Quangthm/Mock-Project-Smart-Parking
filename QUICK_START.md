# Quick start — SmartParking

Chạy PowerShell tại root của checkout vừa clone; không cần đường dẫn máy cá nhân.

```powershell
git branch --show-current
.\setup.ps1
```

Cần Windows PowerShell 5.1+, .NET SDK 10, Docker Desktop và Docker Compose hỗ trợ `up --wait`. Node.js/npm cần cho frontend. `setup.ps1` được lưu trong Git; không cần xin script từ kênh nội bộ.

Mặc định `Local`: dùng lại credential trong `.env`/user-secrets; chỉ sinh khi thiếu. Ba API dùng cùng service key trên máy này. Để các dev dùng cùng credential, lấy **file credential riêng do leader cung cấp**, rồi chạy:

```powershell
.\setup.ps1 -CredentialMode Team -TeamEnvPath '<file-env-riêng-của-team>'
```

File Team phải có `SMARTPARK_DEV_PASSWORD`, `SMARTPARK_SERVICE_KEY` (ít nhất 32 ký tự), `SMARTPARK_ADMIN_PASSWORD` (8–15 ký tự, đủ hoa/thường/số/ký tự đặc biệt). Có thể kèm `SONAR_DB_PASSWORD`, `SMARTPARK_OWNER_PASSWORD`, `SMARTPARK_OPERATOR_PASSWORD`, `VITE_MAPTILER_API_KEY` và `SMARTPARK_SERVICE_USER_URL`, `SMARTPARK_SERVICE_PARKING_URL`, `SMARTPARK_SERVICE_RESERVATION_URL`. File Team không thay thế cả `.env`: port, SMTP và cấu hình riêng trên máy vẫn giữ nguyên. Không commit file Team hoặc secret thật; lưu ngoài repo.

Setup dừng nếu credential Team khác cấu hình local đã có (`Team/local conflict`). Ngược lại, `[5/7]` luôn đồng bộ user-secrets theo `.env` (ghi đè giá trị lệch, xóa khóa optional không còn trong `.env`) — muốn giữ user-secrets cũ thì dùng `-SkipUserSecrets` + runner, không chạy trực tiếp từ Visual Studio:

```powershell
.\setup.ps1 -SkipUserSecrets
.\scripts\run-schema-service.ps1 -Service User
# Hai terminal khác: -Service Parking và -Service Reservation
```

Nếu DB volume cũ đã có password khác, cần khôi phục password gốc; đổi `.env` không đổi password trong PostgreSQL. Admin/owner/operator đã seed cũng không được đổi password chỉ bằng setup. UserService seed ba tài khoản khi service khởi động lần đầu: admin (`admin@smartpark.local`), owner (`owner@smartpark.local`) và operator (`operator@smartpark.local`); password tương ứng trong `.env` (`SMARTPARK_ADMIN_PASSWORD`, `SMARTPARK_OWNER_PASSWORD`, `SMARTPARK_OPERATOR_PASSWORD`). Tài khoản driver demo (`driver@gmail.com / Password@123`) được DataSeeder tạo trong code.

Sau khi UserService đã chạy lần đầu, seed site demo để test owner/operator mà không qua đăng ký/approval:

```powershell
.\scripts\seed-demo-site.ps1
```

Script tạo `DEMO-HQ` (1 floor, 1 zone, 6 slot CAR + 2 slot MOTORCYCLE), gán operator vào site, cấp 5 quyền vận hành và cấu hình capacity. Chạy lại an toàn; script không in password. Nếu báo thiếu owner/operator, chạy `.\scripts\run-schema-service.ps1 -Service User` một lần rồi seed lại.

SonarQube tự khởi động cùng `.\setup.ps1` (bỏ qua bằng `-SkipSonarQube`; `-SkipDatabase` không khởi động container). `SONAR_DB_PASSWORD` được sinh tự động khi thiếu và giữ nguyên giá trị đã cấu hình; UI mặc định http://localhost:9000. Compose Sonar riêng giữ dữ liệu trong named volumes; không tự tiếp quản container Sonar cũ. Cấu hình dựa trên [ví dụ chính thức SonarSource](https://github.com/SonarSource/docker-sonarqube/blob/master/example-compose-files/sq-with-postgres/docker-compose.yml). Pin `SONAR_IMAGE` theo version team duyệt nếu cần môi trường cố định. Lần đầu đăng nhập admin/admin và đổi password trên UI.

AI camera là tùy chọn riêng: `.\setup.ps1 -WithAiCamera` kiểm tra 7 model file trong `src/Services/IoTService/python-ai/models` và network `smartpark-schema_default` trước khi gọi Docker cho AI, rồi dựng 2 container từ `compose.ai.yml` (project `smartpark-ai`): `ai-rest` là REST API tại http://localhost:5001, `ai-live` là luồng nhận diện trực tiếp tại http://localhost:5002. Không thêm flag thì không build/chạy gì thuộc AI. Flag chỉ hợp lệ khi chạy full setup: cần schema DBs đang chạy (không dùng chung `-SkipDatabase`) và `SMARTPARK_DEV_PASSWORD` đã cấu hình trong `.env`. Lần build đầu mất ~5–10 phút (PyTorch CPU wheels + PaddleOCR warm-up) và tốn RAM; các lần sau dùng cache.

Setup chỉ build khi cần: image `smartpark-ai:local` chưa có → build + `up -d --wait`. Image đã có và container đã tồn tại → bỏ qua build, chỉ `up -d --wait --no-build` (container dùng image cũ được cảnh báo và recreate bằng `--force-recreate --no-build`, không tự rebuild). Image có nhưng chưa có container → in playbook A–E, probe thư viện một lần, rồi hỏi `[S] Start / [R] Rebuild / [C] Clean rebuild / [K] Skip AI / [A] Abort`; `-NonInteractive` mặc định Start và không tự rebuild. Container unhealthy → không tự rebuild, in log và hướng dẫn. `-RebuildAi` (chỉ dùng cùng `-WithAiCamera`) buộc build lại có cache. "Skip" chỉ bỏ build, setup vẫn đảm bảo container chạy; chọn `K` mới bỏ toàn bộ AI.

## Docker app stack (BE + FE)

Sau khi `setup.ps1` đã dựng schema DBs, có thể chạy cả app stack bằng Docker mà không cần IDE. Compose project `smartpark-app` dùng external network `smartpark-schema_default` và DB hostname `user-db`/`facility-db`/`operations-db`:

```powershell
.\scripts\run-app-service.ps1 -Service User       # chỉ User, mở CMD log
.\scripts\run-app-service.ps1 -Service Parking
.\scripts\run-app-service.ps1 -Service Reservation
.\scripts\run-app-service.ps1 -Service Frontend
.\scripts\run-app-service.ps1 -Service All        # cả 4 container
```

Lần đầu build 4 image (`.NET 10` + Node 22) nên lâu; các lần sau dùng cache. Dùng `-NoBuild` để bỏ qua bước build, `-NoLogWindow` khi chạy tự động/CI. Có thể double-click các file `scripts\run-*-service.cmd` hoặc `run-all-apps.cmd`. Health: User `http://localhost:5035/health`, Parking `5045/health`, Reservation `5055/health`, FE `http://localhost:5173`. Dừng stack bằng `.\scripts\stop-app-service.ps1 -Service All` (không xóa volume). Không chạy đồng thời runner host và container vì cùng port 5035/5045/5055.

MapTiler: điền `VITE_MAPTILER_API_KEY` trong root `.env` hoặc file Team; setup chỉ bổ sung nếu key trong `FE/.env` còn trống. Key FE đã có được giữ nguyên. Để chạy frontend:

```powershell
cd FE
npm install
npm run dev
```

Port mặc định: User/Parking/Reservation API 5035/5045/5055; User/Parking/Reservation DB 5441/5442/5443; Payment/Notification/IoT DB 5444/5445/5446; ai-rest/ai-live 5001/5002 (chỉ build + chạy bằng `.\setup.ps1 -WithAiCamera`); FE 5173. DB bind loopback. Giao tiếp API xuyên máy cần URL host truy cập được, listener/firewall phù hợp và cùng service key; setup không tự mở mạng.

### Tóm tắt các flag

| Flag | Ý nghĩa |
| --- | --- |
| `-SkipDatabase` | Bỏ qua bước [4/7]: không init DB, không migrate. Vẫn dựng credential. |
| `-SkipRestore` | Bỏ qua bước [7/7]: không `dotnet restore`. |
| `-SkipUserSecrets` | Bỏ qua bước [5/7]: giữ nguyên user-secrets cũ. |
| `-RecreateDb` | Xóa schema volumes rồi init lại 6 DB (mất dữ liệu dev). |
| `-CredentialMode Team -TeamEnvPath <file>` | Dùng credential từ file team thay vì sinh local. |
| `-SkipSonarQube` | Không khởi động SonarQube (mặc định SonarQube chạy cùng setup). |
| `-NonInteractive` | Bỏ qua prompt `.env` overwrite; tự động Keep nếu `.env` đã tồn tại. |
| `-WithAiCamera` | Kiểm tra model/network và dựng 2 container AI (ai-rest :5001, ai-live :5002). Chỉ build khi image chưa có hoặc khi có `-RebuildAi`. Yêu cầu schema DBs đang chạy. |
| `-RebuildAi` | Đi cùng `-WithAiCamera`: buộc build lại image AI có cache rồi start; không dùng một mình. |

## Progress lines `[n/7]` và cách debug khi setup fail

`setup.ps1` in ra các mốc `[n/7]` trước khi vào từng giai đoạn; giai đoạn DB còn có dòng con `    - ...`. Màu hiển thị: header `[n/7]` = cyan, dòng con `    - ...` = xám nhạt, trạng thái hoàn tất mỗi bước = xanh lá, prompt/warning = vàng. **Dòng progress cuối cùng trên màn hình = bước đang chạy khi lỗi xảy ra** — dùng nó để xác định nhanh nguyên nhân:

| Progress line | Giai đoạn | Lỗi thường gặp → Cách xử lý |
| --- | --- | --- |
| `[1/7]` Checking prerequisites | .NET SDK 10, đọc `.env` | Chưa cài .NET SDK 10 → cài đúng major 10. `.env` đã tồn tại → hiện prompt `[O]verwrite / [K]eep / [A]bort` (Enter = Keep; Overwrite backup ra `.env.bak` rồi sinh lại từ `.env.example`). Prompt bị bỏ qua khi chạy tự động bằng `-NonInteractive`. |
| `[2/7]` Resolving credentials | Sinh/dùng lại password, service key, admin/owner/operator password | `Existing database volumes found` → volume cũ cần lại password gốc trong `.env` (setup không tự xóa data); hoặc chạy `-RecreateDb` nếu chấp nhận mất data dev. `Existing service keys differ` → user-secrets 3 service lệch nhau, sửa tay. |
| `[3/7]` Validating configuration | Check key ≥32 ký tự, policy password, 6 port | `Invalid port` → port var không phải số 1–65535. |
| `[4/7]` Initializing databases | Compose config → (drop volume nếu `-RecreateDb`) → `up --wait` 6 DB → probe auth → migration | Treo lâu ở `Starting database containers` → Docker Desktop chưa chạy / cổng 5441–5446 bị chiếm (`docker compose -f compose.schema-integration.yml ps` + `logs <tên-db>`). `authentication failed` → password trong `.env` khác password đã init volume (chỉ fix bằng `-RecreateDb` hoặc khôi phục password cũ). Lỗi SQL khi init → `docker logs <tên-db-container>`, dòng log chỉ đúng file `.sql` bị lỗi. Dòng con `[n/6]` cho biết DB service nào (User→Iot) đang fail. Lỗi `relation "..." already exists` ở dòng con `[n/6]` → xem Nhật ký lỗi đã biết bên dưới. |
| `[5/7]` Configuring user-secrets | Ghi/đồng bộ toàn bộ connection string, URL, admin/owner/operator credentials vào user-secrets theo `.env`; xóa khóa optional (JWT PEM, SMTP) không còn trong `.env` | `Cannot configure ... user-secrets` → thư mục UserSecrets bị khóa/đầy; chạy lại bằng `-SkipUserSecrets` rồi dùng runner. |
| `[6/7]` Preparing frontend environment | Copy `FE/.env`, MapTiler key, AI endpoints; khởi động SonarQube (bỏ qua với `-SkipSonarQube`); nếu `-WithAiCamera`: kiểm tra 7 model/network, dò image + container AI, chỉ build khi image thiếu hoặc có `-RebuildAi`, rồi `up -d --wait` (dùng `--no-build` khi image/container đã có) | `Configure MapTiler key directly in FE/.env` → key chứa ký tự đặc biệt, sửa tay trong `FE/.env`. `SonarQube startup failed` → volume Sonar cũ lệch `SONAR_DB_PASSWORD`, khôi phục password gốc vào `.env`. `AI model assets are missing` → khôi phục file model đã tracked. `Docker network smartpark-schema_default not found` → chạy setup không `-SkipDatabase` trước. |
| `[7/7]` Restoring dependencies | `dotnet restore` | Restore fail → kiểm tra mạng/proxy NuGet; hoặc bỏ qua với `-SkipRestore`. |

Khi gặp lỗi mới: **note lại dòng `[n/7]` (hoặc `[n/6]` con) + thông báo lỗi** vào mục bên dưới để dev sau tra cứu nhanh.

### Nhật ký lỗi đã biết

*(Ghi theo mẫu: **[n/7]** — triệu chứng — nguyên nhân — cách fix.)*

**[4/7]** — lỗi `ERROR: relation "users" already exists` (hoặc bảng base khác) ngay dòng con `    - [n/6] Probing...` — docker init đã chạy đủ script SQL trên volume mới, nhưng runner applied lại cùng các file base — đã fix: `apply-workflow-migrations.ps1` probe sentinel trước khi áp; schema hiện tại → skip + verify, DB trống → apply, schema cũ → báo chạy `-RecreateDb`.

**[5/7]** (bug 28P01) — lỗi `password authentication failed for user "smartpark_user"` khi chạy BE trực tiếp từ Visual Studio/`dotnet run` (đường chạy qua runner `run-schema-service.ps1` không bị) — user-secrets đã lưu connection string chứa password cũ, lệch với password mà volume PostgreSQL đã init; `DbConnectionStringBuilder` không tokenize được giá trị legacy nên `[2/7]` không đọc lại được password, và `[5/7]` cũ chỉ ghi key thiếu nên không bao giờ sửa giá trị lệch — đã fix: `[5/7]` ghi đè mọi giá trị user-secrets lệch theo `.env` mỗi lần chạy (và xóa khóa optional không còn trong `.env`), `[2/7]` parse password bằng regex fallback khi tokenizer fail, các conflict throw `... differs from .env` ở `[3/7]` bị bỏ.

**[6/7] (ai-rest/ai-live)** — lỗi `Docker network smartpark-schema_default not found` — network này chỉ tồn tại sau khi schema DBs được dựng; chạy setup **không** `-SkipDatabase` trước, rồi chạy lại với `-WithAiCamera`.

**[6/7] (ai-rest/ai-live)** — build lần đầu chậm (~5–10 phút) — bình thường do PyTorch CPU wheels + PaddleOCR warm-up; các lần sau dùng cache và setup bỏ qua build nếu image/container đã có.

**[6/7] (ai-rest/ai-live)** — cảnh báo container dùng image cũ — image `smartpark-ai:local` đã được build lại nhưng container còn trỏ image ID cũ; setup không tự rebuild mà recreate bằng `--force-recreate --no-build`. Chỉ dùng `.\setup.ps1 -WithAiCamera -RebuildAi` khi image hiện tại hỏng/thiếu thư viện.

**[6/7] (ai-rest/ai-live)** — container báo unhealthy — setup không tự rebuild; xem log: `docker compose -p smartpark-ai --env-file .env -f compose.ai.yml logs --tail 200`, rồi chỉ rebuild có xác nhận bằng `-RebuildAi` nếu image là nguyên nhân. Image có nhưng chưa có container: chọn `S` để dùng image sẵn có, `R`/`C` để rebuild có cache/sạch, `K` bỏ AI, `A` dừng setup.

**[6/7] (ai-rest/ai-live)** — kết nối DB trong container dùng biến môi trường rời (`IOT_DATABASE_HOST=iot-db`, port 5432) — không dùng DSN URL vì password có thể chứa ký tự `@`.



Volume cũ (schema cũ hơn baseline hiện tại): chạy `.\setup.ps1 -RecreateDb` để build lại 6 DB theo schema canonical; runner tự phân biệt DB trống / schema hiện tại / schema cũ và chỉ áp script khi cần. Chi tiết ở [LOCAL-SETUP.md](LOCAL-SETUP.md).

Chi tiết về volume, migrations và chuyển từ script cũ: [LOCAL-SETUP.md](LOCAL-SETUP.md). Hướng dẫn sửa script: [UPDATE_SCRIPT_GUIDE.md](UPDATE_SCRIPT_GUIDE.md).
