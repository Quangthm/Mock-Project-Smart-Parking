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
`POST /api/v1/stream/recognize_live` on `main_live.py` (default
`http://localhost:5002`, configurable with `VITE_AI_LIVE_API_BASE_URL`). A new
plate at 90% confidence or higher is looked up and checked in automatically
(reservation or walk-in); lower confidence requires operator review. An
already active parking session is surfaced as a check-out action, which the
operator must confirm. Upload recognition continues to use
`POST /api/v1/stream/recognize_uploaded_image` on `main.py` (default
`http://localhost:5001`, configurable with `VITE_AI_API_BASE_URL`).

Run the real-time AI service separately from the `python-ai` directory with
`python main_live.py` (its default port is 5002). The AI service and its
licensed source/model assets are not bundled or copied into this repository.
