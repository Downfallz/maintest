import { resolvedSince, lastResolvedText } from './feed.js';
import { missingRoundEnergy, upkeepReceipt, upkeepDisplay } from './battle-upkeep.js';
import { historyEntry } from './battle-history.js';
import { presentedView } from './presentation.js';
import { combatSnapshot, combatChanges } from './combat-fx.js';
import { livePresentation } from './battle-live.js';
import { IMPACT_MS, REDUCED_MS, SETTLE_MS, WINDUP_MS, playbackRate } from './battle-timing.js';

const resultText = action => action.receipt ? [action.receipt.title, ...action.receipt.sections.flatMap(section => section.items.map(row => row.text))].join(' · ')
  : [lastResolvedText(action), ...(action.dropped ?? []).map(text => `Dropped: ${text}`)].join(' · ');

function pacedUpkeep(entries, board, round, ordinaryRounds) {
  const receipt = upkeepReceipt(entries, round, board);
  return receipt ? upkeepDisplay(receipt.energy, receipt.ongoing, ordinaryRounds.has(round) ? '' : receipt.energyNote) : null;
}

// Only public, meaningful turnover enters the paced sequence. No intermediate board is inferred.
function receipts(entries, board, ordinaryRounds) {
  return entries.flatMap(entry => {
    if (!['ConditionsExpired', 'StunImmunityGained', 'RoundEnergyGranted', 'OngoingEffectsApplied', 'MatchEnded'].includes(entry.event?.kind)) return [];
    const row = historyEntry(entry, board);
    // The opening receipt still belongs to AU3b's arrival-gated introduction.
    if (row?.category === 'Upkeep' && row.round === 1) return [];
    if (!row || (row.category === 'Aftermath' && !row.details.length)) return [];
    if (entry.event.kind === 'OngoingEffectsApplied' && !ordinaryRounds.has(row.round)) row.details.push({ text: missingRoundEnergy, tone: 'neutral' });
    const upkeepEvents = row.category === 'Upkeep' ? [entry] : [];
    return [{
      sequence: row.sequence, round: row.round || board?.roundNumber,
      upkeepEvents, upkeep: upkeepEvents.length ? pacedUpkeep(upkeepEvents, board, row.round, ordinaryRounds) : null,
      receipt: { title: row.title, category: row.category, sections: [{ title: row.title, items: row.details }] },
    }];
  });
}

const framed = action => Array.isArray(action.frame?.before) && Array.isArray(action.frame?.after)
  && action.frame.before.length > 0 && action.frame.before.length === action.frame.after.length
  && action.frame.before.every(one => action.frame.after.some(next => next.id === one.id));

const NEUTRAL = Object.freeze({ family: 'neutral', motif: 'focus', delivery: 'cast' });
// The sub-phases of a round once its combat has begun, as battle-upkeep.js reads them.
const COMBAT_BEGUN = Object.freeze(['IntentSelection', 'Activation', 'Cleanup', 'Finalization']);
const FIZZLE = Object.freeze({ family: 'neutral', motif: 'fizzle', delivery: 'cast' });

// How a cast is drawn comes with its card, where the builder published the spell art (ADR 0131): the page
// knows no spell by name. A card nobody drew, or an older host that serves none, gets the neutral mark.
export function spellVisual(action, cards) {
  if (action.status === 'Fizzled') return FIZZLE;
  const visual = cards?.get(action.action?.spell)?.visual;
  return visual && [visual.family, visual.motif, visual.delivery].every(word => typeof word === 'string') ? visual : NEUTRAL;
}

// A caster's own cost is a wound, never a hit by its spell: damage it took, or a lasting effect the catalogue
// calls harmful landing on the caster itself. The effect's name is the catalogue's to give, not the page's.
const isWound = (effect, action, harmful) => effect.target.id === action.actor?.id &&
  (effect.tone === 'harm' || (effect.onCaster === true && effect.kind === 'ConditionOutcome' && harmful.has(effect.effect?.kind)));

// A motif describes confirmed outcomes, never a spell's predicted damage or target legality.
export function actionCue(action, cards = new Map(), harmful = new Set()) {
  const effects = action.effects.map(effect => ({ creature: String(effect.target.id), text: effect.text, tone: effect.tone, wound: isWound(effect, action, harmful) }));
  const struck = [...new Set(action.effects.filter(effect => effect.tone === 'harm').map(effect => String(effect.target.id)))];
  const changes = framed(action) ? combatChanges(
    combatSnapshot({ allies: action.frame.before }), combatSnapshot({ allies: action.frame.after })) : [];
  let kind = struck.length ? 'strike' : 'pulse';
  if (action.status === 'Fizzled') kind = 'fizzle';
  const fizzled = action.status === 'Fizzled';
  const recipients = fizzled ? [] : [...new Set(effects.map(effect => effect.creature))];
  return { kind, visual: spellVisual(action, cards),
    recipients, contact: recipients.filter(id => id !== String(action.actor?.id)),
    struck: fizzled ? [] : struck, effects: fizzled ? [] : effects, changes };
}

