import { preferredPresentation, presentationPath } from './battle-preferences.js';
import { nextPath } from './account.js';
import { onLobbyChange } from './changes.js';
import { mountNavigation } from './navigation.js';
import { turnOf } from './players.js';
export { turnOf } from './players.js';
export { codePath } from './navigation.js';
import { activateSeats, savedSeats } from './session.js';
import { mountPracticeList } from './lobby-practice.js';

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

export const GAMES_PER_PAGE = 6;
/** The two kinds of game the lobby lists apart (ADR 0147), in the order it shows them. */
export const KINDS = Object.freeze([['players', 'Against players'], ['bots', 'Against bots']]);

/**
 * Every game waiting for this player, whatever its kind, the most pressing first: the one whose time to answer runs
 * out soonest (ADR 0169), then the ones with no time to answer, in the host's order.
 */
export function waitingGames(mine) {
  const waiting = mine.filter(entry => !entry.table.over && turnOf(entry.table, (entry.seats ?? []).map(held => held.seat))?.mine === true);
  const due = entry => { const at = Date.parse(entry.table.answerBy ?? ''); return Number.isFinite(at) ? at : Number.POSITIVE_INFINITY; };
  return waiting.map((entry, index) => ({ entry, index })).sort((a, b) => due(a.entry) - due(b.entry) || a.index - b.index).map(one => one.entry);
}

/**
 * The games against players waiting for this player, the most pressing first: what "Your turn" lists and "Play next"
 * opens the first of. A game against a bot is always waiting for its player, so it would only be in the way here.
 */
export function yourTurn(mine) {
  return waitingGames(mine).filter(entry => gameKind(entry.table) === 'players');
}

/**
 * Whether a game counts on the ladder, as the host keeps its result (`MatchResult.Ranked`, ADR 0124, 0171): between
 * players, in 3v3, not opened unranked, each person's seat held by an account of its own. A game against a bot never
 * does, nor one on a shared device, which the host opens unranked, nor one on a host where nobody signs in, whose
 * seats no account holds. A seat still open, or kept for a friend, is taken by an account when it is taken.
 */
export function isRanked(table) {
  if (gameKind(table) !== 'players' || table?.format !== '3v3' || table?.casual === true) return false;
  const people = (table.seats ?? []).filter(seat => seat.kind === 'person');
  const accounts = people.map(seat => seat.account);
  return people.length > 0 && accounts.every(Boolean) && new Set(accounts).size === accounts.length;
}

/** When a game was created, as a card says it: the day, in the reader's own words for it. */
export function createdOn(createdAt, locale = undefined) {
  const at = createdAt ? new Date(createdAt) : null;
  return at && Number.isFinite(at.getTime()) ? at.toLocaleDateString(locale, { day: 'numeric', month: 'short', year: 'numeric' }) : null;
}

/** Whether a table is played against a bot or between people (a shared device among them), by who sits. */
export function gameKind(table) {
  return (table?.seats ?? []).some(seat => seat.kind === 'bot') ? 'bots' : 'players';
}

/**
 * The player's games in progress, by kind: in each, the ones waiting for this player first, the most pressing first as
 * "Your turn" lists them, then the others in the host's order.
 */
export function ongoingGames(mine) {
  const ongoing = mine.filter(entry => !entry.table.over);
  const waiting = waitingGames(ongoing);
  const ordered = [...waiting, ...ongoing.filter(entry => !waiting.includes(entry))];
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
      opponent: other ? { kind: other.kind, name: other.name } : { kind: 'self', name: 'Shared device' }, seats: entry.seats, replay: null };
  });
  const pages = Math.max(1, Math.ceil(list.length / size)), at = Math.min(Math.max(0, page), pages - 1);
  return { total: list.length, pages, page: at + 1, items: list.slice(at * size, (at + 1) * size) };
}

const OUTCOMES = Object.freeze({ won: 'Victory', lost: 'Defeat', draw: 'Draw', both: 'Shared device' });
/**
 * What a finished game's card says: the result as a tag, who it was against, and how long it ran. A game the operator
 * voided (ADR 0176) says Voided instead of its result, and why, since it counts nowhere.
 */
