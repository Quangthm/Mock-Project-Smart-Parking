# SmartPark 3D Digital Twin Documentation Hub

Welcome to the **SmartPark 3D Digital Twin** architectural and design documentation hub. This directory contains all technical specifications, asset documentation, WebGL implementation guidelines, enterprise onboarding rules, and exported 3D models.

In strict compliance with **SmartPark SRS v0.8.5 (§3.2.3)**, the 3D Digital Twin is a **non-authoritative visualization engine** that consumes authoritative state from the SmartPark backend.

---

## 🧭 DEVELOPER QUICK NAVIGATION & KEY IMPLEMENTATION MAP

For developers and technical leads picking up implementation tasks, use this reference map to navigate directly to the required modules:

| Implementation Goal | Primary Reference & Section | Summary & Takeaway for Developers |
| :--- | :--- | :--- |
| **SaaS Roadmap & 0-Code Design Contract** | [`GUIDE §2.3, §2.4`](./3D_MODEL_IMPLEMENTATION_GUIDE.md#23-architectural-roadmap-phase-1-mvp-vs-phase-2-commercial-saas) | Explains the two-phase roadmap (MVP Catalog vs. SaaS Self-Service) and the 5-pillar design contract that enables dynamic model ingestion without modifying frontend code. |
| **Enterprise Model Rules & Remediation** | [`RULES DOC`](./3D_ENTERPRISE_MODEL_INGESTION_RULES.md) | Official compliance rulebook for Phase 2 B2B clients and 3D artists. Includes naming regex, hierarchy, Draco budgets, error codes, and a step-by-step model remediation playbook. |
| **Project & Directory Layout** | [`GUIDE §8`](./3D_MODEL_IMPLEMENTATION_GUIDE.md#8-project--file-structure) | Clean Next.js/React folder structure (`components/3d/`, `services/3d/`, `constants/3d-palette.ts`). |
| **Draco Optimization & SLA < 3s** | [`GUIDE §9, §25`](./3D_MODEL_IMPLEMENTATION_GUIDE.md#9-asset-preparation--draco-compression) | Exact `gltf-pipeline` CLI commands for compression, 4G math verification (~1.14s transfer), and 60 FPS / <150 draw call budget caps. |
| **PBR Materials (Solving Grey Clay)** | [`GUIDE §16`](./3D_MODEL_IMPLEMENTATION_GUIDE.md#16-material-and-color-management-proposal-for-manager-approval) | Overcomes Spline's monochrome grey export. Contains the Pros/Cons business case for Manager approval and the shared `SMARTPARK_PBR_PALETTE` token dictionary. |
| **Node Mapping & WebSocket Telemetry** | [`GUIDE §17, §18, §20`](./3D_MODEL_IMPLEMENTATION_GUIDE.md#17-object-identification-and-mapping) | Regex categorization (`PARKING_*`, `_EV_`, `Vinfast_`) and real-time WebSocket state binding strictly within the SRS §3.2.3 non-authoritative boundary. |
| **High-Efficiency Vehicle Instancing** | [`GUIDE §19`](./3D_MODEL_IMPLEMENTATION_GUIDE.md#19-vehicle-integration-sedans--motorcycles) | High-performance rendering of 500+ dynamic cars and motorcycles using `THREE.InstancedMesh` (consumes only 1 Draw Call per vehicle archetype). |
| **Floor Culling & "Find My Spot" Navigation** | [`GUIDE §23`](./3D_MODEL_IMPLEMENTATION_GUIDE.md#23-camera--navigation-controls--floor-pagination) | `FloorPaginationController` (isolates active levels, turns off matrix auto updates) and GSAP camera flight focusing on the user's reserved stall. |
| **Pre-PR Developer Checklist** | [`GUIDE §32`](./3D_MODEL_IMPLEMENTATION_GUIDE.md#32-implementation-checklist) | 10-point self-audit quality checklist to verify before opening a pull request. |

---

## 📚 Master Engineering Specifications

1. 📖 [**3D_MODEL_IMPLEMENTATION_GUIDE.md**](./3D_MODEL_IMPLEMENTATION_GUIDE.md)  
   *Comprehensive master technical guide for frontend engineers: Three.js engine setup, Draco compression pipeline, PBR material management, floor pagination, throttled raycasting, instanced vehicles, and complete React TypeScript components.*

2. 📜 [**3D_ENTERPRISE_MODEL_INGESTION_RULES.md**](./3D_ENTERPRISE_MODEL_INGESTION_RULES.md)  
   *Mandatory compliance rulebook and asset formatting standard for Phase 2 Commercial SaaS. Governs 3D model requirements for external B2B enterprise clients and provides a diagnostic remediation playbook for 3D artists.*

---

## 🏢 Specific 3D Asset Documentation (Phase 1 Baseline Catalog)

Each document below provides exact camera vantages, spatial dimensions, full scene node hierarchies, and concrete implementation functions for each facility archetype:

1. 🚗 [**3D_ASSET_DOCUMENTATION_Parking_House.md**](./3D_ASSET_DOCUMENTATION_Parking_House.md)  
   *Multi-level above-ground parking structure: 3 above-ground floors + rooftop deck (`Floor_G`, `Floor_1`, `Floor_2`, `Floor_Roof`), EV charging bays, floor-by-floor isolation controller (`switchParkingHouseFloor`), and slot navigation (`focusUserParkingSlot`).*
2. 🌳 [**3D_ASSET_DOCUMENTATION_Outdoor_Parking.md**](./3D_ASSET_DOCUMENTATION_Outdoor_Parking.md)  
   *Single-surface open-air parking lot: dedicated top-row EV stalls with lightning bolt markings, dual-gun DC chargers, 2 aggregate motorcycle capacity zones, 2 VinFast battery swap kiosks, zone controller (`focusOutdoorZone`), and slot navigation (`focusUserOutdoorSlot`).*
3. 🚇 [**3D_ASSET_DOCUMENTATION_Underground_Parking.md**](./3D_ASSET_DOCUMENTATION_Underground_Parking.md)  
   *Two-level subterranean garage: Basement 1 & Basement 2 (`Floor_B1`, `Floor_B2`), 12 dedicated EV stalls, 5 aggregate motorcycle zones, 5 VinFast stations, inter-level ramps, floor isolation controller (`switchUndergroundFloor`), and slot navigation (`focusUserUndergroundSlot`).*

---

## 📦 3D Model Assets Directory (`src_model/`)

* `indoor_parking_lot.glb` (5.67 MB uncompressed / ~1.6 MB Draco) — Parking House model.
* `outdoor_parking_lot.glb` (8.21 MB uncompressed / ~2.1 MB Draco) — Outdoor Parking lot model.
* `underground_parking_lot.glb` (12.81 MB uncompressed / ~2.85 MB Draco) — Underground Parking garage model.
* `car_model.glb` (431 KB) & `motorcycle_model.glb` (138 KB) — Lightweight vehicle models for runtime slot occupancy.

