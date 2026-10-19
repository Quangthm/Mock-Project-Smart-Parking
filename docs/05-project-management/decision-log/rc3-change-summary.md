# SmartPark v1.0 RC3 — changes from RC2

Approved basis: PAY-01, 13 October 2026. This package contains only replacements/new files relative to RC2. Apply RC2 first if starting from RC1. No files are deleted.

## Explicit changes

1. Prioritize payOS QR as the working MVP online-payment implementation.
2. Replace the one-real-sandbox/VNPay-first gate with controlled low-value payOS production tests plus deterministic failure/race fixtures. A separate native sandbox adapter is no longer required.
3. Retain native VNPay/MoMo/ZaloPay as separate planned options; unfinished implementations may be explicitly simulated/deferred. Real-money checkout only exposes functioning routes. Do not label payOS payments as native provider transactions; app compatibility requires evidence.
4. Synchronize the repeated profile in the payment SRS, quality module, baseline review, acceptance plan, BR-PAY-03 and PM exception guidance. Update UC-PAY-01 and US-D02/US-D06 clarification and payment trace notes.
5. Add focused architecture guidance and a test plan. Add AC-066–072; all 72 scenarios remain NOT RUN.
6. Refresh root navigation, current counts, manifest, validation and checksums. Correct stale RC1 counts in the root README to the already-established RC2 counts; no new FR/BR/UC/US IDs.

## Preserved behavior

No changes to tariff, rounding, reservation minimum, free-time threshold, deposits, cancellation/no-show money, monthly coverage/cancellation dependency rule, refund percentages/eligibility, role permissions, occupancy/capacity races, retention, TOTP or account recovery. Assigned-Operator cash collection remains. Original source files and historical comparisons remain byte-identical.

Refund obligations remain mandatory. Provider refund capability is unverified; a separate payout product is not silently accepted as an original-route refund. Unsupported online refunds stay visibly pending/reconciled. No new manual online-refund method or cash fallback is approved. Pricing and authority cannot be changed by implementation guidance.

## Remaining implementation evidence

Team records account/payee mapping, secrets/SDK configuration, callback hosting, numeric low-value test budget, real collection/query evidence, native provider modes, payer-app compatibility and actual refund capability/execution. Fixtures cannot establish real money delivery. A concrete scope/financial-workflow blocker returns to PM-04. Final release sign-off, delivery assignments and actual support availability remain pending; no new PM business decision is needed to apply this approved patch.

## Comparison and installation

Merge replacements/ into the existing RC2 root, preserving relative paths. Do not delete files absent from the patch. PATCH-INVENTORY.csv lists every new/replaced path with before/after SHA-256; RC2-to-RC3.diff contains all text changes except the mechanically regenerated checksum list. APPLY-PATCH.md explains baseline checks. No code, repository, database or provider deployment was changed.
