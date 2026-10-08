# Fix và review toàn bộ task đã gửi

Phạm vi: Driver Register, Owner Register, Login/Logout, Vehicle Registration, AI chatbot structure, SPARK-188 Parking Structure, SPARK-192 Create Operator, SPARK-196 Admin Approve Owner. User yêu cầu sửa và review sau sửa toàn bộ hai nhóm screenshot. Changes ở working tree `develop`; không commit/push hoặc đổi Jira status.

Nguồn đối chiếu: SRS v0.9, Use Cases v0.9, companion BR/US với status được giữ nguyên, screenshot và quyết định 06/10/2026. [Contract/mapping hiện tại](../02-architecture/api/workflows-2026-10-08.md) ghi các quyết định triển khai và phần chưa được duyệt; không sửa SRS để miễn trừ code thiếu.

## Findings đã sửa

| Task | Sửa và bằng chứng |
| --- | --- |
| Driver Register | Password optional; verified contact timestamps, canonical contact/unique account, resume sau duplicate/timeout, queued delivery. Existing registration tests giữ expiry/3-failure-lock/resend/concurrency/injection cases. |
| Owner Register | Separate email/phone challenges và pending verification/approval. Ordinary email domain hợp lệ. Không có Owner access trước verification + approval. ContactMigration/OwnerOtp/OwnerRequiresBothContacts tests kiểm tra dữ liệu, lock, exact expiry và side effects. |
| Login/Logout | OTP login + optional replay-protected TOTP; password path không bypass MFA. Access 24h/refresh 7d. Existing rotation/revocation/current-state tests và frontend race/logout regressions giữ pass. |
| Vehicle | Backend own-account create/list/edit, required image reference, raw/canonical persistence, deterministic normalization, CAR/MOTORCYCLE. Dùng bảng vehicles sẵn có. Cross-account denied; FR-VEH-02 OPEN không biến thành binding/transfer policy. PostgreSQL reload + real-host HTTP kiểm chứng. |
| AI structure | Configuration/ports/coordinator/adapters, disabled default, read allowlist, write handoff. Scaffold tests không gọi model khi disabled, không cho model intent tự thực thi mutation. |
| Parking Structure | Separate backup configuration/mark/physical state, category/scope capacity views, overlap counted once, child bounds checked. Pending impact workflow, actual confirmed_at, serialized retry và marker chống reapply. PostgreSQL hai DB kiểm chứng rollback/safety/projection/concurrent operation, cùng SQL recovery snapshot có BACKUP. |
| Create Operator | Current OPERATOR_MANAGE/owned lots/delegable grants, consistent account/grants + encrypted onboarding outbox. Optional 24-hour single-use password link, password hash, revoked sessions, delivery retry không tạo thêm account/grant. SLOT_OVERRIDE được Owner cấp riêng theo baseline matrix. |
| Admin Approve | Current ACCOUNT_ADMIN, verified contacts/eligible pending state, serialized concurrent approval, actor/time/decision audit. Notification failure không rollback approval; retry cùng delivery. Locked/revoked JWT protected access denied. |
| Operator BACKUP | Current SLOT_OVERRIDE theo assigned lot, reason + Parking audit, giữ physical truth. HTTP integration kiểm tra deny trước grant, cho mark sau grant, foreign lot denied và old locked-token denied. |
| Frontend | OTP/password selection, Owner verification recovery, bootstrap password link, MFA setup, server vehicles, backend backup views, Operator marking và delivery retries. Removed Driver password-change success giả lập qua local storage. |

## Verification

Final results được ghi ở [test execution inventory](reports/cross-task-2026-10-08.csv). TRX gốc được giữ cục bộ trong `.cache/review-results/`.

| Check | Kết quả |
| --- | --- |
| .NET solution tests, PostgreSQL isolated | 91 pass (77 UserService + 14 ParkingService), 0 fail, 0 skip. |
| Frontend regression tests | 30 pass, 0 fail/skip. |
| FE production build | Pass; cảnh báo bundle >500 kB còn tồn tại. |
| TypeScript `tsc --noEmit` | Pass sau Operator UI. |
| PowerShell parse + recovery SQL thực thi trong PostgreSQL fixture | Parse pass; capacity/backup snapshot matches authoritative fixture. Migration runner chưa chạy trên DB dự án. |
| Diff check | Pass. |

Reproduce:

```powershell
$env:SMARTPARK_SCHEMA_ROOT = '<arch/database-schema-checkout>'
$env:SMARTPARK_AUTH_TEST_CONNECTION = '<isolated-postgres-connection-with-create/drop-db-rights>'
dotnet test SmartParking.slnx --no-restore --logger trx --results-directory .cache/review-results/reproduced
node --test tests/frontend/auth-api.test.cjs tests/frontend/auth-context.test.cjs
Set-Location FE
npm run build
```

Tests tạo/xóa DB UUID riêng. Container test do lượt review tạo, port 65433; DB dự án port 5433 không bị migrate hoặc sửa dữ liệu. Baseline SQL được đọc từ `arch/database-schema`, không copy vào canonical code branch. Fixtures decrypt delivery bằng test-only protector, không có anonymous API đọc OTP hoặc credential.

## QA handoff và giới hạn nghiệm thu

- Chạy browser walkthrough với SMTP/SMS thật: Driver register/verify → OTP/MFA → logout; Owner verify cả contact → Admin approve → lot → Operator invitation → optional password change → login. HTTP TestHost + PostgreSQL đạt không thay thế thao tác DOM, responsive hoặc provider thật.
- Chốt full Vietnamese plate province/series/historical/special formats và image-store policy qua API Design. Pattern application hiện tại đã có validation/test nhưng chưa đủ bằng chứng để nhận toàn bộ format pháp lý Việt Nam.
- Chốt Reservation/Payment reallocation/refund adapter, dynamic protection, backup release và policy bounds. Chưa có downstream implementation thì operation pending, physical occupancy/history và accepted commitment được giữ; không nghiệm thu refund completed.
- SMTP/SMS production, durable shared key ring và deployment migrations cần được cấu hình/triển khai. SMTP retry có thể gửi lại thư sau crash; không tạo lại account hoặc grants.
- Không thêm rejection evidence/resubmission, Operator self-register, CCCD/GPLX, plate-ownership transfer hoặc permanent non-plated vehicle identity. Legacy rejection endpoint được giữ, không được coi là đã chốt criteria.
- PR/reviewer approval, CI trên remote và Jira evidence links chưa có vì lượt này chỉ sửa local. Không đánh dấu tất cả task Done dựa riêng vào test pass.

Review sau sửa đã đọc các diff/flow và dùng ca negative để kiểm tra không có forbidden side effects; các giới hạn ở trên vẫn được ghi rõ, không bị đổi thành success hoặc silently out-of-scope.
