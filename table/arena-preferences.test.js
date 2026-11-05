import test from 'node:test';
import assert from 'node:assert/strict';
import { preferredPresentation, savePresentation, presentationPath } from './arena-preferences.js';
test('arena rollout is opt-in, reversible and never changes seat credentials', () => {
  const values = new Map([['downfall.table.seats', 'private']]);
  const storage = { getItem: key => values.get(key), setItem: (key, value) => values.set(key, value) };
  assert.equal(presentationPath(preferredPresentation(storage)), '/play');
  savePresentation(storage, 'arena'); assert.equal(presentationPath(preferredPresentation(storage)), '/arena');
  savePresentation(storage, 'table'); assert.equal(presentationPath(preferredPresentation(storage)), '/play');
  assert.equal(values.get('downfall.table.seats'), 'private');
});
test('invalid or unavailable preferences safely use the table', () => {
  for (const storage of [null, { getItem() { return '/elsewhere'; } }, { getItem() { throw Error(); }, setItem() { throw Error(); } }]) {
    assert.equal(preferredPresentation(storage), 'table'); assert.doesNotThrow(() => savePresentation(storage, 'arena'));
  }
  assert.equal(presentationPath('https://elsewhere'), '/play');
});
