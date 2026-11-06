import test from 'node:test';
import assert from 'node:assert/strict';
import { createBattleCombat } from './battle-combat.js';

const tick = () => new Promise(resolve => setImmediate(resolve));
const pending = () => { let resolve; const promise = new Promise(done => { resolve = done; }); return { promise, resolve }; };
const ok = body => ({ ok: true, status: 200, body });
const legal = (candidates = [4, 5], minTargets = 1, maxTargets = 1) => ({ candidates, minTargets, maxTargets });
const action = (targets = legal()) => ({ waitingFor: 'Action', waitingAsked: 11, waitingCreature: 1,
  options: { action: { actor: 1, spells: [{ spell: 'bolt', legalTargets: targets }] } }, board: { allies: [{ id: 1, knownSpells: ['bolt', 'locked'] }] } });
function harness(view = action()) {
  const writes = [], acknowledgements = [], frames = [];
  let current = view, visible = true, refreshes = 0;
  const connection = { shown: async asked => { acknowledgements.push(asked); return ok(current); },
    submit: async command => { writes.push(command); return ok(null); }, refresh: () => { refreshes++; } };
  const control = createBattleCombat({ connection, onChange: value => frames.push(value), visible: () => visible });
  control.reset('player1'); control.receive(current); control.setOnline(true);
  return { control, connection, writes, acknowledgements, frames, receive: next => { current = next; control.receive(next); },
    hide: () => { visible = false; }, refreshes: () => refreshes };
}

