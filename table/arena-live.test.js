import test from 'node:test';
import assert from 'node:assert/strict';
import { createLiveConnection, livePresentation } from './arena-live.js';

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
  assert.deepEqual(result.creatures.map(one => one.label), ['Main · 1', 'Main · 4']);
  assert.deepEqual(result.creatures.map(one => one.name), ['Main', 'Main']);
});
