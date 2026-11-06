// Existing command/history journeys run as returning players. Default-on first-use tips are exercised
// in battle-guidance.spec.js and the real-host battle.loop.js journeys, without this preference.
export async function returningBattlePlayer({ context }) {
  await context.addInitScript(() => {
    try { localStorage.setItem('downfall.arena.help.v1', JSON.stringify({ version: 1, muted: true, seen: [] })); } catch { /* Storage-denial fixtures remain meaningful. */ }
  });
}
