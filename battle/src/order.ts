import { battleOrder, createBattleOrderReveal } from '../../table/battle-order.js';

type Model = ReturnType<typeof battleOrder>;
type Row = Model['rows'][number];
const el = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
function text(tag: string, value: string, className = '') {
  const node = document.createElement(tag); node.textContent = value; node.className = className; return node;
}
const initiative = (row: Row) => Number.isFinite(row.initiative) ? `Initiative ${row.initiative}` : 'Initiative not recorded';

export function orderControls(reveal: ReturnType<typeof createBattleOrderReveal>, beforeOpen: () => void,
  inspect: (id: string) => void, onVisibility: () => void) {
  const dialog = el<HTMLDialogElement>('order-dialog');
  let key = '', opener: HTMLElement | null = null, available = false, opening = false;
  // Skipping playback renders at once: while Order opens, that render must not open Order or a tip itself.
  function show(automatic = false) {
    if (!available || dialog.open || opening) return;
    opener = document.activeElement as HTMLElement;
    opening = true;
    try { if (!automatic) beforeOpen(); } finally { opening = false; }
    if (dialog.open) return;
    reveal.presented(); dialog.showModal(); onVisibility();
  }
  const openers = Array.from(document.querySelectorAll<HTMLButtonElement>('[data-order-open]'));
  openers.forEach(button => button.addEventListener('click', () => show()));
  el('order-close').addEventListener('click', () => dialog.close());
  dialog.addEventListener('close', () => {
    if (available && opener?.isConnected && !opener.matches(':disabled') && opener.getClientRects().length) opener.focus({ preventScroll: true });
    opener = null; onVisibility();
  });
  function creatureButton(row: Row) {
    const button = document.createElement('button'); button.type = 'button'; button.dataset.orderCreature = row.id;
    button.disabled = !row.inspectable;
    button.setAttribute('aria-label', ['Inspect ' + row.label, row.team, `position ${row.position}`, row.tempo, initiative(row), row.state, row.condition].filter(Boolean).join(' · '));
    button.addEventListener('click', () => inspect(row.id));
    return button;
  }
  function draw(model: Model) {
    el('timeline').replaceChildren(...model.rows.map((row: Row) => {
      const item = document.createElement('li'); item.dataset.state = row.state;
      if (row.team === 'Opponent') item.className = 'enemy';
      if (row.state === 'Now') item.setAttribute('aria-current', 'step');
      const button = creatureButton(row);
      button.append(text('span', `${row.state === 'Completed' ? '✓ ' : ''}${row.position} ${row.label}`),
        text('span', `${row.team === 'Your team' ? 'You' : row.team} · ${row.tempo}`, 'timeline-side'));
      item.append(button); return item;
    }));
    el('timeline').hidden = model.rows.length === 0; el('no-timeline').hidden = model.rows.length > 0;
    el('no-timeline').textContent = available ? 'Activation order has not been set' : 'Waiting for a board';
    el('order-title').textContent = `Round ${model.round ?? '—'} · Turn order`;
    el('order-summary').textContent = model.rows.length ? model.summary.join(' / ') : model.empty;
    el('order-context').textContent = model.provisional ? 'Team positions fixed · creature order still being chosen' : 'Exact order · recorded dice';
    el('order-explanation').textContent = model.provisional
      ? 'Provisional creature order. The team positions are fixed; tied creatures within each team can still be reordered. Both choices reveal together.'
      : 'Instinct acts before Precision; initiative ranks each band. Cross-team ties use d20 rolls, with rerolls when opposing rolls match. Each team can order its own creatures within its allocated places.';
    el('order-roll-note').textContent = model.hasRolls
      ? 'These are the actual recorded rolls. They determined team positions; a later same-team choice can move a creature within those positions.'
      : 'No d20 rolls recorded for this order. Same-team ties do not need a roll.';
    let previousSpeed: string | null = null;
    el('order-list').replaceChildren(...model.rows.map((row: Row) => {
      const item = document.createElement('li'); item.className = 'order-row'; item.dataset.state = row.state; item.dataset.team = row.team;
      if (row.state === 'Now') item.setAttribute('aria-current', 'step');
      if (row.speed !== previousSpeed) item.append(text('h3', row.tempo, 'order-band'));
      previousSpeed = row.speed;
      const heading = document.createElement('div'); heading.className = 'order-heading';
      const button = creatureButton(row); button.textContent = row.label;
      heading.append(text('span', String(row.position), 'order-position'), button, text('span', row.state, 'order-state'));
      item.append(heading, text('p', [row.team, row.tempo, initiative(row), row.condition].filter(Boolean).join(' · ')),
        text('p', row.roll || 'No d20 roll recorded', 'order-roll')); return item;
    }));
  }
  let shownTurn = '';
  function render(model: Model, enabled: boolean, canAuto: boolean) {
    available = enabled; openers.forEach(button => { button.disabled = !enabled; });
    if (!enabled) { opener = null; if (dialog.open) dialog.close(); el<HTMLDetailsElement>('order-rules').open = false; }
    const next = JSON.stringify([model, enabled]);
    if (key !== next) {
      const active = document.activeElement as HTMLElement | null;
      const focus = active?.dataset.orderCreature;
      const parent = active?.closest('#timeline') ? 'timeline' : 'order-list';
      const scroll = el('order-body').scrollTop;
      draw(model); key = next; el('order-body').scrollTop = scroll;
      // The creature acting is brought into the strip's view when the turn moves, never while the player scrolls it.
      const current = el('timeline').querySelector<HTMLElement>('[aria-current="step"]');
      const turn = current?.querySelector<HTMLElement>('[data-order-creature]')?.dataset.orderCreature ?? '';
      if (current && turn !== shownTurn) {
        const strip = el('timeline');
        strip.scrollLeft = Math.max(0, current.offsetLeft - (strip.clientWidth - current.offsetWidth) / 2);
      }
      shownTurn = turn;
      if (focus) el(parent).querySelector<HTMLButtonElement>(`[data-order-creature="${focus}"]`)?.focus({ preventScroll: true });
    }
    if (canAuto && reveal.model().pending) show(true);
  }
  return { render, isOpening: () => opening };
}
