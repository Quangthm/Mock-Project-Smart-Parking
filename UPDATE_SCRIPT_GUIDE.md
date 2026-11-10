# Bảo trì setup SmartParking

`setup.ps1` được tracked trong Git. Đây là implementation hiện tại; script in progress lines `[1/7]`…`[7/7]` (cộng dòng con `[n/6]` cho từng DB service) để xác định nhanh bước lỗi — mapping bước ↔ lỗi thường gặp ở [QUICK_START.md](QUICK_START.md).

## Luồng và tham số

1. Resolve repo qua `$PSScriptRoot`, đọc credential user-secrets hiện có mà không log value.
2. Đọc `.env` hoặc template bằng `load-env.ps1 -NoExport`; nếu `.env` đã tồn tại và phiên bản tương tác, hỏi `[O]verwrite / [K]eep / [A]bort` (Overwrite backup ra `.env.bak` rồi sinh lại từ `.env.example`; `-NonInteractive` bỏ qua prompt, giữ `.env` hiện có); chế độ Team nhập whitelist key từ file riêng và từ chối xung đột.
3. Local dùng lại credential hoặc sinh phần thiếu. Validate trước khi ghi cấu hình. Không đổi giá trị đã có.
4. Dựng DB bằng compose canonical, probe password qua TCP, áp migrations cho từng service với `-EnvPath` tường minh (runner probe trạng thái DB: schema hiện tại → skip, DB trống → apply, schema cũ → báo `-RecreateDb`).
5. Đồng bộ user-secrets theo `.env` (ghi thiếu, **ghi đè giá trị lệch**, xóa khóa optional JWT/SMTP không còn trong `.env`); `-SkipUserSecrets` bỏ bước này và yêu cầu dùng runner `.env`.
6. Tạo FE env nếu thiếu, bổ sung MapTiler và URL AI (REST 5001 / live 5002) nếu trống; dựng compose SonarQube (sinh `SONAR_DB_PASSWORD` khi thiếu, bỏ qua với `-SkipSonarQube`); nếu `-WithAiCamera`: kiểm tra model/network, dò image + container AI rồi mới quyết định build/start (`compose.ai.yml`, project `smartpark-ai`) sau khi DB và SonarQube đã lên; restore solution.

| Tham số | Ý nghĩa |
| --- | --- |
| `-CredentialMode Local` | Mặc định, credential riêng trên máy nếu chưa cấu hình |
| `-CredentialMode Team -TeamEnvPath <file>` | Bộ credential Team tường minh; không sinh ngẫu nhiên thay giá trị Team |
| `-SkipDatabase` | Không dựng DB, không migration |
| `-SkipRestore` | Không restore NuGet |
| `-SkipUserSecrets` | Không ghi user-secrets; chạy API qua runner |
| `-SkipSonarQube` | Không dựng SonarQube (mặc định tự dựng cùng setup và sinh `SONAR_DB_PASSWORD` khi thiếu); `-WithSonarQube` còn nhận nhưng không còn tác dụng |
| `-WithAiCamera` | Kiểm tra model/network và dựng AI camera containers (ai-rest :5001, ai-live :5002) từ `compose.ai.yml` (project `smartpark-ai`); chỉ build khi image chưa có; cần schema DBs đang chạy, không tương thích với `-SkipDatabase`; build lần đầu ~5–10' |
| `-RebuildAi` | Đi cùng `-WithAiCamera`: buộc build lại image `smartpark-ai:local` có cache rồi start; dùng một mình sẽ throw |
| `-NonInteractive` | Bỏ qua prompt `.env` Overwrite/Keep/Abort (giữ `.env` hiện có); dùng cho CI/test tự động |

AI camera dùng các helper trong `setup.ps1`: `Test-AiModelAssets`/`Assert-AiModelAssets` (7 file model, chạy trước mọi lệnh Docker của AI), `Get-AiImageState` (`docker image inspect --format`), `Test-AiImageLibraries` (probe import, timeout 60s, chỉ ở nhánh image có nhưng chưa có container), `Get-AiStackState` (`compose ps --all --format json`, fallback `docker ps --filter label=com.docker.compose.project=smartpark-ai`), `Show-AiResolutionPlaybook`, `Read-AiResolutionChoice` và `Invoke-AiCameraSetup`. State matrix: image thiếu → build; image + container có → `up -d --wait --no-build`; image có, container không → playbook + prompt (non-interactive chọn Start); container dùng image ID cũ → cảnh báo và `--force-recreate --no-build`; unhealthy → không tự rebuild, in log rồi lỗi nếu `up --wait` fail. `-RebuildAi` chỉ build có cache; không tự clean rebuild.

`load-env.ps1 -NoExport` trả hashtable để setup đọc cấu hình mà không export giá trị. Export mặc định loại private JWT signing variables, kể cả biến kế thừa từ shell và alias ASP.NET; chỉ runner UserService được opt in. Các đường export khác không được cấp private key. `apply-workflow-migrations.ps1 -Service <service> -SchemaRoot <checkout> [-EnvPath <env>] [-ProjectName <project>]` luôn dùng đúng env file; mặc định là `.env` của SchemaRoot, không phụ thuộc thư mục terminal. Không reset volume: runner probe state — schema hiện tại → skip, DB trống → áp canonical, schema cũ → báo chạy `-RecreateDb`.

## JWT signing và public trust (SPARK-327)

