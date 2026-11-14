import { accountRequest, mountAccount, noticeText } from './account.js';
import { mountDevices } from './devices.js';
import { mountFriends } from './friends.js';
import { mountInbox } from './inbox.js';
import { operatorToken } from './session.js';
import { mountGameSettings } from './game-preferences.js';

const headerContent = `
    <a class="brand" href="/" aria-label="Downfall Instinct home"><span class="brand-mark" aria-hidden="true"></span><h1>Downfall Instinct</h1></a>
    <button id="home-menu-toggle" class="home-menu-toggle" type="button" aria-expanded="false" aria-controls="home-navigation" aria-label="Open navigation"><span aria-hidden="true">☰</span><span>Menu</span></button>
    <nav id="home-navigation" class="home-navigation" aria-label="Main navigation">
      <a id="play-link-home" href="/">Lobby</a>
      <button id="friends-open" type="button" data-home-dialog="friends-dialog" hidden>Friends <span id="friend-requests-badge" class="nav-badge" aria-label="Pending friend requests" hidden></span></button>
      <a id="leaderboard-link" href="/leaderboard" hidden>Leaderboard</a>
      <a id="stats-link" href="/stats" hidden>Your stats</a>
      <button id="join-open" type="button" data-home-dialog="join-dialog" hidden>Join with a code</button>
      <button id="app-settings-open" type="button" data-home-dialog="app-settings-dialog">Settings</button>
      <a id="nav-admin-link" href="/admin" hidden>Admin</a>
    </nav>
    <button id="inbox-open" class="inbox-button" type="button" data-home-dialog="inbox-dialog" aria-label="Notifications · nothing waiting" hidden><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 3a6 6 0 0 0-6 6v3.6L4.4 15.5A1 1 0 0 0 5.3 17h13.4a1 1 0 0 0 .9-1.5L18 12.6V9a6 6 0 0 0-6-6Z"/><path d="M9.5 19.5a2.6 2.6 0 0 0 5 0"/></svg><span id="inbox-badge" class="nav-badge inbox-badge" aria-hidden="true" hidden></span></button>
    <div id="account" class="account" hidden><details id="account-menu"><summary class="account-chip" aria-label="Your account"><span id="account-initial" class="account-initial" aria-hidden="true"></span><span id="account-name"></span></summary><div class="account-menu"><p class="account-menu-who"><small>Signed in as</small><strong id="account-menu-name"></strong></p><button id="account-settings" type="button" data-home-dialog="app-settings-dialog">Settings</button><a class="site-link" href="https://downfallinstinct.com/">Game website <span aria-hidden="true">→</span></a><button id="sign-out" type="button">Sign out</button><p id="account-problem" role="alert" hidden></p></div></details></div>
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
  <dialog id="inbox-dialog" class="home-dialog" aria-labelledby="inbox-dialog-title"><div class="dialog-heading"><div><p class="eyebrow">Waiting for you</p><h2 id="inbox-dialog-title">Notifications</h2></div><button type="button" data-home-close aria-label="Close notifications">×</button></div><div class="home-dialog-body"><div id="inbox-list" class="inbox-list"></div><p id="inbox-problem" class="problem" role="alert" hidden></p></div></dialog>
  <dialog id="app-settings-dialog" class="home-dialog app-settings-dialog" aria-labelledby="app-settings-title"><div class="dialog-heading"><div><p class="eyebrow">Downfall Instinct</p><h2 id="app-settings-title">Settings</h2></div><button type="button" data-home-close aria-label="Close settings">×</button></div>
    <div class="app-settings-tabs" role="tablist" aria-label="Settings sections">
      <button id="app-settings-tab-game" type="button" role="tab" aria-controls="app-settings-game" data-settings-tab="game">Game</button><button id="app-settings-tab-notifications" type="button" role="tab" aria-controls="app-settings-notifications" data-settings-tab="notifications" hidden>Notifications</button><button id="app-settings-tab-privacy" type="button" role="tab" aria-controls="app-settings-privacy" data-settings-tab="privacy">Privacy</button><button id="app-settings-tab-account" type="button" role="tab" aria-controls="app-settings-account" data-settings-tab="account" hidden>Account</button>
    </div>
    <div class="home-dialog-body">
    <section id="app-settings-game" class="app-settings-pane" role="tabpanel" aria-labelledby="app-settings-tab-game"><h3>Every new game</h3><form id="game-settings" class="lobby-panel game-settings-panel"><fieldset id="game-settings-fields">
    <label for="game-settings-pace"><span>Read confirmed results<small>Opponent actions always wait for your OK, your own never do · Step also waits for Next on the end-of-round summaries, Auto plays them</small></span><select id="game-settings-pace"><option value="step">Step</option><option value="auto">Auto</option></select></label>
    <label for="game-settings-speed"><span>Playback speed</span><select id="game-settings-speed"><option value="0.5">0.5×</option><option value="1">1×</option><option value="1.5">1.5×</option><option value="2">2×</option></select></label>
    <label for="game-settings-help"><span>Phase tips<small>Explain each phase the first time it comes in each game</small></span><input id="game-settings-help" type="checkbox"></label>
    <label for="game-settings-motion"><span>Less motion<small>Keep the scene still</small></span><input id="game-settings-motion" type="checkbox"></label>
    <label for="game-settings-reactions"><span>Opponent reactions<small>Show the set messages a player sends during a game · yours are always sent</small></span><input id="game-settings-reactions" type="checkbox"></label>
    </fieldset><p id="game-settings-where" class="muted"></p><p id="game-settings-problem" class="problem" role="alert" hidden></p></form></section>
    <section id="app-settings-notifications" class="app-settings-pane" role="tabpanel" aria-labelledby="app-settings-tab-notifications" hidden><section id="device-panel" class="lobby-panel device-panel" aria-labelledby="device-heading" hidden><h2 id="device-heading">Be told on this device</h2><p id="device-hint" class="muted"></p><div class="device-actions"><button id="device-toggle" type="button" class="primary"></button><button id="device-test" type="button" class="secondary" hidden>Send a test</button></div><p id="device-problem" class="problem" role="alert" hidden></p><output id="device-result" class="muted" hidden></output></section><p id="device-unavailable" class="muted">Notifications are not available right now. Your games and invitations are always available here.</p></section>
    <section id="app-settings-privacy" class="app-settings-pane" role="tabpanel" aria-labelledby="app-settings-tab-privacy" hidden><section id="privacy-panel" class="lobby-panel privacy-panel" aria-labelledby="privacy-heading"><h2 id="privacy-heading">What we keep</h2><p>Your player name, an internal id, and the number Google gives your account, so you are you when you come back. Not your email, your real name or your photo.</p><p>Who your friends are, and who asked whom, until one of you removes the other. If you ask to be notified on a device, where to reach that browser, until you stop it or sign out everywhere.</p><p>Your game settings (reading pace, speed, phase tips, less motion, opponent reactions), until you change them or delete your account.</p><p>When you last signed in and when you last used the site, to the hour, so the host can see who still plays, until you delete your account.</p><p>The reactions sent during a game (a few set messages, never free text) are kept with that game's record, the last few of them.</p><p>The games your seats play are recorded under the internal id, to replay them, to improve the bots, and to count your statistics, which only you see.</p><p>Delete your account any time from Settings, Account: your name is freed, your seats in unfinished games are offered to other players, and finished games keep only the id.</p><p>The host's operator sees your name, those two times and how many games you played, and can sign you out everywhere, rename you, or delete your account.</p></section></section>
    <section id="app-settings-account" class="app-settings-pane" role="tabpanel" aria-labelledby="app-settings-tab-account" hidden>
      <div class="app-settings-identity"><span id="app-settings-initial" class="account-initial" aria-hidden="true"></span><div><strong id="app-settings-name"></strong><small>Your player name, as your opponents and friends see it</small></div></div>
      <div class="app-settings-danger" aria-labelledby="app-settings-danger-title"><h3 id="app-settings-danger-title">Sessions and account</h3>
        <div class="app-settings-row"><div><strong>Sign out everywhere</strong><small>Ends your sessions on every device, this one included. Your games wait for you.</small></div><button id="sign-out-everywhere" type="button" class="secondary">Sign out everywhere</button></div>
        <div class="app-settings-row"><div><strong>Delete account</strong><small>Frees your player name and offers your seats in unfinished games to other players. This cannot be undone.</small></div><button id="delete-account" type="button" class="danger">Delete account…</button></div>
        <p id="app-settings-account-problem" class="problem" role="alert" hidden></p>
      </div>
    </section>
    </div></dialog>
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
  // Settings is one window of tabs: Game, Notifications, Privacy and Account. Notifications and Account are for a
  // signed-in player; a visitor sees Game and Privacy.
  const tabs = [...doc.querySelectorAll('[data-settings-tab]')];
  function showTab(name, focus = false) {
    const shown = tabs.find(tab => tab.dataset.settingsTab === name && !tab.hidden) ?? tabs.find(tab => !tab.hidden);
    for (const tab of tabs) {
      const selected = tab === shown;
      tab.setAttribute('aria-selected', String(selected)); tab.tabIndex = selected ? 0 : -1;
      el(tab.getAttribute('aria-controls')).hidden = !selected;
    }
    if (focus) shown?.focus();
  }
  for (const tab of tabs) {
    tab.addEventListener('click', () => showTab(tab.dataset.settingsTab));
    tab.addEventListener('keydown', event => {
      const visible = tabs.filter(one => !one.hidden), index = visible.indexOf(tab);
      const step = { ArrowRight: 1, ArrowLeft: -1 }[event.key];
      if (step) { event.preventDefault(); showTab(visible[(index + step + visible.length) % visible.length].dataset.settingsTab, true); }
      if (event.key === 'Home' || event.key === 'End') { event.preventDefault(); showTab((event.key === 'Home' ? visible[0] : visible.at(-1)).dataset.settingsTab, true); }
    });
  }
  showTab('game');
  for (const button of doc.querySelectorAll('[data-home-dialog]')) button.addEventListener('click', () => {
    const panel = el(button.dataset.homeDialog);
    if (panel.id === 'app-settings-dialog') showTab(button.dataset.settingsOpen ?? 'game');
    closePanels(false);
    openers.set(panel, button);
    closeMenu();
    el('account-menu').open = false;
    panel.showModal();
  });
  for (const panel of panels) {
    // A page's own sheets share the dialog's look and close by their own buttons (the admin panel's): only the
    // header's panels have a close of the header's to wire.
    panel.querySelector('[data-home-close]')?.addEventListener('click', () => panel.close());
    panel.addEventListener('close', () => {
      const opener = openers.get(panel);
      openers.delete(panel);
      // An opener inside the account menu is gone with the menu: the account chip takes the focus back.
      const target = el('account').contains(opener) ? el('account-menu').querySelector('summary') : narrow.matches ? toggle : opener;
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
      // Signed in, Settings lives in the account menu, with who is signed in; a guest, who has no account menu,
      // finds it in the main menu.
      el('app-settings-open').hidden = Boolean(account);
      el('app-settings-tab-notifications').hidden = !account;
      el('app-settings-tab-account').hidden = !account;
      if (!account && ['notifications', 'account'].includes(tabs.find(tab => tab.getAttribute('aria-selected') === 'true')?.dataset.settingsTab)) showTab('game');
      el('leaderboard-link').hidden = mode === 'off';
      el('stats-link').hidden = !account;
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

// The pages share the same controls and dialogs. Only creating a new match belongs to the lobby.
export function mountNavigation(doc, storage, { onInvite, onChange = () => {}, onFriends = () => {}, onInbox = () => {} } = {}) {
  const header = doc.querySelector('[data-navigation]');
  const page = header.dataset.navigation;
  const game = page === 'classic' || page === 'battle';
  const el = id => doc.getElementById(id);
  header.innerHTML = headerContent;
  header.insertAdjacentHTML('afterend', '<p id="host-notice" class="host-notice" role="status" hidden></p>');
  doc.body.insertAdjacentHTML('beforeend', panels);
  const current = { lobby: 'play-link-home', leaderboard: 'leaderboard-link', stats: 'stats-link', admin: 'nav-admin-link' }[page];
  if (current) el(current).setAttribute('aria-current', 'page');
  const navigation = mountHomeNavigation(doc);
  const devices = mountDevices(doc, { storage });
  const gameSettings = mountGameSettings(doc, storage);
  // The bell's actions are the Friends panel's and the lobby's: a friend accepted there is read again here, and
  // the page that mounted the navigation is told what changed.
  let friends = null;
  const inbox = mountInbox(doc, {
    storage,
    onChange: what => {
      if (what === 'friends') {
        void friends?.refresh();
      }
      onInbox(what);
    },
  });
  friends = mountFriends(doc, {
    onRequests: navigation.requests,
    onList: list => { inbox.feedFriends(list); onFriends(list); },
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
      const notice = noticeText(me, page);
      el('host-notice').textContent = notice ?? '';
      el('host-notice').hidden = !notice;
      return me;
    },
    onChange(account, mode) {
      if (previous && !account && page !== 'lobby') { location.replace('/lobby'); return; }
      previous = account;
      header.hidden = game && !account;
      navigation.setAccount(account, mode);
      devices.setAccount(account);
      gameSettings.setAccount(account);
      friends.setAccount(account);
      inbox.setAccount(account);
      onChange(account, mode);
    },
  });
  return { setOutstanding: friends.setOutstanding, inbox };
}

// Every page with the navigation has the service worker (ADR 0166), not only one whose player asked for notices: it
// is what shows the offline page instead of the browser's error when the table cannot be reached. It asks for no
// permission, and a browser that refuses it changes nothing.
if (typeof navigator !== 'undefined' && navigator.serviceWorker?.register) {
  navigator.serviceWorker.register('/sw.js').catch(() => {});
}

if (typeof document !== 'undefined' && document.querySelector('[data-navigation]:not([data-navigation="lobby"])')) {
  let storage = null;
  try { storage = globalThis.localStorage; } catch { /* The menu also works without browser storage. */ }
  mountNavigation(document, storage);
}
