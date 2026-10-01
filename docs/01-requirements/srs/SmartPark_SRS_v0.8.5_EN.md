<!-- LOCALIZATION-START -->
> **Language edition: English (EN).** This edition is the v0.8.5 baseline revision. The Vietnamese (VI) edition is its translation; identifiers, policy keys, API names, state codes, values and remaining open decisions have the same meaning.
<!-- LOCALIZATION-END -->

# Software Requirements Specification (SRS)

## Smart Parking Management System

**Project**: SmartPark  
**Baseline Version**: 0.8.5
**SRS Version**: 0.8.5 (Baseline Revision)  
**Date**: 2026-10-01  
**Status**: Working Baseline Revision  
**Prepared by**: Quang

---

<!-- INTEGRATION-START IR-01 -->

## Baseline Revision Notice — BR-01

**Revision date:** 2026-10-01. **Source baseline:** v0.8. **Revision status:** v0.8.5 working baseline revision; C-01–C-23 are incorporated according to the recorded decisions. Remaining A/X/F items are still proposals, decisions or future/deferred scope unless explicitly changed below.

This revision preserves the original v0.8 requirement structure and traceability while incorporating only the explicitly resolved C-01–C-23 decisions. Existing A/X/F material that was not resolved remains visible and inactive; it is not silently promoted into the baseline. The revision is a requirements baseline update, not an implementation or architecture approval.

| Label | Meaning and implementation treatment |
|---|---|
| C — Confirmed baseline | Business behavior explicitly resolved for v0.8.5 and may be treated as normative. |
| A — Proposed refinement | Compatible elaboration or missing workflow retained for review. It is not a confirmed baseline commitment unless explicitly promoted below. |
| X — Decision required | Conflicts with a remaining unresolved requirement, proposal or deferred decision. Keep the alternative visible and inactive. |
| F — Future / deferred | Retained outside current MVP acceptance unless explicitly promoted below. |

For an affected detail, a **C** entry is normative for v0.8.5. Remaining X/A/F entries stay non-normative until separately resolved or promoted. The Appendix E decision register records the basis of each C-01–C-23 resolution. Unresolved tester and architecture findings remain open/deferred unless directly closed by one of those decisions.

**Reading map:** §2.3.1 permission matrix; §2.4.1 integration names; §3.0 User Stories; domain FR tables throughout §3 and §6; §5.2 Business Rules and policy register; §6.8 AI readiness, contracts and dependencies; Appendix E resolved-decision/change log; Appendix F current MVP monthly-pass design; Appendix G review checks.

Requirements already described in v0.8 are referenced rather than copied. FR tables identify observable system capabilities; Business Rules define constraints and boundary behavior; User Stories state actor goals. Repeating an identifier in traceability is not a second requirement definition. Original duplicate headings and citation artifacts are documented in Appendix E and have not been editorially removed. Legal, performance and infrastructure statements in the original SRS are preserved, not independently certified by this requirements review.

<!-- INTEGRATION-END IR-01 -->

## 1\. Introduction

### 1.1 Purpose

This Software Requirements Specification (SRS) document describes the functional and non-functional requirements for the SmartPark.

SmartPark is a **configurable parking management and reservation platform for a simulated parking environment**.

The system is intended to manage the relationship between parking users, vehicles, parking capacity, reservations, physical occupancy, parking sessions, payments, operational handling, violations, and configurable parking policies.

The document serves as the primary reference for:

- Development teams implementing the system  
- QA teams defining test strategies  
- Operations teams planning deployment and monitoring  
- Stakeholders reviewing and approving requirements

### 1.2 Scope of Project

SmartPark is a comprehensive platform for managing automobile and motorcycle parking lots with the following capabilities:

**In Scope (Current MVP):**

- User registration and vehicle management  
- Parking lot discovery and real-time status tracking  
- Parking space reservation and allocation
- Monthly pass management using the rolling prepaid monthly-pass model in Appendix F, which is part of the v0.8.5 MVP
- Automated fee calculation and payment processing  
- Reservation conflict handling, backup capacity, and queue management  
- Configurable policy parameters
- Real-time notifications via SMS, email, and push  
- Administrative dashboard for parking lot operators  
- Reporting and analytics  
- AI-powered license plate recognition  
- AI chatbot for user support
- Operational issues
- Basic violation handling
- Appeals/support
- Hierarchical parking policies: Admin-defined defaults/bounds with Owner lot-level override where applicable
- 3D digital twin

**Out of Scope (Current Release):**

- Mobile application
- Autonomous vehicle integration  
- Detailed complaint/ticketing system  
- Loyalty points program  
- Full offline mode  
- National digital map integration
- IoT sensor integration for automated status updates


**AI-Enhanced Features (Future Releases):**

- AI-assisted parking spot status detection  
- Personalized/model-based AI parking recommendations
- AI capacity forecasting for dynamic pricing  
- AI anomaly detection for fraud and operations  

---

## 2\. Overall Description

### 2.1 Business Context

Parking operations can fail when reservations, capacity, physical occupancy, payments, and operator actions are treated as one undifferentiated state. SmartPark is intended to provide a unified, auditable parking-management workflow for drivers and parking businesses while explicitly handling uncertainty and operational failure.

Relevant business problems include:

- Uncertain availability information.
- Reservation conflicts caused by overstays, unknown departures, or physical changes.
- Unauthorized vehicles occupying parking capacity.
- Inconsistent information from sensors, cameras, gates, simulations, and operators.
- Manual operational handling of exceptions.
- Need for facility-specific policies without separate codebases.
- Need to distinguish payment completion from valid parking authorization.

SmartPark's business objective is therefore to create a **consistent, auditable parking-management workflow**

### 2.2 System Context Diagram



### 2.3 System Actors

| Actor | Main Responsibilities / Goals | Status |
|---|---|---|
| Driver / Customer | Search parking, view information, create reservations, pay, view history, submit appeals | **BASELINE** |
| Operator | Check vehicles in/out, monitor parking, respond to incidents, perform manual operational actions, manage devices for assigned parking lots | **BASELINE** |
| Owner | Manage parking facilities, operators, pricing and supported parking policies, view revenue and analytics report | **BASELINE** |
| Admin | Manage users, system configuration and audit logs; approve Owner registrations; define defaults/bounds for Owner-configurable policies | **BASELINE** |

<!-- INTEGRATION-START PERM -->

### 2.3.1 Permission Matrix — Integration Review

The actor table above describes the v0.8.5 baseline roles. This matrix records the detailed permission allocation; remaining A/X rows expose proposals or unresolved boundaries instead of granting new permissions. “Own” / “assigned” scopes must be enforced by backend resource authorization. An administrative role does not imply all other business roles. External integrations use service credentials, not a human role.

| Function / permission | Driver | Operator | Owner | Admin | Status / restriction |
| --- | --- | --- | --- | --- | --- |
| Register/verify/sign in and sign out | Own account | Own provisioned account | Own provisioned account | Own provisioned account | A; FR-AUTH-01/02; internal account provisioning remains policy-controlled. |
| Personal profile and vehicles | Own records | — | — | — | A; FR-AUTH-03, FR-VEH-01/02. Account/vehicle ownership changes need separate review. |
| Search, price information, maps, chatbot | Public/customer use without an account; account required only for account-scoped features | Operational views only | Own-lot views only | — | C; anonymous users may use search, price information, maps, and chatbot. Reservations and other account-scoped customer transactions require an account. |
| Create/cancel/view reservations; pay/view history | Own records | Assigned operational actions only | Facility exceptions only | — | A; FR-RES, FR-PAY; no role implicitly approves refunds. |
| Submit/view appeals | Own cases | Record on verified Driver's behalf | — | — | C; appeal follows appeal → evidence → Operator approval → refund; Owner may handle an escalated refund. |
| Check-in/check-out and ticket verification | — | Assigned lot/lane | — | — | A; FR-GATE; cash and emergency release need separate grants. |
| Monitor occupancy/incidents | — | Assigned lots | Owned lots | — | R/A; §3.7.1, §3.9.2; restricted customer data. |
| Record incidents/wrong-slot observations | — | Assigned lots | — | — | A; detailed violation handling remains subject to retained proposal/deferred workflow. |
| Manage lot profile/layout/service status | — | Granted operational override only | Owned lots | — | R/A; §3.2.1/2; changing capacity must check active commitments. |
| View financial transactions and reports | Own payments only | Only assigned checkout/case data | Owned lots | — | R/A; §3.8; viewing is not editing a ledger. |
| ACCOUNT_ADMIN | — | — | — | Platform | C; Admin approves Owner registration after required company name, email and phone information; role changes remain audited. |
| VEHICLE_BINDING_REVIEW | — | — | — | Proposed reviewer | X; C-11; evidence and reviewer permission require approval. |
| OPERATOR_MANAGE | — | — | Owned lots | — | R; §3.7.5; only delegable, lot-scoped permissions. |
| LOT_POLICY_MANAGE | — | — | Owned lots | Platform defaults only | C; Owner configures lot policies; Admin configures defaults and permitted bounds for every Owner-configurable policy/variable. |
| PLATFORM_POLICY_MANAGE | — | — | — | Platform | C/R; Admin defines defaults and permitted bounds for platform policies; Owner may override Owner-configurable policies at lot scope within those bounds. |
| DEVICE_MANAGE | — | Assigned parking lots | — | — | C; Operator manages lot devices only within parking lots assigned by Owner. |
| PLATFORM_INTEGRATION_MANAGE | — | — | — | Platform adapters/settings | A/X; FR-LOT-06; lot device boundary subject to C-02 and ARCH-DEF-03. |
| DEVICE_STATUS_VIEW | — | Assigned lots | — | — | C/F; Operator may view assigned-lot device status. Owner device reporting remains future/deferred while IoT is deferred. |
| SLOT_OVERRIDE | — | Explicit lot grant | — | — | C; manual override is authoritative only when initiated by an authorized actor under an approved operational condition and is audited. |
| CASH_COLLECT | — | Explicit lot grant | — | — | C; Operator may record manual cash collection through the CASH_COLLECT interface. |
| VIOLATION_REVIEW | — | Explicit lot grant | — | — | A/X; C-12. Human verification before an enforceable charge. |
| APPEAL_REVIEW | — | Explicit lot grant | Optional escalation handling | — | C; appeal flow is appeal → evidence → Operator approval → refund; Owner may handle escalated refunds. |
| REFUND_APPROVE | — | — | Escalated financial handling | — | C; Owner may handle a refund when an Operator escalates an appeal. Acceptance does not itself complete the refund. |
| EMERGENCY_GATE_RELEASE | — | Explicit lot grant | — | — | C; permission-gated emergency release/priority handling with audit and recovery. |
| AI_THRESHOLD_MANAGE | — | — | — | Proposed platform permission | X; C-14. Do not reinterpret fixed baseline thresholds as configurable without approval. |
| SIMULATION_RUN | — | — | — | Proposed demo-only permission | X; C-15; a test stub is distinct from a scenario-management product. |
| AUDIT_VIEW | — | Assigned operational history only | Own staff/lot history only | Platform audit | R/A; §3.7.4/5, FR-RPT-03/04; no direct log editing. |

<!-- INTEGRATION-END PERM -->

## 2.4 External Systems / Physical Entities

| Entity | Potential Responsibility | Status |
|---|---|---|
| Payment provider | Process payments/refunds: VNPay, MoMo and ZaloPay in MVP; additional providers may be added later | **BASELINE** |
| Camera | Physical observation / vehicle detection | **FUTURE / PROPOSED** |
| ANPR/LPR service | Vehicle/license-plate recognition | **FUTURE / PROPOSED** |
| Parking sensor | Occupancy observation | **FUTURE / DEFERRED** |
| Gate/barrier | Physical access event | **FUTURE / PROPOSED** |
| Map service | Location/search support: Google Maps / Mapbox  | **BASELINE** |
| Notification service | Email/SMS/push | **BASELINE** |
| AI/LLM service | AI chatbot and approved AI assistance | **MVP — limited scope** |
| Simulation engine | Generate controlled parking scenarios and failures | **FUTURE / DEFERRED** |

<!-- INTEGRATION-START NAMES -->

### 2.4.1 Consistent Integration Names

These are logical labels used by the added requirements. They do not add providers, replace the original entities, or settle service deployment boundaries.

| Canonical label | Existing SRS entity / capability | Boundary and scope |
| --- | --- | --- |
| Payment Provider | Payment provider; VNPay / MoMo / ZaloPay | MVP digital channels are VNPay, MoMo and ZaloPay; manual cash uses the CASH_COLLECT operator interface. |
| Map Service | Map service; Google Maps / Mapbox | Final provider selection and API credentials are still implementation decisions. |
| Notification Service | Notification service; email/SMS/push | SmartPark's internal Notification microservice is distinct from external delivery providers. |
| Recognition Service | ANPR/LPR service | Image-input LPR MVP in §6.2 conflicts with future status in §2.4 (C-14); internal/external ownership remains ARCH-DEF-03. |
| Device Gateway | Logical adapter boundary for camera/sensor/gate | Not an extra physical actor. Show internal adapter or external endpoint according to the approved context boundary, never both for the same component. |
| AI/LLM Service | AI/LLM service | External model integration; chatbot orchestration may be internal, pending ARCH-DEF-02. |
| Simulation engine | Simulation engine | Keep original Future/Deferred label; basic test mocks remain allowed by §2.11. Product simulation controls need C-15. |

<!-- INTEGRATION-END NAMES -->

### 2.5 High-Level Logical Architecture

### 2.5 High-Level Logical Architecture

The SmartParking system follows a **microservice architecture** in which business capabilities are divided into independently deployable services. Each microservice owns its application logic, domain model, and persistence boundary. Services communicate through well-defined synchronous APIs and asynchronous events where appropriate.

```text
┌───────────────────────────────────────────────────────────────┐
│                         Client Layer                          │
│                                                               │
│ ReactJS Web App / Future Mobile App / Admin Interface         │
│ Driver • Operator • Owner • Administrator                     │
└──────────────────────────────┬────────────────────────────────┘
                               │ HTTPS
                               ▼
┌───────────────────────────────────────────────────────────────┐
│                        API Gateway                            │
│                                                               │
│ Authentication / Authorization • Routing • Rate Limiting      │
│ Request Aggregation where required                            │
└──────────────────────────────┬────────────────────────────────┘
                               │
                    HTTPS / HTTP / gRPC
                               │
          ┌────────────────────┼────────────────────┐
          │                    │                    │
          ▼                    ▼                    ▼
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│   User Service  │  │ Parking Service │  │ Reservation     │
│                 │  │                 │  │ Service         │
│ Accounts        │  │ Lots / Zones    │  │ Reservations    │
│ Vehicles        │  │ Slots / Status  │  │ Allocation      │
│ Authentication  │  │ Operations      │  │ Conflicts      │
└────────┬────────┘  └────────┬────────┘  └────────┬────────┘
         │                    │                    │
         ▼                    ▼                    ▼
   PostgreSQL DB        PostgreSQL DB        PostgreSQL DB

          ┌────────────────────┼────────────────────┐
          │                    │                    │
          ▼                    ▼                    ▼
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│ Payment Service │  │ Notification    │  │ IoT Service     │
│                 │  │ Service         │  │                 │
│ Fee Calculation │  │ Email / SMS     │  │ Sensors         │
│ Payment         │  │ Push            │  │ Cameras / LPR   │
│ Refunds         │  │ Alerts          │  │ Device Status   │
└────────┬────────┘  └────────┬────────┘  └────────┬────────┘
         │                    │                    │
         ▼                    ▼                    ▼
   PostgreSQL DB        PostgreSQL DB        PostgreSQL DB


┌───────────────────────────────────────────────────────────────┐
│                    Asynchronous Event Layer                   │
│                              Kafka                            │
│                                                               │
│ Integration Events / Notifications / Async Processing         │
└───────────────────────────────────────────────────────────────┘
           ▲                 ▲                 ▲
           │                 │                 │
      Microservices     Microservices     Microservices


┌───────────────────────────────────────────────────────────────┐
│                    External Integrations                      │
│                                                               │
│ Payment Provider • Maps • AI Provider • SMS/Email             │
│ Camera/Sensor Systems • License Plate Recognition             │
│ Government Systems where authorized                           │
└───────────────────────────────────────────────────────────────┘

```
<!-- INTEGRATION-FENCE-CLOSE 1 -->

Each microservice may use a different internal implementation according to its complexity, but the SmartParking backend will follow a common project convention where applicable:

Microservice
├── API
├── Application
├── Domain
├── Infrastructure
└── Persistence

Where:

API exposes service endpoints and remains thin.
Application contains use cases, CQRS handlers, validation, and application contracts.
Domain contains domain entities and business rules.
Persistence contains EF Core, DbContext, repositories, migrations, and database-specific implementation.
Infrastructure contains external integrations such as Kafka, payment providers, notification providers, AI services, maps, and device integrations.

Each service owns its own data store. Other services shall not directly access another service's database or Persistence layer. This follows the data-sovereignty principle of microservices and prevents database-level coupling.

### 2.6 Architecture Style — WORKING DIRECTION

### 2.6 Architecture Style

The SmartParking backend follows a **microservice architecture with event-driven integration**.

Each microservice represents an independently deployable business capability and owns its domain logic, application logic, and persistence boundary. Microservices communicate through well-defined APIs for synchronous request-response operations and through Kafka-based integration events for asynchronous communication where appropriate.

The architecture aims to provide:

- Independent development and deployment of business services.
- Independent ownership of service data and database schemas.
- Clear service boundaries based on business capabilities.
- Synchronous communication for operations that require an immediate response.
- Asynchronous event-driven communication for notifications, state propagation, and background processing.
- Replaceable external-service integrations through defined interfaces and adapters.
- Containerized deployment using Docker.

The architecture does not require every operation to use Kafka. Direct service-to-service communication should be used when an immediate response is required, while asynchronous messaging should be preferred where loose coupling or background processing provides a meaningful benefit. This is consistent with established .NET microservice guidance, which uses both synchronous API communication and asynchronous integration events depending on the operation.

### 2.7 Domain-to-Integration Flow

```text
Physical observations
(Camera / Sensor / Gate / Simulation / Operator)
                    ↓
            Validation / reconciliation
                    ↓
             Backend authoritative state
                    ↓
       APIs / Events / Views / 3D / AI tools
```

The UI, 3D model, and AI assistant consume backend state; they do not create authoritative state.

### 2.8 Component Diagram




### 2.9 Technology Stack & Frontend Technical Direction

#### 2.9.1 Current Technology Baseline

| Layer | Technology | Status | Notes |
|---|---|---|---|
| Backend | **ASP.NET Core** | **BASELINE** | Core API, authentication, validation, business logic, policy enforcement. |
| Frontend | **ReactJS** | **BASELINE** | Unified web client with role-based interfaces. |
| Styling | **Tailwind CSS** | **current UI direction** | Utility-based styling. |
| Component library | **Material UI** | **current UI direction** | Reusable interface components. |
| Database | **PostgreSQL** | **BASELINE** | Transactional relational data and configuration. |
| Containerization | **Docker** | **BASELINE** | Consistent development/deployment environments. |
| Event infrastructure | **Kafka** | **planned technology / usage OPEN** | Use only where asynchronous/event-driven behavior is justified. |
| 3D rendering | **Three.js** | **BASELINE** | Interactive parking visualization for the MVP digital twin. |
| Map library | `@react-google-maps/api` or alternative | **OPEN** | Follow final provider/access/cost decision. |
| Client state management | Zustand or Redux Toolkit | **OPEN** | Final choice should follow actual shared-state needs. |
| AI/LLM | Not selected | **MVP / implementation OPEN** | Limited to approved MVP AI functions, including chatbot. |
| CV/ANPR | Not selected | **MVP / implementation OPEN** | MVP can use image/simulation input; exact model remains open. |
| Payment provider | VNPay / MoMo / ZaloPay | **BASELINE current MVP digital channels** | CASH_COLLECT is supported through the Operator payment interface; additional providers or card channels may be added later. |

#### 2.9.2 Frontend Architecture Direction

The current frontend direction is a unified React application with role-based routing/interfaces for Driver, Operator, Owner, and Admin.

Specific libraries and visual details remain implementation choices unless explicitly approved as requirements.

#### 2.9.3 Modular Backend Architecture

SmartParking is divided into business-oriented microservices. Each service is independently deployable and responsible for its own business capability and data.

Initial logical service boundaries include:

```text
User Service

- User accounts
- Authentication and authorization support
- Vehicle registration
- User profile and account management


Parking Service

- Parking facility management
- Parking zones
- Parking slots
- Capacity
- Real-time parking status
- Parking operational state


Reservation Service

- Reservation creation
- Reservation availability
- Reservation allocation
- Reservation expiration
- Reservation cancellation
- Reservation conflict handling


Payment Service

- Fee calculation
- Payment initiation
- Payment status
- Refund processing
- Payment-provider integration


Notification Service

- Email
- SMS
- Push notifications
- Reservation and payment notifications
- System alerts


IoT Service

- Sensor ingestion
- Camera integration
- License plate recognition integration
- Device status
- Parking state updates


Search / Recommendation Service

- Parking-lot search
- Location-based search
- Parking availability search
- Recommendation support

```
<!-- INTEGRATION-FENCE-CLOSE 2 -->

Additional services such as Audit, AI, or other specialized services may be introduced if their responsibilities justify an independent service boundary.

Each microservice shall own its internal implementation and persistence. A service shall not directly access another microservice's database, DbContext, repository, or internal domain model.

For example:

Reservation Service
        │
        │ API / application contract
        ▼
Parking Service
        │
        ▼
Parking Service Database

and not:

Reservation Service
        │
        └──── SELECT * FROM ParkingSlot
                       ❌

Similarly, services shall not directly modify another service's persistence records:

Payment Service
      │
      └────► ReservationDbContext
                     ❌

Instead, cross-service state changes shall be coordinated through service APIs, domain/application workflows, or integration events as appropriate.

Microservice Internal Structure

Where the service complexity justifies the full structure, each microservice follows:

Service
├── API
├── Application
├── Domain
├── Infrastructure
└── Persistence

The internal architecture may vary when a service has significantly simpler or different requirements. The important constraint is that the service remains cohesive, independently deployable, and responsible for its own data and business capability.

### 2.10 Event / Messaging Architecture

#### 2.10.1 Event-Driven Communication

SmartParking shall use asynchronous messaging when it provides a clear architectural or business benefit, including:

- Propagating state changes between independent services.
- Triggering asynchronous processing.
- Sending notifications.
- Processing IoT or operational events.
- Decoupling services that do not require an immediate response.

Synchronous HTTP/gRPC communication may be used when the requesting operation requires an immediate response or when the requested information is owned by another service.

The system shall avoid unnecessary synchronous chains between multiple services because excessive service-to-service dependencies can increase latency and reduce service autonomy. :contentReference[oaicite:5]{index=5}

#### 2.10.2 Kafka

Kafka is the planned message-broker technology for SmartParking's asynchronous integration layer.

Potential integration events include:

```text
UserRegistered
UserUpdated
VehicleRegistered

ParkingLotCreated
ParkingSlotStatusChanged
OccupancyChanged

ReservationCreated
ReservationConfirmed
ReservationExpired
ReservationCancelled
ReservationReallocated

PaymentInitiated
PaymentSucceeded
PaymentFailed
RefundRequested
RefundCompleted

VehicleEntered
VehicleExited
ViolationDetected

OperatorOverride
EmergencyActivated

NotificationRequested

```
<!-- INTEGRATION-FENCE-CLOSE 3 -->

The final event catalogue and topic structure shall be defined as individual event-driven use cases are implemented.

Kafka shall not be used for every internal operation. Each proposed event-driven interaction should justify:

