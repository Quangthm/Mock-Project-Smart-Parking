# API handoff cho tester — các task của tác giả

> Cập nhật môi trường local ngày 08/10/2026; xem [review sửa email OTP](owner-email-otp-fix-review-2026-10-08.md):
> SMTP đã gửi thành công, Services:Key đã được cấu hình tại local. Owner onboarding đã sửa dùng email OTP; không cần SMS config cho luồng Owner.
> đây chưa phải kết quả retest môi trường QA bên ngoài.

Ngày 08/10/2026. Contract đối chiếu trực tiếp với controller/DTO trên `develop` tại thời điểm bàn giao. Khi phát hành, ghi code commit, schema commit và URL môi trường vào phiếu kết quả test.

Phạm vi: Driver Register, Owner Register, Login/Logout, Vehicle Registration, Parking Structure (SPARK-188), Owner Create Operator (SPARK-192), Admin Approve Owner (SPARK-196), AI chatbot **project structure**. Reservation/Payment/IoT và các task của thành viên khác chỉ là dependency; không đưa toàn bộ acceptance của chúng vào nghiệm thu phần này.

## 1. Môi trường và dữ liệu

Import [Postman collection](postman/assigned-workflows-2026-10-08.postman_collection.json). Collection phục vụ chạy từng request theo thứ tự; không chạy cả collection bằng Runner vì OTP, approval, password-link và TOTP cần bước tương tác.

| Biến | Giá trị / cách lấy |
| --- | --- |
| userUrl | `http://localhost:5035` hoặc UserService QA URL |
| parkingUrl | `http://localhost:5045` hoặc ParkingService QA URL |
| driverEmail, ownerEmail, operatorEmail | Ba inbox QA khác nhau, mỗi lần chạy dùng bộ contact mới |
| ownerPhone | Số điện thoại hồ sơ Owner; bắt buộc khác dữ liệu hiện có, không cần nhận SMS trong luồng email-only |
| password | Mật khẩu mẫu phục vụ test, 8–15 ký tự, đủ hoa/thường/số/ký tự đặc biệt |
| driverToken, ownerToken, adminToken, operatorToken | `data.accessToken` của session tương ứng; không dùng chung role |
| registrationId | `data.registrationId` của Driver register |
| ownerApplicationId | `data.id` của Owner register |
| ownerEmailChallenge | `data.verification.challengeId` |
| challengeId, otp, totp | Challenge login hiện hành; code nhận từ inbox/SMS QA và authenticator |
| siteId, unitId, slotId, vehicleId, operationId | ID thực tế trả về sau các request tạo; collection tự lưu các ID phổ biến |
| tenantId | Có thể bỏ field khi Owner chỉ có một tenant; nếu nhiều tenant phải chọn tenant được cấp quyền |

Collection khởi tạo email tại `example.test`, token/OTP để trống. Thay bằng inbox QA được quản lý trước khi test OTP. Không lấy OTP từ log, DB hash hoặc thêm endpoint debug. Admin QA được người phụ trách môi trường cấp; chưa có public Admin self-registration API.

Protected API: `Authorization: Bearer <accessToken>`. JSON body: `Content-Type: application/json`. Không cần `X-Service-Key` cho các public API này. UUID lấy từ response, không dùng UUID giả. Không commit/export giá trị token, OTP, secret MFA, password-link hoặc mật khẩu QA thật.

Chuẩn bị database: baseline + integration overlays ở `arch/database-schema`, sau đó các upgrade User `05.8`, Parking `05.9`, Reservation `05.10`. Deployment operator dùng [migration script](../../scripts/apply-workflow-migrations.ps1) với đúng `-Service` và `-SchemaRoot`. Không chạy lại baseline CREATE TABLE trên DB đã có dữ liệu. Lượt sửa này **chưa migrate DB dự án**.

Local: sau khi restore/build và chuẩn bị DB/config, đặt `SMARTPARK_DEV_PASSWORD`, `SMARTPARK_SERVICE_KEY` (ít nhất 32 ký tự) trong từng terminal, chạy `./scripts/run-schema-service.ps1 -Service User`, `Parking`, `Reservation`. SMTP/SMS QA, HTTPS `Onboarding:PublicOrigin`, key ring bền vững phải được người quản trị cấu hình. Password và service key không đưa vào tài liệu công khai.

## 2. Cách đọc response

