// The player's sign-in on the home page (ADR 0122): a button for each way in the host lists (Google's, for one),
// or a typed name on a developer's host,
// the name chosen at the first sign-in, and the account in the masthead. The host owns every rule; this page
// only checks a name's shape before asking, so a keystroke that cannot be a name costs no request.

import { undressScene, wearScene } from './picture.js';
import { CODE_FLOWS, startCodeFlow, takeTicket } from './codeflow.js';

const NAME = /^[A-Za-z][A-Za-z0-9_-]{2,19}$/;
const GOOGLE_SCRIPT = 'https://accounts.google.com/gsi/client';
// The providers this page can draw a button for, each in its element `<provider>-button` (sign-in methods plan, P2, P3,
// P6). A provider the host lists that is not here is left undrawn: a page older than its host.
export const DRAWN_WAYS = ['google', 'discord', 'microsoft'];

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
  if (!next) return null;
  if (next === '/arena' || next.startsWith('/arena?') || next.startsWith('/arena#')) return '/battle' + next.slice('/arena'.length);
  if (next === '/battle' || next.startsWith('/battle?') || next.startsWith('/battle#')) return next;
  if (next.startsWith('/play') || next.startsWith('/j/') || next.startsWith('/?')) return next;
  return null;
}

/**
 * The ways in /api/me lists, in its order, that this page draws: each a provider's name and its public client id. A
 * host of the release before lists none and says `accounts: 'google'` with its client id: that is Google alone.
 */
export function signInWays(me) {
  const older = me?.accounts === 'google' && typeof me?.googleClientId === 'string' ? [{ provider: 'google', clientId: me.googleClientId }] : [];
  const listed = Array.isArray(me?.signIn) ? me.signIn : older;
  return listed
    .filter(way => typeof way?.provider === 'string' && DRAWN_WAYS.includes(way.provider))
    .map(way => ({ provider: way.provider, clientId: typeof way.clientId === 'string' ? way.clientId : null }));
}

/** The player half of /api/me, with off for an older host that does not say. */
export function accountState(me) {
  const ways = signInWays(me);
  return { mode: me?.accounts ?? 'off', account: me?.account ?? null, clientId: ways.find(way => way.provider === 'google')?.clientId ?? null, ways };
}

/**
 * Shows the button of each way in the state lists and hides the others, in the host's order where the page has a row
 * to order them in; none at all on a host that signs nobody in through a provider.
 */
export function drawWays(doc, state) {
  const shown = state.mode === 'dev' || state.mode === 'off' ? [] : state.ways.map(way => way.provider);
  for (const provider of DRAWN_WAYS) {
    const button = doc.getElementById(`${provider}-button`);
    if (button) button.hidden = !shown.includes(provider);
  }
  const row = doc.getElementById('sign-in-ways');
  if (row) row.hidden = shown.length === 0;
  if (typeof row?.append === 'function') row.append(...shown.map(provider => doc.getElementById(`${provider}-button`)).filter(Boolean));
  return shown;
}

/**
 * The operator's notice (ADR 0177) as /api/me carries it, or null for none: one line under the header of every page
 * but the game itself, where nothing is added over the board.
 */
export function noticeText(me, page) {
  if (page === 'classic' || page === 'battle') return null;
  const line = typeof me?.notice === 'string' ? me.notice.trim() : '';
  return line || null;
}

// The host's proof of a recent sign-in in this tab, and when it was kept (sign-in methods plan, P4).
const RECENT = 'downfall.recentSignIn';
// How long the host takes a sign-in as recent (AccountSessions.RecentFor). The page uses it only to ask for a sign in
// again before leaving for Discord, rather than after; the host checks the proof itself.
export const RECENT_FOR_MS = 10 * 60 * 1000;

/** Keeps the host's proof of a recent sign-in in this tab, from a sign-in's answer: what changing a way to sign in asks for. */
export function keepRecentSignIn(storage, value, now = Date.now()) {
  if (typeof value !== 'string' || !value) return;
  try { storage?.setItem(RECENT, JSON.stringify({ value, at: now })); } catch { /* Changing a way then asks for a sign in again. */ }
}

/** Forgets the proof of a recent sign-in: a sign out leaves nothing in the tab that changes the account's ways in. */
export function forgetRecentSignIn(storage) {
  try { storage?.removeItem(RECENT); } catch { /* Nothing kept, nothing to forget. */ }
}

