# SmartPark 3D Integration Review Handoff

**Date:** 12 October 2026  
**Scope:** `3D_MODEL_IMPLEMENTATION_GUIDE.md`, backend slot-state binding implementation/documentation, and `3D_ENTERPRISE_MODEL_INGESTION_RULES.md`  
**Primary Jira:** SPARK-186 and SPARK-228  
**Purpose:** Bring the current 3D implementation and documentation into alignment with the approved SmartPark v1.0 architecture, data ownership, state model, and Jira acceptance criteria.

---

# 1. Executive summary

The current 3D work contains a useful foundation, but **SPARK-186 and especially SPARK-228 should not yet be considered completed**.

The biggest issue is not the Three.js rendering itself. It is the contract between:

**backend operational truth → visualization projection → 3D model objects.**

The current implementation already has a working viewer, model loading, slot detection, material changes, raycasting, camera handling and backend API infrastructure. However, the slot-binding implementation currently:

- associates model meshes with backend slots mainly by array/spatial order;
- fabricates missing slots as `slot-auto-*`;
- defaults missing state to `available`;
- combines physical and reservation state into one status;
- supports only `available`, `occupied`, and `reserved`;
- does not implement the complete v1.0 state model;
- does not yet demonstrate the documented stale-state/test/performance behavior.

These points conflict with SPARK-228 and with the latest architecture.

The corrected direction is:

```text
Parking Operations
AUTHORITATIVE operational truth
        │
        │ accepted occupancy / uncertainty
        │ claims / reservation protection / backup
        │ versioned operational projection
        ▼
Facility & Visualization
layout + resource identities + fixed 3D asset mapping
+ rebuildable display projection of O-owned operational state
        │
        │ one FE-facing visualization/read projection
        ▼
React / Three.js
DISPLAY ONLY
        │
        ├── Physical-state visualization
        └── Reservation/protection overlay

3D interactions MUST NOT become operational authority.
```

This follows the approved ownership model: Facility owns sites, units, slots, layout, coordinates and fixed 3D asset references, while Parking Operations owns accepted occupancy, uncertainty, claims, protection, allocation and authoritative availability. Facility/3D can consume and display O-owned projections but must never become a second occupancy authority.

UC-MAP-01 explicitly follows the same division: Facility owns fixed model/layout/resource association; Operations owns accepted occupancy/uncertainty and parked-session state; IoT observations do not directly become authoritative display state.

---

# 2. Decisions confirmed by the project lead

These decisions are now the baseline for this work and should **not be reopened during implementation unless a newly discovered contradiction requires escalation**.

| Decision | Confirmed direction |
|---|---|
| Canonical 3D slot binding | Use the stable **facility slot code** as the canonical model-binding identifier. Backend UUID remains the database/entity identity. |
| Operational-state delivery | FE should consume **one visualization-oriented projection**, not independently combine authoritative state from several services in the browser. |
| Operational authority | Parking Operations remains authoritative for accepted occupancy, uncertainty, reservation/protection/backup and availability. |
| Visualization ownership | Facility & Visualization owns the structural/resource/model mapping and may maintain a rebuildable display projection sourced from Operations. |
| Visual state model | Physical state and reservation/protection state remain **two independent dimensions**. |
| Enterprise ingestion | Enterprise/self-service arbitrary model ingestion is **Phase 2 / Proposed Specification**, not a v1.0 MVP requirement. |
| v1.0 3D assets | Fixed 3D models remain versioned **deployment assets**; no owner cloud-upload pipeline is required for these models. |
| Realtime | SignalR is approved for 3D/live operational views, but it is a transport/update mechanism, not authoritative state. |

The architecture explicitly states that an operational copy stored by Facility for display must be a **projection with source/version/observed time and must be rebuildable from Operations**.

It also explicitly keeps the fixed 3D models as deployment assets rather than introducing a cloud-upload pipeline for v1.0.

---

# 3. Canonical model-to-slot identity

## Required direction

The canonical mapping should be based on:

```text
Facility Slot UUID
    = backend/database identity

Facility Slot Code
    = stable human/model integration identity

3D node
    = carries or deterministically represents Slot Code
```

Example:

```text
Backend:

id   = 82b2b603-...-0f8c
code = B1-A-023
```