Why asynchronous communication is required.
Which service owns the event.
Which services consume the event.
What happens if the consumer or broker is temporarily unavailable.
Whether the operation requires eventual consistency.
Whether synchronous communication would be simpler for the MVP.
2.10.3 Event Ownership and Contracts

Events shall be owned and published by the service responsible for the corresponding business capability.

Event contracts shall use explicit event names and defined schemas rather than arbitrary payloads.

Services shall not share domain models or directly reference another service's internal domain entities.

Where practical, integration events should contain only the data required by consumers and should remain independent from the publishing service's internal persistence model.

The architecture shall allow consumers to evolve independently and shall consider event versioning and backward compatibility as the event catalogue grows.

2.10.4 Reliability and Consistency

Cross-service operations shall not assume a single distributed database transaction.

Where an operation spans multiple services, the system shall allow for eventual consistency and explicitly define the expected state transitions and failure-handling behavior.

For critical workflows, reliability mechanisms such as transactional outbox, retry handling, idempotent consumers, or compensating actions may be introduced when required.

### 2.11 External Integration Strategy

SmartParking shall isolate external providers, hardware, and third-party services behind application-defined contracts and infrastructure adapters where practical.

Potential integration boundaries include:

```text
IPaymentProvider
IMapProvider
IAIProvider
IVehicleRecognitionProvider
INotificationProvider
ICameraAdapter
ISensorAdapter
IGateAdapter

```
<!-- INTEGRATION-FENCE-CLOSE 4 -->

The Application or Domain layer shall depend on abstractions rather than directly on provider-specific SDKs or APIs.

Provider-specific implementations shall be placed within the Infrastructure layer of the microservice responsible for the integration.

For example:

Payment Service
    │
    ▼
IPaymentProvider
    │
    ▼
Payment Infrastructure Adapter
    │
    ▼
Third-Party Payment Provider

and:

IoT Service
    │
    ▼
ICameraAdapter / ISensorAdapter
    │
    ▼
Camera / Sensor Infrastructure

External providers may be replaced without requiring changes to core domain logic, provided that the replacement satisfies the defined application contract.

Mocks, stubs, or simulators may be used when real external services or physical hardware are unavailable during development and testing.

The final external integration approach shall consider provider availability, authentication, failure handling, timeout/retry policies, cost, data protection, and operational requirements.

Where an external provider processes sensitive or personal data, the integration shall follow the applicable SmartParking privacy, security, and compliance requirements.

---

## 3\. Software Requirements Specification

<!-- INTEGRATION-START US -->

### 3.0 User Stories and Bidirectional Traceability

Existing US identifiers are retained. “Existing” identifies a previously documented user goal, not approval of every imported detail. Added baseline-coverage stories expose missing traceability rather than expanding scope. Monthly stories are defined only in Appendix F. Every FR table also links back to its User Story; there is no second copy of the functional catalog here.

| US ID | Actor | User goal | Status | FR coverage |
| --- | --- | --- | --- | --- |
| US-D01 | Driver | As Driver, I want to book parking ahead of arrival to obtain the entitlement shown in the confirmation. | Existing; C-03 | FR-VEH-04, FR-POL-06, FR-CAP-03, FR-CAP-04, FR-RES-01, FR-RES-02, FR-RES-03, FR-RES-04, FR-RES-05, FR-RES-06, FR-RES-07, FR-RES-11, FR-RES-12, FR-RES-13, FR-BAS-01, FR-BAS-02, FR-BAS-04 |
| US-D02 | Driver | As Driver, I want to pay parking charges and verify settlement of my parking transaction. | Existing; C-08/09/10/21 | FR-PAY-02, FR-PAY-03, FR-PAY-04, FR-PAY-05, FR-PAY-09, FR-PAY-10, FR-PAY-11, FR-BAS-05, FR-BAS-16 |
| US-D03 | Driver | As Driver, I want to view parking information and availability to choose a suitable facility. | Existing | FR-CAP-01, FR-CAP-02 |
| US-D04 | Driver | As Driver, I want to review my parking history. | Existing | FR-GATE-08 |
| US-D05 | Driver | As Driver, I want to review my reservation history and outcomes. | Existing | FR-RES-10 |
| US-D06 | Driver | As Driver, I want to review my payment and refund history. | Existing | FR-PAY-07 |
| US-D07 | Driver | As Driver, I want to submit and follow an appeal about a parking issue. | Existing; detail proposed C-12 | FR-INC-05, FR-INC-07 |
| US-O01 | Operator | As Operator, I want to check vehicles in and record authorized arrival/entry. | Existing; C-01/18 | FR-VEH-03, FR-VEH-04, FR-CAP-03, FR-CAP-04, FR-CAP-05, FR-CAP-06, FR-RES-07, FR-RES-11, FR-RES-12, FR-RES-13, FR-GATE-01, FR-GATE-02, FR-GATE-03, FR-GATE-09 |
| US-O02 | Operator | As Operator, I want to check vehicles out and record actual departure and settlement. | Existing; C-21 | FR-CAP-05, FR-CAP-06, FR-GATE-05, FR-GATE-06, FR-GATE-07, FR-GATE-09, FR-PAY-02, FR-PAY-03, FR-PAY-04, FR-PAY-05, FR-PAY-11, FR-BAS-16 |
| US-O03 | Operator | As Operator, I want to monitor assigned parking lots and current operations. | Existing | FR-LOT-07, FR-CAP-02, FR-CAP-06, FR-GATE-04, FR-MAP-02, FR-MAP-04 |
| US-O04 | Operator | As Operator, I want to handle emergencies and record operational actions. | Existing; detail pending C-13 | FR-OPS-01, FR-OPS-02 |
| US-O05 | Operator | As Operator, I want to resolve parking incidents and reservation conflicts. | Existing; detail pending C-07/12 | FR-LOT-04, FR-CAP-07, FR-RES-09, FR-GATE-05, FR-GATE-06, FR-GATE-07, FR-INC-01, FR-INC-03, FR-INC-04, FR-INC-06, FR-OPS-03, FR-OPS-06, FR-BAS-02, FR-BAS-04 |
| US-OW01 | Owner | As Owner, I want to manage my facility profile and layout. | Existing | FR-LOT-01, FR-LOT-02, FR-LOT-03, FR-LOT-04 |
| US-OW02 | Owner | As Owner, I want to configure lot policies and prices within authorized scope. | Existing; C-04/08 | FR-POL-01, FR-POL-03, FR-POL-04, FR-POL-05, FR-POL-06, FR-POL-07, FR-POL-08, FR-CAP-07, FR-PAY-01, FR-PAY-08, FR-PAY-09, FR-PAY-10, FR-PAY-11, FR-BAS-04, FR-BAS-05 |
| US-OW03 | Owner | As Owner, I want to manage Operators and their lot-scoped access. | Existing | FR-AUTH-04 |
| US-OW04 | Owner | As Owner, I want to review transactions and facility reports to monitor operation and revenue. | Existing; report coverage clarified | FR-PAY-06, FR-RPT-02, FR-BAS-07 |
| US-OW05 | Owner | As Owner, I want to view device-related reports for my facilities. | Future/deferred while IoT scope is future; device management belongs to Operator. | FR-LOT-07 |
| US-A01 | Admin | As Admin, I want to manage accounts, roles and authorized platform access. | Existing | FR-AUTH-02, FR-AUTH-05, FR-AUTH-06, FR-BAS-08 |
| US-A02 | Admin | As Admin, I want to manage platform settings and integration configuration. | Existing; device boundary C-02 | FR-LOT-06, FR-POL-02, FR-POL-03, FR-POL-04, FR-POL-05, FR-POL-07, FR-POL-08 |
| US-A03 | Admin | As Admin, I want to inspect protected audit records of important actions. | Existing | FR-RPT-03, FR-RPT-04 |
| US-D08 | Driver | As Driver, I want to register, authenticate and manage my own profile. | Imported refinement; C-11/14 | FR-AUTH-01, FR-AUTH-02, FR-AUTH-03, FR-BAS-08 |
| US-D09 | Driver | As Driver, I want to manage my vehicles and resolve disputed plate binding. | Imported refinement; C-11 | FR-VEH-01, FR-VEH-02, FR-VEH-03 |
| US-D10 | Driver | As Driver, I want to understand cancellation eligibility and track cancellation/refund outcomes. | Imported refinement | FR-RES-08, FR-RES-09, FR-RES-12, FR-PAY-06 |
| US-D11 | Driver | As Driver, I want to receive supported notifications and reminders about my services. | Imported refinement; baseline channels/categories retained | FR-PAY-08, FR-RPT-01, FR-BAS-06 |
| US-D12 | Driver | As Driver, I want to use conversational help to search, inspect my data and prepare reviewed reservations. | Imported refinement; broader write scope C-16 | FR-AI-01, FR-AI-02, FR-AI-03, FR-AI-04, FR-AI-05, FR-AI-06, FR-AI-07, FR-AI-08, FR-AI-09, FR-AI-10, FR-AI-11, FR-BAS-10, FR-BAS-11 |
| US-O06 | Operator | As Operator, I want to use controlled offline processing and reconcile after recovery. | F; outside MVP | FR-OPS-04, FR-OPS-05 |
| US-O08 | Operator | As Operator, I want to review LPR results to verify vehicles; future visual detection may support incidents. | MVP LPR C-14; per-slot AI F | FR-GATE-01, FR-INC-02, FR-BAS-09, FR-BAS-13 |
| US-OW07 | Owner | As Owner, I want to control appeal delegation and approve financial refunds. | Imported refinement; C-12 | FR-PAY-06, FR-INC-05 |
| US-A04 | Admin | As Admin, I want to review disputed vehicle binding requests. | Imported refinement; C-11 | FR-VEH-02 |
| US-A05 | Admin | As Admin, I want to apply approved data retention and deletion policies. | Imported refinement; C-20 | FR-RPT-05 |
| US-A06 | Admin | As Admin, I want to run isolated demo scenarios without touching operational resources. | Proposed product feature; C-15 | FR-SIM-01 |
| US-A07 | Admin | As Admin, I want to configure approved recognition thresholds and audit changes. | Imported refinement; C-14 | FR-POL-09 |
| US-D16 | Driver | As Driver, I want to use maps and recorded vehicle location to find the facility and my vehicle. | Imported refinement | FR-MAP-01, FR-MAP-02, FR-MAP-03, FR-MAP-04 |
| US-D17 | Driver | As Driver, I want to ask for reallocation or its reason according to the reservation rules. | Added traceability to existing §3.4.6; C-18 details open | FR-BAS-03 |
| US-O09 | Operator | As Operator, I want to receive a human-support escalation with authorized conversation context. | Proposed handler role for existing §6.7 handoff; AI-DEP-07 | FR-BAS-10 |
| US-D18 | Driver | As Driver, I want understandable parking suggestions while retaining access to all options. | Future personalized/model-based recommendation scope; MVP ranking is deterministic backend logic. | FR-BAS-12 |
| US-OW08 | Owner | As Owner, I want to use forecasts to plan capacity and pricing decisions. | F; baseline §6.5 | FR-BAS-14 |
| US-O10 | Operator | As Operator, I want to review automated anomaly alerts and investigate affected operations. | F; baseline §6.6 | FR-BAS-15 |

<!-- INTEGRATION-END US -->

### 3.1 User Management

#### 3.1.1 User Registration

The system shall allow users to register using a phone number or email address. Upon registration, the system shall send a one-time password (OTP) for verification. The OTP shall be valid for 5 minutes. After three consecutive failed OTP attempts, the account shall be temporarily locked for 15 minutes.

Owner registration shall require company name, email address and phone number. The resulting Owner account shall require Admin approval before it becomes active for Owner operations. Owner-created Operator accounts may use any chosen email address; SmartPark shall not enforce a corporate email-domain restriction.

#### 3.1.2 Identity Verification

Sensitive identity information shall be encrypted using AES-256.

#### 3.1.3 Vehicle Registration

Users shall be able to register multiple vehicles per account. Each vehicle record shall include license plate, vehicle type, and vehicle image. The system shall validate license plate format according to Vietnamese regulations.

#### 3.1.4 Authentication

The system shall support OTP-based authentication via SMS or email. Multi-factor authentication (MFA) using TOTP shall be available as an optional security feature. Session tokens shall expire after 24 hours, with refresh tokens valid for 7 days.

<!-- INTEGRATION-START FR-USER -->

#### 3.1.5 User and Vehicle Requirement Index

Status definitions are in the Baseline Revision Notice. Detailed constraints are defined once in §5.2; X items are recorded in Appendix E.

| FR ID | Status | Required behavior / canonical SRS location | Actor | User Story |
| --- | --- | --- | --- | --- |
| FR-AUTH-01 | C | Registration and OTP verification follow §3.1.1. Security-critical authentication values remain constrained; business-policy variables are configurable only where approved by C-14 and §5.2.3. | Driver, Notification Service | US-D08 |
| FR-AUTH-02 | R/A | Authentication follows §3.1.4. Add logout and rejection of operations by locked or unauthorized accounts; BR-AUTH-01. | Driver, Operator, Owner, Admin | US-D08, US-A01 |
| FR-AUTH-03 | A | Allow Driver to view/update permitted fields of their own profile; deny cross-account access (BR-AUTH-03). | Driver | US-D08 |
| FR-AUTH-04 | R | Operator provisioning, lot assignment and permission management: §3.7.5; BR-AUTH-01. | Owner | US-OW03 |
| FR-AUTH-05 | C | Admin account management: §3.7.4. Admin approves Owner registration after required company name, email and phone information is provided; role changes remain audited. | Admin | US-A01 |
| FR-AUTH-06 | A | Enforce role, permission and resource scope for every operation, including revoked permissions; use §2.3.1 and BR-AUTH-03. | Driver, Operator, Owner, Admin | US-A01 |
| FR-VEH-01 | R/A | Vehicle registration follows §3.1.3; add own-vehicle listing and editing with plate normalization (BR-VEH-01/02). | Driver | US-D09 |
| FR-VEH-02 | X | Proposed duplicate-plate claim/review and approved binding transfer without rewriting history: BR-VEH-02; evidence, reviewer and CCCD/GPLX scope require C-11. | Driver, Admin | US-D09, US-A04 |
| FR-VEH-03 | A | Temporary QR identifiers for accepted non-plated vehicles remain a proposal. Motorcycle is a supported vehicle category in v0.8.5; EV charging is not a system function. | Operator | US-D09, US-O01 |
| FR-VEH-04 | R/A | Validate vehicle compatibility under §3.4.3; proposed configurable dimensions and restricted-bay checks are BR-VEH-03. | Driver, Operator | US-D01, US-O01 |
| FR-BAS-08 | R | MFA and token behavior: §3.1.4; security detail remains §4.3. This index does not resolve authentication inconsistencies in C-11. | Driver, Operator, Owner, Admin | US-D08, US-A01 |

<!-- INTEGRATION-END FR-USER -->

### 3.2 Parking Lot Management

#### 3.2.1 Parking Lot Information

Owners shall be able to create and manage parking lot profiles including name, address, GPS coordinates, capacity, hourly rates, reservation policies, and overnight rates. 

#### 3.2.2 Spot State Management

SmartPark shall maintain physical parking state separately from reservation/protection state. These states represent different dimensions and shall not be treated as one mutually exclusive status field.

**Physical State**

Each parking spot shall support at least the following physical states:

- **AVAILABLE**: Spot is physically available for parking.
- **OCCUPIED**: Spot is physically occupied by a vehicle.
- **UNKNOWN**: The current physical condition cannot be determined reliably.
- **MAINTENANCE**: Spot is under maintenance and unavailable for normal parking.
- **UNAVAILABLE**: Spot is closed or otherwise out of service.

**Reservation / Protection State**

The system shall support the following reservation/protection states where applicable:

- **RESERVED**: The spot is associated with an already-paid reservation for a defined time period.
- **PROTECTED**: The spot or capacity is intentionally held out of ordinary walk-in use according to the applicable reservation or protection policy.
- **BACKUP**: The spot or capacity is held as backup inventory for future reservation fulfillment.

A spot with no applicable reservation/protection record is interpreted as not reserved; an explicit `NOT_RESERVED` database state is not required.

Physical state and reservation/protection state may coexist. For example, a spot may be physically **OCCUPIED** while remaining **PROTECTED** for a future reservation-related purpose.

For reservation processing, the following state transitions are explicitly supported:

- A spot with **AVAILABLE + RESERVED** may transition to **OCCUPIED + RESERVED** when the reserving driver physically occupies the reserved slot.
- A spot associated with a **RESERVED** reservation may transition from an available physical condition to **MAINTENANCE** while the reservation remains active; the reservation shall then enter the applicable allocation/reallocation or conflict-handling process.
- A spot associated with a **RESERVED** reservation may transition from an available physical condition to **UNAVAILABLE** while the reservation remains active; the reservation shall then enter the applicable allocation/reallocation or conflict-handling process.

Additional physical-state transitions will be represented in the later state-diagram/domain-model work.

Spot physical state shall be updated through the selected observation mechanism. For MVP, automated IoT updates may be simulated and deterministic test fixtures may be used. Manual status updates by parking attendants shall be supported as a fallback. A manual override has authority only when initiated by an authorized actor under an approved operational condition; the override and reason shall be audited. No blanket manual-over-sensor precedence applies outside an approved override condition.

#### 3.2.3 Parking Lot Diagrams and 3D Digital Twin

The system shall display 2D and 3D diagrams of parking lots showing individual spot statuses. The 3D representation is part of the MVP visualization scope and shall consume authoritative backend state. It shall not become the authoritative source of occupancy, reservation, allocation, or payment state.

The visualization shall represent the supported physical and reservation/protection states and shall update when the authoritative backend state changes.

#### 3.2.4 Policy Scope and Configuration Hierarchy

SmartPark shall support configurable policies at different scopes. Policy configuration shall follow this hierarchy when determining the applicable settings for an operation:

```text
System-wide Admin Policy
        ↓
Owner / Parking-Lot Policy
        ↓
Zone-specific Policy
        ↓
Booking Context
```

System-wide system policies, such as reservation/payment hold behavior and other platform settings, shall be configurable by Admin. Parking-lot and business-operation policies shall be configurable by the Owner within the applicable system-wide policy scope. Zone-specific and booking-context settings may provide more specific policy values where supported.

Admin defines the default value and permitted bounds for every policy or variable that an Owner is allowed to configure. Owner configuration may override the Admin default within the permitted scope and bounds. For always-active policies, the Admin value applies when the Owner has no override. For optional policies, the Owner's explicit enable/disable choice determines whether the policy is active; an Admin recommendation does not by itself activate an optional Owner policy.

<!-- INTEGRATION-START FR-LOT-POL -->

#### 3.2.5 Facility, Policy and Capacity Requirements

Status definitions are in the Baseline Revision Notice. Detailed constraints are defined once in §5.2; X items are recorded in Appendix E.

| FR ID | Status | Required behavior / canonical SRS location | Actor | User Story |
| --- | --- | --- | --- | --- |
| FR-LOT-01 | R | Owner lot profile creation/update/deactivation and open/closed status: §3.2.1 and §3.7.2. | Owner | US-OW01 |
| FR-LOT-02 | A | Maintain configurable floors, zones, slots and access paths supporting §3.2.3; outdoor lots need not contain floors. | Owner | US-OW01 |
| FR-LOT-03 | A | Maintain slot/zone identifiers, vehicle-category capacities and map associations; motorcycle is a supported vehicle category. Detailed category/layout rules remain subject to the retained refinement. | Owner | US-OW01 |
| FR-LOT-04 | R/A | Maintain service/maintenance states under §3.2.2; show affected reservations/sessions and record reasons; permission and observation precedence remain C-07. | Owner, Operator | US-OW01, US-O05 |
| FR-LOT-05 | C | Manage, link, enable and disable lot devices within the Operator's assigned parking-lot scope. Owner grants the Operator access through account provisioning. | Operator | US-O03, US-O05 |
| FR-LOT-06 | A/X | Platform provider adapters/settings remain separate from lot-device management; final technical ownership remains subject to ARCH-DEF-03. | Admin | US-A02 |
| FR-LOT-07 | A | Show scoped device connectivity, last observation and errors; view permission alone does not permit device mutation. | Owner, Operator, Device Gateway | US-OW05, US-O03 |
| FR-POL-01 | C | Owner configures applicable parking-lot/business policies within Admin-defined defaults and permitted bounds. | Owner | US-OW02 |
| FR-POL-02 | R | Admin system-policy configuration, including payment hold: §3.2.4, §3.4.3, §3.7.3. | Admin | US-A02 |
| FR-POL-03 | A | Expose a named, typed, scoped policy catalogue with units and effective values. The detailed catalogue/validation structure remains a refinement; the confirmed hierarchy is defined in §3.2.4 and C-04. | Owner, Admin | US-OW02, US-A02 |
| FR-POL-04 | A | Validate required values, supported ranges and exhaustive/non-overlapping policy bands before activation; reject invalid configurations with reasons (BR-POL-02). | Owner, Admin | US-OW02, US-A02 |
| FR-POL-05 | C | Record policy revisions and the policy/price version applied to an accepted booking or transaction; later policy revisions apply to future applicable transactions. | Owner, Admin | US-OW02, US-A02 |
| FR-POL-06 | A | Present backend-resolved terms to Driver; changed terms require renewed confirmation before a mutation (BR-POL-04). | Driver | US-D01, US-OW02 |
| FR-POL-07 | C | Resolve applicable policy values using the hierarchy in §3.2.4: Admin defaults/bounds → Owner/Parking Lot override → more specific supported scope. Detailed data representation and validation remain refinement under TESTER-DEF-07/08. | Admin, Owner | US-OW02, US-A02 |
| FR-POL-08 | A | Support draft policies, scheduled activation and revision history within assigned scope; invalid drafts cannot authorize transactions (BR-POL-02/03). | Admin, Owner | US-OW02, US-A02 |
| FR-POL-09 | X | Proposed Admin-configurable LPR threshold and audit; C-14 must reconcile fixed baseline values and threshold equality. Multi-frame violation thresholds remain future. | Admin, Recognition Service | US-A07 |
| FR-CAP-02 | C | Display capacity dimensions separately by supported vehicle category and apply the availability accounting in §3.4.5; pending payment consumes held capacity during its active hold. | Driver, Operator, Owner | US-D03, US-O03 |
| FR-CAP-03 | C | Enforce the canonical capacity/availability invariants in §3.4.3–6 and BR-CAP-02/04. Specific Slot, Zone and Capacity remain the reservation modes, using the allocation and protection behavior defined by the baseline. | Driver, Operator | US-D01, US-O01 |
| FR-CAP-04 | A | Ensure concurrent claims cannot consume the same incompatible capacity; retries cannot create duplicate reservations (BR-CAP-03). Lock technology remains open. | Driver, Operator | US-D01, US-O01 |
| FR-CAP-05 | A | Link fulfilled reservation capacity to actual occupancy without double deduction or early release (BR-CAP-05); preserve separate physical/protection states. | Driver, Operator | US-O01, US-O02 |
| FR-CAP-06 | R/A | Reject ordinary walk-ins when eligible capacity is unavailable; valid reservations still require safe physical accommodation under §3.4.6/7 (BR-CAP-02). | Driver, Operator | US-O01, US-O02, US-O03 |
| FR-CAP-07 | C | Backup capacity follows §3.4.5/9. Owner may configure backup capacity. Operator-marked backup slots remain backup; Protected capacity is dynamically calculated from applicable future reservations and the Reservation Protection Window. This protection is separate from configured backup capacity and does not force an already-occupied vehicle to move. | Owner, Operator | US-OW02, US-O05 |

<!-- INTEGRATION-END FR-LOT-POL -->

### 3.3 Parking Search & Recommendation

#### 3.3.1 Location Detection

The system shall automatically detect the user's location using GPS. If GPS is unavailable or inaccurate, the system shall fall back to network-based positioning or allow manual address entry.

