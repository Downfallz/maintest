import test from 'node:test';
import assert from 'node:assert/strict';
import { createLiveConnection, creatureLabel, livePresentation } from './battle-live.js';

const tick = () => new Promise(resolve => setImmediate(resolve));
const pending = () => { let resolve; const promise = new Promise(done => { resolve = done; }); return { promise, resolve }; };
const ok = body => ({ ok: true, status: 200, body });
function harness() {
  const jobs = new Map(); let next = 0;
  const states = [], views = [];
  const connection = createLiveConnection({ onState: state => states.push(state), onView: (...view) => views.push(view),
    schedule: (run, delay) => { jobs.set(++next, { run, delay }); return next; }, cancel: id => jobs.delete(id) });
  const transport = { catalogue: async () => ok({ cards: [] }), seat: async () => ok({ board: {}, feedNext: 0 }), events: () => new Promise(() => {}) };
  return { connection, transport, jobs, states, views, run: async delay => {
    const [id, job] = [...jobs].find(([, job]) => job.delay === delay) ?? [];
    assert.ok(job, `a ${delay}ms job exists`); jobs.delete(id); await job.run();
  } };
}

const planning = Object.freeze({ board: Object.freeze({
  allies: Object.freeze([Object.freeze({ id: 17, name: 'Host name', isAlive: true })]),
  enemies: Object.freeze([Object.freeze({ id: 22, isAlive: false, isStunned: true })]),
  subPhase: 'Speed', activationCursor: 0, slot: 'Player1',
  timeline: Object.freeze([Object.freeze({ creature: 17, owner: 'Player1', speed: 'Quick' })]) }) });

test('live presentation borrows the seat board as the host served it, each creature on its side', () => {
  const result = livePresentation(planning);
  assert.equal(result.creatures[0].name, 'Host name');
  assert.equal(result.creatures[0].enemy, false);
  assert.equal(result.creatures[1].id, '22');
  assert.equal(result.creatures[1].enemy, true);
  assert.equal(result.creatures[1].isAlive, false);
  assert.equal(result.creatures[1].isStunned, true);
});

test('no slot is acting while the round is planned; the cursor\'s creature is during Activation', () => {
  assert.equal(livePresentation(planning).frame.actor, null);
  assert.equal(livePresentation({ board: { ...planning.board, subPhase: 'Activation' } }).frame.actor, '17');
});

test('no view at all presents an empty board', () => {
  assert.equal(livePresentation(null).creatures.length, 0);
  assert.equal(livePresentation(null).frame.actor, null);
});

test('polling uses feedNext including gaps and sends no shown acknowledgement', async () => {
  const h = harness(), requests = [];
  h.transport.seat = async (...args) => { requests.push(args); return ok({ board: {}, feed: [{ sequence: 31 }], feedNext: 83 }); };
  h.connection.start(h.transport); await tick(); await h.run(5000);
  assert.deepEqual(requests, [[0], [83]]);
  assert.equal(h.states.at(-1), 'live'); h.connection.stop();
});

test('notifications arriving during a poll owe exactly one more poll', async () => {
  const h = harness(), response = pending(); let notify, calls = 0;
  h.transport.seat = () => { calls++; return calls === 1 ? response.promise : Promise.resolve(ok({ board: {}, feedNext: 9 })); };
  h.transport.events = callback => { notify = callback; return new Promise(() => {}); };
  h.connection.start(h.transport); await tick(); notify(); notify();
  assert.equal(calls, 1); response.resolve(ok({ board: {}, feedNext: 5 })); await tick(); await h.run(0);
  assert.equal(calls, 2); h.connection.stop();
});

test('a response from a replaced seat cannot repaint the selected seat', async () => {
  const h = harness(), old = pending();
  h.connection.start({ ...h.transport, seat: () => old.promise }); await tick();
  h.connection.start({ ...h.transport, seat: async () => ok({ board: { slot: 'Player2' } }) }); await tick();
  old.resolve(ok({ board: { slot: 'Player1' } })); await tick();
  assert.equal(h.views.at(-1)[0].board.slot, 'Player2'); h.connection.stop();
});

test('a late catalogue after stopping opens no seat and paints no board', async () => {
  const h = harness(), catalogue = pending(); let calls = 0;
  h.connection.start({ ...h.transport, catalogue: () => catalogue.promise, seat: () => { calls++; } });
  h.connection.stop(); catalogue.resolve(ok({ cards: [] })); await tick();
  assert.equal(calls, 0); assert.deepEqual(h.views, [[null, null]]);
});

