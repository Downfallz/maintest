import test from 'node:test';
import assert from 'node:assert/strict';
import { accountMoved, createLobbyChanges, keepWatching, streamEvents } from './changes.js';

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
  const changes = createLobbyChanges({ doc, win: page(), fetchImpl: async () => { opened++; return { ok: true, status: 200, body }; }, ...clock() });

  changes.subscribe(event => heard.push(['bell', event.data]));
  changes.subscribe(event => heard.push(['friends', event.data]));
  await tick();
  body.push('event: changed\ndata: account 1\n\n'); await tick();

  assert.equal(opened, 1);
  assert.deepEqual(heard, [['bell', 'account 1'], ['friends', 'account 1']]);
});

test('a hidden page holds no lobby stream, and opens one when it is in view again', async () => {
  const doc = page(); const signals = [];
  const changes = createLobbyChanges({ doc, win: page(), fetchImpl: async (_, options) => { signals.push(options.signal); return { ok: true, status: 200, body: openBody() }; }, ...clock() });
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