#### 3.3.2 Search Algorithm

The system shall search for parking lots within a default radius of 5km. Distance calculation shall use the Haversine formula for geographic coordinates. Results shall be sorted by distance, showing available spots, pricing, and estimated travel time.

#### 3.3.3 Search Results

Each search result shall display:

- Parking lot name and address  
- Distance from user location  
- Number of available spots  
- Hourly rate and overnight rate  
- Estimated travel time

If no parking lots are found within 5km, the system shall notify the user and offer to expand the search radius. If all nearby parking lots are full, the system shall display a "Full" status and suggest the next available option.

<!-- INTEGRATION-START FR-SEARCH -->

#### 3.3.4 Search and Map Requirement Index

Status definitions are in the Baseline Revision Notice. Detailed constraints are defined once in §5.2; X items are recorded in Appendix E.

| FR ID | Status | Required behavior / canonical SRS location | Actor | User Story |
| --- | --- | --- | --- | --- |
| FR-CAP-01 | R/A | Search and result fields follow §3.3.1–3; add vehicle/time-window filters backed by authoritative availability. | Driver | US-D03 |
| FR-MAP-01 | R/A | Map/directions use §3.3 and §2.4 Map Service; allow permission-based location or manual location input. | Driver, Map Service | US-D16 |

<!-- INTEGRATION-END FR-SEARCH -->

### 3.4 Reservation System

#### 3.4.1 Reservation Modes and Specific-Slot Semantics

SmartPark shall support the following reservation modes where allowed by the applicable owner/parking-lot policy:

1. **Specific Slot Reservation**: The driver may select a preferred physical parking slot.
2. **Zone Reservation**: The driver may reserve parking capacity associated with a requested zone.
3. **Capacity Reservation**: The driver may reserve supported parking capacity without selecting a specific zone or physical slot.

A specific-slot reservation represents a driver request for a preferred physical parking slot. The system shall reserve and protect the requested slot from conflicting reservations and shall make best-effort allocation in accordance with owner-configured policies. A specific-slot reservation does not guarantee that the exact requested physical slot will ultimately be available at arrival.

For a zone reservation, the system shall prioritize allocating an applicable slot within the requested zone. Due to unexpected operational circumstances, a driver may be allocated a slot in a different applicable zone according to the configured policy.

For a capacity reservation, the system shall allocate any applicable capacity according to the configured reservation and allocation policies.

The reservation policy determines which modes are available for a parking facility, zone, category, or booking context. The architecture shall support all three modes without treating any single mode as the only permanent reservation model.

#### 3.4.2 Reservation Lifecycle

The reservation entity shall follow the following conceptual lifecycle:

```text
PENDING_PAYMENT
      ↓
CONFIRMED
      ↓
ALLOCATED
      ↓
PARKING
      ↓
COMPLETED
```

Exceptional paths include:

```text
PENDING_PAYMENT → EXPIRED
CONFIRMED → CANCELLED / NO_SHOW
CONFIRMED → UNFULFILLABLE
```

The lifecycle represents the reservation's business state and is separate from the parking spot's physical state and reservation/protection state.

- **PENDING_PAYMENT**: Reservation intent has been created and the configured payment hold is active. The hold consumes/protects the relevant capacity during the hold period so incompatible competing claims cannot take the same capacity. It counts toward availability during the active hold, but it is not yet the **RESERVED** business state.
- **CONFIRMED**: Payment has been completed and the reservation is valid according to the applicable reservation and payment policies.
- **ALLOCATED**: SmartPark has assigned the reservation an applicable parking slot or capacity according to the allocation process.
- **PARKING**: The reserved driver has entered the parking process and is using applicable parking capacity.
- **COMPLETED**: The reservation-related parking process has been completed.
- **EXPIRED**: The reservation did not complete payment within the applicable payment-hold period; this state is used only for unpaid reservation-hold expiry.
- **NO_SHOW**: When the Owner has enabled a late-tolerance policy and no authoritative arrival occurs by reservation start plus that tolerance, the reservation loses its entitlement similarly to cancellation and protected capacity is released according to policy.
- **CANCELLED**: The driver or applicable system/operator process has cancelled the confirmed reservation according to the applicable policy.
- **UNFULFILLABLE**: SmartPark cannot fulfill the confirmed reservation under the applicable allocation, conflict, and capacity policies.

#### 3.4.3 Reservation Creation and Payment Hold

Account holders shall be able to create a reservation for a selected time window. A person does not need an account to park as a walk-in. Walk-in parking is governed by physical authorization and current lot conditions. The system shall validate a reservation against authoritative reservation capacity, zone/slot availability, vehicle compatibility, and the applicable reservation policy.

After the user confirms reservation intent, the system shall create the reservation in `PENDING_PAYMENT` before payment is completed. The relevant capacity shall be temporarily held during checkout and shall count against availability during the payment-hold period so incompatible competing claims cannot consume it. A PENDING_PAYMENT hold is not yet the RESERVED state.

The payment hold duration shall follow the policy hierarchy: Admin defines the default and permitted bounds, while Owner may override it at parking-lot scope within those bounds. The current Admin default is **5 minutes**.

The payment hold is separate from the later reservation-protection window. The payment hold exists to prevent incompatible competing claims during checkout; the protection window exists to protect an already-confirmed future reservation from avoidable operational conflicts.

A PENDING_PAYMENT reservation hold does not make the spot RESERVED; the RESERVED state applies only after the reservation has been paid. When the hold expires without payment, the pending capacity is released and the reservation enters EXPIRED.

#### 3.4.4 Reservation Protection Window

For reservations that are made before the parking time, SmartPark shall support a configurable **Reservation Protection Window** before the reservation start time.

Example:

```text
Reservation: 09:00–12:00
Protection Window: 4 hours

Protection begins: 05:00
```

When the protection window begins, the system/operator shall take the configured actions needed to protect the reservation, including preventing new non-reservation/walk-in parking from consuming the relevant protected capacity where the applicable enforcement mechanism supports it.

The protection strategy may apply to:

- The originally requested parking spot.
- A compatible alternative spot in the same zone.
- Reserved capacity within the relevant zone/category.
- Configured backup capacity.

The exact protection behavior remains subject to the applicable owner-configured policy. Protection reduces avoidable conflicts but does not create an absolute guarantee that the originally requested physical slot will remain available at arrival.

#### 3.4.5 Continuous Capacity and Availability Tracking

The system shall continuously maintain the current reservation, allocation, protection, and capacity picture throughout the reservation lifecycle, including:

- Available parking capacity.
- Occupied capacity.
- Reserved capacity.
- Protected/backup capacity.
- Zone availability.
- Individual slot availability where applicable.
- Confirmed reservations and their requested periods.
- Current allocations and reallocated reservations.
- Active backup-capacity requirements.

Backup capacity represents capacity reserved for future reservation fulfillment and shall be excluded from ordinary walk-in availability. Operator-designated backup slots remain backup until released by policy. The dynamic protection backup pool is activated by the reservation Protection Window and protects capacity for affected reservations; protection does not force an already-occupied driver to move.

For current availability accounting, SmartPark shall use:

```text
Available Capacity = Total Capacity − Occupied − Protected − Pending Payment − Backup

Example: with Total Capacity = 100, Occupied = 60, Protected = 20, Pending Payment = 5, and Backup = 10, Available Capacity = **5**.
```

An active PENDING_PAYMENT hold therefore reduces availability even though it has not yet reached the RESERVED business state. When the hold expires or is cancelled before confirmation, its temporary capacity claim is released. When payment succeeds, the reservation proceeds to the confirmed flow.

For occupancy projection, a currently OCCUPIED slot is treated as occupied for the configurable **Occupancy Validity Timespan** from the current time unless the observed occupancy changes earlier. The current default is **4 hours**. This projection is used to reduce reservation overload caused by uncertain departure times; it does not create a promise that the vehicle will actually remain parked for the whole timespan.

The purpose is to identify possible conflicts early and reduce the probability of a reservation becoming impossible to fulfill at arrival time.

#### 3.4.6 Allocation Lead Time, Allocation Priority, and Reallocation

SmartPark shall support a configurable **Allocation Lead Time**. The **Allocation Time** is calculated as the reservation start time minus the configured Allocation Lead Time.

Example:

```text
Driver A reservation: 09:30–11:00
Driver B reservation: 10:00–12:00
Allocation Lead Time: 30 minutes
```

For Driver A:

```text
Reservation start: 09:30
Allocation Time: 09:00
```

For Driver B:

```text
Reservation start: 10:00
Allocation Time: 09:30
```

When a reservation reaches its Allocation Time, SmartPark shall begin allocation processing for that reservation.

The normal allocation order shall be:

1. **Earlier reservation start time first.**
2. **When reservation start times are equal, earlier reservation confirmation time first.**

Therefore, if Driver A reserves 09:30–11:00 and Driver B reserves 10:00–12:00, Driver A shall be allocated first, followed by Driver B, subject to applicable capacity and allocation policies.

If Driver A and Driver B have the same reservation start time, the driver whose reservation was confirmed earlier shall be allocated first in cases where available capacity is insufficient for both reservations.

The **Allocation Lead Time** is configurable by the Owner/parking-lot policy.

For a **Specific Slot Reservation**, the requested slot remains the preferred allocation. The system shall attempt to allocate that requested slot first.

An allocation should remain assigned to the driver once made and should be treated as the driver's intended parking allocation. However, an allocated slot may be changed when an unforeseen operational circumstance makes the allocated slot unavailable.

For example:

```text
Driver A initially selects A1.
At Allocation Time, Driver A is allocated A3.

Later, an unexpected emergency vehicle requires A3.
Driver A is therefore reallocated from A3 to A5.
```

Reallocation shall preserve the driver's reservation entitlement. The reallocated slot does not need to be the originally selected slot when unforeseen circumstances make the original allocation unavailable.

A driver may request another reallocation, up to a maximum of **2 driver-submitted reallocation requests** for the reservation. A reallocation request does not guarantee that a reallocation will be permitted; the system/operator shall determine whether the request can be fulfilled under the applicable capacity, reservation, and operational policies.

A driver may also enquire about the reason for an allocation or reallocation, such as an original reserved slot becoming unexpectedly occupied or unavailable. The system/operator shall provide the reason available from the operational record where applicable.

Once a driver has physically occupied a slot, SmartPark shall not automatically reallocate that driver's occupied slot as part of normal reservation conflict processing.

The intended happy path is that SmartPark can fully allocate all valid conflicted reservations.

If a reservation reaches its Allocation Time without an allocated applicable slot, SmartPark shall notify the driver that a slot may not be guaranteed upon arrival.

The reservation shall remain a reservation and shall not automatically become a normal walk-in/guest booking solely because no slot has yet been allocated. The driver may still arrive at the parking lot and may be admitted if an applicable slot or capacity becomes available.

A driver in this situation shall not be treated as an ordinary guest solely because the reservation has not yet been allocated. In particular, the driver shall not be blocked merely because ordinary guest/non-reservation capacity is being restricted to protect active reservations.

If the reservation ultimately cannot be fulfilled, the applicable owner-configured refund policy shall be applied. The configured policy may provide a full refund for this scenario.

If the driver arrives after receiving an allocation and uses the allocated slot, or another applicable empty slot accepted through the operational process, the payment already made through the reservation process remains applicable to that parking session. The driver shall not be charged again solely because the physical slot differs from the originally requested or previously allocated slot.

Refund treatment for an accepted reallocation shall follow the applicable owner-configured refund policy. **The system default is no refund solely because an accepted reallocation occurred.**

When a previously allocated slot or capacity becomes unexpectedly unavailable before the affected driver arrives, the affected reservations shall be resolved according to the physical arrival rule defined in the reservation conflict section.


#### 3.4.7 Reservation Conflict Queue

#### 3.4.7 Reservation Conflict Queue

SmartPark shall support a queue for cases where available and backup capacity is insufficient to fulfill all otherwise-valid reservations for the relevant time period.

The **normal allocation priority** shall be explicitly ordered as follows:

1. **Earlier reservation start time first.**
2. **Earlier reservation confirmation time when reservation start times are equal.**

The Allocation Lead Time determines when allocation processing begins for each reservation; it does not change the allocation priority order.

Example:

```text
Driver A: 09:30–11:00
Driver B: 10:00–12:00

Driver A reaches Allocation Time first.
Driver A is allocated first.
Driver B is allocated afterward.
```

When the reservation start times are equal:

```text
Driver A: 10:00–12:00
Driver B: 10:00–12:00

Driver A confirmed earlier.
Driver A is allocated first.
Driver B is allocated afterward.
```

The normal allocation priority applies when resolving insufficient capacity before physical arrival.

For reservation conflict processing, **physical arrival** means that the driver has reached the parking lot entrance or gate but has not yet physically occupied a parking slot. A driver may be physically arrived even when no applicable slot is available and the driver is therefore prevented from entering the parking area.

When a previously allocated slot or capacity becomes unexpectedly unavailable after allocation, the **physical arrival priority rule** shall apply to the affected reservations:

* The affected driver who arrives first shall have priority to use the remaining applicable capacity.
* If the first arriving driver can be accommodated by reallocating them to another applicable slot, the operator/system may perform that reallocation.
* A later-arriving affected driver may be turned away when no applicable capacity remains.
* The applicable owner-configured refund policy shall then be applied to the affected reservation. The policy may provide a full refund for a driver who is turned away because previously allocated capacity became unavailable.

The physical arrival rule is an exception for unexpected loss of already allocated capacity. It does not replace the normal reservation allocation priority of reservation start time followed by reservation confirmation time.

The queue does not replace reservation/capacity protection or continuous capacity tracking.

#### 3.4.8 Reservation Conflict and Fulfillment Examples

**Example 0 — Normal Allocation Order:**

```text
Driver A → reserves 09:30–11:00
Driver B → reserves 10:00–12:00

Allocation Lead Time is configurable by the Owner.
Assume Allocation Lead Time = 30 minutes.

Driver A Allocation Time = 09:00
Driver B Allocation Time = 09:30

Driver A is allocated first because Driver A's reservation
starts earlier.

Driver B is allocated after Driver A.
```

If the reservation start times are equal:

```text
Driver A → reserves 10:00–12:00
Driver B → reserves 10:00–12:00

Driver A confirmed the reservation before Driver B.

Driver A is allocated first.
Driver B is allocated second.
```

The happy path is that all conflicted reservations can be fully allocated.

**Example A — Capacity conflict before arrival:**

```text
Driver A → reserves A1
Driver B → reserves A2
Driver C → reserves A3

A guest vehicle remains overnight or overstays.
The configured reservation-protection and backup-capacity
policies are applied, but remaining capacity is still insufficient.

A and B are fulfilled according to allocation priority.
C remains the affected lower-priority reservation.

Result: C cannot be fulfilled and the applicable owner-configured
refund policy is applied. The policy may provide a full refund.
```

**Example B — Allocated Slot Becomes Unavailable:**

```text
Driver A is allocated A3.
Driver B is allocated A4.

An emergency vehicle requires A3 before Driver A arrives.
Driver A reaches the parking lot entrance/gate before Driver B.

No other applicable slot is available.

The operator reallocates Driver A from A3 to A4.
Driver A receives priority because Driver A arrived first.

Driver B reaches the parking lot entrance/gate later and no applicable capacity remains.

Result:
Driver A is admitted using A4.
Driver B is turned away at the gate.
The applicable owner-configured refund policy is applied
to Driver B. The policy may provide a full refund.
```

This example demonstrates that physical arrival order is used when previously allocated capacity is unexpectedly lost.

#### 3.4.9 Reservation Constraints and Configurable Parameters

- Minimum reservation duration: N minutes.
- Maximum reservation duration: N hours.
- Payment hold duration: Admin-defined default and permitted bounds; Owner may override at parking-lot scope within those bounds. Current Admin default is 5 minutes.
- Reservation Protection Window: N hours before the reservation start time, configurable by Owner; Admin provides the default and permitted bounds where this is an Owner-configurable variable.
- Allocation Lead Time: N minutes before the reservation start time, configurable by Owner; Admin provides the default and permitted bounds where this is an Owner-configurable variable.
- Backup capacity: configurable by Owner within Admin-defined defaults/bounds; Operator-designated backup slots remain backup. Backup capacity is separate from dynamically calculated protected capacity.
- Conflict handling and refund behavior: governed by the applicable policy hierarchy; detailed refund-policy structure remains subject to retained proposal/deferred work.
- Occupancy Validity Timespan: configurable projection period for current OCCUPIED slots; current default is **4 hours**. A newer authoritative occupancy observation can shorten or extend the projected interval.

The N-valued limits remain pending team/policy completion.

#### 3.4.10 Cancellation, Late Arrival, and No-Show

- A driver may cancel their reservation. Once cancelled, the reservation entitlement ends and any subsequent parking is treated as a normal non-reservation/walk-in session.
- Cancellation refund eligibility shall be determined by the applicable owner-configured refund policy. Existing draft examples such as cancellation more than N hours before the reservation time and no refund within N hours remain policy values to be completed.
- If the Owner does not enable a late-tolerance policy, the reservation remains valid until its configured end time, subject to allocation, capacity and operational policies.
- If the Owner enables a configurable **Late Arrival Tolerance**, the reservation becomes **NO_SHOW** when no authoritative arrival is recorded by reservation start plus the configured tolerance. NO_SHOW releases protected capacity and ends the reservation entitlement similarly to cancellation.
- Any NO_SHOW refund/forfeiture is governed by the applicable Owner/parking-lot refund policy.
- Automatic cancellation of unpaid reservations after the configured payment-hold period remains governed by the system-level Admin policy and produces **EXPIRED**, not NO_SHOW.

#### 3.4.11 Check-in Process

When a reserved user arrives at the parking lot, the system shall identify the vehicle through the configured vehicle-identification mechanism, such as simulated LPR/image processing or QR code scanning. If a physical barrier integration is enabled, it may open only after backend validation confirms that the vehicle is authorized to enter.

Existing vehicles that are already occupying parking capacity shall not be automatically displaced or forcibly removed by the reservation allocation process. Their occupancy shall instead be accounted for during allocation and conflict handling, with applicable reallocation, queue, operational handling, and refund policies applied when reservation fulfillment is affected.

<!-- INTEGRATION-START FR-RES -->

#### 3.4.12 Reservation Requirement Index

Status definitions are in the Baseline Revision Notice. Detailed constraints are defined once in §5.2; X items are recorded in Appendix E.

| FR ID | Status | Required behavior / canonical SRS location | Actor | User Story |
| --- | --- | --- | --- | --- |
| FR-RES-01 | R/A | Create a reviewed vehicle/time-window reservation request under §3.4.1/3; expose the enabled baseline reservation modes. | Driver | US-D01 |
| FR-RES-02 | A/X | Proposed configurable advance/active-booking/action limits and same-vehicle overlap checks: BR-RES-02. Effective-state counting needs TESTER-DEF-01/08. | Driver | US-D01 |
| FR-RES-03 | C | Apply the baseline allocation/protection behavior together with the Specific Slot / Zone / Capacity reservation modes and Allocation Lead Time. These reservation modes are distinct from the underlying allocation and protection behavior. | Driver, Owner, Operator | US-D01 |
| FR-RES-04 | R/X | Payment hold and confirmation follow §3.4.2/3. Imported no-prepayment confirmation route is not baseline-approved (C-09). | Driver, Payment Provider | US-D01 |
| FR-RES-05 | A | Display booking identifier, QR/ticket, vehicle, requested versus allocated place, time window, actual state and applicable payment/arrival deadlines. | Driver | US-D01 |
| FR-RES-06 | X | Payment hold expiry follows §3.4.2/3; the imported start+late-tolerance forfeiture rule conflicts with §3.4.10 (C-05). Do not relabel no-show as baseline EXPIRED. | Driver, Operator | US-D01 |
| FR-RES-07 | A/X | Proposed early-arrival handling checks actual eligible capacity, records actual entry and applies published extra fees (BR-RES-04), while respecting the established reservation modes and allocation/protection behavior. | Operator | US-D01, US-O01 |
| FR-RES-08 | R/A | Driver cancellation and owner-policy refund eligibility follow §3.4.10. Proposed cancellation controls and capacity release are BR-RES-05; payment refund remains a separate result. | Driver | US-D10 |
| FR-RES-09 | R/A | Facility failure invokes allocation/reallocation/conflict/refund behavior in §3.4.6–8; proposed manual action rights remain C-12/C-13. | Operator, Owner | US-D10, US-O05 |
| FR-RES-10 | A | Allow Driver to view their booking list, lifecycle history, reasons, ticket and related payment/refund status. | Driver | US-D05 |
| FR-RES-11 | A | Record a trustworthy gate-arrival event separately from passage/occupancy. Simultaneous-arrival decisions are handled by the Operator under C-18; detailed event/API representation remains refinement. | Operator, Device Gateway | US-D01, US-O01 |
| FR-RES-12 | A/X | Validate lifecycle transitions and concurrent cancellation/payment/arrival outcomes without duplicated effects (BR-RES-07); missing transitions remain TESTER-DEF-01. | Driver, Operator, Payment Provider | US-D01, US-D10, US-O01 |
| FR-RES-13 | X | Gate-time nearest-slot allocation proposal is held under C-03; slot ranking, map distance and manual fallback must not bypass baseline Allocation Lead Time. | Driver, Operator | US-D01, US-O01 |
| FR-BAS-01 | R | Reservation modes and requested-slot preference: §3.4.1. Exact preferred-slot availability is not guaranteed. | Driver, Owner | US-D01 |
| FR-BAS-02 | R | Allocation trigger/order and stable allocations: §3.4.6; exceptional physical-arrival priority: §3.4.7. Detailed edge cases stay deferred. | Driver, Operator | US-D01, US-O05 |
| FR-BAS-03 | R | Driver reallocation requests and reason enquiry: §3.4.6; maximum two submissions remains baseline until C-18. | Driver, Operator | US-D17 |
| FR-BAS-04 | R | Protection window, backup tracking and conflict queue: §3.4.4–8. These do not imply guaranteed admission without capacity. | Driver, Operator, Owner | US-D01, US-O05, US-OW02 |

<!-- INTEGRATION-END FR-RES -->

### 3.5 Payment & Fee Management

#### 3.5.1 Fee Types

The system shall support the following fee types:

1. **Parking Fee** (per minute/hour):  
     
   - Applied to both registered and guest users  
   - Rounded up to the nearest 15 minutes  
   - Calculated based on entry and exit timestamps

   

2. **Monthly Pass Fee** (account holders):
   - A prepaid rolling monthly-pass price defined by the applicable plan.
   - Monthly entitlement types are **when-space-available** and **guaranteed capacity/slot** as defined in Appendix F.


3. **Overnight Fee** (guest users only):  
     
   - Applied when exit time is after N  
   - Fixed tiered rates determined by the platform  
   - Example: 22:00-06:00: 50,000 VND, plus regular hourly rate after 06:00

#### 3.5.2 Dynamic Pricing

The system shall support dynamic pricing configured by the Owner through time blocks for the MVP. The Owner may define the price applicable to each supported time block and vehicle type within the configured policy scope. Event, holiday and demand-driven automatic price changes are future extensions and are not required for the MVP.

#### 3.5.3 Payment Processing

The system shall support the following MVP payment channels:

- **VNPay**
- **MoMo**
- **ZaloPay**
- **CASH_COLLECT**, an Operator interface for recording manual cash collection confirmation

Additional providers or card channels may be added later through the provider abstraction without redesigning the core payment domain.

Payment processing shall include:

- For provider-based digital payment processing, the system may retry a payment after a retryable failure up to the configured `PAYMENT_RETRY_LIMIT`. The initial payment attempt is not counted as a retry; therefore, with `PAYMENT_RETRY_LIMIT = 3`, a maximum of 4 total payment attempts is permitted. An `UNKNOWN` payment outcome shall not be treated as a retryable failure solely for this purpose and shall require payment-result lookup or reconciliation before another attempt.
- Idempotency keys to prevent duplicate charges.
- Real-time webhook updates from payment providers.
- Backend validation of provider results rather than trusting frontend payment-success messages.
- Invoice generation in PDF format.

#### Payment Security

SmartPark shall initially support QR-based payments through a PCI DSS-compliant third-party payment provider. SmartPark shall not directly store, process, or transmit raw cardholder data. If card payments are introduced later, cardholder data shall be collected and processed through the third-party provider's PCI DSS-compliant hosted payment page, secure payment form, or equivalent mechanism.

#### 3.5.4 Grace Period

Configurable through policies

#### 3.5.5 Refund Processing

Refund eligibility and refund amount shall be determined by the applicable owner/parking-lot refund policy. The system shall support full and partial refunds with appropriate audit trails. Refund requests shall be processed within 3-5 business days.

<!-- INTEGRATION-START FR-PAY -->

#### 3.5.6 Payment Requirement Index

Status definitions are in the Baseline Revision Notice. Detailed constraints are defined once in §5.2; X items are recorded in Appendix E.

| FR ID | Status | Required behavior / canonical SRS location | Actor | User Story |
| --- | --- | --- | --- | --- |
| FR-PAY-01 | R/X | Owner tariff configuration follows §3.2.1/§3.7.2; block size, overnight ownership and pricing mode conflict is C-08. | Owner | US-OW02 |
| FR-PAY-02 | X | Detailed fee-breakdown and tariff refinements remain proposal/deferred work; the confirmed baseline for final price is actual recorded exit (C-21), while MVP dynamic pricing uses Owner-configured time blocks (C-08). | Driver, Operator | US-D02, US-O02 |
| FR-PAY-03 | C | Online payment supports VNPay, MoMo and ZaloPay; Operator may record manual cash collection through CASH_COLLECT. | Driver, Operator, Payment Provider | US-D02, US-O02 |
| FR-PAY-04 | R/A | Verified provider results and idempotency follow §3.5.3; proposed amount/currency/payee matching and reconciliation are BR-PAY-04. | Operator, Payment Provider | US-D02, US-O02 |
| FR-PAY-05 | C | Distinguish failed, pending and unknown outcomes. If payment succeeds after the reservation hold expires, reserve it when the requested capacity is still free; if a competing claim has already won the race, notify the losing Driver and make the paid attempt eligible for refund/reconciliation. | Driver, Operator, Payment Provider | US-D02, US-O02 |
| FR-PAY-06 | R/X | Full/partial audited refunds follow §3.5.5. Owner approval, proof of actual refund and refund limits are proposed in BR-REF-01; C-12 decides workflow authority. | Driver, Owner, Payment Provider | US-D10, US-OW04, US-OW07 |
| FR-PAY-07 | R/A | Generate the PDF invoice required by §3.5.3 and expose personal payment/refund history; notification delivery follows §3.6. | Driver, Notification Service | US-D06 |
| FR-PAY-08 | A | Show overstay charges and send configurable advance notice while still counting actual occupancy (BR-PAY-02); pricing bands require policy approval. | Driver, Notification Service | US-D11, US-OW02 |
| FR-PAY-09 | A/X | Proposed short-stay free-parking behavior is BR-PAY-06, separate from late arrival; monetary boundaries await C-08. | Driver, Operator, Owner | US-D02, US-OW02 |
| FR-PAY-10 | X | Proposed day/night TIME_SLICING remains unresolved; accepted-booking policy/price version preservation is confirmed by C-22 and BR-POL-03. | Owner, Driver, Operator | US-OW02, US-D02 |
| FR-PAY-11 | C | Final parking price is determined from the actual recorded entry and exit timestamps. The proposed exit-quote/clearance/requote model is not active in v0.8.5. | Driver, Operator, Payment Provider, Device Gateway | US-D02, US-O02, US-OW02 |
| FR-BAS-05 | C | Dynamic pricing is an MVP capability using Owner-configured time blocks; event/holiday/demand triggers remain future. | Owner, Driver | US-OW02, US-D02 |
| FR-BAS-16 | R/X | Configured retry, provider webhooks and PDF invoices: §3.5.3; retrying a request is not evidence that a prior attempt failed (C-09/C-10). | Driver, Operator | US-D02, US-O02 |

<!-- INTEGRATION-END FR-PAY -->

### 3.6 Notification System

#### 3.6.1 Push Notifications

The system shall send push notifications for:

- Reservation confirmation  
- Payment reminders  
- Parking spot availability alerts (based on user preferences)  
- Promotional offers

#### 3.6.2 SMS Notifications

SMS shall be used for:

- OTP verification  
- Payment invoices  
- Overdue payment reminders according to the applicable configured notification schedule.
- QR codes for reservations

#### 3.6.3 Email Notifications

Email shall be used for:

- VAT invoices  
- Monthly usage reports  
- Policy updates  
- Password reset
- Slot re allocation
- Account registration success(When admin approved account, owner create account for operator)

<!-- INTEGRATION-START FR-NOTIFY -->

#### 3.6.4 Notification Requirement Index

Status definitions are in the Baseline Revision Notice. Detailed constraints are defined once in §5.2; X items are recorded in Appendix E.

| FR ID | Status | Required behavior / canonical SRS location | Actor | User Story |
| --- | --- | --- | --- | --- |
| FR-RPT-01 | R/A | Event/channel notifications follow §3.6; add status delivery for expiry, refund and appeal decisions. Failed delivery does not roll back successful business state. | Driver, Notification Service | US-D11 |
| FR-BAS-06 | R | Notification channels and listed categories, including availability alerts/preferences, policy updates and account provisioning: §3.6. Fixed reminder dates require C-14. | Driver, Owner, Admin | US-D11 |

<!-- INTEGRATION-END FR-NOTIFY -->

### 3.7 Administrative Functions

#### 3.7.1 Dashboard

The owner dashboard shall provide:

- Real-time parking lot utilization overview  
- Revenue reports (daily, weekly, monthly)  
- Heat maps of parking usage  
- Incident alerts and notifications

#### 3.7.2 Parking Lot Management

Owners shall be able to:

- Create, update, and deactivate parking lots  
- Update pricing in real-time  
- Toggle parking lot status (open/closed)
- Configure policies

#### 3.7.3 System Policy and Configuration Management

Administrators shall be able to configure system-wide policies and settings, including system-level reservation/payment hold behavior and other platform settings that apply across parking facilities.

System-wide policy settings shall form the highest-level scope in the policy hierarchy and shall be considered when evaluating owner, zone, and booking-context policies.

#### 3.7.4 User Management

Administrators shall be able to:

- View and search user accounts  
- Lock/unlock accounts  
- View user activity history

#### 3.7.5 Staff Management

Owners shall be able to:

- Assign roles and permissions to parking attendants (Operators)
- View staff activity logs  
- Manage access to specific parking lots

<!-- INTEGRATION-START FR-OPS -->

#### 3.7.6 Operational, Incident and Gate Workflows

Status definitions are in the Baseline Revision Notice. Detailed constraints are defined once in §5.2; X items are recorded in Appendix E.

| FR ID | Status | Required behavior / canonical SRS location | Actor | User Story |
| --- | --- | --- | --- | --- |
| FR-GATE-01 | R/X | Vehicle identification and manual LPR/QR fallback: §3.4.11 and §6.2.1; threshold/equality and physical-adapter scope require C-14/C-15. | Operator, Recognition Service, Device Gateway | US-O01, US-O08 |
| FR-GATE-02 | C | Walk-in intake does not require a customer account. Entry is subject to lot physical authorization and current capacity/operational conditions; an Operator may refuse entry when applicable capacity is unavailable. | Operator, Driver | US-O01 |
| FR-GATE-03 | A/X | Record an actual entry event and session, rather than treating a scan or gate-open command as passage (BR-GATE-02); event contract pending TESTER-DEF-35. | Operator, Device Gateway | US-O01 |
| FR-GATE-04 | R/A | Scoped Operator live overview in §3.9.2; include approaching bookings, open sessions, waiting vehicles and overstays. | Operator | US-O03 |
| FR-GATE-05 | A | Validate exit identity and settlement, then close session/release occupancy only upon confirmed passage (BR-GATE-02). Final price uses actual recorded exit; quoted-fee windows are not part of the baseline. | Operator, Device Gateway | US-O02, US-O05 |
| FR-GATE-06 | A | Stop automated checkout on plate/ticket/session mismatch, alert Operator and record resolution (BR-GATE-04). | Operator, Recognition Service, Device Gateway | US-O02, US-O05 |
| FR-GATE-07 | A/X | Provide scoped lost-ticket session lookup, entry evidence review and audited manual release (BR-GATE-03); identity evidence remains C-11. | Operator | US-O02, US-O05 |
| FR-GATE-08 | A | Display the Driver's active session and parking history with recorded location, actual times and financial links. | Driver | US-D04 |
| FR-GATE-09 | A | Prevent duplicate open sessions and repeated entry/exit effects; completed single-use tickets cannot open a new session (BR-GATE-02). | Operator, Device Gateway | US-O01, US-O02 |
| FR-INC-01 | A | Record an incident, affected lot/slot/device/session, evidence, assigned handler and outcome; minimum incident workflow remains C-12. | Operator | US-O05 |
| FR-INC-02 | F | Multi-frame automated per-slot violation candidates are future-only; BR-VIOL-01. Do not require per-slot camera AI for MVP acceptance. | Operator, Recognition Service | US-O08 |
| FR-INC-03 | A | Operator manually records suspected wrong-slot parking against the actual allocation, time and evidence; BR-VIOL-02. Reallocation is not itself a violation. | Operator | US-O05 |
| FR-INC-04 | R/A | Human verification of alleged violations follows §6.1; proposed Operator review and recorded rejection/confirmation are BR-VIOL-03, subject to C-12. | Operator | US-O05 |
| FR-INC-05 | C | Accept appeal through the flow appeal → evidence → Operator approval → refund. Owner may handle refund when the Operator escalates the case. ACCEPTED and REFUNDED remain separate outcomes. | Driver, Operator, Owner | US-D07, US-OW07 |
| FR-INC-06 | C | Permit audited manual slot override only for an authorized actor acting under an approved operational condition. | Operator | US-O05 |
| FR-INC-07 | A/X | Display own appeal status, disclosed evidence and decision; redact other customers' data (BR-AUTH-03). Identity/document scope remains C-11. | Driver, Operator | US-D07 |
| FR-OPS-01 | C | Emergency priority/release handling requires an authorized actor and an applicable emergency override policy. The system shall record the action and affected reservation/capacity impact. | Operator, Device Gateway | US-O04 |
| FR-OPS-02 | C | Provide permission-gated emergency release/priority handling with an audit trail and recovery record. No automatic forced relocation or impossible over-capacity guarantee is implied. | Operator, Device Gateway | US-O04 |
| FR-OPS-03 | R/A | Represent missing/unreliable physical information as UNKNOWN (§3.2.2); proposed allocation exclusion is BR-EMERG-04, pending reconciliation C-07. | Operator, Device Gateway | US-O05 |
| FR-OPS-04 | F | Offline cached authorization is Post-MVP; BR-FAIL-01 and §1.2. It requires a future conflict/security design. | Operator, Device Gateway | US-O06 |
| FR-OPS-05 | F | Durable offline queue replay and reconciliation are Post-MVP; BR-FAIL-02. No promise of MVP bidirectional synchronization. | Operator, Device Gateway | US-O06 |
| FR-OPS-06 | A | On online-service failure, report unavailable automation and use a recorded local incident/reconciliation procedure (BR-FAIL-03); do not claim remote commands succeeded. | Operator | US-O05 |

<!-- INTEGRATION-END FR-OPS -->

### 3.8 Reporting & Analytics

#### 3.8.1 Revenue Reports

The system shall generate revenue reports showing:

- Revenue by day/week/month/quarter  
- Revenue by parking lot  
- Revenue by user type (registered vs. guest)  
- Revenue by payment method (Future/Deferred)

#### 3.8.2 Usage Reports

The system shall generate usage reports showing:

- Parking lot occupancy rate by hour  
- Average parking duration  
- Vehicle type distribution  
- Peak usage periods

#### 3.8.3 User Reports

The system shall generate user reports showing:

- New user registrations  
- User retention rate  (Future/Deferred)
- Geographic distribution (Future/Deferred)
- Usage patterns (Future/Deferred)


<!-- INTEGRATION-START FR-REPORT -->

#### 3.8.4 Reporting and Audit Requirement Index

Status definitions are in the Baseline Revision Notice. Detailed constraints are defined once in §5.2; X items are recorded in Appendix E.

| FR ID | Status | Required behavior / canonical SRS location | Actor | User Story |
| --- | --- | --- | --- | --- |
| FR-RPT-02 | R | Owner financial, utilization and operational reporting follows §3.7.1 and §3.8; Guest segmentation conflict is C-01. | Owner | US-OW04 |
| FR-RPT-03 | A | Write protected audit records for permissions, policies, overrides, emergency and manual decisions (BR-PRIV-03); retention conflict remains C-20. | Admin | US-A03 |
| FR-RPT-04 | R/A | Expose authorized Admin audit search by actor, time, lot and action; support §2.3 and §3.9.4 without enabling log edits. | Admin | US-A03 |
| FR-RPT-05 | R/A | Enforce type-specific retention and incident holds under §4.3/4.8 and §6.2.3; proposed lifecycle processing is BR-PRIV-02, subject to C-20. | Admin | US-A05 |
| FR-BAS-07 | R | Reporting fields and their future labels: §3.8.1–3; additions must not promote retention/geographic analytics into MVP. | Owner | US-OW04 |

<!-- INTEGRATION-END FR-REPORT -->

### 3.9 UI / UX Architecture Baseline

#### 3.9.1 Driver Interface

Potential areas include:

- Search
- Parking details
- Reservation
- Payment
- Reservation history
- Parking history
- Vehicle management
- Appeal/support

#### 3.9.2 Operator Interface

Potential areas include:

- Live parking overview
- Occupancy
- Reservation conflicts
- Check-in/check-out
- Incidents
- Emergency controls
- Manual overrides
- Historical activity

#### 3.9.3 Owner Interface

Potential areas include:

- Facility management
- Operator management
- Pricing
- Policy configuration
- Financial information
- Operational reports

#### 3.9.4 Admin Interface

Potential areas include:

- User management
- Device/system configuration
- Audit logs
- Administrative controls

#### 3.9.5 UI Rule

UI designs must not silently become backend business rules. A UI showing an exact space does not automatically mean that the reservation engine must guarantee that exact physical slot.

---

<!-- INTEGRATION-START FR-VIS -->

#### 3.9.6 Visualization and Simulation Requirement Index

Status definitions are in the Baseline Revision Notice. Detailed constraints are defined once in §5.2; X items are recorded in Appendix E.

| FR ID | Status | Required behavior / canonical SRS location | Actor | User Story |
| --- | --- | --- | --- | --- |
| FR-MAP-02 | R | 2D/3D state visualization and backend authority follow §3.2.3; show requested and actual allocated positions distinctly. | Driver, Operator, Owner | US-D16, US-O03 |
| FR-MAP-03 | A | Show vehicle location only to the precision actually recorded for its session; do not invent a slot where only a zone is known. | Driver | US-D16 |
| FR-MAP-04 | A | Refresh views from backend-confirmed events; mark stale/disconnected state and reload current state after reconnect; latency targets remain §4.1. | Driver, Operator, Owner | US-D16, US-O03 |
| FR-SIM-01 | A/X | Product-level scenario simulator controls remain optional and time-bounded. Deterministic test fixtures are the MVP mechanism; a simulator may be added only if required and feasible. | Admin, Device Gateway | US-A06 |

<!-- INTEGRATION-END FR-VIS -->

## 4\. Non-Functional Requirements

### 4.1 Performance Requirements

| Metric | Requirement | Condition |
| :---- | :---- | :---- |
| API Response Time (p95) | \< 500ms | Normal load (100 concurrent users) |
| API Response Time (p99) | \< 1000ms | Normal load (100 concurrent users) |
| API Response Time (p95) | \< 2000ms | Peak load (1000 concurrent users) |
| Database Query Time | \< 100ms | Indexed queries |
| Database Query Time (complex) | \< 300ms | Joins, aggregations |
| Web App Initial Load | \< 3s | 3G connection, first visit |
| Parking Status Update Latency | \< 500ms | End-to-end to mobile app |

| Metric | Requirement |
| :---- | :---- |
| API Requests per Second (sustained) | 1,000 req/s |
| API Requests per Second (peak) | 2,000 req/s |
| Concurrent Users | 1,000 users |
| Concurrent Users (peak) | 5,000 users |
| Database Transactions per Second | 500 TPS |

### 4.2 Availability & Reliability

| Component | Availability Target | RTO | RPO |
| :---- | :---- | :---- | :---- |
| API Gateway | 99.9% | 5 minutes | 0 |
| Microservices | 99.9% | 5 minutes | 0 |
| Database (Primary) | 99.99% | 30 seconds | \< 5 minutes |
| Redis Cluster | 99.9% | 10 seconds | \< 1 minute |
| Message Queue | 99.9% | 30 seconds | \< 5 minutes |
| Payment Integration | 99.95% | 15 minutes | 0 |
| System Overall | 99.5% | 15 minutes | \< 5 minutes |

### 4.3 Security Requirements

| Requirement | Specification |
| :---- | :---- |
| Password Policy | Minimum 8 characters, uppercase, lowercase, number, special character |
| Password Hashing | bcrypt with cost factor 12 |
| Session Token Expiry | 24 hours (refresh token 7 days) |
| OTP Expiry | 5 minutes |
| Account Lockout | 15 minutes after 3 failed attempts |
| Data in Transit | TLS 1.3 mandatory for APIs, TLS 1.2+ for internal services |
| Data at Rest | AES-256 encryption for databases and backups |
| Sensitive Data | CCCD/GPLX are not collected or stored in this phase. If future scope introduces such data, the applicable encryption/tokenization controls shall apply. |
| API Rate Limit | 100 requests/minute per user, 10 requests/minute per IP (unauthenticated) |
| JWT Algorithm | RS256 (asymmetric) |
| Audit Log Retention | 7 years |
| Payment Security / PCI DSS | Payments shall use a PCI DSS-compliant third-party payment provider. SmartPark shall not directly store, process, or transmit raw cardholder data. If card payments are introduced, card data shall be collected through the provider's hosted payment page, secure payment form, or equivalent PCI DSS-compliant mechanism. Applicable PCI DSS validation requirements shall be determined based on the final payment architecture, transaction volume, and provider/acquirer requirements. |
### 4.4 Scalability Requirements

| Metric | Requirement |
| :---- | :---- |
| Microservice Instances | Scale to 10 instances per service |
| Database Connections | Scale to 200 connections |
| Redis Cluster | Scale to 6 nodes (3 primary \+ 3 replica) |
| Message Queue | Scale to 5 nodes |
| Database Size | 1TB (Year 1), 5TB (Year 3\) |
| User Records | 100K users (Year 1), 500K users (Year 3\) |
| Transaction Records | 500K transactions/day at scale |

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
| On-call Coverage | 24/7 |
| Incident Response Time | \< 5 minutes |

---

## 5\. Other Requirements

### 5.1 Legal & Compliance Requirements

The SmartPark system shall comply with applicable Vietnamese laws and regulations, including the following:

- **Law No. 91/2025/QH15 on Personal Data Protection**: governs the collection, processing, protection, retention, and other processing of personal data handled by SmartPark. The system shall implement appropriate personal-data protection measures and support applicable data-subject rights and obligations. 

- **Decree No. 356/2025/NĐ-CP**: provides detailed provisions and implementation measures for the Law on Personal Data Protection. SmartPark shall implement applicable requirements concerning personal-data processing and protection. 

- **Law No. 116/2025/QH15 on Cybersecurity**: applicable cybersecurity requirements shall be considered for the protection and operation of SmartPark's information systems and data. The Law took effect on 01 July 2026.

- **Law No. 36/2024/QH15 on Road Traffic Order and Safety** and **Law No. 35/2024/QH15 on Roads**: applicable requirements concerning road traffic safety, road use, and vehicle-related operations shall be considered where SmartPark interacts with vehicle and traffic-related information or processes. Both laws took effect on 01 January 2025.

- **Circular No. 79/2024/TT-BCA, as amended and supplemented by Circular No. 13/2025/TT-BCA and Circular No. 51/2025/TT-BCA**: relevant requirements concerning vehicle registration and vehicle license plates shall be considered when SmartPark collects or validates vehicle registration and license-plate information. Circular 51/2025/TT-BCA specifically amended Circular 79/2024/TT-BCA as previously amended by Circular 13/2025/TT-BCA. 

- **Decree No. 168/2024/NĐ-CP, as amended by Decree No. 238/2026/NĐ-CP**: applicable requirements concerning administrative sanctions for road traffic order and safety violations shall be considered where SmartPark supports violation-related functions that fall within the scope of these regulations. Decree 238/2026/NĐ-CP took effect on 15 August 2026. 

### 5.2 Business Rules


<!-- INTEGRATION-START BR -->

#### 5.2.1 Rule Catalogue and Precedence

Baseline constraints already written elsewhere have a canonical section pointer here; the original wording is not copied. Existing BR identifiers are retained. A/X rules are review candidates, including refinements of the deferred backlog, not silently resolved business policy. The architecture portion of the older business-rule draft is not imported over §2.

