# File Media — business rules RC1

[Rule index](README.md) · [SRS module](../srs/modules/09-file-media.md)

## BR-PRIV-01

**Status:** CONFIRMED — implementation pending / not assessed

Apply least data exposure and approved processing boundaries under the domain contract, the domain contract and the domain contract; do not treat unspecified identity fields as authority to collect documents.

**Trace:** FR-AUTH-06, FR-RPT-05; UC-RPT-01, UC-RPT-02, UC-AI-03

## BR-PRIV-02

**Status:** CONFIRMED — implementation pending / not assessed

Persist protected originating audit facts and project them through Notification & Audit. Each owner enforces scoped access, retention and acknowledged holds. Images max 30 days; audit 7 years; operational logs 90 days hot + 1 year cold; financial documents 7 years from issuance; conversations 90 days from last message, unaffected by administrative reads. Review holds every 30 days and at case closure; release only originating hold, preserving others. Provider deletion target 24 hours does not extend a hard retention deadline.

**Trace:** FR-RPT-05; UC-RPT-02, UC-LPR-01

## BR-PRIV-03

**Status:** CONFIRMED — implementation pending / not assessed

Persist protected originating audit facts and project them through Notification & Audit. Each owner enforces scoped access, retention and acknowledged holds. Images max 30 days; audit 7 years; operational logs 90 days hot + 1 year cold; financial documents 7 years from issuance; conversations 90 days from last message, unaffected by administrative reads. Review holds every 30 days and at case closure; release only originating hold, preserving others. Provider deletion target 24 hours does not extend a hard retention deadline.

**Trace:** FR-RPT-03/04; UC-AUTH-02, UC-RPT-02
