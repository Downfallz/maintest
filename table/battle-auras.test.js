import { test } from 'node:test';
import assert from 'node:assert/strict';
import { aurasShown, conditionAuras } from './battle-auras.js';

test('a creature with no lasting condition shows nothing on its figure', () => {
  assert.deepEqual(conditionAuras([]), { above: null, body: null });
});
test('a stun stands over the head beside one condition on the body', () => {
  assert.deepEqual(conditionAuras(['defense-gain', 'stun', 'health-loss']), { above: 'stun', body: 'health-loss' });
});
test('the body shows the condition that matters most to its life: a bleed over a regeneration over its defense', () => {
  assert.equal(conditionAuras(['health-gain', 'health-loss']).body, 'health-loss');
  assert.equal(conditionAuras(['defense-gain', 'health-gain']).body, 'health-gain');
  assert.equal(conditionAuras(['defense-gain', 'defense-loss']).body, 'defense-loss');
});
test('energy, initiative and damage changes stay on the badges alone', () => {
  assert.deepEqual(conditionAuras(['energy-gain', 'initiative-loss', 'damage-gain', 'other']), { above: null, body: null });
});
test('what the field shows is written creature by creature, the quiet ones left out', () => {
  assert.equal(aurasShown([['1', { above: 'stun', body: 'health-loss' }], ['2', { above: null, body: null }], ['4', { above: null, body: 'defense-gain' }]]),
    '1:stun+health-loss 4:defense-gain');
});