| Rule ID | Status | Constraint / canonical definition | Functional coverage | Decision dependency |
| --- | --- | --- | --- | --- |
| BR-AUTH-01 | R/A | Role and lot/resource scope follow §2.3 and §3.7.5; detailed permission matrix is §2.3.1. No implicit Admin inheritance of operational or financial rights. | FR-AUTH-02/04/05/06 | C-02/04 |
| BR-AUTH-02 | C | Security-critical authentication values remain constrained. Business-policy variables such as retry limits, notification timing and late tolerance may be configurable within Admin-defined bounds. Owner registration does not require a corporate email domain. | FR-AUTH-01/04, FR-BAS-08 | C-14/23 |
| BR-AUTH-03 | A | Read/write access is checked against authenticated identity and the particular resource; Operator may access only assigned lot/lane/case data, not the global customer database. | FR-AUTH-03/06, FR-AI-05 | — |
| BR-VEH-01 | R/A | Vehicle fields and plate validation follow §3.1.3. Proposed temporary identifiers for approved non-plated categories remain account/session-linked and non-reusable across unrelated vehicles. | FR-VEH-01/03 | C-23 |
| BR-VEH-02 | A/X | Proposed: one primary active Driver binding per normalized plate; conflicting claims require a reasoned authorized review; approved transfers never rewrite historical sessions/bookings. Evidence must not be inferred from OTP alone. | FR-VEH-02 | C-11 |
| BR-VEH-03 | R/A | Compatibility is required by §3.4.3. Supported motorcycle parking is part of the baseline. An EV-compatible slot is a slot type/attribute used for occupancy, allocation and payment only; charging functionality and charging-state validation are not implemented. Other proposed size/type constraints remain refinement. | FR-VEH-04 | C-23 |
| BR-CAP-01 | C | Capacity is reported and validated separately by supported vehicle type, including motorcycle. Unused motorcycle capacity does not demonstrate automobile capacity. | FR-CAP-02, FR-VEH-04 | C-23 |
| BR-CAP-02 | C | Canonical capacity states remain separate: PENDING_PAYMENT consumes temporary capacity during the active hold; RESERVED follows successful payment; OCCUPIED is physical reality; PROTECTED is reservation protection; BACKUP is excluded from ordinary availability. Available Capacity = Total Capacity − Occupied − Protected − Pending Payment − Backup. | FR-CAP-03/06 | C-06 |
| BR-CAP-03 | A | No incompatible claims may consume the same last resource. Idempotent retries return the same logical result and do not deduct again. Implementation lock/database/event strategy is not prescribed. | FR-CAP-04, FR-RES-12 | ARCH-DEF-09 |
| BR-CAP-04 | C | Backup inventory is excluded from ordinary availability. Owner-configured backup capacity may be a fixed capacity allocation, while the dynamic protection backup pool protects reservation-related capacity during the Protection Window. A protected/backup capacity claim never forces an already-occupied vehicle to move. | FR-CAP-07, FR-BAS-04 | C-06 |
| BR-CAP-05 | A | A claim becoming occupied changes accounting once; physical/protection states may coexist. Expiry/cancellation of a booking cannot release a vehicle's still-occupied space; release follows verified departure. | FR-CAP-05, FR-GATE-05/09 | TESTER-DEF-05/06 |
| BR-RES-01 | C | Specific Slot, Zone and Capacity are the canonical reservation modes. Specific Slot expresses a preferred physical slot, not an absolute arrival guarantee. Allocation begins at Allocation Time = Reservation Start − Allocation Lead Time. | FR-RES-03/13, FR-BAS-01/02 | C-03 |
| BR-RES-02 | A | Proposed configurable advance window, active-booking count and action-rate window; reject overlapping incompatible reservations for the same vehicle. Active-state counting and all N values must be approved, not inferred. | FR-RES-02 | TESTER-DEF-01/08 |
| BR-RES-03 | C | Free-parking duration and arrival eligibility are separate. Without an Owner late-tolerance policy, reservation entitlement lasts until end time. With an enabled Late Arrival Tolerance, no authoritative arrival by start + tolerance produces NO_SHOW and releases protected capacity according to policy. | FR-RES-06/11 | C-05 |
| BR-RES-04 | A/X | Early entry requires compatible actual capacity and published extra-time charges; if unavailable, offer waiting or an authorized alternative. An occupied vehicle is never automatically displaced (§3.4.11). | FR-RES-07 | C-03/08 |
| BR-RES-05 | R/A | Cancellation/refund entitlement follows §3.4.10. Proposed cancellation is validated again at execution, releases only unused rights once, and shows refund as a separate process. A live occupied session cannot be freed by cancellation. | FR-RES-08/09, FR-PAY-06 | C-12 |
| BR-RES-06 | A | A configurable inter-booking buffer may protect successive use of the same physical resource; buffer must participate in availability checks and is not physical occupancy or a second booking. | FR-CAP-03, FR-RES-02 | C-06 |
| BR-RES-07 | R/A | Lifecycle meanings remain §3.4.2. Candidate transitions must validate current state and preserve one consistent outcome for simultaneous payment, cancellation, expiry and entry; missing transition definitions stay open. | FR-RES-12 | TESTER-DEF-01/12 |
| BR-RES-08 | R | Normal and exceptional physical-arrival priorities, no automatic displacement after occupation, accepted-reallocation default refund treatment and request limit are defined once in §3.4.6/7. | FR-BAS-02/03/04 | C-18 |
| BR-GATE-01 | R | Recognition is evidence; backend authorization remains required by §3.4.11 and §6.1/6.2. Manual/QR fallback cannot bypass identity, capacity or payment checks. | FR-GATE-01 | C-14/15 |
| BR-GATE-02 | A/X | A scan/open command is not passage. Proposed session starts on verified entry and closes on verified exit; ticket is one-use and one vehicle cannot have duplicate open sessions. Payment, entitlement and actual occupancy remain separate. | FR-GATE-03/05/09 | TESTER-DEF-35; C-21 |
| BR-GATE-03 | A/X | Lost-ticket flow requires authorized lookup, comparison against entry evidence and vehicle/account verification, published extra fee if applicable, and reasoned audit. Never infer successful verification from a plate match alone. | FR-GATE-07 | C-11 |
| BR-GATE-04 | A | Plate/token/session mismatch stops the automatic exit process and raises an Operator review; the software does not declare theft or levy a fine from mismatch alone. | FR-GATE-06 | — |
| BR-PAY-01 | X | Proposed TIME_SLICING splits [entry,billing-end) at each configured day/night boundary, rounds each nonempty continuous segment up to its N-minute block, and sums segment rates. Do not merge distinct days, double-add base fees or combine with fixed overnight surcharge. Baseline §3.5.1 remains unresolved. | FR-PAY-01/02/10 | C-08 |
| BR-PAY-02 | A | Proposed overstay: published ordinary time charge plus separately itemized N-configured penalty; send warning N time before expiry. Actual occupancy persists even after reservation time ends. Human-confirmed violation charges remain separate. | FR-PAY-08 | C-08/12 |
| BR-PAY-03 | C | MVP payment channels are VNPay, MoMo and ZaloPay, with CASH_COLLECT for Operator-recorded manual cash collection; PDF invoice behavior remains §3.5.3. Additional provider/card channels are future extensions. | FR-PAY-03/07, FR-BAS-16 | C-10 |
| BR-PAY-04 | C | Provider results are validated and idempotent. A payment that succeeds after hold expiry may still create the reservation when the requested capacity remains free. If a competing reservation has already won the backend race, the losing paid attempt is notified and is eligible for refund/reconciliation. Unknown and duplicate money remain separate reconciliation outcomes. | FR-PAY-04/05 | C-09; TESTER-DEF-12 |
| BR-PAY-05 | F | Proposed exit quote/clearance/requote behavior is retained as an inactive proposal for traceability. It is not the v0.8.5 billing rule; actual recorded exit determines the final price. | FR-PAY-11, FR-GATE-05 | C-21 |
| BR-PAY-06 | A/X | Proposed free short stay: N>0 and actual exit-entry<=N makes time-based parking charge zero; N=0 disables. If exceeded, bill the full chargeable interval, not duration minus N. Separate approved penalties/service fees/deposit consequences remain. Free exit right cannot extend beyond min(D,entry+N); late passage triggers settlement/reconciliation. Overpayment produces a refund request, not assumed completion. | FR-PAY-09/11 | C-08/21 |
| BR-REF-01 | A/X | Proposed refund workflow separates request, human approval, provider submission and confirmed paid-back result. Owner is proposed approver; retries are idempotent and aggregate refunds cannot exceed eligible collected money. ACCEPTED appeal is not REFUNDED. Baseline turnaround remains §3.5.5 until reviewed. | FR-PAY-06, FR-INC-05 | C-12/14 |
| BR-VIOL-01 | F | Future per-slot automated detection must use configurable persistence, multiple frames and confidence, retain evidence and request human review. Do not implement it as MVP image-input LPR. | FR-INC-02 | §1.2, §6.3 |
| BR-VIOL-02 | A | MVP candidate: Operator inspects and records suspected wrong-slot use against actual allocation; no per-slot AI assumed. An accepted alternate allocation is not a violation merely because the requested slot differs. | FR-INC-03 | C-12 |
| BR-VIOL-03 | R/A | AI cannot make punitive/legal decisions (§6.1). Proposed authorized human review records evidence, confirmed/rejected outcome and reason before a charge is added. | FR-INC-04 | C-12 |
| BR-VIOL-04 | C | Appeal workflow is appeal → evidence → Operator approval → refund. ACCEPTED is a decision state and does not mean REFUNDED. An Owner may complete refund handling when an Operator escalates the appeal. | FR-INC-05/07 | C-12 |
| BR-EMERG-01 | C | Backend state remains authoritative over visualization. Manual override has authority only when initiated by an authorized actor under an approved operational condition and is audited; no blanket manual precedence applies. | FR-INC-06, FR-MAP-02 | C-07 |
| BR-EMERG-02 | C | Emergency handling uses permission-gated release/priority actions. It does not imply physically impossible over-capacity admission or forced eviction of occupied vehicles. | FR-OPS-01 | C-13 |
| BR-EMERG-03 | C | Emergency release/priority actions record the authorized actor, time, relevant gate/lot context, reason and outcome. Physical/manual recovery actions must be recorded when connectivity is restored. | FR-OPS-02/06 | C-13/15 |
| BR-EMERG-04 | R/A | UNKNOWN means unverifiable physical state (§3.2.2); proposed allocation excludes it until reliable verification. Restoring availability requires the approved reconciliation process, not UI inference. | FR-OPS-03 | C-07 |
| BR-FAIL-01 | F | Cached offline entitlement evaluation is future work; full offline mode is excluded by §1.2. | FR-OPS-04 | Future security/conflict design |
| BR-FAIL-02 | F | Durable offline replay/reconciliation is future work and must prevent duplicate sessions, capacity deductions and financial effects. | FR-OPS-05 | Future synchronization design |
| BR-FAIL-03 | A | A service/hardware outage is not proof of payment or fee waiver. MVP stops claiming successful automatic processing, uses an approved local incident procedure, then records actual events and reconciles when connected. | FR-OPS-06, FR-PAY-05 | C-13/15 |
| BR-PRIV-01 | R/A | Apply least data exposure and approved processing boundaries under §4.3, §5.1 and §6.9.2; do not treat unspecified identity fields as authority to collect documents. | FR-AUTH-06, FR-RPT-05 | C-11/20 |
| BR-PRIV-02 | R/A | Retention categories and legal/incident exceptions stay as written in §4.3/4.8/6.2.3. Proposed expiry jobs honor authorized holds and deletion rights; N values cannot override approved constraints. | FR-RPT-05 | C-20 |
| BR-PRIV-03 | C | Audit records capture who, when, resource/location, action, reason and outcome. Retention remains 7 years for audit logs, 90 days hot plus 1 year cold for operational logs, and maximum 30 days for images with authorized holds; conversations are logged. Retention categories remain distinct. | FR-RPT-03/04 | C-20 |
| BR-POL-01 | A | Each policy has its own identifier, typed value, unit, scope, permitted editor/override, validity domain and effective revision. N is a placeholder in requirements, never a shared variable or production literal. | FR-POL-03/07 | TESTER-DEF-07/08 |
| BR-POL-02 | A | Reject activation of missing/invalid values, overlapping or uncovered applicable tariff/refund bands, or unresolved precedence. Invalid configuration is not interpreted as a zero fee or unlimited capacity. | FR-POL-04/08 | TESTER-DEF-08/09 |
| BR-POL-03 | C | An accepted booking retains its accepted policy/price version. Later policy revisions apply to future applicable transactions and do not retroactively reprice the accepted booking. | FR-POL-05/08 | C-22 |
| BR-POL-04 | A | Backend supplies effective terms; before executing a transaction, changed price/policy/availability relative to reviewed terms requires fresh review. Web and chatbot invoke the same validation path. | FR-POL-06, FR-AI-07 | — |
| BR-AI-01 | A | Static answers use approved help content; live price, availability, policy and personal state use authoritative APIs. Model output does not replace price/ranking/allocation engines or invent N values. | FR-AI-02/03/04/11 | C-17 |
| BR-AI-02 | A | Tools are allowlisted and authorized for the authenticated session on every call; model-supplied user IDs cannot change account scope. Do not forward credentials or unrelated personal records to the model. | FR-AI-05/10 | AI-DEP-02/06 |
| BR-AI-03 | A | Reservation preparation is a draft. Final UI review/edit/confirmation binds the submitted parameters; later changes invalidate that confirmation. Backend revalidates before creating; a conversational yes alone does not silently submit an altered form. | FR-AI-06/07 | AI-DEP-04 |
| BR-AI-04 | R/A | Financial/access/legal authority stays in §6.1. No raw DB, barrier, policy override or direct charge/refund tools for the model. Ordinary checkout and human refund decisions remain distinct from AI assistance. | FR-AI-08/10 | C-16 |
| BR-AI-05 | A | Report actual API outcome: empty data is different from unauthorized, invalid input, conflict, timeout or service failure. Read retry may be bounded; write retry requires a stable idempotency/result-lookup contract. | FR-AI-09 | AI-DEP-02/04 |

#### 5.2.2 Boundary Scenarios for Dev / QA Review

These are review scenarios, not claims of executable tests or approved acceptance criteria for X behavior. Complete existing v0.8 acceptance remains subject to the open decisions.

| Scenario | Expected result or decision gate | Coverage |
| --- | --- | --- |
| Two simultaneous claims for the last compatible unit | At most one incompatible claim succeeds; retry has no extra effect. | BR-CAP-03; FR-CAP-04 |
| Unpaid checkout hold | Blocks incompatible claims; reserved total is unchanged; expiry follows Admin5-minute default unless configured otherwise. | §3.4.2/3; C-04 |
| Reservation reaches Allocation Time | Apply baseline start-time then confirmation-time priority; not unconditional gate-only allocation. | §3.4.6; C-03 |
| Allocated slot lost before arrival | Apply exceptional affected-driver physical-arrival priority; no automatic eviction of an occupied car. | §3.4.7; equal/3+ arrival cases await C-18 |
| Late paid callback after hold expiry | Proposed reconciliation without entitlement revival; QA must not mark expected outcome confirmed until C-09 is closed. | BR-PAY-04 |
| Accepted alternate slot | Default no refund solely for accepting reallocation; payment remains linked to session and is not charged again for changing slot. | §3.4.6 |
| Short stay exactly N versus N+epsilon | Proposed exact N free, above N full interval billed; no subtraction of N. | BR-PAY-06; C-08 |
| Day/night crossing20:30–23:30 | Candidate60-minute blocks:2 day blocks+2 night blocks; demo20000+30000=50000. This is not a baseline fee test until C-08. | BR-PAY-01 |
| Quoted exit expires | Candidate T=11:00,Q=11:02,D=11:05: settlement11:01 permits exit11:04; requote11:06 subtracts prior receipts; occupancy remains until passage. | BR-PAY-05; C-21 |
| Appeal accepted | Proposed Owner refund approval and actual payout still separate; do not display REFUNDED yet. | BR-VIOL-04; C-12 |
| Model returns LPR confidence exactly90% | Unspecified baseline equality; must be decided, not guessed by QA. | C-14 |
| Other user's booking ID in chat | Backend denies access; model never retries with a different user identity. | BR-AI-02 |
| Search API timeout | Report temporary failure; do not say every lot is full. | BR-AI-05 |
| Reservation draft edited after confirmation | Prior confirmation cannot submit new terms; show revised form and require confirmation. | BR-AI-03 |
| Repeated write/checkout action | One logical reservation/payment effect; result lookup/reconciliation after unknown result. | BR-CAP-03, BR-PAY-04 |
| Provider unavailable | Do not fabricate payment success or erase fee; display incident/fallback path. | BR-FAIL-03 |

#### 5.2.3 Configurable Policy Register and Demo Values

This is the single current-phase policy register. Every N is an independent configurable value with a unit, not a literal value shown to customers. Numeric examples are isolated Dev/QA proposals unless the row explicitly identifies an existing baseline default. Withheld candidates must not seed a baseline environment. Existing fixed values remain unchanged until a recorded approval converts them to N. Technical performance/SLO metrics stay in §4/§6, not automatically in this business-policy catalogue. Allowed ranges below are proposals unless already fixed by baseline; TESTER-DEF-08 remains open until every field's detailed default, validation and representation are agreed. Current MVP monthly-plan settings are defined in Appendix F; unrelated future settings remain non-normative.

| Parameter | Unit / type | Configuration authority | Baseline value / proposed demo value | Validation / activation condition |
| --- | --- | --- | --- | --- |
| OTP_EXPIRY / OTP_MAX_FAILURES / OTP_LOCK_DURATION | Minutes / count / minutes | Admin proposed; security baseline retained | 5 / 3 / 15 in §3.1.1/4.3 | N conversion pending C-14; positive integer counts/durations proposed. |
| SESSION_TOKEN_EXPIRY / REFRESH_TOKEN_EXPIRY | Duration | Admin proposed | 24 hours / 7 days §3.1.4 | Do not silently convert security requirements into arbitrary Owner settings. |
| PAYMENT_HOLD_DURATION | Minutes | Admin default; Owner may override | 5 minutes Admin default | Positive duration; Owner override remains within Admin-defined bounds. |
| RESERVATION_MIN_DURATION / MAX_DURATION | Minutes / hours | Owner proposed within system scope | N; no numeric default approved | Positive; max >= min; TESTER-DEF-08. |
| MAX_ADVANCE_BOOKING | Hours | Owner proposed | N; isolated demo proposal24 | Previous BR8h is not a confirmed default; team approval needed. |
| MAX_ACTIVE_BOOKINGS | Count | Admin proposed | N; demo2 | Positive integer; counted states pending TESTER-DEF-01. |
| BOOKING_ACTION_LIMIT / WINDOW | Count / duration | Admin proposed | N / N; demo6 per24h | Define counted events and interval boundaries before rate-limit tests. |
| RESERVATION_PROTECTION_WINDOW | Hours | Owner baseline | N; example4h is illustrative §3.4.4 | Nonnegative proposed; not the payment hold or allocation lead time. |
| ALLOCATION_LEAD_TIME | Minutes | Owner baseline | N; example30min §3.4.6 | Allocation time=start-lead; late-created behavior TESTER-DEF-13. |
| RESERVATION_MODE | Enum/set | Owner baseline | Specific Slot / Zone / Capacity | Reservation mode selection only; allocation and protection behavior are defined separately by the baseline. |
| DRIVER_REALLOCATION_REQUEST_LIMIT | Count | Admin default; Owner may override | 2 default §3.4.6 | Positive integer; Admin defines default/bounds and Owner may override within scope. |
| BACKUP_CAPACITY_POLICY | Structured demand/capacity rule | Admin default/bounds; Owner may override | Configurable; no numeric default | Fixed backup capacity and the dynamic protection-pool behavior are supported. Optional policy activation follows the Owner choice. |
| OCCUPANCY_VALIDITY_TIMESPAN | Hours | Admin default/bounds; Owner may override | 4 hours default | Current OCCUPIED projection remains occupied for this timespan unless observed occupancy changes earlier; used only for availability projection. |
| INTER_BOOKING_BUFFER | Minutes | Owner proposed | N; demo15 | Nonnegative; include in claim interval without double counting. |
| ARRIVAL_LATE_TOLERANCE | Minutes | Admin default/bounds; Owner may override/enable | N | Optional policy; when disabled, reservation remains valid until end time. When enabled, NO_SHOW occurs at start + tolerance without authoritative arrival. |
| VERIFIED_ARRIVAL_WAIT_LIMIT | Minutes | Owner proposed | N; demo15 withheld | Proposed Operator alert only; not automatic no-show/forfeiture for verified arrivals. |
| CANCELLATION_TIME_BANDS / REFUND_RATE | Minutes / percent | Owner baseline; schema proposed | N; demo >=30:100%, 15..<30:50%, <15:0% | Proposal only, not compulsory policy; boundaries exhaustive; percentage0..100. |
| NO_SHOW_REFUND_RATE / FACILITY_FAILURE_REFUND_RATE | Percent | Owner baseline | N; demo0% /100% | Different reasons, not one generic refund rate; no compulsory100% implied by examples. |
| ACCEPTED_REALLOCATION_REFUND_POLICY | Rule | Owner baseline | No refund solely for accepted reallocation | Baseline §3.4.6; all other charge/refund conditions remain independent. |
| PREPAYMENT_MODE / VALUE | Enum + money or percent | Owner proposed | N; demo30% withheld | Deposit/full/zero-payment treatment requires C-09; one clear unit per rule. |
| BILLING_BLOCK_DURATION | Minutes | Owner proposed; baseline ownership incomplete | 15 minutes baseline §3.5.1; imported60 withheld | C-08 determines configurable block and rounding; positive integer proposed. |
| PRICING_MODE | Enum | Owner proposed / platform baseline tier wording | Baseline overnight tiers; TIME_SLICING candidate | Approve one fee algorithm and its category coverage C-08; do not stack alternatives. |
| DAY_START / NIGHT_START | Local clock time | Owner proposed | 06:00 /22:00 demo only | Frames must cover each local day without overlap; timezone proposed Asia/Ho_Chi_Minh. |
| DAY_BLOCK_PRICE / NIGHT_BLOCK_PRICE | VND per block and vehicle type | Owner proposed | N; demo car10000/15000, motorcycle2000/3000 withheld | Activation depends on C-08/C-23; fixture values are not market prices. |
| OVERNIGHT_FEE / OVERNIGHT_TIME | Money / local time | Platform wording in §3.5.1 conflicts Owner | N; example50000 VND,22:00–06:00 | Illustration retained, not imported as TIME_SLICING seed; C-08. |
| MAX_DYNAMIC_PRICE_INCREASE | Percent of base | Owner/platform split pending | N §3.5.2 | MVP triggers and precise cap formula need TESTER-DEF-25. |
| FREE_PARKING_GRACE_PERIOD | Minutes | Owner proposed | N; demo15 | Candidate BR-PAY-06: N=0 off; exact boundary included; separate from lateness C-05. |
| OVERSTAY_PENALTY / EXPIRY_NOTICE_LEAD_TIME | Money/band / minutes | Owner proposed | N /N; demo5000 per block /15 | Rule structure, triggers and allowed adjustments need approval. |
| LOST_TICKET_FEE | VND | Owner proposed | N; demo20000 | Nonnegative, disclosed before charge; lost ticket does not prove identity. |
| EXIT_QUOTE_VALIDITY / EXIT_CLEARANCE_WINDOW | Minutes | Owner proposed | N /N; demo2 /5 withheld | 0<quote<clearance; BR-PAY-05 awaits C-21. |
| VEHICLE_DIMENSION_LIMIT | Metres for each dimension | Owner proposed | N; demo height2,width2,length5 | Supported category fixtures only; C-23. |
| LPR_CONFIDENCE_THRESHOLD | Percent or calibrated score | Admin proposed | >90% baseline; <90 manual | Exact90 and model score interpretation unresolved C-14; domain proposed0..100%. |
| CHATBOT_ESCALATION_CONFIDENCE / REPEAT_COUNT | Score / count | Admin proposed | 70% /3 baseline §6.7.2 | Calibration and exact equality need team decision; no fabricated LLM certainty. |
| REFUND_ESCALATION_THRESHOLD | Money | Authority pending | Unspecified §6.7.2 | Define currency, scope and comparison operator; all final refund decisions human. |
| PAYMENT_RETRY_LIMIT | Count of retries after the initial attempt | Admin default; Owner may override if exposed | 3 default §3.5.3 | Applies only to retryable payment failures. The initial attempt is not counted. With value 3, at most 4 total attempts are permitted. UNKNOWN outcomes require lookup/reconciliation rather than automatic retry. |
| OVERDUE_REMINDER_SCHEDULE | Days after obligation due | Admin default; Owner may override | N | Notification schedule is a business-policy variable; it does not create unpaid monthly debt by implication. |
| NOTIFICATION_DAYS | Days / notification offsets | Admin default; Owner may override | N | Defines the configured timing for applicable notifications, including overdue-payment reminders. Owner overrides remain within Admin-defined bounds; security-critical timings remain constrained separately. |
| REFUND_PROCESSING_TARGET | Business days | Owner/Admin split pending | 3–5 baseline §3.5.5 | N conversion and operational enforcement remain C-14. |
| IMAGE_RETENTION | Days and exception hold | Admin proposed within approved constraints | Maximum30 baseline §6.2.3 | Do not replace baseline ceiling/legal hold with unrestricted N; C-20. |
| AUDIT / OPERATIONAL / CHAT_RETENTION | Duration by data class | Admin proposed within approved constraints | 7y audit;90d hot/1y cold logs; chat unspecified | Separate categories and holds; C-20. No permanent default imported. |
| VIOLATION_PERSISTENCE / FRAME_COUNT | Seconds / count | Admin proposed; Future only | N /N; demo5 /3, disabled MVP | Only future per-slot detection, not LPR threshold. |
| SEARCH_RADIUS | Kilometres | Configuration owner pending | Default5 §3.3.2/3 | Expansion offered by baseline; configurable ownership/N conversion requires C-14. |

