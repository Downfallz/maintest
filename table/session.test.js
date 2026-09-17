import { test } from 'node:test';
import assert from 'node:assert/strict';
import { forget, heldSeats, seatsFromLocation } from './session.js';

// What a browser's localStorage is, for the three lines of it this page uses.
function storage(initial = {}) {
  const kept = { ...initial };
  return {
    kept,
    getItem: key => kept[key] ?? null,
    setItem: (key, value) => { kept[key] = value; },
    removeItem: key => { delete kept[key]; },
  };
}

// A browser in private mode, which throws on the way in rather than keeping nothing quietly.
const refusing = {
  getItem: () => { throw new Error('denied'); },
  setItem: () => { throw new Error('denied'); },
  removeItem: () => { throw new Error('denied'); },
};

test('every seat the link carries a token for is read off it, in board order', () => {
  assert.deepEqual(seatsFromLocation('?player2=beef&player1=cafe'), [
    { seat: 'player1', token: 'cafe' },
    { seat: 'player2', token: 'beef' },
  ]);
  assert.deepEqual(seatsFromLocation('?seat=player2'), []);
  assert.deepEqual(seatsFromLocation(''), []);
});

test('a token the link carries is kept, so a reload does not send the player back for a code', () => {
  const kept = storage();

  const first = heldSeats('?player1=cafe', kept);
  const afterReload = heldSeats('', kept);

  assert.deepEqual(first, [{ seat: 'player1', token: 'cafe' }]);
  assert.deepEqual(afterReload, first);
});

test('a second code on the same device adds its seat rather than replacing the first', () => {
  const kept = storage();

  heldSeats('?player1=cafe', kept);
  const both = heldSeats('?player2=beef', kept);

  assert.deepEqual(both, [{ seat: 'player1', token: 'cafe' }, { seat: 'player2', token: 'beef' }]);
});

test('a fresh token for a seat wins over the one this browser kept', () => {
  const kept = storage();

  heldSeats('?player1=cafe', kept);
  const now = heldSeats('?player1=f00d', kept);

  assert.deepEqual(now, [{ seat: 'player1', token: 'f00d' }]);
});

test('forgetting a seat is what lets the next table be joined', () => {
  const kept = storage();
  heldSeats('?player1=cafe', kept);

  forget(kept, 'player1');

  assert.deepEqual(heldSeats('', kept), []);
});

// The case that costs a player a trip back to the console: a stale token from an earlier table sits beside the
// code they just typed for this one, which the page has already taken out of the address bar.
test('forgetting the seat this table refused keeps the seat it accepted', () => {
  const kept = storage();
  heldSeats('?player1=stale&player2=beef', kept);

  forget(kept, 'player1');

  assert.deepEqual(heldSeats('', kept), [{ seat: 'player2', token: 'beef' }]);
});

test('forgetting a seat in a browser that keeps nothing is not an error', () => {
  assert.doesNotThrow(() => forget(refusing, 'player1'));
  assert.doesNotThrow(() => forget(null, 'player1'));
});

test('a browser that keeps nothing still plays the link it was opened with', () => {
  assert.deepEqual(heldSeats('?player1=cafe', refusing), [{ seat: 'player1', token: 'cafe' }]);
  assert.deepEqual(heldSeats('', refusing), []);
});

test('a page with no storage at all reads the link and nothing else', () => {
  assert.deepEqual(heldSeats('?player2=beef', null), [{ seat: 'player2', token: 'beef' }]);
  assert.deepEqual(heldSeats('', undefined), []);
});

test('what this browser kept is ignored when it is not a set of seats', () => {
  assert.deepEqual(heldSeats('', storage({ 'downfall.table.seats': 'not json' })), []);
  assert.deepEqual(heldSeats('', storage({ 'downfall.table.seats': '"player1"' })), []);
});
