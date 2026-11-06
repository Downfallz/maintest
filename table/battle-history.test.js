import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createBattleHistory, historyEntry } from './battle-history.js';
import { createBattlePlayback } from './battle-playback.js';

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
  const history = createBattleHistory(), playback = createBattlePlayback({ onChange() {} });
  const initial = view([cast(5), cast(2)]);
  history.receive(initial, catalogue); playback.receive(initial, new Map()); playback.skip();
  assert.equal(playback.model().active, false);
  assert.deepEqual(history.model().rows.map(row => row.sequence), [2, 5]);
  assert.match(text(history.model().rows[0]), /First \(Your team\).*Strike.*No critical.*Other · 2 \(Opponent\).*Damage 7/);
});
test('duplicates and filtered sequence gaps neither duplicate entries nor lose later outcomes', () => {
  const history = createBattleHistory();
  history.receive(view([cast(1), cast(1), cast(50)]), catalogue);
  history.receive(view([cast(50), cast(90)]), catalogue);
  assert.deepEqual(history.model().rows.map(row => row.sequence), [1, 50, 90]);
});
test('private decisions and unknown payloads are never retained or serialized into the journal', () => {
  const history = createBattleHistory();
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
  const history = createBattleHistory();
  history.receive(view([entry(1, 'RoundEnded', {}, 1), cast(2)]), catalogue);
  assert.deepEqual(history.model().rows.map(row => row.round), [1, 2]);
  history.receive(view([entry(3, 'RoundStarted', {}, 3)], { board: { ...board, roundNumber: 3 } }), catalogue);
  assert.deepEqual(history.model().rows.map(row => row.round), [2, 3]); assert.equal(history.model().omitted, true);
  history.receive(view([entry(1, 'RoundEnded', {}, 1)]), catalogue);
  assert.equal(history.model().current, 3); assert.equal(history.model().rows.length, 2);
});
test('the cap bounds retained rows and states that older entries are omitted', () => {
  const history = createBattleHistory({ limit: 3 });
  history.receive(view([cast(1), cast(2), cast(3), cast(4)]), catalogue);
  assert.deepEqual(history.model().rows.map(row => row.sequence), [2, 3, 4]); assert.equal(history.model().omitted, true);
});
test('waiting-room events remain silent until arrivals clear, including an empty waiting list', () => {
  const history = createBattleHistory();
  history.receive(view([cast(1)], { waiting: { seats: [] } }), catalogue);
  assert.equal(history.model().waiting, true); assert.deepEqual(history.model().rows, []);
  assert.equal(history.model().latest, undefined); assert.equal(history.model().unread, 0);
  history.receive(view([]), catalogue); assert.equal(history.model().rows.length, 1);
});
test('access loss and explicit viewpoint reset erase journal text and its bounds', () => {
  const history = createBattleHistory(); history.receive(view([cast(1)]), catalogue); history.receive(null);
  assert.deepEqual(history.model(), createBattleHistory().model());
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
  assert.deepEqual(built.details.map(one => one.text), ['1 · Other · 2 (Opponent) · Instinct · Initiative not recorded · No d20 roll recorded', '2 · First (Your team) · Precision · Initiative not recorded · No d20 roll recorded']);
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
  const history = createBattleHistory();
  history.receive(view([cast(1)], { waiting: { seats: [] }, over: true }), catalogue);
  assert.equal(history.model().waiting, false); assert.equal(history.model().rows.length, 1);
});
test('normalization never mutates a frozen response or retains its raw frame', () => {
  const input = cast(1); const frozen = value => { if (value && typeof value === 'object') { Object.values(value).forEach(frozen); Object.freeze(value); } }; frozen(input);
  const history = createBattleHistory(); history.receive(view([input]), catalogue);
  assert.doesNotMatch(JSON.stringify(history.model()), /"frame"|"resolution"|"knownSpells"/);
});

