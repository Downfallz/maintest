import { battleOrder, createBattleOrderReveal, orderReason } from '../../table/battle-order.js';

type Model = ReturnType<typeof battleOrder>;
type Row = Model['rows'][number];
type Reason = NonNullable<ReturnType<typeof orderReason>>;
const el = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
function text(tag: string, value: string, className = '') {
  const node = document.createElement(tag); node.textContent = value; node.className = className; return node;
}
const initiative = (row: Row) => Number.isFinite(row.initiative) ? `Initiative ${row.initiative}` : 'Initiative not recorded';

export function orderControls(reveal: ReturnType<typeof createBattleOrderReveal>, beforeOpen: () => void,
  inspect: (id: string) => void, onVisibility: () => void) {
  const dialog = el<HTMLDialogElement>('order-dialog');
  let key = '', opener: HTMLElement | null = null, available = false, opening = false;
  // The creature the strip lights on the field, with why it holds its place: a tap lights it, a second tap, the
  // card's close or Escape puts it out, and a creature no longer in the order is let go of.
  let focused: string | null = null, why = '';
  function setFocus(id: string | null) {
    if (focused === id) return;
    focused = id; key = ''; onVisibility();
  }
  el('order-why-close').addEventListener('click', () => {
    const was = focused; setFocus(null);
    if (was) el('timeline').querySelector<HTMLButtonElement>(`[data-order-creature="${was}"]`)?.focus({ preventScroll: true });
  });
  el('order-why-inspect').addEventListener('click', () => { if (focused) inspect(focused); });
  document.addEventListener('keydown', event => {
    if (event.key === 'Escape' && focused && !document.querySelector('dialog:modal')) { event.preventDefault(); setFocus(null); }
  });
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
    // A reference opened by hand is the order read: closing it lets the seen mark pass it. Opened for its details
    // from the field's reading, the order is still read only at Continue.
    if (!reading) reveal.read();
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
  // A strip entry lights its creature on the field rather than opening its sheet: the card it opens offers the sheet.
  function stripButton(row: Row) {
    const button = document.createElement('button'); button.type = 'button'; button.dataset.orderCreature = row.id;
    button.disabled = !row.inspectable;
    button.setAttribute('aria-pressed', String(focused === row.id));
    button.setAttribute('aria-label', ['Highlight ' + row.label, row.team, `position ${row.position}`, row.tempo, initiative(row), row.roll, row.state, row.condition].filter(Boolean).join(' · '));
    button.addEventListener('click', () => setFocus(focused === row.id ? null : row.id));
    return button;
  }
  function draw(model: Model) {
    el('timeline').replaceChildren(...model.rows.map((row: Row) => {
      const item = document.createElement('li'); item.dataset.state = row.state;
      if (row.team === 'Opponent') item.className = 'enemy';
      if (row.state === 'Now') item.setAttribute('aria-current', 'step');
      if (focused === row.id) item.dataset.focused = 'true';
      // The place is a badge in the tempo's colour, a tick once played, so the number never reads as part of the name.
      const button = stripButton(row), rank = text('span', row.state === 'Completed' ? '✓' : String(row.position), 'timeline-rank');
      rank.dataset.tempo = row.speed; rank.setAttribute('aria-hidden', 'true');
      const words = text('span', '', 'timeline-words');
      // Initiative on every entry, and a die on those a roll-off placed: the order reads at a glance.
      const side = text('span', `${row.team === 'Your team' ? 'You' : row.team} · ${row.tempo}`, 'timeline-side');
      if (Number.isFinite(row.initiative)) side.append(text('span', `⚡${row.initiative}`, 'timeline-init'));
      if (row.roll) side.append(text('span', 'd20', 'timeline-die'));
      words.append(text('span', row.label, 'timeline-name'), side);
      button.append(rank, words);
      item.append(button); return item;
    }));
    el('timeline').hidden = model.rows.length === 0; el('no-timeline').hidden = model.rows.length > 0;
    el('no-timeline').textContent = available ? 'Set once both teams have picked their tempos' : 'Set once the match begins';
    el('order-title').textContent = `Round ${model.round ?? '—'}`;
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
      item.dataset.speed = row.speed;
      if (row.speed !== previousSpeed) { const band = text('h3', row.tempo, 'order-band'); band.dataset.tempo = row.speed; item.append(band); }
      previousSpeed = row.speed;
      // A card per creature: its place as the tempo's badge, its name, where it stands in the round, and its facts as chips.
      const heading = document.createElement('div'); heading.className = 'order-heading';
      const button = creatureButton(row); button.textContent = row.label;
      const position = text('span', row.state === 'Completed' ? '✓' : String(row.position), 'order-position'); position.dataset.tempo = row.speed;
      const state = text('span', row.state, 'order-state'); state.dataset.value = row.state;
      heading.append(position, button, state);
      const facts = text('p', '', 'order-facts');
      facts.append(...[[row.team, 'order-team'], [initiative(row), 'order-initiative'], [row.condition, 'order-condition']]
        .filter(([value]) => value).map(([value, className]) => text('span', value as string, `order-fact ${className}`)));
      const card = text('div', '', 'order-card');
      card.append(heading, facts, text('p', row.roll || 'No d20 roll recorded', row.roll ? 'order-roll' : 'order-roll order-roll-none'));
      item.append(card); return item;
    }));
  }
  let shownTurn = '';
  // A newly revealed order is read on the field (a numbered badge on each creature) and in the dock's card,
  // never in a dialog over the field; the full order, rules and rolls stay one tap away. Everything waits for
  // Continue, an opponent's action included.
  let reading = false;
  function finish() { if (!reading) return; reading = false; reveal.read(); onVisibility(); }
  // Why the lit creature holds its place: its rank, its tempo band, its initiative against its neighbours', and the
  // tie that placed it, with the d20s a roll-off threw.
  function drawWhy(reason: Reason | null) {
    const card = el('order-why');
    card.hidden = !reason;
    if (!reason) return;
    card.dataset.team = reason.team === 'Your team' ? 'ally' : 'enemy';
    const rank = el('order-why-rank'); rank.textContent = String(reason.position); rank.dataset.tempo = reason.speed;
    el('order-why-name').textContent = reason.label;
    el('order-why-meta').textContent = `${reason.team} · ${reason.position} of ${reason.total}`;
    el('order-why-lines').replaceChildren(...reason.lines.map(line => text('li', line)));
    el('order-why-dice').hidden = reason.dice.length === 0;
    el('order-why-dice').replaceChildren(...reason.dice.map(die => {
      const chip = text('span', '', `order-why-die${die.self ? ' is-self' : ''}`); chip.dataset.team = die.team === 'Your team' ? 'ally' : 'enemy';
      const face = text('span', String(die.rolls.at(-1)), 'order-why-face'); face.setAttribute('aria-hidden', 'true');
      const roll = text('span', die.label, 'order-why-roll'); roll.append(text('span', die.rolls.join(' → '), 'order-why-throws'));
      chip.setAttribute('aria-label', `${die.label}: d20 ${die.rolls.join(' then ')}`);
      chip.append(face, roll);
      return chip;
    }));
  }
  // On a narrow field the card takes the side of the field with more room beside the lit creature, and no more of it
  // than that room, so it never covers the creature it explains; the field's layout moves with the scene, so this is
  // asked again whenever the scene lays its creatures out. A wide field keeps the card on the far side by team.
  function place() {
    const card = el('order-why');
    const lit = focused ? document.querySelector<HTMLElement>(`#actor-layer [data-creature="${focused}"]`) : null;
    const zone = card.offsetParent as HTMLElement | null;
    if (card.hidden || !lit || !zone || !matchMedia('(max-width: 619px)').matches) { delete card.dataset.place; card.style.removeProperty('max-height'); return; }
    const field = zone.getBoundingClientRect(), creature = lit.getBoundingClientRect();
    const above = creature.top - field.top, below = field.bottom - creature.bottom;
    card.dataset.place = above > below ? 'top' : 'bottom';
    card.style.maxHeight = `${Math.max(120, Math.floor(Math.max(above, below)) - 16)}px`;
  }
  // The lit creature is let go of once it leaves the order; the card is drawn again only when its reason changes.
  function refreshWhy(model: Model, enabled: boolean, board: unknown) {
    if (focused && (!enabled || !model.rows.some((row: Row) => row.id === focused))) focused = null;
    const reason = focused ? orderReason(board, focused) : null;
    const nextWhy = JSON.stringify(reason);
    if (nextWhy !== why) { why = nextWhy; drawWhy(reason); }
    place();
  }
  function render(model: Model, enabled: boolean, canAuto: boolean, board: unknown = null) {
    available = enabled; openers.forEach(button => { button.disabled = !enabled; });
    if (!enabled) { opener = null; reading = false; if (dialog.open) dialog.close(); el<HTMLDetailsElement>('order-rules').open = false; }
    refreshWhy(model, enabled, board);
    const next = JSON.stringify([model, enabled, focused]);
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
    if (canAuto && reveal.model().pending && !reading) { reveal.presented(); reading = true; onVisibility(); }
  }
  return { render, isOpening: () => opening, isReading: () => reading, finish, details: () => show(true), focused: () => focused, place };
}
