import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createBattleUpkeep, roundBar, upkeepReceipt, upkeepDisplay, missingRoundEnergy } from './battle-upkeep.js';
import { roundNotes, replayText } from './battle-match.js';
import { matchReplay } from './feed.js';
import { createBattleHistory } from './battle-history.js';
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
  const history = createBattleHistory(); history.receive(view(1, [ongoing()]));
  assert.equal(history.model().rows[0].details[0].text, missingRoundEnergy);
  history.reset(); history.receive(view());
  assert.doesNotMatch(JSON.stringify(history.model()), /actual gain is unknown/);
});
test('first receipt waits for arrivals and completion, retaining earlier delta-feed facts', () => {
  const control = createBattleUpkeep(); control.reset('table:player1');
  control.receive(view(1, [energy()], { waiting: { seats: [] } }));
  assert.equal(control.model().receipt, null); assert.equal(control.model().pending, false);
  control.receive(view(1, [], { waiting: null })); assert.equal(control.model().pending, false);
  control.receive(view(1, [ongoing()])); assert.equal(control.model().pending, true); assert.equal(control.model().receipt.energy.length, 2);
  control.presented(); control.receive(view()); assert.equal(control.model().pending, false);
});
test('reconnect, visibility restoration and reload do not announce a receipt twice', () => {
  const saved = storage(); const control = createBattleUpkeep(saved); control.reset('a:player1');
  // suspend() clears and choose() resets: the mark is what keeps the round from being announced again.
  control.receive(view()); control.presented(); control.clear(); control.reset('a:player1'); control.receive(view()); assert.equal(control.model().pending, false);
  const reloaded = createBattleUpkeep(saved); reloaded.reset('a:player1'); reloaded.receive(view()); assert.equal(reloaded.model().pending, false);
  reloaded.receive(view(2)); assert.equal(reloaded.model().pending, true); reloaded.presented();
  reloaded.reset('b:player1'); reloaded.receive(view()); assert.equal(reloaded.model().pending, true);
  reloaded.reset('a:player2'); reloaded.receive(view()); assert.equal(reloaded.model().pending, true);
});
test('joining mid-match offers no backlog, but the next observed round does', () => {
  const control = createBattleUpkeep(); control.reset('a:player1'); control.receive(view(4));
  assert.equal(control.model().pending, false); control.receive(view(4)); assert.equal(control.model().pending, false);
  control.receive(view(5)); assert.equal(control.model().pending, true); assert.equal(control.model().receipt.round, 5);
  control.receive(view(6)); assert.equal(control.model().receipt.round, 6); assert.equal(control.model().pending, true);
  control.presented(); control.receive(view(6)); assert.equal(control.model().pending, false);
  control.reset('a:player1'); control.receive(view(6)); assert.equal(control.model().pending, false);
});
test('replays and unavailable seats expose no automatic receipt or retained facts', () => {
  const control = createBattleUpkeep(); control.reset('a:player1'); control.receive(view(1, undefined, { replay: true }));
  assert.equal(control.model().receipt, null); control.receive(view()); assert.equal(control.model().pending, true);
  control.receive(null); assert.equal(control.model().available, false); assert.equal(control.model().receipt, null);
  control.reset('b:player1'); control.receive(view(1, [])); assert.equal(control.model().receipt, null);
});
test('blocked and corrupt storage retain the in-memory once-only presentation', () => {
  for (const saved of [{ getItem: () => '{', setItem() { throw new Error('blocked'); } }, { getItem() { throw new Error('blocked'); } }]) {
    const control = createBattleUpkeep(saved); control.reset('a:player1'); control.receive(view()); control.presented();
    control.clear(); control.reset('a:player1'); control.receive(view()); assert.equal(control.model().pending, false);
  }
});

