# File Media — business rules v1.1

[Rule index](README.md) · [SRS module](../srs/modules/09-file-media.md)

## BR-PRIV-01

**Status:** CONFIRMED — implementation pending / not assessed

Apply least exposure and owning-service authorization. Initial Partner agreement/policy uploads are required restricted scope: retain original/replacement versions, actor/time/reason, bind approval to the reviewed version, and permit Admin replacement only while the initial application is pending. No other fields, additional-lot document editing or changes to finalized applications. Stable service-owned assets and authenticated reads replace arbitrary URLs. Formats/limits, storage concurrency and retention classification remain Q01 design-pending; mandatory simulated VNeID returns a GUID substitute associated with the user and persisted in the User & Access database. Additional mock fields, consent/completion evidence encoding and retention remain Q02; the GUID is neither a real identity document/number nor an authentication token.

**Trace:** FR-AUTH-01/05/06, FR-RPT-05; UC-RPT-01, UC-RPT-02, UC-AI-03

## BR-PRIV-02

**Status:** CONFIRMED — implementation pending / not assessed

Persist protected originating audit facts and project them through Notification & Audit. Each owner enforces scoped access, retention and acknowledged holds. Images max 30 days; audit 7 years; operational logs 90 days hot + 1 year cold; financial documents 7 years from issuance; conversations 90 days from last message, unaffected by administrative reads. Review holds every 30 days and at case closure; release only originating hold, preserving others. Provider deletion target 24 hours does not extend a hard retention deadline.

**Trace:** FR-RPT-05; UC-RPT-02, UC-LPR-01

## BR-PRIV-03

**Status:** CONFIRMED — implementation pending / not assessed

Persist protected originating audit facts and project them through Notification & Audit. Each owner enforces scoped access, retention and acknowledged holds. Images max 30 days; audit 7 years; operational logs 90 days hot + 1 year cold; financial documents 7 years from issuance; conversations 90 days from last message, unaffected by administrative reads. Review holds every 30 days and at case closure; release only originating hold, preserving others. Provider deletion target 24 hours does not extend a hard retention deadline.

**Trace:** FR-RPT-03/04; UC-AUTH-02, UC-RPT-02
