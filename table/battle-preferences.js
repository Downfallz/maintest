const key = 'downfall.presentation';

// The battle view is the game (ADR 0156). The classic table is archived: it is still served at /play, for an
// admin replay, the practice scenarios, and a browser that asked for it by storing `classic` under this key, which
// is how its own tests reach it. Any other value, an older `table` or `battle` choice included, plays the battle.
export function preferredPresentation(storage) {
  try { return storage?.getItem(key) === 'classic' ? 'table' : 'battle'; }
  catch { return 'battle'; }
}
export function savePresentation(storage, value) {
  try { storage?.setItem(key, value === 'table' ? 'classic' : 'battle'); }
  catch { /* The battle view is the default anyway. */ }
}
export function presentationPath(value) { return value === 'table' ? '/play' : '/battle'; }
