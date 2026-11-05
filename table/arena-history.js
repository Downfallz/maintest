import { missingRoundEnergy } from './arena-upkeep.js';
import { resolvedSince, roundEnergyGrants, roundEnergyText, roundUpkeep, turnoverSteps, upkeepText } from './feed.js';
import { combatSnapshot, combatChanges } from './combat-fx.js';
import { outcomeText } from './outcome.js';
import { creatureLabel } from './arena-live.js';
import { stepLabel, tempoLabel } from './terms.js';

// This journal is public even though a seat feed also contains that seat's own hidden decisions.
// Default deny mirrors SeatVisibility's public cases; unknown payloads are never copied to the UI.
const PUBLIC = new Set(['CombatActionResolved', 'ActionRevealed', 'SlotSkipped', 'RoundEnergyGranted', 'OngoingEffectsApplied',
  'ConditionsExpired', 'StunImmunityGained', 'PurchasesRevealed', 'TimelineBuilt', 'TiesOrdered',
  'RoundStarted', 'RoundEnded', 'SubPhaseEntered', 'MatchStarted', 'MatchEnded', 'PlayerJoined']);
const PRIVATE = new Set(['IntentSubmitted', 'SpeedChoiceSubmitted', 'EvolutionChoiceSubmitted', 'EvolutionPassed', 'TieOrderSubmitted']);
const roundOf = entry => entry.event?.roundId ?? entry.round;
const line = (text, tone = 'neutral') => ({ text, tone });
const words = value => typeof value === 'string' ? value.replace(/([a-z])([A-Z])/g, '$1 $2') : '';
const reason = value => value?.message ?? value?.code ?? 'Reason not recorded';
// The feed writes a slot by its enum name (Player1); the page names seats in lower case (player1), the way
// the seat view's own outcome does, and outcomeText says "you" only to the name that matches.
const seatName = slot => typeof slot === 'string' ? slot.toLowerCase() : slot ?? null;
const spellName = (id, catalogue) => catalogue?.cards?.find(card => card.id === id)?.name ?? id ?? 'Unknown spell';
const orderRows = (timeline, board) => (Array.isArray(timeline?.slots) ? timeline.slots : [])
  .map((slot, index) => line(`${index + 1} · ${who(slot.creature, board)} · ${tempoLabel(slot.speed)}`));

// Named the way the scene names it (creatureLabel), so a line in the journal reads the same as the figure.
function person(id, board) {
  const ally = (board?.allies ?? []).some(creature => creature.id === id);
  const known = ally || (board?.enemies ?? []).some(creature => creature.id === id);
  return { label: creatureLabel(board, id), side: ally ? 'Your team' : known ? 'Opponent' : 'Unknown team' };
}
const who = (id, board) => { const one = person(id, board); return `${one.label} (${one.side})`; };

function frameDetails(frame, board) {
  if (!Array.isArray(frame?.before) || !Array.isArray(frame?.after)) return [line('Before/after values were not recorded for this action.')];
  const changes = combatChanges(combatSnapshot({ allies: frame.before }), combatSnapshot({ allies: frame.after }));
  return changes.flatMap(change => {
    const before = frame.before.find(one => one.id === change.creature);
    const after = frame.after.find(one => one.id === change.creature);
    const values = [['health', 'HP'], ['energy', 'Energy'], ['totalDefense', 'Defense']]
      .filter(([key]) => Number.isFinite(before[key]) && Number.isFinite(after[key]) && before[key] !== after[key])
      .map(([key, label]) => line(`${who(change.creature, board)} · ${label} ${before[key]} → ${after[key]}`));
    if (change.defeated) values.push(line(`${who(change.creature, board)} · Defeated`, 'harm'));
    return values;
  });
}

function combatRow(entry, board, catalogue) {
  const cards = new Map((catalogue?.cards ?? []).map(card => [card.id, card]));
  const action = resolvedSince([entry], undefined, board, cards)[0];
  const resolution = entry.event.resolution ?? {};
  const status = action.status === 'Resolved' && resolution.isCritical === false ? 'No critical' : action.status;
  const targets = action.targets.map(target => who(target.id, board)).join(', ');
  const effects = action.effects.map(effect => line(`${who(effect.target.id, board)} · ${effect.text}`, effect.tone));
  return { title: `${who(action.actor.id, board)} · ${action.spell}`, category: 'Combat', status,
    summary: `${action.spell} · ${status}${targets ? ` → ${targets}` : ''}`,
    details: [line(targets ? `Targets: ${targets}` : 'No targets'),
      ...(action.reason ? [line(`Fizzled: ${action.reason}`, 'harm')] : []),
      ...(effects.length ? effects : [line('No applied effects recorded.')]),
      ...(resolution.droppedTargets ?? []).map(failure => line(`Dropped target: ${failure.target == null ? '' : `${who(failure.target, board)}: `}${reason(failure.error)}`)),
      ...frameDetails(action.frame, board)] };
}

