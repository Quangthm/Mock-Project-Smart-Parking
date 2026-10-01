# Software Requirements Specification (SRS)

## Smart Parking Management System

**Project**: SmartPark  
**Baseline Version**: 0.8
**SRS Version**: 1.0 (Draft)  
**Date**: 2026-09-30  
**Status**: Draft / Working Compilation  
**Prepared by**: Quang

---

## 1\. Introduction

### 1.1 Purpose

This Software Requirements Specification (SRS) document describes the functional and non-functional requirements for the SmartPark.

SmartPark is a **configurable parking management and reservation platform for a simulated parking environment**.

The system is intended to manage the relationship between parking users, vehicles, parking capacity, reservations, physical occupancy, parking sessions, payments, operational handling, violations, and operator-controlled policies.

The document serves as the primary reference for:

- Development teams implementing the system  
- QA teams defining test strategies  
- Operations teams planning deployment and monitoring  
- Stakeholders reviewing and approving requirements

### 1.2 Scope of Project

SmartPark is a comprehensive platform for managing automobile parking lots with the following capabilities:

**In Scope (Current MVP):**

- User registration and vehicle management  
- Parking lot discovery and real-time status tracking  
- Parking space reservation and allocation  
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
- Operator-controlled parking policies
- 3D digital twin

**Out of Scope (Current Release):**

- Mobile application
- Autonomous vehicle integration  
- Detailed complaint/ticketing system  
- Loyalty points program  
- Monthly pass management  
- Full offline mode  
- National digital map integration
- IoT sensor integration for automated status updates


**AI-Enhanced Features (Future Releases):**

- AI-assisted parking spot status detection  
- AI-based parking recommendations  
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
| Operator | Check vehicles in/out, monitor parking, respond to incidents, perform manual operational actions, manage devices | **BASELINE** |
| Owner | Manage parking facilities, operators, pricing and supported parking policies, view revenue and analytics report | **BASELINE** |
| Admin | Manage users, system configuration, and audit logs | **BASELINE** |

## 2.4 External Systems / Physical Entities

| Entity | Potential Responsibility | Status |
|---|---|---|
| Payment provider | Process payments/refunds: VNPay, MoMo (current gateways; additional providers may be added later) | **BASELINE** |
| Camera | Physical observation / vehicle detection | **FUTURE / PROPOSED** |
| ANPR/LPR service | Vehicle/license-plate recognition | **FUTURE / PROPOSED** |
| Parking sensor | Occupancy observation | **FUTURE / DEFERRED** |
| Gate/barrier | Physical access event | **FUTURE / PROPOSED** |
| Map service | Location/search support: Google Maps / Mapbox  | **BASELINE** |
| Notification service | Email/SMS/push | **BASELINE** |
| AI/LLM service | AI chatbot and approved AI assistance | **MVP — limited scope** |
| Simulation engine | Generate controlled parking scenarios and failures | **FUTURE / DEFERRED** |

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
| Payment provider | VNPay / MoMo | **BASELINE current gateways** | Additional providers may be added later. |

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

### 3.1 User Management

#### 3.1.1 User Registration

The system shall allow users to register using a phone number or email address. Upon registration, the system shall send a one-time password (OTP) for verification. The OTP shall be valid for 5 minutes. After three consecutive failed OTP attempts, the account shall be temporarily locked for 15 minutes.

#### 3.1.2 Identity Verification

Sensitive identity information shall be encrypted using AES-256.

#### 3.1.3 Vehicle Registration

Users shall be able to register multiple vehicles per account. Each vehicle record shall include license plate, vehicle type, and vehicle image. The system shall validate license plate format according to Vietnamese regulations.

#### 3.1.4 Authentication

The system shall support OTP-based authentication via SMS or email. Multi-factor authentication (MFA) using TOTP shall be available as an optional security feature. Session tokens shall expire after 24 hours, with refresh tokens valid for 7 days.

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

Spot physical state shall be updated through the selected observation mechanism. For MVP, automated IoT updates may be simulated. Manual status updates by parking attendants shall be supported as a fallback mechanism. In case of conflict between sensor data and manual updates, the current MVP rule is to prioritize sensor data and log the conflict for administrative review.

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

