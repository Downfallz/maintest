import { livePresentation } from './battle-live.js';
import { orderOf } from './ties.js';
import { tempoLabel } from './terms.js';

// Consume the presented board, including its historical cursor/rolls during playback. Never rank locally.
function stateOf(index, board, finished) {
  if (finished || (board.cursor >= 0 && index < board.cursor)) return 'Completed';
  return index === board.cursor ? 'Now' : 'Upcoming';
}
const teamOf = slot => slot.side === 'ally' ? 'Your team' : 'Opponent';
function conditionOf(creature) {
  if (creature?.isAlive === false) return 'Defeated';
  return creature?.isStunned ? 'Stunned' : '';
}

export function battleOrder(board) {
  const finished = ['Cleanup', 'Finalization'].includes(board?.subPhase) || board?.phase === 'EndOfRound';
  const rows = (board?.timeline ?? []).map((slot, index) => {
    const creature = board.creatures?.find(one => one.id === String(slot.creature));
    return { id: String(slot.creature), position: index + 1, label: creature?.label ?? `Creature ${slot.creature}`,
      team: teamOf(slot), speed: slot.speed, tempo: tempoLabel(slot.speed), initiative: slot.initiative,
      roll: slot.roll || '', state: stateOf(index, board, finished), condition: conditionOf(creature),
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
export function battleTieGroups(view, tapped = []) {
  const rows = battleOrder(livePresentation(view)).rows;
  return (view?.options?.tieOrder?.ties ?? []).map((group, index) => {
    const positions = rows.filter(row => group.some(id => String(id) === row.id));
    const ordered = orderOf([group], tapped);
    return { number: index + 1, places: positions.map(row => row.position),
      rolled: positions.some(row => row.roll),
      assignments: ordered.map((id, offset) => ({ id, position: positions[offset]?.position ?? null })),
      fixed: rows.filter(row => !group.some(id => String(id) === row.id) && positions.some(one =>
        one.speed === row.speed && one.initiative === row.initiative) && row.team === 'Opponent') };
  }).map(group => ({ ...group,
    // An opponent holding two or more of these places orders its own creatures too: only the places are known.
    opponentOrderOpen: group.fixed.length > 1 }));
}

// A first read/reconnect is a baseline, not a fresh reveal. Keep one pending public reveal, discard it on
// round change, and wait out both teams' tie ordering. No timers, writes, private choices or persisted facts.
export function createBattleOrderReveal() {
  // `held` is the reveal being read: until Continue, the page must not count it as seen (ADR 0148), or a seat that
  // leaves and comes back would never be shown the order it had not read.
  let high = null, pending = null, sequence = null, held = null, latest = null;
  function reset() { high = null; pending = null; sequence = null; held = null; latest = null; }
  // `since` is the last sequence this seat was shown before the page was closed or hidden (battle-seen.js): on the
  // first read, an order built after it is revealed as it would have been live, instead of being taken as the
  // baseline. The creature acting first is the seat told it is its turn, the one most likely to come back to it.
  function receive(view, { since = null } = {}) {
    if (!view?.board) { reset(); return; }
    latest = view;
    const entries = (view.feed ?? []).filter(entry => Number.isInteger(entry.sequence));
    const end = Math.max(-1, ...entries.map(entry => entry.sequence));
    const after = high ?? (Number.isInteger(since) ? since : null);
    if (after !== null) for (const entry of entries) {
      if (entry.sequence > after && entry.event?.kind === 'TimelineBuilt' && entry.event.roundId === view.board.roundNumber) {
        pending = entry.event.roundId; sequence = entry.sequence;
      }
    }
    high = Math.max(high ?? -1, end);
    if (pending !== view.board.roundNumber || view.over || view.waiting || view.replay) { pending = null; sequence = null; }
  }
  function model() {
    return { pending: pending !== null && latest?.board?.timeline?.length > 0 &&
      ['IntentSelection', 'Activation'].includes(latest.board.subPhase) && latest.waitingFor !== 'TieOrder',
      // The first sequence the seat has not read yet, while an order waits or is being read.
      unread: sequence ?? held };
  }
  function presented() { held = sequence; pending = null; sequence = null; }
  function read() { held = null; }
  return { receive, reset, model, presented, read };
}
