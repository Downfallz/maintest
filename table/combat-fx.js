// Immutable combat-state changes for future visual FX. No rules and no DOM access.
// Only an explicitly new, server-confirmed event can produce transitions.
// A local choice, repeated SSE refresh or the first snapshot must not replay an effect.

export function combatSnapshot(board) {
  const actors = new Map();
  for (const creature of [...(board?.allies ?? []), ...(board?.enemies ?? [])]) {
    if (creature?.id == null) continue;
    actors.set(creature.id, {
      health: creature.health,
      energy: creature.energy,
      defense: creature.totalDefense,
      alive: creature.isAlive,
      stunned: creature.isStunned,
    });
  }
  return actors;
}

export function combatChanges(before, after) {
  const changes = [];
  for (const [id, next] of after) {
    const previous = before.get(id);
    if (!previous) continue; // new actor, not a hit
    const delta = {};
    for (const key of ['health', 'energy', 'defense']) {
      if (Number.isFinite(previous[key]) && Number.isFinite(next[key]) && previous[key] !== next[key]) {
        delta[key] = next[key] - previous[key];
      }
    }
    if (previous.alive === true && next.alive === false) delta.defeated = true;
    if (previous.stunned !== true && next.stunned === true) delta.stunned = true;
    if (Object.keys(delta).length) changes.push({ creature: id, ...delta });
  }
  return changes;
}

// One tracker per seat/table. The caller gives the latest confirmed event identity;
// null is deliberately NOT a cue to animate. Reset on round/replay/seat changes.
export function createCombatTransitionTracker() {
  let context = null;
  let eventId = null;
  let snapshot = null;
  return {
    observe({ board, contextKey, confirmedEventId }) {
      const next = combatSnapshot(board);
      if (contextKey !== context || snapshot === null) {
        context = contextKey;
        eventId = confirmedEventId ?? null;
        snapshot = next;
        return [];
      }
      if (confirmedEventId == null || confirmedEventId === eventId) return [];
      const changes = combatChanges(snapshot, next);
      eventId = confirmedEventId;
      snapshot = next;
      return changes;
    },
    reset() {
      context = null;
      eventId = null;
      snapshot = null;
    },
  };
}
