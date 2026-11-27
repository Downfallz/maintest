import { livePresentation } from './battle-live.js';
import { orderOf, untapped } from './ties.js';
import { tempoLabel } from './terms.js';

// Consume the presented board, including its historical cursor during playback. Never rank locally.
function stateOf(index, board, finished) {
  if (finished || (board.cursor >= 0 && index < board.cursor)) return 'Completed';
  return index === board.cursor ? 'Now' : 'Upcoming';
}
const teamOf = slot => slot.side === 'ally' ? 'Your team' : 'Opponent';
function conditionOf(creature) {
  if (creature?.isAlive === false) return 'Defeated';
  return creature?.isStunned ? 'Stunned' : '';
}

// The spell a slot's creature has turned face up this round, from the host's public reveals, never a private intent
// (UX12 P7): its name and the portrait the catalogue names for it, drawn beside the creature in the strip.
function revealedSpell(board, id, cards) {
  const action = (board?.revealedActions ?? []).find(one => String(one?.actor) === id);
  if (!action?.spell) return null;
  const card = cards.get(action.spell);
  return { id: action.spell, name: card?.name ?? action.spell, portrait: card?.visual?.portrait ?? null };
}

/** @param {{ cards?: { id: string, name?: string, visual?: { portrait?: string | null } | null }[] } | null} [catalogue] */
export function battleOrder(board, catalogue = null) {
  const cards = new Map((catalogue?.cards ?? []).map(card => [card.id, card]));
  const finished = ['Cleanup', 'Finalization'].includes(board?.subPhase) || board?.phase === 'EndOfRound';
  const rows = (board?.timeline ?? []).map((slot, index) => {
    const creature = board.creatures?.find(one => one.id === String(slot.creature));
    return { id: String(slot.creature), position: index + 1, label: creature?.label ?? `Creature ${slot.creature}`,
      team: teamOf(slot), speed: slot.speed, tempo: tempoLabel(slot.speed), initiative: slot.initiative,
      tie: slot.tie || '', state: stateOf(index, board, finished), condition: conditionOf(creature),
      inspectable: Boolean(creature), spell: revealedSpell(board, String(slot.creature), cards) };
  });
  const summary = team => {
    const counts = new Map();
    for (const row of rows.filter(one => one.team === team)) counts.set(row.tempo, (counts.get(row.tempo) ?? 0) + 1);
    return `${team}: ${[...counts].map(([tempo, count]) => `${count} ${tempo}`).join(' · ') || 'No slots'}`;
  };
  return { round: board?.round, rows, summary: [summary('Your team'), summary('Opponent')],
    provisional: board?.subPhase === 'TieOrder', randomTie: rows.some(row => row.tie),
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
      random: positions.some(row => row.tie),
      assignments: ordered.map((id, offset) => ({ id, position: positions[offset]?.position ?? null })),
      fixed: rows.filter(row => !group.some(id => String(id) === row.id) && positions.some(one =>
        one.speed === row.speed && one.initiative === row.initiative) && row.team === 'Opponent') };
  }).map(group => ({ ...group,
    // Which team the random draw gave the tie's earlier place (ADR 0208); null when nothing was drawn or a side
    // of the tie has no place in this snapshot.
    earlier: group.random && group.places.length && group.fixed.length
      ? (Math.min(...group.places) < Math.min(...group.fixed.map(row => row.position)) ? 'Your team' : 'Opponent') : null,
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
  function receive(view, { since = /** @type {number | null} */ (null) } = {}) {
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

const sameTie = (a, b) => a.speed === b.speed && a.initiative === b.initiative;
const names = rows => {
  const labels = rows.map(row => row.label);
  return labels.length < 2 ? labels.join('') : `${labels.slice(0, -1).join(', ')} and ${labels.at(-1)}`;
};
const chooser = team => team === 'Your team' ? 'you chose' : 'your opponent chose';

/**
 * Why a creature holds its place in the order, from the presented board alone (game-rules.md, turn order): the
 * tempo band first, then initiative within the band, then a tie, which is settled at random between teams (ADR 0208)
 * and which the owner orders within their own places. Null for a creature with no place. Never re-ranks: it explains
 * the order the host served, and which team it gave the tie's earlier place.
 */
export function orderReason(board, id) {
  const rows = battleOrder(board).rows;
  const row = rows.find(one => one.id === String(id));
  if (!row) return null;
  const band = rows.filter(one => one.speed === row.speed);
  const tied = band.filter(one => one !== row && sameTie(one, row));
  const head = row.speed === 'Quick' ? `${row.tempo}: acts before every Precision creature.` : `${row.tempo}: acts after every Instinct creature.`;
  const placed = tied.length === 0 ? { tie: 'none', lines: [initiativeLine(row, band)] } : tieReason(row, tied);
  return { id: row.id, label: row.label, team: row.team, position: row.position, total: rows.length,
    speed: row.speed, tempo: row.tempo, initiative: row.initiative, tie: placed.tie, lines: [head, ...placed.lines] };
}

// A creature alone on its tempo and initiative: its initiative between its neighbours' in the band.
function initiativeLine(row, band) {
  if (!Number.isFinite(row.initiative)) return 'Initiative not recorded.';
  const ahead = band.findLast(one => Number.isFinite(one.initiative) && one.initiative > row.initiative);
  const behind = band.find(one => Number.isFinite(one.initiative) && one.initiative < row.initiative);
  const around = [ahead && `after ${ahead.label} (${ahead.initiative})`, behind && `before ${behind.label} (${behind.initiative})`].filter(Boolean);
  return around.length ? `Initiative ${row.initiative}: ${around.join(', ')}.` : `Initiative ${row.initiative}, alone in its tempo.`;
}

// A creature tied on tempo and initiative: settled at random between the teams, with the team that got the
// earlier place, or its owner's choice.
function tieReason(row, tied) {
  const group = [row, ...tied].sort((a, b) => a.position - b.position);
  const lines = [`Initiative ${row.initiative}, tied with ${names(tied)}.`];
  if (!group.some(one => one.team !== row.team)) {
    return { tie: 'own', lines: [...lines, `Same team: ${chooser(row.team)} the order.`] };
  }
  lines.push(`Settled at random: ${group[0].team === 'Your team' ? 'your team' : 'the opponent'} got the earlier place.`);
  if (group.filter(one => one.team === row.team).length > 1) lines.push(`Within its team's places, ${chooser(row.team)} the order.`);
  return { tie: 'random', lines };
}

/**
 * What the field marks while the seat orders its ties (ADR 0063), by creature id: each of the seat's tied creatures
 * with the place it takes now ("placed", once tapped or once it is the last left in its tie) or a "?" while it is
 * still to place ("pending"), and each opponent creature holding a place in the same tie ("opponent").
 */
export function tieMarks(view, tapped = []) {
  const groups = view?.options?.tieOrder?.ties ?? [];
  const left = untapped(groups, tapped);
  const marks = new Map();
  battleTieGroups(view, tapped).forEach((group, index) => {
    const settled = left[index].length <= 1;
    for (const one of group.assignments) {
      const placed = settled || tapped.includes(one.id);
      marks.set(String(one.id), { badge: placed && one.position != null ? String(one.position) : '?', state: placed ? 'placed' : 'pending' });
    }
    for (const row of group.fixed) marks.set(row.id, { badge: String(row.position), state: 'opponent' });
  });
  return marks;
}
