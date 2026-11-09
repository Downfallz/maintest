import { resolvedSince, lastResolvedText } from './feed.js';
import { missingRoundEnergy, upkeepReceipt, upkeepDisplay } from './battle-upkeep.js';
import { historyEntry } from './battle-history.js';
import { presentedView } from './presentation.js';
import { combatSnapshot, combatChanges } from './combat-fx.js';
import { creatureLabel, livePresentation } from './battle-live.js';
import { purchaseLog } from './battle-chronology.js';
import { endedChip, immuneChip, outcomeChip } from './effect-chips.js';
import { IMPACT_MS, REDUCED_MS, SETTLE_MS, WINDUP_MS, playbackRate } from './battle-timing.js';

const resultText = action => action.receipt ? [action.receipt.title, ...action.receipt.sections.flatMap(section => section.items.map(row => row.text))].join(' · ')
  : [lastResolvedText(action), ...(action.dropped ?? []).map(text => `Dropped: ${text}`)].join(' · ');

// What the reading bar says of the item being read: whose it is, what was cast, and what came of it, in the
// names the board uses. A receipt says only its title: the receipt itself is on the field.
export function readingCard(item, board, theirs) {
  if (!item) return null;
  if (item.receipt) {
    const paced = item.upkeep ?? item.aftermath;
    return { kind: 'summary', title: paced?.title ?? item.receipt.title, detail: paced?.summary ?? '' };
  }
  const name = id => creatureLabel(board, id);
  const outcome = item.status === 'Fizzled' ? [`Fizzled: ${item.reason}`]
    : [...(item.status === 'Critical' ? ['Critical'] : []),
      ...(item.effects?.length ? item.effects.map(effect => `${effect.text} → ${name(effect.target?.id)}`) : ['No effect'])];
  // The same outcomes as chips, gathered by the creature they landed on, for the card to draw.
  const groups = [];
  if (item.status !== 'Fizzled') for (const effect of item.effects ?? []) {
    const target = name(effect.target?.id);
    const group = groups.find(one => one.target === target) ?? (groups.push({ target, chips: [] }), groups.at(-1));
    group.chips.push(outcomeChip(effect));
  }
  return { kind: theirs ? 'incoming' : 'own', title: `${name(item.actor?.id)} · ${item.spell}`, detail: outcome.join(' · '), groups };
}

// A paced turnover is read on the field: a plaque on each creature it touched, and one line in the reading
// card. Its full text stays one tap away, in the result details.
function pacedUpkeep(entries, board, round, ordinaryRounds) {
  const receipt = upkeepReceipt(entries, round, board);
  return receipt ? { title: 'Upkeep', ...upkeepDisplay(receipt.energy, receipt.ongoing, ordinaryRounds.has(round) ? '' : receipt.energyNote) } : null;
}
function pacedAftermath(entries, board) {
  const markers = new Map();
  const mark = (id, chip) => {
    const key = String(id);
    if (!markers.has(key)) markers.set(key, { id: key, name: creatureLabel(board, id),
      side: board?.allies?.some(one => one.id === id) ? 'Your team' : 'Opponent', lines: [] });
    markers.get(key).lines.push({ text: chip.value, tone: 'condition', chip });
  };
  let ended = 0, immune = 0;
  for (const entry of entries) {
    if (entry.event?.kind === 'ConditionsExpired') {
      for (const [id, conditions] of Object.entries(entry.event.expired ?? {})) {
        for (const condition of conditions ?? []) { ended++; mark(Number(id), endedChip(condition?.effect?.kind)); }
      }
    }
    if (entry.event?.kind === 'StunImmunityGained') for (const id of entry.event.creatures ?? []) { immune++; mark(id, immuneChip()); }
  }
  const summary = [ended ? `${ended} condition${ended === 1 ? '' : 's'} ended` : '', immune ? `${immune} immune to stun` : ''].filter(Boolean).join(' · ');
  return { title: 'Aftermath', summary, markers: [...markers.values()] };
}
// The aftermath reads on the field as the round's last recorded action left it: conditions end after that
// frame, and nothing later is borrowed.
function aftermathView(view, action) {
  const after = action?.frame?.after;
  if (!Array.isArray(after) || !after.length) return null;
  const known = new Map([...(view.board?.allies ?? []), ...(view.board?.enemies ?? [])].map(one => [one.id, one]));
  const allies = new Set((view.board?.allies ?? []).map(one => one.id));
  // Only the name is borrowed from the latest board: its stats, spells and mutations may already be later ones.
  const shown = one => ({ ...one, name: one.name ?? known.get(one.id)?.name });
  // The round's own order stays on the strip, every turn taken.
  return { ...view, board: { ...view.board, roundNumber: action.round, timeline: action.frame.timeline ?? [], rollOffs: action.frame.rollOffs ?? [],
    allies: after.filter(one => allies.has(one.id)).map(shown), enemies: after.filter(one => !allies.has(one.id)).map(shown) } };
}