function eventRow(entry, board, catalogue) {
  const event = entry.event;
  switch (event.kind) {
    case 'CombatActionResolved': return combatRow(entry, board, catalogue);
    case 'RoundEnergyGranted': return { title: 'Upkeep · round energy', category: 'Upkeep', details: roundEnergyGrants(event).map(grant =>
      line(`${who(grant.creature, board)} · ${roundEnergyText(grant)}`, 'energy')) };
    case 'OngoingEffectsApplied': {
      const rows = roundUpkeep([entry], roundOf(entry))?.rows ?? [];
      return { title: 'Upkeep · ongoing effects', category: 'Upkeep', details: rows.length
        ? rows.map(tick => line(`${who(tick.creature, board)} · ${upkeepText(tick)}`, tick.tone))
        : [line('No ongoing ticks applied. Ordinary round energy is separate from these ticks.')] };
    }
    case 'ConditionsExpired':
    case 'StunImmunityGained': return { title: 'Aftermath · conditions', category: 'Aftermath',
      details: turnoverSteps([entry], roundOf(entry), board).flatMap(step => [
        ...step.ended.map(one => line(`${who(one.creature.id, board)} · ${one.conditions.map(condition => condition?.effect?.kind ?? 'Condition').join(' + ')} ends`, 'condition')),
        ...step.immune.map(one => line(`${who(one.id, board)} · immune to stun`, 'condition'))]) };
    case 'PurchasesRevealed': return { title: 'Mutations revealed', category: 'Command', details: (event.choices ?? []).map(choice =>
      line(`${who(choice.creature, board)} · ${catalogue?.packages?.find(pack => pack.id === choice.tier)?.name ?? choice.tier ?? 'Mutation details unavailable'}`)) };
    case 'SlotSkipped': return { title: `${who(event.actor, board)} · Turn skipped`, category: 'Combat', details: [line(reason(event.reason))] };
    case 'MatchEnded': {
      const outcome = event.outcome ? { ...event.outcome, winner: seatName(event.outcome.winner) } : event.outcome;
      return { title: 'Match complete', category: 'Result', details: [line(outcomeText({ outcome }, seatName(board?.slot)))] };
    }
    case 'ActionRevealed': {
      const action = event.action ?? {}, targets = (action.targets ?? []).map(id => who(id, board)).join(', ');
      return { title: `${who(action.actor, board)} · reveals ${spellName(action.spell, catalogue)}`, category: 'Combat',
        details: [line(targets ? `Targets: ${targets}` : 'No targets'), line('Its result follows as its own entry.')] };
    }
    case 'TimelineBuilt': return { title: 'Tempos revealed · turn order set', category: 'Command', details: orderRows(event.timeline, board) };
    case 'TiesOrdered': return { title: 'Tied creatures ordered', category: 'Command', details: orderRows(event.timeline, board) };
    case 'RoundStarted': return { title: 'Round begins', category: 'Upkeep', details: [] };
    case 'RoundEnded': return { title: 'Round complete', category: 'Aftermath', details: [] };
    case 'SubPhaseEntered': return { title: stepLabel(event.subPhase) || 'Phase changed', category: 'Phase', details: [] };
    case 'MatchStarted': return { title: 'Match begins', category: 'Match', details: [] };
    case 'PlayerJoined': return { title: 'Player joined', category: 'Match', details: [] };
    default: return { title: words(event.kind), category: 'Event', details: [line('Event details unavailable.')] };
  }
}

// Called only after the public allowlist. Normalized rows retain neither raw events nor frame snapshots.
export function historyEntry(entry, board, catalogue) {
  if (!PUBLIC.has(entry?.event?.kind) || !Number.isInteger(entry?.sequence) || entry.sequence < 0) return null;
  const row = eventRow(entry, board, catalogue);
  return { ...row, sequence: entry.sequence, round: roundOf(entry) ?? 0, summary: row.summary ?? row.title };
}

// One instance per selected viewpoint. No animation watermark, requests, writes, or local storage.
// Two rounds and at most 200 rows bound the first slice; earlier-round loading belongs to AU6.
export function createArenaHistory({ limit = 200 } = {}) {
  let ordinaryRounds = new Set();
  let rows = new Map(), current = 0, available = false, waiting = false, omitted = false, unsupported = false;
  function reset() { ordinaryRounds.clear(); rows = new Map(); current = 0; available = false; waiting = false; omitted = false; unsupported = false; }
  function receive(view, catalogue) {
    if (!view?.board) { reset(); return; }
    available = true; waiting = Boolean(view.waiting && !view.over);
    current = Math.max(current, view.board.roundNumber ?? 0);
    for (const entry of view.feed ?? []) if (entry.event?.kind === 'RoundEnergyGranted') ordinaryRounds.add(roundOf(entry));
    ordinaryRounds = new Set([...ordinaryRounds].filter(round => round >= current - 1));
    for (const entry of view.feed ?? []) {
      if (!PUBLIC.has(entry?.event?.kind)) {
        if (entry?.event?.kind && !PRIVATE.has(entry.event.kind)) unsupported = true;
        continue;
      }
      if (rows.has(entry.sequence)) continue;
      const row = historyEntry(entry, view.board, catalogue);
      if (row) {
        if (entry.event.kind === 'OngoingEffectsApplied' && !ordinaryRounds.has(row.round)) row.details.unshift(line(missingRoundEnergy));
        rows.set(row.sequence, row);
      }
    }
    const ordered = [...rows.values()].sort((left, right) => left.sequence - right.sequence);
    const recent = ordered.filter(row => row.round >= current - 1 && row.round <= current);
    if (recent.length < ordered.length || recent.length > limit) omitted = true;
    rows = new Map(recent.slice(-limit).map(row => [row.sequence, row]));
  }
  const model = () => ({ available, waiting, current, omitted, unsupported,
    rows: waiting ? [] : [...rows.values()] });
  return { receive, reset, model };
}
