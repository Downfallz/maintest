// The operator's admin panel: the tables this host is playing, the form that opens one, and the sessions its
// store holds (ADR 0081, amended).
//
// It is the console of a host that has none. What a table's console lines used to say -- each seat's code,
// the pilot's link, where the session is written -- this page shows, to the one person the host lets in:
// whoever holds the token the console printed, or whoever the platform in front of the host signed in. The
// game is not open yet, so the operator opens every table and the players, who never sign in, join by code.

const POLL_MS = 2500;

// Where this browser keeps the operator's token once a link brought it, so the table page can show the way
// back here and a reload needs no link. The same reasoning as the seats' (session.js).
export const OPERATOR_KEY = 'downfall.table.operator';

// `person` is a seat somebody joins by its code; the host's answer carries the bots, read off its weights
// directory, so a new search is offered the day it lands and nothing here names one.
export const PERSON = { value: 'person', label: 'a person, who joins by code' };

// What the pickers offer: the person, then the host's bots in the host's order. Before the host has answered,
// or on a host that offers nothing, the person and the two bots every build has.
export function seatable(answer) {
  const offered = Array.isArray(answer?.agents) && answer.agents.length > 0
    ? answer.agents
    : [{ value: 'greedy', label: 'Greedy — the baseline' }, { value: 'random', label: 'Random' }];
  return [PERSON, ...offered.map(agent => ({ value: agent.value, label: agent.label ?? agent.value, featured: agent.featured === true }))];
}

// The bot a new table's second seat is opened with when the operator has not chosen: the first the host puts
// forward, which is what the journal measured as worth playing against.
export function defaultOpponent(answer) {
  return seatable(answer).find(agent => agent.featured)?.value ?? 'greedy';
}

// The operator's token: the link's, kept, or the kept one from an earlier link. Null when there is none, which
// on a host behind the platform is the ordinary case: the browser's own cookies are the sign-in then.
export function operatorToken(search, storage) {
  const linked = new URLSearchParams(search ?? '').get('token');
  if (linked) {
    try { storage?.setItem(OPERATOR_KEY, linked); } catch { /* a browser that keeps nothing still works from the link */ }
    return linked;
  }
  try {
    return storage?.getItem(OPERATOR_KEY) || null;
  } catch {
    return null;
  }
}

// One factory, so a test hands the page a stub instead of a network. With a token it speaks as the operator
// the console named; without one it sends the browser's own cookies, which is how the platform's sign-in
// reaches the host.
export function adminTransport(token, fetchImpl = globalThis.fetch.bind(globalThis)) {
  async function send(method, path, body) {
    const options = { method, headers: {} };
    if (token) options.headers['X-Seat-Token'] = token;
    // Every write says it is JSON, a close included: the host's same-origin fence takes a write without that
    // header as a form another site posted, and refuses it (HttpHost.CrossSite).
    if (method !== 'GET') {
      options.headers['Content-Type'] = 'application/json';
      options.body = JSON.stringify(body ?? {});
    }
    const response = await fetchImpl(path, options);
    const text = await response.text();
    return { status: response.status, ok: response.ok, body: text ? parse(text) : null };
  }

  // A zip is bytes, not JSON: fetched the same way and handed back as a blob for the browser to save.
  async function download(method, path, body) {
    const options = { method, headers: {} };
    if (token) options.headers['X-Seat-Token'] = token;
    if (method !== 'GET') {
      options.headers['Content-Type'] = 'application/json';
      options.body = JSON.stringify(body ?? {});
    }
    const response = await fetchImpl(path, options);
    if (!response.ok) {
      const text = await response.text();
      return { status: response.status, ok: false, body: text ? parse(text) : null };
    }
    return { status: response.status, ok: true, blob: await response.blob(), filename: filenameOf(response.headers?.get?.('Content-Disposition')) };
  }

  return {
    list: () => send('GET', '/api/tables'),
    open: request => send('POST', '/api/tables', request),
    close: id => send('DELETE', `/api/tables/${encodeURIComponent(id)}`),
    sessions: () => send('GET', '/api/sessions'),
    deleteSessions: ids => send('DELETE', '/api/sessions', { ids }),
    exportSessions: ids => download('POST', '/api/sessions/export', { ids }),
    exportSession: id => download('GET', `/api/sessions/${encodeURIComponent(id)}/export`),
  };
}