test('round-energy facts remain public history after a recipient dies and survive duplicate reads', () => {
  const history = createBattleHistory();
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

test('typed filters include affected creatures and teams, without parsing rendered text', () => {
  const history = createBattleHistory(); history.receive(view([cast(1), entry(2, 'RoundStarted')]), catalogue);
  history.filter('team', 'Opponent'); assert.equal(history.model().rows.length, 1);
  history.filter('creature', '2'); assert.equal(history.model().rows.length, 1);
  history.filter('kind', 'RoundStarted'); assert.equal(history.model().rows.length, 0); assert.equal(history.model().all.length, 2);
  history.latest(); history.filter('round', '2'); assert.equal(history.model().rows.length, 2);
});
test('recorded boundaries never borrow live stats and navigation stays within the active filters', () => {
  const history = createBattleHistory(); history.receive(view([cast(1), cast(2, { frame: undefined }), entry(3, 'RoundStarted')]), catalogue);
  history.open(); history.filter('kind', 'CombatActionResolved'); history.select(1); history.boundary('before');
  assert.equal(history.model().review.row.states.before[1].health, 20);
  history.receive(view([], { board: { ...board, enemies: [creature(2, 1)] } }), catalogue);
  history.boundary('after'); assert.equal(history.model().review.row.states.after[1].health, 13);
  history.move(1); assert.equal(history.model().review.row.states, null); assert.equal(history.model().review.next, false);
  history.move(-1); assert.equal(history.model().review.row.sequence, 1);
  assert.equal(history.model().review.row.sections.find(one => one.title === 'Applied effects').items[0].text.includes('Damage 7'), true);
});
test('reading survives round rollover and unread entries are counted once across duplicates and private gaps', () => {
  const history = createBattleHistory(); history.receive(view([cast(1)]), catalogue); history.open(); history.select(1);
  history.receive(view([entry(20, 'RoundStarted', {}, 4), entry(20, 'RoundStarted', {}, 4), entry(30, 'IntentSubmitted')], { board: { ...board, roundNumber: 4 } }), catalogue);
  assert.equal(history.model().review.row.sequence, 1); assert.equal(history.model().unread, 1);
  history.receive(view([entry(20, 'RoundStarted', {}, 4)]), catalogue); assert.equal(history.model().unread, 1);
  history.latest(); assert.deepEqual(history.model().rows.map(one => one.sequence), [20]); assert.equal(history.model().unread, 0);
});
test('earlier reads are explicit, bounded, private-filtered and cannot replace newer live events', async () => {
  let resolve, calls = 0;
  const history = createBattleHistory({ limit: 2, load: () => { calls++; return new Promise(done => { resolve = done; }); } });
  history.receive(view([cast(10), cast(11)]), catalogue); history.open(); const pending = history.earlier();
  await history.earlier(); assert.equal(calls, 1);
  history.receive(view([cast(12)]), catalogue);
  resolve({ ok: true, body: { feed: [cast(1), cast(2), cast(3), entry(4, 'IntentSubmitted', { secret: 'SECRET' })] } }); await pending;
  assert.deepEqual(history.model().rows.map(one => one.sequence), [2, 3]); assert.equal(history.model().latest.sequence, 12);
  assert.equal(history.model().unread, 1); assert.doesNotMatch(JSON.stringify(history.model()), /SECRET/);
  history.latest(); assert.deepEqual(history.model().rows.map(one => one.sequence), [11, 12]);
});
test('failed history differs from exhausted or filtered-empty history and supports retry', async () => {
  let answer = { ok: false };
  const history = createBattleHistory({ load: async () => answer }); history.receive(view([cast(10)]), catalogue); history.open();
  await history.earlier(); assert.match(history.model().error, /unavailable/); assert.equal(history.model().oldest, false);
  answer = { ok: true, body: { feed: [cast(10)] } }; await history.earlier(); assert.equal(history.model().oldest, true); assert.equal(history.model().error, '');
});
for (const operation of ['reset', 'close', 'latest']) test(`${operation} fences a delayed history result and clears stale review`, async () => {
  let resolve; const history = createBattleHistory({ load: () => new Promise(done => { resolve = done; }) });
  history.receive(view([cast(10)]), catalogue); history.open(); history.select(10); const pending = history.earlier();
  history[operation](); resolve({ ok: true, body: { feed: [cast(1)] } }); await pending;
  assert.equal(history.model().review, null); assert.equal(history.model().loading, false);
  assert.equal(history.model().rows.some(one => one.sequence === 1), false);
});
test('long sessions retain at most two pages of sanitized snapshots, including a pinned selected entry', async () => {
  const feed = Array.from({ length: 10000 }, (_, i) => { const row = cast(i); row.event.roundId = 1 + Math.floor(i / 100); return row; });
  const history = createBattleHistory({ load: async () => ({ ok: true, body: { feed } }) });
  history.receive(view(feed, { board: { ...board, roundNumber: 100 } }), catalogue); history.open(); history.select(9800);
  history.receive(view([ { ...cast(10000), event: { ...cast(10000).event, roundId: 101 } } ], { board: { ...board, roundNumber: 101 } }), catalogue);
  assert.equal(history.model().review.row.sequence, 9800); assert.ok(history.model().all.length <= 200);
  await history.earlier(); assert.equal(history.model().all.length, 200); assert.equal(history.model().latest.sequence, 10000);
  assert.ok(JSON.stringify(history.model()).length < 1500000);
});
test('an older page retains ordinary-energy knowledge even when the grant is outside that page', async () => {
  const feed = [entry(1, 'RoundEnergyGranted', { grants: [] }, 1), entry(2, 'OngoingEffectsApplied', {}, 1)];
  const history = createBattleHistory({ limit: 1, load: async () => ({ ok: true, body: { feed } }) });
  history.receive(view([cast(20)]), catalogue); history.open(); await history.earlier(); history.select(2);
  const row = history.model().review.row;
  assert.doesNotMatch(JSON.stringify(row.details), /not recorded/);
  assert.doesNotMatch(JSON.stringify(row.sections), /not recorded/);
});
test('next-round events read before their board arrives are kept, and only kept rows count as unread', () => {
  // The host reads the board before the feed: one response can carry a round-2 board and round-3 events.
  const history = createBattleHistory(); history.receive(view([cast(1)]), catalogue);
  history.receive(view([entry(10, 'RoundStarted', {}, 3), entry(11, 'CombatActionResolved', cast(11).event, 3)]), catalogue);
  history.receive(view([], { board: { ...board, roundNumber: 3 } }), catalogue);
  assert.deepEqual(history.model().rows.map(one => one.sequence), [1, 10, 11]);
  assert.equal(history.model().unread, 2); assert.equal(history.model().omitted, false);
  history.latest(); assert.deepEqual(history.model().rows.map(one => one.sequence), [1, 10, 11]);
});
test('earlier pages walk back disjoint and descending, stop at the start, and ignore live rows while browsing', async () => {
  const feed = Array.from({ length: 600 }, (_, i) => { const row = cast(i); row.event.roundId = 1 + Math.floor(i / 100); return row; });
  let calls = 0; const history = createBattleHistory({ load: async () => { calls++; return { ok: true, body: { feed } }; } });
  history.receive(view(feed, { board: { ...board, roundNumber: 6 } }), catalogue); history.open();
  assert.deepEqual([history.model().all[0].sequence, history.model().all.at(-1).sequence], [400, 599]);
  await history.earlier();
  assert.deepEqual([history.model().all[0].sequence, history.model().all.at(-1).sequence], [200, 399]); assert.equal(history.model().oldest, false);
  const live = cast(600); live.event.roundId = 6; history.receive(view([live], { board: { ...board, roundNumber: 6 } }), catalogue);
  assert.equal(history.model().all.at(-1).sequence, 399); assert.equal(history.model().latest.sequence, 600);
  await history.earlier();
  assert.deepEqual([history.model().all[0].sequence, history.model().all.at(-1).sequence], [0, 199]); assert.equal(history.model().oldest, true);
  await history.earlier(); assert.equal(calls, 2);
  history.latest(); assert.deepEqual([history.model().all[0].sequence, history.model().all.at(-1).sequence], [401, 600]);
});
test('recorded boundaries keep only public stats and condition durations, never spellbooks, mutations or sources', () => {
  const secret = id => ({ ...creature(id), knownSpells: ['spell:secret:v1'], acquiredTiers: ['tier:hidden:v1'], criticalChance: 0.25,
    currentInitiative: 7, conditions: [{ effect: { kind: 'Bleed', amountPerRound: 2 }, remainingRounds: 2, source: { caster: 9, spell: 'spell:secret:v1' } }] });
  const row = historyEntry(cast(1, { frame: { before: [secret(1), secret(2)], after: [secret(1), secret(2)] } }), board, catalogue);
  assert.deepEqual(Object.keys(row.states.before[0]).sort(),
    ['conditions', 'defeated', 'defense', 'energy', 'health', 'id', 'initiative', 'label', 'maxHealth', 'side']);
  assert.deepEqual(row.states.before[0].conditions, ['Bleed 2 · 2 rounds']);
  assert.doesNotMatch(JSON.stringify(row), /secret|hidden|criticalChance|caster/);
});

// AU11: the journal keeps each recorded order with the initiatives and rolls of its own event.
const orderSlot = (creature, owner = 'Player1') => ({ creature, owner, speed: 'Quick', initiative: 5 });
const orderBoard = { slot: 'Player1', roundNumber: 2, allies: [{ id: 2, name: 'Ally 2' }], enemies: [], rollOffs: [{ creature: 2, rolls: [20] }] };
for (const kind of ['TimelineBuilt', 'TiesOrdered']) {
  test(`a ${kind} entry shows the event's initiatives and rolls, not the board's current ones`, () => {
    const row = historyEntry({ sequence: 4, event: { kind, roundId: 2, timeline: { slots: [orderSlot(2)], rollOffs: [{ creature: 2, rolls: [11, 18] }] } } }, orderBoard);
    assert.match(row.details[0].text, /Initiative 5 · d20 11 → 18/); assert.doesNotMatch(row.details[0].text, /d20 20/);
  });
  test(`a ${kind} entry recorded without rolls says none was recorded`, () => {
    const row = historyEntry({ sequence: 4, event: { kind, roundId: 2, timeline: { slots: [orderSlot(2)] } } }, orderBoard);
    assert.match(row.details[0].text, /No d20 roll recorded/);
  });
}

test('last cast stays linked after journal rollover and never confuses a target with the caster', () => {
  const history = createBattleHistory({ limit: 2 });
  history.receive(view([cast(1), cast(2)]), catalogue);
  history.receive(view([entry(3, 'RoundStarted', {}, 9), entry(4, 'RoundEnded', {}, 9)], { board: { ...board, roundNumber: 9 } }), catalogue);
  assert.deepEqual(history.model().rows.map(row => row.sequence), [3, 4]);
  assert.equal(history.lastAction(1).sequence, 2); assert.equal(history.lastAction(2), null);
  history.open(); history.filter('round', '9'); history.inspect(history.lastAction(1));
  assert.equal(history.model().review.row.sequence, 2); assert.equal(history.model().review.row.states.after[1].health, 13);
  assert.equal(history.model().all.length, 2); assert.deepEqual(history.model().filters, { round: '', kind: '', creature: '', team: '' });
  history.receive(view([cast(1)], { board: { ...board, roundNumber: 9 } }), catalogue);
  assert.equal(history.lastAction(1).sequence, 2);
  history.receive(null); assert.equal(history.lastAction(1), null);
});
test('a historical inspection excludes future and unrevealed casts', () => {
  const history = createBattleHistory();
  history.receive(view([cast(2), cast(5), entry(6, 'IntentSubmitted', { action: { actor: 1, spell: 'secret' } })]), catalogue);
  assert.equal(history.lastAction(1, 4).sequence, 2); assert.equal(history.lastAction(1, 1), null);
  assert.equal(history.lastAction(1).sequence, 5);
  history.receive(view([], { waiting: { seats: [] } }), catalogue);
  assert.equal(history.lastAction(1), null);
});
