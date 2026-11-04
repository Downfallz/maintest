// Existing command/history journeys run as returning players. Default-on first-use tips are exercised
// in arena-guidance.spec.js and the real-host arena.loop.js journeys, without this preference.
export async function returningArenaPlayer({ context }) {
  await context.addInitScript(() => {
    try { localStorage.setItem('downfall.arena.help.v1', JSON.stringify({ version: 1, muted: true, seen: [] })); } catch { /* Storage-denial fixtures remain meaningful. */ }
  });
}
