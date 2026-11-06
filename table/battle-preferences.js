const key = 'downfall.presentation';

// This browser's presentation preference is independent of account and seat capabilities.
export function preferredPresentation(storage) {
  try {
    const value = storage?.getItem(key);
    return value === 'battle' || value === 'arena' ? 'battle' : 'table';
  }
  catch { return 'table'; }
}
export function savePresentation(storage, value) {
  try { storage?.setItem(key, value === 'battle' ? 'battle' : 'table'); }
  catch { /* The visible selection still applies for this visit. */ }
}
export function presentationPath(value) { return value === 'battle' ? '/battle' : '/play'; }
