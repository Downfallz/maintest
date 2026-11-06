// The settings every new game starts from (ADR 0140): how confirmed results are read, at what speed, whether
// first-use help shows, and whether motion is reduced. A signed-in player's are kept by the host with the
// account; a signed-out browser keeps its own. A game can override any of them for its own duration, in this
// browser, without changing them; a new game starts from them again.

import { accountRequest } from './account.js';

export const GAME_DEFAULTS = Object.freeze({ pace: 'step', speed: 1, help: true, reducedMotion: false });
export const SPEEDS = Object.freeze([0.5, 1, 1.5, 2]);
const GLOBAL_KEY = 'downfall.arena.game-settings.v1';
const OVERRIDE_KEY = 'downfall.arena.game-overrides.v1';
// Overrides outlive their games only until enough newer games push them out.
const KEPT_GAMES = 20;
// battle-guidance.js's own key, read here only to carry a muted choice over.
const LEGACY_HELP_KEY = 'downfall.arena.help.v1';

/** The fields of `value` that are settings the match settings offer, and nothing else. */
export function settingsOf(value) {
  const out = {};
  if (value?.pace === 'step' || value?.pace === 'auto') out.pace = value.pace;
  if (SPEEDS.includes(Number(value?.speed))) out.speed = Number(value.speed);
  if (typeof value?.help === 'boolean') out.help = value.help;
  if (typeof value?.reducedMotion === 'boolean') out.reducedMotion = value.reducedMotion;
  return out;
}

/** What a game plays with: its own override over the player's settings, over the defaults. */
export function effectiveSettings(globals, override) {
  return { ...GAME_DEFAULTS, ...settingsOf(globals), ...settingsOf(override) };
}

function read(storage, key) {
  try { return JSON.parse(storage?.getItem(key) ?? 'null'); } catch { return null; }
}
function write(storage, key, value) {
  try { storage?.setItem(key, JSON.stringify(value)); } catch { /* Settings still apply for this visit. */ }
}

/** The settings this browser last knew: a signed-out player's own, or a signed-in player's last read. */
export function localSettings(storage) {
  const saved = read(storage, GLOBAL_KEY);
  // A browser that muted the phase tips before these settings existed keeps them muted.
  const legacy = saved ? {} : read(storage, LEGACY_HELP_KEY)?.muted === true ? { help: false } : {};
  return { ...GAME_DEFAULTS, ...legacy, ...settingsOf(saved) };
}
export function saveLocalSettings(storage, value) {
  const settings = { ...GAME_DEFAULTS, ...settingsOf(value) };
  write(storage, GLOBAL_KEY, settings);
  return settings;
}

/** One game's override, as a page changed it for that game; empty for a game that changed nothing. */
export function gameOverride(storage, game) {
  const all = read(storage, OVERRIDE_KEY);
  return game && Array.isArray(all) ? settingsOf(all.find(one => one?.game === game)) : {};
}
export function saveGameOverride(storage, game, change) {
  if (!game) return {};
  const all = (Array.isArray(read(storage, OVERRIDE_KEY)) ? read(storage, OVERRIDE_KEY) : []).filter(one => one && typeof one.game === 'string');
  const merged = { ...settingsOf(all.find(one => one.game === game)), ...settingsOf(change) };
  write(storage, OVERRIDE_KEY, [{ game, ...merged }, ...all.filter(one => one.game !== game)].slice(0, KEPT_GAMES));
  return merged;
}

/**
 * The player's settings: the account's when the host keeps them for a signed-in player, this browser's
 * otherwise. The host's answer is remembered locally, so the next page opens with it before asking again.
 */
export async function loadSettings(storage, request = accountRequest) {
  try {
    const answer = await request('/api/account/preferences');
    // An account that never chose starts from what this browser chose before signing in.
    return { settings: answer.saved ? saveLocalSettings(storage, answer.preferences) : localSettings(storage), account: true };
  } catch {
    return { settings: localSettings(storage), account: false };
  }
}

/** Saves the player's settings where they are kept: with the account when signed in, and in this browser always. */
export async function saveSettings(storage, value, { account = false, request = accountRequest } = {}) {
  const settings = saveLocalSettings(storage, value);
  if (account) await request('/api/account/preferences', settings, undefined, 'PUT');
  return settings;
}

/** Where the panel says the settings are kept, and what they change. */
export function settingsWhere(account) {
  return account
    ? 'Kept with your account. A game already begun keeps its own settings; change them there from Match settings.'
    : 'Kept in this browser until you sign in. A game already begun keeps its own settings; change them there from Match settings.';
}

/** The menu's Game settings panel: it reads the settings when it opens and saves each change at once. */
export function mountGameSettings(doc, storage, { request = accountRequest } = {}) {
  const el = id => doc.getElementById(id);
  const fields = { pace: el('game-settings-pace'), speed: el('game-settings-speed'), help: el('game-settings-help'), reducedMotion: el('game-settings-motion') };
  let account = false, ticket = 0;
  function show(settings) {
    fields.pace.value = settings.pace; fields.speed.value = String(settings.speed);
    fields.help.checked = settings.help; fields.reducedMotion.checked = settings.reducedMotion;
    el('game-settings-where').textContent = settingsWhere(account);
  }
  function problem(message) { el('game-settings-problem').textContent = message ?? ''; el('game-settings-problem').hidden = !message; }
  async function refresh() {
    const mine = ++ticket;
    show(localSettings(storage));
    const answer = await loadSettings(storage, request);
    if (mine !== ticket) return;
    account = answer.account; show(answer.settings);
  }
  el('game-settings-open').addEventListener('click', () => { problem(null); void refresh(); });
  el('game-settings').addEventListener('submit', event => event.preventDefault());
  el('game-settings').addEventListener('change', async () => {
    ticket++;
    const value = { pace: fields.pace.value, speed: Number(fields.speed.value), help: fields.help.checked, reducedMotion: fields.reducedMotion.checked };
    try { await saveSettings(storage, value, { account, request }); problem(null); }
    catch (error) { problem(`Saved in this browser only: ${error?.message ?? 'the host did not answer'}.`); }
  });
  show(localSettings(storage));
  // Whether the host keeps them is the host's answer, read again whenever the player signs in or out.
  return { setAccount() { void refresh(); } };
}