/** The proof of a recent sign-in this tab keeps, or null when it has none or it is too old to be taken. */
export function recentSignIn(storage, now = Date.now()) {
  try {
    const kept = JSON.parse(storage?.getItem(RECENT) ?? 'null');
    return typeof kept?.value === 'string' && typeof kept.at === 'number' && now - kept.at < RECENT_FOR_MS ? kept.value : null;
  } catch { return null; }
}

function tabSession() {
  try { return globalThis.sessionStorage ?? null; } catch { return null; }
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
    // How long the host asks the page to wait before posting again, in seconds (`Retry-After`), or null.
    const after = response.headers?.get?.('Retry-After');
    error.retryAfter = after && /^\d+$/.test(after.trim()) ? Number(after) : null;
    throw error;
  }
  // Every sign-in's answer carries it, whichever page or provider made it.
  if (typeof result?.recent === 'string') keepRecentSignIn(tabSession(), result.recent);
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

// Google's script keeps one configuration per page, the last `initialize` given (sign-in methods plan, P4): the
// sign-in button and Settings' Add Google share it. It is initialized once per script, and its answer goes to whoever
// asked for it last and is still waiting: Settings while an account is signed in, the sign-in otherwise.
const googleInitialized = new WeakSet();
const googleAnswers = { signin: null, link: null };

/** Who Google's answer goes to: `signin` for the sign-in button, `link` for Settings' Add Google, null to stop. */
export function answerGoogle(kind, handler) {
  googleAnswers[kind] = handler;
}

/** Google's sign-in API, initialized once for this client id, its answer sent where `answerGoogle` says. */
export function prepareGoogle(id, clientId) {
  if (!googleInitialized.has(id)) {
    googleInitialized.add(id);
    id.initialize({
      client_id: clientId,
      callback: response => (googleAnswers.link ?? googleAnswers.signin)?.(response),
      ux_mode: 'popup',
      cancel_on_tap_outside: true,
    });
  }
  return id;
}

/**
 * The scene a sign out's confirmation wears as its head (docs/art/journeys/doors.md, Sign out, the confirmation): the
 * Predator at rest, its head and the ember on its shoulder kept in view, the title on the dark hall at its start.
 */
export const SIGN_OUT_HEAD = { name: 'eye-still-open', form: 'head', side: 'start', focus: '70% 45%', sizes: '(min-width: 512px) 480px, calc(100vw - 32px)' };

/** What a sign out's confirmation says, on this device or everywhere. The action is the filled one, Stay signed in the way back. */
export function signOutWords(everywhere) {
  return everywhere
    ? { title: 'Sign out everywhere?', detail: 'You will need to sign in again on every device.', action: 'Sign out everywhere', keep: 'Stay signed in' }
    : { title: 'Sign out on this device?', detail: 'You can sign in again to resume your games.', action: 'Sign out', keep: 'Stay signed in' };
}

// The tab's mark that this browser just signed out, read once by the home's door (door.js): a reload, or a link
// passed on, never says goodbye.
const SIGNED_OUT = 'downfall.signedOut';

/** Marks the tab once the host has confirmed a sign out from it. A browser without storage says no goodbye. */
/**
 * What a sign-out tells the host (ADR 0202): everywhere, or this browser only, with the push endpoint it holds, so a
 * borrowed browser is no longer one of the account's devices once its player leaves it.
 */
export async function signOutBody(everywhere, nav = globalThis.navigator) {
  if (everywhere) return { everywhere: true };
  try {
    const registration = await nav?.serviceWorker?.getRegistration?.();
    const endpoint = (await registration?.pushManager?.getSubscription())?.endpoint;
    return endpoint ? { everywhere: false, endpoint } : { everywhere: false };
  } catch {
    return { everywhere: false };
  }
}

export function markSignedOut(storage) {
  try { storage?.setItem(SIGNED_OUT, '1'); } catch { /* The plain door then. */ }
}

/** Whether this tab just signed out, and forgets it: the goodbye shows once. */
export function takeSignedOut(storage) {
  try {
    const marked = storage?.getItem(SIGNED_OUT) === '1';
    storage?.removeItem(SIGNED_OUT);
    return marked;
  } catch { return false; }
}

