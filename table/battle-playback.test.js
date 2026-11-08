import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { SETTLE_MS as SETTLE } from './battle-timing.js';
import { createBattlePlayback, actionCue } from './battle-playback.js';
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
  const timers = new Map(), handled = []; let index = 0;
  const queue = createBattlePlayback({ onChange: () => {}, schedule: (fn, ms) => { timers.set(++index, { fn, ms }); return index; }, cancel: id => timers.delete(id),
    onUpkeepHandled: round => handled.push(round) });
  return { queue, timers, handled, receive: (events, extra, options) => queue.receive(view(events, extra), cards, options),
    tick() { const [id, timer] = timers.entries().next().value; timers.delete(id); timer.fn(); },
    // Drains the timers and, in Auto, gives the OK each action waits for, as the player would.
    drain() {
      for (;;) {
        while (timers.size) this.tick();
        const m = queue.model();
        if (m.active && m.stage === 'resolved' && m.mode === 'auto' && !m.receipt) queue.advance(); else break;
      }
    } };
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
  const queue = createBattlePlayback({ onChange() {}, schedule() {} });
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

for (const rate of [0.5, 1, 1.5, 2]) test(`at ${rate}x every full-motion phase is scaled by the speed`, () => {
  const h = harness(); h.queue.setRate(rate); h.receive([]); h.receive([event(10)]);
  assert.equal([...h.timers.values()][0].ms, 840 / rate);
  h.tick(); assert.equal([...h.timers.values()][0].ms, 1700 / rate);
  h.tick(); assert.equal([...h.timers.values()][0].ms, SETTLE / rate, 'the own cast then settles at the speed');
});
for (const rate of [0.5, 1, 1.5, 2]) test(`at ${rate}x a reduced-motion result is still shown for 650 ms`, () => {
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
  h.receive([event(12)]); assert.equal([...h.timers.values()][0].ms, 420);
});
test('choosing the speed already playing leaves the action playing', () => {
  const h = harness(); h.queue.setRate(2); h.receive([]); h.receive([event(10)]);
  h.queue.setRate(2); assert.equal(h.queue.model().active, true);
});
test('a speed outside 0.5x, 1x, 1.5x and 2x plays at 1x', () => {
  const h = harness(); h.queue.setRate(2); h.queue.setRate(Infinity); assert.equal(h.queue.model().rate, 1);
});

const upkeepEvent = (sequence, kind = 'OngoingEffectsApplied', fields = {}) => ({ sequence, event: { kind, roundId: 3, ...fields } });
const energyEvent = sequence => upkeepEvent(sequence, 'RoundEnergyGranted', { grants: [
  { creature: 1, gained: 2, before: 1, after: 3, baseRequested: 2, passiveRequested: 0 }] });
const cleanupEvent = sequence => ({ sequence, event: { kind: 'ConditionsExpired', roundId: 2,
  expired: { 1: [{ effect: { kind: 'DefenseBuff', amount: 2 } }] } } });

// An action the other team's creature took: the board's allies are creature 1 only.
const incoming = (sequence, extras = {}) => event(sequence, { ...extras, resolution: { action: { actor: 2, spell: 'hit', targets: [1] }, isCritical: true },
  appliedOutcomes: [{ kind: 'DamageOutcome', target: 1, amount: 7, critical: true }] });
