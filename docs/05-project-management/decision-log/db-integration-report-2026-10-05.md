# Báo cáo tích hợp Database (PostgreSQL & EF Core)

**Document Type:** Báo cáo kỹ thuật & Quyết định kiến trúc (Decision Record)  
**Status:** `WORKING` (Đã hoàn thành cơ bản, chờ tích hợp hash password và thống nhất workflow với team)  
**Date:** 05/10/2026  

## 1. Mục đích và Quyết định hiện tại

Tài liệu này ghi nhận quá trình và các quyết định kỹ thuật khi chuyển đổi hệ thống từ cơ sở dữ liệu giả lập (In-Memory) sang cơ sở dữ liệu thật (PostgreSQL) để chuẩn bị cho môi trường thực tế.

**Các quyết định đã chốt:**
1. **Sử dụng Docker cho CSDL:** Chạy PostgreSQL 16 qua Docker Compose thay vì cài đặt trực tiếp lên máy. Điều này giúp đồng bộ môi trường giữa các dev và dễ dàng triển khai (deploy).
2. **Dùng Entity Framework Core (Npgsql):** Làm ORM chính để thao tác với CSDL. Bỏ hoàn toàn `InMemoryDatabase`.
3. **Sử dụng Data Seeding:** Viết script tự động tạo tài khoản test (`driver@gmail.com`) mỗi khi Backend khởi động lần đầu, giúp team test giao diện ngay mà không cần đợi API Register.
4. **Đổi Port Docker sang 5433:** Thay vì dùng cổng 5432 mặc định của PostgreSQL, quyết định map cổng `5433:5432` để chống xung đột (conflict) với các bản PostgreSQL có thể đã được cài sẵn trên Windows của các thành viên.

## 2. Nguồn và phạm vi tích hợp

| Tiêu chí | Chi tiết | Tác động |
| --- | --- | --- |
| **Nguồn Script DB** | `05.1-Database-Scripts.sql` | Nạp trực tiếp vào thư mục khởi tạo của Docker container để tự động sinh cấu trúc 3 tầng: `Users` -> `Accounts` -> `Roles`. |
| **Refactor Domain** | Xóa `UserRole.cs`, thêm `Account.cs` và `AccountRole.cs` | Đảm bảo Entity C# khớp 100% với kiến trúc bảng mới. Bỏ kế thừa `BaseEntity` ở bảng `Role` do bảng này chỉ dùng string `code` làm PK và không có Audit Fields. |
| **Persistence Layer** | Viết `AppDbContext`, sửa `GenericRepository` & `UserRepository` | Chuyển hoàn toàn logic truy vấn từ List (Memory) sang `DbSet` của EF Core, sử dụng Fluent API để map các cột snake_case. |
| **API Layer** | Cập nhật `LoginCommandHandler`, `AuthSessionTests` | Cập nhật luồng Login để lấy chức danh thông qua tập hợp `Accounts` thay vì `UserRoles` cũ. |

## 3. Hướng dẫn dành cho Team (Team Guidelines)

Để chạy code bản mới nhất mượt mà, team cần tuân thủ các bước sau:

### 3.1. Khởi động DB và Backend
1. Mở Terminal tại gốc thư mục dự án, chạy lệnh:
   ```bash
   docker-compose up -d
   ```
   *(Docker sẽ tự tải Postgres, tạo DB `smartpark_db` và chạy script SQL tự động).*
2. Di chuyển vào thư mục API và chạy Backend:
   ```bash
   cd src/Services/UserService/SmartParking.UserService.API
   dotnet run
   ```
   *(Khi Backend khởi động, `DataSeeder` sẽ âm thầm kiểm tra, nếu DB trống nó sẽ tự nhồi tài khoản `driver@gmail.com` / `Password@123` vào).*

### 3.2. Xem dữ liệu bằng pgAdmin 4 / DBeaver
Do cổng đã bị đổi để chống xung đột, team lưu ý kết nối bằng thông số sau:
- **Host name/address:** `localhost`
- **Port:** `5433` *(Lưu ý: Không dùng 5432)*
- **Database:** `smartpark_db`
- **Username:** `smartpark_user`
- **Password:** `smartpark_password`

