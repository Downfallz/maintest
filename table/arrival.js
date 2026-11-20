// The cue a page hands the battle it opens (docs/art/journeys/lobby.md, UX9): the line that names the game, "Round 3
// against Rook", which the battle's first frame shows until its board arrives. Kept in the tab's session storage for
// one read, and only for the table it names. It imports nothing, so the battle's bundle takes it without the lobby.

export const ARRIVAL_KEY = 'downfall.arena.arrival.v1';

/** Hands the battle the line of the table it is about to open. */
export function writeArrival(session, table, line) {
  try { session?.setItem(ARRIVAL_KEY, JSON.stringify({ table, line })); } catch { /* The battle opens without its cue. */ }
}

/** The line handed over for this table, read once; null for another table, none, or a storage that refuses. */
export function readArrival(session, table) {
  try {
    const kept = JSON.parse(session?.getItem(ARRIVAL_KEY) ?? 'null');
    if (!kept || kept.table !== table) return null;
    session.removeItem?.(ARRIVAL_KEY);
    return typeof kept.line === 'string' && kept.line ? kept.line : null;
  } catch { return null; }
}
