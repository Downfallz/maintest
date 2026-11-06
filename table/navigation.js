import { accountRequest, mountAccount } from './account.js';
import { mountDevices } from './devices.js';
import { mountFriends } from './friends.js';
import { operatorToken } from './session.js';

const headerContent = `
    <a class="brand" href="/" aria-label="Downfall Instinct home"><span class="brand-mark" aria-hidden="true">D</span><div><span class="eyebrow">Tactical creature combat</span><h1>Downfall Instinct</h1></div></a>
    <button id="home-menu-toggle" class="home-menu-toggle" type="button" aria-expanded="false" aria-controls="home-navigation" aria-label="Open navigation"><span aria-hidden="true">☰</span><span>Menu</span></button>
    <nav id="home-navigation" class="home-navigation" aria-label="Main navigation">
      <a id="play-link-home" href="/">Play</a>
      <button id="friends-open" type="button" data-home-dialog="friends-dialog" hidden>Friends <span id="friend-requests-badge" class="nav-badge" aria-label="Pending friend requests" hidden></span></button>
      <a id="leaderboard-link" href="/leaderboard" hidden>Leaderboard</a>
      <button id="notifications-open" type="button" data-home-dialog="notifications-dialog" hidden>Notifications</button>
      <button id="data-open" type="button" data-home-dialog="data-dialog">Your data</button>
      <button id="join-open" type="button" data-home-dialog="join-dialog" hidden>Join with a code</button>
      <a id="nav-admin-link" href="/admin" hidden>Admin</a>
    </nav>
    <div id="account" class="account" hidden><details id="account-menu"><summary class="account-chip" aria-label="Your account"><span id="account-initial" class="account-initial" aria-hidden="true"></span><span id="account-name"></span></summary><div class="account-menu"><button id="sign-out" type="button">Sign out</button><button id="sign-out-everywhere" type="button">Sign out everywhere</button><button id="delete-account" type="button" class="danger">Delete account…</button><p id="account-problem" role="alert" hidden></p></div></details></div>
`;
const panels = `<div class="navigation-panels lobby-page home-page">
  <dialog id="friends-dialog" class="home-dialog" aria-labelledby="friends-dialog-title"><div class="dialog-heading"><div><p class="eyebrow">Your circle</p><h2 id="friends-dialog-title">Friends</h2></div><button type="button" data-home-close aria-label="Close friends">×</button></div><div class="home-dialog-body"><section id="friends-panel" class="lobby-panel friends-panel" aria-labelledby="friends-heading" hidden>
          <div id="friend-incoming" hidden><h3 id="friend-incoming-title" class="friend-subheading"></h3><ul id="friend-incoming-list" class="friend-list"></ul></div>
          <h3 id="friends-heading">Your friends <span id="friend-count"></span></h3>
          <ul id="friend-list" class="friend-list"></ul>
          <div id="friend-outgoing" hidden><h3 class="friend-subheading">Waiting for an answer</h3><ul id="friend-outgoing-list" class="friend-list"></ul></div>
          <form id="friend-ask"><label for="friend-username">Add a friend by their player name</label><div class="code-row"><input id="friend-username" name="friend-username" autocomplete="off" autocapitalize="off" spellcheck="false" maxlength="20" placeholder="bastien" required /><button id="friend-ask-submit" type="submit">Ask →</button></div></form>
          <p id="friend-note" class="muted" role="status" hidden></p>
          <p id="friend-problem" class="problem" role="alert" hidden></p>
          <p class="muted">A friend can invite you to a table and be told about it on their phone. Declining and removing are silent.</p>
        </section></div></dialog>
  <dialog id="notifications-dialog" class="home-dialog" aria-labelledby="notifications-dialog-title"><div class="dialog-heading"><div><p class="eyebrow">Stay in the game</p><h2 id="notifications-dialog-title">Notifications</h2></div><button type="button" data-home-close aria-label="Close notifications">×</button></div><div class="home-dialog-body"><section id="device-panel" class="lobby-panel device-panel" aria-labelledby="device-heading" hidden><h2 id="device-heading">Be told on this device</h2><p id="device-hint" class="muted"></p><div class="device-actions"><button id="device-toggle" type="button" class="primary"></button><button id="device-test" type="button" class="secondary" hidden>Send a test</button></div><p id="device-problem" class="problem" role="alert" hidden></p><output id="device-result" class="muted" hidden></output></section><p id="device-unavailable" class="muted">Notifications are not available right now. Your games and invitations are always available here.</p></div></dialog>
  <dialog id="data-dialog" class="home-dialog" aria-labelledby="data-dialog-title"><div class="dialog-heading"><div><p class="eyebrow">Your account, your choice</p><h2 id="data-dialog-title">Your data</h2></div><button type="button" data-home-close aria-label="Close your data">×</button></div><div class="home-dialog-body"><section id="privacy-panel" class="lobby-panel privacy-panel" aria-labelledby="privacy-heading"><h2 id="privacy-heading">What we keep</h2><p>Your player name, an internal id, and the number Google gives your account, so you are you when you come back. Not your email, your real name or your photo.</p><p>Who your friends are, and who asked whom, until one of you removes the other. If you ask to be notified on a device, where to reach that browser, until you stop it or sign out everywhere.</p><p>The games your seats play are recorded under the internal id, to replay them and to improve the bots.</p><p>Delete your account any time from your name at the top: your name is freed, your seats in unfinished games are offered to other players, and finished games keep only the id.</p></section></div></dialog>
  <dialog id="join-dialog" class="home-dialog" aria-labelledby="join-dialog-title"><div class="dialog-heading"><div><p class="eyebrow">An invitation awaits</p><h2 id="join-dialog-title">Join a battle</h2></div><button type="button" data-home-close aria-label="Close join a battle">×</button></div><div class="home-dialog-body"><p class="muted">Enter the code shared by another player to join their battle.</p><form id="join-code"><label for="code">Invitation code</label><div class="code-row"><input id="code" name="code" autocomplete="off" autocapitalize="characters" spellcheck="false" placeholder="K7F2Q9XM" maxlength="16" required /><button type="submit" class="primary">Join →</button></div><p id="join-problem" class="problem" role="alert" hidden></p></form></div></dialog>
  <dialog id="delete-dialog" aria-labelledby="delete-title">
    <form id="delete-form"><p class="eyebrow">This cannot be undone</p><h2 id="delete-title" class="name-title">Delete your account</h2>
      <p class="muted">Your player name is freed for someone else. Your seats in unfinished games are offered to other players. Finished games keep only an internal id, never your name. You can sign in again later as a new player.</p>
      <label for="delete-confirm">Type your player name to confirm</label><input id="delete-confirm" name="delete-confirm" autocomplete="off" autocapitalize="off" spellcheck="false" maxlength="20" required />
      <p id="delete-problem" class="problem" role="alert" hidden></p>
      <div class="dialog-actions"><button id="delete-cancel" type="button" class="secondary">Keep my account</button><button id="delete-submit" class="danger" type="submit">Delete my account</button></div>
    </form>
  </dialog>
  <dialog id="action-dialog" aria-labelledby="action-title" aria-describedby="action-detail">
    <form method="dialog"><h2 id="action-title" class="name-title"></h2><p id="action-detail" class="muted"></p>
      <div class="dialog-actions"><button type="submit" value="cancel" autofocus>Cancel</button><button id="action-confirm" value="confirm">Confirm</button></div>
    </form>
  </dialog>
</div>`;

