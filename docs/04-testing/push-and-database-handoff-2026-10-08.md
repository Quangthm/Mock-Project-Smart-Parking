# Push code và database cho phần task đã bàn giao

Kiểm tra local ngày 08/10/2026: code đang ở `develop`, HEAD `078cde0`, upstream `origin/develop`. Toàn bộ sửa đổi của lượt này chưa commit/push. `arch/database-schema` local và remote-tracking cùng `6797923`, có baseline microservices và ba overlay `integration/01`, `02`, `03`. Đây là snapshot refs local, chưa fetch để xác nhận server có commit mới hơn.

**Ba upgrade mới chưa có trên nhánh database**:

| File trong `scripts/database/` | DB sở hữu |
| --- | --- |
| `05.8-Account-Workflow-Vehicles.sql` | User |
| `05.9-Parking-Backup-Operations.sql` | Parking |
| `05.10-Reservation-Confirmation-Time.sql` | Reservation |

Chúng hiện là file untracked trong working tree code. DB dự án chưa được migrate; test đã dùng PostgreSQL riêng. Có SQL trên Git không đồng nghĩa đã deploy vào database.

## Code PR

Chạy tại repo code bằng PowerShell. Các lệnh dưới là hướng dẫn, chưa được thực hiện thay bạn. Không push trực tiếp develop, không force-push shared branch.

```powershell
git status --short
git diff --stat
git switch -c codex/assigned-task-workflows
git fetch origin
```

Working tree đi cùng branch mới, không cần checkout nhánh khác để chép code. Nếu tên branch đã tồn tại thì dùng tên feature mới phù hợp. Fetch cập nhật refs, không sửa working tree.

[Danh sách staging đề xuất](push-files-2026-10-08.txt) chỉ chứa phạm vi sửa và tài liệu/test đi kèm, loại hai thư mục API `Properties` mới xuất hiện sau review. **Đọc từng diff trước khi stage**; danh sách không chứng minh mọi thay đổi trong một file đều do một người làm. Nếu file có sửa của thành viên khác, dùng `git add -p -- <path>` và bỏ file đó khỏi staging list để không stage lại toàn file. Không dùng `git add .`.

```powershell
git diff
# Inspect new files listed in push-files-2026-10-08.txt too; git diff alone omits untracked files.
git --literal-pathspecs add --pathspec-from-file=docs/04-testing/push-files-2026-10-08.txt
git diff --cached --stat
git diff --cached --check
git diff --cached
git status --short
```

Ba service-specific upgrade SQL **được giữ cùng code PR trong cấu trúc hiện hành**, vì migration runner và database tests đang đọc chúng từ checkout code. Canonical baseline/overlays vẫn ở `arch/database-schema`; không copy baseline vào develop. Nếu team yêu cầu mọi SQL chỉ tồn tại trên nhánh database, cần đổi runner/test để đọc upgrade từ schema checkout và test lại trước khi bỏ SQL khỏi code commit. Chỉ loại ba file khỏi commit bây giờ sẽ làm thiếu dependency của bản bàn giao.

Review phần đã stage, bảo đảm không có config/credential thật hoặc thay đổi ngoài task. Sau đó:

```powershell
dotnet test tests/Services/SmartParking.UserService.Tests/SmartParking.UserService.Tests.csproj
dotnet test tests/Services/SmartParking.ParkingService.Tests/SmartParking.ParkingService.Tests.csproj
node --test tests/frontend/auth-api.test.cjs tests/frontend/auth-context.test.cjs
Push-Location FE
npm run build
Pop-Location
git commit -m "feat(workflows): complete assigned auth vehicle and parking tasks"
```

Database tests cần `SMARTPARK_AUTH_TEST_CONNECTION` là PostgreSQL cô lập và `SMARTPARK_SCHEMA_ROOT` là checkout canonical schema. Không có env thì một số tests skip; không báo lại là full integration pass. Evidence lần review đầy đủ nằm trong report/CSV của handoff. Chưa chạy provider UAT hoặc remote CI.

Trước rebase cần working tree sạch. Nếu còn thay đổi của bạn/thành viên khác chưa commit, giữ lại và **chưa chạy rebase**; đừng stash/delete tự động. Khi sạch:

```powershell
git fetch origin
git rebase origin/develop
# Resolve conflicts, then rerun affected checks. git rebase --abort if unsure.
git push -u origin codex/assigned-task-workflows
```

Tạo PR base `develop`, ghi các task thuộc phạm vi, link tester guide + collection + report, ba migrations/deployment order và các cases dependency. Dùng commit message theo Jira convention của team nếu có. Không mark task của thành viên khác Done.

## Database PR riêng, không merge toàn bộ code vào nhánh database

Sau code commit, tạo schema worktree **trong thư mục `.cache` của repo** để không đụng working tree đang có sửa khác. Fetch trước, rồi branch mới từ `origin/arch/database-schema`:

```powershell
$taskCodeRoot = (Get-Location).Path
$taskSchemaRoot = Join-Path $taskCodeRoot '.cache/schema-workflows-pr'
git worktree add -b codex/assigned-task-database $taskSchemaRoot origin/arch/database-schema
$taskSqlFiles = @(
  '05.8-Account-Workflow-Vehicles.sql',
  '05.9-Parking-Backup-Operations.sql',
  '05.10-Reservation-Confirmation-Time.sql'
)
foreach ($taskSqlFile in $taskSqlFiles) {
  Copy-Item -LiteralPath (Join-Path $taskCodeRoot "scripts/database/$taskSqlFile") -Destination (Join-Path $taskSchemaRoot "scripts/database/$taskSqlFile")
}
Push-Location $taskSchemaRoot
git status --short
git add -- scripts/database/05.8-Account-Workflow-Vehicles.sql scripts/database/05.9-Parking-Backup-Operations.sql scripts/database/05.10-Reservation-Confirmation-Time.sql
git diff --cached --check
git diff --cached
git commit -m "feat(database): add assigned workflow upgrades"
git push -u origin codex/assigned-task-database
Pop-Location
```

Tạo PR base `arch/database-schema`, chỉ ba SQL và schema handoff nếu cần. Không merge/rebase toàn bộ develop vào arch. Hai PR liên kết nhau; ghi rõ ba SQL là bản sao **cùng nội dung** của upgrade release trong code PR, không hai migrations khác nhau để apply hai lần. Trước merge/deploy so sánh `Get-FileHash` cho từng file ở hai checkout; sửa một bên thì đồng bộ bên còn lại trước release. Baseline canonical vẫn chỉ ở arch.

Deployment: backup và stop service tương ứng → canonical baseline/overlays đã sẵn có → apply User/Parking/Reservation upgrade vào đúng DB → start compatible services → tester chạy smoke/UAT. Dừng và xử lý migration conflict (ví dụ contact collision) trước khi chạy API mới; không gộp account hoặc backfill verification bằng giả định. Không áp lại CREATE TABLE baseline lên DB hiện hữu.

Release ghi code SHA/schema SHA, migration execution evidence, tester sign-off và CI/PR approval. Chưa có push, migration deployment hay PR approval trong lượt này.
