# Contract sau sửa các task đăng ký, auth, Vehicle và Parking

Ngày 08/10/2026. Đây là contract của code trong working tree, đối chiếu SRS v0.9 và các quyết định 06/10/2026. Không đổi trạng thái yêu cầu OPEN và không thay thế việc duyệt API Design. Các tài liệu API ngày 06/10 được giữ để truy vết; các phần khác với tài liệu này là contract cũ.

## Quyết định và mapping

| Phần | Contract thực thi |
| --- | --- |
| Driver registration | Email hoặc phone; nếu gửi cả hai thì OTP đăng ký gửi email. Chỉ contact đã xác minh được dùng cho login OTP. Password tùy chọn; nếu cung cấp thì theo policy password sẵn có. |
| Owner registration | Company name, email, phone bắt buộc. Xác minh email OTP → pending approval → Admin duyệt. Không có yêu cầu corporate domain, CCCD hoặc GPLX. |
| Contact | Email trim/lowercase; phone trim, `+84` và dạng `84` 11 chữ số chuyển thành `0`. Active contacts có uniqueness theo User DB; trùng sau chuẩn hóa khiến migration rollback, không gộp account. |
| OTP | 6 chữ số, hết hạn tại đúng mốc 5 phút, resend sau 60 giây; 3 lần sai → lock 15 phút. Resend không xóa số lần sai hoặc bypass lock; code cũ bị thay thế. OTP không tạo session khi mới issue/verify đăng ký. |
| Session | Access token 24 giờ, refresh 7 ngày; refresh rotation và logout current-session giữ contract đã có. Session khác độc lập. Current status, permissions và resource scope được kiểm tra lại trên protected requests. |
| MFA | TOTP 6 chữ số, 30 giây, chấp nhận cửa sổ ±1 step, chống replay; 3 lần sai → lock 15 phút. Login password bị từ chối khi MFA bật; dùng OTP login kèm TOTP. |
| Operator | Owner hiện có `OPERATOR_MANAGE`; chọn owned active lots và các quyền delegable. Mật khẩu do Owner cung cấp theo contract hiện có. Onboarding email chứa credential và link đổi password tùy chọn, token riêng SHA-256, single-use, hết hạn 24 giờ. Không bắt buộc đổi ở lần login đầu. |
| Admin approve | Current `ACCOUNT_ADMIN`; email đã verified, user pending approval và account pending approval. Audit actor/time/decision được lưu. Duyệt không unlock hoặc enable account bị block độc lập. |
| Delivery | Encrypted durable outbox trong User DB, `pending/failed/sent/cancelled`, retry độc lập. Không rollback account/approval đã thành công chỉ vì provider gửi thất bại. Payload được xóa khi sent/cancelled. SMTP là at-least-once: crash sau provider acceptance có thể gửi lại cùng thư; account/grants không được tạo lại. SMS nhận Idempotency-Key là delivery ID. |
| Vehicle | Dùng bảng `vehicles` canonical, FK `account_id`; map `rawPlate → original_plate`, `canonicalPlate → normalized_plate`, `imageReference → image_url`. Chỉ active Driver accounts của actor được đọc/sửa. API create/update yêu cầu image reference HTTPS; không fetch URL tại server. Historical IDs, account FK và soft delete được giữ. |
| VEH-OPEN | Gỡ global unique plate index của baseline vì nó áp đặt binding trong FR-VEH-02 còn OPEN; thay bằng non-unique lookup index. Trùng canonical plate không cấp quyền đọc/sửa record khác. Không thêm claim, evidence, transfer hoặc legal-ownership semantics. |
| Plate format | Canonical hóa uppercase và bỏ whitespace, `.`/`-`. Hiện hỗ trợ mẫu application 2-digit province + 1-letter car series hoặc 2-character motorcycle series + 5 digits. Đây chưa phải kiểm chứng toàn bộ biển số hợp pháp Việt Nam; tỉnh/series đặc biệt và biển lịch sử cần chốt trong API Design trước nghiệm thu format đầy đủ. |
| Capacity | Scope site/unit và category riêng. Hiển thị occupied, protected, active pending payment, configured/marked/effective backup và unavailable riêng. BACKUP-marked slot trùng configured pool đếm một lần; physical occupancy/unavailable không bị đếm lại như marked backup. Overlapping ancestor/descendant configured policies phải split rõ. Availability của child scope kế thừa backup chưa phân bổ trả `null`, không đưa ra số giả. |
| Operator BACKUP | `SLOT_OVERRIDE` là grant theo lot; chỉ mark slot operational, unoccupied, unprotected thuộc lot được giao, có reason và audit trong Parking transaction. Không đổi physical state. Không thêm cơ chế policy release chưa được chốt. |
| Owner BACKUP | `BACKUP_CONFIGURE` cấu hình số lượng; Owner không có `BACKUP_MARK` mặc định. Owner marking chỉ khi account có grant explicit. Operational Operator path không cấp Owner CRUD/layout quyền. |
| Capacity impact | Operation có actor/tenant, immutable request và idempotency key. Reservation impact tách active pending holds khỏi confirmed; priority là start time rồi confirmed_at thực tế. Historical confirmation không có bằng chứng vẫn null và operation pending. Không dùng created_at giả làm confirmation. |
| Reallocation | Gửi operation ID, edit, impact và priority/preference tới adapter có cấu hình. Chưa có adapter/reallocation/refund → pending, không sửa physical structure. Resolved vẫn phải qua durable fence và kiểm tra physical occupancy hiện tại. |
| Recovery | Physical commit marker lưu cùng transaction Parking. Concurrent retry cùng operation được serialize. Chưa nhận projection acknowledgement thì giữ fence và pending; không reapply physical edit. Recovery publish lại capacity/backup từ Parking thật trước khi release fence và finalize marker. |
| Structural audit | Actor/time/operation và before/after layout/backup được ghi trong cùng Parking transaction cho create/change. Operator marking còn có reason/action audit riêng. Audit không chứa credential hoặc OTP. |

