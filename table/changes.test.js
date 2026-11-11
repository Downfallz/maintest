import test from 'node:test';
import assert from 'node:assert/strict';
import { accountMoved, backoff, createLobbyChanges, keepWatching, socketOrStream, streamEvents } from './changes.js';

const tick = () => new Promise(resolve => setImmediate(resolve));
const encode = text => new TextEncoder().encode(text);

// A clock the test turns by hand: what is scheduled, and the way to run one of them.
function clock() {
  const jobs = new Map(); let next = 0;
  return {
    jobs,
    schedule: (run, delay) => { jobs.set(++next, { run, delay }); return next; },
    cancel: id => jobs.delete(id),
    async run(delay) {
      const [id, job] = [...jobs].find(([, one]) => one.delay === delay) ?? [];
      assert.ok(job, `a ${delay}ms job exists`);
      jobs.delete(id);
      await job.run();
    },
  };
}

// A body whose chunks are handed over by the test, and which stays open until then.
function openBody() {
  const waiting = []; const queued = [];
  return {
    push(chunk) { const next = waiting.shift(); if (next) next({ value: encode(chunk), done: false }); else queued.push(chunk); },
    getReader: () => ({
      read: () => queued.length ? Promise.resolve({ value: encode(queued.shift()), done: false }) : new Promise(resolve => waiting.push(resolve)),
      cancel: async () => {},
    }),
  };
}

test('a stream tells each ready and change, carries its headers and the same-origin cookie', async () => {
  const chunks = ['event: ready\ndata: 0\n\n', ': keep-alive\n\nevent: changed\ndata: account 2\n\n'].map(encode);
  const body = { getReader: () => ({ read: async () => (chunks.length ? { value: chunks.shift(), done: false } : { done: true }) }) };
  const asked = []; const heard = [];
  const answer = await streamEvents(async (path, options) => { asked.push({ path, options }); return { ok: true, status: 200, body }; },
    '/api/lobby/events', { 'X-Seat-Token': 'cafe' }, event => heard.push(event), new AbortController().signal, clock());

  assert.equal(asked[0].path, '/api/lobby/events');
  assert.equal(asked[0].options.headers['X-Seat-Token'], 'cafe');
  assert.equal(asked[0].options.credentials, 'same-origin');
  assert.deepEqual(heard, [{ event: 'ready', data: '0' }, { event: 'changed', data: 'account 2' }]);
  assert.deepEqual(answer, { status: 200, ok: true, silent: false });
});

test('a stream silent past the keep-alive is taken for dropped and ends, so it is opened again', async () => {
  const time = clock(), body = openBody();
  const ending = streamEvents(async () => ({ ok: true, status: 200, body }), '/x', {}, () => {}, new AbortController().signal, { ...time, silence: 45000 });
  await tick();
  body.push(': keep-alive\n\n'); await tick();
  assert.deepEqual([...time.jobs.values()].map(job => job.delay), [45000], 'what is heard sets the watch again');

  await time.run(45000);

  assert.equal((await ending).silent, true);
});

test('a stream the page lets go of throws, as fetch does, rather than ending quietly', async () => {
  const controller = new AbortController(), body = openBody();
  const ending = streamEvents(async () => ({ ok: true, status: 200, body }), '/x', {}, () => {}, controller.signal, clock());
  await tick();

  controller.abort();

  await assert.rejects(ending, error => error.name === 'AbortError');
});

test('a stream that ends is opened again, a little later each time, and sooner once it said something', async () => {
  const time = clock(); let opened = 0;
  const watching = keepWatching({ open: async onEvent => { opened++; if (opened === 3) onEvent({ event: 'ready' }); return { ok: true, status: 200 }; }, onEvent: () => {}, ...time });
  await tick();
  await time.run(1000); await time.run(2000);

  assert.equal(opened, 3);
  assert.deepEqual([...time.jobs.values()].map(job => job.delay), [1000], 'the third one said ready: the wait starts over');
  watching.stop();
  assert.equal(time.jobs.size, 0);
});

