# PAY-01 — payOS priority and MVP payment acceptance

**Approved by project manager:** 13 October 2026, explicit “Yes i approve” to the corrected channel wording and replacement acceptance proposal. **Document package:** v1.0 RC3 review candidate. **Implementation/acceptance:** NOT RUN / not assessed.

## Approved scope

payOS QR receives implementation priority for MVP online payment. VNPay, MoMo and ZaloPay remain separate planned options, with integration or fixture status explicitly identified. If resources are insufficient, payOS QR provides the working online-payment path; unfinished native provider integrations remain explicitly simulated or deferred. payOS transactions must not be presented or recorded as native VNPay, MoMo or ZaloPay transactions. Payer-application compatibility is claimed only after verification. Assigned-Operator CASH_COLLECT remains unchanged.

Controlled, low-value payOS production testing plus deterministic payment failure/race fixtures replaces the one-real-sandbox-adapter requirement. VNPay native integration may remain simulated/deferred when resources are insufficient; MoMo/ZaloPay fixture arrangements remain. No separate working sandbox adapter is required. Real-money checkout exposes functioning routes only; demo fixtures are explicitly labelled and isolated from real-money data and effects. Fixture-only results cannot satisfy the required payOS real integration evidence.

This is an explicit amendment to the previous VNPay-first/one-real-sandbox acceptance profile (§16.6), not a claim that payOS fulfills three native integrations. Preserve separately functioning native adapters if implemented; the fallback permits unfinished native integrations to remain simulated/deferred without preventing the approved payOS path.

## Preserved decisions

Pricing/rounding, deposits (including fixed deposits above booked charges), minimum booking obligation, zero-price confirmation, cancellation/no-show rules, monthly coverage, surplus refunds, financial retention, role authority and cash collection are unchanged. Payment & Pricing owns money/adapters; Operations owns capacity/rights. TOTP/account recovery from SEC-01/02 remains current MVP.

Approval of this amendment is not final v1.0 release sign-off or proof of a working provider. It does not approve a new manual bank-refund route, silent cash fallback, relaxed refund eligibility or fabricated settlement success.

## Implementation implications

- Actual provider/route and production-versus-fixture mode are explicit in checkout, ledger and evidence; payer app is separate and only recorded if reliably known.
- Client redirects, screenshots, QR generation and payment-link cancellation are not financial proof.
- Verify callback/query authenticity and order, amount, currency, payee and transaction identity; deduplicate business effects.
- UNKNOWN outcomes use same-operation lookup/reconciliation. No blind extra charge/provider/cash switch.
- Preserve ordinary late-payment capacity reacquisition and monthly strict A<H activation; provider status never grants capacity directly.
- Surplus refund obligation is created once; covered checkout may finish while refund is pending. Validate execution capability separately. Unsupported online-route refunds remain pending integration/reconciliation issues.

## Source evidence checked 13 October 2026

- [payOS checkout](https://payos.vn/docs/checkout/how-checkout-works/): documented bank-app VietQR flow, payment links and merchant webhook.
- [payOS test environment](https://payos.vn/docs/moi-truong-test/): no separate sandbox/staging; tests use production and actual low-value transactions.
- [payOS API](https://payos.vn/docs/api/): payment-link creation/query/cancellation and signed data. This review did not establish an original-transaction refund endpoint.
- [payOS payout announcement](https://payos.vn/ra-mat-kenh-chi-tren-payos-giai-phap-chi-tu-dong-qua-api/): separate payout offering; suitability for approved refund semantics is unverified.

[Adapter design](../../02-architecture/payment-integration/payos.md) · [Integration test plan](../../04-testing/payment-integration/payos-test-plan.md) · [Change summary](rc3-change-summary.md)