Corresponding GLB node:

```text
Slot_B1-A-023
```

or preferably explicit GLTF metadata equivalent to:

```text
smartparkSlotCode = B1-A-023
```

The actual exact naming syntax can remain an implementation convention, but **the mapping must resolve by slot code and not by list position**.

UUIDs should continue to be used by backend API/domain operations, but they should not be required inside static GLB model names.

This keeps the 3D asset portable between environments while still allowing:

```text
slot code → Facility Slot → UUID → Operations state
```

---

# 4. Critical problem in the current `slotMatcher.ts`

The current mapper discovers candidate meshes, sorts them spatially and then effectively performs:

```text
candidateMesh[0] → slots[0]
candidateMesh[1] → slots[1]
candidateMesh[2] → slots[2]
...
```

The current code specifically falls back to `slots[index]`. 

This is unsafe because model ordering and backend query ordering are not identity.

A new model export, moved mesh, inserted slot, changed sort order, database query-order change or changed model topology can silently produce:

```text
Backend A-001 → 3D A-002
Backend A-002 → 3D A-003
...
```

without throwing an error.

### Required change

Use the explicit canonical key:

```text
meshSlotCode → normalize → Facility slot code lookup
```

Conceptually:

```text
Model slot B1-A-023
        │
        ▼
normalizeSlotCode()
        │
        ▼
"B1-A-023"
        │
        ▼
Map<string, FacilitySlot>
        │
        ▼
backend slot id + operational projection
```

Spatial matching may be retained only as a **development diagnostic/migration helper**, never as the authoritative production binding rule.

---

# 5. Remove fabricated authoritative slots

The current mapper creates a fallback slot similar to:

```text
id = slot-auto-N
status = available
```

when a model candidate cannot be matched to supplied backend data. 

This must not be used for operational rendering.

A mesh without an authoritative binding is **UNMAPPED**, not AVAILABLE.

Correct behavior:

```text
model slot exists + backend identity found
→ render authoritative state

model slot exists + no backend binding
→ render visibly UNMAPPED / UNKNOWN
→ log/report mapping problem

backend slot exists + no 3D node
→ report missing model binding
→ do not invent a mesh

backend operational state missing/stale
→ UNKNOWN / STALE display
→ never AVAILABLE by default
```

Procedural/demo fixtures can generate synthetic slots, but they must be explicitly identified as **fixture/demo data**, not silently substituted into an authoritative operational view.

---

# 6. Never use `available` as the missing-state fallback

`SlotMaterialManager` currently calculates status approximately as:

```text
slot?.status || 'available'
```

which causes missing state to render as free capacity. 

That violates SPARK-228.

Missing, invalid, unavailable or stale operational data must never be interpreted as AVAILABLE.

This is important because:

```text
UNKNOWN ≠ AVAILABLE
```

and:

```text
backend unreachable ≠ parking slot free
```

The baseline explicitly says accepted uncertainty/UNKNOWN belongs to Parking Operations and conflicting/raw observations must not automatically turn an occupied resource into free capacity.

---

# 7. Replace the current single-axis slot state

Current FE:

```ts
type SlotStatus =
  | 'available'
  | 'occupied'
  | 'reserved';
```



This model is no longer sufficient.

## Required conceptual model

### Physical / operational condition

```text
AVAILABLE
OCCUPIED
UNKNOWN
MAINTENANCE
UNAVAILABLE
```

### Reservation/protection dimension

```text
NOT_RESERVED
RESERVED
PROTECTED
BACKUP
```

The two dimensions must be independent.

Therefore all of these must be valid:

```text
AVAILABLE + NOT_RESERVED
AVAILABLE + RESERVED
AVAILABLE + PROTECTED

OCCUPIED + RESERVED
OCCUPIED + BACKUP

UNKNOWN + RESERVED

MAINTENANCE + RESERVED

UNAVAILABLE + RESERVED
```

A physical state transition must not silently erase an existing reservation/protection relationship.

For example:

```text
AVAILABLE + RESERVED
        ↓
OCCUPIED + RESERVED
```

is possible.

Likewise:

```text
AVAILABLE + RESERVED
        ↓
MAINTENANCE + RESERVED
```

may represent lost capacity requiring operational recovery.

