import { resolvedSince, lastResolvedText } from './feed.js';
import { presentedView } from './presentation.js';
import { combatSnapshot, combatChanges } from './combat-fx.js';
import { livePresentation } from './arena-live.js';
import { IMPACT_MS, REDUCED_MS, SETTLE_MS, WINDUP_MS } from './arena-timing.js';

const resultText = action => [lastResolvedText(action), ...(action.dropped ?? []).map(text => `Dropped: ${text}`)].join(' · ');

const framed = action => Array.isArray(action.frame?.before) && Array.isArray(action.frame?.after)
  && action.frame.before.length > 0 && action.frame.before.length === action.frame.after.length
  && action.frame.before.every(one => action.frame.after.some(next => next.id === one.id));

const NEUTRAL = Object.freeze({ family: 'neutral', motif: 'focus', delivery: 'cast' });
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
export function createArenaPlayback({ onChange, schedule = setTimeout, cancel = clearTimeout, limit = 12 }) {
  let latest = null, queue = [], current = null, stage = 'decision', summary = '';
  let seen = null, timer = null, generation = 0, reduced = false, catalogue = new Map(), harmful = new Set(), bought = new Map();
  const emit = () => onChange();
  function cancelTimer() { generation++; cancel(timer); timer = null; }
  function later(callback, delay) {
    const ticket = generation;
    timer = schedule(() => { if (ticket === generation) { timer = null; callback(); } }, delay);
  }
  function next() {
    current = queue.shift() ?? null;
    if (!current) { stage = 'decision'; emit(); return; }
    summary = resultText(current);
    stage = reduced ? 'impact' : 'windup'; emit();
    if (reduced) later(finish, REDUCED_MS);
    else later(() => { stage = 'impact'; emit(); later(finish, IMPACT_MS); }, WINDUP_MS);
  }
  function finish() {
    stage = 'resolved'; emit(); later(next, SETTLE_MS);
  }
  // Settling shows the latest board with one result to read, whichever the caller names.
  function settle(text) {
    cancelTimer(); queue = []; current = null; stage = 'decision'; summary = text; emit();
  }
  // What the player reads after a skip is the last result they would have reached, not the one cut short.
  function skip() {
    settle(queue.length ? resultText(queue.at(-1)) : summary);
  }
  function reset() {
    cancelTimer(); latest = null; queue = []; current = null; stage = 'decision'; seen = null; summary = ''; catalogue = new Map(); harmful = new Set(); bought = new Map(); emit();
  }
  function receive(view, cards, { animate = true, effects = /** @type {{ kind: string, harmful: boolean }[]} */ ([]), packages = new Map() } = {}) {
    if (!view) { reset(); return; }
    latest = view; catalogue = cards; harmful = new Set(effects.filter(effect => effect.harmful).map(effect => effect.kind)); bought = packages;
    const entries = (view.feed ?? []).filter(entry => Number.isInteger(entry.sequence));
    const end = Math.max(-1, ...entries.map(entry => entry.sequence),
      Number.isInteger(view.feedNext) ? view.feedNext - 1 : -1);
    if (seen === null) { seen = end; emit(); return; }
    const unique = [...new Map(entries.filter(entry => entry.sequence > seen).map(entry => [entry.sequence, entry])).values()];
    seen = Math.max(seen, end);
    const arriving = resolvedSince(unique, undefined, view.board, cards);
    if (!arriving.length) { emit(); return; }
    // Legacy events have text but no trustworthy intermediate board. A long catch-up (one read bringing more
    // than the limit, a reconnection's worth) must not keep a decision behind old pictures either. Both settle
    // on the latest snapshot with the last real result. What is already queued or playing is never dropped
    // for what arrives at the game's own pace: a round brings a handful of actions, not a backlog.
    if (!animate || arriving.some(action => !framed(action)) || arriving.length > limit) {
      settle(resultText(arriving.at(-1))); return;
    }
    queue.push(...arriving);
    if (!current) next(); else emit();
  }
  function model() {
    const replay = current ? { actions: [current], index: 0, stage: stage === 'windup' ? 'before' : 'after', round: current.round } : null;
    const display = current ? presentedView(latest, replay) : latest;
    const board = livePresentation(display, bought);
    const cue = current ? actionCue(current, catalogue, harmful) : null;
    const frame = current ? { ...board.frame, stage, actor: String(current.actor.id),
      target: cue.contact[0] ?? null, targets: [], cue } : board.frame;
    return { board, frame, summary, active: Boolean(current), sequence: current?.sequence ?? null,
      stage, label: current ? `${current.spell} · ${current.status}` : '', remaining: queue.length + (current ? 1 : 0), cue };
  }
  function setReduced(value) {
    if (value === reduced) return;
    reduced = value;
    // Changing a motion preference mid-tween settles immediately and invalidates its timers.
    if (current) skip();
  }
  return { receive, model, skip, reset, setReduced };
}