// The file name a download was given, or a plain one. The host names a single export by its session id.
export function filenameOf(disposition) {
  const match = /filename="([^"]+)"/.exec(disposition ?? '');
  return match ? match[1] : 'sessions.zip';
}

// Where the operator signs in, when the host says a sign-in is what is missing, or nothing. A 401 carrying a
// login is the platform's door; a 403 is a token the console printed and this page was not opened with.
export function signInNeeded(answer) {
  return answer?.status === 401 && typeof answer.body?.login === 'string' ? answer.body.login : null;
}

// What the form asks for, or the reason it is not asked at all. The host checks all of this again; this is so
// a typo reads as a sentence rather than as a 400.
export function openAsked({ player1, player2, who, handover }) {
  if (!player1 || !player2) return { problem: 'Choose who sits in each seat.' };
  if (player1 !== 'person' && player2 !== 'person') return { problem: 'At least one seat is a person: two bots need no table.' };

  const asked = { player1, player2 };
  const initials = (who ?? '').trim();
  if (initials) asked.who = initials;

  const round = String(handover ?? '').trim();
  if (round) {
    const wanted = Number(round);
    if (!Number.isInteger(wanted) || wanted < 1) return { problem: 'A handover round is a whole number, 1 or more.' };
    asked.handover = wanted;
  }

  return asked;
}

// One table as the page draws it, from the host's answer alone. Pure, because what the operator is shown is
// worth a test that does not need a browser.
export function tableRows(list) {
  return (list?.tables ?? []).map(table => ({
    id: table.id,
    state: stateOf(table),
    seats: (table.seats ?? []).map(seat => ({
      slot: seat.slot,
      seated: seat.seated ?? 'nobody',
      code: seat.code ?? null,
      join: seat.join ?? null,
      link: seat.link ?? null,
    })),
    hotseat: table.hotseat ?? null,
    pilot: table.pilot,
    session: table.finished ? table.session : null,
    location: table.location ?? null,
    closable: !table.finished,
  }));
}

// A round the host could not read in time is a match in the middle of something, never one that has not
// started: round 1 begins the moment a table opens.
function stateOf(table) {
  if (table.finished) return 'Finished';
  if (table.over) return 'Over, being written';
  if (Number.isInteger(table.round)) return `Round ${table.round}`;
  return 'Playing';
}

// One recorded session as the list draws it. A session this host still has a table for is live; one with a
// match counted is finished; anything else was abandoned, or is being played by a replica that is gone.
export function sessionRows(answer) {
  return (answer?.sessions ?? []).map(session => ({
    id: session.id,
    when: session.createdAt ? new Date(session.createdAt).toLocaleString() : 'unknown date',
    players: session.player1 && session.player2 ? `${session.player1} vs ${session.player2}` : 'no manifest',
    state: session.live ? 'live on this host' : session.over ? 'finished' : 'not finished',
    steps: session.steps ?? 0,
    live: session.live === true,
    session: session.session,
    export: session.export,
    location: session.location ?? null,
  }));
}

// What the page says after the host answered an opening. A refusal is the host telling the operator something
// true, so it reads as a sentence and keeps its code.
export function said(answer) {
  if (answer.ok) {
    const codes = (answer.body?.seats ?? []).filter(seat => seat.code).map(seat => `${seat.slot} ${seat.code}`);
    const toReadOut = codes.length > 0 ? `: ${codes.join(', ')}` : '';
    return { refused: false, line: `Table ${answer.body?.id ?? ''} opened${toReadOut}.` };
  }

  const body = answer.body ?? {};
  return { refused: true, line: body.message ?? `The host refused it (${answer.status}).` };
}

// What the page says after a deletion: how many went, and which were not there to go.
export function deletionSaid(answer) {
  if (!answer.ok) return { refused: true, line: answer.body?.message ?? `The host refused it (${answer.status}).` };
  const deleted = answer.body?.deleted ?? [];
  const missing = answer.body?.missing ?? [];
  const gone = `${deleted.length} session${deleted.length === 1 ? '' : 's'} deleted`;
  return { refused: false, line: missing.length > 0 ? `${gone}; not found: ${missing.join(', ')}.` : `${gone}.` };
}

