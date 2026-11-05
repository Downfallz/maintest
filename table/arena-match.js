import { upkeepReceipt, missingRoundEnergy } from './arena-upkeep.js';
import { retainRoundEvents, matchReplay, turnoverSteps, turnoverText, lastResolvedText } from './feed.js';
import { playbackBoard } from './replay.js';
import { livePresentation } from './arena-live.js';
import { outcomeText } from './outcome.js';

export function roundNotes(entries, board) {
  const rounds = [...new Set(entries.map(entry => entry.event?.roundId ?? entry.round))].sort((a, b) => a - b);
  return rounds.flatMap(round => {
    const upkeep = upkeepReceipt(entries, round, board);
    const facts = upkeep ? [upkeep.energyNote, ...upkeep.energy.map(row => `${row.label}: ${row.text}`),
      ...upkeep.ongoing.map(row => `${row.label}: ${row.text}`), ...(upkeep.ongoing.length ? [] : [upkeep.ongoingNote])].filter(Boolean) : [];
    const rows = upkeep ? [`Round ${round} · Upkeep · ${facts.join('; ')}`] : [];
    const aftermath = turnoverSteps(entries, round, board).filter(step => step.interlude === 'aftermath');
    rows.push(...aftermath.map(step => `Round ${round} · ${turnoverText(step)}`));
    if (entries.some(entry => entry.event?.kind === 'RoundEnded' && (entry.event.roundId ?? entry.round) === round)) rows.push(`Round ${round} · Aftermath complete`);
    return rows;
  });
}

export function replayText(action, catalogue) {
  if (action.ordinaryEnergy) return turnoverText(action);
  if (action.interlude === 'upkeep') {
    const detail = action.ticks.length ? turnoverText(action) : 'Upkeep · No ongoing ticks applied';
    return action.missingOrdinaryEnergy ? `${missingRoundEnergy} ${detail}` : detail;
  }
  if (action.interlude === 'aftermath') return action.ended.length || action.immune.length ? turnoverText(action) : 'Aftermath · Round complete';
  if (action.interlude === 'purchases') {
    const purchases = action.choices.map(choice => purchaseText(choice, catalogue)).join(' · ');
    return `Mutations revealed · ${purchases || 'No mutations acquired'}`;
  }
  return [lastResolvedText(action), ...(action.dropped ?? []).map(text => `Dropped: ${text}`)].join(' · ');
}

function purchaseText(choice, catalogue) {
  const name = catalogue?.packages?.find(pack => pack.id === choice.tier)?.name ?? choice.tier;
  return `${choice.creature.label}: ${name}`;
}

// A cast has a frame before and one after; an interlude has one reference frame, or none at all.
function replayNote(action, stage) {
  if (!action?.frame) return 'No battlefield was recorded for this event.';
  if (action.interlude) return 'Reference: the battlefield at the start of this round’s combat, after upkeep and mutations.';
  return `${stage === 'before' ? 'Before' : 'After'} the recorded action`;
}

// A frameless event shows an empty battlefield, never a guessed one, so the scene replaces nothing.
const packagesOf = catalogue => new Map((catalogue?.packages ?? []).map(pack => [pack.id, pack]));
function replayBoard(action, actions, index, stage, latest, catalogue) {
  if (!action) return null;
  const board = livePresentation(action.frame ? { board: playbackBoard({ actions, index, stage, round: action.round }, latest.board) } : null, packagesOf(catalogue));
  board.round = action.round;
  if (action.interlude === 'upkeep') board.phase = 'StartOfRound';
  if (action.interlude === 'aftermath') board.phase = 'EndOfRound';
  return board;
}

