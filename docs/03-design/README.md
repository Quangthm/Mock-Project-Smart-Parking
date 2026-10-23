# Design handoff — v1.1 documentation review candidate

Use canonical requirements, policy/permission/state contracts before API and UI design. This is a **v1.1 documentation review candidate based on approved decisions**; wire agreement, implementation and acceptance evidence remain separate.

[API contracts and design index](../02-architecture/api/README.md) consolidates the active minimal designs. [Partner onboarding handoff](../05-project-management/handoffs/OWNER_ONBOARDING_BE_HANDOFF.md) covers first-lot setup, simulation, agreement uploads and review/provisioning/opening gates. [Geographic discovery contract](../02-architecture/api/v1.1-contracts.md#discovery-and-parking-products) is separate from [internal 2D/3D visualization](3d-digital-twin/README.md).

Screens show separate booking, monthly/yearly coverage, money and physical states; requested versus allocated resource; disclosed minimum/deposit/balance/surplus; fixture integration labels and pending/UNKNOWN outcomes. Existing A-01 cancellation shows affected future bookings and links to explicit ordinary cancellation before pass cancellation can complete. Do not auto-cancel/reprice or promise rebooking capacity.

## Authentication and onboarding screens

Partner Registration retains the Owner role and requires simulation plus saved first-lot setup before email OTP/verification and combined review. Admin replacement is limited to initial pending application agreement/policy documents with preserved versions/reasons; review binds the inspected versions. No in-app corrections/re-acceptance or third outcome. Pending/failed initial provisioning blocks normal login; separate safe reasons and email communication remain. Operator activation requires verified email contact and simulation; password setup is optional. The sole seeded Admin has no registration/role-management screen and remains protected from administrative lock/disable/delete/role changes.

Completed normal login returns necessary tokens; retrieve authorized profile/account information separately. Preserve optional Authenticator enrollment/confirmation, restricted second-step challenge, backup-code display/regeneration, voluntary disable, restricted replacement and pre-login ACCOUNT_RECOVERY form. Admin case view shows evidence references and PASSWORD/TOTP/BOTH authorization; never passwords/secrets/codes. Explain reset versus disable and show REPLACEMENT_REQUIRED. UI cannot issue a full session early; the sole Admin uses controlled maintainer recovery.

## Facility and discovery screens

Additional-lot proposals have independent approval; rejected history is immutable and Draft as new creates another proposal, identical content allowed. FE multi-select deactivation explicitly includes/handles descendants; one invalid item means no batch mutation, with per-item errors. Vehicle soft removal blocks active dependencies and retains history.

MapTiler supports consented current/manual origin, 5 km default/expansion, independent vehicle/lot-type filters and backend facts. No interval means current availability and tariff with unit, not an invented total. Selected interval means interval availability and estimated total comparison. Show full/stale/empty/provider errors honestly; detail selection opens the separate internal model for booking with backend revalidation. Directions open an external map; no embedded routing/turn-by-turn or unsourced ETA.
