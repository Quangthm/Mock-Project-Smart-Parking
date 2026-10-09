# Quy trình chuẩn hóa: Viết tài liệu Database (DB Documentation Workflow)

**Mục đích**: Tài liệu này định nghĩa quy trình chuẩn (SOP - Standard Operating Procedure) cho việc sinh và kiểm toán tài liệu Database (DB Docs) trong dự án SmartPark. Bất kỳ AI Agent nào khi bắt đầu một phiên làm việc mới liên quan đến việc viết DB Doc đều **BẮT BUỘC** phải đọc và tuân thủ quy trình này.

---

## Các tài nguyên đầu vào (Inputs)
1. **File DDL SQL**: Các file trong `scripts/database/microservices/*.sql`
2. **File Template chuẩn**: `docs/02-architecture/database/microservices/00-service-db-doc-template.md` (Tuyệt đối tuân thủ cấu trúc trong file này).
3. **SRS (System Requirements Specification)**: `docs/01-requirements/srs/smartpark-srs-v0.9.md`

---

## Quy tắc tối thượng (Architectural Boundary Rule)
**Không được phép tự sáng tạo Microservice mới.**
Danh sách và số lượng các microservices được phép tồn tại được giới hạn **tuyệt đối** bởi kiến trúc đã chốt trong file SRS. Agent **bắt buộc** phải đối chiếu với SRS để biết chính xác có bao nhiêu service đang được định nghĩa. Nếu file SQL hoặc context yêu cầu viết doc cho một service nằm ngoài danh sách chuẩn của SRS (ví dụ: tự nghĩ ra service thứ 7, thứ 8), Agent phải **từ chối** tạo file doc mới, và tiến hành cảnh báo để gộp (merge) hoặc xóa bỏ service thừa đó theo đúng ranh giới của SRS.

---

## 6 Bước Quy trình làm việc (Workflow)

### Bước 1: Thu thập ngữ cảnh và Kiểm tra Ranh giới (Context Gathering & Boundary Check)
- Đọc nội dung của file SQL tương ứng với Service cần làm.
- Quét các phần liên quan đến miền nghiệp vụ (domain) của Service đó trong file SRS. Bắt buộc kiểm tra **Quy tắc tối thượng** để xác nhận service này hợp lệ theo kiến trúc hệ thống.
- Bắt buộc đọc file `00-service-db-doc-template.md` để nhớ lại cấu trúc cần viết.

### Bước 2: Viết tài liệu (Drafting)
- Viết file tài liệu `[tên-service]-db-docs.md` mới (chỉ khi vượt qua kiểm tra ranh giới ở Bước 1).
- **Yêu cầu Zero Omissions**: Phải vét cạn 100% số bảng và 100% số cột/constraints có trong file SQL.
- **Yêu cầu Traceability**: Mapping chính xác với các mã FR, BR, US trong SRS, không được phép hallucination.
- **Yêu cầu Format**:
  - Constraints & Indexes phải là **Bảng Markdown** (không dùng text thường hay list).
  - Phải vẽ sơ đồ **ASCII ERD** thể hiện quan hệ 1-N, N-N ở cuối file.
  - Phải có bảng **Tóm tắt Cross-service References** liệt kê các Logical ID.

### Bước 3: Đóng gói và Lưu file (Persisting)
- Agent tiến hành lưu file `[tên-service]-db-docs.md` vào đúng thư mục `docs/02-architecture/database/microservices/`.

### Bước 4: Triệu hồi Kiểm toán viên (Invoking Independent Auditor)
- Hệ thống duy trì một Subagent có tên `db_doc_auditor` (Independent Database Documentation Auditor). Nếu chưa được định nghĩa trong conversation hiện tại, Agent chính phải định nghĩa (define_subagent) nó với các quyền đọc file (read tools).
- Nhiệm vụ của Auditor: 
  - Dò chéo file `.sql`, file `.md`, và `SRS`.
  - Check Zero Omissions.
  - Check tuân thủ Template.
  - Check Hallucinations trong SRS mapping.
- Agent chính sẽ sử dụng `invoke_subagent` truyền vào đường dẫn của các file liên quan để Auditor bắt đầu kiểm tra.

### Bước 5: Sửa chữa (Refining)
- Nếu Auditor phát hiện có bất kỳ thiếu sót nào (thiếu cột, sai template, hallucinate FR/BR), Auditor sẽ report lại.
- Agent chính phải tiến hành sửa file `.md` ngay lập tức dựa trên report của Auditor. Quá trình này lặp lại cho đến khi Auditor phê duyệt (Approve) 100%.

### Bước 6: Hoàn tất (Completion)
- Agent chính thông báo cho người dùng (User) rằng tài liệu đã được viết và kiểm toán thành công, cung cấp link trực tiếp đến file `.md` để User xem kết quả.

---

## Kịch bản khi có thay đổi (Update Scenario)

Quy trình này định nghĩa luồng "cập nhật cục bộ" (Delta Update) nhằm tối ưu thời gian và nguồn lực. Kịch bản này được kích hoạt khi:
1. Phiên bản SRS có sự cập nhật (version mới).
2. Khi có chỉ định/điều chỉnh thiết kế trực tiếp từ Người dùng (User/Architect), dù SRS chưa phản ánh thay đổi đó.

### Bước 1: Phân tích tác động (Impact Analysis)
- Khi nhận được thông báo thay đổi (từ SRS mới hoặc yêu cầu trực tiếp của User), Agent **bắt buộc** phải phân tích và khoanh vùng chính xác các Service, các bảng, cột hoặc ràng buộc (constraints) bị ảnh hưởng.
- **Ngoại lệ kiến trúc**: Nếu đây là chỉ định thiết kế trực tiếp từ User, Agent được phép linh hoạt vượt qua "Quy tắc tối thượng" (Architectural Boundary Rule) để ưu tiên hiện thực hóa ý định thiết kế của User làm ưu tiên số một.

### Bước 2: Cập nhật cục bộ (Localized Patching)
- Agent **chỉ được phép can thiệp và vá (patch)** đúng những nội dung bị ảnh hưởng trong file `.md` hiện tại (sử dụng công cụ thay thế nội dung cục bộ).
- **Tuyệt đối cấm** tự động sinh lại (regenerate) toàn bộ tài liệu. Việc này nhằm bảo toàn 100% các định dạng (format), bảng, và sơ đồ ASCII ERD đã được phê duyệt ở phiên bản trước đó.

### Bước 3: Kiểm toán chênh lệch (Delta Auditing)
- Triệu hồi Subagent `db_doc_auditor` để kiểm tra chéo (audit) **đúng phần dữ liệu vừa được cập nhật**, không cần audit lại toàn bộ file.
- **Quy tắc chân lý (Source of Truth)**: Đối với các thay đổi do User chỉ định trực tiếp, Auditor phải lấy nội dung chỉ định của User làm "chân lý cao nhất" để đối chiếu. Không ép buộc đối chiếu 1-1 với SRS cũ để tránh báo lỗi "Hallucination" sai lệch.
