import { accountRequest } from './account.js';
import { accountMoved, backoff, onLobbyChange, signedInChanged } from './changes.js';
import { preferredPresentation, presentationPath } from './battle-preferences.js';
import { turnOf } from './players.js';
import { activateSeats } from './session.js';

// What waits for a signed-in player, wherever they are in the app: the games against another person where it is their turn, the
// invitations a friend sent them, the friend requests they were sent, and an invitation of theirs a friend
// declined. One bell in the shared header counts them, and its window acts on each where it stands.

const GROUPS = Object.freeze([
  { kind: 'turn', label: 'Your turn' },
  { kind: 'invitation', label: 'Invitations' },
  { kind: 'friend', label: 'Friend requests' },
  { kind: 'declined', label: 'Declined' },
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

/**
 * The things waiting for this player, in the order the window lists them: the games against another person where it is their turn,
 * then the invitations they received, then the friend requests, then their own invitations a friend declined.
 * `mine` and `invitations` are `GET /api/lobby/mine`'s; `friends` is `GET /api/friends`'s.
 */
export function inboxItems({ mine = [], invitations = [], friends = null } = {}) {
  const items = [];
  for (const entry of mine) {
    const held = (entry.seats ?? []).map(seat => seat.seat), against = personAgainst(entry.table, held);
    if (against && turnOf(entry.table, held)?.mine) {
      const round = Number.isInteger(entry.table.round) ? ` · Round ${entry.table.round}` : '';
      items.push({ kind: 'turn', key: `turn:${entry.table.id}`, title: `Your turn against ${against}`,
        detail: `${entry.table.format ?? ''} battle${round}`.trim(), seats: entry.seats });
    }
  }
  for (const invitation of invitations) {
    items.push({ kind: 'invitation', key: `invitation:${invitation.table.id}`, table: invitation.table.id,
      title: `${invitation.from?.username ?? 'A friend'} invites you`, detail: `${invitation.table.format ?? ''} battle`.trim() });
  }
  for (const asker of friends?.incoming ?? []) {
    items.push({ kind: 'friend', key: `friend:${asker.id}`, account: asker.id, title: `${asker.username} wants to be your friend`, detail: '' });
  }
  for (const entry of mine) {
    if (entry.invited?.declined && !entry.table.over) {
      items.push({ kind: 'declined', key: `declined:${entry.table.id}`, title: `${entry.invited.username ?? 'Your friend'} declined your invitation`,
        detail: `${entry.table.format ?? ''} battle · invite another friend or open the seat from the lobby`.trim() });
    }
  }
  return items;
}

/** What the bell's badge says: nothing at zero, the count up to nine, then 9+. */
export function badgeText(count) {
  if (!Number.isInteger(count) || count <= 0) return '';
  return count > 9 ? '9+' : String(count);
}

/** The bell's accessible name: what it opens and how much waits there. */
export function bellLabel(count) {
  return count > 0 ? `Notifications · ${count} waiting` : 'Notifications · nothing waiting';
}

/**
 * The bell and its window. It reads the player's games and friends itself when the host says the player's lists
 * changed (ADR 0163), unless the page that mounts it handed it the same lists (`feed`, `feedFriends`) within `fresh`
 * milliseconds. Every action is the one the lobby or the Friends panel already offers, sent to the same route.
 */
export function mountInbox(doc, { storage = null, request = accountRequest, win = globalThis, changes = onLobbyChange, reopen = signedInChanged, fresh: freshFor = 5000, onChange = () => {} } = {}) {
  const el = id => doc.getElementById(id);
  let account = null, mine = [], invitations = [], friends = null, fed = 0, busy = false, drawn = '';
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
    const items = account ? inboxItems({ mine, invitations, friends }) : [];
    const key = JSON.stringify([account?.id ?? null, items.map(item => [item.key, item.title, item.detail])]);
    if (key === drawn) return;
    drawn = key;
    const bell = el('inbox-open'), badge = el('inbox-badge');
    bell.hidden = !account;
    badge.textContent = badgeText(items.length);
    badge.hidden = items.length === 0;
    bell.setAttribute('aria-label', bellLabel(items.length));
    appBadge(items.length);
    const groups = GROUPS.map(group => ({ label: group.label, list: items.filter(item => item.kind === group.kind) })).filter(group => group.list.length);
    el('inbox-list').replaceChildren(...(groups.length ? groups.map(({ label, list }) => {
      const section = node('section', '', 'inbox-group');
      section.append(node('h3', `${label} · ${list.length}`));
      const ul = node('ul', '', 'inbox-items');
      ul.append(...list.map(item => {
        const li = node('li', '', `inbox-item ${item.kind}`);
        li.dataset.key = item.key;
        const text = node('div', '', 'inbox-text');
        text.append(node('strong', item.title));
        if (item.detail) text.append(node('small', item.detail));
        const ways = node('div', '', 'inbox-actions');
        ways.append(...actions(item));
        li.append(text, ways);
        return li;
      }));
      section.append(ul);
      return section;
    }) : [node('p', 'Nothing waits for you right now. Your turns, invitations and friend requests will show here.', 'muted inbox-empty')]));
  }
  async function refresh(force = false) {
    if (!account) { mine = []; invitations = []; friends = null; render(); return; }
    const reading = account.id, fresh = !force && Date.now() - fed < freshFor;
    try {
      const [games, list] = await Promise.all([fresh ? null : request('/api/lobby/mine'), request('/api/friends')]);
      if (account?.id !== reading) return;
      if (games) { mine = games.tables ?? []; invitations = games.invitations ?? []; }
      friends = list;
      render();
      retry.answered();
    } catch {
      // A bell that missed one read keeps what it last knew, and reads again shortly: the change it was told of
      // will not be told twice.
      if (account?.id === reading) retry.failed();
    }
  }
  const retry = backoff(() => void refresh(true), { schedule: (run, delay) => win.setTimeout(run, delay), cancel: id => win.clearTimeout(id) });
  el('inbox-open').addEventListener('click', () => { problem(); void refresh(true); });
  // The lobby's stream opening (the page loaded, back in view, back online) and the player's own lists changing,
  // listened to while somebody is signed in: a page nobody signed in to has no bell to keep current.
  let unlisten = null;
  const listen = on => {
    if (on && !unlisten) unlisten = changes(event => { if (!doc.hidden && accountMoved(event)) void refresh(); });
    if (!on && unlisten) { unlisten(); unlisten = null; }
  };
  return {
    stop() { listen(false); retry.stop(); },
    /** Told by the sign-in whenever who is signed in changes. */
    setAccount(next) {
      if ((next?.id ?? null) === (account?.id ?? null)) return;
      account = next; mine = []; invitations = []; friends = null; fed = 0;
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
