# Owner Policy Settings

## Frontend implementation

The Owner workspace shows a Default Policy summary and a searchable list of owned parking lots. On narrow screens each table row becomes a card. The pencil opens the relevant Policies, Pricing, and Deposit editor. Six policy topics have separate text fields and drafting prompts.

Changes remain in memory while the workspace is mounted. Keep Draft retains a draft on this page, Cancel discards the open editor's changes, and Preview reads the effective draft. A lot draft overrides only the fields edited for that lot. Reset to Default requires confirmation and removes all overrides from the open draft. Save Changes is disabled while persistence is unavailable.

Existing browser-stored documents and policy values are neither migrated nor deleted. The new editor does not use them as published values. The existing Platform Policies documents and their read-only viewer are reused. Driver policy and booking/payment behavior are outside this change.

## API availability

| Feature | API available | FE ready | Backend dependency |
| --- | --- | --- | --- |
| Owner parking lots | Yes | Live list, search, retry, and empty states | Owner-scoped Facility `GET /api/parking-lots` |
| Default Owner Policy | No management API found | Draft editor and preview | Owner-scoped read and version-checked update |
| Effective lot policy and overrides | No management API found | Per-field draft overrides | Read effective values and field provenance, validate ownership, and update only changed overrides |
| Pricing | Calculation contract exists. No Owner management API found | Draft car/motorcycle rates, VND formatting, and 1,000 VND validation | Authoritative tariff reads/writes, supported rate fields and concurrency control |
| Deposit | No Owner management API or compatible quote verified | Whole 0–100% draft field and refund/booking text | Read and update the permitted Owner configuration and return a server-calculated quote |
| Reset to Default | No management API found | Confirmed draft reset | Clear lot overrides atomically and return the effective policy |
| Platform Policies | Existing FE read-only content | Existing viewer | None for this change |
| Versioning, publishing and PDF | No Owner publishing or published PDF API found | Read-only web draft preview | Publication status, revisions and effective dates that preserve accepted booking terms. A published document source is needed for PDF export. |

The proposed dependencies describe capabilities and do not define new endpoint paths. No FE calculation is used as a charge or deposit quote.

## Changed frontend files

- `src/roles/owner/policies/PolicySettings.tsx`
- `src/roles/owner/policies/PolicyDraftEditor.tsx`
- `src/roles/owner/policies/PolicyPencil.tsx`
- `src/components/modals/LotPolicyModal.tsx`
- `docs/OWNER-POLICY-SETTINGS-FE-IMPLEMENTATION.md`

## Verification

- TypeScript `tsc --noEmit` and Vite production build passed. Vite reported large chunks and an ineffective dynamic import for `operationalLayout.ts`.
- An automated component smoke check with stubbed React hooks exercised negative and off-step prices, out-of-range and fractional deposits, blank edited policy text, the disabled Save Changes button, draft retention, preview, cancellation, field inheritance, and confirmed reset.
- `node --test tests/frontend/*.test.cjs` reported 211 passed and 4 failed. All four failures are in `tests/frontend/operator-provisioning.test.cjs` at lines 334, 347, 357 and 365. The existing Parking Structure test harness rejects its `motion/react` import before assertions run.
- There is no lint script in `FE/package.json`. Visual, keyboard, authenticated API and backend persistence checks remain unverified in a browser.

## Manual QA

1. Sign in as an Owner, open Policy Settings and confirm that only that Owner's lots appear. Search for one lot and check no-results behavior. Simulate a failed lot-list request and retry.
2. Draft a car hourly rate of 25,000 VND, a deposit of 20%, and text in one policy topic. Preview and keep the default draft. Open a lot and confirm those values are inherited.
3. Override the lot hourly rate to 30,000 VND. Change the default to 35,000 VND. Confirm the lot still shows 30,000 VND and that the draft filter identifies it as customized.
4. Open Reset to Default, cancel it once, then confirm and keep the reset draft. Confirm the lot now shows 35,000 VND. Try entering 1,500 VND and 101% to check the errors. Cancel an edit and confirm it is discarded.
5. Check the UI in light and dark themes at desktop and phone widths, including keyboard focus, scrolling and the pencil controls. Open each Platform Policies document and confirm its original content is available.
6. Reload and confirm that draft values disappear. Verify that existing Driver policy documents and booking flows still behave as before.
