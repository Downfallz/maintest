// Headquarters' Activity tab (docs/tabletop/admin-plan.md, A6): what operators did from the panel, newest first,
// twenty a page, from the operator log the host keeps (the last thousand actions). The host pages; this page draws.

import { ago } from './admin-players.js';

/** What each action the host notes is called here, by its stable name. */
export const ACTION_LABELS = Object.freeze({
  'table.open': 'Opened a table',
  'table.close': 'Closed a table',
  'table.reinvite': 'Gave a seat a new invitation',
  'table.void': 'Voided a match',
  'sessions.delete': 'Deleted a session',
  'sessions.export': 'Exported a session',
  'player.sign-out': 'Signed a player out everywhere',
  'player.rename': 'Renamed a player',
  'player.delete': 'Deleted an account',
  'player.suspend': 'Suspended an account',
  'player.restore': 'Restored an account',
  'jobs.tend': 'Ran a tending pass',
  'turns.requeue': 'Requeued dead letters',
  'turns.drop': 'Dropped dead letters',
  'settings.save': 'Saved the settings',
});

const TABLE_ACTIONS = new Set(['table.open', 'table.close', 'table.reinvite', 'table.void', 'sessions.delete', 'sessions.export']);

/** The address of a page of the log, the page left out when it is the first. */
export function activityPath(page = 1) {
  return Number.isInteger(page) && page > 1 ? `/api/admin/activity?page=${page}` : '/api/admin/activity';
}

/** One action as a row of the list: when, who, what, on what, and the way to its session when it was a table's. */
export function activityRows(answer, now = Date.now()) {
  return (answer?.actions ?? []).map(action => ({
    id: action.id,
    when: ago(action.at, now),
    at: action.at ? new Date(action.at).toISOString().replace('T', ' ').slice(0, 19) + ' UTC' : '',
    by: action.by ?? 'someone',
    what: ACTION_LABELS[action.action] ?? action.action,
    target: action.target ?? '',
    detail: action.detail ?? '',
    session: TABLE_ACTIONS.has(action.action) && action.target && action.action !== 'sessions.delete' ? `/session/${encodeURIComponent(action.target)}` : null,
  }));
}

/** What the pager under the list says. */
export function activityLine(answer) {
  const total = answer?.total ?? 0;
  return `${total} action${total === 1 ? '' : 's'} kept (the last ${answer?.kept ?? 1000}) · page ${answer?.page ?? 1} of ${answer?.pages ?? 1}`;
}

// ---- The tab itself. Everything above is pure and tested; this part only moves it onto the screen.

function made(tag, className, text) {
  const one = document.createElement(tag);
  if (className) one.className = className;
  if (text !== undefined) one.textContent = text;
  return one;
}

/**
 * Draws the Activity tab. `send(method, path)` is the admin transport's; `shown(available)` says whether this host
 * keeps an operator log, which a host recording nothing does not.
 */
export function mountActivity({ send, element, shown }) {
  let page = 1;

  async function redraw() {
    const answer = await send('GET', activityPath(page));
    if (answer.status === 404) {
      shown(false);
      return;
    }
    shown(true);
    const told = element('activity-said');
    if (!answer.ok) {
      told.textContent = answer.body?.message ?? `The host answered ${answer.status}.`;
      told.classList.add('refused');
      return;
    }
    told.textContent = '';
    told.classList.remove('refused');
    page = answer.body.page ?? 1;
    const rows = activityRows(answer.body);
    element('activity').replaceChildren(...rows.map(row => {
      const item = made('li', 'session-row activity-row');
      const body = made('div');
      const what = made('div', 'activity-what');
      what.append(made('strong', '', row.what));
      if (row.target) {
        what.append(' · ');
        if (row.session) {
          const link = made('a', '', row.target);
          link.href = row.session;
          what.append(link);
        } else {
          what.append(made('span', 'activity-target', row.target));
        }
      }
      const when = made('div', 'muted', `${row.when} · ${row.by}`);
      when.title = row.at;
      body.append(what, when);
      if (row.detail) body.append(made('div', 'activity-detail', row.detail));
      item.append(body);
      return item;
    }));
    element('activity-empty').hidden = rows.length > 0;
    element('activity-count').textContent = activityLine(answer.body);
    element('activity-previous').disabled = page <= 1;
    element('activity-next').disabled = page >= (answer.body.pages ?? 1);
  }

  element('activity-previous').addEventListener('click', () => { page -= 1; void redraw(); });
  element('activity-next').addEventListener('click', () => { page += 1; void redraw(); });

  return { redraw };
}
