import { preferredPresentation, presentationPath } from './battle-preferences.js';
import { nextPath } from './account.js';
import { mountNavigation } from './navigation.js';
export { codePath } from './navigation.js';
import { activateSeats, savedSeats } from './session.js';

export function visibleTables(tables, mine, all = false) {
  const held = new Set(mine.map(entry => entry.table.id));
  return tables.filter(table => !table.over && (all || (table.openSeats > 0 && !held.has(table.id))));
}

export function tableState(table) {
  if (table.over) return 'Finished';
  if (table.openSeats > 0) return table.waiting ? 'Waiting for a player' : 'Player needed';
  if (table.waiting) return 'Waiting to start';
  return Number.isInteger(table.round) ? `Round ${table.round}` : 'In progress';
}

export async function lobbyRequest(path, body, token, fetchImpl = globalThis.fetch.bind(globalThis), method = body === undefined ? 'GET' : 'POST') {
  const headers = {};
  if (token) headers['X-Seat-Token'] = token;
  const options = { method, headers };
  if (body !== undefined) { headers['Content-Type'] = 'application/json'; options.body = JSON.stringify(body); }
  const response = await fetchImpl(path, options);
  const text = await response.text();
  let result;
  try { result = text ? JSON.parse(text) : {}; } catch { result = { message: text }; }
  if (!response.ok) throw new Error(result.message || `The host answered ${response.status}. Try again.`);
  return result;
}

/** Who sits where, as the card's versus line says it: each side's name and what it is (a person, a bot, an open place). */
export function versus(table) {
  if (Array.isArray(table.seats) && table.seats.length === 2) return table.seats.map(seat => ({ seat: seat.seat, kind: seat.kind, name: seat.name }));
  return (table.players ?? []).map((name, index) => ({ seat: `player${index + 1}`, kind: 'person', name }));
}

/**
 * Whose turn it is at an unfinished table, from the seats the host says are being asked something now, and
 * whether it is this browser's: null before the match begins, once it is over, or while nobody is asked (the
 * bots are thinking or a round is resolving).
 */
export function turnOf(table, held = []) {
  if (table.over || table.waiting || !Array.isArray(table.seats)) return null;
  const asked = table.seats.filter(seat => seat.turn);
  if (asked.length === 0) return null;
  if (asked.some(seat => held.includes(seat.seat))) {
    return { mine: true, text: asked.length > 1 ? 'Your turn · both players choose' : 'Your turn' };
  }
  if (asked.every(seat => seat.kind === 'open')) return { mine: false, text: 'Waiting for a player to take the open seat' };
  if (asked.length > 1) return { mine: false, text: 'Both players to play' };
  const name = asked[0].name && asked[0].name !== 'Player' ? asked[0].name : 'The other player';
  return { mine: false, text: `${name}'s turn` };
}

export const GAMES_PER_PAGE = 6;
/** The two kinds of game the lobby lists apart (ADR 0147), in the order it shows them. */
export const KINDS = Object.freeze([['players', 'Against players'], ['bots', 'Against bots']]);

/** Whether a table is played against a bot or between people (a shared device among them), by who sits. */
export function gameKind(table) {
  return (table?.seats ?? []).some(seat => seat.kind === 'bot') ? 'bots' : 'players';
}

/** The player's games in progress, by kind: in each, the ones waiting for this player first, then the host's order. */
export function ongoingGames(mine) {
  const ongoing = mine.filter(entry => !entry.table.over);
  const waiting = entry => turnOf(entry.table, (entry.seats ?? []).map(held => held.seat))?.mine === true;
  const ordered = [...ongoing.filter(waiting), ...ongoing.filter(entry => !waiting(entry))];
  return { players: ordered.filter(entry => gameKind(entry.table) === 'players'), bots: ordered.filter(entry => gameKind(entry.table) === 'bots') };
}

/**
 * One page of finished games of one kind where the host keeps no results (a host without accounts): the finished
 * tables this browser's seats reach, in the shape the host's pages have. A page past the end shows the last one.
 */
