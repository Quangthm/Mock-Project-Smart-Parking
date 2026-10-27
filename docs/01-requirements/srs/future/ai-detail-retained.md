# Retained AI details — POST-v1.0 / FUTURE reference

**Not current acceptance gates.** Original source detail retained to avoid losing proposed functionality, accuracy targets, governance and evaluations. Confirmed general authority/manual fallback is in active module10. LPR/Assistant detailed design/integration/evaluation is POST-v1.0; per-slot occupancy, personalization, forecast and anomaly features are FUTURE. Historical thresholds, dates and phase readiness are not newly approved or achieved.

### 6.2 AI License Plate Recognition (LPR)

#### 6.2.1 Functional Requirements

The system shall integrate AI-powered license plate recognition for automated vehicle identification at parking lot entry and exit points. (Image upload simulation for MVP, No real Iot implementation)

**AI Component:**

- Recognize Vietnamese license plates from camera images under various conditions (day, night, rain, partial occlusion)  
- Output confidence score for each recognition result  
- Detect abnormal or suspicious license plates  
- Process recognition requests within 3 seconds

**Traditional System Component:**

- Validate recognized plate against registered user vehicles  
- Confirm entry/exit authorization based on reservation status and payment  
- Log all recognition events for audit and compliance  
- Handle manual override by parking attendants when AI confidence is below threshold

**Integration Rules:**

- Auto-open barrier only when AI confidence \> 90% AND traditional system confirms valid reservation/payment  
- When AI confidence \< 90%, require attendant manual verification  
- All AI recognition results shall be logged with timestamp, image, and confidence score  
- Staff shall have ability to override AI decision and correct plate entries

#### 6.2.2 Accuracy & Performance

| Metric | Requirement |
| :---- | :---- |
| Recognition Accuracy | \> 98% for standard Vietnamese plates |
| Processing Time | \< 3 seconds per frame |
| Confidence Threshold | \> 90% for automated approval |
| Fallback Rate | \< 2% requiring manual intervention |
| Night/Low-light Accuracy | \> 95% |

#### 6.2.3 Privacy & Compliance

- License plate images shall be retained for a maximum of 30 days for operational, security, and dispute-resolution purposes, unless a longer retention period is required or permitted by applicable law or is necessary for an ongoing incident, dispute, or legal obligation.

- License plate recognition logs shall be encrypted at rest and protected by role-based access control. Access shall be limited to authorized personnel based on their assigned responsibilities.

- The license plate recognition functionality shall comply with **Law No. 91/2025/QH15 on Personal Data Protection** and **Decree No. 356/2025/NĐ-CP**, together with other applicable Vietnamese regulations. License plate numbers are classified as basic personal data under Decree 356/2025/NĐ-CP. :contentReference[oaicite:0]{index=0}

- Users shall be informed of license plate recognition and the associated collection and processing of personal data through appropriate notices, including signage at parking lot entrances where applicable.

- When the applicable retention period expires or the processing purpose is no longer applicable, license plate images and related personal data shall be deleted or destroyed in accordance with applicable requirements.

<!-- INTEGRATION-START FR-LPR -->

#### 6.2.4 LPR Traceability Index

Status definitions are in the Baseline Revision Notice. Detailed constraints are defined once in §5.2; X items are recorded in Appendix E.

| FR ID | Status | Required behavior / canonical SRS location | Actor | User Story |
| --- | --- | --- | --- | --- |
| FR-BAS-09 | R/X | LPR image upload, plate/confidence results, suspicious-result reporting, manual correction and event evidence: §6.2.1–3; C-14. | Operator | US-O08 |

<!-- INTEGRATION-END FR-LPR -->

### 6.3 AI Parking Spot Status Detection (Future/Deferred)

#### 6.3.1 Functional Requirements

The system shall use AI computer vision to detect parking spot occupancy status, supplementing or enhancing existing IoT sensor data.

**AI Component:**