The UI should not solve the recovery itself; it only displays the state supplied by the authoritative operational projection.

---

# 8. Recommended FE DTO separation

Do not continue overloading `ParkingSlot.status`.

Use a visualization/query DTO that makes the dimensions explicit.

Conceptually:

```ts
interface SlotVisualizationState {
    slotId: string;
    slotCode: string;

    physicalState:
        | 'AVAILABLE'
        | 'OCCUPIED'
        | 'UNKNOWN'
        | 'MAINTENANCE'
        | 'UNAVAILABLE';

    reservationState:
        | 'NOT_RESERVED'
        | 'RESERVED'
        | 'PROTECTED'
        | 'BACKUP';

    sourceVersion: string | number;
    observedAt?: string;
    projectedAt: string;
    isStale: boolean;
}
```

Exact field names are implementation choices.

Do **not** invent a new business meaning for freshness timeout while doing this work. The architecture requires the projection to define its **source, version/order handling, stale behavior and rebuild procedure**, but the exact timeout should come from the approved operational/technical contract rather than being hidden in the Three.js client.

---

# 9. Aggregated visualization projection — architecture alignment

The FE should not call:

```text
Facility API
+ Operations API
+ IoT API
+ Reservation API
```

and then attempt to decide the truth itself.

The latest ownership direction already prevents this.

## Authority

**Parking Operations**

owns:

```text
accepted physical occupancy
UNKNOWN / uncertainty
claims
holds
reservation protection
backup coverage
allocation
visits
authoritative availability
```

**IoT & Recognition**

owns:

```text
raw observations
sensor data
camera/LPR observations
```

Raw IoT observations are input to Operations reconciliation. They are not direct authority for the Three.js slot.

**Facility & Visualization**

owns:

```text
site
floor
zone
slot identity
slot code
coordinates
layout
model bindings
maintenance/service configuration
fixed 3D assets
```

The six-service architecture explicitly establishes this ownership split.

## Preferred visualization query direction

A good implementation consistent with the architecture is:

```text
Operations
    │
    │ accepted operational-state projection
    ▼
Facility Visualization Read Model
    │
    ├── slot id/code
    ├── layout/coordinates
    ├── physical state
    ├── reservation/protection
    ├── source version
    ├── observed/projected timestamps
    └── stale indicator
    │
    ▼
FE / Three.js
```

Facility does **not** become the owner of the operational state merely because it exposes the visualization read model.

Any operational state cached/projected there must be:

```text
read-only
source-identifiable
versioned
freshness-aware
rebuildable from Operations
```

This is specifically allowed by the data-ownership baseline.

An API Gateway/BFF composition is also architecturally possible, but do not move business-state aggregation logic into React/Three.js.

---

# 10. Current backend/API implementation should be treated as transitional

The current backend already exposes:

```text
GET /api/parking-lots/{id}/operational-layout
```



The current FE API types also already contain richer fields:

```text
operationalStatus
isPhysicallyOccupied
reservationState
```



This is useful implementation groundwork.

However, documentation should **not claim that the current FacilityService endpoint represents the final microservice authority design**.

The target six-service baseline supersedes the old service boundary:

```text
Facility & Visualization
→ resource/configuration/layout

Parking Operations
→ accepted operation/claims/availability
```

Therefore the current endpoint can be documented as a **transitional implementation/read endpoint** until service boundaries are fully migrated.

Do not redesign the whole microservice migration inside SPARK-228. Instead, make the 3D binding layer depend on a clean visualization contract so the underlying endpoint can later move without rewriting Three.js.

---

# 11. Two-channel 3D visualization

The agreed design is to preserve the two state dimensions visually.

## Channel A — physical condition

Represents:

```text
AVAILABLE
OCCUPIED
UNKNOWN
MAINTENANCE
UNAVAILABLE
```

Possible implementation styles include base material, floor marker, vehicle proxy, icon or texture.

The implementation is free to select the visual style as long as the states remain distinguishable and accessible.

## Channel B — reservation/protection

Represents:

```text
RESERVED
PROTECTED
BACKUP
```

This should be an independent overlay:

```text
outline
halo
border
marker
badge
or another non-destructive layer
```

Do not represent reservation by replacing the physical state.

For example:

```text
OCCUPIED
+ RESERVED halo
```

