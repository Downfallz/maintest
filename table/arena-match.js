import { retainRoundEvents, matchReplay, turnoverSteps, turnoverText, lastResolvedText, roundUpkeep } from './feed.js';
import { playbackBoard } from './replay.js';
import { livePresentation } from './arena-live.js';
import { seatLabel } from './invitation.js';

export function arenaOutcome(view, seat) {
  if (!view?.outcome) return 'The match is over.';
  const { winner, reason } = view.outcome;
  const because = { Elimination: 'the last team standing', RoundCap: 'health at the round cap', Concession: 'concession' }[reason] ?? reason;
  const result = winner ? !view.replay && winner.toLowerCase() === seat?.toLowerCase() ? 'You win' : `${seatLabel(winner.toLowerCase())} wins` : 'Draw';
  return [result, because].filter(Boolean).join(' · ') + '.';
}

export function roundNotes(entries, board) {
  const rounds = [...new Set(entries.map(entry => entry.event?.roundId ?? entry.round))].sort((a, b) => a - b);
  return rounds.flatMap(round => {
    const upkeep = roundUpkeep(entries, round);
    const ticks = upkeep?.rows.filter(tick => tick.amount > 0) ?? [];
    const rows = upkeep ? [`Round ${round} · ${ticks.length ? turnoverText({ interlude: 'upkeep', ticks }) : 'Upkeep · No ongoing ticks applied'}`] : [];
    const aftermath = turnoverSteps(entries, round, board).filter(step => step.interlude === 'aftermath');
    rows.push(...aftermath.map(step => `Round ${round} · ${turnoverText(step)}`));
    if (entries.some(entry => entry.event?.kind === 'RoundEnded' && (entry.event.roundId ?? entry.round) === round)) rows.push(`Round ${round} · Aftermath complete`);
    return rows;
  });
}

export function replayText(action, catalogue) {
  if (action.interlude === 'upkeep') {
    const ticks = action.ticks.filter(tick => tick.amount > 0);
    return ticks.length ? turnoverText({ ...action, ticks }) : 'Upkeep · No ongoing ticks applied';
  }
  if (action.interlude === 'aftermath') return action.ended.length || action.immune.length ? turnoverText(action) : 'Aftermath · Round complete';
  if (action.interlude === 'purchases') return `Mutations revealed · ${action.choices.map(choice => `${choice.creature.label}: ${catalogue?.packages?.find(pack => pack.id === choice.tier)?.name ?? choice.tier}`).join(' · ') || 'No mutations acquired'}`;
  return [lastResolvedText(action), ...(action.dropped ?? []).map(text => `Dropped: ${text}`)].join(' · ');
}

// Only two public rounds are retained during play. A finished match explicitly rereads this same seat's
// full feed; no alternate seat, inferred action or private planning event is added to the replay.
export function createArenaMatch({ connection, onChange }) {
  let latest = null, catalogue = null, seat = null, entries = [], actions = [], index = -1, stage = 'before';
  let generation = 0, busy = false, error = '';
  const emit = () => onChange();
  function reset(nextSeat) { generation++; latest = null; catalogue = null; seat = nextSeat; entries = []; actions = []; index = -1; busy = false; error = ''; emit(); }
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
  async function concede() {
    if (!latest || latest.over || latest.playedByBot || latest.replay || busy) return;
    const context = generation; busy = true; error = ''; emit();
    try {
      const answer = await connection.concede();
      if (context === generation && !answer?.ok) error = answer?.body?.message ?? 'The host could not confirm the concession. Check the refreshed board.';
    } catch { if (context === generation) error = 'Connection lost. Check the refreshed result before trying again.'; }
    finally { if (context === generation) { busy = false; emit(); } }
  }
  function model() {
    const action = actions[index], active = Boolean(action);
    const board = active ? livePresentation(action.frame ? { board: playbackBoard({ actions, index, stage, round: action.round }, latest.board) } : null) : null;
    if (board) {
      board.round = action.round;
      if (action.interlude === 'upkeep') board.phase = 'StartOfRound';
      if (action.interlude === 'aftermath') board.phase = 'EndOfRound';
    }
    return { active, board, stage, index, count: actions.length, round: action?.round,
      rounds: [...new Set(actions.map(one => one.round))], summary: action ? replayText(action, catalogue) : '',
      note: !action?.frame ? 'No battlefield was recorded for this event.' : action.interlude ? 'Reference: the battlefield at the start of this round’s combat, after upkeep and mutations.' : `${stage === 'before' ? 'Before' : 'After'} the recorded action`,
      previous: active && (index > 0 || (!action.interlude && action.frame && stage === 'after')),
      next: active && (index + 1 < actions.length || (!action.interlude && action.frame && stage === 'before')),
      outcome: arenaOutcome(latest, seat), over: Boolean(latest?.over),
      canConcede: Boolean(latest && !latest.over && !latest.playedByBot && !latest.replay), busy, error,
      notes: roundNotes(entries, latest?.board) };
  }
  return { reset, receive, model, replay, move, jump, closeReplay, concede };
}
