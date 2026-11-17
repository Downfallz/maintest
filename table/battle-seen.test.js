import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createSeenSender, saveSeen, seenSince, SEND_EVERY_MS, presentAt, savePresent } from './battle-seen.js';

function storage() {
  const values = new Map();
  return { getItem: key => values.get(key) ?? null, setItem: (key, value) => values.set(key, value) };
}

// A clock the test moves, and the one-shot waits it holds until they are due.
function clock() {
  let now = 0, next = 1;
  const waits = new Map();
  return {
    now: () => now,
    later: (run, ms) => { const id = next++; waits.set(id, { run, at: now + ms }); return id; },
    cancel: id => { waits.delete(id); },
    advance(ms) {
      now += ms;
      for (const [id, wait] of [...waits]) if (wait.at <= now) { waits.delete(id); wait.run(); }
    },
    pending: () => waits.size,
  };
}

function sender(time = clock()) {
  const sent = [];
  const seen = createSeenSender({ send: sequence => { sent.push(sequence); return Promise.resolve({ ok: true }); }, now: time.now, later: time.later, cancel: time.cancel });
  return { seen, sent, time };
}

test('each seat of each game remembers the last result this browser showed it', () => {
  const kept = storage();

  saveSeen(kept, 'table-a:player1', 12); saveSeen(kept, 'table-a:player2', 9); saveSeen(kept, 'table-a:player1', 15);

  assert.equal(seenSince(kept, 'table-a:player1'), 15);
  assert.equal(seenSince(kept, 'table-a:player2'), 9);
  assert.equal(seenSince(kept, 'table-b:player1'), null);
});

test('only the newest twenty games are remembered, and a refused store remembers nothing without failing', () => {
  const kept = storage();
  for (let game = 0; game < 25; game++) saveSeen(kept, `table-${game}:player1`, game);

  assert.equal(seenSince(kept, 'table-24:player1'), 24);
  assert.equal(seenSince(kept, 'table-0:player1'), null);
  const refused = { getItem: () => { throw new Error('denied'); }, setItem: () => { throw new Error('denied'); } };
  assert.doesNotThrow(() => saveSeen(refused, 'table-a:player1', 3));
  assert.equal(seenSince(refused, 'table-a:player1'), null);
});

test('the larger of what this browser showed and what the host kept is read (ADR 0194)', () => {
  const kept = storage();
  saveSeen(kept, 'table-a:player1', 8);

  assert.equal(seenSince(kept, 'table-a:player1', { sequence: 14, at: '2026-11-16T10:00:00Z' }), 14);
  assert.equal(seenSince(kept, 'table-a:player1', { sequence: 3, at: '2026-11-16T10:00:00Z' }), 8);
  assert.equal(seenSince(kept, 'table-b:player1', { sequence: 5, at: '2026-11-16T10:00:00Z' }), 5);
  assert.equal(seenSince(kept, 'table-b:player1', null), null);
  assert.equal(seenSince(kept, 'table-a:player1', { sequence: 'x' }), 8);
});

test('the first mark is sent at once', () => {
  const { seen, sent } = sender();

  seen.shown(4);

  assert.deepEqual(sent, [4]);
});

test('marks arriving within a few seconds are sent once, the last of them, when the wait is over', () => {
  const { seen, sent, time } = sender();
  seen.shown(4);

  time.advance(1000); seen.shown(6);
  time.advance(1000); seen.shown(9);
  time.advance(SEND_EVERY_MS);

  assert.deepEqual(sent, [4, 9]);
});

test('a mark after a quiet spell is sent at once', () => {
  const { seen, sent, time } = sender();
  seen.shown(4);

  time.advance(SEND_EVERY_MS);
  seen.shown(7);

  assert.deepEqual(sent, [4, 7]);
});

test('a mark held for the wait is sent at once when the page is hidden, and not again after', () => {
  const { seen, sent, time } = sender();
  seen.shown(4); seen.shown(6);

  seen.flush();
  time.advance(SEND_EVERY_MS);

  assert.deepEqual(sent, [4, 6]);
  assert.equal(time.pending(), 0);
});

test('a mark not further than the last one sent is not sent again', () => {
  const { seen, sent, time } = sender();
  seen.shown(9);

  time.advance(SEND_EVERY_MS); seen.shown(9);
  time.advance(SEND_EVERY_MS); seen.shown(5);

  assert.deepEqual(sent, [9]);
});

test('a refused mark is sent again with the next mark after the wait', async () => {
  const time = clock();
  const sent = [];
  let refuse = true;
  const seen = createSeenSender({ send: sequence => { sent.push(sequence); return Promise.resolve({ ok: !refuse }); }, now: time.now, later: time.later, cancel: time.cancel });
  seen.shown(3);
  await Promise.resolve(); await Promise.resolve();
  refuse = false;

  time.advance(SEND_EVERY_MS); seen.shown(3);

  assert.deepEqual(sent, [3, 3]);
});

test('a refused mark is held, so the page hidden next sends it again', async () => {
  const time = clock();
  const sent = [];
  let refuse = true;
  const seen = createSeenSender({ send: sequence => { sent.push(sequence); return Promise.resolve({ ok: !refuse }); }, now: time.now, later: time.later, cancel: time.cancel });
  seen.shown(6);
  await Promise.resolve(); await Promise.resolve();
  refuse = false;

  seen.flush();

  assert.deepEqual(sent, [6, 6]);
});

test('the time a seat was last on screen is kept for each game, the newest only', () => {
  const store = new Map();
  const storage = { getItem: key => store.get(key) ?? null, setItem: (key, value) => store.set(key, value) };
  savePresent(storage, 'a:player1', Date.parse('2026-11-17T10:00:00Z'));
  savePresent(storage, 'b:player2', Date.parse('2026-11-17T11:00:00Z'));
  savePresent(storage, 'a:player1', Date.parse('2026-11-17T12:00:00Z'));

  assert.equal(presentAt(storage, 'a:player1'), '2026-11-17T12:00:00.000Z');
  assert.equal(presentAt(storage, 'b:player2'), '2026-11-17T11:00:00.000Z');
  assert.equal(presentAt(storage, 'c:player1'), null);
  assert.equal(JSON.parse(store.get('downfall.battle.present.v1')).length, 2);
});

test('a refused or broken store reads as never seen', () => {
  assert.equal(presentAt(null, 'a:player1'), null);
  assert.equal(presentAt({ getItem: () => '{oops' }, 'a:player1'), null);
  savePresent({ getItem: () => null, setItem: () => { throw new Error('full'); } }, 'a:player1', Date.now());
});
