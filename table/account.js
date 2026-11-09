// The player's sign-in on the home page (ADR 0122): Google's button, or a typed name on a developer's host,
// the name chosen at the first sign-in, and the account in the masthead. The host owns every rule; this page
// only checks a name's shape before asking, so a keystroke that cannot be a name costs no request.

const NAME = /^[A-Za-z][A-Za-z0-9_-]{2,19}$/;
const GOOGLE_SCRIPT = 'https://accounts.google.com/gsi/client';

/** What the page can say about a name before asking the host, or null when only the host can tell. */
export function nameProblem(value) {
  const name = value.trim();
  if (!name) return 'Choose the name other players will see.';
  return NAME.test(name) ? null : '3 to 20 characters: letters, digits, _ and -. Starts with a letter.';
}

/** Where a page sends a player who must sign in first, coming back to `here` afterwards. */
export function signInPath(here) {
  // The lobby by its own name: `/` with a query is the game page, for links from before the lobby.
  return `/lobby?next=${encodeURIComponent(here)}`;
}

/** The page to go back to after signing in: only a seat or an invitation of this host, never another site. */
export function nextPath(search) {
  const next = new URLSearchParams(search ?? '').get('next');
  if (next && /^\/arena(?:[?#]|$)/.test(next)) return '/battle' + next.slice('/arena'.length);
  return next && /^\/(?:play|battle(?:[?#]|$)|j\/|\?)/.test(next) && !next.startsWith('//') ? next : null;
}

/** The player half of /api/me, with off for an older host that does not say. */
export function accountState(me) {
  return { mode: me?.accounts ?? 'off', account: me?.account ?? null, clientId: me?.googleClientId ?? null };
}

export async function accountRequest(path, body, fetchImpl = globalThis.fetch.bind(globalThis), method = body === undefined ? 'GET' : 'POST') {
  const options = { method, headers: {}, credentials: 'same-origin' };
  if (body !== undefined) { options.headers['Content-Type'] = 'application/json'; options.body = JSON.stringify(body); }
  const response = await fetchImpl(path, options);
  const text = await response.text();
  let result;
  try { result = text ? JSON.parse(text) : {}; } catch { result = { message: text }; }
  if (!response.ok) {
    const error = new Error(result.message || `The host answered ${response.status}. Try again.`);
    error.code = result.error;
    error.status = response.status;
    throw error;
  }
  return result;
}

/** Loads Google's script once, resolving with its sign-in API. */
export function loadGoogle(doc, win = globalThis) {
  if (win.google?.accounts?.id) return Promise.resolve(win.google.accounts.id);
  return new Promise((resolve, reject) => {
    const script = doc.createElement('script');
    script.src = GOOGLE_SCRIPT;
    script.async = true;
    script.onload = () => (win.google?.accounts?.id ? resolve(win.google.accounts.id) : reject(new Error('Google sign-in did not load.')));
    script.onerror = () => reject(new Error('Google sign-in could not be reached. Check the connection and refresh.'));
    doc.head.append(script);
  });
}

export function confirmAction(doc, title, detail, label) {
  const dialog = doc.getElementById('action-dialog');
  doc.getElementById('action-title').textContent = title;
  doc.getElementById('action-detail').textContent = detail;
  doc.getElementById('action-confirm').textContent = label;
  dialog.returnValue = '';
  return new Promise(resolve => {
    dialog.addEventListener('close', () => resolve(dialog.returnValue === 'confirm'), { once: true });
    dialog.showModal();
  });
}

function mountSignIn(doc, { request, google, refresh, say }) {
  const el = id => doc.getElementById(id);
  let ticket = null;
  let checking = 0;
  let timer = null;
  async function signedIn(answer) {
    if (answer.needsUsername) {
      ticket = answer.ticket;
      say('name-problem');
      say('name-check');
      el('name-dialog').showModal();
      el('username').focus();
      return;
    }
    await refresh();
  }

  async function withGoogle(state) {
    if (state.mode !== 'google' || !state.clientId) return;
    try {
      const id = await google();
      id.initialize({
        client_id: state.clientId,
        callback: response => request('/api/account/google', { credential: response.credential }).then(signedIn).catch(error => say('sign-in-problem', error.message)),
        ux_mode: 'popup',
        cancel_on_tap_outside: true,
      });
      id.renderButton(el('google-button'), { theme: 'filled_black', shape: 'pill', text: 'continue_with', size: 'large' });
    } catch (error) {
      say('sign-in-problem', error.message);
    }
  }

  el('dev-sign-in').addEventListener('submit', event => {
    event.preventDefault();
    say('sign-in-problem');
    request('/api/account/dev', { username: el('dev-username').value }).then(signedIn).catch(error => say('sign-in-problem', error.message));
  });

  el('username').addEventListener('input', () => {
    globalThis.clearTimeout(timer);
    say('name-problem');
    const value = el('username').value;
    const problem = nameProblem(value);
    if (problem) { say('name-check'); el('name-check').className = 'name-check'; return; }
    const asked = ++checking;
    timer = globalThis.setTimeout(async () => {
      const answer = await request(`/api/account/names/${encodeURIComponent(value.trim())}`).catch(() => null);
      if (asked !== checking || !answer) return;
      el('name-check').className = answer.available ? 'name-check good' : 'name-check bad';
      say('name-check', answer.available ? `✓ ${value.trim()} is available` : `✕ ${answer.message}`);
    }, 300);
  });

  el('name-form').addEventListener('submit', async event => {
    event.preventDefault();
    const value = el('username').value;
    const problem = nameProblem(value);
    if (problem) { say('name-problem', problem); return; }
    el('name-submit').disabled = true;
    try {
      await request('/api/account', { ticket, username: value.trim() });
      ticket = null;
      el('name-dialog').close();
      await refresh();
    } catch (error) {
      say('name-problem', error.code === 'Account.TicketExpired' ? 'That sign-in ran out. Close this and sign in again.' : error.message);
    } finally { el('name-submit').disabled = false; }
  });

  el('name-cancel').addEventListener('click', () => { ticket = null; el('name-dialog').close(); });

  return withGoogle;
}

/**
 * Draws the account: the sign-in when nobody is signed in, the chip when somebody is, and the name dialog in
 * between. `onChange` is told the account (or null) whenever it changes, so the lobby can redraw what it shows.
 */
export function mountAccount(doc, { request = accountRequest, readMe = () => request('/api/me'), google = () => loadGoogle(doc), onChange = () => {} } = {}) {
  const el = id => doc.getElementById(id);
  const say = (id, text = '') => { el(id).textContent = text; el(id).hidden = !text; };
  const signIn = el('sign-in-panel') ? mountSignIn(doc, { request, google, refresh, say }) : null;

  async function refresh() {
    const state = accountState(await readMe().catch(() => ({ accounts: 'unavailable' })));
    if (signIn) {
      el('sign-in-panel').hidden = state.mode === 'off' || Boolean(state.account);
      el('dev-sign-in').hidden = state.mode !== 'dev';
      el('google-button').hidden = state.mode !== 'google';
      // Unreachable is neither signed out nor accounts-off: no way in is offered, so the panel says why.
      if (state.mode === 'unavailable') say('sign-in-problem', 'Could not reach the host to check your account. Reload the page to try again.');
      else if (el('sign-in-problem').textContent.startsWith('Could not reach the host to check')) say('sign-in-problem');
    }
    el('account').hidden = !state.account;
    if (state.account) {
      el('account-name').textContent = state.account.username;
      el('account-menu').querySelector('summary').setAttribute('aria-label', `Account: ${state.account.username}`);
      el('account-initial').textContent = state.account.username.slice(0, 1).toUpperCase();
      // The menu and Settings' Account tab name the player too, when the page has them.
      if (el('account-menu-name')) el('account-menu-name').textContent = state.account.username;
      if (el('app-settings-name')) {
        el('app-settings-name').textContent = state.account.username;
        el('app-settings-initial').textContent = state.account.username.slice(0, 1).toUpperCase();
      }
    }
    onChange(state.account, state.mode);
    return state;
  }

  // Sign out sits in the account menu; Sign out everywhere in Settings, Account, which says its own problems.
  for (const [id, everywhere] of [['sign-out', false], ['sign-out-everywhere', true]]) {
    if (!el(id)) continue;
    const problemId = everywhere && el('app-settings-account-problem') ? 'app-settings-account-problem' : 'account-problem';
    el(id).addEventListener('click', async () => {
      say(problemId);
      const title = everywhere ? 'Sign out everywhere?' : 'Sign out on this device?';
      const detail = everywhere ? 'You will need to sign in again on every device.' : 'You can sign in again to resume your games.';
      el('account-menu').open = false;
      if (!everywhere) el('account-menu').querySelector('summary').focus();
      if (!await confirmAction(doc, title, detail, everywhere ? 'Sign out everywhere' : 'Sign out')) return;
      el(id).disabled = true;
      try {
        await request('/api/account/signout', { everywhere });
        await refresh();
      } catch (error) {
        if (!everywhere) el('account-menu').open = true;
        say(problemId, error.message);
      } finally { el(id).disabled = false; }
    });
  }

  // Deleting is final, so it asks for the player's own name, the one thing a stray tap cannot type.
  el('delete-account').addEventListener('click', () => {
    el('account-menu').open = false;
    el('delete-confirm').value = '';
    say('delete-problem');
    el('delete-dialog').showModal();
    el('delete-confirm').focus();
  });
  el('delete-cancel').addEventListener('click', () => el('delete-dialog').close());
  el('delete-form').addEventListener('submit', async event => {
    event.preventDefault();
    el('delete-submit').disabled = true;
    try {
      await request('/api/account', { username: el('delete-confirm').value }, undefined, 'DELETE');
      el('delete-dialog').close();
      await refresh();
    } catch (error) {
      say('delete-problem', error.message);
    } finally { el('delete-submit').disabled = false; }
  });

  return refresh().then(async state => { await signIn?.(state); return state; });
}
