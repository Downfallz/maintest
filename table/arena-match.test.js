import test from 'node:test';
import assert from 'node:assert/strict';
import { matchReplay } from './feed.js';
import { createArenaMatch, roundNotes, replayText } from './arena-match.js';
const creature = (id, health) => ({ id, name: `Creature ${id}`, health, maxHealth: 20, isAlive: health > 0, conditions: [] });
const before = [creature(1, 20), creature(4, 20)], after = [creature(1, 20), creature(4, 0)];
const board = { roundNumber: 2, allies: after.slice(0, 1), enemies: after.slice(1), timeline: [] };
const feed = [{ sequence: 1, round: 1, event: { kind: 'OngoingEffectsApplied', roundId: 1, bleedTicks: [{ creature: 4, damage: 2 }] } },
  { sequence: 2, round: 1, event: { kind: 'PurchasesRevealed', roundId: 1, choices: [{ creature: 1, tier: 'root' }] } },
  { sequence: 3, round: 1, event: { kind: 'CombatActionResolved', roundId: 1, resolution: { action: { actor: 1, spell: 'strike', targets: [4] } }, appliedOutcomes: [{ kind: 'DamageOutcome', target: 4, amount: 20 }], frame: { before, after, timeline: [], rollOffs: [] } } },
  { sequence: 4, round: 2, event: { kind: 'ConditionsExpired', roundId: 1, expired: { 1: [{ effect: { kind: 'Defense' } }] } } },
  { sequence: 5, round: 2, event: { kind: 'RoundEnded', roundId: 1 } }];
const finished = { over: true, board, feed, outcome: { winner: 'player1', reason: 'Elimination' } };
function harness(view = finished) {
  let reads = 0, concessions = 0;
  const connection = { history: async () => { reads++; return { ok: true, body: view }; }, concede: async () => { concessions++; return { ok: true }; } };
  const control = createArenaMatch({ connection, onChange: () => {} }); control.reset('player1'); control.receive(view, { packages: [{ id: 'root', name: 'Root mutation' }] });
  return { control, connection, reads: () => reads, concessions: () => concessions };
}
test('round review uses applied ticks and authoritative cleanup round ids', () => {
  const notes = roundNotes(feed, board).join('\n'); assert.match(notes, /Round 1.*Ongoing damage −2/); assert.match(notes, /Round 1.*Defense ends/); assert.match(notes, /Round 1.*Aftermath complete/);
});
test('finished replay rereads the full seat and steps before and after actual frames', async () => {
  const h = harness(); assert.equal(h.reads(), 0); await h.control.replay();
  assert.match(h.control.model().summary, /Upkeep/); assert.match(h.control.model().note, /Reference/);
  h.control.move(1); assert.match(h.control.model().summary, /Root mutation/);
  h.control.move(1); assert.equal(h.control.model().board.creatures[1].health, 20);
  h.control.move(1); assert.equal(h.control.model().board.creatures[1].health, 0);
  h.control.move(1); assert.match(h.control.model().summary, /Cleanup/); assert.equal(h.control.model().board.creatures.length, 0);
  assert.match(h.control.model().note, /No battlefield/); h.control.move(-1); assert.equal(h.control.model().stage, 'after');
  h.control.jump(1); assert.equal(h.control.model().index, 0); h.control.closeReplay(); assert.equal(h.control.model().active, false);
});
test('live seats cannot request a finished replay and finished seats cannot concede', async () => {
  const live = harness({ ...finished, over: false }); await live.control.replay(); assert.equal(live.reads(), 0);
  const ended = harness(); await ended.control.concede(); assert.equal(ended.concessions(), 0);
});
test('switching seats fences a pending replay and clears private presentation', async () => {
  const h = harness(); let resolve; h.connection.history = () => new Promise(done => { resolve = done; });
  const replay = h.control.replay(); h.control.reset('player2'); resolve({ ok: true, body: finished }); await replay;
  assert.equal(h.control.model().active, false); assert.deepEqual(h.control.model().notes, []);
});
test('an empty or legacy replay does not pretend to have historical boards', async () => {
  const empty = harness({ ...finished, feed: [] }); await empty.control.replay(); assert.match(empty.control.model().error, /Nothing/);
  const event = structuredClone(feed[2]); delete event.event.frame;
  const old = harness({ ...finished, feed: [event] }); await old.control.replay(); assert.equal(old.control.model().board.creatures.length, 0);
});
test('replay refusal is readable without changing result or opening controls', async () => {
  const h = harness(); h.connection.history = async () => ({ ok: false, body: { message: 'Recording unavailable' } }); await h.control.replay();
  assert.equal(h.control.model().error, 'Recording unavailable'); assert.equal(h.control.model().active, false); assert.equal(h.control.model().over, true);
});
test('concession submits once and leaves the board to the host response', async () => {
  const h = harness({ ...finished, over: false }); const first = h.control.concede(); await h.control.concede(); await first;
  assert.equal(h.concessions(), 1); assert.equal(h.control.model().over, false);
});
test('an accepted concession is not offered again while the result is still on its way', async () => {
  const h = harness({ ...finished, over: false }); await h.control.concede();
  assert.equal(h.control.model().canConcede, false); await h.control.concede(); assert.equal(h.concessions(), 1);
});
test('a lost concession answer is an error the player can retry', async () => {
  const h = harness({ ...finished, over: false }); h.connection.concede = async () => null; await h.control.concede();
  assert.match(h.control.model().error, /could not confirm/); assert.equal(h.control.model().canConcede, true);
});
test('the last recorded step has no next and an unknown round is not jumped to', async () => {
  const h = harness(); await h.control.replay(); const last = h.control.model().count - 1;
  h.control.jump(99); assert.equal(h.control.model().index, 0);
  while (h.control.model().next) h.control.move(1);
  assert.equal(h.control.model().index, last); h.control.move(1); assert.equal(h.control.model().index, last);
});

test('an upkeep without applied ticks says so instead of leaving an empty heading', () => {
  assert.equal(replayText({ interlude: 'upkeep', ticks: [] }), 'Upkeep · No ongoing ticks applied');
  assert.match(replayText({ interlude: 'upkeep', ticks: [{ creature: 1, amount: 0, label: 'Healing over time', sign: '+', unit: 'HP' }] }), /Healing over time \+0 HP/);
});

test('an ordinary zero-energy replay keeps its receipt instead of claiming no ongoing ticks', () => {
  const event = { kind: 'RoundEnergyGranted', roundId: 1, grants: [
    { creature: 1, baseRequested: 0, passiveRequested: 0, gained: 0, before: 7, after: 7 },
  ] };
  const [step] = matchReplay([{ sequence: 1, event }], board, []);
  assert.equal(replayText(step), 'Upkeep · round energy · Creature 1: +0 energy · 7 → 7 (base 0, passive 0)');
  assert.match(replayText({ ...step, ticks: [] }), /No recorded grant details/);
});

test('full replay ends on the authoritative final battlefield and returns to its result', async () => {
  const h = harness(); await h.control.replay(); while (h.control.model().next) h.control.move(1);
  assert.match(h.control.model().summary, /You win/); assert.match(h.control.model().note, /Final public battlefield/);
  assert.equal(h.control.model().board.creatures[1].health, 0); assert.equal(h.control.model().result.title, 'Victory');
  h.control.move(-1); assert.equal(h.control.model().board.creatures.length, 0);
});
