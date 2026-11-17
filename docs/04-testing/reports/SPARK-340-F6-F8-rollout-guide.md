# SPARK-340 F6–F8 — DDL, repair và kiểm thử

Thiết kế bảng riêng và công cụ offline đã được user duyệt, gồm ngoại lệ Schema Freeze. Việc duyệt chỉ cho phép chuẩn bị code/DDL và test trên DB tạm. Chưa tự chạy DDL/import trên stack đang UAT. Hướng dẫn này dành cho Payment owner/operator có quyền DB và nguồn evidence đáng tin cậy; không thêm quyền xử lý thanh toán vào Admin settings API.

## Thay đổi hành vi

- F7: binding mới được lưu tại `checkout_signing_bindings`, liên kết tới Payment order; payload mode/version/checksum được DataProtection mã hóa. Không append binding mới vào `policy_revisions`. FK RESTRICT và unique order identity bảo vệ liên kết; chưa có automatic TTL/prune. Giữ binding và key ring cùng financial history cho đến khi có chính sách archive được duyệt.
- Binding chính xác, đã mã hóa trong reserved policy namespace của bản trước vẫn đọc được. Có thể chuyển sang bảng mới bằng source LEGACY_POLICY. Không suy checksum key từ thời điểm tạo order và không dùng key hiện hành làm fallback.
- F6: checkout chưa từng có binding chỉ được khôi phục bằng mapping có provider link khớp transaction PAYOS/PAYMENT của đúng order/tenant, với key/mode/version do owner xác nhận. Dry-run là mặc định. Mapping sai/thiếu evidence/conflict không được ghi. Đây không phải chứng minh tự động rằng một key do operator nhập là key lịch sử đúng; nguồn dữ liệu và callback verification vẫn phải được owner xác nhận.
- F8: platform create/update chỉ nhận omitted/null cho arrivalLateToleranceEnabled. true/false trả validation error trước khi persistence; Owner site flow giữ quyền enablement.

### Follow-up F-A/F-B/F-C

Webhook verification luôn đọc exact per-order binding, kể cả khi adapter được tạo trực tiếp không có settings store; key trong options không còn là nguồn fallback. Checkout thật bắt buộc có cả settings authority và signing binding. Options-only chỉ còn hỗ trợ các nhánh emulation vốn không gửi HTTP.

Checkout issuance và repository dùng chung PayOsOrderCodes.Resolve để đối chiếu provider code với order đọc từ DB, gồm cả mapping Guid fallback đã có. INSERT cũng so sánh local code đã quan sát để không nhận thay đổi chen giữa bước kiểm tra/ghi. Đây là bảo đảm của các supported application writers, không phải trigger chặn mọi DML thủ công ngoài quy trình; không thêm schema trong follow-up này.

## Thứ tự rollout do owner thực hiện

1. Xác định Payment DB đích và sao lưu theo quy trình nhóm; giữ nguyên Payment DataProtection volume/key ring. Không dùng `.env` hoặc key XML trong Git/evidence.
2. Review/apply `scripts/database/upgrades/20261117-payment-checkout-signing-bindings.sql` bằng tài khoản owner. Script additive, transaction-scoped, không sửa money/status, không xóa policy history. Fresh DB init đã có bảng trong `04-payment-pricing-service-db.sql`. Chưa có startup migration tự động.
3. Build offline tool từ cùng branch. Cấp `ConnectionStrings__Payment` qua secret/environment của operator, không qua argv; cấp `DataProtection__KeysDirectory` là đường dẫn tuyệt đối tới đúng key ring Payment, ngoài repo. Tool không tự sinh/rotate key. Bảo vệ ACL và không đưa key directory vào repo.
4. Chạy inventory và dry-run. Thu thập mapping từ provider/audit record được Payment owner xác nhận. Không dùng thứ tự tạo order/thời gian để đoán key.
5. Khi dry-run READY và owner kiểm tra bằng chứng, chạy apply. Nếu lỗi giữa batch, một số row trước đó có thể đã commit; rerun cùng mapping là idempotent. Công cụ preflight toàn manifest trước khi bắt đầu apply; không có all-or-nothing transaction xuyên nhiều order.
6. Kiểm tra binding count/source, callback có chữ ký đúng và sai, replay, decrypt sau restart bằng cùng key ring. Kiểm tra order/transaction/invoice amounts/status không bị sửa bởi import. Lưu kết quả đã sanitize.
7. Deploy binary mới sau khi DDL sẵn sàng và mọi checkout cũ còn cần xử lý đã có disposition. **Inventory có MISSING_BINDING chưa được xử lý là rollout gate**, không coi code tool đã pass test là legacy production data đã được repair.