for (const status of [401, 403, 404]) {
  test(`a stream the host answers ${status} is not opened again`, async () => {
    const time = clock(); let refused = null;
    keepWatching({ open: async () => ({ ok: false, status }), onEvent: () => {}, onRefused: answer => { refused = answer.status; }, ...time });
    await tick();

    assert.equal(refused, status);
    assert.equal(time.jobs.size, 0);
  });
}

test('back online, a stream waiting to open again opens now', async () => {
  const time = clock(); let opened = 0;
  const watching = keepWatching({ open: async () => { opened++; return opened === 1 ? { ok: true, status: 200 } : new Promise(() => {}); }, onEvent: () => {}, ...time });
  await tick();

  watching.now(); await tick();

  assert.equal(opened, 2);
  assert.equal(time.jobs.size, 0);
  watching.stop();
});

// A page: whether it is hidden, and its listeners, for the shared lobby stream to read.
function page() {
  const handlers = new Map();
  return {
    hidden: false,
    addEventListener: (name, handler) => handlers.set(name, handler),
    fire(name) { handlers.get(name)?.(); },
  };
}

test('the lobby has one stream however many parts of the page listen, and tells each of them', async () => {
  const doc = page(); let opened = 0; const body = openBody(); const heard = [];
  const changes = createLobbyChanges({ doc, win: page(), WebSocketImpl: null, fetchImpl: async () => { opened++; return { ok: true, status: 200, body }; }, ...clock() });

  changes.subscribe(event => heard.push(['bell', event.data]));
  changes.subscribe(event => heard.push(['friends', event.data]));
  await tick();
  body.push('event: changed\ndata: account 1\n\n'); await tick();

  assert.equal(opened, 1);
  assert.deepEqual(heard, [['bell', 'account 1'], ['friends', 'account 1']]);
});

test('a hidden page holds no lobby stream, and opens one when it is in view again', async () => {
  const doc = page(); const signals = [];
  const changes = createLobbyChanges({ doc, win: page(), WebSocketImpl: null, fetchImpl: async (_, options) => { signals.push(options.signal); return { ok: true, status: 200, body: openBody() }; }, ...clock() });
  changes.subscribe(() => {}); await tick();

  doc.hidden = true; doc.fire('visibilitychange'); await tick();
  assert.equal(signals[0].aborted, true, 'a phone in a pocket keeps no host awake');

  doc.hidden = false; doc.fire('visibilitychange'); await tick();
  assert.equal(signals.length, 2);
  assert.equal(signals[1].aborted, false);
});

test('the stream opening and the player\'s own lists moving are what the bell and the friends read on', () => {
  assert.equal(accountMoved({ event: 'ready', data: '0' }), true);
  assert.equal(accountMoved({ event: 'changed', data: 'account 3' }), true);
  assert.equal(accountMoved({ event: 'changed', data: 'lobby 7' }), false);
});

test('a read that failed is tried again, later each time, and the wait starts over once one answers', () => {
  const time = clock(); let runs = 0;
  const retry = backoff(() => { runs++; }, time);

  retry.failed(); retry.failed();
  assert.deepEqual([...time.jobs.values()].map(job => job.delay), [2000], 'one retry due, the second one later');
  retry.answered();
  assert.equal(time.jobs.size, 0);
  retry.failed();
  assert.deepEqual([...time.jobs.values()].map(job => job.delay), [1000]);
  assert.equal(runs, 0);
});

test('a sign-in opens the lobby stream again, so the host reads the new player from it', async () => {
  const doc = page(); const signals = [];
  const changes = createLobbyChanges({ doc, win: page(), WebSocketImpl: null, fetchImpl: async (_, options) => { signals.push(options.signal); return { ok: true, status: 200, body: openBody() }; }, ...clock() });
  changes.subscribe(() => {}); await tick();

  changes.reopen(); await tick();

  assert.equal(signals.length, 2);
  assert.equal(signals[0].aborted, true);
  assert.equal(signals[1].aborted, false);
});

