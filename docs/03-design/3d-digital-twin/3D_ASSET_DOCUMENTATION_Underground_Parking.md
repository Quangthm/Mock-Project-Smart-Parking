# 3D Asset Documentation — Underground Parking (2-Level Subterranean Garage)

**Document Reference**: `3D_ASSET_DOCUMENTATION_Underground_Parking.md`  
**System Reference**: SmartPark Parking Management System  
**Baseline Reference**: SmartPark SRS v0.8.5 (Sections 3.2.1, 3.2.2, 3.2.3, 3.4.3, 3.4.5, 3.7.1, BR-CAP-01, BR-VEH-03, C-23)  
**Technical Architecture Reference**: SmartPark Solution Architecture v0.8.5 (3D Digital Twin Component)  
**Creation / Modeling Tool**: Antigravity (Spline 3D DSL Engine)  
**Last Updated**: 2026-10-05  

---

## 1. PROJECT CONTEXT

Within the **SmartPark** ecosystem, this 3D model represents a subterranean, multi-level underground parking facility comprising two basement levels: **Basement 1 (`Floor_B1`)** and **Basement 2 (`Floor_B2`)**. Underground parking structures present distinct operational challenges, including restricted vehicular clearance, compartmentalized fire zones, enclosed lighting dependencies, multi-ramp inter-level circulation, and specialized driver navigation needs.

In strict accordance with **SmartPark SRS v0.8.5 §3.2.3**, the 3D Digital Twin acts as a non-authoritative visualization engine that consumes validated backend states from the SmartPark core microservices. It enables parking lot owners, facility managers, and drivers to inspect floor-by-floor occupancy, verify specific parking bay availability, locate electric vehicle (EV) charging spots, and monitor entry/exit operations. The 3D model **never** executes business logic, authorizes vehicle entry, or modifies database occupancy records.

### Business / Backend Concept vs. 3D Representation

| SmartPark Backend Concept | SRS Authority | 3D Digital Twin Representation |
| :--- | :--- | :--- |
| **Parking Lot Entity** (`ParkingLot`) | Authoritative DB record (Lot ID, Type: Underground Garage, GPS, Operating Policies) | Full subterranean structural shell (`UndergroundParking`) |
| **Basement Floors** (`ParkingFloor`) | Floor hierarchy: Basement 1 (ID: B1, Level: -1) and Basement 2 (ID: B2, Level: -2) | Discrete master groups (`Floor_B1`, `Floor_B2`) with absolute spatial separation |
| **Parking Zone** (`ParkingZone`) | Zone configuration (Zone ID, Vehicle Category constraint, Rate tier) | Bounded functional zones: `CarParking`, `MotorcycleParking`, `DrivingLanes`, `Facilities` |
| **Standard / Accessible Stalls** | Canonical spot record (Slot ID, Spot Type, Physical State, Protection) | Interactive rectangular slot meshes (`B1_Car_001`..`024`, `B2_Car_001`..`024`) |
| **Dedicated EV Car Stalls** | Specialized EV spot record (Slot ID, Slot Type: `EV_COMPATIBLE`, Charging state) | Selectable slots with lightning bolt symbol (`B1_EV_Car_001`..`006`, `B2_EV_Car_001`..`006`) |
| **EV Charging Posts & Screens** | Hardware charging station telemetry (Power kW, Energy delivered, Session state) | Dedicated charging pedestals and interactive touchscreen displays directly behind each EV bay |
| **Motorcycle Capacity Container** | Aggregate capacity counter ($Total - Occupied - Protected$) (SRS BR-CAP-01) | Bounded capacity strip zones (`MotorcycleZone_B1_*`, `MotorcycleZone_B2_*`) |
| **Battery Swap Infrastructure** | Partner charging / swap kiosk telemetry (VinFast network integration) | 5 low-poly VinFast battery swap station models (2 on B1, 3 on B2) |
| **Slot Physical State** (`PhysicalState`) | Five canonical states: `AVAILABLE`, `OCCUPIED`, `UNKNOWN`, `MAINTENANCE`, `UNAVAILABLE` | Visual stall surface fill (tint, transparency, dynamically spawned vehicle model) |
| **Reservation / Protection State** | Three canonical states: `RESERVED`, `PROTECTED`, `BACKUP` (co-exists with physical state) | Auxiliary visual indicators (outline glow, reservation badge, bounding wireframe) |
| **Inter-Level Vehicle Transit** | Movement between basement floors via internal ramps | Sloped concrete ramps (`Ramp_B1_To_B2_Down`, Median Barriers, Safety Walls) |
| **Entry Ramp Access Operations** | Grade-level vehicle descent, ticket issue, LPR identification | Street-to-B1 entrance ramp (`B1_Entrance_Exit_Ramp_Slab`), portal, barrier gates, scanners |

---

## 2. ASSET IDENTITY

| Property | Value |
| :--- | :--- |
| **Asset ID** | `ENV-PKG-UNDGRD-001` |
| **Asset Name** | Underground Parking (2-Level Basement Garage) |
| **Category** | Subterranean Multi-Level Parking Facility |
| **Model Type** | Static Environment with Interactive & Dynamic Component Nodes |
| **Generated With** | Antigravity (Spline 3D DSL Engine) |
| **Total Objects in Scene** | Exactly **496 Objects** across 1 Scene Page |
| **Generation Status** | Completed, Grouping Fixed, Fully Normalized |
| **Documentation Status** | Baseline Approved (Aligned with SRS v0.8.5 & Live Model Verification) |
| **Integration Status** | Ready for Three.js / WebGL Export & Integration |

---

## 3. SHORT MODEL DESCRIPTION

This model represents a modern two-level underground parking garage designed for the SmartPark 3D Digital Twin web client. Architecturally framed as an open cutaway diorama, it features Basement 1 (`Floor_B1`) with a street-level vehicular access ramp and inspection portal, Basement 2 (`Floor_B2`) with heavy structural ceiling beams and hanging LED light fixtures, inter-level connecting ramps, and vertical elevator/stairwell cores. In direct alignment with the SRS v0.8.5 specifications, the model incorporates **12 dedicated EV car stalls with lightning bolt markings and matching charging pedestals** (6 on B1, 6 on B2), **5 aggregate motorcycle capacity zones** that prevent web rendering lag, and **5 VinFast electric motorcycle battery charging swap stations** (2 on B1, 3 on B2).

---

