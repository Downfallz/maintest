// Headquarters' Feedback tab (docs/tabletop/feedback-plan.md, R3, ADR 0189): the bug reports and ideas players sent
// with Report or suggest, newest first, twenty a page, filtered by kind, status and category. A row opens its sheet:
// the player's words, what the page and the host added, the position drawn read-only, the status set by hand, a bug
// report turned into an idea or back, Diagnose… on a table still under way, and the sender muted or let through. An
// idea (I1) is merged into another, read with every idea merged into it and how many asked, planned with its plan's
// path, shipped or declined, and copied as a plan prompt in the operator's own words. The host keeps, guards and
// notes; this page asks and draws.

import { ago } from './admin-players.js';
import { BUG_CATEGORIES, IDEA_PLACES } from './feedback-sheet.js';
import { stepLabel } from './terms.js';

export const KIND_LABELS = Object.freeze({ bug: 'Bug report', idea: 'Idea' });

export const STATUS_LABELS = Object.freeze({
  new: 'New',
  seen: 'Seen',
  reproduced: 'Reproduced',
  filed: 'Filed',
  fixed: 'Fixed',
  dismissed: 'Dismissed',
  planned: 'Planned',
  shipped: 'Shipped',
  declined: 'Declined',
});

/** The reason that marks feedback as spam (Feedback.SpamReason), which counts against its sender. */
export const SPAM = 'spam';

/** What the status filter offers: what is still open first, which is where the tab opens. */
export const STATUS_FILTERS = Object.freeze([
  { value: 'open', label: 'Open' },
  { value: 'new', label: 'New' },
  { value: 'seen', label: 'Seen' },
  { value: 'reproduced', label: 'Reproduced' },
  { value: 'planned', label: 'Planned' },
  { value: 'shipped', label: 'Shipped' },
  { value: 'declined', label: 'Declined' },
  { value: 'dismissed', label: 'Dismissed' },
  { value: '', label: 'Every status' },
]);

/** A bug report's category or an idea's place as the player chose it, in the sheet's own words. */
export function categoryLabel(kind, category) {
  const offered = kind === 'idea' ? IDEA_PLACES : BUG_CATEGORIES;
  return offered.find(one => one.value === category)?.label ?? category ?? '';
}

/** What the category filter offers: both kinds' values, each once, with the kind they belong to when only one has it. */
export function categoryFilters() {
  const values = [...new Set([...BUG_CATEGORIES, ...IDEA_PLACES].map(one => one.value))];
  return [{ value: '', label: 'Every category' }, ...values.map(value => {
    const bug = BUG_CATEGORIES.find(one => one.value === value);
    const idea = IDEA_PLACES.find(one => one.value === value);
    if (bug && idea) return { value, label: value === 'other' ? 'Something else' : idea.label };
    return { value, label: bug ? `${bug.label} (bug)` : `${idea.label} (idea)` };
  })];
}

/** The address of a page of the list, each filter left out when it is every one. */
export function feedbackPath({ kind = '', status = 'open', category = '', page = 1 } = {}) {
  const query = new URLSearchParams();
  if (kind) query.set('kind', kind);
  if (status) query.set('status', status);
  if (category) query.set('category', category);
  if (Number.isInteger(page) && page > 1) query.set('page', String(page));
  const text = query.toString();
  return text ? `/api/admin/feedback?${text}` : '/api/admin/feedback';
}

/** Where feedback was sent from, as one line: a table and round, a practice and round, or nowhere in a game. */
export function placeLine(row) {
  const round = Number.isInteger(row?.round) ? `, round ${row.round}` : '';
  if (row?.table) return `Table ${row.table}${round}`;
  if (row?.scenario) return `Practice ${row.scenario}${round}`;
  return 'Outside a game';
}

/** One piece of feedback as a row of the list; a new bug report stands out. */
export function feedbackRows(answer, now = Date.now()) {
  return (answer?.feedback ?? []).map(row => ({
    id: row.id,
    kind: KIND_LABELS[row.kind] ?? row.kind,
    category: categoryLabel(row.kind, row.category),
    line: row.line ?? '',
    place: placeLine(row),
    status: statusLine(row),
    when: ago(row.at, now),
    more: row.additions > 0 ? `${row.additions} added later` : '',
    asked: Number.isInteger(row.askedBy) ? `asked by ${row.askedBy}` : '',
    warn: row.kind === 'bug' && row.status === 'new',
  }));
}

