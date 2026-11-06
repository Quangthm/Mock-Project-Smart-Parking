# UserService JWKS — API Design

| Metadata | Value |
| --- | --- |
| Status | Implementing approved SPARK-327 request |
| Contract version / date | 1 / 2026-11-05 |
| Owner | UserService |
| Requirements | SRS v1.1 quality requirements §4.3; approved authorization ADR 2026-11-04 |
| Implementation | Uncommitted SPARK-327 changes; see task session for verification |

## Overview and traceability

UserService is the exclusive user access-token issuer. This public discovery endpoint supplies its active verification key. See [security requirements](../../01-requirements/srs/modules/11-quality-deployment.md), [authorization ADR](../../02-architecture/architecture-decisions/authorization-freshness-and-current-access.md) and [operation matrix](../../01-requirements/srs/authorization-operation-classification.md). No business permissions are granted by publication.

## API specification

| Item | Contract |
| --- | --- |
| Method / URL | `GET /.well-known/jwks.json`; deployment may set `Jwt:JwksPath` |
| Authentication / origin | Anonymous / PUBLIC |
| Scope | Active UserService public signing key only |
| Request | No body, parameters or bearer token required |
| Response | HTTP 200; `application/jwk-set+json`; `Cache-Control: public, max-age=300` |

```http
GET /.well-known/jwks.json
```

Response shape (modulus placeholder is illustrative, not usable key material):

```json
{"keys":[{"kty":"RSA","kid":"smartpark-local-01","use":"sig","alg":"RS256","n":"<Base64URL public modulus>","e":"AQAB"}]}
```

`keys` contains exactly one active key. `n` and `e` are Base64URL unsigned public RSA integers; `kid` exactly matches issued JWT JOSE headers. Private parameters and application configuration are never serialized. The standard JWKS shape intentionally has no application success envelope. See [RFC 7517](https://www.rfc-editor.org/rfc/rfc7517.html) and [RFC 7518](https://www.rfc-editor.org/rfc/rfc7518.html).

## Validation, errors and state outcomes

Malformed issuer/audience, key ID, publication path or private key configuration fails startup with sanitized configuration errors. A configured key ID requires stable private material even in Development. Non-Development always requires a valid key. Other HTTP errors retain existing API handling. Repeated GET requests have no side effects and return the same key until restart/reconfiguration. There is no external network or database dependency in publication.

Consumers must validate signature, issuer, audience, RS256 and expiration using configured trusted JWKS location; token-supplied `jku` or `x5u` is never authority. Use HTTPS or equivalently protected transport in production. Internal HTTP here is solely the documented local Development boundary.

## Deferred work and review checklist

- Consumer bearer middleware and caching/refetch/fail-closed behavior remain separate work.
- Only one key is published. Overlapping rotation and production key storage are not implemented. Never reuse a kid for a different key; no historical key registry detects such reuse.
- Future rotation must publish both keys before switching signing and retain the old key for at least 900 seconds plus cache/clock margins after its final issuance. Current publication cache adds 300 seconds; UserService skew is zero.
- Existing UserService session/account checks, refresh rotation and MFA are preserved. JWKS does not resolve the ADR's stale-token or authorization-race debt, nor implement REQUEST Sagas.
- Review anonymous access, exact public-only shape, matching kid, verification across restarts, malformed configuration, and rejection of forged/expired/wrong-issuer/wrong-audience/wrong-algorithm JWTs using `JwtJwksTests`.

Operational instructions: [local setup](../../../LOCAL-SETUP.md), section 5.5.

Deployment verification: [JWT/JWKS Postman guide](../../04-testing/postman/SPARK-327-JWT-JWKS-guide.md), with importable files and manual restart, MFA and expiry checkpoints.
