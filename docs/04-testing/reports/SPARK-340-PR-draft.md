# feat(admin): configure platform policies and integration settings [SPARK-340]

Target: `develop`. Preparation only — not submitted. Keep this PR **DRAFT** until the P5 findings and recorded acceptance gaps have a disposition. The feature branch is now committed and published for tester verification; no remote PR has been created.

> Publication follow-up: user authorized committing/pushing the feature branch for tester verification. This draft is not an instruction to merge or submit a PR. Consult [tester handoff](SPARK-340-tester-handoff.md) and the latest session section for actual Git delivery status; earlier baseline descriptions below are historical preparation context.

## Summary

Admin can manage versioned platform policy defaults and Owner override bounds, inspect effective policy provenance, and configure permitted Payment integration settings through service-owned APIs. The Admin console exposes catalogue, draft/history/activation, effective values and masked integration status while preserving service ownership and role boundaries.

## Related Issue

Refs SPARK-340, SPARK-368, SPARK-369, SPARK-370.

SPARK-273 supplies the reservation commercial integration needed for the linked booking-snapshot UAT case. This draft does not claim that dependency is implemented or that the parent story is fully accepted.

## Changes

- [x] Add typed14-key policy catalogue, effective PLATFORM/SITE resolution and inheritance provenance.
- [x] Add platform draft create/update, optimistic version checks, revision listing/by-ID and immediate/scheduled activation.
- [x] Coordinate Owner publication and Admin activation through the shared compatibility lock.
- [x] Add authenticated Admin Payment settings with mode-isolated credentials, masked responses, URL allowlist, persisted history and protected checksum bindings.
- [x] Add read-only registered Notification fixture status and Admin FE views using existing auth clients.
- [x] Add BE/FE regression coverage, reusable Postman collection, sanitized evidence and delivery documentation.
- [x] Implement approved dedicated signing-binding storage and offline legacy repair (F6/F7), and reject ignored platform site-enablement input (F8).
- [ ] Obtain independent re-review, prepare the target DB with reviewed additive DDL and validate actual legacy checkout mappings before rollout; assign remaining P5 findings.

## Implementation Details

HTTP controllers dispatch through IMediator and application-owned ports. Operation owns platform policy resolution/lifecycle; Payment retains secrets, gateway mode, history and checkout signing; Notification exposes its registered local fixture adapters. Admin configuration uses TOKEN freshness and local role/scope checks. Payment/Notification Admin routes explicitly use strict UserServiceJwt; their pre-existing non-Admin authentication surfaces are separate dependencies.

Policy updates use an explicit conditional write with expectedUpdatedAt/If-Match. Activation and Owner publication share transaction-scoped serialization and reject incompatible intervals/composed settings. Accepted reservation JSON is guarded against mutation in the existing Operations UnitOfWork; full booking-chain HTTP proof remains blocked by SPARK-273.

Payment DataProtection keys use a persistent volume. Durable signing bindings are written before outbound creation and remain bound to the issuing configuration. Approved F6/F7 repair moves new writes to checkout_signing_bindings with financial-history retention and FK RESTRICT, retains exact protected legacy-row reads, and provides an offline dry-run/apply repair tool. Missing bindings fail closed; target DB upgrade and owner-verified legacy mappings must be completed before rollout. See the F6–F8 rollout guide.

## Testing

- [x] Build succeeds — F6–F8 BE build0 errors/29 warnings, including advisory-feed NU1900; prior P6 FE build succeeded and no FE implementation changed in this repair.
- [x] Unit tests pass — latest whole-solution run1176 passed/0failed/113skipped. Skips are not counted as executed passes.
- [x] Scoped FE/Postman regression —latest33 passed (30 Admin FE +3 collection tests); earlier P6 full FE261 passed.
- [x] Integration tests where enabled — latest real PG Payment8/8, including offline CLI and distinct backend-PID proof. Earlier independent PG Operation3/3 remains historical evidence.
- [ ] Manual testing completed — user reports most localhost checks stable, tester-group sign-off pending. U06 boundary values, U11 composed invariant and U20 responsive UAT remain deferred.
- [ ] Full linked booking snapshot — U19 BLOCKED by unavailable commercial integration. Historical Newman76pass/1fail/1skip belongs to54-request revision. Current58-request collection adds4 F8 negatives; live run NOT RUN on this revision. No all-green UAT claim.

**Test results:** [F6–F8 verification](SPARK-340-F6-F8-implementation-20261117.md) and earlier [P6 handoff](SPARK-340-P6-delivery-20261117.md). An expanded generic Payment Postman probe exposed an unrelated baseline environment-example failure; the P6 report records it separately from scoped SPARK-340 regression.

## Screenshots / Evidence

- [Independent review and live verification](SPARK-370-independent-review-20261117-round2.md)
- [Sanitized Newman evidence](evidence/SPARK-370-independent-review-20261117-round2.json)
- [P5 source/security audit](evidence/SPARK-340-P5-source-audit-20261117.json)
- [P5 review and finding dispositions](SPARK-340-P5-review-20261117.md)
- [Localhost UAT checklist](SPARK-340-localhost-UAT-checklist.md)

## Breaking Changes

- [ ] No breaking changes — cannot claim this while legacy checkout callback compatibility remains unresolved.
- [x] Rollout/configuration requirements documented — preserve Payment DataProtection volume/key material and JWT/JWKS configuration. User approved one additive Schema Freeze exception: checkout_signing_bindings. Review/apply the provided upgrade script manually before the new binary; fresh init declaration is also updated. No shared-DB migration was executed by the implementation agent.
- [ ] Legacy checkout migration/drain/reconciliation plan approved and verified.

## Reviewer Notes

P4 was temporarily closed by user instruction to continue P5/P6. That instruction is a phase handoff, not a retroactive passing test or waiver. F6–F8 implementation is now verified, pending independent re-review and owner rollout/inventory validation. Low findings retain their recorded disposition. Baseline/default-auth and reservation order/confirmation findings are delegated to Payment/SPARK-273 under the scoped review.

The initial tester snapshot is9a84aeaf, containing five task-scoped commits on top of baseline055da459. The unrelated SPARK-238 collection change remains outside those commits. Current remote develop freshness has not been checked; compare/reconcile it safely before a future merge. Do not auto-close Jira from this draft. Session remains active until actual delivery/acceptance is complete.
