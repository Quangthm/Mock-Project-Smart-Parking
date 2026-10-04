# Summary Front-End Smart Parking

## 1. Tổng quan

Front-End của dự án **SmartParking** được xây dựng theo hướng interactive web application, cung cấp giao diện và workflow cho 4 nhóm người dùng chính:

- Driver
- Owner
- Operator
- Admin

FE hiện tại không chỉ dừng ở UI tĩnh mà đã triển khai nhiều business flow ở phía client như authentication, booking, payment simulation, parking management, check-in/check-out, support ticket, notification, wallet và dashboard/report.

> **Lưu ý:** Dữ liệu hiện tại chủ yếu được mô phỏng và lưu bằng `localStorage` thông qua client-side store. Backend API, database thực tế, payment gateway và các dịch vụ bên ngoài chưa được tích hợp hoàn chỉnh.

---

## 2. Công nghệ Front-End

Project hiện sử dụng:

- **React 19**
- **TypeScript**
- **Vite**
- **React Router**
- **Tailwind CSS**
- **Recharts** cho dashboard và biểu đồ
- **MapTiler SDK** cho bản đồ
- **localStorage** cho client-side persistence
- Component-based architecture

---

## 3. Kiến trúc Front-End

Cấu trúc chức năng tổng thể:

```text
Smart Parking FE
│
├── Public Pages
│   ├── Landing
│   ├── Pricing
│   ├── Business
│   ├── About
│   ├── FAQ
│   ├── Support
│   ├── Careers
│   └── Policies
│
├── Authentication
│   ├── Sign In
│   └── Sign Up
│
├── Driver
│   ├── Dashboard
│   ├── Find Parking
│   ├── Booking
│   ├── Booking History
│   ├── Vehicle
│   ├── Subscription
│   ├── Wallet
│   └── Support
│
├── Owner
│   ├── Dashboard
│   ├── Parking Lot
│   ├── Site
│   ├── Operator
│   ├── Policy
│   └── Revenue
│
├── Operator
│   ├── Check-in / Check-out
│   ├── Slot Management
│   ├── Lot Status
│   ├── Emergency
│   ├── Ticket
│   ├── Finance
│   └── Cashier
│
├── Admin
│   ├── Overview
│   ├── Applications
│   ├── Driver Accounts
│   ├── Audit Log
│   ├── System Config
│   └── Settings
│
├── Shared Components
│   ├── Navbar
│   ├── Sidebar
│   ├── Notification
│   ├── Modal
│   ├── Logo
│   └── Icons
│
└── Client Data Layer
    └── localStorage Store
```

---

# 4. Authentication & Account

## 4.1 Sign In

FE đã triển khai:

- Email
- Password
- Show/hide password
- Kiểm tra tài khoản
- Kiểm tra trạng thái account
- Login theo Role (Driver/Owner/Admin/Operator)
- Điều hướng đến dashboard tương ứng từng role
- Operator được sử dụng khi được owner cấp tài khoản

## 4.2 Sign Up

Driver có thể đăng ký tài khoản với:

- Full name
- Email
- Phone
- Password
- Confirm password

### Password validation

Password được kiểm tra:

- Tối thiểu 8 ký tự
- Tối đa 15 ký tự
- Có chữ hoa
- Có chữ thường
- Có số
- Có ký tự đặc biệt

Có Password Strength indicator.

## 4.3 Owner / Business Registration

Owner/Business Registration có quy trình đăng kí riêng:

- Business name
- Owner information
- Email
- Phone
- Parking lot type
- Partner Agreement
- Terms/Agreement
- Accept Agreement

Sau khi đăng ký thì sẽ đi theo quy trình:

```text
Owner
  ↓
Pending Approval
  ↓
Admin Review
  ↓
Approve / Reject
```

---

# 5. Public Website

FE đã xây dựng các public pages:

- Landing Page
- Pricing
- Business
- About
- Affiliates
- Careers
- FAQ
- Support
- Terms of Service
- Privacy Policy
- Payment & Refund Policy
- Pending Approval

## 5.1 Landing Page

Bao gồm:

- Hero section
- Carousel
- How It Works
- Benefits
- Payment methods
- CTA
- SmartParking branding

Payment method UI:

- Visa
- Apple Pay
- MoMo
- VNPAY
- ZaloPay
- QR

---

# 6. Driver

Driver là role có nhiều workflow FE nhất.

## 6.1 Driver Dashboard

Bao gồm:

- Dashboard overview
- Navigation sidebar
- Booking
- Booking History
- Parking Search
- Subscription
- Support
- Wallet
- Profile
- Vehicle Management

---

## 6.2 Find Parking

FE tích hợp **MapTiler SDK** để hiển thị bản đồ.

Các chức năng:

- Hiển thị bản đồ
- Hiển thị parking lot markers
- Search parking lot
- Filter parking lot
- Find current location
- Tính khoảng cách
- Hiển thị availability
- Chọn parking lot

Parking lot data gồm:

- Name
- Address
- Latitude
- Longitude
- Total slots
- Available slots
- Occupied slots
- Reserved slots

---

## 6.3 Booking Flow

Flow chính:

```text
Find Parking
    ↓
Select Parking Lot
    ↓
Select Floor
    ↓
Select Slot
    ↓
Select Time
    ↓
Select Vehicle
    ↓
Payment
```

Parking slot có các trạng thái:

- Available
- Occupied
- Reserved

Booking lưu các thông tin:

- Booking ID
- Parking lot
- Slot
- Vehicle
- License plate
- Start time
- End time
- Amount
- Deposit
- Payment method
- Booking status

---

## 6.4 Booking Status

Booking hỗ trợ các trạng thái:

- Pending
- Confirmed
- Checked-in
- Completed
- Cancelled

Payment status:

- Pending
- Paid
- Failed

Có flow:

**Pay Now**

cho booking chưa thanh toán.

---

## 6.5 Vehicle Management

Driver có thể:

- Add vehicle
- Edit vehicle
- Delete vehicle
- Chọn Car / Motorcycle
- Chọn Vietnam / International plate
- Nhập license plate
- Upload/ghi nhận license plate image

Giới hạn:

> Tối đa 3 vehicles / Driver.

Có kiểm tra license plate bị trùng.

---

## 6.6 Booking History

History được tách thành:

### Completed Bookings

Hiển thị các booking đã hoàn thành.

### Ticket History

Hiển thị các ticket Driver gửi Operator.

Ticket status:

- Processing
- Resolved

---

## 6.7 Payment

Payment UI hỗ trợ:

- Visa
- Apple Pay
- MoMo
- VNPAY
- ZaloPay
- QR

Được sử dụng cho:

- Booking
- Subscription
- Wallet Top-up

> Payment hiện là simulation phía Front-End, chưa phải payment gateway thật.

---

## 6.8 Subscription / Parking Pass

Driver có thể:

- Xem parking lot
- Buy Monthly Pass
- Buy Annual Pass
- Chọn payment method
- Tạo package order
- Xem package orders
- Xem active passes

Package:

- Monthly
- Annual

---

## 6.9 Wallet

Wallet có:

- Current balance
- Top up
- Preset amount
- Custom amount
- Payment method
- Transaction history

Transaction được lưu phía client.

---

## 6.10 Support / Ticket

Driver có thể tạo support ticket:

- Subject
- Message
- Related Booking

Ticket được gửi đến Operator.

Operator có thể:

- Mark In Progress
- Resolve
- Reopen

Driver nhận trạng thái:

- Processing
- Resolved

---

# 7. Notification System

FE có Notification Bell và notification system.

Các notification type:

- Payment Success
- Payment Failed
- Parking Expiring
- Login Success
- Package Purchase Success
- Ticket Submitted
- Ticket Resolved
- New Application
- New Driver
- Driver Parked
- Employee Created
- Policy Updated

Có reminder khi booking sắp hết thời gian, ví dụ khoảng 15 phút trước khi kết thúc.

---

# 8. Owner

## 8.1 Owner Dashboard

Dashboard cung cấp:

- Performance overview
- Revenue
- Completed bookings
- Occupancy
- Operators
- Revenue trend

Sử dụng **Recharts** để hiển thị biểu đồ.

---

## 8.2 Parking Lot Management

Owner có thể:

- Create parking lot
- Edit parking lot
- Activate / Deactivate parking lot
- Xem số slot
- Xem available / occupied
- Thiết lập pricing