- Analyze camera feeds to detect occupied vs. empty parking spots  
- Output occupancy prediction with confidence score  
- Handle occlusions, shadows, and adverse weather conditions  
- Detect vehicles that may not trigger sensors (e.g., motorcycles, small vehicles)

**Traditional System Component:**

- Fuse AI predictions with existing IoT sensor data using confidence-weighted algorithm  
- Finalize spot status based on combined data sources  
- Allow staff manual override of AI predictions  
- Maintain audit trail of all status changes with source attribution

**Integration Rules:**

- When AI confidence \> 85%, automatically update spot status  
- When AI confidence \< 85%, rely on sensor data or mark as "uncertain"  
- In case of conflict between AI and sensor, prioritize sensor data but log AI discrepancy  
- Staff shall receive alerts for persistent AI-sensor conflicts requiring investigation

#### 6.3.2 Accuracy & Performance

| Metric | Requirement |
| :---- | :---- |
| Detection Accuracy | \> 95% under normal conditions |
| False Positive Rate | \< 5% |
| Processing Latency | \< 500ms per frame |
| Weather Degradation | \< 10% accuracy drop in light rain |
| Night Performance | \> 90% accuracy with adequate lighting |

#### 6.3.3 Privacy & Compliance

- Camera feeds shall not store continuous video; only processed frames with detected vehicles  
- Spot-level detection images shall be retained for 7 days maximum  
- Clear signage shall inform users of AI-powered monitoring  
- System shall comply with local privacy regulations for video surveillance

### 6.4 AI Parking Recommendations

#### 6.4.1 Functional Requirements

The system shall provide AI-enhanced parking recommendations that go beyond simple nearest-available logic.

**AI Component:**

- Analyze user behavior patterns, preferences, and history  
- Consider contextual factors: time of day, destination type, vehicle size, weather  
- Generate personalized ranked list of parking options  
- Provide explainable recommendations ("Recommended because you often choose covered parking")

**Traditional System Component:**

- Validate all recommended spots for actual availability and legal compliance  
- Enforce business rules on reservations and pricing  
- Present AI recommendations alongside traditional distance-based results  
- Allow users to override AI and view all available options

**Integration Rules:**

- AI recommendations shall be clearly labeled as "Suggested for you"  
- Users shall always have option to view "All nearby parking" without AI filtering  
- AI shall not hide available spots from users  
- Recommendations shall update in real-time as availability changes

#### 6.4.2 Personalization Features

- Learn user preferences: covered vs. open, near elevator, price sensitivity  
- Recognize recurring patterns: workplace, home, regular destinations  
- Adapt to vehicle type: standard vs. oversized vehicles  
- Consider historical booking success rates for each parking lot

#### 6.4.3 Cold Start Strategy

- For new users without history: use demographic and contextual defaults  
- For new parking lots: use rule-based ranking until sufficient data accumulates  
- Gradually introduce personalization as user data grows

### 6.5 AI Capacity Forecasting (Future/Deferred)

#### 6.5.1 Functional Requirements

The system shall use AI to predict future parking lot occupancy and demand patterns.

**AI Component:**

- Forecast occupancy at 1-hour, 3-hour, and 24-hour horizons  
- Incorporate historical patterns, weather, local events, and holidays  
- Predict demand spikes and capacity crunches  
- Provide confidence intervals for predictions

**Traditional System Component:**

- Use forecasts to inform dynamic pricing adjustments  
- Trigger alerts to staff when predicted occupancy exceeds thresholds  
- Generate operational recommendations for staffing and maintenance  
- Validate predictions against actual outcomes for model training

**Integration Rules:**

- AI shall suggest pricing adjustments; traditional system confirms and applies changes  
- AI shall flag high-demand periods; staff shall validate and respond  
- Forecasts shall be logged with actual outcomes for continuous improvement  
- System shall fall back to rule-based pricing when model uncertainty is high

