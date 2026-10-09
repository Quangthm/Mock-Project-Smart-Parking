export const signInContactMessage = 'Enter a valid email address or phone number (9–15 digits, optionally starting with +).';
export function isValidSignInContact(input: string): boolean {
  const value = input.trim();
  if (!value || value.length > 255 || /\s/.test(value)) return false;
  if (/^\+?[0-9]{9,15}$/.test(value)) return true;
  return isValidAccountEmail(value);
}
export function isValidAccountEmail(input: string): boolean {
  const value = input.trim();
  if (!value || value.length > 255 || /\s/.test(value)) return false;
  const parts = value.split('@');
  if (parts.length !== 2) return false;
  const [local, domain] = parts;
  if (!local || local.length > 64 || local.startsWith('.') || local.endsWith('.') || local.includes('..') ||
      !/^[A-Za-z0-9.!#$%&'*+/=?^_`{|}~-]+$/.test(local)) return false;
  const labels = domain.split('.');
  return labels.length >= 2 && labels.every(label => label.length >= 1 && label.length <= 63 && /^[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?$/.test(label));
}
