import { test } from 'node:test';
import assert from 'node:assert/strict';
import { isSettled, orderOf, tap, untapped } from './ties.js';

const groups = [[1, 2, 3], [5, 6]];

test('with no tap the order is the one the rolls left', () => {
  assert.deepEqual(orderOf(groups, []), [1, 2, 3, 5, 6]);
  assert.equal(isSettled(groups, []), false);
});

test('the first tap in a group takes its first place, and a creature moves only within its own group', () => {
  const tapped = tap(groups, tap(groups, [], 3), 6);

  assert.deepEqual(orderOf(groups, tapped), [3, 1, 2, 6, 5]);
});

test('a group is settled once one creature is left, since the last place has only one taker', () => {
  const tapped = tap(groups, tap(groups, [], 2), 1);

  assert.deepEqual(untapped(groups, tapped), [[3], [5, 6]]);
  assert.equal(isSettled(groups, tapped), false);
  assert.equal(isSettled(groups, tap(groups, tapped, 6)), true);
});

test('a tap on a creature the options do not offer, or on one already tapped, changes nothing', () => {
  assert.deepEqual(tap(groups, [1], 1), [1]);
  assert.deepEqual(tap(groups, [1], 9), [1]);
});

test('missing options read as nothing to order', () => {
  assert.deepEqual(orderOf(undefined, undefined), []);
  assert.equal(isSettled(undefined, undefined), true);
});