// Only public, meaningful turnover enters the paced sequence. No intermediate board is inferred. The match's end
// is not read here: the match result opens by itself once the last result is read.
// A defeated creature takes no further part: a condition ending on it, or a stun immunity it gains, is not
// read on the field. History keeps the engine's full record. Who had fallen is read as of that turnover: the
// frame after the last recorded action before it, else the frame before the first one after it, else the
// latest board, which nothing recorded comes after.
function fallenAt(entry, entries, board) {
  const framedActions = entries.filter(one => one.event?.kind === 'CombatActionResolved' && Array.isArray(one.event.frame?.after))
    .sort((left, right) => left.sequence - right.sequence);
  const before = framedActions.filter(one => one.sequence < entry.sequence).at(-1)?.event.frame.after;
  const after = framedActions.find(one => one.sequence > entry.sequence)?.event.frame.before;
  const creatures = before ?? after ?? [...(board?.allies ?? []), ...(board?.enemies ?? [])];
  return new Set(creatures.filter(one => one.isAlive === false).map(one => String(one.id)));
}
function living(entry, entries, board) {
  const defeated = fallenAt(entry, entries, board);
  if (!defeated.size) return entry;
  if (entry.event?.kind === 'ConditionsExpired') return { ...entry, event: { ...entry.event,
    expired: Object.fromEntries(Object.entries(entry.event.expired ?? {}).filter(([id]) => !defeated.has(String(id)))) } };
  if (entry.event?.kind === 'StunImmunityGained') return { ...entry, event: { ...entry.event,
    creatures: (entry.event.creatures ?? []).filter(id => !defeated.has(String(id))) } };
  return entry;
}
function receipts(entries, board, ordinaryRounds) {
  return entries.flatMap(recorded => {
    if (!['ConditionsExpired', 'StunImmunityGained', 'RoundEnergyGranted', 'OngoingEffectsApplied'].includes(recorded.event?.kind)) return [];
    const entry = living(recorded, entries, board);
    const row = historyEntry(entry, board);
    // The opening receipt still belongs to AU3b's arrival-gated introduction.
    if (row?.category === 'Upkeep' && row.round === 1) return [];
    if (!row || (row.category === 'Aftermath' && !row.details.length)) return [];
    if (entry.event.kind === 'OngoingEffectsApplied' && !ordinaryRounds.has(row.round)) row.details.push({ text: missingRoundEnergy, tone: 'neutral' });
    const upkeepEvents = row.category === 'Upkeep' ? [entry] : [];
    const aftermathEvents = row.category === 'Aftermath' ? [entry] : [];
    return [{
      // A receipt takes in later parts of its turnover and its sequence moves to the last; `first` stays on the
      // earliest, which is what a page closed before it was read resumes from.
      sequence: row.sequence, first: row.sequence, round: row.round || board?.roundNumber,
      upkeepEvents, upkeep: upkeepEvents.length ? pacedUpkeep(upkeepEvents, board, row.round, ordinaryRounds) : null,
      aftermathEvents, aftermath: aftermathEvents.length ? pacedAftermath(aftermathEvents, board) : null,
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
  const effects = action.effects.map(effect => ({ creature: String(effect.target.id), text: effect.text, tone: effect.tone, wound: isWound(effect, action, harmful), chip: outcomeChip(effect) }));
  const struck = [...new Set(action.effects.filter(effect => effect.tone === 'harm').map(effect => String(effect.target.id)))];
  const changes = framed(action) ? combatChanges(
    combatSnapshot({ allies: action.frame.before }), combatSnapshot({ allies: action.frame.after })) : [];
  let kind = struck.length ? 'strike' : 'pulse';
  if (action.status === 'Fizzled') kind = 'fizzle';
  const fizzled = action.status === 'Fizzled';
  const recipients = fizzled ? [] : [...new Set(effects.map(effect => effect.creature))];
  // The caster's d20 when it tried to crit (ADR 0154): its face, and whether it reached the threshold.
  // The face it needed comes with the card (ADR 0100: every chance is a twentieth, and only spells carry one).
  const needed = cards?.get(action.action?.spell)?.criticalThreshold;
  const roll = !fizzled && Number.isInteger(action.criticalRoll)
    ? { value: action.criticalRoll, critical: action.status === 'Critical', needed: Number.isInteger(needed) ? needed : null } : null;
  return { kind, visual: spellVisual(action, cards),
    recipients, contact: recipients.filter(id => id !== String(action.actor?.id)),
    struck: fizzled ? [] : struck, effects: fizzled ? [] : effects, changes, roll };
}

// One bounded queue per selected seat. Latest remains authoritative while historical frames are displayed.
// The first read is a baseline (also after hidden-tab return); a sequence is consumed when queued, not when
// its animation ends, so duplicate polls, skip and a cancelled callback can never repeat it.
export function createBattlePlayback({ onChange, schedule = setTimeout, cancel = clearTimeout, limit = 12, onUpkeepHandled = (_round) => {} }) {
  let latest = null, queue = [], current = null, stage = 'decision', summary = '';
  let mode = 'auto', presented = null, read = null, ordinaryRounds = new Set(), lastFramed = null;
  let seen = null, timer = null, generation = 0, reduced = false, rate = 1, catalogue = new Map(), harmful = new Set(), bought = new Map();
  // When each creature bought what it shows, from the reveals this seat's feed brought (ADR 0151).
  const purchases = purchaseLog();
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
    cancelTimer(); latest = null; queue = []; current = null; lastFramed = null; stage = 'decision'; seen = null; presented = null; read = null; ordinaryRounds.clear(); summary = ''; catalogue = new Map(); harmful = new Set(); bought = new Map(); purchases.reset(); emit();
  }
  /**
   * `since` is the last sequence this seat was shown before the page was closed or hidden: on the first read, what
   * came after it is played as it would have been live instead of being taken as the baseline, within the same
   * bounds as any catch-up. Without it, or when it is not behind the feed, the first read is the baseline.
   */
  function receive(view, cards, { animate = true, effects = /** @type {{ kind: string, harmful: boolean }[]} */ ([]), packages = new Map(), since = /** @type {number | null} */ (null) } = {}) {
    if (!view) { reset(); return; }
    latest = view; catalogue = cards; harmful = new Set(effects.filter(effect => effect.harmful).map(effect => effect.kind)); bought = packages;
    purchases.receive(view.feed);
    const entries = (view.feed ?? []).filter(entry => Number.isInteger(entry.sequence));
    const end = Math.max(-1, ...entries.map(entry => entry.sequence),
      Number.isInteger(view.feedNext) ? view.feedNext - 1 : -1);
    for (const entry of entries) if (entry.event?.kind === 'RoundEnergyGranted') ordinaryRounds.add(entry.event.roundId ?? entry.round);
    ordinaryRounds = new Set([...ordinaryRounds].sort((a, b) => b - a).slice(0, 4));
    if (seen === null) {
      if (!Number.isInteger(since) || since >= end || since < -1) { seen = end; emit(); return; }
      seen = since;
    }
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
    // behind it; anything else would play unseen, so it settles. An opponent's action is never passed over
    // unseen: arriving under a dialog (the turn order, a tip), it waits, queued and unplayed, for the field.
    // An opponent's action already waiting, unplayed, holds what comes after it just as one being read does.
    const held = (current !== null && waits(current)) || queue.some(item => incoming(item));
    const theirs = actions.some(action => incoming(action));
    if ((!animate && !held && !theirs) || actions.some(action => !framed(action)) || actions.length > limit || new Set(arriving.map(item => item.round)).size > 2 || arriving.length + queue.length > limit * 5) {
      // A settled action is still the round's latest: a later aftermath reads on its frame, or on none.
      const newest = actions.at(-1);
      if (newest) lastFramed = framed(newest) ? newest : null;
      settle(resultText(arriving.at(-1))); return;
    }
    // An upkeep keeps the board it arrived with, when that board is still its own round before combat: what
    // arrives later belongs to the planning and combat after it, never to the upkeep being read.
    const before = view.board?.roundNumber !== undefined && !COMBAT_BEGUN.includes(view.board.subPhase);
    for (const item of arriving) {
      if (item.receipt?.category === 'Upkeep') item.snapshot = before && view.board.roundNumber === item.round ? view : null;
      if (item.receipt?.category === 'Aftermath') item.snapshot = lastFramed?.round === item.round ? aftermathView(view, lastFramed) : null;
      if (!item.receipt && framed(item)) lastFramed = item;
      const previous = queue.at(-1) ?? current;
      const category = item.receipt?.category;
      if (['Upkeep', 'Aftermath'].includes(category) && previous?.receipt?.category === category && previous.round === item.round) {
        previous.upkeepEvents.push(...item.upkeepEvents); previous.aftermathEvents.push(...item.aftermathEvents);
        if (category === 'Upkeep') previous.upkeep = pacedUpkeep(previous.upkeepEvents, view.board, previous.round, ordinaryRounds);
        else previous.aftermath = pacedAftermath(previous.aftermathEvents, view.board);
        previous.receipt.sections.push(...item.receipt.sections); previous.sequence = item.sequence;
        // A split upkeep reads on the board that brought its last part, while that is still before combat.
        previous.snapshot = item.snapshot ?? previous.snapshot;
        if (previous === current) {
          presented = item.sequence; summary = resultText(previous);
          cancelTimer(); if (!waits(current)) later(next, 3500, false);
        }
      } else queue.push(item);
    }
    if (!current && animate) next(); else emit();
  }
  // The field is in view again: what waited under a dialog plays now.
  function resume() {
    if (!current && queue.length) next();
  }
  function model() {
    const action = current && !current.receipt ? current : null;
    const replay = action ? { actions: [current], index: 0, stage: stage === 'windup' ? 'before' : 'after', round: current.round } : null;
    // An action queued and not yet played (it arrived under the turn order, a tip or another dialog) has not
    // happened on screen: the field and the order strip show the round as it stood before it, never the engine's
    // latest board, which already counts it played and its damage dealt.
    // Only within the round on the board: an earlier round's action keeps the latest board, so a newer round's
    // order being read is never swapped for an older one.
    const queued = !current && framed(queue[0] ?? {}) && !queue[0].receipt && queue[0].round === latest?.board?.roundNumber ? queue[0] : null;
    const waiting = queued ? { actions: [queued], index: 0, stage: 'before', round: queued.round } : null;
    // A turnover reads on a recorded board under its plaques: an upkeep on the board it arrived with, an aftermath
    // on its round's last action frame. Any other receipt, or one without such a board, draws none.
    const own = current?.receipt ? current.snapshot : null;
    const step = current?.receipt?.category === 'Aftermath' ? { phase: 'EndOfRound', subPhase: 'Cleanup' } : { phase: 'StartOfRound', subPhase: 'OngoingEffects' };
    const display = own ? { ...own, board: { ...own.board, ...step, timeline: current.receipt.category === 'Aftermath' ? own.board.timeline : [] } }
      : current?.receipt ? { board: { roundNumber: current.round,
      phase: current.receipt.category === 'Result' ? 'Match complete' : current.receipt.category === 'Aftermath' ? 'EndOfRound' : 'StartOfRound',
      subPhase: current.receipt.category === 'Result' ? 'Finalization' : current.receipt.category === 'Aftermath' ? 'Cleanup' : 'OngoingEffects', allies: [], enemies: [], timeline: [] } }
      : action ? presentedView(latest, replay) : waiting ? presentedView(latest, waiting) : latest;
    // Every frame, live or recorded, shows the purchases it owns, ordered by when they were made (ADR 0151).
    const board = livePresentation(display, bought, { log: purchases });
    const cue = action ? actionCue(action, catalogue, harmful) : null;
    const frame = action ? { ...board.frame, stage, actor: String(current.actor.id),
      target: cue.contact[0] ?? null, targets: [], cue } : board.frame;
    // The last sequence the player has been shown in full: what a page closed now would resume after.
    const unread = [current?.first ?? current?.sequence, queue[0]?.first ?? queue[0]?.sequence].filter(Number.isInteger);
    const watermark = seen === null ? null : unread.length ? Math.min(...unread) - 1 : seen;
    return { board, frame, summary, active: Boolean(current) || queue.length > 0, sequence: current?.sequence ?? null, watermark,
      stage, rate, mode, presented, read, receipt: current?.receipt ?? null, upkeep: current?.upkeep ?? null,
      paced: current?.upkeep ?? current?.aftermath ?? null,
      canNext: Boolean(current && stage === 'resolved'),
      // Next is offered for every incoming action, whatever the pace, and for everything when the player steps.
      asks: Boolean(current && waits(current)), reading: readingCard(current, latest?.board, incoming(current)), incoming: incoming(current), label: current?.receipt?.title ?? (current ? `${current.spell} · ${current.status}` : ''), remaining: queue.length + (current ? 1 : 0), cue };
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
  // The seat's purchase log, for a private preview that adds a drafted purchase as the latest one (ADR 0151).
  return { receive, model, skip, reset, setReduced, setRate, setMode, advance, resume, purchases: () => purchases };
}
