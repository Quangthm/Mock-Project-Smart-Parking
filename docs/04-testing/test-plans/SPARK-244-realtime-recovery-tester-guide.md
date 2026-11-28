# SPARK-244 — Hướng dẫn kiểm thử cải thiện realtime từ đầu đến cuối

Ngày soạn: **2026-11-23, Asia/Saigon**. Worktree `SmartParking-244`, branch `feat/SPARK-244-realtime-monitoring`. Tài liệu hướng dẫn thực thi; **không phải báo cáo PASS**. Slice 3–4 và SPARK-244 vẫn **OPEN**.

---

## 1. Tester cần kiểm tra điều gì?

Mục tiêu là Operator xem đúng trạng thái bãi/slot theo quyền được giao, nhận thay đổi gần thời gian thực, không phát sinh chuỗi REST request liên tục, không hiển thị dữ liệu cũ của phiên hoặc tài khoản khác. Kiểm tra cả FE, BE và đường truyền tích hợp giữa chúng.

### Luồng dữ liệu cần xác minh

```text
Thao tác nghiệp vụ hợp lệ / nguồn sự kiện được duyệt
  -> Operations xử lý nghiệp vụ
  -> Facility projection/writer cập nhật trạng thái phục vụ layout
  -> thay đổi dữ liệu + framework outbox cùng transaction
  -> commit -> RabbitMQ -> authorized relay -> SignalR invalidation
  -> FE đánh dấu resource liên quan cần đồng bộ
  -> coordinator gộp/lập lịch REST read lấy snapshot có thẩm quyền
  -> cập nhật shared resource -> các màn hình/3D đang sử dụng resource
```

### Nguyên tắc cốt lõi cần nhớ khi quan sát

- **SignalR chỉ báo đổi, REST vẫn chạy để lấy dữ liệu mới:** SignalR báo dữ liệu nào thay đổi; FE vẫn gọi REST GET để lấy snapshot có thẩm quyền. Không yêu cầu mọi event phải sinh đúng 1 GET.
- **Dùng chung trong 1 tab, tách riêng giữa 2 tab:** Các component cùng resource dùng chung lần đọc trong một tab; hai tab A và B có kết nối và coordinator riêng. Không có kết nối/GET riêng cho từng slot.
- **Không polling layout khi đứng yên:** Polling nội bộ outbox ở BE không đồng nghĩa polling layout ở FE.
- **Phạm vi thay đổi cần bao phủ:** Publish sau commit; writer coverage/no-op; loại bỏ race broadcast cũ; gộp request và giữ trailing read cần thiết; initial-read reuse 2 giây; auth/session fencing; pause/resume khi ẩn tab hoặc mất mạng; Retry và refresh token; ưu tiên trạng thái Facility khi overlay session cũ; modal Release Backup; phân biệt healthy idle với stale thực sự.
- **Các màn hình liên quan:** `Dashboard Overview` (3D), `Lot Status & 3D Map`, `Gate Check-In`, `Gate Check-Out`, `Backup Slots & Maintenance` (Slot Management/Backup) và Cashier/POS qua luồng hiện có.
- **Ngoài phạm vi:** Không mở rộng Owner/Driver hoặc shared multi-site hub. Không sửa nghiệp vụ thanh toán/rời bãi để đạt kết quả test.

### Giải thích nhanh thuật ngữ cho Tester

| Thuật ngữ | Ý nghĩa đơn giản khi quan sát |
|---|---|
| **Invalidation** | Tin nhắn từ SignalR báo *"dữ liệu X vừa đổi, hãy tải lại"* (chưa mang toàn bộ layout mới). |
| **Managed read** | Lần gọi `GET` lấy dữ liệu layout/sessions/reservations do bộ điều phối (`coordinator`) quản lý. |
| **Coalescing / Trailing read** | Nếu nhiều event báo đổi dồn dập khi đang tải dở, FE gộp lại để không spam request; nếu có thay đổi thật phát sinh lúc đang tải, FE chạy thêm 1 lượt đọc chốt cuối (*trailing read*) để không sót dữ liệu mới nhất. |
| **Session / Generation fencing** | Đánh dấu thế hệ phiên đăng nhập; khi đổi tài khoản hoặc hết phiên, response cũ trả về muộn bị chặn không cho ghi đè lên màn hình mới. |
| **Fail-closed** | Khi dữ liệu freshness bị lỗi/thiếu hoặc source báo cũ (`isFresh=false`), UI phải hiện cảnh báo lỗi thay vì giả vờ đang đồng bộ bình thường dù WebSocket vẫn kết nối. |
| **Provenance** | Nguồn gốc và dấu vết đối chiếu của bằng chứng (run nào, build nào, file raw gốc ở đâu, thời điểm UTC). |

---

## 2. Đọc trước, chọn chế độ chạy & tổng quan 20 test case

### Tài liệu tham chiếu

