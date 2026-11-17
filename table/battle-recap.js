// The recap a player reads coming back to an asynchronous game (docs/tabletop/async-recap-plan.md): where the game
// stands, the two teams now, what happened recently, what the seat had chosen and what it is asked, as data the
// sheet draws. What the seat was not shown, after its last visit (ADR 0194), is marked new.
//
// Built only from the seat's own view and feed, which the host has filtered for it: the rounds go through the
// journal's public rows (battle-history.js), so the recap never says more than the journal does; the seat's own
// hidden choices are read from its board and its own submissions. Nothing here is a rule: it gathers and counts
// what the host served.

import { historyEntry, historySummary } from './battle-history.js';
import { creatureLabel } from './battle-live.js';
import { outcomeText } from './outcome.js';
import { timeLeft } from './players.js';
import { REACTIONS, sentAgo } from './battle-reactions.js';
import { phaseLabel, tempoLabel } from './terms.js';

// What the seat is asked, in the push notice's words (PushNotices.Payload), as a sentence of its own.
const QUESTIONS = { Evolution: 'Choose your mutations', Speed: 'Choose a tempo', TieOrder: 'Order the tie',
  Intent: 'Declare a spell', Target: 'Choose your targets', Action: 'Choose a spell' };

/** The question a seat is asked (`waitingFor`) as the recap says it, or null when it is asked nothing. */
export function questionText(waitingFor) {
  if (!waitingFor) return null;
  return QUESTIONS[waitingFor] ?? 'Decide';
}

/** A creature's health over the span the sheet tells, as its line says it: `−12 HP`, `+3 HP`, or nothing. */
export function healthChangeText(change) {
  if (!Number.isFinite(change) || change === 0) return '';
  return `${change < 0 ? '−' : '+'}${Math.abs(change)} HP`;
}

const roundOf = entry => entry?.event?.roundId ?? entry?.round;
const isInt = Number.isInteger;
// A content id is the domain's word, never the player's: a name the catalogue lacks is said as unknown.
const spellName = (id, catalogue) => catalogue?.cards?.find(card => card.id === id)?.name ?? 'Unknown spell';
const mutationName = (id, catalogue) => catalogue?.packages?.find(pack => pack.id === id)?.name ?? 'Unknown mutation';
// The feed and the board write a slot by its enum name (Player1); a seat is named in lower case (player1).
const slotName = slot => typeof slot === 'string' && slot ? slot.toLowerCase() : null;

/**
 * The five parts of the sheet. `feed` is the seat's feed, at least the last completed round, the round under way and
 * everything after `seen`; `seen` the last sequence the seat was shown (seenSince), null when it was never told of
 * one: the recent rounds are then told with nothing marked new. `opponent` is the name the header gives the other seat.
 * A round the feed holds nothing of is told with `recorded: false`, so the sheet never reads a gap as a quiet round;
 * loading the feed back to `seen` is the caller's.
 * `seenAt` is when that was (the seat view's `seen.at` by default): the reactions sent since are marked new.
 * @param {{ view: any, feed: any[], seen: number | null, seenAt?: string | null, now: number, catalogue?: any,
 *   opponent?: string | null }} input
 */
