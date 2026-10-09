import { useState } from 'react';
import { UntitledIcon } from '../../../components/icon/UntitledIcon';
import type { ParkingSpaceItem, SpaceStatus } from '../../../lib/structureTypes';

interface SpaceDetailModalProps {
  space: ParkingSpaceItem | null;
  floorName: string;
  floorCode: string;
  zoneName: string;
  onClose: () => void;
  onUpdateStatus: (spaceId: string, newStatus: SpaceStatus, note?: string) => void;
}

export function SpaceDetailModal({
  space,
  floorName,
  floorCode,
  zoneName,
  onClose,
  onUpdateStatus,
}: SpaceDetailModalProps) {
  const [maintenanceNote, setMaintenanceNote] = useState('');
  const [showMaintInput, setShowMaintInput] = useState(false);

  if (!space) return null;

  const getStatusBadge = (status: SpaceStatus) => {
    switch (status) {
      case 'available':
        return {
          bg: '#22c55e18',
          border: '#22c55e',
          text: '#16a34a',
          label: 'Available (Trống)',
          icon: 'check-circle',
        };
      case 'occupied':
        return {
          bg: '#ef444418',
          border: '#ef4444',
          text: '#dc2626',
          label: 'Occupied (Đang đỗ)',
          icon: 'car',
        };
      case 'reserved':
        return {
          bg: '#f59e0b18',
          border: '#f59e0b',
          text: '#d97706',
          label: 'Reserved (Đã đặt trước)',
          icon: 'clock',
        };
      case 'maintenance':
        return {
          bg: '#f9731618',
          border: '#f97316',
          text: '#ea580c',
          label: 'Maintenance (Bảo trì)',
          icon: 'wrench',
        };
      case 'disabled':
        return {
          bg: '#64748b18',
          border: '#64748b',
          text: '#475569',
          label: 'Disabled (Tạm ngưng)',
          icon: 'x',
        };
    }
  };

  const badge = getStatusBadge(space.status);

  return (
    <div
      role="presentation"
      onMouseDown={e => {
        if (e.target === e.currentTarget) onClose();
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
        aria-labelledby="space-modal-title"
        className="card"
        style={{
          width: '100%',
          maxWidth: 520,
          background: 'var(--bg)',
          color: 'var(--fg)',
          border: '1px solid var(--border)',
          borderRadius: '1rem',
          boxShadow: '0 20px 50px rgba(0, 0, 0, 0.25)',
          padding: '1.5rem',
        }}
      >
        {/* Header */}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '1.25rem' }}>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.25rem' }}>
              <span style={{ fontSize: '0.72rem', fontWeight: 700, color: 'var(--primary)', letterSpacing: '0.08em', textTransform: 'uppercase' }}>
                Floor {floorCode} · {zoneName}
              </span>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
              <h3 id="space-modal-title" style={{ fontFamily: 'Outfit', fontWeight: 800, fontSize: '1.6rem', margin: 0 }}>
                Space {space.code}
              </h3>
              <span
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '0.35rem',
                  fontSize: '0.75rem',
                  fontWeight: 700,
                  padding: '0.2rem 0.65rem',
                  borderRadius: '999px',
                  background: badge.bg,
                  color: badge.text,
                  border: `1px solid ${badge.border}40`,
                }}
              >
                <UntitledIcon name={badge.icon} size={14} />
                {badge.label}
              </span>
            </div>
          </div>
          <button
            type="button"
            aria-label="Close"
            onClick={onClose}
            style={{
              width: 32,
              height: 32,
              borderRadius: '50%',
              border: '1px solid var(--border)',
              background: 'var(--card)',
              color: 'var(--fg)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              cursor: 'pointer',
            }}
          >
            <UntitledIcon name="x" size={16} />
          </button>
        </div>

        {/* Space Meta Details */}
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(2, 1fr)',
            gap: '0.75rem',
            background: 'var(--card)',
            padding: '1rem',
            borderRadius: '0.75rem',
            border: '1px solid var(--border)',
            marginBottom: '1.25rem',
          }}
        >
          <div>
            <span style={{ fontSize: '0.75rem', color: 'var(--muted)', display: 'block' }}>Vehicle Type</span>
            <strong style={{ fontSize: '0.9rem', display: 'flex', alignItems: 'center', gap: '0.35rem', marginTop: '0.15rem' }}>
              <UntitledIcon name={space.vehicleType === 'car' ? 'car' : 'motorcycle'} size={16} />
              {space.vehicleType === 'car' ? 'Car (Ô tô)' : 'Motorcycle (Xe máy)'}
            </strong>
          </div>
          <div>
            <span style={{ fontSize: '0.75rem', color: 'var(--muted)', display: 'block' }}>Floor Level</span>
            <strong style={{ fontSize: '0.9rem', marginTop: '0.15rem', display: 'block' }}>
              {floorName} ({floorCode})
            </strong>
          </div>
          <div>
            <span style={{ fontSize: '0.75rem', color: 'var(--muted)', display: 'block' }}>Space UID</span>
            <code style={{ fontSize: '0.78rem', color: 'var(--muted)', wordBreak: 'break-all' }}>
              {space.id}
            </code>
          </div>
          <div>
            <span style={{ fontSize: '0.75rem', color: 'var(--muted)', display: 'block' }}>Last Updated</span>
            <span style={{ fontSize: '0.8rem', color: 'var(--fg)' }}>
              {new Date(space.lastUpdated).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })} - {new Date(space.lastUpdated).toLocaleDateString()}
            </span>
          </div>
        </div>

        {/* Occupied Booking info */}
        {space.status === 'occupied' && space.currentBooking && (
          <div
            style={{
              padding: '1rem',
              borderRadius: '0.75rem',
              background: '#ef444410',
              border: '1px solid #ef444430',
              marginBottom: '1.25rem',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.5rem' }}>
              <span style={{ color: 'var(--occupied)' }}><UntitledIcon name="car" size={16} /></span>
              <strong style={{ fontSize: '0.875rem', color: 'var(--fg)' }}>Current Active Booking</strong>
              <span style={{ fontSize: '0.72rem', background: '#ef444425', color: '#dc2626', padding: '0.1rem 0.4rem', borderRadius: '4px', fontWeight: 700 }}>
                {space.currentBooking.bookingId}
              </span>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '0.5rem', fontSize: '0.82rem' }}>
              <div>
                <span style={{ color: 'var(--muted)' }}>Driver:</span>{' '}
                <strong>{space.currentBooking.driverName}</strong>
              </div>
              <div>
                <span style={{ color: 'var(--muted)' }}>License Plate:</span>{' '}
                <span style={{ fontFamily: 'monospace', fontWeight: 700, background: 'var(--bg)', padding: '0.1rem 0.35rem', borderRadius: '4px', border: '1px solid var(--border)' }}>
                  {space.currentBooking.licensePlate}
                </span>
              </div>
              <div style={{ gridColumn: 'span 2' }}>
                <span style={{ color: 'var(--muted)' }}>Check-in:</span>{' '}
                <span>{new Date(space.currentBooking.startTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })} ({new Date(space.currentBooking.startTime).toLocaleDateString()})</span>
              </div>
            </div>
          </div>
        )}

        {/* Maintenance Note info */}
        {space.status === 'maintenance' && (
          <div
            style={{
              padding: '1rem',
              borderRadius: '0.75rem',
              background: '#f9731610',
              border: '1px solid #f9731630',
              marginBottom: '1.25rem',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.35rem' }}>
              <span style={{ color: '#ea580c' }}><UntitledIcon name="wrench" size={16} /></span>
              <strong style={{ fontSize: '0.875rem', color: '#ea580c' }}>Maintenance In Progress</strong>
            </div>
            <p style={{ margin: 0, fontSize: '0.82rem', color: 'var(--fg)', lineHeight: 1.5 }}>
              {space.maintenanceNote || 'Sensor calibration or slot physical inspection.'}
            </p>
          </div>
        )}

        {/* Status Actions */}
        <div style={{ marginTop: '0.5rem' }}>
          <span style={{ display: 'block', fontSize: '0.8rem', fontWeight: 600, color: 'var(--muted)', marginBottom: '0.6rem' }}>
            Quick Status Override & Controls:
          </span>

          {showMaintInput ? (
            <div style={{ display: 'grid', gap: '0.5rem', padding: '0.75rem', background: 'var(--card)', borderRadius: '0.5rem', border: '1px solid var(--border)', marginBottom: '0.75rem' }}>
              <label style={{ fontSize: '0.8rem', fontWeight: 600 }}>Reason / Note for Maintenance:</label>
              <input
                className="input"
                placeholder="e.g. Paint refresh, sensor battery low, camera blocked..."
                value={maintenanceNote}
                onChange={e => setMaintenanceNote(e.target.value)}
                autoFocus
              />
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem' }}>
                <button type="button" className="btn-outline" style={{ fontSize: '0.78rem', padding: '0.35rem 0.75rem' }} onClick={() => setShowMaintInput(false)}>
                  Cancel
                </button>
                <button
                  type="button"
                  className="btn-primary"
                  style={{ fontSize: '0.78rem', padding: '0.35rem 0.75rem', background: '#ea580c' }}
                  onClick={() => {
                    onUpdateStatus(space.id, 'maintenance', maintenanceNote || 'Under scheduled maintenance');
                    setShowMaintInput(false);
                    onClose();
                  }}
                >
                  Confirm Maintenance
                </button>
              </div>
            </div>
          ) : (
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem' }}>
              {space.status !== 'available' && (
                <button
                  type="button"
                  onClick={() => {
                    onUpdateStatus(space.id, 'available');
                    onClose();
                  }}
                  style={{
                    padding: '0.45rem 0.85rem',
                    borderRadius: 'var(--radius)',
                    border: '1px solid #22c55e',
                    background: '#22c55e18',
                    color: '#16a34a',
                    fontWeight: 600,
                    fontSize: '0.8rem',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '0.4rem',
                  }}
                >
                  <UntitledIcon name="check-circle" size={14} />
                  Mark as Available
                </button>
              )}

              {space.status !== 'maintenance' && (
                <button
                  type="button"
                  onClick={() => setShowMaintInput(true)}
                  style={{
                    padding: '0.45rem 0.85rem',
                    borderRadius: 'var(--radius)',
                    border: '1px solid #f97316',
                    background: '#f9731618',
                    color: '#ea580c',
                    fontWeight: 600,
                    fontSize: '0.8rem',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '0.4rem',
                  }}
                >
                  <UntitledIcon name="wrench" size={14} />
                  Set Maintenance
                </button>
              )}

              {space.status !== 'disabled' && (
                <button
                  type="button"
                  onClick={() => {
                    onUpdateStatus(space.id, 'disabled');
                    onClose();
                  }}
                  style={{
                    padding: '0.45rem 0.85rem',
                    borderRadius: 'var(--radius)',
                    border: '1px solid #64748b',
                    background: '#64748b18',
                    color: 'var(--muted)',
                    fontWeight: 600,
                    fontSize: '0.8rem',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '0.4rem',
                  }}
                >
                  <UntitledIcon name="x" size={14} />
                  Disable Slot
                </button>
              )}

              {space.status !== 'occupied' && (
                <button
                  type="button"
                  onClick={() => {
                    onUpdateStatus(space.id, 'occupied');
                    onClose();
                  }}
                  style={{
                    padding: '0.45rem 0.85rem',
                    borderRadius: 'var(--radius)',
                    border: '1px solid #ef4444',
                    background: '#ef444418',
                    color: '#dc2626',
                    fontWeight: 600,
                    fontSize: '0.8rem',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '0.4rem',
                  }}
                >
                  <UntitledIcon name="car" size={14} />
                  Simulate Occupied
                </button>
              )}
            </div>
          )}
        </div>

        <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '1.25rem', borderTop: '1px solid var(--border)', paddingTop: '1rem' }}>
          <button type="button" className="btn-outline" onClick={onClose} style={{ fontSize: '0.85rem' }}>
            Close Details
          </button>
        </div>
      </div>
    </div>
  );
}
