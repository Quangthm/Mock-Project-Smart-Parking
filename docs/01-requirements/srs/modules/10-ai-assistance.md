# Ai Assistance — SRS v1.0 RC1

[Requirements index](../README.md) · [Traceability](../../requirements-traceability/README.md)

**Status:** review candidate, 13 October 2026; implementation/evidence not assessed. Source review section numbers below refer to the decision review, not this module numbering.

**Security:** Cloudflare Turnstile selected; password and verified registration/contact/reset flows retained; current authorization enforced on protected operations. **SEC-01: optional TOTP and recovery are now current MVP for all four roles.** Enabled TOTP is enforced by User & Access; Assistant cannot enroll, disable, recover or reset factors/passwords and only routes the user to ordinary security/support screens. Confirmed token values: 15-minute access / 7-day absolute refresh session / 24-hour refresh inactivity. Separate passwordless OTP login is approved for all roles via email and phone (§16.5); Turnstile is not a substitute for authorization or rate limiting.

**Assistant:** FAQ, search, authorized reads and reservation form preparation. Final commands require ordinary UI confirmation and authorized domain APIs. Cancellation/refund/security requests route to their normal screens; no autonomous financial/security writes. Gemini and OpenAI are provider directions; exact models/routing/budgets/data-sharing and evaluation are later design details. Detailed chatbot document need not be fully completed for SRS v1.0, but scope/authority/error fallback must be explicit.

**Knowledge:** Admin-only publication authority. Admin/dev team configures a simple versioned approved corpus; no new Developer business role, advanced CMS or mandatory Draft/Approved/Superseded workflow. Live prices/availability/accepted terms come from authoritative APIs. Owner content can be supplied to site Admin outside the MVP submission workflow; no Owner publication permission is granted. Exclude unresolved proposals and stale historical terms.

**Support:** asynchronous tickets, not staffed live chat. Lot matters routed to assigned Operator; platform/account/global plate disputes to Admin. Scoped context references only; no whole-conversation duplication in Audit. Hours/response targets and allowed contextual fields remain to specify; no implied 24/7 response.

**LPR:** explicit unsuccessful outcomes and manual review at uncertain boundary. Conflicting observations never automatically free an occupied slot. Exact recognizer/dataset/confidence scale/equality, integration and accuracy/latency evaluation are explicitly deferred after v1.0 (§16.8).

Retain v0.9 general directions as amended by confirmed later decisions: device-free LPR/manual review, raw observations versus accepted Operations state, scoped correction history; Assistant FAQ/search/authorized reads/reservation preparation, normal UI confirmation and authoritative APIs; Admin-published approved corpus; Gemini/OpenAI direction and asynchronous support.

Defer exact recognizer/model, dataset, confidence scale/equality, detailed provider/tool integration, routing/data-sharing/budgets and evaluation after the v1.0 requirements baseline. Do not preserve contradictory older autonomous-write, guaranteed accuracy or staffed-live-chat statements as active acceptance obligations. Existing numerical AI targets remain historical/unverified references pending the later explicit evaluation baseline; no lowered/achieved target is inferred. General security, truthful failure reporting and manual fallback remain baseline requirements.

See the deferred register for every AI-DEP item and future capability. Confirmed general direction does not certify model integration, accuracy or live hardware.

## Functional requirements

### FR-SIM-01

**Status:** FUTURE — outside MVP  
**Actors:** Admin, Device Gateway  
**Stories:** US-A06

Deterministic test fixtures remain the MVP simulation mechanism. A product-level simulator remains outside MVP unless separately approved as a scoped, isolated demonstration capability; any future simulator must show fixture results without touching operational resources and may not automatically promote SIMULATION_RUN into production device control.

### FR-BAS-09

**Status:** CONFIRMED DIRECTION — detailed integration/evaluation POST-v1.0  
**Actors:** Operator  
**Stories:** US-O08

Retain device-free image recognition, original evidence/result, explicit failure/manual review and audited session-identification corrections. IoT owns raw recognition; Operations owns accepted session identity; correction does not rebind global vehicles. Recognizer, confidence threshold/equality, dataset and accuracy/latency tests are POST-v1.0, with historical targets preserved as unvalidated proposals.

### FR-AI-01

**Status:** CONFIRMED DIRECTION — detailed integration/evaluation POST-v1.0  
**Actors:** Driver  
**Stories:** US-D12

Natural-language intents, entity extraction and multi-turn conversation: the domain contract; missing/ambiguous mutation parameters remain to be clarified per the retained AI-team design.

### FR-AI-02

**Status:** CONFIRMED DIRECTION — detailed integration/evaluation POST-v1.0  
**Actors:** Driver  
**Stories:** US-D12

