# UC-PAY-01 — Pay, verify results and reconcile late/unknown money

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, assigned Operator, Payment provider

[Use-case index](README.md) · [SRS module](../srs/modules/06-pricing-payments.md)

## Goal

Pay, verify results and reconcile late/unknown money.

## Preconditions

Accepted order/charge and current authorized operation.

## Main flow

1. Persist logical payment and attempt before external effect; use online channel adapter or audited original cash collection.
2. Validate amount/currency/order/payee and deduplicate provider transaction and callback.
3. Apply success once and fulfill rights through owning domain workflow.
4. After definitive retryable failure allow up to three additional attempts; UNKNOWN instead queries/reconciles same operation.
5. Record surplus/refund obligations independently; payout completes only with proof.

## Alternatives and failures

Late ordinary money reacquires capacity or refunds; monthly money commits rights only A<H. No blind provider/cash switch on UNKNOWN. Genuine duplicate collection reconciles separately, never grants a second entitlement.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-BAS-16, FR-PAY-03, FR-PAY-04, FR-PAY-05  
**US:** US-D02, US-O02  
**Rules:** BR-PAY-03, BR-PAY-04

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.

## PAY-01 channel selection and evidence

Offer payOS QR as the implementation-priority online route. Real-money checkout lists only functioning routes. In demonstrations, native VNPay/MoMo/ZaloPay fixtures are clearly labelled simulations and cannot create real-money effects. Persist actual provider, route and environment/mode with the logical payment. Do not silently translate a selected native-provider option into payOS. If a payer application can scan payOS QR, record the transaction as payOS; do not infer a native provider integration from that scan.

Use controlled low-value payOS production evidence plus deterministic failure/race fixtures for acceptance. Refund capability remains a separate implementation verification: unsupported online refunds stay visibly pending/reconciled; link cancellation is not payout proof. Existing success, retry, UNKNOWN, late-payment and refund rules above remain binding.
