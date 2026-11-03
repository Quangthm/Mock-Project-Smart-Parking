# 09 — setup.ps1: SonarQube mặc định + AI Camera Docker + Dev container (thêm rồi xoá) + Seed user OWNER/OPERATOR

> Report này ghi nhận 5 phần thay đổi lớn do T7 thực hiện, sau khi T1–T6 hoàn tất DB hybrid. Bao gồm các sửa lỗi runtime (P0) phát hiện khi build lần đầu.

## Meta

| Hạt | Giá trị |
|---|---|
| Ngày | 2026-10-19 |
| Branch | `feature/db-hybrid-srs-v1` |
| Base commit | `0c8a6fb` (= `develop`) |
| Trạng thái git | Toàn bộ thay đổi ở working tree, chưa commit |

## Phần 1 — SonarQube: opt-in → mặc định chạy

### Vấn đề
User chạy `.\setup.ps1` thuần không thấy SonarQube; không phải code bị xoá mà flag `-WithSonarQube` mặc định `$false` từ develop.

### Sửa `setup.ps1`
- **Thêm param:** `[switch]$SkipSonarQube`. Biến `$sonarEnabled = !$SkipSonarQube` (dòng 13).
- **Giữ `[switch]$WithSonarQube`** deprecated no-op để lệnh cũ parse OK, không gây lỗi.
- **Credential SONAR** (password, port) luôn được chuẩn bị (kể cả `-SkipDatabase`).
- **Block start container:** `if ($sonarEnabled -and !$SkipDatabase)` — giữ `$SkipDatabase` không phụ thuộc Docker.
- **Final message:** In URL SonarQube UI khi enabled.
- **Failure mode:** Volume cũ lệch password → throw hướng dẫn khôi phục password gốc.

### File liên quan
- `setup.ps1` — logic chính
- `.env.example` — thêm/hiệu chỉnh biến SONAR
- `LOCAL-SETUP.md`, `QUICK_START.md`, `UPDATE_SCRIPT_GUIDE.md` — docs cập nhật
- `scripts/test-setup.ps1` — regression test

### Verify
- PowerShell parser: 0 lỗi
- `test-setup.ps1`: PASS

---

## Phần 2 — AI Camera (python-ai) Docker: compose + P0 fix + Dockerfile warm-up fix

### S1 — `compose.ai.yml` (NEW)
- 2 service: `ai-rest` (port 5001) + `ai-live` (port 5002).
- Dùng YAML anchor chung cho env/logging.
- Socket healthcheck cho cả 2 service.
- External network: `smartpark-schema_default`.

### S2 — `main_live.py` — P0 fix + health route
- Bọc `load_dotenv(Path(...).parents[4])` trong `try/except IndexError/OSError` để an toàn khi chạy trong container (không có cấu trúc thư mục local).
- Thêm route `GET /health` trả `{"status": "ok"}`.

### S3 — `setup.ps1`: flag AI Camera
- Thêm `[switch]$WithAiCamera`.
- Block validate/build/up giống pattern SonarQube.
- Final message in URL AI services khi enabled.

### S4 — `requirements.txt`
- `opencv-python` → `opencv-contrib-python`.
- Thêm `requests`.
- Thêm `--extra-index-url .../whl/cpu` cho CPU wheels.

### P0 Runtime bug — zlib decompression fail
**Lỗi phát hiện khi build lần đầu:**
```
zlib.error: Error -2 while decompressing data: inconsistent stream state
```
Xảy ra tại Dockerfile warm-up step `from paddleocr import PaddleOCR`.

**Root cause:**
PaddlePaddle's `libpaddle.so` interposes zlib symbols process-wide → mọi C extension load sau paddle (pyclipper) crash.

**Fix:**
Thêm preload `import scipy._lib._ccallback; import skimage.morphology` trước `from paddleocr import PaddleOCR` trong Dockerfile `RUN`.

**Tham khảo:** `muhammad-asifkhan/ocr` PR #1 (commit c56a2a9).

### File mới / sửa
| File | Trạng thái |
|---|---|
| `compose.ai.yml` | NEW |
| `main_live.py` | Sửa |
| `requirements.txt` | Sửa |
| `Dockerfile` | Sửa (preload workaround) |
| `setup.ps1` | Sửa |
| `QUICK_START.md`, `LOCAL-SETUP.md`, `UPDATE_SCRIPT_GUIDE.md` | Sửa |
| `scripts/test-setup.ps1` | Sửa |
| `.env.example` | Sửa |

### Verify
- `docker compose -f compose.ai.yml config --quiet` → exit 0
- `python -m py_compile main_live.py` → exit 0; `python -m py_compile main.py` → exit 0
- PS parser `setup.ps1` → 0 errors
- Build lần đầu `setup.ps1 -WithAiCamera`:
  - Trước fix: fail (zlib error)
  - Sau fix Dockerfile: build OK (đã xác minh user build thành công)
