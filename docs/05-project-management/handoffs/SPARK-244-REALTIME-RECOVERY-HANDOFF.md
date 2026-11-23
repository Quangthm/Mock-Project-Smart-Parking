# SPARK-244 — Handoff tổng hợp cải thiện realtime

Ngày hợp nhất: **2026-11-23, Asia/Saigon**. Worktree `SmartParking-244`, branch `feat/SPARK-244-realtime-monitoring`, baseline review `3b60787d06f3452fff276f622219de1659ee49d0`. Các review lịch sử dùng baseline cộng thay đổi chưa commit; bản bàn giao đã được user cho phép commit/push, xem mục11 để lấy commit code và kết quả kiểm tra mới. Tài liệu này hợp nhất nội dung điều hành từ audit và các handoff ngày 18–22/11. **Slice 3–4 và SPARK-244 vẫn OPEN.**

## 1. Bốn tài liệu duy trì trong worktree

- [Hướng dẫn tester FE + BE/integration](../../04-testing/test-plans/SPARK-244-realtime-recovery-tester-guide.md).
- Handoff tổng hợp này: scope, audit, quyết định, ma trận và acceptance hiện hành.
- [Checklist original Operator AC](2026-11-22-SPARK-244-OPERATOR-AC-CHECKLIST.md).
- [Active session](../../../ai/tasks/active/TASK-SPARK-244-SESSION-20261118.md): lịch sử và checkpoint tiếp tục công việc.

Evidence, manifest và bản gốc audit/handoff được giữ ngoài worktree theo yêu cầu của user, tại `D:/My file/FA26 - OJT FSoft/Report/SPARK-244-realtime-recovery-2026-11-23/`. [Manifest và các bản gốc](<../../../../../Report/SPARK-244-realtime-recovery-2026-11-23/2026-11-23-SPARK-244-DOCUMENT-INVENTORY-AND-CONSOLIDATION.md>) ghi từng nguồn, hash và đường dẫn chuyển. Link Report phụ thuộc bố cục máy hiện tại; clone repo ở máy khác cần nhóm cấp thư mục Report tương ứng. Không nhúng lại artifacts vào Git.

<a id="scope"></a>
## 2. Yêu cầu và ranh giới

SPARK-244 gốc phục vụ Operator US-O03: xem layout/slot và hoạt động hiện tại của site được giao, nhận cập nhật có thẩm quyền gần thời gian thực. Theo dõi assigned scope, requested khác allocated position, physical/allocation/visual axes riêng, entry/departure, freshness, dedup/order và read-only presentation. Mười original AC được kiểm soát tại checklist, không được thay bằng engineering budget.

Nguồn: [US catalogue](../../01-requirements/use-cases/user-stories/catalogue.md), [SRS v1.1](../../01-requirements/srs/smartpark-srs-v1.1.md), [operations rules](../../01-requirements/business-rules/05-sessions-operations.md), [delta design G1–G6/Q4](../../03-design/api/3d-digital-twin-spark-244-delta-design.md). [Thiết kế lịch sử](../../archive/api/3d-digital-twin-spark-244-api-design.md) §12/Test Suite1B là trace gốc, không phải hướng dẫn áp dụng lại service names/wire contracts cũ.

Owner/Driver và shared multi-site từng được đề xuất trong kế hoạch mở rộng, **chưa được chấp nhận triển khai/rollout bởi nghiệm thu Operator**. Giữ `Realtime:SharedMultiSiteHubEnabled=false`. Không dùng tài liệu hợp nhất để mở rộng nghiệp vụ/quyền truy cập hoặc thay runtime.

Hai quyết định bắt buộc vẫn OPEN:

1. **Payment không đồng nghĩa actual departure.** Pipeline hiện có `PaymentSucceededConsumer -> SimulatePaidGateDepartureCommandHandler` mô phỏng departure rồi release. Cash→release PASS chỉ xác nhận plumbing; không đóng AC-5/6. Operations/gate business owner + Tech Lead phải đối chiếu BR-GATE-02 và chốt fixture payment-only/actual-departure. Không tự sửa financial/physical lifecycle trong đợt realtime này.
2. **Latency:** original target p95 ≤2 giây ở LAN dev khác recovery ≤3 giây được đề xuất. Chưa sửa AC, chưa có phân phối p95 được nghiệm thu. Product/Tech Lead cần chốt anchor state-change/commit, endpoint rendered trên hai tab, clock alignment, tải và samples. Pre-commit StageAsync time và snapshot thưa không chứng minh commit-to-render p95.