/** A status as the sheet and the list say it: with the operator's reason when dismissed or declined, with its plan when planned or shipped. */
export function statusLine(feedback) {
  const label = STATUS_LABELS[feedback?.status] ?? feedback?.status ?? '';
  if (['planned', 'shipped'].includes(feedback?.status) && feedback.plan) return `${label}: ${feedback.plan}`;
  if (!['dismissed', 'declined'].includes(feedback?.status) || !feedback.reason) return label;
  return feedback.status === 'dismissed' && feedback.spam === true ? 'Dismissed as spam' : `${label}: ${feedback.reason}`;
}

/** What the pager under the list says. */
export function feedbackLine(answer) {
  const total = answer?.total ?? 0;
  return `${total} piece${total === 1 ? '' : 's'} of feedback · page ${answer?.page ?? 1} of ${answer?.pages ?? 1}`;
}

const plural = (count, one, many = `${one}s`) => `${count} ${count === 1 ? one : many}`;

const day = at => (at ? new Date(at).toISOString().slice(0, 10) : '');

/** How many asked an idea, the ideas merged into it counted, and since the first of them asked. */
export function askedLine(askedBy, since) {
  const first = day(since);
  return first ? `Asked by ${plural(askedBy, 'player')} since ${first}` : `Asked by ${plural(askedBy, 'player')}`;
}

const stamp = at => (at ? new Date(at).toISOString().replace('T', ' ').slice(0, 16) + ' UTC' : '');

/** Who sent it, as the sheet says it: a guest, an account, or one deleted since. */
function senderLine(answer) {
  if (answer.senderGone) return 'An account since deleted';
  return answer.guest ? 'A guest, by their seat' : `Account ${answer.account}`;
}

/** What the host added about where it was sent from, each a label and a line. */
function contextFacts(context) {
  const where = [placeLine(context), context.seat, context.subPhase ? stepLabel(context.subPhase) : null,
    Number.isInteger(context.journalLength) ? plural(context.journalLength, 'journal entry', 'journal entries') : null].filter(Boolean);
  const facts = [['Where', where.join(' · ')]];
  const basis = [context.rules, context.content ? `content ${context.content.slice(0, 12)}` : null, context.engine ? `engine ${context.engine}` : null].filter(Boolean);
  if (basis.length) facts.push(['Played on', basis.join(' · ')]);
  if (Number.isInteger(context.practiceEntries)) facts.push(['Practice', `${plural(context.practiceEntries, 'entry', 'entries')} since the cut, copied`]);
  return facts;
}

/** The facts the sheet lists, each a label and a line, from `GET /api/admin/feedback/<id>`. */
export function sheetFacts(answer, now = Date.now()) {
  const facts = [['Sent', `${ago(answer.at, now)} · ${stamp(answer.at)}`], ['From', senderLine(answer)]];
  if (Number.isInteger(answer.askedBy)) facts.push(['Asked by', askedLine(answer.askedBy, answer.since).slice('Asked by '.length)]);
  if (answer.mergedInto) facts.push(['Merged into', answer.mergedInto.line]);
  if (answer.context) facts.push(...contextFacts(answer.context));
  if (answer.page) facts.push(['Page', answer.page]);
  if (answer.browser) facts.push(['Browser', answer.browser]);
  for (const error of answer.errors ?? []) facts.push(['Page error', error.where ? `${error.message} (${error.where})` : error.message]);
  if (answer.mute) facts.push(['Muted', muteLine(answer.mute)]);
  return facts;
}

/** The sender's mute as one line: how many days the host says are left, and whether the operator or the spam marks did it. */
export function muteLine(mute) {
  if (!mute) return '';
  const days = Math.max(1, mute.daysLeft ?? 1);
  const why = mute.why === SPAM ? 'three dismissed as spam in 30 days' : `by ${mute.by}`;
  return `This player can send no feedback for ${plural(days, 'more day')} (${why}).`;
}

/** The player's words: what happened or the idea, what they expected or wished, then what they added later. */
export function sheetWords(answer, now = Date.now()) {
  const bug = answer.kind === 'bug';
  const words = [{ label: bug ? 'What happened' : 'What they were trying to do', text: answer.text }];
  if (answer.expected) words.push({ label: bug ? 'What they expected' : 'How they would like it', text: answer.expected });
  for (const added of answer.additions ?? []) {
    words.push({ label: `Added ${ago(added.at, now)}`, text: added.expected ? `${added.text}\n${added.expected}` : added.text });
  }
  for (const ask of answer.asks ?? []) {
    const said = [ask.text, ask.expected, ...(ask.additions ?? []).flatMap(added => [added.text, added.expected])].filter(Boolean);
    words.push({ label: `Also asked ${ago(ask.at, now)}, by ${askerLine(ask)}`, text: said.join('\n') });
  }
  return words;
}

