import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createArenaPlayback, actionCue } from './arena-playback.js';
const creature = (id, health = 20) => ({ id, health, maxHealth: 20, energy: 5, totalDefense: 2, isAlive: health > 0, name: `Creature ${id}` });
const before = [creature(1), creature(2), creature(3)];
const timeline = [1, 2, 3].map(creature => ({ creature, owner: creature === 1 ? 'Player1' : 'Player2', speed: 'Standard' }));
function event(sequence, extras = {}) {
  return { sequence, event: { kind: 'CombatActionResolved', roundId: 2,
    resolution: { action: { actor: 1, spell: 'hit', targets: [2] }, isCritical: true },
    appliedOutcomes: [{ kind: 'DamageOutcome', target: 2, amount: 7, critical: true }],
    frame: { before, after: [creature(1), creature(2, 13), creature(3)], timeline }, ...extras } };
}
const view = (feed = [], extra = {}) => ({ board: { allies: [creature(1)], enemies: [creature(2, 13), creature(3)],
  slot: 'Player1', roundNumber: 2, subPhase: 'Activation', phase: 'Combat', timeline, activationCursor: 1 }, feed, ...extra });
const cards = new Map([['hit', { name: 'Strike' }]]);
function harness() {
  const timers = new Map(); let index = 0;
  const queue = createArenaPlayback({ onChange: () => {}, schedule: (fn, ms) => { timers.set(++index, { fn, ms }); return index; }, cancel: id => timers.delete(id) });
  return { queue, timers, receive: (events, extra, options) => queue.receive(view(events, extra), cards, options),
    tick() { const [id, timer] = timers.entries().next().value; timers.delete(id); timer.fn(); },
    drain() { while (timers.size) this.tick(); } };
}
test('first snapshot is a baseline, including historical results', () => {
  const h = harness(); h.receive([event(2)], { feedNext: 9 }); assert.equal(h.queue.model().active, false);
  h.receive([event(2), event(8)]); assert.equal(h.queue.model().active, false);
  h.receive([event(9)]); assert.equal(h.queue.model().sequence, 9);
});
test('a poll\'s actions play in sequence order, once each, however the host listed them', () => {
  const h = harness(); h.receive([]); h.receive([event(6), event(4), event(4)]);
  assert.equal(h.queue.model().sequence, 4); assert.equal(h.queue.model().remaining, 2);
  h.drain(); assert.equal(h.queue.model().active, false);
});
test('windup shows the frame before the action and impact the frame after it', () => {
  const h = harness(); h.receive([]); h.receive([event(4)]);
  assert.equal(h.queue.model().stage, 'windup'); assert.equal(h.queue.model().board.creatures[1].health, 20);
  h.tick(); assert.equal(h.queue.model().stage, 'impact'); assert.equal(h.queue.model().board.creatures[1].health, 13);
});
test('a later poll repeating consumed sequences queues nothing again', () => {
  const h = harness(); h.receive([]); h.receive([event(4), event(6)]); h.tick();
  h.receive([event(4), event(6)]); assert.equal(h.queue.model().remaining, 2);
  h.drain(); assert.equal(h.queue.model().active, false);
});
test('the summary after the queue drains is the last action\'s full result', () => {
  const h = harness(); h.receive([]); h.receive([event(4), event(6)]); h.drain();
  assert.match(h.queue.model().summary, /Strike.*critical.*Damage 7/);
});
test('arrivals during playback join behind the current action without restarting its windup', () => {
  const h = harness(); h.receive([]); h.receive([event(4)]); h.tick(); h.receive([event(5)]);
  assert.equal(h.queue.model().stage, 'impact'); assert.equal(h.queue.model().remaining, 2); h.drain();
});
test('last action retains its round and death before the host moves to upkeep', () => {
  const h = harness(); h.receive([]);
  const latest = view([], { over: true }); latest.board.roundNumber = 3; latest.board.subPhase = 'Evolution'; latest.board.enemies[0] = creature(2, 0);
  latest.feed = [event(4, { frame: { before, after: [creature(1), creature(2, 0), creature(3)], timeline } })];
  h.queue.receive(latest, cards); assert.equal(h.queue.model().board.round, 2); h.tick();
  assert.equal(h.queue.model().board.creatures[1].isAlive, false);
  assert.equal(h.queue.model().cue.changes.find(one => one.creature === 2).defeated, true);
  h.drain(); assert.equal(h.queue.model().board.round, 3);
});
test('skip invalidates callbacks and consumes pending identities without replaying them', () => {
  const h = harness(); h.receive([]); h.receive([event(4), event(5)]);
  const stale = [...h.timers.values()][0].fn; h.queue.skip(); stale();
  assert.equal(h.queue.model().stage, 'decision');
  h.receive([event(4), event(5)]); assert.equal(h.queue.model().active, false); assert.equal(h.timers.size, 0);
  h.receive([event(6)]); assert.equal(h.queue.model().sequence, 6);
});
test('reset clears private summaries, frames and callbacks and resumes with a new baseline', () => {
  const h = harness(); h.receive([]); h.receive([event(4)]); const stale = [...h.timers.values()][0].fn;
  h.queue.reset(); stale(); assert.equal(h.queue.model().summary, ''); assert.equal(h.queue.model().board.creatures.length, 0);
  assert.equal(h.queue.model().stage, 'decision');
  h.receive([event(4), event(5)]); assert.equal(h.queue.model().active, false);
});
test('legacy events keep the confirmed text on the latest board with no inferred attack', () => {
  const h = harness(); h.receive([]); h.receive([event(4, { frame: undefined })]);
  assert.equal(h.queue.model().active, false); assert.match(h.queue.model().summary, /Strike/);
  assert.equal(h.queue.model().board.creatures[1].health, 13); assert.equal(h.timers.size, 0);
});
test('a read bringing twelve actions plays them all', () => {
  const h = harness(); h.receive([]); h.receive(Array.from({ length: 12 }, (_, i) => event(i)));
  assert.equal(h.queue.model().active, true); assert.equal(h.queue.model().remaining, 12);
});
test('a read bringing more than twelve settles at once instead of holding the next question behind stale frames', () => {
  const h = harness(); h.receive([]); h.receive(Array.from({ length: 13 }, (_, i) => event(i)));
  assert.equal(h.queue.model().active, false); assert.equal(h.timers.size, 0);
});
test('what is already queued and playing is not dropped for arrivals at the game\'s pace', () => {
  const h = harness(); h.receive([]); h.receive(Array.from({ length: 12 }, (_, i) => event(i)));
  h.receive([event(12)]);
  assert.equal(h.queue.model().sequence, 0); assert.equal(h.queue.model().remaining, 13);
});
test('a read mixing a legacy event with framed ones settles them all on the latest board', () => {
  const h = harness(); h.receive([]); h.receive([event(4), event(5, { frame: undefined }), event(6)]);
  assert.equal(h.queue.model().active, false); assert.equal(h.timers.size, 0);
  assert.match(h.queue.model().summary, /Strike/);
});
test('an arrival under an open draft settles the action in flight too', () => {
  const h = harness(); h.receive([]); h.receive([event(4)]); h.tick();
  h.receive([event(5)], {}, { animate: false });
  assert.equal(h.queue.model().active, false); assert.equal(h.queue.model().stage, 'decision'); assert.equal(h.timers.size, 0);
});
test('a current legal draft receives results immediately without historical playback', () => {
  const h = harness(); h.receive([]); h.receive([event(4)], {}, { animate: false });
  assert.equal(h.queue.model().active, false); assert.match(h.queue.model().summary, /Damage/);
});
test('reduced motion begins at the applied frame and changing the preference cancels in-flight motion', () => {
  const h = harness(); h.queue.setReduced(true); h.receive([]); h.receive([event(4)]);
  assert.equal(h.queue.model().stage, 'impact'); assert.equal(h.queue.model().board.creatures[1].health, 13);
  h.drain(); h.queue.setReduced(false); h.receive([event(5)]); assert.equal(h.queue.model().stage, 'windup');
  h.queue.setReduced(true); assert.equal(h.queue.model().active, false); assert.equal(h.timers.size, 0);
});
test('damage motifs include every applied target and actual boundary changes', () => {
  const h = harness(); h.receive([]); h.receive([event(4, { appliedOutcomes: [
    { kind: 'DamageOutcome', target: 2, amount: 7 }, { kind: 'DamageOutcome', target: 3, amount: 0 },
    { kind: 'ConditionOutcome', target: 2, effect: { kind: 'Bleed', amountPerRound: 2 } }] })]);
  assert.deepEqual(h.queue.model().cue.struck, ['2', '3']); assert.equal(h.queue.model().cue.effects.length, 3);
  assert.deepEqual(h.queue.model().cue.changes, [{ creature: 2, health: -7 }]);
});
test('recovery, energy and conditions pulse without pretending to strike', () => {
  const cue = actionCue({ status: 'Resolved', effects: [{ target: { id: 1 }, tone: 'recovery', text: 'Heal 3' },
    { target: { id: 1 }, tone: 'energy', text: 'Energy 2' }, { target: { id: 2 }, tone: 'condition', text: 'Stun' }] });
  assert.equal(cue.kind, 'pulse'); assert.deepEqual(cue.struck, []); assert.equal(cue.effects.length, 3);
});
test('fizzle text and dropped targets are preserved and never produce a strike', () => {
  const h = harness(); h.receive([]); h.receive([event(4, { resolution: { action: { actor: 1, spell: 'hit', targets: [2] },
    fizzled: true, fizzleReason: { message: 'Not enough energy' } }, appliedOutcomes: [] })]);
  assert.equal(h.queue.model().cue.kind, 'fizzle'); assert.deepEqual(h.queue.model().cue.struck, []);
  assert.match(h.queue.model().summary, /fizzled: Not enough energy/);
});

test('skip reports the last queued result rather than the first action it interrupted', () => {
  const h = harness(); h.receive([]);
  h.receive([event(4), event(5, { resolution: { action: { actor: 2, spell: 'recovery', targets: [2] } }, appliedOutcomes: [{ kind: 'HealOutcome', target: 2, amount: 3 }] })]);
  h.queue.skip(); assert.match(h.queue.model().summary, /recovery.*Heal 3/);
});
test('dropped target explanations remain available with the full result', () => {
  const h = harness(); h.receive([]); h.receive([event(4, { resolution: { action: { actor: 1, spell: 'hit', targets: [2, 3] },
    droppedTargets: [{ target: 3, error: { message: 'Target is dead' } }] } })]);
  assert.match(h.queue.model().summary, /Dropped: Creature 3: Target is dead/);
});