#### 6.5.2 Model Requirements

| Metric | Requirement |
| :---- | :---- |
| Prediction Horizon | 1 hour to 7 days |
| Forecast Accuracy (MAPE) | \< 15% |
| Retraining Frequency | Weekly |
| Feature Sources | Historical occupancy, weather, events, holidays |
| Explainability | SHAP values or similar for pricing decisions |

### 6.6 AI Anomaly Detection & Staff Support (Future/Deferred)

#### 6.6.1 Functional Requirements

The system shall use AI to detect anomalous patterns and provide actionable insights to parking staff.

**Detection Areas:**

- **Payment Fraud**: Unusual payment patterns, repeated failed transactions, suspicious refund requests  
- **Operational Anomalies**: Sensor failures, unusual occupancy patterns, barrier malfunctions  
- **Security Incidents**: Unauthorized access attempts, suspicious loitering patterns  
- **Revenue Leakage**: Unrecorded entries, bypass attempts, inconsistent audit trails

**AI Component:**

- Establish baseline "normal" behavior patterns for each parking lot  
- Score events against baseline to detect outliers  
- Categorize alerts by severity and type  
- Provide recommended actions for each alert

**Traditional System Component:**

- Route alerts to appropriate staff based on type and severity  
- Log all alert acknowledgments and resolutions  
- Enforce escalation procedures for unresolved alerts  
- Generate weekly anomaly reports for management review

**Integration Rules:**

- AI shall flag anomalies; staff shall investigate and take action  
- All AI alerts shall be logged with context and confidence score  
- Staff shall provide feedback on false positives to improve model  
- Critical security alerts shall bypass normal channels and alert management immediately

#### 6.6.2 Alert Tiers

| Tier | Description | Response Time | Action |
| :---- | :---- | :---- | :---- |
| Info | Pattern deviation, low confidence | 24 hours | Log for weekly review |
| Warning | Moderate anomaly, potential issue | 4 hours | Staff investigation |
| Critical | High confidence fraud/security issue | 15 minutes | Immediate management alert |

### 6.7 AI Chatbot for User Support

#### 6.7.1 Functional Requirements

The system shall provide an AI-powered chatbot to assist users with common queries and issues.

**Supported Use Cases:**

- FAQ: pricing, parking locations, operating hours, payment methods  
- Reservation status: check active reservations, extend time, cancel  
- Payment issues: explain charges, initiate refund requests, update payment methods  
- Account management: password reset, vehicle management, profile updates  
- Navigation: directions to parking lot, barrier location

**AI Component:**

- Understand natural language queries in Vietnamese, English
- Maintain conversation context across multiple turns  
- Recognize user intent and extract relevant entities  
- Escalate to human support when confidence is low or user requests

**Traditional System Component:**

- Execute actual reservation changes, cancellations, and refunds **only after user confirmation through the normal application flow; backend services perform the final business write**  
- Access user accounts and transaction history  
- Escalate complex issues to human support agents  
- Log all conversations for quality improvement

**Integration Rules:**

- Chatbot shall clearly identify itself as AI assistant  
- Users shall have immediate option to "Talk to human" at any point  
- Chatbot shall not make final decisions on refunds or disputes; only human agents can  
- All chatbot escalations shall include conversation history and user context

#### 6.7.2 Escalation Triggers

- User explicitly requests human support  
- Sentiment analysis indicates user frustration or anger  
- Query involves legal disputes, complaints, or refunds over a threshold  
- Chatbot confidence below 70% for intent recognition  
- Same query repeated 3+ times without resolution

<!-- INTEGRATION-START FR-AI -->

#### 6.7.3 AI Requirement Index

Status definitions are in the Baseline Revision Notice. Detailed constraints are defined once in §5.2; X items are recorded in Appendix E.

