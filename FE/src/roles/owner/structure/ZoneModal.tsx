import { useState, useEffect, type FormEvent } from 'react';
import { UntitledIcon } from '../../../components/icon/UntitledIcon';
import type { VehicleCategory } from '../../../lib/structureTypes';

interface ZoneModalProps {
  isOpen: boolean;
  floorCode: string;
  onClose: () => void;
  onSave: (name: string, vehicleType: VehicleCategory, initialSpaces?: number, status?: 'active' | 'inactive') => void;
  initialData?: { id?: string; name: string; vehicleType: VehicleCategory; status: 'active' | 'inactive' } | null;
}

export function ZoneModal({ isOpen, floorCode, onClose, onSave, initialData }: ZoneModalProps) {
  const [name, setName] = useState('');
  const [vehicleType, setVehicleType] = useState<VehicleCategory>('car');
  const [spacesCount, setSpacesCount] = useState(12);
  const [status, setStatus] = useState<'active' | 'inactive'>('active');
  const [error, setError] = useState('');

  useEffect(() => {
    if (initialData) {
      setName(initialData.name);
      setVehicleType(initialData.vehicleType);
      setStatus(initialData.status);
    } else {
      setName(`Khu Ô Tô ${floorCode || 'A'}`);
      setVehicleType('car');
      setSpacesCount(12);
      setStatus('active');
    }
    setError('');
  }, [initialData, isOpen, floorCode]);

  if (!isOpen) return null;

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      setError('Please provide a name for this zone.');
      return;
    }
    if (!initialData && (spacesCount <= 0 || spacesCount > 120)) {
      setError('Initial spaces count must be between 1 and 120.');
      return;
    }

    onSave(name.trim(), vehicleType, initialData ? undefined : spacesCount, status);
    onClose();
  };

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
        aria-labelledby="zone-modal-title"
        className="card"
        style={{
          width: '100%',
          maxWidth: 480,
          background: 'var(--bg)',
          color: 'var(--fg)',
          border: '1px solid var(--border)',
          borderRadius: '1rem',
          boxShadow: '0 20px 50px rgba(0, 0, 0, 0.25)',
          padding: '1.5rem',
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.25rem' }}>
          <div>
            <span style={{ fontSize: '0.72rem', fontWeight: 700, color: 'var(--primary)', letterSpacing: '0.08em', textTransform: 'uppercase' }}>
              Floor {floorCode} Structure
            </span>
            <h3 id="zone-modal-title" style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '1.25rem', margin: '0.2rem 0 0' }}>
              {initialData ? 'Edit Zone / Area' : 'Add New Zone / Area'}
            </h3>
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

        <form onSubmit={handleSubmit} style={{ display: 'grid', gap: '1rem' }}>
          <div>
            <label style={{ display: 'block', fontSize: '0.82rem', fontWeight: 600, marginBottom: '0.35rem' }}>
              Zone / Area Name <span style={{ color: 'var(--occupied)' }}>*</span>
            </label>
            <input
              className="input"
              placeholder="e.g. Zone A - Ô Tô Tiêu Chuẩn, Zone M - Xe Máy"
              value={name}
              onChange={e => setName(e.target.value)}
              autoFocus
            />
          </div>

          <div>
            <label style={{ display: 'block', fontSize: '0.82rem', fontWeight: 600, marginBottom: '0.35rem' }}>
              Designated Vehicle Type
            </label>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
              <button
                type="button"
                onClick={() => setVehicleType('car')}
                style={{
                  padding: '0.75rem',
                  borderRadius: 'var(--radius)',
                  border: `2px solid ${vehicleType === 'car' ? 'var(--primary)' : 'var(--border)'}`,
                  background: vehicleType === 'car' ? 'color-mix(in srgb, var(--primary) 12%, transparent)' : 'var(--card)',
                  color: 'var(--fg)',
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '0.5rem',
                  fontWeight: 600,
                  fontSize: '0.875rem',
                }}
              >
                <UntitledIcon name="car" size={18} />
                <span>Car (Ô tô)</span>
              </button>
              <button
                type="button"
                onClick={() => setVehicleType('motorcycle')}
                style={{
                  padding: '0.75rem',
                  borderRadius: 'var(--radius)',
                  border: `2px solid ${vehicleType === 'motorcycle' ? 'var(--primary)' : 'var(--border)'}`,
                  background: vehicleType === 'motorcycle' ? 'color-mix(in srgb, var(--primary) 12%, transparent)' : 'var(--card)',
                  color: 'var(--fg)',
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '0.5rem',
                  fontWeight: 600,
                  fontSize: '0.875rem',
                }}
              >
                <UntitledIcon name="motorcycle" size={18} />
                <span>Motorcycle (Xe máy)</span>
              </button>
            </div>
          </div>

          {!initialData && (
            <div>
              <label style={{ display: 'block', fontSize: '0.82rem', fontWeight: 600, marginBottom: '0.35rem' }}>
                Initial Parking Spaces to Generate
              </label>
              <input
                className="input"
                type="number"
                min={1}
                max={120}
                value={spacesCount}
                onChange={e => setSpacesCount(Math.max(1, parseInt(e.target.value) || 1))}
              />
              <span style={{ fontSize: '0.75rem', color: 'var(--muted)', marginTop: '0.25rem', display: 'block' }}>
                Automatically creates spaces (e.g. {floorCode}01, {floorCode}02...) ready for occupancy.
              </span>
            </div>
          )}

          <div>
            <label style={{ display: 'block', fontSize: '0.82rem', fontWeight: 600, marginBottom: '0.35rem' }}>
              Zone Operating Status
            </label>
            <select
              className="input"
              value={status}
              onChange={e => setStatus(e.target.value as 'active' | 'inactive')}
            >
              <option value="active">Active</option>
              <option value="inactive">Inactive</option>
            </select>
          </div>

          {error && (
            <p style={{ margin: 0, color: 'var(--occupied)', fontSize: '0.82rem' }}>
              {error}
            </p>
          )}

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem', marginTop: '0.5rem' }}>
            <button type="button" className="btn-outline" onClick={onClose}>
              Cancel
            </button>
            <button type="submit" className="btn-primary">
              {initialData ? 'Save Changes' : 'Create Zone'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
