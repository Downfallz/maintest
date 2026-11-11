// A player's friends (ADR 0129): the list, who asked, who was asked, and asking by exact name. The host owns
// every rule; this page only checks a name's shape before asking, as the sign-in does, and draws what the host
// says. The pure part is what the panel says; the mount does the asking.

import { accountRequest, confirmAction, nameProblem } from './account.js';
import { accountMoved, backoff, onLobbyChange } from './changes.js';

/** The sections of the panel from the host's list: what each shows, and what is said when it is empty. */
export function friendSections(list) {
  const friends = list?.friends ?? [];
  const incoming = list?.incoming ?? [];
  const outgoing = list?.outgoing ?? [];
  return {
    incoming: { items: incoming, title: incoming.length === 1 ? 'One player asked to be your friend' : `${incoming.length} players asked to be your friends`, shown: incoming.length > 0 },
    friends: { items: friends, title: 'Friends', empty: 'No friends yet. Ask a player by the name on their cards.', shown: true },
    outgoing: { items: outgoing, title: 'Waiting for an answer', shown: outgoing.length > 0 },
  };
}

/** What asking a name came to, as the panel says it. */
export function askedText(answer) {
  if (!answer?.other) return '';
  return answer.accepted ? `${answer.other.username} had already asked you: you are friends now.` : `Asked. ${answer.other.username} will see your request.`;
}