export function finishedSummary(item) {
  const voided = item.voided?.explanation ?? null;
  return { title: `${item.format ?? ''} battle`.trim(), tag: voided ? 'Voided' : OUTCOMES[item.outcome] ?? 'Finished', tone: voided ? 'voided' : item.outcome ?? 'finished', casual: item.casual === true,
    // Whether it counted on the ladder, as the host's kept result says; null for a host that did not say (an older one, or
    // a game this browser only knows from its own list).
    ranked: typeof item.ranked === 'boolean' ? item.ranked : null,
    voided,
    against: item.opponent?.name ?? 'Unknown', bot: item.opponent?.kind === 'bot',
    detail: Number.isInteger(item.rounds) && item.rounds > 0 ? `${item.rounds} round${item.rounds === 1 ? '' : 's'}` : '',
    action: item.seats?.length ? 'View game →' : item.replay ? 'Watch the replay →' : null };
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
  if (again === 'hotseat') return { opponent: again, friend: null, format };
  // The same bot level again (ADR 0180), when the link names one.
  if (again === 'bot') return { opponent: again, friend: null, format, level: params.get('level') || null };
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

/**
 * The record against a recent opponent, from this player's side: the score a card shows (won–lost, and drawn
 * when there was a draw), and the words it is read as (the games won, lost and drawn, the ones that happened only).
 */
export function opponentRecord(opponent) {
  const count = value => (Number.isInteger(value) && value > 0 ? value : 0);
  const won = count(opponent?.won), lost = count(opponent?.lost), drawn = count(opponent?.drawn);
  const parts = [['Won', won], ['Lost', lost], ['Drew', drawn]].filter(([, games]) => games > 0);
  if (!parts.length) return { score: '–', text: 'No result kept' };
  return { score: [won, lost, ...(drawn ? [drawn] : [])].join('–'), text: parts.map(([word, games]) => `${word} ${games}`).join(' · ') };
}

/**
 * How a recent opponent is challenged again (ADR 0153): a friend is invited from the create dialog, anyone else is
 * asked to be a friend first (asking one who asked you accepts), and a request already sent waits. Nothing is sent
 * to a stranger. Null where the host keeps no friends.
 */
export function opponentAction(opponent) {
  switch (opponent?.relation) {
    case 'friend': return { label: 'Challenge →', act: 'challenge', primary: true };
    case 'asking': return { label: 'Accept friend', act: 'ask', primary: false };
    case 'none': return { label: 'Add friend', act: 'ask', primary: false };
    case 'asked': return { label: 'Friend request sent', act: null, primary: false };
    default: return null;
  }
}

/**
 * The recent opponents with their friendship read again from a friends list (`GET /api/friends`): a request
 * accepted, declined or withdrawn in the Friends panel changes the way each card offers to play again. An opponent
 * the host gave no relation (no friends kept) keeps none.
 */
export function reconcileRelations(opponents, list) {
  if (!list) return opponents;
  // In this order: a friendship outranks a request either way, as the host reads them.
  const kinds = [['friends', 'friend'], ['outgoing', 'asked'], ['incoming', 'asking']]
    .map(([key, relation]) => [new Set((list[key] ?? []).map(link => link.id)), relation]);
  return opponents.map(opponent => {
    if (opponent.relation == null) return opponent;
    const relation = kinds.find(([ids]) => ids.has(opponent.id))?.[1] ?? 'none';
    return relation === opponent.relation ? opponent : { ...opponent, relation };
  });
}

/**
 * The order the recent opponents are shown in: friends first, the ones a tap challenges, then everyone else, each
 * group met most recently first as the host listed it. A friendship accepted in the page moves its card forward.
 */
export function opponentOrder(opponents) {
  return [...opponents.filter(one => one.relation === 'friend'), ...opponents.filter(one => one.relation !== 'friend')];
}

/** What the create dialog opens on to challenge a friend again: them, in the format and rating of the last game. */
export function challengeRequest(opponent) {
  return { friend: opponent.id, format: opponent.format || null, ranked: opponent.casual !== true };
}

function statusText(state) {
  if (!state.recording) return 'This host is not saving games. New tables are unavailable until recording is enabled.';
  const count = Number.isInteger(state.ongoing) ? state.ongoing : (state.tables ?? []).length;
  return `${count} ${count === 1 ? 'table' : 'tables'} ongoing · updates automatically`;
}

export function mountLobby(doc, location, storage, { changes = onLobbyChange } = {}) {
  const el = id => doc.getElementById(id);
  // Games open in the battle view (ADR 0157); only a browser that asked for the archived table opens /play.
  const presentation = preferredPresentation(storage);
  let listing = null;
  let mine = [], mineTab = 'ongoing', limit = null;
  // Each kind's finished page, read from the host a page at a time (ADR 0147); `seen` is what the last read was of.
  const finished = { players: { page: 0, data: null }, bots: { page: 0, data: null } };
  let finishedSeen = null, finishedReads = 0;
  let invitations = [];
  let opponents = [];
  let friends = [];
  let reinviting = null;
  let pendingRematch = rematchRequest(location.search);
  let onOutstanding = () => {};
  let onMine = () => {};
  let showAll = false;
  // The page of Find an opponent on show, read from the host a page at a time with the rest of the lobby.
  const publicTables = { page: 0, data: null };
  let loading = false;
  let drawn = '';
  // A time left to answer counts down on its own (ADR 0169): the cards are drawn again each minute while one says
  // it, with nothing read from the host.
  let minute = 0;
  globalThis.setInterval(() => {
    if (!listing || ![...mine.map(entry => entry.table), ...(publicTables.data?.items ?? [])].some(table => table?.answerBy)) return;
    minute += 1;
    render();
  }, 60000);
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
    // Both seats on this device (a shared device) play at the classic table, the one view with a handover between
    // two players (ADR 0157); the battle view plays one seat.
    const view = entry.seats.length > 1 ? 'table' : presentation;
    location.assign(`${presentationPath(view)}?${new URLSearchParams(entry.seats.map(item => [item.seat, item.token]))}`);
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
  // A title, with the tag that says whether the game counts on the ladder: `standing` ('ranked' or 'unranked') when
  // the host said so, which a finished game's card does; otherwise an unranked game alone says it (`casual`).
  function title(text, casual, standing) {
    const heading = node('h3', text);
    const shown = standing ?? (casual ? 'unranked' : '');
    if (shown) heading.append(node('span', shown === 'ranked' ? 'Ranked' : 'Unranked', shown === 'ranked' ? 'casual-tag ranked' : 'casual-tag'));
    return heading;
  }
  // One row of a list: what it is and its state, who plays, a detail line, and the one thing to do. Public tables
  // and finished games share it, so a long list stays one line an entry and reads the same everywhere.
  function row({ heading, casual = false, standing, tag, tone = '', sides = [], detail = '', action = null, className = '' }) {
    const root = node('article', '', `game-row ${className}`.trim());
    const top = node('div', '', 'game-row-top');
    top.append(title(heading, casual, standing), node('span', tag, `status-tag ${tone}`.trim()));
    const line = node('div', '', 'game-row-who');
    sides.forEach((side, index) => {
      if (index > 0) line.append(node('span', 'vs', 'vs'));
      const who = node('span', side.name, `who ${side.kind}${side.you ? ' you' : ''}`);
      line.append(who);
      if (side.kind === 'bot') line.append(node('span', 'bot', 'bot-tag'));
    });
    if (sides.length) line.setAttribute('aria-label', sides.map(side => side.name).join(' versus '));
    root.append(top, line, node('p', detail, 'game-row-detail'));
    if (action) root.append(action);
    return root;
  }
  // The pages of a list served a page at a time: previous, where, next. Nothing when it fits on one page.
  function pager(label, shown, go) {
    if (!shown || shown.pages <= 1) return null;
    const pages = node('nav', '', 'lobby-pages'); pages.setAttribute('aria-label', label);
    const previous = node('button', '← Previous'), next = node('button', 'Next →');
    for (const [button, step] of [[previous, -1], [next, 1]]) {
      button.type = 'button'; button.dataset.page = String(step);
      button.addEventListener('click', () => go(shown.page - 1 + step));
    }
    previous.disabled = shown.page <= 1; next.disabled = shown.page >= shown.pages;
    const at = node('span', `Page ${shown.page} of ${shown.pages}`); at.setAttribute('aria-live', 'polite');
    pages.append(previous, at, next);
    return pages;
  }
  // A game in progress in Find an opponent: its state, who sits, and a seat to take, or the way back to one's own.
  function tableRow(table) {
    const entry = mine.find(one => one.table.id === table.id);
    const held = (entry?.seats ?? []).map(seat => seat.seat);
    const turn = turnOf(table, held);
    let action = null;
    if (entry || table.openSeats > 0) {
      action = node('button', cardAction(table, entry), entry ? 'primary' : 'secondary');
      action.type = 'button';
      action.setAttribute('aria-label', `${entry ? 'Resume' : 'Join'} ${table.format} table ${table.id}`);
      if (entry) action.addEventListener('click', () => enter(entry));
      else action.addEventListener('click', () => act(action, () => lobbyRequest(`/api/lobby/tables/${encodeURIComponent(table.id)}/join`, {})));
    }
    return row({ heading: `${table.format} battle`, casual: table.casual === true, tag: tableState(table), tone: table.openSeats > 0 ? 'available' : '',
      sides: versus(table).map(side => ({ ...side, you: held.includes(side.seat) })), detail: turn?.text ?? '', action, className: 'public' });
  }
  function card(table, entry) {
    const root = node('article', '', `game-card${entry ? ' mine' : ''}`);
    const top = node('div', '', 'game-card-top');
    const turn = turnOf(table, (entry?.seats ?? []).map(held => held.seat));
    const invited = invitedState(entry?.invited);
    // An unranked game (ADR 0155) says so beside its title.
    // One's own card says ranked or not in its last line, beside its date; another's says it in the title.
    top.append(title(`${table.format} battle`, !entry && table.casual === true), turn?.mine
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
    // When it was created and whether it counts on the ladder, on every card of one's own.
    if (entry) {
      const created = createdOn(table.createdAt);
      const meta = node('p', '', 'game-meta');
      if (created) meta.append(node('span', `Created ${created}`, 'game-created'));
      meta.append(node('span', isRanked(table) ? 'Ranked' : 'Unranked', `game-ranked${isRanked(table) ? ' ranked' : ''}`));
      root.append(meta);
    }
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
  // A player met recently: the record between them, when and what they last played, and the way to play again.
  function opponentCard(opponent) {
    const root = node('article', '', `game-card opponent ${opponent.relation ?? 'unknown'}`);
    const top = node('div', '', 'game-card-top');
    const record = opponentRecord(opponent), score = node('span', record.score, 'status-tag record');
    score.setAttribute('aria-label', record.text); score.title = record.text;
    top.append(node('h3', opponent.name), score);
    const when = opponent.lastPlayedAt ? new Date(opponent.lastPlayedAt).toLocaleDateString(undefined, { day: 'numeric', month: 'short' }) : '';
    // The last game, which a challenge starts from: its format and rating, and when.
    root.append(top, node('p', [opponent.format, opponent.casual ? 'unranked' : '', when].filter(Boolean).join(' · '), 'game-turn'));
    const action = opponentAction(opponent);
    if (action) {
      const button = node('button', action.label, action.primary ? 'primary' : 'secondary');
      button.type = 'button';
      button.disabled = !action.act;
      if (action.act === 'challenge') button.addEventListener('click', () => openCreate(challengeRequest(opponent)));
      else if (action.act === 'ask') button.addEventListener('click', () => askFriend(button, opponent));
      root.append(button);
    }
    return root;
  }
  // Asking a recent opponent to be a friend: their card then says the request went, or, when they had asked first,
  // that they are a friend now and can be challenged.
  async function askFriend(button, opponent) {
    if (busy) return;
    busy = true;
    button.disabled = true;
    problem();
    try {
      const answer = await lobbyRequest('/api/friends/requests', { username: opponent.name });
      opponents = opponents.map(one => one.id === opponent.id ? { ...one, relation: answer?.accepted ? 'friend' : 'asked' } : one);
    } catch (error) { problem(error.message || 'Could not send the friend request. Try again.'); button.disabled = false; }
    finally { busy = false; }
    if (listing) render();
  }
  // The players met most recently (signed in only), read with the finished games: a finished game is what adds one.
  async function loadOpponents() {
    const reading = revision;
    try {
      const data = account ? await lobbyRequest('/api/lobby/opponents') : { opponents: [] };
      if (reading !== revision) return;
      opponents = data.opponents ?? [];
    } catch { if (reading === revision) opponents = []; }
    if (listing) render();
  }
  function group(kind, label, count) {
    const root = node('section', '', `mine-group ${kind}`);
    root.dataset.kind = kind; root.setAttribute('aria-label', label);
    const head = node('header', '', 'mine-group-head');
    head.append(node('h3', label), node('span', count.text, `lobby-count${count.full ? ' full' : ''}`));
    root.append(head);
    return root;
  }
  // Every game waiting for the player, both kinds together, the most pressing first, each a tap away, and the first
  // one tap away from the header: the games are also in their groups below, this is the way to go through them.
  function yourTurnBand(waiting) {
    const root = node('section', '', 'your-turn-band');
    root.setAttribute('aria-labelledby', 'your-turn-heading');
    const head = node('header', '', 'your-turn-head');
    const heading = node('h3', `Your turn · ${waiting.length}`);
    heading.id = 'your-turn-heading';
    const next = node('button', 'Play next →', 'primary');
    next.type = 'button';
    next.id = 'play-next';
    next.setAttribute('aria-label', `Play next: ${waiting[0].table.format} battle`);
    next.addEventListener('click', () => enter(waiting[0]));
    head.append(heading, next);
    const rows = node('div', '', 'game-rows');
    rows.append(...waiting.map(entry => {
      const held = (entry.seats ?? []).map(seat => seat.seat);
      const play = node('button', 'Play →', 'secondary');
      play.type = 'button';
      play.setAttribute('aria-label', `Play ${entry.table.format} table ${entry.table.id}`);
      play.addEventListener('click', () => enter(entry));
      return row({ heading: `${entry.table.format} battle`, tag: isRanked(entry.table) ? 'Ranked' : 'Unranked', tone: isRanked(entry.table) ? 'ranked' : '',
        sides: versus(entry.table).map(side => ({ ...side, you: held.includes(side.seat) })), detail: `${turnOf(entry.table, held).text} · ${tableState(entry.table)}`, action: play, className: 'mine' });
    }));
    root.append(head, rows);
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
    const created = createdOn(item.createdAt), when = createdOn(item.finishedAt);
    let action = null;
    if (summary.action && item.seats?.length) {
      action = node('button', summary.action, 'secondary'); action.type = 'button';
      action.addEventListener('click', () => enter({ seats: item.seats }));
    } else if (summary.action) {
      action = node('a', summary.action, 'button-link'); action.href = item.replay;
    }
    // Created and finished, labelled, since a card carries both: a game against a player can last days (ADR 0169).
    const standing = { true: 'ranked', false: 'unranked' }[String(summary.ranked)];
    return row({ heading: summary.title, casual: summary.casual, standing, tag: summary.tag, tone: `outcome ${summary.tone}`,
      sides: [{ name: `vs ${summary.against}`, kind: item.opponent?.kind ?? 'person' }],
      detail: [summary.voided, created && `Created ${created}`, when && `Finished ${when}`, summary.detail].filter(Boolean).join(' · '), action,
      className: `finished ${summary.tone}` });
  }
  // A kind's finished games, a page at a time from the host, with that kind's own pages.
  function finishedGroup(kind, label) {
    const shown = finished[kind].data;
    const root = group(kind, label, { text: shown ? String(shown.total) : '…', full: false });
    const rows = node('div', '', 'game-rows');
    if (!shown) rows.append(node('p', 'Finding your finished games…', 'empty-state'));
    else rows.append(...(shown.items.length ? shown.items.map(finishedCard) : [node('p', `No finished games ${label.toLowerCase()} yet.`, 'empty-state')]));
    root.append(rows);
    const pages = pager(`Pages of your finished games ${label.toLowerCase()}`, shown, page => {
      finished[kind].page = page; void loadFinished(kind); root.scrollIntoView?.({ block: 'start', behavior: 'smooth' });
    });
    if (pages) root.append(pages);
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
      // Read again on the next change: a page that failed once is not given up on.
      if (reading === revision) { finishedReads = Math.max(finishedReads, 1); problem(error.message || 'Could not read your finished games. Try again.'); }
    }
    if (listing) render();
  }
  // Read on the first read, and again when a game of this player's finished or who is signed in changed: never on
  // every read. A game the host says is over may still be having its result written, so the three reads after a
  // finish read again, which is a bounded wait rather than a page that misses it until the next reload.
  function maybeLoadFinished() {
    if (signInFirst()) return;
    const seen = JSON.stringify([account?.id ?? null, mine.filter(entry => entry.table.over).map(entry => entry.table.id)]);
    if (seen !== finishedSeen) { finishedReads = finishedSeen === null ? 1 : 3; finishedSeen = seen; }
    if (finishedReads <= 0) return;
    finishedReads -= 1;
    for (const [kind] of KINDS) void loadFinished(kind);
    void loadOpponents();
  }
  function render() {
    const key = JSON.stringify([listing, mine, invitations, showAll, account, mode, mineTab, finished, limit, opponents, publicTables, minute]);
    if (key === drawn) return;
    drawn = key;
    // A host without accounts cannot tell whose seats this browser holds: its own open tables are left out here.
    const shownTables = publicTables.data, available = shownTables ? visibleTables(shownTables.items, mine, showAll) : null;
    el('invitations').hidden = invitations.length === 0;
    el('invitations-count').textContent = invitations.length || '';
    el('invitation-cards').replaceChildren(...invitations.map(invitationCard));
    el('opponents').hidden = signInFirst() || !account || opponents.length === 0;
    el('opponents-count').textContent = opponents.length || '';
    el('opponent-cards').replaceChildren(...opponentOrder(opponents).map(opponentCard));
    onOutstanding(mine.some(entry => invitedState(entry.invited)?.waiting));
    const ongoing = ongoingGames(mine);
    const finishedTotal = KINDS.reduce((sum, [kind]) => sum + (finished[kind].data?.total ?? 0), 0);
    const loaded = KINDS.every(([kind]) => finished[kind].data);
    el('mine-count').textContent = ongoing.players.length + ongoing.bots.length + finishedTotal;
    el('ongoing-count').textContent = ongoing.players.length + ongoing.bots.length ? `· ${ongoing.players.length + ongoing.bots.length}` : '';
    el('finished-count').textContent = loaded && finishedTotal ? `· ${finishedTotal}` : '';
    el('tables-count').textContent = shownTables ? shownTables.total : '';
    const none = signInFirst() ? 'Sign in to see your games, on this device and every other one you sign in on.' : 'No games here yet. Create a table, take an open seat or enter an invitation code.';
    const waiting = signInFirst() ? [] : yourTurn(mine);
    el('your-turn').replaceChildren(...(waiting.length ? [yourTurnBand(waiting)] : []));
    el('your-turn').hidden = waiting.length === 0;
    if (signInFirst() || (!mine.length && !finishedTotal && mineTab === 'ongoing')) el('my-tables').replaceChildren(node('p', none, 'empty-state'));
    else el('my-tables').replaceChildren(...KINDS.map(([kind, label]) => mineTab === 'ongoing' ? ongoingGroup(kind, label, ongoing[kind]) : finishedGroup(kind, label)));
    const rows = node('div', '', 'game-rows');
    if (!available) rows.append(node('p', 'Finding open tables…', 'empty-state'));
    else rows.append(...(available.length ? available.map(tableRow) : [node('p', showAll ? 'No battles underway. Create the first table.' : 'No open seats right now. Create a table and invite another player, or play a bot.', 'empty-state')]));
    const tablePages = pager(showAll ? 'Pages of the battles underway' : 'Pages of the open seats', shownTables, page => { publicTables.page = page; void loadTables(); });
    el('open-tables').replaceChildren(rows, ...(tablePages ? [tablePages] : []));
    el('new-table').disabled = !listing.recording;
    el('lobby-status').textContent = statusText(listing);
    const options = listing.formats;
    if (el('format').options.length === 0) {
      el('format').replaceChildren(...options.map(format => { const option = node('option', format); option.value = format; return option; }));
      el('format').value = listing.format;
    }
  }
  const tablesPath = (all, page) => `/api/lobby/page/${all ? 'all' : 'open'}/${page + 1}`;
  // A page asked for (a filter, previous, next) is read at once; each change the host announces reads the page on show again.
  async function loadTables() {
    const view = showAll, page = publicTables.page, reading = revision;
    try {
      const data = await lobbyRequest(tablesPath(view, page));
      if (reading !== revision || view !== showAll || page !== publicTables.page || !Array.isArray(data?.items)) return;
      publicTables.data = data; publicTables.page = data.page - 1;
    } catch (error) { if (reading === revision) problem(error.message || 'Could not read the tables. Try again.'); }
    if (listing) render();
  }
  let failures = 0, retry = null;
  async function refresh() {
    if (loading) { refreshAgain = true; return; }
    loading = true;
    const reading = revision;
    try {
      const tokens = savedSeats(storage).map(item => item.token);
      const view = showAll, page = publicTables.page;
      const [summary, saved, tables] = await Promise.all([lobbyRequest('/api/lobby'), yours(tokens), lobbyRequest(tablesPath(view, page)).catch(() => null)]);
      if (reading !== revision) return;
      // A page that failed keeps the one on show: it is read again on the next change.
      if (Array.isArray(tables?.items) && view === showAll && page === publicTables.page) { publicTables.data = tables; publicTables.page = tables.page - 1; }
      listing = summary;
      mine = saved.tables;
      invitations = saved.invitations ?? [];
      limit = Number.isInteger(saved.ongoingPerKind) ? saved.ongoingPerKind : null;
      onMine({ tables: mine, invitations });
      render();
      maybeLoadFinished();
      // A request waiting either way is answered elsewhere: read the opponents again until it is.
      if (account && opponents.some(one => one.relation === 'asked' || one.relation === 'asking')) void loadOpponents();
      el('lobby-status').textContent = statusText(listing);
      maybeRematch();
      failures = 0;
    } catch (error) {
      if (reading === revision) {
        el('lobby-status').textContent = 'Connection interrupted. Reconnecting automatically…';
        // Tried again, later each time, until it answers: a retry, not a clock.
        failures += 1;
        globalThis.clearTimeout(retry);
        retry = globalThis.setTimeout(() => void refresh(), Math.min(30000, 1000 * 2 ** (failures - 1)));
      }
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
    if (showAll === all) return;
    showAll = all; publicTables.page = 0; publicTables.data = null;
    el('filter-open').setAttribute('aria-pressed', String(!all));
    el('filter-all').setAttribute('aria-pressed', String(all));
    if (listing) render();
    void loadTables();
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
  // Solo plays a bot; PvP another player, a friend, or a second player on this device.
  let kind = 'pvp';
  const opponentOf = () => kind === 'solo' ? 'bot' : el('opponent').value;
  function choose(next) {
    kind = next;
    for (const [tab, panel, which] of [['create-solo', 'create-solo-panel', 'solo'], ['create-pvp', 'create-pvp-panel', 'pvp']]) {
      el(tab).setAttribute('aria-selected', String(kind === which));
      el(tab).tabIndex = kind === which ? 0 : -1;
      el(panel).hidden = kind !== which;
    }
    showOpponent();
  }
  for (const [tab, which] of [['create-solo', 'solo'], ['create-pvp', 'pvp']]) el(tab).addEventListener('click', () => choose(which));
  // Two tabs, as a tab list moves: an arrow key goes to the other one.
  el('create-tabs').addEventListener('keydown', event => {
    if (!['ArrowLeft', 'ArrowRight'].includes(event.key) || reinviting) return;
    event.preventDefault();
    choose(kind === 'solo' ? 'pvp' : 'solo');
    el(kind === 'solo' ? 'create-solo' : 'create-pvp').focus();
  });
  function showOpponent() {
    const value = opponentOf();
    const missing = value === 'friend' && friendChoice(friends, wanted).missing;
    el('create-help').textContent = missing
      ? 'That player is not in your friends yet. Ask them from the Friends panel, then invite them.'
      : value === 'friend' && friends.length === 0 ? 'Ask a player to be your friend first: the Friends panel asks by their player name.' : help[value];
    el('friend-row').hidden = value !== 'friend';
    // Only a game between two players can count on the ladder: a bot or a shared device never does.
    el('ranked-row').hidden = !['person', 'friend'].includes(value) || mode === 'off';
    // A ranked game is 3v3 (ADR 0171): its format is fixed while Ranked is on, and free otherwise.
    const ranked = rankedChosen();
    if (ranked) el('format').value = '3v3';
    el('format').disabled = ranked || Boolean(reinviting);
    el('format-note').hidden = !ranked;
    // So is how long a player has to answer before losing (ADR 0169): a bot always answers.
    el('turn-limit-row').hidden = !['person', 'friend'].includes(value);
    // At the most games of that kind at once (ADR 0147), the host would refuse: say so before the player asks.
    const kind = value === 'bot' ? 'bots' : 'players', full = !reinviting && Number.isInteger(limit) && ongoingGames(mine)[kind].length >= limit;
    if (full) el('create-help').textContent = `You already have ${limit} games ${kind === 'bots' ? 'against bots' : 'against players'} in progress, the most at once. Finish one to start another.`;
    // No friend chosen, nothing to send: a seat is kept for the friend the player picked, never for a default.
    el('create-submit').disabled = full || (value === 'friend' && !el('friend').value);
  }
  el('friend').addEventListener('change', showOpponent);
  el('ranked').addEventListener('change', showOpponent);
  function rankedChosen() {
    return kind === 'pvp' && ['person', 'friend'].includes(el('opponent').value) && mode !== 'off' && el('ranked').checked;
  }
  /**
   * Opens the create dialog, on the opponent, friend, format and rank a rematch or a challenge asks for.
   * @param {{ friend?: string | null, reinvite?: string | null, opponent?: string | null, format?: string | null, ranked?: boolean, level?: string | null }} [asked]
   */
  function openCreate({ friend = null, reinvite = null, opponent = null, format = null, ranked = true, level = null } = {}) {
    if (signInFirst()) { askSignIn(); return; }
    if (!listing) return;
    reinviting = reinvite;
    wanted = friend;
    el('create-problem').hidden = true;
    el('create-title').textContent = reinvite ? 'Invite another friend' : 'Create a table';
    el('create-submit').textContent = reinvite ? 'Invite →' : 'Create & play →';
    el('opponent').disabled = Boolean(reinvite);
    for (const tab of ['create-solo', 'create-pvp']) el(tab).disabled = Boolean(reinvite);
    kind = opponent === 'bot' ? 'solo' : 'pvp';
    if (friend || reinvite) el('opponent').value = 'friend';
    else if (opponent && opponent !== 'bot') el('opponent').value = opponent;
    if (format && [...el('format').options].some(option => option.value === format)) el('format').value = format;
    // A rematch against a bot plays the same level again (ADR 0180); a new table starts on Hard, as the lobby always did.
    el('bot-level').value = [...el('bot-level').options].some(option => option.value === level) ? level : 'hard';
    // A ranked game is 3v3 (ADR 0171): played again in another format, a game from before that is played unranked.
    el('ranked').checked = ranked !== false && (!format || format === '3v3'); el('ranked').disabled = Boolean(reinvite);
    el('turn-limit').value = '72'; el('turn-limit').disabled = Boolean(reinvite);
    const opening = revision;
    void loadFriends(friend).then(() => {
      if (opening !== revision || signInFirst()) return;
      choose(kind);
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
    bot: 'The bot plays the other side. Your game is saved so you can come back later.',
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
      if (opponentOf() === 'friend' && !el('friend').value) {
        throw new Error('Choose a friend to keep the seat for.');
      }

      if (reinviting) {
        await lobbyRequest(`/api/lobby/tables/${encodeURIComponent(reinviting)}/invitation`, { friend: el('friend').value });
        dialog.close();
        await refresh();
      } else {
        const opponent = opponentOf(), between = ['person', 'friend'].includes(opponent);
        enter(await lobbyRequest('/api/lobby/tables', { opponent, format: el('format').value, friend: opponent === 'friend' ? el('friend').value : undefined,
          // A bot is chosen by how strong it is, never by its name (ADR 0180).
          ...(opponent === 'bot' ? { level: el('bot-level').value } : {}),
          ...(between && !el('ranked').checked ? { ranked: false } : {}), ...(between ? { turnLimit: Number(el('turn-limit').value) } : {}) }));
      }
    } catch (error) { el('create-problem').textContent = error.message || 'Could not create the table. Try again.'; el('create-problem').hidden = false; }
    finally { busy = false; showOpponent(); }
  });
  void refresh();
  // No clock reads the lists (ADR 0164): the host says when they changed, and the stream's `ready` -- the page
  // opened, back in view, back online -- reads them too. A change heard while the player is in the create dialog
  // or acting waits for them to finish rather than redrawing under them.
  let owed = false;
  const resume = () => {
    if (doc.hidden) return;
    if (dialog.open || busy) { owed = true; return; }
    owed = false;
    void refresh();
  };
  dialog.addEventListener('close', () => { if (owed) resume(); });
  // Back in view or back online reads at once, whatever the stream can say yet.
  doc.addEventListener('visibilitychange', resume);
  globalThis.addEventListener?.('online', resume);
  const unlisten = changes(resume);
  return {
    stop: () => { unlisten(); globalThis.clearTimeout(retry); doc.removeEventListener('visibilitychange', resume); globalThis.removeEventListener?.('online', resume); },
    /** Opens the create dialog with that friend chosen: what the Friends panel's Invite does. */
    invite: friend => openCreate({ friend: friend?.id ?? null }),
    /** Told every friends list the Friends panel reads, so each recent opponent offers the way its friendship allows. */
    friendsChanged(list) {
      const next = reconcileRelations(opponents, list);
      if (next === opponents || next.every((one, index) => one === opponents[index])) return;
      opponents = next;
      if (listing) render();
    },
    /** Told every read of the player's games, so the bell counts them without reading them again. */
    onMine(tell) { onMine = tell; },
    /** Reads the lists again: the bell answered an invitation. */
    refresh: () => void refresh(),
    /** Told whenever whether an invitation of this player's is waiting changes, so the Friends panel can say so. */
    onOutstanding(tell) { onOutstanding = tell; },
    /** Told by the sign-in whenever who is signed in changes; the lists are read again for them. */
    setAccount(next, nextMode) {
      const changed = (next?.id ?? null) !== (account?.id ?? null) || nextMode !== mode;
      account = next;
      mode = nextMode;
      if (changed) {
        revision += 1; adopted = false; mine = []; invitations = []; opponents = []; limit = null; finishedSeen = null; finishedReads = 0;
        // Open seats leave out the player's own tables: another account's page is not this one's.
        publicTables.page = 0; publicTables.data = null;
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
  const practiceList = mountPracticeList(document);
  const next = nextPath(globalThis.location.search);
  const navigation = mountNavigation(document, storage, {
    onInvite: lobby.invite,
    onFriends: lobby.friendsChanged,
    onInbox: () => lobby.refresh(),
    onChange(account, mode) {
      if (account && next) { globalThis.location.assign(next); return; }
      const playable = Boolean(account) || mode === 'off';
      document.getElementById('welcome-intro').hidden = playable;
      document.getElementById('home-games').hidden = !playable;
      document.getElementById('hero-eyebrow').textContent = account ? `Welcome back, ${account.username}` : 'Your games';
      lobby.setAccount(account, mode);
      practiceList.setAccount(account);
    },
  });
  lobby.onOutstanding(navigation.setOutstanding);
  lobby.onMine(navigation.inbox.feed);
}