Giữ nguyên workflow `.env` duy nhất: missing → template; existing → Overwrite/Keep/Abort, Enter và NonInteractive là Keep, Overwrite backup `.env.bak`; không merge key template mới, không tự sinh/thay RSA signing key. Base64 placeholder không phải key. Hướng dẫn developer và rotation ở [LOCAL-SETUP §5.5](LOCAL-SETUP.md#55-jwt-ổn-định-và-jwks-spark-327).

- `[5/7]` map UserService sang `Jwt:Issuer/Audience/KeyId/PrivateKeyPemBase64/PrivateKeyPem/JwksPath`; giữ stdin JSON và xử lý removable keys. Consumer chỉ có `JwtValidation:Issuer/Audience/JwksUri`, không có private key (kể cả secret cũ còn sót).
- Runner host chỉ opt in signing-secret export khi target là UserService, dọn môi trường signing khi runner kết thúc. Non-User runner phải scrub cả raw `SMARTPARK_JWT_PRIVATE_KEY_*`, `Jwt__PrivateKey*`/`Jwt:PrivateKey*` và prefixed alias kế thừa trước khi spawn process. Không làm mất DB/SMTP/RabbitMQ/payment settings hiện có.
- Host JWKS URI derive từ UserService URL đã cấu hình; Docker dùng internal `http://user-service:8080` + JWKS path. Compose chỉ đưa signing key vào `user-service.environment`, không build args hay FE. Không copy public PEM sang consumer.
- `KeyId` nonempty yêu cầu key ổn định. Chỉ Development không key/no kid được ephemeral. Legacy PEM vẫn hợp lệ, hai input cùng lúc phải cùng key. Non-Development/malformed configuration fail startup; không fallback âm thầm.
- Khi sửa mapping, kiểm tra [matrix trong environment skill](docs/07-skills/SmartParking_Env_Config_SKILL_v1.0.md#8-jwt-signing-and-public-trust-spark-327), script harness có parent-shell leakage, PS 5.1 parser, Compose `config --quiet`, và UserService JWT/JWKS tests. Không dump Compose config hoặc user-secrets vào log.

## Docker app runner

`compose.app.yml` chạy 6 container riêng (`user-service`, `facility-service`, `operation-service`, `payment-service`, `notification-service`, `frontend`) trong project `smartpark-app`, dùng external network `smartpark-schema_default`. Backend dùng `docker/Dockerfile.backend` với build arg `SERVICE`; FE dùng `FE/Dockerfile` (Node 22, Vite port 5173). Không thêm `container_name` để tránh xung đột nhiều checkout.

`scripts/run-app-service.ps1` nhận `-Service User|Facility|Operation|Frontend|All`, `-NoBuild`, `-NoLogWindow`. Script kiểm tra `.env`, `compose.app.yml`, network, chạy `up -d`, poll trạng thái tối đa 120 giây, in 100 dòng log và fail nếu container exit/unhealthy. Mỗi target mở một cửa sổ CMD `logs -f` trừ khi có `-NoLogWindow`. `scripts/stop-app-service.ps1` dừng target hoặc `down --remove-orphans` cho `All`, không xóa volume. Các wrapper `.cmd` chỉ gọi lại script PowerShell.
`payment-service` là service duy nhất đọc `Services:ReservationService:BaseUrl` (khác `Services:Reservation` dùng bởi các service còn lại) và nhận `PayOs__*` từ `SMARTPARK_PAYOS_*`; giá trị PayOS rỗng giữ sandbox emulation (`PayOsAdapter`), nên local không cần key thật.

`scripts/run-app-service.ps1` nhận `-Service User|Facility|Operation|Payment|Notification|Frontend|All`, `-NoBuild`, `-NoLogWindow`. `Parking` và `Reservation` vẫn được nhận làm alias tương thích cho `Facility` và `Operation`; `scripts/stop-app-service.ps1` dùng cùng tên và alias. Script kiểm tra `.env`, `compose.app.yml`, network, chạy `up -d`, poll trạng thái tối đa 120 giây, in 100 dòng log và fail nếu container exit/unhealthy. Mỗi target mở một cửa sổ CMD `logs -f` trừ khi có `-NoLogWindow`. `scripts/stop-app-service.ps1` dừng target hoặc `down --remove-orphans` cho `All`, không xóa volume. Các wrapper `.cmd` chỉ gọi lại script PowerShell.

Lưu ý PowerShell 5.1: Docker ghi progress ra stderr; script dùng helper native-command để không biến stderr thành terminating error, và quyết định pass/fail bằng `$LASTEXITCODE`. Khi sửa wrapper, giữ behavior này và parse `docker compose ps --all --format json` cho cả array/NDJSON.

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
# Cloudinary configuration maintenance (SPARK-373)

The three optional `SMARTPARK_CLOUDINARY_*` fields are imported in Team mode, mapped in setup step 5 for all four .NET projects, and removed from user-secrets when cleared. They are never generated. The User-only `SMARTPARK_USER_MEDIA_ENABLED` switch defaults false and is deliberately not imported as a Team credential. The direct host runner reads parsed values to avoid inheriting removed credentials. Compose maps media fields only to four .NET backends and `ai-rest`/`ai-live`; preserve the latter's database environment during YAML merges.

Maintain the [environment map and rotation/removal instructions](docs/07-skills/SmartParking_Env_Config_SKILL_v1.0.md#cloudinary-development-media-settings-spark-373), the synthetic `scripts/test-setup.ps1` fixtures, and `scripts/test-media-compose.ps1` together. Do not print rendered credential-bearing configuration or write provider values to frontend settings. Database resets are not part of credential rotation.
