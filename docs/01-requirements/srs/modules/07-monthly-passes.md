# Monthly and Yearly Driver Parking Products — SRS v1.1

[Requirements index](../README.md) · [Traceability](../../requirements-traceability/README.md)

**Status:** v1.1 documentation review candidate based on approved decisions, 20 October 2026; implementation/evidence not assessed. Existing requirement IDs and unrelated approved amendments remain in force.

Canonical parking-product business rules are M-01–M-09 in the linked rule file; stable monthly filenames/IDs now also carry the approved yearly extension. These are Driver parking products, not an Owner platform subscription. Owners explicitly enable monthly and/or yearly products per lot, separately from their prices. A yearly pass covers 12 consecutive calendar months, with at most one yearly period purchased in advance; the unchanged monthly maximum is three prepaid monthly periods. Separate configurable yearly policies, including potentially different refund rates, require specified values before sale; no monthly rate or renewal default is silently copied to yearly. Operations owns rights/plan definition; Payment owns prices/orders/paid financial periods. Available-only; no capacity guarantee, sales cap or automatic renewal.

Before voluntary cancellation, show affected future reservations whose accepted price depends on coverage being cancelled and require Driver explicitly cancel them first (A-01). Do not auto-cancel, reprice or charge them. Recheck dependencies atomically with accepted cancellation; if a new dependency wins the race, return blockers for Driver resolution. Driver may rebook at ordinary prices subject to capacity. Owner handles cancellation; Payment refunds monthly products per M-05 and yearly products using their separately accepted policy, without inventing a yearly percentage. Failure/closure follows its separate approved rules; no release of occupied capacity.

A-01 example: a future reservation was confirmed at zero because a paid pass covers it. Driver requests pass cancellation. Display the reservation and require its explicit cancellation first; only then accept pass cancellation and determine C/refund. Ordinary booking cancellation rules apply to that separate action. If Driver rebooks after cancellation, obtain new ordinary terms and capacity. No automatic debt or preservation of released reservation capacity.

## Functional requirements

### FR-MON-01

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Owner  
**Stories:** US-OW06

Owner explicitly enables monthly and/or yearly versioned available-only plans with separate Payment financial terms; Driver reviews full-period price, bound account/vehicle/lot or zone, anchor, [S,E), ordinary day/night coverage and refund terms. No guaranteed capacity/slot, sales cap, monthly queue or automatic payment. Price is required Owner configuration; 1,200,000 VND is only a fixture.

### FR-MON-02

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver  
**Stories:** US-D13

Owner explicitly enables monthly and/or yearly versioned available-only plans with separate Payment financial terms; Driver reviews full-period price, bound account/vehicle/lot or zone, anchor, [S,E), ordinary day/night coverage and refund terms. No guaranteed capacity/slot, sales cap, monthly queue or automatic payment. Price is required Owner configuration; 1,200,000 VND is only a fixture.

### FR-MON-03

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver  
**Stories:** US-D13

Create immediate or future-start vehicle/location-bound order after validating duplicate rights and max three consecutive prepaid monthly periods or at most one yearly period in advance. A yearly period is 12 calendar months; do not reject it by counting its months against the monthly limit. Yearly order deadlines and renewal settings require their own reviewed configuration rather than an invented default. The unchanged monthly order hold is 15 minutes and does not hold parking capacity. For monthly, full verified payment plus Operations entitlement commit must satisfy strict A<H; yearly uses its separately accepted deadline contract.

### FR-MON-04

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Owner  
**Stories:** US-D13, US-OW06

Backend calculates monthly anchored periods under M-01 and yearly 12-calendar-month periods with their separately specified calendar-boundary rules and stores [S,E), original anchor and accepted price for each period.

### FR-MON-05

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator, Payment Provider  
**Stories:** US-D13

Backend grants monthly rights only after full verified payment and A<H; yearly also requires verified payment and timely entitlement commitment under its separately agreed deadline; handle late, mismatched, duplicate money and failed fulfillment under M-03.

### FR-MON-06

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Owner, Operator  
**Stories:** US-O07, US-OW06

An available-only pass reserves no capacity while outside. Each visit requires actual compatible availability or an independently confirmed ordinary reservation. Count occupation once; no guaranteed-capacity/slot product is active. Operations owns coverage rights; Payment owns financial paid periods.

### FR-MON-07

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Operator, Device Gateway  
**Stories:** US-O07

Operator validates account, vehicle, state, timestamps and resources per visit; a monthly or yearly right does not replace the single-use session ticket or permit duplicate open sessions.

### FR-MON-08

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator  
**Stories:** US-D14, US-O07

Exclude valid ordinary day/night covered intervals from money, but evaluate the 15-minute exemption against whole session to accepted checkout. Never restart grace at monthly or yearly coverage boundaries. Separate service fees remain separate; no EV charging feature is implied. Covered 60 + uncovered 10 minutes charges only the uncovered 10.

### FR-MON-09

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Payment Provider  
**Stories:** US-D14

Monthly timely renewal extends from the last committed E; late repurchase creates a new non-retroactive period after price/resource validation (M-02). Yearly renewal timing/financial terms must be configured separately before use; no new automatic renewal or default is implied.

### FR-MON-10

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Notification Service  
**Stories:** US-D14

Driver views monthly/yearly period/order/payment/refund independently and receives product-specific configured reminders; notification failure never creates renewal.

### FR-MON-11

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Owner, Payment Provider  
**Stories:** US-D15

Before voluntary cancellation, show affected future reservations whose accepted price depends on coverage being cancelled and require Driver explicitly cancel them first (A-01). Do not auto-cancel, reprice or charge them. Recheck dependencies atomically with accepted cancellation; if a new dependency wins the race, return blockers for Driver resolution. Driver may rebook at ordinary prices subject to capacity. Owner handles cancellation; Payment refunds monthly products per M-05 and yearly products using their separately accepted policy, without inventing a yearly percentage. Failure/closure follows its separate approved rules; no release of occupied capacity.

### FR-MON-12

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Owner, Notification Service  
**Stories:** US-OW06

Preserve accepted paid periods against later price or sales changes. Temporary closure keeps pass active and end date unchanged, refunding verified unavailable interval value once. Permanent closure cancels remaining coverage and refunds eligible unused value at 100%, excluding previously compensated overlap. No automatic extension; ordinary fullness is not facility failure.

### FR-MON-13

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Owner, Operator  
**Stories:** US-OW06

Owner views scoped collected/refunded money; Operator sees only necessary operational data. Collected money awaiting refund is not displayed as a successfully granted plan.

### FR-MON-14

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator  
**Stories:** US-D14, US-O07

For monthly and yearly products, at E stop coverage/new entry entitlement, not physical occupancy. Apply eligible monthly/yearly coverage before calculating ordinary reservation minimum: zero-price reservation confirms without payment with normal claims/cap checks; positive price uses approved prepayment. Prevent incompatible overlapping reservations and double charges; enforce A-01 dependency guard for voluntary cancellation.

[Canonical M-01–M-09 rules](../../business-rules/07-monthly-passes.md).


Design/implementation owners and exit criteria: [V11-DES-05 (Q05)](../../../05-project-management/decision-log/deferred-and-implementation-register.md#v11-design-and-implementation-actions). Requirement approval does not imply an approved wire contract or completed implementation.
