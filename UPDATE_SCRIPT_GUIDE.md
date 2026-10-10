# UPDATE SCRIPT GUIDE - SmartPark onboarding scripts

Tài liệu này dành cho maintainer sửa/mở rộng bộ script setup. Mục tiêu: giữ
hành vi hiện tại ổn định, idempotent, và không rò rỉ secret.

---

## 1. Bản đồ file

| File | Vai trò |
| --- | --- |
| `setup.ps1` (root) | Script setup gốc duy nhất: 8 nhãn bước `[0/7]`..`[7/7]` (Prerequisites → Env bootstrap → Validate → Docker DB → Migrations → SonarQube → User-secrets → Summary). **Bị gitignore**; phân phối qua kênh nội bộ team. |
| `scripts/load-env.ps1` | Helper parse `.env`: export mọi key vào process environment + trả về hashtable. Được `setup.ps1` (dot-source) và `run-schema-service.ps1` dùng chung. Có trong git. |
| `scripts/run-schema-service.ps1` | Chạy 1 API trong Development; đọc root `.env`, set env vars (`ASPNETCORE_ENVIRONMENT`, `Services__*`, `ConnectionStrings__<Service>`), rồi `dotnet run`. |
| `scripts/apply-workflow-migrations.ps1` | Áp dụng migration workflow cho 1 service (`User`/`Parking`/`Reservation`) lên container PostgreSQL tương ứng; kiểm tra bảng bắt buộc trước khi chạy SQL. Được `setup.ps1` gọi ở bước `[4/7]`. |
| `compose.schema-integration.yml` | Định nghĩa **6** PostgreSQL container (user/parking/reservation/payment/notification/iot); host port đọc từ `.env` (`${SMARTPARK_DB_*_PORT:-default}`). |
| `scripts/setup-local.ps1` | Deprecation shim: in cảnh báo rồi chuyển tiếp `@args` sang root `setup.ps1`. Không xóa để giữ tham chiếu trong docs. |
| `.env.example` (root) | Template cho `.env` (infra/BE). Placeholder `replace-with-...`, không chứa secret. 3 key (`SMARTPARK_SERVICE_KEY`, `SMARTPARK_ADMIN_PASSWORD`, `SONAR_DB_PASSWORD`) được `setup.ps1` điền giá trị dùng chung khi tạo/ghi đè `.env`. |
| `FE/.env.example` | Template cho `FE/.env` (frontend: `VITE_*`). |

Cấu hình tách 2 nơi: **root `.env`** cho infra/BE, **`FE/.env`** riêng cho frontend.

---

## 2. Kiến trúc `setup.ps1`

### State ở script scope

```powershell
$repoRoot      = $PSScriptRoot                 # KHÔNG hardcode path
$envFile       = Join-Path $repoRoot '.env'
$envExampleFile= Join-Path $repoRoot '.env.example'
$loadEnvScript = Join-Path $repoRoot 'scripts\load-env.ps1'
$script:StepResults    = @()                    # lịch sử từng bước cho Summary
$script:CurrentStep    = 0
$script:ComposeStyle   = 'plugin'               # 'plugin' => docker compose; 'legacy' => docker-compose
$script:LastComposeExitCode = ...               # do Invoke-Compose set
$script:SharedEnvDefaults = @{ ... }            # 3 giá trị dùng chung của team; áp khi tạo/ghi đè .env từ template
$script:MapTilerApiKey    = '...'               # key MapTiler dùng chung; ghi vào FE\.env khi bootstrap
```

### Helpers

