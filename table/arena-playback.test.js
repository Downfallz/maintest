import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
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
test('a fallback while older actions are queued reports the newest result, not the queue\'s last', () => {
  const h = harness(); h.receive([]); h.receive([event(4), event(5)]);
  h.receive([event(6, { frame: undefined, resolution: { action: { actor: 2, spell: 'recovery', targets: [2] } }, appliedOutcomes: [{ kind: 'HealOutcome', target: 2, amount: 3 }] })]);
  assert.equal(h.queue.model().active, false);
  assert.match(h.queue.model().summary, /recovery.*Heal 3/);
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

// ART-4 (ADR 0131): how a cast is drawn comes with its card; the page names no spell and no effect.
const art = JSON.parse(readFileSync(new URL('../data/presentation/spell-art.json', import.meta.url), 'utf8'));
const drawn = new Map(Object.entries(art).map(([id, visual]) => [id, { name: 'Renamed or translated', visual }]));
const cast = (spell, effects = [], extra = {}) => ({ actor: { id: 1 }, action: { spell, targets: [2, 3] }, status: 'Resolved', effects, ...extra });
const outcome = (id, tone, extra = {}) => ({ target: { id }, tone, text: 'Confirmed result', ...extra });

test('every authored spell is drawn by the spell art the builder publishes', () => {
  const root = new URL('../data/Spells/', import.meta.url);
  const spells = readdirSync(root, { recursive: true }).filter(path => path.endsWith('.json'))
    .map(path => JSON.parse(readFileSync(new URL(path, root), 'utf8')).id);
  assert.deepEqual(Object.keys(art).sort(), spells.sort());
});
test('the card decides how a cast is drawn, whatever its label says', () => {
  for (const [id, family, motif] of [['tornado', 'physical', 'wave'], ['soul_devourer', 'biological', 'bite'], ['summon_minions', 'necromantic', 'spirits'], ['toxic_waves', 'astral', 'void']]) {
    const visual = actionCue(cast(`spell:${id}:v1`), drawn).visual;
    assert.equal(visual.family, family); assert.equal(visual.motif, motif);
  }
});
test('a card nobody drew, an unknown spell and an older host without art all get the neutral mark', () => {
  const older = new Map([['spell:tornado:v1', { name: 'Tornado' }]]);
  for (const [spell, cards] of [['spell:tornado:v1', older], ['spell:lightning_bolt:v2', drawn], [undefined, drawn], ['toString', drawn], ['spell:tornado:v1', undefined]]) {
    assert.deepEqual(actionCue(cast(spell), cards).visual, { family: 'neutral', motif: 'focus', delivery: 'cast' });
  }
});
test('recipients come only from applied outcomes, deduplicated, and the caster is no contact target', () => {
  const cue = actionCue(cast('spell:summon_minions:v1', [outcome(1, 'harm'), outcome(2, 'condition'), outcome(2, 'harm')]), drawn);
  assert.deepEqual(cue.recipients, ['1', '2']); assert.deepEqual(cue.contact, ['2']); assert.equal(cue.visual.delivery, 'cast');
});
test('a fizzle draws only its broken cast and touches nobody, whatever a malformed record lists', () => {
  const cue = actionCue(cast('spell:throwing_star:v1', [outcome(2, 'harm')], { status: 'Fizzled' }), drawn);
  assert.equal(cue.visual.motif, 'fizzle'); assert.deepEqual(cue.effects, []); assert.deepEqual(cue.recipients, []); assert.deepEqual(cue.struck, []);
});
test('empty outcomes fabricate no recipient from the declaration', () => {
  const cue = actionCue(cast('spell:tornado:v1'), drawn); assert.deepEqual(cue.recipients, []); assert.deepEqual(cue.contact, []);
});
test('damage the caster took is a wound, and so is a harmful lasting effect it put on itself', () => {
  const harmful = new Set(['Bleed']);
  const bleed = outcome(1, 'condition', { kind: 'ConditionOutcome', effect: { kind: 'Bleed' }, onCaster: true });
  const ward = outcome(1, 'condition', { kind: 'ConditionOutcome', effect: { kind: 'DefenseBuff' }, onCaster: false });
  const cue = actionCue(cast('spell:bone_ward:v1', [outcome(1, 'harm'), bleed, ward, outcome(2, 'condition')]), drawn, harmful);
  assert.deepEqual(cue.effects.map(effect => effect.wound), [true, true, false, false]);
  assert.deepEqual(cue.contact, ['2']); assert.ok(!cue.struck.includes('2'));
});
test('without the catalogue naming the harmful effects, only damage on the caster is a wound', () => {
  const bleed = outcome(1, 'condition', { kind: 'ConditionOutcome', effect: { kind: 'Bleed' }, onCaster: true });
  assert.deepEqual(actionCue(cast('spell:bone_ward:v1', [bleed]), drawn).effects.map(effect => effect.wound), [false]);
});
test('the live queue carries the card\'s visual and the harmful effects it was given through to the cue', () => {
  const start = [1, 2, 3].map(id => ({ id, health: 20, maxHealth: 20, energy: 5, isAlive: true }));
  const seat = { board: { allies: start.slice(0, 1), enemies: start.slice(1), roundNumber: 2 }, feed: [] };
  const queue = createArenaPlayback({ onChange() {}, schedule() {} });
  const options = { effects: [{ kind: 'Bleed', glyph: '', harmful: true }, { kind: 'DefenseBuff', glyph: '', harmful: false }] };
  queue.receive(seat, drawn, options);
  queue.receive({ ...seat, feed: [{ sequence: 1, event: { kind: 'CombatActionResolved', roundId: 2,
    resolution: { action: { actor: 1, spell: 'spell:bone_ward:v1', targets: [2, 3] } },
    appliedOutcomes: [{ kind: 'ConditionOutcome', target: 1, effect: { kind: 'Bleed', amountPerRound: 2 }, onCaster: true }, { kind: 'ConditionOutcome', target: 2, effect: { kind: 'DefenseBuff', amount: 2 } }],
    frame: { before: start, after: start } } }] }, drawn, options);
  assert.match(queue.model().label, /Renamed or translated/);
  assert.equal(queue.model().cue.visual.motif, 'bones'); assert.equal(queue.model().frame.target, '2');
  assert.deepEqual(queue.model().cue.effects.map(effect => effect.wound), [true, false]);
});

for (const rate of [1, 1.5, 2]) test(`at ${rate}x every full-motion phase is scaled by the speed`, () => {
  const h = harness(); h.queue.setRate(rate); h.receive([]); h.receive([event(10)]);
  assert.equal([...h.timers.values()][0].ms, 420 / rate);
  h.tick(); assert.equal([...h.timers.values()][0].ms, 850 / rate);
  h.tick(); assert.equal([...h.timers.values()][0].ms, 250 / rate);
});
for (const rate of [1, 1.5, 2]) test(`at ${rate}x a reduced-motion result is still shown for 650 ms`, () => {
  const h = harness(); h.queue.setRate(rate); h.queue.setReduced(true); h.receive([]); h.receive([event(10)]);
  assert.equal(h.queue.model().stage, 'impact'); assert.equal([...h.timers.values()][0].ms, 650);
});
test('a speed change settles the whole queue', () => {
  const h = harness(); h.receive([]); h.receive([event(10), event(11)]);
  h.queue.setRate(2); assert.equal(h.queue.model().active, false); assert.equal(h.queue.model().remaining, 0);
});
test('a callback scheduled before a speed change does nothing after it', () => {
  const h = harness(); h.receive([]); h.receive([event(10)]);
  const stale = [...h.timers.values()][0].fn; h.queue.setRate(2); stale();
  assert.equal(h.queue.model().active, false); assert.equal(h.queue.model().stage, 'decision');
});
test('actions settled by a speed change are not replayed by the next poll', () => {
  const h = harness(); h.receive([]); h.receive([event(10), event(11)]); h.queue.setRate(2);
  h.receive([event(10), event(11)]); assert.equal(h.queue.model().active, false);
  h.receive([event(12)]); assert.equal([...h.timers.values()][0].ms, 210);
});
test('choosing the speed already playing leaves the action playing', () => {
  const h = harness(); h.queue.setRate(2); h.receive([]); h.receive([event(10)]);
  h.queue.setRate(2); assert.equal(h.queue.model().active, true);
});
test('a speed outside 1x, 1.5x and 2x plays at 1x', () => {
  const h = harness(); h.queue.setRate(2); h.queue.setRate(Infinity); assert.equal(h.queue.model().rate, 1);
});