test('an incoming action holds its exact applied frame until an explicit Next, with received, presented and read positions separate', () => {
  const h = harness(); h.queue.setMode('step'); h.receive([]); h.receive([incoming(4), incoming(5)]);
  h.queue.advance(); assert.equal(h.queue.model().sequence, 4); // not during a windup
  h.drain(); assert.equal(h.queue.model().stage, 'resolved'); assert.equal(h.queue.model().sequence, 4);
  assert.equal(h.queue.model().read, null); assert.equal(h.queue.model().presented, 4);
  h.queue.advance(); assert.equal(h.queue.model().sequence, 5); assert.equal(h.queue.model().read, 4);
  h.drain(); h.queue.advance(); assert.equal(h.queue.model().active, false); assert.equal(h.queue.model().read, 5);
});
test('the player\'s own cast moves on by itself, in Step as in Auto', () => {
  const h = harness(); h.receive([]); h.receive([event(4), event(5)]); h.tick(); h.tick();
  assert.equal(h.queue.model().asks, false); assert.equal([...h.timers.values()][0].ms, SETTLE);
  assert.equal(h.queue.model().incoming, false);
  h.tick(); assert.equal(h.queue.model().sequence, 5);
  const step = harness(); step.queue.setMode('step'); step.receive([]); step.receive([event(4), event(5)]); step.tick(); step.tick();
  assert.equal(step.queue.model().asks, false); assert.equal([...step.timers.values()][0].ms, SETTLE);
  step.tick(); assert.equal(step.queue.model().sequence, 5);
});
test('in Auto as in Step an incoming action holds for the OK; switching pace never moves past it', () => {
  const h = harness(); h.receive([]); h.receive([incoming(4), incoming(5)]); h.tick(); h.tick();
  assert.equal(h.queue.model().stage, 'resolved'); assert.equal(h.timers.size, 0);
  assert.equal(h.queue.model().asks, true); assert.equal(h.queue.model().canNext, true);
  assert.equal(h.queue.model().incoming, true, 'an incoming action is read, never skipped');
  h.queue.setMode('step'); h.queue.setMode('auto'); assert.equal(h.queue.model().sequence, 4); assert.equal(h.timers.size, 0);
  h.queue.advance(); assert.equal(h.queue.model().sequence, 5); assert.equal(h.queue.model().stage, 'windup');
  h.tick(); h.tick(); h.queue.advance(); assert.equal(h.queue.model().active, false, 'the OK on the last action returns to the present');
});
test('in Auto the turnover after an incoming last action waits for its OK, then reads by itself', () => {
  const h = harness(); h.receive([]); h.receive([incoming(4), cleanupEvent(5), energyEvent(6), upkeepEvent(7)]);
  h.tick(); h.tick(); assert.equal(h.queue.model().sequence, 4); assert.equal(h.timers.size, 0);
  h.queue.advance(); assert.equal(h.queue.model().receipt.category, 'Aftermath'); assert.equal(h.queue.model().asks, false);
  assert.equal([...h.timers.values()][0].ms, 3500); h.tick(); assert.equal(h.queue.model().receipt.category, 'Upkeep');
});
test('the own closing cast, meaningful aftermath and combined next upkeep are read in order', () => {
  const h = harness(); h.queue.setMode('step'); h.receive([]);
  const latest = view().board; latest.roundNumber = 3; latest.phase = 'Planning'; latest.subPhase = 'Evolution';
  latest.allies[0].knownSpells = ['future-spell']; latest.allies[0].energy = 99;
  h.receive([event(4), cleanupEvent(5), energyEvent(6), upkeepEvent(7)], { board: latest });
  assert.equal(h.queue.model().board.round, 2); assert.equal(h.queue.model().board.creatures[0].energy, 5);
  assert.equal(h.queue.model().board.creatures[0].knownSpells, undefined);
  h.drain();
  assert.equal(h.queue.model().receipt.category, 'Aftermath', 'the own cast moved on by itself; Step holds the aftermath'); assert.equal(h.queue.model().board.round, 2);
  assert.equal(h.queue.model().board.phase, 'EndOfRound');
  // On the round's last action frame, nothing of the next round's board: creature 2 as the strike left it.
  assert.equal(h.queue.model().board.creatures.find(one => one.id === '2').health, 13);
  assert.equal(h.queue.model().board.creatures[0].energy, 5); assert.equal(h.queue.model().board.creatures[0].knownSpells, undefined);
  assert.deepEqual(h.queue.model().paced.markers.map(one => [one.id, one.lines.map(line => line.text)]), [['1', ['Defense Buff ends']]]);
  h.queue.advance(); assert.equal(h.queue.model().receipt.category, 'Upkeep'); assert.equal(h.queue.model().board.round, 3);
  assert.match(h.queue.model().summary, /\+2 energy.*1 → 3/); assert.match(h.queue.model().summary, /No ongoing ticks/);
  assert.doesNotMatch(h.queue.model().summary, /unknown|not recorded/); assert.equal(h.queue.model().receipt.sections.length, 2);
  h.queue.advance(); assert.equal(h.queue.model().board.creatures[0].energy, 99); assert.equal(h.queue.model().board.subPhase, 'Evolution');
});
test('events arriving ahead of their board stay queued when the empty next-round snapshot arrives', () => {
  const h = harness(); h.queue.setMode('step'); h.receive([]); h.receive([event(4), energyEvent(5), upkeepEvent(6)]);
  h.receive([], { board: { ...view().board, roundNumber: 3 } });
  assert.equal(h.queue.model().remaining, 2); h.drain(); h.queue.advance();
  assert.match(h.queue.model().summary, /\+2 energy/); assert.equal(h.queue.model().board.round, 3);
});
test('split upkeep responses append facts to the same held receipt without duplicating their sequences', () => {
  const h = harness(); h.queue.setMode('step'); h.receive([]); h.receive([energyEvent(4)]);
  h.receive([upkeepEvent(5, 'OngoingEffectsApplied', { bleedTicks: [{ creature: 2, damage: 3 }] })]);
  assert.equal(h.queue.model().remaining, 1); assert.equal(h.queue.model().sequence, 5);
  assert.match(h.queue.model().summary, /Ongoing damage −3 HP/); h.receive([energyEvent(4), upkeepEvent(5)]);
  assert.equal(h.queue.model().receipt.sections.length, 2);
});
test('legacy upkeep says the ordinary energy is unknown, not zero', () => {
  const h = harness(); h.receive([]); h.receive([upkeepEvent(4)]);
  assert.match(h.queue.model().summary, /Ordinary round energy was not recorded/);
  assert.match(h.queue.model().summary, /No ongoing ticks/); assert.deepEqual(h.queue.model().board.creatures, []);
});
test('lethal upkeep is read, and the match\'s end adds no step of its own: the result opens after it', () => {
  const h = harness(); h.queue.setMode('step'); h.receive([]);
  h.receive([energyEvent(4), upkeepEvent(5, 'OngoingEffectsApplied', { bleedTicks: [{ creature: 2, damage: 13 }] }),
    upkeepEvent(6, 'MatchEnded', { outcome: { winner: 'Player1', reason: 'Elimination' } })], { over: true });
  assert.equal(h.queue.model().receipt.category, 'Upkeep'); assert.match(h.queue.model().summary, /−13 HP/);
  assert.equal(h.queue.model().cue, null); assert.equal(h.queue.model().remaining, 1);
  h.queue.advance(); assert.equal(h.queue.model().active, false);
});
test('empty cleanup has no pace step; immunity alone does', () => {
  const h = harness(); h.receive([]); h.receive([{ sequence: 4, event: { kind: 'ConditionsExpired', roundId: 2, expired: {} } }]);
  assert.equal(h.queue.model().active, false);
  h.receive([{ sequence: 5, event: { kind: 'StunImmunityGained', roundId: 2, creatures: [2] } }]);
  assert.match(h.queue.model().summary, /immune to stun/);
});
test('the player\'s own skip handles queued upkeep notices but never claims they were explicitly read', () => {
  const handled = []; const queue = createBattlePlayback({ onChange() {}, schedule() {}, onUpkeepHandled: round => handled.push(round) });
  queue.receive(view(), cards); queue.receive(view([event(4), energyEvent(5), upkeepEvent(6)]), cards);
  assert.deepEqual(handled, []); queue.skip({ dismissUpkeep: true }); assert.deepEqual(handled, [3]);
  assert.equal(queue.model().read, null); assert.equal(queue.model().presented, 4);
  assert.match(queue.model().summary, /\+2 energy/);
});
test('presenting upkeep marks it handled before handing the screen back to automatic tips', () => {
  const handled = []; const queue = createBattlePlayback({ onChange() {}, schedule() {}, onUpkeepHandled: round => handled.push(round) });
  queue.receive(view(), cards); queue.receive(view([energyEvent(4), upkeepEvent(5)]), cards);
  assert.deepEqual(handled, [3]); assert.equal(queue.model().read, null);
});
test('a long catch-up of receipts settles with a summary and cancels the older Step', () => {
  const h = harness(); h.queue.setMode('step'); h.receive([]); h.receive([event(4)]); h.drain();
  h.receive(Array.from({ length: 61 }, (_, index) => upkeepEvent(5 + index, 'OngoingEffectsApplied', { roundId: 3 + index })));
  assert.equal(h.queue.model().active, false); assert.equal(h.queue.model().remaining, 0); assert.equal(h.timers.size, 0);
  assert.match(h.queue.model().summary, /No ongoing ticks/);
});
test('a Step backlog grown over several reads settles once it passes sixty items', () => {
  const h = harness(); h.queue.setMode('step'); h.receive([]);
  for (let read = 0; read < 5; read++) h.receive(Array.from({ length: 12 }, (_, index) => event(4 + read * 12 + index)));
  assert.equal(h.queue.model().active, true); assert.equal(h.queue.model().remaining, 60);
  h.receive([event(64), event(65)]);
  assert.equal(h.queue.model().active, false); assert.equal(h.queue.model().remaining, 0); assert.equal(h.timers.size, 0);
});
test('rotation, motion, speed and connection settles leave an unshown upkeep to its own notice', () => {
  for (const settle of [q => q.skip(), q => q.setRate(2), q => q.setReduced(true)]) {
    const h = harness(); h.receive([]); h.receive([event(4), energyEvent(5), upkeepEvent(6)]);
    settle(h.queue); assert.equal(h.queue.model().active, false); assert.deepEqual(h.handled, []);
  }
});
test('what arrives under a dialog queues behind a receipt held in Step, while Auto settles it', () => {
  const step = harness(); step.queue.setMode('step'); step.receive([]); step.receive([cleanupEvent(4)]); step.drain();
  step.receive([event(5), energyEvent(6)], {}, { animate: false });
  assert.equal(step.queue.model().sequence, 4); assert.equal(step.queue.model().remaining, 3); assert.deepEqual(step.handled, []);
  const auto = harness(); auto.receive([]); auto.receive([event(4)]);
  auto.receive([event(5), energyEvent(6)], {}, { animate: false });
  assert.equal(auto.queue.model().active, false); assert.deepEqual(auto.handled, []);
});
test('a catch-up that never shows its upkeep leaves it to the upkeep notice', () => {
  const h = harness(); h.receive([]);
  h.receive([event(4), energyEvent(5), upkeepEvent(6), event(7, { roundId: 4 }), event(8, { roundId: 5 })]);
  assert.equal(h.queue.model().active, false); assert.deepEqual(h.handled, []);
});
test('reduced motion retains Step pacing and resetting clears every receipt', () => {
  const h = harness(); h.queue.setMode('step'); h.queue.setReduced(true); h.receive([]); h.receive([incoming(4), energyEvent(5)]);
  h.drain(); assert.equal(h.queue.model().sequence, 4); h.queue.advance(); assert.equal(h.queue.model().receipt.category, 'Upkeep');
  h.drain(); assert.equal(h.queue.model().receipt.category, 'Upkeep', 'Step holds the upkeep');
  h.queue.reset(); assert.equal(h.queue.model().receipt, null); assert.equal(h.queue.model().presented, null); assert.equal(h.queue.model().read, null);
  h.receive([energyEvent(5)]); assert.equal(h.queue.model().active, false);
});
test('a response crossing several rounds catches up without a queue even with few casts', () => {
  const h = harness(); h.receive([]); h.receive([event(4), event(5, { roundId: 3 }), event(6, { roundId: 4 })]);
  assert.equal(h.queue.model().active, false); assert.match(h.queue.model().summary, /Strike/);
});
test('late ongoing facts give an automatic energy receipt a fresh reading interval', () => {
  const h = harness(); h.receive([]); h.receive([energyEvent(4)]); const stale = [...h.timers.values()][0].fn;
  h.receive([upkeepEvent(5)]); stale(); assert.equal(h.queue.model().active, true);
  assert.equal([...h.timers.values()][0].ms, 3500); h.drain(); assert.equal(h.queue.model().active, false);
});
test('opening-round introduction remains arrival-gated by the upkeep controller', () => {
  const h = harness(); h.receive([], { waiting: {} });
  h.receive([{ ...energyEvent(4), event: { ...energyEvent(4).event, roundId: 1 } }]);
  assert.equal(h.queue.model().active, false);
});
test('an incoming action held in Auto keeps its hold when the next result arrives under a dialog', () => {
  const h = harness(); h.receive([]); h.receive([incoming(4)]); h.tick(); h.tick();
  assert.equal(h.queue.model().stage, 'resolved');
  h.receive([incoming(5)], {}, { animate: false });
  assert.equal(h.queue.model().sequence, 4, 'the held action is still the one being read');
  assert.equal(h.queue.model().remaining, 2);
  h.queue.advance(); assert.equal(h.queue.model().sequence, 5);
});
test('an upkeep read on its own round shows that round\'s creatures; one the board has moved past shows none', () => {
  const h = harness(); h.queue.setMode('step'); h.receive([]);
  const latest = { ...view().board, roundNumber: 3, phase: 'Planning', subPhase: 'Evolution' };
  h.receive([energyEvent(4), upkeepEvent(5)], { board: latest });
  assert.equal(h.queue.model().receipt.category, 'Upkeep'); assert.equal(h.queue.model().board.round, 3);
  assert.deepEqual(h.queue.model().board.creatures.map(one => one.id), ['1', '2', '3']);
  const later = harness(); later.queue.setMode('step'); later.receive([]);
  const moved = view().board; moved.roundNumber = 4;
  later.receive([energyEvent(4), upkeepEvent(5)], { board: moved });
  assert.deepEqual(later.queue.model().board.creatures, []);
});
test('a held upkeep keeps the board it arrived with when the same round\'s combat arrives behind it', () => {
  const h = harness(); h.queue.setMode('step'); h.receive([]);
  h.receive([energyEvent(4), upkeepEvent(5)], { board: { ...view().board, roundNumber: 3, phase: 'Planning', subPhase: 'Evolution' } });
  const health = () => h.queue.model().board.creatures.find(one => one.id === '2').health;
  const before = health();

  h.receive([], { board: { ...view().board, roundNumber: 3, enemies: [creature(2, 1), creature(3)] } });

  assert.equal(h.queue.model().receipt.category, 'Upkeep');
  assert.equal(health(), before, 'the combat after it is not read into the upkeep');
});
test('an upkeep that arrives once its round\'s combat has begun draws no borrowed board', () => {
  const h = harness(); h.queue.setMode('step'); h.receive([]);

  h.receive([energyEvent(4), upkeepEvent(5)], { board: { ...view().board, roundNumber: 3 } });

  assert.equal(h.queue.model().receipt.category, 'Upkeep');
  assert.deepEqual(h.queue.model().board.creatures, []);
});
test('the reading card says whose action it is, what was cast and what came of it, in the board\'s names', () => {
  const h = harness(); h.receive([]);
  h.receive([incoming(4)], { board: { ...view().board, allies: [{ ...creature(1), name: 'Main' }], enemies: [{ ...creature(2, 13), name: 'Main' }, creature(3)] } });

  const card = h.queue.model().reading;

  assert.equal(card.kind, 'incoming'); assert.equal(card.title, 'Creature 2 · Strike');
  assert.match(card.detail, /^Critical · .+ → Creature 1$/);
});
test('the reading card marks the player\'s own cast, a fizzle and a round summary', () => {
  const own = harness(); own.receive([]); own.receive([event(4)]);
  assert.equal(own.queue.model().reading.kind, 'own');
  const fizzle = harness(); fizzle.receive([]);
  fizzle.receive([event(4, { resolution: { action: { actor: 1, spell: 'hit', targets: [2] }, fizzled: true, fizzleReason: { message: 'Not enough energy' } }, appliedOutcomes: [] })]);
  assert.match(fizzle.queue.model().reading.detail, /^Fizzled: /);
  const summary = harness(); summary.queue.setMode('step'); summary.receive([]); summary.receive([cleanupEvent(4)]);
  assert.deepEqual(summary.queue.model().reading, { kind: 'summary', title: 'Aftermath', detail: '1 condition ended' });
});
test('an aftermath with no recorded action frame in its round reads by its plaques alone', () => {
  const h = harness(); h.queue.setMode('step'); h.receive([]);

  h.receive([cleanupEvent(4)]);

  assert.deepEqual(h.queue.model().board.creatures, []);
  assert.equal(h.queue.model().paced.title, 'Aftermath'); assert.equal(h.queue.model().paced.markers.length, 1);
});
test('an aftermath reads on the round\'s latest action frame, even one settled without playing', () => {
  const h = harness(); h.queue.setMode('step'); h.receive([]);
  h.receive([event(4)]); h.drain();
  const deeper = event(5); deeper.event.frame = { ...deeper.event.frame, after: [creature(1), creature(2, 6), creature(3)] };

  h.receive([deeper], {}, { animate: false });
  h.receive([cleanupEvent(6)]);

  assert.equal(h.queue.model().receipt.category, 'Aftermath');
  assert.equal(h.queue.model().board.creatures.find(one => one.id === '2').health, 6, 'not the earlier cast\'s 13');
});
test('an aftermath after an unframed action draws no board rather than an older one', () => {
  const h = harness(); h.queue.setMode('step'); h.receive([]);
  h.receive([event(4)]); h.drain();
  const legacy = event(5); delete legacy.event.frame;

  h.receive([legacy]);
  h.receive([cleanupEvent(6)]);

  assert.deepEqual(h.queue.model().board.creatures, []);
});
test('an opponent\'s action arriving under a dialog waits unplayed, and plays for its OK once the field is in view', () => {
  const h = harness(); h.receive([]);

  h.receive([incoming(4)], {}, { animate: false });

  assert.equal(h.queue.model().active, true, 'results remain to be read');
  assert.equal(h.queue.model().sequence, null, 'nothing plays under the dialog'); assert.equal(h.timers.size, 0);
  h.queue.resume(); assert.equal(h.queue.model().sequence, 4);
  h.tick(); h.tick(); assert.equal(h.queue.model().asks, true, 'it waits for its OK');
});
test('the player\'s own action arriving under a dialog still settles, as before', () => {
  const h = harness(); h.receive([]);

  h.receive([event(4)], {}, { animate: false });

  assert.equal(h.queue.model().active, false);
});
test('a turnover arriving later under the same dialog stays behind an opponent\'s action still waiting to play', () => {
  const h = harness(); h.receive([]);
  h.receive([incoming(4)], {}, { animate: false });

  h.receive([cleanupEvent(5)], {}, { animate: false });

  assert.equal(h.queue.model().remaining, 2, 'neither was settled');
  h.queue.resume(); assert.equal(h.queue.model().sequence, 4);
});

