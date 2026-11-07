import { test } from 'node:test';
import assert from 'node:assert/strict';
import { saveSeen, seenSince } from './battle-seen.js';

function storage() {
  const values = new Map();
  return { getItem: key => values.get(key) ?? null, setItem: (key, value) => values.set(key, value) };
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
