// Settings, Account, Ways to sign in (sign-in methods plan, P4, ADR 0204): the providers the signed-in account uses,
// and the others this host offers. Adding one goes through that provider's own sign-in, then
// `POST /api/account/identities/<provider>`; removing one is `DELETE` there, and signs the account out on every other
// device. Both ask for a sign-in made in the last ten minutes, which the host hands the page in a sign-in's answer
// (`recent`) and never in a cookie, so a session cookie taken from a device left signed in cannot change how the
// account is reached. The host decides what may be added or removed; this page draws what it answered.

import { accountRequest, answerGoogle, confirmAction, loadGoogle, prepareGoogle, forgetRecentSignIn, recentSignIn, signInWays } from './account.js';
import { startDiscord } from './discord.js';

export const WAYS_ROUTE = '/api/account/identities';

// A note handed to Settings in the same tab, by the Discord return page or by a sign in again.
const NOTE = 'downfall.waysNote';

/** How a player reads a provider's name; a provider this page does not know by its own name, capitalised. */
export function wayName(provider) {
  const known = { google: 'Google', discord: 'Discord', microsoft: 'Microsoft', dev: 'A typed name' }[provider];
  return known ?? String(provider).slice(0, 1).toUpperCase() + String(provider).slice(1);
}

/**
 * The rows Settings draws: each way the account uses, removable where the host says so, then each way the host
 * offers that the account does not use yet, to add. `held` is the host's `identities`, `me` its `/api/me`.
 */
export function wayRows(held, me) {
  const kept = (Array.isArray(held) ? held : []).filter(way => typeof way?.provider === 'string');
  const used = kept.map(way => way.provider);
  const offered = signInWays(me);
  return [
    ...kept.map(way => ({ provider: way.provider, name: wayName(way.provider), held: true, removable: way.removable === true })),
    ...offered.filter(way => !used.includes(way.provider)).map(way => ({ provider: way.provider, name: wayName(way.provider), held: false, clientId: way.clientId })),
  ];
}

/** What removing a way in asks first: the player is signed out on every other device, and pop-ups stop. */
export function removeWords(provider) {
  const name = wayName(provider);
  return {
    title: `Remove ${name}?`,
    detail: `You will not be able to sign in with ${name} any more. You stay signed in here. Every other device is signed out, and pop-ups stop on all of them, this one too.`,
    action: `Remove ${name}`,
  };
}

