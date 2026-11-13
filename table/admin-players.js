// Headquarters' Players tab (docs/tabletop/admin-plan.md, A2; ADR 0174): every account, twenty a page, searched by
// name and sorted by a column, and one account's sheet with the three things the operator can do to it: sign it out
// everywhere, rename it, delete it. The host pages and sorts; this page draws what it is sent and says what it did.

export const PLAYER_SORTS = Object.freeze([
  { value: 'seen', label: 'Last seen' },
  { value: 'signed-in', label: 'Last sign-in' },
  { value: 'created', label: 'Newest' },
  { value: 'name', label: 'Name' },
  { value: 'games', label: 'Most games' },
  { value: 'rating', label: 'Rating' },
]);

/** The address of a page of players: the search, the sort and the page, each left out when it is the default. */
export function playersPath({ q = '', sort = 'seen', page = 1 } = {}) {
  const asked = new URLSearchParams();
  const search = String(q ?? '').trim();
  if (search) asked.set('q', search);
  if (sort && sort !== 'seen') asked.set('sort', sort);
  if (Number.isInteger(page) && page > 1) asked.set('page', String(page));
  const query = asked.toString();
  return `/api/admin/players${query ? `?${query}` : ''}`;
}

/** How long ago a moment was, in the largest unit that keeps it short; "never" for a moment not kept. */
export function ago(moment, now = Date.now()) {
  if (!moment) return 'never';
  const then = new Date(moment).getTime();
  if (!Number.isFinite(then)) return 'never';
  const minutes = Math.floor(Math.max(0, now - then) / 60000);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours} h ago`;
  const days = Math.floor(hours / 24);
  if (days < 60) return `${days} d ago`;
  return new Date(then).toISOString().slice(0, 10);
}

/** A rating as the list shows it: the number, marked while it is provisional, or a dash before a ranked game. */
export function ratingText(player) {
  if (!Number.isInteger(player?.rating)) return '—';
  return player.provisional ? `${player.rating}?` : String(player.rating);
}

/** One account as a row of the list, from the host's answer alone. */
export function playerRows(answer, now = Date.now()) {
  return (answer?.players ?? []).map(player => ({
    id: player.id,
    username: player.username,
    created: player.createdAt ? new Date(player.createdAt).toISOString().slice(0, 10) : '—',
    signedIn: ago(player.lastSignIn, now),
    seen: ago(player.lastSeen, now),
    games: player.games ?? 0,
    split: `${player.ranked ?? 0} ranked · ${player.againstPlayers ?? 0} vs players · ${player.againstBots ?? 0} vs bots`,
    wins: player.wins ?? 0,
    rating: ratingText(player),
  }));
}

/** What the pager under the list says: how many players, how many of them the search kept, and which page. */
export function playersLine(answer) {
  const total = answer?.total ?? 0;
  const matching = answer?.matching ?? total;
  const counted = matching === total ? `${total}` : `${matching} of ${total}`;
  return `${counted} player${total === 1 ? '' : 's'} · page ${answer?.page ?? 1} of ${answer?.pages ?? 1}`;
}

/** Whether the name typed confirms deleting this account: the account's name, whatever its case. */
export function deleteConfirmed(typed, username) {
  return String(typed ?? '').trim().toLowerCase() === String(username ?? '').toLowerCase() && String(username ?? '') !== '';
}

/** What the sheet says after the host answered one of its actions. A refusal keeps the host's own sentence. */
export function actionSaid(action, answer, username) {
  if (!answer?.ok) return { refused: true, line: answer?.body?.message ?? `The host refused it (${answer?.status}).` };
  switch (action) {
    case 'sign-out': return { refused: false, line: `${username} is signed out everywhere. Every device asks them to sign in again.` };
    case 'rename': return { refused: false, line: `Renamed to ${answer.body?.account?.username ?? 'the new name'}.` };
    case 'delete': return { refused: false, line: `${username}'s account is deleted.` };
    default: return { refused: false, line: 'Done.' };
  }
}

/** One finished game as the sheet lists it. */
export function gameLine(game, now = Date.now()) {
  const outcome = { win: 'Won', loss: 'Lost', draw: 'Drew', played: 'Played both seats' }[game?.outcome] ?? 'Played';
  const against = game?.outcome === 'played' ? '' : ` against ${game?.opponent ?? 'someone'}`;
  const kind = game?.ranked ? 'ranked' : 'unranked';
  return `${outcome}${against} · ${game?.format ?? '?'} · ${kind} · ${ago(game?.finishedAt, now)}`;
}

// ---- The tab itself. Everything above is pure and tested; this part only moves it onto the screen.

function made(tag, className, text) {
  const one = document.createElement(tag);
  if (className) one.className = className;
  if (text !== undefined) one.textContent = text;
  return one;
}

function fact(term, value) {
  return [made('dt', '', term), made('dd', '', value)];
}

/**
 * Draws the Players tab into the page: the list, its pager, and the sheet. `send(method, path, body)` is the
 * admin transport's; `shown(available)` tells the page whether this host has players at all, so it can hide the tab.
 */