Success thường `{ "success": true, "data": ... }`; mutation không trả entity có thể chỉ có `success`/`message`. Lỗi nghiệp vụ thường có `success:false, code, message`; lỗi model binding có thể là ASP.NET validation problem, nên không assert mọi lỗi cùng một envelope.

`201` tạo thành công; `200` đọc/sửa/verify; `400` input không hợp lệ; `401` session/credential không hợp lệ; `403` không đủ quyền hoặc account gate; `404` resource không có/không được truy cập theo contract; `409` conflict; `423` challenge lock; `429` resend cooldown hoặc limiter. Password login account lock dùng **403**, không phải 423. Khi test lỗi ghi cả HTTP status, `code` nếu có và tác dụng phụ trong DB/GET.

Parking operation: `200 + data.status=completed`, `202 + success=false + data.status=pending`, `409 + data.status=failed`. `202` không có nghĩa physical edit đã hoàn tất. GET operation `success:true` chỉ nghĩa đọc thành công; phải kiểm tra `data.status`.

## 3. Driver đăng ký → login → session

1. `POST {{userUrl}}/api/auth/register/driver` → **201**:

```json
{"fullName":"QA Driver","email":"<driver-inbox>","password":"Test@1234"}
```

Có thể thay email bằng `phone`; password tùy chọn. Nếu gửi cả hai contact thì registration OTP gửi email và chỉ contact đó được verified. Lưu `registrationId`, `channel`, `expiresAt`, `resendAvailableAt`; chưa có access token.

2. `POST /api/auth/register/driver/verify` → **200**:

```json
{"registrationId":"<registrationId>","code":"<six-digits-from-QA-inbox>"}
```

3. `POST /api/auth/otp/request` với `{"contact":"<verified-driver-contact>"}` → **200**, lưu `data.challengeId`. `POST /api/auth/otp/login` → **200**:

```json
{"challengeId":"<login-challenge>","code":"<new-login-OTP>"}
```

Lưu `data.accessToken`, `data.refreshToken`; `expiresIn=86400`. Refresh token có tuổi thọ 7 ngày theo contract hiện hành. Password login là `POST /api/auth/login` với `{"email":"<driver-email>","password":"Test@1234"}` khi đã đặt password và chưa bật MFA.

4. `GET /api/auth/me` với Driver token → **200**, đúng user/role. `POST /api/auth/refresh` body `{"refreshToken":"<current-refresh>"}` → **200**, lưu cặp token mới; refresh cũ không được dùng lại.
5. `POST /api/auth/logout` với token hiện hành, body `{"refreshToken":"<current-refresh>"}` → **200**. Token đã logout gọi `/me` và refresh đã logout đều bị từ chối. Session độc lập khác vẫn theo phạm vi logout current-session.

Phục hồi đăng ký: `POST /api/auth/register/driver/recover` body `{"contact":"<pending-contact>"}` → metadata; resend `POST /api/auth/register/driver/resend` body `{"registrationId":"<id>"}`. Không tạo account mới khi timeout/duplicate.

MFA optional: authenticated `POST /api/auth/mfa/setup` → secret + otpauth URI; thêm vào authenticator, `POST /api/auth/mfa/enable` body `{"code":"<totp>"}` → **200**. Sau đó OTP login phải gửi thêm `totp`; password login → **401 MFA_REQUIRED**. Không đính secret MFA vào bug report.

## 4. Owner đăng ký → xác minh email → Admin duyệt

`POST /api/auth/register/owner` → **201**:

```json
{"fullName":"QA Owner","businessName":"QA Parking","email":"<owner-inbox>","phone":"<QA-phone>","lotType":"outdoor","agreedToPolicy":true,"password":"Test@1234"}
```

`lotType`: `outdoor`, `basement`, `multi-storey`. Public-domain email hợp lệ được chấp nhận; không có field CCCD/GPLX. Lưu application ID và email challenge ID (data.verification.challengeId). Với email challenge, gọi `POST /api/auth/register/owner/verify` body `{"challengeId":"<selected-contact-challenge>","code":"<corresponding-code>"}` → **200**.

Email OTP hợp lệ là đủ điều kiện Admin approval; không có bước SMS OTP. Sau email verification, Owner pending approval vẫn chưa có Owner operational access. Recovery `POST /api/auth/register/owner/recover` body `{"contact":"<email-or-phone>"}` trả metadata và trạng thái contact. Resend `POST /api/auth/register/owner/resend` **field tên `registrationId` nhưng value là contact challenge ID**, không phải application ID.

