# Trace edits

- FR-LOT-01 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-LOT-02 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-LOT-03 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-LOT-04 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-LOT-05 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-LOT-06 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-LOT-07 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-POL-01 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-POL-02 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-POL-09 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-CAP-01 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-MAP-01 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-RES-01 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-RES-04 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-RES-05 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-RES-10 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-BAS-05 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-RPT-01 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-BAS-06 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-GATE-02 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-GATE-04 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-GATE-08 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-INC-01 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-RPT-02 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-BAS-07 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-MAP-03 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-MAP-04 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-SIM-01 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-BAS-09 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-AI-01 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-BAS-10 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-BAS-11 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-BAS-12 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-BAS-13 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-BAS-14 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-BAS-15 rule coverage derived through its existing use cases (association, not a new behavior)
- FR-LOT-05 added to US-O03/US-O05 reverse lists (SPLIT-CF-21 corrected).
- A-01 explicitly links FR-MON-11/14 ↔ UC-MON-05 ↔ M-05/M-07; E03 links FR-RES-04 ↔ UC-MON-04 ↔ M-07.
- US-O05 linked to FR-PAY-06 for approved Operator ordinary refund handling.

Abbreviated source FR lists were expanded. Missing direct rule links use existing UC-associated rule relationships; these are navigation associations, not asserted one-to-one implementation tests. Monthly M-rule edges were made explicit. No requirement ID was renumbered or dropped.


## RC2 security additions

FR-AUTH-07–11, BR-AUTH-04–07, UC-AUTH-04–06 and US-SEC01–03 are new IDs. FR-AUTH-02/FR-BAS-08 gain staged-login rule links; FR-AUTH-05 gains assisted-recovery links. No original ID is removed or renumbered. TOTP-FUTURE source labels are superseded by SEC-01/02; previous comparison reports remain RC1 history.

## RC3 / PAY-01 — 13 October 2026

Amended FR-PAY-03/BR-PAY-03 channel implementation priority and the acceptance environment. Existing FR/BR/US/UC edges and matrix.csv remain valid. Added decision-to-acceptance mapping to payment traceability; AC-066–072 supplement retained scenarios. No money, capacity, permissions or authentication rule changes.
