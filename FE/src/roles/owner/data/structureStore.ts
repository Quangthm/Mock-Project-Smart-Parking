import type {
  ParkingFloorItem,
  ParkingZoneItem,
  ParkingSpaceItem,
  ParkingStructureData,
  SpaceStatus,
  VehicleCategory,
} from '../../../lib/structureTypes';
import { store as mainStore } from '../../../lib/store';

const STRUCTURES_KEY = 'sp_parking_structures';

function generateInitialSpaces(
  count: number,
  prefix: string,
  zoneId: string,
  floorId: string,
  lotId: string,
  vehicleType: VehicleCategory,
  sampleOccupiedIndexes: number[] = []
): ParkingSpaceItem[] {
  const spaces: ParkingSpaceItem[] = [];
  const now = new Date();

  for (let i = 1; i <= count; i++) {
    const code = `${prefix}${String(i).padStart(2, '0')}`;
    let status: SpaceStatus = 'available';

    if (sampleOccupiedIndexes.includes(i)) {
      status = 'occupied';
    } else if (i === count && count > 5) {
      status = 'maintenance';
    } else if (i === count - 1 && count > 7) {
      status = 'reserved';
    }

    const item: ParkingSpaceItem = {
      id: `space-${zoneId}-${code.toLowerCase()}`,
      code,
      zoneId,
      floorId,
      lotId,
      vehicleType,
      status,
      lastUpdated: new Date(now.getTime() - i * 600000).toISOString(),
    };

    if (status === 'occupied') {
      item.currentBooking = {
        bookingId: `BK-20261008-0${i}`,
        driverName: i % 2 === 0 ? 'Trần Văn Nam' : 'Lê Thị Thu',
        licensePlate: vehicleType === 'car' ? `51A-${10000 + i * 111}` : `59-K1 ${20000 + i * 222}`,
        startTime: new Date(now.getTime() - 7200000).toISOString(),
        endTime: new Date(now.getTime() + 3600000).toISOString(),
      };
    } else if (status === 'maintenance') {
      item.maintenanceNote = 'Sensor recalibration and line painting in progress.';
    }

    spaces.push(item);
  }

  return spaces;
}