/** Who asked an idea merged into this one, inside a sentence. */
function askerLine(ask) {
  if (ask.senderGone) return 'an account since deleted';
  return ask.guest ? 'a guest' : `account ${ask.account}`;
}

/**
 * The plan prompt for `/new-plan`: the operator's words on one line, never the player's pasted unread, and how many
 * asked since when. None until the operator wrote something.
 */
export function planPrompt(words, answer) {
  const text = String(words ?? '').replace(/\s+/g, ' ').trim();
  if (!text) return null;
  const ended = /[.!?]$/.test(text) ? text : `${text}.`;
  return `/new-plan ${ended} ${askedLine(answer?.askedBy ?? 1, answer?.since ?? answer?.at)}.`;
}

/** The pages of open ideas the merge picker reads at most: twenty a page. */
export const CHOICE_PAGES = 5;

/** What the merge picker says under its list: that more ideas are open than it read, or that it could not read them. */
export function choicesNote(pages, read) {
  if (!read) return 'The open ideas could not be read. Close the sheet and open it again.';
  return pages > CHOICE_PAGES ? `Only the first ${CHOICE_PAGES * 20} open ideas are listed.` : '';
}

/** The ideas this one can be merged into, from the list's rows: every other idea, each with how many asked it. */
export function mergeChoices(rows, id) {
  return (rows ?? []).filter(row => row.kind === 'idea' && row.id !== id)
    .map(row => ({ value: row.id, label: Number.isInteger(row.askedBy) ? `${row.line} (asked by ${row.askedBy})` : row.line }));
}

const number = value => (typeof value === 'number' ? value : value?.value ?? '?');

/** One creature of the position as a line: its name, health, energy, and what holds it. */
export function creatureLine(creature) {
  const held = [creature.isStunned ? 'stunned' : null, ...(creature.conditions ?? []).map(condition => condition?.effect?.kind ?? 'condition')].filter(Boolean);
  const life = number(creature.health) <= 0 ? 'down' : `${number(creature.health)}/${number(creature.maxHealth)} health`;
  return [creature.name ?? 'A creature', life, `${number(creature.energy)} energy`, held.length ? held.join(', ') : null].filter(Boolean).join(' · ');
}

/** The position the player attached, as two read-only sides: theirs first. None when nothing was attached. */
export function positionSides(position, seat) {
  if (!position || typeof position !== 'object') return [];
  return [
    { label: seat ? `The reporter's side (${seat})` : 'The reporter\'s side', creatures: (position.allies ?? []).map(creatureLine) },
    { label: 'The other side', creatures: (position.enemies ?? []).map(creatureLine) },
  ];
}

/** The sheet's buttons, by the names the host offers them under (`actions`), each shown only when the host offers it. */
export const SHEET_ACTIONS = Object.freeze(['seen', 'reproduced', 'dismiss', 'spam', 'toIdea', 'toBug', 'diagnose', 'mute', 'unmute',
  'planned', 'shipped', 'declined', 'merge', 'prompt']);

/** Which of the sheet's buttons apply to this feedback as it stands: what the host says, and nothing else. */
export function sheetActions(answer) {
  const offered = new Set(Array.isArray(answer?.actions) ? answer.actions : []);
  return Object.fromEntries(SHEET_ACTIONS.map(name => [name, offered.has(name)]));
}

/** The words a sheet's change is told with. */
export function changeSaid(answer, done) {
  if (!answer.ok) return { refused: true, line: answer.body?.message ?? `The host answered ${answer.status}.` };
  return { refused: false, line: done };
}

// ---- The tab itself. Everything above is pure and tested; this part only moves it onto the screen.

function made(tag, className, text) {
  const one = document.createElement(tag);
  if (className) one.className = className;
  if (text !== undefined) one.textContent = text;
  return one;
}

function fill(select, options) {
  select.replaceChildren(...options.map(({ value, label }) => {
    const option = made('option', '', label);
    option.value = value;
    return option;
  }));
}