<a id="audit"></a>
## 3. Audit: từ request storm đến cơ chế hiện tại

Audit 18/11 phân biệt hai baseline: tip cũ `bdac777b` có reconnect không giới hạn/default transport fallback và refresh amplification; baseline sau đồng bộ đã có containment `a504739a`. Không suy nguyên nhân deployed HTTP403 hoặc đúng 500 request/phút chỉ từ source audit.

| Vấn đề lịch sử xác định trong source | Cải thiện và giới hạn |
|---|---|
| Startup/close tự reconnect, fallback có nhiều negotiate/GET/POST/DELETE/OPTIONS; remount có thể tạo budget mới | WebSocket-only có retry budget/cooldown, phân loại lỗi, connection lifetime chia sẻ theo session/site/tab; revalidate sau async boundaries. Không gọi mọi request đó là GET riêng từng slot |
| Mỗi notification tăng refreshKey, kéo cả dashboard/site resources, effect site revision và refresh định kỳ khuếch đại | Resource-scoped invalidation và coordinator, bỏ global refresh trigger ở các Operator consumers đã migrate. Không polling snapshot layout khi idle |
| Long Polling context có thể mất route values; parsing route-only báo INVALID_SITE_ID | Transport/auth/site validation phải xét code/runtime thực; không tắt auth/CORS để chữa symptom. Query JWT trên browser WS không được log |
| Legacy direct SignalR báo trước Facility commit; một số writer thiếu invalidation | Framework Bus Outbox cùng transaction Facility, publish sau commit qua relay; delta direct cũ bị chặn khi relay bật |
| Mọi subscriber loader chạy dù cùng resource, sync error bị nuốt, stale session overlay | Single fetcher per key, explicit read outcomes, giữ layout cũ + warning; Facility physical state có thẩm quyền và session identity fencing |

400–500 request/phút là symptom user báo; không phải số đo benchmark trước/sau cùng điều kiện. Fix initial-read duplication sau đó có red→green và bounded navigation riêng, không biến thành kết luận “6 giảm còn5” dưới tải tương đương.

## 4. Luồng triển khai và invariants

```text
Operations business event / Facility writer hợp lệ
 -> Facility data + framework outbox cùng EF-owned transaction
 -> commit -> RabbitMQ -> authorized Operations relay -> SignalR invalidation
 -> FE đánh dấu resource dirty -> shared coordinator lập lịch REST snapshot
 -> authoritative data -> React/ThreeJS và các consumer cùng resource
```

- Raw Npgsql enlist vào connection/DbTransaction do `ParkingDbContext` sở hữu qua `FacilityOutboxTransaction`; framework MassTransit Bus Outbox là cơ chế capture. Không thêm bảng outbox/receipt/sequence tự chế. Helper không chứng minh arbitrary nested transaction ngoài contract đang kiểm thử.
- Operations giữ authority nghiệp vụ; Facility phục vụ layout/projection. Event là hint đọc lại đúng resources, không dùng payload cũ để override snapshot tùy tiện.
- Một tab chia sẻ connection/resource theo site + identity/session/role/filter phù hợp; hai tab có connection riêng. Một resource chỉ một fetch in-flight, gộp duplicate và giữ một trailing read khi có distinct mutation thực sự.
- Initial read còn in-flight được reuse; dữ liệu synced/không dirty đủ mới có cửa sổ reuse 2.000ms. Không bỏ trailing read đã queue bởi thay đổi thật.
- Engineering budget mỗi tab: rolling ≤4 read starts/1.000ms, ≤4 concurrent managed reads; full-layout event pacing 2 giây. Đây là trần khi có tải, không cho phép idle polling 240 lần/phút.
- Purge auth giữ ownership permits của unfinished reads/start-window, đồng thời fence generation/cache epoch để old response không ghi data. Revalidate hidden/offline/stopped/subscribers/generation/disconnected/starting/budget sau refresh-token/shutdown async boundaries.
- Giữ data/modelUrl/draft khi lỗi sync. WS connected không tự đồng nghĩa data synced; lỗi HTTP503/isFresh=false/metadata lỗi vẫn phải cảnh báo. Healthy idle chỉ miễn age-only warning khi metadata hợp lệ, source fresh, connected/synced và không sync failure.