export function mountPlayers({ send, element, shown }) {
  const state = { q: '', sort: 'seen', page: 1, last: null };
  let opened = null;
  let timer = null;

  element('players-sort').replaceChildren(...PLAYER_SORTS.map(sort => Object.assign(made('option', '', sort.label), { value: sort.value })));

  async function redraw() {
    const answer = await send('GET', playersPath(state));
    const told = element('players-said');
    if (answer.status === 404) {
      shown(false);
      return;
    }
    shown(true);
    if (!answer.ok) {
      told.textContent = answer.body?.message ?? `The host answered ${answer.status}.`;
      told.classList.add('refused');
      return;
    }
    told.textContent = '';
    told.classList.remove('refused');
    state.last = answer.body;
    state.page = answer.body.page ?? 1;
    const rows = playerRows(answer.body);
    element('players').replaceChildren(...rows.map(row => playerRow(row)));
    element('players-empty').hidden = rows.length > 0;
    element('players-empty').textContent = state.q ? `No player's name holds "${state.q}".` : 'Nobody has an account yet.';
    element('players-count').textContent = playersLine(answer.body);
    element('players-previous').disabled = state.page <= 1;
    element('players-next').disabled = state.page >= (answer.body.pages ?? 1);
    element('tab-players-count').textContent = String(answer.body.total ?? 0);
  }

  function playerRow(row) {
    const line = made('tr');
    const name = made('td', 'player-name');
    const open = made('button', 'player-open', row.username);
    open.type = 'button';
    open.dataset.player = row.id;
    open.addEventListener('click', () => void openSheet(row.id));
    name.append(open);
    const games = made('td', 'player-games', String(row.games));
    games.title = row.split;
    line.append(
      name,
      made('td', 'player-created', row.created),
      made('td', 'player-signed-in', row.signedIn),
      made('td', 'player-seen', row.seen),
      games,
      made('td', 'player-wins', String(row.wins)),
      made('td', 'player-rating', row.rating),
    );
    return line;
  }

  async function openSheet(id) {
    const answer = await send('GET', `/api/admin/players/${encodeURIComponent(id)}`);
    if (!answer.ok) {
      element('players-said').textContent = answer.body?.message ?? `The host answered ${answer.status}.`;
      element('players-said').classList.add('refused');
      await redraw();
      return;
    }
    opened = answer.body;
    drawSheet();
    const sheet = element('player-sheet');
    if (!sheet.open) sheet.showModal?.() ?? sheet.setAttribute('open', '');
  }

  function drawSheet() {
    const player = opened.player;
    const [row] = playerRows({ players: [player] });
    element('player-sheet-name').textContent = player.username;
    const facts = element('player-facts');
    facts.replaceChildren(
      ...fact('Account', player.id),
      ...fact('Created', row.created),
      ...fact('Last sign-in', row.signedIn),
      ...fact('Last seen', row.seen),
      ...fact('Games', `${row.games} (${row.split})`),
      ...fact('Wins', String(row.wins)),
      ...fact('Rating', row.rating),
      ...fact('Friends', `${opened.friends ?? 0}${opened.requests ? ` · ${opened.requests} pending` : ''}`),
      ...fact('Devices told', String(opened.devices ?? 0)),
    );
    const recent = opened.recent ?? [];
    element('player-recent').replaceChildren(...recent.map(game => made('li', '', gameLine(game))));
    element('player-recent-empty').hidden = recent.length > 0;
    element('player-rename-name').value = player.username;
    element('player-delete-name').value = '';
    element('player-delete-name').placeholder = player.username;
    element('player-delete').disabled = true;
    element('player-signout-confirm').hidden = true;
    say('', false);
  }

  function say(line, refused) {
    const told = element('player-said');
    told.textContent = line;
    told.classList.toggle('refused', refused);
  }

  async function act(action, method, path, body) {
    const username = opened.player.username;
    const answer = await send(method, path, body);
    const outcome = actionSaid(action, answer, username);
    if (action === 'delete' && answer.ok) {
      element('player-sheet').close?.();
      opened = null;
      await redraw();
      element('players-said').textContent = outcome.line;
      element('players-said').classList.remove('refused');
      return;
    }
    await redraw();
    if (answer.ok) {
      const again = await send('GET', `/api/admin/players/${encodeURIComponent(opened.player.id)}`);
      if (again.ok) {
        opened = again.body;
        drawSheet();
      }
    }
    say(outcome.line, outcome.refused);
  }

  const base = () => `/api/admin/players/${encodeURIComponent(opened.player.id)}`;
  element('player-signout').addEventListener('click', () => {
    element('player-signout-line').textContent = `Sign ${opened.player.username} out everywhere? Every device they use asks them to sign in again, and stops being told of their turns.`;
    element('player-signout-confirm').hidden = false;
  });
  element('player-signout-no').addEventListener('click', () => { element('player-signout-confirm').hidden = true; });
  element('player-signout-yes').addEventListener('click', () => void act('sign-out', 'POST', `${base()}/sign-out`));
  element('player-rename').addEventListener('click', () => void act('rename', 'POST', `${base()}/rename`, { username: element('player-rename-name').value.trim() }));
  element('player-delete-name').addEventListener('input', () => {
    element('player-delete').disabled = !deleteConfirmed(element('player-delete-name').value, opened?.player?.username);
  });
  element('player-delete').addEventListener('click', () => void act('delete', 'DELETE', base(), { username: element('player-delete-name').value.trim() }));
  element('player-sheet-close').addEventListener('click', () => element('player-sheet').close?.());

  element('players-search').addEventListener('input', () => {
    clearTimeout(timer);
    timer = setTimeout(() => { state.q = element('players-search').value; state.page = 1; void redraw(); }, 250);
  });
  element('players-sort').addEventListener('change', () => { state.sort = element('players-sort').value; state.page = 1; void redraw(); });
  element('players-previous').addEventListener('click', () => { state.page -= 1; void redraw(); });
  element('players-next').addEventListener('click', () => { state.page += 1; void redraw(); });

  return { redraw };
}