| FR ID | Status | Required behavior / canonical SRS location | Actor | User Story |
| --- | --- | --- | --- | --- |
| FR-AI-01 | R/A | Natural-language intents, entity extraction and multi-turn conversation: §6.7.1; missing/ambiguous mutation parameters remain to be clarified per the retained AI-team design. | Driver | US-D12 |
| FR-AI-02 | A | Answer static help using approved content; never present brainstorming or unresolved alternatives as active policy (BR-AI-01). | Driver | US-D12 |
| FR-AI-03 | A | Search through approved APIs using user-supplied location, vehicle and time; dynamic values come from returned results (BR-AI-01). | Driver | US-D12 |
| FR-AI-04 | C | Explain deterministic backend-ranked parking results without creating an LLM ranking formula. Personalized/model-based AI ranking is not used in the MVP. | Driver | US-D12 |
| FR-AI-05 | A | Read the signed-in Driver's bookings, session, saved vehicles, payment/refund status and cancellation eligibility through scoped APIs (BR-AI-02). | Driver | US-D12 |
| FR-AI-06 | A | Prepare a reservation draft and populate the normal application form for review/editing; saved-vehicle retrieval does not permit vehicle modification (BR-AI-03). | Driver | US-D12 |
| FR-AI-07 | A | Create a reservation only after final UI confirmation and fresh backend validation; changed terms require reconfirmation (BR-AI-03). | Driver | US-D12 |
| FR-AI-08 | A | Return actual backend reservation/payment status and hand off to ordinary checkout. Financial authority remains §6.1; chatbot mutation requires the confirmed user-review/backend-execution boundary. | Driver | US-D12 |
| FR-AI-09 | A | Distinguish empty results, authentication/authorization failure, invalid input, conflict, timeout and server errors; never fabricate success (BR-AI-05). | Driver | US-D12 |
| FR-AI-10 | R/A | Enforce §6.1 through allowlisted tools, resource authorization and no direct database/gate/policy/penalty access; see BR-AI-02/04. | Driver, Admin | US-D12 |
| FR-AI-11 | A | Explain backend-calculated charges and effective policy values; no hard-coded N or model-generated replacement calculation (BR-AI-01). | Driver | US-D12 |
| FR-BAS-10 | R | Vietnamese/English chatbot, AI identification and human handoff with context: §6.7.1/2. Handoff channel and handler need AI-DEP-07. | Driver, Operator | US-D12, US-O09 |
| FR-BAS-11 | C | Chatbot may read permitted information and prepare a proposed action, but any mutation requires user confirmation and backend execution. The final intent-by-intent allowlist remains an AI-team design detail. | Driver | US-D12 |
| FR-BAS-12 | F | Personalized/model-based AI recommendations remain future. MVP recommendations use deterministic backend ranking and the chatbot may explain that ranking; all available options remain accessible. | Driver | US-D18 |
| FR-BAS-13 | F | Future AI occupancy detection is defined once in §6.3, including uncertainty and human fallback. | Operator | US-O08 |
| FR-BAS-14 | F | Future capacity forecasting and model requirements are defined once in §6.5. | Owner | US-OW08 |
| FR-BAS-15 | F | Future anomaly detection/staff alert tiers are defined once in §6.6. | Operator, Owner | US-O10 |

<!-- INTEGRATION-END FR-AI -->

### 6.8 AI Implementation Phases


<!-- INTEGRATION-START AI-READY -->

#### 6.8.1 Readiness Conclusion

**AI foundation work can start now using synthetic data, mock APIs and provisional contracts. The entire AI MVP is not yet ready for end-to-end implementation or acceptance without decisions below.** This assessment is based on requirements only; no repository, deployed API, provider account, credential, labeled dataset or running model was supplied/verified. A capability in the SRS is not evidence that its backend already exists.

The project does not need to wait for every deferred allocation or architecture issue before building conversation state, API adapters, form handoff, error handling and tests. However, a real write cannot be enabled while the business outcome it changes remains undecided. In particular, the broad §6.7 write list and the limited imported chatbot scope are not silently reconciled.