for (const status of [401, 403]) for (const source of ['catalogue', 'seat', 'events']) {
  test(`${status} from ${source} clears the board and stops polling and notifications`, async () => {
    const h = harness(); let signal;
    const transport = { ...h.transport, events: (_, value) => { signal = value; return new Promise(() => {}); } };
    transport[source] = async (_, value) => { if (value) signal = value; return { ok: false, status }; };
    h.connection.start(transport); await tick();
    assert.equal(h.states.at(-1), status === 401 ? 'signin' : 'revoked');
    assert.deepEqual(h.views.at(-1), [null, null]); assert.equal(h.jobs.size, 0); assert.equal(signal.aborted, true);
  });
}

test('a transient failure marks the old snapshot stale and polling recovers', async () => {
  const h = harness(); let calls = 0;
  h.transport.seat = async () => { if (++calls === 2) throw new Error('offline'); return ok({ board: { roundNumber: calls } }); };
  h.connection.start(h.transport); await tick(); await h.run(5000);
  assert.equal(h.states.at(-1), 'stale'); assert.equal(h.views.at(-1)[0].board.roundNumber, 1);
  await h.run(5000); assert.equal(h.states.at(-1), 'live'); assert.equal(h.views.at(-1)[0].board.roundNumber, 3); h.connection.stop();
});

test('closed notification streams reconnect with bounded backoff while polling continues', async () => {
  const h = harness(); h.transport.events = async () => ({ ok: true, status: 200 });
  h.connection.start(h.transport); await tick();
  for (const delay of [1000, 2000, 4000, 8000, 16000, 30000, 30000]) await h.run(delay);
  assert.ok([...h.jobs.values()].some(job => job.delay === 5000)); h.connection.stop(); assert.equal(h.jobs.size, 0);
});

test('identical host names remain distinguishable without replacing their names or identities', () => {
  const result = livePresentation({ board: { allies: [{ id: 1, name: 'Main' }], enemies: [{ id: 4, name: 'Main' }] } });
  assert.deepEqual(result.creatures.map(one => one.label), ['Creature 1', 'Creature 4'], 'the base creature reads as a creature, numbered');
  assert.deepEqual(result.creatures.map(one => one.name), ['Main', 'Main']);
});
test('one label names a creature on every surface: the name alone, the id beside a namesake, the id for an unknown', () => {
  const board = { allies: [{ id: 1, name: 'Main' }, { id: 2, name: 'Scout' }], enemies: [{ id: 4, name: 'Main' }, { id: 5, name: '' }] };
  assert.deepEqual([1, 2, 4, 5, 9].map(id => creatureLabel(board, id)), ['Creature 1', 'Scout', 'Creature 4', 'Creature 5', 'Creature 9']);
  const twins = { allies: [{ id: 1, name: 'Scout' }], enemies: [{ id: 4, name: 'Scout' }] };
  assert.deepEqual([1, 4].map(id => creatureLabel(twins, id)), ['Scout · 1', 'Scout · 4']);
  assert.equal(creatureLabel(null, 3), 'Creature 3'); assert.equal(creatureLabel(board, undefined), 'Creature ?');
});

test('the first stream\'s ready owes no second poll; a reconnect\'s ready refreshes', async () => {
  const h = harness(); let calls = 0; const notify = [];
  h.transport.seat = async () => { calls++; return ok({ board: {}, feedNext: 1 }); };
  h.transport.events = callback => { notify.push(callback); return new Promise(() => {}); };
  h.connection.start(h.transport); await tick();
  notify[0]({ event: 'ready' }); await tick();
  assert.equal(calls, 1, 'the board already in flight is the one ready announces');
  assert.equal(h.jobs.size, 1, 'only the next poll is due');
  notify[0]({ event: 'changed' }); await tick();
  assert.equal(calls, 2);
  h.connection.stop();
});

test('a reconnected stream\'s ready refreshes the board', async () => {
  const h = harness(); let calls = 0; const notify = [];
  h.transport.seat = async () => { calls++; return ok({ board: {}, feedNext: 1 }); };
  h.transport.events = callback => { notify.push(callback); return notify.length === 1 ? Promise.resolve({ ok: true, status: 200 }) : new Promise(() => {}); };
  h.connection.start(h.transport); await tick();
  // The reconnect job opens a stream that stays open: run it without waiting for it to close.
  const [id, reconnect] = [...h.jobs].find(([, job]) => job.delay === 1000);
  h.jobs.delete(id); void reconnect.run(); await tick();
  notify[1]({ event: 'ready' }); await tick();
  assert.equal(calls, 2);
  h.connection.stop();
});