Pool projection sang Reservation hiện hỗ trợ CAR/MOTORCYCLE theo schema của service này. Enum OVERSIZED cũ của Parking không được gửi thành một reservation category mới chưa có contract. Dynamic protection calculation, policy bounds/defaults và refund execution vẫn thuộc các service/features sở hữu tương ứng.

## API

Responses có `success`, thêm `data` khi có kết quả. Mutations không trả entity có thể chỉ trả `{success:true}`. Error response có HTTP status và `code/message`; không trả OTP, password hash, bootstrap token hoặc outbox plaintext. Anonymous auth endpoints dùng shared limiter 10 request/phút/IP/instance; cần shared limiter tại gateway nếu chạy nhiều replicas.

| Method / path | Body hoặc quyền | Kết quả |
| --- | --- | --- |
| POST `/api/auth/register/driver` | fullName, email/phone, optional password; unknown fields bị reject | 201 registrationId, channel, expiry/resend deadlines |
| POST `/api/auth/register/driver/verify` | registrationId, code | 200, account verified; chưa có session |
| POST `/api/auth/register/driver/resend` | registrationId | 200 deadlines; 429 cooldown, 423 lock |
| POST `/api/auth/register/driver/recover` | contact | 200 pending challenge metadata; không credential |
| POST `/api/auth/register/owner` | fullName, businessName, email, phone, lotType, agreedToPolicy, optional password | 201 application + verification (email) |
| POST `/api/auth/register/owner/verify` | challengeId, code | 200; sau email OTP hợp lệ chuyển pending approval |
| POST `/api/auth/register/owner/resend` | registrationId = email challengeId | 200 updated deadlines |
| POST `/api/auth/register/owner/recover` | contact | metadata và emailVerified/phoneVerified; FE resume sau duplicate/timeout |
| POST `/api/auth/otp/request` | contact đã verified hoặc provisioned Operator/Admin | 200 challengeId, expiresAt, resendAt, deliveryStatus |
| POST `/api/auth/otp/login` | challengeId, code, optional totp | 200 accessToken/refreshToken/expiresIn/user; 401 MFA_REQUIRED, 409 consumed, 423 locked |
| POST `/api/auth/mfa/setup` | current authenticated actor | secret/otpauth URI cho setup; không ghi log |
| POST `/api/auth/mfa/enable` | code, current authenticated actor | 200; replay/invalid code rejected |
| POST `/api/auth/bootstrap-password` | challengeId, token, password | 200 single-use, revoke all old sessions; invalid/expired 401 |
| GET `/api/owner-applications` và `/{id}` | current ACCOUNT_ADMIN | required company/contact, verification/decision/delivery status |
| PATCH `/api/owner-applications/{id}/review` | status approved, optional reviewNote | 200 audited decision; notification pending riêng |
| POST `/api/users` | Owner OPERATOR_MANAGE; fullName/email/password/siteIds/permissions | 201 Operator + deliveryId/status; no plaintext credential response |
| GET `/api/operators/me` | current Operator | active assigned lots và current grants |
| GET `/api/operators/slot-scope?siteId=...` | current Operator, explicit SLOT_OVERRIDE | actor/tenant scope; role token đơn lẻ không đủ |
| GET `/api/auth/deliveries/{id}`; POST `/{id}/retry` | original authorized actor, action permission | delivery status; retry same failed record |
| GET/POST `/api/vehicles`; PATCH `/{id}` | current Driver | own vehicle records; 201 create, 400 invalid plate/image/category, 404 foreign/missing record |
| PATCH `/api/parking-lots/{id}/structure` | Owner; action backup, unitId, vehicleType, capacity | 200; refreshed capacity views từ GET structure |
| GET `/api/parking-lots/{id}/operational-layout` | Operator SLOT_OVERRIDE tại lot | authoritative slots/capacity để chọn slot |
| POST `/api/parking-lots/{id}/slots/{slot}/backup` | Operator SLOT_OVERRIDE, reason | 200 audited BACKUP mark; occupied/protected/foreign/locked denied |
| POST `/api/parking-lots/{id}/operations` | Owner, idempotencyKey, edit | 200 completed; 202 pending, success false; 409 failed |
| GET `/api/parking-lots/{id}/operations/{operationId}` | original actor/tenant | authoritative workflow status |