// Creature 3 has fallen: what ends on it, or what it gains, is not read on the field (History keeps it).
const fallen = feed => ({ ...view(feed), board: { ...view().board, enemies: [creature(2, 13), creature(3, 0)] } });
const ending = (sequence, expired) => ({ sequence, event: { kind: 'ConditionsExpired', roundId: 2, expired } });
test('an aftermath reads only the creatures still standing', () => {
  const h = harness(); h.queue.setMode('step'); h.queue.receive(fallen([]), cards);

  h.queue.receive(fallen([ending(4, { 1: [{ effect: { kind: 'Bleed', amount: 1 } }], 3: [{ effect: { kind: 'Bleed', amount: 2 } }] })]), cards);

  const model = h.queue.model();
  assert.deepEqual(model.paced.markers.map(marker => marker.id), ['1']); assert.equal(model.paced.summary, '1 condition ended');
  assert.equal(model.receipt.sections[0].items.length, 1);
});
test('an aftermath only on fallen creatures is not read at all', () => {
  const h = harness(); h.queue.setMode('step'); h.queue.receive(fallen([]), cards);

  h.queue.receive(fallen([ending(4, { 3: [{ effect: { kind: 'Bleed', amount: 2 } }] }),
    { sequence: 5, event: { kind: 'StunImmunityGained', roundId: 2, creatures: [3] } }]), cards);

  assert.equal(h.queue.model().active, false); assert.equal(h.queue.model().receipt, null);
});
test('an aftermath is read on the creatures standing at it, not on a later death in the same batch', () => {
  const h = harness(); h.queue.setMode('step'); h.queue.receive(fallen([]), cards);
  const lethal = event(6, { frame: { before, after: [creature(1), creature(2, 13), creature(3, 0)], timeline } });

  h.queue.receive(fallen([event(4), ending(5, { 3: [{ effect: { kind: 'Bleed', amount: 2 } }] }), lethal]), cards);

  assert.equal(h.queue.model().remaining, 3, 'the cast, the aftermath on creature 3 while it stood, and the lethal cast');
});

