import test from 'node:test';
import assert from 'node:assert/strict';
import { createArenaSound } from './arena-sound.js';
function harness(resume) {
  const calls = []; const param = { setValueAtTime() {}, exponentialRampToValueAtTime() {} };
  const context = { state: 'running', currentTime: 1, resume: resume ?? (async () => {}),
    createOscillator() { return { frequency: param, connect() {}, disconnect() {}, start() { calls.push('play'); }, stop() { calls.push('stop'); } }; },
    createGain() { return { gain: param, connect() {}, disconnect() {} }; } };
  const sound = createArenaSound({ createContext() { calls.push('create'); return context; }, onChange: value => calls.push(value) });
  return { sound, calls, context };
}
test('sound is silent until enabled and disabling stops active voices', async () => {
  const h = harness(); h.sound.play(); assert.deepEqual(h.calls, []);
  await h.sound.enable(); h.sound.play('recovery'); assert.deepEqual(h.calls, ['create', true, 'play', 'stop']);
  h.sound.disable(); h.sound.play(); assert.deepEqual(h.calls.slice(4), ['stop', false]);
});
test('a late resume cannot reenable sound after suspension', async () => {
  let finish; const h = harness(() => new Promise(resolve => { finish = resolve; }));
  const pending = h.sound.enable(); h.sound.disable(); finish(); await pending; h.sound.play();
  assert.deepEqual(h.calls, ['create', false]);
});
test('unavailable audio is optional and suspended contexts stay silent', async () => {
  const h = harness(async () => { throw Error('unavailable'); }); await h.sound.enable(); h.sound.play(); assert.deepEqual(h.calls, ['create', false]);
  const ready = harness(); await ready.sound.enable(); ready.context.state = 'suspended'; ready.sound.play(); assert.equal(ready.calls.includes('play'), false);
});