test('a catalogue handed to a started connection is not read again', async () => {
  const h = harness(); let reads = 0;
  h.transport.catalogue = async () => { reads++; return ok({ cards: [] }); };
  h.connection.start(h.transport, { cards: ['known'] }); await tick();
  assert.equal(reads, 0);
  assert.deepEqual(h.views.at(-1)[1], { cards: ['known'] });
  h.connection.stop();
});

test('a read begun before a command cannot repaint its old board after the write', async () => {
  const h = harness(), old = pending(), write = pending(); let reads = 0;
  h.transport.seat = () => ++reads === 2 ? old.promise : Promise.resolve(ok({ board: { roundNumber: reads } }));
  h.transport.decide = () => write.promise;
  h.connection.start(h.transport); await tick(); const read = h.connection.refresh(); await tick();
  const send = h.connection.submit({ kind: 'Action', asked: 11 }); write.resolve(ok(null)); await send;
  old.resolve(ok({ board: { roundNumber: 2 } })); await read; await h.run(0);
  assert.deepEqual(h.views.filter(([view]) => view).map(([view]) => view.board.roundNumber), [1, 3]); h.connection.stop();
});
test('notifications during a command do not read until it settles', async () => {
  const h = harness(), write = pending(); let notify, reads = 0;
  h.transport.seat = async () => { reads++; return ok({ board: {} }); };
  h.transport.decide = () => write.promise;
  h.transport.events = callback => { notify = callback; return new Promise(() => {}); };
  h.connection.start(h.transport); await tick(); const send = h.connection.submit({ kind: 'Action' });
  notify({ event: 'changed' }); notify({ event: 'changed' }); await tick(); assert.equal(reads, 1);
  write.resolve(ok(null)); await send; await tick(); assert.equal(reads, 2); assert.deepEqual([...h.jobs.values()].map(job => job.delay), [5000]); h.connection.stop();
});
test('a failed write refreshes the board without retrying the command', async () => {
  const h = harness(); let writes = 0, reads = 0;
  h.transport.seat = async () => { reads++; return ok({ board: {} }); };
  h.transport.decide = async () => { writes++; throw new Error('lost'); };
  h.connection.start(h.transport); await tick(); await assert.rejects(h.connection.submit({ kind: 'Action' }), /lost/); await tick();
  assert.equal(reads, 2); assert.equal(writes, 1); h.connection.stop();
});
for (const status of [401, 403]) for (const source of ['shown', 'submit']) test(`${status} on ${source} erases the private board`, async () => {
  const h = harness(); h.connection.start(h.transport); await tick();
  if (source === 'shown') h.transport.seat = async () => ({ ok: false, status });
  else h.transport.decide = async () => ({ ok: false, status });
  await h.connection[source](11);
  assert.deepEqual(h.views.at(-1), [null, null]); assert.equal(h.states.at(-1), status === 401 ? 'signin' : 'revoked'); assert.equal(h.jobs.size, 0);
});

test('concession shares the command write fence and refreshes the authoritative result', async () => {
  const h = harness(), write = pending(); let concessions = 0, decisions = 0;
  h.transport.concede = () => { concessions++; return write.promise; };
  h.transport.decide = async () => { decisions++; return ok(null); };
  h.connection.start(h.transport); await tick();
  const first = h.connection.concede(); assert.equal(await h.connection.submit({ kind: 'Speed' }), null);
  assert.equal(await h.connection.concede(), null); write.resolve(ok(null)); await first; await tick();
  assert.equal(concessions, 1); assert.equal(decisions, 0); assert.equal(h.states.at(-1), 'live'); h.connection.stop();
});
test('finished history reads the selected seat from zero and a seat replacement fences the result', async () => {
  const h = harness(), full = pending(), reads = [];
  h.transport.seat = async since => { reads.push(since); return reads.length === 2 ? full.promise : ok({ board: {}, feedNext: 80 }); };
  h.connection.start(h.transport); await tick(); const history = h.connection.history();
  h.connection.start({ ...h.transport, seat: async () => ok({ board: {}, feedNext: 10 }) });
  full.resolve(ok({ over: true, board: {}, feedNext: 90 })); assert.equal(await history, null); assert.deepEqual(reads, [0, 0]); h.connection.stop();
});
test('recorded transports need no live notification channel', async () => {
  const h = harness(); delete h.transport.events; h.connection.start(h.transport); await tick();
  assert.equal(h.states.at(-1), 'live'); assert.deepEqual([...h.jobs.values()].map(job => job.delay), [5000]); h.connection.stop();
});