## 4. KEY VISUAL DETAILS
**Link**: https://my.spline.design/undergroundparkinglot-A6f81JnEYWK1MrVy7n5PS1MV/
* **Two Distinct Basement Levels**:
  * **Basement 1 (`Floor_B1`)**: Upper basement ($Y \in [-13, +216]$) featuring entrance ramp access from street grade, toll control island, pedestrian safety paths, elevator core, 18 standard/accessible car stalls, **6 dedicated EV car bays with charging pedestals**, and **2 aggregate motorcycle capacity zones with 2 VinFast battery swap stations**.
  * **Basement 2 (`Floor_B2`)**: Deep subterranean level ($Y \in [-330, -44]$) featuring heavy overhead ceiling beams, industrial LED fixtures, elevator core, 18 standard car stalls, **6 dedicated EV car bays with charging pedestals**, and **3 aggregate motorcycle capacity zones with 3 VinFast battery swap stations**.
* **EV Charging Infrastructure (12 EV Bays + 12 Chargers)**:
  * **Basement 1**: 6 dedicated stalls (`B1_EV_Car_001`..`006`) with distinct lightning bolt pavement symbols, served by 6 charging pedestals (`B1_EV_Charger_001`..`006`) and 6 interactive status screens (`B1_EV_Charger_Screen_001`..`006`).
  * **Basement 2**: 6 dedicated stalls (`B2_EV_Car_001`..`006`) with matching lightning bolt pavement symbols, served by 6 charging pedestals (`B2_EV_Charger_001`..`006`) and 6 interactive status screens (`B2_EV_Charger_Screen_001`..`006`).
* **VinFast Battery Swap Infrastructure (5 Stations Total)**:
  * 2 stations on B1 (`Vinfast_Battery_Station_B1_1`, `Vinfast_Battery_Station_B1_2`) and 3 stations on B2 (`Vinfast_Battery_Station_B2_1`, `Vinfast_Battery_Station_B2_2`, `Vinfast_Battery_Station_B2_3`).
  * Each station features 4 battery swap bays, 4 interactive touchscreen panels, 8 LED charging indicator lights, VinFast branding logo, header, and plinth.
* **Motorcycle Parking (Aggregate Capacity - SRS BR-CAP-01)**:
  * 5 aggregate strip zones (2 on B1, 3 on B2), each featuring 10 stall delineators and an entrance divider. Individual stalls are rendered as aggregate capacity strips rather than distinct clickable IDs, preventing browser lag. Total motorcycle capacity: $\approx 100$ motorcycles (40 on B1, 60 on B2).
* **Vehicular Street Entrance Ramp**: Long sloped entrance/exit ramp slab descending from grade ($Y=+94.38 \rightarrow -12.62$ at $-13.57^\circ$ incline) with illuminated overhead sign portal (`Entrance_Portal_Header`), dual automated boom gates (`B1_Entrance_BoomGate_Entry`, `B1_Entrance_BoomGate_Exit`), and ticket/scanner island.
* **Inter-Level Circulation Ramps**:
  * Descending ramp from B1 to B2 (`Ramp_B1_To_B2_Down`, $Y = -172.85$, $14.04^\circ$ slope) with median barriers and protective side walls.
* **Structural Framing & Lighting**: 24 heavy reinforced concrete column shafts with broad base footings (12 on B1, 12 on B2), matched by 3 massive cross-span ceiling beams on B2 and 6 industrial hanging LED fixtures.

---

## 5. STRUCTURE AND SPATIAL LAYOUT

The model is strictly partitioned along the vertical $Y$-axis, maintaining absolute separation between floors:

```text
STREET GRADE (y ≈ +100 to +320)   [Entrance Portal & Grade Access Ramp]
                                   \
                                    \ -13.57° Incline Ramp
                                     v
========================================================================================
BASEMENT 1 (y ∈ [-13, +216])      [Floor_B1]
                                  - Car Zone: 18 Standard/Accessible Stalls + 6 Dedicated EV Bays
                                  - EV Hardware: 6 Charging Pedestals + 6 Touchscreens
                                  - Moto Zone: 2 Aggregate Capacity Zones + 2 VinFast Stations
                                  - Vertical Core: Elevator & Staircase
----------------------------------------------------------------------------------------
                                   \   ^
                                    \ /  14.04° Inter-Level Connecting Ramps
                                     v /
----------------------------------------------------------------------------------------
BASEMENT 2 (y ∈ [-330, -44])      [Floor_B2]
                                  - Car Zone: 18 Standard Stalls + 6 Dedicated EV Bays
                                  - EV Hardware: 6 Charging Pedestals + 6 Touchscreens
                                  - Moto Zone: 3 Aggregate Capacity Zones + 3 VinFast Stations
                                  - Vertical Core: Elevator & Staircase
                                  - Ceiling Beams & Industrial LED Fixtures
========================================================================================
```

### Hierarchy Tree

