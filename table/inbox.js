import { accountRequest } from './account.js';
import { accountMoved, backoff, noticesMoved, onLobbyChange, signedInChanged } from './changes.js';
import { preferredPresentation, presentationPath } from './battle-preferences.js';
import { sinceVisitLine, turnOf } from './players.js';
import { sentAgo } from './battle-reactions.js';
import { activateSeats } from './session.js';

// What waits for a signed-in player, wherever they are in the app: the games against another person where it is their turn, the
// invitations a friend sent them, the friend requests they were sent, and an invitation of theirs a friend
// declined. Below them, the news the host kept in the account's notice log (ADR 0201): what happened and calls for
// nothing, kept whatever the push does. One bell in the shared header counts what waits and the unread news, and its
// window acts on each where it stands.

const GROUPS = Object.freeze([
  { kind: 'turn', label: 'Your turn' },
  { kind: 'invitation', label: 'Invitations' },
  { kind: 'friend', label: 'Friend requests' },
  { kind: 'declined', label: 'Declined' },
  { kind: 'news', label: 'News' },
]);

/**
 * The person a game is against, from the seats this player does not hold, or null when nobody waits on them: a game
 * against a bot or on one shared device is played when the player chooses to, so the bell never counts its turn.
 */
function personAgainst(table, held) {
  const others = (table.seats ?? []).filter(seat => !held.includes(seat.seat));
  if (others.length === 0 || others.some(seat => seat.kind !== 'person')) return null;
  const name = others[0].name;
  return name && name !== 'Player' ? name : 'another player';
}

// A game against another person where it is this player's turn, as the bell lists it; null when none waits on them.
function turnItem(entry, now) {
  const held = (entry.seats ?? []).map(seat => seat.seat), against = personAgainst(entry.table, held);
  if (!against || !turnOf(entry.table, held)?.mine) return null;
  const round = Number.isInteger(entry.table.round) ? ` · Round ${entry.table.round}` : '';
  return { kind: 'turn', key: `turn:${entry.table.id}`, title: `Your turn against ${against}`,
    detail: [`${entry.table.format ?? ''} battle${round}`.trim(), sinceVisitLine(entry.recap, now)].filter(Boolean).join(' · '), seats: entry.seats };
}

// An invitation of this player's a friend declined, while its game is still open; null otherwise.
function declinedItem(entry) {
  if (!entry.invited?.declined || entry.table.over) return null;
  return { kind: 'declined', key: `declined:${entry.table.id}`, title: `${entry.invited.username ?? 'Your friend'} declined your invitation`,
    detail: `${entry.table.format ?? ''} battle · invite another friend or open the seat from the lobby`.trim() };
}

// Only a path of this site is a link: `//host` and `/\host` leave it.
const sitePath = url => (typeof url === 'string' && /^\/(?![/\\])/.test(url) ? url : null);

// One entry of the notice log, as the bell lists it; null for one without an id or a title.
function newsItem(entry, now) {
  if (!Number.isFinite(entry?.id) || !entry.title) return null;
  const ago = entry.at ? sentAgo(entry.at, now) || 'just now' : '';
  return { kind: 'news', key: `news:${entry.id}`, id: entry.id, title: entry.title, unread: entry.read !== true,
    detail: [entry.body, ago].filter(Boolean).join(' · '), url: sitePath(entry.url) };
}

/**
 * The things waiting for this player, in the order the window lists them: the games against another person where it is their turn,
 * then the invitations they received, then the friend requests, then their own invitations a friend declined,
 * then the news, newest first. `mine` and `invitations` are `GET /api/lobby/mine`'s; `friends` is `GET /api/friends`'s;
 * `news` is `GET /api/account/notices`'s `notices`.
 */
export function inboxItems({ mine = [], invitations = [], friends = null, news = [] } = {}, now = Date.now()) {
  return [
    ...mine.map(entry => turnItem(entry, now)),
    ...invitations.map(invitation => ({ kind: 'invitation', key: `invitation:${invitation.table.id}`, table: invitation.table.id,
      title: `${invitation.from?.username ?? 'A friend'} invites you`, detail: `${invitation.table.format ?? ''} battle`.trim() })),
    ...(friends?.incoming ?? []).map(asker => ({ kind: 'friend', key: `friend:${asker.id}`, account: asker.id, title: `${asker.username} wants to be your friend`, detail: '' })),
    ...mine.map(declinedItem),
    ...(news ?? []).map(entry => newsItem(entry, now)),
  ].filter(Boolean);
}