// Only two public rounds are retained during play. A finished match explicitly rereads this same seat's
// full feed; no alternate seat, inferred action or private planning event is added to the replay.
export function createArenaMatch({ connection, onChange }) {
  let latest = null, catalogue = null, seat = null, entries = [], actions = [], index = -1, stage = 'before';
  let generation = 0, busy = false, error = '', cached = null, conceded = false;
  const emit = () => { cached = null; onChange(); };
  function reset(nextSeat) { generation++; latest = null; catalogue = null; seat = nextSeat; entries = []; actions = []; index = -1; busy = false; error = ''; conceded = false; emit(); }
  function receive(view, cards) {
    latest = view; catalogue = cards;
    if (!view) { reset(seat); return; }
    entries = retainRoundEvents(entries, view.feed, view.board?.roundNumber); emit();
  }
  async function replay() {
    if (!latest?.over || busy) return;
    const context = generation;
    busy = true; error = ''; emit();
    try {
      const answer = await connection.history();
      if (context !== generation) return;
      if (!answer?.ok || !answer.body?.over) { error = answer?.body?.message ?? 'Could not load the finished match. Try again.'; return; }
      const view = answer.body, feed = view.feed ?? [];
      const casts = matchReplay(feed, view.board, new Map((catalogue?.cards ?? []).map(card => [card.id, card])));
      const rounds = [...new Set(feed.map(entry => entry.event?.roundId ?? entry.round))];
      const aftermath = rounds.flatMap(round => {
        const cleanup = turnoverSteps(feed, round, view.board).filter(step => step.interlude === 'aftermath');
        const ended = feed.find(entry => entry.event?.kind === 'RoundEnded' && (entry.event.roundId ?? entry.round) === round);
        return cleanup.length || !ended ? cleanup : [{ sequence: ended.sequence, round, interlude: 'aftermath', ended: [], immune: [] }];
      });
      actions = [...casts, ...aftermath].sort((a, b) => a.sequence - b.sequence);
      index = actions.length ? 0 : -1; stage = 'before';
      if (!actions.length) error = 'Nothing was recorded to replay in this match.';
    } catch { if (context === generation) error = 'Could not load the replay. Try again.'; }
    finally { if (context === generation) { busy = false; emit(); } }
  }
  function move(direction) {
    if (index < 0) return;
    const two = !actions[index].interlude && actions[index].frame;
    if (two && ((direction > 0 && stage === 'before') || (direction < 0 && stage === 'after'))) stage = direction > 0 ? 'after' : 'before';
    else {
      const next = index + direction;
      if (next < 0 || next >= actions.length) return;
      index = next; stage = direction > 0 && !actions[index].interlude ? 'before' : 'after';
    }
    emit();
  }
  function jump(round) { const next = actions.findIndex(action => action.round === round); if (next >= 0) { index = next; stage = 'before'; emit(); } }
  function closeReplay() { index = -1; actions = []; emit(); }
  const canConcede = () => Boolean(latest && !latest.over && !latest.playedByBot && !latest.replay && !conceded);
  // A concession the host accepted is final even when the refresh that shows the result is late or lost.
  async function concede() {
    if (!canConcede() || busy) return;
    const context = generation; busy = true; error = ''; emit();
    try {
      const answer = await connection.concede();
      if (context !== generation) return;
      if (answer?.ok) conceded = true;
      else error = answer?.body?.message ?? 'The host could not confirm the concession. Check the refreshed board.';
    } catch { if (context === generation) error = 'Connection lost. Check the refreshed result before trying again.'; }
    finally { if (context === generation) { busy = false; emit(); } }
  }
  // Four readers ask for the model on every poll; the notes and the replay board are built once per change.
  function model() {
    if (cached) return cached;
    const action = actions[index], active = Boolean(action);
    const twoFrames = active && !action.interlude && Boolean(action.frame);
    cached = { active, sequence: action?.sequence, board: replayBoard(action, actions, index, stage, latest, catalogue), stage, index, count: actions.length, round: action?.round,
      rounds: [...new Set(actions.map(one => one.round))], summary: active ? replayText(action, catalogue) : '',
      note: replayNote(action, stage),
      previous: active && (index > 0 || (twoFrames && stage === 'after')),
      next: active && (index + 1 < actions.length || (twoFrames && stage === 'before')),
      outcome: outcomeText(latest, seat), over: Boolean(latest?.over),
      canConcede: canConcede(), busy, error,
      notes: roundNotes(entries, latest?.board) };
    return cached;
  }
  return { reset, receive, model, replay, move, jump, closeReplay, concede };
}