| Workstream | Can begin now | Still required before integrated delivery |
| --- | --- | --- |
| Chatbot foundation | Yes: intent extraction, Vietnamese/English conversation scaffolding, missing-field clarification, tool registry, synthetic READ fixtures, safe errors | AI-DEP-01/02/05/06/09; actual provider credentials and contract approval. |
| FAQ and parking discovery | Yes with approved sample content and declared mock results | Knowledge approval, current policy/price/availability APIs and freshness handling; AI-DEP-03/05. |
| Personal-data assistant | Yes with fictional accounts and deny-access fixtures | Real auth/resource scope and minimal data contract; AI-DEP-02/06. |
| Reservation assistance | Yes: prepare/edit form and simulated confirmation results | Live writes need approved schema, UI confirmation, payment/hold outcomes and idempotency; AI-DEP-04. |
| Human support/escalation | Yes: UI entry point and mocked delivery result | Actual handler/channel, permissions and off-hours behavior; AI-DEP-07. |
| Image-input LPR | Yes: image upload UI, adapter and deterministic recognition fixtures | Recognizer, permitted labeled dataset, calibrated threshold/equality, manual correction and passage event contract; AI-DEP-08/09. No claim of98% accuracy from specification alone. |
| Personalized recommendations | Prototype only, independently from simple search narration | Release/scope decision C-17 plus ranking/data/evaluation contracts. |
| Occupancy detection / forecasting / anomalies | Design backlog only | Future scope remains §6.3/5/6; not a blocker for chatbot foundation. |

#### 6.8.2 Dependencies and Team Decisions

The named owners below are suggested responsible groups, not assignments to individuals. “Before” identifies the actual gate for a capability; it does not stop unrelated foundation work.

| ID | When it blocks | Suggested owner | Decision / input needed | Concrete exit evidence |
| --- | --- | --- | --- | --- |
| AI-DEP-01 | Before committing MVP scope; not before isolated prototype | BA + team lead + AI lead | Approve intent allowlist: FAQ/search/read/create-draft/create-confirmed; decide extend/cancel/refund-request/profile/vehicle/payment-method/password use cases. Decide personalized recommendations release. | Signed capability matrix with READ, WRITE, UI handoff or deferred per intent; C-16/17; FR-BAS-11/12. |
| AI-DEP-02 | Before real personal-data/API integration | Backend + security + AI | Specify authentication propagation, session identity, per-resource authorization, tool schemas, pagination, error codes, timeout and idempotency/result lookup; decide API facade ownership. | Versioned OpenAPI/JSON Schema and positive/negative fixtures; cross-account test passes; ARCH-DEF-02/05/07/09. |
| AI-DEP-03 | Before meaningful live search/recommendation | Parking/Reservation/Search teams | Select authoritative availability query, pricing quote result and distinction among requested/preferred/allocated slot; define stale-data indication. | Contract with lot/options, compatibility, time window, current data timestamp, quote/policy version and explainable ranking output; C-03/06/08/17. |
| AI-DEP-04 | Before live reservation writes | Reservation + Payment + frontend + AI | Agree create payload/response, confirmation binding, PENDING_PAYMENT behavior, Admin hold expiry, payment handoff, unknown/late callback results and concurrency behavior. | Reviewed form→confirmed command contract, idempotency, hold/quote deadlines and authoritative status lookup; C-04/09/18; TESTER-DEF-12/13; no model-side rule engine. |
| AI-DEP-05 | Before reliable FAQ/policy answers | BA + policy owner + AI | Select approved help content, publication owner, version/effective dates, retrieval access and how unresolved rules are excluded. | Approved knowledge set and regression Q&A; never index the whole conflict register as active policy. Live numerical terms come from backend. |
| AI-DEP-06 | Before external model receives real user content | AI + architect + security/project lead | Choose hosted/self-hosted provider/model, runtime location, secret management, budget, timeouts/fallbacks, data sent, masking, retention and deletion; no provider currently selected. | Configured development adapter can use synthetic data; production data handling and access approved; C-20, ARCH-DEF-02. |
| AI-DEP-07 | Before claiming complete baseline support experience | Operations + BA + frontend | Choose human-support channel/recipient role, availability, queue acknowledgement, allowed history transfer, escalation rules and return-to-app fallback. | Demonstrable Talk to human route with consent/access restrictions and off-hours behavior; Operator is proposed handler, not a confirmed fifth actor or confirmed assignment. |
| AI-DEP-08 | Before model-backed LPR integration and evaluation | LPR/AI + backend + QA | Choose recognizer, plate normalization/format, image schema, model/version, confidence scale/equality, correction flow, event producer and representative licensed test images. | Image→plate/confidence contract plus labeled day/night/invalid/low-confidence fixtures; C-14/15; ARCH-DEF-03; TESTER-DEF-32/35. |
| AI-DEP-09 | Before MVP acceptance/release | QA + AI + project lead | Set representative Vietnamese/English scenarios, failure/security fixtures, measurable acceptance thresholds, cost/latency budget and interpretation of A/B/shadow requirements in demo. | Approved evaluation protocol; model confidence is not a calibrated percentage without evidence; §6.9.4 and TESTER-DEF-27/32 remain unchanged. |
| AI-DEP-10 | Before enabling deferred AI features | BA + data/AI + architect | Approve release, data consent/history, ranking ownership, occupancy labels, forecasting/anomaly datasets and human operational response. | Separate approved feature plan; §6.3/5/6 and personalized scope C-17. Not a prerequisite for FAQ or reservation-form prototype. |

