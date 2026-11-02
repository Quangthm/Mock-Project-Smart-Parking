# SPARK-322 — Operational fixture alignment

## A. Executive summary

The operational fixtures had a valid two-axis slot shape, stable UUIDs, and consistent car counts, but omitted a required zone metric, described an unrealized operator/session context, overstated motorcycle occupancy, and used a fixed "current" timestamp that was stale at runtime. The stale fixture also had slot updates later than its snapshot time. These mismatches made the demo differ from the FacilityService projection and could obscure stale-data behavior.

Both fixture files now match the verified wire shape and the current projection's count rules. The development fixture loader gives its static snapshot a current observation time; the deliberately stale fixture remains frozen. The Operator view labels fixture data as simulated. A fixture response is rejected if its site ID differs from the requested site, rather than silently attaching it to a real site.

## B. Contract audit

Source: the local `origin/develop` tracking ref (`d269c686`) and the checked-out implementation (`fdb84d6f`), read without changing branches. The local `develop` ref (`f188870d`) is older and does not contain `OperationalLayoutProjection.cs`; the tracking ref and checkout agree on the DTO below. A remote update after the local tracking ref is **UNVERIFIED**.

Verified references:

- `src/Services/FacilityService/SmartParking.FacilityService.Application/OperationalLayoutProjection.cs`: version `1.2.0`, DTO fields, enums, and 30-second threshold.
- `src/Services/FacilityService/SmartParking.FacilityService.Persistence/PostgresOperationalLayoutReader.cs`: projected slot state, `isBookable`, capacity calculations, `isEvCharging`, and current null session/operator context.
- `src/Services/FacilityService/SmartParking.FacilityService.Application/OperationalLayoutRedaction.cs`: public sanitization of sensitive operator data and BACKUP state.
- `FE/src/lib/operationalLayout.ts`: frontend DTO types and two-axis visual/staleness adapters.

| Field / concept | Previous fixture | Verified current contract | FE change |
|---|---|---|---|
| Parking ID | One fixed UUID shared by both files | `siteId: Guid` | Kept fixture-specific UUID; the loader now rejects a request for another site ID. |
| Slot ID / mesh key | 48 fixed UUIDs and distinct raw slot codes | `slotId: Guid`, raw `slotCode: string` | Kept IDs/codes; tested uniqueness and cross-file identity. |
| Physical status | Five known states | `AVAILABLE`, `OCCUPIED`, `UNKNOWN`, `MAINTENANCE`, `UNAVAILABLE` | Kept separate from reservation; tested all values and fail-closed visualization. |
| Reservation / protection | Four states, including simultaneous occupancy and holds | `UNRESERVED`, `RESERVED`, `PROTECTED`, `BACKUP` | Kept independent of physical state; no flattening. |
| Requested / allocated | Non-null `operatorContext.approachingReservation` examples; zone allocation count absent | DTO permits JSON context, but current reader emits `operatorContext: null`; zone `allocatedSlotCount` currently approximates subtree `RESERVED` count | Set operator context to null and populated both zone counts from RESERVED slots. True requested-versus-allocated facts remain **OPEN** in BE. |
| Capacity | Car totals consistent; motorcycle 8 occupied with no occupied motorcycle slot; zone-only counts zero | Car capacity counts CAR slots; motorcycle occupied counts occupied MOTORCYCLE slots; both zone reserved fields currently approximate subtree RESERVED count | Motorcycle capacity is 20/0/20; zone allocation and reserved counts are 5 and 4. |
| `sessionType` / EV | Several non-null session types; four EV slots not occupied | Current reader emits `sessionType: null`; `isEvCharging` is true only for an occupied EV slot | Set session type null. Existing false EV flags match the four vacant EV slots. |
| Stale metadata | Static current timestamp from 2026-10-26; stale slots updated after stale snapshot | `generatedAt`, `isFresh`, and `stalenessThresholdSeconds=30`; current BE reports snapshot time at read | Dev loader rebases only the current snapshot's observation time. Stale file remains `isFresh=false` and its slot updates are no later than `generatedAt`. Missing `isFresh` now fails closed. |

The raw fixture JSON is a deterministic captured example, not a continually live snapshot. Its `isFresh=true` means it was current at capture. Only the development loader refreshes `generatedAt` when serving that example; the IDs, states, and counts remain fixed. The stale file is not loaded by the production path.

## C. Modified files