function buildDefaultStructure(lotId: string, lotName: string, address: string): ParkingStructureData {
  if (lotId === 'lot-002') {
    // Bitexco Outdoor Lot (Ground Floor, Car & EV/Motorcycle Zones)
    const floorG: ParkingFloorItem = {
      id: 'fl-lot-002-g',
      lotId,
      name: 'Ground Level (Mặt đất)',
      code: 'G',
      status: 'active',
      zones: [
        {
          id: 'zn-002-car-a',
          floorId: 'fl-lot-002-g',
          lotId,
          name: 'Zone A - Ô Tô & Xe Điện',
          vehicleType: 'car',
          status: 'active',
          spaces: generateInitialSpaces(24, 'A', 'zn-002-car-a', 'fl-lot-002-g', lotId, 'car', [2, 7, 12, 18]),
        },
        {
          id: 'zn-002-car-b',
          floorId: 'fl-lot-002-g',
          lotId,
          name: 'Zone B - Ô Tô Tiêu Chuẩn',
          vehicleType: 'car',
          status: 'active',
          spaces: generateInitialSpaces(16, 'B', 'zn-002-car-b', 'fl-lot-002-g', lotId, 'car', [1, 5, 9]),
        },
        {
          id: 'zn-002-moto',
          floorId: 'fl-lot-002-g',
          lotId,
          name: 'Zone M - Xe Máy',
          vehicleType: 'motorcycle',
          status: 'active',
          spaces: generateInitialSpaces(8, 'M', 'zn-002-moto', 'fl-lot-002-g', lotId, 'motorcycle', [2, 4]),
        },
      ],
    };

    return {
      lotId,
      lotName,
      address,
      floors: [floorG],
    };
  }

  if (lotId === 'lot-001') {
    // Vinhomes Grand Park Multi-storey: 3 floors
    const floorB1: ParkingFloorItem = {
      id: 'fl-001-b1',
      lotId,
      name: 'Tầng Hầm B1',
      code: 'B1',
      status: 'active',
      zones: [
        {
          id: 'zn-001-b1-car',
          floorId: 'fl-001-b1',
          lotId,
          name: 'Khu Ô Tô B1',
          vehicleType: 'car',
          status: 'active',
          spaces: generateInitialSpaces(20, 'A', 'zn-001-b1-car', 'fl-001-b1', lotId, 'car', [1, 4, 8, 12]),
        },
        {
          id: 'zn-001-b1-moto',
          floorId: 'fl-001-b1',
          lotId,
          name: 'Khu Xe Máy B1',
          vehicleType: 'motorcycle',
          status: 'active',
          spaces: generateInitialSpaces(20, 'M', 'zn-001-b1-moto', 'fl-001-b1', lotId, 'motorcycle', [3, 6, 9]),
        },
      ],
    };

    const floorB2: ParkingFloorItem = {
      id: 'fl-001-b2',
      lotId,
      name: 'Tầng Hầm B2',
      code: 'B2',
      status: 'active',
      zones: [
        {
          id: 'zn-001-b2-car',
          floorId: 'fl-001-b2',
          lotId,
          name: 'Khu Ô Tô B2',
          vehicleType: 'car',
          status: 'active',
          spaces: generateInitialSpaces(25, 'B', 'zn-001-b2-car', 'fl-001-b2', lotId, 'car', [2, 5, 11]),
        },
      ],
    };

    return {
      lotId,
      lotName,
      address,
      floors: [floorB1, floorB2],
    };
  }

  // Generic fallback for any other site
  const defaultFloor: ParkingFloorItem = {
    id: `fl-${lotId}-1`,
    lotId,
    name: 'Tầng 1 (Trệt)',
    code: '1F',
    status: 'active',
    zones: [
      {
        id: `zn-${lotId}-car`,
        floorId: `fl-${lotId}-1`,
        lotId,
        name: 'Khu Ô Tô',
        vehicleType: 'car',
        status: 'active',
        spaces: generateInitialSpaces(18, 'A', `zn-${lotId}-car`, `fl-${lotId}-1`, lotId, 'car', [1, 6]),
      },
      {
        id: `zn-${lotId}-moto`,
        floorId: `fl-${lotId}-1`,
        lotId,
        name: 'Khu Xe Máy',
        vehicleType: 'motorcycle',
        status: 'active',
        spaces: generateInitialSpaces(12, 'M', `zn-${lotId}-moto`, `fl-${lotId}-1`, lotId, 'motorcycle', [2, 5]),
      },
    ],
  };

  return {
    lotId,
    lotName,
    address,
    floors: [defaultFloor],
  };
}

function loadAllStructures(): Record<string, ParkingStructureData> {
  try {
    const raw = localStorage.getItem(STRUCTURES_KEY);
    if (raw) return JSON.parse(raw);
  } catch {
    // fallback
  }
  return {};
}

function saveAllStructures(data: Record<string, ParkingStructureData>) {
  try {
    localStorage.setItem(STRUCTURES_KEY, JSON.stringify(data));
    window.dispatchEvent(new Event('sp-structure-change'));
  } catch (err) {
    console.error('Failed to save parking structures', err);
  }
}

