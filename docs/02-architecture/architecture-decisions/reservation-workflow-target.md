# Reservation architecture — review, admission and confirmation

**Status:** Approved SPARK-273 workflow, corrected to the implemented current contract. Historical decisions remain authoritative only where not superseded below. See the [18 November decision amendment](../../05-project-management/decision-log/2026-11-18-SPARK-273-reservation-contract-amendment.md) for precedence and the [owner handoffs](<../../05-project-management/handoffs/SPARK-273 — Reservation/2026-11-18-SPARK-273-reservation-owner-handoffs.md>) for source evidence and rollout gates. The [historical proposal](reservation-first-slice-implementation-proposal.md) retains the Facility protocol, Account-current replay, trust boundary and checkpoint decisions.

## Current SPARK-273 status (2026-11-26)

- Operations `OrderId` is nullable by design. Positive admission atomically commits `PENDING_PAYMENT`, concrete hold, exact H and durable order-creation intent; Payment creates the payable order independently. Operations may still report null `OrderId` after Payment creates the order and binds it only on authoritative successful settlement. Payment is the source of checkout readiness; `OrderId`/`HoldExpiresAt` alone are not readiness evidence.
- Coverage binding and authoritative accepted commercial evidence are evaluated in the committed admission handler. Zero-deposit accepted evidence commits `CONFIRMED` immediately with no unpaid H; only specifically deferred subscription functionality remains pending. Coverage, tariff and policy revisions remain meaningful.
- Checkpoint chronology: B4 retired the `ADMISSION` token path, C retired the remaining reservation-specific `CONFIRMATION` acquire/complete/abort token workflow, and D retired the remaining authorization and unpaid hold-expiry polling workers. HTTP slot metadata lookup and messaging-based interval-aware Facility eligibility remain.
- Driver updates use SignalR with one authoritative read on initial join, reconnect, or explicit page restoration. There is no recurring reservation-progression or reservation-checkout-readiness polling. Independent Payment QR/transaction status checks are Payment-scoped.
- Current verification entry points: [manual test guide](../../04-testing/postman/SPARK-273-manual-test-guide.md), [deployment checklist](../../04-testing/reports/SPARK-273-deployment-checklist.md#checkpoint-d-coordinated-cutover-and-recovery), and [Checkpoint E tester handoff](../../04-testing/reports/SPARK-273-checkpoint-e-tester-handoff.md).

## Ownership

| Owner | Authority and boundary |
| --- | --- |
| Operations | Authenticated request receipt, EF-backed MassTransit Saga StateMachine, plate/site/interval booking constraints, prospective and final entitlement checks, reservation and capacity claim, durable status projection, SignalR and guarded confirmation. |
| UserService | Account-current and role decision; active vehicle registration and unambiguous plate ownership by initiating Account. Reuse existing registration flow. |
| FacilityService | Slot GUID resolution to authoritative site/tenant and eligibility, configuration/version facts, and structure/physical writer coordination with Operations. |
| PaymentService | `quoteId`-identified commercial quote, tariff/coverage amounts, currency and acceptance; real order, payOS and verified collection through the agreed reservation event; zero-deposit acceptance/receipt evidence where authoritative, with only specifically identified subscription functionality deferred. |
| Frontend | Vehicle selection/registration, review and consent, authenticated progress and recovery, checkout for the real order. Operations commits entitlement. |

## Request to confirmation

```mermaid
sequenceDiagram
    participant D as Driver web
    participant O as Operations API and application
    participant S as Operations admission saga
    participant U as UserService
    participant F as FacilityService
    participant P as PaymentService
    participant C as Operations post-commit consumers
    D->>U: HTTP list or register vehicle through existing flow
    D->>O: HTTP review normalized plate, slot, interval, method
    O->>F: Internal HTTP read by slot GUID
    O->>O: Prospective coverage from authoritative quote evidence
    O->>P: Internal HTTP immutable quote
    O-->>D: QuoteId quote and disclosure
    Note over D,O: Driver accepts quote with no capacity held
    D->>O: HTTP create with accepted quoteId and idempotency key
    O->>O: Commit intent and transactional outbox
    O-->>D: HTTP 202 with request ID
    O-->>S: MassTransit durable start
    D->>O: SignalR subscribe + one initial status read
    par Account-current
        S-->>U: MassTransit Account-current request
        U-->>S: Correlated decision
    and Plate ownership
        S-->>U: MassTransit plate validation
        U-->>S: Correlated authoritative plate outcome
    and Slot eligibility
        S-->>F: MassTransit slot validation
        F-->>S: Correlated authoritative site and eligibility
    end
    S->>S: Persist accepted outcomes and fence terminal attempts
    S-->>D: SignalR via Operations after committed progress
    alt Rejection or exhausted dependency check
        S->>S: Commit terminal receipt without claim
        S-->>D: SignalR terminal progress
    else All required checks succeed
        S->>F: Interval-aware eligibility decision outside guarded transaction
        S->>S: Guarded final booking evaluation using committed claim evidence
        alt Guard rejects or cannot complete
            S->>S: Commit terminal outcome and resolve Facility dependency
            S-->>D: SignalR terminal progress
        else Guard passes
        S->>S: Atomic hold, terminal receipt and outbox commit
        S-->>C: MassTransit post-commit hold trigger
        alt Positive deposit
            C->>P: Internal HTTP accept quote and ensure real order intent
            C-->>D: SignalR checkout hint; Payment is readiness authority
            D->>P: HTTP checkout with Payment-authoritative order ID
            P-->>C: MassTransit agreed verified reservation collection event
            C->>C: Guarded confirmation from committed claim and eligibility
            C-->>D: SignalR committed confirmation via Operations
        else Zero-deposit accepted evidence
            S->>S: Commit CONFIRMED immediately without unpaid H
            S-->>D: SignalR committed confirmation via Operations
        end
        end
    end
```

If final evaluation rejects, Operations commits the terminal rejection without creating a hold and fences any uncommitted effects. The retired reservation-specific Facility token workflow is no longer completed or aborted; the retained interval-aware eligibility decision and structure-edit coordination remain independent. The diagram omits reconciliation and connection recovery, which are specified below and in the [API](../../03-design/api/reservation-first-slice-api-design.md).

The initial receipt and start intent are durable before HTTP 202. The saga accepts only the current request/branch/attempt and matching immutable input. It persists one outcome per required branch, fails logically on the first definitive rejection and fences terminal attempts against late, duplicate, contradictory or out-of-order messages. Transport faults and timeout exhaustion are dependency outcomes. The reservation exception in the [authorization ADR](authorization-freshness-and-current-access.md) sends one logical request per branch. D defaults to 15 seconds from admission and covers the guarded booking decision. Persistent Operations Quartz jobs enforce D and positive H. A timely structure hold rejects with STRUCTURE_BUSY. The owner-issued plate result establishes the admission-time observation. A transfer recorded before the check must reject, and transfer/removal after booking alone leaves the booking valid. Preserve the [accepted 9 November read-to-commit race](../../05-project-management/decision-log/2026-11-09-reservation-confirmation-and-arrival.md#approved-identity-and-workflow-checks) for changes after the durable plate decision. Verify this boundary with UserService; the read establishes no lease or atomicity across service stores.

Before the final short Operations transaction, use the committed interval-aware Facility eligibility decision and applicable claim evidence with existing coordination. Under Operations guards, resolve current local policy, restrictions, same-plate/site overlap, physical-session compatibility, applicable capacity and quote binding/validity. Use the canonical plate and slot/site/tenant from correlated authoritative answers. Recheck current local plate entitlement with the same site and interval. Changed coverage/version, changed binding or expired quote requires renewed review. Store the authoritative temporary claim, accepted policy/deadline context, Account, canonical plate, site/tenant, slot, interval and required commercial references. External HTTP stays outside this transaction. The former Facility token outcome/complete/abort recovery was retired by C; eligibility and structure-edit coordination remain.

The owning transaction must atomically capture the terminal receipt, reservation, claim and hold trigger using the MassTransit transactional outbox. EF saga finalization and terminal fencing must survive replay without recreating the hold, even if finalized saga storage is cleaned up. The saga ends at the committed hold. Durable post-commit delivery is needed because the process can die after local commit and before dispatch or the Payment HTTP call. HTTP provides the authoritative acceptance response, while the outbox provides durable scheduling independently of the browser and any in-memory task.

Positive-deposit confirmation is a separate guarded Operations transition. Bind verified Payment collection to the exact booking, commercial terms and committed claim/eligibility evidence; Checkpoint C retired the separate Facility confirmation token gate, while capacity and physical constraints remain under Operations guards. For verified paid collections, preserve the existing compatible reacquisition exception before T where permitted, or reconcile the money. An unpaid positive-deposit hold uses H = min(hold creation + effective reservation hold duration, T) and confirms only with verified collection and a valid protected claim at the guarded checkpoint strictly before H. If expiry wins or protection cannot be established, close/release the hold under existing rules and require a fresh submission and review. Zero-deposit accepted evidence confirms immediately during admission with no unpaid H; only specifically deferred subscription functionality remains pending. Same-operation replay may recover commercial evidence for reconciliation; it cannot reopen the booking. Evaluate once using Operations database time under the guards. Paid eligibility remains strictly before T. Persist confirmation, evaluation and effect identity and the outbox fact atomically. The result becomes visible after durable commit. Preserve the [10 November checkpoint](../../05-project-management/decision-log/2026-11-10-reservation-evaluation-checkpoint.md) and [11 November competing-writer proof gate](../../05-project-management/decision-log/2026-11-11-reservation-event-driven-confirmation.md), with the token-specific portions superseded by C.

See the [Payment confirmation contract](<../../05-project-management/handoffs/SPARK-273 — Reservation/2026-11-19-SPARK-273-payment-authority.md#owner-agreement-and-wire-contracts>) for the separate paid/covered evidence requirements and the open generic event exception.

## State transitions

States below describe the implemented contract. Zero-deposit accepted evidence confirms immediately; only specifically identified subscription functionality remains deferred. Keep paid-hold expiry and zero-deposit paths distinct.

| Layer | Trigger | Durable outcome / observable meaning |
| --- | --- | --- |
| Review | Valid selection, resolved slot and Payment quote | Preview/review available with `quoteId`, validity, amounts and meaningful policy/tariff/coverage revisions. No reservation or hold. |
| Review | Quote expiry, input/coverage change, unavailable owner | Renew review, or show dependency unavailable. No claim. |
| Admission | Durable create | Pending with three branch states. Only committed owner checks can be shown successful. |
| Admission | Definitive failed check | Terminal rejected. Remaining in-flight reads cannot reopen request. |
| Admission | Timeout or unavailable dependency after policy budget | Terminal dependency outcome. No business rejection inferred. |
| Admission | All checks passed | Final booking evaluation pending. Guarded decision records rejection, dependency outcome or committed hold. |
| Reservation | Ordinary hold committed | `PENDING_PAYMENT`. Concrete hold, exact H and durable order-creation intent committed; Operations `OrderId` may be null. |
| Payment readiness | Payment creates/replays the payable order | Payment is authoritative for checkout readiness. Reservation remains `PENDING_PAYMENT`, and Operations may still report null `OrderId` until authoritative settlement binding. |
| Payment readiness | Definitive unpayable order or expired checkout | Closed/unavailable with a reason. Release or reconcile under the applicable hold and financial rules. |
| Payment readiness | Ambiguous order or payment effect | Unknown/pending reconciliation. Reuse stable effect identity. Never start a new effect on an unknown result. |
| Reservation | Zero-deposit accepted evidence evaluated in admission | `CONFIRMED` immediately with no unpaid H; `orderId`, `holdExpiresAt` and other financial identities may remain null. |
| Commercial readiness | Authoritative acceptance evidence evaluated in admission | Valid accepted evidence confirms immediately. An explicit invalid quote/entitlement is a known rejection; only specifically identified subscription functionality remains deferred. |
| Reservation | Eligible guarded paid or covered confirmation succeeds | `CONFIRMED` after commit. |
| Reservation | Paid hold expires, invalidation or cancellation prevents confirmation | Release/expire as applicable. The pre-T compatible reacquisition exception is for verified paid collections. Expired paid review/hold requires fresh review and a new request; zero-deposit confirmation creates no unpaid hold to expire. Late evidence reconciles without reviving a terminal claim. |

Allocation, expiry, cancellation, arrival, actual parking, departure, final settlement, refunds and disputes use their existing owners and policies. Customer access stays Account-scoped. Gate association uses canonical plate and trusted evidence. Plate knowledge alone confers no reservation access.
