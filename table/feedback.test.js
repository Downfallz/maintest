import test from 'node:test';
import assert from 'node:assert/strict';
import { createFeedback, feedbackSettings } from './feedback.js';

function store() {
  const values = new Map();
  return { getItem: key => values.get(key) ?? null, setItem: (key, value) => values.set(key, value) };
}

test('sound and haptics are disabled by default and persist after explicit opt-in', () => {
  const local = store();
  const first = createFeedback({}, local);
  assert.deepEqual(first.settings(), { sound: false, haptics: false });
  first.update({ haptics: true, sound: true });
  assert.deepEqual(feedbackSettings(local), { sound: true, haptics: true });
});

test('selection is silent without opt-in and vibrates only after enabling haptics', () => {
  const pulses = [];
  const f = createFeedback({ navigator: { vibrate: n => pulses.push(n) } }, store());
  f.cue('select');
  assert.deepEqual(pulses, []);
  f.update({ haptics: true });
  f.cue('select');
  f.cue('confirm');
  assert.deepEqual(pulses, [8, 18]);
});

test('optional feedback tolerates unavailable browser APIs and storage', () => {
  const f = createFeedback({}, null);
  f.update({ sound: true, haptics: true });
  assert.doesNotThrow(() => f.cue('select'));
});