Admin token có current `ACCOUNT_ADMIN`:

- `GET /api/owner-applications` và `GET /api/owner-applications/<applicationId>` → **200**, company/contact/verification đúng.
- `PATCH /api/owner-applications/<applicationId>/review` body `{"status":"approved","reviewNote":"QA confirmed email"}` → **200**, audit reviewer/time/note, approval được lưu; delivery có trạng thái riêng.
- Owner login lại bằng OTP/password, dùng Owner token kiểm tra Owner operations. Approval không unlock/enable account bị block độc lập.

Endpoint cũ nhận `rejected` để tương thích; evidence/rejection/resubmission policy chưa được chốt, không tự thêm thành acceptance bắt buộc của task này.

## 5. Parking Structure (Owner)

Owner đã approved, active và có quyền hiện hành. Dùng `parkingUrl`, Owner token.

| Request | Body ví dụ | Expect |
| --- | --- | --- |
| GET `/api/parking-lots` | không body; optional `?tenantId=<authorized>` | 200, chỉ owned lots |
| POST `/api/parking-lots` | `{"code":"QA-LOT-01","name":"QA lot","address":"QA address"}` | 201; lưu `data.id` → siteId |
| GET `/api/parking-lots/<siteId>/structure` | không body | 200 authoritative units/slots/paths/backup/capacity |
| POST `/api/parking-lots/<siteId>/units` | `{"type":"ZONE","name":"Zone A","capacity":10}` | 200; `data` UUID → unitId |
| POST `/api/parking-lots/<siteId>/slots` | `{"unitId":"<unitId>","code":"A-01","vehicleType":"CAR","type":"STANDARD"}` | 200; `data` UUID → slotId |
| POST `/api/parking-lots/<siteId>/paths` | `{"code":"ENTRY-A","to":"<unitId>"}` | 200 UUID |
| PATCH `/api/parking-lots/<siteId>/structure` | `{"action":"renameUnit","resourceId":"<unitId>","name":"Zone B"}` | 200; GET phản ánh tên mới |
| PATCH cùng path | `{"action":"backup","unitId":"<unitId>","vehicleType":"CAR","capacity":1}` | 200 với BACKUP_CONFIGURE; GET capacity thay đổi |

Unit types `FLOOR/ZONE/BLOCK`. Outdoor không bắt buộc FLOOR. Slot types `STANDARD/VIP/EV/DISABLED`. Chỉ nghiệm thu pool projection CAR/MOTORCYCLE theo Reservation contract hiện hành.

Các action PATCH khác: `profile` (code/name/address/latitude/longitude), `active` (active), `capacity` (resourceId/capacity), `removeUnit/removeSlot/removePath` (resourceId), `moveSlot` (resourceId/unitId/code), `configureSlot` (resourceId/vehicleType/slotType/coordinates3D/features), `markBackup` (resourceId/active, **explicit BACKUP_MARK**). Owner không có BACKUP_MARK mặc định. Action có thể phá active commitments phải đi qua operation workflow; direct mutation không được bypass safety gate.

Thử workflow trên slot fixture không có occupancy/commitments:

```http
POST /api/parking-lots/<siteId>/operations
Authorization: Bearer <ownerToken>
Content-Type: application/json
```

```json
{"idempotencyKey":"<stable-UUID-for-this-edit>","edit":{"action":"removeSlot","resourceId":"<slotId>"}}
```

Lưu `data.id`, đọc `GET /api/parking-lots/<siteId>/operations/<operationId>`. Retry giữ nguyên key **và edit**. Không đổi resource với key đã dùng. Completed → structure đúng, audit đúng, không apply lần hai. Nếu planner/downstream unavailable, pending/failed rõ ràng; không báo completed giả, không xóa occupied vehicle/history. Reallocation/refund thực tế cần fixture hoặc service của thành viên sở hữu dependency.

Capacity assert từng category/scope: `total, occupied, protected, pendingPayment, configuredBackup, markedBackup, effectiveBackup, unavailable, available`. Backup configured/marked overlap không trừ hai lần. Slot physically occupied/unavailable không bị trừ lại như eligible backup. `inheritedBackup:true` ở child chưa phân bổ thì `available:null` là contract đúng. Pending payment chưa phải confirmed reservation.

## 6. Operator provision → optional đổi password → BACKUP