export function localFinished(mine, kind, page = 0, size = GAMES_PER_PAGE) {
  const list = mine.filter(entry => entry.table.over && gameKind(entry.table) === kind).map(entry => {
    const held = (entry.seats ?? []).map(one => one.seat), other = versus(entry.table).find(side => !held.includes(side.seat));
    return { table: entry.table.id, format: entry.table.format, casual: entry.table.casual === true, finishedAt: null, rounds: entry.table.round ?? null, outcome: null,
      opponent: other ? { kind: other.kind, name: other.name } : { kind: 'self', name: 'Shared device' }, seats: entry.seats, session: null };
  });
  const pages = Math.max(1, Math.ceil(list.length / size)), at = Math.min(Math.max(0, page), pages - 1);
  return { total: list.length, pages, page: at + 1, items: list.slice(at * size, (at + 1) * size) };
}

const OUTCOMES = Object.freeze({ won: 'Victory', lost: 'Defeat', draw: 'Draw', both: 'Shared device' });
/** What a finished game's card says: the result as a tag, who it was against, and how long it ran. */
export function finishedSummary(item) {
  return { title: `${item.format ?? ''} battle`.trim(), tag: OUTCOMES[item.outcome] ?? 'Finished', tone: item.outcome ?? 'finished', casual: item.casual === true,
    against: item.opponent?.name ?? 'Unknown', bot: item.opponent?.kind === 'bot',
    detail: Number.isInteger(item.rounds) && item.rounds > 0 ? `${item.rounds} round${item.rounds === 1 ? '' : 's'}` : '',
    action: item.seats?.length ? 'View game →' : item.session ? 'Watch the replay →' : null };
}

/** What a group of games in progress says of its limit (ADR 0147): how many of how many, and whether it is full. */
export function ongoingLimit(count, limit) {
  if (!Number.isInteger(limit)) return { text: String(count), full: false };
  return { text: `${count} / ${limit}`, full: count >= limit };
}

function cardAction(table, entry) {
  if (!entry) return 'Take a seat →';
  return table.over ? 'View game →' : 'Resume game →';
}

/**
 * What an inviter's card says of the seat kept for a friend (ADR 0129): waiting, with a cancel; or declined (or
 * run out), with the three ways out; null when the seat is kept for nobody and nobody declined it.
 */
export function invitedState(invited) {
  if (!invited) return null;
  if (invited.free) return { tag: 'Seat kept for nobody', waiting: false, actions: ['reinvite', 'open', 'close'] };
  if (invited.declined) return { tag: `${invited.username} declined`, waiting: false, actions: ['reinvite', 'open', 'close'] };
  return { tag: `Waiting for ${invited.username}…`, waiting: true, actions: ['cancel'] };
}

/**
 * What a "Rematch" link asks of the lobby (ADR 0129, stage 4): the opponent to choose again, as `?rematch=` names
 * it (a friend's account id, `bot`, or `hotseat`), and the format; null when the query asks for none.
 */
export function rematchRequest(search) {
  const params = new URLSearchParams(search ?? '');
  const again = params.get('rematch');
  if (!again) return null;
  const format = params.get('format') || null;
  if (again === 'bot' || again === 'hotseat') return { opponent: again, friend: null, format };
  // A rematch of an unranked game (ADR 0155) starts unranked too.
  const ranked = params.get('ranked') !== '0';
  return /^acct_[a-z2-7]{16}$/.test(again) ? { opponent: 'friend', friend: again, format, ranked } : null;
}

/**
 * Which friend the create dialog starts on (ADR 0129): the one asked for when they are in the list, nobody when
 * they are not or none was asked for, so a rematch against a player who is not a friend never sends an
 * invitation to whoever happens to be first in the list.
 */
export function friendChoice(friends, wanted) {
  const found = wanted && (friends ?? []).some(friend => friend.id === wanted);
  return { value: found ? wanted : '', missing: Boolean(wanted) && !found };
}

