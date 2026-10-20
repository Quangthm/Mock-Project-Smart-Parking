# Bảo trì setup SmartParking

`setup.ps1` được tracked trong Git. Đây là implementation hiện tại; script in progress lines `[1/7]`…`[7/7]` (cộng dòng con `[n/6]` cho từng DB service) để xác định nhanh bước lỗi — mapping bước ↔ lỗi thường gặp ở [QUICK_START.md](QUICK_START.md).

## Luồng và tham số

1. Resolve repo qua `$PSScriptRoot`, đọc credential user-secrets hiện có mà không log value.
2. Đọc `.env` hoặc template bằng `load-env.ps1 -NoExport`; nếu `.env` đã tồn tại và phiên bản tương tác, hỏi `[O]verwrite / [K]eep / [A]bort` (Overwrite backup ra `.env.bak` rồi sinh lại từ `.env.example`; `-NonInteractive` bỏ qua prompt, giữ `.env` hiện có); chế độ Team nhập whitelist key từ file riêng và từ chối xung đột.
3. Local dùng lại credential hoặc sinh phần thiếu. Validate trước khi ghi cấu hình. Không đổi giá trị đã có.
4. Dựng DB bằng compose canonical, probe password qua TCP, áp migrations cho từng service với `-EnvPath` tường minh (runner probe trạng thái DB: schema hiện tại → skip, DB trống → apply, schema cũ → báo `-RecreateDb`).
5. Đồng bộ user-secrets theo `.env` (ghi thiếu, **ghi đè giá trị lệch**, xóa khóa optional JWT/SMTP không còn trong `.env`); `-SkipUserSecrets` bỏ bước này và yêu cầu dùng runner `.env`.
6. Tạo FE env nếu thiếu, bổ sung MapTiler và URL AI (REST 5001 / live 5002) nếu trống; dựng compose SonarQube (sinh `SONAR_DB_PASSWORD` khi thiếu, bỏ qua với `-SkipSonarQube`); nếu `-WithAiCamera`: build + start container AI (`compose.ai.yml`, project `smartpark-ai`) sau khi DB và SonarQube đã lên; restore solution.

| Tham số | Ý nghĩa |
| --- | --- |
| `-CredentialMode Local` | Mặc định, credential riêng trên máy nếu chưa cấu hình |
| `-CredentialMode Team -TeamEnvPath <file>` | Bộ credential Team tường minh; không sinh ngẫu nhiên thay giá trị Team |
| `-SkipDatabase` | Không dựng DB, không migration |
| `-SkipRestore` | Không restore NuGet |
| `-SkipUserSecrets` | Không ghi user-secrets; chạy API qua runner |
| `-SkipSonarQube` | Không dựng SonarQube (mặc định tự dựng cùng setup và sinh `SONAR_DB_PASSWORD` khi thiếu); `-WithSonarQube` còn nhận nhưng không còn tác dụng |
| `-WithAiCamera` | Build và start AI camera containers (ai-rest :5001, ai-live :5002) từ `compose.ai.yml` (project `smartpark-ai`); cần schema DBs đang chạy, không tương thích với `-SkipDatabase`; build lần đầu ~5–10' |
| `-NonInteractive` | Bỏ qua prompt `.env` Overwrite/Keep/Abort (giữ `.env` hiện có); dùng cho CI/test tự động |

`load-env.ps1 -NoExport` trả hashtable và không thay process environment. Không có switch thì giữ behavior export hiện có. `apply-workflow-migrations.ps1 -Service <service> -SchemaRoot <checkout> [-EnvPath <env>] [-ProjectName <project>]` luôn dùng đúng env file; mặc định là `.env` của SchemaRoot, không phụ thuộc thư mục terminal. Không reset volume: runner probe state — schema hiện tại → skip, DB trống → áp canonical, schema cũ → báo chạy `-RecreateDb`.

## Chuyển từ setup local ignored sang tracked

Trước khi pull/merge, copy script riêng cũ ra ngoài repo hoặc vào `.cache/setup-backup/` và giữ lại `.env`/user-secrets. Không dùng script cũ ghi đè file tracked mới. Chuyển những cấu hình riêng cần giữ sang `.env` hoặc file Team. Sau tích hợp, kiểm tra `git ls-files setup.ps1` và chạy setup với đúng chế độ. Nếu Git chặn file untracked cùng tên, chỉ di chuyển file đó sau khi backup; không dùng clean/reset để vượt qua lỗi.

## Kiểm tra thay đổi

- Giữ cú pháp PowerShell 5.1; parse script trước khi chạy.
- Dùng `scripts/test-setup.ps1` để kiểm thử credential/import/idempotence với CLI doubles trong scratch checkout; không chạm user-secrets hoặc DB thật.
- Kiểm tra Compose bằng `config --quiet` để tránh in secret.
- Kiểm chứng migration trên PostgreSQL tạm bằng `scripts/test-workflow-upgrade.ps1`: DB trống → runner áp trọn bộ canonical, chạy lần hai xác nhận skip, restart compose giữ nguyên dữ liệu sentinel.
- Chạy UserService và Parking structure integration tests trên DB UUID riêng. Không chạy thử setup lên volume deployment để review.

Không log password/key; JSON credential chỉ gửi qua stdin cho `dotnet user-secrets set`. Chỉ thêm key trong `.env.example`, mapping setup/runner và cập nhật hướng dẫn khi thêm cấu hình. Thiết lập Sonar thật phụ thuộc tài nguyên Docker/kernel và version image; compose render không chứng minh Sonar đã operational.

Project migration mặc định là `smartpark-schema`. Mọi lệnh Compose truyền `--project-name` tường minh; test upgrade truyền UUID riêng kể cả cleanup, nên `COMPOSE_PROJECT_NAME` trong terminal không thể đổi target. Chạy test upgrade bằng `powershell -NoProfile -File scripts/test-workflow-upgrade.ps1`; chỉ script test này dọn các volume UUID do chính nó tạo.
