// Headquarters' Jobs tab (docs/tabletop/admin-plan.md, A3): the tending runs, newest first, twenty a page, with what
// the last day and the last week of them did; a pass run now from the host; and one run's sheet, every table it
// touched with what it did to it and the way to its session. The host pages and adds up; this page draws it.

import { ago } from './admin-players.js';

/** The address of a page of runs, the page left out when it is the first. */
export function jobsPath(page = 1) {
  return Number.isInteger(page) && page > 1 ? '/api/admin/jobs/tend?page=' + page : '/api/admin/jobs/tend';
}

function counted(count, one, many = one + 's') {
  return `${count} ${count === 1 ? one : many}`;
}

/** What a run did, in a line: only what it did, or that it found nothing due. */
export function didLine(run) {
  const parts = [];
  if (run?.neverBegan?.length) parts.push(`${counted(run.neverBegan.length, 'table')} closed`);
  if (run?.invitationsExpired) parts.push(`${counted(run.invitationsExpired, 'invitation')} let go of`);
  if (run?.timedOut?.length) parts.push(`${counted(run.timedOut.length, 'game')} timed out`);
  if (run?.abandoned?.length) parts.push(`${counted(run.abandoned.length, 'left game')} ended`);
  if (run?.warned?.length) parts.push(`${counted(run.warned.length, 'player')} warned`);
  return parts.length ? parts.join(' · ') : 'Nothing was due';
}

/** Who or what started a run. */
export function triggerText(run) {
  if (run?.trigger !== 'manual') return 'Hourly job';
  return run.by ? 'By hand, ' + run.by : 'By hand';
}

/** How a run ended, with how long it took when that is known. */
export function tookText(run) {
  const outcome = run?.outcome === 'failed' ? 'Failed' : 'Completed';
  return Number.isFinite(run?.seconds) ? `${outcome} in ${run.seconds} s` : outcome;
}

/** One run as a row of the list, from the host's answer alone. */
export function runRows(answer, now = Date.now()) {
  return (answer?.runs ?? []).map(run => ({
    id: run.id,
    when: ago(run.started, now),
    started: run.started ? new Date(run.started).toISOString().replace('T', ' ').slice(0, 19) + ' UTC' : '—',
    trigger: triggerText(run),
    failed: run.outcome === 'failed',
    outcome: run.outcome === 'failed' ? 'Failed' : 'Completed',
    seconds: Number.isFinite(run.seconds) ? `${run.seconds} s` : '',
    took: tookText(run),
    did: run.outcome === 'failed' ? (run.error ?? 'It stopped on an error.') : didLine(run),
  }));
}

/** What a span of runs did, added up: "In 24 hours: 24 runs, 3 tables closed, ...". */
export function totalsLine(totals, span) {
  if (!totals?.runs) return `${span}: no run.`;
  const parts = [
    counted(totals.runs, 'run'),
    `${counted(totals.neverBegan ?? 0, 'table')} that never began closed`,
    `${counted(totals.timedOut ?? 0, 'game')} timed out`,
    `${totals.abandoned ?? 0} left by a player`,
    `${counted(totals.invitationsExpired ?? 0, 'invitation')} let go of`,
  ];
  const failed = totals.failed ? ` (${totals.failed} failed)` : '';
  return `${span}: ${parts.join(', ')}${failed}.`;
}

/** What the pager under the list says. */
export function runsLine(answer) {
  const total = answer?.total ?? 0;
  return `${counted(total, 'run')} kept · page ${answer?.page ?? 1} of ${answer?.pages ?? 1}`;
}

/** Every table a run touched, with what it did to it and the way to its session, in the order they were done. */
export function touchedTables(run) {
  const done = [
    ['neverBegan', 'Closed: it never began'],
    ['timedOut', 'Ended: a player let their time run out'],
    ['abandoned', 'Ended: nobody took the seat its player left'],
    ['warned', 'Warned: a quarter of the time left'],
  ];
  return done.flatMap(([key, what]) => (run?.[key] ?? []).map(id => ({ id, what, session: `/session/${encodeURIComponent(id)}` })));
}

/** What the page says after a pass run now. */
export function passSaid(answer) {
  if (!answer?.ok) return { refused: true, line: answer?.body?.message ?? `The host refused it (${answer?.status}).` };
  return { refused: false, line: `Pass done: ${didLine(answer.body?.run)}.` };
}

// ---- The tab itself. Everything above is pure and tested; this part only moves it onto the screen.