function parse(text) {
  try {
    return JSON.parse(text);
  } catch {
    return { message: text };
  }
}

// ---- The page itself. Everything above is pure and tested; this part only moves it onto the screen.

function element(id) {
  return document.getElementById(id);
}

function option(value, label) {
  const made = document.createElement('option');
  made.value = value;
  made.textContent = label;
  return made;
}

function anchor(href, text) {
  const made = document.createElement('a');
  made.href = href;
  made.textContent = text;
  return made;
}

function line(term, ...content) {
  const dt = document.createElement('dt');
  dt.textContent = term;
  const dd = document.createElement('dd');
  dd.append(...content);
  return [dt, dd];
}

function card(row, transport, redraw) {
  const box = document.createElement('section');
  box.className = 'seat-card table-card';

  const name = document.createElement('h2');
  name.textContent = row.id;
  const state = document.createElement('p');
  state.className = 'muted';
  state.textContent = row.state;

  const list = document.createElement('dl');
  for (const seat of row.seats) {
    const shown = seat.code
      ? [document.createTextNode(`${seat.seated} · code `), Object.assign(document.createElement('strong'), { className: 'code', textContent: seat.code }), document.createTextNode(' · '), anchor(seat.join, 'join link')]
      : [document.createTextNode(seat.seated)];
    list.append(...line(seat.slot, ...shown));
  }
  if (row.hotseat) list.append(...line('Both seats', anchor(row.hotseat, 'one browser, both seats')));
  list.append(...line('Pilot', anchor(row.pilot, 'hand a seat over')));
  if (row.session) list.append(...line('Session', anchor(row.session, 'read it in the viewer')));
  if (row.location) list.append(...line('Written to', document.createTextNode(row.location)));

  box.append(name, state, list);
  if (row.closable) {
    const close = document.createElement('button');
    close.type = 'button';
    close.className = 'close';
    close.textContent = 'Close this table';
    close.addEventListener('click', async () => {
      close.disabled = true;
      await transport.close(row.id);
      await redraw();
    });
    box.append(close);
  }
  return box;
}

function sessionItem(row, transport) {
  const item = document.createElement('li');
  item.className = `session-row${row.live ? ' live' : ''}`;
  const check = document.createElement('input');
  check.type = 'checkbox';
  check.value = row.id;
  check.setAttribute('aria-label', `Select ${row.id}`);
  const body = document.createElement('div');
  const id = document.createElement('div');
  id.className = 'session-id';
  id.textContent = row.id;
  const facts = document.createElement('div');
  facts.className = 'muted';
  facts.textContent = `${row.when} · ${row.players} · ${row.state} · ${row.steps} steps`;
  const links = document.createElement('div');
  links.className = 'session-links';
  links.append(anchor(row.session, 'viewer'));
  const save = document.createElement('a');
  save.href = row.export;
  save.textContent = 'export zip';
  save.addEventListener('click', async event => {
    event.preventDefault();
    await saveZip(await transport.exportSession(row.id));
  });
  links.append(save);
  if (row.location) links.append(Object.assign(document.createElement('span'), { className: 'muted', textContent: row.location }));
  body.append(id, facts, links);
  item.append(check, body);
  return item;
}