<!-- INTEGRATION-END BR -->

### 5.3 Assumptions

1. Users have smartphones running iOS or Android  
2. Users grant location permissions and have internet connectivity  
3. Parking lots have basic IoT infrastructure (parking sensors)  
4. Payment gateways are integrated and operational  
5. Parking attendants receive basic system training  
6. Stable internet connectivity is available at parking lots  
7. Users have basic smartphone literacy

### 5.4 Constraints

1. **Legal**: Must comply with Law No. 91/2025/QH15 on Personal Data Protection 
2. **Technical**: Dependent on reliable IoT sensor operation (Future/Deferred)  
3. **Financial**: Development cost estimated at \$50,000 \- \$150,000  
5. **Resources**: Requires 1 BAs, 2 BEs, 2 FEs, 1 DevOps, 1 QA, 1 PM

### 5.5 Deployment Considerations

The system shall be containerized using Docker and shall support deployment to a cloud or server infrastructure capable of running the SmartPark microservices.

The deployment shall support:

- Independent deployment and horizontal scaling of individual microservices.
- PostgreSQL as the primary relational database for transactional data.
- Automated CI/CD pipelines for building, testing, packaging, and deploying services.
- Secure configuration of application settings, credentials, and connection strings through environment variables or a managed secrets mechanism.
- Database backup and recovery mechanisms appropriate to the selected PostgreSQL deployment.
- High-availability deployment options where required by the production environment.

For cloud deployment, AWS or Azure may be used. The production environment shall be deployed in infrastructure suitable for users and operations in Vietnam and shall comply with applicable Vietnamese data-residency requirements. The selected deployment location shall be validated for service availability, cost, and network latency through performance testing.

### 5.6 Third-Party Dependencies

| Dependency | Purpose | Fallback Strategy |
| :---- | :---- | :---- |
| VNPay, MoMo, ZaloPay | Payment processing | Queue payment, retry later, notify user |
| Google Maps / Mapbox | Location services | Show cached map, allow manual entry |
| SMS Provider | OTP, notifications | Use email fallback, queue SMS |
| Email Provider | Invoices, notifications | Queue email, retry later |
| MQTT Broker (Future/Deferred) | IoT sensor data | Staff manual update, show last known status | 

---

## 6\. AI Integration Requirements

### 6.1 AI Role & Traditional System Boundaries

The system shall separate AI responsibilities from traditional business logic to ensure reliability, compliance, and user trust.

**AI Responsibilities (Recognition, Prediction, Recommendation, Alerting):**

- Recognize license plates, parking spot occupancy, and user preferences  
- Predict parking demand, capacity, and pricing optimization (Future/Deferred) 
- Explain deterministic backend-ranked parking options based on returned authoritative results; personalized/model-based ranking remains future  
- Alert staff to anomalies, fraud, and operational issues  (Future/Deferred)
- Support users through conversational AI interfaces 

**Traditional System Responsibilities (Confirmation, Calculation, Authorization, Payment, Legal):**

- Confirm reservations, payments, and access permissions  
- Calculate fees, taxes, and dynamic pricing rules  
- Authorize users, staff, and system actions  
- Process payments, refunds, and financial transactions  
- Make final legal decisions on violations and penalties

**Boundary Rules:**

- AI shall not charge or collect money directly; the traditional system calculates and processes all financial transactions  
- AI shall not grant or deny access; the traditional system enforces permissions and validates AI suggestions  
- AI shall provide recommendations with confidence scores; humans confirm critical decisions  
- AI shall flag anomalies; humans investigate and take action  
- AI shall recognize patterns; the traditional system validates against business rules and regulations

### 6.2 AI License Plate Recognition (LPR)

#### 6.2.1 Functional Requirements

The system shall integrate AI-powered license plate recognition for automated vehicle identification at parking lot entry and exit points. (Image upload simulation for MVP, No real Iot implementation)

**AI Component:**

- Recognize Vietnamese license plates from camera images under various conditions (day, night, rain, partial occlusion)  
- Output confidence score for each recognition result  
- Detect abnormal or suspicious license plates  
- Process recognition requests within 3 seconds

**Traditional System Component:**

- Validate recognized plate against registered user vehicles  
- Confirm entry/exit authorization based on reservation status and payment  
- Log all recognition events for audit and compliance  
- Handle manual override by parking attendants when AI confidence is below threshold

**Integration Rules:**

- Auto-open barrier only when AI confidence \> 90% AND traditional system confirms valid reservation/payment  
- When AI confidence \< 90%, require attendant manual verification  
- All AI recognition results shall be logged with timestamp, image, and confidence score  
- Staff shall have ability to override AI decision and correct plate entries

#### 6.2.2 Accuracy & Performance

| Metric | Requirement |
| :---- | :---- |
| Recognition Accuracy | \> 98% for standard Vietnamese plates |
| Processing Time | \< 3 seconds per frame |
| Confidence Threshold | \> 90% for automated approval |
| Fallback Rate | \< 2% requiring manual intervention |
| Night/Low-light Accuracy | \> 95% |

#### 6.2.3 Privacy & Compliance

- License plate images shall be retained for a maximum of 30 days for operational, security, and dispute-resolution purposes, unless a longer retention period is required or permitted by applicable law or is necessary for an ongoing incident, dispute, or legal obligation.

- License plate recognition logs shall be encrypted at rest and protected by role-based access control. Access shall be limited to authorized personnel based on their assigned responsibilities.

- The license plate recognition functionality shall comply with **Law No. 91/2025/QH15 on Personal Data Protection** and **Decree No. 356/2025/NĐ-CP**, together with other applicable Vietnamese regulations. License plate numbers are classified as basic personal data under Decree 356/2025/NĐ-CP. :contentReference[oaicite:0]{index=0}

- Users shall be informed of license plate recognition and the associated collection and processing of personal data through appropriate notices, including signage at parking lot entrances where applicable.

- When the applicable retention period expires or the processing purpose is no longer applicable, license plate images and related personal data shall be deleted or destroyed in accordance with applicable requirements.

<!-- INTEGRATION-START FR-LPR -->

#### 6.2.4 LPR Traceability Index

Status definitions are in the Baseline Revision Notice. Detailed constraints are defined once in §5.2; X items are recorded in Appendix E.

| FR ID | Status | Required behavior / canonical SRS location | Actor | User Story |
| --- | --- | --- | --- | --- |
| FR-BAS-09 | R/X | LPR image upload, plate/confidence results, suspicious-result reporting, manual correction and event evidence: §6.2.1–3; C-14. | Operator | US-O08 |

<!-- INTEGRATION-END FR-LPR -->

### 6.3 AI Parking Spot Status Detection (Future/Deferred)

#### 6.3.1 Functional Requirements

The system shall use AI computer vision to detect parking spot occupancy status, supplementing or enhancing existing IoT sensor data.

**AI Component:**

- Analyze camera feeds to detect occupied vs. empty parking spots  
- Output occupancy prediction with confidence score  
- Handle occlusions, shadows, and adverse weather conditions  
- Detect vehicles that may not trigger sensors (e.g., motorcycles, small vehicles)

**Traditional System Component:**

- Fuse AI predictions with existing IoT sensor data using confidence-weighted algorithm  
- Finalize spot status based on combined data sources  
- Allow staff manual override of AI predictions  
- Maintain audit trail of all status changes with source attribution

**Integration Rules:**

- When AI confidence \> 85%, automatically update spot status  
- When AI confidence \< 85%, rely on sensor data or mark as "uncertain"  
- In case of conflict between AI and sensor, prioritize sensor data but log AI discrepancy  
- Staff shall receive alerts for persistent AI-sensor conflicts requiring investigation

#### 6.3.2 Accuracy & Performance

| Metric | Requirement |
| :---- | :---- |
| Detection Accuracy | \> 95% under normal conditions |
| False Positive Rate | \< 5% |
| Processing Latency | \< 500ms per frame |
| Weather Degradation | \< 10% accuracy drop in light rain |
| Night Performance | \> 90% accuracy with adequate lighting |

#### 6.3.3 Privacy & Compliance

- Camera feeds shall not store continuous video; only processed frames with detected vehicles  
- Spot-level detection images shall be retained for 7 days maximum  
- Clear signage shall inform users of AI-powered monitoring  
- System shall comply with local privacy regulations for video surveillance

### 6.4 AI Parking Recommendations

#### 6.4.1 Functional Requirements

The system shall provide AI-enhanced parking recommendations that go beyond simple nearest-available logic.

**AI Component:**

- Analyze user behavior patterns, preferences, and history  
- Consider contextual factors: time of day, destination type, vehicle size, weather  
- Generate personalized ranked list of parking options  
- Provide explainable recommendations ("Recommended because you often choose covered parking")

**Traditional System Component:**

- Validate all recommended spots for actual availability and legal compliance  
- Enforce business rules on reservations and pricing  
- Present AI recommendations alongside traditional distance-based results  
- Allow users to override AI and view all available options

**Integration Rules:**

- AI recommendations shall be clearly labeled as "Suggested for you"  
- Users shall always have option to view "All nearby parking" without AI filtering  
- AI shall not hide available spots from users  
- Recommendations shall update in real-time as availability changes

#### 6.4.2 Personalization Features

- Learn user preferences: covered vs. open, near elevator, price sensitivity  
- Recognize recurring patterns: workplace, home, regular destinations  
- Adapt to vehicle type: standard vs. oversized vehicles  
- Consider historical booking success rates for each parking lot

#### 6.4.3 Cold Start Strategy

- For new users without history: use demographic and contextual defaults  
- For new parking lots: use rule-based ranking until sufficient data accumulates  
- Gradually introduce personalization as user data grows

### 6.5 AI Capacity Forecasting (Future/Deferred)

#### 6.5.1 Functional Requirements

The system shall use AI to predict future parking lot occupancy and demand patterns.

**AI Component:**

- Forecast occupancy at 1-hour, 3-hour, and 24-hour horizons  
- Incorporate historical patterns, weather, local events, and holidays  
- Predict demand spikes and capacity crunches  
- Provide confidence intervals for predictions

**Traditional System Component:**

- Use forecasts to inform dynamic pricing adjustments  
- Trigger alerts to staff when predicted occupancy exceeds thresholds  
- Generate operational recommendations for staffing and maintenance  
- Validate predictions against actual outcomes for model training

**Integration Rules:**

- AI shall suggest pricing adjustments; traditional system confirms and applies changes  
- AI shall flag high-demand periods; staff shall validate and respond  
- Forecasts shall be logged with actual outcomes for continuous improvement  
- System shall fall back to rule-based pricing when model uncertainty is high

#### 6.5.2 Model Requirements

| Metric | Requirement |
| :---- | :---- |
| Prediction Horizon | 1 hour to 7 days |
| Forecast Accuracy (MAPE) | \< 15% |
| Retraining Frequency | Weekly |
| Feature Sources | Historical occupancy, weather, events, holidays |
| Explainability | SHAP values or similar for pricing decisions |

### 6.6 AI Anomaly Detection & Staff Support (Future/Deferred)

#### 6.6.1 Functional Requirements

The system shall use AI to detect anomalous patterns and provide actionable insights to parking staff.

**Detection Areas:**

- **Payment Fraud**: Unusual payment patterns, repeated failed transactions, suspicious refund requests  
- **Operational Anomalies**: Sensor failures, unusual occupancy patterns, barrier malfunctions  
- **Security Incidents**: Unauthorized access attempts, suspicious loitering patterns  
- **Revenue Leakage**: Unrecorded entries, bypass attempts, inconsistent audit trails

**AI Component:**

- Establish baseline "normal" behavior patterns for each parking lot  
- Score events against baseline to detect outliers  
- Categorize alerts by severity and type  
- Provide recommended actions for each alert

**Traditional System Component:**

- Route alerts to appropriate staff based on type and severity  
- Log all alert acknowledgments and resolutions  
- Enforce escalation procedures for unresolved alerts  
- Generate weekly anomaly reports for management review

**Integration Rules:**

- AI shall flag anomalies; staff shall investigate and take action  
- All AI alerts shall be logged with context and confidence score  
- Staff shall provide feedback on false positives to improve model  
- Critical security alerts shall bypass normal channels and alert management immediately

#### 6.6.2 Alert Tiers

| Tier | Description | Response Time | Action |
| :---- | :---- | :---- | :---- |
| Info | Pattern deviation, low confidence | 24 hours | Log for weekly review |
| Warning | Moderate anomaly, potential issue | 4 hours | Staff investigation |
| Critical | High confidence fraud/security issue | 15 minutes | Immediate management alert |

### 6.7 AI Chatbot for User Support

#### 6.7.1 Functional Requirements

The system shall provide an AI-powered chatbot to assist users with common queries and issues.

**Supported Use Cases:**

- FAQ: pricing, parking locations, operating hours, payment methods  
- Reservation status: check active reservations, extend time, cancel  
- Payment issues: explain charges, initiate refund requests, update payment methods  
- Account management: password reset, vehicle management, profile updates  
- Navigation: directions to parking lot, barrier location

**AI Component:**

- Understand natural language queries in Vietnamese, English
- Maintain conversation context across multiple turns  
- Recognize user intent and extract relevant entities  
- Escalate to human support when confidence is low or user requests

**Traditional System Component:**

- Execute actual reservation changes, cancellations, and refunds **only after user confirmation through the normal application flow; backend services perform the final business write**  
- Access user accounts and transaction history  
- Escalate complex issues to human support agents  
- Log all conversations for quality improvement

**Integration Rules:**

- Chatbot shall clearly identify itself as AI assistant  
- Users shall have immediate option to "Talk to human" at any point  
- Chatbot shall not make final decisions on refunds or disputes; only human agents can  
- All chatbot escalations shall include conversation history and user context

#### 6.7.2 Escalation Triggers

- User explicitly requests human support  
- Sentiment analysis indicates user frustration or anger  
- Query involves legal disputes, complaints, or refunds over a threshold  
- Chatbot confidence below 70% for intent recognition  
- Same query repeated 3+ times without resolution

<!-- INTEGRATION-START FR-AI -->

#### 6.7.3 AI Requirement Index

Status definitions are in the Baseline Revision Notice. Detailed constraints are defined once in §5.2; X items are recorded in Appendix E.

| FR ID | Status | Required behavior / canonical SRS location | Actor | User Story |
| --- | --- | --- | --- | --- |
| FR-AI-01 | R/A | Natural-language intents, entity extraction and multi-turn conversation: §6.7.1; missing/ambiguous mutation parameters remain to be clarified per the retained AI-team design. | Driver | US-D12 |
| FR-AI-02 | A | Answer static help using approved content; never present brainstorming or unresolved alternatives as active policy (BR-AI-01). | Driver | US-D12 |
| FR-AI-03 | A | Search through approved APIs using user-supplied location, vehicle and time; dynamic values come from returned results (BR-AI-01). | Driver | US-D12 |
| FR-AI-04 | C | Explain deterministic backend-ranked parking results without creating an LLM ranking formula. Personalized/model-based AI ranking is not used in the MVP. | Driver | US-D12 |
| FR-AI-05 | A | Read the signed-in Driver's bookings, session, saved vehicles, payment/refund status and cancellation eligibility through scoped APIs (BR-AI-02). | Driver | US-D12 |
| FR-AI-06 | A | Prepare a reservation draft and populate the normal application form for review/editing; saved-vehicle retrieval does not permit vehicle modification (BR-AI-03). | Driver | US-D12 |
| FR-AI-07 | A | Create a reservation only after final UI confirmation and fresh backend validation; changed terms require reconfirmation (BR-AI-03). | Driver | US-D12 |
| FR-AI-08 | A | Return actual backend reservation/payment status and hand off to ordinary checkout. Financial authority remains §6.1; chatbot mutation requires the confirmed user-review/backend-execution boundary. | Driver | US-D12 |
| FR-AI-09 | A | Distinguish empty results, authentication/authorization failure, invalid input, conflict, timeout and server errors; never fabricate success (BR-AI-05). | Driver | US-D12 |
| FR-AI-10 | R/A | Enforce §6.1 through allowlisted tools, resource authorization and no direct database/gate/policy/penalty access; see BR-AI-02/04. | Driver, Admin | US-D12 |
| FR-AI-11 | A | Explain backend-calculated charges and effective policy values; no hard-coded N or model-generated replacement calculation (BR-AI-01). | Driver | US-D12 |
| FR-BAS-10 | R | Vietnamese/English chatbot, AI identification and human handoff with context: §6.7.1/2. Handoff channel and handler need AI-DEP-07. | Driver, Operator | US-D12, US-O09 |
| FR-BAS-11 | C | Chatbot may read permitted information and prepare a proposed action, but any mutation requires user confirmation and backend execution. The final intent-by-intent allowlist remains an AI-team design detail. | Driver | US-D12 |
| FR-BAS-12 | F | Personalized/model-based AI recommendations remain future. MVP recommendations use deterministic backend ranking and the chatbot may explain that ranking; all available options remain accessible. | Driver | US-D18 |
| FR-BAS-13 | F | Future AI occupancy detection is defined once in §6.3, including uncertainty and human fallback. | Operator | US-O08 |
| FR-BAS-14 | F | Future capacity forecasting and model requirements are defined once in §6.5. | Owner | US-OW08 |
| FR-BAS-15 | F | Future anomaly detection/staff alert tiers are defined once in §6.6. | Operator, Owner | US-O10 |

<!-- INTEGRATION-END FR-AI -->

### 6.8 AI Implementation Phases


<!-- INTEGRATION-START AI-READY -->

#### 6.8.1 Readiness Conclusion

**AI foundation work can start now using synthetic data, mock APIs and provisional contracts. The entire AI MVP is not yet ready for end-to-end implementation or acceptance without decisions below.** This assessment is based on requirements only; no repository, deployed API, provider account, credential, labeled dataset or running model was supplied/verified. A capability in the SRS is not evidence that its backend already exists.

The project does not need to wait for every deferred allocation or architecture issue before building conversation state, API adapters, form handoff, error handling and tests. However, a real write cannot be enabled while the business outcome it changes remains undecided. In particular, the broad §6.7 write list and the limited imported chatbot scope are not silently reconciled.

| Workstream | Can begin now | Still required before integrated delivery |
| --- | --- | --- |
| Chatbot foundation | Yes: intent extraction, Vietnamese/English conversation scaffolding, missing-field clarification, tool registry, synthetic READ fixtures, safe errors | AI-DEP-01/02/05/06/09; actual provider credentials and contract approval. |
| FAQ and parking discovery | Yes with approved sample content and declared mock results | Knowledge approval, current policy/price/availability APIs and freshness handling; AI-DEP-03/05. |
| Personal-data assistant | Yes with fictional accounts and deny-access fixtures | Real auth/resource scope and minimal data contract; AI-DEP-02/06. |
| Reservation assistance | Yes: prepare/edit form and simulated confirmation results | Live writes need approved schema, UI confirmation, payment/hold outcomes and idempotency; AI-DEP-04. |
| Human support/escalation | Yes: UI entry point and mocked delivery result | Actual handler/channel, permissions and off-hours behavior; AI-DEP-07. |
| Image-input LPR | Yes: image upload UI, adapter and deterministic recognition fixtures | Recognizer, permitted labeled dataset, calibrated threshold/equality, manual correction and passage event contract; AI-DEP-08/09. No claim of98% accuracy from specification alone. |
| Personalized recommendations | Prototype only, independently from simple search narration | Release/scope decision C-17 plus ranking/data/evaluation contracts. |
| Occupancy detection / forecasting / anomalies | Design backlog only | Future scope remains §6.3/5/6; not a blocker for chatbot foundation. |

#### 6.8.2 Dependencies and Team Decisions

The named owners below are suggested responsible groups, not assignments to individuals. “Before” identifies the actual gate for a capability; it does not stop unrelated foundation work.

| ID | When it blocks | Suggested owner | Decision / input needed | Concrete exit evidence |
| --- | --- | --- | --- | --- |
| AI-DEP-01 | Before committing MVP scope; not before isolated prototype | BA + team lead + AI lead | Approve intent allowlist: FAQ/search/read/create-draft/create-confirmed; decide extend/cancel/refund-request/profile/vehicle/payment-method/password use cases. Decide personalized recommendations release. | Signed capability matrix with READ, WRITE, UI handoff or deferred per intent; C-16/17; FR-BAS-11/12. |
| AI-DEP-02 | Before real personal-data/API integration | Backend + security + AI | Specify authentication propagation, session identity, per-resource authorization, tool schemas, pagination, error codes, timeout and idempotency/result lookup; decide API facade ownership. | Versioned OpenAPI/JSON Schema and positive/negative fixtures; cross-account test passes; ARCH-DEF-02/05/07/09. |
| AI-DEP-03 | Before meaningful live search/recommendation | Parking/Reservation/Search teams | Select authoritative availability query, pricing quote result and distinction among requested/preferred/allocated slot; define stale-data indication. | Contract with lot/options, compatibility, time window, current data timestamp, quote/policy version and explainable ranking output; C-03/06/08/17. |
| AI-DEP-04 | Before live reservation writes | Reservation + Payment + frontend + AI | Agree create payload/response, confirmation binding, PENDING_PAYMENT behavior, Admin hold expiry, payment handoff, unknown/late callback results and concurrency behavior. | Reviewed form→confirmed command contract, idempotency, hold/quote deadlines and authoritative status lookup; C-04/09/18; TESTER-DEF-12/13; no model-side rule engine. |
| AI-DEP-05 | Before reliable FAQ/policy answers | BA + policy owner + AI | Select approved help content, publication owner, version/effective dates, retrieval access and how unresolved rules are excluded. | Approved knowledge set and regression Q&A; never index the whole conflict register as active policy. Live numerical terms come from backend. |
| AI-DEP-06 | Before external model receives real user content | AI + architect + security/project lead | Choose hosted/self-hosted provider/model, runtime location, secret management, budget, timeouts/fallbacks, data sent, masking, retention and deletion; no provider currently selected. | Configured development adapter can use synthetic data; production data handling and access approved; C-20, ARCH-DEF-02. |
| AI-DEP-07 | Before claiming complete baseline support experience | Operations + BA + frontend | Choose human-support channel/recipient role, availability, queue acknowledgement, allowed history transfer, escalation rules and return-to-app fallback. | Demonstrable Talk to human route with consent/access restrictions and off-hours behavior; Operator is proposed handler, not a confirmed fifth actor or confirmed assignment. |
| AI-DEP-08 | Before model-backed LPR integration and evaluation | LPR/AI + backend + QA | Choose recognizer, plate normalization/format, image schema, model/version, confidence scale/equality, correction flow, event producer and representative licensed test images. | Image→plate/confidence contract plus labeled day/night/invalid/low-confidence fixtures; C-14/15; ARCH-DEF-03; TESTER-DEF-32/35. |
| AI-DEP-09 | Before MVP acceptance/release | QA + AI + project lead | Set representative Vietnamese/English scenarios, failure/security fixtures, measurable acceptance thresholds, cost/latency budget and interpretation of A/B/shadow requirements in demo. | Approved evaluation protocol; model confidence is not a calibrated percentage without evidence; §6.9.4 and TESTER-DEF-27/32 remain unchanged. |
| AI-DEP-10 | Before enabling deferred AI features | BA + data/AI + architect | Approve release, data consent/history, ranking ownership, occupancy labels, forecasting/anomaly datasets and human operational response. | Separate approved feature plan; §6.3/5/6 and personalized scope C-17. Not a prerequisite for FAQ or reservation-form prototype. |

#### 6.8.3 Proposed Tool / Contract Inventory