export function buildRecap({ view, feed, seen, seenAt = view?.seen?.at ?? null, now, catalogue = null, opponent = null }) {
  const board = view?.board ?? {};
  // One entry a sequence: a feed merged from the live window and a page loaded back holds some twice.
  const entries = [...new Map((Array.isArray(feed) ? feed : []).filter(entry => isInt(entry?.sequence) && entry.sequence >= 0)
    .map(entry => [entry.sequence, entry])).values()].sort((a, b) => a.sequence - b.sequence);
  const isNew = sequence => isInt(seen) && sequence > seen;
  const bled = bledOut(entries, board);
  const over = Boolean(view?.over) || board.state === 'Ended';

  const current = Math.max(board.roundNumber ?? 0, ...entries.map(roundOf).filter(isInt));
  const ended = entries.filter(entry => entry.event?.kind === 'RoundEnded').map(roundOf).filter(isInt);
  // The last round that ended before this one; a feed that starts later still knows the round before is over.
  const before = ended.filter(round => round < current);
  const completed = before.length ? Math.max(...before) : current > 1 ? current - 1 : null;

  // The journal's public rows, in order; a row it does not tell is not the recap's either.
  const rows = entries.map(entry => historyEntry(entry, board, catalogue)).filter(Boolean);
  const told = new Set([current, completed, ...rows.filter(row => isNew(row.sequence)).map(row => row.round)]
    .filter(round => isInt(round) && round > 0));
  const span = told.size ? Math.min(...told) : current;

  const rounds = [...told].sort((a, b) => a - b).map(round => tellRound(round, rows.filter(row => row.round === round), {
    isNew, folded: round < (completed ?? current), completed: ended.includes(round) || (over && round === current),
    recorded: entries.some(entry => roundOf(entry) === round), bled: bled.filter(one => one.round === round) }));

  const teams = teamsNow(board, entries.filter(entry => roundOf(entry) >= span), isNew, bled);
  const side = team => ({ standing: team.filter(one => !one.defeated).length, total: team.length,
    health: team.filter(one => !one.defeated).reduce((sum, one) => sum + (one.health ?? 0), 0) });
  const sides = { you: side(teams.you), opponent: side(teams.opponent) };
  const result = over ? outcomeText(view, view?.seat) : null;

  return {
    seen: isInt(seen) ? seen : null,
    unseen: rounds.reduce((sum, one) => sum + newLines(one), 0),
    span,
    where: {
      opponent: opponent ?? null,
      round: current || null,
      step: phaseLabel(board.phase) || null,
      lastMove: view?.lastMoveAt ? { at: view.lastMoveAt, ago: sentAgo(view.lastMoveAt, now) || 'just now' } : null,
      timeLeft: over ? null : timeLeft(view?.answerBy, now),
      over, result, sides, leader: leaderOf(sides),
    },
    teams,
    rounds,
    chosen: chosenBy(board, entries, current, catalogue),
    asked: { question: over ? null : questionText(view?.waitingFor), reactions: reactionsFrom(view, seenAt, now) },
  };
}

// The lines of a round the sheet marks new: what R3's threshold and R4's count read.
const newLines = round => [...round.mutations, ...round.casts, ...round.skipped, ...round.defeated, ...(round.result ? [round.result] : [])]
  .filter(line => line.unseen).length;

function tellRound(round, rows, { isNew, folded, completed, recorded, bled }) {
  const told = { round, state: completed ? 'completed' : 'under way', folded, recorded, unseen: false,
    mutations: [], casts: [], skipped: [], defeated: [], result: null };
  for (const row of rows) {
    const summary = historySummary([row])[0];
    if (!summary) continue;
    const unseen = isNew(row.sequence);
    told.unseen ||= unseen;
    told.mutations.push(...summary.mutations.map(one => ({ ...one, unseen })));
    told.casts.push(...summary.casts.map(one => ({ ...one, unseen })));
    told.skipped.push(...summary.skipped.map(text => ({ text, unseen })));
    told.defeated.push(...summary.defeated.map(one => ({ ...one, unseen })));
    if (summary.result) told.result = { text: summary.result, unseen };
  }
  for (const one of bled) {
    const unseen = isNew(one.sequence);
    told.unseen ||= unseen;
    told.defeated.push({ creature: one.label, side: one.side, unseen });
  }
  return told;
}

const castFrames = entries => entries.filter(entry => entry.event?.kind === 'CombatActionResolved' && Array.isArray(entry.event.frame?.before));

// The creatures an ongoing effect defeated at an Upkeep: no cast frame records their fall, so the fall is the last
// Upkeep after the last frame that shows them standing whose bleed ticks name them. Read only for a creature the
// board says is defeated: the feed records the tick, never the health it left.
function bledOut(entries, board) {
  const frames = castFrames(entries);
  const fellInCast = id => frames.some(entry => entry.event.frame.before.find(one => one.id === id)?.isAlive !== false
    && entry.event.frame.after?.find(one => one.id === id)?.isAlive === false);
  const allies = new Set((board.allies ?? []).map(one => one.id));
  return [...(board.allies ?? []), ...(board.enemies ?? [])].filter(one => one.isAlive === false && !fellInCast(one.id)).flatMap(one => {
    const standing = frames.filter(entry => [...entry.event.frame.before, ...(entry.event.frame.after ?? [])]
      .some(seen => seen.id === one.id && seen.isAlive !== false)).map(entry => entry.sequence);
    const after = standing.length ? Math.max(...standing) : -1;
    // The last such tick: a defeated creature takes no more, and an earlier one it may have survived.
    const tick = entries.findLast(entry => entry.sequence > after && entry.event?.kind === 'OngoingEffectsApplied'
      && (entry.event.bleedTicks ?? []).some(bleed => bleed.creature === one.id));
    return tick ? [{ id: one.id, sequence: tick.sequence, round: roundOf(tick), label: creatureLabel(board, one.id),
      side: allies.has(one.id) ? 'Your team' : 'Opponent' }] : [];
  });
}