export const structureStore = {
  getStructure(lotId: string, fallbackName = 'Parking Lot', fallbackAddress = 'Ho Chi Minh City'): ParkingStructureData {
    const all = loadAllStructures();
    if (all[lotId]) {
      return all[lotId];
    }

    // Check main store lots
    const lot = mainStore.getLots().find(l => l.id === lotId);
    const newStructure = buildDefaultStructure(lotId, lot?.name || fallbackName, lot?.address || fallbackAddress);
    all[lotId] = newStructure;
    saveAllStructures(all);
    return newStructure;
  },

  saveStructure(data: ParkingStructureData) {
    const all = loadAllStructures();
    all[data.lotId] = data;
    saveAllStructures(all);
  },

  calculateStats(data: ParkingStructureData) {
    const totalFloors = data.floors.length;
    let totalSpaces = 0;
    let availableSpaces = 0;
    let occupiedSpaces = 0;
    let reservedSpaces = 0;
    let maintenanceSpaces = 0;
    let disabledSpaces = 0;

    for (const floor of data.floors) {
      for (const zone of floor.zones) {
        for (const space of zone.spaces) {
          totalSpaces++;
          if (space.status === 'available') availableSpaces++;
          else if (space.status === 'occupied') occupiedSpaces++;
          else if (space.status === 'reserved') reservedSpaces++;
          else if (space.status === 'maintenance') maintenanceSpaces++;
          else if (space.status === 'disabled') disabledSpaces++;
        }
      }
    }

    return {
      totalFloors,
      totalSpaces,
      availableSpaces,
      occupiedSpaces,
      reservedSpaces,
      maintenanceSpaces,
      disabledSpaces,
    };
  },

  addFloor(lotId: string, name: string, code: string) {
    const current = structureStore.getStructure(lotId);
    const newFloorId = `fl-${lotId}-${Date.now().toString(36)}`;
    const newZoneId = `zn-${newFloorId}-car`;

    const newFloor: ParkingFloorItem = {
      id: newFloorId,
      lotId,
      name: name.trim(),
      code: code.trim().toUpperCase(),
      status: 'active',
      zones: [
        {
          id: newZoneId,
          floorId: newFloorId,
          lotId,
          name: `Khu Ô Tô ${code.trim().toUpperCase()}`,
          vehicleType: 'car',
          status: 'active',
          spaces: generateInitialSpaces(12, code.trim().toUpperCase(), newZoneId, newFloorId, lotId, 'car'),
        },
      ],
    };

    current.floors.push(newFloor);
    structureStore.saveStructure(current);
    return newFloor;
  },

  updateFloor(lotId: string, floorId: string, updates: Partial<Pick<ParkingFloorItem, 'name' | 'code' | 'status'>>) {
    const current = structureStore.getStructure(lotId);
    current.floors = current.floors.map(f => (f.id === floorId ? { ...f, ...updates } : f));
    structureStore.saveStructure(current);
  },

  deleteFloor(lotId: string, floorId: string) {
    const current = structureStore.getStructure(lotId);
    current.floors = current.floors.filter(f => f.id !== floorId);
    structureStore.saveStructure(current);
  },

  addZone(lotId: string, floorId: string, name: string, vehicleType: VehicleCategory, initialSpacesCount = 10) {
    const current = structureStore.getStructure(lotId);
    const floor = current.floors.find(f => f.id === floorId);
    if (!floor) return;

    const newZoneId = `zn-${floorId}-${Date.now().toString(36)}`;
    const prefix = vehicleType === 'motorcycle' ? 'M' : floor.code || 'A';

    const newZone: ParkingZoneItem = {
      id: newZoneId,
      floorId,
      lotId,
      name: name.trim(),
      vehicleType,
      status: 'active',
      spaces: generateInitialSpaces(initialSpacesCount, prefix, newZoneId, floorId, lotId, vehicleType),
    };

    floor.zones.push(newZone);
    structureStore.saveStructure(current);
    return newZone;
  },

  updateZone(
    lotId: string,
    floorId: string,
    zoneId: string,
    updates: Partial<Pick<ParkingZoneItem, 'name' | 'vehicleType' | 'status'>>
  ) {
    const current = structureStore.getStructure(lotId);
    const floor = current.floors.find(f => f.id === floorId);
    if (!floor) return;

    floor.zones = floor.zones.map(z => {
      if (z.id !== zoneId) return z;
      const updatedZone = { ...z, ...updates };
      // If vehicle type changed, update its spaces
      if (updates.vehicleType && updates.vehicleType !== z.vehicleType) {
        updatedZone.spaces = updatedZone.spaces.map(s => ({ ...s, vehicleType: updates.vehicleType! }));
      }
      return updatedZone;
    });

    structureStore.saveStructure(current);
  },

  deleteZone(lotId: string, floorId: string, zoneId: string) {
    const current = structureStore.getStructure(lotId);
    const floor = current.floors.find(f => f.id === floorId);
    if (!floor) return;

    floor.zones = floor.zones.filter(z => z.id !== zoneId);
    structureStore.saveStructure(current);
  },

  updateSpaceStatus(lotId: string, spaceId: string, newStatus: SpaceStatus, note?: string) {
    const current = structureStore.getStructure(lotId);
    let found = false;

    for (const floor of current.floors) {
      for (const zone of floor.zones) {
        const space = zone.spaces.find(s => s.id === spaceId);
        if (space) {
          space.status = newStatus;
          space.lastUpdated = new Date().toISOString();
          if (newStatus === 'maintenance') {
            space.maintenanceNote = note || 'Maintenance initiated by Owner';
          } else {
            space.maintenanceNote = undefined;
          }
          if (newStatus === 'available') {
            space.currentBooking = undefined;
          }
          found = true;
          break;
        }
      }
      if (found) break;
    }

    if (found) {
      structureStore.saveStructure(current);
    }
  },
};