- [Original Operator AC checklist](<../../05-project-management/handoffs/2026-11-22-SPARK-244-OPERATOR-AC-CHECKLIST.md>): tiêu chí gốc và các điểm chưa đóng.
- [Scope audit](<../../05-project-management/handoffs/SPARK-244-REALTIME-RECOVERY-HANDOFF.md#scope>): ranh giới yêu cầu/nghiệp vụ.
- [Review C1/C2/C3 và fixture F1 được duyệt](<../../05-project-management/handoffs/SPARK-244-REALTIME-RECOVERY-HANDOFF.md#f1>): giới hạn bằng chứng và quyền thực thi hiện tại.
- [Direct execution summary](<../../../../../Report/SPARK-244-realtime-recovery-2026-11-23/evidence/spark-244-final-gates-codex-2026-11-22/execution-summary.md>): không nhập kết quả của run khác vào run mới. *(Lưu ý: thư mục `Report` nằm ngoài Git theo bố cục máy nội bộ; nếu clone repo ở máy khác, cần nhận thư mục `Report` từ nhóm).*

### Chọn chế độ chạy

**Gate bàn giao qua GitHub (Engineer xác nhận trước khi tester bắt đầu):**

- Commit bàn giao phải chứa cả code FE/BE, các file mới/phụ thuộc cần thiết, regression tests và 4 tài liệu của đợt cải thiện. Push riêng guide không đưa bản sửa realtime tới máy tester. Ghi commit đã push và xác nhận remote branch `feat/SPARK-244-realtime-monitoring` chứa commit đó; không chỉ dùng HEAD local hoặc trạng thái `ahead` so với remote-tracking cũ.
- Engineer cung cấp FE build/source và BE image/source tương ứng commit bàn giao, health/config cần thiết, account/site có quyền và cách khởi động môi trường test theo [README](../../../README.md). Tester chỉ có repo chưa đồng nghĩa DB/queue/schema/config của môi trường mới đã sẵn sàng; thiếu readiness thì ghi `BLOCKED`, không tự chạy SQL hoặc reset DB.
- Các tài liệu hiện hành trong repo đủ hướng dẫn chạy mới. `Report` ngoài Git là evidence lịch sử: nếu chưa nhận được thư mục/link chia sẻ thì không dùng các PASS cũ làm kết quả của run mới; vẫn có thể chạy ca mới khi readiness/fixture cho ca đó đã đủ. Owner cung cấp nơi lưu evidence mới trước khi bắt đầu capture.
- Chốt một commit/build cho mỗi run. Nếu engineer sửa code/config giữa chừng, ghi boundary và xác định ca bị ảnh hưởng cần retest; không gộp kết quả từ hai build mà không ghi rõ.

- **Chế độ tiếp tục nghiệm thu (mặc định hiện tại):** Ưu tiên chạy **`TC-08 / F1`**; không chạy lại các ca cash / backend / idle / outage / Sign Out đã được chấp nhận nếu không có regression hoặc build/config mới cần xác minh.
- **Chế độ hồi quy toàn diện:** Tester lập run mới, xác nhận build/config và fixture với owner, thực hiện toàn bộ ca phù hợp dưới đây. Kết quả cũ chỉ là tham chiếu; từng hàng run mới bắt đầu `NOT RUN`. Tài liệu này không tự cấp quyền cho fixture/hạ tầng còn OPEN.

### Phân loại nhãn thực thi

| Nhãn | Cách xử lý |
|---|---|
| `UI thường` | Dùng tài khoản/quyền hiện có và UI được hỗ trợ trong môi trường test được giao |
| `F1 được duyệt` | Chỉ fixture Backup giới hạn tại `TC-08`, tuân thủ baseline/cleanup |
| `Cần owner/fixture` | Chuẩn bị kế hoạch; chưa thực thi tới khi có cơ chế và phạm vi được duyệt |
| `Engineer` | Dev/QA engineer chạy với môi trường cô lập, cung cấp evidence cho tester |

### Bảng tổng quan nhanh 20 Test Case (Quick Triage)

| TC | Nội dung ngắn | Nhãn / Người chạy | Tình trạng tham chiếu | Việc cần làm ở chế độ tiếp tục nghiệm thu |
|---|---|---|---|---|
| **TC-01** | Phân quyền site được giao (positive & negative) | `UI thường` / `Cần owner/fixture` (negative) | Chưa đóng negative | Chạy positive bình thường; chuẩn bị negative khi owner cấp account/site không quyền |
| **TC-02** | Initial load & chuyển qua lại các màn hình | `UI thường` | Đã có D2 bounded | Chỉ chạy lại nếu kiểm tra regression điều hướng |
| **TC-03** | Đứng yên (idle ≥ 60s) sau mutation | `UI thường` | Đã có PASS bounded (D1) | Không cần chạy lại nếu không regression |
| **TC-04** | Thay đổi slot & actual entry | `UI thường` / `Fixture gate` (actual entry) | Demo Scan đã chạy (D1) | Chạy UI demo nếu cần; actual gate chờ fixture AC-4 |
| **TC-05** | Confirm Cash → projection → remote UI | `Fixture cash cũ` | Đã có PASS pipeline (0 VND) | Chỉ chạy hồi quy khi cần; **không đóng AC-5/AC-6** |
| **TC-06** | Designate Backup & modal Release Backup | `UI thường` | Modal đã đạt live (L4) | Dùng/kiểm tra lại khi hồi quy hoặc làm nền cho F1 |
| **TC-07** | Ẩn/hiện tab thụ động (không có event) | `UI thường` | Đã có C3 PASS bounded | Không cần chạy lại nếu không regression |
| **TC-08 / F1** | **Commit Backup lúc Tab A ẩn → hiện lại tự bắt kịp** | **`F1 được duyệt`** | **OPEN — Ưu tiên số 1** | **Thực thi theo 6 bước mục 4 sau khi đủ readiness/baseline** |
| **TC-09** | Mất mạng tab-scoped (`Offline` → `Online`) | `UI thường` | Đã có C2 PASS bounded | Không cần chạy lại nếu không regression |
| **TC-10** | Bấm Retry khi JWT hết hạn tự nhiên | `UI thường` (race cần fixture) | Đã có L3 Retry thành công | Chạy khi quan sát hết hạn tự nhiên; failed refresh là TC-17 |
| **TC-11** | Facility unreachable và phục hồi | `Engineer` điều phối | Đã có PASS bounded (D1) | Không tự chạy lại nếu không regression/build mới |
| **TC-12** | Sign Out, đổi account & session fencing | `UI thường` (race cần fixture) | Sign Out B đã PASS (D1) | Kiểm tra account-switch nếu có account thay thế được cấp |
| **TC-13** | Giữ draft, assets (`.glb`) & tách biệt resource | `UI thường` | Kiểm tra theo scope mới | Chạy trong đợt hồi quy UI |
| **TC-14..20** | Same-slot reuse, burst, load ≥ 60s, failed refresh, 503, stale, heartbeat loss | `Cần owner/fixture` + `Engineer` | **OPEN — Chờ duyệt fixture** | **Dừng trước bước thực thi**, lập kế hoạch & chờ duyệt fixture |

### Quy tắc cấm tuyệt đối (Không vi phạm khi test)

Không dùng raw-CDP workaround, không bypass Turnstile, không ghi business state qua Console/`localStorage`, không chạy SQL/reset DB live, không replay payment, không tự tạo endpoint/package hoặc thay cấu hình toàn stack. Không commit/push. Giữ `Realtime:SharedMultiSiteHubEnabled=false`.

---

## 3. Chuẩn bị môi trường & Hướng dẫn DevTools thực chiến

### 3.1. Thông tin & readiness trước khi mở ca test

1. **Ghi định danh phiên chạy:** Ghi `Run ID`, người chạy, ngày/giờ, timezone, worktree/branch/HEAD, build đang phục vụ FE/BE và thay đổi chưa commit liên quan. **Lưu ý:** `HEAD` không đủ định danh build khi worktree còn dirty; engineer ghi source hash/image provenance nếu có (không điền hash suy đoán).
2. **Thiết lập 2 tab trình duyệt (Tab A và Tab B):**
   - **Tab A:** Tab quan sát (`Lot Status & 3D Map` hoặc màn hình đang theo dõi).
   - **Tab B:** Tab thao tác mutation hoặc tab đối chứng (control).
   - Ghi browser/version, `account alias` (không ghi email/token), `role`, `site alias` và endpoint origin thực tế. Đăng nhập bình thường; người dùng tự hoàn thành challenge (nếu có).
   - Với ca cần hai session riêng (đặc biệt `TC-12`), mở từng tab/context và đăng nhập riêng bằng UI. Không dùng Duplicate tab/copy storage để coi như đã có hai session độc lập; engineer xác nhận scope phiên bằng metadata không chứa secret trước khi kiểm tra Sign Out isolation.
3. **Engineer kiểm tra readiness (read-only):** Kiểm tra Operations, Facility, PostgreSQL, RabbitMQ, và Payment (nếu ca cần cash); queue/binding/consumer và outbox delivery. Xác nhận flag hiện hành theo handoff:
   - Facility layout outbox/delivery: **bật**
   - Operations layout relay/outbox delivery: **bật**
   - Operations messaging: **bật** cho pipeline được duyệt
   - Shared multi-site hub (`Realtime:SharedMultiSiteHubEnabled`): **tắt (`false`)**
   - Không đổi flag từ hướng dẫn này. Không ghi secret/config dump.
4. **Chờ ổn định trước khi đo:** Chờ cả Tab A và Tab B đạt `connected` / `synced`, layout tải xong, số request đang chạy (`activeConcurrentReads`) về `0`. Ghi lại trạng thái slot/counter ban đầu, cấu hình cache/throttle của DevTools (ví dụ: có tick *Disable cache* hay không, *No throttling*), `visibility`/`online`, giờ UTC và metrics từng tab.

### 3.2. Bản đồ màn hình & nút bấm thực tế trên UI

Để không mất thời gian tìm nút, đối chiếu đúng tên trên giao diện Operator (`http://localhost:5173/dashboard/operation` ở môi trường dev; origins BE lấy từ môi trường đang chạy):

- **Thanh điều hướng bên trái (Sidebar):**
  - `Dashboard Overview` (tổng quan & 3D)
  - `Gate Check-In`
  - `Gate Check-Out`
  - `Lot Status & 3D Map`
- **Vị trí màn hình Quản lý Backup (`SlotManagement`):**
  - Không có mục riêng tên "Slot Management" trên sidebar.
  - Vào **`Lot Status & 3D Map`** → cuộn xuống phần **`Backup Slots & Maintenance`** (đang thu gọn mặc định) → bấm mở ra → chọn nút tab **`Backup Slots`**.
  - **Form tạo Backup** (`Designate Space as Operational Backup`): chọn slot ở `Select Vacant Operational Slot` → nhập lý do ở `Designation Reason / Purpose (Required)` → bấm **`Confirm Backup Designation`**.
  - **Bảng danh sách & nhả Backup** (`Current Backup Slots`): bấm nút **`Release Backup`** ở dòng slot cần nhả → mở hộp thoại **`Release Backup Designation`** → có nút **`Cancel`** và **`Confirm Release`**.
- **Banner & nút Retry khi có sự cố:**
  - Mất kết nối live (`unavailable`): hiện dòng *"Live updates are unavailable. Showing the last loaded parking layout."* kèm nút **`Retry live updates`**.
  - WebSocket vẫn nối nhưng lỗi tải snapshot (`connected` + `error`): hiện dòng *"Latest parking layout synchronization failed. Showing the last synchronized layout."* kèm nút **`Retry sync`**.

### 3.3. Hướng dẫn thao tác Chrome DevTools & thu thập metrics

1. **Bật ghi Network trước cửa sổ đo (cả Tab A và Tab B):**
   - Mở DevTools (`F12` hoặc `Ctrl+Shift+I`) ở **từng tab riêng**.
   - Vào tab **Network** → đảm bảo nút ghi màu đỏ đang bật (Recording) và **tick chọn `Preserve log`**.
   - Lưu HAR riêng cho Tab A và Tab B. Phải giữ **cả origin Operations và Facility**; không lọc một origin rồi kết luận tổng số request.
2. **Quan sát Tab A khi chạy ca ẩn tab (`TC-07`, `TC-08 / F1`):**
   - Có thể chọn DevTools `⋮` → **Dock side → Undock into separate window** để thử quan sát Console mà không chọn lại trang A. Undock hoặc cửa sổ bị che **không tự chứng minh** `document.visibilityState === "hidden"`.
   - Chuyển A sang background tab bằng thao tác bình thường và đọc `visibility` bằng snippet. Chỉ dùng snapshot có giá trị `"hidden"` cho mốc hidden; ghi mọi lần chọn lại trang A hoặc thay đổi visibility quan sát được. Không suy trạng thái liên tục từ hai snapshot thưa.
   - Nếu mở Console/DevTools làm A visible hoặc không lấy được hidden snapshot bằng cơ chế hỗ trợ, ghi giới hạn và để subcriterion thiếu evidence `UNVERIFIED`; không inject script/hook hay đổi expected để tạo PASS. Thiếu bằng chứng A hidden ở thời điểm commit thì chưa được PASS committed-while-hidden.
3. **Bật Offline chỉ riêng 1 tab (`TC-09`):**
   - Trong DevTools của Tab A → tab **Network** → bấm vào dropdown throttling (mặc định là `No throttling`) → chọn **`Offline`**. Không ngắt Wi-Fi hay mạng toàn máy. Nhớ trả về `No throttling` khi dọn dẹp xong.
4. **Xem và lưu WebSocket frames (`WS`):**
   - HAR thông thường **không chứng minh được tab đã nhận WS frame**.
   - Trong DevTools → tab **Network** → bấm nút lọc **`WS`** → chọn kết nối `.../ws/parking-lots/{siteId}/layout` → mở tab phụ **`Messages`** để chụp ảnh/ghi lại timestamp và nội dung frame (che JWT/token nếu có trước khi chia sẻ).
5. **Nhận diện nhanh các loại request trong Network tab:**
   *(Đường dẫn/port dưới đây là mặc định dev; luôn đối chiếu origin thực tế của môi trường test)*

   | Nhóm request | Đường dẫn tiêu biểu | Có tính vào Managed Read Budget? |
   |---|---|---|
   | **Facility layout reads** (mặc định `:5045`) | `GET .../{siteId}/operational-layout`; `GET .../{siteId}/operator-layout` là legacy fallback | **Có** nếu do coordinator khởi phát; ghi riêng retry/fallback thực tế |
   | **Structure / bootstrap reads** (Facility) | `GET .../{siteId}/structure`, assignments/model metadata và các reads khi khởi tạo | Không tự gán là managed chỉ theo URL; xác định initiator/call path và báo riêng nếu ngoài coordinator |
   | **Operations managed reads** (mặc định `:5055`) | `GET .../api/parking-sessions`, `GET .../api/reservations` | **Có** (tính vào budget) |
   | **Backup mutations** (Facility) | `POST` / `DELETE .../{siteId}/slots/{slotId}/backup` | Mutation (không tính là GET read) |
   | **SignalR / WebSocket** (Operations) | `POST .../ws/parking-lots/{siteId}/layout/negotiate`, WS `.../ws/parking-lots/{siteId}/layout` | **Không** (báo cáo tách riêng) |
   | **Khác** | `OPTIONS`, auth/refresh, Payment (mặc định `:5065`), assets/file `.glb` | **Không** (báo cáo tách riêng) |

6. **Lấy snapshot trạng thái qua Console (chỉ đọc):**
   Tester copy và dán đoạn sau vào Console của tab cần đo (đổi `label` tương ứng như `"A-before"`, `"B-before"`...). **Không** thay thế bằng hành động ghi storage, gọi mutation hay tự `fetch` tạo traffic:

```js
JSON.stringify({
  label: "A-before",
  utc: new Date().toISOString(),
  online: navigator.onLine,
  visibility: document.visibilityState,
  metrics: window.__smartparkRealtimeMetrics?.(),
  connection: document.querySelector("main[data-layout-connection]")
    ?.getAttribute("data-layout-connection"),
  sync: document.querySelector("main[data-layout-sync-state]")
    ?.getAttribute("data-layout-sync-state")
}, null, 2)
```

   - Nếu getter trả về `undefined` hoặc trang không có attribute `data-layout-*`, ghi `UNAVAILABLE`, không tự thêm hook.
   - 6 chỉ số trong `metrics`: `totalReadsStarted` (tổng lần bắt đầu đọc), `activeConcurrentReads` (đang chạy song song), `totalCoalescedInvalidations` (số event được gộp), `lastQueueDelayMs`, `maxQueueDelayMs`, `trackedResources`.
   - Lưu ý: Chụp Console thưa từng lúc không tự chứng minh được tốc độ tối đa theo từng giây; số đếm trên DOM cũng không thay thế được file capture HTTP.

### 3.4. An toàn evidence (Bắt buộc tuân thủ)

- **Tuyệt đối không đưa raw HAR vào Git/repo:** HAR gốc có thể chứa JWT, `connectionToken`, cookie, body và thông tin account/session — **kể cả khi dùng nút Export HAR (sanitized) của trình duyệt**.
- **Phân công xử lý:** Tester lưu file raw HAR ở thư mục riêng ngoài repo được nhóm quy định, sau đó chuyển cho Engineer chạy bước lọc/tạo bản chia sẻ và tính hash; **không sửa tay trực tiếp trong file JSON** dễ làm sót secret hoặc sai cấu trúc.
- **Quy chuẩn bản chia sẻ (allowlist):** Chỉ giữ lại tab/run, method, origin/path đã bỏ định danh nhạy cảm/query, thời gian start/end/timing, status/error, resource class và mã tương quan được phép. Bỏ toàn bộ headers, bodies, credentials, URL tokens; kiểm tra kỹ cả ảnh chụp màn hình và ảnh WS. Lưu fingerprint/hash, phép biến đổi và provenance.
- **Không giả lập dữ liệu:** Không xóa file raw để giả vờ không có secret. File HAR rỗng hợp lệ được ghi nhận đúng là rỗng; không bấm reload hay tạo request giả chỉ để file export có dữ liệu.

---

## 4. Các ca UI và tích hợp thường dùng (`TC-01` → `TC-13`)

**Nguyên tắc chung:** Mỗi ca phải ghi riêng baseline, thời điểm action (UTC), kết quả ở cả Tab A và Tab B, HTTP/metrics, dọn dẹp (cleanup) và verdict riêng. Không chuyển sang ca tiếp theo khi thao tác (mutation) ở ca hiện tại chưa rõ thành công hay thất bại.

---

### TC-01 — Assigned scope và quyền truy cập `[UI thường | Cần owner/fixture cho negative]`

- **Các bước thực hiện:**
  1. *(Positive)* Đăng nhập bằng tài khoản Operator hợp lệ, mở site được phân công. Quan sát dữ liệu hiển thị và log REST/WS.
  2. *(Negative — chỉ chạy khi identity owner cấp tài khoản/site không có quyền)* Đăng nhập bằng tài khoản đó hoặc truy cập site không được phân công. Không tự cấp quyền, tạo account hay đoán site ID để thử trên môi trường khác.
- **Kỳ vọng đạt:**
  - Positive: Chỉ thấy dữ liệu đúng phạm vi site được phân công; cả REST và live update đều tuân thủ quyền.
  - Negative: Server từ chối REST và subscription (hoặc không phát dữ liệu ngoài quyền theo contract). Lưu ý: `negotiate` trả về `200` tự nó **không** chứng minh được quyền membership hay nhận dữ liệu.
- **Bằng chứng & Lưu ý:** Endpoint/channel, role/scope alias, kết quả server và UI đã che secret.

---

### TC-02 — Initial load và chuyển màn hình `[UI thường]`

- **Các bước thực hiện:**
  1. Tab B đứng yên tại `Dashboard Overview`.
  2. Trên Tab A, lần lượt bấm chuyển và **chờ từng màn hình tải hoàn tất** theo thứ tự: `Dashboard Overview` → `Gate Check-Out` → `Lot Status & 3D Map` → `Gate Check-In` → `Gate Check-Out` → `Lot Status & 3D Map`.
  3. Lấy Console metrics trước và sau chuỗi chuyển màn hình ở cả 2 tab.
- **Kỳ vọng đạt:**
  - Không có vòng lặp GET theo từng slot, không có cặp initial request trùng sát nhau.
  - Resource đang tải dở (`in-flight`) được dùng chung; dữ liệu còn mới trong cửa sổ reuse 2 giây có thể không cần gọi lại GET. Sau khi qua 2 giây hoặc sau mutation thật thì có thể gọi GET mới — không áp đặt cứng *"mỗi lần click phải đúng 1 GET"*.
  - Cùng site/session trong 1 tab chỉ dùng 1 hub kết nối (không mở hub theo từng component/slot); Tab A và Tab B có connection riêng là hợp lệ.
  - Không kéo lại toàn bộ layout chỉ vì tickets/notifications thay đổi hoặc event chỉ liên quan bookings.
- **Bằng chứng & Lưu ý:** Timeline thời điểm bắt đầu request (starts) theo từng action/resource/tab và chênh lệch (delta) Console metrics.
- **Tham chiếu lịch sử:** Run D2 trước đây ghi nhận 5 transitions / 5 session GET, 0 reservation GET trong 53.012s; đây chỉ là mức tham chiếu bounded navigation, không phải load benchmark hay cam kết giảm `6 → 5` dưới tải tương đương.

---

### TC-03 — Idle sau mutation `[UI thường]`

- **Các bước thực hiện:**
  1. Thực hiện một thao tác (mutation) hợp lệ, chờ cả Tab A và Tab B đồng bộ hoàn tất.
  2. Ghi mốc thời gian bắt đầu cửa sổ idle, sau đó **không chạm chuột/phím và không inject event** trên cả Tab A và Tab B trong ít nhất **60 giây**.
  3. Ghi Console metrics lúc bắt đầu và lúc kết thúc cửa sổ idle, kèm HAR.
- **Kỳ vọng đạt:**
  - Không phát sinh chuỗi managed layout/session reads lặp lại theo timer khi không có invalidation liên quan.
  - Phân loại rõ heartbeat/WS ping/auth hoặc request trình duyệt khác; không đòi hỏi cả trình duyệt tuyệt đối zero traffic.
- **Bằng chứng & Lưu ý:** Thời điểm start/end, số lượng request **starts** từng loại và Console metrics (không chỉ đếm request đã hoàn tất).
- **Tham chiếu lịch sử:** D1 đã PASS bounded với idle 264.258s không có session/reservation starts hoặc layout completions; **không cần chạy lại** ở chế độ tiếp tục nghiệm thu nếu không có regression.

---

### TC-04 — Thay đổi slot và actual entry `[UI thường / Fixture gate cho actual entry]`

- **Các bước thực hiện:**
  1. Chọn 1 slot hợp lệ (`AVAILABLE`, chưa bị actor khác giữ). Ghi lại slot alias và trạng thái trước thao tác ở cả Tab A và Tab B.
  2. Thực hiện đúng **1 luồng check-in** bằng UI được môi trường test hỗ trợ.
  3. Chờ đồng bộ và ghi nhận trạng thái sau thao tác ở cả 2 tab.
- **Kỳ vọng đạt:**
  - Actual entry có thẩm quyền làm trạng thái physical `OCCUPIED` phản ánh đúng slot trên cả 2 tab, không làm mất canvas 3D/góc camera hoặc dữ liệu đang nhập dở (draft) ngoài phần nghiệp vụ buộc đổi.
  - Không phát sinh request GET riêng theo từng slot hay fetch trùng lặp không giải thích được.
- **Bằng chứng & Lưu ý:** Ghi rõ nếu dùng *Scan Now / demo simulate* thì chỉ là *supporting demo pipeline*, **không** tự nhận là đã kiểm chứng nguồn actual gate/IoT của `AC-4`.
- **Dọn dẹp:** Dọn xe/session test theo đúng luồng được owner duyệt; **không tạo xe/session test khi chưa có cách dọn hợp lệ**.

---

### TC-05 — Confirm Cash → projection → remote UI `[Fixture cash cũ | Chỉ hồi quy khi cần]`

- **Chuẩn bị:** **Bắt buộc** chỉ dùng fixture test **0 VND** được owner xác nhận, với queue/binding/consumer Payment sẵn sàng trước khi tạo ca mới. **Nếu thấy phát sinh tiền thật hoặc phí ngoài fixture: DỪNG NGAY.**
- **Các bước thực hiện:**
  1. Trên 1 tab, bấm xác nhận thanh toán bằng UI **Confirm Cash** (không dùng endpoint direct checkout để thay thế thao tác UI).
  2. Quan sát session chuyển `Completed`, Facility cập nhật/nhả slot và tab đối chứng cập nhật qua live.
- **Kỳ vọng đạt:**
  - Checkout đã giải phóng slot không bị dữ liệu demo assignment cũ ép quay lại `OCCUPIED`.
  - Nếu slot đang có `BACKUP`, không tự ý xóa `BACKUP` chỉ vì physical về `AVAILABLE`.
  - Không thanh toán lại / replay payment để *"làm cho chạy"*.
- **Bằng chứng & Lưu ý:** Giữ mã tương quan được phép để nối các bước.
- **Giới hạn quan trọng:** Cash 0 VND → release đã PASS pipeline ở D1, nhưng **KHÔNG chứng minh `AC-5`/`AC-6`**. Code hiện tại đang có bước simulation departure tự động sau khi thanh toán. Yêu cầu gốc *"payment không đồng nghĩa với actual departure"* vẫn **OPEN**; phải báo riêng, không sửa kỳ vọng của AC để hợp thức hóa hành vi hiện có.

---

### TC-06 — Designate Backup và modal Release Backup `[UI thường]`

- **Chuẩn bị:** Tài khoản có quyền `SLOT_OVERRIDE` (hoặc `ADMIN`) trên site, có ít nhất 1 slot đủ điều kiện (`AVAILABLE`, unreserved).
- **Các bước thực hiện:**
  1. Trên Tab B, vào `Lot Status & 3D Map` → mở **`Backup Slots & Maintenance`** → tab **`Backup Slots`**.
  2. Chọn slot đủ điều kiện, nhập lý do, bấm **`Confirm Backup Designation`**. Quan sát Tab A và Tab B.
  3. Tại bảng **`Current Backup Slots`**, bấm **`Release Backup`** ở slot vừa tạo → kiểm tra hiển thị hộp thoại **`Release Backup Designation`**.
  4. Thử bấm phím **`Escape`** hoặc nút **`Cancel`**. Kiểm tra dialog đóng lại và không phát sinh mutation.
  5. Bấm **`Release Backup`** mở lại dialog → bấm **`Confirm Release`** một lần. Quan sát cả Tab A và Tab B.
- **Kỳ vọng đạt:**
  - Khi designate: cả Tab A và Tab B cập nhật trục allocation sang `BACKUP`, trục physical không bị đổi giả sang `OCCUPIED` (trạng thái physical, allocation và visual là 3 trục độc lập, không gộp làm một).
  - Modal `Release Backup Designation` hiển thị đè lên trên nội dung chính, không bị cắt xén (clip) hay che khuất; nhìn rõ và bấm được `Cancel` / `Confirm Release`.
   - Escape/Cancel không submit mutation; sau `Confirm Release`, cả 2 tab quay về baseline. Nếu kiểm tra Enter, ghi focus vào nút nào và hành vi bàn phím theo control đang focus; Enter kích hoạt Confirm đang focus không tự là lỗi.
- **Bằng chứng & Lưu ý:** Lưu ảnh chụp modal đang mở và vết mutation (provenance).
- **Tham chiếu lịch sử:** Modal portal đã được chấp nhận live ở L4.

---

### TC-07 — Ẩn / hiện tab thụ động, không có event `[UI thường]`

- **Các bước thực hiện:**
  1. Đảm bảo kết nối mạng bình thường (`online`), không chạy bất kỳ mutation nào.
  2. Đã tách (Undock) DevTools của Tab A nếu muốn đo Console lúc ẩn.
  3. Chuyển Tab A theo chu trình: `visible` → `hidden` (giữ một khoảng thời gian) → `visible`. Ghi nhận `visibilityState` thật và Console metrics ở các mốc trước/trong/sau.
  4. *(Nếu kiểm tra Tab B riêng thì ghi thành một ca tách biệt, không gọi Tab B là foreground control trong ca này).*
- **Kỳ vọng đạt:**
  - Không có managed reads **mới khởi phát** trong lúc Tab A đang `hidden` (nếu có request khởi phát *trước* khi ẩn rồi mới hoàn tất *trong* lúc ẩn thì phân loại riêng, không tính là lỗi).
  - Khi hiện lại (`visible`), tab ổn định, không lặp request vô hạn.
- **Bằng chứng & Lưu ý:** Ca không có event không chứng minh được khả năng bắt kịp sự kiện bị lỡ (*catch-up*).
- **Tham chiếu lịch sử:** C3 đã PASS bounded (Tab A reads `16 → 16`, Tab B `8 → 8`, 2 HAR rỗng 155 bytes); không dùng thay cho bằng chứng HTTP đầy đủ hoặc load test.

---

### TC-08 / F1 — Commit Backup lúc Tab A ẩn, khi hiện lại tự bắt kịp `[F1 được duyệt | Ưu tiên số 1]`

**Người phụ trách:** Người thực thi QA + Operator/Facility owner kiểm soát slot; Tech Lead xét bằng chứng.
**Phạm vi duy nhất được duyệt:** Đúng **1 lần Designate Backup** và **1 lần Release Backup** tương ứng qua UI hiện có; **không** tạo session, payment hay reservation mới.

**Chuẩn bị trước khi bấm giờ:**
- Chọn 1 slot `S` đang `AVAILABLE`, chưa bị đặt (`unreserved`), đủ điều kiện backup; đảm bảo không có actor/người dùng nào khác tác động vào `S`.
- **Tab A:** Mở sẵn trang `Lot Status & 3D Map` của site, đang `online`, `visible`, `connected`, `synced`, reads đã ổn định. Bật ghi Network + Preserve log; xác nhận cách quan sát hidden phù hợp theo mục 3.3 bước 2, không giả định Undock tự bảo đảm hidden.
- **Tab B:** Đang ở foreground (nhìn thấy trên màn hình), tài khoản có quyền override sẵn, mở `Lot Status & 3D Map` → `Backup Slots & Maintenance` → `Backup Slots`, chọn sẵn slot `S`, điền Reason là `SPARK244-F1-<run-id>` (**chưa bấm submit**). Bật ghi Network + Preserve log.
- Ghi baseline cả A/B, trạng thái cache/throttle của DevTools.

**6 bước thực hiện bắt buộc (đúng thứ tự):**

1. **Chụp mốc trước khi ẩn:** Chạy Console snippet trên Tab A với `label: "A-visible-before"` và trên Tab B với `label: "B-before"`. Ghi trạng thái slot/counter hiện tại ở cả 2 tab.
2. **Ẩn Tab A:** Chuyển A sang background tab bằng thao tác bình thường, giữ B foreground. Ghi action UTC và lấy snapshot `label: "A-hidden-before"` qua cơ chế hỗ trợ mà không chọn lại trang A; phải quan sát `"visibility": "hidden"`. Che kín cửa sổ không thay được kiểm tra này. Nếu thao tác đo làm A visible, ghi interval và chuẩn bị lại baseline trước B submit; nếu không xác minh được thì ghi blocker/evidence gap, không bấm mutation chỉ để hoàn thành checklist.
3. **Tab B submit Designate Backup:** Trên Tab B, bấm **`Confirm Backup Designation` đúng 1 lần**. Ghi trạng thái HTTP của lệnh POST, slot/kết quả đã commit, giao diện Tab B cập nhật thế nào, và event/correlation provenance (nếu có). *(Lưu ý: POST `200` hay Tab B nhận frame không tự chứng minh Tab A đã nhận frame).*
4. **Đo Tab A khi vẫn đang ẩn:** Đợi vài giây trong khi Tab A **vẫn đang `hidden`**, chạy Console snippet trên DevTools của Tab A với `label: "A-hidden-after"`. Kiểm tra Network của Tab A: **không được có managed reads mới khởi phát** trong suốt khoảng thời gian `hidden` (các request chạy từ *trước* khi ẩn mới xong thì phân loại riêng). Nếu muốn kết luận *"Tab A đã nhận invalidation lúc đang ẩn"*, **phải có matching WS frame trên Tab A** với timestamp/event identity khớp.
5. **Hiện lại Tab A:** Đưa Tab A trở lại `visible` **đúng 1 lần**. **Không** bấm `Retry live updates`, `Retry sync`, reload trang hay đăng nhập lại. Chạy Console snippet với `label: "A-visible-after"`.
   - **Kỳ vọng:** Tab A tự bắt kịp (catch-up) đúng trạng thái slot `S` và các counter giống hệt Tab B; không mất canvas 3D/model/draft. Ghi nhận GET starts/terminals, giải thích riêng các request reconnect/auth (nếu có), chỉ giữ trailing read khi có thay đổi thật liên quan.
6. **Dọn dẹp (Cleanup):** Sau khi đã lưu toàn bộ quan sát và HAR/WS của bước 1–5, trên Tab B bấm **`Release Backup`** → **`Confirm Release` đúng 1 lần** cho đúng slot `S` vừa tạo. Xác nhận cả Tab A và Tab B quay về trạng thái ban đầu (baseline). Khôi phục cấu hình DevTools. *(Nếu phát hiện actor khác đã đổi trạng thái `S` hoặc cleanup lỗi: **DỪNG NGAY**, bàn giao cho owner; không tự force release hay chạy SQL).*

**Bảng tóm tắt 4 nhãn Console snapshot cho Tab A (TC-08):**

| Thứ tự | Lúc chạy | `label` gợi ý | `visibility` bắt buộc |
|---|---|---|---|
| 1 | Trước khi ẩn Tab A | `"A-visible-before"` | `"visible"` |
| 2 | Ngay sau khi ẩn Tab A (trước khi B submit) | `"A-hidden-before"` | `"hidden"` |
| 3 | Sau khi B đã submit xong (A vẫn đang ẩn) | `"A-hidden-after"` | `"hidden"` |
| 4 | Ngay sau khi bật Tab A hiện lại & đã đồng bộ | `"A-visible-after"` | `"visible"` |

**Quy tắc chấm kết quả (Verdict) tách riêng cho TC-08:**
Chấm tách từng phần: (1) `committed-while-hidden → visible catch-up`, (2) `actual A frame arrival while hidden`, (3) `no hidden starts`, (4) `state preservation`, (5) `cleanup`. Nếu thiếu WS frame của Tab A thì phần arrival ghi `UNVERIFIED` — **không kéo các phần khác thành PASS toàn bộ `AC-8`**. Không suy ra latency p95 từ các snapshot thưa. F1 không phải load test.

---

### TC-09 — Mất mạng rồi có mạng lại (`Offline` → `Online`) `[UI thường]`

- **Các bước thực hiện:**
  1. Ghi baseline Tab A, Tab B và cấu hình throttle/cache. Giữ Tab B ở foreground để đối chứng.
  2. Trên DevTools của Tab A, chỉnh Network throttling từ `No throttling` sang **`Offline`** (chỉ phạm vi tab A; không tắt mạng toàn máy/stack). Ghi mốc thời điểm offline.
  3. Duy trì trạng thái `Offline` ít nhất **60 giây**. Quan sát giao diện và Network tab của Tab A.
  4. Trả Network throttling của Tab A về đúng baseline ban đầu (`No throttling`), ghi mốc thời điểm online. **Không** bấm nút Retry, không reload trang hay đăng nhập lại.
- **Kỳ vọng đạt:**
  - Lúc Offline: Tab A giữ nguyên layout đã tải trước đó, hiển thị banner báo mất kết nối live phù hợp, không spam managed read hay reconnect storm liên tục.
  - Lúc Online lại: Tab A tự kết nối lại và đồng bộ snapshot cần thiết.
- **Bằng chứng & Lưu ý:** Thu riêng negotiate và REST, ghi trạng thái UI/canvas, thao tác chính xác và Console metrics.
- **Dọn dẹp:** Đảm bảo Network của Tab A đã trả về `No throttling`.
- **Tham chiếu lịch sử:** C2 đã PASS bounded (offline 73.121s, reads `10 → 10` rồi 3 GET `200` / `+3` reads); do các snapshot khi đó đều ở trạng thái `hidden`, C2 chỉ là PASS bounded offline, chưa đóng toàn bộ lifecycle hay latency.

---

### TC-10 — Bấm Retry khi JWT hết hạn tự nhiên `[UI thường với expiry tự nhiên]`

- **Các bước thực hiện:**
  1. Dùng đúng thời hạn token thật của môi trường; **không** tự sửa token hay `localStorage`.
  2. Khi token đã hết hạn tự nhiên và live chuyển sang trạng thái `unavailable` (còn refresh token hợp lệ), bấm nút **`Retry live updates`** trên cùng trang đó.
  3. *(Nếu kiểm tra race condition khi refresh đang chạy dở mà tab bị ẩn/unmount/đổi user: cần delay fixture được duyệt, không tự đoán).*
- **Kỳ vọng đạt:**
  - Refresh token chạy thành công trước khi SignalR start lại, sau đó kết nối chuyển `connected` và layout về `synced`; không cần reload trang và không tạo reconnect storm.
  - Nếu tab bị `hidden`, unmount hoặc đổi tài khoản trong lúc refresh đang chờ (`pending`), không được start kết nối của phiên cũ; khi hiện lại hoặc sang tài khoản mới chỉ start đúng phạm vi hiện tại.
- **Bằng chứng & Lưu ý:** Không gọi PASS chỉ từ unit test. L3 Retry thành công đã được chấp nhận trước đó; trường hợp refresh *thất bại* (`401`) được tách riêng ở **TC-17**.

---

### TC-11 — Facility unreachable và phục hồi `[Engineer điều phối]`

- **Lưu ý quyền chạy:** Chỉ tái chạy khi cần kiểm tra regression hoặc build mới. Engineer phải xin phép phạm vi outage ngắn **đúng riêng service Facility** và chịu trách nhiệm khôi phục. **Không tự stop DB, RabbitMQ hay cả stack từ guide này.**
- **Các bước thực hiện:**
  1. Engineer tạm ngưng riêng Facility trong phạm vi được duyệt; tester quan sát Tab A.
  2. Engineer khôi phục Facility; tester bấm **`Retry live updates`** / **`Retry sync`** trên cùng trang đó (không reload).
- **Kỳ vọng đạt:**
  - Khi Facility ngưng: Tab A giữ dữ liệu cũ đã tải, hiển thị thông báo lỗi sync/live failure.
  - Khi Facility lên lại và bấm Retry cùng trang: trả về `HTTP 200`, trạng thái quay lại `connected` / `synced`, hết banner lỗi, không mất canvas 3D.
- **Bằng chứng & Lưu ý:** Lưu trạng thái health, HTTP/WS capture và kết quả cleanup. Lưu ý kỹ thuật: lỗi *network unreachable* **không phải** `HTTP 503`; ca Retry có reconnect/refresh token không thay thế được ca WS vẫn `connected` + `genuine 503` ở **TC-18**.
- **Tham chiếu lịch sử:** D1 đã có PASS bounded cho ca Facility unreachable → same-page Retry.

---

### TC-12 — Sign Out, đổi account và session fencing `[UI thường | Race cần fixture]`

- **Các bước thực hiện:**
  1. Trên Tab B, bấm **Sign Out** rõ ràng bằng UI. Quan sát Tab B và kiểm tra Tab A.
  2. *(Nếu có account thay thế hợp lệ do owner cấp)* Đăng nhập bình thường bằng account thay thế trên tab vừa Sign Out (không tự tạo account, không nới quyền, không bypass challenge).
- **Kỳ vọng đạt:**
  - Tab B trở về giao diện public, không phát thêm managed reads hay giữ subscription trái phép cho Tab B.
  - Nếu Tab A và Tab B dùng session riêng như cấu hình đã ghi, Tab A vẫn giữ nguyên phiên làm việc của mình (không tự áp đặt giả định global logout nếu sản phẩm không thiết kế vậy).
  - Khi đăng nhập account mới: chỉ hiển thị site/resource của account mới; cache cũ bị xóa/rào chắn (`fence`), mọi response/event của phiên cũ về muộn không được phép ghi đè lên màn hình mới.
- **Bằng chứng & Lưu ý:** Lưu timeline generation/scope alias, số reads và trạng thái UI; kiểm tra request budget trong lượt chuyển account riêng. Nếu không tạo được tình huống *late response* có nguồn gốc rõ ràng, đánh dấu riêng phần đó là `UNVERIFIED`.
- **Tham chiếu lịch sử:** Sign Out trên Tab B đã PASS bounded ở D1.

---

### TC-13 — Giữ draft, assets (`.glb`) và tách biệt resource `[UI thường]`

- **Các bước thực hiện:**
  1. Trên Tab A, giữ một form đang có dữ liệu nhập dở (draft) và khung hình camera/3D hiện tại.
  2. Trên Tab B, thực hiện một mutation hợp lệ cùng phạm vi site (ví dụ đổi trạng thái slot khác hoặc checkin/backup không liên quan form đang mở). Quan sát Tab A (UI + Network tab).
  3. Tiếp tục quan sát xem khi có event chỉ liên quan bookings hoặc khi chuyển site thật thì assets `.glb` và thông báo lỗi (nếu có) hành xử thế nào.
- **Kỳ vọng đạt:**
  - Không tải lại file mô hình 3D (`.glb`) chỉ vì có invalidation thay đổi trạng thái slot.
  - Không xóa mất dữ liệu draft trên form, tickets hoặc notifications tùy tiện.
  - Event chỉ liên quan bookings (`bookings-only`) không được tự xóa banner lỗi sync layout đang tồn tại; lỗi sync layout chỉ được xóa khi lượt tải layout tương ứng thành công.
- **Bằng chứng & Lưu ý:** Theo dõi riêng nhóm `assets` trên Network tab. Lưu ý: nếu người dùng chủ động chuyển sang site/resource khác thật thì việc tải asset `.glb` mới là hợp lệ, không quy kết nhầm là duplicate.

---

## 5. Các gate cần fixture/cơ chế được duyệt trước (`TC-14` → `TC-20`)

Không thể thay các ca này bằng cách tự sửa storage, bắn endpoint tùy tiện, dùng random event ID hoặc chụp ảnh thưa. Mỗi fixture proposal phải ghi rõ: **owner, entry point, scope/auth, sự kiện/commit thật hay synthetic loại nào, cách capture và cleanup**. Hiện toàn bộ các ca dưới đây vẫn **OPEN**.

| ID / Owner | Thao tác sau khi fixture được duyệt | Kỳ vọng và evidence bắt buộc |
|---|---|---|
| **TC-14** Same-slot reuse / Operations gate + FE | Tạo 2 session `A-old` và `B-new` có identity rõ, actual release/reuse qua cơ chế hợp lệ; delay event/response của `A-old` rồi giao sau khi `B-new` đang dùng slot `S`; quan sát cả hai trang đang render | Response/event cũ không được giải phóng slot hoặc gán biển số/session cũ lên `B-new`. Đối chiếu session/slot/event ordering thực, authoritative read và UI; helper test không thay live proof. Cleanup đúng session, không free generic `S` |
| **TC-15** Duplicate burst / Messaging + FE performance | Generator được duyệt đẩy cùng một invalidation nhiều lần; lưu actual FE arrivals và danh tính event/resource. Replay transport chỉ để test dedup, **không replay payment** | Không nhân bản effect/GET theo số lần duplicate; `0` read nếu đã xử lý hợp lệ hoặc read cần thiết nếu đang dirty, không áp cứng *"đúng 1"*. Nếu có distinct mutation thật phát sinh lúc `in-flight`, vẫn phải có trailing read để không mất update |
| **TC-16** Sustained distinct events / Facility + FE performance | Chạy liên tục **≥ 60 giây** (đề xuất **120 giây**) với các committed changes/identities thật theo fixture; đủ active keys để kiểm tra fairness và mức tải đủ thử giới hạn | Rolling starts **≤ 4 / 1.000 ms** và concurrent managed reads **≤ 4 mỗi tab**; hội tụ sau burst, không starvation, báo cáo queue delay theo resource/priority. Current metrics khớp HTTP; không dùng thao tác bấm Refresh hoặc lặp no-op thay cho distinct committed events |
| **TC-17** Failed refresh / Identity + FE | Fixture refresh trả `401`/hết hạn phiên có provenance rõ; kiểm tra lúc refresh đang pending kết hợp hidden/offline/unmount/đổi user/concurrent resume | Không start kết nối của phiên cũ; giữ đúng retry budget, không lặp vô hạn, báo `unavailable`/`expired` theo contract. Không làm hỏng token, tắt auth hay thu hồi account thật ngoài phạm vi; phân biệt lỗi mạng khác `401` |
| **TC-18** Genuine HTTP 503 / Facility + FE | Dùng cơ chế tab-scoped status/body interception được duyệt hoặc controlled service failure trong lúc WS **vẫn `connected`**; sau đó phục hồi cùng phiên sang `200` | Giữ layout cũ, hiện banner cảnh báo lỗi sync (`Retry sync`) dù WS đang `connected`; bấm Retry → `200` / `synced` và hết lỗi. Phải capture được **status `503` thật**; bật `Offline`/block request không phải `503`, sửa header không tự đổi HTTP status |
| **TC-19** Source stale/freshness / Projection + FE | Fixture trả response `isFresh=false`; sai định dạng (`malformed`) `generatedAt`/threshold; lỗi sync; snapshot healthy idle quá tuổi nhưng metadata hợp lệ và live `synced`; sau đó phục hồi bằng dữ liệu fresh hợp lệ | Stale thật hoặc metadata lỗi phải fail-closed (hiện cảnh báo), không bị trạng thái WS `connected` che giấu lỗi. Healthy idle hợp lệ không hiện banner stale giả; khi phục hồi phải xóa đúng lỗi của resource đó. Synthetic response chỉ chứng minh UI contract, không chứng minh real heartbeat loss |
| **TC-20** Source heartbeat loss / Source + projection owner | Ngắt nguồn heartbeat bằng cơ chế được duyệt, giữ WS nếu fixture yêu cầu; sau đó khôi phục projection có thẩm quyền | Đúng ngưỡng (threshold)/contract stale-dim và recovery snapshot, không chỉ dựa vào `connected` / `HTTP 200`. Ngưỡng mất nguồn lịch sử > 30s phải đối chiếu lại contract thực của fixture; không tự sửa metadata để gọi là source-loss integration |

### 5.1. Cách tính request budget đúng cho TC-15 / TC-16 `[Dành cho Engineer / QA Automation]`

- **Đếm thời điểm bắt đầu (`start`),** không dùng thời điểm hoàn tất (`completion`) để thay thế. Managed resource gồm các snapshot/layout, sessions, reservations được coordinator quản lý; xác định từ endpoint thực tế, không đoán bằng một substring duy nhất.
- **Giới hạn tốc độ (Rolling window):** Với mỗi tab, xét **mọi** cửa sổ trượt `[t, t + 1000ms)`, tổng số managed read starts phải **≤ 4**. Chia theo các ô 1 giây cố định (*fixed 1-second buckets*) có thể bỏ sót burst vắt qua ranh giới giây.
- **Giới hạn song song (Concurrency):** Lấy các khoảng `[start, end)` đang chồng nhau, quy ước xử lý `end` trước `start` nếu trùng timestamp; mọi request tính vào concurrency phải có trạng thái kết thúc (success / failure / abort). File HAR thiếu trạng thái kết thúc hoặc còn kẹt `loading` thì không đủ cơ sở khẳng định max concurrency.
- **Đối chiếu metric:** So sánh `delta totalReadsStarted` với số starts thực tế trên HTTP cùng tab/window/resource scope. Ghi tách riêng request *pre-window in-flight*, *after-window terminals*, *aborted/error* và *auth reads* nằm ngoài coordinator. `OPTIONS`, `negotiate`, WS và `assets` không cộng nhầm vào budget, nhưng vẫn báo cáo riêng trong tổng traffic.
- **Phân biệt job và HTTP:** Getter đếm lần coordinator bắt đầu resource job; không mặc định một job luôn bằng một HTTP GET. Production `readAssignedOperatorSite()` có thể fallback từ operational-layout không đầy đủ sang operator-layout; `readOperatorLayout()` có retry khi `409 STRUCTURE_BUSY`. Lập mapping job → từng HTTP attempt, không xóa những attempt này khỏi capture để ép delta bằng nhau. Ghi riêng budget coordinator và budget HTTP thực tế; retry/fallback không tự miễn tiêu chí HTTP ≤4 starts/s hoặc giải thích thay cho bằng chứng. Nếu không đối chiếu đủ, phần reconciliation/budget tương ứng vẫn `UNVERIFIED` hoặc `FAIL` nếu có vượt ngưỡng đã đo.
- **Báo cáo chi tiết tải:** Ghi arrival rate, danh sách/số lượng event ID thực, interval, số distinct resource, coalescing delta, queue delay và thời gian hội tụ. Lưu ý `maxQueueDelayMs` là giá trị lũy kế; không reset runtime tùy tiện hoặc lấy snapshot cuối để suy diễn phân phối fairness. Muốn khẳng định fairness phải có timeline cấp resource; thiếu công cụ đo thì phần đó ghi `UNVERIFIED`.
- **Ý nghĩa ngân sách:** Full-layout event reads được pacing/gộp; các lượt đọc initial/mutation cần phân loại rõ lý do. Không quy đổi trần budget `4 req/s` thành *"cho phép idle polling 240 request/phút"*.

---

## 6. Đối chiếu yêu cầu gốc — không bỏ sót vì tập trung performance

| AC gốc | Cách bao phủ / điều kiện còn lại |
|---|---|
| **AC-1** Assigned scope REST + WS | `TC-01`; phải có cả positive và negative với quyền hợp lệ |
| **AC-2** Requested khác allocated actual position | Fixture booking/allocation do owner cung cấp; UI không được biến vị trí requested thành vị trí đã phân bổ thực tế; ghi nhận cả hai nguồn |
| **AC-3** Physical / allocation / visual độc lập | `TC-04`, `TC-06`, `TC-13` và fixture kiểm thử đủ các trục; trạng thái `AVAILABLE` + `BACKUP` không được suy diễn thành `OCCUPIED` |
| **AC-4** Actual entry → occupied | `TC-04` với actual authoritative source; demo Scan chỉ là luồng hỗ trợ |
| **AC-5** Payment không tự release `OCCUPIED` | **OPEN nghiệp vụ:** cần quyết định của Operations/gate owner về simulation hiện có. Phải test kịch bản thanh toán xong nhưng chưa rời bãi (departure) thì slot vẫn phải còn `OCCUPIED` theo AC. Không dùng PASS của cash pipeline để đóng AC này |
| **AC-6** Actual departure release đúng một lần | Fixture actual departure + duplicate + same-slot reuse (`TC-14`); session cũ không được giải phóng session mới |
| **AC-7** Zone-only precision | Fixture chỉ có vị trí mức zone: UI không được tự bịa slot cụ thể hoặc vẽ halo chính xác. So sánh API precision và giao diện ở cả 2 tab |
| **AC-8** Freshness / heartbeat / reconnect | `TC-08`, `TC-09`, `TC-10`, `TC-18`, `TC-19`, `TC-20`; ca offline và ẩn tab thụ động chỉ là nhánh phụ (subcase), không đóng toàn bộ AC |
| **AC-9** Duplicate / replay / out-of-order | `TC-14`, `TC-15`, `TC-16`; kiểm tra cả thứ tự nguồn và trang đã render, không giật ngược trạng thái (flicker / backwards state) |
| **AC-10** Read-only 3D & BE mutation authority | Fixture negative hợp lệ gọi trực tiếp real endpoint/channel, không cho phép mutation trực tiếp từ tầng hiển thị; việc UI không vẽ nút bấm chưa chứng minh BE đã chặn đúng |

**Quy định về Latency:** Target lịch sử là **p95 ≤ 2 giây ở môi trường LAN dev** (tính từ Operations state change đến lúc client projection cập nhật xong); mốc recovery **≤ 3 giây** chỉ là đề xuất khác, chưa thay thế AC gốc. Tech Lead / Product owner phải chốt mốc neo (commit/state change), điểm kết thúc render trên cả hai tab, đồng bộ đồng hồ (clock alignment), kích thước mẫu và mức tải đại diện trước khi đo. Không dùng thời điểm `pre-commit outbox staging` làm thời điểm commit; không suy p95 từ 2 event lẻ hoặc ảnh chụp thưa. Chưa đo thì ghi `UNVERIFIED`, không tuyên bố production SLA.

---

## 7. Kiểm tra BE và tự động `[Dành cho Engineer — Tester nhận evidence]`

Đợt kiểm thử này là **tích hợp FE + BE**, không chỉ test riêng giao diện React. Tuyệt đối **không** chạy integration tests trên DB đang dùng cho demo hoặc người dùng thật. Engineer chuẩn bị PostgreSQL/RabbitMQ cô lập theo đúng project test, xác minh test connection trước khi chạy lệnh; không chạy SQL/reset DB live hay tự sửa hạ tầng từ tài liệu này.

### 7.1. Gate tự động cho phiên bản nguồn được kiểm tra

Chạy các lệnh sau từ repo root (đúng SDK/dependencies hiện có, không tự cài thêm package; build BE phải thành công trước khi chạy các lệnh `--no-build` để tránh chạy nhầm binary cũ):

```powershell
node --test tests/frontend/spark-244-corrective-l1-l5.test.cjs tests/frontend/request-scheduling.test.cjs tests/frontend/operational-layout-visual.test.cjs
npm --prefix FE run build
npx --prefix FE tsc -p FE/tsconfig.json --noEmit
dotnet build SmartParking.slnx --no-restore --nologo
dotnet test scripts/architecture/SmartParking.Architecture.Tests/SmartParking.Architecture.Tests.csproj --no-build --disable-build-servers
dotnet test tests/Services/SmartParking.OperationService.Tests/SmartParking.OperationService.Tests.csproj --no-build --disable-build-servers --nologo
dotnet test tests/Services/SmartParking.FacilityService.Tests/SmartParking.FacilityService.Tests.csproj --no-build --disable-build-servers --filter "FullyQualifiedName~FacilitySlice3WriterOutboxTests|FullyQualifiedName~FacilityLayoutOutboxTests|FullyQualifiedName~OccupancyProjectionRepositoryTests" --nologo
```

- **Biến môi trường cho Integration test:** Trỏ `SMARTPARK_STRUCTURE_TEST_CONNECTION` và `SMARTPARK_RABBITMQ_TEST_CONNECTION` về môi trường cô lập; **không in giá trị** connection string ra log/báo cáo. Kiểm tra các test attributes hiện tại trước khi chạy.
- **Baseline tham chiếu từ lần chạy trước (để đối chiếu, không copy làm kết quả mới):**
  - FE: **43/43** (`25` scheduling + `11` visual + `7` corrective), Vite build PASS; typecheck toàn dự án (`tsc --noEmit`) còn lỗi ngoài phạm vi — **không gọi là typecheck PASS toàn dự án**.
  - BE: từng ghi nhận architecture `12`, Operations `75`; nhóm Facility chọn lọc có **12 real integration tests bắt buộc 0 skip** (writer `5` + layout `2` + occupancy `5`), cùng `5` layout unit tests nếu discovery giữ nguyên.
  - Run mới phải ghi rõ số lượng discovery / pass / fail / skip thực tế và build hash mới, không ép số cũ thành kết quả mới. Nếu Docker offline → ghi `BLOCKED` / `UNVERIFIED`, không đánh dấu PASS khi test bị skip.

### 7.2. Nội dung BE phải được evidence bao phủ

| Gate | Nội dung kiểm tra |
|---|---|
| **Transaction / outbox** | Dữ liệu Facility và staged framework outbox nằm cùng transaction; khi rollback không để notification "thay đổi" lọt ra trước khi commit. Không thêm bảng outbox/receipt/sequence tùy biến để test |
| **Writer coverage** | Structure đổi thật → `structure` / `full-layout` / `capacity-summary`; maintenance đổi thật → `structure`; occupancy `parking_slots` đổi thật → `full-layout` / `capacity-summary`. Thao tác no-op không phát invalidation thừa; các store không ảnh hưởng served layout không spam |
| **Projection ordering** | Giữ nguyên receipt / watermark / idempotency / ack; duplicate hoặc event cũ không đảo ngược trạng thái; release/session identity cũ không tác động session mới; reservation state hiện hữu được bảo toàn |
| **Relay race** | Khi `LayoutRelayEnabled` bật: occupancy delta vẫn đi tới Facility qua RabbitMQ, chặn đường legacy direct SignalR delta trước commit; `SnapshotRequired` / bookings vẫn hoạt động bình thường |
| **Delivery / security** | Queue / binding / consumer / ACL đúng, lỗi transient có cơ chế phục hồi theo thiết kế hiện tại; không publish site ngoài quyền; không replay payment cũ làm fixture |

*(Lưu ý: Unit/mock test không thay thế được real PostgreSQL/RabbitMQ gate. Test UI 2 tab không thay thế được test rollback/watermark ở BE, và ngược lại BE PASS không thay thế được chứng cứ UI rendered same-slot reuse hay network budget ở FE).*

---

## 8. Thứ tự bàn giao và mẫu ghi nhận kết quả

### Thứ tự thực hiện & bàn giao

1. **Chuẩn bị:** Xác minh build và capture readiness; đọc lại accepted evidence để biết ca nào cần chạy lại.
2. **Ưu tiên số 1:** Thực hiện **`TC-08 / F1`** trước; ghi verdict tách riêng cho từng subcriterion và kết quả cleanup.
3. **Lập kế hoạch fixture:** Đề xuất/duyệt fixture cho **`TC-14` … `TC-20`** và các AC gốc còn thiếu; dừng đúng ca bị block, vẫn có thể chuẩn bị kế hoạch đọc-only.
4. **Chạy theo batch ngắn:** Thực hiện từng batch theo fixture đã được duyệt; đính kèm request accounting, clock/provenance và bằng chứng UI. Chỉ sửa code ứng dụng nếu tái hiện được lỗi mới, sau đó chạy regression tương ứng với phần thay đổi.
5. **Chốt gate:** Tech Lead quyết định closure hoặc defer cho từng tiêu chí. **Không** tự ý chuyển `UNVERIFIED` thành `DEFERRED` hoặc đóng toàn bộ task chỉ vì build/unit tests xanh.

### Quy tắc lưu trữ & trạng thái Verdict

- Mỗi run dùng một thư mục/manifest do owner thống nhất. Không copy lại toàn bộ lịch sử hoặc tạo một file handoff mới cho mỗi tấm screenshot. Giữ raw nhạy cảm ngoài repo; chỉ tham chiếu manifest và artifacts đã lọc.
- **Định nghĩa Verdict:**
  - `PASS`: Chỉ áp dụng khi tiêu chí có đủ bằng chứng theo yêu cầu.
  - `FAIL`: Bắt buộc kèm steps tái hiện, expected theo nguồn yêu cầu, actual, ảnh/capture và thông tin build.
  - `BLOCKED`: Ghi rõ nguyên nhân chặn (blocker) và owner chịu trách nhiệm gỡ.
  - `UNVERIFIED`: Ghi rõ còn thiếu bằng chứng gì.
  - `DEFERRED`: Chỉ dùng khi có quyết định phê duyệt rõ ràng gồm: tiêu chí, **tên người** nhận trách nhiệm (không chỉ ghi tên role chung chung), lý do, rủi ro còn lại (residual risk) và điều kiện nghiệm thu sau.

### Mẫu A — Thông tin chung của cả Run (Điền 1 lần đầu buổi test)

*(Các trường kỹ thuật như Source hash / BE image provenance có thể phối hợp với Engineer để điền)*

```text
Run ID / executor / date / timezone:
Source HEAD + dirty source hashes / served FE build / BE image provenance:
Environment / site alias / A-B browser + account-scope aliases:
Fixture approval / owner / allowed actions / baseline:
```

### Mẫu B — Phiếu kết quả cho từng Test Case (Điền cho mỗi TC)

```text
Case ID / criterion:
Actions UTC / visibility-online timeline:
Expected vs actual (A và B riêng):
HTTP starts + terminals / resource classification / metrics before-after:
WS event/commit/session correlation (allowed aliases):
Evidence paths + hashes / private raw owner:
Cleanup result / settings restored / outstanding test state:
Verdict: NOT RUN | PASS | FAIL | BLOCKED | UNVERIFIED
Limits / next owner:
```

---

### Điều kiện chốt (Closure criteria)

Từng gate áp dụng phải có `PASS` đủ evidence hoặc `DEFERRED` được chấp nhận rõ ràng; các AC gốc và hai quyết định mở (*payment vs actual departure*, *latency*) được xử lý minh bạch; fixture cleanup hoàn tất; phạm vi Owner/Driver vẫn tách riêng. **Tài liệu này không đóng Slice 3–4 hoặc SPARK-244.**
