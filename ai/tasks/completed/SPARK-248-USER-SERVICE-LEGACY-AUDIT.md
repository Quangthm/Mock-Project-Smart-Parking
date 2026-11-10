# User Service Legacy Implementation Audit
**Task ID:** SPARK-248-USER-SERVICE-LEGACY-AUDIT
**Date:** 2026-11-01
**Status:** COMPLETED (Audit Phase)

## A. Executive Summary

The `UserService` vertical slice currently contains three overlapping generations of implementation.

1.  **Legacy (Gen 1):** Monolithic patterns from `05.x` scripts (e.g., `access_version`, `onboarding_complete`).
2.  **Custom Durable (Gen 2):** A homegrown outbox/projection system for access events (`access_event_outbox`, `AccessOutboxWorker`).
3.  **Modern (Gen 3):** The approved SPARK-248 pattern using MassTransit EF Transactional Outbox and Account-based authorization.

**Key Findings:**
- **High Risk:** The canonical database scripts are missing columns (`access_version`, `onboarding_complete`) that the runtime code still maps, meaning fresh installs will crash.
- **Redundancy:** There are two competing outbox mechanisms. The custom `AccessEventStore` is entirely redundant given the MassTransit outbox.
- **Boundary Violation:** `UserService` is directly performing SMTP/SMS delivery via `WorkflowTransport`, which belongs in `Notification & Audit Service`.

## B. Candidate Inventory

| Candidate | Type | Current purpose | Evidence/usage | Requirement/owner | Replacement | Classification | Confidence | Risk |
|---|---|---|---|---|---|---|---|---|
| `User.AccessVersion` | Property/Column | Event ordering | `AppDbContext.cs:136`, `User.cs:18` | Obsolete | Authoritative state check | **REMOVE** | High | Low |
| `User.OnboardingComplete` | Property/Column | Onboarding flag | `AppDbContext.cs:142`, `User.cs:17` | Redundant | `OwnerApplication.Status` | **REMOVE** | High | Low |
| `access_event_outbox` | Table | Custom outbox | `AccessEventStore.cs:20` | Superseded | MassTransit Outbox | **REMOVE** | High | Medium |
| `access_event_inbox` | Table | Idempotency log | `AccessEventStore.cs:50` | Superseded | MassTransit Inbox | **REMOVE** | High | Medium |
| `access_projection` | Table | Access state cache | `AccessEventStore.cs:55` | Superseded | Direct Account check | **REMOVE** | High | Medium |
| `AccessOutboxWorker` | HostedService | Custom relay | `AccessMessaging.cs:29` | Superseded | MassTransit Worker | **REMOVE** | High | Medium |
| `WorkflowTransport` | Service | SMTP/SMS Delivery | `WorkflowTransport.cs:16` | Wrong Owner | Notification Service | **MOVE / REHOME** | High | High |
| `OtpEmailTemplate` | Class | Email Templates | `OtpEmailTemplate.cs:9` | Wrong Owner | Notification Service | **MOVE / REHOME** | High | High |
| `Class1.cs` | File | Placeholder | `Infrastructure/Class1.cs` | None | None | **REMOVE** | High | Low |

## C. Database Drift Report

| Object | Domain Model | EF Model | Canonical SQL | Legacy SQL (`05.x`) | Status |
|---|---|---|---|---|---|
| `users.access_version` | Yes | Yes | **No** | Yes | **DRIFT** |
| `users.onboarding_complete`| Yes | Yes | **No** | Yes | **DRIFT** |
| `access_event_outbox` | No | Yes | **No** | Yes | **OBSOLETE** |
| `workflow_deliveries` | Yes | Yes | Yes | Yes | ALIGNED |
| `OutboxMessage` | Yes | Yes | Yes | No | ALIGNED |

**Critical Gap:** A fresh installation using `microservices/01-user-access-service-db.sql` will fail because the C# code expects `access_version` and `onboarding_complete` to exist.

## D. RabbitMQ/Outbox Map

### 1. Profile Event Flow (Modern - KEEP)
`ProfileController` $\rightarrow$ `UpdateOwnProfileCommand` $\rightarrow$ `IEventPublisher` $\rightarrow$ **MassTransit EF Outbox** $\rightarrow$ RabbitMQ $\rightarrow$ Consumers.

### 2. Access-Change Flow (Legacy - REMOVE)
`ActivateOperator` $\rightarrow$ `IAccessEventStore.Stage` $\rightarrow$ **`access_event_outbox`** $\rightarrow$ `AccessOutboxWorker` $\rightarrow$ RabbitMQ $\rightarrow$ `AccessProjectionConsumer` $\rightarrow$ **`access_projection`**.

### 3. OTP Delivery Flow (Mixed - REHOME)
`AccountWorkflowService` $\rightarrow$ `workflow_deliveries` table $\rightarrow$ `WorkflowDeliveryWorker` $\rightarrow$ `WorkflowTransport` $\rightarrow$ **Direct SMTP/SMS API**.
*Note: The intent log (`workflow_deliveries`) is correct, but the transport (`WorkflowTransport`) is in the wrong service.*

## E. Service-Ownership Violations

- **Violation:** `UserService` $\rightarrow$ `WorkflowTransport` $\rightarrow$ SMTP/SMS Providers.
- **Approved Boundary:** `UserService` publishes `NotificationRequested` $\rightarrow$ `Notification & Audit Service` $\rightarrow$ SMTP/SMS Providers.
- **Status:** **APPROVAL REQUIRED** for the new event contract, but the current implementation is definitely misplaced.

## F. Recommended Cleanup Sequence

1.  **Schema Alignment:** Add `access_version` and `onboarding_complete` to canonical SQL (temporary fix) OR remove them from C# entities (permanent fix).
2.  **Outbox Consolidation:** Migrate `OperatorAccessChanged` events to use the MassTransit Outbox.
3.  **Legacy Removal:** Drop `access_event_outbox`, `access_event_inbox`, `access_projection` and remove `AccessOutboxWorker`.
4.  **Notification Migration:** Define `NotificationRequested` contract $\rightarrow$ Move `WorkflowTransport` logic to Notification Service.
5.  **Persistence Cleanup:** Remove obsolete columns from `users` table.

## G. High-Confidence Safe Cleanup

- Remove `src/Services/UserService/SmartParking.UserService.Infrastructure/Class1.cs`.
- Remove `access_event_outbox` related code (once migrated to MassTransit).

## H. Approval-Required Decisions

**Problem:** `UserService` owns the delivery of OTPs/Emails.
**Current Behavior:** Uses `WorkflowTransport` to call SMTP/SMS providers.
**Recommended Direction:** `UserService` stores the "intent" in `workflow_deliveries`, then publishes a `NotificationRequested` event. `Notification & Audit Service` consumes this and handles the actual transport.
**Trade-offs:** Increases asynchronous complexity but enforces strict service boundaries.
**Approval Required:** Approval of the `NotificationRequested` event schema.

## I. Verification Evidence

- `rg "access_version"`: Found in `User.cs`, `AppDbContext.cs`, and legacy scripts.
- `rg "workflow_deliveries"`: Confirmed as application-level state, distinct from MassTransit outbox.
- `Glob src/Services/UserService/**/*`: Mapped all components of the identity slice.
- `Read scripts/database/microservices/01-user-access-service-db.sql`: Confirmed missing columns.