| File | Change and reason |
|---|---|
| `FE/src/fixtures/slots-operational-fixture.json` | Added required zone metric, reconciled zone and motorcycle counts, removed context values not currently emitted. |
| `FE/src/fixtures/slots-operational-stale-fixture.json` | Same schema/count alignment; kept an intentionally stale snapshot with consistent slot update times. |
| `FE/src/lib/operationalLayout.ts` | Missing `isFresh` fails closed; fixture activation is explicit dev-only; current demo snapshot gets a runtime observation time. |
| `FE/src/lib/parkingApi.ts` | Fixture site ID must match the requested site; a mismatch never falls through to HTTP. |
| `FE/src/roles/operator/parking-lot/LotStatus.tsx` | Visible simulated-data notice in fixture mode. |
| `FE/tests/operational-fixture-contract.test.cjs` | Focused schema, count, state, identity, stale, and isolation checks. |

## D. Acceptance criteria

| Criterion | Status | Evidence |
|---|---|---|
| Fixture matches API/DTO | PASS | FE contract test validates fields and count semantics against the verified DTO/reader. |
| Stale fixture is intentional | PASS | `isFresh=false`, older timestamp, and slot updates no later than snapshot; test passes. |
| UNKNOWN is not AVAILABLE | PASS | Existing 20-cell adapter test and new FE test pass. |
| Physical and reservation states separated | PASS | Both fields retained; occupied-plus-hold visualization tested. |
| Deterministic fixture IDs | PASS | Fixed UUIDs and matching IDs/codes across both files, tested. |
| No obsolete fields/enums | PASS | Fields and enum values match current DTO; unrealized context values are null. |
| Fixture consumers render correctly | BLOCKED for browser | Adapter/visual tests pass; no SmartParking page was open for manual browser inspection. |
| Mock distinguished from production | PASS in code/tests | Dev-only flag, Operator demo notice, site-ID check, and production-path test. |

## E. Testing evidence

- `node --test FE/tests/operational-fixture-contract.test.cjs`: **4 PASS, 0 FAIL**.
- `node --test tests/frontend/operational-layout-adapter.test.cjs tests/frontend/operational-layout-visual.test.cjs`: **26 PASS, 0 FAIL**.
- `npx tsc --noEmit` from `FE/`: **PASS**.
- `npm run build` from `FE/`: **PASS**, with large-chunk and ineffective-dynamic-import warnings.
- Follow-up verification (2026-11-02): `node --test` with all ten `tests/frontend/*.test.cjs` files and `FE/tests/operational-fixture-contract.test.cjs`: **144 PASS, 0 FAIL**. The ten stale cases in `tests/frontend/operator-provisioning.test.cjs` now exercise the current Operator, Sites, Owner dashboard, and Parking Structure flows. The test harness mocks the current imports and React hooks; obsolete Add Unit/Add Slot expectations were replaced with assertions for the current read-only structure UI and backend load/error states. This follow-up was explicitly authorized to edit the root frontend tests.
- Follow-up `npx tsc --noEmit` and `npm run build` from `FE/`: **PASS**. The build still reports large-chunk and ineffective-dynamic-import warnings.
- Browser availability check: Chrome DevTools exposed only `about:blank`. Driver discovery/details/booking, Owner structure, Operator 2D/3D, and Admin were **not manually verified**. No app or backend services were started.
- No lint or test script exists in `FE/package.json`; the tests above were run directly with Node.

## F. Backend dependencies

- **OPEN:** The reader comments state that `isFresh=true` is a prototype until a real freshness source exists. FE relies on its existing 30-second timestamp guard but cannot create authoritative freshness.
- **OPEN:** Current FacilityService has no source for a true requested-versus-allocated slot distinction or live session/plate context; it emits null operator context. Zone `allocatedSlotCount` and `zoneOnlyReservedCount` are approximations from subtree RESERVED slots in the current reader.
- **UNVERIFIED:** No live API/browser E2E check was possible; local `origin/develop` may lag the remote after its last fetched commit.

## G. Remaining risks

- Operator fixture mode needs the selected site ID to equal the fixture UUID; otherwise it fails clearly. This avoids showing one site's simulated slots under another site's identity.
- The broader FE still contains separate local demo data and booking flow behavior. SPARK-322 changes only operational-layout fixtures and their consumers; it does not make local booking data authoritative.
- Manual browser regression remains for follow-up. The ten root frontend harness failures were resolved in the authorized follow-up. No backend files were modified.
