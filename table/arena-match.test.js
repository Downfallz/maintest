import test from 'node:test';
import assert from 'node:assert/strict';
import { createArenaMatch, arenaOutcome, roundNotes, replayText } from './arena-match.js';
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
test('outcomes name the host winner and distinguish elimination, cap, draw and concession', () => {
  assert.match(arenaOutcome(finished, 'player1'), /^You win.*last team standing/);
  assert.match(arenaOutcome({ outcome: { winner: null, reason: 'RoundCap' } }, 'player1'), /^Draw.*round cap/);
  assert.match(arenaOutcome({ outcome: { winner: 'Player2', reason: 'Concession' } }, 'player1'), /Player 2 wins.*concession/);
  assert.doesNotMatch(arenaOutcome({ ...finished, replay: true }, 'player1'), /You win/);
});
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

test('an upkeep without applied ticks says so instead of leaving an empty heading', () => {
  assert.equal(replayText({ interlude: 'upkeep', ticks: [] }), 'Upkeep · No ongoing ticks applied');
  assert.equal(replayText({ interlude: 'upkeep', ticks: [{ amount: 0 }] }), 'Upkeep · No ongoing ticks applied');
});