Owner có current `OPERATOR_MANAGE`; siteId phải owned và active. `POST {{userUrl}}/api/users` → **201**:

```json
{"fullName":"QA Operator","email":"<operator-inbox>","password":"Test@1234","siteIds":["<siteId>"],"permissions":["SLOT_OVERRIDE"]}
```

Permissions cho phép: `DEVICE_MANAGE`, `DEVICE_STATUS_VIEW`, `CASH_COLLECT`, `APPEAL_REVIEW`, `SLOT_OVERRIDE`; 1–5 distinct permissions; 1–100 distinct sites. Response không chứa plaintext credential; lưu operator ID, `deliveryId`, `deliveryStatus`. Email gửi credential và link đổi password tùy chọn.

Có thể login với password Owner đã cấp nếu chưa đổi. Để đổi, lấy challenge/token từ link email rồi `POST /api/auth/bootstrap-password` body `{"challengeId":"<from-link>","token":"<from-link>","password":"New@12345"}` → **200**; link single-use, 24 giờ. Old password và old sessions bị từ chối sau thành công. Không bắt buộc đổi ở first login.

Operator token:

- `GET {{userUrl}}/api/operators/me` → assignments/grants hiện hành.
- `GET {{parkingUrl}}/api/parking-lots/<siteId>/operational-layout` → **200**, chỉ assigned lot với SLOT_OVERRIDE.
- `POST {{parkingUrl}}/api/parking-lots/<siteId>/slots/<slotId>/backup` body `{"reason":"QA operational backup"}` → **200**; slot cần operational/unoccupied/unprotected, audit actor/reason/time; physical state giữ nguyên.
- `GET {{userUrl}}/api/operators/slot-scope?siteId=<siteId>` trả trực tiếp `userId,tenantIds`, không dùng envelope chung.

Tạo Operator với permissions khác để kiểm tra thiếu SLOT_OVERRIDE bị deny. Sau revoke quyền/disable actor, JWT cũ không giữ được quyền. Later access modification là task follow-up, không thêm public API vào test plan này. Operator unmark/release policy chưa chốt.

Owner/Admin đã thực hiện action: `GET /api/auth/deliveries/<deliveryId>` và `POST /api/auth/deliveries/<deliveryId>/retry`; retry failed record, không tạo account/grants/approval lần hai. Actor khác không được xem/retry. QA provider failure có kiểm soát: action vẫn lưu thành công và delivery failed/retryable.

## 7. Vehicle (Driver)

Driver token: `GET {{userUrl}}/api/vehicles` → **200**, own active records. `POST /api/vehicles` → **201**:

```json
{"plate":"51a-123.45","vehicleType":"CAR","imageReference":"https://qa-image-store.example.test/vehicle.jpg"}
```

Lưu `data.id`. Expect `rawPlate="51a-123.45"`, `canonicalPlate="51A12345"`, account ownership FK đúng. `PATCH /api/vehicles/<vehicleId>` dùng **đủ ba field** như create → **200**. Motorcycle mẫu `59X1-123.45` → `59X112345`. Không có Vehicle single-GET/DELETE API trong scope hiện hành.

Biến thể space/case/dot/hyphen cho cùng canonical. Blank/invalid plate, thiếu image, image HTTP, category non-plated → **400**. Driver B PATCH vehicle Driver A → **404**; list B không chứa record A. Duplicate canonical không tự cấp quyền/claim/transfer; FR-VEH-02 vẫn OPEN. Format hiện tại là application MVP (province 2 digits, car 1-letter hoặc motorcycle 2-character series, 5 digits); chưa nghiệm thu mọi biển lịch sử/đặc biệt hợp pháp Việt Nam.

## 8. Ma trận âm và boundary bắt buộc

Mỗi dòng dùng fixture độc lập để không khóa actor của happy path. Ghi response, ID và xác minh không có tác dụng phụ trái phép.

