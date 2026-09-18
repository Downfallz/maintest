// The pilot's own page: who is playing each seat, and how to hand one over.
//
// It is not a god view. In hotseat the operator is usually also one of the two players, so a page showing both
// boards would hand that person their opponent's face-down Intents (playtest-app.md §2.4) -- through the back
// door, on their own screen. The host's answer carries no board and no hand at all, and this page draws only
// what that answer holds.

const POLL_MS = 900;

// What a pilot may seat. `person` is the seat's own player, which only a seat that has one can be handed back
// to; the rest are agent specs the CLI understands, chosen for what the journal measured rather than for what
// sounds hard -- `lookahead` and `minimax` read like the difficult setting and neither beats Greedy
// (docs/learning/journal.md, 2026-09-16). This list is not the whole of what the host accepts: any spec the CLI
// parses works through `POST /api/pilot/seats/<slot>`, including a weights or policy path a page cannot know is
// on disk.
export const SEATABLE = [
  { value: 'person', label: 'the person whose seat it is' },
  { value: 'greedy', label: 'Greedy — the baseline' },
  { value: 'random', label: 'Random' },
  { value: 'heuristic:learning/weights/search-4.json', label: 'search-4 — the strongest general weights' },
  { value: 'heuristic:learning/weights/stun-first.json', label: 'stun-first — beats Greedy every match' },
  { value: 'heuristic:learning/weights/kill-first.json', label: 'kill-first — beats Greedy every match' },
];

// One factory, so a test hands the page a stub instead of a network. It speaks for the pilot and nothing else:
// the pilot token is not a seat's, and the host refuses it on every seat route.
export function pilotTransport(token, fetchImpl = globalThis.fetch.bind(globalThis)) {
  if (!token) throw new Error('The pilot needs the token the host printed on the console.');

  async function send(method, path, body) {
    const options = { method, headers: { 'X-Seat-Token': token } };
    if (body !== undefined) {
      options.headers['Content-Type'] = 'application/json';
      options.body = JSON.stringify(body);
    }
    const response = await fetchImpl(path, options);
    const text = await response.text();
    return { status: response.status, ok: response.ok, body: text ? parse(text) : null };
  }

  return {
    view: () => send('GET', '/api/pilot'),
    swap: (slot, agent, round) => send('POST', `/api/pilot/seats/${slot}`, { agent, round }),
  };
}

// What each seat reads as, from the host's answer alone. Pure, because what a pilot is allowed to see is worth
// a test that does not need a browser.
export function seatRows(view) {
  return (view?.seats ?? []).map(seat => ({
    slot: seat.slot,
    seated: seat.seated ?? 'nobody',
    // A seat with no person can be handed to any agent and back to none: there is no player holding a token
    // for it, so `person` would name somebody who never joined.
    canTakeThePerson: seat.hasPerson === true,
    asked: seat.waitingFor
      ? `${seat.waitingFor}${seat.waitingCreature ? ` · creature ${seat.waitingCreature}` : ''}`
      : null,
    pending: seat.pending ? `${seat.pending.to} from round ${seat.pending.round}` : null,
  }));
}

// The one line the page says about the match, and the reason a swap may be refused before it is sent.
export function whereItIs(view) {
  if (!view) return 'Connecting…';
  if (view.over) return 'The match is over.';
  return view.round ? `Round ${view.round}${view.subPhase ? ` · ${view.subPhase}` : ''}` : 'Waiting for the first round.';
}

// The earliest round a swap may name: the one after the round being played, because a seat changes hands at the
// top of a round. Read off the same answer the page is drawing, so the field cannot suggest a round the host
// will refuse -- it can still be refused, because the match moves while somebody types.
export function earliestRound(view) {
  return (view?.round ?? 0) + 1;
}