must still visibly communicate both meanings.

This matches the existing binding document's better architectural direction, which described physical visualization and reservation/protection overlays separately.

---

# 12. Do not overwrite enterprise model materials unnecessarily

The existing `SlotMaterialManager` replaces slot materials with generated `MeshStandardMaterial` instances. 

That can be acceptable for simple internal template models, but it becomes problematic for textured or enterprise-quality models because it can erase:

```text
paint
surface markings
PBR textures
slot labels
material identity
```

Recommended direction:

```text
preserve original material
+
apply minimally destructive visualization overlays
```

Especially for reservation/protection:

```text
outline/halo/helper mesh
```

rather than replacing the original asset material.

This also prepares the renderer for future Phase 2 enterprise ingestion without making Phase 2 part of the current MVP.

---

# 13. 3D interaction boundary

SPARK-228 requires the 3D model to remain non-authoritative.

Current raycasting/select behavior is acceptable **only as UI selection/navigation**.

Clicking a slot can:

```text
select
focus camera
open information panel
show availability explanation
prepare navigation
```

It must not directly:

```text
mark OCCUPIED
create reservation
allocate slot
release slot
mark backup
change payment
change authoritative availability
```

Any later command must go through the normal application/domain workflow with permission and confirmation.

The baseline consistently states that Facility/3D projections never become a second occupancy or availability authority.

---

# 14. SignalR behavior

SignalR is confirmed for 3D live views.

However, the correct pattern is:

```text
initial page load
      ↓
GET current visualization projection
      ↓
render
      ↓
SignalR event arrives
      ↓
update/refetch affected projection
```

After reconnect:

```text
SignalR reconnect
      ↓
GET authoritative current projection again
      ↓
replace/reconcile local view
```

Do not assume missed SignalR messages can reconstruct authoritative state.

The approved baseline explicitly requires current authorized state to be fetched again after reconnect and states that missed messages never change authoritative capacity/business rights. SignalR handles client updates while RabbitMQ is used for durable inter-service outcomes.

The current approved healthy-local-profile target for these live views is **p95 ≤2 seconds**, but that is a target requiring measurement, not evidence already achieved.

---

# 15. SPARK-228 — concrete correction matrix

| Area | Current condition | Required correction |
|---|---|---|
| Slot identity | Spatial/index mapping | Map using stable Facility `slot.code` |
| Missing slot | Creates `slot-auto-*` | Mark UNMAPPED; never fabricate operational slot |
| Missing state | Defaults to available | Use UNKNOWN/STALE |
| Physical state | available/occupied only | Add AVAILABLE/OCCUPIED/UNKNOWN/MAINTENANCE/UNAVAILABLE |
| Reservation state | `reserved` is physical status alternative | Separate NOT_RESERVED/RESERVED/PROTECTED/BACKUP |
| Rendering | Single material/status | Two independent visual channels |
| Backend source | Viewer supplied simple `ParkingSlot[]` | Consume visualization DTO/projection |
| Operational authority | Risk of FE interpreting status | Operations remains authoritative |
| IoT | Potential direct visual input | Raw observation only; Operations reconciles |
| Staleness | Document claims more than implementation | Implement explicit freshness metadata/handling |
| Click behavior | Selection exists | Keep read/navigation only |
| Realtime | Not fully architecture-aligned | SignalR update + state refetch/recovery |
| Testing | Documentation matrix, no matching committed tests | Add executable tests and recorded integration checks |

---

# 16. Minimum tests required for SPARK-228

The implementation should not be submitted as complete until at least the following behavior is demonstrably tested:

| Test | Expected result |
|---|---|
| Stable slot-code mapping | Same backend slot always updates same model object regardless of API array ordering |
| Reordered API response | No visual slot binding changes |
| Missing backend slot | Model slot becomes UNMAPPED/UNKNOWN, not AVAILABLE |
| Unknown backend state | Visible UNKNOWN representation |
| OCCUPIED + RESERVED | Both physical and reservation dimensions visible |
| AVAILABLE + PROTECTED | Both dimensions visible |
| MAINTENANCE + RESERVED | Maintenance remains visible; reservation not erased |
| UNAVAILABLE + reservation | Unavailable remains physical truth |
| Stale projection | Visible stale/degraded state; no default-to-free |
| Backend failure | Existing state becomes degraded/stale according to contract |
| SignalR disconnect/reconnect | Full authorized state is fetched after reconnect |
| Duplicate live event | Does not produce duplicated state effects |
| Model with shuffled node order | Correct slot-code mapping retained |
| Backend slots shuffled | Correct model binding retained |
| Slot click | Does not mutate operational backend state |
| Model reload/unmount | No leaked renderer/material/geometry/texture resources |

