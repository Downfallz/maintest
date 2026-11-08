import { createBattleHistory, historyEntry, historySummary, KIND_LABELS } from '../../table/battle-history.js';

type Model = ReturnType<ReturnType<typeof createBattleHistory>['model']>;
type Row = NonNullable<ReturnType<typeof historyEntry>> & { status?: string };
const el = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
function textNode(tag: string, text: string, className = '') {
  const node = document.createElement(tag); node.textContent = text; node.className = className; return node;
}

// The journal opens on its summary, the main moments of each round; All events is the full record. A reader's
// choice is remembered on this device. An inspected event is always read in the full record.
const VIEW_KEY = 'downfall.battle.history-view.v1';
type View = 'summary' | 'events';
function storedView(): View {
  try { return localStorage.getItem(VIEW_KEY) === 'events' ? 'events' : 'summary'; } catch { return 'summary'; }
}

export function historyControls(controller: ReturnType<typeof createBattleHistory>, beforeOpen: () => void, onVisibility: () => void) {
  const dialog = el<HTMLDialogElement>('history-dialog'), list = el('history-list'), digest = el('history-summary');
  const entries = new Map<number, HTMLElement>();
  let opener: HTMLElement | null = null, active = false, key = '', reviewKey = '', summaryKey = '';
  let view: View = storedView(), current: Model | null = null;
  function choose(next: View) {
    view = next;
    try { localStorage.setItem(VIEW_KEY, next); } catch { /* The choice lasts for this page only. */ }
    if (current) render(current);
  }
  el('history-mode-summary').addEventListener('click', () => choose('summary'));
  el('history-mode-events').addEventListener('click', () => choose('events'));
  const detail = el('history-detail');
  const launchers = Array.from(document.querySelectorAll<HTMLButtonElement>('[data-history-open]'));
  const close = () => dialog.close();
  function open(event: Event) {
    if (!active || dialog.open) return;
    opener = event.currentTarget as HTMLElement;
    beforeOpen(); controller.open(); dialog.showModal(); el('history-new').hidden = true;
    // Initial opening follows the latest entry; incoming entries never move a reader browsing above it.
    list.scrollTop = list.scrollHeight; digest.scrollTop = 0; onVisibility();
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
    const row = inspected == null ? null : view === 'summary' ? digest.querySelector<HTMLElement>(`[data-sequence="${inspected}"]`) : entries.get(inspected)?.querySelector<HTMLElement>('summary');
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

  type Round = ReturnType<typeof historySummary>[number];
  const SHORT_STATUS: Record<string, string> = { Critical: 'Crit', Fizzled: 'Fizzled' };
  const EFFECTS_SHOWN = 4;
  function roundCard(one: Round) {
    const card = textNode('article', '', 'summary-round'); card.dataset.round = String(one.round);
    const head = textNode('header', '', 'summary-head');
    const counts = [one.casts.length ? `${one.casts.length} ${one.casts.length === 1 ? 'spell' : 'spells'}` : '', one.defeated.length ? `${one.defeated.length} defeated` : ''].filter(Boolean).join(' · ');
    const title = textNode('div', ''); title.append(textNode('h3', `Round ${one.round}`), ...(counts ? [textNode('span', counts, 'summary-counts')] : []));
    const more = textNode('button', 'All events', 'summary-more'); (more as HTMLButtonElement).type = 'button';
    more.setAttribute('aria-label', `All events of round ${one.round}`);
    more.addEventListener('click', () => { choose('events'); controller.filter('round', String(one.round)); el('history-close').focus({ preventScroll: true }); });
    head.append(title, more); card.append(head);
    if (one.result) card.append(textNode('p', one.result, 'summary-result'));
    if (one.mutations.length) {
      const mutations = textNode('ul', '', 'summary-mutations'); mutations.setAttribute('aria-label', 'Mutations revealed');
      for (const pick of one.mutations) {
        const item = textNode('li', ''); item.dataset.side = pick.side;
        item.append(textNode('span', pick.creature, 'summary-who'), textNode('span', pick.mutation || 'Mutation', 'summary-what'));
        mutations.append(item);
      }
      card.append(mutations);
    }
    if (one.casts.length) {
      const casts = textNode('ol', '', 'summary-casts'); casts.setAttribute('aria-label', 'Spells in order');
      for (const cast of one.casts) {
        const item = textNode('li', ''); item.dataset.side = cast.side; item.dataset.status = cast.status;
        // A cast opens its own record: the full facts and the battlefield before and after it.
        const line = textNode('button', '', 'summary-cast') as HTMLButtonElement; line.type = 'button'; line.dataset.sequence = String(cast.sequence);
        line.setAttribute('aria-label', `${cast.caster} cast ${cast.spell}${SHORT_STATUS[cast.status] ? `, ${cast.status.toLowerCase()}` : ''}. Inspect event`);
        line.append(textNode('span', cast.caster, 'summary-who'), textNode('span', cast.spell, 'summary-what'),
          ...(SHORT_STATUS[cast.status] ? [textNode('span', SHORT_STATUS[cast.status], 'summary-status')] : []));
        line.addEventListener('click', () => {
          // The summary lists every loaded round whatever All events filters; a filter that excludes this cast would
          // leave its inspection with nothing to select, so the filters are cleared first.
          for (const [name, value] of Object.entries(controller.model().filters)) if (value) controller.filter(name, '');
          controller.select(cast.sequence); el('history-back').focus({ preventScroll: true });
        });
        item.append(line);
        if (cast.effects.length) {
          const effects = textNode('ul', '', 'summary-effects');
          for (const effect of cast.effects.slice(0, EFFECTS_SHOWN)) { const chip = textNode('li', effect.text); chip.dataset.tone = effect.tone; effects.append(chip); }
          if (cast.effects.length > EFFECTS_SHOWN) effects.append(textNode('li', `+${cast.effects.length - EFFECTS_SHOWN} more`, 'summary-extra'));
          item.append(effects);
        }
        casts.append(item);
      }
      card.append(casts);
    }
    if (one.defeated.length) {
      const fallen = textNode('ul', '', 'summary-defeated'); fallen.setAttribute('aria-label', 'Defeated');
      for (const down of one.defeated) { const item = textNode('li', `${down.creature} defeated`); item.dataset.side = down.side; fallen.append(item); }
      card.append(fallen);
    }
    if (one.skipped.length) card.append(textNode('p', one.skipped.join(' · '), 'summary-skipped'));
    return card;
  }
  function summary(model: Model) {
    const nextKey = model.all.map(row => row.sequence).join('/');
    if (nextKey === summaryKey) return;
    summaryKey = nextKey;
    const rounds = historySummary(model.all);
    digest.replaceChildren(...(rounds.length ? rounds.map(roundCard)
      : [textNode('p', model.waiting ? 'The match begins when everyone is seated.' : 'No spells or mutations yet. All events lists every phase.', 'summary-empty')]));
  }
  function views(model: Model) {
    const shown: View = model.review ? 'events' : view;
    dialog.dataset.view = shown;
    digest.hidden = shown !== 'summary'; list.hidden = shown !== 'events';
    el('history-filters').hidden = shown !== 'events';
    el('history-mode-summary').setAttribute('aria-pressed', String(shown === 'summary'));
    el('history-mode-events').setAttribute('aria-pressed', String(shown === 'events'));
    return shown;
  }

  function render(model: Model) {
    current = model;
    active = model.available;
    launchers.forEach(button => { button.disabled = !active; });
    if (!active) {
      if (dialog.open) dialog.close();
      entries.clear(); list.replaceChildren(); detail.replaceChildren(); reviewKey = ''; el('history-review').hidden = true; filters(model); key = ''; opener = null;
      el('history-preview').textContent = 'No match loaded'; el('history-new').hidden = true;
      el('history-open').removeAttribute('title'); el('history-unread').textContent = ''; el('history-unread').dataset.count = ''; el('history-load-error').textContent = '';
      el('history-range').textContent = ''; el('history-empty').textContent = ''; el('history-limit').textContent = '';
      digest.replaceChildren(); summaryKey = ''; views(model);
      return;
    }
    filters(model); review(model);
    const shown = views(model);
    if (shown === 'summary') summary(model);
    el<HTMLButtonElement>('history-earlier').disabled = model.loading || model.oldest || model.waiting;
    el('history-earlier').textContent = model.loading ? 'Loading…' : model.oldest ? 'Beginning of available history' : 'Load earlier rounds';
    el('history-load-error').textContent = model.error;
    el('history-latest').textContent = model.browsing || model.review ? `Return to latest${model.unread ? ` (${model.unread} new)` : ''}` : 'Latest';
    el('history-unread').textContent = model.unread ? ` · ${model.unread} new` : ''; el('history-unread').dataset.count = model.unread ? String(model.unread) : '';
    el('history-new').hidden = model.unread === 0 || model.browsing || Boolean(model.review);
    el('history-latest').hidden = model.unread > 0 && !model.browsing && !model.review;
    el('history-new').textContent = `${model.unread} new ${model.unread === 1 ? 'event' : 'events'} · Latest`;
    // The summary shows every loaded round at once, the latest first: nothing waits below for the reader.
    if (shown === 'summary' && dialog.open && model.unread) {
      controller.read(); el('history-unread').textContent = ''; el('history-unread').dataset.count = '';
      el('history-new').hidden = true; el('history-latest').hidden = false;
    }
    const rows: Row[] = model.rows, latest = model.latest;
    el('history-preview').textContent = latest?.summary ?? (model.waiting ? 'Waiting for players' : 'No events yet');
    el('history-open').title = latest?.summary ?? 'Open match history';
    const loaded = model.all.map(row => row.round);
    const first = Math.min(...loaded), last = Math.max(...loaded);
    const rounds = loaded.length ? first === last ? `Loaded round ${first}` : `Loaded rounds ${first}–${last}` : 'No loaded events';
    el('history-range').textContent = model.waiting ? 'Waiting for players' : `${rounds} · ${rows.length} ${rows.length === 1 ? 'event' : 'events'}`;
    el('history-empty').hidden = rows.length > 0 || shown === 'summary';
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