test('a page opened again plays what came after the last result it showed, and a first visit starts from the board', () => {
  const missed = [event(4), event(6, { resolution: { action: { actor: 2, spell: 'hit', targets: [1] } } })];
  const fresh = harness(); fresh.queue.receive(view([event(2), ...missed]), cards);
  assert.equal(fresh.queue.model().active, false, 'nothing remembered: the first read is the baseline');

  const back = harness(); back.queue.receive(view([event(2), ...missed]), cards, { since: 2 });
  assert.deepEqual([back.queue.model().sequence, back.queue.model().remaining], [4, 2], 'the two actions after the last one shown play as they would have live');

  const ahead = harness(); ahead.queue.receive(view([event(2), ...missed]), cards, { since: 6 });
  assert.equal(ahead.queue.model().active, false, 'a mark at or past the feed\'s end is the baseline');
});
test('what a page closed now would resume after is the last result shown in full', () => {
  const h = harness(); h.queue.setMode('step');
  assert.equal(h.queue.model().watermark, null, 'nothing read yet');
  h.receive([event(2)], { feedNext: 3 }); assert.equal(h.queue.model().watermark, 2);
  h.receive([event(4), event(6)]); assert.equal(h.queue.model().watermark, 3, 'the action playing is not yet seen');
  h.drain(); assert.equal(h.queue.model().watermark, 6);
});
test('a page closed on a held upkeep that took in a later part resumes from its first part', () => {
  const h = harness(); h.queue.setMode('step'); h.receive([]); h.receive([energyEvent(4)]);
  h.receive([upkeepEvent(5, 'OngoingEffectsApplied', { bleedTicks: [{ creature: 2, damage: 3 }] })]);

  assert.equal(h.queue.model().sequence, 5, 'one receipt holds both parts');
  assert.equal(h.queue.model().watermark, 3, 'neither part is read yet');
});

test('an action that tried to crit hands the scene its d20: the face and whether it reached the threshold', () => {
  const hit = { status: 'Critical', criticalRoll: 19, actor: { id: 1 }, effects: [{ target: { id: 2 }, tone: 'harm', text: 'Damage 6' }] };
  assert.deepEqual(actionCue(hit).roll, { value: 19, critical: true });
  assert.deepEqual(actionCue({ ...hit, status: 'Resolved', criticalRoll: 4 }).roll, { value: 4, critical: false });
  assert.equal(actionCue({ ...hit, status: 'Resolved', criticalRoll: null }).roll, null);
  assert.equal(actionCue({ ...hit, status: 'Fizzled', criticalRoll: 12 }).roll, null);
});
