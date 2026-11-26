const standardSlotNumbers = [1, 2, ...Array.from({ length: 24 }, (_, index) => index + 13), ...Array.from({ length: 10 }, (_, index) => index + 39)];

export const DEMO_SITE_ID = "b0000000-0000-4000-8000-000000000001";
export const DEMO_SLOT_CODES = [
  ...standardSlotNumbers.map((number) => `CarParkingSpace_${String(number).padStart(3, "0")}`),
  ...Array.from({ length: 12 }, (_, index) => `PARKING_EV_CAR_${String(index + 1).padStart(3, "0")}`),
];

export function demoSlotLabel(slotCode) {
  const index = DEMO_SLOT_CODES.indexOf(slotCode);
  if (index < 0) return null;
  const zone = String.fromCharCode(65 + Math.floor(index / 12));
  return `${zone}${String((index % 12) + 1).padStart(2, "0")}`;
}

export function demoSlotCode(label) {
  const match = /^([A-D])(\d{2})$/.exec(label ?? "");
  if (!match) return null;
  const index = (match[1].charCodeAt(0) - 65) * 12 + Number(match[2]) - 1;
  return DEMO_SLOT_CODES[index] ?? null;
}
