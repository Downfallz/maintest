import test from 'node:test';
import assert from 'node:assert/strict';
import { createArenaPlanning, planningDecision } from './arena-planning.js';
const ok = body => ({ ok: true, body });
const pending = () => { let resolve; const promise = new Promise(done => { resolve = done; }); return { promise, resolve }; };
const evolution = () => ({ waitingAsked: 11, waitingFor: 'Evolution', waitingCreature: null,
  board: { allies: [{ id: 1 }] }, options: { evolution: { remainingPicks: 2, creatures: [{ creature: 1, availableTiers: ['root'] }] } } });
function harness(view = evolution()) {
  let current = view, visible = true, refreshes = 0;
  const writes = [], shown = [];
  const connection = { shown: async asked => { shown.push(asked); return ok(current); }, submit: async data => { writes.push(data); return ok(null); }, refresh: () => refreshes++ };
  const control = createArenaPlanning({ connection, visible: () => visible, onChange: () => {} });
  control.reset('player1'); control.receive(view); control.setOnline(true);
  return { control, connection, writes, shown, receive: next => { current = next; control.receive(next); }, hide: () => { visible = false; }, refreshes: () => refreshes };
}
test('only host-offered mutations can become a complete command', () => {
  const view = evolution();
  assert.equal(planningDecision(view, { creature: 2, tier: 'root' }), null);
  assert.equal(planningDecision(view, { creature: 1, tier: 'future' }), null);
  assert.deepEqual(planningDecision(view, { creature: 1, tier: 'root' }), { kind: 'Evolution', creature: 1, tier: 'root' });
  assert.deepEqual(planningDecision(view, { pass: true }), { kind: 'Evolution', pass: true });
});
test('tempo commands use the currently asked creature and wire tempo names', () => {
  const view = { ...evolution(), waitingFor: 'Speed', waitingCreature: 3 };
  assert.deepEqual(planningDecision(view, { speed: 'Quick', creature: 1 }), { kind: 'Speed', creature: 3, speed: 'Quick' });
  assert.equal(planningDecision(view, { speed: 'Instinct' }), null);
});
test('ties stay in their original groups and require every group to be settled', () => {
  const view = { ...evolution(), waitingFor: 'TieOrder', options: { tieOrder: { ties: [[1, 2], [3, 4]] } } };
  assert.equal(planningDecision(view, null, [2]), null);
  assert.deepEqual(planningDecision(view, null, [4, 2]), { kind: 'TieOrder', order: [2, 1, 4, 3] });
  assert.deepEqual(planningDecision(view, { keep: true }), { kind: 'TieOrder', order: [1, 2, 3, 4] });
});
test('opening starts the clock, selection writes nothing and confirmation is sent only once', async () => {
  const h = harness(); assert.deepEqual(h.shown, []);
  h.control.open(); h.control.choose({ creature: 1, tier: 'root' }); assert.deepEqual(h.writes, []);
  await h.control.submit(); await h.control.submit();
  assert.deepEqual(h.shown, [11]); assert.deepEqual(h.writes, [{ kind: 'Evolution', creature: 1, tier: 'root', asked: 11 }]);
  assert.equal(h.control.model().available, false);
});
for (const change of ['seat', 'question', 'hidden', 'offline', 'offer']) test(`a pending acknowledgement cannot submit after ${change} changes`, async () => {
  const h = harness(), ack = pending(); h.connection.shown = () => ack.promise;
  h.control.open(); h.control.choose({ creature: 1, tier: 'root' }); const sending = h.control.submit();
  if (change === 'seat') h.control.reset('player2');
  if (change === 'question') h.receive({ ...evolution(), waitingAsked: 12 });
  if (change === 'hidden') h.hide();
  if (change === 'offline') h.control.setOnline(false);
  if (change === 'offer') h.receive({ ...evolution(), options: { evolution: { creatures: [] } } });
  ack.resolve(ok(evolution())); await sending; assert.deepEqual(h.writes, []);
});
test('a question that changed before it was shown forces a refresh without a write', async () => {
  const h = harness(); h.connection.shown = async () => ok({ ...evolution(), waitingAsked: 12 });
  h.control.open(); h.control.choose({ pass: true }); await h.control.submit();
  assert.equal(h.refreshes(), 1); assert.deepEqual(h.writes, []); assert.equal(h.control.model().opened, false);
});
test('a refusal arriving after the next asking remains readable and is never retried', async () => {
  const h = harness(); h.connection.submit = async command => { h.writes.push(command); h.receive({ ...evolution(), waitingAsked: 12 }); return { ok: false, body: { message: 'Pick no longer available' } }; };
  h.control.open(); h.control.choose({ pass: true }); await h.control.submit();
  assert.equal(h.control.model().error, 'Pick no longer available'); assert.equal(h.writes.length, 1);
});
test('double confirmation while a write is pending cannot duplicate a decision', async () => {
  const h = harness(), write = pending(); h.connection.submit = command => { h.writes.push(command); return write.promise; };
  h.control.open(); h.control.choose({ pass: true }); const first = h.control.submit(); await h.control.submit();
  write.resolve(ok(null)); await first; assert.equal(h.writes.length, 1);
});
test('bot, finished and unasked seats have no planning controls', () => {
  for (const extra of [{ playedByBot: true }, { over: true }, { waitingAsked: null }]) {
    const h = harness({ ...evolution(), ...extra }); h.control.open(); assert.equal(h.control.model().opened, false); assert.deepEqual(h.shown, []);
  }
});
test('keeping the dice order sets the taps aside so the rows show the order that is sent', () => {
  const view = { ...evolution(), waitingFor: 'TieOrder', options: { tieOrder: { ties: [[1, 2], [3, 4]] } } };
  const h = harness(view); h.control.open(); h.control.order(2); assert.deepEqual(h.control.model().tapped, [2]);
  h.control.choose({ keep: true }); assert.deepEqual(h.control.model().tapped, []);
  assert.deepEqual(planningDecision(view, h.control.model().choice, h.control.model().tapped), { kind: 'TieOrder', order: [1, 2, 3, 4] });
});
test('a hidden page opens no planning controls and acknowledges nothing', () => {
  const h = harness(); h.hide(); h.control.open(); assert.equal(h.control.model().opened, false); assert.deepEqual(h.shown, []);
});