export function mountFriends(doc, { request = accountRequest, nav = globalThis.navigator, changes = onLobbyChange, onInvite = null, onRequests = () => {}, onList = () => {} } = {}) {
  const el = id => doc.getElementById(id);
  let account = null;
  let list = null;
  let drawn = '';
  let busy = false;
  let outstanding = false;
  const problem = (text = '') => { el('friend-problem').textContent = text; el('friend-problem').hidden = !text; };
  const note = (text = '') => { el('friend-note').textContent = text; el('friend-note').hidden = !text; };

  function node(tag, text, className) {
    const result = doc.createElement(tag);
    if (text) result.textContent = text;
    if (className) result.className = className;
    return result;
  }

  function action(label, className, act, confirm = null) {
    const button = node('button', label, className);
    button.type = 'button';
    button.addEventListener('click', async () => {
      if (busy) return;
      busy = true;
      try {
        if (confirm && !await confirm()) return;
        button.disabled = true;
        problem();
        await act();
        await refresh(true);
      } catch (error) { problem(error.message || 'Could not reach the host. Try again.'); }
      finally { busy = false; button.disabled = false; }
    });
    return button;
  }

  function row(link, actions) {
    const item = node('li', '', 'friend-row');
    item.append(node('span', link.username, 'friend-name'));
    const buttons = node('span', '', 'friend-actions');
    buttons.append(...actions);
    item.append(buttons);
    return item;
  }

  function removeButton(link) {
    return action('Remove', 'secondary',
      () => request(`/api/friends/${encodeURIComponent(link.id)}`, {}, undefined, 'DELETE'),
      () => confirmAction(doc, `Remove ${link.username}?`, 'You will no longer be friends. They will not be notified.', 'Remove friend'));
  }

  function inviteButton(link) {
    const button = node('button', 'Invite', 'primary');
    button.type = 'button';
    button.disabled = outstanding;
    button.title = outstanding ? 'You already have an invitation waiting. Cancel it first, or wait for the answer.' : `Keep a seat for ${link.username}`;
    button.addEventListener('click', () => onInvite?.(link));
    return button;
  }
  function render() {
    const key = JSON.stringify([account?.id ?? null, list, outstanding]);
    if (key === drawn) return;
    drawn = key;
    el('friends-panel').hidden = !account;
    onRequests(account ? (list?.incoming?.length ?? 0) : 0);
    // The lobby's recent opponents say where each friendship stands: they follow every list read here.
    onList(account ? list : null);
    if (!account || !list) {
      el('friend-incoming').hidden = true;
      el('friend-outgoing').hidden = true;
      el('friend-incoming-list').replaceChildren();
      el('friend-outgoing-list').replaceChildren();
      el('friend-count').textContent = '';
      el('friend-list').replaceChildren(...(account ? [node('li', 'Finding your friends…', 'empty-state')] : []));
      return;
    }
    const sections = friendSections(list);
    el('friend-incoming-title').textContent = sections.incoming.title;
    el('friend-incoming').hidden = !sections.incoming.shown;
    el('friend-incoming-list').replaceChildren(...sections.incoming.items.map(link => row(link, [
      action('Accept', 'primary', () => request(`/api/friends/requests/${encodeURIComponent(link.id)}/accept`, {})),
      action('Decline', 'secondary', () => request(`/api/friends/requests/${encodeURIComponent(link.id)}`, {}, undefined, 'DELETE')),
    ])));
    el('friend-list').replaceChildren(...(sections.friends.items.length
      ? sections.friends.items.map(link => row(link, [...(onInvite ? [inviteButton(link)] : []), removeButton(link)]))
      : [node('li', sections.friends.empty, 'empty-state')]));
    el('friend-count').textContent = sections.friends.items.length || '';
    el('friend-outgoing').hidden = !sections.outgoing.shown;
    el('friend-outgoing-list').replaceChildren(...sections.outgoing.items.map(link => row(link, [
      action('Withdraw', 'secondary', () => request(`/api/friends/requests/${encodeURIComponent(link.id)}`, {}, undefined, 'DELETE')),
    ])));
  }

  async function refresh(quiet = false) {
    if (!account) { list = null; render(); return; }
    // A list read for one account is drawn for that account only: a sign-out and a sign-in while it was in flight
    // make it the previous player's, and it is dropped.
    const forAccount = account.id;
    try {
      const read = await request('/api/friends');
      if (account?.id !== forAccount) return;
      list = read;
      render();
      retry.answered();
    } catch (error) {
      if (!quiet && account?.id === forAccount) problem(error.message || 'Could not load your friends. Try again.');
      // Read again shortly: the change that asked for this read will not be announced twice.
      if (account?.id === forAccount) retry.failed();
    }
  }
  const retry = backoff(() => void refresh(true));

  el('friend-ask').addEventListener('submit', async event => {
    event.preventDefault();
    if (busy) return;
    const value = el('friend-username').value;
    const shape = nameProblem(value);
    if (shape) { problem(shape); el('friend-username').focus(); return; }
    busy = true;
    el('friend-ask-submit').disabled = true;
    problem();
    note();
    try {
      const answer = await request('/api/friends/requests', { username: value.trim() });
      note(askedText(answer));
      el('friend-username').value = '';
      await refresh(true);
    } catch (error) {
      problem(error.message || 'Could not ask. Try again.');
    } finally { busy = false; el('friend-ask-submit').disabled = false; }
  });

  nav?.serviceWorker?.addEventListener?.('message', event => { if (event.data?.kind === 'notice') void refresh(true); });
  // Read when the host says this player's lists changed, and when the lobby's stream opens (ADR 0163): no clock.
  // Listened to while somebody is signed in only.
  let unlisten = null;
  const listen = on => {
    if (on && !unlisten) unlisten = changes(event => { if (accountMoved(event) && account && !busy) void refresh(true); });
    if (!on && unlisten) { unlisten(); unlisten = null; }
  };
  render();
  return {
    stop: () => { listen(false); retry.stop(); },
    /** Reads the list again: a request answered from the bell changes it. */
    refresh: () => refresh(true),
    /** Told by the lobby whether an invitation of this player's is waiting: one at a time, so Invite waits too. */
    setOutstanding(next) {
      if (Boolean(next) === outstanding) return;
      outstanding = Boolean(next);
      render();
    },
    /** Told by the sign-in whenever who is signed in changes. */
    setAccount(next) {
      if ((next?.id ?? null) === (account?.id ?? null)) return;
      account = next;
      list = null;
      retry.stop();
      listen(Boolean(account));
      render();
      problem();
      note();
      void refresh();
    },
  };
}
