import test from 'node:test';
import assert from 'node:assert/strict';
import { createArenaUpkeep, upkeepReceipt, missingRoundEnergy } from './arena-upkeep.js';
import { roundNotes, replayText } from './arena-match.js';
import { matchReplay } from './feed.js';
import { createArenaHistory } from './arena-history.js';
const board = { roundNumber: 1, allies: [{ id: 1, name: 'Warden', energy: 99, health: 0, isAlive: false, conditions: [] }], enemies: [{ id: 4, name: 'Stalker' }] };
const energy = (round = 1) => ({ sequence: round * 10, round: round + 1, event: { kind: 'RoundEnergyGranted', roundId: round, grants: [
  { creature: 1, gained: 5, baseRequested: 3, passiveRequested: 2, before: 2, after: 7 },
  { creature: 4, gained: 0, baseRequested: 0, passiveRequested: 0, before: 9, after: 9 },
] } });
const ongoing = (round = 1) => ({ sequence: round * 10 + 1, round: round + 1, event: { kind: 'OngoingEffectsApplied', roundId: round,
  energyRegenerationTicks: [{ creature: 1, gained: 2 }], regenerationTicks: [{ creature: 1, healed: 0 }], bleedTicks: [{ creature: 1, damage: 7 }] } });
const view = (round = 1, feed = [energy(round), ongoing(round)], extra = {}) => ({ board: { ...board, roundNumber: round }, feed, ...extra });
const storage = () => { const values = new Map(); return { getItem: key => values.get(key), setItem: (key, value) => values.set(key, value) }; };

test('receipt preserves boundary facts, zero and engine order even when the current creature is dead', () => {
  const receipt = upkeepReceipt([ongoing(), energy()], 1, board);
  assert.deepEqual(receipt.energy.map(row => row.amount), [5, 0]);
  assert.match(receipt.energy[0].text, /2 → 7 \(base 3, passive 2\)/);
  assert.doesNotMatch(JSON.stringify(receipt), /99|remainingRounds|conditions/);
  assert.deepEqual(receipt.ongoing.map(row => [row.tone, row.amount]), [['energy', 2], ['recovery', 0], ['harm', 7]]);
  assert.equal(receipt.energy[0].inspectable, true); assert.equal(receipt.energy[1].side, 'Opponent');
  assert.equal(upkeepReceipt([energy(), ongoing()], 2, board), null);
});
test('empty ticks remain a complete receipt and absent energy remains explicitly unknown', () => {
  const empty = ongoing(); empty.event = { kind: 'OngoingEffectsApplied', roundId: 1 };
  const recorded = upkeepReceipt([energy(), empty], 1, board);
  assert.equal(recorded.complete, true); assert.equal(recorded.energy.length, 2); assert.match(recorded.ongoingNote, /No ongoing/);
  const legacy = upkeepReceipt([ongoing()], 1, board);
  assert.equal(legacy.energyNote, missingRoundEnergy); assert.equal(legacy.energy.length, 0); assert.equal(legacy.ongoing.length, 3);
});
test('round review, replay and public journal retain capped zero healing and legacy gaps', () => {
  const feed = [energy(), ongoing()];
  assert.match(roundNotes(feed, board).join(' '), /Warden: \+5 energy/);
  assert.match(roundNotes(feed, board).join(' '), /Healing over time \+0 HP/);
  const replay = matchReplay([ongoing()], board);
  assert.match(replayText(replay[0]), /actual gain is unknown/); assert.match(replayText(replay[0]), /Healing over time \+0 HP/);
  const history = createArenaHistory(); history.receive(view(1, [ongoing()]));
  assert.equal(history.model().rows[0].details[0].text, missingRoundEnergy);
  history.reset(); history.receive(view());
  assert.doesNotMatch(JSON.stringify(history.model()), /actual gain is unknown/);
});
test('first receipt waits for arrivals and completion, retaining earlier delta-feed facts', () => {
  const control = createArenaUpkeep(); control.reset('table:player1');
  control.receive(view(1, [energy()], { waiting: { seats: [] } }));
  assert.equal(control.model().receipt, null); assert.equal(control.model().pending, false);
  control.receive(view(1, [], { waiting: null })); assert.equal(control.model().pending, false);
  control.receive(view(1, [ongoing()])); assert.equal(control.model().pending, true); assert.equal(control.model().receipt.energy.length, 2);
  control.presented(); control.receive(view()); assert.equal(control.model().pending, false);
});
test('reconnect, visibility restoration and reload do not announce a receipt twice', () => {
  const saved = storage(); const control = createArenaUpkeep(saved); control.reset('a:player1');
  // suspend() clears and choose() resets: the mark is what keeps the round from being announced again.
  control.receive(view()); control.presented(); control.clear(); control.reset('a:player1'); control.receive(view()); assert.equal(control.model().pending, false);
  const reloaded = createArenaUpkeep(saved); reloaded.reset('a:player1'); reloaded.receive(view()); assert.equal(reloaded.model().pending, false);
  reloaded.receive(view(2)); assert.equal(reloaded.model().pending, true); reloaded.presented();
  reloaded.reset('b:player1'); reloaded.receive(view()); assert.equal(reloaded.model().pending, true);
  reloaded.reset('a:player2'); reloaded.receive(view()); assert.equal(reloaded.model().pending, true);
});
test('joining mid-match offers no backlog, but the next observed round does', () => {
  const control = createArenaUpkeep(); control.reset('a:player1'); control.receive(view(4));
  assert.equal(control.model().pending, false); control.receive(view(4)); assert.equal(control.model().pending, false);
  control.receive(view(5)); assert.equal(control.model().pending, true); assert.equal(control.model().receipt.round, 5);
  control.receive(view(6)); assert.equal(control.model().receipt.round, 6); assert.equal(control.model().pending, true);
  control.presented(); control.receive(view(6)); assert.equal(control.model().pending, false);
  control.reset('a:player1'); control.receive(view(6)); assert.equal(control.model().pending, false);
});
test('replays and unavailable seats expose no automatic receipt or retained facts', () => {
  const control = createArenaUpkeep(); control.reset('a:player1'); control.receive(view(1, undefined, { replay: true }));
  assert.equal(control.model().receipt, null); control.receive(view()); assert.equal(control.model().pending, true);
  control.receive(null); assert.equal(control.model().available, false); assert.equal(control.model().receipt, null);
  control.reset('b:player1'); control.receive(view(1, [])); assert.equal(control.model().receipt, null);
});
test('blocked and corrupt storage retain the in-memory once-only presentation', () => {
  for (const saved of [{ getItem: () => '{', setItem() { throw new Error('blocked'); } }, { getItem() { throw new Error('blocked'); } }]) {
    const control = createArenaUpkeep(saved); control.reset('a:player1'); control.receive(view()); control.presented();
    control.clear(); control.reset('a:player1'); control.receive(view()); assert.equal(control.model().pending, false);
  }
});

test('opening an already finished match presents its result, not its old opening upkeep', () => {
  const control = createArenaUpkeep(); control.reset('a:player1'); control.receive(view(1, undefined, { over: true }));
  assert.equal(control.model().pending, false); assert.equal(control.model().receipt.round, 1);
  control.receive(view(1, [], { over: true })); assert.equal(control.model().pending, false);
  control.reset('b:player1'); control.receive(view()); control.presented();
  control.receive(view(2, undefined, { over: true })); assert.equal(control.model().pending, true);
});