```text
UndergroundParking (Page Root)
├── Floor_B1 (efd29272)
│   ├── CarParking (ccf63ec0)
│   │   ├── 6 Dedicated EV Stalls (B1_EV_Car_001..006 with lightning bolt mark)
│   │   ├── 6 EV Charging Posts & Screens (B1_EV_Charger_001..006, B1_EV_Charger_Screen_001..006)
│   │   └── 18 Standard / Accessible Stalls (B1_Car_001..002, B1_Car_009..024)
│   ├── MotorcycleParking (3f382368)
│   │   ├── 2 Aggregate Capacity Zones (MotorcycleZone_B1_North, MotorcycleZone_B1_South)
│   │   ├── 2 VinFast Battery Swap Stations (Vinfast_Battery_Station_B1_1, Vinfast_Battery_Station_B1_2)
│   │   └── Curbs & Separators (B1_Zone_Separator_Curb_1, Curb_2)
│   ├── DrivingLanes (eacb289c)
│   │   ├── B1_DrivingArrows (B1_RoadArrow_1..6)
│   │   ├── Lane Dividers (B1_LaneDivider_Car_1..3) & Pedestrian Paths
│   │   └── Guard Railings (B1_Pedestrian_Railing_1..3)
│   ├── AccessInfrastructure (84d6f903)
│   │   ├── Grade Access Ramp Slab, Median & Retaining Walls
│   │   ├── Entrance Portal Header, Title ("UNDERGROUND PARKING") & Subtitle
│   │   ├── Dual Boom Barriers (B1_Entrance_BoomGate_Entry, B1_Entrance_BoomGate_Exit)
│   │   ├── Toll Payment Island & AI Industrial Scanners
│   │   └── Ramp Speed Limit Sign & Directional Arrows
│   ├── InterLevelRamp (69320c88)
│   │   ├── Descending Ramp Slab (Ramp_B1_To_B2_Down)
│   │   ├── Ramp Median Barriers & Safety Opening Wall
│   │   ├── Overhead Directional Gantry Sign (Sign_Panel_B1_Ramp, Sign_Text_B1_To_B2)
│   │   └── Safety Convex Mirror (Post & Lens)
│   ├── Facilities (aff446f8)
│   │   ├── Concrete Floor Slabs & Ramp Landing Slabs
│   │   ├── North, West, East & Front Cutaway Boundary Walls
│   │   ├── 12 Structural Columns (B1_Column_Base_01..12 & Shaft_01..12)
│   │   ├── Zone Signboards, Cutaway Diorama Badge & Paging Lights
│   │   └── Emergency Equipment (B1_FireHose_Cabinet_1)
│   ├── Elevator (cd55fae8) (Shaft Core, 2 Automatic Doors, Illuminated LED Header)
│   └── Staircase (d3a3c77a) (Concrete Steps, Emergency Exit Door, Exit Signage)
│
└── Floor_B2 (9cde8b11)
    ├── CarParking (93486058)
    │   ├── 6 Dedicated EV Stalls (B2_EV_Car_001..006 with lightning bolt mark)
    │   ├── 6 EV Charging Posts & Screens (B2_EV_Charger_001..006, B2_EV_Charger_Screen_001..006)
    │   └── 18 Standard Stalls (B2_Car_001..002, B2_Car_009..024)
    ├── MotorcycleParking (a62c1894)
    │   ├── 3 Aggregate Capacity Zones (MotorcycleZone_B2_North, Center, South)
    │   ├── 3 VinFast Battery Swap Stations (Vinfast_Battery_Station_B2_1, Vinfast_Battery_Station_B2_2, B2_3)
    │   └── Curbs & Separators (B2_Zone_Separator_Curb_1, Curb_2)
    ├── DrivingLanes (849263f8)
    │   ├── B2_DrivingArrows (B2_RoadArrow_1..6)
    │   ├── Lane Dividers & Pedestrian Guard Railings
    │   └── Pedestrian Walkway Paths
    ├── Ramp_B2_To_B1_Up (78c9f33d) (Ascending Return Ramp Slab)
    ├── Facilities (7311fc88)
    │   ├── B2 Concrete Floor Slab, North, West, East & Front Cutaway Walls
    │   ├── 12 Heavy Structural Columns (B2_Column_Base_01..12 & Shaft_01..12)
    │   ├── 3 Cross-Span Heavy Ceiling Beams (B2_Ceiling_Beam_1..3)
    │   ├── 6 Hanging Industrial LED Lighting Fixtures
    │   ├── Zone Signboards, Front Cutaway Diorama Badge & Interior Lights
    │   └── Safety Equipment (B2_FireHose_Cabinet_1, B2_CCTV_Camera_Main)
    ├── Elevator (b82a44cf) (Shaft Core, 2 Automatic Doors, Illuminated LED Header)
    └── Staircase (a3a11282) (Concrete Steps 1..4, Emergency Fire Exit Door, Exit Signage)
```

---

## 6. MAIN COMPONENTS

| Component | Identifier / Group | Node Type | Interactive / Dynamic | Operational & Backend Role |
| :--- | :--- | :--- | :--- | :--- |
| **Basement 1 Master** | `Floor_B1` | Empty Group | Interactive (Visibility) | Isolates upper basement level for camera focus and floor-by-floor inspection. |
| **Basement 2 Master** | `Floor_B2` | Empty Group | Interactive (Visibility) | Isolates lower basement level for camera focus and floor-by-floor inspection. |
| **Standard Car Stalls** | `B1_Car_*`, `B2_Car_*` | Mesh / Rectangle | Interactive | Individually addressable automobile stalls mapped to backend `ParkingSlot.id`. |
| **Dedicated EV Car Bays** | `B1_EV_Car_*`, `B2_EV_Car_*` | Mesh / Group | Interactive | Stalls with lightning bolt mark mapped to `slotType: EV_COMPATIBLE`. |
| **EV Charging Posts** | `B1_EV_Charger_*`, `B2_*` | Mesh / Cube | Interactive | Visualizes EV charging pedestal presence and active power delivery. |
| **EV Charger Screens** | `B1_EV_Charger_Screen_*` | Mesh / Cube | Interactive | Interactive terminal display showing charging progress, kWh delivered, and billing. |
| **Motorcycle Capacity Zones** | `MotorcycleZone_B1_*`, `B2_*` | Empty Groups | Interactive | Aggregate capacity containers tracking available 2W capacity (SRS BR-CAP-01). |
| **VinFast Battery Stations** | `Vinfast_Battery_Station_*` | 5 Composite Groups | Interactive | Visualizes battery swap station presence, operational state, and battery stock. |
| **Street Access Ramp** | `B1_Entrance_Exit_Ramp_Slab` | Mesh / Cube | Static | Physical vehicle descent path from street level to B1 floor level. |
| **Entrance Boom Barriers** | `B1_Entrance_BoomGate_*` | Composite Groups | Dynamic (Animated) | Pivots $90^\circ$ upward upon check-in ticket validation (`GateOpenedEvent`). |
| **Inter-Level Ramps** | `Ramp_B1_To_B2_Down` | Mesh / Cube | Static | Internal circulation ramp connecting B1 to B2. |
| **Elevator & Stair Cores** | `Elevator`, `Staircase` | Composite Groups | Static | Continuous vertical pedestrian access cores linking both basement floors. |

---

## 7. SMARTPARK FUNCTIONAL RELEVANCE

### 1. Multi-Level Underground Facility Hierarchy (§3.2.1)
* **Backend Concept**: Underground parking facility containing multiple subterranean floors (`ParkingFloor: B1, B2`), each with independent capacity constraints and pricing tiers.
* **3D Representation**: Master floor groups (`Floor_B1`, `Floor_B2`) that can be viewed together as an open cutaway diorama or isolated floor-by-floor.
* **Relationship**: Direct representation of `ParkingLot` containing multiple `ParkingFloor` records.

### 2. Spot State Decoupling (§3.2.2)
* **Backend Concept**: Decoupled **Physical State** (`AVAILABLE`, `OCCUPIED`, `UNKNOWN`, `MAINTENANCE`, `UNAVAILABLE`) and **Reservation / Protection State** (`RESERVED`, `PROTECTED`, `BACKUP`).
* **3D Representation**:
  * Physical reality rendered via slot surface material (green for vacant, red for vehicle present).
  * Reservation commitments rendered via outer border stroke, floating holographic icon, or warning wireframe.