Test fixtures may be deterministic Sprint 2 fixtures where the full Operations implementation is not yet available, but they must use the same DTO contract as the real projection.

---

# 17. SPARK-186 — documentation and render-optimization corrections

`3D_MODEL_IMPLEMENTATION_GUIDE.md` is useful, but currently mixes:

```text
implemented behavior
planned architecture
recommended optimization
claimed measurements
```

These need to be separated.

## Required document structure

Use explicit labels:

```text
IMPLEMENTED
PROPOSED / TARGET
OPTIONAL OPTIMIZATION
MEASURED
NOT YET VERIFIED
PHASE 2
```

A reader must never interpret example architecture as already implemented code.

---

# 18. Correct technology-stack drift in the implementation guide

The current FE repository is React 19 + Vite + Three.js. `package.json` currently has no Next.js, GSAP, Jest/Vitest or STOMP dependencies and contains no `test` or `lint` command. 

Therefore documentation should not present those technologies as existing SmartPark runtime dependencies unless they are actually added.

Correct wording should distinguish:

```text
Current:
React
Vite
TypeScript
Three.js

Optional/recommended if later adopted:
GSAP
Draco loader
testing framework
other rendering helpers
```

If a library is only shown in an example, label it explicitly as an example.

---

# 19. Draco/compression documentation

The implementation guide currently documents Draco compression and measured improvements.

The current inspected viewer loads models with `GLTFLoader`, but no matching `DRACOLoader` configuration was found in the implementation. 

Therefore choose one of these valid directions:

### Direction A — implement it

Actually:

```text
compress the target GLB
configure DRACOLoader
commit reproducible assets/config
measure results
record commit/environment
```

### Direction B — keep as recommendation

Rewrite documentation as:

```text
Recommended optimization — not currently enabled
```

Do not present a theoretical command as evidence of the production/current implementation.

---

# 20. Performance evidence must be reproducible

The guide currently contains detailed benchmark claims for:

```text
TTI
FPS
draw calls
VRAM
network transfer
Draco decode
```

That is useful only if the evidence is reproducible.

For each measured result record:

```text
commit SHA
model version/hash
browser version
machine CPU/GPU/RAM
resolution/device pixel ratio
network profile
test steps
profiling tool
before asset
after asset
raw measurement or screenshot/trace location
```

Do not use a measured-looking number if the test was not actually executed.

The broader SmartPark baseline follows the same principle: approved performance values are **targets**, not achieved results, until tests are run and evidence is recorded.

---

# 21. Resource cleanup

Current code already performs useful cleanup, including renderer/controls/material cleanup and animation-frame cancellation. 

Improve it by explicitly disposing loaded model resources when replacing/unmounting a GLB:

```text
geometry.dispose()
material.dispose()
texture.dispose()
```

where applicable.

Do not only:

```text
scene.remove(model)
```

because removal from the scene does not itself guarantee GPU resources are released.

Document the disposal strategy and provide a repeat mount/unmount/reload test rather than claiming zero leaks without evidence.

---

# 22. Missing/corrupt asset handling

Current behavior falls back to the procedural lot when GLB loading fails. 

The fallback is useful, but it must be visible.

Recommended:

```text
MODEL LOAD FAILED
Using procedural fallback
```

or an equivalent non-intrusive UI state.

Otherwise a tester/operator can believe the real facility model loaded successfully.

The fallback also must never create the impression that generated slots correspond to authoritative facility slots unless a deterministic fixture mapping explicitly exists.

---

# 23. SPARK-186 completion evidence

Before moving SPARK-186 to Done, documentation should provide or link:

| Evidence | Required |
|---|---|
| Current FE integration guide | Yes |
| Correct current stack | Yes |
| Optimization implemented or clearly marked optional | Yes |
| Reproducible benchmark procedure | Yes |
| Actual benchmark output | Yes if claiming measured results |
| Model/resource disposal verification | Yes |
| Missing/corrupt model path verified | Yes |
| Slot-binding optimization does not break stable mapping | Yes |
| Build clean | Yes |
| Lint clean | Add/execute if required by DoD |
| Relevant tests | Yes |
| PR/Jira traceability | Yes |

SPARK-186 should not be considered completed merely because the guide is long or contains benchmark tables.

---

# 24. Enterprise Model Ingestion document

`3D_ENTERPRISE_MODEL_INGESTION_RULES.md` should **not be deleted**.

It should be repositioned as:

```text
Status: PHASE 2 — PROPOSED SPECIFICATION
Not part of SmartPark v1.0 MVP acceptance
```

The existing document already contains sections marked pending PM/team review. Those sections should no longer appear to be active v1.0 requirements.

The current v1.0 decision baseline confirms **fixed 3D deployment assets**, and the data-ownership guide explicitly says to persist asset version/resource bindings and layout metadata rather than building a cloud upload pipeline for these fixed models. 

---

# 25. v1.0 versus Phase 2 boundary

## v1.0

Keep:

```text
three approved facility archetypes/templates
fixed GLB deployment assets
versioned asset references
stable slot-code mapping
layout/resource binding
backend operational-state visualization
SignalR-supported live updates
loading/fallback/error handling
performance optimization
```

Do not require:

```text
Owner arbitrary GLB upload
enterprise self-service ingestion
automatic semantic model analysis
cloud-hosted 3D model pipeline
arbitrary model schema normalization
automatic database synchronization from uploaded GLB
```

## Phase 2

May explore:

```text
enterprise model upload
validation
material preservation
semantic metadata validation
mapping wizard
ingestion manifest
owner/admin approval
asset revision/migration
automated compatibility checks
```

The Phase 2 proposal should reuse the same v1 identity contract:

```text
3D model semantic slot
→ stable Facility slot code
```

This prevents v1 and future enterprise ingestion from developing incompatible mapping systems.

---

# 26. Enterprise document wording to correct

Change wording such as:

```text
Active Specification
```

to:

```text
Phase 2 Proposed Specification
```

Add a prominent scope notice:

```text
This document describes a proposed future enterprise/self-service
3D ingestion capability.

SmartPark v1.0 uses fixed, versioned deployment assets.

Nothing in this document should be interpreted as requiring Owner-uploaded
arbitrary 3D models, cloud ingestion, automatic DB synchronization,
or Phase 2 ingestion services for v1.0 acceptance.
```

Rules that are useful even in v1 can be moved or referenced from the active implementation guide, especially:

```text
stable semantic identifiers
material-preservation principles
asset versioning
model validation
coordinate conventions
```

But clearly distinguish these general modeling standards from the deferred self-service ingestion pipeline.

---

# 27. Documentation source-of-truth hierarchy

When documents conflict, use this precedence for the 3D work:

```text
1. Latest explicitly approved SmartPark decisions
2. SmartPark Microservice Architecture and Data Ownership v1.0
3. v1.0 baseline decision review
4. Jira acceptance criteria
5. Active architecture/design documentation
6. Current implementation
7. Older historical/proposal documents
```

Current code is evidence of what is implemented, but it does not override an approved architecture decision.

Likewise, an approved architecture document does not prove code/tests are already complete.

The baseline explicitly warns against treating approved behavior as implementation completion.

---

# 28. Recommended file-level work

| File/component | Required action |
|---|---|
| `3D_MODEL_IMPLEMENTATION_GUIDE.md` | Align with actual stack; separate implemented/planned/measured; correct performance claims; document stable binding and lifecycle cleanup |
| Backend slot-binding document | Rewrite around O-authoritative state → F visualization projection → FE |
| `3D_ENTERPRISE_MODEL_INGESTION_RULES.md` | Reclassify as Phase 2 Proposed Specification |
| `src/lib/types.ts` | Replace single `SlotStatus` representation for 3D integration with independent physical/reservation types |
| `src/lib/3d/slotMatcher.ts` | Replace spatial/index authority with stable slot-code mapping |
| `src/lib/3d/slotMaterialManager.ts` | Stop defaulting missing state to available; implement two-channel visual model |
| `Parking3DViewer.tsx` | Consume visualization DTO; render unknown/stale/unmapped visibly; maintain read-only interaction boundary |
| FE API adapter | Add/use a visualization projection endpoint rather than browser-side domain aggregation |
| Realtime integration | SignalR notification/update handling + full projection refresh on reconnect |
| Tests | Add mapping/state/staleness/reconnect/disposal tests |
| Jira/PR evidence | Link tests, screenshots/trace, commit/PR and completion evidence |