| Helper | Nhiệm vụ |
| --- | --- |
| `Invoke-SetupStep -Index -Name -Body` | Chạy 1 bước, ghi kết quả, fail-fast. |
| `Complete-Step -Name -Status -Detail -Lines` | In dòng `[n/7] <name> ... OK\|SKIP\|FAIL - detail` + các dòng chi tiết; lưu vào `$script:StepResults`. |
| `Show-StepSummary` | In bảng tổng kết từ `$script:StepResults`. |
| `Invoke-Compose -ComposeArguments` | Gọi `docker compose` (hoặc `docker-compose` nếu `$script:ComposeStyle='legacy'`), pipe ra `Out-Host`, lưu `$script:LastComposeExitCode`. |
| `Invoke-SwallowedCommand -FilePath -Arguments` | Chạy native command và "nuốt" lỗi/stderr, trả về exit code. Dùng cho `docker start`, `docker network create/connect`. |
| `Get-ToolVersion -FilePath -Arguments` | Chạy native command, trả `[pscustomobject]@{ ExitCode; Text }` (gộp stdout+stderr). Dùng cho các bước probe/version và `dotnet user-secrets list`. |
| `Test-Placeholder -Value` | `$true` nếu giá trị bắt đầu bằng `replace-with-`. |
| `Set-EnvKeyValue -Path -Key -Value` | Thay (hoặc thêm) dòng `KEY="value"` trong file env, ghi lại UTF-8 không BOM. Dùng để bake giá trị dùng chung vào `.env`/`FE\.env`. |

### Luồng chạy và mẫu kết quả

Mỗi bước được bọc trong `Invoke-SetupStep`. Body **trả về** một object:

```powershell
return [pscustomobject]@{
    Status = 'OK'        # 'OK' | 'SKIP' | 'FAIL'
    Detail = 'mô tả ngắn'
    Lines  = @('dòng chi tiết 1', 'dòng chi tiết 2')
}
```

- Không trả gì → mặc định `OK`.
- Body `throw` exception → `Invoke-SetupStep` bắt và đổi thành `FAIL` (Detail =
  message).
- Log: `[n/7] <name> ... <STATUS>` (xanh OK, vàng SKIP, đỏ FAIL).
- Fail-fast: `Status = 'FAIL'` → in "Setup stopped at step [n/7]...", in bảng
  tổng kết, `exit 1`.

### Lưu ý PowerShell 5.1 (quan trọng)

Trong Windows PowerShell 5.1, khi `$ErrorActionPreference = 'Stop'` và redirect
stderr của native command (`2>&1`), stderr có thể biến thành terminating error.
Vì vậy `Get-ToolVersion` và `Invoke-SwallowedCommand` **tạm hạ**
`$ErrorActionPreference` xuống `'Continue'` trong `try/finally` rồi khôi phục.
Khi thêm lệnh native mới có redirect stderr, hãy tái sử dụng 2 helper này thay
vì tự viết `2>&1`.

Ngoài ra, khi nhận kết quả từ body, `$result = & $Body` sẽ gom **mọi** output
vào pipeline. Đừng để native command/`Write-Output` "rơi" tự do trong body —
hãy pipe ra `Out-Host`/`Out-Null` để không phá vỡ object trả về và không lộ
secret (ví dụ `dotnet user-secrets set ... | Out-Null`).

---

## 3. Hợp đồng `scripts/load-env.ps1`

```powershell
param([string]$EnvPath)                       # mặc định: <repo>\.env
```

Hành vi:

1. `$EnvPath` rỗng → mặc định `Join-Path (Split-Path $PSScriptRoot -Parent) '.env'`.
2. File không tồn tại → `throw "Environment file not found: <path>. Run setup.ps1 at the repository root first."`.
3. Đọc bằng `Get-Content -Encoding UTF8`, xử lý từng dòng:
   - Bỏ qua dòng trống và dòng bắt đầu bằng `#`.
   - Dòng không có `=` (hoặc `=` ở vị trí 0) → bỏ qua.
   - Key chứa khoảng trắng → bỏ qua.
   - Strip cặp quote bao ngoài (`"..."` hoặc `'...'`) nếu có.
4. Lưu vào hashtable và `[Environment]::SetEnvironmentVariable($key, $value, 'Process')`
   (giữ nguyên tên key, ví dụ `SMARTPARK_DEV_PASSWORD`).
5. `return` hashtable.

Cách dùng:

```powershell
# Dot-source trong setup.ps1: vừa export env, vừa lấy map trong scope hiện tại
$envMap = . $loadEnvScript -EnvPath $envFile

# Hoặc gọi như script và bỏ qua giá trị trả về (run-schema-service.ps1)
$null = & (Join-Path $PSScriptRoot 'load-env.ps1') -EnvPath $envFile
```

Giá trị **không bao giờ** được log. Khi in ra, chỉ in tên key.

---

## 4. Thêm một biến `.env` mới

1. Khai báo trong `.env.example` (root), có comment nhóm rõ ràng.
2. Nếu **bắt buộc**: thêm validate ở bước `[2/7] Validate` — thêm vào `$issues`
   khi thiếu/không hợp lệ (dùng `Test-Placeholder` nếu cần chặn placeholder).
   Nếu chỉ tùy chọn: thêm vào `$warnings`.
3. Nếu phải truyền cho backend: map sang user-secrets ở bước `[6/7]` (thêm key
   vào `$settings` của `User`; hoặc `$sharedSettings` nếu dùng cho cả 3),
   HOẶC truyền qua env var cho compose / `run-schema-service.ps1`.
4. Nếu ảnh hưởng compose: thêm biến vào `compose.schema-integration.yml` với
   default an toàn, dạng `${MY_VAR:-default}`.
5. Chạy lại các bước test ở mục 7.

Lưu ý: `load-env.ps1` không cần sửa khi thêm biến — nó tự export mọi key.

---

## 5. Thêm một bước mới vào `setup.ps1`

```powershell
Invoke-SetupStep -Index <n> -Name '<Tên bước>' -Body {
    # ... công việc ...
    if (<điều kiện lỗi>) { throw 'thông báo lỗi rõ ràng' }   # => FAIL tự động
    return [pscustomobject]@{
        Status = 'SKIP'                  # nếu bỏ qua hợp lệ
        Detail = 'lý do/lý do ngắn'
        Lines  = @('chi tiết')
    }
}
```

Quy tắc:

- `-Index` là số nguyên dùng cho nhãn log `[Index/7]`. Nếu thêm bước, phải cập
  nhật mẫu số `7` trong `Complete-Step`/`Show-StepSummary` cho nhất quán.
- Body phải **idempotent**: kiểm tra tồn tại trước khi tạo container/network/
  volume; dùng `docker compose up -d` (idempotent); tái sử dụng `Invoke-SwallowedCommand`.
- Không log secret. Chỉ in tên key.
- Lệnh native có redirect stderr: bọc bằng `Get-ToolVersion`/`Invoke-SwallowedCommand`.

---

## 6. Ràng buộc quan trọng

- **PowerShell 5.1**: KHÔNG dùng `??`, ternary `? :`, `-Parallel`, hay cú pháp
  PS7-only khác. Phải chạy trên Windows PowerShell 5.1.
- **Không hardcode path**: dùng `$PSScriptRoot`; script trong `scripts/` resolve
  repo root bằng `Split-Path $PSScriptRoot -Parent`.
- **Không commit / không log secret**: `.env` bị gitignore; secret truyền qua
  pipe JSON (`$settings | ConvertTo-Json | dotnet user-secrets set ...`), không
  qua command-line argument. Output của `user-secrets set` pipe ra `Out-Null`;
  `user-secrets list` chỉ in **tên key**.
- **Idempotent**: chạy lại không lỗi, không tạo trùng container/network/volume.
  `.env` đã tồn tại thì prompt `Overwrite/Keep/Abort`, không tự ghi đè.
- **Fail-fast**: bước `FAIL` dừng toàn bộ và trả exit code `1`.
- **`setup.ps1` bị gitignore**: mọi thay đổi phải được phân phối lại qua kênh
  nội bộ team; `load-env.ps1` và `.env.example` thì có trong git.

---

## 7. Cách test thay đổi

### Syntax check (không thực thi)

```powershell
powershell -NoProfile -Command "[void][scriptblock]::Create((Get-Content -Raw -LiteralPath '<file>.ps1'))"
```

Ví dụ kiểm tra toàn bộ file `.ps1` trong repo (trừ `node_modules`/`.cache`):