<a id="matrices"></a>
## 5. Writer Coverage Matrix và Production-Consumer Migration Matrix

### 5.1 Writer coverage

| Writer/path | Served resources / nguyên tắc | Trạng thái bằng chứng |
|---|---|---|
| Gate claim/release, `OperatorGateSlotRepository` + handlers | Slot transition thật + Bus Outbox cùng Facility transaction; layout/capacity | Slice2 đã được chấp nhận theo phạm vi run lịch sử; không thay actual-departure AC |
| `PostgresParkingStructureRepository` create/edit | structure, full-layout, capacity-summary; semantic no-op không phát thừa | Code + isolated tests trong bàn giao Slice3; giữ commit/rollback/production-DI proof theo đúng run |
| `PostgresMaintenanceWindowStore` create/extend/cancel | structure khi served structure thực sự đổi; operational-layout không đọc maintenance metadata | Same transaction helper; không phát full-layout thừa |
| `OccupancyProjectionRepository` | full-layout, capacity-summary với original eventId nếu parking_slots đổi; receipt/watermark/release ledger cùng transaction | Idempotency/ack/order/reservation-state phải được giữ; real integration và UI reuse là hai gate khác nhau |
| `ReservationDependencyRepository` | Bookkeeping hold/abort/complete không tự đổi served layout/capacity | Không phát invalidation thừa; source exclusion không phải chứng nhận mọi mutation tương lai |
| `StructureOperationStore`, `PostgresStructureSynchronizationRepository` | Coordination/audit/locks; actual writes thuộc structure repository | Scope exclusion giữ nguyên |
| `OperatorOperationsStore`, `FacilityDirectoryStore`, `OwnerOnboardingStore` | Không có thêm agreed Operator served-slot writer cần stream thứ hai trong scope đã review | Không suy ra Owner/Driver hoàn tất |
| `OccupancyOutboxWorker` | Relay bật: delta vẫn publish RabbitMQ cho Facility, không direct SignalR trước commit; SnapshotRequired/bookings giữ luồng phù hợp | Worker regression và scoped runtime provenance được lưu riêng |

### 5.2 Production consumers

| Consumer | Cách dùng resource hiện tại | Acceptance còn phân biệt |
|---|---|---|
| `OperatorDashboard` / 3D / live transport | Shared initial/connect/reconnect/layout; sync outcome riêng connection state | L2 mount/navigation accepted bounded; load và original AC chưa đóng |
| Bookings/financial sessions | Key scope/site/filter; tickets/notifications tách khỏi layout refreshKey | Sessions GET cần thiết theo action phải giải thích; bookings-only không reset layout error |
| `useLotStatus` | Shared layout/session; scan/checkout invalidation; authoritative Facility axes | AVAILABLE thắng stale demo overlay; immediate reuse/late response rendered pages còn OPEN |
| `SlotManagement` | Shared invalidation thay global refresh; Release modal portal | Cancel/Escape/Confirm live đã được chấp nhận trong run nêu dưới |
| `OperatorBackupPanel` | Shared layout reconciliation | F1 hidden commit/catch-up là next gate, không tự xem backup modal PASS là catch-up PASS |
| `CheckInOut` + nested `CashierPOS` | Shared sessions/history/lookup; local action invalidation; giữ payOS restoration | Cash0 UI pipeline có bounded PASS; nghiệp vụ payment/departure không được chứng nhận |
| Owner/Driver và shared components | Render snapshot; không socket/HTTP per mesh/slot; multi-site extension chưa bật | Chưa được nghiệm thu bởi Slice4 Operator, shared hub=false |

## 6. Quyết định review và corrective history

Tóm tắt dùng để resume; từng finding/test chi tiết và approval ở bản gốc ngoài Report. Quyết định mới nhất trong handoff này được ưu tiên cho **trạng thái hiện tại**; approval runtime có phạm vi/ngày cụ thể trong lịch sử không phải quyền chạy lại tùy ý.