// Each creature as the board has it now, the ones defeated while away marked, and the health it gained or lost
// since the first recorded cast of the span: the frames are the only recorded health the feed holds.
// A creature an Upkeep's bleed defeated while away is marked as one a cast defeated.
function teamsNow(board, spanEntries, isNew, bled) {
  const frames = castFrames(spanEntries);
  const startOf = id => frames.map(entry => entry.event.frame.before.find(one => one.id === id)).find(Boolean);
  const fell = new Set(frames.filter(entry => isNew(entry.sequence)).flatMap(entry => (entry.event.frame.after ?? [])
    .filter(after => after.isAlive === false && entry.event.frame.before.find(one => one.id === after.id)?.isAlive !== false)
    .map(after => after.id)).concat(bled.filter(one => isNew(one.sequence)).map(one => one.id)));
  const creature = one => {
    const start = startOf(one.id);
    return { id: String(one.id), label: creatureLabel(board, one.id), health: one.health, maxHealth: one.maxHealth,
      energy: one.energy, defeated: one.isAlive === false, fellWhileAway: fell.has(one.id),
      change: Number.isFinite(start?.health) && Number.isFinite(one.health) ? one.health - start.health : null };
  };
  return { you: (board.allies ?? []).map(creature), opponent: (board.enemies ?? []).map(creature) };
}

// Who is ahead as the sheet says it: more creatures standing, then more health among them. A count, not a rule:
// the round cap's own reading is the host's (outcome).
function leaderOf({ you, opponent }) {
  if (you.standing !== opponent.standing) return you.standing > opponent.standing ? 'you' : 'opponent';
  if (you.health !== opponent.health) return you.health > opponent.health ? 'you' : 'opponent';
  return 'even';
}

// The seat's own choices still in force: its tempos this round, the spells it declared face down, and the mutations
// it bought last. Read from its board, then its own submissions; another seat's never reach this seat's feed, and a
// submission naming another slot is left out all the same.
function chosenBy(board, entries, current, catalogue) {
  const slot = slotName(board.slot);
  const own = kind => slot === null ? [] : entries.filter(entry => entry.event?.kind === kind && slotName(entry.event.slot) === slot);
  const label = id => creatureLabel(board, id);
  const speeds = board.speedChoices?.length ? board.speedChoices
    : own('SpeedChoiceSubmitted').filter(entry => roundOf(entry) === current).map(entry => entry.event.choice);
  const intents = board.intents?.length ? board.intents
    : own('IntentSubmitted').filter(entry => roundOf(entry) === current).map(entry => entry.event.intent);
  const bought = own('EvolutionChoiceSubmitted');
  const rounds = bought.map(roundOf).filter(isInt);
  const last = rounds.length ? Math.max(...rounds) : null;
  const picks = last !== null ? bought.filter(entry => roundOf(entry) === last).map(entry => entry.event.choice)
    : board.evolutionChoices?.length ? board.evolutionChoices : [];
  return {
    tempos: speeds.filter(Boolean).map(one => ({ creature: label(one.creature), tempo: tempoLabel(one.speed) })),
    spells: intents.filter(Boolean).map(one => ({ creature: label(one.actor), spell: spellName(one.spell, catalogue) })),
    mutations: picks.length ? { round: last ?? current, picks: picks.filter(Boolean).map(one => ({ creature: label(one.creature), mutation: mutationName(one.tier, catalogue) })) } : null,
  };
}

// The reactions the other seat sent, the newest last; those sent since the last visit marked new.
function reactionsFrom(view, seenAt, now) {
  const since = Date.parse(seenAt ?? '');
  const recent = Array.isArray(view?.reactions?.recent) ? view.reactions.recent : [];
  return recent.filter(one => one?.seat !== view?.seat).flatMap(one => {
    const known = REACTIONS.find(reaction => reaction.id === one.reaction);
    if (!known) return [];
    const at = Date.parse(one.at);
    return [{ glyph: known.glyph, label: known.label, ago: sentAgo(one.at, now), unseen: Number.isFinite(since) && at > since }];
  });
}
