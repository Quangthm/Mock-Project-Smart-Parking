# SmartPark User Stories — v0.8.5

**Document Type:** User Story catalogue  
**Owner:** BA / team lead (review responsibility; no individual assignment)  
**Status:** WORKING — review pending  
**Related SRS Baseline:** Supplied v0.8.5 Working Baseline Revision  
**Date:** 2026-10-01

## Purpose and scope

C means a confirmed fact already recorded in supplied v0.8.5, not a new approval by this document. A means PROPOSED; X means OPEN/conflicting and inactive; F means FUTURE/DEFERRED. Legacy R means a pointer to stated SRS behavior. Mixed R/A, R/X or A/X must be read clause by clause; their candidate clauses do not become confirmed. Main-flow detail marked PROPOSED stays proposed even inside an otherwise current-MVP use case. The split does not resolve architecture, provider, policy-default or event-contract questions. N values and demo/fixture labels retain their original authority.

All 43 original User Story identifiers and goal texts are retained. Existing/imported labels describe traceability history, not approval of every linked FR. Monthly stories belong to the confirmed MVP in C-19. Each goal is defined once here; SRS and use cases retain only references. Linked future FRs remain future. No new acceptance condition or business permission is inferred from a goal.

## User Story catalogue

| US ID | Actor | Retained user goal | Original status / scope | FR coverage | Use cases |
| --- | --- | --- | --- | --- | --- |
| US-D01 | Driver | As Driver, I want to book parking ahead of arrival to obtain the entitlement shown in the confirmation. | Existing; C-03 | FR-VEH-04, FR-POL-06, FR-CAP-03, FR-CAP-04, FR-RES-01, FR-RES-02, FR-RES-03, FR-RES-04, FR-RES-05, FR-RES-06, FR-RES-07, FR-RES-11, FR-RES-12, FR-RES-13, FR-BAS-01, FR-BAS-02, FR-BAS-04 | UC-VEH-01, UC-RES-01, UC-RES-02, UC-RES-03, UC-GATE-01 |
| US-D02 | Driver | As Driver, I want to pay parking charges and verify settlement of my parking transaction. | Existing; C-08/09/10/21 | FR-PAY-02, FR-PAY-03, FR-PAY-04, FR-PAY-05, FR-PAY-09, FR-PAY-10, FR-PAY-11, FR-BAS-05, FR-BAS-16 | UC-POL-01, UC-GATE-02, UC-PAY-01 |
| US-D03 | Driver | As Driver, I want to view parking information and availability to choose a suitable facility. | Existing | FR-CAP-01, FR-CAP-02 | UC-SEARCH-01 |
| US-D04 | Driver | As Driver, I want to review my parking history. | Existing | FR-GATE-08 | UC-GATE-02 |
| US-D05 | Driver | As Driver, I want to review my reservation history and outcomes. | Existing | FR-RES-10 | UC-RES-03 |
| US-D06 | Driver | As Driver, I want to review my payment and refund history. | Existing | FR-PAY-07 | UC-PAY-02 |
| US-D07 | Driver | As Driver, I want to submit and follow an appeal about a parking issue. | Existing; detail proposed C-12 | FR-INC-05, FR-INC-07 | UC-INC-02 |
| US-O01 | Operator | As Operator, I want to check vehicles in and record authorized arrival/entry. | Existing; C-01/18 | FR-VEH-03, FR-VEH-04, FR-CAP-03, FR-CAP-04, FR-CAP-05, FR-CAP-06, FR-RES-07, FR-RES-11, FR-RES-12, FR-RES-13, FR-GATE-01, FR-GATE-02, FR-GATE-03, FR-GATE-09 | UC-VEH-01, UC-RES-01, UC-RES-02, UC-RES-03, UC-GATE-01 |
| US-O02 | Operator | As Operator, I want to check vehicles out and record actual departure and settlement. | Existing; C-21 | FR-CAP-05, FR-CAP-06, FR-GATE-05, FR-GATE-06, FR-GATE-07, FR-GATE-09, FR-PAY-02, FR-PAY-03, FR-PAY-04, FR-PAY-05, FR-PAY-11, FR-BAS-16 | UC-RES-02, UC-GATE-01, UC-GATE-02, UC-PAY-01 |
| US-O03 | Operator | As Operator, I want to monitor assigned parking lots and current operations. | Existing | FR-LOT-07, FR-CAP-02, FR-CAP-06, FR-GATE-04, FR-MAP-02, FR-MAP-04 | UC-LOT-02, UC-SEARCH-01, UC-RES-02, UC-RPT-01, UC-MAP-01 |
| US-O04 | Operator | As Operator, I want to handle emergencies and record operational actions. | Existing; detail pending C-13 | FR-OPS-01, FR-OPS-02 | UC-OPS-01 |
| US-O05 | Operator | As Operator, I want to resolve parking incidents and reservation conflicts. | Existing; detail pending C-07/12 | FR-LOT-04, FR-CAP-07, FR-RES-09, FR-GATE-05, FR-GATE-06, FR-GATE-07, FR-INC-01, FR-INC-03, FR-INC-04, FR-INC-06, FR-OPS-03, FR-OPS-06, FR-BAS-02, FR-BAS-04 | UC-LOT-01, UC-LOT-02, UC-POL-01, UC-RES-01, UC-RES-02, UC-RES-03, UC-GATE-02, UC-INC-01, UC-OPS-01 |
| US-OW01 | Owner | As Owner, I want to manage my facility profile and layout. | Existing | FR-LOT-01, FR-LOT-02, FR-LOT-03, FR-LOT-04 | UC-LOT-01 |
| US-OW02 | Owner | As Owner, I want to configure lot policies and prices within authorized scope. | Existing; C-04/08 | FR-POL-01, FR-POL-03, FR-POL-04, FR-POL-05, FR-POL-06, FR-POL-07, FR-POL-08, FR-CAP-07, FR-PAY-01, FR-PAY-08, FR-PAY-09, FR-PAY-10, FR-PAY-11, FR-BAS-04, FR-BAS-05 | UC-POL-01, UC-RES-01, UC-GATE-02, UC-NOT-01 |
| US-OW03 | Owner | As Owner, I want to manage Operators and their lot-scoped access. | Existing | FR-AUTH-04 | UC-AUTH-03 |
| US-OW04 | Owner | As Owner, I want to review transactions and facility reports to monitor operation and revenue. | Existing; report coverage clarified | FR-PAY-06, FR-RPT-02, FR-BAS-07 | UC-INC-02, UC-RPT-01 |
| US-OW05 | Owner | As Owner, I want to view device-related reports for my facilities. | Future/deferred while IoT scope is future; device management belongs to Operator. | FR-LOT-07 | UC-LOT-02 |
| US-A01 | Admin | As Admin, I want to manage accounts, roles and authorized platform access. | Existing | FR-AUTH-02, FR-AUTH-05, FR-AUTH-06, FR-BAS-08 | UC-AUTH-01, UC-AUTH-02 |
| US-A02 | Admin | As Admin, I want to manage platform settings and integration configuration. | Existing; device boundary C-02 | FR-LOT-06, FR-POL-02, FR-POL-03, FR-POL-04, FR-POL-05, FR-POL-07, FR-POL-08 | UC-POL-01, UC-POL-02 |
| US-A03 | Admin | As Admin, I want to inspect protected audit records of important actions. | Existing | FR-RPT-03, FR-RPT-04 | UC-RPT-02 |
| US-D08 | Driver | As Driver, I want to register, authenticate and manage my own profile. | Imported refinement; C-11/14 | FR-AUTH-01, FR-AUTH-02, FR-AUTH-03, FR-BAS-08 | UC-AUTH-01 |
| US-D09 | Driver | As Driver, I want to manage my vehicles and resolve disputed plate binding. | Imported refinement; C-11 | FR-VEH-01, FR-VEH-02, FR-VEH-03 | UC-VEH-01, UC-VEH-02 |
| US-D10 | Driver | As Driver, I want to understand cancellation eligibility and track cancellation/refund outcomes. | Imported refinement | FR-RES-08, FR-RES-09, FR-RES-12, FR-PAY-06 | UC-RES-03, UC-INC-02 |
| US-D11 | Driver | As Driver, I want to receive supported notifications and reminders about my services. | Imported refinement; baseline channels/categories retained | FR-PAY-08, FR-RPT-01, FR-BAS-06 | UC-NOT-01 |
| US-D12 | Driver | As Driver, I want to use conversational help to search, inspect my data and prepare reviewed reservations. | Imported refinement; broader write scope C-16 | FR-AI-01, FR-AI-02, FR-AI-03, FR-AI-04, FR-AI-05, FR-AI-06, FR-AI-07, FR-AI-08, FR-AI-09, FR-AI-10, FR-AI-11, FR-BAS-10, FR-BAS-11 | UC-AI-01, UC-AI-02, UC-AI-03 |
| US-O06 | Operator | As Operator, I want to use controlled offline processing and reconcile after recovery. | F; outside MVP | FR-OPS-04, FR-OPS-05 | UC-OPS-02 |
| US-O08 | Operator | As Operator, I want to review LPR results to verify vehicles; future visual detection may support incidents. | MVP LPR C-14; per-slot AI F | FR-GATE-01, FR-INC-02, FR-BAS-09, FR-BAS-13 | UC-GATE-01, UC-LPR-01, UC-FUT-01 |
| US-OW07 | Owner | As Owner, I want to control appeal delegation and approve financial refunds. | Imported refinement; C-12 | FR-PAY-06, FR-INC-05 | UC-INC-02 |
| US-A04 | Admin | As Admin, I want to review disputed vehicle binding requests. | Imported refinement; C-11 | FR-VEH-02 | UC-VEH-02 |
| US-A05 | Admin | As Admin, I want to apply approved data retention and deletion policies. | Imported refinement; C-20 | FR-RPT-05 | UC-RPT-02 |
| US-A06 | Admin | As Admin, I want to run isolated demo scenarios without touching operational resources. | Proposed product feature; C-15 | FR-SIM-01 | UC-SIM-01 |
| US-A07 | Admin | As Admin, I want to configure approved recognition thresholds and audit changes. | Imported refinement; C-14 | FR-POL-09 | UC-POL-02 |
| US-D16 | Driver | As Driver, I want to use maps and recorded vehicle location to find the facility and my vehicle. | Imported refinement | FR-MAP-01, FR-MAP-02, FR-MAP-03, FR-MAP-04 | UC-SEARCH-01, UC-MAP-01 |
| US-D17 | Driver | As Driver, I want to ask for reallocation or its reason according to the reservation rules. | Added traceability to existing §3.4.6; C-18 details open | FR-BAS-03 | UC-RES-02 |
| US-O09 | Operator | As Operator, I want to receive a human-support escalation with authorized conversation context. | Proposed handler role for existing §6.7 handoff; AI-DEP-07 | FR-BAS-10 | UC-AI-03 |
| US-D18 | Driver | As Driver, I want understandable parking suggestions while retaining access to all options. | Future personalized/model-based recommendation scope; MVP ranking is deterministic backend logic. | FR-BAS-12 | UC-FUT-02 |
| US-OW08 | Owner | As Owner, I want to use forecasts to plan capacity and pricing decisions. | F; baseline §6.5 | FR-BAS-14 | UC-FUT-03 |
| US-O10 | Operator | As Operator, I want to review automated anomaly alerts and investigate affected operations. | F; baseline §6.6 | FR-BAS-15 | UC-FUT-03 |
| US-D13 | Driver | Purchase a monthly plan after reviewing price, period and entitlement type. | C — monthly MVP under C-19 / Appendix F | FR-MON-02, FR-MON-03, FR-MON-04, FR-MON-05 | UC-MON-02 |
| US-D14 | Driver | Renew on time and understand what happens after unpaid, late or uncertain payment. | C — monthly MVP under C-19 / Appendix F | FR-MON-08, FR-MON-09, FR-MON-10, FR-MON-14 | UC-MON-03, UC-MON-04 |
| US-D15 | Driver | Cancel a monthly plan and follow any eligible refund. | C — monthly MVP under C-19 / Appendix F | FR-MON-11 | UC-MON-05 |
| US-O07 | Operator | Validate monthly rights and settle uncovered usage without duplicate charges. | C — monthly MVP under C-19 / Appendix F | FR-MON-06, FR-MON-07, FR-MON-08, FR-MON-14 | UC-MON-02, UC-MON-03, UC-MON-04 |
| US-OW06 | Owner | Manage plan prices, capacity promises, changes and refunds. | C — monthly MVP under C-19 / Appendix F | FR-MON-01, FR-MON-04, FR-MON-06, FR-MON-12, FR-MON-13 | UC-MON-01, UC-MON-02 |

## Open questions / proposal treatment

US-OW05 stays future/deferred Owner device reporting despite FR-LOT-07. US-D18 remains the future personalized-story record; current deterministic ranking is also mapped through US-D12/FR-AI-04. Plate-binding review, offline processing, simulator controls, recognition threshold configuration and AI mutation intents retain their corresponding A/X/F scope. Existing goal text does not grant a new role or permission.

**Retained traceability defect (SPLIT-CF-21):** FR-LOT-05 points to US-O03/US-O05, but their original reverse FR lists omit it. Proposed review-only correction: add FR-LOT-05 to both reverse lists. The original coverage column above is preserved; the UC column reflects the mapped flow coverage.

[Use cases](../use-cases/smartpark-use-cases-v0.8.5.md) · [FR traceability](../requirements-traceability/smartpark-traceability-v0.8.5.md) · [SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Review](../srs/revisions/smartpark-v0.8.5-split-review.md)
