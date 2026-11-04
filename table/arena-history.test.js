import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createArenaHistory, historyEntry } from './arena-history.js';
import { createArenaPlayback } from './arena-playback.js';

const creature = (id, health = 20) => ({ id, name: id === 1 ? 'First' : 'Other', health, maxHealth: 20, energy: 4, totalDefense: 2, isAlive: health > 0 });
const before = [creature(1), creature(2), creature(3)];
const board = { roundNumber: 2, slot: 'Player1', allies: before.slice(0, 1), enemies: before.slice(1) };
const catalogue = { cards: [{ id: 'hit', name: 'Strike' }], packages: [{ id: 'root', name: 'Warden' }] };
const entry = (sequence, kind, payload = {}, roundId = 2) => ({ sequence, round: 99, event: { kind, roundId, ...payload } });
function cast(sequence, extra = {}) {
  return entry(sequence, 'CombatActionResolved', {
    resolution: { action: { actor: 1, spell: 'hit', targets: [2] }, isCritical: false },
    appliedOutcomes: [{ kind: 'DamageOutcome', target: 2, amount: 7 }],
    frame: { before, after: [creature(1), creature(2, 13), creature(3)] }, ...extra });
}
const view = (feed, extras = {}) => ({ board, feed, feedNext: 100, ...extras });
const text = row => [row.title, row.summary, ...row.details.map(one => one.text)].join(' ');

