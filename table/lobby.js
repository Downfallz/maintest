// The operator's lobby: the tables this host is playing, and the form that opens one (ADR 0081).
//
// It is the console of a host that has none. What a table's console lines used to say -- each seat's code,
// the pilot's link, where the session is written -- this page shows, to the one person the host lets in:
// whoever holds the token the console printed, or whoever the platform in front of the host signed in.

const POLL_MS = 2500;

// Who a seat may be opened with. `person` is a seat somebody joins by its code; the rest are agent specs the
// CLI understands, the same short list the pilot offers, chosen for what the journal measured. The host takes
// any spec it can parse, including a weights or policy path this page cannot know is on disk.
export const SEATABLE = [
  { value: 'person', label: 'a person, who joins by code' },
  { value: 'greedy', label: 'Greedy — the baseline' },
  { value: 'random', label: 'Random' },
  { value: 'heuristic:learning/weights/search-23.json', label: 'search-23 — the strongest general weights' },
  { value: 'lookahead', label: 'Lookahead' },
];

// One factory, so a test hands the page a stub instead of a network. With a token it speaks as the operator
// the console named; without one it sends the browser's own cookies, which is how the platform's sign-in
// reaches the host.
export function lobbyTransport(token, fetchImpl = globalThis.fetch.bind(globalThis)) {
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

  return {
    list: () => send('GET', '/api/tables'),
    open: request => send('POST', '/api/tables', request),
    close: id => send('DELETE', `/api/tables/${encodeURIComponent(id)}`),
  };
}

// Where the operator signs in, when the host says a sign-in is what is missing, or nothing. A 401 carrying a
// login is the platform's door; a 403 is a token the console printed and this page was not opened with.
export function signInNeeded(answer) {
  return answer && answer.status === 401 && typeof answer.body?.login === 'string' ? answer.body.login : null;
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

// What the page says after the host answered an opening. A refusal is the host telling the operator something
// true, so it reads as a sentence and keeps its code.
export function said(answer) {
  if (answer.ok) {
    const codes = (answer.body?.seats ?? []).filter(seat => seat.code).map(seat => `${seat.slot} ${seat.code}`);
    return { refused: false, line: `Table ${answer.body?.id ?? ''} opened${codes.length > 0 ? `: ${codes.join(', ')}` : ''}.` };
  }

  const body = answer.body ?? {};
  return { refused: true, line: body.message ?? `The host refused it (${answer.status}).` };
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

async function start() {
  const token = new URLSearchParams(globalThis.location?.search ?? '').get('token');
  const transport = lobbyTransport(token);
  const problem = element('problem');

  for (const chooser of ['seat-1', 'seat-2']) {
    element(chooser).replaceChildren(...SEATABLE.map(seat => option(seat.value, seat.label)));
  }
  element('seat-2').value = 'greedy';

  async function redraw() {
    const answer = await transport.list();
    const login = signInNeeded(answer);
    if (login) {
      const door = element('sign-in');
      door.href = login;
      door.hidden = false;
      element('lobby').hidden = true;
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
    element('rules').textContent = answer.body?.rules ?? '';
    element('recording').textContent = answer.body?.recording ? 'Sessions are recorded.' : 'Nothing is recorded (--no-record).';
    element('tables').replaceChildren(...tableRows(answer.body).map(row => card(row, transport, redraw)));
    element('lobby').hidden = false;
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
  });

  // Rescheduled after each answer rather than on an interval, so a poll slower than POLL_MS leaves nothing
  // queued behind it.
  const again = () => setTimeout(() => void redraw().finally(again), POLL_MS);
  await redraw();
  again();
}

if (globalThis.document) await start();