| Chặng | Kết quả / invariant giữ lại |
|---|---|
| Slice0–1 | Chốt contract/baseline; sửa budget reset quá sớm, foreground cooldown probe, start completion làm mất pending acceptance, swallowed sync errors. Connection success không tự chứng minh cross-client mutation |
| Slice2 | Production-DI framework outbox, rollback atomicity và real broker thay test insert OutboxMessage thủ công; separate relay khỏi payment enablement; scoped rollout có điều kiện |
| Slice3–4 R1/R2 | R1 CLOSED offline: permits thuộc từng unfinished request, purge không reset budget; late completion fenced. R2 targeted type extraction accepted offline, không blanket chứng nhận mọi C# file |
| L1–L5 | Authoritative slot state + checkout matcher; sessions dedup; refresh-token Retry; modal portal; healthy-idle freshness. Offline tests không thay browser gates |
| C1–C3 corrective review | Production-module regressions thay bản copy helper; revalidate connection sau awaits; malformed freshness metadata fail-closed. Nhãn corrective C1–C3 **khác** evidence capture C1/C2/C3 ở mục7 |
| D1 cleanup follow-up | Identity-aware cleanup giữ session mới dùng cùng slot; offline closed. Live reuse + delayed old response vẫn OPEN |
| L2 subsequent fix | `invalidateOperatorParkingSessions(..., initial)` reuse in-flight hiện hữu trước mọi invalidate; red→green, giữ real-event trailing read. Bounded navigation accepted |

Framework outbox SQL fingerprint lịch sử: `D411776B9CA614AF6407753510E4FB2A5201460E1FEE7CFF66096EF3B8F84EE4`; là provenance cũ, không xác minh lại schema live trong lượt hợp nhất. Không apply SQL/reset DB. Scoped operation/facility rebuild hoặc Operations-only messaging enablement được ghi ở run được phép; không thay biến messaging toàn stack.

<a id="acceptance"></a>
## 7. Acceptance hiện có — tách provenance

| Nguồn | Phạm vi chấp nhận | Không suy ra |
|---|---|---|
| Direct execution summary | UI cash0→Completed→Facility release→remote48/0; idle264.258s; Facility unreachable→same-page Retry200/connected/synced; B Sign Out public, A giữ session riêng | Nonzero fee, AC5/6 compliance, connected503, global logout, load/fairness |
| D2 direct navigation | Window14:10:21.468Z–14:11:14.480Z,53.012s;5 verified transitions/5 session starts,0 reservation starts, không adjacent duplicate; L2 bounded accepted | Capture server tổng hợp không tự per-tab network attribution; không comparable6→5 benchmark; không saturation |
| Capture C1 user manual | A5 managed GET/+5 metrics; B2/+2; hai API origins/timing hữu hạn | Không event load/failed terminal coverage; native automation vẫn chưa được capability/policy chấp nhận |
| Capture C2 user manual | Offline73.121s,0 starts/reads10→10; negotiate200+3GET200/reads10→13; sau đó online/connected/synced | Tất cả snapshots hidden; thiếu B-before/cache baseline/exact restore; UI retention/noRetry là user testimony; không full lifecycle/p95 |
| Capture C3 user manual | Passive visible→hidden→visible A/B; hidden sample separations47.535s/23.830s, reads16/8 giữ nguyên, cuối visible | Hai HAR rỗng155bytes/pages0/entries0 không chứng minh capture đầy đủ; không hidden mutation/catch-up; B cũng hidden riêng |
| Implementing-agent runs | L4 portal và L3 positive Retry đã được review chấp nhận trong phạm vi minh chứng; broader reports giữ provenance riêng | Không nhập toàn bộ PASS của run khác vào direct/C1/C2/C3; deterministic fixture/CDP claims cần review riêng |

C2 corrected HAR thay export nhầm trong **một** run. C3 hai export có hash giống nhau vẫn là hai artifacts theo provenance; không xóa vì duplicate hash. Không có defect application mới được xác định chỉ bởi ba batch capture C1/C2/C3.

Automated baseline **đã ghi nhận ở run trước**, không rerun do consolidation: FE43/43 (25 scheduling +11 visual +7 corrective), Vite build PASS; project-wide tsc không green vì lỗi ngoài changed adapter. R1/R2 review cũ ghi22 rồi follow-up25 scheduling; số43 là baseline sau corrective, không rewrite kết quả lịch sử. BE run báo architecture12/Operations75 và Facility selected17 gồm12real+5unit; real12 gồm writer5/layout2/occupancy5 phải0skip. Reviewer-interrupted discovery cũ không gọi PASS. Dùng report/run/source provenance trong Report khi tái sử dụng; guide ghi lệnh cho run mới.

<a id="gates"></a>
## 8. Các gate còn OPEN và owner