test('first load retains every public action, independently of animation baselines and skipped playback', () => {
  const history = createArenaHistory(), playback = createArenaPlayback({ onChange() {} });
  const initial = view([cast(5), cast(2)]);
  history.receive(initial, catalogue); playback.receive(initial, new Map()); playback.skip();
  assert.equal(playback.model().active, false);
  assert.deepEqual(history.model().rows.map(row => row.sequence), [2, 5]);
  assert.match(text(history.model().rows[0]), /First \(Your team\).*Strike.*No critical.*Other · 2 \(Opponent\).*Damage 7/);
});
test('duplicates and filtered sequence gaps neither duplicate entries nor lose later outcomes', () => {
  const history = createArenaHistory();
  history.receive(view([cast(1), cast(1), cast(50)]), catalogue);
  history.receive(view([cast(50), cast(90)]), catalogue);
  assert.deepEqual(history.model().rows.map(row => row.sequence), [1, 50, 90]);
});
test('private decisions and unknown payloads are never retained or serialized into the journal', () => {
  const history = createArenaHistory();
  const hidden = ['IntentSubmitted', 'SpeedChoiceSubmitted', 'EvolutionChoiceSubmitted', 'EvolutionPassed', 'TieOrderSubmitted'];
  history.receive(view(hidden.map((kind, i) => entry(i, kind, { spell: 'secret', tier: 'private', speed: 'Quick' }))), catalogue);
  assert.deepEqual(history.model().rows, []); assert.equal(history.model().unsupported, false);
  history.receive(view([entry(10, 'FuturePrivateEvent', { details: 'secret' })]), catalogue);
  assert.deepEqual(history.model().rows, []); assert.equal(history.model().unsupported, true);
  assert.doesNotMatch(JSON.stringify(history.model()), /secret|FuturePrivateEvent/);
});
test('a public purchase reveal names the mutation and recipient without exposing its earlier pick', () => {
  const row = historyEntry(entry(2, 'PurchasesRevealed', { choices: [{ creature: 2, tier: 'root' }] }), board, catalogue);
  assert.match(text(row), /Mutations revealed.*Other · 2 \(Opponent\) · Warden/);
});
test('actual multi-target and caster effects retain durations, criticals, dropped targets and defeat', () => {
  const row = historyEntry(cast(5, {
    resolution: { action: { actor: 2, spell: 'hit', targets: [1, 3] }, isCritical: true,
      droppedTargets: [{ target: 3, error: { message: 'No longer alive' } }] },
    appliedOutcomes: [{ kind: 'DamageOutcome', target: 1, amount: 20, critical: true },
      { kind: 'EnergyOutcome', target: 2, amount: 2, onCaster: true },
      { kind: 'ConditionOutcome', target: 1, effect: { kind: 'Bleed', amountPerRound: 2, duration: { rounds: 3 } } }],
    frame: { before, after: [creature(1, 0), creature(2), creature(3)] }
  }), board, catalogue);
  assert.equal(row.status, 'Critical');
  for (const expected of ['Opponent', 'Damage 20!', 'Energy 2 · caster', 'Bleed 2 / round · 3 rounds', 'Dropped target: Other · 3 (Opponent): No longer alive', 'HP 20 → 0', 'Defeated']) assert.ok(text(row).includes(expected), expected);
});
test('a fizzle reports its refusal without claiming a critical roll', () => {
  const row = historyEntry(cast(1, { resolution: { action: { actor: 1, spell: 'hit', targets: [] }, fizzled: true,
    fizzleReason: { code: 'Energy.TooLow' } }, appliedOutcomes: [], frame: undefined }), board, catalogue);
  assert.equal(row.status, 'Fizzled'); assert.match(text(row), /No targets.*Energy.TooLow.*No applied effects recorded.*not recorded/);
  assert.doesNotMatch(text(row), /No critical|Critical/);
});
test('legacy actions do not use the live board to invent before/after changes or defeats', () => {
  const row = historyEntry(cast(1, { frame: undefined }), { ...board, enemies: [creature(2, 0)] }, catalogue);
  assert.match(text(row), /Damage 7.*Before\/after values were not recorded/);
  assert.doesNotMatch(text(row), /Defeated|HP 20/);
});
test('upkeep records actual ticks in engine order, including zero, without claiming ordinary energy gains', () => {
  const row = historyEntry(entry(1, 'OngoingEffectsApplied', { energyRegenerationTicks: [{ creature: 1, gained: 1 }],
    regenerationTicks: [{ creature: 2, healed: 0 }], bleedTicks: [{ creature: 2, damage: 2 }] }), board, catalogue);
  assert.deepEqual(row.details.map(one => one.tone), ['energy', 'recovery', 'harm']);
  assert.match(text(row), /Energy from effects \+1.*Healing over time \+0.*Ongoing damage −2/);
  const empty = historyEntry(entry(2, 'OngoingEffectsApplied'), board, catalogue);
  assert.match(text(empty), /Ordinary round energy is separate/);
});
test('aftermath retains expired conditions and immunity facts, and an aftermath that changed nothing says nothing', () => {
  const gone = historyEntry(entry(1, 'ConditionsExpired', { expired: { 2: [{ effect: { kind: 'Bleed' } }, { effect: { kind: 'Defense' } }], 3: [] } }), board, catalogue);
  const immune = historyEntry(entry(2, 'StunImmunityGained', { creatures: [1] }), board, catalogue);
  assert.deepEqual(gone.details, [{ text: 'Other · 2 (Opponent) · Bleed + Defense ends', tone: 'condition' }]);
  assert.deepEqual(immune.details, [{ text: 'First (Your team) · immune to stun', tone: 'condition' }]);
  assert.deepEqual(historyEntry(entry(3, 'ConditionsExpired', { expired: {} }), board, catalogue).details, []);
});
test('round ownership uses the event round rather than the next snapshot, and retains two rounds', () => {
  const history = createArenaHistory();
  history.receive(view([entry(1, 'RoundEnded', {}, 1), cast(2)]), catalogue);
  assert.deepEqual(history.model().rows.map(row => row.round), [1, 2]);
  history.receive(view([entry(3, 'RoundStarted', {}, 3)], { board: { ...board, roundNumber: 3 } }), catalogue);
  assert.deepEqual(history.model().rows.map(row => row.round), [2, 3]); assert.equal(history.model().omitted, true);
  history.receive(view([entry(1, 'RoundEnded', {}, 1)]), catalogue);
  assert.equal(history.model().current, 3); assert.equal(history.model().rows.length, 2);
});
test('the cap bounds retained rows and states that older entries are omitted', () => {
  const history = createArenaHistory({ limit: 3 });
  history.receive(view([cast(1), cast(2), cast(3), cast(4)]), catalogue);
  assert.deepEqual(history.model().rows.map(row => row.sequence), [2, 3, 4]); assert.equal(history.model().omitted, true);
});
test('waiting-room events remain silent until arrivals clear, including an empty waiting list', () => {
  const history = createArenaHistory();
  history.receive(view([cast(1)], { waiting: { seats: [] } }), catalogue);
  assert.equal(history.model().waiting, true); assert.deepEqual(history.model().rows, []);
  history.receive(view([]), catalogue); assert.equal(history.model().rows.length, 1);
});
test('access loss and explicit viewpoint reset erase journal text and its bounds', () => {
  const history = createArenaHistory(); history.receive(view([cast(1)]), catalogue); history.receive(null);
  assert.deepEqual(history.model(), { available: false, waiting: false, current: 0, omitted: false, unsupported: false, rows: [] });
  history.receive(view([cast(1)]), catalogue); history.reset(); assert.deepEqual(history.model().rows, []);
});
test('invalid sequences are rejected and missing catalogue data remains readable', () => {
  assert.equal(historyEntry({ ...cast(1), sequence: undefined }, board, catalogue), null);
  assert.equal(historyEntry(cast(-1), board, catalogue), null);
  assert.match(text(historyEntry(cast(2), {}, null)), /Creature 1 \(Unknown team\) · hit/);
});
test('a skipped turn names who lost it and why', () => {
  assert.match(text(historyEntry(entry(1, 'SlotSkipped', { actor: 1, reason: { message: 'Unable to act' } }), board, catalogue)), /First \(Your team\) · Turn skipped.*Unable to act/);
});
test('the outcome is read for this seat although the feed names slots the way the engine does', () => {
  const ended = winner => entry(2, 'MatchEnded', { outcome: { winner, reason: 'Elimination' } });
  assert.match(text(historyEntry(ended('Player1'), board, catalogue)), /You win · the last team standing/);
  assert.match(text(historyEntry(ended('Player2'), board, catalogue)), /Player 2 wins · the last team standing/);
  assert.match(text(historyEntry(ended(null), board, catalogue)), /draw/);
  assert.match(text(historyEntry(ended('Player1'), { ...board, slot: 'Player2' }, catalogue)), /Player 1 wins/);
});
test('creatures are named the way the scene names them: the id only tells namesakes apart', () => {
  const row = historyEntry(cast(1, { resolution: { action: { actor: 1, spell: 'hit', targets: [2, 3] } } }), board, catalogue);
  assert.match(text(row), /First \(Your team\) · Strike.*Targets: Other · 2 \(Opponent\), Other · 3 \(Opponent\)/);
  assert.match(text(historyEntry(cast(1), { allies: [{ id: 1 }], enemies: [{ id: 2, name: '' }] }, catalogue)), /Creature 1 \(Your team\).*Creature 2 \(Opponent\)/);
});
test('a reveal names the caster, the spell and the targets it was declared on', () => {
  const row = historyEntry(entry(1, 'ActionRevealed', { action: { actor: 2, spell: 'hit', targets: [1] } }), board, catalogue);
  assert.equal(row.title, 'Other · 2 (Opponent) · reveals Strike');
  assert.match(text(row), /Targets: First \(Your team\).*result follows/);
  assert.match(text(historyEntry(entry(2, 'ActionRevealed', { action: { actor: 1, spell: 'unknown', targets: [] } }), board, catalogue)), /reveals unknown.*No targets/);
});
test('a built or reordered timeline lists every slot in order with its tempo', () => {
  const timeline = { slots: [{ owner: 'Player2', creature: 2, speed: 'Quick' }, { owner: 'Player1', creature: 1, speed: 'Standard' }], rollOffs: [] };
  const built = historyEntry(entry(1, 'TimelineBuilt', { timeline }), board, catalogue);
  assert.deepEqual(built.details.map(one => one.text), ['1 · Other · 2 (Opponent) · Instinct', '2 · First (Your team) · Precision']);
  assert.deepEqual(historyEntry(entry(2, 'TiesOrdered', { timeline }), board, catalogue).details.length, 2);
  assert.deepEqual(historyEntry(entry(3, 'TimelineBuilt', {}), board, catalogue).details, []);
});
test('round and phase markers carry no detail and use the player\'s words', () => {
  const tempo = historyEntry(entry(1, 'SubPhaseEntered', { subPhase: 'Speed' }), board, catalogue);
  assert.equal(tempo.title, 'Tempo'); assert.deepEqual(tempo.details, []); assert.equal(tempo.summary, 'Tempo');
  assert.equal(historyEntry(entry(2, 'SubPhaseEntered', {}), board, catalogue).title, 'Phase changed');
  assert.equal(historyEntry(entry(3, 'RoundStarted', {}, 3), board, catalogue).round, 3);
});
test('a finished match is never a waiting room, whatever the view still says about arrivals', () => {
  const history = createArenaHistory();
  history.receive(view([cast(1)], { waiting: { seats: [] }, over: true }), catalogue);
  assert.equal(history.model().waiting, false); assert.equal(history.model().rows.length, 1);
});
test('normalization never mutates a frozen response or retains its raw frame', () => {
  const input = cast(1); const frozen = value => { if (value && typeof value === 'object') { Object.values(value).forEach(frozen); Object.freeze(value); } }; frozen(input);
  const history = createArenaHistory(); history.receive(view([input]), catalogue);
  assert.doesNotMatch(JSON.stringify(history.model()), /"frame"|"resolution"|"knownSpells"/);
});

test('round-energy facts remain public history after a recipient dies and survive duplicate reads', () => {
  const history = createArenaHistory();
  const receipt = { sequence: 5, round: 2, event: { kind: 'RoundEnergyGranted', roundId: 2, grants: [
    { creature: 1, baseRequested: 4, passiveRequested: 1, gained: 5, before: 2, after: 7 },
    { creature: 3, baseRequested: 0, passiveRequested: 0, gained: 0, before: 3, after: 3 },
  ] } };
  const view = { board: { ...board, allies: [creature(1, 0)] }, feed: [receipt] };
  history.receive(view, catalogue); history.receive(view, catalogue);
  const rows = history.model().rows;
  assert.equal(rows.length, 1); assert.equal(history.model().unsupported, false);
  assert.ok(rows[0].details[0].text.includes('+5 energy · 2 → 7 (base 4, passive 1)'));
  assert.ok(rows[0].details[1].text.includes('+0 energy'));
  history.receive({ ...view, waiting: { seats: [] } }, catalogue);
  assert.deepEqual(history.model().rows, []);
  history.receive({ ...view, feed: [] }, catalogue); assert.equal(history.model().rows.length, 1);
});
