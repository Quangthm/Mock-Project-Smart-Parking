# 07 — setup.ps1: -RecreateDb, 6-port validation, 6-service loop

## Meta
- **Ngày:** 2026-10-18
- **Branch:** `feature/db-hybrid-srs-v1`
- **Base commit:** `develop` @ `0c8a6fb`
- **File:** `setup.ps1` (root)
- **Lane:** T5 infra (setup)

## Thay đổi chính
1. **Thêm switch `-RecreateDb`** (dòng 2) + comment mô tả (dòng 6): drop schema volumes và init lại cả 6 DB từ SQL init scripts; destructive — xoá dev data.
2. **Block `down -v`** (dòng 186–190): khi `-RecreateDb`, chạy `docker compose --project-name smartpark-schema --env-file $envFile -f $composeFile down -v` **trước** `up -d --wait`, dùng **cùng** project name / env file / compose file như `up` (chỉ target schema project).
3. **Vòng validate port** (dòng 178) mở rộng từ 3 lên **6 biến**: `SMARTPARK_DB_USER_PORT`, `SMARTPARK_DB_PARKING_PORT`, `SMARTPARK_DB_RESERVATION_PORT`, `SMARTPARK_DB_PAYMENT_PORT`, `SMARTPARK_DB_NOTIFICATION_PORT`, `SMARTPARK_DB_IOT_PORT`.
4. **Vòng migration + probe** (dòng 193) mở rộng từ 3 lên **6 service**: `@('User','Parking','Reservation','Payment','Notification','Iot')`; container `$service.ToLowerInvariant() + '-db'` khớp tên service trong compose; runner `-Service` nhận đủ 6 giá trị.
5. **Password guard** (dòng 84–94): khi `-RecreateDb` được set, **không throw** khi phát hiện volume cũ + password placeholder — hạ xuống `Write-Warning 'Password guard skipped because -RecreateDb will wipe existing schema volumes.'` (dòng 90) vì volume sắp bị `down -v` xoá. Khi **không** có `-RecreateDb`, giữ nguyên hành vi `throw`.
5. **Vòng user-secrets** (dòng 205) **giữ nguyên 3 service** (`User`,`Parking`,`Reservation`) — chỉ 3 service .NET có user-secrets; cố ý không đổi.

## Lý do / quyết định liên quan
- **Q1 (T4):** 6 DB vật lý ngay trong `setup.ps1`.
- **Risk #3 (plan):** volume cũ = schema cũ ⇒ init script không chạy lại ⇒ cần `-RecreateDb` để `down -v` rồi init lại.
- **D1 (T4):** tên port env giữ nguyên; chỉ mở rộng số lượng biến được validate.
- Password guard: khi volume cũ + password placeholder, guard cũ throw **trước** khi `down -v` chạy ⇒ chặn nhầm luồng recreate; nay downgrade thành warning khi `-RecreateDb` được set.

## Evidence
`git diff 0c8a6fb -- setup.ps1` (trích, đã lược):

```diff
-    [switch]$SkipDatabase, [switch]$SkipRestore, [switch]$SkipUserSecrets,
+    [switch]$SkipDatabase, [switch]$SkipRestore, [switch]$SkipUserSecrets, [switch]$RecreateDb,
…
-                if ($volumes) { throw 'Existing database volumes found. …' }
+                if ($volumes) {
+                    if ($RecreateDb) {
+                        Write-Warning 'Password guard skipped because -RecreateDb will wipe existing schema volumes.'
+                    } else {
+                        throw 'Existing database volumes found. …'
+                    }
+                }
…
-foreach ($name in @('SMARTPARK_DB_USER_PORT','SMARTPARK_DB_PARKING_PORT','SMARTPARK_DB_RESERVATION_PORT')) {
+foreach ($name in @('SMARTPARK_DB_USER_PORT','SMARTPARK_DB_PARKING_PORT','SMARTPARK_DB_RESERVATION_PORT','SMARTPARK_DB_PAYMENT_PORT','SMARTPARK_DB_NOTIFICATION_PORT','SMARTPARK_DB_IOT_PORT')) {
…
+    if ($RecreateDb) {
+        & docker compose --project-name smartpark-schema --env-file $envFile -f $composeFile down -v
+        if ($LASTEXITCODE -ne 0) { throw 'Failed to remove schema volumes for -RecreateDb. Existing volumes were preserved.' }
+    }
     & docker compose --project-name smartpark-schema --env-file $envFile -f $composeFile up -d --wait
-    foreach ($service in @('User','Parking','Reservation')) {
+    foreach ($service in @('User','Parking','Reservation','Payment','Notification','Iot')) {
```