| Gate | Owner chức năng / bước tiếp | Giới hạn hiện tại |
|---|---|---|
| Hidden committed event → visible catch-up | Facility/Operator owner + executor; **F1 được duyệt** tại mục9 | Chưa chạy bởi consolidation; actual hidden frame arrival cần evidence riêng |
| Same-slot reuse + late old event/response | Operations/gate + FE lead; trình fixture hai-session/identity/delay được hỗ trợ | Không Console/localStorage business writes; helper tests không thay rendered proof |
| Duplicate burst; sustained distinct ≥60s | Facility messaging + FE performance; trình generator entrypoint/auth/commit ledger/arrival capture/cleanup | Không random event IDs/no-op/manual Refresh/payment replay; rolling≤4/s và concurrency≤4 mỗi tab, fairness/queue delay/HTTP metrics reconciliation chưa đóng |
| Alternate-account replacement | Identity owner + QA; account hợp lệ và UI login/challenge bình thường | Không tự provision account/nới quyền; late-response evidence riêng |
| Failed refresh401, genuine HTTP503, source isFresh=false | Identity hoặc Facility/FE owner tùy case; response mechanism có documented support/tab scope trước thực thi | Offline/block không phải503; synthetic stale UI contract khác real heartbeat loss |
| Full AC8/source heartbeat loss | Source/projection owner + FE | Passive visibility/offline không chứng minh freshness source/dim/catch-up đầy đủ |
| Original AC còn lại/payment vs departure/latency | Product/Operations gate + Tech Lead | Checklist vẫn kiểm soát riêng, không sửa yêu cầu theo behavior simulation |

Không criterion nào tự thành DEFERRED. Cần tên accepting owner, criterion, lý do, residual risk và điều kiện kiểm chứng sau. Các role ở bảng là accountable area, chưa phải cam kết cá nhân nhận lịch. Không lặp các gate cash/backend/idle/outage/Sign Out đã PASS nếu không có relevant regression/build/config thay đổi cần kiểm tra.

<a id="f1"></a>
## 9. Next approved batch: F1

Đọc chi tiết [TC-08 trong tester guide](../../04-testing/test-plans/SPARK-244-realtime-recovery-tester-guide.md). Phạm vi được giữ nguyên từ review 22/11:

1. Một assigned demo site, một slot S AVAILABLE/unreserved/eligible; B có quyền override hiện hữu. Ghi baseline A/B và source/build provenance; không cho actor khác dùng S. B chuẩn bị form Backup với reason `SPARK244-F1-<run-id>`, chưa submit.
2. Native Chrome user manual DevTools/UI là route đã chứng minh; agent hướng dẫn/phân tích local artifacts. Không raw-CDP workaround. Capture riêng A/B origins, starts+terminals, current getter, visibility/online UTC, cache/throttle baseline; WS messages giữ riêng đã lọc secret.
3. A ở Lot Status/3D chuyển hidden, B foreground control. Không âm thầm foreground A để lấy snapshot. B designate Backup một lần, ghi successful command và committed slot/result/event provenance.
4. A hidden không khởi phát managed read mới; pre-hidden read completion phân loại riêng. Chỉ matching A WS frame trong hidden interval mới chứng minh A nhận event lúc hidden; B frame/POST200 không thay thế.
5. A visible tự catch-up đúng S/counters trên cả hai rendered pages, không Refresh/Retry/reload/re-login. Giữ model/draft; giải thích reconnect/auth/trailing reads. Phần catch-up có thể PASS riêng dù hidden frame arrival UNVERIFIED; không suy p95 từ sparse snapshots.
6. Sau observations, B release đúng designation F1 bằng UI một lần, xác nhận baseline cả hai. Nếu S thuộc actor/session khác, stop; cleanup fail bàn giao owner, không force clear/SQL. Restore tab settings về baseline.

F1 không tạo session/vehicle/payment/reservation, không load generator, không sửa runtime hoặc phát triển endpoint/package. Lưu run mới dưới Report evidence, không thêm file report vào branch. Không fix code nếu chưa có defect mới tái hiện.

<a id="evidence"></a>
## 10. Vị trí evidence và nguồn lịch sử

- [Direct execution và C1/C2/C3](<../../../../../Report/SPARK-244-realtime-recovery-2026-11-23/evidence/spark-244-final-gates-codex-2026-11-22/execution-summary.md>).
- [Resume checkpoint của direct run](<../../../../../Report/SPARK-244-realtime-recovery-2026-11-23/evidence/spark-244-final-gates-codex-2026-11-22/resume-checkpoint.md>).
- [Manifest: exact paths/hashes, archived audit/handoffs và hai implementing-agent evidence roots](<../../../../../Report/SPARK-244-realtime-recovery-2026-11-23/2026-11-23-SPARK-244-DOCUMENT-INVENTORY-AND-CONSOLIDATION.md>).

