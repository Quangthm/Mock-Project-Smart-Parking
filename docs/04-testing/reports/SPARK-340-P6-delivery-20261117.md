# SPARK-340 — P6 verification và chuẩn bị delivery

> Current18/11/2026: P5 code review closed per [closure record](SPARK-340-P5-closure-20261118.md). User requests branch publication and full tester verification before any PR. Earlier phase/gate values below are historical; use [pre-PR test prompt](SPARK-340-pre-PR-full-test-prompt-20261118.md) for the next run. No PR created and no shared DB rollout authorized by this update.

Ngày 17/11/2026. HEAD `055da4591ee9a1822eb9870dc261c9685ca3258b`, branch `feat/SPARK-340-admin-platform-policies`.

> Delivery follow-up: user subsequently authorized publishing the feature branch for tester access while P5 continues. Five task-scoped commits were pushed successfully, initial published tip9a84aeaf. [Tester handoff](SPARK-340-tester-handoff.md) defines the scope and known issues. Verification below predates that Git delivery; [publication evidence](evidence/SPARK-340-tester-publication-20261117.json) records the actual commits.

## Trạng thái phase

| Phase | Trạng thái thực tế |
|---|---|
| P4 | **Tạm đóng theo chỉ đạo user**, giữ nguyên FAIL/BLOCKED/NOT RUN và các việc hoãn; không phải tất cả acceptance đã PASS. |
| P5 | Review đã thực hiện, report đã giao; **CHANGES REQUESTED**, gate0 Critical/High chưa đạt vì F6 trong nhánh. |
| P6.1 | Verification suite theo planning đã chạy lại. Kết quả bên dưới. |
| P6.2 | Session, review, evidence và PR draft đã chuẩn bị; feature branch đã commit/push cho tester theo chỉ đạo bổ sung. **Chưa ready-to-merge**, chưa mở PR trên GitHub. |

Session vẫn ở active vì parent task, findings và tester sign-off chưa hoàn tất. Tạm đóng một phase không làm parent story Done.

## Task 6.1 — Kết quả chạy mới trong lượt P5/P6

| Lệnh | Kết quả |
|---|---|
| `dotnet build SmartParking.slnx --nologo -m:1` | exit0, **0 errors / 56 warnings**, 17.26s. Warnings NU1902/NU1903 là dependency advisories; không đổi packages ngoài phạm vi. |
| `dotnet test SmartParking.slnx --no-build --nologo -m:1` | exit0, **1161 passed / 0 failed / 109 skipped / total1270**. Chạy ngoài sandbox cho testhost. |
| `npm --prefix FE run build` | exit0, build thành công. Còn chunk-size và ineffective dynamic import warnings. |
| `node --test --test-reporter=dot tests/frontend/*.test.cjs tests/postman/spark340-collection.test.cjs` | exit0, **263 tests passed**, gồm261 FE và2 regression collection SPARK-340. |
| Targeted secret/JWT/PEM scan trên delta và file nonignored mới |128 files tại checkpoint, không có flag. Giới hạn scanner và file hashes ở source-audit evidence. |
| Schema freeze inspection | Không có migration/SQL file thay đổi trong inventory tại checkpoint. |

BE per-project: Architecture12/0skip; Facility199/29skip; IoT1/0skip; Notification220/0skip; Operation66/3skip; Payment311/7skip; User352/70skip. Không tính skipped thành pass. PG/broker suites thiếu opt-in env trong whole-solution run vẫn skip đúng hành vi.

Probe mở rộng ngoài phạm vi bằng `node --test tests/frontend/*.test.cjs tests/postman/*.test.cjs` có284 pass/1fail trên285 tests. Fail nằm ở baseline `tests/postman/payment-webhook.test.cjs`, kiểm tra generic PaymentService environment example phải có checksum trống. File environment đó không thuộc delta SPARK-340; không sửa hoặc in lại giá trị. Theo chỉ đạo giới hạn phạm vi, ghi riêng observation này, không gộp vào regression result SPARK-340. Sau đó đã chạy đúng scoped command ở bảng trên và pass. Không tuyên bố toàn bộ tests/postman wildcard xanh.

