// Headquarters' Jobs tab, the notices (docs/tabletop/notifications-plan.md, N6): the three queues a notice goes
// through, how many items each holds and how old its oldest is, and what the notices worker sent, had refused and
// found gone, a kind each, today and over the last seven days, whichever process sent them. The host counts; this
// page draws the counts.

import { ago } from './admin-players.js';

const QUEUE_LABELS = [
  [/-dlq$/, 'Given up on, kept a week'],
  [/-poison$/, 'Given up on, being set aside'],
  [/.*/, 'Waiting to be sent'],
];

// The push topic a notice is sent under, in the words of Settings, Notifications, What pops up.
const KIND_LABELS = { turn: 'Your turn', invitation: 'Invitations', friend: 'Friends', match: 'Results' };

/** What a notices queue is, in words, from its name. */
export function noticeQueueLabel(name) {
  return QUEUE_LABELS.find(([pattern]) => pattern.test(String(name ?? '')))[1];
}

/** One queue as the section shows it; one holding notices given up on is a warning. */
export function noticeQueueRows(answer, now = Date.now()) {
  return (answer?.queues ?? []).map(queue => ({
    name: queue.name,
    label: noticeQueueLabel(queue.name),
    count: queue.count ?? 0,
    oldest: queue.count ? ago(queue.oldest, now) : '',
    warn: /-(dlq|poison)$/.test(queue.name ?? '') && (queue.count ?? 0) > 0,
  }));
}

/** A kind of notice in words: one this page does not know keeps the host's name for it. */
export function kindLabel(kind) {
  return KIND_LABELS[kind] ?? String(kind ?? '');
}

const number = value => (Number.isFinite(value) && value > 0 ? value : 0);

/** The counts as the table shows them: a row a kind, today beside the week, and a last row of totals. */
export function countRows(answer) {
  const kinds = new Map();
  const row = kind => kinds.get(kind) ?? kinds.set(kind, { kind, label: kindLabel(kind), day: { sent: 0, refused: 0, gone: 0 }, week: { sent: 0, refused: 0, gone: 0 } }).get(kind);
  for (const span of ['day', 'week']) {
    for (const count of answer?.[span] ?? []) {
      if (!count?.kind) continue;
      row(count.kind)[span] = { sent: number(count.sent), refused: number(count.refused), gone: number(count.gone) };
    }
  }
  const rows = [...kinds.values()].sort((a, b) => a.label.localeCompare(b.label));
  if (rows.length === 0) return rows;
  const total = span => ['sent', 'refused', 'gone'].reduce((sum, key) => ({ ...sum, [key]: rows.reduce((all, each) => all + each[span][key], 0) }), {});
  return [...rows, { kind: null, label: 'All', day: total('day'), week: total('week'), total: true }];
}

/** The line above the table: what is counted, or why nothing is. */
export function countedLine(answer) {
  if (!answer?.counted) return 'This host could not open its counts, so its notices are sent and not counted. Its console says why.';
  const days = answer.days ?? 7;
  if ((answer.week ?? []).length === 0) return `Nothing was sent in the last ${days} days.`;
  return `Today (UTC, since midnight) and the last ${days} days. Refused counts every try a push service refused or did not answer; gone, a notice for a device that no longer takes them.`;
}

// ---- The section itself. Everything above is pure and tested; this part only moves it onto the screen.

function made(tag, className, text) {
  const one = document.createElement(tag);
  if (className) one.className = className;
  if (text !== undefined) one.textContent = text;
  return one;
}

/**
 * Draws the notices into the Jobs tab. `send(method, path)` is the admin transport's; `shown(available)` says whether
 * this host sends its notices from a queue at all; `warn(bool)` whether a notice given up on waits on the operator.
 */
export function mountNotices({ send, element, shown, warn = () => {} }) {
  async function redraw() {
    const answer = await send('GET', '/api/admin/jobs/notices');
    if (answer.status === 404) {
      shown(false);
      return;
    }
    shown(true);
    const said = element('notices-said');
    said.textContent = answer.ok ? '' : answer.body?.message ?? `The host answered ${answer.status}.`;
    said.classList.toggle('refused', !answer.ok);
    if (!answer.ok) return;
    const queues = noticeQueueRows(answer.body);
    element('notices-queues').replaceChildren(...queues.map(queue => {
      const item = made('li', `turn-queue${queue.warn ? ' warning' : ''}`);
      item.append(made('strong', 'turn-queue-count', String(queue.count)), made('span', '', queue.label),
        made('span', 'muted', queue.oldest ? `oldest ${queue.oldest}` : queue.name));
      return item;
    }));
    element('notices-counted').textContent = countedLine(answer.body);
    const rows = countRows(answer.body);
    element('notices-table').hidden = rows.length === 0;
    element('notices-counts').replaceChildren(...rows.map(row => {
      const line = made('tr', row.total ? 'notice-total' : '');
      const name = made('th', '', row.label);
      name.scope = 'row';
      line.append(name);
      for (const span of ['day', 'week']) {
        for (const key of ['sent', 'refused', 'gone']) {
          line.append(made('td', `notice-count${key === 'refused' && row[span][key] > 0 ? ' warning' : ''}`, String(row[span][key])));
        }
      }
      return line;
    }));
    warn(queues.some(queue => queue.warn));
  }

  element('notices-refresh').addEventListener('click', () => void redraw());
  return { redraw };
}
