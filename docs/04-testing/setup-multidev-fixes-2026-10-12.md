# Setup / multi-dev fixes — 12/10/2026

Repo: `D:\DataFPTU\Semester6\Mock-Project-Smart-Parking`, branch `develop-fix-application`. Phạm vi: các nhận xét setup/multi-dev trong ảnh leader. Clean UserService toàn bộ và tích hợp/push nhánh không được coi là hoàn tất bởi báo cáo này; Vehicle refactor thuộc đợt riêng.

## Thay đổi

- Credential mode `Local` mặc định: giữ cấu hình và dùng lại secrets khi có; `Team -TeamEnvPath <private-file>` nhập bộ credential đã được leader cung cấp, không hardcode giá trị dùng chung trong Git. Team yêu cầu đủ 3 credential chính và không sinh ngẫu nhiên thay chúng. Chặn xung đột Team/.env hoặc credential và URL tường minh giữa .env/user-secrets trước khi ghi file hoặc gọi DB.
- `-SkipUserSecrets` là lựa chọn rõ ràng để giữ cấu hình VS cũ và chỉ dùng runner .env. Setup không đổi password DB volume hoặc Admin đã seed. File Team chỉ nhập whitelist, giữ port và SMTP của từng dev. `load-env -NoExport` không đổi process env.
- Runner và setup hỗ trợ URL service xuyên máy; host/listener/firewall phải được cấu hình riêng. Không tự mở mạng hoặc bật API.
- Bỏ path cá nhân khỏi onboarding; viết lại QUICK_START và UPDATE_SCRIPT_GUIDE cho implementation thật, hướng dẫn backup script ignored cũ trước khi tích hợp tracked script.
- MapTiler nhập từ root .env hoặc Team vào FE/.env chỉ khi key FE trống. Key cũ được giữ. Sonar bật tường minh qua `-WithSonarQube` và compose riêng; cần password do dev/team cấu hình, không lấy secret từ ảnh.
- Migration luôn truyền EnvPath và project name tường minh; không phụ thuộc cwd hoặc COMPOSE_PROJECT_NAME. Giữ 05.11/05.12 và thứ tự migration theo service.

## Kiểm chứng

- Windows PowerShell 5.1 scratch suite: Local reuse, rerun idempotence, hai máy giả lập cùng Team baseline, remote URL mapping, MapTiler import, Team/secret/URL conflict preservation, SkipUserSecrets, NoExport no-mutation, invalid port không tạo .env, bảo vệ volume cũ không rõ password. CLI/user-secrets dùng doubles, không ghi user-secrets thật.
- `scripts/test-workflow-upgrade.ps1`: PostgreSQL thật trong Compose UUID riêng, baseline pre-05.11/05.12, sentinel ở cả ba DB, migration hai lần từ cwd khác với env tường minh, restart giữ volume và dữ liệu cũ. Rerun với COMPOSE_PROJECT_NAME=smartpark-schema vẫn chỉ tác động project UUID test. Log `.cache/review-results/setup-upgrade-final.log`. Containers/volumes test đã dọn.
- Solution: **109 passed, 0 failed, 0 skipped** (77 UserService + 32 FacilityService), với cả SMARTPARK_AUTH_TEST_CONNECTION và SMARTPARK_STRUCTURE_TEST_CONNECTION trên PostgreSQL tạm riêng. TRX `.cache/review-results/setup-fix-20261012-final/`. Container tạm đã dọn.
- Syntax scripts, Sonar Compose `config --quiet` và `git diff --check` pass.

## Review chéo

Hai subagent medium `review_layers_retry` và `review_transactions` review độc lập và kiểm tra chéo. Phát hiện P1 trong script test upgrade: COMPOSE_PROJECT_NAME có thể vượt YAML name và đổi target cleanup. Đã sửa bằng --project-name tường minh ở toàn bộ create/query/migrate/restart/cleanup và helper migration; cả hai reviewer xác nhận không còn finding actionable. Runtime adversarial rerun ở trên đã pass sau fix.

## Giới hạn và cách chạy

File Team thật và key MapTiler phải do nhóm cung cấp; không tự tạo baseline credential chung thay leader. Chưa chạy Sonar UI/provider SMTP/MapTiler thật, chưa kiểm chứng mạng trên nhiều máy hoặc dữ liệu deployment lịch sử khác fixture. Compose Sonar dựa trên [ví dụ chính thức SonarSource](https://github.com/SonarSource/docker-sonarqube/blob/master/example-compose-files/sq-with-postgres/docker-compose.yml); pin image version đã duyệt nếu cần baseline cố định.

```powershell
# Tại root checkout đúng
.\setup.ps1 -CredentialMode Team -TeamEnvPath '<file-env-riêng-của-team>'
# Thêm -SkipUserSecrets nếu chỉ chạy qua runner .env.
# Thêm -WithSonarQube khi SONAR_DB_PASSWORD đã được cấu hình.
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-setup.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-workflow-upgrade.ps1
```

Không chạy setup thật lên DB của nhóm trong lượt này. Không sửa file .env thật, không reset volume deployment và chưa commit/merge/push. Source trước fix được backup ở `.cache/setup-fix-before-20261012/`.
