import { useState, useMemo, useEffect } from 'react';
import { structureStore } from '../data/structureStore';
import type {
  ParkingFloorItem,
  ParkingZoneItem,
  ParkingSpaceItem,
  ParkingStructureData,
  SpaceStatus,
  VehicleCategory,
} from '../../../lib/structureTypes';
import type { ParkingLot } from '../../../lib/types';
import { UntitledIcon } from '../../../components/icon/UntitledIcon';
import { FloorModal } from './FloorModal';
import { ZoneModal } from './ZoneModal';
import { SpaceDetailModal } from './SpaceDetailModal';

interface ParkingStructureProps {
  sites?: ParkingLot[];
  selectedSiteId?: string;
}

export function ParkingStructure({ sites = [], selectedSiteId = 'all' }: ParkingStructureProps) {
  // Available lots
  const availableLots = useMemo(() => {
    if (sites && sites.length > 0) return sites;
    return [
      { id: 'lot-002', name: 'Bitexco Financial Tower Outdoor Lot', address: '2 Hải Triều, Bến Nghé, Quận 1, TP. Hồ Chí Minh' } as ParkingLot,
      { id: 'lot-001', name: 'Vinhomes Grand Park Parking Center', address: 'Khu đô thị Vinhomes Grand Park, Phường Long Thạnh Mỹ, TP. Thủ Đức' } as ParkingLot,
      { id: 'lot-003', name: 'Nguyen Hue Boulevard Outdoor Lot', address: '68 Nguyễn Huệ, Bến Nghé, Quận 1, TP. Hồ Chí Minh' } as ParkingLot,
    ];
  }, [sites]);

  const [activeLotId, setActiveLotId] = useState<string>(() => {
    if (selectedSiteId && selectedSiteId !== 'all') return selectedSiteId;
    return availableLots[0]?.id || 'lot-002';
  });

  useEffect(() => {
    if (selectedSiteId && selectedSiteId !== 'all') {
      setActiveLotId(selectedSiteId);
    }
  }, [selectedSiteId]);

  // Current Structure Data
  const [structure, setStructure] = useState<ParkingStructureData>(() =>
    structureStore.getStructure(activeLotId)
  );

  const reloadStructure = () => {
    setStructure(structureStore.getStructure(activeLotId));
  };

  useEffect(() => {
    setStructure(structureStore.getStructure(activeLotId));
  }, [activeLotId]);

  // Active Floor Selection
  const [selectedFloorId, setSelectedFloorId] = useState<string>('');

  useEffect(() => {
    if (structure.floors.length > 0) {
      if (!structure.floors.some(f => f.id === selectedFloorId)) {
        setSelectedFloorId(structure.floors[0].id);
      }
    } else {
      setSelectedFloorId('');
    }
  }, [structure, selectedFloorId]);

  // Active Zone Selection ('all' or specific zoneId)
  const [selectedZoneFilter, setSelectedZoneFilter] = useState<string>('all');

  // Search & Filters
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<'all' | SpaceStatus>('all');
  const [vehicleFilter, setVehicleFilter] = useState<'all' | VehicleCategory>('all');

  // Modal States
  const [isFloorModalOpen, setIsFloorModalOpen] = useState(false);
  const [editingFloor, setEditingFloor] = useState<ParkingFloorItem | null>(null);

  const [isZoneModalOpen, setIsZoneModalOpen] = useState(false);
  const [editingZone, setEditingZone] = useState<ParkingZoneItem | null>(null);

  const [deletingTarget, setDeletingTarget] = useState<{
    type: 'floor' | 'zone';
    id: string;
    name: string;
  } | null>(null);

  const [inspectingSpace, setInspectingSpace] = useState<ParkingSpaceItem | null>(null);

  // Computed Current Floor & Zones
  const currentFloor = useMemo(() => {
    return structure.floors.find(f => f.id === selectedFloorId) || structure.floors[0] || null;
  }, [structure.floors, selectedFloorId]);

  // Structure Stats
  const stats = useMemo(() => structureStore.calculateStats(structure), [structure]);

  // Spaces Filtering
  const displayedSpaces = useMemo(() => {
    if (!currentFloor) return [];

    let zones = currentFloor.zones;
    if (selectedZoneFilter !== 'all') {
      zones = zones.filter(z => z.id === selectedZoneFilter);
    }

    const allSpaces: Array<ParkingSpaceItem & { floorCode: string; floorName: string; zoneName: string }> = [];

    zones.forEach(zone => {
      zone.spaces.forEach(space => {
        allSpaces.push({
          ...space,
          floorCode: currentFloor.code,
          floorName: currentFloor.name,
          zoneName: zone.name,
        });
      });
    });

    return allSpaces.filter(space => {
      if (searchQuery.trim()) {
        const query = searchQuery.trim().toLowerCase();
        const matchesCode = space.code.toLowerCase().includes(query);
        const matchesZone = space.zoneName.toLowerCase().includes(query);
        const matchesPlate = space.currentBooking?.licensePlate.toLowerCase().includes(query);
        if (!matchesCode && !matchesZone && !matchesPlate) return false;
      }
      if (statusFilter !== 'all' && space.status !== statusFilter) {
        return false;
      }
      if (vehicleFilter !== 'all' && space.vehicleType !== vehicleFilter) {
        return false;
      }
      return true;
    });
  }, [currentFloor, selectedZoneFilter, searchQuery, statusFilter, vehicleFilter]);

  // Floor Handlers
  const handleSaveFloor = (name: string, code: string, status?: 'active' | 'inactive') => {
    if (editingFloor) {
      structureStore.updateFloor(activeLotId, editingFloor.id, { name, code, status });
    } else {
      const created = structureStore.addFloor(activeLotId, name, code);
      if (created) setSelectedFloorId(created.id);
    }
    reloadStructure();
    setEditingFloor(null);
  };

  const handleConfirmDelete = () => {
    if (!deletingTarget) return;
    if (deletingTarget.type === 'floor') {
      structureStore.deleteFloor(activeLotId, deletingTarget.id);
      reloadStructure();
    } else if (deletingTarget.type === 'zone' && currentFloor) {
      structureStore.deleteZone(activeLotId, currentFloor.id, deletingTarget.id);
      reloadStructure();
    }
    setDeletingTarget(null);
  };

  // Zone Handlers
  const handleSaveZone = (
    name: string,
    vehicleType: VehicleCategory,
    initialSpaces?: number,
    status?: 'active' | 'inactive'
  ) => {
    if (!currentFloor) return;
    if (editingZone) {
      structureStore.updateZone(activeLotId, currentFloor.id, editingZone.id, {
        name,
        vehicleType,
        status,
      });
    } else {
      structureStore.addZone(activeLotId, currentFloor.id, name, vehicleType, initialSpaces || 12);
    }
    reloadStructure();
    setEditingZone(null);
  };

  // Space Status Update
  const handleUpdateSpaceStatus = (spaceId: string, newStatus: SpaceStatus, note?: string) => {
    structureStore.updateSpaceStatus(activeLotId, spaceId, newStatus, note);
    reloadStructure();
    // Update inspected space state if still open
    if (inspectingSpace && inspectingSpace.id === spaceId) {
      setInspectingSpace(prev => (prev ? { ...prev, status: newStatus, maintenanceNote: note } : null));
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
      {/* SECTION A: LOT HEADER & COMPACT SUMMARY STATS */}
      <div className="card" style={{ display: 'flex', flexDirection: 'column', gap: '1rem', padding: '1.25rem' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '1rem' }}>
          <div style={{ flex: '1 1 320px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.25rem' }}>
              <span style={{ fontSize: '0.72rem', fontWeight: 700, color: 'var(--primary)', letterSpacing: '0.08em', textTransform: 'uppercase' }}>
                Owner Management · Physical Layout
              </span>
              <span style={{ fontSize: '0.7rem', padding: '0.1rem 0.45rem', borderRadius: '999px', background: '#22c55e20', color: '#16a34a', fontWeight: 700 }}>
                ACTIVE HIERARCHY
              </span>
            </div>
            <h2 style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '1.35rem', margin: '0 0 0.25rem', color: 'var(--fg)' }}>
              {structure.lotName}
            </h2>
            <p style={{ margin: 0, fontSize: '0.82rem', color: 'var(--muted)', display: 'flex', alignItems: 'center', gap: '0.35rem' }}>
              <UntitledIcon name="map-pin" size={14} />
              {structure.address}
            </p>
          </div>

          {/* Lot Selector */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <label style={{ fontSize: '0.82rem', fontWeight: 600, color: 'var(--muted)' }}>Switch Lot:</label>
            <select
              className="input"
              style={{ minWidth: 220, fontSize: '0.85rem', padding: '0.45rem 0.75rem' }}
              value={activeLotId}
              onChange={e => setActiveLotId(e.target.value)}
            >
              {availableLots.map(lot => (
                <option key={lot.id} value={lot.id}>
                  {lot.name}
                </option>
              ))}
            </select>
          </div>
        </div>

        {/* Compact Summary Cards */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))', gap: '0.625rem' }}>
          <div style={{ background: 'var(--bg)', border: '1px solid var(--border)', borderRadius: '0.5rem', padding: '0.75rem 0.875rem' }}>
            <div style={{ fontSize: '0.72rem', color: 'var(--muted)', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '0.3rem' }}>
              <UntitledIcon name="building" size={13} /> Total Floors
            </div>
            <div style={{ fontSize: '1.25rem', fontWeight: 800, fontFamily: 'Outfit', marginTop: '0.2rem', color: 'var(--fg)' }}>
              {stats.totalFloors}
            </div>
          </div>

          <div style={{ background: 'var(--bg)', border: '1px solid var(--border)', borderRadius: '0.5rem', padding: '0.75rem 0.875rem' }}>
            <div style={{ fontSize: '0.72rem', color: 'var(--muted)', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '0.3rem' }}>
              <UntitledIcon name="grid" size={13} /> Total Spaces
            </div>
            <div style={{ fontSize: '1.25rem', fontWeight: 800, fontFamily: 'Outfit', marginTop: '0.2rem', color: 'var(--fg)' }}>
              {stats.totalSpaces}
            </div>
          </div>

          <div style={{ background: 'var(--bg)', border: '1px solid #22c55e40', borderRadius: '0.5rem', padding: '0.75rem 0.875rem' }}>
            <div style={{ fontSize: '0.72rem', color: '#16a34a', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '0.3rem' }}>
              <UntitledIcon name="check-circle" size={13} /> Available
            </div>
            <div style={{ fontSize: '1.25rem', fontWeight: 800, fontFamily: 'Outfit', marginTop: '0.2rem', color: '#16a34a' }}>
              {stats.availableSpaces}
            </div>
          </div>

          <div style={{ background: 'var(--bg)', border: '1px solid #ef444440', borderRadius: '0.5rem', padding: '0.75rem 0.875rem' }}>
            <div style={{ fontSize: '0.72rem', color: '#dc2626', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '0.3rem' }}>
              <UntitledIcon name="car" size={13} /> Occupied
            </div>
            <div style={{ fontSize: '1.25rem', fontWeight: 800, fontFamily: 'Outfit', marginTop: '0.2rem', color: '#dc2626' }}>
              {stats.occupiedSpaces}
            </div>
          </div>

          <div style={{ background: 'var(--bg)', border: '1px solid #f9731640', borderRadius: '0.5rem', padding: '0.75rem 0.875rem' }}>
            <div style={{ fontSize: '0.72rem', color: '#ea580c', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '0.3rem' }}>
              <UntitledIcon name="wrench" size={13} /> Maintenance
            </div>
            <div style={{ fontSize: '1.25rem', fontWeight: 800, fontFamily: 'Outfit', marginTop: '0.2rem', color: '#ea580c' }}>
              {stats.maintenanceSpaces}
            </div>
          </div>

          <div style={{ background: 'var(--bg)', border: '1px solid #f59e0b40', borderRadius: '0.5rem', padding: '0.75rem 0.875rem' }}>
            <div style={{ fontSize: '0.72rem', color: '#d97706', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '0.3rem' }}>
              <UntitledIcon name="clock" size={13} /> Reserved
            </div>
            <div style={{ fontSize: '1.25rem', fontWeight: 800, fontFamily: 'Outfit', marginTop: '0.2rem', color: '#d97706' }}>
              {stats.reservedSpaces}
            </div>
          </div>
        </div>
      </div>

      {/* SECTION B: FLOOR MANAGEMENT */}
      <div className="card" style={{ padding: '1.25rem', display: 'flex', flexDirection: 'column', gap: '1rem' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.75rem' }}>
          <div>
            <h3 style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '1.1rem', margin: 0, color: 'var(--fg)' }}>
              Floors & Levels ({structure.floors.length})
            </h3>
            <p style={{ margin: '0.15rem 0 0', fontSize: '0.78rem', color: 'var(--muted)' }}>
              Select a floor level to view zones and manage parking grid slots.
            </p>
          </div>
          <button
            type="button"
            className="btn-primary"
            style={{ fontSize: '0.82rem', padding: '0.45rem 0.85rem' }}
            onClick={() => {
              setEditingFloor(null);
              setIsFloorModalOpen(true);
            }}
          >
            <UntitledIcon name="plus" size={15} /> Add Floor
          </button>
        </div>

        {/* Floor Cards / Tabs Strip */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(260px, 1fr))', gap: '0.75rem' }}>
          {structure.floors.map(floor => {
            const isSelected = currentFloor?.id === floor.id;
            const floorSpaces = floor.zones.flatMap(z => z.spaces);
            const avail = floorSpaces.filter(s => s.status === 'available').length;
            const occ = floorSpaces.filter(s => s.status === 'occupied').length;

            return (
              <div
                key={floor.id}
                onClick={() => setSelectedFloorId(floor.id)}
                style={{
                  border: `2px solid ${isSelected ? 'var(--primary)' : 'var(--border)'}`,
                  background: isSelected ? 'color-mix(in srgb, var(--primary) 7%, var(--card))' : 'var(--bg)',
                  borderRadius: '0.75rem',
                  padding: '0.875rem 1rem',
                  cursor: 'pointer',
                  transition: 'all 0.15s ease',
                  position: 'relative',
                }}
              >
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '0.35rem' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
                    <span
                      style={{
                        fontFamily: 'Outfit',
                        fontWeight: 800,
                        fontSize: '1rem',
                        color: isSelected ? 'var(--primary)' : 'var(--fg)',
                      }}
                    >
                      [{floor.code}] {floor.name}
                    </span>
                  </div>
                  <span
                    style={{
                      fontSize: '0.68rem',
                      fontWeight: 700,
                      padding: '0.1rem 0.4rem',
                      borderRadius: '999px',
                      background: floor.status === 'active' ? '#22c55e18' : '#ef444418',
                      color: floor.status === 'active' ? '#16a34a' : '#dc2626',
                    }}
                  >
                    {floor.status.toUpperCase()}
                  </span>
                </div>

                <div style={{ display: 'flex', gap: '0.85rem', fontSize: '0.78rem', color: 'var(--muted)', marginTop: '0.4rem' }}>
                  <span>{floor.zones.length} Zones</span>
                  <span>{floorSpaces.length} Spaces</span>
                  <span style={{ color: '#16a34a' }}>● {avail} free</span>
                  <span style={{ color: '#dc2626' }}>● {occ} occ</span>
                </div>

                {/* Floor Controls */}
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'flex-end',
                    gap: '0.35rem',
                    marginTop: '0.65rem',
                    paddingTop: '0.5rem',
                    borderTop: '1px solid var(--border)',
                  }}
                  onClick={e => e.stopPropagation()}
                >
                  <button
                    type="button"
                    title="Edit Floor"
                    className="btn-outline"
                    style={{ fontSize: '0.72rem', padding: '0.2rem 0.5rem', border: '1px solid var(--border)' }}
                    onClick={() => {
                      setEditingFloor(floor);
                      setIsFloorModalOpen(true);
                    }}
                  >
                    Edit
                  </button>
                  <button
                    type="button"
                    title={floor.status === 'active' ? 'Deactivate' : 'Activate'}
                    style={{
                      fontSize: '0.72rem',
                      padding: '0.2rem 0.5rem',
                      borderRadius: 'var(--radius)',
                      border: '1px solid var(--border)',
                      background: 'transparent',
                      color: floor.status === 'active' ? '#ea580c' : '#16a34a',
                      cursor: 'pointer',
                    }}
                    onClick={() => {
                      structureStore.updateFloor(activeLotId, floor.id, {
                        status: floor.status === 'active' ? 'inactive' : 'active',
                      });
                      reloadStructure();
                    }}
                  >
                    {floor.status === 'active' ? 'Deactivate' : 'Activate'}
                  </button>
                  <button
                    type="button"
                    title="Delete Floor"
                    style={{
                      fontSize: '0.72rem',
                      padding: '0.2rem 0.45rem',
                      borderRadius: 'var(--radius)',
                      border: '1px solid #ef444440',
                      background: 'transparent',
                      color: '#dc2626',
                      cursor: 'pointer',
                    }}
                    onClick={() =>
                      setDeletingTarget({
                        type: 'floor',
                        id: floor.id,
                        name: `Floor ${floor.code} (${floor.name})`,
                      })
                    }
                  >
                    Delete
                  </button>
                </div>
              </div>
            );
          })}

          {structure.floors.length === 0 && (
            <div style={{ gridColumn: '1 / -1', textAlign: 'center', padding: '2rem', color: 'var(--muted)' }}>
              No floors defined yet. Click "+ Add Floor" to begin configuring this parking structure.
            </div>
          )}
        </div>
      </div>

      {/* SECTION C & D: ZONE MANAGEMENT & PARKING SPACE VISUAL GRID */}
      {currentFloor && (
        <div className="card" style={{ padding: '1.25rem', display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
          {/* Floor Header Bar */}
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.75rem' }}>
            <div>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <h3 style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '1.2rem', margin: 0, color: 'var(--fg)' }}>
                  Level {currentFloor.code} Zones & Space Grid
                </h3>
                <span style={{ fontSize: '0.72rem', color: 'var(--muted)', background: 'var(--bg)', padding: '0.15rem 0.5rem', borderRadius: '4px', border: '1px solid var(--border)' }}>
                  {currentFloor.name}
                </span>
              </div>
              <p style={{ margin: '0.2rem 0 0', fontSize: '0.78rem', color: 'var(--muted)' }}>
                Visual allocation grid for cars and motorcycles in this floor.
              </p>
            </div>

            <button
              type="button"
              className="btn-primary"
              style={{ fontSize: '0.82rem', padding: '0.45rem 0.85rem' }}
              onClick={() => {
                setEditingZone(null);
                setIsZoneModalOpen(true);
              }}
            >
              <UntitledIcon name="plus" size={15} /> Add Zone to {currentFloor.code}
            </button>
          </div>

          {/* Zones Summary Strip */}
          <div style={{ display: 'flex', gap: '0.75rem', overflowX: 'auto', paddingBottom: '0.25rem' }}>
            <button
              type="button"
              onClick={() => setSelectedZoneFilter('all')}
              style={{
                padding: '0.5rem 0.85rem',
                borderRadius: 'var(--radius)',
                border: `1.5px solid ${selectedZoneFilter === 'all' ? 'var(--primary)' : 'var(--border)'}`,
                background: selectedZoneFilter === 'all' ? 'var(--primary)' : 'var(--bg)',
                color: selectedZoneFilter === 'all' ? 'var(--primary-fg)' : 'var(--fg)',
                fontWeight: 600,
                fontSize: '0.8rem',
                cursor: 'pointer',
                whiteSpace: 'nowrap',
                display: 'flex',
                alignItems: 'center',
                gap: '0.4rem',
              }}
            >
              <UntitledIcon name="grid" size={14} /> All Zones ({currentFloor.zones.length})
            </button>

            {currentFloor.zones.map(zone => {
              const isSelected = selectedZoneFilter === zone.id;
              const avail = zone.spaces.filter(s => s.status === 'available').length;
              const occ = zone.spaces.filter(s => s.status === 'occupied').length;
              const maint = zone.spaces.filter(s => s.status === 'maintenance').length;

              return (
                <div
                  key={zone.id}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: '0.5rem',
                    padding: '0.4rem 0.75rem',
                    borderRadius: 'var(--radius)',
                    border: `1.5px solid ${isSelected ? 'var(--primary)' : 'var(--border)'}`,
                    background: isSelected ? 'color-mix(in srgb, var(--primary) 12%, var(--bg))' : 'var(--bg)',
                    whiteSpace: 'nowrap',
                    cursor: 'pointer',
                  }}
                  onClick={() => setSelectedZoneFilter(zone.id)}
                >
                  <UntitledIcon name={zone.vehicleType === 'car' ? 'car' : 'motorcycle'} size={15} />
                  <span style={{ fontSize: '0.82rem', fontWeight: 600, color: 'var(--fg)' }}>{zone.name}</span>
                  <span style={{ fontSize: '0.72rem', color: 'var(--muted)' }}>
                    ({avail} avail / {zone.spaces.length})
                  </span>

                  {/* Actions */}
                  <div style={{ display: 'flex', alignItems: 'center', gap: '0.2rem', marginLeft: '0.35rem' }} onClick={e => e.stopPropagation()}>
                    <button
                      type="button"
                      title="Edit Zone"
                      style={{ border: 'none', background: 'transparent', color: 'var(--muted)', cursor: 'pointer', padding: '2px' }}
                      onClick={() => {
                        setEditingZone(zone);
                        setIsZoneModalOpen(true);
                      }}
                    >
                      <UntitledIcon name="settings" size={13} />
                    </button>
                    <button
                      type="button"
                      title="Delete Zone"
                      style={{ border: 'none', background: 'transparent', color: '#dc2626', cursor: 'pointer', padding: '2px' }}
                      onClick={() =>
                        setDeletingTarget({
                          type: 'zone',
                          id: zone.id,
                          name: `${zone.name} (${zone.spaces.length} spaces)`,
                        })
                      }
                    >
                      <UntitledIcon name="x" size={13} />
                    </button>
                  </div>
                </div>
              );
            })}
          </div>

          {/* SECTION E: SEARCH & FILTER CONTROLS */}
          <div
            style={{
              display: 'flex',
              flexWrap: 'wrap',
              alignItems: 'center',
              justifyContent: 'space-between',
              gap: '0.75rem',
              padding: '0.75rem 1rem',
              background: 'var(--bg)',
              borderRadius: '0.625rem',
              border: '1px solid var(--border)',
            }}
          >
            {/* Search Box */}
            <div style={{ position: 'relative', flex: '1 1 200px', maxWidth: 300 }}>
              <span style={{ position: 'absolute', left: '0.7rem', top: '50%', transform: 'translateY(-50%)', color: 'var(--muted)' }}>
                <UntitledIcon name="search" size={14} />
              </span>
              <input
                className="input"
                style={{ paddingLeft: '2.1rem', fontSize: '0.82rem', paddingBlock: '0.4rem' }}
                placeholder="Search slot code (e.g. A01, M02)..."
                value={searchQuery}
                onChange={e => setSearchQuery(e.target.value)}
              />
            </div>

            {/* Filter Pills */}
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', flexWrap: 'wrap' }}>
              {/* Vehicle Type Filter */}
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.25rem', fontSize: '0.78rem' }}>
                <span style={{ color: 'var(--muted)', fontWeight: 600 }}>Vehicle:</span>
                <select
                  className="input"
                  style={{ width: 'auto', fontSize: '0.78rem', padding: '0.35rem 0.65rem' }}
                  value={vehicleFilter}
                  onChange={e => setVehicleFilter(e.target.value as any)}
                >
                  <option value="all">All Vehicles</option>
                  <option value="car">Car (Ô tô)</option>
                  <option value="motorcycle">Motorcycle (Xe máy)</option>
                </select>
              </div>

              {/* Status Filter */}
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.25rem', fontSize: '0.78rem' }}>
                <span style={{ color: 'var(--muted)', fontWeight: 600 }}>Status:</span>
                <select
                  className="input"
                  style={{ width: 'auto', fontSize: '0.78rem', padding: '0.35rem 0.65rem' }}
                  value={statusFilter}
                  onChange={e => setStatusFilter(e.target.value as any)}
                >
                  <option value="all">All Statuses</option>
                  <option value="available">🟢 Available (Trống)</option>
                  <option value="occupied">🔴 Occupied (Đang đỗ)</option>
                  <option value="reserved">🟡 Reserved (Đã đặt)</option>
                  <option value="maintenance">🔧 Maintenance (Bảo trì)</option>
                  <option value="disabled">🚫 Disabled (Ngưng hoạt động)</option>
                </select>
              </div>

              {(searchQuery || statusFilter !== 'all' || vehicleFilter !== 'all' || selectedZoneFilter !== 'all') && (
                <button
                  type="button"
                  className="btn-outline"
                  style={{ fontSize: '0.75rem', padding: '0.3rem 0.65rem' }}
                  onClick={() => {
                    setSearchQuery('');
                    setStatusFilter('all');
                    setVehicleFilter('all');
                    setSelectedZoneFilter('all');
                  }}
                >
                  Reset
                </button>
              )}
            </div>
          </div>

          {/* PARKING SPACE VISUAL GRID */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: '0.78rem', color: 'var(--muted)' }}>
              <span>
                Showing <strong>{displayedSpaces.length}</strong> spaces matching filters
              </span>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', flexWrap: 'wrap' }}>
                <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.3rem' }}>
                  <span style={{ width: 9, height: 9, borderRadius: '50%', background: '#22c55e' }} />
                  Available
                </span>
                <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.3rem' }}>
                  <span style={{ width: 9, height: 9, borderRadius: '50%', background: '#ef4444' }} />
                  Occupied
                </span>
                <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.3rem' }}>
                  <span style={{ width: 9, height: 9, borderRadius: '50%', background: '#f59e0b' }} />
                  Reserved
                </span>
                <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.3rem' }}>
                  <span style={{ width: 9, height: 9, borderRadius: '50%', background: '#f97316' }} />
                  Maintenance
                </span>
                <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.3rem' }}>
                  <span style={{ width: 9, height: 9, borderRadius: '50%', background: '#64748b' }} />
                  Disabled
                </span>
              </div>
            </div>

            {displayedSpaces.length > 0 ? (
              <div
                style={{
                  display: 'grid',
                  gridTemplateColumns: 'repeat(auto-fill, minmax(105px, 1fr))',
                  gap: '0.625rem',
                }}
              >
                {displayedSpaces.map(space => {
                  const isAvail = space.status === 'available';
                  const isOcc = space.status === 'occupied';
                  const isRes = space.status === 'reserved';
                  const isMaint = space.status === 'maintenance';
                  const isDis = space.status === 'disabled';

                  let borderColor = '#22c55e';
                  let bgColor = '#22c55e10';
                  let textColor = '#16a34a';
                  let statusText = 'Trống';
                  let statusIcon = 'check';

                  if (isOcc) {
                    borderColor = '#ef4444';
                    bgColor = '#ef444412';
                    textColor = '#dc2626';
                    statusText = 'Đang đỗ';
                    statusIcon = 'car';
                  } else if (isRes) {
                    borderColor = '#f59e0b';
                    bgColor = '#f59e0b12';
                    textColor = '#d97706';
                    statusText = 'Đã đặt';
                    statusIcon = 'clock';
                  } else if (isMaint) {
                    borderColor = '#f97316';
                    bgColor = '#f9731612';
                    textColor = '#ea580c';
                    statusText = 'Bảo trì';
                    statusIcon = 'wrench';
                  } else if (isDis) {
                    borderColor = '#64748b';
                    bgColor = '#64748b10';
                    textColor = '#475569';
                    statusText = 'Khóa';
                    statusIcon = 'x';
                  }

                  return (
                    <div
                      key={space.id}
                      onClick={() => setInspectingSpace(space)}
                      style={{
                        padding: '0.65rem 0.5rem',
                        borderRadius: '0.5rem',
                        border: `1.5px ${isDis ? 'dashed' : 'solid'} ${borderColor}`,
                        background: bgColor,
                        cursor: 'pointer',
                        display: 'flex',
                        flexDirection: 'column',
                        alignItems: 'center',
                        justifyContent: 'center',
                        gap: '0.2rem',
                        textAlign: 'center',
                        transition: 'transform 0.12s, box-shadow 0.12s',
                        userSelect: 'none',
                      }}
                      onMouseEnter={e => {
                        e.currentTarget.style.transform = 'translateY(-2px)';
                        e.currentTarget.style.boxShadow = '0 4px 12px rgba(0, 0, 0, 0.08)';
                      }}
                      onMouseLeave={e => {
                        e.currentTarget.style.transform = 'translateY(0)';
                        e.currentTarget.style.boxShadow = 'none';
                      }}
                    >
                      {/* Space Code */}
                      <span
                        style={{
                          fontFamily: 'Outfit',
                          fontWeight: 800,
                          fontSize: '1.05rem',
                          color: 'var(--fg)',
                          lineHeight: 1,
                        }}
                      >
                        {space.code}
                      </span>

                      {/* Vehicle Category & Status Icon */}
                      <div style={{ display: 'flex', alignItems: 'center', gap: '0.25rem', color: textColor, margin: '0.1rem 0' }}>
                        <UntitledIcon name={space.vehicleType === 'car' ? 'car' : 'motorcycle'} size={13} />
                        <UntitledIcon name={statusIcon} size={11} />
                      </div>

                      {/* Text Badge for Accessibility */}
                      <span
                        style={{
                          fontSize: '0.65rem',
                          fontWeight: 700,
                          padding: '0.1rem 0.35rem',
                          borderRadius: '3px',
                          background: 'var(--bg)',
                          color: textColor,
                          border: `1px solid ${borderColor}50`,
                          textTransform: 'uppercase',
                        }}
                      >
                        {statusText}
                      </span>
                    </div>
                  );
                })}
              </div>
            ) : (
              <div
                style={{
                  textAlign: 'center',
                  padding: '2.5rem',
                  background: 'var(--bg)',
                  borderRadius: '0.75rem',
                  border: '1px dashed var(--border)',
                  color: 'var(--muted)',
                }}
              >
                <div style={{ color: 'var(--primary)', marginBottom: '0.5rem' }}>
                  <UntitledIcon name="search" size={28} />
                </div>
                <p style={{ margin: 0, fontWeight: 600 }}>No parking spaces match the current filter criteria.</p>
                <button
                  type="button"
                  className="btn-outline"
                  style={{ marginTop: '0.75rem', fontSize: '0.8rem', padding: '0.35rem 0.85rem' }}
                  onClick={() => {
                    setSearchQuery('');
                    setStatusFilter('all');
                    setVehicleFilter('all');
                    setSelectedZoneFilter('all');
                  }}
                >
                  Clear All Filters
                </button>
              </div>
            )}
          </div>
        </div>
      )}

      {/* MODAL: FLOOR ADD / EDIT */}
      <FloorModal
        isOpen={isFloorModalOpen}
        initialData={editingFloor}
        onClose={() => {
          setIsFloorModalOpen(false);
          setEditingFloor(null);
        }}
        onSave={handleSaveFloor}
      />

      {/* MODAL: ZONE ADD / EDIT */}
      <ZoneModal
        isOpen={isZoneModalOpen}
        floorCode={currentFloor?.code || ''}
        initialData={editingZone}
        onClose={() => {
          setIsZoneModalOpen(false);
          setEditingZone(null);
        }}
        onSave={handleSaveZone}
      />

      {/* MODAL: SPACE DETAILS */}
      <SpaceDetailModal
        space={inspectingSpace}
        floorName={currentFloor?.name || ''}
        floorCode={currentFloor?.code || ''}
        zoneName={inspectingSpace ? currentFloor?.zones.find(z => z.id === inspectingSpace.zoneId)?.name || 'Zone' : ''}
        onClose={() => setInspectingSpace(null)}
        onUpdateStatus={handleUpdateSpaceStatus}
      />

      {/* CONFIRMATION MODAL: DELETE */}
      {deletingTarget && (
        <div
          role="presentation"
          onMouseDown={e => {
            if (e.target === e.currentTarget) setDeletingTarget(null);
          }}
          style={{
            position: 'fixed',
            inset: 0,
            zIndex: 1000,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            padding: '1rem',
            background: 'rgba(2, 6, 23, 0.65)',
            backdropFilter: 'blur(4px)',
          }}
        >
          <div
            role="dialog"
            aria-modal="true"
            className="card"
            style={{
              width: '100%',
              maxWidth: 420,
              background: 'var(--bg)',
              color: 'var(--fg)',
              border: '1px solid var(--border)',
              borderRadius: '1rem',
              padding: '1.5rem',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#dc2626', marginBottom: '0.75rem' }}>
              <UntitledIcon name="alert" size={22} />
              <h3 style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '1.2rem', margin: 0 }}>
                Confirm Deletion
              </h3>
            </div>
            <p style={{ margin: '0 0 1.25rem', fontSize: '0.875rem', color: 'var(--fg)', lineHeight: 1.5 }}>
              Are you sure you want to delete <strong>{deletingTarget.name}</strong>?
              {deletingTarget.type === 'floor' && ' All zones and parking spaces under this floor will also be permanently removed.'}
            </p>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.625rem' }}>
              <button type="button" className="btn-outline" onClick={() => setDeletingTarget(null)}>
                Cancel
              </button>
              <button
                type="button"
                className="btn-primary"
                style={{ background: '#dc2626' }}
                onClick={handleConfirmDelete}
              >
                Delete Permanently
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
