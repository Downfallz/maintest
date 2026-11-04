import { createArenaHistory } from '../../table/arena-history.js';

type Model = ReturnType<ReturnType<typeof createArenaHistory>['model']>;
type Row = Model['rows'][number];
const el = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
function textNode(tag: string, text: string, className = '') {
  const node = document.createElement(tag); node.textContent = text; node.className = className; return node;
}

export function historyControls(beforeOpen: () => void, onVisibility: () => void) {
  const dialog = el<HTMLDialogElement>('history-dialog'), list = el('history-list');
  const entries = new Map<number, HTMLDetailsElement>();
  let opener: HTMLElement | null = null, active = false, key = '', lastSequence: number | null = null, unread = 0;
  const launchers = Array.from(document.querySelectorAll<HTMLButtonElement>('[data-history-open]'));
  const close = () => dialog.close();
  function open(event: Event) {
    if (!active || dialog.open) return;
    opener = event.currentTarget as HTMLElement;
    beforeOpen(); dialog.showModal(); unread = 0; el('history-new').hidden = true;
    // Initial opening follows the latest entry; incoming entries never move a reader browsing above it.
    list.scrollTop = list.scrollHeight; onVisibility();
  }
  launchers.forEach(button => button.addEventListener('click', open));
  el('history-close').addEventListener('click', close);
  dialog.addEventListener('cancel', event => { event.preventDefault(); close(); });
  dialog.addEventListener('close', () => {
    onVisibility();
    if (active && opener?.isConnected && !opener.matches(':disabled') && opener.getClientRects().length) opener.focus({ preventScroll: true });
    opener = null;
  });
  el('history-new').addEventListener('click', () => { list.scrollTop = list.scrollHeight; unread = 0; el('history-new').hidden = true; });

  function entryNode(row: Row) {
    const node = document.createElement('details'); node.className = 'history-entry'; node.dataset.sequence = String(row.sequence);
    const summary = document.createElement('summary');
    summary.append(textNode('span', `Round ${row.round || '—'} · ${row.category}`, 'history-meta'),
      textNode('strong', row.title), ...(row.status ? [textNode('span', row.status, 'history-status')] : []));
    const details = document.createElement('ul'); details.className = 'history-effects';
    for (const effect of row.details) {
      const item = textNode('li', effect.text); item.dataset.tone = effect.tone; details.append(item);
    }
    if (!row.details.length) details.append(textNode('li', row.summary));
    node.append(summary, details); return node;
  }

  function render(model: Model) {
    active = model.available;
    launchers.forEach(button => { button.disabled = !active; });
    if (!active) {
      if (dialog.open) dialog.close();
      entries.clear(); list.replaceChildren(); key = ''; lastSequence = null; unread = 0; opener = null;
      el('history-preview').textContent = 'No match loaded'; el('history-new').hidden = true;
      el('history-open').removeAttribute('title');
      el('history-range').textContent = ''; el('history-empty').textContent = ''; el('history-limit').textContent = '';
      return;
    }
    const rows = model.rows, latest = rows.at(-1);
    el('history-preview').textContent = latest?.summary ?? (model.waiting ? 'Waiting for players' : 'No events yet');
    el('history-open').title = latest?.summary ?? 'Open match history';
    const rounds = model.current > 1 ? `Rounds ${model.current - 1}–${model.current}` : `Round ${model.current || '—'}`;
    el('history-range').textContent = model.waiting ? 'Waiting for players' : `${rounds} · ${rows.length} ${rows.length === 1 ? 'event' : 'events'}`;
    el('history-empty').hidden = rows.length > 0;
    el('history-empty').textContent = model.waiting ? 'The match begins when everyone is seated.' : 'No public events received for these rounds yet.';
    el('history-limit').textContent = [model.omitted ? 'Older entries are not included in this recent history.' : 'Current and previous round · up to 200 events.',
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
    if (dialog.open && lastSequence !== null) unread += rows.filter(row => row.sequence > lastSequence!).length;
    if (following) { list.scrollTop = list.scrollHeight; unread = 0; } else list.scrollTop = scroll;
    el('history-new').hidden = unread === 0;
    el('history-new').textContent = `${unread} new ${unread === 1 ? 'event' : 'events'} · Latest`;
    lastSequence = latest?.sequence ?? lastSequence; key = nextKey;
  }
  return { render };
}