export function codePath(value) {
  const code = value.trim().replace(/[\s-]/g, '').toUpperCase();
  return /^[A-Z0-9]{8}$/.test(code) ? `/j/${code}` : null;
}

// A disclosure on phones, a normal navigation bar on desktop. Feature panels keep the
// existing account/friends/device controllers and let native dialogs handle modality.
function mountHomeNavigation(doc, win = globalThis) {
  const el = id => doc.getElementById(id);
  const toggle = el('home-menu-toggle'), menu = el('home-navigation');
  const narrow = win.matchMedia('(max-width: 1240px)');
  const panels = [...doc.querySelectorAll('.home-dialog')];
  const openers = new Map();
  let identity;
  function closeMenu(focus = false) {
    toggle.setAttribute('aria-expanded', 'false');
    toggle.setAttribute('aria-label', 'Open navigation');
    if (focus && narrow.matches) toggle.focus();
  }
  toggle.addEventListener('click', () => {
    const open = toggle.getAttribute('aria-expanded') !== 'true';
    toggle.setAttribute('aria-expanded', String(open));
    toggle.setAttribute('aria-label', open ? 'Close navigation' : 'Open navigation');
    el('account-menu').open = false;
  });
  narrow.addEventListener('change', () => {
    const focusWasInMenu = menu.contains(doc.activeElement);
    closeMenu(focusWasInMenu);
  });
  function closePanels(restore = true) {
    for (const panel of panels) if (panel.open) {
      if (!restore) openers.delete(panel);
      panel.close();
    }
  }
  for (const button of doc.querySelectorAll('[data-home-dialog]')) button.addEventListener('click', () => {
    const panel = el(button.dataset.homeDialog);
    closePanels(false);
    openers.set(panel, button);
    closeMenu();
    el('account-menu').open = false;
    panel.showModal();
  });
  for (const panel of panels) {
    panel.querySelector('[data-home-close]').addEventListener('click', () => panel.close());
    panel.addEventListener('close', () => {
      const opener = openers.get(panel);
      openers.delete(panel);
      const target = narrow.matches ? toggle : opener;
      if (opener && target?.getClientRects().length) target.focus();
    });
  }
  menu.addEventListener('click', event => { if (event.target.closest('a')) closeMenu(); });
  doc.addEventListener('click', event => {
    if (!menu.contains(event.target) && !toggle.contains(event.target)) closeMenu();
    if (!el('account').contains(event.target)) el('account-menu').open = false;
  });
  doc.addEventListener('keydown', event => {
    if (event.key !== 'Escape') return;
    if (toggle.getAttribute('aria-expanded') === 'true') { closeMenu(true); event.preventDefault(); }
    if (el('account-menu').open) { el('account-menu').open = false; el('account-menu').querySelector('summary').focus(); }
  });
  return {
    closePanels,
    setAccount(account, mode) {
      const playable = Boolean(account) || mode === 'off';
      doc.body.dataset.home = account ? 'member' : mode === 'off' ? 'local' : 'guest';
      el('join-open').hidden = !playable;
      el('friends-open').hidden = !account;
      el('notifications-open').hidden = !account;
      el('leaderboard-link').hidden = mode === 'off';
      if (identity !== (account?.id ?? null)) {
        closePanels(false);
        if (el('action-dialog').open) el('action-dialog').close('cancel');
        closeMenu();
        identity = account?.id ?? null;
      }
    },
    requests(count) {
      el('friend-requests-badge').textContent = count ? String(count) : '';
      el('friend-requests-badge').hidden = !count;
      el('friends-open').setAttribute('aria-label', count ? `Friends · ${count} pending request${count === 1 ? '' : 's'}` : 'Friends');
    },
  };
}