Tool names describe capabilities, not existing deployed endpoints or final service boundaries. All schemas must define timezone, money units/currency, nullable values, enum compatibility, correlation/request IDs and error semantics where applicable. APIs authorize every request independently of prompts. Do not expose client-supplied identity as a tool override.

| Capability | Effect | Minimum contract content | Readiness / owner dependency |
| --- | --- | --- | --- |
| searchHelpArticles | READ | Query, optional facility; approved snippets + content revision + scope/effective date | Synthetic approved knowledge set; BA publishing owner TBD. |
| searchParkingLots | READ | Location/vehicle/time; backend options, real availability, quoted price/currency, requested/allocated distinction, freshness | Parking/Reservation/Search query owner unresolved; use deterministic fixture and declared timestamp. |
| recommendParkingLots / recommendReservationOptions | READ, optional | Search context; ranked options plus backend reasons | May reuse search result in prototype; do not claim §6.4 personalization is delivered. |
| getMySavedVehicles | READ | Authenticated session; scoped vehicle IDs/plate/type | User domain; exclude unrelated data and identity documents. |
| getMyActiveBookings / getMyBookingDetails | READ | Optional booking ID; authorize ownership; actual lifecycle and timing | Reservation domain; baseline enums preserved; unknown new states rendered safely. |
| getMyCurrentParkingSession | READ | Authenticated session; actual recorded location/times and status | Session API ownership must be agreed; no invented live occupancy. |
| getMyPaymentStatus / getMyRefundStatus | READ | Booking/transaction ID; authorized verified financial states | Payment domain; distinguish request approval from actual settlement/refund. |
| checkMyCancellationEligibility | READ | Booking ID; authoritative eligibility/reason/estimated refund and applicable policy version | Reservation/Payment collaboration; result is provisional until cancellation execution. |
| prepareReservationForm | UI draft, not domain WRITE | Chosen backend option, saved vehicle, local timezone/offset, start/end; editable form | Frontend-owned review handoff; can be built without creating a booking. |
| createReservation | WRITE after UI confirmation | Confirmed vehicle/lot/mode/requested zone/slot/time/terms, auth, idempotency; actual booking ID/state/payment requirement/hold deadline | Exact schema and orchestration pending AI-DEP-04; revalidate current conditions and reject/reconfirm stale terms. |
| cancel/extend/refund-request/profile/vehicle/payment-method updates | Not enabled in limited candidate pilot | Schemas, authority and confirmation not yet approved | C-16 determines which are direct backend actions, UI handoff or deferred. Existing baseline listing is retained. |
| LPR recognize image / correct plate | Recognition READ-like analysis; correction audited WRITE by Operator | Image/event ID/time; normalized plate/confidence/model version; human correction record | AI-DEP-08 determines producer and ownership. Recognizer cannot grant gate access. |

#### 6.8.4 Proposed Delivery Gates

1. **Foundation with mocks:** implement approved-sample FAQ, intent/parameter collection, account-isolated fixtures, search responses, reservation form preparation, API failures and human-handoff UI. Label all fixture results as test data. No actual reservation, payment or gate claim is made.
2. **Read integration:** satisfy AI-DEP-02/03/05/06; verify identity isolation, stale/empty/error responses and numeric values from backend. Read integrations may ship internally while mutation scope remains open.
3. **Confirmed reservation integration:** satisfy AI-DEP-01/04 for this action; execute only after final form confirmation, verify returned PENDING_PAYMENT and checkout handoff, and test duplicate/unknown/expired outcomes. Allocation, price and refund rules remain backend-owned.
4. **Support and LPR integration:** independently satisfy AI-DEP-07 and AI-DEP-08. Do not wait for real camera hardware if the team confirms the image-upload MVP already stated in §6.2.
5. **MVP acceptance:** satisfy AI-DEP-09 and applicable original §6.9 checks. Required scenarios include cross-account access attempts, malicious instructions inside retrieved help/API text, stale confirmed forms, duplicate writes, provider timeout, model outage, LPR equality/low confidence and successful human escalation. Report measured results; do not lower original targets without recorded approval.

A minimal decision package to unblock meaningful integration is: agreed chatbot intent matrix; versioned tool contracts/auth boundary; frontend confirmation contract; approved knowledge pack; provider/data-handling choice; LPR input/model/evaluation choice; human-support route. Core business-rule decisions are additionally required only when a corresponding live mutation, price explanation or gate outcome depends on them. No endpoint names, deployed infrastructure, model quality, API readiness or budget sufficiency are assumed by this document.

<!-- INTEGRATION-END AI-READY -->

### 6.9 AI Governance & Risk Mitigation

#### 6.9.1 Model Governance

- All AI models shall be version-controlled and auditable  
- Model decisions affecting users shall be explainable  
- Regular bias audits shall be conducted on recommendation and pricing models  
- Human-in-the-loop required for all financial and legal decisions  
- Model performance shall be monitored continuously with automated alerts for degradation

#### 6.9.2 Privacy & Data Protection

- AI training data shall be anonymized and aggregated where possible  
- User consent shall be obtained for personalized features  
- Data retention policies shall apply to AI training data  
- Right to deletion shall extend to AI-derived profiles  
- Regular privacy impact assessments for new AI features

#### 6.9.3 Fallback & Degradation

| AI Feature | Fallback Strategy |
| :---- | :---- |
| License Plate Recognition | Manual entry by attendant, QR code fallback |
| Spot Status Detection | Existing IoT sensors, staff manual updates |
| Recommendations | Distance-based default ranking |
| Capacity Forecasting | Rule-based pricing and staffing |
| Anomaly Detection | Traditional threshold-based alerts |
| Chatbot | FAQ page, email support, phone support |

#### 6.9.4 Testing & Validation

- AI models shall undergo rigorous testing before production deployment  
- A/B testing required for all user-facing AI features  
- Shadow mode deployment for validation before full rollout  
- Regular accuracy testing with holdout datasets  
- User feedback loops for continuous improvement

---

## Appendix

### A. Glossary

| Term | Definition |
| :---- | :---- |
| OTP | One-Time Password |
| MFA | Multi-Factor Authentication |
| CCCD | Citizen Identity Card (Vietnam) |
| GPLX | Driver's License (Vietnam) |
| CSGT | Traffic Police (Vietnam) |
| RTO | Recovery Time Objective |
| RPO | Recovery Point Objective |
| SLA | Service Level Agreement |
| NPS | Net Promoter Score |
| CSAT | Customer Satisfaction Score |
| MQTT | Message Queuing Telemetry Transport |
| IoT | Internet of Things |
| LPR | License Plate Recognition |
| AI | Artificial Intelligence |
| ML | Machine Learning |
| OCR | Optical Character Recognition |
| Allocation Lead Time | Configurable period before reservation start during which SmartPark begins allocation processing. |
| Allocation Time | Calculated timestamp at which a reservation enters its configured Allocation Lead Time. |
| Reservation Protection Window | Configurable period before reservation start during which the system/operator applies reservation-protection actions. |
| Backup Capacity | Capacity reserved for future reservation fulfillment and excluded from ordinary walk-in availability. It may be fixed backup capacity or protected through the dynamic reservation-protection behavior. |
| Physical State | The current observed physical condition of a parking spot, such as AVAILABLE or OCCUPIED. |
| Reservation / Protection State | The reservation-related state associated with a spot or capacity, such as RESERVED, PROTECTED, or BACKUP. |
| Reservation Lifecycle State | The business lifecycle of a reservation, including PENDING_PAYMENT, CONFIRMED, ALLOCATED, PARKING, COMPLETED, and applicable exceptional states such as EXPIRED, NO_SHOW, CANCELLED, and UNFULFILLABLE. |
| Physical Arrival | The driver has reached the parking lot entrance or gate but has not yet physically occupied a parking slot; the driver may still be prevented from entering if no applicable capacity is available. |
| Payment Hold | A temporary PENDING_PAYMENT claim that blocks incompatible competing claims during checkout and counts against available capacity while the hold is active; it is not the RESERVED business state. |
| Reallocation Request | A driver-submitted request to obtain another applicable allocation; submission does not guarantee approval. The default limit is two requests per reservation and the limit is configurable by Admin/Owner policy. |

### B. Baseline Revision Note

This baseline was compiled from the previous SmartPark draft and the appended architecture material. The restructuring keeps the existing content direction, preserves unresolved `N` placeholders and other blank/unfinished areas for team completion, and incorporates only the C-01–C-23 business decisions recorded for v0.8.5, including the current MVP payment channels, reservation/capacity rules, policy hierarchy, monthly-pass scope, emergency/appeal authority, deterministic recommendation behavior, policy-version preservation, and approved Owner onboarding and vehicle scope.


### C. Deferred Architecture Alignment Notes — v0.8

The microservice architecture direction in this baseline has been intentionally preserved as provided. The following items were identified during the v0.8 BA refinement but are **deferred and intentionally not resolved in this revision**:

1. **Service boundary ownership:** The initial service list includes User, Parking, Reservation, Payment, Notification, IoT, and Search/Recommendation services, while other capabilities mentioned elsewhere (such as Policy/Configuration, Audit, Operations/Emergency, and AI/Chatbot) do not yet have equally explicit service ownership.
2. **IoT / camera / LPR boundary:** The IoT Service currently includes camera and LPR responsibilities, while the external-entity section separately identifies Camera and ANPR/LPR. The final ownership boundary between the IoT Service and external recognition/provider integrations should be clarified later.
3. **Search / Availability ownership:** Search/Recommendation Service includes parking-availability search while Parking Service owns real-time parking status and Reservation Service owns reservation availability. The final source-of-truth and query responsibility split should be clarified later.
4. **Pricing ownership:** Payment Service currently includes fee calculation, while the wider requirements also contain pricing and dynamic-pricing responsibilities. The final ownership of pricing/policy calculation should be clarified later.
5. **Authentication ownership:** API Gateway lists authentication/authorization responsibilities while User Service also contains authentication/authorization support. The final division between gateway-level security and User Service responsibilities should be clarified later.
6. **Event publisher ownership:** The event catalogue contains events related to violations, emergency actions, and other capabilities that are not yet represented as dedicated service boundaries. Publisher ownership should be finalized later.
7. **AI service boundary:** AI/LLM functionality is identified as an external integration and as an AI capability, but a dedicated AI/Chatbot service boundary is not yet explicitly established in the initial microservice list. This should be clarified during API/AI architecture refinement.
8. **Infrastructure alignment:** Redis/message-queue infrastructure appears in the NFR section while Redis is not identified in the current technology baseline. This is deferred to the architecture/NFR review and is intentionally not changed here.

These notes are for follow-up architecture work only and do not change the v0.8.5 requirements baseline.

### D. Deferred Tester Findings and Follow-up Backlog

The following items were identified during Tester validation of SRS v0.7/v0.8 but are intentionally deferred from the current baseline because they do not block the immediate Business Rules, API Contract, or AI Chatbox foundation work.

Items not directly resolved by C-01–C-23 remain **OPEN / DEFERRED** and shall be revisited during subsequent SRS refinement. Findings directly resolved by a recorded decision remain in this appendix for traceability and are annotated as closed; any remaining detailed implementation refinement stays open.

---

#### D.1 Reservation & Allocation Refinement

| ID | Deferred Item | Required Follow-up |
|---|---|---|
| TESTER-DEF-01 | Complete Reservation lifecycle/state definition | Review whether additional reservation states are required beyond `PENDING_PAYMENT`, `CONFIRMED`, `ALLOCATED`, `PARKING`, `COMPLETED`, `EXPIRED`, `CANCELLED`, and `UNFULFILLABLE`. |
| TESTER-DEF-02 | Allocation representation | Decide whether Allocation is a separate persisted domain concept, a reservation-owned allocation record, or another representation. |
| TESTER-DEF-03 | Backup-capacity assignment mechanism | Define how dynamically calculated backup capacity is assigned without allowing incompatible reservations to consume the same physical capacity. The business invariant is already established; implementation mechanism remains open. |
| TESTER-DEF-04 | Reallocation lifecycle refinement | Clarify detailed rules for multiple reallocations, driver requests for reallocation, operator/system decisions, and final fulfillment failure. Current maximum driver reallocation-request count is 2. |
| TESTER-DEF-05 | Physical-state transition catalogue | Produce the complete physical-state/reservation-state transition diagram covering cases such as `AVAILABLE`, `OCCUPIED`, `UNKNOWN`, `MAINTENANCE`, and `UNAVAILABLE`. |
| TESTER-DEF-06 | Reservation/slot state completion | Define detailed behavior when reserved/protected capacity becomes occupied, unknown, unavailable, or enters maintenance. |

---

#### D.2 Policy Configuration Refinement

| ID | Deferred Item | Required Follow-up |
|---|---|---|
| TESTER-DEF-07 | Policy hierarchy implementation semantics | Define how inheritance, specialization, override, unset values, invalid values, and effective-policy resolution are represented and evaluated. Current hierarchy is: Admin → Owner/Parking Lot → Zone → Booking Context. |
| TESTER-DEF-08 | Configurable-policy catalogue | Consolidate all configurable parameters into one policy catalogue identifying their owner, scope, default value, allowed range, and whether lower scopes may override them. |
| TESTER-DEF-09 | Refund-policy structure | Define the conceptual structure of owner-configured refund policies without prematurely fixing implementation/database schema. |
| TESTER-DEF-10 | Conflict-policy configuration | Consolidate configurable conflict-resolution behavior such as reallocation, backup capacity, queueing, operator intervention, and refund handling. |
| TESTER-DEF-11 | Backup-capacity configuration | Define the detailed calculation and release rules for dynamically calculated backup capacity. |

---

#### D.3 Reservation Edge Cases

| ID | Deferred Item | Required Follow-up |
|---|---|---|
| TESTER-DEF-12 | Payment success after payment-hold expiry | Closed by C-09: late payment succeeds when capacity is still free; otherwise the backend race winner keeps the capacity and the losing paid attempt is notified and eligible for refund/reconciliation. |
| TESTER-DEF-13 | Late-created reservation | Define allocation behavior when a reservation is created after its calculated Allocation Time. |
| TESTER-DEF-14 | Protection Window and pending payment interaction | Closed at business-rule level by C-04/C-06: payment hold and Protection Window are separate; PENDING_PAYMENT consumes capacity during its active hold, while reservation protection applies to confirmed reservation capacity. Exceptional timing/API details remain refinement. |
| TESTER-DEF-15 | Simultaneous arrival | Closed by C-18: Operator handles the simultaneous-arrival tie-break and records the decision; detailed event/API representation remains refinement. |
| TESTER-DEF-16 | Alternative-allocation refusal | Define whether a driver may reject an alternative allocation and what happens to the reservation afterward. |
| TESTER-DEF-17 | Multiple affected reservations | Extend physical-arrival conflict rules from the two-driver example to cases involving three or more affected reservations. |

---

#### D.4 Terminology and Domain Clarification

| ID | Deferred Item | Required Follow-up |
|---|---|---|
| TESTER-DEF-18 | Guest vs. walk-in terminology | C-01 resolves account requirement for physical parking, but detailed fee-category terminology remains a refinement. |
| TESTER-DEF-19 | Reservation fulfillment terminology | Standardize the distinction among reservation, allocation, parking session, physical occupancy, and fulfillment. |
| TESTER-DEF-20 | Arrival terminology | Maintain the current definition of physical arrival as reaching the parking-lot entrance/gate before occupying a slot, but formalize its observable system event during later workflow/API refinement. |

---

#### D.5 Scope and Functional Requirement Gaps

| ID | Deferred Item | Required Follow-up |
|---|---|---|
| TESTER-DEF-21 | Operational issues requirements | Define the functional scope, actors, states, and workflows for operational issues if they remain in MVP scope. |
| TESTER-DEF-22 | Basic violation handling | Define violation lifecycle, evidence, authority, operator actions, and outcomes if retained in MVP scope. |
| TESTER-DEF-23 | Appeals/support | Define minimum appeal/support workflow if retained in MVP scope. |
| TESTER-DEF-24 | Emergency handling | Define emergency-event authority, affected capacity, operational actions, and recovery behavior where emergency handling remains part of the system scope. |
| TESTER-DEF-25 | Dynamic pricing | C-08 resolves MVP ownership and time-block configuration; event/holiday/demand triggering remains future and the remaining tariff-detail catalogue is refinement. |
| TESTER-DEF-26 | AI parking recommendations | C-17 resolves the MVP behavior as deterministic backend ranking plus chatbot explanation; personalized/model-based ranking remains future. |

---

#### D.6 Non-Functional Requirements Review

| ID | Deferred Item | Required Follow-up |
|---|---|---|
| TESTER-DEF-27 | MVP performance targets | Review current throughput, latency, concurrency, and database targets against the actual project scope and deployment environment. |
| TESTER-DEF-28 | Availability and reliability targets | Review whether current availability, RTO, and RPO values are intended for the student MVP or a future production deployment. |
| TESTER-DEF-29 | Scalability targets | Reconcile infrastructure/scaling targets with the selected deployment architecture and project scope. |
| TESTER-DEF-30 | Redis / message infrastructure requirements | Confirm whether Redis and other infrastructure components are actual project dependencies or future architectural options. |
| TESTER-DEF-31 | Observability / operations targets | Review 24/7 on-call, incident-response, retention, and monitoring requirements for applicability to the project environment. |
| TESTER-DEF-32 | AI model performance targets | Reassess AI/LPR accuracy and performance requirements against the actual MVP test dataset and simulated-input approach. |

---

#### D.7 IoT and Physical Integration Clarification

| ID | Deferred Item | Required Follow-up |
|---|---|---|
| TESTER-DEF-33 | IoT scope consistency | Clarify the distinction between parking lots potentially having physical sensors and SmartPark's MVP not implementing direct IoT integration. |
| TESTER-DEF-34 | Sensor/manual reconciliation | Finalize source-of-truth behavior when sensor, simulation, camera, and operator observations disagree. |
| TESTER-DEF-35 | Gate/barrier integration | Define the exact role of physical gate integration and whether entry control is simulated or implemented through a real adapter. |

---

#### D.8 Architecture Alignment Review

The microservice architecture is currently being refined separately and is intentionally **not automatically resolved by this SRS revision**.

The following architecture alignment items remain deferred:

| ID | Deferred Item | Required Follow-up |
|---|---|---|
| ARCH-DEF-01 | Service boundary validation | Review whether the current User, Parking, Reservation, Payment, Notification, IoT, and Search/Recommendation service boundaries correctly reflect business ownership. |
| ARCH-DEF-02 | AI/Chatbot service boundary | Determine whether AI Chatbox belongs to a dedicated AI service or another service boundary. |
| ARCH-DEF-03 | IoT vs. LPR ownership | Review whether camera, sensor, and LPR responsibilities should remain within the current IoT Service or be separated by capability. |
| ARCH-DEF-04 | Search/Recommendation ownership | Determine whether search and recommendation should remain combined or be separated into distinct capabilities. |
| ARCH-DEF-05 | Availability ownership | Clarify which service owns authoritative availability calculations when Parking and Reservation services both require availability information. |
| ARCH-DEF-06 | Pricing ownership | Determine whether pricing/fee rules belong entirely to Payment Service or should be represented by a separate Pricing capability. |
| ARCH-DEF-07 | Authentication ownership | Review the division of authentication/authorization responsibilities between the API Gateway and User Service. |
| ARCH-DEF-08 | Event ownership | Validate publishers, consumers, event boundaries, and ownership of integration events. |
| ARCH-DEF-09 | Cross-service consistency | Define which critical workflows require synchronous coordination, eventual consistency, retries, idempotency, outbox patterns, or compensating actions. |

---

<!-- INTEGRATION-START DEF-INDEX -->

#### D.8.1 Integration Follow-up Crosswalk — Status Unchanged

| Existing backlog | Review location | Status after integration |
| --- | --- | --- |
| TESTER-DEF-01–06 | §3.4.12, BR-CAP/RES, C-03/06/18 | OPEN / DEFERRED; candidate constraints do not settle full state/allocation models. |
| TESTER-DEF-07–11 | §5.2.3, C-04/06/12/22 | OPEN / DEFERRED where detailed representation remains unresolved; v0.8.5 confirms policy hierarchy, capacity accounting and accepted-booking policy/price preservation. |
| TESTER-DEF-12–17 | C-09/18, AI-DEP-04 | TESTER-DEF-12 RESOLVED by C-09; TESTER-DEF-13–17 remain OPEN / DEFERRED for detailed arrival/allocation edge cases. |
| TESTER-DEF-18–20 | C-01/03/05/18 | TESTER-DEF-18 RESOLVED by C-01; TESTER-DEF-19–20 remain OPEN / DEFERRED for remaining terminology/event-detail work. |
| TESTER-DEF-21–26 | §3.7.6, C-08/12/13/17 | C-08, C-12 and C-17 MVP scope points are resolved; detailed workflow/test criteria not explicitly resolved remain OPEN / DEFERRED. |
| TESTER-DEF-27–32 | §6.8, C-14/20, Appendix E | OPEN / DEFERRED; original NFRs preserved and not performance-tested. |
| TESTER-DEF-33–35 | C-07/14/15, AI-DEP-08 | TESTER-DEF-33 resolved for MVP fixture-only scope; TESTER-DEF-34–35 remain OPEN / DEFERRED for detailed observation reconciliation and event contracts. |
| ARCH-DEF-01–09 | Appendix C/D.8, AI-DEP-02/03/04/06/08 | OPEN / DEFERRED; logical contracts do not choose service deployments or consistency mechanisms. |

<!-- INTEGRATION-END DEF-INDEX -->

### D.9 Deferred Status Rule

Deferred items in this appendix:

- are **not considered resolved requirements**;
- shall not be silently implemented as fixed business rules;
- may be refined by the BA, Tester, Architect, Owner, or relevant module team;
- must be explicitly marked as resolved, superseded, or moved to Future/Out of Scope when revisited;
- shall not block the current Business Rules, API Contract, or AI Chatbox foundation work unless a later implementation task directly depends on them.

Any future resolution that changes an existing requirement shall be recorded as a new baseline revision rather than silently changing the current baseline.<!-- INTEGRATION-START CONFLICTS -->

### E. Integration Conflicts, Decisions and Change Register

#### E.1 Decision Register

The following C-01–C-23 decisions are incorporated into the v0.8.5 baseline. The register records the resolved business direction; detailed alternatives that remain proposals are retained in their original A/X/F locations and are not treated as normative.