// One bounded queue per selected seat. Latest remains authoritative while historical frames are displayed.
// The first read is a baseline (also after hidden-tab return); a sequence is consumed when queued, not when
// its animation ends, so duplicate polls, skip and a cancelled callback can never repeat it.
export function createBattlePlayback({ onChange, schedule = setTimeout, cancel = clearTimeout, limit = 12, onUpkeepHandled = (_round) => {} }) {
  let latest = null, queue = [], current = null, stage = 'decision', summary = '';
  let mode = 'auto', presented = null, read = null, ordinaryRounds = new Set();
  let seen = null, timer = null, generation = 0, reduced = false, rate = 1, catalogue = new Map(), harmful = new Set(), bought = new Map();
  const emit = () => onChange();
  function cancelTimer() { generation++; cancel(timer); timer = null; }
  // Animation phases follow the playback speed; a reduced-motion receipt is reading time and does not.
  function later(callback, delay, scaled = true) {
    const ticket = generation;
    timer = schedule(() => { if (ticket === generation) { timer = null; callback(); } }, scaled ? delay / rate : delay);
  }
  function next() {
    current = queue.shift() ?? null;
    if (!current) { stage = 'decision'; emit(); return; }
    presented = current.sequence;
    if (current.receipt?.category === 'Upkeep') onUpkeepHandled(current.round);
    summary = resultText(current);
    if (current.receipt) { stage = 'resolved'; emit(); if (!waits(current)) later(next, 3500, false); return; }
    stage = reduced ? 'impact' : 'windup'; emit();
    if (reduced) later(finish, REDUCED_MS, false);
    else later(() => { stage = 'impact'; emit(); later(finish, IMPACT_MS); }, WINDUP_MS);
  }
  // An incoming action, one the other team's creature took, waits for the player's OK in Auto as in Step:
  // nothing after it, a next action, the turnover or the live question, shows before it is read (ADR 0139).
  // The player's own casts, which they chose, never wait (ADR 0141); the turnover receipts wait in Step only.
  function incoming(item) {
    return Boolean(item && !item.receipt && !latest?.board?.allies?.some(one => String(one.id) === String(item.actor?.id)));
  }
  function waits(item) {
    return incoming(item) || (mode === 'step' && Boolean(item?.receipt));
  }
  function finish() {
    stage = 'resolved'; emit(); if (!waits(current)) later(next, SETTLE_MS);
  }
  // Settling shows the latest board with one result to read, whichever the caller names. An upkeep already
  // presented was handled when it was shown; one still queued is handled only when the player chose to skip
  // it. A rotation, a motion or speed change, a lost connection or a catch-up leaves it to the upkeep notice.
  function settle(text, dismissUpkeep = false) {
    if (dismissUpkeep) for (const item of queue) if (item.receipt?.category === 'Upkeep') onUpkeepHandled(item.round);
    cancelTimer(); queue = []; current = null; stage = 'decision'; summary = text; emit();
  }
  // What the player reads after a skip is the last result they would have reached, not the one cut short.
  function skip({ dismissUpkeep = false } = {}) {
    settle(queue.length ? resultText(queue.at(-1)) : summary, dismissUpkeep);
  }
  function reset() {
    cancelTimer(); latest = null; queue = []; current = null; stage = 'decision'; seen = null; presented = null; read = null; ordinaryRounds.clear(); summary = ''; catalogue = new Map(); harmful = new Set(); bought = new Map(); emit();
  }
  function receive(view, cards, { animate = true, effects = /** @type {{ kind: string, harmful: boolean }[]} */ ([]), packages = new Map() } = {}) {
    if (!view) { reset(); return; }
    latest = view; catalogue = cards; harmful = new Set(effects.filter(effect => effect.harmful).map(effect => effect.kind)); bought = packages;
    const entries = (view.feed ?? []).filter(entry => Number.isInteger(entry.sequence));
    const end = Math.max(-1, ...entries.map(entry => entry.sequence),
      Number.isInteger(view.feedNext) ? view.feedNext - 1 : -1);
    for (const entry of entries) if (entry.event?.kind === 'RoundEnergyGranted') ordinaryRounds.add(entry.event.roundId ?? entry.round);
    ordinaryRounds = new Set([...ordinaryRounds].sort((a, b) => b - a).slice(0, 4));
    if (seen === null) { seen = end; emit(); return; }
    const unique = [...new Map(entries.filter(entry => entry.sequence > seen).map(entry => [entry.sequence, entry])).values()];
    seen = Math.max(seen, end);
    const actions = resolvedSince(unique, undefined, view.board, cards);
    const arriving = [...actions, ...receipts(unique, view.board, ordinaryRounds)].sort((a, b) => a.sequence - b.sequence);
    if (!arriving.length) { emit(); return; }
    // Legacy events have text but no trustworthy intermediate board. A long catch-up (one read bringing more
    // than the limit or crossing several rounds, a reconnection's worth) must not keep a decision behind old pictures either. Both settle
    // on the latest snapshot with the last real result. What is already queued or playing is never dropped
    // for what arrives at the game's own pace: a round brings a handful of actions, not a backlog. An incoming
    // action waiting for its OK (ADR 0139), or a receipt held in Step, keeps what arrives under a dialog queued
    // behind it; anything else would play unseen, so it settles.
    const held = current !== null && waits(current);
    if ((!animate && !held) || actions.some(action => !framed(action)) || actions.length > limit || new Set(arriving.map(item => item.round)).size > 2 || arriving.length + queue.length > limit * 5) {
      settle(resultText(arriving.at(-1))); return;
    }
    // An upkeep keeps the board it arrived with, when that board is still its own round before combat: what
    // arrives later belongs to the planning and combat after it, never to the upkeep being read.
    const before = view.board?.roundNumber !== undefined && !COMBAT_BEGUN.includes(view.board.subPhase);
    for (const item of arriving) {
      if (item.receipt?.category === 'Upkeep') item.snapshot = before && view.board.roundNumber === item.round ? view : null;
      const previous = queue.at(-1) ?? current;
      if (item.receipt?.category === 'Upkeep' && previous?.receipt?.category === 'Upkeep' && previous.round === item.round) {
        previous.upkeepEvents.push(...item.upkeepEvents);
        previous.upkeep = pacedUpkeep(previous.upkeepEvents, view.board, previous.round, ordinaryRounds);
        previous.receipt.sections.push(...item.receipt.sections); previous.sequence = item.sequence;
        // A split upkeep reads on the board that brought its last part, while that is still before combat.
        previous.snapshot = item.snapshot ?? previous.snapshot;
        if (previous === current) {
          presented = item.sequence; summary = resultText(previous);
          cancelTimer(); if (!waits(current)) later(next, 3500, false);
        }
      } else queue.push(item);
    }
    if (!current) next(); else emit();
  }
  function model() {
    const action = current && !current.receipt ? current : null;
    const replay = action ? { actions: [current], index: 0, stage: stage === 'windup' ? 'before' : 'after', round: current.round } : null;
    // An upkeep read on the board it arrived with shows that round's creatures under its markers, as the
    // opening upkeep does; any other receipt, or an upkeep that arrived with a later board, draws none.
    const own = current?.receipt?.category === 'Upkeep' ? current.snapshot : null;
    const display = own ? { ...own, board: { ...own.board, phase: 'StartOfRound', subPhase: 'OngoingEffects', timeline: [] } }
      : current?.receipt ? { board: { roundNumber: current.round,
      phase: current.receipt.category === 'Result' ? 'Match complete' : current.receipt.category === 'Aftermath' ? 'EndOfRound' : 'StartOfRound',
      subPhase: current.receipt.category === 'Result' ? 'Finalization' : current.receipt.category === 'Aftermath' ? 'Cleanup' : 'OngoingEffects', allies: [], enemies: [], timeline: [] } }
      : action ? presentedView(latest, replay) : latest;
    const board = livePresentation(display, bought);
    const cue = action ? actionCue(action, catalogue, harmful) : null;
    const frame = action ? { ...board.frame, stage, actor: String(current.actor.id),
      target: cue.contact[0] ?? null, targets: [], cue } : board.frame;
    return { board, frame, summary, active: Boolean(current), sequence: current?.sequence ?? null,
      stage, rate, mode, presented, read, receipt: current?.receipt ?? null, upkeep: current?.upkeep ?? null,
      canNext: Boolean(current && stage === 'resolved'),
      // Next is offered for every incoming action, whatever the pace, and for everything when the player steps.
      asks: Boolean(current && waits(current)), incoming: incoming(current), label: current?.receipt?.title ?? (current ? `${current.spell} · ${current.status}` : ''), remaining: queue.length + (current ? 1 : 0), cue };
  }
  function setReduced(value) {
    if (value === reduced) return;
    reduced = value;
    // Changing a motion preference mid-tween settles immediately and invalidates its timers.
    if (current) skip();
  }
  function setRate(value) {
    const nextRate = playbackRate(value);
    if (nextRate === rate) return;
    rate = nextRate;
    if (current) skip(); else emit();
  }
  function setMode(value) {
    const nextMode = value === 'step' ? 'step' : 'auto';
    if (mode === nextMode) return;
    mode = nextMode;
    if (current && stage === 'resolved' && !incoming(current)) {
      cancelTimer(); if (!waits(current)) later(next, current.receipt ? 3500 : SETTLE_MS, !current.receipt);
    }
    emit();
  }
  function advance() {
    if (!current || stage !== 'resolved') return;
    read = current.sequence; cancelTimer(); next();
  }
  return { receive, model, skip, reset, setReduced, setRate, setMode, advance };
}