Pricing gồm:

- Hourly rate
- Daily rate
- Night rate
- Grace period
- Monthly pass
- Annual pass

---

## 8.3 Parking Lot Onboarding

Có wizard nhiều bước.

### Step 1 — Parking Information

- Name
- Address
- Total slots
- Floors

### Step 2 — Devices

- Camera
- RFID
- EV Charger
- Sensor
- Barrier

### Step 3 — Policy

- Partner Agreement
- Policy document

### Final

**Activate My Parking Lot**

---

## 8.4 Site Management

Owner có thể quản lý nhiều site:

- Add site
- Edit site
- Parking lot type
- Address
- Capacity
- Floors
- Slot generation
- Employee count

Parking lot types:

- Outdoor
- Basement
- Multi-storey

---

## 8.5 Operator Management

Owner có thể:

- Create Operator
- Name
- Email
- Password
- Role
- Site assignment

Operator roles:

- Financial
- Operation
- Cashier

Quản lý:

- Lock account
- Unlock
- Change role
- Change site
- Delete operator

---

## 8.6 Policy Management

Owner có thể cấu hình:

### Pricing Policy

- Day rate
- Night rate

### Deposit Policy

- Percentage
- Fixed amount

### Grace Period

### Violation Alert

- Wrong spot
- Overtime

### Warning Alert

- Grace period sắp hết

Có Payment Preview để preview cách tính tiền.

---

## 8.7 Revenue

Revenue Dashboard gồm:

- Total revenue
- Completed bookings
- Revenue trend
- Last 6 months
- Revenue theo parking lot

---

# 9. Operator

Operator được chia thành các nhóm chức năng:

```text
Operator
├── Operation
├── Support
├── Financial
└── Cashier
```

---

## 9.1 Check-in / Check-out

Operator nhập:

**License Plate**

→ Search booking.

### Check-in

```text
Booking:
Confirmed → Checked-in

Slot:
Reserved → Occupied
```

### Check-out

```text
Booking:
Checked-in → Completed

Slot:
Occupied → Available
```

Sau checkout, booking trở thành Completed và có thể xuất hiện trong Driver History.

---

## 9.2 Slot Management

Operator có thể:

- Xem slot
- Đổi floor
- Đổi slot number
- Đổi status
- Save changes

Slot status:

- Available
- Occupied
- Reserved

---

## 9.3 Parking Lot Status

Dashboard hiển thị:

- Available
- Occupied
- Reserved
- Utilization

Giúp Operator theo dõi nhanh tình trạng bãi.

---

## 9.4 Emergency / Incident

Operator có thể tạo incident:

- Wrong Spot
- Overtime
- Vehicle Damage
- Other

Thông tin:

- License plate
- Slot number
- Notes

Có workflow xử lý incident.

---

## 9.5 Ticket Management

Ticket dashboard gồm:

- Open tickets
- In Progress
- Resolved

Action:

- Mark In Progress
- Resolve
- Reopen

---

## 9.6 Financial / Cashier

Financial dashboard có:

- Completed bookings
- Vehicle
- Payment method
- Amount
- Revenue / reconciliation

Cashier có flow:

**Cashier Checkout → Collect Payment**

---

# 10. Admin

## 10.1 Admin Dashboard

Có:

- Total users
- Parking lots
- Bookings
- Revenue
- Pending applications
- Booking activity
- User distribution
- System health
- Recent activity

Charts:

- Booking activity
- User distribution

---

## 10.2 Owner Applications

Admin có thể xem:

**Owner Partnership Applications**

Thông tin:

- Pending Review
- Business name
- Owner
- Lot type
- Review note

Action:

- Approve
- Reject

Sau review chuyển sang:

**Previously Reviewed**

---

## 10.3 Driver Accounts

Admin có thể:

- View Driver
- Edit profile
- Activate
- Unlock
- Suspend
- Lock
- Reset password
- View vehicles

---

## 10.4 Audit Log

Audit Log lưu:

- User
- Role
- Action
- Details
- Timestamp

Ví dụ action:

- REGISTER
- BOOKING_CREATED
- CHECK-IN
- CHECK-OUT
- LOT_CREATED
- SYSTEM_INIT