// What a swap asks for, or the reason it is not asked at all. The host checks all of this again; this is so a
// typo costs nothing and reads as a sentence rather than as a 409.
export function swapAsked(slot, agent, round, view) {
  if (!slot) return { problem: 'Choose a seat.' };
  if (!agent) return { problem: 'Choose who takes it.' };

  const wanted = Number(round);
  if (!Number.isInteger(wanted) || wanted < 1) return { problem: 'A round is a whole number, 1 or more.' };

  const floor = earliestRound(view);
  if (wanted < floor) {
    return { problem: `Round ${wanted} is played or being played. Name ${floor} or later.` };
  }

  const seat = seatRows(view).find(row => row.slot === slot);
  if (agent === 'person' && seat && !seat.canTakeThePerson) {
    return { problem: `Nobody ever joined ${slot}, so there is no person to hand it back to.` };
  }

  return { slot, agent, round: wanted };
}

// What the page says after the host answered. A refused swap is not an error in the page: it is the host
// telling the operator something true about the match, so it reads as a sentence and keeps its code.
export function said(answer) {
  if (answer.ok) {
    const { from, to, round } = answer.body ?? {};
    return { refused: false, line: `${answer.body?.slot ?? 'the seat'}: ${from} → ${to}, from round ${round}.` };
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

function draw(view) {
  element('where').textContent = whereItIs(view);
  element('seats').replaceChildren(...seatRows(view).map(card));

  const chooser = element('swap-seat');
  const rows = seatRows(view);
  if (chooser.options.length !== rows.length) {
    chooser.replaceChildren(...rows.map(row => option(row.slot, row.slot)));
  }

  const round = element('swap-round');
  round.min = String(earliestRound(view));
  if (Number(round.value) < earliestRound(view)) round.value = String(earliestRound(view));
  element('pilot').hidden = false;
}

function card(row) {
  const box = document.createElement('section');
  box.className = 'seat-card';

  const name = document.createElement('h2');
  name.textContent = row.slot;

  const list = document.createElement('dl');
  add(list, 'Playing', row.seated);
  add(list, 'Being asked', row.asked ?? 'nothing right now', row.asked ? 'asked' : null);
  add(list, 'Waiting to become', row.pending ?? 'nobody', row.pending ? 'pending' : null);

  box.append(name, list);
  return box;
}

function add(list, term, value, className) {
  const dt = document.createElement('dt');
  dt.textContent = term;
  const dd = document.createElement('dd');
  dd.textContent = value;
  if (className) dd.className = className;
  list.append(dt, dd);
}

function option(value, label) {
  const made = document.createElement('option');
  made.value = value;
  made.textContent = label;
  return made;
}

async function start() {
  const token = new URLSearchParams(globalThis.location?.search ?? '').get('token');
  if (!token) {
    const problem = element('problem');
    problem.textContent = 'Open this page with the token the console printed: /pilot?token=…';
    problem.hidden = false;
    return;
  }

  const transport = pilotTransport(token);
  element('swap-agent').replaceChildren(...SEATABLE.map(seat => option(seat.value, seat.label)));

  let latest = null;
  element('swap').addEventListener('click', async () => {
    const asking = swapAsked(element('swap-seat').value, element('swap-agent').value, element('swap-round').value, latest);
    const line = element('said');
    if (asking.problem) {
      line.textContent = asking.problem;
      line.classList.add('refused');
      return;
    }

    const answer = await transport.swap(asking.slot, asking.agent, asking.round);
    const told = said(answer);
    line.textContent = told.line;
    line.classList.toggle('refused', told.refused);
    await poll();
  });

  async function poll() {
    const answer = await transport.view();
    if (!answer.ok) {
      const problem = element('problem');
      problem.textContent = answer.body?.message ?? `The host answered ${answer.status}.`;
      problem.hidden = false;
      return;
    }

    element('problem').hidden = true;
    latest = answer.body;
    draw(latest);
  }

  await poll();
  setInterval(poll, POLL_MS);
}

if (globalThis.document) await start();