## Bằng chứng trước đó được tham chiếu, không nhận là run mới

- Independent round2 ngày17/11: PG Operation3/3, PG Payment4/4.
- Newman54 defined,48 executed,6 NOT RUN;76 assertions pass/1fail/1skip. Fail duy nhất terms503, skip optional SANDBOX credentials.
- SITE_OVERRIDE20 đã chứng minh qua Owner path. Payment restart/force-recreate giữ config/key volume trong test window được user cấp.
- Fresh-context binding proof không chứng minh hai backend PID khác nhau. Giữ giới hạn của report independent, không nâng thành độc lập connection tuyệt đối.

P6 không chạy lại mutation Newman hoặc restart service trong lượt này. Không thay đổi policy/runtime người dùng đang UAT.

## Task 6.2 — Git và PR preparation

- PR title và body theo template được chuẩn bị tại [PR draft](SPARK-340-PR-draft.md), target đề xuất `develop`.
- Tại checkpoint chuẩn bị, `develop...HEAD` local là0/0 và tính năng chưa commit. Sau chỉ đạo giao tester, đã tạo năm task-scoped commits và push feature branch, initial tip9a84aeaf. Không push vào develop/main.
- Remote freshness chưa xác minh. Không fetch/rebase/merge để tránh thay đổi checkout có nhiều uncommitted work.
- Không tự chạy git add -A. Inventory audit chứa cả thay đổi ngoài nhóm task, ví dụ collection SPARK-238 và một số test bảo trì. Cần đối soát nguồn thay đổi trước khi stage từng path.
- Không đưa `.env`, `.cache`, populated environments, raw Newman exports hoặc DataProtection key material vào commit. FE dist là output build, không tự stage.
- Planning ngoài repo tại `D:\My file\FA26 - OJT FSoft\Planning\SPARK-340-planning.md` được dùng read-only. Bảng phase ở tài liệu này và session là addendum trạng thái, không sửa các checkbox lịch sử của planning.

### Đề xuất nhóm commit — chưa thực thi

1. `feat(policy): add admin platform policy lifecycle [SPARK-368]` — Operation contract/catalogue/resolve/draft/activation và Owner compatibility liên quan.
2. `feat(integration): add protected admin integration settings [SPARK-368]` — Payment/Notification API, application ports, persistence/config/auth/adapter changes đã review. Giữ F6/F7 rõ trong review trước khi quyết định đưa bản này vào PR.
3. `feat(admin): add platform policy and integration console [SPARK-369]` — FE clients/views/SystemConfig trong phạm vi.
4. `test(policy): verify admin policy and integration behavior [SPARK-370]` — focused BE/FE/Postman tests và sanitized evidence có liên quan.
5. `docs(policy): document verification and delivery handoff [SPARK-340]` — approved contract/review/session/delivery docs, chọn lịch sử cần thiết theo quy ước repo.

Đây là grouping proposal, không chứng nhận mọi file hiện có đã được phép stage. Chỉ commit/push/tạo PR khi user yêu cầu thao tác đó.

## Điều kiện trước ready-to-merge / rollout

- Giải quyết F6: bảo toàn verified callback cho checkout cũ hoặc owner duyệt phương án chuyển đổi có bằng chứng. Không miễn chỉ vì local FIXTURE không gửi HTTP.
- Quyết định xử lý F7/F8 và các low findings trong [P5 report](SPARK-340-P5-review-20261117.md). Chưa có miễn trừ từ user cho findings này.
- Gắn kết quả tester cho U06/U11/U20 và sign-off localhost. U19 được theo dõi BLOCKED dependency SPARK-273, không ghi PASS.
- Review/stage đúng phạm vi, chuẩn bị commit, kiểm tra remote develop và conflict trong bước delivery Git riêng được user cho phép.

**Bàn giao hiện tại: P4 tạm đóng; P5 review và P6 verification/PR preparation đã thực hiện. Chưa xác nhận parent SPARK-340 Done hoặc ready-to-merge.**