/** The line an invitation card leads with: who asks, to what. */
export function invitationLine(invitation) {
  const from = invitation?.from?.username ?? 'A player';
  return `${from} invites you to a ${invitation?.table?.format ?? ''} battle`.replace('  ', ' ');
}

function statusText(state) {
  if (!state.recording) return 'This host is not saving games. New tables are unavailable until recording is enabled.';
  const tables = state.tables.length === 1 ? 'table' : 'tables';
  return `${state.tables.length} ${tables} ongoing · updates automatically`;
}

export function mountLobby(doc, location, storage) {
  const el = id => doc.getElementById(id);
  // Games open in the battle view (ADR 0156); only a browser that asked for the archived table opens /play.
  const presentation = preferredPresentation(storage);
  let listing = null;
  let mine = [], mineTab = 'ongoing', limit = null;
  // Each kind's finished page, read from the host a page at a time (ADR 0147); `seen` is what the last read was of.
  const finished = { players: { page: 0, data: null }, bots: { page: 0, data: null } };
  let finishedSeen = null, finishedReads = 0;
  let invitations = [];
  let friends = [];
  let reinviting = null;
  let pendingRematch = rematchRequest(location.search);
  let onOutstanding = () => {};
  let showAll = false;
  let loading = false;
  let drawn = '';
  let busy = false;
  let account = null;
  let mode = 'loading';
  let revision = 0;
  let refreshAgain = false;
  let adopted = false;
  const signInFirst = () => mode !== 'off' && account === null;
  const problem = (text = '') => { el('lobby-problem').textContent = text; el('lobby-problem').hidden = !text; };
  function node(tag, text, className) {
    const result = doc.createElement(tag);
    if (text) result.textContent = text;
    if (className) result.className = className;
    return result;
  }
  function enter(entry) {
    activateSeats(entry.seats, storage);
    // The link also carries the seats so browsers that refuse storage can still play this visit.
    location.assign(`${presentationPath(presentation)}?${new URLSearchParams(entry.seats.map(item => [item.seat, item.token]))}`);
  }
  function askSignIn() {
    problem('Sign in to play, then choose your player name.');
    el('sign-in-panel')?.scrollIntoView?.({ block: 'center' });
  }
  async function act(button, action) {
    if (signInFirst()) { askSignIn(); return; }
    if (busy) return;
    busy = true;
    button.disabled = true;
    problem();
    try { enter(await action()); }
    catch (error) { problem(error.message || 'Could not reach the table. Try again.'); await refresh(); }
    finally { busy = false; button.disabled = false; }
  }
  function tableAction(label, className, path, method = 'POST', body = {}) {
    const button = node('button', label, className);
    button.type = 'button';
    button.addEventListener('click', async () => {
      if (busy) return;
      busy = true;
      button.disabled = true;
      problem();
      try { await lobbyRequest(path, body, undefined, undefined, method); await refresh(); }
      catch (error) { problem(error.message || 'Could not reach the host. Try again.'); }
      finally { busy = false; button.disabled = false; }
    });
    return button;
  }
  function title(text, casual) {
    const heading = node('h3', text);
    if (casual) heading.append(node('span', 'Unranked', 'casual-tag'));
    return heading;
  }
  function card(table, entry) {
    const root = node('article', '', `game-card${entry ? ' mine' : ''}`);
    const top = node('div', '', 'game-card-top');
    const turn = turnOf(table, (entry?.seats ?? []).map(held => held.seat));
    const invited = invitedState(entry?.invited);
    // An unranked game (ADR 0155) says so beside its title.
    top.append(title(`${table.format} battle`, table.casual === true), turn?.mine
      ? node('span', 'Your turn', 'status-tag your-turn')
      : node('span', invited?.tag ?? tableState(table), `status-tag${table.openSeats > 0 ? ' available' : ''}${invited ? ' invited' : ''}`));
    const line = node('div', '', 'versus');
    const sides = versus(table);
    sides.forEach((side, index) => {
      if (index > 0) line.append(node('span', 'vs', 'vs'));
      const who = node('span', '', `side ${side.seat}${entry?.seats?.some(held => held.seat === side.seat) ? ' you' : ''}`);
      const name = node('span', side.name, `who ${side.kind}`);
      who.append(name);
      if (side.kind === 'bot') who.append(node('span', 'bot', 'bot-tag'));
      line.append(who);
    });
    line.setAttribute('aria-label', sides.map(side => side.name).join(' versus '));
    root.append(top, line);
    if (turn) root.append(node('p', turn.mine ? `${turn.text} · ${tableState(table)}` : turn.text, `game-turn${turn.mine ? ' mine' : ''}`));
    root.append(node('p', table.id, 'game-id'));
    const button = node('button', cardAction(table, entry));
    button.type = 'button';
    button.setAttribute('aria-label', `${entry ? 'Resume' : 'Join'} ${table.format} table ${table.id}`);
    if (entry) { button.className = 'primary'; button.addEventListener('click', () => enter(entry)); }
    else button.addEventListener('click', () => act(button, () => lobbyRequest(`/api/lobby/tables/${encodeURIComponent(table.id)}/join`, {})));
    if (entry || table.openSeats > 0) root.append(button);
    if (invited) {
      // The ways out of a seat kept for a friend (ADR 0129): cancel while they have not answered; after a no,
      // another friend, everyone, or nobody.
      const ways = node('div', '', 'card-ways');
      const at = `/api/lobby/tables/${encodeURIComponent(table.id)}`;
      for (const way of invited.actions) {
        if (way === 'cancel') ways.append(tableAction('Cancel invitation', 'secondary', `${at}/invitation`, 'DELETE'));
        if (way === 'reinvite') { const b = node('button', 'Invite another friend', 'secondary'); b.type = 'button'; b.addEventListener('click', () => openCreate({ reinvite: table.id })); ways.append(b); }
        if (way === 'open') ways.append(tableAction('Open the seat to everyone', 'secondary', `${at}/open`));
        if (way === 'close') ways.append(tableAction('Close the table', 'secondary danger', at, 'DELETE'));
      }
      root.append(ways);
    }
    return root;
  }
  // A table waiting for this player (ADR 0129): who asks, and the two answers.
  function invitationCard(invitation) {
    const table = invitation.table;
    const root = node('article', '', 'game-card invitation');
    const top = node('div', '', 'game-card-top');
    top.append(node('h3', invitationLine(invitation)), node('span', 'Invitation', 'status-tag invited'));
    root.append(top, node('p', `${table.format} battle · ${table.id}`, 'game-id'));
    const ways = node('div', '', 'card-ways');
    const accept = node('button', 'Accept & play →', 'primary');
    accept.type = 'button';
    accept.addEventListener('click', () => act(accept, () => lobbyRequest(`/api/lobby/tables/${encodeURIComponent(table.id)}/accept`, {})));
    ways.append(accept, tableAction('Decline', 'secondary', `/api/lobby/tables/${encodeURIComponent(table.id)}/decline`));
    root.append(ways);
    return root;
  }
  function group(kind, label, count) {
    const root = node('section', '', `mine-group ${kind}`);
    root.dataset.kind = kind; root.setAttribute('aria-label', label);
    const head = node('header', '', 'mine-group-head');
    head.append(node('h3', label), node('span', count.text, `lobby-count${count.full ? ' full' : ''}`));
    root.append(head);
    return root;
  }
  // A kind's games in progress: all of them, since the host keeps at most a few of each (ADR 0147).
  function ongoingGroup(kind, label, entries) {
    const count = ongoingLimit(entries.length, limit), root = group(kind, label, count);
    const cards = node('div', '', 'lobby-grid');
    cards.append(...(entries.length ? entries.map(entry => card(entry.table, entry)) : [node('p', `No games ${label.toLowerCase()} in progress.`, 'empty-state')]));
    root.append(cards);
    if (count.full) root.append(node('p', `That is the most at once: finish one to start another ${kind === 'bots' ? 'against a bot' : 'against a player'}.`, 'mine-limit'));
    return root;
  }
  // A finished game, as the kept results tell it: the result, who it was against, and the way back to it.
  function finishedCard(item) {
    const summary = finishedSummary(item);
    const root = node('article', '', `game-card mine finished ${summary.tone}`);
    const top = node('div', '', 'game-card-top');
    top.append(title(summary.title, summary.casual), node('span', summary.tag, `status-tag outcome ${summary.tone}`));
    const line = node('div', '', 'versus');
    const who = node('span', '', 'side');
    who.append(node('span', `vs ${summary.against}`, `who ${item.opponent?.kind ?? 'person'}`));
    if (summary.bot) who.append(node('span', 'bot', 'bot-tag'));
    line.append(who);
    const when = item.finishedAt ? new Date(item.finishedAt).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' }) : '';
    root.append(top, line, node('p', [when, summary.detail].filter(Boolean).join(' · ') || ' ', 'game-turn'));
    if (summary.action && item.seats?.length) {
      const button = node('button', summary.action, 'primary'); button.type = 'button';
      button.addEventListener('click', () => enter({ seats: item.seats }));
      root.append(button);
    } else if (summary.action) {
      const link = node('a', summary.action, 'button-link'); link.href = item.session;
      root.append(link);
    }
    return root;
  }
  // A kind's finished games, a page at a time from the host, with that kind's own pages.
  function finishedGroup(kind, label) {
    const shown = finished[kind].data;
    const root = group(kind, label, { text: shown ? String(shown.total) : '…', full: false });
    const cards = node('div', '', 'lobby-grid');
    if (!shown) cards.append(node('p', 'Finding your finished games…', 'empty-state'));
    else cards.append(...(shown.items.length ? shown.items.map(finishedCard) : [node('p', `No finished games ${label.toLowerCase()} yet.`, 'empty-state')]));
    root.append(cards);
    if (shown && shown.pages > 1) {
      const pages = node('nav', '', 'lobby-pages'); pages.setAttribute('aria-label', `Pages of your finished games ${label.toLowerCase()}`);
      const previous = node('button', '← Previous'), next = node('button', 'Next →');
      for (const [button, step] of [[previous, -1], [next, 1]]) {
        button.type = 'button'; button.dataset.page = String(step);
        button.addEventListener('click', () => { finished[kind].page = shown.page - 1 + step; void loadFinished(kind); root.scrollIntoView?.({ block: 'start', behavior: 'smooth' }); });
      }
      previous.disabled = shown.page <= 1; next.disabled = shown.page >= shown.pages;
      const at = node('span', `Page ${shown.page} of ${shown.pages}`); at.setAttribute('aria-live', 'polite');
      pages.append(previous, at, next);
      root.append(pages);
    }
    return root;
  }
  // Signed in, a kind's finished page is the host's (ADR 0147); elsewhere, the finished tables this browser reaches.
  async function loadFinished(kind) {
    const wanted = finished[kind].page, reading = revision;
    try {
      const data = account ? await lobbyRequest(`/api/lobby/finished/${kind}/${wanted + 1}`) : localFinished(mine, kind, wanted);
      if (reading !== revision || wanted !== finished[kind].page) return;
      finished[kind].data = data; finished[kind].page = data.page - 1;
    } catch (error) {
      // Read again on the next poll: a page that failed once is not given up on.
      if (reading === revision) { finishedReads = Math.max(finishedReads, 1); problem(error.message || 'Could not read your finished games. Try again.'); }
    }
    if (listing) render();
  }
  // Read on the first poll, and again when a game of this player's finished or who is signed in changed: never on
  // every poll. A game the host says is over may still be having its result written, so the three polls after a
  // finish read again, which is a bounded wait rather than a page that misses it until the next reload.
  function maybeLoadFinished() {
    if (signInFirst()) return;
    const seen = JSON.stringify([account?.id ?? null, mine.filter(entry => entry.table.over).map(entry => entry.table.id)]);
    if (seen !== finishedSeen) { finishedReads = finishedSeen === null ? 1 : 3; finishedSeen = seen; }
    if (finishedReads <= 0) return;
    finishedReads -= 1;
    for (const [kind] of KINDS) void loadFinished(kind);
  }
  function render() {
    const key = JSON.stringify([listing, mine, invitations, showAll, account, mode, mineTab, finished, limit]);
    if (key === drawn) return;
    drawn = key;
    const available = visibleTables(listing.tables, mine, showAll);
    el('invitations').hidden = invitations.length === 0;
    el('invitations-count').textContent = invitations.length || '';
    el('invitation-cards').replaceChildren(...invitations.map(invitationCard));
    onOutstanding(mine.some(entry => invitedState(entry.invited)?.waiting));
    const ongoing = ongoingGames(mine);
    const finishedTotal = KINDS.reduce((sum, [kind]) => sum + (finished[kind].data?.total ?? 0), 0);
    const loaded = KINDS.every(([kind]) => finished[kind].data);
    el('mine-count').textContent = ongoing.players.length + ongoing.bots.length + finishedTotal;
    el('ongoing-count').textContent = ongoing.players.length + ongoing.bots.length ? `· ${ongoing.players.length + ongoing.bots.length}` : '';
    el('finished-count').textContent = loaded && finishedTotal ? `· ${finishedTotal}` : '';
    el('tables-count').textContent = available.length;
    const none = signInFirst() ? 'Sign in to see your games, on this device and every other one you sign in on.' : 'No games here yet. Create a table, take an open seat or enter an invitation code.';
    if (signInFirst() || (!mine.length && !finishedTotal && mineTab === 'ongoing')) el('my-tables').replaceChildren(node('p', none, 'empty-state'));
    else el('my-tables').replaceChildren(...KINDS.map(([kind, label]) => mineTab === 'ongoing' ? ongoingGroup(kind, label, ongoing[kind]) : finishedGroup(kind, label)));
    el('open-tables').replaceChildren(...(available.length ? available.map(table => card(table)) : [node('p', showAll ? 'No battles underway. Create the first table.' : 'No open seats right now. Create a table and invite another player, or play a bot.', 'empty-state')]));
    el('new-table').disabled = !listing.recording;
    el('lobby-status').textContent = statusText(listing);
    const options = listing.formats;
    if (el('format').options.length === 0) {
      el('format').replaceChildren(...options.map(format => { const option = node('option', format); option.value = format; return option; }));
      el('format').value = listing.format;
    }
  }
  async function refresh() {
    if (loading) { refreshAgain = true; return; }
    loading = true;
    const reading = revision;
    try {
      const tokens = savedSeats(storage).map(item => item.token);
      const [publicTables, saved] = await Promise.all([lobbyRequest('/api/lobby'), yours(tokens)]);
      if (reading !== revision) return;
      listing = publicTables;
      mine = saved.tables;
      invitations = saved.invitations ?? [];
      limit = Number.isInteger(saved.ongoingPerKind) ? saved.ongoingPerKind : null;
      render();
      maybeLoadFinished();
      el('lobby-status').textContent = statusText(listing);
      maybeRematch();
    } catch (error) {
      if (reading === revision) el('lobby-status').textContent = 'Connection interrupted. Reconnecting automatically…';
    } finally {
      loading = false;
      if (refreshAgain) { refreshAgain = false; void refresh(); }
    }
  }
  // Signed in, a player's games are the host's list, the same on every device; the seats this browser saved before
  // signing in are handed to the account once. Signed out where players sign in, there is nothing to resume yet.
  async function yours(tokens) {
    if (account) {
      if (adopted) return lobbyRequest('/api/lobby/mine');
      const owner = account.id;
      const saved = await lobbyRequest('/api/lobby/adopt', { tokens });
      if (account?.id === owner) adopted = true;
      return saved;
    }
    return signInFirst() ? { tables: [] } : lobbyRequest('/api/lobby/resume', { tokens });
  }
  for (const [id, tab] of [['mine-ongoing', 'ongoing'], ['mine-finished', 'finished']]) el(id).addEventListener('click', () => {
    mineTab = tab;
    el('mine-ongoing').setAttribute('aria-pressed', String(tab === 'ongoing'));
    el('mine-finished').setAttribute('aria-pressed', String(tab === 'finished'));
    if (listing) render();
  });
  for (const [id, all] of [['filter-open', false], ['filter-all', true]]) el(id).addEventListener('click', () => {
    showAll = all;
    el('filter-open').setAttribute('aria-pressed', String(!all));
    el('filter-all').setAttribute('aria-pressed', String(all));
    if (listing) render();
  });
  const dialog = el('create-dialog');
  // The friends a seat can be kept for (ADR 0129), read when the dialog opens: the list is short and changes rarely.
  async function loadFriends(chosen) {
    if (mode === 'off' || !account) { friends = []; }
    else {
      try { friends = (await lobbyRequest('/api/friends')).friends ?? []; } catch { friends = []; }
    }
    const placeholder = node('option', 'Choose a friend…');
    placeholder.value = '';
    el('friend').replaceChildren(placeholder, ...friends.map(friend => { const option = node('option', friend.username); option.value = friend.id; return option; }));
    el('friend').value = friendChoice(friends, chosen).value;
    el('opponent').querySelector('option[value="friend"]').hidden = mode === 'off';
  }
  let wanted = null;
  function showOpponent() {
    const value = el('opponent').value;
    const missing = value === 'friend' && friendChoice(friends, wanted).missing;
    el('create-help').textContent = missing
      ? 'That player is not in your friends yet. Ask them from the Friends panel, then invite them.'
      : value === 'friend' && friends.length === 0 ? 'Ask a player to be your friend first: the Friends panel asks by their player name.' : help[value];
    el('friend-row').hidden = value !== 'friend';
    // Only a game between two players can count on the ladder: a bot or a shared device never does.
    el('ranked-row').hidden = !['person', 'friend'].includes(value) || mode === 'off';
    // At the most games of that kind at once (ADR 0147), the host would refuse: say so before the player asks.
    const kind = value === 'bot' ? 'bots' : 'players', full = !reinviting && Number.isInteger(limit) && ongoingGames(mine)[kind].length >= limit;
    if (full) el('create-help').textContent = `You already have ${limit} games ${kind === 'bots' ? 'against bots' : 'against players'} in progress, the most at once. Finish one to start another.`;
    // No friend chosen, nothing to send: a seat is kept for the friend the player picked, never for a default.
    el('create-submit').disabled = full || (value === 'friend' && !el('friend').value);
  }
  el('friend').addEventListener('change', showOpponent);
  function openCreate({ friend = null, reinvite = null, opponent = null, format = null, ranked = true } = {}) {
    if (signInFirst()) { askSignIn(); return; }
    if (!listing) return;
    reinviting = reinvite;
    wanted = friend;
    el('create-problem').hidden = true;
    el('create-title').textContent = reinvite ? 'Invite another friend' : 'Create a table';
    el('create-submit').textContent = reinvite ? 'Invite →' : 'Create & play →';
    el('opponent').disabled = Boolean(reinvite);
    el('format').disabled = Boolean(reinvite);
    if (friend || reinvite) el('opponent').value = 'friend';
    else if (opponent) el('opponent').value = opponent;
    if (format && [...el('format').options].some(option => option.value === format)) el('format').value = format;
    el('ranked').checked = ranked !== false; el('ranked').disabled = Boolean(reinvite);
    const opening = revision;
    void loadFriends(friend).then(() => {
      if (opening !== revision || signInFirst()) return;
      showOpponent();
      if (!dialog.open) dialog.showModal();
    });
  }
  // Sent here by "Rematch" (ADR 0129): the dialog opens on the same opponent once the player is signed in and the
  // tables are listed, and the address is cleaned so a reload does not ask again.
  function maybeRematch() {
    if (!pendingRematch || !listing || signInFirst()) return;
    const again = pendingRematch;
    pendingRematch = null;
    globalThis.history?.replaceState?.(null, '', '/lobby');
    openCreate(again);
  }
  el('new-table').addEventListener('click', () => openCreate());
  for (const id of ['create-close', 'create-cancel']) el(id).addEventListener('click', () => dialog.close());
  const help = {
    person: 'You take the first seat. Another player can join from this lobby or with your invitation.',
    friend: 'The second seat is kept for your friend: nobody else can take it. They are told on their phone, and you wait at the table. One invitation at a time.',
    bot: 'The strongest bot the game has plays the other side. Your game is saved so you can come back later.',
    hotseat: 'Both players use this device. Pass it between turns.',
  };
  el('opponent').addEventListener('change', showOpponent);
  el('create-table').addEventListener('submit', async event => {
    event.preventDefault();
    if (busy) return;
    busy = true;
    el('create-submit').disabled = true;
    el('create-problem').hidden = true;
    try {
      if (el('opponent').value === 'friend' && !el('friend').value) {
        throw new Error('Choose a friend to keep the seat for.');
      }

      if (reinviting) {
        await lobbyRequest(`/api/lobby/tables/${encodeURIComponent(reinviting)}/invitation`, { friend: el('friend').value });
        dialog.close();
        await refresh();
      } else {
        const between = ['person', 'friend'].includes(el('opponent').value);
        enter(await lobbyRequest('/api/lobby/tables', { opponent: el('opponent').value, format: el('format').value, friend: el('opponent').value === 'friend' ? el('friend').value : undefined,
          ...(between && !el('ranked').checked ? { ranked: false } : {}) }));
      }
    } catch (error) { el('create-problem').textContent = error.message || 'Could not create the table. Try again.'; el('create-problem').hidden = false; }
    finally { busy = false; showOpponent(); }
  });
  void refresh();
  const resume = () => { if (!doc.hidden && !dialog.open && !busy) void refresh(); };
  doc.addEventListener('visibilitychange', resume);
  globalThis.addEventListener?.('online', resume);
  const interval = globalThis.setInterval(() => { if (!doc.hidden && !dialog.open && !busy) void refresh(); }, 10000);
  return {
    stop: () => { globalThis.clearInterval(interval); doc.removeEventListener('visibilitychange', resume); globalThis.removeEventListener?.('online', resume); },
    /** Opens the create dialog with that friend chosen: what the Friends panel's Invite does. */
    invite: friend => openCreate({ friend: friend?.id ?? null }),
    /** Told whenever whether an invitation of this player's is waiting changes, so the Friends panel can say so. */
    onOutstanding(tell) { onOutstanding = tell; },
    /** Told by the sign-in whenever who is signed in changes; the lists are read again for them. */
    setAccount(next, nextMode) {
      const changed = (next?.id ?? null) !== (account?.id ?? null) || nextMode !== mode;
      account = next;
      mode = nextMode;
      if (changed) {
        revision += 1; adopted = false; mine = []; invitations = []; limit = null; finishedSeen = null; finishedReads = 0;
        for (const [kind] of KINDS) finished[kind] = { page: 0, data: null };
        if (dialog.open) dialog.close();
        problem();
        if (listing) render();
        void refresh();
      }
    },
  };
}

if (typeof document !== 'undefined') {
  let storage = null;
  try { storage = globalThis.localStorage; } catch { /* Invitations still work without browser storage. */ }
  const lobby = mountLobby(document, globalThis.location, storage);
  const next = nextPath(globalThis.location.search);
  const friends = mountNavigation(document, storage, {
    onInvite: lobby.invite,
    onChange(account, mode) {
      if (account && next) { globalThis.location.assign(next); return; }
      const playable = Boolean(account) || mode === 'off';
      document.getElementById('welcome-intro').hidden = playable;
      document.getElementById('home-games').hidden = !playable;
      document.getElementById('hero-eyebrow').textContent = account ? `Welcome back, ${account.username}` : 'Your games';
      lobby.setAccount(account, mode);
    },
  });
  lobby.onOutstanding(friends.setOutstanding);
}