function made(tag, className, text) {
  const one = document.createElement(tag);
  if (className) one.className = className;
  if (text !== undefined) one.textContent = text;
  return one;
}

/**
 * Draws the Jobs tab into the page. `send(method, path, body)` is the admin transport's; `shown(available)` says
 * whether this host keeps runs at all, which a host recording nothing does not.
 */
export function mountJobs({ send, element, shown }) {
  let page = 1;

  async function redraw() {
    const answer = await send('GET', jobsPath(page));
    if (answer.status === 404) {
      shown(false);
      return;
    }
    shown(true);
    const told = element('jobs-said');
    if (!answer.ok) {
      told.textContent = answer.body?.message ?? `The host answered ${answer.status}.`;
      told.classList.add('refused');
      return;
    }
    page = answer.body.page ?? 1;
    element('jobs-day').textContent = totalsLine(answer.body.day, 'In 24 hours');
    element('jobs-week').textContent = totalsLine(answer.body.week, 'In 7 days');
    element('jobs-day').classList.toggle('warning', (answer.body.day?.failed ?? 0) > 0);
    const rows = runRows(answer.body);
    element('jobs-runs').replaceChildren(...rows.map(runRow));
    element('jobs-empty').hidden = rows.length > 0;
    element('jobs-count').textContent = runsLine(answer.body);
    element('jobs-previous').disabled = page <= 1;
    element('jobs-next').disabled = page >= (answer.body.pages ?? 1);
    element('tab-jobs-count').textContent = (answer.body.day?.failed ?? 0) > 0 ? '!' : '';
  }

  function runRow(row) {
    const item = made('li', `session-row run-row${row.failed ? ' failed' : ''}`);
    const open = made('button', 'player-open', `${row.started}`);
    open.type = 'button';
    open.dataset.run = row.id;
    open.addEventListener('click', () => void openSheet(row.id));
    const body = made('div');
    body.append(open, made('div', 'muted', `${row.when} · ${row.trigger} · ${row.took}`), made('div', row.failed ? 'run-error' : '', row.did));
    item.append(body);
    return item;
  }

  async function openSheet(id) {
    const answer = await send('GET', `/api/admin/jobs/tend/${encodeURIComponent(id)}`);
    if (!answer.ok) {
      element('jobs-said').textContent = answer.body?.message ?? `The host answered ${answer.status}.`;
      element('jobs-said').classList.add('refused');
      return;
    }
    const run = answer.body.run;
    const [row] = runRows({ runs: [run] });
    element('run-sheet-name').textContent = row.started;
    const facts = element('run-facts');
    facts.replaceChildren(
      made('dt', '', 'Started by'), made('dd', '', row.trigger),
      made('dt', '', 'Outcome'), made('dd', '', row.took),
      made('dt', '', 'Did'), made('dd', '', run.outcome === 'failed' ? 'It stopped before the end.' : didLine(run)),
    );
    element('run-error').hidden = run.outcome !== 'failed';
    element('run-error').textContent = run.error ?? '';
    const touched = touchedTables(run);
    element('run-tables').replaceChildren(...touched.map(table => {
      const item = made('li');
      const link = made('a', '', table.id);
      link.href = table.session;
      item.append(link, made('span', 'muted', ` · ${table.what}`));
      return item;
    }));
    element('run-tables-empty').hidden = touched.length > 0;
    const sheet = element('run-sheet');
    if (!sheet.open) sheet.showModal?.() ?? sheet.setAttribute('open', '');
  }

  element('jobs-pass').addEventListener('click', () => { element('jobs-pass-confirm').hidden = false; });
  element('jobs-pass-no').addEventListener('click', () => { element('jobs-pass-confirm').hidden = true; });
  element('jobs-pass-yes').addEventListener('click', async () => {
    element('jobs-pass-confirm').hidden = true;
    element('jobs-pass').disabled = true;
    const told = element('jobs-said');
    told.textContent = 'Running a pass…';
    told.classList.remove('refused');
    const answer = await send('POST', '/api/admin/jobs/tend');
    element('jobs-pass').disabled = false;
    page = 1;
    await redraw();
    const outcome = passSaid(answer);
    told.textContent = outcome.line;
    told.classList.toggle('refused', outcome.refused);
  });
  element('jobs-previous').addEventListener('click', () => { page -= 1; void redraw(); });
  element('jobs-next').addEventListener('click', () => { page += 1; void redraw(); });
  element('run-sheet-close').addEventListener('click', () => element('run-sheet').close?.());

  return { redraw };
}