Existing rejection endpoint is preserved for compatibility; this change does not define rejection/evidence/resubmission criteria. FE approval is disabled until contacts are verified.

Examples (placeholder IDs/codes, never fixture secrets):

```json
{ "contact": "driver@example.test" }
{ "challengeId": "<issued-challenge-id>", "code": "<delivered-six-digits>", "totp": "<authenticator-code-if-enabled>" }
{ "plate": "51a-123.45", "vehicleType": "CAR", "imageReference": "https://approved-image-store.example.test/vehicle.jpg" }
{ "idempotencyKey": "<stable-request-uuid>", "edit": { "action": "removeSlot", "resourceId": "<slot-id>" } }
```

## Migrations và cấu hình

Giữ baseline/overlay ở `arch/database-schema`. Ba SQL mới là **service-specific upgrades**, không phải SQL monolith cũ. Dừng API tương ứng và backup DB trước khi deploy. Script chỉ chọn đúng DB của service, không apply lại baseline CREATE TABLE:

```powershell
./scripts/apply-workflow-migrations.ps1 -Service User -SchemaRoot '<canonical-schema-checkout>'
./scripts/apply-workflow-migrations.ps1 -Service Parking -SchemaRoot '<canonical-schema-checkout>'
./scripts/apply-workflow-migrations.ps1 -Service Reservation -SchemaRoot '<canonical-schema-checkout>'
```

- User: `05.8-Account-Workflow-Vehicles.sql`: contact normalization, explicit action permissions, verified timestamps, challenges, encrypted outbox, MFA và vehicles mapping/index decision.
- Parking: `05.9-Parking-Backup-Operations.sql`: backup policies, pending operations/physical marker, slot audit.
- Reservation: `05.10-Reservation-Confirmation-Time.sql`: confirmed_at và trigger ghi actual transition time; không backfill lịch sử bằng created_at.

Active historical contacts không được tự đánh dấu verified. Legacy password login giữ tương thích; contact chưa có verification evidence không được dùng OTP login. Legacy vehicles vẫn giữ IDs/raw/canonical/image cũ; phải audit dữ liệu không phù hợp thay vì âm thầm viết lại lịch sử.

User configuration: `Workflow:KeyDirectory` là key ring bền vững, cùng application name trên các replicas. Windows dùng DPAPI theo service identity; Linux cần bảo vệ key directory và key-at-rest theo deployment. Giữ key ring khi restart/backup để đọc outbox và MFA. Production host yêu cầu key directory rõ ràng.

SMTP: `Otp:Smtp:Host/Port/Username/Password/From`; SMS HTTPS: `Otp:Sms:Url/ApiKey`; invitation origin HTTPS: `Onboarding:PublicOrigin`. Secrets đặt ngoài source. Không còn DevelopmentLogCodes/raw OTP logging. Khi chưa cấu hình provider, outbox có failed/retry outcome; không giả báo đã gửi.

Parking dùng `Services:Reservation`, `Services:Key`; optional HTTPS `Services:StructureReallocation` cần contract trả `{status:resolved|pending|failed,reason,impact}`. Không dùng adapter chưa triển khai để báo completed.

Recovery: `scripts/recover-structure-hold.ps1 -SiteId <id> -ParkingStopped -SchemaRoot <checkout>` inspect trước; `-Apply` chỉ sau khi mọi Parking instance đã dừng. Script kiểm tra không còn DB connections, publish physical/capacity/backup snapshot, release fence rồi finalize operations có physical marker. Không release mù với outcome null.

AI scaffold: xem [chatbot-scaffold](../../03-design/ai/chatbot-scaffold.md). Disabled mặc định, model/backend ports và read allowlist; không thêm chatbot business rules hoặc live writes.

## Owner email OTP correction (08/10/2026)

SRS §3.1.4 uses SMS or email; UC-AUTH-02 requires company/email/phone and Admin approval, not two OTPs. Owner onboarding currently uses email only; SMS fallback is not implemented in this flow. Phone remains required but unverified. OTP expiry remains 5 minutes per SRS/business rules. Recovery promotes legacy email-verified pending registrations and cancels their obsolete phone challenge/outbox; phone-only verification never qualifies for approval. Existing endpoints/DTO input fields remain unchanged; the unrelated remote design endpoint/field/expiry differences still require contract reconciliation.