| ID | Topic | v0.8.5 confirmed decision | Affected baseline areas |
| --- | --- | --- | --- |
| C-01 | Guest, walk-in and mandatory account | A person does not need an account to physically park as a walk-in. Reservations and account-scoped customer transactions require an account. Walk-in parking is governed by physical authorization and current lot conditions. | §1.2, §2.3.1, §3.4.3, §3.5.1, §3.8 |
| C-02 | Device authority | Operator manages devices within parking lots assigned by Owner. Owner device reporting is future/deferred while IoT is deferred. Admin/Owner do not perform lot-device management. | §2.3, §2.3.1, FR-LOT-05/07 |
| C-03 | Reservation mode and allocation timing | Specific Slot, Zone and Capacity remain the canonical reservation modes. Specific Slot is a preference, not an absolute exact-slot guarantee. Allocation begins at Reservation Start − Allocation Lead Time, using the allocation and protection behavior defined by the baseline. | §3.4.1, §3.4.6, BR-RES-01, FR-RES-03 |
| C-04 | Policy ownership and payment hold | Admin defines defaults and bounds for Owner-configurable policies/variables. Owner may override within scope. Admin default applies to always-active policies without an override; optional policy activation follows the Owner choice. Payment hold default remains 5 minutes. | §3.2.4, §3.4.3, §3.7, §5.2.3 |
| C-05 | Late arrival and no-show | EXPIRED is only unpaid reservation-hold expiry. Without an Owner late-tolerance policy, a reservation remains valid until end time. With enabled Late Arrival Tolerance, no authoritative arrival by start + tolerance produces NO_SHOW and releases protected capacity similarly to cancellation. | §3.4.2, §3.4.10, BR-RES-03 |
| C-06 | Capacity accounting and backup | Available Capacity = Total Capacity − Occupied − Protected − Pending Payment − Backup. Pending payment consumes availability during its active hold. Backup capacity is configurable; Operator-designated backup slots remain backup. The dynamic protection backup pool is reservation-protection behavior. Occupancy projection uses a configurable Occupancy Validity Timespan, default 4 hours. | §3.4.3–5, §5.2.3, BR-CAP-02/04 |
| C-07 | Observation authority | Manual override is authoritative only when initiated by an authorized actor under an approved operational condition and is audited. No blanket manual-over-sensor precedence is established. | §3.2.2, FR-INC-06, BR-EMERG-01 |
| C-08 | Dynamic pricing | MVP supports Owner-configured time-block pricing. Event/holiday/demand-driven automatic dynamic pricing remains future. | §3.5.2, FR-BAS-05 |
| C-09 | Late payment success | After hold expiry, payment may still succeed if the requested capacity remains free. If a competing claim has already won the backend race, the losing paid attempt is notified and eligible for refund/reconciliation. | §3.4.3, FR-PAY-05, BR-PAY-04 |
| C-10 | Payment channels and retry | MVP digital channels are VNPay, MoMo and ZaloPay. CASH_COLLECT supports Operator-recorded manual cash collection. Retry default remains 3. | §3.5.3, §5.2.3, FR-PAY-03 |
| C-11 | Identity and document collection | CCCD/GPLX are not collected or stored in this phase. Generic protection applies only to data actually processed. Proposed evidence/review workflows remain proposals. | §3.1.2, §4.3, BR-AUTH/VEH rules |
| C-12 | Appeals and refund authority | Appeal flow is appeal → evidence → Operator approval → refund. Owner may handle refunds after Operator escalation. ACCEPTED is not REFUNDED. | §3.7.6, FR-INC-05, FR-PAY-06, BR-VIOL-04 |
| C-13 | Emergency operations | Emergency release/priority handling is permission-gated, audited and recoverable. No automatic forced relocation or impossible over-capacity admission is promised. | FR-OPS-01/02, BR-EMERG-02/03 |
| C-14 | Configurable business values | Security-critical values remain constrained. Business-policy values may be configurable N within Admin-defined bounds. Confirmed examples include retry limit default 3, notification timing N and late tolerance N. Unresolved LPR score/equality details remain unresolved. | §5.2.3, BR-AUTH-02, LPR-related X items |
| C-15 | Simulation and physical integration | MVP uses deterministic test fixtures. A product simulator is optional only if needed and feasible. Direct real-IoT/gate integration remains deferred. | §1.2, §2.11, FR-SIM-01, Appendix D |
| C-16 | Chatbot mutation scope | Chatbot may read and prepare actions; user confirmation is required before mutation, and backend executes the business write. Detailed intent allowlist remains AI-team scope. | §6.7, FR-BAS-11 |
| C-17 | MVP recommendations | MVP uses deterministic backend ranking with chatbot explanation. No LLM-generated or personalized model ranking is used; all available options remain accessible. | §1.2, §6.4, FR-AI-04, FR-BAS-12 |
| C-18 | Reallocation request limit and simultaneous arrival | Default driver-submitted reallocation limit is 2; Admin and Owner may configure it within allowed bounds. Operator handles simultaneous-arrival tie-breaks and logs the decision. | §3.4.6/7, §5.2.3 |
| C-19 | Monthly pass | Monthly pass is MVP, using the Appendix F rolling prepaid design with when-space-available and guaranteed capacity/slot entitlements. The empty Monthly Management Fee placeholder is removed from §3.5.1. | §1.2, §3.5.1, Appendix F |
| C-20 | Retention and audit | Audit logs: 7 years. Operational logs: 90 days hot + 1 year cold. Images: maximum 30 days with authorized holds. Conversations are logged. Categories remain distinct. | §4.3, §4.8, §6.2.3, §6.7 |
| C-21 | Exit billing | Actual recorded exit determines final price. The quote/clearance/requote proposal remains inactive. | §3.5.1, FR-PAY-11, BR-PAY-05 |
| C-22 | Policy version effect | Accepted bookings preserve their accepted policy/price version. Later policy revisions apply to future applicable transactions and do not retroactively reprice accepted bookings. | §3.7.2, FR-POL-05, BR-POL-03 |
| C-23 | Motorcycle, EV slot type and Owner onboarding | Motorcycle is supported. EV charging is not implemented; an EV-compatible slot is only a slot type/attribute for occupancy/allocation/payment. Corporate email-domain enforcement is not required. Owner registration collects company name, email and phone and requires Admin approval. | §1.2, §3.1, FR-AUTH-05, BR-VEH-03 |

#### E.2 Recorded Changes

| Change ID | Location | Actual change | Baseline protection |
| --- | --- | --- | --- |
| CH-01 | Revision notice and status convention | Revised document metadata to v0.8.5 and recorded C-01–C-23 as incorporated baseline decisions. | Remaining A/X/F items are still non-normative unless separately resolved. |
| CH-02 | §2.3.1 permission matrix | Applied resolved device, policy, override, cash and appeal authorities. | Unresolved proposal rows remain separate. |
| CH-03 | §2.4.1 integration naming | Mapped canonical logical names to existing entities/providers. | No new provider or settled architecture boundary. |
| CH-04 | §3.0 User Stories | Updated C-01/C-02/C-17 ownership and monthly-pass story framing. | Only conflict-related stories were revised. |
| CH-05 | Domain FR tables in §3 and §6 | Promoted resolved C-01–C-23 behavior to C where directly affected; retained unrelated A/X/F requirements. | No unrelated proposal was silently promoted. |
| CH-06 | §5.2 Business Rules | Filled empty section with retained39 original BR IDs plus14 additional rule IDs, constraints, boundary scenarios and policy register. | Obsolete architecture and hard-coded examples not adopted; actual approval remains separate. |
| CH-07 | §6.8 AI implementation/readiness | Filled empty phase section with conditional start assessment,10 dependency entries, contract inventory and delivery gates. | No claim that APIs, model/provider or datasets already exist. |
| CH-08 | D.8.1 follow-up crosswalk | Updated only directly resolved tester findings (for example DEF-12, DEF-18 and DEF-33). | Remaining deferred findings stay open/deferred. |
| CH-09 | Appendix E conflicts/change log | Converted C-01–C-23 into an explicit resolved-decision register for v0.8.5. | Alternatives/proposals remain documented in their original non-normative locations. |
| CH-10 | Appendix F monthly-pass design | Promoted the documented rolling prepaid monthly-pass design to MVP under C-19 and removed the empty monthly-management-fee placeholder. | Numeric examples remain Dev/QA fixtures. |
| CH-11 | Appendix G review checks | Added coverage/preservation and dependency validation criteria. | Document checks, not execution of application tests. |
| CH-12 | §2 code-block rendering only | Added4 missing closing fences after architecture/service/event/adapter lists, each explicitly marked. | No original characters or business wording removed; removing inserted blocks/fences restores original text. |

#### E.3 Existing Editorial and Structural Findings

The duplicate §2.5, §2.6 and §3.4.7 headings, blank §2.2/§2.8 diagram areas, unnumbered architecture subheadings, monthly fee placeholder and citation-rendering artifacts already exist in v0.8. They were not created by the new catalogs and are not removed here. Four missing code-block closing fences were added solely to make the existing headings and inserted tables render correctly; this is the only formatting repair to the original flow. Other cleanup can be performed as a separately recorded editorial revision. No new external-source column or bibliography is added.

No legal statement, SLO, service boundary, Kafka/Redis choice, or unrelated Future/deferred requirement is silently replaced. This revision resolves only the C-01–C-23 business decisions explicitly incorporated above.

<!-- INTEGRATION-END CONFLICTS -->

<!-- INTEGRATION-START MONTHLY -->

### F. Monthly Pass — MVP Design

This appendix defines the v0.8.5 MVP monthly-pass design. Monthly passes are account-scoped prepaid plans. The MVP supports two entitlement types: **when-space-available** and **guaranteed capacity/slot**. The rules below are the confirmed monthly-pass baseline adopted for C-19; unrelated future AI, IoT, charging and architecture proposals remain unchanged elsewhere in the SRS.

#### F.1 Single Monthly Rule Definition

| Rule | Topic | Confirmed MVP behavior |
| --- | --- | --- |
| M-01 | Rolling period and anchor | Prepaid calendar-month cycle, not first-of-month billing. For a future purchase, start S is the chosen local date at00:00. For start-now, S is backend entitlement commitment time A. End E is the same local day/time next month, clamped to that month's last day if necessary. Preserve ORIGINAL anchor day/time across consecutive renewals: Jan31→Feb28(or29)→Mar31, not Mar28. Feb15→Mar15→Apr15. Rights cover [S,E); at E the old period is no longer valid. Do not treat one month as30days. Full-price calendar periods only, including a clamped February period; no partial-month sale in the initial extension. Store the contract timezone with the original day/time; later lot-timezone edits cannot shift purchased periods. If additional timezones with daylight-saving changes are supported, their ambiguous/missing-time rule must be defined first. |
| M-02 | Payment deadline and late renewal | Let order creation O, configurable hold N and deadline H. Future purchase/renewal: H=min(O+N,S); if no payable interval remains, do not create an activatable order. Start-now: H=O+N. Activation requires authoritative full-settlement validation and entitlement commit A<H; exactly H is too late. Renewal new S is current E. No auto-charge or next-period debt is assumed. Unpaid/late renewal expires; subsequent purchase after expiry uses a new start and anchor, not retroactive renewal. A renewal request opens at E−MONTHLY_RENEWAL_WINDOW. If multiple periods are already paid, append to the final committed E. No order means no free indefinite hold of the next period; an order protects only until H and only after resource validation. |
| M-03 | Money and state exceptions | Separate order(PENDING_PAYMENT/FULFILLED/EXPIRED/CANCELLED), transaction(PENDING/SUCCESS/FAILED/UNKNOWN) and entitlement(SCHEDULED/ACTIVE/EXPIRED/CANCELLED). FULFILLED means payment AND rights were successfully committed. Refund states are REQUESTED/APPROVED/PROCESSING/COMPLETED/REJECTED; approval is not payout. A provider paidAt before H but backend commit at/after H enters refund/reconciliation; never restores released capacity automatically. Under/overpayment, currency/order/payee mismatch do not auto-grant. Failed transactions grant nothing; UNKNOWN needs lookup before retry. Duplicate callback is idempotent; a genuinely separate duplicate charge is recorded and refunded separately. Valid paid money with failed capacity/entitlement commit enters reconciliation/refund, not disappearance. Bank success screenshot is not settlement proof. A single provider transaction cannot fund multiple orders. A later failed callback cannot erase SUCCESS; chargeback/reversal is a separate reconciliation event. Entitlement validity is checked against timestamp and revocation at use, not dependent on a scheduler updating the label on time. |
| M-04 | Capacity and occupied vehicle boundaries | When-space-available grants no reservation of capacity. Guaranteed-capacity/slot plans reserve only the approved resource/interval and exclude incompatible ordinary claims; do not count both guarantee and its occupied vehicle twice. Gate authorization rechecks vehicle, lot, period and actual compatible space; no admission into impossible occupancy. An occupied vehicle remains counted after pass expiry. Time before S/at-or-after E and separate fees are charged under approved ordinary tariff rules; covered intervals are not charged again. FREE_PARKING_GRACE_PERIOD is evaluated once for the physical session, never restarted at each monthly boundary. Continuous renewed coverage has no artificial uncovered gap. Physical departure, not entitlement expiry, releases occupancy. |
| M-05 | Cancellation and refund behavior | Owner approval and confirmed payout remain separate. Before S: proposed refundable amount P×configured prestart rate. During [S,E): proposed refundable amount P×((E-C)/(E-S))×R for cancellation time C and unused-value refund rate R; actual timestamp duration is used, not a30-day assumption. At/after E unused amount is0. Proposed demo prestart100%, midperiod50% of unused value; facility failure100% of unused value. For multiple periods, compute unused amount for each paid period separately, apply its rate, sum, then round down once to whole VND for the entire refund request. Each interval/receipt may be refunded only once; days without visits do not increase unused value. Clamp cumulative refund to eligible collected money, record cancellation/entitlement release separately, never free occupied capacity. Future paid renewal not yet started is assessed as its own period. C is the backend-accepted cancellation time; service failure uses verified loss-of-service time and does not incur the voluntary-cancellation penalty. Past disruption is handled as a separate incident without rewriting history or double refund. Do not automatically offset a refund against additional parking fees or other debt; separate obligations remain visible. |
| M-06 | Plan changes and operational failure | Purchased period retains accepted plan/version; price/type/vehicle changes, transfers, suspension and temporary closure compensation are not implicit capabilities. Define them before offering them. For guaranteed capacity unavailable, deny impossible entry and apply approved incident/refund process; a paid pass is not a command to displace a parked vehicle. Prior paid money remains visible during any failure. Ordinary exit must settle remaining eligible charges; emergency release follows approved emergency authority and does not erase debt. |
| M-07 | Guarantee lifecycle and overlapping rights | When a guaranteed-plan vehicle leaves before E, its still-valid reserved resource returns to protected inventory, not ordinary availability. After E no expired guarantee is restored. Check the whole sold interval against reservation, protection, backup and monthly commitments; opening simultaneous orders must not double-hold the same vehicle's next period. A separate booking for the same vehicle/period cannot duplicate an already-matching guaranteed entitlement; direct the Driver to existing rights. Monthly passes do not automatically create bookings or provide an unapproved booking discount. |
| M-08 | Repurchase while already parked and exit | Validate the existing vehicle/session, plan eligibility and capacity before granting a new period while the vehicle is in the lot. Before new S is uncovered; after S is covered only if plan conditions hold. Do not create a new entry or deduct occupancy again, and do not silently force relocation. Use the ordinary actual-exit billing rule confirmed by C-21; a free covered exit right cannot stretch coverage past E. Keep prior legitimate charges and new entitlement allocation consistent. |
| M-09 | Initial-extension limits | Plans bind account, vehicle, lot/zone and entitlement type. Vehicle/lot/type transfers and suspend-and-add-days are unsupported in the first future extension; use a disclosed cancellation/new purchase instead of directly editing an active contract. Preserve accepted period price and configured extra-use tariff; no automatic retroactive repricing. Operator cannot modify a period to give credit or postpone payment. |

#### F.2 MVP FR and User Stories

Monthly FR identifiers, functional meaning and User Story edges are retained as MVP requirements and reference the single rule definition in F.1. They are included in v0.8.5 MVP acceptance for the monthly-pass scope.

| FR ID | Status | Functional requirement | Actor | User Story |
| --- | --- | --- | --- | --- |
| FR-MON-01 | C | Owner configures versioned plans, price N, entitlement type and parameters in F.3 with an effective date. | Owner | US-OW06 |
| FR-MON-02 | C | Driver reviews full-period price, scope, original anchor, S/E, guarantee type and refund terms before purchase (M-01). | Driver | US-D13 |
| FR-MON-03 | C | Driver creates a vehicle/lot-bound immediate or future-start order; validate prepaid limits, duplicate plans and resources before accepting payment (M-02/M-07). | Driver | US-D13 |
| FR-MON-04 | C | Backend calculates anchored periods under M-01 and stores [S,E), original anchor and accepted price for each period. | Driver, Owner | US-D13, US-OW06 |
| FR-MON-05 | C | Backend grants rights only after full verified payment and A<H; handle late, mismatched, duplicate money and failed fulfillment under M-03. | Driver, Operator, Payment Provider | US-D13 |
| FR-MON-06 | C | Protect capacity/slot by plan type, commitment schedule and hold; avoid double accounting when rights become occupied (M-04/M-07). | Owner, Operator | US-OW06, US-O07 |
| FR-MON-07 | C | Operator validates account, vehicle, state, timestamps and resources per visit; a monthly right does not replace the single-use session ticket or permit duplicate open sessions. | Operator, Device Gateway | US-O07 |
| FR-MON-08 | C | Separate covered and uncovered parking intervals under M-04; charge uncovered use without restarting short-stay grace at each boundary. | Driver, Operator | US-O07, US-D14 |
| FR-MON-09 | C | Timely renewal extends from the last committed E; late repurchase creates a new non-retroactive period after price/resource validation (M-02). | Driver, Payment Provider | US-D14 |
| FR-MON-10 | C | Driver views period/order/payment/refund independently and receives configured reminders; notification failure never creates renewal. | Driver, Notification Service | US-D14 |
| FR-MON-11 | C | Driver confirms cancellation time C and estimated refund; Owner approves/reconciles under M-05 without freeing still-occupied capacity. | Driver, Owner, Payment Provider | US-D15 |
| FR-MON-12 | C | Owner price changes or sales closure do not rewrite a paid period; facility cancellation follows M-05/M-06 with reasons and notification. | Owner, Notification Service | US-OW06 |
| FR-MON-13 | C | Owner views scoped collected/refunded money; Operator sees only necessary operational data. Collected money awaiting refund is not displayed as a successfully granted plan. | Owner, Operator | US-OW06 |
| FR-MON-14 | C | At E stop new entry entitlement while retaining actual sessions/occupancy; prevent overlap with bookings and settle exit under M-04/M-07/M-08. | Driver, Operator | US-O07, US-D14 |

| US ID | Actor | User goal | FR coverage |
| --- | --- | --- | --- |
| US-D13 | Driver | Purchase a monthly plan after reviewing price, period and entitlement type. | FR-MON-02, FR-MON-03, FR-MON-04, FR-MON-05 |
| US-D14 | Driver | Renew on time and understand what happens after unpaid, late or uncertain payment. | FR-MON-08, FR-MON-09, FR-MON-10, FR-MON-14 |
| US-D15 | Driver | Cancel a monthly plan and follow any eligible refund. | FR-MON-11 |
| US-O07 | Operator | Validate monthly rights and settle uncovered usage without duplicate charges. | FR-MON-06, FR-MON-07, FR-MON-08, FR-MON-14 |
| US-OW06 | Owner | Manage plan prices, capacity promises, changes and refunds. | FR-MON-01, FR-MON-04, FR-MON-06, FR-MON-12, FR-MON-13 |

#### F.3 Monthly Configuration and Boundary Tests

Owner manages monthly-plan settings within the same hierarchical policy model: Admin provides defaults/bounds and Owner may override within scope. Operator validates monthly rights and may record approved CASH_COLLECT payments; Operator does not set monthly prices or approve refunds. The monthly payment hold is a monthly-plan order hold and is separate from the ordinary reservation payment hold. Numeric examples in this table remain isolated Dev/QA fixtures, not production prices. The rolling anchor, prepayment and non-retroactive grant rules are baseline monthly-pass behavior, not arbitrary Owner switches.

| Parameter | Meaning / unit | Value | Isolated Dev/QA fixture |
| --- | --- | --- | --- |
| MONTHLY_PRICE | Price per complete calendar period and plan/vehicle | N VND/period | 1200000 VND for a demo automobile plan |
| MONTHLY_PAYMENT_HOLD_DURATION | Order payment deadline window | N minutes | 15; separate from Admin booking hold |
| MONTHLY_RENEWAL_WINDOW | Open renewal request before E | N days | 7 |
| MONTHLY_EXPIRY_NOTICE | Reminder offsets before E | List of N durations | 7 days and1 day |
| MONTHLY_MAX_PREPAID_TERMS | Consecutive prepaid period limit | N periods | 3 |
| MONTHLY_SALES_LIMIT | Concurrent sold-plan limit; does not replace resource validation | N plans | 10 in an isolated fixture |
| MONTHLY_CANCEL_REFUND_RATE | Rate applied to unused value | N percent per band | Before S100%; during period50% |
| MONTHLY_ALLOCATION_TYPE | Offered entitlement | When available / guaranteed capacity / guaranteed slot | Separate demo plans |


| Case | Expected result |
| --- | --- |
| Start15/02 at00:00 | First period [15/02,15/03); timely renewal [15/03,15/04), with no bill on01/03 solely because the calendar month changed. |
| Start31/01 in non-leap year | Ends28/02 at same local time; next timely renewed period ends31/03; original31 anchor retained. |
| Start31/01 in leap year | Ends29/02, then31/03 on timely renewal. |
| Start-now at15/02 14:20 | Ends15/03 14:20; do not round activation backward to00:00. |
| Payment at deadline H | No activation under proposed strict A<H; record/reconcile real received money. |
| Payment before H but delayed callback/commit after H | No retroactive entitlement; reconciliation/refund, with no automatic resource revival. |
| No renewal payment | Expires at E; no automatic next-month debt or penalty for not renewing; actual uncovered parking charges can still arise. |
| Late repurchase20/03 after expiry15/03 | New effective start and20 anchor after validation; do not charge an unused15–20/03 monthly period retroactively. |
| Pass expires while car remains parked | Retain occupancy; charge uncovered interval once; do not reset session grace or free guaranteed occupied slot as available. |
| Duplicate payment/event | One entitlement and one logical capacity commitment; extra real money separately reconciled/refunded. |
| Paid guaranteed plan but no capacity at commit | No impossible promise; record failure and refund/reconciliation, retaining money audit. |
| Midperiod cancellation | Use actual remaining/total timestamp fraction under M-05; approved refund and confirmed payout remain separate. |

<!-- INTEGRATION-END MONTHLY -->

<!-- INTEGRATION-START REVIEW -->

### G. Integration Review and Limitations

#### G.1 Document Integrity Checks

- Original baseline wording and ordering are recoverable exactly after removing marked insertions and the four marked closing-fence additions. Original input file is not overwritten.
- The integration contains 116 current-domain/future-AI FR records (100 retained identifiers plus16 identifiers indexing omitted baseline capabilities) and14 monthly-pass MVP FR records, each with a User Story link.
- The story catalogue contains38 non-monthly records (33 retained plus5 baseline-coverage additions) and5 monthly-pass records. Forward/reverse FR–US references must agree; a story linked to an X/F FR does not convert it to approved MVP.
- The Business Rule register contains53 IDs: all39 original rule IDs retained, plus14 added rules. Reused topics refer to their single baseline definition or rule row rather than copying the original SRS paragraphs.
- All23 decision entries and10 AI dependency entries have explicit identifiers. All original 35 tester and 9 architecture deferred IDs remain represented; directly resolved tester findings are annotated as closed by C-09/C-18/C-04/C-06 where applicable, while their remaining detailed refinement stays open/deferred.
- Unique definition IDs, table column counts, code fences, missing FR/US/BR references and the baseline-preservation check are validated by a local document-check script. Short forms such as FR-AUTH-01/02 denote the same prefix, not a new identifier.

**Executed document-check result (2026-09-30): PASS.** All 185 FR–US links agree in both directions; 55 Markdown tables have consistent column counts; all requirement/story/rule/decision/dependency definitions are unique; code blocks are closed. All 114 earlier FR IDs, 38 earlier US IDs and 39 earlier BR IDs remain represented. The original download's checksum is unchanged, and removing the marked additions reconstructs its original text and order. These checks do not change any OPEN decision status.

#### G.2 Review Conclusion

The v0.8.5 baseline incorporates the resolved C-01–C-23 business decisions. Remaining A/X/F items in the document are still proposals, deferred decisions or future scope and must not be implemented as confirmed requirements merely because they are described in the SRS.

This revision does not certify runtime correctness, provider integration, data-protection compliance, model accuracy, performance/availability targets or completed API implementation. Existing legal text is retained without a new legal review. Remaining deferred/proposal items are not marked complete merely by appearing in this document.

<!-- INTEGRATION-END REVIEW -->