/** Removes a way in. A write carries JSON, or the host's fence refuses it (415). */
export function removeWay(request, provider, recent) {
  return request(`${WAYS_ROUTE}/${provider}`, { recent }, undefined, 'DELETE');
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

/** Whether a line waits to be read, without reading it. */
function noteWaits(storage) {
  try { return Boolean(storage?.getItem(NOTE)); } catch { return false; }
}

function tabStorage() {
  try { return globalThis.sessionStorage ?? null; } catch { return null; }
}

/**
 * Draws the ways in, in Settings' Account tab, for the account the navigation says is signed in. They are read from
 * the host when the tab is shown (`shown`), not on every page. `openSettings` opens Settings on a tab, which a note
 * handed over by the Discord return page or a sign in again asks for.
 */
export function mountWays(doc, { request = accountRequest, google = () => loadGoogle(doc), session = tabStorage(), win = globalThis, openSettings = () => {} } = {}) {
  const el = id => doc.getElementById(id);
  const list = el('ways-list');
  if (!list) return { setAccount() {}, shown() {} };
  const say = (id, text = '') => { el(id).textContent = text; el(id).hidden = !text; };
  const quiet = () => { say('ways-problem'); say('ways-note'); el('ways-again').hidden = true; };
  let me = null;
  let account = null;
  let read = null;
  let drawn = 0;

  // A sign in again: this device signs out, the lobby asks for the sign-in, and Settings opens again after it.
  el('ways-again-button')?.addEventListener('click', async () => {
    el('ways-again-button').disabled = true;
    try {
      await request('/api/account/signout', { everywhere: false });
      forgetRecentSignIn(session);
      keepWaysNote(session, 'Signed in again. You can change how you sign in for the next ten minutes.');
      win.location.assign('/lobby');
    } catch (error) {
      say('ways-problem', error.message);
      el('ways-again-button').disabled = false;
    }
  });

  /** What a refusal says, the sign in again it asks for, and the list read again: the change may have gone through. */
  function refused(error) {
    say('ways-problem', error.message);
    if (error.code === 'Account.SignInAgain') { el('ways-again').hidden = false; return; }
    void refresh();
  }

  /** The proof of a recent sign-in this tab holds; without one, asks for a sign in again and says so. */
  function recentOrAsk() {
    const recent = recentSignIn(session);
    if (!recent) refused({ code: 'Account.SignInAgain', message: 'For your safety, sign in again to change how you sign in.' });
    return recent;
  }

  function draw(held) {
    drawn += 1;
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
      quiet();
      const recent = recentOrAsk();
      if (!recent) return;
      const words = removeWords(provider);
      if (!await confirmAction(doc, words.title, words.detail, words.action)) return;
      button.disabled = true;
      const asking = account?.id;
      try {
        const answer = await removeWay(request, provider, recent);
        if (asking !== account?.id) return;
        draw(answer.identities);
        // The row and its button are gone: the line saying what happened takes the focus.
        say('ways-note', `${wayName(provider)} removed. Other devices are signed out.`);
        el('ways-note').focus?.();
      } catch (error) {
        button.disabled = false;
        refused(error);
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
      quiet();
      // Asked before leaving: a sign-in too old would be refused only once Discord has sent the player back.
      if (!recentOrAsk()) return;
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

  // Google draws its own button. Its configuration is the page's one (account.js), shared with the sign-in: its
  // answer comes here while an account is signed in.
  function googleSlot(row) {
    const slot = doc.createElement('div');
    slot.className = 'way-google';
    if (!row.clientId) return slot;
    answerGoogle('link', response => {
      quiet();
      request(`${WAYS_ROUTE}/google`, { credential: response.credential, recent: recentSignIn(session) ?? '' })
        .then(answer => { draw(answer.identities); say('ways-note', 'Google added. You can sign in with it now.'); el('ways-note').focus?.(); })
        .catch(refused);
    });
    google().then(id => prepareGoogle(id, row.clientId).renderButton(slot, { theme: 'filled_black', shape: 'rectangular', text: 'continue_with', size: 'large' }))
      .catch(error => say('ways-problem', error.message));
    return slot;
  }

  async function refresh() {
    if (!account) { list.replaceChildren(); return; }
    const asking = account.id, before = drawn;
    try {
      const answer = await request(WAYS_ROUTE);
      // Another account signed in on this tab meanwhile, or a later answer drew the list already.
      if (asking === account?.id && before === drawn) draw(answer.identities);
    } catch (error) {
      if (asking === account?.id) say('ways-problem', error.message);
    }
  }

  return {
    /** The account signed in now, or null, and the host's /api/me it was read from. */
    setAccount(next, answer) {
      const changed = (next?.id ?? null) !== (account?.id ?? null);
      account = next ?? null;
      me = answer ?? me;
      el('ways').hidden = !account || signInWays(me).length === 0;
      if (!changed) return;
      read = null;
      quiet();
      list.replaceChildren();
      if (!account) { answerGoogle('link', null); return; }
      // A line handed over opens Settings on Account, which reads the ways and says it.
      if (noteWaits(session)) openSettings('account');
    },
    /** Settings shows a tab: Account reads the ways once for this account, and says a line handed over. */
    async shown(tab) {
      if (tab !== 'account' || !account) return;
      read ??= refresh();
      await read;
      const note = takeWaysNote(session);
      if (note) { say('ways-note', note); el('ways-note').focus?.(); }
    },
    refresh,
  };
}
