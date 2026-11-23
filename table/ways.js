// Settings, Account, Ways to sign in (sign-in methods plan, P4, ADR 0204): the providers the signed-in account uses,
// and the others this host offers. Adding one goes through that provider's own sign-in, then
// `POST /api/account/identities/<provider>`; removing one is `DELETE` there, and signs the account out on every other
// device. The host decides what may be added or removed; this page draws what it answered.

import { accountRequest, confirmAction, loadGoogle, signInWays } from './account.js';
import { startDiscord } from './discord.js';

export const WAYS_ROUTE = '/api/account/identities';

// A note the Discord return page hands to Settings in the same tab, once a way in was added there.
const NOTE = 'downfall.waysNote';

/** How a player reads a provider's name; a provider this page does not know by its own name, capitalised. */
export function wayName(provider) {
  const known = { google: 'Google', discord: 'Discord', microsoft: 'Microsoft', dev: 'A typed name' }[provider];
  return known ?? String(provider).slice(0, 1).toUpperCase() + String(provider).slice(1);
}

/**
 * The rows Settings draws: each way the account uses, removable while it is not the only one, then each way the host
 * offers that the account does not use yet, to add. `held` is the host's `identities`, `me` its `/api/me`.
 */
export function wayRows(held, me) {
  const used = (Array.isArray(held) ? held : []).map(way => way?.provider).filter(provider => typeof provider === 'string');
  const offered = signInWays(me);
  return [
    ...used.map(provider => ({ provider, name: wayName(provider), held: true, removable: used.length > 1 })),
    ...offered.filter(way => !used.includes(way.provider)).map(way => ({ provider: way.provider, name: wayName(way.provider), held: false, clientId: way.clientId })),
  ];
}

/** What removing a way in asks first: the player is signed out on every other device. */
export function removeWords(provider) {
  const name = wayName(provider);
  return {
    title: `Remove ${name}?`,
    detail: `You will not be able to sign in with ${name} any more. You stay signed in here. Every other device is signed out.`,
    action: `Remove ${name}`,
  };
}

/** Hands a line to Settings for the page the tab goes to next. */
export function keepWaysNote(storage, text) {
  try { storage?.setItem(NOTE, text); } catch { /* Settings then opens without a line. */ }
}

/** The line a return page handed over, read once. */
export function takeWaysNote(storage) {
  try {
    const note = storage?.getItem(NOTE) ?? null;
    storage?.removeItem(NOTE);
    return typeof note === 'string' && note ? note : null;
  } catch { return null; }
}

function tabStorage() {
  try { return globalThis.sessionStorage ?? null; } catch { return null; }
}

/**
 * Draws the ways in, in Settings' Account tab, for the account the navigation says is signed in. `openSettings`
 * opens Settings on a tab, which a note handed over by the Discord return page asks for.
 */
export function mountWays(doc, { request = accountRequest, google = () => loadGoogle(doc), session = tabStorage(), win = globalThis, openSettings = () => {} } = {}) {
  const el = id => doc.getElementById(id);
  const list = el('ways-list');
  if (!list) return { setAccount() {} };
  const say = (id, text = '') => { el(id).textContent = text; el(id).hidden = !text; };
  let me = null;
  let account = null;

  function draw(held) {
    list.replaceChildren(...wayRows(held, me).map(row => {
      const item = doc.createElement('li');
      item.className = 'app-settings-row';
      const words = doc.createElement('div');
      const name = doc.createElement('strong');
      name.textContent = row.name;
      const hint = doc.createElement('small');
      hint.textContent = row.held ? (row.removable ? 'You sign in with it.' : 'Your only way to sign in. Add another before you can remove it.') : 'You do not sign in with it yet.';
      words.append(name, hint);
      item.append(words);
      if (row.held && row.removable) item.append(removeButton(row.provider));
      if (!row.held && row.provider === 'discord') item.append(discordButton(row));
      if (!row.held && row.provider === 'google') item.append(googleSlot(row));
      return item;
    }));
  }

  function removeButton(provider) {
    const button = doc.createElement('button');
    button.type = 'button';
    button.className = 'secondary';
    button.textContent = 'Remove';
    button.setAttribute('aria-label', `Remove ${wayName(provider)}`);
    button.addEventListener('click', async () => {
      say('ways-problem'); say('ways-note');
      const words = removeWords(provider);
      if (!await confirmAction(doc, words.title, words.detail, words.action)) return;
      button.disabled = true;
      try {
        draw((await request(`${WAYS_ROUTE}/${provider}`, undefined, undefined, 'DELETE')).identities);
        say('ways-note', `${wayName(provider)} removed. Other devices are signed out.`);
      } catch (error) {
        say('ways-problem', error.message);
        button.disabled = false;
      }
    });
    return button;
  }

  function discordButton(row) {
    const button = doc.createElement('button');
    button.type = 'button';
    button.className = 'secondary';
    button.textContent = 'Add';
    button.setAttribute('aria-label', 'Add Discord');
    button.addEventListener('click', async () => {
      say('ways-problem'); say('ways-note');
      button.disabled = true;
      win.addEventListener?.('pageshow', event => { if (event.persisted) button.disabled = false; }, { once: true });
      try {
        await startDiscord({ clientId: row.clientId, origin: win.location.origin, purpose: 'link', storage: session, go: url => win.location.assign(url) });
      } catch (error) {
        say('ways-problem', error.message);
        button.disabled = false;
      }
    });
    return button;
  }

  // Google draws its own button; its answer is posted to the ways route instead of signing in.
  function googleSlot(row) {
    const slot = doc.createElement('div');
    slot.className = 'way-google';
    if (!row.clientId) return slot;
    google().then(id => {
      id.initialize({
        client_id: row.clientId,
        callback: response => {
          say('ways-problem'); say('ways-note');
          request(`${WAYS_ROUTE}/google`, { credential: response.credential })
            .then(answer => { draw(answer.identities); say('ways-note', 'Google added. You can sign in with it now.'); })
            .catch(error => say('ways-problem', error.message));
        },
        ux_mode: 'popup',
        cancel_on_tap_outside: true,
      });
      id.renderButton(slot, { theme: 'filled_black', shape: 'rectangular', text: 'continue_with', size: 'large' });
    }).catch(error => say('ways-problem', error.message));
    return slot;
  }

  async function refresh() {
    if (!account) { list.replaceChildren(); return; }
    try {
      draw((await request(WAYS_ROUTE)).identities);
    } catch (error) {
      say('ways-problem', error.message);
    }
  }

  return {
    /** The account signed in now, or null, and the host's /api/me it was read from. */
    async setAccount(next, answer) {
      const changed = (next?.id ?? null) !== (account?.id ?? null);
      account = next ?? null;
      me = answer ?? me;
      el('ways').hidden = !account || signInWays(me).length === 0;
      if (!changed) return;
      say('ways-problem'); say('ways-note');
      await refresh();
      const note = account ? takeWaysNote(session) : null;
      if (note) { openSettings('account'); say('ways-note', note); }
    },
    refresh,
  };
}
