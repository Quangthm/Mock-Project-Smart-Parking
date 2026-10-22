# SmartParking Pull Request Guide & Checklist Skill

**Version:** 1.0  
**Project:** SmartParking  
**Repository workflow:** GitHub + Jira  
**Status:** Team convention  

---

## 1. Mục đích

Tài liệu này chuẩn hóa quy trình mở Pull Request (PR) cho dự án SmartPark, đảm bảo:
- Mọi thay đổi đều được theo dõi chặt chẽ và gắn liền với Jira Ticket (`SPARK-<number>`).
- Nội dung PR rõ ràng, giúp reviewer nắm bắt nhanh bối cảnh kỹ thuật và phạm vi ảnh hưởng.
- Đảm bảo chất lượng qua checklist kiểm thử (Build, Unit test, Integration test, Manual verification).
- Kiểm soát các rủi ro liên quan đến breaking changes, migration và cấu hình môi trường.

---

## 2. Tiêu chuẩn đặt tiêu đề Pull Request

Định dạng bắt buộc theo chuẩn Conventional Commits kết hợp Jira Key:

```text
<type>(<scope>): <short description> [SPARK-<number>]
```

Ví dụ:
- `feat(reservation): implement slot allocation logic [SPARK-105]`
- `fix(auth): prevent login with expired otp code [SPARK-47]`
- `docs(ai): update agent instructions and session skill [SPARK-262]`

Target branch mặc định trong giai đoạn phát triển tích cực là: `develop` (hoặc feature branch cha nếu làm việc theo nhóm nhánh).

---

## 3. Template Pull Request chuẩn

Khi tạo PR trên GitHub, bắt buộc sử dụng template sau vào phần Description của PR:

```markdown
## Summary
<!-- Briefly explain the purpose of this PR. -->

## Related Issue
- Closes #<issue-number> (hoặc Refs [SPARK-xxx])

## Changes
- [ ] Added ...
- [ ] Updated ...
- [ ] Removed ...

## Implementation Details
<!-- Explain important technical decisions or architectural changes. -->

## Testing
- [ ] Build succeeds
- [ ] Unit tests pass
- [ ] Integration tests pass (if applicable)
- [ ] Manual testing completed (if applicable)

**Test results:**
<!-- Describe the commands executed and their results. -->

## Screenshots / Evidence
<!-- Attach screenshots, logs, or API responses if relevant. -->

## Breaking Changes
- [ ] No breaking changes
- [ ] Breaking changes described below

<!-- Describe any breaking changes, configuration changes, or migration requirements. -->

## Reviewer Notes
<!-- Highlight files, logic, or risks that need special attention. -->
```

---

## 4. Checklist bắt buộc trước khi Submit PR

- [ ] Branch đã được rebase/merge mới nhất từ `develop` để tránh conflict.
- [ ] Không commit secret, file cấu hình nhạy cảm (`.env`, `*.user`, connection string).
- [ ] Mã nguồn đã được build thành công (`dotnet build SmartParking.slnx` / `pnpm build` ở FE).
- [ ] Toàn bộ unit tests liên quan đã pass.
- [ ] Tiêu đề PR tuân thủ đúng format `type(scope): description [SPARK-xxx]`.
- [ ] Đã điền đầy đủ các mục trong template PR mô tả bên trên.
