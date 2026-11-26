# SPARK-177 — QA handoff (consolidated PR, 12/11/2026)

## Scope and Jira
- Testing Jira: https://mangamanagement.atlassian.net/browse/SPARK-177
- Historical QA PR: https://github.com/Quangthm/Mock-Project-Smart-Parking/pull/11
- Reporting only: historical login/logout, Fix2FA, unit, MFA exploratory and UAT evidence. No new execution is claimed on 12/11/2026.
- Review verdict: **PARTIAL / NOT SIGNED OFF**. PR #11 review comment says `wrong format` without specifying whether PR or artifacts; this handoff is a proposed normalization, pending Leader review.

## Canonical files
- `docs/04-testing/SPARK-177/SPARK-177_Login_Logout_QA_Report_2026-11-12.docx`
- `docs/04-testing/test-cases/SmartParking_User_Master_TestCases.xlsx` (one persistent User Master, with new `SP177_Traceability` and `SP177_Evidence_Index` sheets)

Historical workbook `SPARK-177_QA_Traceability_Workbook_DRAFT.xlsx` and historical PR/Jira draft are already preserved in PR #11. They are intentionally **not re-submitted** as current QA master or current PR instructions. Source information has been migrated to the User Master.

## Historical result boundaries
| Evidence | Reported result | Limitation |
|---|---|---|
| Demo Login/Logout | 19 PASS; 1 not executed, 1 N/A | Historical baseline only |
| Fix2FA manual | 9 PASS, 2 NOT TESTED | Historic partial coverage |
| Fix2FA negative API smoke | 5 PASS | Newman report not supplied |
| UserService.Tests 19/10 | 73 PASS, 0 FAIL, 26 SKIP (99 total) | Independent assertion review pending |
| MFA observation | Challenge HTTP 200; verification later HTTP 400/500 | Root cause unverified; NOT confirmed backend bug |
| UAT 05/10 | 8 PASS, 4 FAIL, 1 BLOCKED, 7 NOT TESTED | Not proof of current FE |

Do **not** turn the 15 `SP177_Planned_QA` cases into PASS and do not add historical totals together.

## Open gaps
- Token refresh/rotation/reuse/expiry, logout–refresh race and old token after lock/disable/revoke.
- TOTP/OTP boundaries and triage of observed MFA HTTP 500.
- PostgreSQL integration fixture alignment; 26 SKIPPED unit tests.
- Sanitized Newman report and current FE UAT with reviewer evidence.
- QA approval; no automatic Jira Done or merge by tester.

## Security / provenance
- No populated Postman environment file is part of this handoff.
- Raw historical TRX was referenced by local path only; it is **not attached** to this bundle.
- This note does not resolve the separate risk of sensitive values in historical public Git branches.