#### 6.8.3 Proposed Tool / Contract Inventory

Tool names describe capabilities, not existing deployed endpoints or final service boundaries. All schemas must define timezone, money units/currency, nullable values, enum compatibility, correlation/request IDs and error semantics where applicable. APIs authorize every request independently of prompts. Do not expose client-supplied identity as a tool override.

| Capability | Effect | Minimum contract content | Readiness / owner dependency |
| --- | --- | --- | --- |
| searchHelpArticles | READ | Query, optional facility; approved snippets + content revision + scope/effective date | Synthetic approved knowledge set; BA publishing owner TBD. |
| searchParkingLots | READ | Location/vehicle/time; backend options, real availability, quoted price/currency, requested/allocated distinction, freshness | Parking/Reservation/Search query owner unresolved; use deterministic fixture and declared timestamp. |
| recommendParkingLots / recommendReservationOptions | READ, optional | Search context; ranked options plus backend reasons | May reuse search result in prototype; do not claim §6.4 personalization is delivered. |
| getMySavedVehicles | READ | Authenticated session; scoped vehicle IDs/plate/type | User domain; exclude unrelated data and identity documents. |
| getMyActiveBookings / getMyBookingDetails | READ | Optional booking ID; authorize ownership; actual lifecycle and timing | Reservation domain; baseline enums preserved; unknown new states rendered safely. |
| getMyCurrentParkingSession | READ | Authenticated session; actual recorded location/times and status | Session API ownership must be agreed; no invented live occupancy. |
| getMyPaymentStatus / getMyRefundStatus | READ | Booking/transaction ID; authorized verified financial states | Payment domain; distinguish request approval from actual settlement/refund. |
| checkMyCancellationEligibility | READ | Booking ID; authoritative eligibility/reason/estimated refund and applicable policy version | Reservation/Payment collaboration; result is provisional until cancellation execution. |
| prepareReservationForm | UI draft, not domain WRITE | Chosen backend option, saved vehicle, local timezone/offset, start/end; editable form | Frontend-owned review handoff; can be built without creating a booking. |
| createReservation | WRITE after UI confirmation | Confirmed vehicle/lot/mode/requested zone/slot/time/terms, auth, idempotency; actual booking ID/state/payment requirement/hold deadline | Exact schema and orchestration pending AI-DEP-04; revalidate current conditions and reject/reconfirm stale terms. |
| cancel/extend/refund-request/profile/vehicle/payment-method updates | Not enabled in limited candidate pilot | Schemas, authority and confirmation not yet approved | C-16 determines which are direct backend actions, UI handoff or deferred. Existing baseline listing is retained. |
| LPR recognize image / correct plate | Recognition READ-like analysis; correction audited WRITE by Operator | Image/event ID/time; normalized plate/confidence/model version; human correction record | AI-DEP-08 determines producer and ownership. Recognizer cannot grant gate access. |

