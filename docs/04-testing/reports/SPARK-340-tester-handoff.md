# SPARK-340 — Bản giao tester và công việc P5 song song

Branch kiểm thử: `feat/SPARK-340-admin-platform-policies`, repository `Quangthm/Mock-Project-Smart-Parking`. Đây là branch feature để kiểm thử, P5 vẫn CHANGES REQUESTED. Không merge vào develop hoặc dùng làm bản production đã nghiệm thu. P4 tạm đóng theo chỉ đạo user; các ca hoãn giữ nguyên trạng thái.

Initial published snapshot: `9a84aeaf197a5e770bb573108b20157b31354a00` (five feature/test/docs commits). Branch đã push thành công lên origin ngày17/11/2026. Các commit ghi nhận publication sau đó chỉ cập nhật tài liệu; tester vẫn ghi HEAD thực tế đã checkout vào report.

## Lấy bản và chuẩn bị môi trường

Trong checkout riêng của tester, đã giữ an toàn công việc local:

```powershell
git fetch origin
git switch --track origin/feat/SPARK-340-admin-platform-policies
git rev-parse HEAD
```

Nếu đã có local branch này, chuyển sang branch đó và `git pull --ff-only`; không reset/clean khi có thay đổi local. Ghi commit hash thực tế vào kết quả test. Không dùng HEAD055da459 làm bản feature: đó là baseline trước khi commit bộ thay đổi.

1. Dùng `.env.example` làm mẫu và cấu hình secrets riêng. Không copy `.env` của tác giả, token hay DataProtection keys qua Git. Đọc [guide dựng stack và lấy token](../postman/SPARK-340-Admin-Platform-Policies-guide.md) trước khi chạy Compose. Repo dùng .NET10, Node và Docker Compose theo guide.
2. Dựng `compose.schema-integration.yml`, sau đó `compose.app.yml` trên môi trường tester riêng. Không dùng database/volumes của production. Với RabbitMQ password có ký tự URI đặc biệt, điền biến percent-encoded tương ứng. Giữ volume Payment DataProtection để kiểm tra restart/recreate.
3. Mở FE `http://localhost:5173`. User5035, Facility5045, Operation5055, Payment5065, Notification5068. Dùng tài khoản Admin/Owner seed của môi trường và Driver có vehicle hợp lệ thuộc tài khoản đó.
4. Admin Dashboard → System Config là điểm vào policy/integration console. Theo [checklist U01–U20](SPARK-340-localhost-UAT-checklist.md).
5. Import collection `docs/04-testing/postman/SPARK-340-Admin-Platform-Policies.postman_collection.json` và environment example cùng thư mục. Tạo local environment ngoài Git hoặc dưới `.cache/`, lấy token thật từ UserService và fixture ID qua API. Không điền secret/token vào example trong repo.

Fresh database dùng schema declarations hiện có. Nếu DB tester cũ thiếu operational_policy_drafts, dừng và đối chiếu setup/schema với owner; không tự chạy migration suy đoán hoặc xóa volumes. File SQL của task không thay đổi. Nếu máy tester chưa có dedicated Facility signing configuration, xem guide/setup và ghi rõ dependency thay vì bật giả commercial producer.

## Tester thực hiện

- Ưu tiên U06 before/exact/after effectiveFrom (thời điểm, revision và giá trị), U11 composed invariant inherited minimum/site maximum, U20 desktop/tablet/mobile, rồi sign-off lại các case Admin/Owner/masking/concurrency đã chạy local.
- U04 phải tái hiện stale If-Match/expectedUpdatedAt409 RESOURCE_VERSION_CONFLICT. U07 overlap409 là ca khác.
- Newman full collection có tạo/activate platform revisions; phối hợp window trước khi chạy trên shared test stack. Scope này không yêu cầu gọi PayOS thật. Optional SANDBOX creds thiếu thì SKIP.
- Không nhập credential thật vào provider khi dùng fixture. Notification LOCAL_*_FIXTURE ACTIVE chỉ nói adapter fixture sẵn sàng, không chứng minh SMS/email ngoài hệ thống đã gửi.
- Restart/force-recreate Payment chỉ trên test stack và giữ DB/key volumes. Nếu cần thử trên stack user đang UAT, phối hợp lịch trước.

Các lệnh regression đã dùng:

```powershell
dotnet build SmartParking.slnx --nologo -m:1
dotnet test SmartParking.slnx --no-build --nologo -m:1
npm --prefix FE run build
node --test tests/frontend/*.test.cjs tests/postman/spark340-collection.test.cjs
```

Whole-solution có optional DB/broker suites tự skip. Bật đúng env theo guide để chạy PG Operation/Payment riêng, chỉ dùng database tạm do fixture tạo. Chạy Newman theo guide và xuất evidence đã sanitize.

## Known issues — không suy thành PASS

| Nội dung | Trạng thái và ảnh hưởng kiểm thử |
|---|---|
| U19 / SPARK-273 | Terms hiện503 OWNER_UNAVAILABLE vì commercial producer chưa tích hợp; sáu request snapshot downstream NOT RUN. Không yêu cầu tester sửa SPARK-273 hoặc đánh dấu booking snapshot PASS. |
| F6 — checkout cũ trước signing binding | P5 High còn mở. Callback không có binding bị từ chối. Cần owner duyệt backfill/drain/reconciliation trước rollout; fresh FIXTURE test không loại bỏ rủi ro này. |
| F7 — binding retention/storage | Chưa có disposition cuối cho việc dùng policy_revisions. Không prune/history-cleanup khi còn binding cần xác minh callback. |
| F8 — non-null platform arrival enablement | Input có thể được nhận rồi bỏ qua. FE hiện gửi null, enablement thuộc site. Cần kiểm tra contract negative case và ghi finding, không coi save200 là đã áp dụng true/false. |
| U06/U11/U20 và sign-off | Chưa được nghiệm thu từ nhận xét chung "localhost phần lớn ổn". Cần bằng chứng từng ca. |

[P5 report](SPARK-340-P5-review-20261117.md) là danh sách đầy đủ. Baseline Payment default auth và deposit-order findings đã chuyển đúng phạm vi owner; không mở rộng triển khai trong task SPARK-340.

## Công việc P5 có thể làm song song

Developer đối soát/sửa contract F8, phân loại low findings, và phối hợp Payment owner chốt F6/F7. Reviewer kiểm tra regression của từng bản sửa trên cùng branch; tester gắn bug với commit đã chạy. Các sửa ảnh hưởng DTO/status/rollout được cập nhật guide và thông báo trong handoff trước khi tester rerun. Không cần đợi kết thúc toàn bộ UAT mới bắt đầu review các thay đổi này.

## Mẫu trả kết quả

Ghi commit, ngày/giờ, môi trường, role, fixture IDs không nhạy cảm, bước tái hiện, expected/actual, HTTP status/code và ảnh đã che dữ liệu nhạy cảm. Với mỗi U01–U20 dùng PASS/FAIL/SKIP/NOT RUN/BLOCKED có lý do. Tách kết quả tự động và thao tác browser.

Known issues không phải miễn trừ mặc định. Tester gửi sign-off riêng khi hoàn tất phần chức năng trong phạm vi. Không lưu Authorization/Bearer, populated environment, password, provider keys hoặc key XML vào repo. Parent SPARK-340 và gate P5 vẫn mở cho đến khi các finding/acceptance có disposition rõ.
