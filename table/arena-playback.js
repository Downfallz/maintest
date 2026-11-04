import { resolvedSince, lastResolvedText } from './feed.js';
import { presentedView } from './presentation.js';
import { combatSnapshot, combatChanges } from './combat-fx.js';
import { livePresentation } from './arena-live.js';

const resultText = action => [lastResolvedText(action), ...(action.dropped ?? []).map(text => `Dropped: ${text}`)].join(' · ');

const framed = action => Array.isArray(action.frame?.before) && Array.isArray(action.frame?.after)
  && action.frame.before.length > 0 && action.frame.before.length === action.frame.after.length
  && action.frame.before.every(one => action.frame.after.some(next => next.id === one.id));

// A motif describes confirmed outcomes, never a spell's predicted damage or target legality.
export function actionCue(action) {
  const effects = action.effects.map(effect => ({ creature: String(effect.target.id), text: effect.text, tone: effect.tone }));
  const struck = [...new Set(action.effects.filter(effect => effect.tone === 'harm').map(effect => String(effect.target.id)))];
  const changes = framed(action) ? combatChanges(
    combatSnapshot({ allies: action.frame.before }), combatSnapshot({ allies: action.frame.after })) : [];
  let kind = struck.length ? 'strike' : 'pulse';
  if (action.status === 'Fizzled') kind = 'fizzle';
  return { kind,
    struck: action.status === 'Fizzled' ? [] : struck, effects, changes };
}

// One bounded queue per selected seat. Latest remains authoritative while historical frames are displayed.
// The first read is a baseline (also after hidden-tab return); a sequence is consumed when queued, not when
// its animation ends, so duplicate polls, skip and a cancelled callback can never repeat it.
export function createArenaPlayback({ onChange, schedule = setTimeout, cancel = clearTimeout, limit = 12 }) {
  let latest = null, queue = [], current = null, stage = 'decision', summary = '';
  let seen = null, timer = null, generation = 0, reduced = false;
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
    if (reduced) later(finish, 650);
    else later(() => { stage = 'impact'; emit(); later(finish, 850); }, 420);
  }
  function finish() {
    stage = 'resolved'; emit(); later(next, 250);
  }
  function skip() {
    if (queue.length) summary = resultText(queue.at(-1));
    cancelTimer(); queue = []; current = null; stage = 'decision'; emit();
  }
  function reset() {
    cancelTimer(); latest = null; queue = []; current = null; stage = 'decision'; seen = null; summary = ''; emit();
  }
  function receive(view, cards, { animate = true } = {}) {
    if (!view) { reset(); return; }
    latest = view;
    const entries = (view.feed ?? []).filter(entry => Number.isInteger(entry.sequence));
    const end = Math.max(-1, ...entries.map(entry => entry.sequence),
      Number.isInteger(view.feedNext) ? view.feedNext - 1 : -1);
    if (seen === null) { seen = end; emit(); return; }
    const unique = [...new Map(entries.filter(entry => entry.sequence > seen).map(entry => [entry.sequence, entry])).values()];
    seen = Math.max(seen, end);
    const arriving = resolvedSince(unique, undefined, view.board, cards);
    if (!arriving.length) { emit(); return; }
    // Legacy events have text but no trustworthy intermediate board. A long catch-up must not keep a
    // decision behind old pictures either. Both settle on the latest snapshot with the last real result.
    if (!animate || arriving.some(action => !framed(action)) || queue.length + arriving.length + (current ? 1 : 0) > limit) {
      queue = []; summary = resultText(arriving.at(-1)); skip(); return;
    }
    queue.push(...arriving);
    if (!current) next(); else emit();
  }
  function model() {
    const replay = current ? { actions: [current], index: 0, stage: stage === 'windup' ? 'before' : 'after', round: current.round } : null;
    const display = current ? presentedView(latest, replay) : latest;
    const board = livePresentation(display);
    const cue = current ? actionCue(current) : null;
    const frame = current ? { ...board.frame, stage, actor: String(current.actor.id),
      target: cue.struck[0] ?? null, targets: [], cue } : board.frame;
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
