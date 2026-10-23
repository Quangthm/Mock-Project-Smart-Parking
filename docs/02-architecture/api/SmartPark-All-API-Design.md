# SmartPark — API design navigation and historical inventory

Stable compatibility entry for former All-API links. The active **v1.1 documentation review candidate based on approved decisions** is [the API index](README.md) and [shared contracts](v1.1-contracts.md); it does not claim implementation acceptance or completed wire agreement.

The former file was an implementation snapshot dated 12 October 2026, not the authority for v1.1. Its complete bytes, endpoint tables, JSON examples, PlantUML diagrams, source references, errors and unresolved implementation notes are preserved in the [historical snapshot](../../archive/api/SmartPark-All-API-Design-2026-10-12.md). The [archive provenance](../../archive/api/README.md) records hashes. Superseded token lifetimes, login profile data, immediate Operator access, tenant-only onboarding, arbitrary vehicle URLs and legacy Parking ownership apply only to that historical code observation.

## Current contract mapping

| Historical area | Active target |
|---|---|
| U01, U10–U19 authentication/MFA/delivery | [Shared login, recovery and delivery](v1.1-contracts.md#authentication-and-normal-login) |
| U02–U05 Driver | [Driver registration](../../03-design/api/driver-register-api-design.md) |
| U06–U09 Partner registration | [First-lot setup and verification](../../03-design/api/owner-register-api-design.md) |
| U21–U23 Admin | [Directory and restricted access changes](../../03-design/api/admin-manage-users-api-design.md) |
| U24–U27 Operator | [Invitation, activation and current lot grants](../../03-design/api/owner-create-operator-api-design.md) |
| U28–U30 initial review | [Document versions, review and provisioning](../../03-design/api/admin-approve-owner-api-design.md) |
| U31–U33 vehicles | [Binding, media and soft removal](../../03-design/api/vehicle-registration-api-design.md) |
| U20, P01–P13, R01–R03 scope/structure/internal contracts | [Facility and Operations target](../../03-design/api/parking-structure-api-design.md); unchanged historical request details remain available below, not newly approved. |
| Discovery and monthly/yearly products | [Operations/Payment integration](v1.1-contracts.md#discovery-and-parking-products) |

## Historical endpoint anchors

These stable anchors preserve navigation; **each link below is historical implementation evidence**. D01 below is the old inventory endpoint ID, distinct from the v1.1 business decision D01.

<a id="u01"></a>

### U01 — Đăng nhập bằng password

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u01).

<a id="u02"></a>

### U02 — Đăng ký Driver

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u02).

<a id="u03"></a>

### U03 — Xác minh Driver OTP

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u03).

<a id="u04"></a>

### U04 — Gửi lại Driver OTP

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u04).

<a id="u05"></a>

### U05 — Khôi phục pending Driver registration

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u05).

<a id="u06"></a>

### U06 — Đăng ký Owner

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u06).

<a id="u07"></a>

### U07 — Xác minh email OTP Owner

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u07).

<a id="u08"></a>

### U08 — Gửi lại email OTP Owner

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u08).

<a id="u09"></a>

### U09 — Khôi phục pending Owner registration

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u09).

<a id="u10"></a>

### U10 — Yêu cầu OTP đăng nhập

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u10).

<a id="u11"></a>

### U11 — Đăng nhập OTP và optional MFA

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u11).

<a id="u12"></a>

### U12 — Lấy identity hiện tại

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u12).

<a id="u13"></a>

### U13 — Rotate refresh token

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u13).

<a id="u14"></a>

### U14 — Đăng xuất session hiện tại

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u14).

<a id="u15"></a>

### U15 — Đổi password qua onboarding link

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u15).

<a id="u16"></a>

### U16 — Khởi tạo MFA

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u16).

<a id="u17"></a>

### U17 — Bật MFA

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u17).

<a id="u18"></a>

### U18 — Xem trạng thái delivery

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u18).

<a id="u19"></a>

### U19 — Yêu cầu retry delivery

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u19).

<a id="u20"></a>

### U20 — Xác minh Owner tenant scope

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u20).

<a id="u21"></a>

### U21 — Admin tìm kiếm người dùng

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u21).

<a id="u22"></a>

### U22 — Admin xem chi tiết người dùng

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u22).

<a id="u23"></a>

### U23 — Admin khóa/mở khóa người dùng

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u23).

<a id="u24"></a>

### U24 — Owner tạo Operator

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u24).

<a id="u25"></a>

### U25 — Owner liệt kê Operator

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u25).

<a id="u26"></a>

### U26 — Operator xem assigned lots

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u26).

<a id="u27"></a>

### U27 — Xác minh Operator slot scope

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u27).

<a id="u28"></a>

### U28 — Admin liệt kê đơn Owner

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u28).

<a id="u29"></a>

### U29 — Admin xem đơn Owner

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u29).

<a id="u30"></a>

### U30 — Admin duyệt/từ chối Owner

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u30).

<a id="u31"></a>

### U31 — Driver liệt kê xe

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u31).

<a id="u32"></a>

### U32 — Driver đăng ký xe

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u32).

<a id="u33"></a>

### U33 — Driver cập nhật xe

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#u33).

<a id="p01"></a>

### P01 — Owner liệt kê bãi

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#p01).

<a id="p02"></a>

### P02 — Owner tạo bãi

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#p02).

<a id="p03"></a>

### P03 — Owner đọc cấu trúc bãi

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#p03).

<a id="p04"></a>

### P04 — Operator đọc operational layout

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#p04).

<a id="p05"></a>

### P05 — Operator mark slot BACKUP

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#p05).

<a id="p06"></a>

### P06 — Owner yêu cầu impact operation

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#p06).

<a id="p07"></a>

### P07 — Owner đọc trạng thái operation

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#p07).

<a id="p08"></a>

### P08 — Owner thêm spatial unit

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#p08).

<a id="p09"></a>

### P09 — Owner thêm parking slot

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#p09).

<a id="p10"></a>

### P10 — Owner sửa cấu trúc/action

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#p10).

<a id="p11"></a>

### P11 — Owner thêm access path

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#p11).

<a id="p12"></a>

### P12 — Internal provision tenant

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#p12).

<a id="p13"></a>

### P13 — Internal validate active sites

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#p13).

<a id="r01"></a>

### R01 — Internal acquire structure hold

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#r01).

<a id="r02"></a>

### R02 — Internal inspect structure impact

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#r02).

<a id="r03"></a>

### R03 — Internal publish outcome/release hold

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#r03).

<a id="d01"></a>

### D01 — Development OpenAPI document

[Historical endpoint detail](../../archive/api/SmartPark-All-API-Design-2026-10-12.md#d01).
