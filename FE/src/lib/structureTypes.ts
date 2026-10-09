export type SpaceStatus = 'available' | 'occupied' | 'reserved' | 'maintenance' | 'disabled';
export type VehicleCategory = 'car' | 'motorcycle';

export interface ParkingSpaceItem {
  id: string;
  code: string;
  zoneId: string;
  floorId: string;
  lotId: string;
  vehicleType: VehicleCategory;
  status: SpaceStatus;
  currentBooking?: {
    bookingId: string;
    driverName: string;
    licensePlate: string;
    startTime: string;
    endTime: string;
  };
  maintenanceNote?: string;
  lastUpdated: string;
}

export interface ParkingZoneItem {
  id: string;
  floorId: string;
  lotId: string;
  name: string;
  vehicleType: VehicleCategory;
  status: 'active' | 'inactive';
  spaces: ParkingSpaceItem[];
}

export interface ParkingFloorItem {
  id: string;
  lotId: string;
  name: string;
  code: string;
  status: 'active' | 'inactive';
  zones: ParkingZoneItem[];
}

export interface ParkingStructureData {
  lotId: string;
  lotName: string;
  address: string;
  floors: ParkingFloorItem[];
}

export type PaymentTransactionStatus = 'paid' | 'pending' | 'failed' | 'refunded';

export interface PaymentTransactionRecord {
  id: string;
  driverId: string;
  bookingId: string;
  lotId: string;
  lotName: string;
  spaceCode?: string;
  amount: number;
  paymentMethod: 'qr' | 'momo' | 'vnpay' | 'visa' | 'applepay' | 'zalopay';
  paymentStatus: PaymentTransactionStatus;
  createdAt: string;
  failureReason?: string;
  refundInfo?: {
    refundedAt: string;
    amount: number;
    reason?: string;
  };
}
