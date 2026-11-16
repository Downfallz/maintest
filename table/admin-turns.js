// Headquarters' Jobs tab, the bots' turns (docs/tabletop/admin-plan.md, A3b): the three queues a bot's turn goes
// through, how many items each holds and how old its oldest is, and the dead letters -- turns given up on after five
// failed deliveries -- each requeued or dropped. The host reads and acts on the queues; this page draws them.

import { ago } from './admin-players.js';

const QUEUE_LABELS = [
  [/-dlq$/, 'Dead letters'],
  [/-poison$/, 'Given up on, being set aside'],
  [/.*/, 'Turns owed'],
];

/** What a queue is, in words, from its name. */
export function queueLabel(name) {
  return QUEUE_LABELS.find(([pattern]) => pattern.test(String(name ?? '')))[1];
}

/** One queue as the tab shows it: what it is, its name, how many items, and how long its oldest has waited. */
export function queueRows(answer, now = Date.now()) {
  return (answer?.queues ?? []).map(queue => ({
    name: queue.name,
    label: queueLabel(queue.name),
    count: queue.count ?? 0,
    oldest: queue.count ? ago(queue.oldest, now) : '',
    warn: /-(dlq|poison)$/.test(queue.name ?? '') && (queue.count ?? 0) > 0,
  }));
}

/** One dead letter as the list shows it, from the host's answer alone. */
export function letterRows(answer, now = Date.now()) {
  return (answer?.letters ?? []).map(letter => ({
    id: letter.id,
    table: letter.table ?? 'not a turn',
    delivered: Number.isInteger(letter.delivered) ? `${letter.delivered} deliveries` : '',
    when: ago(letter.deadLetteredAt, now),
    error: letter.error ?? '',
    fullError: letter.fullError ?? letter.error ?? '',
    host: letter.host ?? '',
    session: letter.table ? `/session/${encodeURIComponent(letter.table)}` : null,
  }));
}

/** What is said under the list when the dead letters are more than a queue lets anybody see at once. */
export function moreLine(answer) {
  const letters = answer?.letters?.length ?? 0;
  const held = (answer?.queues ?? []).find(queue => (queue.name ?? '').endsWith('-dlq'))?.count ?? letters;
  return held > letters ? `Showing the first ${letters} of ${held}. Requeue or drop these to see the rest.` : '';
}

/** What the tab says after a requeue or a drop. A refusal keeps the host's own sentence. */
export function handledSaid(action, answer) {
  if (!answer?.ok) return { refused: true, line: answer?.body?.message ?? `The host refused it (${answer?.status}).` };
  const handled = answer.body?.handled?.length ?? 0;
  const missing = answer.body?.missing?.length ?? 0;
  const done = action === 'drop' ? 'dropped' : 'requeued';
  const letters = `${handled} letter${handled === 1 ? '' : 's'} ${done}`;
  return { refused: false, line: missing ? `${letters}; ${missing} no longer there.` : `${letters}.` };
}

// ---- The section itself. Everything above is pure and tested; this part only moves it onto the screen.

function made(tag, className, text) {
  const one = document.createElement(tag);
  if (className) one.className = className;
  if (text !== undefined) one.textContent = text;
  return one;
}

/**
 * Draws the bots' turns into the Jobs tab. `send(method, path, body)` is the admin transport's; `shown(available)`
 * says whether this host's turns go through queues at all; `warn(bool)` whether something waits on the operator.
 */