* **Relationship**: Direct state visualization consuming `ParkingSlot` entity updates.

### 3. Separate Capacity Tracking for Motorcycles (§3.4.3, BR-CAP-01)
* **Backend Concept**: Capacity is calculated and reported separately by vehicle category. Unused motorcycle capacity never offsets automobile exhaustion.
* **3D Representation**: 5 aggregate capacity zones (2 on B1, 3 on B2) without individual clickable slot IDs, preventing rendering lag and displaying live capacity metrics (e.g., `B1 Motorcycles: 28 / 40`, `B2 Motorcycles: 45 / 60`).
* **Relationship**: Direct visual reinforcement of SRS capacity invariants.

### 4. Electric Vehicle (EV) Management (BR-VEH-03)
* **Backend Concept**: Segregation of internal combustion engine (ICE) vehicles and electric vehicles (EV). EV slots offer charging compatibility and customized tariff structures.
* **3D Representation**: 12 dedicated EV car stalls with high-contrast lightning bolt markings and matching charging pedestals with status screens, plus 5 VinFast electric motorcycle battery swap stations.
* **Relationship**: Direct visual representation of EV charging and battery swap infrastructure.

### 5. Inter-Level Circulation & Navigation (§3.7.1)
* **Backend Concept**: Routing drivers to their assigned floor and slot via designated circulation ramps.
* **3D Representation**: Animated 3D chevron breadcrumb paths navigating from the street entrance ramp down to B1 or via the inter-level ramp down to B2.

---

## 8. 3D DIGITAL TWIN ROLE

The Underground Parking asset fulfills key operational requirements in the SmartPark Digital Twin:

* **Floor-by-Floor Layer Isolation**: The web application allows operators to toggle between an establishing cutaway overview showing both levels or isolating a single floor (`Floor_B1.visible = true; Floor_B2.visible = false;`), optimizing camera angles and eliminating visual clutter.
* **Driver Navigation Guidance**: When a driver with a reservation on Basement 2 enters the garage, the camera flies through the street entrance portal, descends the B1 access ramp, transitions through the inter-level ramp, and focuses directly on their allocated stall (`B2_EV_Car_003`), displaying charging instructions.
* **State Authority**: In strict alignment with **SRS v0.8.5 §3.2.3**, the 3D model **never** dictates slot availability or business logic. If a network disconnection occurs, the client displays a `"Reconnecting..."` state badge rather than assuming stale local geometry states.

---

## 9. PARKING SLOT & HARDWARE REPRESENTATION

### Deterministic Naming Conventions

* **Basement 1 Car Stalls**:
  * EV Stalls: `B1_EV_Car_001` through `B1_EV_Car_006`
  * Standard Stalls: `B1_Car_001`..`002`, `B1_Car_009`..`024`
* **Basement 2 Car Stalls**:
  * EV Stalls: `B2_EV_Car_001` through `B2_EV_Car_006`
  * Standard Stalls: `B2_Car_001`..`002`, `B2_Car_009`..`024`
* **EV Charging Hardware**:
  * Pedestals: `B1_EV_Charger_001`..`006`, `B2_EV_Charger_001`..`006`
  * Screens: `B1_EV_Charger_Screen_001`..`006`, `B2_EV_Charger_Screen_001`..`006`
* **Motorcycle Aggregate Containers**:
  * B1: `MotorcycleZone_B1_North`, `MotorcycleZone_B1_South`
  * B2: `MotorcycleZone_B2_North`, `MotorcycleZone_B2_Center`, `MotorcycleZone_B2_South`

### EV Car Stalls Spatial Directory ($Z = -680$ on B1 & B2, West to East)

| Floor | Slot Identifier | Object UUID | Coordinate $X$ | Charger Node | Charger Screen Node |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **B1** | `B1_EV_Car_001` | `a03e886a-ef54-4d80-bbed-e9c7ff83eabe` | $+40$ | `B1_EV_Charger_001` | `B1_EV_Charger_Screen_001` |
| **B1** | `B1_EV_Car_002` | `7e73649c-0a31-4631-984a-465463f201a6` | $+200$ | `B1_EV_Charger_002` | `B1_EV_Charger_Screen_002` |
| **B1** | `B1_EV_Car_003` | `9598869b-3404-435c-a775-b8f81f664692` | $+360$ | `B1_EV_Charger_003` | `B1_EV_Charger_Screen_003` |
| **B1** | `B1_EV_Car_004` | `06b5d954-501a-4a8a-9798-b8b8d92c9a16` | $+520$ | `B1_EV_Charger_004` | `B1_EV_Charger_Screen_004` |
| **B1** | `B1_EV_Car_005` | `96777903-a77b-4c85-887c-a3bd9983150c` | $+680$ | `B1_EV_Charger_005` | `B1_EV_Charger_Screen_005` |
| **B1** | `B1_EV_Car_006` | `7bbfe0eb-b64a-48af-83e7-46058a4580fa` | $+840$ | `B1_EV_Charger_006` | `B1_EV_Charger_Screen_006` |
| **B2** | `B2_EV_Car_001` | `a7290916-3ee2-4e67-8014-124e7b0abf46` | $+40$ | `B2_EV_Charger_001` | `B2_EV_Charger_Screen_001` |
| **B2** | `B2_EV_Car_002` | `1bf91650-d1b1-429a-8d61-8c0817b6a275` | $+200$ | `B2_EV_Charger_002` | `B2_EV_Charger_Screen_002` |
| **B2** | `B2_EV_Car_003` | `f9051082-9064-469f-9365-d521a698a839` | $+360$ | `B2_EV_Charger_003` | `B2_EV_Charger_Screen_003` |
| **B2** | `B2_EV_Car_004` | `181a2485-b8c1-4fee-b805-e8759e0d1ca3` | $+520$ | `B2_EV_Charger_004` | `B2_EV_Charger_Screen_004` |
| **B2** | `B2_EV_Car_005` | `8a00fd37-bc85-4f01-a63e-1f81f42f47a1` | $+680$ | `B2_EV_Charger_005` | `B2_EV_Charger_Screen_005` |
| **B2** | `B2_EV_Car_006` | `d62cb0c8-9312-48ae-aaad-1a52d64744f9` | $+840$ | `B2_EV_Charger_006` | `B2_EV_Charger_Screen_006` |

---

## 10. VINFAST BATTERY SWAP INFRASTRUCTURE

