import { test } from 'node:test';
import assert from 'node:assert/strict';
import { purchaseLog } from './battle-chronology.js';

const reveal = (sequence, choices) => ({ sequence, event: { kind: 'PurchasesRevealed', choices } });

test('purchases are ordered by their reveal\'s sequence, then by the order the picks were made in it', () => {
  const log = purchaseLog();
  log.receive([reveal(30, [{ creature: 1, tier: 'c' }]), reveal(10, [{ creature: 1, tier: 'a' }, { creature: 2, tier: 'x' }, { creature: 1, tier: 'b' }])]);
  assert.deepEqual(log.order(1), ['a', 'b', 'c']); assert.deepEqual(log.order('2'), ['x']);
});
test('a frame asks for the purchases known up to its own sequence', () => {
  const log = purchaseLog();
  log.receive([reveal(10, [{ creature: 1, tier: 'a' }]), reveal(40, [{ creature: 1, tier: 'b' }])]);
  assert.deepEqual(log.order(1, 39), ['a']); assert.deepEqual(log.order(1, 40), ['a', 'b']); assert.deepEqual(log.order(1, 9), []);
});
test('an entry read twice counts once, and other events are not purchases', () => {
  const log = purchaseLog();
  const feed = [reveal(10, [{ creature: 1, tier: 'a' }]), { sequence: 11, event: { kind: 'CombatActionResolved' } }];
  log.receive(feed); log.receive(feed);
  assert.deepEqual(log.order(1), ['a']); assert.equal(log.size(), 1);
});
test('a reset forgets every purchase, as a new seat starts from its own feed', () => {
  const log = purchaseLog(); log.receive([reveal(10, [{ creature: 1, tier: 'a' }])]); log.reset();
  assert.deepEqual(log.order(1), []); log.receive([reveal(10, [{ creature: 1, tier: 'b' }])]); assert.deepEqual(log.order(1), ['b']);
});
