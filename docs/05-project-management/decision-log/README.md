# Decision Log

Record significant project decisions, their status, source, and affected documents.
Approved decisions must be integrated into canonical requirements, rather than left only in this log.

- [5 November reservation first slice](2026-11-05-reservation-first-slice.md): D1–D4c, CAR/Specific Slot, cross-lot vehicle exclusivity, earned facility-failure rights, recovery through start and strict pre-start confirmation. Supersedes the optional Account cap and October 27 T−5 milestones; implementation/acceptance pending.
- [9 November reservation confirmation and arrival amendments](2026-11-09-reservation-confirmation-and-arrival.md): approved PostgreSQL-recorded cutoff, plate identity, one-time binding, live form amount, walk-in/early-arrival rules and full booked minimum; wrong-parking policy and next-phase boundary.

- [Approved v1.1 change register, revision 0.2](2026-10-20-approved-v1.1-changes.md): project-lead decisions dated 20 October 2026 (Asia/Saigon), wording preserved from the handoff, with Markdown trailing whitespace normalized for diff checks. Its completion statements describe preparation of that register, not SPARK-296 execution.
- [21 October VNeID GUID clarification](2026-10-21-vneid-simulation-guid.md): the fake API returns a GUID substitute persisted against the user; implements the lead clarification under V11-06.
- [25 October reservation/allocation state clarification](2026-10-25-reservation-allocation-state-clarification.md): 1-hour Allocation Lead Time is the default effective policy value; `OCCUPIED` does not identify the reservation holder; same/different occupant-claimant relationships require Operations linkage; RA-06 makes the listed reservation/monthly numeric values configurable defaults under Admin default/bounds → authorized Owner scope; 2D/3D keeps mutually exclusive `RESERVED`/`PROTECTED` projection values without schema redesign; no missing numeric bounds are invented.
- [26 October SPARK-228 3D Digital Twin sign-off memo](3d-digital-twin-signoff-memo.md): PM sign-off baseline approving the read-path and Three.js state binding prototype scope.
- [v1.1 decision coverage](../../01-requirements/requirements-traceability/v1.1-decision-coverage.md): canonical clauses, API/architecture contracts, NOT RUN acceptance and remaining work for each V11/D decision.
- [Design and implementation backlog](deferred-and-implementation-register.md).
- [9 November unit-of-work repository refactoring](2026-11-09-unit-of-work-repository-refactoring.md): USER-UOW-01 records deferred media-handler dependency simplification, fixture follow-up and a bounded audit; production DI registrations remain necessary.
- [6 November Cloudinary foundation and Partner policy](2026-11-06-cloudinary-foundation-and-partner-policy.md): approved PDF formats/counts, lifecycle/retention/holds, shared development provider permissions and User-only rollout boundary; Partner runtime remains deferred.
- [6 November User media cleanup deferral](2026-11-06-user-media-cleanup-deferral.md): bounded SkiaSharp decoding, 24-hour drafts, timestamps and expired-read checks in the foundation; automatic deletion deferred under USER-MEDIA-CLEANUP-01. Physical retention is not implemented.
- [7 November vehicle claim images](2026-11-07-vehicle-claim-images.md): approved claim-photo association, authorized reviewer access with later adjudication, unchanged admission-anchored expiry, and deferred claim-specific preservation under VEH-CLAIM-HOLD-01; implementation pending and service authentication undecided.
- [7 November service-authentication deferral](2026-11-07-service-authentication-deferral.md): option 2 retained as future/refactoring direction under SERVICE-AUTH-01; the existing shared service key is subsequently approved for the temporary binding lookup, with caller isolation deferred.
- [Release review and ownership actions](pm-release-actions.md).

Earlier dated decision/review records remain historical sources. v1.0 was an unsigned review candidate. The current package is a **v1.1 documentation review candidate based on approved decisions**; requirements approval does not prove API approval, implementation or product acceptance.

- [10 November confirmation evaluation checkpoint](2026-11-10-reservation-evaluation-checkpoint.md): replaces commit-time cutoff and candidate proof with one guarded database evaluation timestamp; durable commit still required for exposure.

- [**11 November reservation event-driven confirmation architecture**](2026-11-11-reservation-event-driven-confirmation.md): creation ends at committed `PENDING_PAYMENT` hold; separate Payment event consumer; immutable versioned reviewed pricing and combined `EnsureOrder`; live-hold aggregate-only confirmation after proved Facility/writer invariants, otherwise existing confirmation guard; expired-hold reacquisition before T or reconciliation. This does not supersede the approved 10 November timestamp cutoff.
