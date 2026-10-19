# Quality Deployment — SRS v1.0 RC3

[Requirements index](../README.md) · [Traceability](../../requirements-traceability/README.md)

**Status:** review candidate, 13 October 2026; implementation/evidence not assessed. Source review section numbers below refer to the decision review, not this module numbering.

| Integration | Approved local profile | Evidence / limit |
|---|---|---|
| Payments | **payOS QR first:** controlled, low-value production integration tests plus deterministic failure/race fixtures; tested Operator cash collection | PAY-01 replaces the one-real-sandbox gate. VNPay/MoMo/ZaloPay retain separate planned identities and honest integration/fixture/deferred labels. Native VNPay may be simulated/deferred if resources are insufficient. Fixtures do not prove real collection/refund delivery; no claim that payOS implements all three providers. |
| Media | Evaluate Cloudinary adapter for backend upload, restricted reads, holds, deletion and recovery; fixtures for deterministic failures | Provider capability and account limits unverified until tested. No production/free-tier suitability claim. |
| Notifications | Local email capture and SMS fixtures; real delivery tests separately labelled if configured | No proof of external delivery from local capture. Applies to all-role OTP flows. |
| Hardware/gate | Deterministic events and Operator workflows | No real adapter or physical device acceptance. |
| Maps | Manual location fallback and deterministic coordinates for core tests; configured provider connectivity tested separately | No GPS-only dependency or claim of provider integration from coordinates alone. |
| Assistant/LPR | General contracts and manual/ordinary-UI fallback; detailed integration/evaluation deferred after v1.0 | No accuracy, model-completion or live-provider claim from fixtures. |
| Deployment | Docker local profile, one instance per required service, separate PostgreSQL databases/credentials, RabbitMQ and scoped SignalR | One PostgreSQL server permitted. No mandatory clustering or Redis scale-out. |

PAY-01 supersedes the earlier one-real-payment-sandbox requirement with controlled low-value payOS production tests plus deterministic failure/race fixtures. Three native real payment integrations, live SMS/email, real hardware and production HA are not required by this local acceptance profile. Local fixtures must be isolated from any real-money/live-device environment. UNKNOWN payment outcomes are reconciled rather than blindly recharged.

| Aspect | Approved target / recipe | Required test-plan detail |
|---|---|---|
| Candidate reference machine | 4 CPU cores and 16 GB RAM, subject to actual team equipment | Declare actual CPU/RAM/OS/container allocations, build and commit; different hardware is reported explicitly. |
| Seed scale | 10 lots; 1,000 represented spaces/category-capacity units; 10,000 users; 100,000 historical reservation/session records | Define distribution, active claims, vehicle categories and representative image sizes; do not double-count a represented slot and its pool projection. |
| Warm-up | 2 minutes | Exclude warm-up from steady-state percentile reporting. |
| Steady workload | 20 concurrent users for 10 minutes | Declare search/availability, detail reads and operational/reservation command proportions and pacing. |
| Burst | 50 concurrent users for 1 minute | Report separately from steady state; do not average away burst failures. |
| Core API latency | p95 ≤500 ms | Core read/write endpoint classes reported separately; external-provider work separately measured. |
| Unexpected errors | <1% | Expected business conflicts separated; report their frequency rather than hiding them. |
| SignalR freshness | p95 ≤2 seconds under healthy local profile | Define origin-commit-to-authorized-client-update measurement and report reconnect recovery separately. |
| Restart recovery | ≤5 minutes | Preserve acknowledged local transactions and durable recovery intents; demonstrate reconciliation without duplicate financial/capacity effects. |
| Backup schedule / RPO | Daily backup; backup RPO ≤24 hours | Backup-loss tolerance is separate from ordinary-restart durability. Failed/overdue backup must be visible. |
| Restore | ≤30 minutes | Restore into declared clean local target and verify core data/relationships and recoverable workflows. Define recovery dataset/configuration scope. |

No incompatible double capacity commitment, duplicate collection/refund or unauthorized access is acceptable merely because aggregate latency/error targets pass. Scope provider upload/payment/AI latency separately. Record production, sandbox (if separately used) and fixture modes and actual timings; unexecuted tests stay NOT RUN. These are local mock-project objectives, not production SLOs or claims of achieved throughput.

