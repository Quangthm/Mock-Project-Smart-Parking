# 08 — VehicleRegistrationService: duplicate active plate → 409 (D2a)

> Report này **chỉ ghi nhận** thay đổi do lane BE-handler thực hiện (T5). Lane tài liệu không sửa file này.

## Meta
- **Ngày:** 2026-10-18
- **Branch:** `feature/db-hybrid-srs-v1`
- **Base commit:** `develop` @ `0c8a6fb`
- **File:** `src/Services/UserService/SmartParking.UserService.Application/Services/VehicleRegistrationService.cs`
- **Lane:** T5 BE-handler (parallel lane)

## Thay đổi chính
- Thêm `using System.Data.Common;` (dòng 2) để dùng `DbException`.
- Bọc `await unitOfWork.SaveChangesAsync(ct)` trong `try/catch` (dòng 71–74): khi lưu xe trùng plate active, DB ném lỗi unique violation `SqlState = "23505"` (index `uq_active_vehicle_plate`) → bắt và ném `AuthException("PLATE_EXISTS", "A vehicle with this plate is already registered.", 409)`.
- Kết quả: đăng ký xe trùng plate active trả **409** thay vì **500** (unhandled exception).

```csharp
try { await unitOfWork.SaveChangesAsync(ct); }
// Duplicate active plate hits uq_active_vehicle_plate; surface it as a 409 like the other registration flows.
catch (Exception e) when (e.InnerException is DbException { SqlState: "23505" })
{ throw new AuthException("PLATE_EXISTS", "A vehicle with this plate is already registered.", 409); }
```

## Lý do / quyết định liên quan
- **D2a (T4):** giữ UNIQUE index trên plate active (đúng invariant SRS v1.0) **và** thêm handler `23505 → 409` ở BE. Đây là lựa chọn (a) của Q4.
- **Q4 (T4):** develop trước đây không enforce uniqueness (overlay `05.8` drop `uq_active_vehicle_plate`); hybrid theo arch giữ UNIQUE ⇒ nếu không sửa BE thì plate trùng → `23505` unhandled → 500 (Risk #4 trong plan).
- Cặp đôi với **UNIQUE index** do lane SQL tạo (xem report 01–04 của lane SQL: base file user-access + overlay). Report này chỉ mô tả phía BE.
- `AuthException` được map ra HTTP status bởi `AuthExceptionMiddleware` (`SmartParking.UserService.API/Middleware/AuthExceptionMiddleware.cs`).

## Evidence
`git diff 0c8a6fb -- src/Services/UserService/SmartParking.UserService.Application/Services/VehicleRegistrationService.cs`:

```diff
 using System.ComponentModel.DataAnnotations;
+using System.Data.Common;
 using SmartParking.UserService.Domain.Entities;
…
-        await unitOfWork.SaveChangesAsync(ct);
+        try { await unitOfWork.SaveChangesAsync(ct); }
+        // Duplicate active plate hits uq_active_vehicle_plate; surface it as a 409 like the other registration flows.
+        catch (Exception e) when (e.InnerException is DbException { SqlState: "23505" })
+        { throw new AuthException("PLATE_EXISTS", "A vehicle with this plate is already registered.", 409); }
         await transaction.CommitAsync(ct);
```

Line refs hiện tại: `using System.Data.Common;` dòng 2; `try/catch` dòng 71–74.

## Ảnh hưởng
- Endpoint đăng ký/cập nhật xe (UserService) trả 409 `PLATE_EXISTS` khi plate active trùng.
- Không đổi luồng thành công; transaction vẫn commit sau khi `SaveChangesAsync` thành công.
- Phụ thuộc index `uq_active_vehicle_plate` tồn tại trong DB user (do lane SQL tạo) — nếu index không có, nhánh catch không bao giờ kích hoạt.

## Cách tái kiểm chứng
```powershell
git diff 0c8a6fb -- src/Services/UserService/SmartParking.UserService.Application/Services/VehicleRegistrationService.cs
# build UserService
dotnet build src/Services/UserService/SmartParking.UserService.API
# test thủ công: đăng ký 2 xe cùng plate active → kỳ vọng HTTP 409, body code PLATE_EXISTS
```