test('inspection is read only until combat controls are visibly opened', async () => {
  const h = harness(); await tick(); assert.deepEqual(h.acknowledgements, []);
  h.control.open(); await tick(); assert.deepEqual(h.acknowledgements, [11]); assert.equal(h.frames.at(-1).opened, true);
});
test('a local spell choice and repeated target taps never submit an action', async () => {
  const h = harness(); h.control.open(); h.control.choose('bolt'); h.control.pick(4); h.control.pick(4); await tick();
  assert.deepEqual(h.control.model().picked, []); assert.deepEqual(h.writes, []);
});
for (const [label, targets, picked] of [
  ['enemy', legal(), [4]], ['friendly', legal([2]), [2]], ['self', legal([1]), [1]],
  ['multiple', legal([1, 4, 5], 2, 2), [1, 5]], ['targetless', legal([], 0, 0), []], ['fizzle', legal([], 1, 1), []],
]) test(`${label} targets use the host's exact bounds and explicit confirmation`, async () => {
  const h = harness(action(targets)); h.control.open(); h.control.choose('bolt'); picked.forEach(id => h.control.pick(id));
  assert.deepEqual(h.writes, []); await h.control.submit();
  assert.deepEqual(h.writes, [{ kind: 'Action', creature: 1, spell: 'bolt', targets: picked, asked: 11 }]);
});
test('a known spell the host did not offer cannot be chosen', async () => {
  const h = harness(action(legal([4, 5], 2, 2))); h.control.open(); h.control.choose('locked'); await h.control.submit();
  assert.equal(h.control.model().chosen, null); assert.deepEqual(h.writes, []);
});
test('too few targets picked cannot submit', async () => {
  const h = harness(action(legal([4, 5], 2, 2))); h.control.open(); h.control.choose('bolt'); h.control.pick(4); await h.control.submit();
  assert.deepEqual(h.writes, []);
});
test('a hidden unaffordable intent remains an offered plan, separate from binding targets', async () => {
  const h = harness({ ...action(), waitingFor: 'Intent', options: { intent: { creatures: [{ creature: 1, castableSpells: [], unaffordableSpells: ['bolt'] }] } } });
  h.control.open(); h.control.choose('bolt'); assert.equal(h.control.model().target, null); await h.control.submit();
  assert.deepEqual(h.writes, [{ kind: 'Intent', creature: 1, spell: 'bolt', asked: 11 }]);
});
test('a revealed intent binds targets without declaring a new spell', async () => {
  const h = harness({ ...action(), waitingFor: 'Target', options: { target: { spell: 'bolt', legalTargets: legal() } } });
  h.control.open(); h.control.pick(5); await h.control.submit(); assert.deepEqual(h.writes, [{ kind: 'Target', targets: [5], asked: 11 }]);
});
test('double confirmation while acknowledgement or write is pending posts only once', async () => {
  const h = harness(), ack = pending(), write = pending();
  h.connection.shown = () => ack.promise; h.connection.submit = command => { h.writes.push(command); return write.promise; };
  h.control.open(); h.control.choose('bolt'); h.control.pick(4);
  const first = h.control.submit(); await h.control.submit(); ack.resolve(ok(action())); await tick(); await h.control.submit();
  assert.equal(h.writes.length, 1); write.resolve(ok(null)); await first; await h.control.submit(); assert.equal(h.writes.length, 1);
});
test('an old asking discovered by acknowledgement closes the draft without posting', async () => {
  const h = harness(); h.connection.shown = async () => ok({ ...action(), waitingAsked: 12 });
  h.control.open(); h.control.choose('bolt'); h.control.pick(4); await h.control.submit();
  assert.deepEqual(h.writes, []); assert.equal(h.control.model().opened, false); assert.equal(h.refreshes(), 1);
});
test('changing questions while acknowledgement is pending invalidates its draft', async () => {
  const h = harness(), ack = pending(); h.connection.shown = () => ack.promise;
  h.control.open(); h.control.choose('bolt'); h.control.pick(4); const send = h.control.submit();
  h.receive({ ...action(), waitingAsked: 12 }); ack.resolve(ok(action())); await send;
  assert.deepEqual(h.writes, []); assert.equal(h.control.model().sending, false); assert.equal(h.control.model().opened, false);
});
test('removed options are revalidated before the acknowledged draft is posted', async () => {
  const h = harness(), ack = pending(); h.connection.shown = () => ack.promise;
  h.control.open(); h.control.choose('bolt'); h.control.pick(4); const send = h.control.submit();
  h.receive(action(legal([5]))); ack.resolve(ok(action())); await send; assert.deepEqual(h.writes, []);
});
test('a late acknowledgement from a closed draft cannot close a reopened one', async () => {
  const h = harness(), old = pending(); let count = 0;
  h.connection.shown = () => ++count === 1 ? old.promise : Promise.resolve(ok(action()));
  h.control.open(); h.control.close(); h.control.open(); await tick(); old.resolve(ok({ ...action(), waitingAsked: 12 })); await tick();
  assert.equal(h.control.model().opened, true); assert.equal(h.control.model().error, '');
});
test('a hidden or covered page cannot post an acknowledged action', async () => {
  const h = harness(); h.control.open(); h.control.choose('bolt'); h.control.pick(4); h.hide(); await h.control.submit(); assert.deepEqual(h.writes, []);
});
test('a replaced seat ignores the old command response', async () => {
  const h = harness(), write = pending(); h.connection.submit = () => write.promise;
  h.control.open(); h.control.choose('bolt'); h.control.pick(4); const send = h.control.submit(); await tick();
  h.control.reset('player2'); h.receive(action()); h.control.setOnline(true); h.control.open();
  write.resolve({ ok: false, status: 409, body: { message: 'Old seat' } }); await send;
  assert.equal(h.control.model().opened, true); assert.equal(h.control.model().error, '');
});
test('host rejection is shown and clears the draft without an automatic retry', async () => {
  const h = harness(); h.connection.submit = async () => ({ ok: false, status: 409, body: { message: 'Question expired' } });
  h.control.open(); h.control.choose('bolt'); h.control.pick(4); await h.control.submit();
  assert.equal(h.control.model().error, 'Question expired'); assert.equal(h.control.model().opened, false); assert.equal(h.control.model().chosen, null);
});
test('an unknown write outcome warns the player and never retries automatically', async () => {
  const h = harness(); let calls = 0; h.connection.submit = async () => { calls++; throw new Error('lost'); };
  h.control.open(); h.control.choose('bolt'); h.control.pick(4); await h.control.submit(); await h.control.submit();
  assert.match(h.control.model().error, /may have arrived/); assert.equal(calls, 1);
});
test('unrelated planning, bot and ended views offer no combat controls', () => {
  for (const changes of [{ waitingFor: 'Speed' }, { playedByBot: true }, { over: true }, { waitingAsked: null }]) {
    const h = harness({ ...action(), ...changes }); h.control.open(); assert.equal(h.control.model().opened, false);
  }
});