// The four pages share the same controls and dialogs. Only creating a new match belongs to the lobby.
export function mountNavigation(doc, storage, { onInvite, onChange = () => {} } = {}) {
  const header = doc.querySelector('[data-navigation]');
  const page = header.dataset.navigation;
  const game = page === 'classic' || page === 'battle';
  const el = id => doc.getElementById(id);
  header.innerHTML = headerContent;
  doc.body.insertAdjacentHTML('beforeend', panels);
  if (page === 'lobby' || page === 'leaderboard') {
    el(page === 'lobby' ? 'play-link-home' : 'leaderboard-link').setAttribute('aria-current', 'page');
  }
  const navigation = mountHomeNavigation(doc);
  const devices = mountDevices(doc, { storage });
  const friends = mountFriends(doc, {
    onRequests: navigation.requests,
    onInvite: friend => {
      navigation.closePanels(false);
      if (onInvite) onInvite(friend);
      else location.assign(`/lobby?rematch=${encodeURIComponent(friend.id)}`);
    },
  });
  el('join-code').addEventListener('submit', event => {
    event.preventDefault();
    const path = codePath(el('code').value);
    if (path) location.assign(path);
    else { el('join-problem').textContent = 'Enter the eight-character code from your invitation.'; el('join-problem').hidden = false; el('code').focus(); }
  });
  el('code').addEventListener('input', () => { el('join-problem').hidden = true; });
  let previous = null;
  void mountAccount(doc, {
    readMe: async () => {
      const token = operatorToken(storage);
      el('nav-admin-link').hidden = true;
      const me = await accountRequest('/api/me', undefined, (url, options) =>
        fetch(url, { ...options, headers: token ? { 'X-Seat-Token': token } : {} }));
      el('nav-admin-link').hidden = me.operator !== true;
      return me;
    },
    onChange(account, mode) {
      if (previous && !account && page !== 'lobby') { location.replace('/lobby'); return; }
      previous = account;
      header.hidden = game && !account;
      navigation.setAccount(account, mode);
      devices.setAccount(account);
      friends.setAccount(account);
      onChange(account, mode);
    },
  });
  return friends;
}

if (typeof document !== 'undefined' && document.querySelector('[data-navigation]:not([data-navigation="lobby"])')) {
  let storage = null;
  try { storage = globalThis.localStorage; } catch { /* The menu also works without browser storage. */ }
  mountNavigation(document, storage);
}
