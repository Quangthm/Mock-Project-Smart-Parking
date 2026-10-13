# User stories — v1.0 RC1

All 43 IDs retained. Four goal texts changed explicitly (US-OW03, US-OW06, US-OW07, US-D15); US-OW05 now current read-only diagnostics. Scope labels distinguish current/pending, POST-v1.0 details and FUTURE. Goals do not grant independent permissions.

| ID | Actor | Goal | Status | FR coverage | Use cases |
|---|---|---|---|---|---|
| US-D01 | Driver | As Driver, I want to book parking ahead of arrival to obtain the entitlement shown in the confirmation. | CONFIRMED — implementation pending / not assessed | FR-VEH-04, FR-POL-06, FR-CAP-03, FR-CAP-04, FR-RES-01, FR-RES-02, FR-RES-03, FR-RES-04, FR-RES-05, FR-RES-06, FR-RES-07, FR-RES-11, FR-RES-12, FR-RES-13, FR-BAS-01, FR-BAS-02, FR-BAS-04 | UC-GATE-01, UC-RES-01, UC-RES-02, UC-RES-03, UC-VEH-01 |
| US-D02 | Driver | As Driver, I want to pay parking charges and verify settlement of my parking transaction. | CONFIRMED — implementation pending / not assessed | FR-PAY-02, FR-PAY-03, FR-PAY-04, FR-PAY-05, FR-PAY-09, FR-PAY-10, FR-PAY-11, FR-BAS-05, FR-BAS-16 | UC-GATE-02, UC-PAY-01, UC-POL-01 |
| US-D03 | Driver | As Driver, I want to view parking information and availability to choose a suitable facility. | CONFIRMED — implementation pending / not assessed | FR-CAP-01, FR-CAP-02 | UC-SEARCH-01 |
| US-D04 | Driver | As Driver, I want to review my parking history. | CONFIRMED — implementation pending / not assessed | FR-GATE-08 | UC-GATE-02 |
| US-D05 | Driver | As Driver, I want to review my reservation history and outcomes. | CONFIRMED — implementation pending / not assessed | FR-RES-10 | UC-RES-03 |
| US-D06 | Driver | As Driver, I want to review my payment and refund history. | CONFIRMED — implementation pending / not assessed | FR-PAY-07 | UC-PAY-02 |
| US-D07 | Driver | As Driver, I want to submit and follow an appeal about a parking issue. | CONFIRMED — implementation pending / not assessed | FR-INC-05, FR-INC-07 | UC-INC-02 |
| US-O01 | Operator | As Operator, I want to check vehicles in and record authorized arrival/entry. | CONFIRMED — implementation pending / not assessed | FR-VEH-03, FR-VEH-04, FR-CAP-03, FR-CAP-04, FR-CAP-05, FR-CAP-06, FR-RES-07, FR-RES-11, FR-RES-12, FR-RES-13, FR-GATE-01, FR-GATE-02, FR-GATE-03, FR-GATE-09 | UC-GATE-01, UC-RES-01, UC-RES-02, UC-RES-03, UC-VEH-01 |
| US-O02 | Operator | As Operator, I want to check vehicles out and record actual departure and settlement. | CONFIRMED — implementation pending / not assessed | FR-CAP-05, FR-CAP-06, FR-GATE-05, FR-GATE-06, FR-GATE-07, FR-GATE-09, FR-PAY-02, FR-PAY-03, FR-PAY-04, FR-PAY-05, FR-PAY-11, FR-BAS-16 | UC-GATE-01, UC-GATE-02, UC-PAY-01, UC-RES-02 |
| US-O03 | Operator | As Operator, I want to monitor assigned parking lots and current operations. | CONFIRMED — implementation pending / not assessed | FR-LOT-07, FR-CAP-02, FR-CAP-06, FR-GATE-04, FR-MAP-02, FR-MAP-04 , FR-LOT-05 | UC-LOT-02, UC-MAP-01, UC-RES-02, UC-RPT-01, UC-SEARCH-01 |
| US-O04 | Operator | As Operator, I want to handle emergencies and record operational actions. | CONFIRMED — implementation pending / not assessed | FR-OPS-01, FR-OPS-02 | UC-OPS-01 |
| US-O05 | Operator | As Operator, I want to resolve parking incidents and reservation conflicts. | CONFIRMED — implementation pending / not assessed | FR-BAS-02, FR-BAS-04, FR-CAP-07, FR-GATE-05, FR-GATE-06, FR-GATE-07, FR-INC-01, FR-INC-03, FR-INC-04, FR-INC-06, FR-LOT-04, FR-LOT-05, FR-OPS-03, FR-OPS-06, FR-PAY-06, FR-RES-09 | UC-GATE-02, UC-INC-01, UC-LOT-01, UC-LOT-02, UC-OPS-01, UC-POL-01, UC-RES-01, UC-RES-02, UC-RES-03 |
| US-OW01 | Owner | As Owner, I want to manage my facility profile and layout. | CONFIRMED — implementation pending / not assessed | FR-LOT-01, FR-LOT-02, FR-LOT-03, FR-LOT-04 | UC-LOT-01 |
| US-OW02 | Owner | As Owner, I want to configure lot policies and prices within authorized scope. | CONFIRMED — implementation pending / not assessed | FR-POL-01, FR-POL-03, FR-POL-04, FR-POL-05, FR-POL-06, FR-POL-07, FR-POL-08, FR-CAP-07, FR-PAY-01, FR-PAY-08, FR-PAY-09, FR-PAY-10, FR-PAY-11, FR-BAS-04, FR-BAS-05 | UC-GATE-02, UC-NOT-01, UC-POL-01, UC-RES-01 |
| US-OW03 | Owner | As Owner, I want to create Operator accounts and assign or revoke their owned-lot access. | CONFIRMED — implementation pending / not assessed | FR-AUTH-04 | UC-AUTH-03 |
| US-OW04 | Owner | As Owner, I want to review transactions and facility reports to monitor operation and revenue. | CONFIRMED — implementation pending / not assessed | FR-PAY-06, FR-RPT-02, FR-BAS-07 | UC-INC-02, UC-RPT-01 |
| US-OW05 | Owner | As Owner, I want to view device-related reports for my facilities. | CONFIRMED — implementation pending / not assessed | FR-LOT-07 | UC-LOT-02 |
| US-A01 | Admin | As Admin, I want to manage accounts, roles and authorized platform access. | CONFIRMED — implementation pending / not assessed | FR-AUTH-02, FR-AUTH-05, FR-AUTH-06, FR-BAS-08 | UC-AUTH-01, UC-AUTH-02 |
| US-A02 | Admin | As Admin, I want to manage platform settings and integration configuration. | CONFIRMED — implementation pending / not assessed | FR-LOT-06, FR-POL-02, FR-POL-03, FR-POL-04, FR-POL-05, FR-POL-07, FR-POL-08 | UC-POL-01, UC-POL-02 |
| US-A03 | Admin | As Admin, I want to inspect protected audit records of important actions. | CONFIRMED — implementation pending / not assessed | FR-RPT-03, FR-RPT-04 | UC-RPT-02 |
| US-D08 | Driver | As Driver, I want to register, authenticate and manage my own profile. | CONFIRMED — implementation pending / not assessed | FR-AUTH-01, FR-AUTH-02, FR-AUTH-03, FR-BAS-08 | UC-AUTH-01 |
| US-D09 | Driver | As Driver, I want to manage my vehicles and resolve disputed plate binding. | CONFIRMED — implementation pending / not assessed | FR-VEH-01, FR-VEH-02, FR-VEH-03 | UC-VEH-01, UC-VEH-02 |
| US-D10 | Driver | As Driver, I want to understand cancellation eligibility and track cancellation/refund outcomes. | CONFIRMED — implementation pending / not assessed | FR-RES-08, FR-RES-09, FR-RES-12, FR-PAY-06 | UC-INC-02, UC-RES-03 |
| US-D11 | Driver | As Driver, I want to receive supported notifications and reminders about my services. | CONFIRMED — implementation pending / not assessed | FR-PAY-08, FR-RPT-01, FR-BAS-06 | UC-NOT-01 |
| US-D12 | Driver | As Driver, I want to use conversational help to search, inspect my data and prepare reviewed reservations. | CONFIRMED DIRECTION — detailed integration/evaluation POST-v1.0 | FR-AI-01, FR-AI-02, FR-AI-03, FR-AI-04, FR-AI-05, FR-AI-06, FR-AI-07, FR-AI-08, FR-AI-09, FR-AI-10, FR-AI-11, FR-BAS-10, FR-BAS-11 | UC-AI-01, UC-AI-02, UC-AI-03 |
| US-O06 | Operator | As Operator, I want to use controlled offline processing and reconcile after recovery. | FUTURE — outside MVP | FR-OPS-04, FR-OPS-05 | UC-OPS-02 |
| US-O08 | Operator | As Operator, I want to review LPR results to verify vehicles; future visual detection may support incidents. | CONFIRMED manual/operational scope; LPR details POST-v1.0; per-slot AI FUTURE | FR-GATE-01, FR-INC-02, FR-BAS-09, FR-BAS-13 | UC-FUT-01, UC-GATE-01, UC-LPR-01 |
| US-OW07 | Owner | As Owner, I want to handle appeal escalations and monthly cancellations within my owned-lot scope. | CONFIRMED — implementation pending / not assessed | FR-PAY-06, FR-INC-05 | UC-INC-02 |
| US-A04 | Admin | As Admin, I want to review disputed vehicle binding requests. | CONFIRMED — implementation pending / not assessed | FR-VEH-02 | UC-VEH-02 |
| US-A05 | Admin | As Admin, I want to apply approved data retention and deletion policies. | CONFIRMED — implementation pending / not assessed | FR-RPT-05 | UC-RPT-02 |
| US-A06 | Admin | As Admin, I want to run isolated demo scenarios without touching operational resources. | FUTURE — outside MVP | FR-SIM-01 | UC-SIM-01 |
| US-A07 | Admin | As Admin, I want to configure approved recognition thresholds and audit changes. | CONFIRMED DIRECTION — detailed integration/evaluation POST-v1.0 | FR-POL-09 | UC-POL-02 |
| US-D16 | Driver | As Driver, I want to use maps and recorded vehicle location to find the facility and my vehicle. | CONFIRMED — implementation pending / not assessed | FR-MAP-01, FR-MAP-02, FR-MAP-03, FR-MAP-04 | UC-MAP-01, UC-SEARCH-01 |
| US-D17 | Driver | As Driver, I want to ask for reallocation or its reason according to the reservation rules. | CONFIRMED — implementation pending / not assessed | FR-BAS-03 | UC-RES-02 |
| US-O09 | Operator | As Operator, I want to receive a human-support escalation with authorized conversation context. | CONFIRMED DIRECTION — detailed integration/evaluation POST-v1.0 | FR-BAS-10 | UC-AI-03 |
| US-D18 | Driver | As Driver, I want understandable parking suggestions while retaining access to all options. | FUTURE — outside MVP | FR-BAS-12 | UC-FUT-02 |
| US-OW08 | Owner | As Owner, I want to use forecasts to plan capacity and pricing decisions. | FUTURE — outside MVP | FR-BAS-14 | UC-FUT-03 |
| US-O10 | Operator | As Operator, I want to review automated anomaly alerts and investigate affected operations. | FUTURE — outside MVP | FR-BAS-15 | UC-FUT-03 |
| US-D13 | Driver | Purchase a monthly plan after reviewing price, period and entitlement type. | CONFIRMED — implementation pending / not assessed | FR-MON-02, FR-MON-03, FR-MON-04, FR-MON-05 | UC-MON-02 |
| US-D14 | Driver | Renew on time and understand what happens after unpaid, late or uncertain payment. | CONFIRMED — implementation pending / not assessed | FR-MON-08, FR-MON-09, FR-MON-10, FR-MON-14 | UC-MON-03, UC-MON-04 |
| US-D15 | Driver | Cancel a monthly plan after explicitly resolving dependent future reservations and follow any eligible refund. | CONFIRMED — implementation pending / not assessed | FR-MON-11 | UC-MON-05 |
| US-O07 | Operator | Validate monthly rights and settle uncovered usage without duplicate charges. | CONFIRMED — implementation pending / not assessed | FR-MON-06, FR-MON-07, FR-MON-08, FR-MON-14 | UC-MON-02, UC-MON-03, UC-MON-04 |
| US-OW06 | Owner | Manage available-only monthly plans, prices, changes and eligible refunds. | CONFIRMED — implementation pending / not assessed | FR-MON-01, FR-MON-04, FR-MON-06, FR-MON-12, FR-MON-13 | UC-MON-01, UC-MON-02 |


## SEC-01/SEC-02 added stories

The original 43 IDs/goals remain. New security stories cover all four roles without implying cross-account rights.

| ID | Actor | Goal | Status | FR coverage | Use cases |
|---|---|---|---|---|---|
| US-SEC01 | Driver, Operator, Owner, Admin | As an account holder, I want to optionally enroll/manage TOTP and complete its challenge when enabled. | CONFIRMED MVP; implementation pending | FR-AUTH-07, FR-AUTH-08 | UC-AUTH-01, UC-AUTH-04 |
| US-SEC02 | Driver, Operator, Owner, Admin | As an account holder, I want to recover a lost Authenticator with backup codes or submit a restricted account-recovery request. | CONFIRMED MVP; implementation pending | FR-AUTH-09, FR-AUTH-10 | UC-AUTH-05, UC-AUTH-06 |
| US-SEC03 | Admin | As Admin, I want to review account-recovery evidence and authorize scoped password/factor resets without knowing user secrets or approving my own case. | CONFIRMED MVP; implementation pending | FR-AUTH-10, FR-AUTH-11 | UC-AUTH-06 |