---

## 10.5 System Configuration

Có:

### Device Connectivity

- Camera
- RFID
- EV Charger
- Sensor
- Barrier

### Platform Parameters

- Platform Fee
- Default Grace Period
- Maintenance Mode

---

## 10.6 Admin Settings

Admin có thể:

- Đổi Display Name
- Change Password
- Confirm Password

---

# 11. Shared UI Components

FE đã xây dựng các component dùng chung:

```text
components/
├── brand/
│   └── BrandLogo
│
├── forms/
│   └── PasswordVisibilityIcon
│
├── icon/
│   ├── ThemeIcon
│   └── UntitledIcon
│
├── layout/
│   ├── Navbar
│   ├── DashboardNavbar
│   ├── DashboardSidebar
│   └── Footer
│
├── modals/
│   └── OTPModal
│
└── navigation/
    ├── NotificationBell
    └── RoleRoute
```

Các thành phần dùng chung gồm:

- Navbar
- Dashboard Navbar
- Dashboard Sidebar
- Footer
- Notification Bell
- Role-based route
- Logo
- Icons
- OTP Modal
- Password visibility
- Theme controls

---

# 12. Theme & UI System

FE hỗ trợ:

- Light Theme
- Dark Theme
- Accent Color

Dashboard sử dụng `data-accent` để thay đổi màu chủ đạo.

Theme và một số UI preferences được lưu phía client.

---

# 13. Role-based Routing

Hệ thống sử dụng `RoleRoute` để bảo vệ và điều hướng người dùng theo role.

Ví dụ:

```text
Driver
  → Driver Dashboard

Owner
  → Owner Dashboard

Operator
  → Operator Dashboard

Admin
  → Admin Dashboard
```

Operator có thêm:

```text
Financial
Operation
Cashier
```

---

# 14. Client-side Data Layer

Project hiện có client-side store:

```text
src/lib/store.ts
```

Store quản lý nhiều loại dữ liệu:

- Users
- Parking Lots
- Bookings
- Vehicles
- Applications
- Audit Logs
- Tickets
- Notifications
- Parking Passes
- Package Orders
- Wallet Transactions
- Owner Policies

Dữ liệu được persist bằng `localStorage`.

Các key chính gồm:

```text
sp_users
sp_lots
sp_bookings
sp_applications
sp_audit_logs
sp_tickets
sp_vehicles
sp_notifications
sp_passes
sp_package_orders
sp_wallet_transactions
```

---

# 15. Các phần cần tích hợp tiếp

Để chuyển từ Front-End prototype sang hệ thống SmartParking thực tế, các phần FE cần kết nối với Backend:

## Backend API

Thay thế các dữ liệu mock/localStorage bằng API thực tế:

```text
Authentication API
User API
Parking Lot API
Parking Slot API
Booking API
Vehicle API
Payment API
Wallet API
Subscription API
Ticket API
Notification API
Owner Application API
Admin API
Audit Log API
```

## Database

Backend sẽ quản lý dữ liệu thực tế thay cho:

```text
localStorage
```

## Authentication

Cần tích hợp:

- JWT / Session
- Refresh token
- Role authorization
- Password hashing
- Account status

## Payment

Cần tích hợp payment gateway thực tế thay cho payment simulation.

## Email

Có thể tích hợp email service cho:

- Owner đăng ký parking lot
- Driver gửi support ticket
- Operator xử lý ticket
- Payment notification
- Booking notification
- Account notification

## Real-time Parking

Nếu hệ thống có hardware/IoT:

```text
Sensor
   ↓
Backend
   ↓
WebSocket / API
   ↓
SmartParking FE
   ↓
Real-time Slot Status
```

---

### Hạn chế hiện tại

Phần lớn dữ liệu vẫn đang chạy phía Front-End:

```text
FE
 ↓
Client Store
 ↓
localStorage
```

thay vì:

```text
FE
 ↓
REST API / WebSocket
 ↓
Backend
 ↓
Database
```

Do đó, bước tiếp theo quan trọng nhất là **thiết kế API contract và thay thế các mock/localStorage operation bằng Backend API**, đồng thời tích hợp authentication, payment, email và real-time parking data.


