import { createBattleHistory, historyEntry, KIND_LABELS } from '../../table/battle-history.js';

type Model = ReturnType<ReturnType<typeof createBattleHistory>['model']>;
type Row = NonNullable<ReturnType<typeof historyEntry>> & { status?: string };
const el = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
function textNode(tag: string, text: string, className = '') {
  const node = document.createElement(tag); node.textContent = text; node.className = className; return node;
}

export function historyControls(controller: ReturnType<typeof createBattleHistory>, beforeOpen: () => void, onVisibility: () => void) {
  const dialog = el<HTMLDialogElement>('history-dialog'), list = el('history-list');
  const entries = new Map<number, HTMLElement>();
  let opener: HTMLElement | null = null, active = false, key = '', reviewKey = '';
  const detail = el('history-detail');
  const launchers = Array.from(document.querySelectorAll<HTMLButtonElement>('[data-history-open]'));
  const close = () => dialog.close();
  function open(event: Event) {
    if (!active || dialog.open) return;
    opener = event.currentTarget as HTMLElement;
    beforeOpen(); controller.open(); dialog.showModal(); el('history-new').hidden = true;
    // Initial opening follows the latest entry; incoming entries never move a reader browsing above it.
    list.scrollTop = list.scrollHeight; onVisibility();
  }
  launchers.forEach(button => button.addEventListener('click', open));
  el('history-close').addEventListener('click', close);
  dialog.addEventListener('close', () => {
    controller.close(); onVisibility();
    if (active && opener?.isConnected && !opener.matches(':disabled') && opener.getClientRects().length) opener.focus({ preventScroll: true });
    opener = null;
  });
  const latest = () => { controller.latest(); list.scrollTop = list.scrollHeight; el('history-new').hidden = true; };
  el('history-new').addEventListener('click', latest); el('history-latest').addEventListener('click', latest);
  el('history-earlier').addEventListener('click', () => { void controller.earlier(); });
  // Back returns the reader to the row they inspected, not to the top of the list.
  el('history-back').addEventListener('click', () => {
    const inspected = controller.model().review?.row.sequence;
    controller.select(null);
    const row = inspected == null ? null : entries.get(inspected)?.querySelector<HTMLElement>('summary');
    (row ?? el('history-close')).focus({ preventScroll: true });
  });
  el('history-previous').addEventListener('click', () => controller.move(-1));
  el('history-next').addEventListener('click', () => controller.move(1));
  for (const stage of ['before', 'after']) el(`history-${stage}`).addEventListener('click', () => controller.boundary(stage));
  for (const key of ['round', 'creature', 'team', 'kind']) el(`history-filter-${key}`).addEventListener('change', event => controller.filter(key, (event.target as HTMLSelectElement).value));

  // A row with nothing under its title is a marker (a round or a phase beginning): it is read in place rather
  // than offering a disclosure that opens on nothing.
  function entryNode(row: Row) {
    const heading = [textNode('span', `Round ${row.round || '—'} · ${row.category}`, 'history-meta'), textNode('strong', row.title)];
    const node = document.createElement(row.details.length ? 'details' : 'div');
    node.className = row.details.length ? 'history-entry' : 'history-entry history-marker';
    node.dataset.sequence = String(row.sequence);
    if (!row.details.length) { node.append(...heading); return node; }
    const summary = document.createElement('summary');
    summary.append(...heading, ...(row.status ? [textNode('span', row.status, 'history-status')] : []));
    const details = document.createElement('ul'); details.className = 'history-effects';
    for (const effect of row.details) {
      const item = textNode('li', effect.text); item.dataset.tone = effect.tone; details.append(item);
    }
    const inspect = document.createElement('button'); inspect.type = 'button'; inspect.className = 'history-inspect'; inspect.textContent = 'Inspect event';
    inspect.addEventListener('click', () => { controller.select(row.sequence); el('history-back').focus({ preventScroll: true }); });
    node.append(summary, details, inspect); return node;
  }

  function options(id: string, values: [string, string][], value: string) {
    if (value && !values.some(one => one[0] === value)) values.push([value, `Not in this page (${value})`]);
    const select = el<HTMLSelectElement>(id), key = JSON.stringify(values);
    if (select.dataset.options !== key) {
      select.replaceChildren(...values.map(([value, label]) => { const option = document.createElement('option'); option.value = value; option.textContent = label; return option; }));
      select.dataset.options = key;
    }
    select.value = value;
  }
  function filters(model: Model) {
    const all = model.all;
    options('history-filter-round', [['', 'All loaded rounds'], ...[...new Set(all.map(row => row.round))].map(round => [String(round), `Round ${round}`] as [string, string])], model.filters.round);
    const people = new Map(all.flatMap(row => row.participants).map(one => [one.id, `${one.label} · ${one.side}`]));
    options('history-filter-creature', [['', 'All creatures'], ...people.entries()], model.filters.creature);
    const kinds = new Map(all.map(row => [row.kind, (KIND_LABELS as Record<string, string>)[row.kind] ?? row.category]));
    options('history-filter-kind', [['', 'All event kinds'], ...kinds.entries()], model.filters.kind);
    el<HTMLSelectElement>('history-filter-team').value = model.filters.team;
  }
  function review(model: Model) {
    const selected = model.review;
    dialog.dataset.review = String(Boolean(selected)); el('history-review').hidden = !selected;
    if (!selected) { detail.replaceChildren(); reviewKey = ''; return; }
    el<HTMLButtonElement>('history-previous').disabled = !selected.previous;
    el<HTMLButtonElement>('history-next').disabled = !selected.next;
    for (const stage of ['before', 'after']) {
      el<HTMLButtonElement>(`history-${stage}`).disabled = !selected.row.states;
      el(`history-${stage}`).setAttribute('aria-pressed', String(stage === selected.stage));
    }
    const key = `${selected.row.sequence}/${selected.stage}`;
    if (key === reviewKey) return;
    reviewKey = key; const row = selected.row;
    detail.replaceChildren(textNode('p', `Round ${row.round} · ${row.category} · event ${row.sequence} · ${selected.stage.toUpperCase()}`, 'history-meta'), textNode('h3', row.title));
    for (const section of row.sections) {
      if (!section.items.length) continue;
      const facts = document.createElement('ul'); facts.className = 'history-effects';
      facts.append(...section.items.map((one: { text: string; tone: string }) => { const item = textNode('li', one.text); item.dataset.tone = one.tone; return item; }));
      detail.append(textNode('h4', section.title), facts);
    }
    const boundary = row.states?.[selected.stage as 'before' | 'after'];
    detail.append(textNode('h4', boundary ? `Recorded ${selected.stage} this event` : 'Historical battlefield unavailable'));
    if (!boundary) { detail.append(textNode('p', 'No exact before/after snapshot was recorded. The facts above remain available; current stats are never substituted.')); return; }
    const board = textNode('div', '', 'history-snapshot');
    const number = (value: unknown) => typeof value === 'number' ? String(value) : '—';
    for (const one of boundary) {
      const creature = textNode('section', '', 'history-creature'); creature.dataset.side = one.side;
      creature.append(textNode('h4', `${one.label} · ${one.side}`),
        textNode('p', `${number(one.health)}/${number(one.maxHealth)} HP · ${number(one.energy)} energy`),
        textNode('p', `${number(one.defense)} defense · ${number(one.initiative)} initiative`),
        textNode('p', [one.defeated ? 'Defeated' : '', ...one.conditions].filter(Boolean).join(' / ') || 'No recorded conditions'));
      board.append(creature);
    }
    detail.append(board);
  }

  function render(model: Model) {
    active = model.available;
    launchers.forEach(button => { button.disabled = !active; });
    if (!active) {
      if (dialog.open) dialog.close();
      entries.clear(); list.replaceChildren(); detail.replaceChildren(); reviewKey = ''; el('history-review').hidden = true; filters(model); key = ''; opener = null;
      el('history-preview').textContent = 'No match loaded'; el('history-new').hidden = true;
      el('history-open').removeAttribute('title'); el('history-unread').textContent = ''; el('history-unread').dataset.count = ''; el('history-load-error').textContent = '';
      el('history-range').textContent = ''; el('history-empty').textContent = ''; el('history-limit').textContent = '';
      return;
    }
    filters(model); review(model);
    el<HTMLButtonElement>('history-earlier').disabled = model.loading || model.oldest || model.waiting;
    el('history-earlier').textContent = model.loading ? 'Loading…' : model.oldest ? 'Beginning of available history' : 'Load earlier rounds';
    el('history-load-error').textContent = model.error;
    el('history-latest').textContent = model.browsing || model.review ? `Return to latest${model.unread ? ` (${model.unread} new)` : ''}` : 'Latest';
    el('history-unread').textContent = model.unread ? ` · ${model.unread} new` : ''; el('history-unread').dataset.count = model.unread ? String(model.unread) : '';
    el('history-new').hidden = model.unread === 0 || model.browsing || Boolean(model.review);
    el('history-latest').hidden = model.unread > 0 && !model.browsing && !model.review;
    el('history-new').textContent = `${model.unread} new ${model.unread === 1 ? 'event' : 'events'} · Latest`;
    const rows: Row[] = model.rows, latest = model.latest;
    el('history-preview').textContent = latest?.summary ?? (model.waiting ? 'Waiting for players' : 'No events yet');
    el('history-open').title = latest?.summary ?? 'Open match history';
    const loaded = model.all.map(row => row.round);
    const first = Math.min(...loaded), last = Math.max(...loaded);
    const rounds = loaded.length ? first === last ? `Loaded round ${first}` : `Loaded rounds ${first}–${last}` : 'No loaded events';
    el('history-range').textContent = model.waiting ? 'Waiting for players' : `${rounds} · ${rows.length} ${rows.length === 1 ? 'event' : 'events'}`;
    el('history-empty').hidden = rows.length > 0;
    el('history-empty').textContent = model.waiting ? 'The match begins when everyone is seated.' : model.all.length ? 'No events match these filters. Choose another filter or Latest.' : 'No public events received for these rounds yet.';
    el('history-limit').textContent = [model.omitted ? 'Older entries are not included in this recent history.' : 'Up to 200 events per page. Load earlier rounds for more.',
      model.unsupported ? 'Some event types have no journal presentation.' : '', 'Reading history does not change the battlefield.'].filter(Boolean).join(' ');
    const nextKey = rows.map(row => row.sequence).join('/');
    if (nextKey === key) return;
    const following = list.scrollHeight - list.scrollTop - list.clientHeight < 40;
    const scroll = list.scrollTop;
    const ids = new Set(rows.map(row => row.sequence));
    const focused = list.contains(document.activeElement);
    for (const [id, node] of entries) if (!ids.has(id)) { node.remove(); entries.delete(id); }
    // Existing nodes are untouched: open details, keyboard focus and deliberate scroll survive polling.
    rows.forEach((row, index) => {
      let node = entries.get(row.sequence);
      if (!node) { node = entryNode(row); entries.set(row.sequence, node); list.insertBefore(node, list.children[index] ?? null); }
    });
    if (focused && !list.contains(document.activeElement)) el('history-close').focus({ preventScroll: true });
    if (dialog.open && following && !model.loading && !model.browsing && !model.review && !Object.values(model.filters).some(Boolean)) {
      list.scrollTop = list.scrollHeight; controller.read(); el('history-new').hidden = true; el('history-latest').hidden = false; el('history-unread').textContent = ''; el('history-unread').dataset.count = '';
    } else list.scrollTop = scroll;
    key = nextKey;

  }
  function openEntry(row: Model['all'][number], source: HTMLElement) {
    if (!active) return;
    // Return to the creature reference when the event closes. Keep the normalized receipt
    // even after its recent journal window expires.
    opener = source; beforeOpen(); controller.open(); controller.inspect(row);
    if (!dialog.open) dialog.showModal();
    el('history-back').focus({ preventScroll: true }); onVisibility();
  }
  return { render, openEntry };
}
