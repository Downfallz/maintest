// Headquarters' diagnosis of a table in progress (docs/tabletop/scenarios-plan.md, S5b): what anyone may see of a
// stuck table, a check that its journal still replays, what its seats hold face down (asked for, and noted), its bot
// turn owed again, and the Void sheet (ADR 0176). The host reads and acts; this page asks and draws.

const ago = (at, now) => {
  if (!at) return 'never';
  const minutes = Math.max(0, Math.round((now - new Date(at).getTime()) / 60000));
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.round(minutes / 60);
  return hours < 48 ? `${hours} h ago` : `${Math.round(hours / 24)} d ago`;
};

/** A seat as one line: who sits, and what it is asked, if anything. */
export function seatLine(seat) {
  const who = `${seat.slot}: ${seat.name ?? 'nobody'} (${seat.kind}${seat.playedByBot && seat.kind === 'person' ? ', played by a bot now' : ''})`;
  if (!seat.asked) return `${who} · asked nothing`;
  return `${who} · asked ${seat.asked.kind}${Number.isInteger(seat.asked.creature) ? ` for creature ${seat.asked.creature}` : ''}`;
}

/** The facts the sheet lists, each a label and a line, from `GET /api/tables/<id>/diagnosis`. */
export function diagnosisFacts(answer, now = Date.now()) {
  const facts = [
    ['State', [answer.status, answer.held ? 'held by this process' : 'not held here', answer.begun ? 'begun' : 'not begun', answer.over ? 'over' : null, Number.isInteger(answer.round) ? `round ${answer.round}` : null].filter(Boolean).join(' · ')],
    ...(answer.seats ?? []).map(seat => [seat.slot === 'player1' ? 'Seat 1' : 'Seat 2', seatLine(seat)]),
  ];
  if (answer.answerBy) facts.push(['Answer by', new Date(answer.answerBy).toISOString().replace('T', ' ').slice(0, 16) + ' UTC']);
  const journal = answer.journal ?? {};
  facts.push(['Journal', `${journal.entries ?? 0} entries · last written ${ago(journal.lastWritten, now)}`]);
  if (journal.tail?.length) facts.push(['Last lines', journal.tail.map(line => `${line.kind}${line.seat ? ` (${line.seat})` : ''}`).join(' → ')]);
  const basis = answer.basis ?? {};
  facts.push(['Played on', [basis.rules, basis.content ? `content ${basis.content.slice(0, 12)}` : 'this host\'s content', `engine ${basis.engine}`].filter(Boolean).join(' · ')]);
  if (basis.refusedUnder) facts.push(['Refused under', basis.refusedUnder]);
  const turns = answer.turns ?? {};
  const troubles = [turns.queued ? `${turns.letters ?? 0} dead letter${turns.letters === 1 ? '' : 's'}` : 'turns taken in this process', turns.lastFailure ? `last failure: ${turns.lastFailure}` : null, turns.lastError ? `last letter: ${turns.lastError}` : null].filter(Boolean);
  facts.push(['Bot turns', troubles.join(' · ')]);
  return facts;
}

/** What each seat holds face down, as lines, from `POST .../diagnosis/hidden`. */
export function hiddenLines(answer) {
  return (answer?.seats ?? []).map(seat => {
    const parts = [
      ...seat.speeds.map(one => `creature ${one.creature} ${one.speed}`),
      ...seat.intents.map(one => `creature ${one.creature} intends ${one.spell}`),
      ...seat.picks.map(one => `creature ${one.creature} picks ${one.tier}`),
      ...seat.held.map(one => one.pass ? 'holds a pass' : `holds ${one.kind}${one.creature ? ` for creature ${one.creature}` : ''}${one.spell ? ` ${one.spell}` : ''}${one.tier ? ` ${one.tier}` : ''}${one.speed ? ` ${one.speed}` : ''}`),
    ];
    return `${seat.slot}: ${parts.length ? parts.join(' · ') : 'nothing face down'}`;
  });
}

// ---- The sheet. Everything above is pure and tested; this part only moves it onto the screen.

function made(tag, className, text) {
  const one = document.createElement(tag);
  if (className) one.className = className;
  if (text !== undefined) one.textContent = text;
  return one;
}

/** Mounts the Diagnose sheet; `openVoid(row)` opens the Void sheet on the same table, `after()` redraws the tables. */
export function mountDiagnosis({ send, element, openVoid, after }) {
  let shown = null;
  const base = () => `/api/tables/${encodeURIComponent(shown.id)}/diagnosis`;

  function said(line, refused = false) {
    element('diagnosis-said').textContent = line;
    element('diagnosis-said').classList.toggle('refused', refused);
  }

  async function read() {
    const answer = await send('GET', base());
    if (!answer.ok) {
      said(answer.body?.message ?? `The host answered ${answer.status}.`, true);
      return;
    }
    element('diagnosis-facts').replaceChildren(...diagnosisFacts(answer.body).flatMap(([label, line]) => [made('dt', '', label), made('dd', '', line)]));
  }

  async function open(row) {
    shown = row;
    element('diagnosis-sheet-name').textContent = row.id;
    element('diagnosis-facts').replaceChildren();
    element('diagnosis-hidden').replaceChildren();
    element('diagnosis-hidden').hidden = true;
    said('Reading the table…');
    const sheet = element('diagnosis-sheet');
    if (!sheet.open) sheet.showModal?.() ?? sheet.setAttribute('open', '');
    await read();
    if (element('diagnosis-said').textContent === 'Reading the table…') said('');
  }

  element('diagnosis-close').addEventListener('click', () => element('diagnosis-sheet').close?.());
  element('diagnosis-refresh').addEventListener('click', () => void read());
  element('diagnosis-replay').addEventListener('click', async () => {
    said('Rebuilding the table aside from its journal…');
    const answer = await send('POST', `${base()}/replay`);
    said(answer.body?.message ?? `The host answered ${answer.status}.`, !answer.ok || answer.body?.replays === false);
  });
  element('diagnosis-reveal').addEventListener('click', async () => {
    if (!globalThis.confirm?.('Show what each seat holds face down? It is noted in the Activity tab with your name.')) return;
    const answer = await send('POST', `${base()}/hidden`);
    if (!answer.ok) {
      said(answer.body?.message ?? `The host answered ${answer.status}.`, true);
      return;
    }
    element('diagnosis-hidden').replaceChildren(...hiddenLines(answer.body).map(line => made('li', '', line)));
    element('diagnosis-hidden').hidden = false;
    said('Shown, and noted in the Activity tab.');
  });
  element('diagnosis-turn').addEventListener('click', async () => {
    const answer = await send('POST', `${base()}/turn`);
    said(answer.body?.message ?? `The host answered ${answer.status}.`, !answer.ok);
    if (answer.ok) { await read(); await after(); }
  });
  element('diagnosis-void').addEventListener('click', () => {
    if (!shown) return;
    element('diagnosis-sheet').close?.();
    openVoid?.({ id: shown.id, players: shown.players, over: false });
  });

  return { open };
}
