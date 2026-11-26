# SPARK-392 · Vehicle photo acceptance

**Frontend functional and visual acceptance — PENDING USER VERIFICATION.** Use a disposable Driver account and an unused plate in a local test environment. Registration and replacement write real test records.

## Setup

1. Follow the local setup guide for PostgreSQL, the UserService schema, authentication and the services needed for booking. Start UserService and the frontend from the branch under test. Check UserService health at `http://localhost:5035/health`. The usual frontend origin is `http://localhost:5173`. Configure that exact origin in the existing CORS allowlist. [Local setup](../../../LOCAL-SETUP.md) · [Booking service setup](SPARK-273-manual-test-guide.md)
2. Enable UserService media with `SMARTPARK_USER_MEDIA_ENABLED=true` and configure `SMARTPARK_CLOUDINARY_CLOUD_NAME`, `SMARTPARK_CLOUDINARY_API_KEY` and `SMARTPARK_CLOUDINARY_API_SECRET` locally. Recreate the changed service using the existing app runner. Confirm the provider permits authenticated upload and signed reads. Keep all credentials local.
3. Import the collection and example environment. Select the environment, set local Driver credentials and a unique supported CAR plate. Use a verified active Driver and complete any required sign-in challenge. Supply a fresh Turnstile token if enabled. For MFA, finish the normal challenge and set the resulting access token locally in `driverToken`. [Collection](SPARK-392.postman_collection.json) · [Example environment](SPARK-392-Local.postman_environment.example.json)
4. From the repository root run `python docs/04-testing/postman/spark392-fixtures.py "<Postman working directory>"`. The collection refers to files under `spark392-fixtures/` in that directory. Select the indicated file manually if Postman prompts. The generator pads a valid PNG with a metadata chunk and creates exact file lengths. For visual testing, also use nonprivate photographs with detail, a portrait with EXIF rotation, a transparent PNG, and still and animated WebP/PNG examples.

## Browser acceptance

The original failure occurred in **Booking → Time & Vehicle → Add New Vehicle**. Uploading a valid file over 65,536 bytes returned 413 and could display an authentication-server or CORS error. Repeat from booking and dashboard registration with Network and Console open.

| Select or perform | Expected result |
|---|---|
| Still JPEG, PNG or WebP below 64 KiB, then one between 64 KiB and 1 MiB | Preview, upload, ready asset, successful Save. Refresh and confirm the same photo displays. Download the owned content and compare its bytes with the selected file when it already fit. |
| Original above 1 MiB through exactly 5 MiB | Optimization finishes before upload is available. Inspect the transmitted file size as described below. Verify orientation, contrast on transparency, aspect ratio, plate detail and readable image at normal display size. |
| Original above 5 MiB, empty, unsupported, malformed, animated, or over dimension/pixel limits | Actionable selection or optimization error. Save stays disabled and no file is uploaded. |
| Select a second file during preparation or upload, or navigate away and return | Only the current selection can become ready. A previously ready asset cannot enable Save for a new selection. |
| Take the browser offline during upload, restore connectivity, then retry | Vehicle-photo connection guidance. The repeat POST carries identical file bytes, the same idempotency key and the same draft/vehicle binding. A different selection starts a new intent. |
| Provider pending, unavailable, or invalid image; HTTP 413; expired session or denied role | Pending keeps Save disabled. Retry the existing file for a pending result. Storage, invalid-image and upload-size errors identify the upload issue. Session expiry and access-denied retain their normal behavior. |

**Measure the binary part.** In DevTools Sources, set a breakpoint at the vehicle upload's `body.append("file", file)` and inspect `file.size`, `file.type` and `file.name`. Compare with the original file's size on disk. After the append, inspect `body.get("file").size` and confirm this POST in Network. For retries, compare SHA-256 digests of that File in both paused requests or compare downloaded authenticated bytes. A multipart request includes boundaries, headers and fields. Its Content-Length alone cannot establish the file size. The collection exercises direct API clients only.

## Direct API acceptance

Run requests 01–13 in order. For a 202, record the asset ID, wait, and retry the same upload with the same file and environment. Request 03 also reads the draft status. Continue to Save, attach or read only after ready. For a fresh run use a new local environment copy and a new plate.

| Boundary or workflow | Expected |
|---|---|
| Ordinary UserService HTTP body | 65,536 bytes maximum. A declared 65,537-byte request returns 413. |
| Vehicle binary and multipart | Exactly 1,048,576 bytes can become ready. One byte over with normal form overhead returns 400 `MEDIA_INVALID_IMAGE`. A total body above 1,114,112 bytes returns 413 `PAYLOAD_TOO_LARGE`. |
| Avatar binary and multipart | Over 1 MiB through 5,242,880 bytes can become ready and return the authenticated content. One byte over with normal overhead returns 400. The whole body permits 5,308,416 bytes. |
| Vehicle registration and replacement | POST /api/vehicles returns 201 with the ready image. Upload a second file bound to the returned vehicle ID. PUT its new asset ID and the previous expected asset ID, then retrieve its content. A stale expected reference returns 409 `MEDIA_CONFLICT`. Replacement is tested via API. |
| Identity, ownership and retention | Missing or expired credentials return 401, an ineligible role returns 403, foreign images return 404, and expired content returns 410. Read a still-valid owned pre-change vehicle image over 1 MiB. |
| CORS | An allowed Origin receives its exact Access-Control-Allow-Origin on the application's 413 JSON response. Re-send request 10 with an unlisted Origin and verify the header is absent. Restore the environment afterward. |

If the response is 503, check the media switch, provider account and private delivery permissions. If the browser reports a network failure, compare UserService directly with any gateway and inspect response headers. Kestrel counts HTTP/1.1 chunk framing toward its transport ceiling. A proxy or a connection terminated while a body is being transmitted can prevent the client receiving application JSON.

Pre-change pending vehicle files over 1 MiB cannot be replayed. Leave the old record to its existing lifecycle and select the photo again for a new optimized file and new intent. Completed historical photos retain their ownership, integrity and expiry checks.

For the shared-path smoke check, exercise Driver login, registration and OTP, profile/avatar, a registration claim, booking and payment, and Operator check-in/check-out with its existing photo flow. Record environment, response status, safe code and observed behavior. Keep tokens and private photos out of evidence.

## Revised Jira acceptance criteria

- Driver can select a supported still image up to 5 MiB from booking or the dashboard. Uploads over 64 KiB succeed when within their applicable bounds.
- Every submitted vehicle file is at most 1 MiB. Direct API admission applies the same inclusive limit. Oversized files and HTTP bodies receive their documented errors, with readable application-generated errors from the allowed frontend.
- Save or replacement uses a ready asset. Refresh retrieves the attached image. Retrying preserves exact bytes and intent.
- Avatar admission stays at 5 MiB. Historical vehicle retrieval, ownership, expiry and gate handling retain their existing behavior.

These criteria reflect the user-approved change to the earlier 5 MiB vehicle-upload expectation.