test('opening an already finished match presents its result, not its old opening upkeep', () => {
  const control = createBattleUpkeep(); control.reset('a:player1'); control.receive(view(1, undefined, { over: true }));
  assert.equal(control.model().pending, false); assert.equal(control.model().receipt.round, 1);
  control.receive(view(1, [], { over: true })); assert.equal(control.model().pending, false);
  control.reset('b:player1'); control.receive(view()); control.presented();
  control.receive(view(2, undefined, { over: true })); assert.equal(control.model().pending, true);
});
test('paced upkeep handled ahead of its board cannot reopen or move the tab marker backwards', () => {
  const saved = storage(); const controller = createBattleUpkeep(saved); controller.reset('table:player1');
  controller.receive(view(2)); controller.presented(3); controller.presented(2);
  controller.receive(view(3)); assert.equal(controller.model().pending, false);
  const restored = createBattleUpkeep(saved); restored.reset('table:player1'); restored.receive(view(3));
  assert.equal(restored.model().pending, false); restored.receive(view(4)); assert.equal(restored.model().pending, true);
});

test('scene markers group actual deltas by creature, including zero and separate energy sources', () => {
  const receipt = upkeepReceipt([energy(), ongoing()], 1, board);
  const display = upkeepDisplay(receipt.energy, receipt.ongoing, receipt.energyNote);
  assert.equal(display.summary, 'Round energy · 2 creatures · 3 ongoing effects');
  assert.deepEqual(display.markers.map(marker => [marker.id, ...marker.lines.map(line => line.text)]),
    [['1', '+5 EN', '+2 EN', '+0 HP', '−7 HP'], ['4', '+0 EN']]);
  assert.equal(upkeepDisplay(receipt.energy.slice(1), []).summary, '+0 energy each · 1 creature');
  assert.equal(upkeepDisplay([], [], missingRoundEnergy).summary, missingRoundEnergy);
});
for (const subPhase of ['IntentSelection', 'Activation', 'Cleanup']) test(`a page opened once the round is in ${subPhase} does not announce its upkeep`, () => {
  const combat = round => view(round, [energy(round), ongoing(round)], { board: { ...board, roundNumber: round, phase: 'Combat', subPhase } });
  const fresh = createBattleUpkeep(storage()); fresh.reset('seat'); fresh.receive(combat(1));
  assert.equal(fresh.model().pending, false, 'round 1 opened mid-combat');
  const store = storage(); const before = createBattleUpkeep(store); before.reset('seat'); before.receive(view(3)); before.presented(3);
  const back = createBattleUpkeep(store); back.reset('seat'); back.receive(combat(5));
  assert.equal(back.model().pending, false, 'returning rounds later, mid-combat');
  back.receive(view(6)); assert.equal(back.model().pending, true, 'the next round seen live is announced as before');
});
test('a page opened while the round is still in its opening phases still announces its upkeep', () => {
  const opening = createBattleUpkeep(storage()); opening.reset('seat');
  opening.receive(view(1, [energy(1), ongoing(1)], { board: { ...board, roundNumber: 1, phase: 'Planning', subPhase: 'Evolution' } }));
  assert.equal(opening.model().pending, true);
});

// The round's bar (docs/art/journeys/match.md, second-round; first-playtest-plan.md, F2): a phase only while a
// player decides or watches a cast, and at a round's start or end what the moment gave or ended.
test('the round\'s bar names a phase only while a player decides or watches a cast, and WAITING before both are seated', () => {
  assert.deepEqual(roundBar({ phase: 'Planning' }), { short: 'COMMAND', full: 'COMMAND', icon: null, said: '', saidFull: '' });
  assert.equal(roundBar({ phase: 'Combat' }).short, 'COMBAT');
  assert.equal(roundBar({ waiting: true, phase: 'StartOfRound', moment: { moment: 'start', each: 2, given: true } }).full, 'WAITING');
  assert.equal(roundBar({ phase: 'Match complete' }).full, 'MATCH COMPLETE');
});

