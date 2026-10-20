# Quy trình chuẩn hóa: Viết tài liệu Database (DB Documentation Workflow)

**Mục đích**: Tài liệu này định nghĩa quy trình chuẩn (SOP - Standard Operating Procedure) cho việc sinh và kiểm toán tài liệu Database (DB Docs) trong dự án SmartPark. Bất kỳ AI Agent nào khi bắt đầu một phiên làm việc mới liên quan đến việc viết DB Doc đều **BẮT BUỘC** phải đọc và tuân thủ quy trình này.

---

## Các tài nguyên đầu vào (Inputs)
1. **File DDL SQL**: base files trong `scripts/database/microservices/*.sql` (7 file: `01`, `02`, `03a`, `03b`, `04`, `05`, `06`) **và** overlay trong `scripts/database/overlay/*.sql` (8 file). Base + overlay là schema thực tế được deploy.
2. **File Template chuẩn**: `docs/02-architecture/database/microservices/00-service-db-doc-template.md` (Tuyệt đối tuân thủ cấu trúc trong file này).
3. **SRS (System Requirements Specification)**: `docs/01-requirements/srs/smartpark-srs-v1.0.md` (v1.0 RC3, commit `6165024`).
4. **Deployment truth**: `compose.schema-integration.yml`, `scripts/apply-workflow-migrations.ps1`, `setup.ps1` — dùng để xác nhận DB vật lý, thứ tự mount base/overlay và tên cột runtime.

**Nguyên tắc nguồn chân lý:** Tài liệu mô tả **schema đang triển khai** (base + overlay), không chỉ file arch. Mọi deviation so với arch/SRS phải được ghi rõ trong mục `Deployment profile`/`Deviations` của tài liệu service; không âm thầm chuẩn hóa hoặc xóa deviation.

---

## Quy tắc tối thượng (Architectural Boundary Rule)
**Không được phép tự sáng tạo Microservice mới.**
Danh sách và số lượng các microservices được phép tồn tại được giới hạn **tuyệt đối** bởi kiến trúc đã chốt trong file SRS. Agent **bắt buộc** phải đối chiếu với SRS để biết chính xác có bao nhiêu service đang được định nghĩa. Nếu file SQL hoặc context yêu cầu viết doc cho một service nằm ngoài danh sách chuẩn của SRS (ví dụ: tự nghĩ ra service thứ 7, thứ 8), Agent phải **từ chối** tạo file doc mới, và tiến hành cảnh báo để gộp (merge) hoặc xóa bỏ service thừa đó theo đúng ranh giới của SRS.

**Lưu ý deployment:** `03a` và `03b` không phải service thứ 7; chúng là hai phần vật lý của cùng logical Parking Operations Service (D1). Tài liệu `03` phải mô tả cả hai DB đích và overlay tương ứng.

---

## 6 Bước Quy trình làm việc (Workflow)

### Bước 1: Thu thập ngữ cảnh và Kiểm tra Ranh giới (Context Gathering & Boundary Check)
- Đọc nội dung của file SQL tương ứng với Service cần làm.
- Quét các phần liên quan đến miền nghiệp vụ (domain) của Service đó trong file SRS. Bắt buộc kiểm tra **Quy tắc tối thượng** để xác nhận service này hợp lệ theo kiến trúc hệ thống.
- Bắt buộc đọc file `00-service-db-doc-template.md` để nhớ lại cấu trúc cần viết.
- Đọc cả overlay liên quan (`scripts/database/overlay/*.sql`) và đối chiếu `compose.schema-integration.yml`/`setup.ps1` để biết DB đích và thứ tự mount.
- Xác định đúng DB vật lý (6 DB; `03` tách `03a`/`03b`).

### Bước 2: Viết tài liệu (Drafting)
- Viết file tài liệu `[tên-service]-db-docs.md` mới (chỉ khi vượt qua kiểm tra ranh giới ở Bước 1).
- **Yêu cầu Zero Omissions**: Phải vét cạn 100% số bảng và 100% số cột/constraints có trong **base SQL + overlay** của DB đích.
- **Deployment profile bắt buộc**: mỗi file doc phải có mục `## 0. Deployment profile` ghi DB vật lý, base DDL, overlay, trạng thái khớp/deviation.
- **Deviation bắt buộc**: nếu schema khác arch/SRS (ví dụ 7 role codes, 2 cột slot runtime, rename `unit_id`/`count`), phải có mục `Deviations` nêu mã quyết định (D1–D7) và lý do.
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
  - Dò chéo base SQL + overlay, file `.md`, `SRS`, và deployment truth (`compose.schema-integration.yml`, `setup.ps1`).
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
3. Khi thay đổi deployment (thêm/xóa overlay, đổi DB đích, đổi `setup.ps1`/compose) dù SRS không đổi.

### Bước 1: Phân tích tác động (Impact Analysis)
- Khi nhận được thông báo thay đổi (từ SRS mới hoặc yêu cầu trực tiếp của User), Agent **bắt buộc** phải phân tích và khoanh vùng chính xác các Service, các bảng, cột hoặc ràng buộc (constraints) bị ảnh hưởng.
- **Ngoại lệ kiến trúc**: Nếu đây là chỉ định thiết kế trực tiếp từ User, Agent được phép linh hoạt vượt qua "Quy tắc tối thượng" (Architectural Boundary Rule) để ưu tiên hiện thực hóa ý định thiết kế của User làm ưu tiên số một.

### Bước 2: Cập nhật cục bộ (Localized Patching)
- Agent **chỉ được phép can thiệp và vá (patch)** đúng những nội dung bị ảnh hưởng trong file `.md` hiện tại (sử dụng công cụ thay thế nội dung cục bộ), bao gồm `Deployment profile`/`Deviations` nếu thay đổi deployment.
- **Tuyệt đối cấm** tự động sinh lại (regenerate) toàn bộ tài liệu. Việc này nhằm bảo toàn 100% các định dạng (format), bảng, và sơ đồ ASCII ERD đã được phê duyệt ở phiên bản trước đó.

### Bước 3: Kiểm toán chênh lệch (Delta Auditing)
- Triệu hồi Subagent `db_doc_auditor` để kiểm tra chéo (audit) **đúng phần dữ liệu vừa được cập nhật**, không cần audit lại toàn bộ file.
- **Quy tắc chân lý (Source of Truth)**: Đối với các thay đổi do User chỉ định trực tiếp, Auditor phải lấy nội dung chỉ định của User làm "chân lý cao nhất" để đối chiếu. Không ép buộc đối chiếu 1-1 với SRS cũ để tránh báo lỗi "Hallucination" sai lệch.