// A WebSocket the test drives: what it was opened on, and the way to open it, say something, or close it.
function sockets() {
  const made = [];
  class FakeSocket {
    constructor(url) { this.url = url; this.closed = false; made.push(this); }
    close() { if (!this.closed) { this.closed = true; this.onclose?.(); } }
  }
  return { made, FakeSocket };
}
const negotiated = url => async path => (path.endsWith('/negotiate')
  ? { ok: true, status: 200, json: async () => ({ url }) }
  : { ok: true, status: 200, body: openBody() });

test('with a relay, the page hears through the address the host signed: ready once open, changed on each message', async () => {
  const { made, FakeSocket } = sockets(); const heard = [];
  const ending = socketOrStream(negotiated('wss://relay.example/a'), '/api/seat/player1', { 'X-Seat-Token': 'cafe' }, event => heard.push(event), new AbortController().signal, { WebSocketImpl: FakeSocket });
  await tick();
  made[0].onopen(); made[0].onmessage({ data: 'changed' }); made[0].close();

  assert.equal(made[0].url, 'wss://relay.example/a');
  assert.deepEqual(heard, [{ event: 'ready', data: '' }, { event: 'changed', data: 'changed' }]);
  assert.deepEqual(await ending, { status: 200, ok: true, socket: true });
});

test('a host without a relay is read through its own stream', async () => {
  const asked = [];
  const fetchImpl = async path => { asked.push(path); return path.endsWith('/negotiate') ? { ok: false, status: 404 } : { ok: true, status: 200, body: { getReader: () => ({ read: async () => ({ done: true }) }) } }; };
  await socketOrStream(fetchImpl, '/api/lobby-a', {}, () => {}, new AbortController().signal, { WebSocketImpl: sockets().FakeSocket });

  assert.deepEqual(asked, ['/api/lobby-a/negotiate', '/api/lobby-a/events']);
});

test('a relay that refuses the connection before it opens sends the page to the host\'s stream, and not back again', async () => {
  const { made, FakeSocket } = sockets(); const asked = [];
  const fetchImpl = async path => { asked.push(path); return path.endsWith('/negotiate') ? { ok: true, status: 200, json: async () => ({ url: 'wss://full' }) } : { ok: true, status: 200, body: { getReader: () => ({ read: async () => ({ done: true }) }) } }; };
  const first = socketOrStream(fetchImpl, '/api/lobby-b', {}, () => {}, new AbortController().signal, { WebSocketImpl: FakeSocket });
  await tick(); made[0].close(); await first;
  await socketOrStream(fetchImpl, '/api/lobby-b', {}, () => {}, new AbortController().signal, { WebSocketImpl: FakeSocket });

  assert.deepEqual(asked, ['/api/lobby-b/negotiate', '/api/lobby-b/events', '/api/lobby-b/events']);
});

test('a seat the host no longer knows is refused, with no connection opened', async () => {
  const { made, FakeSocket } = sockets();
  const answer = await socketOrStream(async () => ({ ok: false, status: 403 }), '/api/seat/player2', {}, () => {}, new AbortController().signal, { WebSocketImpl: FakeSocket });

  assert.deepEqual(answer, { status: 403, ok: false });
  assert.equal(made.length, 0);
});

test('a relay connection the page lets go of is closed, and the abort is thrown as fetch throws it', async () => {
  const { made, FakeSocket } = sockets(); const controller = new AbortController();
  const ending = socketOrStream(negotiated('wss://relay.example/c'), '/api/seat/player1', {}, () => {}, controller.signal, { WebSocketImpl: FakeSocket });
  await tick(); made[0].onopen();

  controller.abort();

  await assert.rejects(ending, error => error.name === 'AbortError');
  assert.equal(made[0].closed, true);
});
