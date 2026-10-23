# UC-LOT-01 — Manage facility profile, layout and service state

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Prospective Owner, Owner, Admin (application review), assigned Operator

[Use-case index](README.md) · [SRS module](../srs/modules/02-facility-capacity.md)

## Goal

Manage facility profile, layout and service state.

## Preconditions

Operational/configuration actions require current owned-lot or assigned-task authority. Initial first-lot proposal setup instead uses a valid application-bound onboarding credential without a normal Owner session; Admin review requires explicit current review permission.

## Main flow

1. For the first lot, save setup in the initial Partner application before email verification and combined review (UC-AUTH-01/02). An approved Owner submits each additional lot for its own Admin approval/rejection.
2. Keep rejected additional-lot history immutable without affecting the Owner or other lots. Draft as new creates a new proposal with independent review, even for identical content; submission/provisioning retries cannot duplicate applications/sites.
3. After approval, provision/activate once with visible retryable failure/unknown state. Open only after required Facility configuration and Operations readiness under current permissions; approval never opens automatically.
4. Configure owned profile/layout/category pools and supported 3D templates. User owns application evidence; Facility configuration, Operations accepted occupancy/claims/plan coverage and Payment terms remain separate.
5. For spatial deactivation explicitly select/handle descendants and resolve operational dependencies; FE multiselect submits one-lot all-or-nothing batches. Validate every item's authorization/version/dependencies and return per-item errors with no mutation if any fails.
6. Submit planned service/capacity change; Operations restricts conflicting claims and evaluates commitments. Commit after compatible recovery or explicit failure outcomes; emergency physical closure is recorded immediately and stops admission safely.

## Alternatives and failures

No Admin document editing on additional-lot applications, silent descendant cascade, cross-lot/partial batch or overwrite of rejection. Do not erase occupancy/displace occupants; communication failure cannot roll back emergency physical facts.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-LOT-01, FR-LOT-02, FR-LOT-03, FR-LOT-04  
**US:** US-O05, US-OW01  
**Rules:** BR-CAP-01, BR-EMERG-01, BR-AUTH-01, BR-AUTH-02

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
