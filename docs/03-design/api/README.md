# API Design Specifications

This directory contains the detailed API design specifications for SmartPark service capabilities, as well as the standard design template for authoring new endpoint contracts.

## Standard Template

All new API endpoint specifications and refactors must follow the standard template:
- [API Design Template](api-design-template.md)

## Active API Designs

| Domain / Capability | Document | Owning Service |
|---|---|---|
| Owned avatar and vehicle images | [user-images-api-design.md](user-images-api-design.md) | UserService |
| Partner / Owner Registration & First Lot Setup | [owner-register-api-design.md](owner-register-api-design.md) | UserService |
| Admin Review & Owner Approval | [admin-approve-owner-api-design.md](admin-approve-owner-api-design.md) | UserService |
| Driver Registration & Verification | [driver-register-api-design.md](driver-register-api-design.md) | UserService |
| Public Auth Bot Protection — SPARK-257 (Cloudflare Turnstile, cross-cutting) | [bot-protection-spark-257-api-design.md](bot-protection-spark-257-api-design.md) | UserService |
| Owner Create Operator & Invitation | [owner-create-operator-api-design.md](owner-create-operator-api-design.md) | UserService |
| Admin User Directory & Role Management | [admin-manage-users-api-design.md](admin-manage-users-api-design.md) | UserService |
| Parking Structure & Slot Capacity | [parking-structure-api-design.md](parking-structure-api-design.md) | FacilityService |
| Vehicle Registration / Private Claims (SPARK-243); Removal Deferred | [vehicle-registration-api-design.md](vehicle-registration-api-design.md) | UserService |
| 3D Digital Twin & Operational Layout Projection (hub) | [3d-digital-twin-api-design.md](3d-digital-twin-api-design.md) | FacilityService (transitional: ParkingService) |
| Own Normal Profile & Name Update | [own-profile-api-design.md](own-profile-api-design.md) | UserService |
| Driver Maps & Recorded Vehicle Location | [driver-maps-vehicle-location-api-design.md](driver-maps-vehicle-location-api-design.md) | OperationService (discovery/location) + FacilityService (layout projection) |
| Driver Payment History & Server-Side PDF Invoices | [driver-payment-history-and-pdf-invoice-api-design.md](driver-payment-history-and-pdf-invoice-api-design.md) | PaymentService |

> **Machine-Readable Specifications:** The OpenAPI 3.1 contract for the 3D Digital Twin subsystem is located at [3d-digital-twin-openapi.yaml](3d-digital-twin-openapi.yaml).
> **Task-Specific History:** Historical task split specs (SPARK-228, SPARK-244) are preserved in [../../archive/api/](../../archive/api/README.md).