## Functional requirements

## Retained quality requirements

These requirements remain specified; no achievement is asserted. Original production load/availability/scale tables move to FUTURE reference. Local approval replaces those acceptance gates, not unrelated security/compatibility/maintainability constraints.

### 4.3 Security Requirements

| Requirement | Specification |
| :---- | :---- |
| Password Policy | Minimum 8 characters, uppercase, lowercase, number, special character |
| Password Hashing | bcrypt with cost factor 12 |
| Session Token Expiry | Access 15 minutes; refresh absolute 7 days; inactivity 24 hours |
| OTP Expiry | 5 minutes |
| Account Lockout | 15 minutes after 3 failed attempts |
| Data in Transit | TLS 1.3 mandatory for APIs, TLS 1.2+ for internal services |
| Data at Rest | AES-256 encryption for databases and backups |
| Sensitive Data | CCCD/GPLX are not collected or stored in this phase. If future scope introduces such data, the applicable encryption/tokenization controls shall apply. |
| API Rate Limit | 100 requests/minute per user, 10 requests/minute per IP (unauthenticated) |
| JWT Algorithm | RS256 (asymmetric) |
| Audit Log Retention | 7 years |
| Payment Security / PCI DSS | Payments shall use a PCI DSS-compliant third-party payment provider. SmartPark shall not directly store, process, or transmit raw cardholder data. If card payments are introduced, card data shall be collected through the provider's hosted payment page, secure payment form, or equivalent PCI DSS-compliant mechanism. Applicable PCI DSS validation requirements shall be determined based on the final payment architecture, transaction volume, and provider/acquirer requirements. |

### 4.5 Compatibility Requirements

#### Web App

- Chrome 90+  
- Safari 14+  
- Firefox 88+  
- Edge 90+  
- Responsive design for 320px-2560px width

#### API

- Semantic versioning (v1, v2)  
- 12 months deprecation notice for breaking changes  
- OpenAPI 3.0 documentation

### 4.6 Usability Requirements

| Metric | Requirement |
| :---- | :---- |
| Task Success Rate | \> 95% |
| Time on Task (Find parking) | \< 45 seconds (web) |
| Time on Task (Make reservation) | \< 90 seconds (web) |
| Time on Task (Complete payment) | \< 60 seconds (web) |
| Error Rate | \< 2% |
| User Satisfaction (CSAT) | \> 4.2/5 |
| Net Promoter Score (NPS) | \> 50 |

### 4.7 Maintainability Requirements

| Metric | Requirement |
| :---- | :---- |
| Code Coverage | 80% minimum |
| Unit Test Coverage | 80% per microservice |
| Integration Test Coverage | 70% API contracts |
| Technical Debt Ratio | \< 5% |
| Code Duplication | \< 3% |
| Code Complexity | \< 10 cyclomatic complexity per function |
| API Documentation | 100% coverage with OpenAPI 3.0 |

### 4.8 Observability Requirements

| Metric | Requirement |
| :---- | :---- |
| Metrics Collection | All services |
| Metrics Retention | 90 days |
| Log Retention | 90 days (hot), 1 year (cold) |
| Trace Retention | 30 days |
| Dashboard Coverage | All services |
| Alerting Rules | All critical paths |
| On-call Coverage | FUTURE production target; actual project support availability requires PM declaration |
| Incident Response Time | FUTURE production target; no active staffed-response SLA |

---


## Current MFA acceptance and security

SEC-01/SEC-02 adds real local TOTP enrollment/challenge, single-use backup recovery, reviewed Admin reset and session/security-version enforcement. Protect TOTP secrets at rest, hash backup codes, redact QR/secrets/codes/tokens from all logs, bound attempts and expiry, reject replay and enforce revocation at resource APIs/SignalR. Use the security-reviewed profile in [authentication recovery](../authentication-recovery.md); numeric technical parameters must be finalized/tested before acceptance. Existing primary-token/OTP values remain unchanged. Email/SMS delivery retains declared capture/fixture modes. See the additional acceptance scenarios; all are NOT RUN.