test('at a round\'s start the bar says what the start gave: short under the number on a phone, the plan\'s words on a wide screen', () => {
  const display = upkeepDisplay([{ creature: 1, amount: 2, sign: '+', unit: 'energy', tone: 'energy', label: 'Warden', side: 'Your team' },
    { creature: 4, amount: 2, sign: '+', unit: 'energy', tone: 'energy', label: 'Stalker', side: 'Opponent' }], []);
  assert.deepEqual(display.bar, { moment: 'start', each: 2, given: true });
  assert.deepEqual(roundBar({ phase: 'StartOfRound', moment: display.bar }),
    { short: '+2', full: '+2 ENERGY EACH', icon: 'energy-gain', said: 'Round start: +2 energy each', saidFull: 'Round start' });
  const differing = upkeepDisplay([{ creature: 1, amount: 3, sign: '+', unit: 'energy', tone: 'energy', label: 'Warden', side: 'Your team' },
    { creature: 4, amount: 2, sign: '+', unit: 'energy', tone: 'energy', label: 'Stalker', side: 'Opponent' }], []);
  assert.deepEqual(roundBar({ phase: 'StartOfRound', moment: differing.bar }),
    { short: 'ENERGY', full: 'ROUND ENERGY', icon: null, said: 'Round start: round energy', saidFull: 'Round start' });
  // No grant recorded, or a start read with nothing of its own (a recorded replay): the moment's own word, never an old one.
  assert.deepEqual(roundBar({ phase: 'StartOfRound', moment: upkeepDisplay([], [], missingRoundEnergy).bar }),
    { short: 'START', full: 'ROUND START', icon: null, said: '', saidFull: '' });
  assert.equal(roundBar({ phase: 'StartOfRound' }).full, 'ROUND START');
});

test('at a round\'s end the bar says what ended, or else who became immune, or the moment alone', () => {
  assert.deepEqual(roundBar({ phase: 'EndOfRound', moment: { moment: 'end', ended: 2, immune: 1 } }),
    { short: '2 ENDED', full: '2 CONDITIONS ENDED', icon: null, said: 'Round end: 2 conditions ended', saidFull: 'Round end' });
  assert.equal(roundBar({ phase: 'EndOfRound', moment: { moment: 'end', ended: 1, immune: 0 } }).full, '1 CONDITION ENDED');
  assert.deepEqual(roundBar({ phase: 'EndOfRound', moment: { moment: 'end', ended: 0, immune: 1 } }),
    { short: '1 IMMUNE', full: '1 IMMUNE TO STUN', icon: null, said: 'Round end: 1 immune to stun', saidFull: 'Round end' });
  assert.deepEqual(roundBar({ phase: 'EndOfRound' }), { short: 'END', full: 'ROUND END', icon: null, said: '', saidFull: '' });
  for (const moment of [{ moment: 'start', each: 2, given: true }, { moment: 'end', ended: 12, immune: 0 }, null]) {
    for (const phase of ['StartOfRound', 'EndOfRound']) {
      const bar = roundBar({ phase, moment });
      assert.doesNotMatch(`${bar.short} ${bar.full} ${bar.said}`, /upkeep|aftermath/i);
    }
  }
});

test('from 760 px the bar\'s cell reserves a width no word it can say is longer than', () => {
  const css = readFileSync(new URL('./battle.css', import.meta.url), 'utf8');
  const reserved = /#round-phase::after \{ content: '([^']+)'/.exec(css)?.[1];
  assert.ok(reserved, 'battle.css reserves the cell with a hidden word');
  const moments = [null, { moment: 'start', each: 99, given: true }, { moment: 'start', each: null, given: true },
    ...[1, 9, 12, 99].flatMap(count => [{ moment: 'end', ended: count, immune: 0 }, { moment: 'end', ended: 0, immune: count }])];
  for (const phase of ['StartOfRound', 'Planning', 'Combat', 'EndOfRound']) {
    for (const moment of moments) {
      const { full } = roundBar({ phase, moment });
      assert.ok(full.length <= reserved.length, `"${full}" is longer than the reserved "${reserved}"`);
    }
  }
  assert.ok(roundBar({ waiting: true }).full.length <= reserved.length);
});