| ID | Case | Assert |
| --- | --- | --- |
| AUTH-01 | Missing/malformed contact; injected role/status/permissions | Reject; không account hoặc privilege grant trái phép |
| AUTH-02 | Email case/space; +84/0 equivalent phone; simultaneous registration | Uniqueness sau normalize; tối đa một account/contact |
| AUTH-03 | Wrong/expired/old-resend OTP, purpose/account khác; consumed challenge | Deny; không session/verification trái phép |
| AUTH-04 | Trước/tại/sau 5 phút expiry; resend trước/tại 60s | Đúng boundary; 429 cooldown, không reset attempts |
| AUTH-05 | Sai lần 1/2/3/4; trước/tại/sau 15 phút lock | 3 sai khóa; retry/resend không bypass lock |
| AUTH-06 | Pending/unverified/disabled/locked; revoke sau cấp JWT | Protected operation deny với current state |
| AUTH-07 | Refresh replay; logout rồi dùng JWT/refresh; independent session | Revocation/rotation và current-session scope đúng |
| AUTH-08 | MFA missing/wrong/replayed; password route sau enable | MFA_REQUIRED/deny; không bypass, không leak secret |
| OWN-01 | Chỉ verify một contact, approve chưa eligible | Deny approval/operational access |
| OWN-02 | Public domain; thiếu company/contact; injected approval | Domain được nhận; invalid/injected input bị reject |
| OWN-03 | Hai Admin approve concurrent; retry; racing lock/disable | Một logical decision, audit; không unlock độc lập |
| OPR-01 | Foreign site, undelegable grant, duplicate email/concurrent creation | Deny/controlled conflict; không partial grant hoặc duplicate |
| OPR-02 | Link expired/tampered/reused/wrong account; optional no-change | Deny link sai; no-change login vẫn đúng contract |
| OPR-03 | Email failure/retry; different actor delivery access | Có delivery state; không tạo Operator lại; scope deny |
| VEH-01 | Normalize/boundary/image/category/cross-account | Canonical deterministic; raw persisted; reject/404 đúng |
| PARK-01 | Indoor/outdoor; parent cycle/foreign parent/scoped duplicate | Valid hierarchy; invalid edit không commit |
| PARK-02 | Negative/fraction/overflow capacity, BACKUP > total, overlap | Reject; không silent negative hoặc double subtraction |
| PARK-03 | Occupied/protected slot; pending hold; remove parent with children | Physical/history preserved; explicit outcome |
| PARK-04 | Other Owner; disabled/unapproved; Operator missing grant | Deny với JWT còn thời hạn; không mutation |
| PARK-05 | Same operation retry/concurrent; same key different edit | Serialize/idempotent; không reapply; conflict đúng |
| PARK-06 | Planner down/ambiguous acknowledgement sau physical commit | Pending/failed rõ; fence bảo vệ; không completed giả |

HTTP tests cho exact expiry/concurrency dùng test clock/fixture harness; không sửa thời gian máy hoặc dữ liệu production. Anonymous registration/OTP shared limiter 10 requests/min/IP/instance có thể trả 429, tách khỏi resend cooldown. Retry delay đúng response.

## 9. AI scaffold và kết quả bàn giao

AI task chỉ setup cấu trúc: application ports/context/options, infrastructure adapters, read allowlist FAQ/PARKING_SEARCH, disabled mặc định, unit tests. **Chưa có live chat public endpoint**; không gửi `/chat` và ghi bug thiếu business chatbot. Xem [scaffold contract](../03-design/ai/chatbot-scaffold.md).

Evidence lượt sửa: [review report](cross-task-fix-review-2026-10-08.md), [91 backend test results](reports/cross-task-2026-10-08.csv), 30 frontend regression tests và FE build đã pass tại lượt review. Đây là evidence developer, chưa thay thế tester UAT/provider test và remote CI. PostgreSQL tests chạy DB cô lập; không migrate DB dự án.

Phiếu mỗi case: `caseId | code SHA | schema SHA | environment | role/fixture IDs | method/path | redacted request | HTTP + redacted response | GET/DB outcome | expected | actual | Pass/Fail/Blocked | bug/dependency link`. Dùng **Blocked/dependency** khi chưa có inbox/SMS, Admin fixture, commitment fixture hoặc reallocation/refund adapter; vẫn test failure/pending semantics thuộc scope này. Không đánh Done task của thành viên khác.

Tester sign-off chỉ sau API Design chốt phần plate-format đang giới hạn và các applicable cases của task tác giả pass; các dependency chưa sẵn sàng phải ghi rõ case/evidence, không thay đổi spec để che thiếu implementation.

Owner legacy recovery: registrations stuck after email verification can recover using email/phone; backend moves them to pending approval and cancels obsolete SMS challenges/deliveries. Phone verification remains unset. Registration without email verification must still complete email OTP. QA does not require SMS config for this Owner flow.