export function mountTurns({ send, element, shown, warn = () => {} }) {
  let rows = [];

  async function redraw() {
    const answer = await send('GET', '/api/admin/jobs/turns');
    if (answer.status === 404) {
      shown(false);
      return;
    }
    shown(true);
    if (!answer.ok) {
      say(answer.body?.message ?? `The host answered ${answer.status}.`, true);
      return;
    }
    const queues = queueRows(answer.body);
    element('turns-queues').replaceChildren(...queues.map(queue => {
      const item = made('li', `turn-queue${queue.warn ? ' warning' : ''}`);
      item.append(made('strong', 'turn-queue-count', String(queue.count)), made('span', '', queue.label),
        made('span', 'muted', queue.oldest ? `oldest ${queue.oldest}` : queue.name));
      return item;
    }));
    rows = letterRows(answer.body);
    element('turns-letters').replaceChildren(...rows.map(letterRow));
    element('turns-empty').hidden = rows.length > 0;
    const more = moreLine(answer.body);
    element('turns-more').textContent = more;
    element('turns-more').hidden = !more;
    element('turns-requeue-all').disabled = rows.length === 0;
    element('turns-all').checked = false;
    arm();
    warn(queues.some(queue => queue.warn));
  }

  function letterRow(row) {
    const item = made('li', 'session-row letter-row');
    const check = made('input');
    check.type = 'checkbox';
    check.value = row.id;
    check.setAttribute('aria-label', `Select the letter for ${row.table}`);
    const body = made('div');
    const open = made('button', 'player-open', row.table);
    open.type = 'button';
    open.addEventListener('click', () => openSheet(row));
    body.append(open, made('div', 'muted', [row.when, row.delivered, row.host].filter(Boolean).join(' · ')), made('div', 'run-error', row.error));
    item.append(check, body);
    return item;
  }

  function openSheet(row) {
    element('letter-sheet-name').textContent = row.table;
    element('letter-facts').replaceChildren(
      made('dt', '', 'Set aside'), made('dd', '', row.when),
      made('dt', '', 'Delivered'), made('dd', '', row.delivered || '—'),
      made('dt', '', 'By'), made('dd', '', row.host || '—'),
    );
    const link = element('letter-session');
    link.hidden = !row.session;
    if (row.session) link.href = row.session;
    element('letter-error').textContent = row.fullError;
    element('letter-requeue').dataset.id = row.id;
    const sheet = element('letter-sheet');
    if (!sheet.open) sheet.showModal?.() ?? sheet.setAttribute('open', '');
  }

  function selected() {
    return [...element('turns-letters').querySelectorAll('input[type=checkbox]:checked')].map(box => box.value);
  }

  function arm() {
    const count = selected().length;
    element('turns-requeue').disabled = count === 0;
    element('turns-drop').disabled = count === 0;
  }

  function say(line, refused) {
    const told = element('turns-said');
    told.textContent = line;
    told.classList.toggle('refused', refused);
  }

  async function act(action, body) {
    const answer = await send('POST', `/api/admin/jobs/turns/${action}`, body);
    await redraw();
    const outcome = handledSaid(action, answer);
    say(outcome.line, outcome.refused);
  }

  element('turns-letters').addEventListener('change', arm);
  element('turns-all').addEventListener('change', () => {
    for (const box of element('turns-letters').querySelectorAll('input[type=checkbox]')) box.checked = element('turns-all').checked;
    arm();
  });
  element('turns-requeue').addEventListener('click', () => void act('requeue', { ids: selected() }));
  element('turns-requeue-all').addEventListener('click', () => void act('requeue', { all: true }));
  element('turns-drop').addEventListener('click', () => {
    const count = selected().length;
    element('turns-confirm-line').textContent = `Drop ${count} letter${count === 1 ? '' : 's'}? Their turns are not requeued: each table waits for its next move to ask again.`;
    element('turns-confirm').hidden = false;
  });
  element('turns-drop-no').addEventListener('click', () => { element('turns-confirm').hidden = true; });
  element('turns-drop-yes').addEventListener('click', () => {
    element('turns-confirm').hidden = true;
    void act('drop', { ids: selected() });
  });
  element('turns-refresh').addEventListener('click', () => void redraw());
  element('letter-requeue').addEventListener('click', () => {
    element('letter-sheet').close?.();
    void act('requeue', { ids: [element('letter-requeue').dataset.id] });
  });
  element('letter-sheet-close').addEventListener('click', () => element('letter-sheet').close?.());

  return { redraw };
}
