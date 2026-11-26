# SPARK-392 session, 27 November 2026

## 1. Session information
Main SmartParking worktree, branch `fix/SPARK-392-vehicle-image-upload-compression`, baseline `474c605af091716dd84d11e1739a02b1d35c181d` (PR #104). Implementation ready for review. Browser and live-provider acceptance are pending.

## 2. Objectives
Correct vehicle photo uploads above 64 KiB, enforce purpose-specific admission, optimize vehicle selections, preserve existing managed media and supply QA assets.

## 3. Work completed

| Checkpoint | Outcome |
|---|---|
| A | Applied native Kestrel and MVC limits, placed the routed declared-length guard after CORS and retained the centralized response envelope. Real loopback HTTP verified known lengths, chunked bodies and allowed/disallowed origins. |
| B | Command validation and bounded file reading distinguish vehicle and avatar admission. PostgreSQL and fake-provider tests cover rejection before admission, exact accepted bytes, historical attached reads and identical-byte replay. |
| C | The shared vehicle component validates source formats and stillness, decodes the original, retains suitable files and otherwise measures up to four bounded canvas JPEG encodings. New selections abort and supersede previous work. |
| D | Upload-specific messages distinguish preparation, transfer, ready, pending, 413, invalid content, unavailable storage and connectivity. Existing authentication error handling remains in the shared request helper. |
| E | Solution and frontend production builds pass. Focused backend and scoped TypeScript results below. Manual acceptance delegated to the requester. |
| F | Aligned six current requirements/design documents. Added a 13-request Postman collection, example environment, exact-size PNG generator and QA handoff with revised Jira criteria. |

## 4. Technical decisions and shared consumers

- **Transport and errors.** Ordinary bodies use the 65,536-byte Kestrel default. MVC endpoint metadata supplies the two multipart allowances. The declared-length guard covers requests that may finish without a body read. Native enforcement applies during streamed reads. MVC can convert form IO failures to model-binding errors. The upload resource filter reads the framework-cached form after authorization and size filters on unknown-length requests, preserving the typed size failure for the central mapper. Kestrel's concrete bad-request exception requires its own entry in the existing exact-type mapping. The permitted-origin callback runs before rejection and sets headers as the error response starts. [ASP.NET Core form value provider](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Mvc/Mvc.Core/src/ModelBinding/FormValueProviderFactory.cs)
- **Media ownership.** Vehicle uploads use 1 MiB. Avatar admission, Skia content inspection and bounded Cloudinary download use 5 MiB. Existing handlers fingerprint purpose, binding and SHA-256 and verify provider identity, byte count, dimensions and digest. UserService hands the submitted bytes directly to the existing provider. Registration claims and replacement retain their existing ownership, lease, expiry and attachment rules.
- **Frontend.** Booking and dashboard registration share the component. JPEG encoding uses the gate camera's aspect-preserving canvas technique with bounded long sides and qualities, white backing for transparency, and a byte-size check on every Blob. The browser retains one final File, UUID key and target/draft per selection. Gate capture, IoT, reservation and payment sources were outside this change.
- **Source organization.** Moved touched types into dedicated files. The untouched remaining declarations in the legacy `MediaContracts.cs` aggregate retain their original contracts.

## 5. Changed-file inventory

- `FE/src/components/VehicleImageInput.tsx`
- `docs/01-requirements/business-rules/09-file-media.md`
- `docs/01-requirements/business-rules/policy-catalogue.md`
- `docs/01-requirements/srs/modules/09-file-media.md`
- `docs/02-architecture/service-boundaries/media-contract.md`
- `docs/03-design/api/user-images-api-design.md`
- `docs/03-design/api/vehicle-registration-api-design.md`
- `src/Services/UserService/SmartParking.UserService.API/Common/ApiExceptionMapper.cs`
- `src/Services/UserService/SmartParking.UserService.API/Common/ApiResponseFactory.cs`
- `src/Services/UserService/SmartParking.UserService.API/Controllers/MediaImagesController.cs`
- `src/Services/UserService/SmartParking.UserService.API/Program.cs`
- `src/Services/UserService/SmartParking.UserService.Application/Media/MediaContracts.cs`
- `tests/Services/SmartParking.UserService.Tests/MediaAssetTests.cs`
- `FE/src/lib/vehiclePhoto.ts`
- `ai/tasks/active/TASK-SPARK-392-SESSION-20261127.md`
- `docs/04-testing/postman/SPARK-392-Local.postman_environment.example.json`
- `docs/04-testing/postman/SPARK-392-tester-handoff.md`
- `docs/04-testing/postman/SPARK-392.postman_collection.json`
- `docs/04-testing/postman/spark392-fixtures.py`
- `src/Services/UserService/SmartParking.UserService.API/Common/ApiErrorDetail.cs`
- `src/Services/UserService/SmartParking.UserService.API/Common/ApiResponse.cs`
- `src/Services/UserService/SmartParking.UserService.API/Common/ErrorHandlingResult.cs`
- `src/Services/UserService/SmartParking.UserService.API/Controllers/ImageUploadForm.cs`
- `src/Services/UserService/SmartParking.UserService.API/Controllers/ReadStreamedUploadFormAttribute.cs`
- `src/Services/UserService/SmartParking.UserService.API/UserHost.cs`
- `src/Services/UserService/SmartParking.UserService.Application/Media/UploadImageCommand.cs`
- `src/Services/UserService/SmartParking.UserService.Application/Media/UploadImageValidator.cs`
- `tests/Services/SmartParking.UserService.Tests/RequestSizeTests.cs`

## 6. Issues and limitations
Pre-change pending uploads above 1 MiB cannot replay; a new selection starts a new intent and the old record follows its existing lifecycle. The browser may report a connection failure if a proxy or server terminates transmission before returning application JSON. Kestrel counts chunk framing toward the HTTP/1.1 transport ceiling.

## 7. Validation results

- `dotnet build SmartParking.slnx --nologo -m:1` passed (75 warnings including restore). Final `dotnet build SmartParking.slnx --no-restore --nologo -m:1 -v:q` passed, 36 warnings, zero errors. Existing dependencies produced NuGet advisory and unavailable audit-feed warnings. An overlapping interim compilation encountered a file lock; the final build ran serially.
- Backend verification used `dotnet test tests/Services/SmartParking.UserService.Tests --no-build -m:1 --filter "FullyQualifiedName~RequestSizeTests|FullyQualifiedName~OwnProfileTests|FullyQualifiedName~DriverRegistrationHttpTests|FullyQualifiedName~MediaAsset|FullyQualifiedName~VehicleRegistrationTests|FullyQualifiedName~VehicleClaimHttpTests|FullyQualifiedName~ApiErrorHandlingTests"`. Final result: **110 passed, 0 failed, 0 skipped**. The fixture used isolated PostgreSQL 18 databases, a fake provider and process-local Turnstile disablement. The dedicated transport test uses real Kestrel with anonymous test-only access and disabled storage. Persistence/ownership tests use the real authenticated controller via TestServer.
- Initial sandbox VSTest loopback timed out. The isolated host rerun exposed a streamed 413 mapping and the login fixture's external Turnstile requirement; the final verification includes both corrections.
- `npm.cmd run build` passed in FE. Vite warned about chunk size and an ineffective dynamic import in operationalLayout, and reported plugin timings. An initial source encoding error was fixed. Scoped strict `tsc --noEmit` passed for both changed frontend files and imports. Full `tsc --noEmit --pretty false` reported 15 diagnostics outside the changed files (ES2020 library mismatch in two assistant modules and missing test-library types/dependent errors).
- Both Postman JSON files parsed, scripts for 13 requests compiled, and the PNG generator produced all seven expected file lengths. Live Postman/Cloudinary and browser journeys remain unverified.

## 8. Git status
Clean worktree and index at start. Changes listed above are unstaged, with no change to HEAD, Jira or PRs.

## 9. Handoff
Use the SPARK-392 tester handoff in `docs/04-testing/postman` for setup, live-provider and browser acceptance, regression smoke checks and revised Jira criteria.

## 10. Context
The user explicitly approved the 1 MiB admission amendment, delegated functional and visual browser testing, and confirmed how to handle old oversized pending records. Review and publication remain with the requester.
