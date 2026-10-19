# Design handoff — RC1

Use requirements policy/permission/state contracts before API and UI design. Screens must show separate booking, pass, money and physical states; requested versus allocated resource; disclosed minimum/deposit/balance/surplus; fixture integration labels; and pending/UNKNOWN outcomes. A-01 cancellation shows affected future bookings and links to their explicit ordinary cancellation flow before pass cancellation can complete. Do not auto-cancel/reprice or promise rebooking capacity.

Wire schemas, page designs and implementation are pending; this package does not claim UI completion.


## RC2 security screens

Add optional Authenticator enrollment/confirmation, restricted second-step challenge, backup-code display/regeneration, voluntary disable, restricted replacement and pre-login ACCOUNT_RECOVERY form. Admin case view shows evidence references and explicit PASSWORD/TOTP/BOTH authorization; never passwords/secrets/codes. Explain reset versus disable and show REPLACEMENT_REQUIRED. UI cannot issue a full session early. Wire schemas and implementation remain pending.