- `test-setup.ps1` regression → PASS

---

## Phần 3 — Dev container (FE + 3 BE): thêm rồi xoá

### Lý do thêm
User yêu cầu step [8/8] chạy cả FE + 3 BE trong 1 container để dev nhanh.

### Đã triển khai
- `Dockerfile.dev` — multi-stage chứa Node + .NET SDK
- `compose.dev.yml` — 1 service chạy FE + BE đồng thời
- `scripts/docker/dev-entrypoint.sh` — start script

### Vấn đề phát hiện khi test
1. **BE `dotnet run` fail** — `DirectoryNotFoundException` vì `--contentRoot` relative path không tồn tại trong container; cần absolute path.
2. **OOM kill (Exit 137)** — 3 dotnet SDK process (UserService, FacilityService, OperationService) + Node dev server vượt quá RAM allocation.
3. **Healthcheck sai** — chỉ check FE:5173 (TCP) → pass sớm nhưng BE chưa build xong.

### Kết luận
Dev container chiếm tài nguyên quá lớn, không ổn định. User quyết định xoá toàn bộ.

### Đã xoá
- `Dockerfile.dev`
- `compose.dev.yml`
- `scripts/docker/dev-entrypoint.sh`
- `setup.ps1`: param `-WithDevContainer`, block [8/8], final message dev
- Docs revert: 3 docs file
- Docker image `smartpark-dev:local` đã xoá, container đã dừng.

### Ghi nhận
Cách chạy riêng BE/FE vẫn dùng `scripts/run-schema-service.ps1` + `npm run dev` như cũ — không thay đổi.

---

## Phần 4 — Seed user OWNER + OPERATOR (login thẳng, không qua OTP)

### Sửa `DataSeeder.cs`
- Thêm method generic **`SeedRoleUserAsync(email, password, fullName, roleCode, roleName)`** — mirror logic `SeedAdminAsync` (idempotent, validate email/password).
- **Refactor `SeedAdminAsync`** thành 1-liner gọi `SeedRoleUserAsync`.

### Sửa `Program.cs` (UserService)
- Thêm 2 block: `DevelopmentOwner` + `DevelopmentOperator` (mirror admin pattern).
- Config key mapping:
  ```
  DevelopmentOwner__Email / DevelopmentOwner__Password
  DevelopmentOperator__Email / DevelopmentOperator__Password
  ```

### Sửa `run-schema-service.ps1`
- Map biến môi trường:
  ```
  SMARTPARK_OWNER_EMAIL → DevelopmentOwner__Email
  SMARTPARK_OWNER_PASSWORD → DevelopmentOwner__Password
  SMARTPARK_OPERATOR_EMAIL → DevelopmentOperator__Email
  SMARTPARK_OPERATOR_PASSWORD → DevelopmentOperator__Password
  ```

### Sửa `.env.example`
- Thêm 4 biến mới.

### Verify
- `dotnet build SmartParking.UserService.API` → 0 errors
- PS parser `run-schema-service.ps1` → 0 errors

---

## Phần 5 — Kiểm tra tổng thể setup.ps1

### 6 DB mapping
- Compose mounts → từng service mount đúng volume.
- `apply-workflow-migrations.ps1` script list → tên DB đúng cho từng service.

### Runtime test
- Chạy qua cả 6 service trên container đang live.
- Kết quả: tất cả trả về `"schema already current; verified required relations"` — không lỗi.

### SRS v1.0 đối chiếu
- DB names trong SRS là naming guidance, không cần sửa.

---

## Tổng kết

| Phần | Thay đổi | Trạng thái |
|---|---|---|
| 1 — SonarQube | Mặc định chạy, thêm `-SkipSonarQube` | ✅ Xong |
| 2 — AI Camera | Compose mới, P0 fix zlib, Dockerfile workaround | ✅ Xong |
| 3 — Dev container | Thêm rồi xoá — không ổn định | 🔙 Đã xoá |
| 4 — Seed OWNER/OPERATOR | Generic seeder + config mapping | ✅ Xong |
| 5 — Kiểm tra tổng thể | 6 DB, runtime, SRS | ✅ Pass |

### Lệnh verify chính
```powershell
# 1. SonarQube — check param
.\setup.ps1 -SkipSonarQube       # SonarQube không chạy
.\setup.ps1                      # SonarQube chạy mặc định

# 2. AI Camera
docker compose -f compose.ai.yml config --quiet
python -m py_compile main_live.py
python -m py_compile main.py
.\setup.ps1 -WithAiCamera        # Build container AI

# 3. Seed OWNER/OPERATOR
dotnet build src/Services/UserService/SmartParking.UserService.API

# 4. Run regresion
.\scripts\test-setup.ps1
```