/** What the bell counts: what waits, and the news not read yet; the badge shows their sum. */
export function inboxCount(items) {
  const waiting = items.filter(item => item.kind !== 'news').length;
  return { waiting, unread: items.filter(item => item.kind === 'news' && item.unread).length };
}

/** The newest news entry's id, which a "read up to" names; null when no news is unread. */
export function readUpTo(items) {
  const unread = items.filter(item => item.kind === 'news' && item.unread).map(item => item.id);
  return unread.length ? Math.max(...items.filter(item => item.kind === 'news').map(item => item.id)) : null;
}

/** What the bell's badge says: nothing at zero, the count up to nine, then 9+. */
export function badgeText(count) {
  if (!Number.isInteger(count) || count <= 0) return '';
  return count > 9 ? '9+' : String(count);
}

/** The bell's accessible name: what it opens, how much waits there, and how much news is unread. */
export function bellLabel(waiting, unread = 0) {
  const parts = [waiting > 0 ? `${waiting} waiting` : 'nothing waiting'];
  if (unread > 0) parts.push(`${unread} new`);
  return `Notifications · ${parts.join(' · ')}`;
}

/**
 * The bell and its window. It reads the player's games and friends itself when the host says the player's lists
 * changed (ADR 0164), unless the page that mounts it handed it the same lists (`feed`, `feedFriends`) within `fresh`
 * milliseconds. Every action is the one the lobby or the Friends panel already offers, sent to the same route.
 */
