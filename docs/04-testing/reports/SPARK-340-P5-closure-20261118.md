# SPARK-340 — Đóng P5 code review, chuyển kiểm thử trước PR

Ngày18/11/2026, Asia/Saigon. Reviewed source HEAD `fb4d2ce41683ff727efd8b48bd6ebaff8ee9d098`, branch `feat/SPARK-340-admin-platform-policies`.

## Quyết định

**P5 CLOSED — IMPLEMENTATION REVIEW ACCEPTED trong phạm vi SPARK-340/368/369/370.** User yêu cầu đóng P5, push branch cho tester kiểm thử lại toàn bộ trước khi mở PR. Không tạo PR, merge hoặc deploy trong bước bàn giao này.

Căn cứ là [independent review vòng2](SPARK-340-FA-FC-independent-review-20261117.md): F6/F7/F8 được chấp nhận ở mức implementation; F-A/F-B/F-C CLOSED, F-B bảo đảm cho supported writers. Không phát hiện P1/P2 mới trong phần sửa. Những phát hiện baseline/default-auth/deposit-order đã được tách sang đúng owner theo phạm vi user đã xác nhận; việc đóng P5 không miễn các rủi ro đó cho production.

Phần source đã qua review không thay đổi trong lượt đóng P5 này. Chỉ cập nhật session, biên bản và prompt kiểm thử. Các số dưới đây là **kết quả reviewer tự chạy**, không phải một lần chạy mới của agent bàn giao:

| Gate | Kết quả independent |
|---|---|
| BE build có restore |0errors/60warnings; checkpoint --no-restore trước đó29warnings là lệnh khác, không phải mâu thuẫn. |
| Full BE |1190passed/0failed/114skipped/1304total. |
| PG Payment |9passed/0failed/0skipped trên DB tạm. |
| Scoped Admin FE/Postman |33passed/0failed/0skipped. |

## Finding không blocking gate code-review, tiếp tục theo dõi

| Nhóm | Disposition |
|---|---|
| F-A/F-B/F-C | CLOSED theo independent review, giữ giới hạn supported application writers. |
| G-A | Low follow-up: issuance chấp nhận một số numeric format lạ, inventory bảo thủ hơn và báo unsupported. Không đổi code tiếp trong đợt bàn giao; tester ghi nhận nếu ảnh hưởng luồng dữ liệu được hỗ trợ. |
| G-B | Sự cố thiết lập test HTTP đã công khai và sửa/re-proven. Không phải defect sản phẩm còn mở, không xóa lịch sử. |
| G-C | Low cleanup: method keyring cũ không còn caller production. |
| F-D/F-E/F-F/F-G/F-H/F-I | Low follow-ups giữ nguyên. Không khẳng định table chống mọi DML đặc quyền, usage exit code đã được guide mô tả đúng, operator phải bảo vệ private paths/key ring và unsupported historical codes cần disposition riêng. |

Disposition này cho phép kết thúc review source; không tự động chấp thuận mọi điều kiện triển khai hoặc nghiệm thu story. Tester phát hiện regression/P1/P2 mới trong phạm vi thì mở lại finding/P5 tương ứng.

## Trạng thái phase và gate còn lại

| Phần | Trạng thái |
|---|---|
| P4 | Tạm đóng có điều kiện theo user. Các ca chưa chạy/blocked không trở thành PASS. |
| P5 | CLOSED ở mức implementation review. |
| P6 | Branch đã được công bố; cập nhật thêm biên bản/prompt cho lượt test trước PR. **Chưa mở PR**. |
| Kiểm thử trước PR | PENDING — tester chạy baseline, HTTP58, Owner/SITE, F8 HTTP negatives, integration durability và browser UAT. |
| Live rollout | NOT READY — target DDL/inventory/import/restart và HTTP live chưa thực hiện. |
| Parent SPARK-340 | Vẫn active, chưa Done/ready-to-merge. |

U19 vẫn BLOCKED dependency SPARK-273 và snapshot immutability vẫn là acceptance criterion của SPARK-340/370. Không yêu cầu task này tự triển khai SPARK-273. Khi chuẩn bị PR, ghi rõ dependency/tiêu chí được hoãn và quyết định của owner; không dùng việc đóng P5 để xóa AC hoặc đánh dấu PASS. U06/U11/U20 và tester-group sign-off phải có kết quả từng ca.

## Bàn giao tester

Giao [prompt kiểm thử toàn bộ trước PR](SPARK-340-pre-PR-full-test-prompt-20261118.md), [checklist UAT](SPARK-340-localhost-UAT-checklist.md) và [DDL/repair runbook](SPARK-340-F6-F8-rollout-guide.md). Dùng môi trường tester riêng hoặc target/window được owner chỉ định. Approval trước đây chỉ cho chuẩn bị DDL/code và test DB tạm; không suy ra quyền áp vào DB user đang chạy.

Push tài liệu lên feature branch không phải deploy. Không cần chạy lại toàn bộ build/test cho thay đổi docs-only này; kiểm tra diff/secret hygiene trước commit. Giữ nguyên collection SPARK-238 ngoài commit. Không thay đổi Jira hoặc chuyển session sang completed khi parent và nghiệm thu còn mở.
