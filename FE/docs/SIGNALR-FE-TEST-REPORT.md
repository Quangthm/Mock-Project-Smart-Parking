# Frontend realtime verification

The checks below distinguish mocked lifecycle behavior from integration across authenticated browser sessions. The [implementation report](./SIGNALR-FE-IMPLEMENTATION.md) lists the covered screens and intervals.

| Feature | FE implemented | BE event available | Tested | Result |
| --- | --- | --- | --- | --- |
| Driver parking availability | No authoritative map or booking-slot integration | No verified event | Static audit | BLOCKED BY BACKEND |
| Booking synchronization | Operator reservation reads refresh. Driver booking flow is unchanged. | No verified event | No cross-session test | PARTIAL |
| Owner parking structure | Foreground REST refresh | No verified event | Build and shared scheduler mock only. Screen tests blocked by test stubs. | PARTIAL |
| Operator check-in/out | Existing action-triggered refresh and foreground dashboard reconciliation | No verified event | No live gate action | NOT TESTED |
| Parking prices and deposit | No authoritative management/read integration | No verified event | Static audit | BLOCKED BY BACKEND |
| Parking policies | No published Owner policy integration | No verified event | Static audit | BLOCKED BY BACKEND |
| Payment status | Scoped polling for pending checkout | No verified event | No real payOS transaction | NOT TESTED |
| Admin approval | Application list reconciles via REST | No verified event | No live approval | NOT TESTED |
| Owner sees approval | No verified Owner status endpoint | No verified event | Static audit | BLOCKED BY BACKEND |
| Owner Operator Management | Shared connection, burst coalescing and reconnection invalidation | `OperatorsChanged` | Mocked connection and 20-event burst | PASS |
| Role switching and assignment isolation | Scope checks and cleanup. Existing role switch logic reused. | No verified Operator-side assignment event | No authenticated multi-role browser session | NOT TESTED |
| Connection recovery | Reconcile after reconnect and tear down after final unsubscribe | Owner hub verified in source | Mocked disconnect/reconnect and existing lifecycle tests | PASS |
| Access revocation | Existing client behavior retained | FE contract only in this checkout | Existing mocked revocation suite | PARTIAL |
| Direct DB changes | Eventual reads for covered authorized APIs | No verified DB-to-hub integration | No direct DB mutation or latency measurement | NOT TESTED |

## Automated evidence

- `node --test tests/frontend/*.test.cjs FE/tests/server-realtime.test.cjs`: 212 passed, 5 failed. Two new mocked tests passed.
- The five suite failures occur before behavior assertions in `tests/frontend/operator-provisioning.test.cjs`. Line 251 rejects the existing `./LotActionMenu` import. Lines 334, 347, 357 and 365 reject `motion/react`. Reproduce with `node --test tests/frontend/operator-provisioning.test.cjs`. The test harness lives outside the FE-only edit boundary.
- `tsc --noEmit`, `npm run build` and `git diff --check` passed. Vite reported large chunks and the existing mixed static/dynamic import of `operationalLayout.ts`.

## Live QA scenarios still to run

1. In separate Owner and Operator sessions, run an authorized check-in and checkout. Keep the Owner capacity screen visible. Check the affected capacity and reservation values after the next scheduled read. Repeat while changing the site or leaving a slot detail open.
2. Open Owner Operator Management in two tabs and activate a test Operator in another session. Check the Owner hub event and API requests. Disable network, activate another test Operator from an independent session, then reconnect and check the list reconciles without a reload.
3. Change an Operator's authorized site or slot permission from Owner. Confirm the Operator dashboard reloads the authorized assignment list, clears removed site data and updates the Backup controls. Repeat after role switch and logout.
4. In two Admin sessions, change a test account's status or review an application. Check that the other Admin's list updates while search, filters, notes and pagination remain in place.
5. Use a real development payment transaction to verify polling stops after authoritative success or failure. Hide and restore the tab while pending to verify status recovery.
6. Measure visibility after an approved test database write through an authorized test operator. The expected observation is the next relevant API refresh if the read model reflects the write. Record measured latency and whether any deployed event was emitted.
7. Repeat the Driver availability, price and competing-booking cases once the missing Driver-authorized contracts are available. Confirm with two Driver sessions and an Owner or Operator session, without relying on browser storage.