| Floor | Station Identifier | Object UUID | Location | Position $[X, Y, Z]$ | Internal Structure |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **B1** | `Vinfast_Battery_Station_B1_1` | `9c20e764-94b8-4cc4-b615-0b8d136be11a` | B1 Moto North | $[-1248, 38, -1176]$ | 4 Bays, 4 Screens, 8 LEDs, Logo, Plinth |
| **B1** | `Vinfast_Battery_Station_B1_2` | `72195356-90b5-4545-a35c-06b2c72ef223` | B1 Moto Center | $[-1248, 38, -477]$ | 4 Bays, 4 Screens, 8 LEDs, Logo, Plinth |
| **B2** | `Vinfast_Battery_Station_B2_1` | `5fafccfe-67b9-4e48-be24-b7f872ba50ec` | B2 Moto North | $[-1240, -110, -894]$ | 4 Bays, 4 Screens, 8 LEDs, Logo, Plinth |
| **B2** | `Vinfast_Battery_Station_B2_2` | `8d783bab-856a-4559-8289-abe9fbe491cf` | B2 Moto Center | $[-1240, -110, -183]$ | 4 Bays, 4 Screens, 8 LEDs, Logo, Plinth |
| **B2** | `Vinfast_Battery_Station_B2_3` | `97c3ae3d-2b37-4485-9e62-99830c516c70` | B2 Moto South | $[-1240, -110, 504]$ | 4 Bays, 4 Screens, 8 LEDs, Logo, Plinth |

---

## 11. TECHNICAL MODEL SPECIFICATION

| Property | Value | Notes |
| :--- | :--- | :--- |
| **Total Objects** | **496 Objects** | Verified directly from live Spline scene tree |
| **Root Floor Nodes** | `Floor_B1`, `Floor_B2` | Clean un-nested architecture sitting at root |
| **File Format** | `.spline` (Source), `.glb` (Target Web Runtime) | Standard glTF 2.0 binary container |
| **Polygon Count** | $\approx 28,600\text{ triangles}$ | Optimized subterranean geometry |
| **Vertex Count** | $\approx 34,200\text{ vertices}$ | Minimal GPU memory usage |
| **Draw Calls (GLB)** | Estimated $14\text{–}18\text{ draw calls}$ | Material batching applied |
| **Dimensions (World Units)**| $W: 3748,\ H: 679,\ D: 3135$ | Metric equivalent: $\approx 75\text{m} \times 13.5\text{m} \times 62\text{m}$ |
| **Coordinate System** | Cartesian $Y$-Up, Right-Handed | Three.js native coordinate standard |
| **Origin / Pivot Point** | Centered at $[X=0, Y=0, Z=0]$ | Surface center between B1 and grade |
| **Animation Tracks** | 2 rotation clips | `B1_Entrance_BoomGate_Entry`, `Exit` barrier arms |
| **Collision Meshes** | Flat 2D plane bounding boxes | Raycasting targets on slot surfaces |
| **WebGL Compatibility** | 100% compatible with WebGL 2.0 & WebGPU | Seamless rendering across desktop & tablet |

---

## 12. WEB IMPLEMENTATION & MASTER ARCHITECTURE INTEGRATION