/**
 * Asks once, in the shared confirmation window. `filled` makes the action the window's one filled button, focused
 * first, and `keep` names the way back; `head` dresses the title with a scene (wearScene's options and the page's
 * name), which another confirmation, removing a friend, takes off again.
 */
export function confirmAction(doc, title, detail, label, { filled = false, keep = 'Cancel', head = null } = {}) {
  const dialog = doc.getElementById('action-dialog');
  const heading = doc.getElementById('action-heading'), confirm = doc.getElementById('action-confirm'), cancel = doc.getElementById('action-cancel');
  doc.getElementById('action-title').textContent = title;
  doc.getElementById('action-detail').textContent = detail;
  confirm.textContent = label;
  confirm.classList.toggle('primary', filled);
  cancel.textContent = keep;
  if (head?.page) {
    const { name, ...options } = head;
    wearScene(doc, heading, name, { ...options, words: doc.getElementById('action-title') });
  } else undressScene(heading);
  dialog.returnValue = '';
  return new Promise(resolve => {
    dialog.addEventListener('close', () => resolve(dialog.returnValue === 'confirm'), { once: true });
    dialog.showModal();
    if (filled) confirm.focus();
  });
}

function mountSignIn(doc, { request, google, refresh, say, session, win }) {
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
      return false;
    }
    return (await refresh()).leaving;
  }

  // Discord's and Microsoft's buttons leave for the provider's authorize page; its return page (signin.js) comes back
  // here with `next`.
  let offeredWays = [];
  for (const provider of Object.keys(CODE_FLOWS)) {
    const button = el(`${provider}-button`);
    button?.addEventListener('click', async () => {
      const way = offeredWays.find(each => each.provider === provider);
      if (!way?.clientId) return;
      say('sign-in-problem');
      button.disabled = true;
      // A page brought back from the back-forward cache, the player having pressed Back at the provider, gives the button back.
      win.addEventListener?.('pageshow', event => { if (event.persisted) button.disabled = false; }, { once: true });
      try {
        await startCodeFlow({ provider, clientId: way.clientId, origin: win.location.origin, next: nextPath(win.location.search), storage: session, go: url => win.location.assign(url) });
      } catch (error) {
        say('sign-in-problem', error.message);
        button.disabled = false;
      }
    });
  }

  // The ways in, once the host has said which: Discord's and Microsoft's buttons are ours, Google's is drawn by Google's script. A name
  // to choose that a return page handed over opens the name window, as a first sign-in on this page does.
  async function withWays(state) {
    const offered = state.mode === 'dev' || state.mode === 'off' ? [] : state.ways;
    offeredWays = offered;
    // Taken only where the window can open: a host that could not be read keeps it for the reload it asks for.
    const handed = state.account || offered.length === 0 ? null : takeTicket(session);
    if (handed) await signedIn({ needsUsername: true, ticket: handed });
    await withGoogle(offered.find(each => each.provider === 'google'));
  }

  async function withGoogle(way) {
    if (!way?.clientId) return;
    try {
      answerGoogle('signin', response => request('/api/account/google', { credential: response.credential }).then(signedIn).catch(error => say('sign-in-problem', error.message)));
      const id = prepareGoogle(await google(), way.clientId);
      id.renderButton(el('google-button'), { theme: 'filled_black', shape: 'rectangular', text: 'continue_with', size: 'large' });
    } catch (error) {
      say('sign-in-problem', error.message);
    }
  }

  // The press is answered at once: the button says it is signing in until the host has.
  el('dev-sign-in').addEventListener('submit', async event => {
    event.preventDefault();
    say('sign-in-problem');
    const button = el('dev-sign-in-submit'), label = button?.textContent;
    if (button) { button.disabled = true; button.textContent = 'Signing in…'; }
    let leaving = false;
    try {
      leaving = await signedIn(await request('/api/account/dev', { username: el('dev-username').value }));
    } catch (error) {
      say('sign-in-problem', error.message);
    } finally {
      // A page on its way elsewhere keeps the button as it is: a second press would sign in again.
      if (button && !leaving) { button.disabled = false; button.textContent = label; }
    }
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
    } catch (error) {
      say('name-problem', error.code === 'Account.TicketExpired' ? 'That sign-in ran out. Close this and sign in again.' : error.message);
      el('name-submit').disabled = false;
      return;
    }
    ticket = null;
    say('name-check', `✓ ${value.trim()} is yours`);
    el('name-check').className = 'name-check good';
    // The window stays while the page goes where the press leads, its line saying where: the cue the player
    // carries across the page that changes. Its button stays off, since the name is taken. It closes when the
    // account stays on this page, or when the page could not be read again.
    const leaving = await refresh().then(state => state.leaving).catch(() => false);
    if (!leaving) { el('name-dialog').close(); el('name-submit').disabled = false; }
  });

  // Closed without a name, the focus goes back to the way in: the developer's button, or the first provider's shown.
  el('name-dialog').addEventListener('close', () => {
    if (!ticket) return;
    ticket = null;
    const way = DRAWN_WAYS.map(provider => el(`${provider}-button`)).find(button => button && !button.hidden);
    (el('dev-sign-in').hidden ? way?.querySelector('[role="button"], button, iframe') : el('dev-sign-in-submit'))?.focus();
  });
  el('name-cancel').addEventListener('click', () => el('name-dialog').close());

  return withWays;
}

