import test from 'node:test';
import assert from 'node:assert/strict';
import { preferredPresentation, savePresentation, presentationPath } from './battle-preferences.js';
test('the battle view is the game, an older choice of the table included, and seat credentials are untouched', () => {
  const values = new Map([['downfall.table.seats', 'private']]);
  const storage = { getItem: key => values.get(key), setItem: (key, value) => values.set(key, value) };
  assert.equal(presentationPath(preferredPresentation(storage)), '/battle');
  values.set('downfall.presentation', 'table');
  assert.equal(presentationPath(preferredPresentation(storage)), '/battle', 'the old dropdown\'s table choice no longer holds a player on it');
  assert.equal(values.get('downfall.table.seats'), 'private');
});
test('only a browser that stores the archive\'s own value reaches the classic table', () => {
  const values = new Map();
  const storage = { getItem: key => values.get(key), setItem: (key, value) => values.set(key, value) };
  savePresentation(storage, 'table'); assert.equal(presentationPath(preferredPresentation(storage)), '/play');
  savePresentation(storage, 'battle'); assert.equal(presentationPath(preferredPresentation(storage)), '/battle');
});
test('invalid or unavailable preferences play the battle', () => {
  for (const storage of [null, { getItem() { return '/elsewhere'; } }, { getItem() { throw Error(); }, setItem() { throw Error(); } }]) {
    assert.equal(preferredPresentation(storage), 'battle'); assert.doesNotThrow(() => savePresentation(storage, 'battle'));
  }
  assert.equal(presentationPath('https://elsewhere'), '/battle');
});