## 4. Giới hạn và Quyết định kỹ thuật còn mở (Deferred Items)

- **Mật khẩu đang lưu dạng thô (Plain-text):** `DataSeeder` và `LoginCommandHandler` vẫn đang so sánh chuỗi thô (`Password@123`). Quyết định dời việc tích hợp thư viện Hashing (BCrypt hoặc ASP.NET Identity) sang Task tiếp theo để tránh làm gián đoạn việc test luồng kết nối cơ bản.
- **Workflow Quản lý Schema CSDL:** Hiện tại bảng được tạo bằng script thô nạp vào Docker. Team cần thống nhất về sau sẽ quản lý thay đổi CSDL bằng **EF Core Migrations** (Code-First) hay tiếp tục maintain file `.sql` (Database-First).
- **Phạm vi Data Seeding:** Script hiện tại chỉ nhồi duy nhất 1 User. Cần mở rộng thêm các loại tài khoản khác (Platform Admin, Business Owner) sau khi hoàn thiện bảng phân quyền.

## 5. Câu trả lời chuẩn bị khi báo cáo (Q&A)

**Q: Tại sao lại dùng Docker để chứa Database mà không cài trực tiếp Postgres lên máy?**
> **A:** Để đảm bảo tính đồng nhất (Consistency) cho cả team. Bất kỳ ai clone code về chỉ cần gõ 1 lệnh `docker-compose up -d` là có ngay DB cấu hình chuẩn xác, tự động chạy script tạo bảng. Khỏi lo lỗi người dùng phiên bản này, người dùng phiên bản kia, hay quên setup User/Password. Lúc deploy lên Server/Cloud cũng tái sử dụng file cấu hình này rất dễ.

**Q: Tại sao lại đổi port sang 5433 chứ không để 5432 mặc định?**
> **A:** Do phát hiện trên máy tính Windows (của em và có thể của nhiều bạn khác) hay cài sẵn phần mềm PostgreSQL mặc định chiếm cổng 5432. Nếu Docker cũng dùng 5432 thì sẽ bị chéo (Backend chui nhầm vào Postgres máy thật thay vì Docker, dẫn đến sai mật khẩu). Sửa thành 5433 giúp 격 ly hoàn toàn, code chạy 100% ăn chắc ở mọi máy.

**Q: Data Seeding chạy như thế nào? Cứ mỗi lần bật API lên là nó tạo thêm dữ liệu ảo đè vào à?**
> **A:** Không ạ. Script Seeding dùng hàm `AnyAsync()` để kiểm tra bảng. Lần đầu bật API, thấy bảng trống nó mới Insert. Các lần sau bật API lên, nó kiểm tra thấy có dữ liệu rồi thì nó Skip qua luôn. Cơ chế này đảm bảo dữ liệu vừa tự động khởi tạo lại vừa an toàn không bị rác.

**Q: Thế sao không dùng API Register để tạo tài khoản chuẩn thực tế mà phải xài Seeding?**
> **A:** Trong thực tế (Business Standard), API Register chỉ dùng cho khách hàng cuối (Tài xế). Còn các tài khoản quản trị hệ thống (Super Admin) bắt buộc phải được Seed lúc hệ thống khởi động (Bootstrap) chứ không ai cho phép đăng ký tự do qua API cả. Đồng thời Seeding giúp team phát triển (Dev) có ngay tài khoản test để làm việc liền mà không tốn công đăng ký lặp đi lặp lại.

**Q: Mật khẩu hiện tại có mã hóa chưa?**
> **A:** Hiện tại em cố tình giữ nguyên dạng thô (Plain-text) theo đúng logic của bản Mock In-memory ban đầu để đảm bảo luồng cũ không bị gãy vỡ. Việc tích hợp thư viện Băm mật khẩu (Hashing) em ghi nhận trong phần "Deferred Items" và sẽ làm thành 1 Task riêng biệt sau khi chốt luồng nền tảng này ạ.
