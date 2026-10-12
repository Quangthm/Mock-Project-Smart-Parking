# SmartPark 3D Digital Twin Documentation Hub

Welcome to the **SmartPark 3D Digital Twin** architectural and design documentation hub. This directory contains all technical specifications, asset documentation, WebGL implementation guidelines, enterprise onboarding rules, and exported 3D models.

In strict compliance with **SmartPark SRS v0.9 (§3.2.3)**, the 3D Digital Twin is a **non-authoritative visualization engine** that consumes authoritative state from the SmartPark backend.

---

## 🧭 DEVELOPER QUICK NAVIGATION & KEY IMPLEMENTATION MAP

For developers and technical leads picking up implementation tasks, use this reference map to navigate directly to the required modules:

| Implementation Goal | Primary Reference & Section | Summary & Takeaway for Developers |
| :--- | :--- | :--- |
| **SaaS Roadmap & 0-Code Design Contract** | [`GUIDE §2.3, §2.4`](./3D_MODEL_IMPLEMENTATION_GUIDE.md#23-architectural-roadmap-phase-1-mvp-vs-phase-2-commercial-saas) | Explains the two-phase roadmap (MVP Catalog vs. SaaS Self-Service) and the 5-pillar design contract that enables dynamic model ingestion without modifying frontend code. |
| **Enterprise Model Rules & Remediation** | [`RULES DOC`](./3D_ENTERPRISE_MODEL_INGESTION_RULES.md) | Official compliance rulebook for Phase 2 B2B clients and 3D artists. Includes naming regex, hierarchy, Draco budgets, error codes, and a step-by-step model remediation playbook. |
| **Project & Directory Layout** | [`GUIDE §8`](./3D_MODEL_IMPLEMENTATION_GUIDE.md#8-project--file-structure) | Clean Next.js/React folder structure (`components/3d/`, `services/3d/`, `constants/3d-palette.ts`). |
| **Draco Optimization & SLA < 3s** | [`GUIDE §9, §25`](./3D_MODEL_IMPLEMENTATION_GUIDE.md#9-asset-preparation--draco-compression) | Exact `gltf-pipeline` CLI commands for compression, measured Chrome DevTools benchmarks, and 60 FPS / <250 draw call budget caps. |
| **Dual-Track PBR Pipeline (Preserving Textures)** | [`GUIDE §16`](./3D_MODEL_IMPLEMENTATION_GUIDE.md#16-material-and-color-management-dual-track-pbr-pipeline) | Dual-Track engine: preserves native enterprise image textures/PBR shaders while applying fallback auto-styling to untextured clay imports. |
| **Orthogonal 2-Axis State Model** | [`GUIDE §16.3, §21`](./3D_MODEL_IMPLEMENTATION_GUIDE.md#21-runtime-state-visualization) | Decouples Physical State (`AVAILABLE` / `OCCUPIED` / `MAINTENANCE`) and Reservation Protection State (`UNRESERVED` / `RESERVED` / `PROTECTED`) per SRS v0.9 §3.2.2. |
| **Node Mapping & WebSocket Telemetry** | [`GUIDE §17, §18, §20`](./3D_MODEL_IMPLEMENTATION_GUIDE.md#17-object-identification-and-mapping) | Regex categorization (`PARKING_*`, `_EV_`, `Vinfast_`) and real-time WebSocket state binding strictly within the SRS v0.9 §3.2.3 non-authoritative boundary. |
| **Vehicle Instancing & `car_color status`** | [`GUIDE §19`](./3D_MODEL_IMPLEMENTATION_GUIDE.md#19-vehicle-integration-sedans--motorcycles-with-car_color-status) | Dynamic vehicle spawning using `car_color status` (Navy, Cyan, Amber, Purple, Red) to show session types without overwriting slot floor textures. |
| **Floor Culling & "Find My Spot" Navigation** | [`GUIDE §23`](./3D_MODEL_IMPLEMENTATION_GUIDE.md#23-camera--navigation-controls--floor-pagination) | `FloorPaginationController` (isolates active levels, turns off matrix auto updates) and GSAP camera flight focusing on the user's reserved stall. |
| **Pre-PR Developer Checklist** | [`GUIDE §32`](./3D_MODEL_IMPLEMENTATION_GUIDE.md#32-implementation-checklist) | 10-point self-audit quality checklist to verify before opening a pull request. |

---

## 📚 Master Engineering Specifications

1. 📖 [**3D_MODEL_IMPLEMENTATION_GUIDE.md**](./3D_MODEL_IMPLEMENTATION_GUIDE.md)  
   *Comprehensive master technical guide for frontend engineers: Three.js engine setup, Blender 5.2.2 LTS PBR pipeline, Dual-Track material management, `car_color status` paradigm, Draco compression, orthogonal 2-axis state model, floor pagination, throttled raycasting, and measured performance benchmarks (SPARK-184 / SPARK-186).*

2. 📜 [**3D_ENTERPRISE_MODEL_INGESTION_RULES.md**](./3D_ENTERPRISE_MODEL_INGESTION_RULES.md)  
   *Mandatory compliance rulebook and asset formatting standard for Phase 2 Commercial SaaS. Governs 3D model requirements for external B2B enterprise clients, Dual-Track Material Ingestion Policy (R6), digital ingestion agreement modal copy, and an AI-assisted remediation playbook (SPARK-185).*

---

## 🏢 Specific 3D Asset Documentation (Phase 1 Baseline Catalog)

Each document below provides exact camera vantages, spatial dimensions, verified node hierarchies, PBR manifests, and concrete implementation functions for each facility archetype:

1. 🚗 [**3D_ASSET_DOCUMENTATION_Parking_House.md**](./3D_ASSET_DOCUMENTATION_Parking_House.md)  
   *Multi-level above-ground parking structure: 3 above-ground floors (`Floor_G`, `Floor_L1`, `Floor_L2` [Rooftop Deck]), 549 glTF nodes / 467 meshes, 28 native PBR materials, floor-by-floor isolation controller (`switchParkingHouseFloor`), and slot navigation (`focusUserParkingSlot`).*
2. 🌳 [**3D_ASSET_DOCUMENTATION_Outdoor_Parking.md**](./3D_ASSET_DOCUMENTATION_Outdoor_Parking.md)  
   *Single-surface open-air parking lot: 349 glTF nodes / 322 meshes, embedded custom image textures for EV & standard stall decals, 2 aggregate motorcycle capacity zones, 4 VinFast battery swap stations across 2 cluster zones (North & Central), zone controller (`focusOutdoorZone`), and slot navigation (`focusUserOutdoorSlot`).*
3. 🚇 [**3D_ASSET_DOCUMENTATION_Underground_Parking.md**](./3D_ASSET_DOCUMENTATION_Underground_Parking.md)  
   *Two-level subterranean garage: Basement 1 & Basement 2 (`Floor_B1`, `Floor_B2`), 501 glTF nodes / 433 meshes, isolated floor hierarchy, 12 dedicated EV stalls, 5 aggregate motorcycle zones, 5 VinFast stations, inter-level ramps, floor isolation controller (`switchUndergroundFloor`), and slot navigation (`focusUserUndergroundSlot`).*

---

## 📦 Verified 3D Model Assets Manifest (`src_model/`)

### Primary Production Assets (Blender 5.2.2 LTS PBR Export — Recommended)
All models below have been authored/replicated in **Blender 5.2.2 LTS** with embedded PBR materials, custom surface textures, and origin-centered coordinates:

| Production Asset File | File Size | glTF Nodes / Meshes | Key Features & Enhancements | Status |
| :--- | :--- | :--- | :--- | :--- |
| `indoor_parking_lot_blender.glb` | **3.85 MB** (4,039,980 B) | 549 Nodes / 467 Meshes | 28 native PBR materials, isolated `Floor_G`, `Floor_L1`, `Floor_L2` | **Production Ready** |
| `outdoor_parking_lot_blender.glb` | **5.55 MB** (5,814,552 B) | 349 Nodes / 322 Meshes | Embedded image textures on EV/standard slot pads, asphalt PBR | **Production Ready** |
| `underground_parking_lot_blender.glb` | **9.17 MB** (9,617,160 B) | 501 Nodes / 433 Meshes | Isolated `Floor_B1` / `Floor_B2` groups, embedded decal textures | **Production Ready** |
| `car_model_blender.glb` | **0.27 MB** (288,072 B) | 12 Nodes / 8 Meshes | Centered at $(0,0,0)$, wheels grounded on $Z=0$, `car_color status` ready | **Production Ready** |
| `motorcycle_model_blender.glb` | **0.19 MB** (200,992 B) | 8 Nodes / 5 Meshes | Centered at $(0,0,0)$, wheels grounded on $Z=0$, aggregate capacity ready | **Production Ready** |

### Master Blender Project Files & Legacy Reference
* **Blender 5.2.2 Projects**: `indoor_parking_lot.blend`, `outdoor_parking_lot.blend`, `underground_parking_lot.blend`, `car_model.blend`, `motorcycle_model.blend`.
* **Legacy Untextured Exports** (Fallback Testing): `indoor_parking_lot.glb`, `outdoor_parking_lot.glb`, `underground_parking_lot.glb`, `car_model.glb`, `motorcycle_model.glb`.

---

## 🛡️ Jira Verification & Compliance Reference

* **SPARK-184**: Visual Three.js mount verification, database mapping matrix, and immutable asset SHA-256 hashes documented.
* **SPARK-185**: Asset versioning (`v1.2.0`), FSoft proprietary licensing, source/export paths, texture manifests, changelog, and stable-ID contract enforced.
* **SPARK-186**: Measured Chrome DevTools benchmarks (TTI < 1.5s on Fast 4G), 10-cycle mount/unmount memory leak tests (0 MB VRAM leak), and WebSocket reconnect backoff specified.