Answer static help using approved content; never present brainstorming or unresolved alternatives as active policy (BR-AI-01).

### FR-AI-03

**Status:** CONFIRMED DIRECTION — detailed integration/evaluation POST-v1.0  
**Actors:** Driver  
**Stories:** US-D12

Search through approved APIs using user-supplied location, vehicle and time; dynamic values come from returned results (BR-AI-01).

### FR-AI-04

**Status:** CONFIRMED DIRECTION — detailed integration/evaluation POST-v1.0  
**Actors:** Driver  
**Stories:** US-D12

Explain deterministic backend-ranked parking results without creating an LLM ranking formula. Personalized/model-based AI ranking is not used in the MVP.

### FR-AI-05

**Status:** CONFIRMED DIRECTION — detailed integration/evaluation POST-v1.0  
**Actors:** Driver  
**Stories:** US-D12

Read the signed-in Driver's bookings, session, saved vehicles, payment/refund status and cancellation eligibility through scoped APIs (BR-AI-02).

### FR-AI-06

**Status:** CONFIRMED DIRECTION — detailed integration/evaluation POST-v1.0  
**Actors:** Driver  
**Stories:** US-D12

Prepare a reservation draft and populate the normal application form for review/editing; saved-vehicle retrieval does not permit vehicle modification (BR-AI-03).

### FR-AI-07

**Status:** CONFIRMED DIRECTION — detailed integration/evaluation POST-v1.0  
**Actors:** Driver  
**Stories:** US-D12

Create a reservation only after final UI confirmation and fresh backend validation; changed terms require reconfirmation (BR-AI-03).

### FR-AI-08

**Status:** CONFIRMED DIRECTION — detailed integration/evaluation POST-v1.0  
**Actors:** Driver  
**Stories:** US-D12

Return actual backend reservation/payment status and hand off to ordinary checkout. Financial authority remains the domain contract; chatbot mutation requires the confirmed user-review/backend-execution boundary.

### FR-AI-09

**Status:** CONFIRMED DIRECTION — detailed integration/evaluation POST-v1.0  
**Actors:** Driver  
**Stories:** US-D12

Distinguish empty results, authentication/authorization failure, invalid input, conflict, timeout and server errors; never fabricate success (BR-AI-05).

### FR-AI-10

**Status:** CONFIRMED DIRECTION — detailed integration/evaluation POST-v1.0  
**Actors:** Driver, Admin  
**Stories:** US-D12

Enforce the domain contract through allowlisted tools, resource authorization and no direct database/gate/policy/penalty access; see BR-AI-02/04.

### FR-AI-11

**Status:** CONFIRMED DIRECTION — detailed integration/evaluation POST-v1.0  
**Actors:** Driver  
**Stories:** US-D12

Explain backend-calculated charges and effective policy values; no hard-coded N or model-generated replacement calculation (BR-AI-01).

### FR-BAS-10

**Status:** CONFIRMED DIRECTION — detailed integration/evaluation POST-v1.0  
**Actors:** Driver, Operator  
**Stories:** US-D12, US-O09

Retain Vietnamese/English assistive help and clear AI identification. Human handoff creates asynchronous General Support in Notification & Audit: lot matters to assigned Operator; platform/account/global binding matters to Admin. Pass scoped references only, not unrestricted whole-chat copies. No live-chat/24-hour support promise; published hours/handlers are PM operational declarations.

### FR-BAS-11

**Status:** CONFIRMED DIRECTION — detailed integration/evaluation POST-v1.0  
**Actors:** Driver  
**Stories:** US-D12

Assistant may read permitted information and prepare a reservation form. Normal UI binds final confirmation to exact terms; authorized backend performs the command. Cancellation/refund/security requests route to ordinary UI. Exact tool schemas/integration remain POST-v1.0.

### FR-BAS-12

**Status:** FUTURE — outside MVP  
**Actors:** Driver  
**Stories:** US-D18

Personalized/model-based AI recommendations remain future. MVP recommendations use deterministic backend ranking and the chatbot may explain that ranking; all available options remain accessible.

### FR-BAS-13

**Status:** FUTURE — outside MVP  
**Actors:** Operator  
**Stories:** US-O08

Future AI occupancy detection is defined once in the domain contract, including uncertainty and human fallback.

### FR-BAS-14

**Status:** FUTURE — outside MVP  
**Actors:** Owner  
**Stories:** US-OW08

Future capacity forecasting and model requirements are defined once in the domain contract.

### FR-BAS-15

**Status:** FUTURE — outside MVP  
**Actors:** Operator, Owner  
**Stories:** US-O10

Future anomaly detection/staff alert tiers are defined once in the domain contract.