Higher-scope policies provide the default values for applicable lower scopes. A lower scope may specialize or override a higher-scope value when that policy is configurable at the lower scope. If no lower-scope value is configured, the applicable higher-scope value remains in effect.

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
CONFIRMED → CANCELLED
CONFIRMED → UNFULFILLABLE
```

The lifecycle represents the reservation's business state and is separate from the parking spot's physical state and reservation/protection state.

- **PENDING_PAYMENT**: Reservation intent has been created and the configured payment hold is active. The hold blocks incompatible competing claims against the same requested capacity during the hold period, but does not count toward total reserved slot capacity and does not set the spot's reservation/protection state to **RESERVED**.
- **CONFIRMED**: Payment has been completed and the reservation is valid according to the applicable reservation and payment policies.
- **ALLOCATED**: SmartPark has assigned the reservation an applicable parking slot or capacity according to the allocation process.
- **PARKING**: The reserved driver has entered the parking process and is using applicable parking capacity.
- **COMPLETED**: The reservation-related parking process has been completed.
- **EXPIRED**: The reservation did not complete payment within the applicable payment-hold period.
- **CANCELLED**: The driver or applicable system/operator process has cancelled the confirmed reservation according to the applicable policy.
- **UNFULFILLABLE**: SmartPark cannot fulfill the confirmed reservation under the applicable allocation, conflict, and capacity policies.

#### 3.4.3 Reservation Creation and Payment Hold

Registered users shall be able to create a reservation for a selected time window. The system shall validate the requested reservation against authoritative reservation capacity, zone/slot availability, vehicle compatibility, and the applicable reservation policy.

After the user confirms reservation intent, the system shall create the reservation in `PENDING_PAYMENT` before payment is completed. The relevant capacity shall be temporarily held during checkout only for the purpose of preventing incompatible competing claims during the payment-hold period. A PENDING_PAYMENT hold shall not count toward total reserved slot capacity.

The payment hold duration shall be configurable by Admin as a system-level policy; the current default is **5 minutes** unless changed by Admin.

The payment hold is separate from the later reservation-protection window. The payment hold exists to prevent incompatible competing claims during checkout; the protection window exists to protect an already-confirmed future reservation from avoidable operational conflicts.

A PENDING_PAYMENT reservation hold does not make the spot RESERVED; the RESERVED state applies only after the reservation has been paid.

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

Backup capacity represents capacity reserved for future reservation fulfillment and shall be excluded from ordinary walk-in availability while the corresponding reservation demand remains active.

A physical slot or capacity unit shall not be simultaneously committed to incompatible reservations. PENDING_PAYMENT holds are temporary competing-claim protections and do not count toward total reserved slot capacity.

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
- Payment hold duration: Configurable by Admin as a system-level policy; current default is 5 minutes.
- Reservation Protection Window: N hours before the reservation start time, configurable by the Owner/parking-lot policy.
- Allocation Lead Time: N minutes before the reservation start time, configurable by the Owner/parking-lot policy.
- Backup capacity requirement: dynamically calculated and configurable according to owner/parking-lot policy.
- Conflict handling and refund behavior: governed by applicable owner/parking-lot policy.

The N-valued limits remain pending team/policy completion.

#### 3.4.10 Cancellation, Late Arrival, and No-Show

- A driver may cancel their reservation. Once cancelled, the reservation entitlement ends and any subsequent parking is treated as a normal non-reservation/walk-in session.
- Cancellation refund eligibility shall be determined by the applicable owner-configured refund policy. Existing draft examples such as cancellation more than N hours before the reservation time and no refund within N hours remain policy values to be completed.
- A driver arriving within the reservation time remains eligible under the reservation, subject to applicable allocation, capacity, and operational policies.
- No-show handling and any related refund or forfeiture shall be governed by the applicable owner/parking-lot policy.
- Automatic cancellation of unpaid reservations after the configured payment-hold period shall be governed by the system-level Admin policy.

#### 3.4.11 Check-in Process

When a reserved user arrives at the parking lot, the system shall identify the vehicle through the configured vehicle-identification mechanism, such as simulated LPR/image processing or QR code scanning. If a physical barrier integration is enabled, it may open only after backend validation confirms that the vehicle is authorized to enter.

Existing vehicles that are already occupying parking capacity shall not be automatically displaced or forcibly removed by the reservation allocation process. Their occupancy shall instead be accounted for during allocation and conflict handling, with applicable reallocation, queue, operational handling, and refund policies applied when reservation fulfillment is affected.

### 3.5 Payment & Fee Management

#### 3.5.1 Fee Types

The system shall support the following fee types:

1. **Parking Fee** (per minute/hour):  
     
   - Applied to both registered and guest users  
   - Rounded up to the nearest 15 minutes  
   - Calculated based on entry and exit timestamps

   

2. **Monthly Management Fee** (registered users only):  
     

   

3. **Overnight Fee** (guest users only):  
     
   - Applied when exit time is after N  
   - Fixed tiered rates determined by the platform  
   - Example: 22:00-06:00: 50,000 VND, plus regular hourly rate after 06:00

#### 3.5.2 Dynamic Pricing

The system shall support dynamic pricing that adjusts rates based on:

- Peak hours  
- Special events  
- High demand periods Maximum dynamic pricing shall not exceed N% of the base rate.

#### 3.5.3 Payment Processing

The system shall integrate with **VNPay and MoMo as the current primary payment gateways**. Additional payment providers may be supported later through the provider abstraction without redesigning the core payment domain.

Payment processing shall include:

- Automatic retry up to 3 times on failure.
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
- Overdue payment reminders (at 1, 3, and 7 days)  
- QR codes for reservations

#### 3.6.3 Email Notifications

Email shall be used for:

- VAT invoices  
- Monthly usage reports  
- Policy updates  
- Password reset
- Slot re allocation
- Account registration success(When admin approved account, owner create account for operator)

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
| Sensitive Data | AES-256 encryption \+ tokenization for CCCD, GPLX |
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
| VNPay, MoMo | Payment processing | Queue payment, retry later, notify user |
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
- Recommend parking lots and spots based on user context  
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

- Execute actual reservation changes, cancellations, and refunds  
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

### 6.8 AI Implementation Phases


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
| Backup Capacity | Capacity reserved for future reservation fulfillment and excluded from ordinary walk-in availability while the corresponding demand remains active. |
| Physical State | The current observed physical condition of a parking spot, such as AVAILABLE or OCCUPIED. |
| Reservation / Protection State | The reservation-related state associated with a spot or capacity, such as RESERVED, PROTECTED, or BACKUP. |
| Reservation Lifecycle State | The business lifecycle of a reservation, including PENDING_PAYMENT, CONFIRMED, ALLOCATED, PARKING, COMPLETED, and applicable exceptional states such as EXPIRED, CANCELLED, and UNFULFILLABLE. |
| Physical Arrival | The driver has reached the parking lot entrance or gate but has not yet physically occupied a parking slot; the driver may still be prevented from entering if no applicable capacity is available. |
| Payment Hold | A temporary PENDING_PAYMENT claim that blocks incompatible competing claims during checkout but does not count toward total reserved slot capacity. |
| Reallocation Request | A driver-submitted request to obtain another applicable allocation; submission does not guarantee approval, and a reservation allows at most two such driver-submitted requests. |

### B. Baseline Revision Note

This baseline was compiled from the previous SmartPark draft and the appended architecture material. The restructuring keeps the existing content direction, preserves unresolved `N` placeholders and other blank/unfinished areas for team completion, and incorporates the latest project decisions: MVP AI LPR, MVP chatbot, MVP 3D digital twin, current VNPay/MoMo gateways, a hybrid reservation model supporting specific-slot, zone, and capacity reservation, separated physical and reservation/protection states, a conceptual reservation lifecycle, configurable Allocation Lead Time, owner-configured conflict/refund handling, higher-scope policy defaults with lower-scope specialization/override, payment holds that block only incompatible competing claims without counting toward reserved capacity, defined physical-arrival semantics, and a maximum of two driver-submitted reallocation requests.


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

These notes are for follow-up architecture work only and do not change the current v0.8 requirements baseline.

### D. Deferred Tester Findings and Follow-up Backlog

The following items were identified during Tester validation of SRS v0.7/v0.8 but are intentionally deferred from the current baseline because they do not block the immediate Business Rules, API Contract, or AI Chatbox foundation work.

These items remain **OPEN / DEFERRED** and shall be revisited during subsequent SRS refinement.

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
| TESTER-DEF-12 | Payment success after payment-hold expiry | Define authoritative behavior when a payment provider reports success after the reservation/payment hold has already expired. |
| TESTER-DEF-13 | Late-created reservation | Define allocation behavior when a reservation is created after its calculated Allocation Time. |
| TESTER-DEF-14 | Protection Window and pending payment interaction | Confirm that Reservation Protection applies only to confirmed/paid reservations and define behavior for any exceptional timing overlap. |
| TESTER-DEF-15 | Simultaneous arrival | Define the tie-break behavior when multiple affected drivers arrive at effectively the same time. |
| TESTER-DEF-16 | Alternative-allocation refusal | Define whether a driver may reject an alternative allocation and what happens to the reservation afterward. |
| TESTER-DEF-17 | Multiple affected reservations | Extend physical-arrival conflict rules from the two-driver example to cases involving three or more affected reservations. |

---

#### D.4 Terminology and Domain Clarification

| ID | Deferred Item | Required Follow-up |
|---|---|---|
| TESTER-DEF-18 | Guest vs. walk-in terminology | Define whether "guest" means an unregistered user, any non-reservation driver, or another category, particularly for fee calculation. |
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
| TESTER-DEF-25 | Dynamic pricing | Confirm MVP scope and define the required pricing rules, configuration ownership, and triggering conditions. |
| TESTER-DEF-26 | AI parking recommendations | Resolve whether AI Parking Recommendations are part of the current MVP or a future release, since scope and detailed AI requirements currently require alignment. |

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

### D.9 Deferred Status Rule

Deferred items in this appendix:

- are **not considered resolved requirements**;
- shall not be silently implemented as fixed business rules;
- may be refined by the BA, Tester, Architect, Owner, or relevant module team;
- must be explicitly marked as resolved, superseded, or moved to Future/Out of Scope when revisited;
- shall not block the current Business Rules, API Contract, or AI Chatbox foundation work unless a later implementation task directly depends on them.

Any future resolution that changes an existing requirement shall be recorded as a new baseline revision rather than silently changing the current baseline.