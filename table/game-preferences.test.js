import { test } from 'node:test';
import assert from 'node:assert/strict';
import { effectiveSettings, GAME_DEFAULTS, gameOverride, loadSettings, localSettings, saveGameOverride, saveSettings, settingsOf } from './game-preferences.js';

function storage(initial = {}) {
  const values = new Map(Object.entries(initial));
  return { values, getItem: key => values.get(key) ?? null, setItem: (key, value) => values.set(key, value), removeItem: key => values.delete(key) };
}
const refused = async () => { throw Object.assign(new Error('Sign in first.'), { status: 401 }); };

test('a new game reads step by step at normal speed with help on', () => {
  assert.deepEqual(GAME_DEFAULTS, { pace: 'step', speed: 1, help: true, reducedMotion: false, reactions: true });
  assert.deepEqual(localSettings(storage()), GAME_DEFAULTS);
  assert.deepEqual(effectiveSettings(null, null), GAME_DEFAULTS);
});

test('only the settings the match settings offer are kept', () => {
  assert.deepEqual(settingsOf({ pace: 'fast', speed: 3, help: 'yes', reducedMotion: true, token: 'x' }), { reducedMotion: true });
  assert.deepEqual(settingsOf({ pace: 'auto', speed: '1.5' }), { pace: 'auto', speed: 1.5 });
});

test('a game override wins over the player settings, which win over the defaults', () => {
  assert.deepEqual(effectiveSettings({ pace: 'auto', speed: 2 }, { speed: 0.5 }), { ...GAME_DEFAULTS, pace: 'auto', speed: 0.5 });
});

test('a game override changes that game alone and leaves the player settings as they were', () => {
  const kept = storage();
  saveSettings(kept, { pace: 'auto', speed: 1.5, help: false, reducedMotion: false });

  saveGameOverride(kept, 'table-a', { pace: 'step' });
  saveGameOverride(kept, 'table-a', { speed: 2 });

  assert.deepEqual(gameOverride(kept, 'table-a'), { pace: 'step', speed: 2 });
  assert.deepEqual(gameOverride(kept, 'table-b'), {});
  assert.deepEqual(localSettings(kept), { pace: 'auto', speed: 1.5, help: false, reducedMotion: false, reactions: true });
});

test('only the latest games keep an override', () => {
  const kept = storage();

  for (let game = 0; game < 25; game++) saveGameOverride(kept, `table-${game}`, { pace: 'auto' });

  assert.deepEqual(gameOverride(kept, 'table-24'), { pace: 'auto' });
  assert.deepEqual(gameOverride(kept, 'table-0'), {});
});

test('a browser that muted the phase tips before keeps them muted until it saves settings', () => {
  const kept = storage({ 'downfall.arena.help.v1': JSON.stringify({ version: 1, muted: true, seen: [] }) });

  assert.equal(localSettings(kept).help, false);
  saveSettings(kept, { ...GAME_DEFAULTS, help: true });
  assert.equal(localSettings(kept).help, true);
});

test('a signed-in player reads the account settings and this browser remembers them', async () => {
  const kept = storage();
  const request = async path => { assert.equal(path, '/api/account/preferences'); return { saved: true, preferences: { pace: 'auto', speed: 2, help: false, reducedMotion: true } }; };

  const loaded = await loadSettings(kept, request);

  assert.deepEqual(loaded, { account: true, settings: { pace: 'auto', speed: 2, help: false, reducedMotion: true, reactions: true } });
  assert.deepEqual(localSettings(kept), loaded.settings);
});

test('an account that never chose starts from what this browser chose', async () => {
  const kept = storage();
  await saveSettings(kept, { ...GAME_DEFAULTS, speed: 0.5 });

  const loaded = await loadSettings(kept, async () => ({ saved: false, preferences: GAME_DEFAULTS }));

  assert.equal(loaded.settings.speed, 0.5);
});

test('signed out, the settings are this browser\'s and saving asks the host nothing', async () => {
  const kept = storage(), asked = [];
  await saveSettings(kept, { pace: 'auto' });

  const loaded = await loadSettings(kept, refused);
  await saveSettings(kept, { ...loaded.settings, speed: 2 }, { account: loaded.account, request: async (...args) => { asked.push(args); } });

  assert.equal(loaded.account, false);
  assert.equal(loaded.settings.pace, 'auto');
  assert.equal(asked.length, 0);
  assert.equal(localSettings(kept).speed, 2);
});

test('signed in, saving puts the whole settings with the account', async () => {
  const asked = [];

  await saveSettings(storage(), { pace: 'auto', speed: 1.5 }, { account: true, request: async (...args) => { asked.push(args); } });

  assert.deepEqual(asked, [['/api/account/preferences', { pace: 'auto', speed: 1.5, help: true, reducedMotion: false, reactions: true }, undefined, 'PUT']]);
});

test('the opponent\'s reactions show unless the player hid them, and one game can hide them alone', () => {
  assert.equal(effectiveSettings({}, {}).reactions, true);
  assert.equal(effectiveSettings({ reactions: false }, {}).reactions, false);
  assert.equal(effectiveSettings({ reactions: true }, { reactions: false }).reactions, false);
  assert.deepEqual(settingsOf({ reactions: 'no' }), {});
});