Nếu binary mới chạy trước DDL, binding repository sẽ fail closed, outbound checkout bị chặn. Không sửa bằng cách tắt persistence hoặc nhận callback không xác minh. Không drop bảng/key ring khi rollback; binary cũ không đọc bảng mới nên rollback trực tiếp có thể lại mất callback của checkout mới. Dùng bản tương thích đọc cả hai nơi hoặc hoàn tất reconciliation trước rollback theo owner-approved plan.

## Lệnh operator — chưa thực thi trên DB user

Sau khi operator cấu hình psql bằng secret/env riêng và chọn đúng Payment DB:

```powershell
psql -v ON_ERROR_STOP=1 -f scripts/database/upgrades/20261117-payment-checkout-signing-bindings.sql
dotnet build scripts/payment/SmartParking.CheckoutBindingRepair --nologo
dotnet run --project scripts/payment/SmartParking.CheckoutBindingRepair --no-build -- --inventory --offset 0 --limit 100
dotnet run --project scripts/payment/SmartParking.CheckoutBindingRepair --no-build -- --manifest C:\Secure\private-bindings.json
dotnet run --project scripts/payment/SmartParking.CheckoutBindingRepair --no-build -- --manifest C:\Secure\private-bindings.json --apply
```

Inventory paginates over PAYOS checkout transactions without a dedicated binding. Each row reports PaymentOrderId, OriginalOrderCode, nullable numeric OrderCode, ProviderLinkId and State. Nonnumeric, nonpositive or noncanonical historical codes return UNSUPPORTED_ORDER_CODE with OrderCode=null; the scan continues through following rows. These rows require owner investigation/reconciliation rather than guessed lookup. Exit0 means the inventory report completed, **not** that all rows are ready: MISSING_BINDING and UNSUPPORTED_ORDER_CODE remain rollout actions.

Capture all pages on a quiescent snapshot **before** applying imports; applying changes between offset pages changes membership. After apply, rescan from offset0. BOUND_LEGACY_POLICY means an authentic protected policy row exists and can be migrated, not that tariff cleanup is already safe. New checkout writes validate provider code against the stored order using the shared issuance mapping; a caller-supplied PaymentOrderId alone is insufficient. The existing nonnumeric webhook local-order lookup limitation is not repaired by reporting the inventory row.

Private manifest is an array, absolute path outside repository, max1MiB/1000 unique order codes. Template for manual legacy import (invalid until owner supplies actual evidence and key):

```json
[
  {
    "orderCode": 123456789,
    "providerLinkId": "owner-must-supply-exact-stored-provider-link-id",
    "source": "LEGACY_IMPORT",
    "evidenceReference": "owner-export/verified-entry-id",
    "mode": null,
    "version": 0,
    "checksumKey": null
  }
]
```

Use LIVE or SANDBOX, positive configuration version and actual checksum from the issuing authority. FIXTURE is not an authoritative provider mode. Unknown/missing historical key or missing stored provider-link evidence stays a manual reconciliation case. Protect manifest ACL, keep it outside Git, and follow the team's secret-file disposal process. Do not echo it to terminal or include it in raw Newman exports.

For an existing reserved-policy binding, use `source: LEGACY_POLICY`, same real orderCode/providerLinkId/evidenceReference, omit mode/version/checksumKey. The tool decrypts the exact old protected row and writes the dedicated row; no new key is guessed. Old policy row is retained. No automatic deletion is implemented; owner may only archive the legacy namespace after migration verification, backup and retention approval. Ordinary pricing rows are not migrated or deleted.

Exit0 = READY dry-run, APPLIED apply or completed inventory report. Exit2 = invalid/conflicting per-entry result and the no-arguments usage path. Other malformed usage currently follows the generic exit1 path (known low finding). Exit1 also covers schema/key access/invalid JSON. Output never includes checksum or protected payload. Exit codes and individual states must be checked; process completion alone is not proof of application.

## Reviewer checklist

- Verify real PG suites, especially backend PID inequality and read-before-outbound flag, source migration with unchanged pricing count, concurrent first-writer winner, orphan rejection and FK retention.
- Run offline CLI integration test: missing pre-feature binding fails signature; dry-run writes0; apply/replay; wrong provider/tenant/method/secret conflict; batch preflight; malformed JSON never echoes secrets; fresh key provider verifies the restored signature, tampered signature rejected.
- Verify missing/corrupt binding never falls back to current key. Normal initiate must persist a binding before any provider HTTP. Import never initiates checkout or changes financial status.
- For F8 exercise both create and update, true and false, and unchanged row after validation failure. Null/omitted remain accepted; existing Owner enablement behavior still passes.
- No live U19/SPARK-273 conclusion follows from these repairs. Existing user stack has not been migrated/restarted by this implementation turn.
