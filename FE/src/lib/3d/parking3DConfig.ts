import type { LotType, SlotStatus } from '../types';

export interface Parking3DModelDef {
  id: string;
  fileName: 'outdoor_parking_lot.glb' | 'indoor_parking_lot.glb' | 'underground_parking_lot.glb';
  path: string;
  label: string;
  lotType: LotType;
  description: string;
}

export const ALLOWED_PARKING_MODELS: Record<LotType, Parking3DModelDef> = {
  outdoor: {
    id: 'outdoor_parking_lot',
    fileName: 'outdoor_parking_lot.glb',
    path: '/models/parking/outdoor_parking_lot.glb',
    label: 'Outdoor Parking Lot',
    lotType: 'outdoor',
    description: 'Mô hình 3D bãi đỗ xe ngoài trời (outdoor_parking_lot.glb)',
  },
  'multi-storey': {
    id: 'indoor_parking_lot',
    fileName: 'indoor_parking_lot.glb',
    path: '/models/parking/indoor_parking_lot.glb',
    label: 'Indoor / Multi-Storey Parking Lot',
    lotType: 'multi-storey',
    description: 'Mô hình 3D bãi đỗ xe trong nhà / nhiều tầng (indoor_parking_lot.glb)',
  },
  basement: {
    id: 'underground_parking_lot',
    fileName: 'underground_parking_lot.glb',
    path: '/models/parking/underground_parking_lot.glb',
    label: 'Underground / Basement Parking Lot',
    lotType: 'basement',
    description: 'Mô hình 3D bãi đỗ tầng hầm ngầm (underground_parking_lot.glb)',
  },
};

export const ALLOWED_MODEL_FILE_NAMES = [
  'outdoor_parking_lot.glb',
  'indoor_parking_lot.glb',
  'underground_parking_lot.glb',
] as const;

export type AllowedModelFileName = (typeof ALLOWED_MODEL_FILE_NAMES)[number];

export function isAllowedModelFileName(fileName: string): fileName is AllowedModelFileName {
  return ALLOWED_MODEL_FILE_NAMES.includes(fileName.trim().toLowerCase() as AllowedModelFileName);
}

export function getModelDefByFileName(fileName: string): Parking3DModelDef | undefined {
  const normalized = fileName.trim().toLowerCase();
  return Object.values(ALLOWED_PARKING_MODELS).find(m => m.fileName.toLowerCase() === normalized);
}

export function getModelDefByLotType(lotType: LotType): Parking3DModelDef {
  return ALLOWED_PARKING_MODELS[lotType] || ALLOWED_PARKING_MODELS.outdoor;
}

export function validateModelUpload(fileName: string, expectedLotType?: LotType): {
  isValid: boolean;
  message?: string;
  modelDef?: Parking3DModelDef;
} {
  const normalized = fileName.trim().toLowerCase();

  if (!isAllowedModelFileName(normalized)) {
    return {
      isValid: false,
      message: `Tên file "${fileName}" không hợp lệ! Hệ thống chỉ chấp nhận đúng 3 file mô hình chuẩn: outdoor_parking_lot.glb, indoor_parking_lot.glb hoặc underground_parking_lot.glb.`,
    };
  }

  const modelDef = getModelDefByFileName(normalized);
  if (!modelDef) {
    return {
      isValid: false,
      message: 'Mô hình 3D không tìm thấy định nghĩa tương ứng.',
    };
  }

  if (expectedLotType && modelDef.lotType !== expectedLotType) {
    return {
      isValid: false,
      message: `File "${fileName}" thuộc loại bãi "${modelDef.label}", nhưng loại bãi hiện tại đang chọn là "${ALLOWED_PARKING_MODELS[expectedLotType].label}". Vui lòng chọn đúng file tương ứng với loại bãi hoặc đổi loại bãi.`,
      modelDef,
    };
  }

  return {
    isValid: true,
    modelDef,
  };
}

export const SLOT_STATUS_COLORS: Record<SlotStatus | 'selected' | 'default', number> = {
  available: 0x22c55e, // Emerald Green
  occupied: 0xef4444,  // Red
  reserved: 0xf59e0b,  // Amber
  selected: 0x3b82f6,  // Blue
  default: 0x64748b,   // Slate Gray
};

export const SLOT_STATUS_HEX = {
  available: '#22c55e',
  occupied: '#ef4444',
  reserved: '#f59e0b',
  selected: '#3b82f6',
  default: '#64748b',
};
