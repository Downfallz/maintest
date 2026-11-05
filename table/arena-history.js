import { chipText, laneOf } from './board.js';
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
    sections: [
      { title: 'Caster and spell', items: [line(`${who(action.actor.id, board)} · ${action.spell}`)] },
      { title: 'Targets and result', items: [line(targets || 'No targets'), line(status), ...(action.reason ? [line(`Fizzled: ${action.reason}`, 'harm')] : [])] },
      { title: 'Applied effects', items: effects.length ? effects : [line('No applied effects recorded.')] },
      { title: 'Dropped targets', items: (resolution.droppedTargets ?? []).map(failure => line(`${who(failure.target, board)} · ${reason(failure.error)}`)) },
      { title: 'Recorded changes', items: frameDetails(action.frame, board) },
    ],
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

// Called only after the public allowlist. Keep public text and sanitized boundary values, never raw events.
export function historyEntry(entry, board, catalogue) {
  if (!PUBLIC.has(entry?.event?.kind) || !Number.isInteger(entry?.sequence) || entry.sequence < 0) return null;
  const row = eventRow(entry, board, catalogue);
  return { ...row, sections: row.sections ?? [{ title: 'Recorded facts', items: row.details }], kind: entry.event.kind, participants: participants(entry.event, board),
    states: recordedStates(entry.event, board), sequence: entry.sequence, round: roundOf(entry) ?? 0, summary: row.summary ?? row.title };
}

// Public typed identifiers only: filters never recover identity by parsing narration.
function participants(event, board) {
  const action = event.resolution?.action ?? event.action;
  const ids = [action?.actor, ...(action?.targets ?? []), event.actor,
    ...(event.appliedOutcomes ?? []).map(one => one.target),
    ...(event.resolution?.droppedTargets ?? []).map(one => one.target),
    ...(event.grants ?? []).map(one => one.creature), ...(event.choices ?? []).map(one => one.creature),
    ...(event.timeline?.slots ?? []).map(one => one.creature), ...(event.creatures ?? []),
    ...Object.keys(event.expired ?? {}).map(Number),
    ...['energyRegenerationTicks', 'regenerationTicks', 'bleedTicks'].flatMap(key => (event[key] ?? []).map(one => one.creature))];
  return [...new Set(ids.filter(id => id != null))].map(id => ({ id: String(id), ...person(id, board) }));
}

// Keep only recorded public stats, not raw boards, spellbooks, intents or legal options.
function recordedStates(event, board) {
  if (!Array.isArray(event.frame?.before) || !Array.isArray(event.frame?.after)) return null;
  const snapshot = one => ({ id: String(one.id), ...person(one.id, board),
    health: one.health, maxHealth: one.maxHealth, energy: one.energy, defense: one.totalDefense,
    initiative: one.currentInitiative, defeated: one.isAlive === false,
    conditions: (one.conditions ?? []).map(condition => {
      const lane = laneOf(condition);
      return `${chipText(condition)} · ${lane === null ? 'permanent' : lane === 'new' ? 'first countdown pending' : `${lane} rounds`}`;
    }) });
  return { before: event.frame.before.map(snapshot), after: event.frame.after.map(snapshot) };
}

// The kind filter speaks the player's words (docs/domain/player-terminology.md), never the engine's event names.
export const KIND_LABELS = Object.freeze({
  CombatActionResolved: 'Spells cast', ActionRevealed: 'Spells revealed', SlotSkipped: 'Turns skipped',
  RoundEnergyGranted: 'Round energy', OngoingEffectsApplied: 'Ongoing effects', ConditionsExpired: 'Conditions ended',
  StunImmunityGained: 'Stun immunity', PurchasesRevealed: 'Mutations revealed', TimelineBuilt: 'Tempos revealed',
  TiesOrdered: 'Tie order', RoundStarted: 'Rounds begun', RoundEnded: 'Rounds complete', SubPhaseEntered: 'Steps begun',
  MatchStarted: 'Match start', MatchEnded: 'Match result', PlayerJoined: 'Players joined',
});

export function filterHistory(rows, filters) {
  return rows.filter(row => (!filters.round || String(row.round) === filters.round) &&
    (!filters.kind || row.kind === filters.kind) &&
    (!filters.creature || row.participants.some(one => one.id === filters.creature)) &&
    (!filters.team || row.participants.some(one => one.side === filters.team)));
}
const noFilters = () => ({ round: '', kind: '', creature: '', team: '' });
const ordered = rows => [...rows.values()].sort((a, b) => a.sequence - b.sequence);

