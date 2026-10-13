# Product AI

This folder documents AI functionality that belongs to the SmartPark product.

The current SRS includes AI LPR, AI chatbot, and additional AI capabilities,
but some AI features are explicitly marked Future/Deferred and some sections
remain unfinished.

When documenting product AI:
- preserve the SRS status;
- do not promote Future/Deferred capabilities to MVP;
- do not override traditional backend authorization, calculation, payment,
  or legal responsibilities;
- preserve any unresolved implementation choices.

## Operator plate recognition integration

The operator gate screen is present when the page opens; the device camera
stays off until the operator chooses **Open camera**. With the camera on, the
frontend continuously sends frames to
`POST /api/v1/stream/recognize_live` on the IoT AI service (default
`http://localhost:5002`, configurable with `VITE_AI_LIVE_API_BASE_URL`). Image
uploads use `POST /api/v1/stream/recognize_uploaded_image` on the same service
(`VITE_AI_API_BASE_URL`). The service writes recognized plates, confidence,
direction, site/tenant scope, and timestamp to `lpr_events` in the IoT
database. It creates or updates a logical `CAMERA_LPR` device per site because
the existing LPR schema requires each event to reference a device. Repeated
detections of the same plate and direction within eight seconds are stored
once. The full gate image is stored with the parking session, not in the
LPR event's `image_url` field.

A new plate at 90% confidence or higher is looked up and checked in
automatically (reservation or walk-in); lower confidence requires operator
review. An already active parking session is surfaced as a check-out action,
which the operator must confirm.

From the repository root, start the IoT database:

```powershell
docker compose --env-file .env -f compose.schema-integration.yml up -d iot-db
```

Then run the IoT recognition service from `src\Services\IoTService\python-ai`:

```powershell
python -m pip install -r requirements.txt
python main_live.py
```

The service runs on port 5002 and loads database settings from the
repository-root `.env`. It writes to the existing `devices` and `lpr_events`
tables, so no additional IoT database migration is required. Apply
`scripts/database/microservices/03.2-reservation-gate-images.sql` to an
existing Reservation database so it can store the captured image data URLs.
`main_live.py`, its model assets, and recognition helpers are now contained
in the IoTService.
