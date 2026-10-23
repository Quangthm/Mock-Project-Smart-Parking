# Overview — SRS v1.1

[Requirements index](../README.md) · [Traceability](../../requirements-traceability/README.md)

**Status:** v1.1 documentation review candidate based on approved decisions, 20 October 2026; implementation/evidence not assessed. Existing requirement IDs and unrelated approved amendments remain in force.

SmartPark is a Vietnam-focused web parking management mock project with Driver, assigned Operator, Owner and Admin. Preserve motorcycles and supported non-plated visits; EV charging is excluded. A reservation is an entitlement/compatible temporal claim; allocation identifies its current fulfillment resource. Arrival at gate, actual entry, occupied resource, accepted checkout, money settlement and actual departure are distinct. An available-only monthly pass covers fees, not admission capacity. No fifth Developer actor. Backend domain facts control maps and AI output.

Partner Registration is the combined first-lot onboarding process, using the existing Owner role. Monthly/yearly products are Driver parking products; MapTiler geographic discovery hands off to a separate internal 2D/3D page. MVP has one protected seeded Admin, no Admin registration or role management.

Authority: [approved v1.1 register](../../../05-project-management/decision-log/2026-10-20-approved-v1.1-changes.md), latest explicit user decisions, then confirmed baseline review clauses, then non-conflicting source requirements. Historical alternatives and fixture examples are not active policy. Technical contract choices in architecture/state files implement these outcomes; they do not certify deployed code. All current requirements are implementation-pending/not assessed. Detailed LPR/Assistant integration is POST-v1.0; broader exclusions are FUTURE. No unsatisfied implementation task is relabelled as an undecided business rule.

## Functional requirements


## Explicit source exclusions

Native mobile app, autonomous-vehicle integration, loyalty points, national digital-map integration and a full complaint/helpdesk product remain outside this release. The approved minimal asynchronous support case does not turn that exclusion into a full ticketing suite. Real IoT and full offline operation remain future. The web application retains responsive role-based interfaces.