test('a failed acknowledgement blocks every command until the controls are reopened', async () => {
  const h = harness(); let calls = 0;
  h.connection.shown = async () => { if (++calls === 1) throw new Error('offline'); return ok(action()); };
  h.control.open(); h.control.choose('bolt'); h.control.pick(4); await h.control.submit(); await h.control.submit();
  assert.deepEqual(h.writes, []); assert.equal(calls, 1); assert.match(h.control.model().error, /Close and reopen/);
});
test('reopening after a failed acknowledgement asks again and then posts', async () => {
  const h = harness(); let calls = 0;
  h.connection.shown = async () => { if (++calls === 1) throw new Error('offline'); return ok(action()); };
  h.control.open(); h.control.choose('bolt'); h.control.pick(4); await h.control.submit();
  h.control.close(); h.control.open(); h.control.choose('bolt'); h.control.pick(4); await h.control.submit();
  assert.equal(calls, 2); assert.equal(h.writes.length, 1);
});
test('a connection that becomes stale during acknowledgement cannot post', async () => {
  const h = harness(), ack = pending(); h.connection.shown = () => ack.promise;
  h.control.open(); h.control.choose('bolt'); h.control.pick(4); const send = h.control.submit();
  h.control.setOnline(false); ack.resolve(ok(action())); await send; assert.deepEqual(h.writes, []); assert.equal(h.control.model().canSubmit, false);
});

test('too few candidates offer an empty cast, never a partial target set', async () => {
  const h = harness(action(legal([4], 2, 2))); h.control.open(); h.control.choose('bolt'); h.control.pick(4);
  assert.deepEqual(h.control.model().picked, []); assert.equal(h.control.model().emptyCast, true);
  await h.control.submit(); assert.deepEqual(h.writes[0].targets, []);
});

// The host refuses a stale asking with the next one in hand: the refresh the write triggers delivers that new
// asking, and the refusal has to survive it, or the player never learns why nothing happened.
test('a refusal is still readable once the refresh after the write has moved the asking on', async () => {
  const h = harness(); h.connection.submit = async () => ({ ok: false, status: 409, body: { message: 'Another client answered first.' } });
  h.control.open(); h.control.choose('bolt'); h.control.pick(4); await h.control.submit();
  h.receive({ ...action(), waitingAsked: 12 });
  assert.equal(h.control.model().error, 'Another client answered first.');
  assert.equal(h.control.model().opened, false);
});

test('an uncertain write\'s warning survives the refresh, and the player\'s next move takes it down', async () => {
  const h = harness(); h.connection.submit = async () => { throw new Error('lost'); };
  h.control.open(); h.control.choose('bolt'); h.control.pick(4); await h.control.submit();
  h.receive({ ...action(), waitingAsked: 12 });
  assert.match(h.control.model().error, /may have arrived/);
  h.control.open();
  assert.equal(h.control.model().error, '');
});

test('a question that stops being combat closes the draft and keeps the host\'s last word', async () => {
  const h = harness(); h.connection.submit = async () => ({ ok: false, status: 409, body: { message: 'Refused' } });
  h.control.open(); h.control.choose('bolt'); h.control.pick(4); await h.control.submit();
  h.receive({ ...action(), waitingFor: 'Speed', waitingAsked: 12 });
  assert.equal(h.control.model().opened, false);
  assert.equal(h.control.model().error, 'Refused');
});