15 bản gốc (14 handoff ngoài checklist +1 audit) được bảo toàn dưới `source-documents/` cùng original relative paths, để kiểm toán full wording/approvals và các link lịch sử. Chúng là lịch sử, không phải runtime instructions hiện hành. Link cũ trong historical session hoặc report được hiểu qua manifest/path relocation; không sửa bản gốc để giả lập nội dung đã khác khi thực hiện run.

Lượt hợp nhất chỉ thay tài liệu/vị trí lưu. Không chạy lại UI/backend gates/build/tests, không thay DB/runtime/code, không commit/push. Closure vẫn cần gate-by-gate acceptance hoặc defer rõ ràng; **Slice3–4 và SPARK-244 OPEN**.

## 11. Bản bàn giao GitHub cho tester — 2026-11-23

User đã cho phép commit/push toàn bộ thay đổi. Các câu không commit/push ở mục lịch sử mô tả giới hạn của các lượt trước, không phải trạng thái hay yêu cầu của lượt bàn giao này.

| Commit code/test | Phạm vi |
|---|---|
| `de4e2ba8` | Facility transactional outbox/writer coverage, Operations relay/auth/hub và compose flags/SQL reviewable |
| `1536d2b5` | Operator shared coordinator/resources, lifecycle/auth fencing, corrective UI và request dedup |
| `a96aa968` | Thay đổi Driver đã có trong worktree: reconcile layout của selected-site khi mở details; không bật shared multi-site hub hoặc chứng nhận Owner/Driver realtime adoption |
| `0b6b6afe` | Regression tests FE/BE, test harnesses và doubles |

Commit tài liệu nằm sau bốn commit trên. Tester ghi full HEAD sau khi pull branch để định danh toàn bộ bản bàn giao, rồi engineer xác nhận FE served source và BE images tương ứng. Việc push không tự rebuild môi trường Docker hoặc apply SQL. Các compose flags layout capture/relay mặc định vẫn tắt để rollout có kiểm soát; engineer chuẩn bị môi trường được duyệt trước khi tester kỳ vọng realtime chạy. Shared hub giữ false.

Kết quả kiểm tra mới trên source code bàn giao (không phải live acceptance):

- FE scheduling/corrective/visual: **43 passed, 0 failed, 0 skipped**. Vite build: **PASS**, 1344 modules; còn cảnh báo chunk size/dynamic import.
- Solution build: **PASS**, 0 errors/12 package advisory hoặc vulnerability-data warnings. Lần sandbox trước dừng 0 errors; lần chạy lại ngoài sandbox thành công, không sửa dependency để che cảnh báo.
- Architecture: **12/12 PASS**. Operations: **75/75 PASS**. Facility offline filter `Category!=Postgres&Category!=RabbitMQ`: **210 passed, 4 skipped, 0 failed**; bốn occupancy Postgres facts cũ thiếu category vẫn bị skip khi test connections trống. Không gọi đây là full real-integration PASS.
- Startup loopback: **3/3 PASS** ngoài sandbox. Trong sandbox trước đó ba health probes timeout; .NET tests bị testhost connection timeout. Các attempt đó không được ghi thành PASS; lần rerun ngoài sandbox mới là kết quả thành công.
- Project-wide TypeScript: **không PASS**; vẫn có lỗi `BookingFlow.tsx:1157` và missing test dependencies/types tại `FE/src/test/**`, không có diagnostic ở các app files đã sửa. Không cài package hoặc sửa feature ngoài scope để làm xanh typecheck.
- Diff whitespace và kiểm tra pattern private-key/GitHub-token/JWT của 72 file trước commit: PASS; không đưa raw HAR, manifest/evidence, `.env` hoặc generated build artifacts vào bản bàn giao.

Không chạy lại real PostgreSQL/RabbitMQ, F1/two-browser, load hoặc latency ở lượt publish này. Tester tiếp tục theo guide và OPEN gates; không xem việc publish là đóng task. Evidence Report vẫn cần cấp riêng cho nhóm nếu muốn xem lại lịch sử; guide trong repo đủ để bắt đầu run mới khi environment/fixture readiness đã có.