export function mountInbox(doc, { storage = null, request = accountRequest, win = globalThis, changes = onLobbyChange, reopen = signedInChanged, fresh: freshFor = 5000, onChange = () => {} } = {}) {
  const el = id => doc.getElementById(id);
  let account = null, mine = [], invitations = [], friends = null, news = [], keepsNews = true, marking = false, refused = null, readThrough = 0, issued = 0, applied = 0, newsIssued = 0, newsApplied = 0, fed = 0, busy = false, drawn = '';
  // The news unread when the window showed it: marked new while the window stays open, though the host has it read.
  const shownNew = new Set();
  const dialog = () => doc.getElementById('inbox-dialog');
  const problem = (text = '') => { el('inbox-problem').textContent = text; el('inbox-problem').hidden = !text; };

  function node(tag, text, className) {
    const result = doc.createElement(tag);
    if (text) result.textContent = text;
    if (className) result.className = className;
    return result;
  }
  function enter(seats) {
    activateSeats(seats, storage);
    win.location.assign(`${presentationPath(preferredPresentation(storage))}?${new URLSearchParams(seats.map(item => [item.seat, item.token]))}`);
  }
  function action(label, className, act) {
    const button = node('button', label, className);
    button.type = 'button';
    button.addEventListener('click', async () => {
      if (busy) return;
      busy = true;
      button.disabled = true;
      problem();
      try { await act(); }
      catch (error) { problem(error.message || 'Could not reach the host. Try again.'); }
      finally { busy = false; button.disabled = false; }
    });
    return button;
  }
  function actions(item) {
    switch (item.kind) {
      case 'turn': return [action('Play →', 'primary', async () => enter(item.seats))];
      case 'invitation': return [
        action('Accept & play →', 'primary', async () => enter((await request(`/api/lobby/tables/${encodeURIComponent(item.table)}/accept`, {})).seats)),
        action('Decline', 'secondary', async () => { await request(`/api/lobby/tables/${encodeURIComponent(item.table)}/decline`, {}); await refresh(true); onChange('invitations'); }),
      ];
      case 'friend': return [
        action('Accept', 'primary', async () => { await request(`/api/friends/requests/${encodeURIComponent(item.account)}/accept`, {}); await refresh(true); onChange('friends'); }),
        action('Decline', 'secondary', async () => { await request(`/api/friends/requests/${encodeURIComponent(item.account)}`, {}, undefined, 'DELETE'); await refresh(true); onChange('friends'); }),
      ];
      case 'news': {
        if (!item.url) return [];
        const link = node('a', 'Open →', 'button-link');
        link.href = item.url;
        return [link];
      }
      default: {
        const link = node('a', 'Open the lobby →', 'button-link');
        link.href = '/lobby';
        return [link];
      }
    }
  }
  // An installed app shows the same count on its icon, where the platform offers one; a refusal changes nothing.
  function appBadge(count) {
    const navigator = win.navigator;
    if (typeof navigator?.setAppBadge !== 'function') return;
    const pending = count ? navigator.setAppBadge(count) : navigator.clearAppBadge?.();
    pending?.catch?.(() => {});
  }
  function render() {
    const items = account ? inboxItems({ mine, invitations, friends, news }) : [];
    const { waiting, unread } = inboxCount(items), count = waiting + unread;
    if (dialog()?.open) void markRead(items);
    const key = JSON.stringify([account?.id ?? null, items.map(item => [item.key, item.title, item.detail, item.unread || shownNew.has(item.key)]), unread]);
    if (key === drawn) return;
    drawn = key;
    const bell = el('inbox-open'), badge = el('inbox-badge');
    bell.hidden = !account;
    badge.textContent = badgeText(count);
    badge.hidden = count === 0;
    bell.setAttribute('aria-label', bellLabel(waiting, unread));
    // Where the bar has no room for the bell (the battle view under 360 px, battle.css), the menu holds it: the
    // same count on the menu's button, and an entry that opens the same window (home-navigation.md, "The bell").
    // The menu's button says the count too, as its description: its drawn badge is silent.
    const entry = doc.getElementById('inbox-open-menu');
    if (entry) entry.hidden = !account;
    for (const one of [doc.getElementById('inbox-menu-count'), doc.getElementById('menu-inbox-badge')].filter(Boolean)) {
      one.textContent = badgeText(count); one.hidden = count === 0;
    }
    const said = doc.getElementById('menu-inbox-said');
    if (said) said.textContent = count ? bellLabel(waiting, unread) : '';
    appBadge(count);
    const groups = GROUPS.map(group => ({ label: group.label, list: items.filter(item => item.kind === group.kind) })).filter(group => group.list.length);
    el('inbox-list').replaceChildren(...(groups.length ? groups.map(({ label, list }) => {
      const section = node('section', '', 'inbox-group');
      section.append(node('h3', `${label} · ${list.length}`));
      const ul = node('ul', '', 'inbox-items');
      ul.append(...list.map(item => {
        const fresh = item.kind === 'news' && (item.unread || shownNew.has(item.key));
        const li = node('li', '', `inbox-item ${item.kind}${fresh ? ' unread' : ''}`);
        li.dataset.key = item.key;
        const text = node('div', '', 'inbox-text');
        if (fresh) text.append(node('span', 'New', 'inbox-new'));
        text.append(node('strong', item.title));
        if (item.detail) text.append(node('small', item.detail));
        const ways = node('div', '', 'inbox-actions');
        ways.append(...actions(item));
        li.append(text, ways);
        return li;
      }));
      section.append(ul);
      return section;
    }) : [node('p', 'Nothing waits for you right now. Your turns, invitations, friend requests and news will show here.', 'muted inbox-empty')]));
  }
  // The news shown in the open window is read: the host is told up to the newest entry, and tells the player's other
  // pages through their stream. A refusal leaves it unread, to be told again at the next opening.
  async function markRead(items) {
    const upTo = readUpTo(items);
    if (upTo === null || upTo === refused || marking || !account) return;
    const reading = account.id;
    marking = true;
    for (const item of items) if (item.kind === 'news' && item.unread) shownNew.add(item.key);
    try {
      await request('/api/account/notices/read', { upTo });
      if (account?.id !== reading) return;
      readThrough = Math.max(readThrough, upTo);
      news = news.map(entry => (entry.id <= upTo ? { ...entry, read: true } : entry));
    } catch {
      // Unread still: the badge keeps counting it, and the window is not asked to tell it again until it is reopened.
      refused = upTo;
    } finally {
      marking = false;
    }
    render();
  }
  // The account's news, read beside its lists: a host that keeps none (404) is not asked again, and a read that
  // failed keeps what the bell last knew.
  async function readNews() {
    if (!keepsNews) return null;
    try { return (await request('/api/account/notices')).notices ?? []; }
    catch (error) {
      if (error?.status === 404) { keepsNews = false; return null; }
      return { failed: true };
    }
  }
  async function refresh(force = false) {
    if (!account) { mine = []; invitations = []; friends = null; render(); return; }
    const reading = account.id, fresh = !force && Date.now() - fed < freshFor, order = ++issued, newsOrder = ++newsIssued;
    try {
      const [games, list, kept] = await Promise.all([fresh ? null : request('/api/lobby/mine'), request('/api/friends'), readNews()]);
      // A read that answers after a later one was drawn is older than what the bell shows: dropped.
      if (account?.id !== reading || order < applied) return;
      applied = order;
      if (games) { mine = games.tables ?? []; invitations = games.invitations ?? []; }
      friends = list;
      // A list read before a mark the host confirmed may answer after it: what was marked stays read.
      // News a later read of the news alone already drew is older than it: kept as drawn.
      if (Array.isArray(kept) && newsOrder > newsApplied) {
        newsApplied = newsOrder;
        news = kept.map(entry => (entry?.id <= readThrough ? { ...entry, read: true } : entry));
      }
      render();
      // The lists answered and the news did not: read again shortly, as when the lists fail.
      if (kept?.failed) retry.failed(); else retry.answered();
    } catch {
      // A bell that missed one read keeps what it last knew, and reads again shortly: the change it was told of
      // will not be told twice.
      if (account?.id === reading) retry.failed();
    }
  }
  // Notices marked read on another of the player's pages: only the news is read again (#1152).
  async function refreshNews() {
    if (!account) return;
    // Its own order: a read of the lists issued before it still draws its lists, though not its older news.
    const reading = account.id, order = ++newsIssued;
    const kept = await readNews();
    if (account?.id !== reading || order < newsApplied || !Array.isArray(kept)) {
      if (kept?.failed && account?.id === reading) retry.failed();
      return;
    }
    newsApplied = order;
    news = kept.map(entry => (entry?.id <= readThrough ? { ...entry, read: true } : entry));
    render();
  }
  const retry = backoff(() => void refresh(true), { schedule: (run, delay) => win.setTimeout(run, delay), cancel: id => win.clearTimeout(id) });
  for (const opener of [el('inbox-open'), doc.getElementById('inbox-open-menu')].filter(Boolean)) opener.addEventListener('click', () => {
    problem(); refused = null;
    // The news already drawn is read as the window opens, whether or not the read that follows answers.
    void refresh(true);
    if (account) void markRead(inboxItems({ mine, invitations, friends, news }));
  });
  // Closing the window lets the news it showed as new be drawn as read.
  dialog()?.addEventListener('close', () => { refused = null; if (shownNew.size) { shownNew.clear(); render(); } });
  // The lobby's stream opening (the page loaded, back in view, back online) and the player's own lists changing,
  // listened to while somebody is signed in: a page nobody signed in to has no bell to keep current.
  let unlisten = null;
  const listen = on => {
    if (on && !unlisten) unlisten = changes(event => {
      if (doc.hidden) return;
      if (accountMoved(event)) void refresh();
      else if (noticesMoved(event)) void refreshNews();
    });
    if (!on && unlisten) { unlisten(); unlisten = null; }
  };
  return {
    stop() { listen(false); retry.stop(); },
    /** Told by the sign-in whenever who is signed in changes. */
    setAccount(next) {
      if ((next?.id ?? null) === (account?.id ?? null)) return;
      account = next; mine = []; invitations = []; friends = null; news = []; keepsNews = true; readThrough = 0; fed = 0;
      shownNew.clear();
      retry.stop();
      listen(Boolean(account));
      // The lobby's stream knows who is signed in from when it opened: it is opened again for whoever is now.
      reopen();
      render();
      void refresh(true);
    },
    /** The lobby's own read of the player's games: the bell shows it and skips its own read for a while. */
    feed({ tables = [], invitations: received = [] } = {}) {
      if (!account) return;
      mine = tables; invitations = received; fed = Date.now();
      render();
    },
    /** Every friends list the Friends panel reads. */
    feedFriends(list) {
      if (!account || !list) return;
      friends = list;
      render();
    },
  };
}
