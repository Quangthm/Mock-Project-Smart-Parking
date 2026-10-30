# API Design Specifications

This directory contains the detailed API design specifications for SmartPark service capabilities, as well as the standard design template for authoring new endpoint contracts.

## Standard Template

All new API endpoint specifications and refactors must follow the standard template:
- [API Design Template](api-design-template.md)

## Active API Designs

| Domain / Capability | Document | Owning Service |
|---|---|---|
| Partner / Owner Registration & First Lot Setup | [owner-register-api-design.md](owner-register-api-design.md) | UserService |
| Admin Review & Owner Approval | [admin-approve-owner-api-design.md](admin-approve-owner-api-design.md) | UserService |
| Driver Registration & Verification | [driver-register-api-design.md](driver-register-api-design.md) | UserService |
| Owner Create Operator & Invitation | [owner-create-operator-api-design.md](owner-create-operator-api-design.md) | UserService |
| Admin User Directory & Role Management | [admin-manage-users-api-design.md](admin-manage-users-api-design.md) | UserService |
| Parking Structure & Slot Capacity | [parking-structure-api-design.md](parking-structure-api-design.md) | FacilityService (transitional: FacilityService) |
| Vehicle Registration & Soft Removal | [vehicle-registration-api-design.md](vehicle-registration-api-design.md) | UserService |
