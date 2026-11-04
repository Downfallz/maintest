const key = 'downfall.presentation';

// This browser's presentation preference is independent of account and seat capabilities.
export function preferredPresentation(storage) {
  try { return storage?.getItem(key) === 'arena' ? 'arena' : 'table'; }
  catch { return 'table'; }
}
export function savePresentation(storage, value) {
  try { storage?.setItem(key, value === 'arena' ? 'arena' : 'table'); }
  catch { /* The visible selection still applies for this visit. */ }
}
export function presentationPath(value) { return value === 'arena' ? '/arena' : '/play'; }
