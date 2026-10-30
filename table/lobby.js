import { activateSeats, isOperator, operatorToken, savedSeats } from './session.js';

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

export function codePath(value) {
  const code = value.trim().replace(/[\s-]/g, '').toUpperCase();
  return /^[A-Z0-9]{8}$/.test(code) ? `/j/${code}` : null;
}

export async function lobbyRequest(path, body, token, fetchImpl = globalThis.fetch.bind(globalThis)) {
  const headers = {};
  if (token) headers['X-Seat-Token'] = token;
  const options = { method: body === undefined ? 'GET' : 'POST', headers };
  if (body !== undefined) { headers['Content-Type'] = 'application/json'; options.body = JSON.stringify(body); }
  const response = await fetchImpl(path, options);
  const text = await response.text();
  let result;
  try { result = text ? JSON.parse(text) : {}; } catch { result = { message: text }; }
  if (!response.ok) throw new Error(result.message || `The host answered ${response.status}. Try again.`);
  return result;
}

export function mountLobby(doc, location, storage) {
  const el = id => doc.getElementById(id);
  let listing = null;
  let mine = [];
  let showAll = false;
  let loading = false;
  let drawn = '';
  let busy = false;
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
    location.assign(`/play?${new URLSearchParams(entry.seats.map(item => [item.seat, item.token]))}`);
  }
  async function act(button, action) {
    if (busy) return;
    busy = true;
    button.disabled = true;
    problem();
    try { enter(await action()); }
    catch (error) { problem(error.message || 'Could not reach the table. Try again.'); await refresh(); }
    finally { busy = false; button.disabled = false; }
  }
  function cardAction(table, entry) {
    if (!entry) return 'Take a seat →';
    return table.over ? 'View game →' : 'Resume game →';
  }
  function card(table, entry) {
    const root = node('article', '', `game-card${entry ? ' mine' : ''}`);
    const top = node('div', '', 'game-card-top');
    top.append(node('h3', `${table.format} battle`), node('span', tableState(table), `status-tag${table.openSeats > 0 ? ' available' : ''}`));
    root.append(top, node('p', table.id, 'game-id'), node('p', (table.players ?? []).join(' vs '), 'game-players'));
    const button = node('button', cardAction(table, entry));
    button.type = 'button';
    button.setAttribute('aria-label', `${entry ? 'Resume' : 'Join'} ${table.format} table ${table.id}`);
    if (entry) { button.className = 'primary'; button.addEventListener('click', () => enter(entry)); }
    else button.addEventListener('click', () => act(button, () => lobbyRequest(`/api/lobby/tables/${encodeURIComponent(table.id)}/join`, {})));
    if (entry || table.openSeats > 0) root.append(button);
    return root;
  }
  function statusText(state) {
    if (!state.recording) return 'This host is not saving games. New tables are unavailable until recording is enabled.';
    const tables = state.tables.length === 1 ? 'table' : 'tables';
    return `${state.tables.length} ${tables} ongoing · updates automatically`;
  }
  function render() {
    const key = JSON.stringify([listing, mine, showAll]);
    if (key === drawn) return;
    drawn = key;
    const available = visibleTables(listing.tables, mine, showAll);
    el('mine-count').textContent = mine.length;
    el('tables-count').textContent = available.length;
    el('my-tables').replaceChildren(...(mine.length ? mine.map(entry => card(entry.table, entry)) : [node('p', 'No saved games here yet. Create a table, take an open seat or enter an invitation code.', 'empty-state')]));
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
    if (loading) return;
    loading = true;
    el('refresh').disabled = true;
    try {
      const [publicTables, saved] = await Promise.all([
        lobbyRequest('/api/lobby'),
        lobbyRequest('/api/lobby/resume', { tokens: savedSeats(storage).map(item => item.token) }),
      ]);
      listing = publicTables;
      mine = saved.tables;
      render();
    } catch (error) {
      el('lobby-status').textContent = 'Connection interrupted. Your saved seats are kept; refresh to retry.';
      problem(error.message || 'Could not load the tables. Try again.');
    } finally { loading = false; el('refresh').disabled = false; }
  }
  el('refresh').addEventListener('click', () => { problem(); void refresh(); });
  for (const [id, all] of [['filter-open', false], ['filter-all', true]]) el(id).addEventListener('click', () => {
    showAll = all;
    el('filter-open').setAttribute('aria-pressed', String(!all));
    el('filter-all').setAttribute('aria-pressed', String(all));
    if (listing) render();
  });
  el('join-code').addEventListener('submit', event => {
    event.preventDefault();
    const path = codePath(el('code').value);
    if (path) location.assign(path);
    else { problem('Enter the eight-character seat code from your invitation.'); el('code').focus(); }
  });
  const dialog = el('create-dialog');
  el('new-table').addEventListener('click', () => { if (listing) { el('create-problem').hidden = true; dialog.showModal(); } });
  for (const id of ['create-close', 'create-cancel']) el(id).addEventListener('click', () => dialog.close());
  const help = {
    person: 'You take the first seat. Another player can join from this lobby or with your invitation.',
    greedy: 'A tactical bot plays the other side. Your game is saved so you can come back later.',
    random: 'A bot chooses random legal moves. Your game is saved so you can come back later.',
    hotseat: 'Both players use this device. Pass it between turns; both seats are saved here.',
  };
  el('opponent').addEventListener('change', () => { el('create-help').textContent = help[el('opponent').value]; });
  el('create-table').addEventListener('submit', async event => {
    event.preventDefault();
    if (busy) return;
    busy = true;
    el('create-submit').disabled = true;
    el('create-problem').hidden = true;
    try { enter(await lobbyRequest('/api/lobby/tables', { opponent: el('opponent').value, format: el('format').value })); }
    catch (error) { el('create-problem').textContent = error.message || 'Could not create the table. Try again.'; el('create-problem').hidden = false; }
    finally { busy = false; el('create-submit').disabled = false; }
  });
  void lobbyRequest('/api/me', undefined, operatorToken(storage)).then(body => {
    if (isOperator({ ok: true, body })) el('admin-link').hidden = false;
  }).catch(() => {});
  void refresh();
  const interval = globalThis.setInterval(() => { if (!doc.hidden && !dialog.open && !busy) void refresh(); }, 10000);
  return () => globalThis.clearInterval(interval);
}

if (typeof document !== 'undefined') {
  let storage = null;
  try { storage = globalThis.localStorage; } catch { /* Invitations still work without browser storage. */ }
  mountLobby(document, globalThis.location, storage);
}