#### 6.8.4 Proposed Delivery Gates

1. **Foundation with mocks:** implement approved-sample FAQ, intent/parameter collection, account-isolated fixtures, search responses, reservation form preparation, API failures and human-handoff UI. Label all fixture results as test data. No actual reservation, payment or gate claim is made.
2. **Read integration:** satisfy AI-DEP-02/03/05/06; verify identity isolation, stale/empty/error responses and numeric values from backend. Read integrations may ship internally while mutation scope remains open.
3. **Confirmed reservation integration:** satisfy AI-DEP-01/04 for this action; execute only after final form confirmation, verify returned PENDING_PAYMENT and checkout handoff, and test duplicate/unknown/expired outcomes. Allocation, price and refund rules remain backend-owned.
4. **Support and LPR integration:** independently satisfy AI-DEP-07 and AI-DEP-08. Do not wait for real camera hardware if the team confirms the image-upload MVP already stated in §6.2.
5. **MVP acceptance:** satisfy AI-DEP-09 and applicable original §6.9 checks. Required scenarios include cross-account access attempts, malicious instructions inside retrieved help/API text, stale confirmed forms, duplicate writes, provider timeout, model outage, LPR equality/low confidence and successful human escalation. Report measured results; do not lower original targets without recorded approval.

A minimal decision package to unblock meaningful integration is: agreed chatbot intent matrix; versioned tool contracts/auth boundary; frontend confirmation contract; approved knowledge pack; provider/data-handling choice; LPR input/model/evaluation choice; human-support route. Core business-rule decisions are additionally required only when a corresponding live mutation, price explanation or gate outcome depends on them. No endpoint names, deployed infrastructure, model quality, API readiness or budget sufficiency are assumed by this document.

<!-- INTEGRATION-END AI-READY -->

### 6.9 AI Governance & Risk Mitigation

#### 6.9.1 Model Governance

- All AI models shall be version-controlled and auditable  
- Model decisions affecting users shall be explainable  
- Regular bias audits shall be conducted on recommendation and pricing models  
- Human-in-the-loop required for all financial and legal decisions  
- Model performance shall be monitored continuously with automated alerts for degradation

#### 6.9.2 Privacy & Data Protection

- AI training data shall be anonymized and aggregated where possible  
- User consent shall be obtained for personalized features  
- Data retention policies shall apply to AI training data  
- Right to deletion shall extend to AI-derived profiles  
- Regular privacy impact assessments for new AI features

#### 6.9.3 Fallback & Degradation

| AI Feature | Fallback Strategy |
| :---- | :---- |
| License Plate Recognition | Manual entry by attendant, QR code fallback |
| Spot Status Detection | Existing IoT sensors, staff manual updates |
| Recommendations | Distance-based default ranking |
| Capacity Forecasting | Rule-based pricing and staffing |
| Anomaly Detection | Traditional threshold-based alerts |
| Chatbot | FAQ page, email support, phone support |

#### 6.9.4 Testing & Validation

- AI models shall undergo rigorous testing before production deployment  
- A/B testing required for all user-facing AI features  
- Shadow mode deployment for validation before full rollout  
- Regular accuracy testing with holdout datasets  
- User feedback loops for continuous improvement

---