Line refs hiện tại: `-RecreateDb` dòng 2; comment dòng 6; password guard dòng 88–90; port loop dòng 178; `down -v` dòng 186–190; migration/probe loop dòng 193; user-secrets loop dòng 205 (giữ 3 service).

## Ảnh hưởng
- `.\setup.ps1 -RecreateDb` là đường chạy T6: wipe volume cũ → `up -d --wait` 6 DB → probe auth + apply migration cho cả 6 service.
- Không có `-RecreateDb`: hành vi cũ giữ nguyên (throw khi volume cũ + password mismatch).
- User-secrets vẫn chỉ cấu hình cho 3 .NET service (User/Parking/Reservation) — có chủ đích.

## Cách tái kiểm chứng
```powershell
git diff 0c8a6fb -- setup.ps1
# syntax check
$e=$null;$t=$null;[System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path setup.ps1),[ref]$t,[ref]$e); $e.Count
# chạy recreate (destructive — xoá dev data)
.\setup.ps1 -RecreateDb
```

## Cập nhật 2026-10-18 — user-secrets sync theo `.env` (fix 28P01)

- **Triệu chứng:** chạy BE trực tiếp từ Visual Studio/`dotnet run` fail `password authentication failed for user "smartpark_user"` (28P01); đường chạy qua runner `run-schema-service.ps1` không bị.
- **Root cause:** user-secrets đã lưu connection string chứa password cũ, lệch với password mà volume PostgreSQL đã init. `DbConnectionStringBuilder` không tokenize được giá trị legacy (trả về một key `ConnectionString` duy nhất) nên `[2/7]` không đọc lại được password để reuse, và `[3/7]`/`[5/7]` cũ không phát hiện/giá trị lệch — `[5/7]` chỉ ghi key thiếu, không bao giờ sửa giá trị đã lưu.
- **Thay đổi trong `setup.ps1`:**
  - `[5/7]` đồng bộ toàn bộ theo `.env`: ghi key thiếu, **ghi đè giá trị lệch**, xóa khóa optional (`Jwt:PrivateKeyPem`, `Otp:Smtp:*`) không còn trong `.env`. Message: `<Service> configuration ready (N synced, M removed).`
  - `[3/7]` bỏ các conflict throw `... differs from .env`; còn lại validate value (key ≥32, policy admin, 6 port, URLs, MapTiler, SonarQube). Conflict `Team/local conflict` ở `[2/7]` giữ nguyên.
  - `[2/7]` thêm helper `Get-SecretConnectionStringPassword`: thử `DbConnectionStringBuilder`, fallback regex `Password="..."` / `Password=([^;]+)` khi tokenizer fail — đọc lại được password của volume cũ.
  - Ghi đè `DevelopmentAdmin:Password` phát cảnh báo: setup **không đổi** admin password đã seed trong DB; nếu login Admin fail sau đó, khôi phục password cũ trong `.env` hoặc chạy `-RecreateDb`.
- **An toàn pipeline:** `[4/7]` auth-probe đảm bảo `.env` password == password volume trước khi `[5/7]` sync secrets theo `.env`; `-SkipDatabase` bỏ qua guarantee này.
- **Test:** `scripts/test-setup.ps1` cập nhật case sync (Services:Key/URL/Admin lệch → ghi đè về `.env`), removal (JWT/SMTP stale → xóa), warning admin-password, giữ conflict Team/local + old-volume guard — **PASS** (chạy `powershell -NoProfile -File scripts\test-setup.ps1`).
