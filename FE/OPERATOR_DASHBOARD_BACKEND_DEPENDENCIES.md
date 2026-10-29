# Operator Dashboard: các phụ thuộc API và nghiệp vụ chưa giải quyết

Tài liệu này ghi lại các vấn đề nằm ngoài phạm vi sửa state và race condition đã được duyệt. Không có thay đổi nào với Back-End, database, API contract hoặc quy tắc nghiệp vụ.

## Quyền Owner với Financial Reports

Front-End cho Owner mở `/dashboard/finance` (`src/App.tsx`), nhưng trang lập báo cáo qua `reservationApi.list(siteId)` (`src/roles/operator/dashboard/OperatorDashboard.tsx`). Reservation API kiểm tra quyền qua `/api/operators/site-scope`, yêu cầu role `operator` (`../src/Services/OperationService/SmartParking.OperationService.API/OperatorIdentityClient.cs` và `../src/Services/UserService/SmartParking.UserService.API/Controllers/OperatorsController.cs`). Vì vậy không thể giả định token Owner đọc được Reservations. Front-End hiện hiển thị lỗi request và ẩn tổng số cũ hoặc chưa đầy đủ. Việc cấp quyền xem báo cáo cho Owner cần quyết định về authorization hoặc API ở Back-End.

## Định nghĩa Reports và Settlement

Không tìm thấy endpoint Reports hoặc Settlement riêng trong các route Back-End đã kiểm tra. Finance view hiện tính số booking hoàn tất và doanh thu từ Reservations lọc theo site. Chỉ số “Pending Settlement” dựa trên `paymentMethod` bị thiếu; chưa có contract xác nhận đây là trạng thái settlement. `src/lib/operatorApi.ts` cũng map `amount` nullable thành `0`. Các mapping nghiệp vụ hiện có được giữ nguyên vì cách hiểu số tiền hoặc phương thức thanh toán bị thiếu cần contract thống nhất. Báo cáo settlement chính thức cần Back-End hỗ trợ hoặc quy tắc nghiệp vụ được tài liệu hóa.

## Cashier được gán nhiều site

Cashier hiện dùng site đầu tiên trong danh sách được gán vì shortcut view không có bộ chọn site. Phần sửa đã duyệt bảo vệ loading, error và checkout cho site hiện hành nhưng không thêm quy trình chọn site. Product/Operations cần xác định Cashier có được thao tác trên nhiều site hay không trước khi thay đổi UI và hành vi này.

## Cần kiểm tra runtime có xác thực

Các GET không xác thực, chỉ đọc tới User, Parking và Reservation API cục bộ đều trả `401`. Kết quả này chỉ xác nhận server có phản hồi. Response với token hợp lệ, assignment thực tế, trạng thái schema database và độ chính xác dữ liệu Finance vẫn cần tài khoản/môi trường kiểm thử phù hợp. Task này không chạy migration hoặc request ghi dữ liệu.