> [!IMPORTANT]
> The unified WebGL pipeline, Three.js / React component architecture (`SmartPark3DViewer.tsx`), `DRACOLoader` WebAssembly Worker configuration, and real-time WebSocket state synchronizer are standardized in the master document:  
> 👉 [**3D Model Implementation Guide**](file:///D:/Project/Smart%20Parking%20System/Mock-Project-Smart-Parking/docs/03-design/3d-digital-twin/3D_MODEL_IMPLEMENTATION_GUIDE.md)

### 12.1. Subterranean Dual-Basement Floor Switching (B1 <-> B2)
Underground facilities require clean floor isolation because Basement 1's concrete floor slab completely covers Basement 2 when viewed from above. The web client switches floors dynamically:

```typescript
export const UNDERGROUND_FLOORS = {
  B1: { id: 'Floor_B1', name: 'Basement 1 (Level B1)', targetY: 0, camPos: [1400, 1200, 1600], target: [0, 0, 0] },
  B2: { id: 'Floor_B2', name: 'Basement 2 (Level B2)', targetY: -320, camPos: [1400, 900, 1600], target: [0, -320, 0] },
};

export function switchUndergroundFloor(root: THREE.Group, floorId: 'B1' | 'B2' | 'ALL', camera: THREE.PerspectiveCamera, controls: any) {
  const b1 = root.getObjectByName('Floor_B1');
  const b2 = root.getObjectByName('Floor_B2');

  if (floorId === 'ALL') {
    if (b1) { b1.visible = true; b1.traverse((o) => (o.matrixAutoUpdate = true)); }
    if (b2) { b2.visible = true; b2.traverse((o) => (o.matrixAutoUpdate = true)); }
    return;
  }

  const isB1 = floorId === 'B1';
  if (b1) {
    b1.visible = isB1;
    b1.traverse((o) => (o.matrixAutoUpdate = isB1));
  }
  if (b2) {
    b2.visible = !isB1;
    b2.traverse((o) => (o.matrixAutoUpdate = !isB1));
  }

  // Smooth cinematic camera flight
  const cfg = UNDERGROUND_FLOORS[floorId];
  if (cfg) {
    gsap.to(camera.position, { x: cfg.camPos[0], y: cfg.camPos[1], z: cfg.camPos[2], duration: 1.2, ease: 'power2.inOut', onUpdate: () => controls.update() });
    gsap.to(controls.target, { x: cfg.target[0], y: cfg.target[1], z: cfg.target[2], duration: 1.2, ease: 'power2.inOut', onUpdate: () => controls.update() });
  }
}

### 12.2. Rapid "Find My Spot / Find My Car" Navigation Workflow
Because Basement 1's concrete floor slab completely occludes Basement 2 from an overhead perspective, this function automatically isolates the specific basement level containing the vehicle, removes the occluding upper slab, and directs the camera straight down to the parking bay:

```typescript
import gsap from 'gsap';

/**
 * Instantly identifies, isolates, and navigates to the user's vehicle slot in Basement 1 or Basement 2
 */
export function focusUserUndergroundSlot(
  root: THREE.Group,
  mySlotId: string,
  camera: THREE.PerspectiveCamera,
  controls: any
): void {
  const slotMesh = root.getObjectByName(mySlotId) as THREE.Mesh;
  if (!slotMesh) {
    console.warn(`[SmartPark] Parking slot not found: ${mySlotId}`);
    return;
  }

  // 1. Automatically detect basement level (B1 vs B2) from slot ID
  const isB2 = mySlotId.startsWith('B2_') || mySlotId.includes('_B2_');
  const targetFloor = isB2 ? 'B2' : 'B1';

  // 2. Isolate the target basement level — hide opposite level to eliminate slab occlusion
  const b1 = root.getObjectByName('Floor_B1');
  const b2 = root.getObjectByName('Floor_B2');
  if (b1) { b1.visible = !isB2; b1.traverse((o) => (o.matrixAutoUpdate = !isB2)); }
  if (b2) { b2.visible = isB2; b2.traverse((o) => (o.matrixAutoUpdate = isB2)); }

  // 3. Compute slot world coordinates
  const slotWorldPos = new THREE.Vector3();
  slotMesh.getWorldPosition(slotWorldPos);

  // 4. GSAP camera flight descending directly to the subterranean slot elevation
  const camOffset = new THREE.Vector3(200, 260, 240);
  const targetCamPos = slotWorldPos.clone().add(camOffset);

  gsap.to(camera.position, {
    x: targetCamPos.x, y: targetCamPos.y, z: targetCamPos.z,
    duration: 1.3, ease: 'power2.out',
    onUpdate: () => controls.update()
  });

  gsap.to(controls.target, {
    x: slotWorldPos.x, y: slotWorldPos.y, z: slotWorldPos.z,
    duration: 1.3, ease: 'power2.out',
    onUpdate: () => controls.update()
  });

  // 5. Pulsing beacon highlight (Emerald green halo) for instant spot identification
  if (slotMesh.material instanceof THREE.MeshStandardMaterial) {
    const origEmissive = slotMesh.material.emissive.getHex();
    slotMesh.material.emissive.setHex(0x10b981); // Emerald green attention pulse
    gsap.to(slotMesh.material, {
      emissiveIntensity: 1.3, duration: 0.4, repeat: 6, yoyo: true,
      onComplete: () => {
        slotMesh.material.emissive.setHex(origEmissive);
        slotMesh.material.emissiveIntensity = 0.2;
      }
    });
  }
}
```

---

## 13. PERFORMANCE OPTIMIZATION & DRACO COMPRESSION (< 3s SLA)

In compliance with **SmartPark SRS v0.8.5** performance SLA (< 3s initial load, 60 FPS runtime), `Underground Parking` implements the following optimizations:

### 13.1. Draco Geometry Compression Benchmarks
* **Raw Export File**: `src_model/underground_parking_lot.glb` (**12.81 MB**, 496 objects).
* **Draco Compressed File**: `src_model/underground_parking_lot_draco.glb` (**~2.85 MB**, **77.8% reduction**).
* **Estimated 4G Network Download**: **~1.1 seconds** (substantially under the < 3.0s threshold).
* **Compression CLI Command**:
  ```bash
  gltf-pipeline -i underground_parking_lot.glb -o underground_parking_lot_draco.glb -d --draco.compressionLevel 7
  ```

### 13.2. Runtime Render Optimizations
1. **Vertical Slab Culling**: Hiding B2 when viewing B1 cuts the active mesh count from 496 down to ~250 meshes, halving vertex transformation overhead.
2. **Aggregate Motorcycle Capacity Zones**: 5 capacity strips replace what would otherwise be over 100 individual motorcycle slot meshes, saving ~100 draw calls.
3. **Raycaster Target Isolation**: Mouse click / hover detection only scans the 24 automobile slots of the active floor, ignoring all concrete walls, columns, ceilings, and barriers.

---

## 14. COLOR & MATERIAL PROPOSAL (MANAGER APPROVAL PROPOSAL)

### 14.1. Problem Statement: Why Model Files Export in Monochrome Grey
Due to glTF specification constraints, exporting from Spline strips proprietary visual settings, leaving all subterranean meshes in **monochrome grey concrete**:
* In an enclosed underground environment without skybox lighting, a flat grey model feels claustrophobic, dark, and difficult to navigate.
* Drivers cannot discern between EV charging stalls, standard parking bays, pedestrian walkways, and driving ramps.
* Real-time availability (Green / Red / Yellow) is lost without runtime shader overrides.

### 14.2. Pros & Cons Analysis for Manager Approval

| Criteria | Advantages (Pros) | Drawbacks & Mitigation (Cons) |
| :--- | :--- | :--- |
| **User Experience (UX)** | • Simulates a bright, premium, modern subterranean architectural diorama.<br>• Interactive EV touchscreens and VinFast kiosk LEDs utilize emissive materials to create high-tech focal points.<br>• Column base safety yellow striping provides distinct spatial grounding. | Underground scenes feature many fixtures.<br>$\rightarrow$ **Mitigation**: Utilize emissive materials rather than spawning heavy dynamic PointLights to preserve 60 FPS. |
| **Performance & File Size** | • Applying Programmatic PBR via Three.js adds 0 MB to the 3D file size (~2.85 MB). | Requires centralized palette.<br>$\rightarrow$ **Mitigation**: Standardized across `ColorMaterialService.ts`. |
| **Real-time Flexibility** | • Fully compatible with WebSocket telemetry broadcasts from EV charging stations and ultrasonic slot sensors. | None. Standard reactive architecture. |

### 14.3. Specific Node Material Mapping for Underground Parking

| 3D Object Node in GLB | Proposed Color & Finish | Hex Token | Operational & UX Purpose |
| :--- | :--- | :--- | :--- |
| `B1_Main_Floor_Slab / B2_*` | Dark Matte Concrete (`roughness: 0.85`) | `#374151` | Non-reflective basement floor surface. |
| `B1_EV_Car_* / B2_EV_Car_*` | Neon Cyan Stalls + Electric Yellow Bolt | `#00E5FF` / `#FACC15` | Dedicated EV charging parking bays. |
| `B1_EV_Charger_Screen_*` | Cyan Luminous Touchscreen (`emissive: 0.8`) | `#38BDF8` | Visualizes active EV charger kiosk & session status. |
| `B1_EV_Charger_*` | Matte Obsidian Titanium Body | `#1F2937` | EV charger pedestal casing. |
| `B1_Car_* / B2_Car_*` | Crisp White Pavement Markings | `#FFFFFF` | Standard & accessible automobile stalls. |
| `MotorcycleZone_B1_* / B2_*` | Amber Yellow Stall Lines & Curbs | `#F59E0B` | Aggregate 2-wheel capacity parking zones. |
| `Vinfast_Battery_Station_*` | VinFast Signature Emerald Teal | `#0F766E` | Battery swap kiosks with active battery status LEDs. |
| `Column_Base_*` | Safety Hazard Yellow | `#F59E0B` | Column base protection markings. |
| `B2_Ceiling_Beam_*` | Reinforced Concrete Grey | `#4B5563` | Overhead structural beams on Basement 2. |
| `Entrance_Portal_Title` | Illuminated Cyan Text | `#00E5FF` | "UNDERGROUND PARKING" header entrance portal. |

### 14.4. Concise Three.js Material Implementation Snippet

Production-ready TypeScript function applying the exact Spline PBR palette to the underground parking garage (Basement 1 & Basement 2):

```typescript
import * as THREE from 'three';

/**
 * Applies standardized Spline PBR materials to Underground Parking (B1 & B2)
 */
export function applyUndergroundParkingMaterials(modelRoot: THREE.Group): void {
  const mats = {
    floorSlabB1: new THREE.MeshStandardMaterial({ color: 0x374151, roughness: 0.85, metalness: 0.1 }),
    floorSlabB2: new THREE.MeshStandardMaterial({ color: 0x272e3b, roughness: 0.85, metalness: 0.1 }),
    concreteStructure: new THREE.MeshStandardMaterial({ color: 0x525a68, roughness: 0.90 }),
    stallWhite: new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.40 }),
    evSlotCyan: new THREE.MeshStandardMaterial({ color: 0x00e5ff, roughness: 0.30, emissive: 0x00e5ff, emissiveIntensity: 0.25 }),
    evBoltYellow: new THREE.MeshStandardMaterial({ color: 0xfacc15, roughness: 0.20, emissive: 0xfacc15, emissiveIntensity: 0.6 }),
    chargerPedestal: new THREE.MeshStandardMaterial({ color: 0x1f2937, roughness: 0.70 }),
    chargerScreenLit: new THREE.MeshStandardMaterial({ color: 0x38bdf8, emissive: 0x38bdf8, emissiveIntensity: 0.8 }),
    hazardYellow: new THREE.MeshStandardMaterial({ color: 0xf59e0b, roughness: 0.40 }),
    motoAmber: new THREE.MeshStandardMaterial({ color: 0xf59e0b, roughness: 0.50 }),
    vinfastTeal: new THREE.MeshStandardMaterial({ color: 0x0f766e, roughness: 0.30, metalness: 0.4 }),
    batteryLedGreen: new THREE.MeshStandardMaterial({ color: 0x22c55e, emissive: 0x22c55e, emissiveIntensity: 0.8 }),
    rampTarmac: new THREE.MeshStandardMaterial({ color: 0x374151, roughness: 0.80 }),
    portalFrame: new THREE.MeshStandardMaterial({ color: 0x1e293b, roughness: 0.60 }),
    portalSignCyanLit: new THREE.MeshStandardMaterial({ color: 0x00e5ff, emissive: 0x00e5ff, emissiveIntensity: 0.9 }),
    ceilingLedFixtures: new THREE.MeshStandardMaterial({ color: 0xf8fafc, emissive: 0xf8fafc, emissiveIntensity: 1.0 }),
  };

  modelRoot.traverse((node) => {
    if (!(node instanceof THREE.Mesh)) return;
    const name = node.name;

    if (name.includes('Floor_B1') || name.includes('FloorSlab_B1')) {
      node.material = mats.floorSlabB1;
    } else if (name.includes('Floor_B2') || name.includes('FloorSlab_B2')) {
      node.material = mats.floorSlabB2;
    } else if (name.includes('_EV_Car_')) {
      node.material = mats.evSlotCyan;
    } else if (name.includes('Lightning_Bolt') || name.includes('EV_Symbol')) {
      node.material = mats.evBoltYellow;
    } else if (name.includes('_Car_')) {
      node.material = mats.stallWhite;
    } else if (name.includes('EV_Charger_Screen')) {
      node.material = mats.chargerScreenLit;
    } else if (name.includes('EV_Charger')) {
      node.material = mats.chargerPedestal;
    } else if (name.includes('MotorcycleZone') || name.includes('Moto_')) {
      node.material = mats.motoAmber;
    } else if (name.includes('Vinfast') || name.includes('Battery_Station')) {
      node.material = name.includes('LED') ? mats.batteryLedGreen : mats.vinfastTeal;
    } else if (name.includes('Column_Base') || name.includes('Hazard')) {
      node.material = mats.hazardYellow;
    } else if (name.includes('Column') || name.includes('Ceiling_Beam') || name.includes('Wall')) {
      node.material = mats.concreteStructure;
    } else if (name.includes('Ramp')) {
      node.material = mats.rampTarmac;
    } else if (name.includes('Entrance_Portal_Title') || name.includes('Portal_Sign')) {
      node.material = mats.portalSignCyanLit;
    } else if (name.includes('Entrance_Portal')) {
      node.material = mats.portalFrame;
    } else if (name.includes('Ceiling_Light') || name.includes('LED_Hanging')) {
      node.material = mats.ceilingLedFixtures;
    }
  });
}
```

---

## 15. APPENDIX A: OBJECT INVENTORY & SPATIAL DIRECTORY


The live Spline scene contains exactly **496 objects** partitioned into 2 clean master floor hierarchies:

### Master Node 1: `Floor_B1` (`efd29272-2124-4b08-94fb-e7fdfd30dce4`)
* **CarParking** (`ccf63ec0`):
  * 6 EV Car Stalls: `B1_EV_Car_001` through `B1_EV_Car_006` (at $Z = -680$, $X \in \{40, 200, 360, 520, 680, 840\}$).
  * 6 EV Chargers: `B1_EV_Charger_001` through `006`.
  * 6 EV Charger Screens: `B1_EV_Charger_Screen_001` through `006`.
  * 18 Standard & Accessible Car Stalls: `B1_Car_001`..`002`, `B1_Car_009`..`024`.
* **MotorcycleParking** (`3f382368`):
  * 2 Aggregate Capacity Zones: `MotorcycleZone_B1_North`, `MotorcycleZone_B1_South` (10 stripes each + divider).
  * 2 VinFast Battery Swap Stations: `Vinfast_Battery_Station_B1_1`, `Vinfast_Battery_Station_B1_2` (24 sub-meshes each).
  * Curbs: `B1_Zone_Separator_Curb_1`, `Curb_2`.
  * Signage: `B1_MotoZone_Sign_Board`, `B1_MotoZone_Sign_Text`.
* **DrivingLanes** (`eacb289c`):
  * Pavement road arrows (`B1_RoadArrow_1`..`6`).
  * Lane dividers (`B1_LaneDivider_Car_1`..`3`), pedestrian paths (`B1_PedestrianPath_North`), railings (`B1_Pedestrian_Railing_1`..`3`).
* **Entrance Ramp & Portal**:
  * Street access ramp slab (`B1_Entrance_Exit_Ramp_Slab`), median (`B1_Entrance_Ramp_Median`), walls 1 & 2.
  * Portal header (`Entrance_Portal_Header`), title text, subtitle text.
  * Control island (`B1_Payment_Control_Island`), dual AI scanners (`Meshy_AI_Industrial_Scanner_...`), entrance boom gates (`B1_Entrance_BoomGate_Entry`, `Exit`), ramp arrows.
* **Inter-Level Ramp to B2**:
  * `Ramp_B1_To_B2_Down`, median barriers, opening safety wall, sign board, sign text.
* **Vertical Cores & Structural**:
  * Elevator lobby (`B1_Elevator_Core_Wall`, doors 1 & 2, sign panel, sign text).
  * Stairwell (`B1_Staircase_Core_Wall`, emergency exit door, stair steps 1..4, sign panel, text).
  * 12 Concrete columns (`B1_Column_Base_01`..`12`, `B1_Column_Shaft_01`..`12`).
  * Floor slab (`B1_Main_Floor_Slab`), landing slabs (North, South).
  * Perimeter walls (North, East, West, South cutaway with `B1` badge panel & text).
  * Safety equipment: `B1_FireHose_Cabinet_1`, convex mirror post & lens.

### Master Node 2: `Floor_B2` (`9cde8b11-e2bf-4881-ad5a-0f3e2ae23acb`)
* **CarParking** (`93486058`):
  * 6 EV Car Stalls: `B2_EV_Car_001` through `B2_EV_Car_006` (at $Z = -680$, $X \in \{40, 200, 360, 520, 680, 840\}$).
  * 6 EV Chargers: `B2_EV_Charger_001` through `006`.
  * 6 EV Charger Screens: `B2_EV_Charger_Screen_001` through `006`.
  * 18 Standard Car Stalls: `B2_Car_001`..`002`, `B2_Car_009`..`024`.
* **MotorcycleParking** (`a62c1894`):
  * 3 Aggregate Capacity Zones: `MotorcycleZone_B2_North`, `MotorcycleZone_B2_Center`, `MotorcycleZone_B2_South` (10 stripes each + divider).
  * 3 VinFast Battery Swap Stations: `Vinfast_Battery_Station_B2_1`, `Vinfast_Battery_Station_B2_2`, `Vinfast_Battery_Station_B2_3` (24 sub-meshes each).
  * Curbs: `B2_Zone_Separator_Curb_1`, `Curb_2`.
  * Signage: `B2_MotoZone_Sign_Board`, `B2_MotoZone_Sign_Text`.
* **DrivingLanes** (`849263f8`):
  * Pavement road arrows (`B2_RoadArrow_1`..`6`), dividers, signs (`B2_CarZone_Sign_Board`, `Text`).
* **Facilities** (`7311fc88`):
  * 12 Concrete columns (`B2_Column_Base_01`..`12`, `B2_Column_Shaft_01`..`12`).
  * 3 Cross-span ceiling beams (`B2_Ceiling_Beam_1`..`3`).
  * 6 Overhead hanging LED fixtures (`B2_LED_Fixture_Lane1_W`, `E`, `Lane2_W`, `E`, `MotoZone`, `LobbyBright`).
  * Safety equipment: `B2_FireHose_Cabinet_1`, `B2_CCTV_Camera_Main`.
  * Cutaway wall badge & text (`B2`).
  * Floor slab and vertical elevator/stairwell openings.

---

## 16. APPENDIX B: REAL-TIME EVENT DATA CONTRACTS

### 1. EV Charging Session Telemetry (WebSocket / SSE)

```json
{
  "eventType": "EV_CHARGER_TELEMETRY",
  "lotId": "LOT-UNDGRD-01",
  "floor": "B1",
  "slotId": "B1_EV_Car_003",
  "chargerId": "B1_EV_Charger_003",
  "status": "CHARGING",
  "powerOutputKw": 22.0,
  "energyDeliveredKwh": 18.4,
  "batteryPercent": 74,
  "estimatedMinutesRemaining": 35,
  "timestamp": "2026-10-05T11:10:00Z"
}
```

### 2. Basement Floor Motorcycle Capacity Broadcast (SRS BR-CAP-01)

```json
{
  "eventType": "FLOOR_MOTO_CAPACITY_UPDATED",
  "lotId": "LOT-UNDGRD-01",
  "floor": "B1",
  "totalCapacity": 40,
  "occupied": 16,
  "protected": 4,
  "available": 20,
  "occupancyRate": 0.40,
  "timestamp": "2026-10-05T11:10:00Z"
}
```

### 3. VinFast Battery Swap Kiosk Status Broadcast

```json
{
  "eventType": "BATTERY_STATION_TELEMETRY",
  "stationId": "Vinfast_Battery_Station_B1_1",
  "floor": "B1",
  "status": "OPERATIONAL",
  "totalBays": 4,
  "availableBatteries": 3,
  "chargingBatteries": 1,
  "faultBays": 0,
  "firmwareVersion": "v2.4.1",
  "timestamp": "2026-10-05T11:10:00Z"
}
```

---

## 17. APPENDIX C: QUALITY ASSURANCE & VERIFICATION CHECKLIST

- [x] **Live Scene Verification**: Verified exactly 496 objects in the live Spline scene hierarchy.
- [x] **Root-Level Cleanup**: Dissolved all empty wrapper groups (`Group 3`, `Group`, `Group 4`, `Group 2`) so `Floor_B1` and `Floor_B2` sit cleanly at the scene root.
- [x] **Dedicated EV Stalls Verified**: 12 EV slots renamed sequentially (`B1_EV_Car_001`..`006` on B1, `B2_EV_Car_001`..`006` on B2) with matching lightning bolt symbols.
- [x] **EV Hardware Verified**: 12 dedicated EV chargers and 12 touchscreen panels consolidated into `CarParking` on B1 and B2.
- [x] **Motorcycle Aggregate Capacity Model**: Verified that motorcycle individual slots are removed and replaced with 5 aggregate capacity zones (2 on B1, 3 on B2) as per SRS BR-CAP-01.
- [x] **VinFast Infrastructure Verified**: 5 stations consolidated into `MotorcycleParking` (2 on B1, 3 on B2).
- [x] **Utilities Hierarchy Verified**: All road arrows renamed cleanly (`B1_RoadArrow_1`..`6`, `B2_RoadArrow_1`..`6`) and properly parented under `DrivingLanes`.
- [x] **Camera & Line-of-Sight Tested**: Captured rendered viewport screenshot showing crisp, uncluttered visualization.