async function saveZip(answer) {
  const told = element('sessions-said');
  if (!answer.ok) {
    told.textContent = answer.body?.message ?? `The host refused it (${answer.status}).`;
    told.classList.add('refused');
    return;
  }
  const url = URL.createObjectURL(answer.blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = answer.filename;
  document.body.append(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
  told.textContent = `Saved ${answer.filename}.`;
  told.classList.remove('refused');
}

async function start() {
  const token = operatorToken(globalThis.location?.search ?? '', kept());
  if (globalThis.location?.search && globalThis.history?.replaceState) {
    globalThis.history.replaceState(null, '', globalThis.location.pathname);
  }
  const transport = adminTransport(token);
  const problem = element('problem');
  let offered = null;

  function fillPickers(answer) {
    const list = seatable(answer);
    const key = list.map(agent => agent.value).join('|');
    if (key === offered) return;
    offered = key;
    for (const chooser of ['seat-1', 'seat-2']) {
      const kept = element(chooser).value;
      element(chooser).replaceChildren(...list.map(agent => option(agent.value, agent.label)));
      if (list.some(agent => agent.value === kept)) element(chooser).value = kept;
    }
    if (!element('seat-2').value || element('seat-2').value === 'person') element('seat-2').value = defaultOpponent(answer);
  }
  fillPickers(null);

  async function redraw() {
    const answer = await transport.list();
    const login = signInNeeded(answer);
    if (login) {
      const door = element('sign-in');
      door.href = login;
      door.hidden = false;
      element('admin').hidden = true;
      problem.textContent = 'This host is behind a sign-in.';
      problem.hidden = false;
      return;
    }

    if (!answer.ok) {
      problem.textContent = answer.body?.message ?? `The host answered ${answer.status}.`;
      problem.hidden = false;
      return;
    }

    problem.hidden = true;
    fillPickers(answer.body);
    element('rules').textContent = answer.body?.rules ?? '';
    element('recording').textContent = answer.body?.recording ? 'Sessions are recorded.' : 'Nothing is recorded (--no-record).';
    element('tables').replaceChildren(...tableRows(answer.body).map(row => card(row, transport, redraw)));
    element('admin').hidden = false;
  }

  function selected() {
    return [...element('sessions').querySelectorAll('input[type=checkbox]:checked')].map(box => box.value);
  }

  function armTools() {
    const count = selected().length;
    element('sessions-export').disabled = count === 0;
    element('sessions-delete').disabled = count === 0;
  }

  async function redrawSessions() {
    const answer = await transport.sessions();
    const told = element('sessions-said');
    if (!answer.ok) {
      told.textContent = answer.body?.message ?? `The host answered ${answer.status}.`;
      told.classList.add('refused');
      element('sessions').replaceChildren();
      return;
    }
    told.classList.remove('refused');
    element('sessions').replaceChildren(...sessionRows(answer.body).map(row => sessionItem(row, transport)));
    element('sessions-all').checked = false;
    armTools();
  }

  element('open').addEventListener('click', async () => {
    const asking = openAsked({
      player1: element('seat-1').value,
      player2: element('seat-2').value,
      who: element('who').value,
      handover: element('handover').value,
    });
    const told = element('said');
    if (asking.problem) {
      told.textContent = asking.problem;
      told.classList.add('refused');
      return;
    }

    const answer = await transport.open(asking);
    const outcome = said(answer);
    told.textContent = outcome.line;
    told.classList.toggle('refused', outcome.refused);
    await redraw();
    await redrawSessions();
  });

  element('sessions').addEventListener('change', armTools);
  element('sessions-all').addEventListener('change', () => {
    for (const box of element('sessions').querySelectorAll('input[type=checkbox]')) box.checked = element('sessions-all').checked;
    armTools();
  });
  element('sessions-refresh').addEventListener('click', redrawSessions);
  element('sessions-export').addEventListener('click', async () => saveZip(await transport.exportSessions(selected())));
  element('sessions-delete').addEventListener('click', () => {
    const ids = selected();
    element('sessions-confirm-line').textContent = `Delete ${ids.length} session${ids.length === 1 ? '' : 's'}? A table still being played is closed, and its recording goes with it.`;
    element('sessions-confirm').hidden = false;
  });
  element('sessions-delete-no').addEventListener('click', () => { element('sessions-confirm').hidden = true; });
  element('sessions-delete-yes').addEventListener('click', async () => {
    element('sessions-confirm').hidden = true;
    const answer = await transport.deleteSessions(selected());
    const outcome = deletionSaid(answer);
    const told = element('sessions-said');
    told.textContent = outcome.line;
    told.classList.toggle('refused', outcome.refused);
    await redraw();
    await redrawSessions();
  });

  // Rescheduled after each answer rather than on an interval, so a poll slower than POLL_MS leaves nothing
  // queued behind it. The sessions list is read on demand: listing a store is a walk of every run in it.
  const again = () => setTimeout(() => void redraw().finally(again), POLL_MS);
  await redraw();
  await redrawSessions();
  again();
}

function kept() {
  try {
    return globalThis.localStorage ?? null;
  } catch {
    return null;
  }
}

if (globalThis.document) await start();