```powershell
Get-ChildItem -Recurse -Filter *.ps1 -File |
  Where-Object { $_.FullName -notmatch '\\node_modules\\|\\.cache\\' } |
  ForEach-Object {
    try { [void][scriptblock]::Create((Get-Content -Raw -LiteralPath $_.FullName)); "OK   $($_.Name)" }
    catch { "FAIL $($_.Name) :: $($_.Exception.Message)" }
  }
```

### Kiểm tra compose render (không cần daemon)

```powershell
$env:SMARTPARK_DEV_PASSWORD='validation-only'
docker compose -f compose.schema-integration.yml config
```

Kiểm tra port override (phải thấy `published: "<port>"` tương ứng):

```powershell
$env:SMARTPARK_DB_USER_PORT='6001'
docker compose -f compose.schema-integration.yml config
```

### ⚠️ KHÔNG chạy `setup.ps1` thật khi DB đang có dữ liệu

`setup.ps1` chạy `docker compose up`, migration và ghi user-secrets — có thể
phá dữ liệu local. Chỉ chạy khi Docker sạch, hoặc dùng reset có chủ đích
(`docker compose -f compose.schema-integration.yml down -v` — mất dữ liệu).
Ưu tiên syntax check + `compose config` khi review thay đổi.

---

## 8. Trạng thái legacy

Các file/thành phần cũ dùng `.cache/qa-local.json` **không còn** là đường setup
chính. Hiện trạng:

| File | Trạng thái |
| --- | --- |
| `scripts/import-local-config.ps1` | Vẫn tồn tại; đọc `.cache/qa-local.json`, map `': '` → `__` và set env (chỉ khi env chưa có). Không còn được `setup.ps1`/`run-schema-service.ps1` gọi. |
| `scripts/qa-local.example.json` | Config mẫu cũ (`.cache/qa-local.json`). Giữ để tham chiếu; nên chuyển sang `.env`. |
| `scripts/setup-local.ps1` | Đã thành deprecation shim → forward sang root `setup.ps1`. Không xóa. |
| `scripts/database/compose.remaining-services.yml` | Đã merge vào `compose.schema-integration.yml` và xóa file. |
| `docs/04-testing/database-integration-guide.md` | Giữ nội dung lịch sử (`arch/database-schema` worktree); đã thêm ghi chú trỏ về `setup.ps1`/`QUICK_START.md`. |
| `LOCAL-SETUP.md` | Đã cập nhật sang luồng root `.env` (`setup.ps1`). |

Khuyến nghị (không bắt buộc trong scope hiện tại): migrate các flow legacy sang
root `.env` qua `scripts/load-env.ps1`, dần thay tham chiếu `.cache/qa-local.json`
bằng `.env`. **KHÔNG xóa** file cũ cho tới khi không còn tham chiếu.

---

## 9. Điểm cần chú ý khi bảo trì

- `run-schema-service.ps1` đã bỏ tham số `-ConfigPath`; caller cũ truyền
  `-ConfigPath` sẽ gặp lỗi binding. Giữ `--contentRoot $projectDir` (hành vi cũ).
- `ConnectionStrings__<Service>` bọc password trong dấu `"` để an toàn với dấu
  `;`.
- SonarQube dùng 2 named volume `sonarqube_data_extensions`,
  `sonarqube_data_logs` (spec gốc `sonarqube_data/extensions/logs` không phải là
  tên volume hợp lệ vì chứa `/`), cộng `sonarqube_db_data`.
- `sonarqube-db` được gắn vào network `smartpark-net` để container `sonarqube`
  resolve được hostname trong `SONAR_JDBC_URL`.
- `SMARTPARK_SCHEMA_ROOT` đã gỡ khỏi `.env.example` vì `setup.ps1` không tiêu
  thụ biến này (luôn dùng repo root). `compose.remaining-services.yml` đã bị xóa
  (merge vào compose chính), nên biến này hiện chỉ còn xuất hiện trong các script/
  tài liệu legacy (`scripts/qa-local.example.json`, `scripts/recover-structure-hold.ps1`).
