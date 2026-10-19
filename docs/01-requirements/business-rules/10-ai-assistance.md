# Ai Assistance — business rules RC1

[Rule index](README.md) · [SRS module](../srs/modules/10-ai-assistance.md)

## BR-AI-01

**Status:** CONFIRMED DIRECTION — detailed integration/evaluation POST-v1.0

Static answers use approved help content; live price, availability, policy and personal state use authoritative APIs. Model output does not replace price/ranking/allocation engines or invent N values.

**Trace:** FR-AI-02/03/04/11; UC-SEARCH-01, UC-AI-01, UC-FUT-02

## BR-AI-02

**Status:** CONFIRMED DIRECTION — detailed integration/evaluation POST-v1.0

Tools are allowlisted and authorized for the authenticated session on every call; model-supplied user IDs cannot change account scope. Do not forward credentials or unrelated personal records to the model.

**Trace:** FR-AI-05/10; UC-AI-01, UC-AI-02, UC-AI-03

## BR-AI-03

**Status:** CONFIRMED DIRECTION — detailed integration/evaluation POST-v1.0

Reservation preparation is a draft. Final UI review/edit/confirmation binds the submitted parameters; later changes invalidate that confirmation. Backend revalidates before creating; a conversational yes alone does not silently submit an altered form.

**Trace:** FR-AI-06/07; UC-AI-02

## BR-AI-04

**Status:** CONFIRMED DIRECTION — detailed integration/evaluation POST-v1.0

Financial/access/legal authority stays in the domain contract. No raw DB, barrier, policy override or direct charge/refund tools for the model. Ordinary checkout and human refund decisions remain distinct from AI assistance.

**Trace:** FR-AI-08/10; UC-AI-01, UC-AI-02, UC-FUT-03

## BR-AI-05

**Status:** CONFIRMED DIRECTION — detailed integration/evaluation POST-v1.0

Report actual API outcome: empty data is different from unauthorized, invalid input, conflict, timeout or service failure. Read retry may be bounded; write retry requires a stable idempotency/result-lookup contract.

**Trace:** FR-AI-09; UC-AI-01