/**
 * Draws the Feedback tab and its sheet. `send(method, path, body)` is the admin transport's; `shown(available)` says
 * whether this host keeps feedback; `openDiagnosis(row)` opens the Diagnose sheet on a table (S5b).
 */
export function mountFeedbackTab({ send, element, shown, openDiagnosis }) {
  let page = 1;
  let open = null;
  // Each listing and each sheet read is numbered, so an answer that comes back after a later one is dropped.
  let listed = 0;
  let read = 0;

  fill(element('admin-feedback-kind'), [{ value: '', label: 'Every kind' }, { value: 'bug', label: 'Bug reports' }, { value: 'idea', label: 'Ideas' }]);
  fill(element('admin-feedback-status'), STATUS_FILTERS);
  fill(element('admin-feedback-category'), categoryFilters());

  const BUTTONS = { seen: 'admin-feedback-seen', reproduced: 'admin-feedback-reproduced', dismiss: 'admin-feedback-dismiss', spam: 'admin-feedback-spam',
    toIdea: 'admin-feedback-to-idea', toBug: 'admin-feedback-to-bug', diagnose: 'admin-feedback-diagnose', mute: 'admin-feedback-mute', unmute: 'admin-feedback-unmute',
    planned: 'admin-feedback-planned', shipped: 'admin-feedback-shipped', declined: 'admin-feedback-declined', merge: 'admin-feedback-merge', prompt: 'admin-feedback-prompt' };

  // The blocks an idea's actions sit in, each shown while one of its buttons is.
  const BLOCKS = { 'admin-feedback-dismissing': ['dismiss'], 'admin-feedback-planning': ['planned', 'shipped'], 'admin-feedback-declining': ['declined'],
    'admin-feedback-merging': ['merge'], 'admin-feedback-prompting': ['prompt'] };

  const filters = () => ({ kind: element('admin-feedback-kind').value, status: element('admin-feedback-status').value, category: element('admin-feedback-category').value, page });

  async function redraw() {
    const asked = ++listed;
    const answer = await send('GET', feedbackPath(filters()));
    if (asked !== listed) return;
    if (answer.status === 404 && !answer.body?.error) {
      shown(false);
      return;
    }
    shown(true);
    const told = element('admin-feedback-said');
    if (!answer.ok) {
      told.textContent = answer.body?.message ?? `The host answered ${answer.status}.`;
      told.classList.add('refused');
      return;
    }
    told.textContent = '';
    told.classList.remove('refused');
    page = answer.body.page ?? 1;
    const rows = feedbackRows(answer.body);
    element('admin-feedback-list').replaceChildren(...rows.map(row => {
      const item = made('li', `session-row feedback-row${row.warn ? ' warning' : ''}`);
      const button = made('button', 'feedback-open');
      button.type = 'button';
      button.dataset.feedback = row.id;
      button.append(made('strong', '', `${row.kind} · ${row.category}`), made('span', 'feedback-line', row.line),
        made('span', 'muted', [row.place, row.when, row.status, row.asked, row.more].filter(Boolean).join(' · ')));
      button.addEventListener('click', () => void openSheet(row.id));
      item.append(button);
      return item;
    }));
    element('admin-feedback-empty').hidden = rows.length > 0;
    element('admin-feedback-count').textContent = feedbackLine(answer.body);
    element('admin-feedback-previous').disabled = page <= 1;
    element('admin-feedback-next').disabled = page >= (answer.body.pages ?? 1);
  }

  function said(line, refused = false) {
    element('admin-feedback-sheet-said').textContent = line;
    element('admin-feedback-sheet-said').classList.toggle('refused', refused);
  }

  function drawSheet(answer) {
    open = answer;
    element('admin-feedback-sheet-kind').textContent = `${KIND_LABELS[answer.kind] ?? answer.kind} · ${statusLine(answer)}`;
    element('admin-feedback-sheet-name').textContent = categoryLabel(answer.kind, answer.category);
    element('admin-feedback-words').replaceChildren(...sheetWords(answer).flatMap(({ label, text }) => [made('dt', '', label), made('dd', 'feedback-text', text)]));
    element('admin-feedback-facts').replaceChildren(...sheetFacts(answer).flatMap(([label, line]) => [made('dt', '', label), made('dd', '', line)]));
    const sides = positionSides(answer.context?.position, answer.context?.seat);
    element('admin-feedback-position-block').hidden = sides.length === 0;
    element('admin-feedback-position').replaceChildren(...sides.map(side => {
      const block = made('section', 'feedback-side');
      block.append(made('h4', '', side.label));
      const list = made('ul');
      list.append(...side.creatures.map(line => made('li', '', line)));
      block.append(list);
      return block;
    }));
    const session = element('admin-feedback-session');
    session.hidden = !answer.session;
    if (answer.session) session.href = answer.session;
    element('admin-feedback-joined').hidden = !answer.mergedInto;
    const can = sheetActions(answer);
    drawActions(can);
    if (can.merge) void listChoices(answer.id);
  }

  function drawActions(can) {
    for (const name of SHEET_ACTIONS) element(BUTTONS[name]).hidden = !can[name];
    for (const [block, names] of Object.entries(BLOCKS)) element(block).hidden = !names.some(name => can[name]);
  }

  // The ideas this one can join: every open idea, CHOICE_PAGES pages at most, read when its sheet offers the merge.
  async function listChoices(id) {
    const asked = read;
    const select = element('admin-feedback-merge-into');
    fill(select, [{ value: '', label: 'Reading the open ideas…' }]);
    element('admin-feedback-merge-note').textContent = '';
    const rows = [];
    let pages = 1;
    let readAll = true;
    for (let number = 1; number <= Math.min(pages, CHOICE_PAGES); number += 1) {
      const answer = await send('GET', feedbackPath({ kind: 'idea', status: 'open', page: number }));
      if (asked !== read) return;
      if (!answer.ok) {
        readAll = false;
        break;
      }
      rows.push(...(answer.body.feedback ?? []));
      pages = answer.body.pages ?? 1;
    }
    const choices = mergeChoices(rows, id);
    fill(select, choices.length ? [{ value: '', label: 'Pick an idea' }, ...choices] : [{ value: '', label: 'No other open idea' }]);
    element('admin-feedback-merge-note').textContent = choicesNote(pages, readAll);
  }

  // Emptied before the host answers, so nothing of the sheet opened before can be read or acted on meanwhile.
  function emptySheet() {
    open = null;
    element('admin-feedback-sheet-kind').textContent = 'Feedback';
    element('admin-feedback-sheet-name').textContent = '';
    for (const id of ['admin-feedback-words', 'admin-feedback-facts', 'admin-feedback-position', 'admin-feedback-merge-into']) element(id).replaceChildren();
    element('admin-feedback-position-block').hidden = true;
    element('admin-feedback-session').hidden = true;
    element('admin-feedback-joined').hidden = true;
    element('admin-feedback-prompt-said').textContent = '';
    element('admin-feedback-merge-note').textContent = '';
    drawActions(sheetActions(null));
  }

  // A button the change hid takes the focus with it: it moves to what the sheet says, so the place is not lost.
  function keepFocus() {
    const focused = document.activeElement;
    if (!focused || focused === document.body || focused.hidden || focused.closest('[hidden]')) element('admin-feedback-sheet-said').focus();
  }

  async function openSheet(id) {
    const asked = ++read;
    emptySheet();
    said('Reading the feedback…');
    const sheet = element('admin-feedback-sheet');
    if (!sheet.open) sheet.showModal?.() ?? sheet.setAttribute('open', '');
    const answer = await send('GET', `/api/admin/feedback/${encodeURIComponent(id)}`);
    if (asked !== read) return;
    if (!answer.ok) {
      said(answer.body?.message ?? `The host answered ${answer.status}.`, true);
      return;
    }
    for (const field of ['admin-feedback-reason', 'admin-feedback-decline-reason', 'admin-feedback-prompt-text']) element(field).value = '';
    element('admin-feedback-plan').value = answer.body.plan ?? '';
    drawSheet(answer.body);
    said('');
  }

  async function change(method, suffix, body, done) {
    if (!open) return;
    // The sheet this change was asked from: an answer that comes back once another was opened draws nothing on it.
    const from = read;
    const answer = await send(method, `/api/admin/feedback/${encodeURIComponent(open.id)}/${suffix}`, body);
    if (from !== read) {
      await redraw();
      return;
    }
    const outcome = changeSaid(answer, done);
    if (answer.ok) drawSheet(answer.body);
    said(outcome.line, outcome.refused);
    keepFocus();
    await redraw();
  }

  element('admin-feedback-close').addEventListener('click', () => element('admin-feedback-sheet').close?.());
  element('admin-feedback-seen').addEventListener('click', () => void change('PUT', 'status', { status: 'seen' }, 'Marked seen.'));
  element('admin-feedback-reproduced').addEventListener('click', () => void change('PUT', 'status', { status: 'reproduced' }, 'Marked reproduced.'));
  element('admin-feedback-dismiss').addEventListener('click', () => {
    const reason = element('admin-feedback-reason').value.trim();
    if (!reason) {
      said('Give a reason for dismissing it. It is kept with the feedback.', true);
      return;
    }
    void change('PUT', 'status', { status: 'dismissed', reason }, 'Dismissed.');
  });
  element('admin-feedback-spam').addEventListener('click', () => {
    if (!globalThis.confirm?.('Dismiss this as spam? A third spam dismissal within 30 days stops its sender from sending feedback for 30 days.')) return;
    void change('PUT', 'status', { status: 'dismissed', reason: SPAM }, 'Dismissed as spam.');
  });
  element('admin-feedback-to-idea').addEventListener('click', () => void change('PUT', 'kind', { kind: 'idea' }, 'Now an idea. Its words and position are kept.'));
  element('admin-feedback-to-bug').addEventListener('click', () => void change('PUT', 'kind', { kind: 'bug' }, 'Now a bug report. Its words are kept.'));
  element('admin-feedback-mute').addEventListener('click', () => {
    if (!globalThis.confirm?.('Stop feedback from this player for 30 days? The host will refuse what they send.')) return;
    void change('POST', 'mute', undefined, 'This player can send no feedback for 30 days.');
  });
  element('admin-feedback-unmute').addEventListener('click', () => void change('DELETE', 'mute', undefined, 'This player can send feedback again.'));
  element('admin-feedback-planned').addEventListener('click', () => {
    const plan = element('admin-feedback-plan').value.trim();
    if (!plan) {
      said('Name the plan\'s path in the repository, such as docs/tabletop/replay-plan.md.', true);
      return;
    }
    void change('PUT', 'status', { status: 'planned', plan }, `Planned in ${plan}.`);
  });
  element('admin-feedback-shipped').addEventListener('click', () => void change('PUT', 'status', { status: 'shipped' }, 'Marked shipped.'));
  element('admin-feedback-declined').addEventListener('click', () => {
    const reason = element('admin-feedback-decline-reason').value.trim();
    if (!reason) {
      said('Give a reason for declining it. The players who asked will read it.', true);
      return;
    }
    void change('PUT', 'status', { status: 'declined', reason }, 'Declined.');
  });
  element('admin-feedback-merge').addEventListener('click', () => {
    const select = element('admin-feedback-merge-into');
    if (!select.value) {
      said('Pick the idea to merge this one into.', true);
      return;
    }
    const label = select.selectedOptions?.[0]?.textContent ?? select.value;
    if (!globalThis.confirm?.(`Merge this idea into "${label}"? It leaves the list and is read inside that one, its player counted.`)) return;
    void change('POST', 'merge', { into: select.value }, 'Merged. It is read inside the idea it joined.');
  });
  element('admin-feedback-joined').addEventListener('click', () => {
    if (open?.mergedInto?.id) void openSheet(open.mergedInto.id);
  });
  element('admin-feedback-prompt').addEventListener('click', async () => {
    const prompt = planPrompt(element('admin-feedback-prompt-text').value, open);
    if (!prompt) {
      said('Write the idea in your own words first: the prompt never carries a player\'s words unread.', true);
      return;
    }
    element('admin-feedback-prompt-said').textContent = prompt;
    try {
      await navigator.clipboard.writeText(prompt);
      said('The plan prompt is copied. Paste it into a new session.');
    } catch {
      said('The browser did not let the page copy it: select the prompt below and copy it.', true);
    }
  });
  element('admin-feedback-diagnose').addEventListener('click', () => {
    if (!open?.context?.table) return;
    element('admin-feedback-sheet').close?.();
    openDiagnosis?.({ id: open.context.table, players: '' });
  });
  for (const id of ['admin-feedback-kind', 'admin-feedback-status', 'admin-feedback-category']) {
    element(id).addEventListener('change', () => { page = 1; void redraw(); });
  }
  element('admin-feedback-previous').addEventListener('click', () => { page -= 1; void redraw(); });
  element('admin-feedback-next').addEventListener('click', () => { page += 1; void redraw(); });

  return { redraw, open: openSheet };
}
