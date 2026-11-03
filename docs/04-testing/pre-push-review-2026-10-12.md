# Pre-push review cho tester — 12/10/2026

Repo `D:\DataFPTU\Semester6\Mock-Project-Smart-Parking`, branch `develop-fix-application`. Base HEAD `70348b17668a46f9de67c1c1e39cdc7d0d705c4a` (`chore(dev): restore local setup and document service APIs`). Review toàn bộ working-tree diff so với HEAD và 10 file mới chưa tracked tại thời điểm review, không chỉ riêng một lượt fix.

## Kết luận

Sẵn sàng commit/push để tester kiểm thử local theo tài liệu hiện tại. Ba reviewer reasoning medium (`review_layers_retry`, `review_transactions`, `review_tester_readiness`) review độc lập và kiểm tra chéo. Không còn finding actionable.

Finding tài liệu được sửa trong lượt này: dòng OWN-01 cũ yêu cầu verify hai contact trái với code Owner email-only. Checklist mới phân biệt email chưa verify/application không eligible (deny) và email verified, phone chưa verified nhưng mọi điều kiện khác hợp lệ (Admin có thể approve). Trước approval vẫn deny operational access. Cả ba reviewer đối chiếu lại với OwnerRegistrationService.ReviewAsync và xác nhận fix.

Vehicle API/validation/error/ownership/filter/locks/transaction và test assertions giữ hành vi so với HEAD. Setup Local/Team, credential conflicts, runner mapping, EnvPath/project pin, Sonar opt-in và UUID test cleanup đã được rà soát. Tài liệu bàn giao có thay đổi từ trước cũng được đưa vào phạm vi push review.

## Bằng chứng

- TRX solution cuối: `.cache/review-results/setup-fix-20261012-final/`: 109 passed, 0 failed, 0 skipped (77 UserService + 32 FacilityService).
- Không có executable delta sau lượt 109 tests; lượt review này chỉ chỉnh checklist tester. Không chạy lại solution khi không có thay đổi code mới.
- Scratch setup suite chạy lại trên Windows PowerShell 5.1 pass trong lượt pre-push.
- `git diff --check` pass; repeated-upgrade/old-volume restart và adversarial COMPOSE_PROJECT_NAME đã pass trong `.cache/review-results/setup-upgrade-final.log`.

## Bàn giao

Commit đủ các file mới: IVehicleRepository, IVehicleRegistration, Application VehicleRegistrationService, Persistence VehicleRepository, UnsupportedVehicleRepository, compose.sonar.yml, hai script test và các báo cáo bàn giao. Chỉ commit tracked diff mà bỏ các file mới sẽ làm package không đầy đủ. Báo cáo pre-push này là file tài liệu bổ sung sau review.

Tester lấy credential Team từ leader qua file riêng nếu cần baseline chung; không commit/export .env hoặc credential thật. Với env tự viết, giữ đủ các port key theo .env.example. Sonar UI, SMTP/MapTiler thật, mạng nhiều máy và full UAT chưa được xác nhận bởi automated/local review; tester cần ghi environment/code/schema commit và kết quả thực tế.

Lượt này chưa stage/commit/push/merge và không thao tác DB deployment. Ready cho QA local không có nghĩa toàn bộ UserService đã clean hoặc đủ điều kiện production.
