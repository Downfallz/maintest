// The cue a page hands the battle it opens (docs/art/journeys/lobby.md, UX9): the line that names the game, "Round 3
// against Rook", which the battle's first frame shows until its board arrives. Kept in the tab's session storage for
// one read, and only for the table it names. It imports nothing, so the battle's bundle takes it without the lobby.

export const ARRIVAL_KEY = 'downfall.arena.arrival.v1';

/** How long a cue holds: the press to the battle's first frame. One left unread goes stale and names no old round. */
export const ARRIVAL_MS = 60000;
const fresh = (kept, now) => kept && Number.isFinite(kept.at) && now - kept.at >= 0 && now - kept.at < ARRIVAL_MS;

/** Hands the battle the line of the table it is about to open, and the seat tokens its address carries. */
export function writeArrival(session, table, line, tokens = [], now = Date.now()) {
  try { session?.setItem(ARRIVAL_KEY, JSON.stringify({ table, line, tokens, at: now })); } catch { /* The battle opens without its cue. */ }
}

/**
 * The line for the address being opened, before the battle knows its table: kept when a seat token of the address
 * is one the lobby handed over. Not spent: `readArrival` spends it once the table is known.
 */
export function arrivalFor(session, search, now = Date.now()) {
  try {
    const kept = JSON.parse(session?.getItem(ARRIVAL_KEY) ?? 'null');
    const tokens = new Set(new URLSearchParams(search ?? '').values());
    return fresh(kept, now) && typeof kept.line === 'string' && kept.line && (kept.tokens ?? []).some(token => tokens.has(token)) ? kept.line : null;
  } catch { return null; }
}

/** The line handed over for this table, read once; null for another table, none, or a storage that refuses. */
export function readArrival(session, table, now = Date.now()) {
  try {
    const kept = JSON.parse(session?.getItem(ARRIVAL_KEY) ?? 'null');
    if (!kept) return null;
    // Spent once read for its table, and once stale for any: the seat tokens it carries do not stay in the tab.
    if (!fresh(kept, now)) { session.removeItem?.(ARRIVAL_KEY); return null; }
    if (kept.table !== table) return null;
    session.removeItem?.(ARRIVAL_KEY);
    return typeof kept.line === 'string' && kept.line ? kept.line : null;
  } catch { return null; }
}