function tabStorage() {
  try { return globalThis.sessionStorage ?? null; } catch { return null; }
}

/**
 * Draws the account: the sign-in when nobody is signed in, the chip when somebody is, and the name dialog in
 * between. `onChange` is told the account (or null) whenever it changes, so the lobby can redraw what it shows.
 * `signOutPage` is the page's name in docs/art/page-budget.json when its sign out's confirmation wears its head.
 */
export function mountAccount(doc, { request = accountRequest, readMe = () => request('/api/me'), google = () => loadGoogle(doc), onChange = () => {}, signOutPage = null, session = tabStorage(), win = globalThis } = {}) {
  const el = id => doc.getElementById(id);
  const say = (id, text = '') => { el(id).textContent = text; el(id).hidden = !text; };
  const signIn = el('sign-in-panel') ? mountSignIn(doc, { request, google, refresh, say, session, win }) : null;

  async function refresh() {
    const state = accountState(await readMe().catch(() => ({ accounts: 'unavailable' })));
    if (signIn) {
      el('sign-in-panel').hidden = state.mode === 'off' || Boolean(state.account);
      el('dev-sign-in').hidden = state.mode !== 'dev';
      drawWays(doc, state);
      // Unreachable is neither signed out nor accounts-off: no way in is offered, so the panel says why.
      if (state.mode === 'unavailable') say('sign-in-problem', 'Could not reach the host to check your account. Reload the page to try again.');
      else if (el('sign-in-problem').textContent.startsWith('Could not reach the host to check')) say('sign-in-problem');
    }
    el('account').hidden = !state.account;
    // Somebody is signed in: a mark left by an earlier sign out is no goodbye any more.
    if (state.account) takeSignedOut(session);
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
    // A page that goes elsewhere because of who is signed in (a link to follow, the first lesson) says so, once it
    // knows: `leaving` is a promise of that boolean. The rest of the page is drawn without waiting for it (#972); only
    // what must stay until the page leaves (the name window, the button that signed in) awaits it, and sees it fail.
    state.leaving = Promise.resolve(onChange(state.account, state.mode)).then(Boolean);
    state.leaving.catch(() => {});
    return state;
  }

  // Sign out sits in the account menu; Sign out everywhere in Settings, Account, which says its own problems.
  for (const [id, everywhere] of [['sign-out', false], ['sign-out-everywhere', true]]) {
    if (!el(id)) continue;
    const problemId = everywhere && el('app-settings-account-problem') ? 'app-settings-account-problem' : 'account-problem';
    el(id).addEventListener('click', async () => {
      say(problemId);
      const words = signOutWords(everywhere);
      el('account-menu').open = false;
      if (!everywhere) el('account-menu').querySelector('summary').focus();
      const head = signOutPage ? { ...SIGN_OUT_HEAD, page: signOutPage } : null;
      if (!await confirmAction(doc, words.title, words.detail, words.action, { filled: true, keep: words.keep, head })) return;
      el(id).disabled = true;
      try {
        await request('/api/account/signout', await signOutBody(everywhere));
        markSignedOut(session);
        forgetRecentSignIn(session);
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
