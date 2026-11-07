// The last result each seat of each game was shown in this browser, so a page opened again plays what happened
// while it was closed instead of starting from the board as it is. Only the newest games are remembered.
const KEY = 'downfall.battle.seen.v1';
const KEPT = 20;

function read(storage) {
  try { const value = JSON.parse(storage?.getItem(KEY) ?? 'null'); return Array.isArray(value) ? value : []; } catch { return []; }
}

/** The last sequence `game` (a table and seat) was shown here, or null for one this browser never showed. */
export function seenSince(storage, game) {
  const found = read(storage).find(one => one?.game === game);
  return Number.isInteger(found?.sequence) ? found.sequence : null;
}

/** Remembers what `game` was shown up to; the newest games first, the oldest let go of. */
export function saveSeen(storage, game, sequence) {
  if (!game || !Number.isInteger(sequence)) return;
  const kept = read(storage).filter(one => one && typeof one.game === 'string' && one.game !== game);
  try { storage?.setItem(KEY, JSON.stringify([{ game, sequence }, ...kept].slice(0, KEPT))); } catch { /* A full or refused store resumes from the board. */ }
}
