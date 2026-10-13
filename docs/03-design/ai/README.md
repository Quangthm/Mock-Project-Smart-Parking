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

The operator gate screen can capture a camera frame or accept an image upload
and send it to `POST /api/v1/stream/recognize_uploaded_image` on the separately
run Python AI service. Configure its origin with
`FE/.env`'s `VITE_AI_API_BASE_URL` (default `http://localhost:5001`).

The recognition result only pre-fills the reservation lookup. Operators must
verify the plate and explicitly confirm the Reservation Service check-in or
check-out. The AI service and its licensed source/model assets are not bundled
or copied into this repository.