// A live two-round window and one explicit reading page, each capped. Historical reads never enter
// the live connection or playback; a generation fences responses after close, viewpoint/access changes.
export function createArenaHistory({ limit = 200, load = async () => null, onChange = () => {} } = {}) {
  let rows = new Map(), page = null, current = 0, available = false, waiting = false, omitted = false, unsupported = false;
  let ordinaryRounds = new Set(), board = null, catalogue = null, generation = 0, loading = false, error = '', oldest = false;
  let filters = noFilters(), selected = null, stage = 'after', unread = 0, high = -1, browsing = false;
  function reset() {
    generation++; rows.clear(); page = null; current = 0; available = false; waiting = false; omitted = false; unsupported = false;
    ordinaryRounds.clear(); board = null; catalogue = null; loading = false; error = ''; oldest = false;
    filters = noFilters(); selected = null; stage = 'after'; unread = 0; high = -1; browsing = false;
  }
  function normalize(entries, source, content, ordinary = new Set(entries.filter(entry => entry.event?.kind === 'RoundEnergyGranted').map(roundOf))) {
    return entries.map(entry => {
      const row = historyEntry(entry, source, content);
      if (row?.kind === 'OngoingEffectsApplied' && !ordinary.has(row.round) && !ordinaryRounds.has(row.round)) row.details.unshift(line(missingRoundEnergy));
      return row;
    }).filter(Boolean);
  }
  function receive(view, content) {
    if (!view?.board) { reset(); return; }
    available = true; waiting = Boolean(view.waiting && !view.over); board = view.board; catalogue = content;
    // The host reads the board before the feed, so a response can hold next-round events on this round's board.
    // The window follows the newest round either names; the delta cursor never serves those events again.
    const rounds = (view.feed ?? []).filter(entry => PUBLIC.has(entry.event?.kind)).map(roundOf).filter(Number.isInteger);
    current = Math.max(current, board.roundNumber ?? 0, ...rounds);
    for (const entry of view.feed ?? []) {
      if (entry.event?.kind === 'RoundEnergyGranted') ordinaryRounds.add(roundOf(entry));
      if (entry.event?.kind && !PUBLIC.has(entry.event.kind) && !PRIVATE.has(entry.event.kind)) unsupported = true;
    }
    const publicEntries = (view.feed ?? []).filter(entry => PUBLIC.has(entry.event?.kind) && Number.isInteger(entry.sequence) && entry.sequence >= 0);
    if (high >= 0) unread += new Set(publicEntries.filter(entry => entry.sequence > high).map(entry => entry.sequence)).size;
    const recentEntries = publicEntries.filter(entry => roundOf(entry) >= current - 1 && roundOf(entry) <= current);
    omitted ||= recentEntries.length < publicEntries.length || recentEntries.length > limit;
    const additions = normalize(recentEntries.sort((a, b) => a.sequence - b.sequence).slice(-limit), board, catalogue);
    for (const row of additions) {
      if (!rows.has(row.sequence)) rows.set(row.sequence, row);
      if (page && !browsing && !page.has(row.sequence)) page.set(row.sequence, row);
    }
    for (const entry of publicEntries) high = Math.max(high, entry.sequence);
    const all = ordered(rows), recent = all.filter(row => row.round >= current - 1 && row.round <= current);
    omitted ||= recent.length < all.length || recent.length > limit;
    rows = new Map(recent.slice(-limit).map(row => [row.sequence, row]));
    if (page) {
      const pinned = page.get(selected), kept = ordered(page).slice(-limit);
      if (pinned && !kept.includes(pinned)) kept[0] = pinned;
      page = new Map(kept.map(row => [row.sequence, row]));
    }
    ordinaryRounds = new Set([...ordinaryRounds].filter(round => round >= current - 1));
  }
  const readingRows = () => waiting ? [] : ordered(page ?? rows);
  function model() {
    const all = readingRows(), filtered = filterHistory(all, filters), index = filtered.findIndex(row => row.sequence === selected);
    return { available, waiting, current, omitted, unsupported, rows: filtered, all, filters: { ...filters },
      loading, error, oldest, browsing, unread: waiting ? 0 : unread, latest: waiting ? undefined : ordered(rows).at(-1),
      review: index < 0 ? null : { row: filtered[index], stage, previous: index > 0, next: index < filtered.length - 1 } };
  }
  function open() { page ??= new Map(rows); unread = 0; }
  function close() { generation++; loading = false; page = null; selected = null; browsing = false; error = ''; oldest = false; }
  function latest() { generation++; loading = false; page = new Map(rows); filters = noFilters(); selected = null; browsing = false; unread = 0; error = ''; oldest = false; onChange(); }
  function filter(key, value) { if (!(key in filters)) return; filters[key] = String(value); selected = null; onChange(); }
  function select(sequence) { selected = sequence; stage = 'after'; onChange(); }
  function move(delta) {
    const visible = filterHistory(readingRows(), filters), index = visible.findIndex(row => row.sequence === selected);
    if (index >= 0 && visible[index + delta]) select(visible[index + delta].sequence);
  }
  function boundary(value) { if (value === 'before' || value === 'after') { stage = value; onChange(); } }
  function read() { unread = 0; }
  async function earlier() {
    if (!available || waiting || loading || oldest) return;
    const context = ++generation, before = readingRows()[0]?.sequence ?? Infinity;
    loading = true; error = ''; onChange();
    try {
      const answer = await load();
      if (context !== generation || !available) return;
      if (!answer?.ok || !Array.isArray(answer.body?.feed)) throw new Error('History unavailable. Try loading earlier rounds again.');
      const candidates = answer.body.feed.filter(entry => entry.sequence < before && PUBLIC.has(entry.event?.kind));
      // Slice before normalization: even a whole-match response never allocates a whole-match frame cache.
      const sorted = candidates.sort((a, b) => a.sequence - b.sequence);
      const selectedEntries = sorted.slice(-limit);
      const ordinary = new Set(answer.body.feed.filter(entry => entry.event?.kind === 'RoundEnergyGranted').map(roundOf));
      const loaded = normalize(selectedEntries, board, catalogue, ordinary);
      if (!loaded.length) { oldest = true; return; }
      page = new Map(loaded.map(row => [row.sequence, row])); browsing = true; filters = noFilters(); selected = null;
      oldest = sorted.length <= limit;
    } catch (failure) { if (context === generation) error = failure.message; }
    finally { if (context === generation) { loading = false; onChange(); } }
  }
  return { receive, reset, model, open, close, latest, filter, select, move, boundary, earlier, read };
}
