import { roundEnergyGrants, upkeepTicks, upkeepText, retainRoundEvents } from './feed.js';
import { creatureLabel } from './arena-live.js';

export const missingRoundEnergy = 'Ordinary round energy was not recorded. Its actual gain is unknown.';
const roundOf = entry => entry.event?.roundId ?? entry.round;

// Only the two public upkeep events enter this receipt. Current boards supply names and inspection links,
// never historical amounts, survival, conditions or durations (their snapshot can be several phases later).
export function upkeepReceipt(entries, round, board) {
  const events = entries.filter(entry => roundOf(entry) === round).sort((a, b) => a.sequence - b.sequence);
  const energy = events.find(entry => entry.event?.kind === 'RoundEnergyGranted');
  const ongoing = events.find(entry => entry.event?.kind === 'OngoingEffectsApplied');
  if (!energy && !ongoing) return null;
  const rows = ticks => ticks.map(tick => ({ ...tick, text: upkeepText(tick), label: creatureLabel(board, tick.creature),
    side: board?.allies?.some(one => one.id === tick.creature) ? 'Your team' : 'Opponent',
    inspectable: [...(board?.allies ?? []), ...(board?.enemies ?? [])].some(one => one.id === tick.creature) }));
  return { round, energy: rows(roundEnergyGrants(energy?.event)), ongoing: rows(ongoing ? upkeepTicks(ongoing.event) : []),
    energyNote: energy ? (energy.event.grants?.length ? '' : 'No ordinary energy grants recorded.') : missingRoundEnergy,
    ongoingNote: ongoing ? 'No ongoing effects ticked.' : 'Ongoing-effect details have not been received.',
    complete: Boolean(ongoing) };
}

// A bounded, tab-local high-water mark, containing no tokens, choices, boards or receipt facts. Reloads and
// reconnects must not announce an already presented round. Blocked storage falls back to this page's memory.
export function createArenaUpkeep(storage) {
  const storageKey = 'downfall.arena.upkeep.seen.v1';
  let seen = {};
  try {
    const saved = JSON.parse(storage?.getItem(storageKey) ?? '{}');
    seen = Object.fromEntries(Object.entries(saved).filter(([key, value]) => key.length < 200 && Number.isInteger(value) && value > 0).slice(-32));
  } catch { /* No presentation history is required to play. */ }
  let scope = '', entries = [], latest = null, observed = 0, pendingRound = 0;
  function mark(round) {
    if (!scope) return;
    delete seen[scope]; seen[scope] = round;
    seen = Object.fromEntries(Object.entries(seen).slice(-32));
    try { storage?.setItem(storageKey, JSON.stringify(seen)); } catch { /* Keep the in-memory mark. */ }
  }
  function reset(key = '') {
    scope = key; entries = []; latest = null; observed = seen[scope] ?? 0; pendingRound = 0;
  }
  function clear() { entries = []; latest = null; }
  function receive(view) {
    if (!view?.board) { clear(); return; }
    latest = view;
    const round = view.board.roundNumber ?? 0;
    entries = retainRoundEvents(entries, (view.feed ?? []).filter(entry => ['RoundEnergyGranted', 'OngoingEffectsApplied'].includes(entry.event?.kind)), round);
    if (round > observed) {
      // Joining an already advanced match is not an observed round transition.
      if ((round === 1 && !view.over) || observed > 0) pendingRound = round;
      else mark(round);
      observed = round;
    }
  }
  function model() {
    const available = Boolean(latest?.board && !latest.waiting && !latest.replay);
    const receipt = available ? upkeepReceipt(entries, latest.board.roundNumber, latest.board) : null;
    return { available, receipt, pending: Boolean(receipt?.complete && pendingRound === receipt.round && receipt.round > (seen[scope] ?? 0)) };
  }
  function presented(round = model().receipt?.round) {
    if (Number.isInteger(round) && round > (seen[scope] ?? 0)) mark(round);
    if (pendingRound <= round) pendingRound = 0;
  }
  return { reset, clear, receive, model, presented };
}
