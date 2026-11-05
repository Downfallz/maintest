import { livePresentation } from './arena-live.js';
import { orderOf } from './ties.js';
import { tempoLabel } from './terms.js';

// Consume the presented board, including its historical cursor/rolls during playback. Never rank locally.
export function arenaOrder(board) {
  const finished = ['Cleanup', 'Finalization'].includes(board?.subPhase) || board?.phase === 'EndOfRound';
  const rows = (board?.timeline ?? []).map((slot, index) => {
    const creature = board.creatures?.find(one => one.id === String(slot.creature));
    const state = finished || (board.cursor >= 0 && index < board.cursor) ? 'Completed'
      : index === board.cursor ? 'Now' : 'Upcoming';
    return { id: String(slot.creature), position: index + 1, label: creature?.label ?? `Creature ${slot.creature}`,
      team: slot.side === 'ally' ? 'Your team' : slot.side === 'enemy' ? 'Opponent' : 'Unknown team',
      speed: slot.speed, tempo: tempoLabel(slot.speed), initiative: slot.initiative,
      roll: slot.roll || '', state, condition: creature?.isAlive === false ? 'Defeated' : creature?.isStunned ? 'Stunned' : '',
      inspectable: Boolean(creature) };
  });
  const summary = team => {
    const counts = new Map();
    for (const row of rows.filter(one => one.team === team)) counts.set(row.tempo, (counts.get(row.tempo) ?? 0) + 1);
    return `${team}: ${[...counts].map(([tempo, count]) => `${count} ${tempo}`).join(' · ') || 'No slots'}`;
  };
  return { round: board?.round, rows, summary: [summary('Your team'), summary('Opponent')],
    provisional: board?.subPhase === 'TieOrder', hasRolls: rows.some(row => row.roll),
    empty: 'Tempos stay private until the turn order is revealed.' };
}

// Only the offered groups are editable. Their positions come from the public order, even when interleaved
// with opponent positions. Missing timeline data is unknown, never reconstructed by sorting initiatives.
export function arenaTieGroups(view, tapped = []) {
  const rows = arenaOrder(livePresentation(view)).rows;
  return (view?.options?.tieOrder?.ties ?? []).map((group, index) => {
    const positions = rows.filter(row => group.some(id => String(id) === row.id));
    const ordered = orderOf([group], tapped);
    return { number: index + 1, places: positions.map(row => row.position),
      rolled: positions.some(row => row.roll),
      assignments: ordered.map((id, offset) => ({ id, position: positions[offset]?.position ?? null })),
      fixed: rows.filter(row => !group.some(id => String(id) === row.id) && positions.some(one =>
        one.speed === row.speed && one.initiative === row.initiative) && row.team === 'Opponent') };
  });
}

// A first read/reconnect is a baseline, not a fresh reveal. Keep one pending public reveal, discard it on
// round change, and wait out both teams' tie ordering. No timers, writes, private choices or persisted facts.
export function createArenaOrderReveal() {
  let high = null, pending = null, latest = null;
  function reset() { high = null; pending = null; latest = null; }
  function receive(view) {
    if (!view?.board) { reset(); return; }
    latest = view;
    const entries = (view.feed ?? []).filter(entry => Number.isInteger(entry.sequence));
    const end = Math.max(-1, ...entries.map(entry => entry.sequence));
    if (high !== null) for (const entry of entries) {
      if (entry.sequence > high && entry.event?.kind === 'TimelineBuilt' && entry.event.roundId === view.board.roundNumber)
        pending = entry.event.roundId;
    }
    high = Math.max(high ?? -1, end);
    if (pending !== view.board.roundNumber || view.over || view.waiting || view.replay) pending = null;
  }
  function model() {
    return { pending: pending !== null && latest?.board?.timeline?.length > 0 &&
      latest.board.subPhase !== 'TieOrder' && latest.waitingFor !== 'TieOrder' };
  }
  function presented() { pending = null; }
  return { receive, reset, model, presented };
}