---

# 29. What does NOT need another project-lead decision

The teammate can proceed without asking the project lead again on:

```text
using slot code instead of array index
not defaulting UNKNOWN to AVAILABLE
keeping backend UUID as entity identity
separating physical and reservation/protection state
making 3D read-only/non-authoritative
using Operations as operational authority
using Facility for model/layout/resource mapping
making display projection rebuildable/versioned
showing stale/unmapped state visibly
refetching state after SignalR reconnect
disposing Three.js resources correctly
correcting documentation that claims unimplemented libraries/features
labeling unexecuted measurements as NOT RUN
moving enterprise ingestion to Phase 2 proposed scope
```

These follow already-approved architecture or are implementation correctness measures that preserve it.

---

# 30. Items that should be escalated before inventing a solution

Ask the project lead only if implementation uncovers a need to change one of these:

```text
A new authoritative service boundary
A new physical/reservation business state
A new Owner/Admin permission
A new business staleness timeout with operational consequences
A new v1 model-upload capability
A new automated action from the 3D scene
A new capacity/reservation rule
A change from fixed v1 deployment assets to owner-uploaded models
A state transition that could release or grant capacity
```

Do not hide any of these as a frontend implementation detail.

---

# 31. Definition of success for this correction

After the corrections, the following scenario should work reliably:

```text
Facility:
Slot UUID = 9c...
Slot Code = B1-A-023
Model binding = B1-A-023

Operations:
Physical = OCCUPIED
Reservation = RESERVED
Version = 481

Visualization projection:
slotId = 9c...
slotCode = B1-A-023
physicalState = OCCUPIED
reservationState = RESERVED
sourceVersion = 481
stale = false

Three.js:
finds model object B1-A-023
renders OCCUPIED physical representation
renders RESERVED overlay
does not change either state itself
```

If Operations becomes unavailable or the projection becomes stale:

```text
B1-A-023
→ STALE / UNKNOWN visual treatment
```

not:

```text
B1-A-023
→ AVAILABLE
```

If a different API ordering is returned:

```text
mapping remains identical
```

because the identity is the slot code, not array index.

If SignalR disconnects:

```text
reconnect
→ refetch projection
→ restore current view
```

rather than trusting missed incremental messages.

That is the behavior SPARK-228 should demonstrate.

---

# 32. Jira disposition after the correction

### SPARK-228

Do **not** move to Review until:

```text
stable slot-code mapping exists
no auto-AVAILABLE fallback exists
all required physical states exist
reservation/protection is independent
UNKNOWN/stale handling exists
3D remains non-authoritative
projection contract is documented
tests/manual evidence are recorded
existing 3D assets still render correctly
```

### SPARK-186

Keep in Review until:

```text
implementation guide matches actual technology
optimization claims correspond to implementation
measurements are reproducible
resource cleanup is verified
fallback/error cases are documented/tested
slot-state binding remains correct after optimization
DoD evidence is linked
```

### Enterprise Model Ingestion

Do **not** treat it as a v1 Jira completion dependency.

Maintain it as:

```text
Phase 2 / Proposed Specification
```

and remove any wording that makes the self-service ingestion pipeline appear mandatory for SmartPark v1.0.

---

# Final architectural principle

The 3D digital twin is a **projection of SmartPark**, not SmartPark's operational brain.

```text
Facility says:
"What resource is this and where is it?"

Operations says:
"What is currently true operationally about it?"

IoT says:
"What did the devices observe?"

Three.js says:
"How should that approved information be visualized?"
```

Those responsibilities must remain separate.

If that boundary is preserved, future SignalR updates, IoT integration, enterprise models, additional facility layouts and different rendering strategies can evolve without turning the frontend or the 3D asset into a second source of truth.